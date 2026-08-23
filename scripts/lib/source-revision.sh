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
  local head

  if head="$(git rev-parse HEAD 2>/dev/null)"; then
    if [[ -n "$(git status --porcelain 2>/dev/null)" ]]; then
      printf '%s-dirty' "$head"
    else
      printf '%s' "$head"
    fi
    return 0
  fi

  printf '%s' "${SOURCE_REVISION:-unknown}"
}

# True only for a revision that identifies exactly one source state. "unknown" identifies nothing,
# and <sha>-dirty identifies a commit plus changes that were never recorded - two different dirty
# trees produce the same string, so comparing them proves nothing.
source_revision_is_exact() {
  [[ "$1" =~ ^[0-9a-f]{40}$ ]]
}
