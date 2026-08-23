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
  sed     -e 's|^COMPOSE=(docker compose .*|COMPOSE=(echo TRACE compose)|'     -e 's|^  bash scripts/scan-container-images.sh app$|  echo TRACE scan|'     -e 's|^cd "$(dirname "$0")/\.\."$|cd "$(dirname "$0")/../.."|'     "$source" > "$destination"
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
compose up -d postgres redis
compose --profile tools run --rm migrate
compose up -d --wait api worker web
compose ps"

expect_trace "an update backs up while stopped and before migrating"   "$traceable" update   "compose config --quiet
compose build api web worker migrate
scan
compose stop web api worker
backup
compose --profile tools run --rm migrate
compose up -d --wait --force-recreate api worker web
compose ps
compose logs --tail 200 web api worker"

expect_trace "an ad-hoc backup stops, backs up and starts again"   "$traceable" backup   "compose stop web api worker
backup
compose up -d --wait api worker web
compose ps"

# A failed backup must not leave the application down. Nothing has changed at that point - no
# migration has run - so the previous version has to come back up.
backup_fails="$work/deploy-backup-fails"
make_traceable scripts/deploy-production.sh "$backup_fails"
sed -i 's|echo TRACE backup$|echo TRACE backup; return 4|' "$backup_fails"

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

target_output="$(PATH="$PWD/$work/bin:$PATH" PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example   BACKUP_TARGET_STUB="$PWD/$work/backup-target-stub.sh" BACKUP_PROJECT_NAME=not-the-production-stack   bash "$backup_target" backup 2>&1)" || true

if ! grep -qF "TRACE backup-target=seo-intelligence-prod" <<< "$target_output"; then
  echo "FAIL: an inherited BACKUP_PROJECT_NAME redirected the deployment's backup." >&2
  grep '^TRACE ' <<< "$target_output" | sed 's/^/      /' >&2
  failures=$((failures + 1))
else
  echo "ok: the deployment pins its own backup to the production stack."
fi

# A deployment script whose procedures never call build_and_scan. The commands are still present in
# the function body, so a source-level check passes; the trace shows they never run.
awk '!/^    build_and_scan$/' scripts/deploy-production.sh > "$work/deploy-uncalled-source"
make_traceable "$work/deploy-uncalled-source" "$work/deploy-uncalled"
expect_trace "a build and scan that is defined but never called is visible in the trace"   "$work/deploy-uncalled" initial   "compose up -d postgres redis
compose --profile tools run --rm migrate
compose up -d --wait api worker web
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
sed   -e 's|^COMPOSE=(docker compose .*|COMPOSE=(echo COMPOSE-RAN)|'   -e 's|^  bash scripts/scan-container-images.sh app$|  echo SCAN-RAN; return 3|'   -e 's|^cd "$(dirname "$0")/\.\."$|cd "$(dirname "$0")/../.."|'   scripts/deploy-production.sh > "$stub"

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
  slow="$work/deploy-slow"
  make_traceable scripts/deploy-production.sh "$slow"
  # Hold the first deployment inside the locked region long enough for the second to try.
  sed -i 's|^    echo TRACE backup$|    echo TRACE backup; sleep 5|' "$slow"

  PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example bash "$slow" update > "$work/lock-first" 2>&1 &
  first=$!
  sleep 2
  PRODUCTION_LOCK_DIR="$PWD/$work/lock" ENV_FILE=.env.production.example bash "$slow" update > "$work/lock-second" 2>&1 && second_status=0 || second_status=$?
  wait "$first" && first_status=0 || first_status=$?

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

if [[ "$failures" -ne 0 ]]; then
  echo "$failures guard(s) did not catch their case." >&2
  exit 1
fi

echo "All deployment guards catch the case they exist for."
