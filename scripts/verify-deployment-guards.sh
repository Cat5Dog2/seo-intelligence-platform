#!/usr/bin/env bash
# Regression test for the guards in verify-production-compose.sh.
#
#   bash scripts/verify-deployment-guards.sh
#
# Those guards are the only thing standing between a careless edit and a deployment that runs an
# unscanned image, skips the migration build, or starts before the scan. A guard that silently
# stops catching its case is worse than no guard, because it still reads as protection. This
# asserts each one fails on the tampering it exists to catch, and fails for the stated reason -
# an unrelated error would otherwise look like the guard working. Everything runs on copies, so
# the repository is never modified.
set -euo pipefail

cd "$(dirname "$0")/.."

# Unique per run: a fixed path would delete another run's files and two runs would corrupt each
# other's fixtures. Kept under artifacts/ rather than /tmp because the verifier hands the lock path
# to Python, and on Git Bash for Windows that is the Windows build, which cannot open an MSYS path.
work="artifacts/guard-test-$$-${RANDOM}"
mkdir -p "$work/bin" "$work/lock"
chmod 700 "$work/lock"

# The deployment requires flock so two runs cannot overlap. Git Bash on Windows does not ship it,
# and the ordering tests below are about what runs in what order, not about locking - so they get a
# stub that always acquires. The real mutual-exclusion check further down uses the real flock and
# skips where there is none.
cat > "$work/bin/flock" <<'FLOCK'
#!/usr/bin/env bash
exit 0
FLOCK
chmod +x "$work/bin/flock"
trap 'rm -rf "$work"' EXIT

failures=0

# Runs the verifier against tampered copies and requires it to fail with the expected reason.
# DEPLOYMENT_DOC, DIGEST_LOCK_FILE and DEPLOY_SCRIPT default to the real files, so each case only
# overrides the one it tampers with.
expect_failure() {
  local description="$1" expected_message="$2"
  shift 2

  local output status=0
  output="$(env "$@" bash scripts/verify-production-compose.sh 2>&1)" || status=$?

  if [[ "$status" -eq 0 ]]; then
    echo "FAIL: the verifier passed despite $description." >&2
    failures=$((failures + 1))
    return
  fi

  if ! grep -qF -- "$expected_message" <<< "$output"; then
    echo "FAIL: $description was rejected, but not for the expected reason." >&2
    echo "      expected to contain: $expected_message" >&2
    echo "      actual output:" >&2
    sed 's/^/        /' <<< "$output" >&2
    failures=$((failures + 1))
    return
  fi

  echo "ok: $description is caught."
}

if ! bash scripts/verify-production-compose.sh > /dev/null 2>&1; then
  echo "FAIL: the verifier does not pass on the unmodified repository." >&2
  exit 1
fi
echo "ok: the unmodified repository passes."

# --- digest lock file ---------------------------------------------------------------------------

# A service dropped from the lock. Deriving the compared set from the lock would remove it from
# both sides and pass.
grep -v $'^redis\t' image-digests.lock > "$work/lock-missing-redis"
expect_failure "a service missing from the digest lock file" \
  "every third-party service this stack runs must be pinned" \
  "DIGEST_LOCK_FILE=$work/lock-missing-redis"

# A digest that no longer matches what Compose deploys.
sed 's/sha256:cf78e766/sha256:00000000/' image-digests.lock > "$work/lock-wrong-digest"
expect_failure "a digest that does not match the rendered image" \
  "the rendered images do not match the digest lock file" \
  "DIGEST_LOCK_FILE=$work/lock-wrong-digest"

# --- deployment script: what it actually runs ----------------------------------------------------

