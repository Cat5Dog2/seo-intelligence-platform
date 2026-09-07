#!/usr/bin/env bash
# Asserts the safety-critical values of the production Compose rendering.
#
# `config --quiet` only proves the files parse and merge. It would still pass if the
# AdminSeed overrides were removed from compose.production.yaml, which would silently
# fall back to the development credentials in compose.yaml - credentials that are public
# in this repository. If an Admin account were ever lost, an administrator could then be
# recreated from them. This script pins the rendered result instead.
#
# It also pins that the Web service loads no env_file. That file carries the external API key
# and the Discord webhook; re-adding env_file to web would hand both to the internet-facing
# service without any error to notice.
#
#   bash scripts/verify-production-compose.sh
set -euo pipefail

cd "$(dirname "$0")/.."

# Placeholders for the two variables the production overlay requires. The point of this
# check is what happens when the optional variables are absent, so AdminSeed is not set.
export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-compose-validation-only}"
export API_SERVICE_KEY="${API_SERVICE_KEY:-compose-validation-only}"
unset ADMIN_SEED_EMAIL ADMIN_SEED_PASSWORD || true

# Point the app env_file at the committed example so the rendering actually contains the
# backend-only keys. Without this the leak check below would pass on a missing file and prove
# nothing: `.env.production.app` is not in the repository and env_file is `required: false`.
export APP_ENV_FILE=.env.production.app.example

# Overridable so the guard-regression test can point them at tampered copies.
DEPLOYMENT_DOC="${DEPLOYMENT_DOC:-docs/docker_deployment.md}"
DIGEST_LOCK_FILE="${DIGEST_LOCK_FILE:-image-digests.lock}"

# CI runners expose python3; Git Bash on Windows exposes python.
if command -v python3 > /dev/null 2>&1 && python3 -c "" > /dev/null 2>&1; then
  python_bin=python3
elif command -v python > /dev/null 2>&1; then
  python_bin=python
else
  echo "ERROR: python3 or python is required to inspect the Compose rendering." >&2
  exit 1
fi

# The deployment procedure has two paths, first deploy and update, and each needs both commands.
# Counting occurrences rather than looking for malformed ones also catches a line being deleted or
# reworded, which a "fail on a bad match" check would pass.
#
# `migrate` sits behind the `tools` profile, so a bare `compose build` silently skips it: on a first
# deploy there would be no migrate image, and on an update migrations would run from the previous
# release's bundle. The app scan has to run on the VPS because the images CI scanned are not the
# images the VPS builds.
# The deployment procedure lives in a script so that a failing vulnerability scan actually stops
# the deployment. Its ordering is verified by running it against stubs in
# scripts/verify-deployment-guards.sh, not by pattern-matching this source: a check that only finds
# the commands cannot tell whether they are ever reached.
DEPLOY_SCRIPT="${DEPLOY_SCRIPT:-scripts/deploy-production.sh}"

if ! grep -q '^set -euo pipefail$' "$DEPLOY_SCRIPT"; then
  echo "ERROR: $DEPLOY_SCRIPT does not 'set -euo pipefail', so a failing scan would not stop the deployment." >&2
  exit 1
fi

# COMPOSE_PROJECT_NAME in the environment overrides `name:` in compose.production.yaml, and the
# project decides which volumes a migration writes to and which containers get stopped. Passing
# --project-name explicitly is what makes that impossible to get wrong from the outside.
if ! grep -q -- '--project-name "\$PROJECT_NAME"' "$DEPLOY_SCRIPT"; then
  echo "ERROR: $DEPLOY_SCRIPT does not pass --project-name explicitly. A COMPOSE_PROJECT_NAME in" >&2
  echo "       the environment would then redirect the deployment at another project's volumes." >&2
  exit 1
fi

