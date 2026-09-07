#!/usr/bin/env bash
# Guards one invariant in the Dockerfile: nothing a later stage needs may live only in a BuildKit
# cache mount.
#
#   bash scripts/verify-dockerfile-restore.sh
#
# The restore stage runs `dotnet tool restore` and `dotnet restore`; every stage after it runs
# --no-restore or --no-build and reads the result. A cache mount is not part of any layer, so it is
# not carried by a registry or GitHub Actions layer cache. A build that restores its layers from a
# remote cache and then re-executes one of the later stages finds nothing there:
#
#   Run "dotnet tool restore" to make the "dotnet-ef" command available.
#   MSB3030: Could not copy the file ".../dapper/2.0.123/lib/net5.0/Dapper.dll"
#
# Both were seen in CI, on pull requests only, while main stayed green - which is what a build
# depending on state outside the layer graph looks like. Reproducible locally with
# `docker build --no-cache-filter migrate-bundle .` after a warm build.
#
# This is a text check rather than a build: reproducing it properly means a full image build, which
# is not worth a CI step of its own. What it pins is the shape that caused it.
set -euo pipefail

cd "$(dirname "$0")/.."

failures=0

fail() {
  echo "FAIL: $*" >&2
  failures=$((failures + 1))
}

# The packages and the local tools have to be in the layer, because the stages below consume them
# without restoring again.
if grep -qE 'mount=type=cache[^ ]*nuget' Dockerfile; then
  fail "Dockerfile mounts the NuGet directory as a cache mount. Later stages run --no-restore and
      --no-build against it, and a cache mount is not restored by a layer cache. Either restore
      into the layer, or make every consuming stage restore for itself."
else
  echo "PASS: the NuGet directory is not a cache mount"
fi

# The other half of the pair. If these disappear the invariant above stops mattering, and a future
# reader should be told that rather than left with a check guarding nothing.
if ! grep -q -- '--no-restore' Dockerfile || ! grep -q -- '--no-build' Dockerfile; then
  fail "Dockerfile no longer uses --no-restore/--no-build. If the later stages restore for
      themselves now, this check and its comment are stale - delete them deliberately."
else
  echo "PASS: later stages still consume the restore stage's output"
fi

if [ "$failures" -ne 0 ]; then
  exit 1
fi

echo "Dockerfile restore invariant holds."