# Verified by running it, not by pattern-matching its source. A source check finds the commands
# inside `build_and_scan` whether or not anything ever calls it; only a trace can tell.
#
# Compose, the scanner and the backup are stubbed, so the assertions are about control flow rather
# than about building images or dumping a database.
make_traceable() {
  local source="$1" destination="$2" backup_command="${3:-echo TRACE backup}"
  sed     -e 's|^COMPOSE=(docker compose .*|COMPOSE=(echo TRACE compose)|'     -e 's|^  APP_SCAN_MANIFEST=.* bash scripts/scan-container-images.sh app$|  echo TRACE scan|'     -e 's|^  pin_scanned_images$|  echo TRACE pin|'     -e 's|^    pin_running_images api worker web$|    echo TRACE pin-running|'     -e 's|^\([[:space:]]*\)assert_running_images .*$|\1echo TRACE verify|'     -e 's|^cd "$(dirname "$0")/\.\."$|cd "$(dirname "$0")/../.."|'     "$source" > "$destination"

  # Same reasoning as the backup stub below: a pattern that stopped matching would leave the real
  # scan and the real image verification running against a real Docker, and every trace case would
  # fail slowly for the wrong reason.
  for pattern in 'scripts/scan-container-images.sh app' 'pin_scanned_images$' '^[[:space:]]*pin_running_images ' '^[[:space:]]*assert_running_images '; do
    if grep -qE "$pattern" "$destination"; then
      echo "FAIL: '$pattern' in $source no longer matches the stub pattern." >&2
      failures=$((failures + 1))
    fi
  done
  # The backup is its own script and shells out to docker; stubbed so the trace stays about order.
  # Only the command is replaced, never the whole line: the deployment sets the backup's target on
  # that line, and a stub that overwrote it would leave a test about what the backup is given
  # measuring what this function wrote instead.
  sed -i "s|bash scripts/backup-production.sh\$|${backup_command}|" "$destination"
  # A stub that never replaced anything would leave the real backup running against a real Docker
  # and turn every trace case into a slow, confusing failure. Fail here instead, where the reason
  # is obvious: the line in deploy-production.sh was edited and this pattern was not.
  if grep -q 'bash scripts/backup-production.sh' "$destination"; then
    echo "FAIL: the backup call in $source no longer matches the stub pattern." >&2
    failures=$((failures + 1))
  fi
}

expect_trace() {
  local description="$1" script="$2" mode="$3" expected="$4"
  local actual status=0

  # The backup directory is named after the current second, which can tick over between building
  # the expectation and running the script. The name is not what this asserts on; its position is.
  # The exit status has to be the script's, not grep's, so the output is captured first.
  local raw
  raw="$(PATH="$PWD/$work/bin:$PATH" PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example bash "$script" "$mode" 2>&1)" || status=$?
  actual="$(grep '^TRACE ' <<< "$raw" | sed -e 's/^TRACE //' || true)"

  if [[ "$status" -ne 0 ]]; then
    echo "FAIL: $description - the script exited $status." >&2
    echo "      A trace that matches but ends in failure is not a working deployment." >&2
    failures=$((failures + 1))
    return
  fi

  if [[ "$actual" != "$expected" ]]; then
    echo "FAIL: $description" >&2
    echo "      expected:" >&2
    sed 's/^/        /' <<< "$expected" >&2
    echo "      actual:" >&2
    sed 's/^/        /' <<< "$actual" >&2
    failures=$((failures + 1))
    return
  fi

  echo "ok: $description"
}

traceable="$work/deploy-traceable"
make_traceable scripts/deploy-production.sh "$traceable"

expect_trace "the first deployment builds and scans before anything starts"   "$traceable" initial   "compose config --quiet
compose build api web worker migrate
scan
pin
compose up -d postgres redis
compose --profile tools run --rm migrate
compose up -d --wait api worker web
verify
compose ps"

expect_trace "an update backs up while stopped and before migrating"   "$traceable" update   "compose config --quiet
compose build api web worker migrate
scan
pin
compose stop web api worker
backup
compose --profile tools run --rm migrate
compose up -d --wait --force-recreate api worker web
verify
compose ps
compose logs --tail 200 web api worker"

expect_trace "an ad-hoc backup restarts the image IDs that were running before it stopped"   "$traceable" backup   "pin-running
compose stop web api worker
backup
compose up -d --wait api worker web
verify
compose ps"

