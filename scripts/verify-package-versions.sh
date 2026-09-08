#!/usr/bin/env bash
# Guards the invariants that make central package management mean anything.
#
#   bash scripts/verify-package-versions.sh
#
# Directory.Packages.props exists because Microsoft.EntityFrameworkCore used to have no stated
# version at all: it arrived transitively, and because Microsoft.EntityFrameworkCore.Design carries
# PrivateAssets=all and does not flow to consumers, the answer differed per project. Raising Design
# raised what Infrastructure compiled against while Web and ContractTests kept the older version
# they saw through Identity.EntityFrameworkCore, and the build failed with CS1705 in two projects
# that nothing had touched.
#
# Most of that is now enforced by NuGet itself - a Version left on a PackageReference is NU1008, a
# VersionOverride is NU1013 - so this script does not re-check what restore already refuses. What
# it pins are the three things nothing else would notice:
#
#   - the settings that make NuGet refuse those, which can be deleted without any build failing;
#   - the Dockerfile copying the props file, without which restore inside the image resolves
#     nothing (CI would catch it, but in the image build, minutes later and less legibly).
#
# A text check rather than a build, for the same reason as verify-dockerfile-restore.sh: what
# matters is the shape, and reproducing the failure properly costs a full restore per case.
set -euo pipefail

cd "$(dirname "$0")/.."

failures=0

fail() {
  echo "FAIL: $*" >&2
  failures=$((failures + 1))
}

props="Directory.Packages.props"

if [[ ! -f "$props" ]]; then
  fail "$props is missing. Every version would fall back to whatever each csproj says, which is
      the arrangement that produced CS1705."
  echo "$failures guard(s) did not catch their case." >&2
  exit 1
fi

if grep -qE '<ManagePackageVersionsCentrally>[[:space:]]*true[[:space:]]*<' "$props"; then
  echo "PASS: package versions are managed centrally"
else
  fail "$props does not set ManagePackageVersionsCentrally to true, so the PackageVersion entries
      in it are inert and each csproj decides its own version again."
fi

# The escape hatch. Measured before closing it: with StackExchange.Redis pinned at 2.13.17 in the
# props file, VersionOverride=3.1.31 on one csproj restored cleanly and resolved to 3.1.31, with no
# error and no warning. Nothing but review would have seen it.
if grep -qE '<CentralPackageVersionOverrideEnabled>[[:space:]]*false[[:space:]]*<' "$props"; then
  echo "PASS: per-project VersionOverride is refused"
else
  fail "$props does not set CentralPackageVersionOverrideEnabled to false. A csproj can then carry
      VersionOverride=\"...\" and NuGet silently prefers it over the central version, which is the
      per-project divergence this file exists to remove."
fi

# Ordering matters, not just presence: the copy has to precede the restore that reads it.
copy_line="$(grep -nE '^COPY[[:space:]]+Directory\.Packages\.props' Dockerfile | head -1 | cut -d: -f1 || true)"
restore_line="$(grep -nE '^[^#]*dotnet restore ' Dockerfile | head -1 | cut -d: -f1 || true)"

if [[ -z "$copy_line" ]]; then
  fail "Dockerfile never copies $props into the restore stage. With central package management on,
      no package inside the image has a version, and restore fails before it reaches the first
      project."
elif [[ -z "$restore_line" ]]; then
  fail "Dockerfile no longer runs 'dotnet restore', so this check cannot tell whether $props
      arrives in time. Update the check with whatever replaced it."
elif [[ "$copy_line" -gt "$restore_line" ]]; then
  fail "Dockerfile copies $props at line $copy_line, after the restore at line $restore_line."
else
  echo "PASS: the Dockerfile copies $props before restoring"
fi

if [[ "$failures" -ne 0 ]]; then
  echo "$failures guard(s) did not catch their case." >&2
  exit 1
fi

echo "Central package management checks passed."
