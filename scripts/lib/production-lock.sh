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
# Scope and its limit: the lock excludes concurrent operations by the same Unix user, and only by
# that user. Deploying as one dedicated user is a requirement, not a recommendation - the lock
# directory must be owned by the caller and the lock file is created 0600, so a second deploy user
# could neither share the directory nor open the file. Two users deploying would not be serialised
# by this; they would each be refused or each proceed, depending on which of the two failures came
# first. Supporting that would mean a group-owned directory and a group-writable lock file, which
# is a different design and is not what this is.
#
# PRODUCTION_LOCK_DIR overrides where the lock lives. It exists for hosts where neither default is
# usable - Debian's /var/lock is a 1777 symlink to /run/lock, which is rejected - and for the
# restore rehearsal, which locks its own isolated stack.

# Where the lock may live. Chosen deliberately rather than defaulted into /tmp: a lock any user can
# hold or pre-create is not a lock, so no safe location means no deployment.
production_lock_dir_is_safe() {
  local dir="$1" resolved owner mode

  resolved="$(readlink -f "$dir" 2>/dev/null)" || return 1
  [[ -d "$resolved" && -w "$resolved" ]] || return 1

  # stat's format flags differ on BSD; this runs on the Linux VPS and in Linux CI.
  owner="$(stat -c '%u' "$resolved" 2>/dev/null)" || return 1
  [[ "$owner" == "$(id -u)" ]] || return 1

  # World-writable rules out /tmp and anything like it, including an XDG_RUNTIME_DIR pointed there.
  mode="$(stat -c '%a' "$resolved" 2>/dev/null)" || return 1
  (( 8#$mode & 0002 )) && return 1

  return 0
}

production_lock_path() {
  local project="$1" dir="${PRODUCTION_LOCK_DIR:-}"

  if [[ -n "$dir" ]]; then
    if ! production_lock_dir_is_safe "$dir"; then
      echo "ERROR: PRODUCTION_LOCK_DIR ($dir) is not a directory this user owns and can write to," >&2
      echo "       or it is world-writable. A lock another user can hold or pre-create is not a lock." >&2
      return 1
    fi
  elif production_lock_dir_is_safe "${XDG_RUNTIME_DIR:-}"; then
    dir="$XDG_RUNTIME_DIR"
  elif production_lock_dir_is_safe /var/lock; then
    dir="/var/lock"
  else
    echo "ERROR: no safe place to put the deployment lock." >&2
    echo "       XDG_RUNTIME_DIR and /var/lock are unusable: they must be directories this user" >&2
    echo "       owns, can write to, and that are not world-writable." >&2
    echo "       Set PRODUCTION_LOCK_DIR to such a directory." >&2
    return 1
  fi

  printf '%s/%s.deploy.lock' "$dir" "$project"
}

# Re-entrancy is verified, not merely declared. deploy-production.sh holds the lock and then calls
# backup-production.sh, which must not deadlock - but an exported marker on its own would let
# anyone skip the lock by setting an environment variable, which is exactly what the lock exists to
# make impossible.
#
# The check locks the inherited descriptor itself. Re-locking a descriptor this process already
# holds succeeds; a descriptor merely opened on the same path does not, because the real holder's
# lock is in the way. Re-opening the path and testing that instead would only prove that somebody
# holds the lock - which is equally true when the holder is a different process, and that is the
# forgery this has to reject.
production_lock_is_inherited() {
  local project="$1" lock_path="$2"

  [[ "${PRODUCTION_LOCK_HELD:-}" == "$project" ]] || return 1
  [[ -n "${PRODUCTION_LOCK_FD:-}" ]] || return 1
  [[ "${PRODUCTION_LOCK_PATH:-}" == "$lock_path" ]] || return 1

  # The descriptor must still be open on the same file.
  local fd_target="/proc/$$/fd/${PRODUCTION_LOCK_FD}"
  [[ -e "$fd_target" ]] || return 1
  [[ "$(readlink -f "$fd_target" 2>/dev/null)" == "$(readlink -f "$lock_path" 2>/dev/null)" ]] || return 1

  flock --nonblock --exclusive "${PRODUCTION_LOCK_FD}" 2>/dev/null || return 1

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

  # The lock file is only ever a lock, but it names the project and sits in a shared directory when
  # one is configured, so it is created owner-only rather than at the caller's umask.
  local previous_umask
  previous_umask="$(umask)"
  umask 077
  # The descriptor is deliberately left open for the life of the process: the lock is released when
  # the process exits, including on a kill, so a crashed run leaves nothing to clean up.
  exec {production_lock_fd}> "$lock_path"
  umask "$previous_umask"

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