# The image variables are part of the Compose model, so pinning only after the build is too late:
# inherited values would make Compose place the new build under other tags while the scanner keeps
# reading the four production tags. The deployment has to choose the build targets before its first
# Compose call.
cat > "$work/build-image-stub.sh" <<'STUB'
#!/usr/bin/env bash
if [[ "${1:-}" == "build" ]]; then
  printf 'TRACE build-images=%s|%s|%s|%s\n' \
    "${API_IMAGE:-unset}" "${WEB_IMAGE:-unset}" "${WORKER_IMAGE:-unset}" "${MIGRATE_IMAGE:-unset}"
else
  printf 'TRACE compose %s\n' "$*"
fi
STUB
chmod +x "$work/build-image-stub.sh"

build_targets="$work/deploy-build-targets"
make_traceable scripts/deploy-production.sh "$build_targets"
sed -i 's|^COMPOSE=(echo TRACE compose)$|COMPOSE=(bash "$BUILD_IMAGE_STUB")|' "$build_targets"

build_target_output="$(
  PATH="$PWD/$work/bin:$PATH" PRODUCTION_LOCK_DIR="$PWD/$work/lock" \
    ENV_FILE=.env.production.example BUILD_IMAGE_STUB="$PWD/$work/build-image-stub.sh" \
    API_IMAGE=inherited-api WEB_IMAGE=inherited-web WORKER_IMAGE=inherited-worker \
    MIGRATE_IMAGE=inherited-migrate bash "$build_targets" initial 2>&1
)" || true

expected_build_targets='TRACE build-images=seo-intelligence-api|seo-intelligence-web|seo-intelligence-worker|seo-intelligence-migrate'
if ! grep -qF "$expected_build_targets" <<< "$build_target_output"; then
  echo "FAIL: inherited image variables changed the targets produced by the build." >&2
  grep '^TRACE ' <<< "$build_target_output" | sed 's/^/      /' >&2
  failures=$((failures + 1))
else
  echo "ok: the deployment fixes all four build targets before Compose runs."
fi

# A failed backup must not leave the application down. Nothing has changed at that point - no
# migration has run - so the previous version has to come back up.
backup_fails="$work/deploy-backup-fails"
make_traceable scripts/deploy-production.sh "$backup_fails" 'echo TRACE backup; return 4'

backup_output="$(PATH="$PWD/$work/bin:$PATH" PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example   bash "$backup_fails" backup 2>&1)" && backup_status=0 || backup_status=$?

if [[ "$backup_status" -eq 0 ]]; then
  echo "FAIL: the backup mode reported success even though the backup failed." >&2
  failures=$((failures + 1))
elif ! grep -qF "compose up -d --wait api worker web" <<< "$backup_output"; then
  echo "FAIL: a failed ad-hoc backup left the application stopped." >&2
  sed 's/^/      /' <<< "$backup_output" >&2
  failures=$((failures + 1))
else
  echo "ok: a failed ad-hoc backup restarts the services it stopped."
fi

# Backup does not scan, so it must preserve the exact images that were running before stop. This
# exercises the real pinning and post-start verification functions at the Docker process boundary;
# the trace test above only proves that calls exist in the right order.
mkdir -p "$work/backup-image-bin"
cat > "$work/backup-image-bin/docker" <<'DOCKER'
#!/usr/bin/env bash
set -euo pipefail
case "${1:-} ${2:-} ${3:-} ${4:-}" in
  "inspect --format {{.Image}} container-api") printf 'sha256:%064d\n' 1 ;;
  "inspect --format {{.Image}} container-web") printf 'sha256:%064d\n' 2 ;;
  "inspect --format {{.Image}} container-worker") printf 'sha256:%064d\n' 3 ;;
  *) echo "unexpected docker invocation: $*" >&2; exit 91 ;;
esac
DOCKER
chmod +x "$work/backup-image-bin/docker"