# Everything between a `## ` heading and the next one.
section_of() {
  local heading="$1"
  awk -v heading="$heading" '
    $0 == heading { inside = 1; next }
    /^## / { inside = 0 }
    inside { print }
  ' "$DEPLOYMENT_DOC"
}

# Every production Compose command in the documentation has to pin the project. The deployment
# script does, but a hostile COMPOSE_PROJECT_NAME would still redirect a hand-run stop, exec or
# restart at another stack.
for doc in "$DEPLOYMENT_DOC" docs/operations_runbook.md; do
  unpinned="$(grep -nE '^docker compose --env-file' "$doc" || true)"
  if [[ -n "$unpinned" ]]; then
    echo "ERROR: $doc has production Compose commands that do not pass --project-name:" >&2
    sed 's/^/       /' <<< "$unpinned" >&2
    echo "       A COMPOSE_PROJECT_NAME in the environment would redirect them at another project." >&2
    exit 1
  fi
done

# The document has to send the operator to the script, in both procedures.
for heading in "## 3. VPSの初回デプロイ" "## 4. 更新手順"; do
  count="$(section_of "$heading" | grep -cE '^bash scripts/deploy-production\.sh (initial|update)' || true)"
  if [[ "$count" -ne 1 ]]; then
    echo "ERROR: '$heading' in $DEPLOYMENT_DOC invokes the deployment script $count time(s), expected 1." >&2
    exit 1
  fi
done

rendered_path="$(mktemp)"
trap 'rm -f "$rendered_path"' EXIT

# Rendered with a hostile COMPOSE_PROJECT_NAME on purpose: the check below has to prove the
# project cannot be redirected from the environment, and would pass vacuously without one.
COMPOSE_PROJECT_NAME=verify-should-not-win docker compose \
  --project-name seo-intelligence-prod \
  --env-file .env.production.example \
  -f compose.yaml \
  -f compose.production.yaml \
  config --format json > "$rendered_path"

# The rendering goes through a file rather than a pipe: stdin already carries the script.
"$python_bin" - "$rendered_path" "$DIGEST_LOCK_FILE" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as rendered_file:
    rendered_project = json.load(rendered_file)
services = rendered_project["services"]
environment = services["web"]["environment"]
failures = []

# The project decides which volumes a migration writes to and which containers get stopped.
# compose.production.yaml sets it, but a COMPOSE_PROJECT_NAME in the environment wins over that
# key - so the caller renders with exactly such a variable set, and this asserts it lost.
PRODUCTION_PROJECT_NAME = "seo-intelligence-prod"
if rendered_project.get("name") != PRODUCTION_PROJECT_NAME:
    failures.append(
        f"the rendering names the project {rendered_project.get('name')!r}, not "
        f"{PRODUCTION_PROJECT_NAME!r}. Every production command must pass --project-name so an "
        f"environment variable cannot redirect it at another stack's volumes."
    )

# The third-party images have to be exactly what image-digests.lock records. Scanning by digest
# proves nothing if the VPS then runs something else.
locked = {}
with open(sys.argv[2], encoding="utf-8") as lock_file:
    for line in lock_file:
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        service, tag, digest = line.split("\t", 2)
        locked[service] = f"{tag}@{digest}"

# The services that must be pinned, stated here rather than derived from the lock file. Deriving
# them would drop a service from both sides of the comparison when its line was removed, so
# deleting an entry would silently disable its check.
REQUIRED_SERVICES = {"postgres", "redis"}

if set(locked) != REQUIRED_SERVICES:
    failures.append(
        f"the digest lock file covers {sorted(locked)} but every third-party service this stack "
        f"runs must be pinned: {sorted(REQUIRED_SERVICES)}."
    )
