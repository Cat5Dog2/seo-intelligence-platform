# Shared shell logic for the release-candidate-notify workflow.
#
# Sourced both by .github/workflows/release-candidate-notify.yaml and by
# scripts/verify-release-candidate-notify.sh, so the exact validation and
# payload-construction logic that runs in CI is also exercised by local
# tests - not a re-implementation of it that could drift.
#
# The shared contract with wwt-seo-infra (do not change without updating the
# other repositories that send this event): a repository_dispatch with
# event_type "app-release-candidate-v1" and client_payload
# {component, source_sha, source_run_id, source_run_attempt}. component is
# always "seo" for this repository. source_sha is the 40-character commit
# SHA the upstream CI run verified; source_run_id and source_run_attempt
# identify that CI run so infra can trace a release candidate back to the
# workflow run that produced it.

# CI runners expose python3; Git Bash on Windows exposes python.
if command -v python3 > /dev/null 2>&1 && python3 -c "" > /dev/null 2>&1; then
  RC_NOTIFY_PYTHON_BIN="${RC_NOTIFY_PYTHON_BIN:-python3}"
elif command -v python > /dev/null 2>&1; then
  RC_NOTIFY_PYTHON_BIN="${RC_NOTIFY_PYTHON_BIN:-python}"
else
  echo "ERROR: python3 or python is required by scripts/lib/release-candidate-notify.sh." >&2
  return 1 2>/dev/null || exit 1
fi

# Note: the CD_ENABLED gate is deliberately NOT a function here. It is
# evaluated inline in the workflow's "gate" step, before the checkout step
# that makes this file available on the runner - sourcing a shared function
# for it would mean checking out the repository (or duplicating the check)
# before deciding whether release-candidate notification is even enabled.
# scripts/verify-release-candidate-notify.sh extracts and executes that
# step's actual `run:` body directly from the workflow YAML instead, so the
# gate behavior under test is the real one, not a hand-maintained stand-in.

# rc_notify_split_infra_repository <INFRA_REPOSITORY value>
#
# Validates that the value is a non-empty "owner/repository" pair and prints
# it as GITHUB_OUTPUT-style lines ("owner=...", "repo=...") on success.
# Fails (non-zero, nothing on stdout, an ERROR: line on stderr) if the value
# is empty, has no slash, has more than one slash, or either side is empty
# or contains a character GitHub does not allow in owner/repository names.
rc_notify_split_infra_repository() {
  local infra_repository="${1:-}"
  if [[ -z "$infra_repository" ]]; then
    echo "ERROR: the INFRA_REPOSITORY repository variable is not set." >&2
    return 1
  fi
  if ! [[ "$infra_repository" =~ ^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$ ]]; then
    echo "ERROR: INFRA_REPOSITORY must be exactly 'owner/repository', got: '$infra_repository'" >&2
    return 1
  fi
  echo "owner=${infra_repository%%/*}"
  echo "repo=${infra_repository##*/}"
}

# rc_notify_build_payload <head_sha> <run_id> <run_attempt>
#
# Validates the upstream CI run's identifying fields and, only if all three
# are well-formed, prints the repository_dispatch request body (event_type +
# client_payload, per the shared contract above) to stdout as one JSON line.
# Fails closed on any malformed input: nothing is printed to stdout, so a
# caller that forgets to check the exit status still cannot send a
# half-built payload (the empty stdout is not valid JSON either).
rc_notify_build_payload() {
  local head_sha="${1:-}" run_id="${2:-}" run_attempt="${3:-}"

  if ! [[ "$head_sha" =~ ^[0-9a-f]{40}$ ]]; then
    echo "ERROR: workflow_run.head_sha is not a full 40-character lowercase commit SHA: '$head_sha'" >&2
    return 1
  fi
  if ! [[ "$run_id" =~ ^[0-9]+$ ]]; then
    echo "ERROR: workflow_run.id is not a decimal integer: '$run_id'" >&2
    return 1
  fi
  if ! [[ "$run_attempt" =~ ^[1-9][0-9]*$ ]]; then
    echo "ERROR: workflow_run.run_attempt is not a positive integer: '$run_attempt'" >&2
    return 1
  fi

  "$RC_NOTIFY_PYTHON_BIN" - "$head_sha" "$run_id" "$run_attempt" <<'PY'
import json
import sys

head_sha, run_id, run_attempt = sys.argv[1], sys.argv[2], sys.argv[3]

# source_run_id is a string and source_run_attempt is a number: this mirrors
# the shared contract's example payload exactly (source_sha/source_run_id as
# quoted strings, source_run_attempt as a bare integer) and must not be
# "simplified" to two strings or two numbers.
payload = {
    "event_type": "app-release-candidate-v1",
    "client_payload": {
        "component": "seo",
        "source_sha": head_sha,
        "source_run_id": run_id,
        "source_run_attempt": int(run_attempt),
    },
}
print(json.dumps(payload))
PY
}