cat > "$work/backup-image-compose.sh" <<'COMPOSE'
#!/usr/bin/env bash
set -euo pipefail
case "$*" in
  "ps --quiet api") printf '%s\n' container-api ;;
  "ps --quiet web") printf '%s\n' container-web ;;
  "ps --quiet worker") printf '%s\n' container-worker ;;
  "stop web api worker") printf 'TRACE stopped\n' ;;
  "up -d --wait api worker web")
    printf 'TRACE restart-images=%s|%s|%s\n' "$API_IMAGE" "$WEB_IMAGE" "$WORKER_IMAGE"
    ;;
  "ps") printf 'TRACE status\n' ;;
  *) echo "unexpected compose invocation: $*" >&2; exit 92 ;;
esac
COMPOSE
chmod +x "$work/backup-image-compose.sh"

cat > "$work/backup-result.sh" <<'BACKUP'
#!/usr/bin/env bash
exit "${BACKUP_RESULT:-0}"
BACKUP
chmod +x "$work/backup-result.sh"

backup_images="$work/deploy-backup-images"
sed \
  -e 's|^COMPOSE=(docker compose .*|COMPOSE=(bash "$BACKUP_IMAGE_COMPOSE")|' \
  -e 's|bash scripts/backup-production.sh$|bash "$BACKUP_RESULT_STUB"|' \
  -e 's|^cd "$(dirname "$0")/\.\."$|cd "$(dirname "$0")/../.."|' \
  scripts/deploy-production.sh > "$backup_images"

expected_restart="TRACE restart-images=sha256:$(printf '%064d' 1)|sha256:$(printf '%064d' 2)|sha256:$(printf '%064d' 3)"
for backup_result in 0 4; do
  backup_image_status=0
  backup_image_output="$(
    PATH="$PWD/$work/backup-image-bin:$PWD/$work/bin:$PATH" \
      PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example \
      BACKUP_IMAGE_COMPOSE="$PWD/$work/backup-image-compose.sh" \
      BACKUP_RESULT_STUB="$PWD/$work/backup-result.sh" BACKUP_RESULT="$backup_result" \
      API_IMAGE=inherited-api WEB_IMAGE=inherited-web WORKER_IMAGE=inherited-worker \
      bash "$backup_images" backup 2>&1
  )" || backup_image_status=$?

  if [[ "$backup_result" -eq 0 && "$backup_image_status" -ne 0 ]] ||
     [[ "$backup_result" -ne 0 && "$backup_image_status" -eq 0 ]]; then
    echo "FAIL: backup result $backup_result produced exit $backup_image_status." >&2
    sed 's/^/      /' <<< "$backup_image_output" >&2
    failures=$((failures + 1))
  elif ! grep -qF "$expected_restart" <<< "$backup_image_output"; then
    echo "FAIL: backup result $backup_result did not restart the image IDs captured before stop." >&2
    sed 's/^/      /' <<< "$backup_image_output" >&2
    failures=$((failures + 1))
  else
    echo "ok: backup result $backup_result restarts the image IDs captured before stop."
  fi
done

# BACKUP_PROJECT_NAME exists for the restore rehearsal. Left behind in a shell, an inherited value
# would point the deployment's own backup at another stack - after the application is stopped and
# just before the migration. The stub is a separate process, so it reports the value the backup
# would really have been given rather than the one this test set.
cat > "$work/backup-target-stub.sh" <<'STUB'
#!/usr/bin/env bash
printf 'TRACE backup-target=%s\n' "${BACKUP_PROJECT_NAME:-unset}"
STUB

backup_target="$work/deploy-backup-target"
make_traceable scripts/deploy-production.sh "$backup_target" 'bash "$BACKUP_TARGET_STUB"'

