#!/usr/bin/env bash
# Tests scripts/lib/release-candidate-notify.sh and statically checks
# .github/workflows/release-candidate-notify.yaml.
#
#   bash scripts/verify-release-candidate-notify.sh
#
# This never sends a real repository_dispatch and never calls the GitHub
# API - CD_ENABLED, CD_APP_ID, CD_APP_PRIVATE_KEY, and INFRA_REPOSITORY do
# not need to be configured for this to run. It checks two different things
# for two different reasons:
#
#   - The shell functions in the library are exercised directly with a
#     battery of inputs, because they are what decides whether a real run
#     notifies, what it sends, and whether malformed upstream data can ever
#     reach a payload.
#   - The workflow file itself is checked statically (grep, plus a full
#     YAML parse when PyYAML is available), because GitHub's `if:`
#     expression language is not something a local shell can execute; the
#     job-level gate condition can only be verified by inspecting the text
#     GitHub will evaluate.
set -euo pipefail

cd "$(dirname "$0")/.."
repo_root="$PWD"

LIB="scripts/lib/release-candidate-notify.sh"
WORKFLOW=".github/workflows/release-candidate-notify.yaml"
CI_WORKFLOW=".github/workflows/ci.yaml"
EXPECTED_APP_TOKEN_PIN="actions/create-github-app-token@bcd2ba49218906704ab6c1aa796996da409d3eb1 # v3.2.0"

if [[ ! -f "$LIB" ]]; then
  echo "ERROR: $LIB not found." >&2
  exit 1
fi
if [[ ! -f "$WORKFLOW" ]]; then
  echo "ERROR: $WORKFLOW not found." >&2
  exit 1
fi

# shellcheck source=scripts/lib/release-candidate-notify.sh
source "$LIB"

failures=0

check() {
  local description="$1" expected="$2" actual="$3"
  if [[ "$actual" == "$expected" ]]; then
    echo "ok: $description"
  else
    echo "FAIL: $description (expected '$expected', got '$actual')" >&2
    failures=$((failures + 1))
  fi
}

# expect_fail <description> <function-call...>
# Runs a command that must fail with no stdout. Used for malformed-input
# cases where the only acceptable outcomes are "rejected" and "rejected
# without printing anything that looks like a usable payload or output".
expect_fail() {
  local description="$1"
  shift
  local out status
  set +e
  out="$("$@" 2>/dev/null)"
  status=$?
  set -e
  if [[ "$status" -eq 0 ]]; then
    echo "FAIL: $description (expected non-zero exit, got 0, stdout='$out')" >&2
    failures=$((failures + 1))
  elif [[ -n "$out" ]]; then
    echo "FAIL: $description (rejected as expected, but printed to stdout: '$out')" >&2
    failures=$((failures + 1))
  else
    echo "ok: $description"
  fi
}

echo "--- workflow file: the real gate step, extracted from the YAML and executed ---"

# This does NOT test a shell function that stands in for the gate: the gate is inline bash in the
# workflow's "gate" step, deliberately not a call into this library (see the note in
# scripts/lib/release-candidate-notify.sh - it runs before checkout, so the library is not even on
# disk yet). Testing a hand-maintained copy of that logic would pass even if the inline condition
# in the YAML itself regressed - for example `==` flipped to `!=` - which is exactly the kind of
# change this step exists to catch. So the step's actual `run:` body is pulled out of the YAML
# (stdlib only, no PyYAML dependency - this must work everywhere the rest of this script does) and
# executed for real, with CD_ENABLED and GITHUB_OUTPUT as its only inputs, the same as GitHub
# Actions would provide them.

extract_step_run() {
  local step_id="$1"
  "$RC_NOTIFY_PYTHON_BIN" - "$WORKFLOW" "$step_id" <<'PY'
import re
import sys

path, step_id = sys.argv[1], sys.argv[2]

with open(path, encoding="utf-8") as f:
    lines = f.readlines()


def indent_of(line):
    return len(line) - len(line.lstrip(" "))


id_pattern = re.compile(rf"^\s*id:\s*{re.escape(step_id)}\s*$")
id_line_idx = next((i for i, line in enumerate(lines) if id_pattern.match(line)), None)
if id_line_idx is None:
    sys.exit(f"ERROR: no step with id: {step_id} found in {path}")
step_indent = indent_of(lines[id_line_idx])

run_line_idx = None
for i in range(id_line_idx, len(lines)):
    line = lines[i]
    if i > id_line_idx and line.strip() and indent_of(line) < step_indent:
        break  # ran off the end of this step (e.g. the next step's "- name:") without a run:
    if indent_of(line) == step_indent and line.lstrip(" ").startswith("run:"):
        run_line_idx = i
        break
if run_line_idx is None:
    sys.exit(f"ERROR: no 'run:' key found for step id: {step_id} in {path}")

run_line = lines[run_line_idx]
scalar_marker = run_line.split("run:", 1)[1].strip()
if not scalar_marker.startswith("|"):
    sys.exit(f"ERROR: step id: {step_id} 'run:' is not a literal block scalar ('run: |'): {run_line!r}")
run_key_indent = indent_of(run_line)

body = []
for line in lines[run_line_idx + 1:]:
    if line.strip() == "":
        body.append(line)
        continue
    if indent_of(line) <= run_key_indent:
        break
    body.append(line)
if not body:
    sys.exit(f"ERROR: step id: {step_id} has an empty 'run:' block")

base_indent = indent_of(next(l for l in body if l.strip() != ""))


def deindent(line):
    if line.strip() == "":
        return "\n"
    return line[base_indent:] if indent_of(line) >= base_indent else line.lstrip(" ")


sys.stdout.write("".join(deindent(l) for l in body))
PY
}

