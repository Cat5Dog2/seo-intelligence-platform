#!/usr/bin/env bash
# Tests scripts/lib/source-revision.sh.
#
#   bash scripts/verify-source-revision.sh
#
# The revision this resolves is stamped into the application images and is the only record of which
# commit a running container came from. The restore rehearsal also uses it to decide whether an
# already-built image may be reused. Both uses collapse if the value can be anything the caller's
# environment says it is, so the precedence and the dirty-tree handling are pinned here.
#
# Each case runs in a scratch repository of its own, because the answer depends on the state of the
# working tree the process is standing in.
set -euo pipefail

cd "$(dirname "$0")/.."
repo_root="$PWD"

if ! command -v git > /dev/null 2>&1; then
  echo "skip: these checks need git."
  exit 0
fi

work="artifacts/revision-test-$$-${RANDOM}"
mkdir -p "$work"
trap 'rm -rf "$work"' EXIT

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

# Sourced and called in a child process so each case sees its own working directory, and so an
# environment variable set for one case cannot leak into the next.
cat > "$work/resolve.sh" <<'RESOLVE'
#!/usr/bin/env bash
source "$1/scripts/lib/source-revision.sh"
resolve_source_revision
RESOLVE

# Reports REFUSED rather than the value when resolution fails, so a case can assert that the
# library declined to answer instead of answering wrongly.
cat > "$work/resolve-or-refuse.sh" <<'REFUSE'
#!/usr/bin/env bash
source "$1/scripts/lib/source-revision.sh"
if resolved="$(resolve_source_revision 2>/dev/null)"; then
  printf '%s' "$resolved"
else
  printf 'REFUSED'
fi
REFUSE

cat > "$work/is-exact.sh" <<'EXACT'
#!/usr/bin/env bash
source "$1/scripts/lib/source-revision.sh"
source_revision_is_exact "$2" && echo EXACT || echo INEXACT
EXACT

# A repository with one commit and nothing else going on.
scratch="$repo_root/$work/checkout"
mkdir -p "$scratch"
git -C "$scratch" init --quiet
# The scratch repository is only ever read by this test; line-ending translation would add a
# warning on every git call on Windows and nothing else.
git -C "$scratch" config core.autocrlf false
printf 'content\n' > "$scratch/tracked.txt"
git -C "$scratch" add tracked.txt
git -C "$scratch" -c user.email=test@localhost -c user.name=test commit --quiet -m "initial"
head_sha="$(git -C "$scratch" rev-parse HEAD)"

resolve_in() {
  local directory="$1"
  shift
  ( cd "$directory" && env "$@" bash "$repo_root/$work/resolve.sh" "$repo_root" )
}

# --- git decides where there is git -----------------------------------------------------------

check "a clean checkout resolves to HEAD" "$head_sha" \
  "$(resolve_in "$scratch" -u SOURCE_REVISION)"

# The whole point. A value left over in a shell from an earlier build, or set by someone who wants
# an image to claim a commit it was not built from, must not win over the repository in front of it.
check "an inherited SOURCE_REVISION does not override git" "$head_sha" \
  "$(resolve_in "$scratch" SOURCE_REVISION=0000000000000000000000000000000000000000)"

# --- a working tree that is not the commit -------------------------------------------------------

printf 'changed\n' >> "$scratch/tracked.txt"
check "a modified tracked file is reported as dirty" "${head_sha}-dirty" \
  "$(resolve_in "$scratch" -u SOURCE_REVISION)"
git -C "$scratch" checkout --quiet -- tracked.txt

# Untracked files are in the build context unless .dockerignore excludes them, so they are in the
# image: an image built from this tree is not the commit either.
printf 'stray\n' > "$scratch/untracked.txt"
check "an untracked file is reported as dirty" "${head_sha}-dirty" \
  "$(resolve_in "$scratch" -u SOURCE_REVISION)"
rm -f "$scratch/untracked.txt"

check "the tree is clean again" "$head_sha" \
  "$(resolve_in "$scratch" -u SOURCE_REVISION)"

# --- an exported tree, where there is no git to ask ------------------------------------------------

exported="$repo_root/$work/exported"
mkdir -p "$exported"
printf 'content\n' > "$exported/tracked.txt"

# Deliberately without GIT_CEILING_DIRECTORIES. This directory sits inside the repository the test
# is running from, which is exactly the situation that matters: an implementation that asks git
# "are we in a checkout?" gets yes, from the repository above, and labels an exported tree with a
# commit that has nothing to do with its contents.
check "an explicit value is honoured where the tree itself is not a checkout" "abc123" \
  "$(resolve_in "$exported" SOURCE_REVISION=abc123)"