else:
    # Compared as whole dictionaries, and against the rendered output rather than the Compose
    # source. Asking only whether each locked image appears somewhere would pass even if two
    # services had each other's image; grepping the source would pass on a matching comment.
    rendered = {service: services[service].get("image", "") for service in REQUIRED_SERVICES}
    if rendered != locked:
        failures.append(
            f"the rendered images do not match the digest lock file.\n"
            f"    locked:   {locked}\n"
            f"    rendered: {rendered}\n"
            f"    The lock file is the source of truth: compose.yaml must deploy exactly what "
            f"was scanned and judged in docs/operations_runbook.md section 7.3."
        )

# Compose inlines env_file entries into `environment`, so the absence of the key itself is what
# proves the Web host never receives it - checking for an `env_file` field would always pass.
# Secrets__ApiServiceKey is excluded: the Web host presents it to the API on every call.
leaked = sorted(
    key
    for key in environment
    if key.startswith(("RakkoKeyword__", "Discord__"))
    or (key.startswith("Secrets__") and key != "Secrets__ApiServiceKey")
)
if leaked:
    failures.append(
        f"web receives backend-only configuration: {', '.join(leaked)}. Only api and worker call "
        f"external APIs, so re-adding env_file to web (or listing these under its environment) "
        f"would hand the external API key and the Discord webhook to the internet-facing service."
    )

# The same keys must still reach the services that do call out, otherwise this check could be
# "satisfied" by dropping the app env file everywhere.
for service in ("api", "worker"):
    service_environment = services[service]["environment"]
    if "RakkoKeyword__Mode" not in service_environment:
        failures.append(
            f"{service} does not receive RakkoKeyword__Mode. api and worker must still load the "
            f"application env file named by APP_ENV_FILE."
        )

for key in ("AdminSeed__Email", "AdminSeed__Password"):
    if key not in environment:
        failures.append(
            f"{key} is absent from the production rendering. compose.production.yaml must "
            f"override it with an empty value so the development default in compose.yaml "
            f"cannot reach production."
        )
        continue

    value = environment[key]
    if value:
        failures.append(
            f"{key} rendered as {value!r} with no ADMIN_SEED_* variables set. It must be "
            f"empty: the Web host already refuses to start when no Admin exists, and a "
            f"non-empty default would let a lost Admin be recreated from known credentials."
        )

if failures:
    for failure in failures:
        print(f"ERROR: {failure}", file=sys.stderr)
    sys.exit(1)

print(
    "Production Compose rendering leaves AdminSeed__Email and AdminSeed__Password empty, "
    "keeps backend-only secrets out of web, and still delivers them to api and worker."
)
PY

# Forwarded headers: the trusted range must be explicit, and the switch that trusted everything
# must be gone. ASPNETCORE_FORWARDEDHEADERS_ENABLED enables the middleware by clearing the
# known-proxy and known-network lists, so leaving it beside an explicit range would quietly restore
# "trust every source". Checked against the rendered output, not the source, so a value reaching
# the container through any file or variable is caught.
rendered_environment="$(POSTGRES_PASSWORD=verify API_SERVICE_KEY=verify   docker compose --env-file .env.production.example -f compose.yaml -f compose.production.yaml config --format json)"

if printf '%s' "$rendered_environment" | grep -q 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'; then
  echo "ERROR: the rendered production Compose still sets ASPNETCORE_FORWARDEDHEADERS_ENABLED." >&2
  echo "       It enables the forwarded-headers middleware by trusting every source, which is what" >&2
  echo "       TrustedProxy__Subnet replaced. See docs/operations_runbook.md." >&2
  exit 1
fi

for service in api web; do
  if ! printf '%s' "$rendered_environment" | "$python_bin" -c "
import json, sys
service = sys.argv[1]
config = json.load(sys.stdin)
value = (config['services'][service]['environment'] or {}).get('TrustedProxy__Subnet')
sys.exit(0 if value else 1)
" "$service"; then
    echo "ERROR: $service has no TrustedProxy__Subnet, so it would trust no proxy and report Caddy's address as every client's." >&2
    exit 1
  fi
done

echo "Forwarded headers are scoped to an explicit subnet for api and web."