# Both procedures that take a backup, not just the ad-hoc one. `update` is the worse of the two:
# it backs up with the application stopped and a migration next, so a backup sent at another stack
# there leaves production down with its schema about to change.
for backup_mode in update backup; do
  target_output="$(PATH="$PWD/$work/bin:$PATH" PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example     BACKUP_TARGET_STUB="$PWD/$work/backup-target-stub.sh" BACKUP_PROJECT_NAME=not-the-production-stack     bash "$backup_target" "$backup_mode" 2>&1)" || true

  if ! grep -qF "TRACE backup-target=seo-intelligence-prod" <<< "$target_output"; then
    echo "FAIL: in ${backup_mode} mode an inherited BACKUP_PROJECT_NAME redirected the backup." >&2
    grep '^TRACE ' <<< "$target_output" | sed 's/^/      /' >&2
    failures=$((failures + 1))
  else
    echo "ok: ${backup_mode} pins its own backup to the production stack."
  fi
done

# The images are labelled with the commit they were built from, and that is the only record of what
# a running container holds - the VPS builds from source rather than pulling a tag. If the revision
# cannot be established the deployment has to stop, not build something unattributable. A corrupt
# index is the awkward case: HEAD still resolves, so only `git status` fails, and it fails with
# empty output that reads exactly like a clean tree.
if [[ ! -e .git ]]; then
  echo "skip: the source-revision guard needs a git checkout."
else
printf 'not-an-index' > "$work/corrupt-index"
if GIT_INDEX_FILE="$PWD/$work/corrupt-index" PATH="$PWD/$work/bin:$PATH"   PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example   bash "$traceable" backup > "$work/revision-out" 2>&1; then
  echo "FAIL: the deployment ran even though the source revision could not be determined." >&2
  failures=$((failures + 1))
elif ! grep -q "git status failed" "$work/revision-out"; then
  echo "FAIL: the deployment stopped, but not because the revision was unresolvable." >&2
  sed 's/^/      /' "$work/revision-out" >&2
  failures=$((failures + 1))
else
  echo "ok: a source revision that cannot be established stops the deployment."
fi
fi

# A deployment script whose procedures never call build_and_scan. The commands are still present in
# the function body, so a source-level check passes; the trace shows they never run.
awk '!/^    build_and_scan$/' scripts/deploy-production.sh > "$work/deploy-uncalled-source"
make_traceable "$work/deploy-uncalled-source" "$work/deploy-uncalled"
expect_trace "a build and scan that is defined but never called is visible in the trace"   "$work/deploy-uncalled" initial   "compose up -d postgres redis
compose --profile tools run --rm migrate
compose up -d --wait api worker web
verify
compose ps"

# Without `set -e` a failing scan would not stop the deployment, which is the whole reason the
# procedure is a script rather than a list of commands.
sed 's/^set -euo pipefail$/set -uo pipefail/' scripts/deploy-production.sh > "$work/deploy-no-set-e"
expect_failure "a deployment script that does not abort on failure"   "does not 'set -euo pipefail'"   "DEPLOY_SCRIPT=$work/deploy-no-set-e"

# --project-name removed: a COMPOSE_PROJECT_NAME in the environment would then decide which
# project's volumes the migration writes to.
sed 's|--project-name "$PROJECT_NAME" ||' scripts/deploy-production.sh > "$work/deploy-no-project"
expect_failure "a deployment script that does not pin the Compose project"   "does not pass --project-name explicitly"   "DEPLOY_SCRIPT=$work/deploy-no-project"

# --- deployment document ------------------------------------------------------------------------

# Each procedure has to send the operator to the script. Removing the call from one section leaves
# the other, so a document-wide count would still find one.
awk '!/^bash scripts\/deploy-production\.sh update/' docs/docker_deployment.md > "$work/doc-no-update-call"
expect_failure "the update procedure no longer invoking the deployment script" \
  "invokes the deployment script 0 time(s)" \
  "DEPLOYMENT_DOC=$work/doc-no-update-call"

awk '!/^bash scripts\/deploy-production\.sh initial/' docs/docker_deployment.md > "$work/doc-no-initial-call"
expect_failure "the first-deploy procedure no longer invoking the deployment script" \
  "invokes the deployment script 0 time(s)" \
  "DEPLOYMENT_DOC=$work/doc-no-initial-call"

# --- the deployment script actually aborts ------------------------------------------------------