gate_script="$(mktemp)"
trap 'rm -f "$gate_script"' EXIT
extract_step_run "gate" > "$gate_script"

if ! grep -q 'GITHUB_OUTPUT' "$gate_script"; then
  echo "FAIL: extracted gate script does not look like the gate step (no GITHUB_OUTPUT reference); extraction is likely broken" >&2
  echo "--- extracted content ---" >&2
  cat "$gate_script" >&2
  failures=$((failures + 1))
fi

run_gate() {
  local cd_enabled_value="$1"
  local output_file
  output_file="$(mktemp)"
  CD_ENABLED="$cd_enabled_value" GITHUB_OUTPUT="$output_file" bash "$gate_script" 2>/dev/null
  cat "$output_file"
  rm -f "$output_file"
}

check "real gate step: unset CD_ENABLED disables notification" "enabled=false" "$(run_gate "")"
check "real gate step: CD_ENABLED=false disables notification" "enabled=false" "$(run_gate "false")"
check "real gate step: CD_ENABLED is case-sensitive (TRUE disables)" "enabled=false" "$(run_gate "TRUE")"
check "real gate step: CD_ENABLED with stray whitespace disables notification" "enabled=false" "$(run_gate " true")"
check "real gate step: CD_ENABLED=true enables notification" "enabled=true" "$(run_gate "true")"

echo "--- rc_notify_split_infra_repository ---"

check "splits a well-formed owner/repository" "owner=my-org
repo=wwt-seo-infra" "$(rc_notify_split_infra_repository "my-org/wwt-seo-infra")"
check "allows dots, underscores and hyphens" "owner=my-org.io
repo=wwt_seo-infra.2" "$(rc_notify_split_infra_repository "my-org.io/wwt_seo-infra.2")"
expect_fail "empty INFRA_REPOSITORY is rejected" rc_notify_split_infra_repository ""
expect_fail "INFRA_REPOSITORY without a slash is rejected" rc_notify_split_infra_repository "no-slash-here"
expect_fail "INFRA_REPOSITORY with two slashes is rejected" rc_notify_split_infra_repository "a/b/c"
expect_fail "INFRA_REPOSITORY with an empty owner is rejected" rc_notify_split_infra_repository "/repo"
expect_fail "INFRA_REPOSITORY with an empty repo name is rejected" rc_notify_split_infra_repository "owner/"

echo "--- rc_notify_build_payload ---"

valid_sha="0123456789abcdef0123456789abcdef01234567"

payload="$(rc_notify_build_payload "$valid_sha" "123456789" "1")"
set +e
"$RC_NOTIFY_PYTHON_BIN" - "$payload" "$valid_sha" <<'PY'
import json
import sys

payload_text, expected_sha = sys.argv[1], sys.argv[2]
failures = 0


def check(description, expected, actual):
    global failures
    if actual == expected:
        print(f"ok: {description}")
    else:
        print(f"FAIL: {description} (expected {expected!r}, got {actual!r})", file=sys.stderr)
        failures += 1


try:
    doc = json.loads(payload_text)
except json.JSONDecodeError as exc:
    print(f"FAIL: payload is not valid JSON: {exc}", file=sys.stderr)
    sys.exit(1)

check("payload has exactly event_type and client_payload", {"event_type", "client_payload"}, set(doc.keys()))
check("event_type is app-release-candidate-v1", "app-release-candidate-v1", doc.get("event_type"))

