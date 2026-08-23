#!/usr/bin/env bash
# Shared single-flight lock for every operation that touches the production stack.
#
#   source scripts/lib/production-lock.sh
#   acquire_production_lock "$PROJECT_NAME"
#
# The lock lives outside the repository, keyed by Compose project name. Inside the checkout it
# would only exclude runs from that one checkout, while a second clone or a git worktree could
# still stop containers and migrate the same project at the same time - the project is what is
# shared, not the working copy.
#
# Re-entrant through an exported marker: deploy-production.sh holds the lock and then calls
# backup-production.sh, which must not deadlock trying to take it again.

# XDG_RUNTIME_DIR is per-user and mode 0700, so no other user can plant a symlink where the lock
# file goes. /var/lock is root-owned on systemd hosts and usually not writable by the deploying
# user, so it is not the default. /tmp is the last resort and is world-writable; the deployment
# still works there, it just relies on the VPS having a single operator.
production_lock_path() {
  local project="$1" dir="${PRODUCTION_LOCK_DIR:-${XDG_RUNTIME_DIR:-/tmp}}"
  printf '%s/%s.deploy.lock' "$dir" "$project"
}

acquire_production_lock() {
  local project="$1"

  if [[ "${PRODUCTION_LOCK_HELD:-}" == "$project" ]]; then
    return 0
  fi

  if ! command -v flock > /dev/null 2>&1; then
    echo "ERROR: flock is required so two operations cannot touch the production stack at once." >&2
    echo "       Install util-linux." >&2
    return 1
  fi

  local lock_path
  lock_path="$(production_lock_path "$project")"
  mkdir -p "$(dirname "$lock_path")"

  # The descriptor is deliberately left open for the life of the process: the lock is released
  # when the process exits, including on a kill, so a crashed run leaves nothing to clean up.
  exec {production_lock_fd}> "$lock_path"
  if ! flock --nonblock "$production_lock_fd"; then
    echo "ERROR: another operation is already working on ${project} (${lock_path} is held)." >&2
    echo "       Wait for it to finish. Two at once would dump the database during the other's" >&2
    echo "       migration, or stop containers a deployment is still using." >&2
    return 1
  fi

  export PRODUCTION_LOCK_HELD="$project"
}