# The checks above prove the ordering is written correctly. This proves it behaves that way: with a
# failing scan, nothing after it must run. Compose is stubbed so the assertion is about control
# flow rather than about building images, and the start commands announce themselves so their
# absence is evidence rather than an assumption.
stub="$work/deploy-abort"
sed   -e 's|^COMPOSE=(docker compose .*|COMPOSE=(echo COMPOSE-RAN)|'   -e 's|^  APP_SCAN_MANIFEST=.* bash scripts/scan-container-images.sh app$|  echo SCAN-RAN; return 3|'   -e 's|^cd "$(dirname "$0")/\.\."$|cd "$(dirname "$0")/../.."|'   scripts/deploy-production.sh > "$stub"

abort_output="$(PATH="$PWD/$work/bin:$PATH" PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example bash "$stub" initial 2>&1)" && abort_status=0 || abort_status=$?

if [[ "$abort_status" -eq 0 ]]; then
  echo "FAIL: the deployment script completed even though the image scan failed." >&2
  failures=$((failures + 1))
elif ! grep -qF "SCAN-RAN" <<< "$abort_output"; then
  echo "FAIL: the deployment script did not reach the image scan, so the abort proves nothing." >&2
  failures=$((failures + 1))
elif grep -qF "up -d postgres redis" <<< "$abort_output"; then
  echo "FAIL: the deployment script started containers after the image scan failed." >&2
  sed 's/^/        /' <<< "$abort_output" >&2
  failures=$((failures + 1))
else
  echo "ok: a failing image scan stops the deployment before anything starts."
fi

# --- one deployment at a time -------------------------------------------------------------------

# Two updates started more than a second apart get different backup directories, so the exclusive
# mkdir inside the backup does not stop them. Without a lock the second would dump the database
# while the first is running its migration.
if ! command -v flock > /dev/null 2>&1; then
  echo "skip: the single-flight check needs flock, which is not available here."
else
  # The two runs are synchronised through files rather than through sleeps. A fixed delay decides
  # the outcome by timing: too short and the second starts before the first holds the lock, too
  # long and the first has finished by the time the second tries - and on a loaded runner both
  # happen. Either way the case passes or fails for reasons that have nothing to do with locking.
  #
  # This stands in for the backup, which runs inside the locked region. It announces that the lock
  # is held and then keeps holding it until the test says otherwise.
  cat > "$work/lock-handshake.sh" <<'HANDSHAKE'
#!/usr/bin/env bash
echo TRACE backup
touch "$LOCK_READY"
for _ in $(seq 1 600); do
  if [[ -e "$LOCK_RELEASE" ]]; then
    exit 0
  fi
  sleep 0.1
