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
# Scope and its limit: the lock excludes concurrent operations by the same Unix user. Deploy as one
# dedicated user. If more than one user must deploy, point PRODUCTION_LOCK_DIR at a directory they
# all own, created in advance with restrictive permissions - do not let it fall back to a
# world-writable location, where another user could hold or pre-create the lock file.

# Chosen deliberately rather than defaulted into /tmp. /tmp is world-writable, so another user could
# hold the lock or plant the file, and a lock that anyone can hold is not a lock.
production_lock_path() {
  local project="$1" dir="${PRODUCTION_LOCK_DIR:-}"

  if [[ -z "$dir" ]]; then
    # Per-user and mode 0700, so no other user can interfere.
    if [[ -n "${XDG_RUNTIME_DIR:-}" && -d "${XDG_RUNTIME_DIR}" && -w "${XDG_RUNTIME_DIR}" ]]; then
      dir="$XDG_RUNTIME_DIR"
    elif [[ -d /var/lock && -w /var/lock ]]; then
      dir="/var/lock"
    else
      echo "ERROR: no safe place to put the deployment lock." >&2
      echo "       XDG_RUNTIME_DIR is unset or not writable and /var/lock is not writable." >&2
      echo "       Set PRODUCTION_LOCK_DIR to a directory only the deploying user can write to." >&2
      return 1
    fi
  fi

  printf '%s/%s.deploy.lock' "$dir" "$project"
}

# Re-entrancy is verified, not merely declared. deploy-production.sh holds the lock and then calls
# backup-production.sh, which must not deadlock - but an exported marker on its own would let
# anyone skip the lock by setting an environment variable, which is exactly what the lock exists to
# make impossible. The inherited descriptor has to still point at the lock file and still hold it.
production_lock_is_inherited() {
  local project="$1" lock_path="$2"

  [[ "${PRODUCTION_LOCK_HELD:-}" == "$project" ]] || return 1
  [[ -n "${PRODUCTION_LOCK_FD:-}" ]] || return 1
  [[ "${PRODUCTION_LOCK_PATH:-}" == "$lock_path" ]] || return 1

  # The descriptor must be open and still refer to the same file. Without /proc this cannot be
  # checked, so the claim is not accepted and the lock is taken normally instead.
  local fd_target="/proc/$$/fd/${PRODUCTION_LOCK_FD}"
  [[ -e "$fd_target" ]] || return 1
  [[ "$(readlink -f "$fd_target" 2>/dev/null)" == "$(readlink -f "$lock_path" 2>/dev/null)" ]] || return 1

  # And it must actually be locked. A fresh non-blocking attempt on a second descriptor succeeds
  # only if nobody holds it, so success here means the marker was describing a lock that is not
  # there.
  if flock --nonblock --exclusive "$lock_path" true 2>/dev/null; then
    return 1
  fi

  return 0
}

acquire_production_lock() {
  local project="$1" lock_path

  if ! command -v flock > /dev/null 2>&1; then
    echo "ERROR: flock is required so two operations cannot touch the production stack at once." >&2
    echo "       Install util-linux." >&2
    return 1
  fi

  lock_path="$(production_lock_path "$project")" || return 1

  if production_lock_is_inherited "$project" "$lock_path"; then
    return 0
  fi

  if [[ -n "${PRODUCTION_LOCK_HELD:-}" ]]; then
    echo "ERROR: PRODUCTION_LOCK_HELD is set but no inherited lock backs it." >&2
    echo "       The lock cannot be claimed by setting an environment variable." >&2
    return 1
  fi

  mkdir -p "$(dirname "$lock_path")"

  # The descriptor is deliberately left open for the life of the process: the lock is released when
  # the process exits, including on a kill, so a crashed run leaves nothing to clean up.
  exec {production_lock_fd}> "$lock_path"
  if ! flock --nonblock "$production_lock_fd"; then
    echo "ERROR: another operation is already working on ${project} (${lock_path} is held)." >&2
    echo "       Wait for it to finish. Two at once would dump the database during the other's" >&2
    echo "       migration, or stop containers a deployment is still using." >&2
    return 1
  fi

  export PRODUCTION_LOCK_HELD="$project"
  export PRODUCTION_LOCK_FD="$production_lock_fd"
  export PRODUCTION_LOCK_PATH="$lock_path"
}