client_payload = doc.get("client_payload", {})
check(
    "client_payload has exactly the four contract fields",
    {"component", "source_sha", "source_run_id", "source_run_attempt"},
    set(client_payload.keys()),
)
check("component is the fixed value 'seo'", "seo", client_payload.get("component"))
check("source_sha is the upstream head_sha", expected_sha, client_payload.get("source_sha"))
check("source_run_id is the string '123456789'", "123456789", client_payload.get("source_run_id"))
check("source_run_id is a JSON string, not a number", True, isinstance(client_payload.get("source_run_id"), str))
check("source_run_attempt is the integer 1", 1, client_payload.get("source_run_attempt"))
check(
    "source_run_attempt is a JSON number, not a string",
    True,
    isinstance(client_payload.get("source_run_attempt"), int) and not isinstance(client_payload.get("source_run_attempt"), bool),
)

sys.exit(1 if failures else 0)
PY
payload_check_status=$?
set -e
if [[ "$payload_check_status" -ne 0 ]]; then
  failures=$((failures + 1))
fi

payload_attempt_3="$(rc_notify_build_payload "$valid_sha" "42" "3")"
check "source_run_attempt reflects a retried CI run (attempt 3)" \
  '{"event_type": "app-release-candidate-v1", "client_payload": {"component": "seo", "source_sha": "0123456789abcdef0123456789abcdef01234567", "source_run_id": "42", "source_run_attempt": 3}}' \
  "$payload_attempt_3"

expect_fail "a 39-character sha is rejected" rc_notify_build_payload "0123456789abcdef0123456789abcdef0123456" "1" "1"
expect_fail "a 41-character sha is rejected" rc_notify_build_payload "0123456789abcdef0123456789abcdef012345678" "1" "1"
expect_fail "an uppercase-hex sha is rejected" rc_notify_build_payload "0123456789ABCDEF0123456789abcdef01234567" "1" "1"
expect_fail "a non-hex sha is rejected" rc_notify_build_payload "g123456789abcdef0123456789abcdef01234567" "1" "1"
expect_fail "an empty run_id is rejected" rc_notify_build_payload "$valid_sha" "" "1"
expect_fail "a non-numeric run_id is rejected" rc_notify_build_payload "$valid_sha" "12a" "1"
expect_fail "a negative run_id is rejected" rc_notify_build_payload "$valid_sha" "-1" "1"
expect_fail "run_attempt 0 is rejected" rc_notify_build_payload "$valid_sha" "1" "0"
expect_fail "a negative run_attempt is rejected" rc_notify_build_payload "$valid_sha" "1" "-1"
expect_fail "a leading-zero run_attempt is rejected" rc_notify_build_payload "$valid_sha" "1" "01"
expect_fail "a non-numeric run_attempt is rejected" rc_notify_build_payload "$valid_sha" "1" "abc"

echo "--- workflow file: grep-based structural checks (always run) ---"

grep_has() {
  local description="$1" pattern="$2"
  if grep -qF -- "$pattern" "$WORKFLOW"; then
    echo "ok: $description"
  else
    echo "FAIL: $description (did not find: $pattern)" >&2
    failures=$((failures + 1))
  fi
}

grep_absent() {
  local description="$1" pattern="$2"
  if grep -qE -- "$pattern" "$WORKFLOW"; then
    echo "FAIL: $description (found forbidden pattern: $pattern)" >&2
    failures=$((failures + 1))
  else
    echo "ok: $description"
  fi
}

grep_has "triggers on completion of the CI workflow" 'workflows: ["CI"]'
grep_has "triggers only on the completed type" "types: [completed]"
grep_has "gate condition checks conclusion == success" "github.event.workflow_run.conclusion == 'success'"
grep_has "gate condition checks event == push" "github.event.workflow_run.event == 'push'"
grep_has "gate condition checks head_branch == main" "github.event.workflow_run.head_branch == 'main'"
grep_has "gate condition checks head_repository against github.repository (fork guard)" \
  "github.event.workflow_run.head_repository.full_name == github.repository"
grep_has "payload step reads head_sha, not github.sha" 'SOURCE_SHA: ${{ github.event.workflow_run.head_sha }}'
grep_has "payload step reads the run id" 'SOURCE_RUN_ID: ${{ github.event.workflow_run.id }}'
grep_has "payload step reads the run attempt" 'SOURCE_RUN_ATTEMPT: ${{ github.event.workflow_run.run_attempt }}'
grep_has "CD_ENABLED gate step reads the repository variable" 'CD_ENABLED: ${{ vars.CD_ENABLED }}'
grep_has "later steps are gated on CD_ENABLED" "steps.gate.outputs.enabled == 'true'"
grep_has "the App token is scoped with permission-contents: write" "permission-contents: write"
grep_has "the App token is minted for the resolved infra repo, not this one" \
  "repositories: \${{ steps.validate.outputs.repo }}"
grep_has "the dispatch call sends a structured JSON body, not flag interpolation" "--input -"
grep_has "the create-github-app-token pin matches the verified release SHA" "$EXPECTED_APP_TOKEN_PIN"

