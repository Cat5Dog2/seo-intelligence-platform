#!/usr/bin/env bash
# Asserts the safety-critical values of the production Compose rendering.
#
# `config --quiet` only proves the files parse and merge. It would still pass if the
# AdminSeed overrides were removed from compose.production.yaml, which would silently
# fall back to the development credentials in compose.yaml - credentials that are public
# in this repository. If an Admin account were ever lost, an administrator could then be
# recreated from them. This script pins the rendered result instead.
#
#   bash scripts/verify-production-compose.sh
set -euo pipefail

cd "$(dirname "$0")/.."

# Placeholders for the two variables the production overlay requires. The point of this
# check is what happens when the optional variables are absent, so AdminSeed is not set.
export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-compose-validation-only}"
export API_SERVICE_KEY="${API_SERVICE_KEY:-compose-validation-only}"
unset ADMIN_SEED_EMAIL ADMIN_SEED_PASSWORD || true

# CI runners expose python3; Git Bash on Windows exposes python.
if command -v python3 > /dev/null 2>&1 && python3 -c "" > /dev/null 2>&1; then
  python_bin=python3
elif command -v python > /dev/null 2>&1; then
  python_bin=python
else
  echo "ERROR: python3 or python is required to inspect the Compose rendering." >&2
  exit 1
fi

rendered_path="$(mktemp)"
trap 'rm -f "$rendered_path"' EXIT

docker compose \
  --env-file .env.production.example \
  -f compose.yaml \
  -f compose.production.yaml \
  config --format json > "$rendered_path"

# The rendering goes through a file rather than a pipe: stdin already carries the script.
"$python_bin" - "$rendered_path" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as rendered_file:
    environment = json.load(rendered_file)["services"]["web"]["environment"]
failures = []

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

print("Production Compose rendering leaves AdminSeed__Email and AdminSeed__Password empty.")
PY