check "no checkout and no value resolves to unknown" "unknown" \
  "$(resolve_in "$exported" -u SOURCE_REVISION)"

# --- git that will not answer -------------------------------------------------------------------

resolve_or_refuse_in() {
  local directory="$1"
  shift
  ( cd "$directory" && env "$@" bash "$repo_root/$work/resolve-or-refuse.sh" "$repo_root" )
}

# `git status` reads the index; HEAD does not. A corrupt index therefore leaves HEAD answerable
# while the state of the working tree is unknowable - and a failing status prints nothing on
# stdout, which is exactly what a clean tree prints. Treating that as clean would stamp a bare sha
# onto an image whose contents nobody could account for.
printf 'not-an-index' > "$work/corrupt-index"
check "a failing git status is refused, not read as clean" REFUSED \
  "$(resolve_or_refuse_in "$scratch" -u SOURCE_REVISION GIT_INDEX_FILE="$repo_root/$work/corrupt-index")"

# A repository with no commit: .git is there, HEAD resolves to nothing. This must not be mistaken
# for an exported tree, or an inherited value would be accepted for a checkout that is simply
# broken - the one case where a caller-supplied revision is least likely to be true.
unborn="$repo_root/$work/unborn"
mkdir -p "$unborn"
git -C "$unborn" init --quiet
git -C "$unborn" config core.autocrlf false
check "a checkout with no resolvable HEAD does not fall back to SOURCE_REVISION" REFUSED \
  "$(resolve_or_refuse_in "$unborn" SOURCE_REVISION=1111111111111111111111111111111111111111)"

# --- git pointed somewhere else ---------------------------------------------------------------

# The environment must not be able to move the answer to another repository. What gets built is the
# source root, so the source root's own .git is what has to describe it - a GIT_DIR left over from
# some other tool would otherwise label the image with a commit from a different project entirely.
check "a misconfigured GIT_DIR does not move the answer" "$head_sha" \
  "$(resolve_in "$scratch" -u SOURCE_REVISION GIT_DIR=/nonexistent/path.git)"

check "a misconfigured GIT_DIR does not open the door to an inherited value" "$head_sha" \
  "$(resolve_in "$scratch" GIT_DIR=/nonexistent/path.git SOURCE_REVISION=2222222222222222222222222222222222222222)"

other="$repo_root/$work/other-checkout"
mkdir -p "$other"
git -C "$other" init --quiet
git -C "$other" config core.autocrlf false
printf 'elsewhere\n' > "$other/file.txt"
git -C "$other" add file.txt
git -C "$other" -c user.email=test@localhost -c user.name=test commit --quiet -m "other"

check "GIT_WORK_TREE cannot redirect the answer to another checkout" "$head_sha" \
  "$(resolve_in "$scratch" -u SOURCE_REVISION GIT_WORK_TREE="$other")"

# --- git that warns but succeeds -------------------------------------------------------------------

# An unreadable core.excludesFile makes git print a warning and carry on. Warnings go to stderr and
# the porcelain listing to stdout; an implementation that reads them together sees a non-empty
# "listing" and calls a clean tree dirty. Simulated with a wrapper rather than by breaking a config
# file, because which conditions produce a warning differs between git versions and platforms.
real_git="$(type -P git)"
mkdir -p "$work/bin"
cat > "$work/bin/git" <<GITSTUB
#!/usr/bin/env bash
echo "warning: unable to access '/nonexistent/excludes': No such file or directory" >&2
exec "$real_git" "\$@"
GITSTUB
chmod +x "$work/bin/git"

check "a warning on stderr does not make a clean tree dirty" "$head_sha" \
  "$(resolve_in "$scratch" -u SOURCE_REVISION PATH="$repo_root/$work/bin:$PATH")"

# --- what counts as an exact revision --------------------------------------------------------------

exact() { bash "$work/is-exact.sh" "$repo_root" "$1"; }

check "a bare sha is exact" EXACT "$(exact "$head_sha")"
check "a dirty revision is not exact" INEXACT "$(exact "${head_sha}-dirty")"
check "unknown is not exact" INEXACT "$(exact unknown)"
check "an abbreviated sha is not exact" INEXACT "$(exact "${head_sha:0:12}")"
check "an empty revision is not exact" INEXACT "$(exact "")"

if [[ "$failures" -ne 0 ]]; then
  echo "$failures revision check(s) failed." >&2
  exit 1
fi

echo "The image revision comes from git, and says so when the tree is not the commit."
