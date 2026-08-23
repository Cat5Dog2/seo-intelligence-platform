#!/usr/bin/env bash
# Resolves the revision stamped into the application images as
# org.opencontainers.image.revision.
#
#   source scripts/lib/source-revision.sh
#   revision="$(resolve_source_revision)"
#
# Git decides whenever there is a git checkout to ask, and an inherited SOURCE_REVISION is ignored
# there. The label exists so a running container can be traced back to a commit, and so the restore
# rehearsal can check mechanically that a reused image was built from the checkout it is verifying;
# both are worth exactly nothing if a stale value left in a shell can name any commit it likes. An
# explicit value is honoured only where there is no git to ask - an exported tree, which is how the
# rehearsal is run from a tarball.
#
# A working tree with changes is reported as <sha>-dirty rather than refused. Refusing would block
# a deployment during an incident, which is the wrong trade for a label; reporting the bare sha
# would be a lie, because the image contains something no commit does. Untracked files count:
# unless .dockerignore excludes them they are in the build context, so they are in the image.

resolve_source_revision() {
  local head worktree status_code=0

  # Asked separately from reading HEAD. "There is no git here" and "there is git here and it will
  # not answer" are different situations, and only the first one may fall back to an explicit
  # value: a repository that cannot say what it is checked out at is not an exported tree.
  if ! git rev-parse --git-dir > /dev/null 2>&1; then
    printf '%s' "${SOURCE_REVISION:-unknown}"
    return 0
  fi

  if ! head="$(git rev-parse HEAD 2>/dev/null)"; then
    echo "ERROR: this is a git checkout, but HEAD cannot be resolved." >&2
    echo "       Refusing to fall back to SOURCE_REVISION here - a repository with no resolvable" >&2
    echo "       HEAD is broken or has no commits, not an exported tree." >&2
    return 1
  fi

  # The exit code is checked, not just the output. A failing `git status` - a corrupt index, a
  # permission problem, a GIT_INDEX_FILE pointing somewhere unusable - prints nothing on stdout and
  # exits non-zero, which read as output alone is indistinguishable from a clean tree. That is the
  # worst possible way to be wrong here: it would stamp a bare sha onto an image whose contents
  # nobody could verify.
  worktree="$(git status --porcelain 2>&1)" || status_code=$?
  if [[ "$status_code" -ne 0 ]]; then
    echo "ERROR: git status failed (exit ${status_code}), so the working tree cannot be shown to" >&2
    echo "       match ${head}." >&2
    sed 's/^/       /' <<< "$worktree" >&2
    return 1
  fi

  if [[ -n "$worktree" ]]; then
    printf '%s-dirty' "$head"
  else
    printf '%s' "$head"
  fi
}

# True only for a revision that identifies exactly one source state. "unknown" identifies nothing,
# and <sha>-dirty identifies a commit plus changes that were never recorded - two different dirty
# trees produce the same string, so comparing them proves nothing.
source_revision_is_exact() {
  [[ "$1" =~ ^[0-9a-f]{40}$ ]]
}