done
echo "the release signal never arrived within 60s" >&2
exit 1
HANDSHAKE

  slow="$work/deploy-slow"
  # Passed to make_traceable rather than sed'd in afterwards. An expression that stops matching
  # leaves the case testing nothing, which is exactly how this check came to pass a deployment that
  # was never held up; make_traceable verifies its own substitution.
  make_traceable scripts/deploy-production.sh "$slow" 'bash "$LOCK_HANDSHAKE"'

  first_ready="$PWD/$work/first-ready"
  first_release="$PWD/$work/first-release"
  rm -f "$first_ready" "$first_release"

  LOCK_HANDSHAKE="$PWD/$work/lock-handshake.sh" LOCK_READY="$first_ready" LOCK_RELEASE="$first_release"   PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example   bash "$slow" update > "$work/lock-first" 2>&1 &
  first=$!

  for _ in $(seq 1 600); do
    if [[ -e "$first_ready" ]] || ! kill -0 "$first" 2>/dev/null; then
      break
    fi
    sleep 0.1
  done

  if [[ ! -e "$first_ready" ]]; then
    echo "FAIL: the first deployment never reached the locked region." >&2
    sed 's/^/      /' "$work/lock-first" >&2
    failures=$((failures + 1))
    wait "$first" > /dev/null 2>&1 || true
    first_status=0
    second_status=1
  else
    # The second run gets its own signals, with the release already in place. If the lock wrongly
    # lets it through it finishes immediately and reports success - rather than blocking on a
    # release meant for the first run and timing out, which would look like a refusal.
    second_ready="$PWD/$work/second-ready"
    second_release="$PWD/$work/second-release"
    rm -f "$second_ready"
    touch "$second_release"

    LOCK_HANDSHAKE="$PWD/$work/lock-handshake.sh" LOCK_READY="$second_ready" LOCK_RELEASE="$second_release"     PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example     bash "$slow" update > "$work/lock-second" 2>&1 && second_status=0 || second_status=$?

    touch "$first_release"
    wait "$first" && first_status=0 || first_status=$?

    if [[ -e "$second_ready" ]]; then
      echo "FAIL: the second deployment reached the locked region while the first held the lock." >&2
      failures=$((failures + 1))
    fi
  fi

  if [[ "$first_status" -ne 0 ]]; then
    echo "FAIL: the first deployment did not complete." >&2
    sed 's/^/      /' "$work/lock-first" >&2
    failures=$((failures + 1))
  elif [[ "$second_status" -eq 0 ]]; then
    echo "FAIL: a second deployment ran while the first was still going." >&2
    failures=$((failures + 1))
  elif ! grep -qF "another operation is already working on" "$work/lock-second"; then
    echo "FAIL: the second deployment failed, but not on the lock." >&2
    sed 's/^/      /' "$work/lock-second" >&2
    failures=$((failures + 1))
  else
    echo "ok: a second deployment is refused while the first holds the lock."
  fi
fi


# --- the scan manifest ---------------------------------------------------------------------------

# compose.yaml names the four application images by variable so the deployment can start exactly
# the IDs the scan inspected. That is only worth anything if a missing or malformed manifest stops
# the deployment: falling back to the tags would start whatever they point at by then, which is the
# gap the manifest exists to close. pin_scanned_images is left real here; only Compose and the scan
# are stubbed, and the scan is what writes the manifest.
expect_manifest_refusal() {
  local description="$1" expected_message="$2" scan_body="$3"
  local stub="$work/deploy-manifest-$RANDOM"
  local manifest="$work/manifest-$RANDOM.tsv"

  sed \
    -e 's|^COMPOSE=(docker compose .*|COMPOSE=(echo COMPOSE-RAN)|' \
    -e "s|^  APP_SCAN_MANIFEST=.* bash scripts/scan-container-images.sh app\$|  ${scan_body}|" \
    -e 's|^SCAN_MANIFEST=.*|SCAN_MANIFEST="'"$manifest"'"|' \
    -e 's|^cd "$(dirname "$0")/\.\."$|cd "$(dirname "$0")/../.."|' \
    scripts/deploy-production.sh > "$stub"

  local output status=0
  output="$(PATH="$PWD/$work/bin:$PATH" PRODUCTION_LOCK_DIR="$PWD/$work/lock" \
    ENV_FILE=.env.production.example MANIFEST_PATH="$manifest" \
    API_IMAGE="sha256:$(printf '1%.0s' {1..64})" \
    WEB_IMAGE="sha256:$(printf '2%.0s' {1..64})" \
    WORKER_IMAGE="sha256:$(printf '3%.0s' {1..64})" \
    MIGRATE_IMAGE="sha256:$(printf '4%.0s' {1..64})" \
    bash "$stub" initial 2>&1)" || status=$?

  if [[ "$status" -eq 0 ]]; then
    echo "FAIL: $description was accepted." >&2
    failures=$((failures + 1))
  elif ! grep -qF -- "$expected_message" <<< "$output"; then
    echo "FAIL: $description was rejected, but not for the expected reason." >&2
    echo "      expected to contain: $expected_message" >&2
    sed 's/^/        /' <<< "$output" >&2
    failures=$((failures + 1))
  elif grep -qF "up -d postgres redis" <<< "$output"; then
    echo "FAIL: $description started containers anyway." >&2
    failures=$((failures + 1))
  else
    echo "ok: $description is caught."
  fi
}