grep_absent "the notify workflow's own github.sha is never used" '\bgithub\.sha\b'
grep_absent "no step disables failure with continue-on-error" "continue-on-error"
grep_absent "no step silently swallows failure with '|| true' around the dispatch call" '\|\|\s*true'

uses_lines="$(grep -cE '^\s*uses:' "$WORKFLOW")"
pinned_lines="$(grep -cE 'uses:\s*\S+@[0-9a-f]{40}\s+#' "$WORKFLOW")"
check "every 'uses:' line is pinned to a 40-character commit SHA with a version comment" \
  "$uses_lines" "$pinned_lines"

if grep -qF -- "verify-release-candidate-notify.sh" "$CI_WORKFLOW"; then
  echo "ok: ci.yaml runs this verification"
else
  echo "FAIL: ci.yaml does not call scripts/verify-release-candidate-notify.sh" >&2
  failures=$((failures + 1))
fi

echo "--- workflow file: full YAML parse (best-effort) ---"

set +e
"$RC_NOTIFY_PYTHON_BIN" - "$WORKFLOW" <<'PY'
import sys

try:
    import yaml
except ImportError:
    print("skip: PyYAML is not installed in this environment; relying on the grep-based checks above only.")
    sys.exit(0)

path = sys.argv[1]
failures = 0


def check(description, expected, actual):
    global failures
    if actual == expected:
        print(f"ok: {description}")
    else:
        print(f"FAIL: {description} (expected {expected!r}, got {actual!r})", file=sys.stderr)
        failures += 1


with open(path, encoding="utf-8") as f:
    doc = yaml.safe_load(f)

check("the file parses as a single YAML document (a mapping)", True, isinstance(doc, dict))

# YAML 1.1 treats the bare word `on` as the boolean True; GitHub Actions workflows always key
# their triggers under the literal string "on", so PyYAML's default loader hands it back as the
# key `True` rather than `"on"`. Accept either so this check reflects how the file is actually
# authored rather than an artifact of the loader.
on_section = doc.get("on", doc.get(True))
check("the workflow_run trigger names CI as the workflow to watch", ["CI"], (on_section or {}).get("workflow_run", {}).get("workflows"))
check("the workflow_run trigger only reacts to 'completed'", ["completed"], (on_section or {}).get("workflow_run", {}).get("types"))

check("top-level permissions are exactly contents: read", {"contents": "read"}, doc.get("permissions"))

jobs = doc.get("jobs", {})
check("there is exactly one job", ["notify"], list(jobs.keys()))

notify = jobs.get("notify", {})
check("the job runs on ubuntu-latest", "ubuntu-latest", notify.get("runs-on"))
check("the job has a short timeout", True, isinstance(notify.get("timeout-minutes"), int) and notify.get("timeout-minutes", 999) <= 10)

job_if = notify.get("if", "")
for fragment in [
    "conclusion == 'success'",
    "event == 'push'",
    "head_branch == 'main'",
    "head_repository.full_name == github.repository",
]:
    check(f"job-level if: contains {fragment!r}", True, fragment in job_if)

steps = notify.get("steps", [])
steps_by_id = {step["id"]: step for step in steps if "id" in step}

check("a gate step exists and is unconditional", True, "gate" in steps_by_id and "if" not in steps_by_id["gate"])

for step_id in ["validate", "app_token"]:
    check(f"step '{step_id}' is gated on CD_ENABLED", "steps.gate.outputs.enabled == 'true'", steps_by_id.get(step_id, {}).get("if"))

# The notification-sending step is identified by content rather than an id: it is the one that
# actually calls the GitHub API, which is the step that must never run while disabled.
send_steps = [s for s in steps if "gh api" in s.get("run", "")]
check("exactly one step calls the GitHub API", 1, len(send_steps))
if send_steps:
    check("the API-calling step is gated on CD_ENABLED", "steps.gate.outputs.enabled == 'true'", send_steps[0].get("if"))

# No `run:` script body may contain a literal `${{ }}` expression: every event/context value this
# workflow uses must arrive through `env:` instead, per the "do not expand event values directly
# into the shell" requirement. A `run:` block that references `${{ }}` is exactly the classic
# script-injection shape this rule exists to rule out.
offending = [s.get("name", "<unnamed>") for s in steps if "${{" in s.get("run", "")]
check("no run: block interpolates a GitHub expression directly", [], offending)

sys.exit(1 if failures else 0)
PY
python_status=$?
set -e
if [[ "$python_status" -ne 0 ]]; then
  failures=$((failures + 1))
fi

if [[ "$failures" -ne 0 ]]; then
  echo "$failures release-candidate-notify check(s) failed." >&2
  exit 1
fi

echo "release-candidate-notify: gating, validation, payload construction, and workflow structure all check out."
