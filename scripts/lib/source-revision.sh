#!/usr/bin/env bash
# Resolves the revision stamped into the application images as
# org.opencontainers.image.revision.
#
#   cd <source root>
#   source scripts/lib/source-revision.sh
#   revision="$(resolve_source_revision)" || exit 1
#
# Answers for the current directory, which callers set to the source root - the directory whose
# contents go into the image. Nothing else would be meaningful: the label describes what was built.
#
# Git decides whenever the source root is a checkout, and an inherited SOURCE_REVISION is ignored
# there. The label exists so a running container can be traced back to a commit, and so the restore
# rehearsal can check mechanically that a reused image was built from the checkout it is verifying;
# both are worth exactly nothing if a stale value left in a shell can name any commit it likes. An
# explicit value is honoured only where there is no checkout to ask.
#
# A working tree with changes is reported as <sha>-dirty rather than refused. Refusing would block
# a deployment during an incident, which is the wrong trade for a label; reporting the bare sha
# would be a lie, because the image contains something no commit does. Untracked files count:
# unless .dockerignore excludes them they are in the build context, so they are in the image.
#
# Everything else - git that will not answer, or answers about somewhere else - is refused rather
# than guessed at.

# Repository-selection variables are cleared for every call. GIT_DIR or GIT_WORK_TREE left in the
# environment points git at some other repository, which would then describe something other than
# the tree being built.
git_at_source_root() {
  env -u GIT_DIR -u GIT_WORK_TREE -u GIT_COMMON_DIR git "$@"
}

resolve_source_revision() {
  local head worktree status_code=0

  # The source root's own .git decides. `git rev-parse --git-dir` cannot: it answers for the
  # nearest repository *above* here as well, so an exported tree unpacked inside somebody's
  # checkout would be labelled with that checkout's HEAD, and it also fails for a misconfigured
  # GIT_DIR or unreadable metadata - which is not the same thing as "there is no git here".
  # A .git entry is a directory normally and a file in a worktree or submodule.
  if [[ ! -e .git ]]; then
    printf '%s' "${SOURCE_REVISION:-unknown}"
    return 0
  fi

  if ! head="$(git_at_source_root rev-parse HEAD 2>/dev/null)"; then
    echo "ERROR: $PWD is a git checkout, but HEAD cannot be resolved." >&2
    echo "       Refusing to fall back to SOURCE_REVISION here - a repository with no resolvable" >&2
    echo "       HEAD is broken or has no commits, not an exported tree. git reports:" >&2
    git_at_source_root rev-parse HEAD > /dev/null || true
    return 1
  fi

  # stdout only, and the exit code separately.
  #
  # Not `2>&1`: git writes warnings to stderr on runs that succeed - an unreadable
  # core.excludesFile is one - and folding those into the porcelain output turns a clean tree into
  # <sha>-dirty. Not stdout alone either: a failing `git status` prints nothing on stdout and exits
  # non-zero, which read as output alone is indistinguishable from a clean tree. That is the worst
  # available way to be wrong here, because it would stamp a bare sha onto an image whose contents
  # nobody could account for.
  worktree="$(git_at_source_root status --porcelain 2>/dev/null)" || status_code=$?
  if [[ "$status_code" -ne 0 ]]; then
    echo "ERROR: git status failed (exit ${status_code}), so the working tree cannot be shown to" >&2
    echo "       match ${head}. git reports:" >&2
    git_at_source_root status --porcelain > /dev/null || true
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