expect_manifest_refusal "a scan that records nothing" \
  "is missing or empty" \
  'echo SCAN-RAN'

expect_manifest_refusal "a manifest naming a tag instead of an image ID" \
  "which is not an image ID" \
  'printf "seo-intelligence-api\\tseo-intelligence-api:latest\\n" > "$MANIFEST_PATH"'

expect_manifest_refusal "a manifest missing one of the four images" \
  "has no scanned image for" \
  'printf "seo-intelligence-api\\tsha256:%064d\\n" 1 > "$MANIFEST_PATH"'

expect_manifest_refusal "a manifest naming the same image twice" \
  "more than once" \
  'printf "seo-intelligence-api\\tsha256:%064d\\nseo-intelligence-api\\tsha256:%064d\\nseo-intelligence-web\\tsha256:%064d\\nseo-intelligence-worker\\tsha256:%064d\\nseo-intelligence-migrate\\tsha256:%064d\\n" 1 2 3 4 5 > "$MANIFEST_PATH"'

# A post-start check is only a safety control if it removes an image it cannot verify. Reporting an
# error while leaving api/web/worker online would turn a failed deployment into an unbounded period
# serving the artifact the check rejected.
mkdir -p "$work/mismatch-bin"
cat > "$work/mismatch-bin/docker" <<'DOCKER'
#!/usr/bin/env bash
if [[ "${1:-}" == "inspect" ]]; then
  printf 'sha256:%064d\n' 9
  exit 0
fi
echo "unexpected docker command: $*" >&2
exit 1
DOCKER
chmod +x "$work/mismatch-bin/docker"

cat > "$work/write-valid-manifest.sh" <<'MANIFEST'
#!/usr/bin/env bash
printf 'seo-intelligence-api\tsha256:%064d\n' 1 > "$MANIFEST_PATH"
printf 'seo-intelligence-web\tsha256:%064d\n' 2 >> "$MANIFEST_PATH"
printf 'seo-intelligence-worker\tsha256:%064d\n' 3 >> "$MANIFEST_PATH"
printf 'seo-intelligence-migrate\tsha256:%064d\n' 4 >> "$MANIFEST_PATH"
MANIFEST
chmod +x "$work/write-valid-manifest.sh"

mismatch_deploy="$work/deploy-running-mismatch"
mismatch_manifest="$PWD/$work/mismatch-manifest.tsv"
sed \
  -e 's|^COMPOSE=(docker compose .*|COMPOSE=(echo TRACE compose)|' \
  -e 's|^  APP_SCAN_MANIFEST=.* bash scripts/scan-container-images.sh app$|  bash "$MANIFEST_WRITER"|' \
  -e 's|^SCAN_MANIFEST=.*|SCAN_MANIFEST="'"$mismatch_manifest"'"|' \
  -e 's|^cd "$(dirname "$0")/\.\."$|cd "$(dirname "$0")/../.."|' \
  scripts/deploy-production.sh > "$mismatch_deploy"

mismatch_status=0
mismatch_output="$(
  PATH="$PWD/$work/mismatch-bin:$PWD/$work/bin:$PATH" \
    PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example \
    MANIFEST_PATH="$mismatch_manifest" MANIFEST_WRITER="$PWD/$work/write-valid-manifest.sh" \
    bash "$mismatch_deploy" initial 2>&1
)" || mismatch_status=$?

if [[ "$mismatch_status" -eq 0 ]]; then
  echo "FAIL: a running image mismatch was accepted." >&2
  failures=$((failures + 1))
elif ! grep -qF "TRACE compose stop web api worker" <<< "$mismatch_output"; then
  echo "FAIL: a running image mismatch left the application services online." >&2
  sed 's/^/      /' <<< "$mismatch_output" >&2
  failures=$((failures + 1))
else
  echo "ok: a running image mismatch stops all application services."
fi

if [[ "$failures" -ne 0 ]]; then
  echo "$failures guard(s) did not catch their case." >&2
  exit 1
fi

echo "All deployment guards catch the case they exist for."
