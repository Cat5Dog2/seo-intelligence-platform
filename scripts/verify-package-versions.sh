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
# VersionOverride is NU1013 - but only while every project evaluates the central settings to the
# reviewed values. Directory.Packages.props is imported early enough that a project can redefine a
# property, and a closer Directory.Packages.props can shadow this one. This script therefore checks
# the evaluated settings for every project, not merely that the expected text occurs in the root
# file. It also pins the Docker restore input that nothing else would notice early:
#
#   - the settings that make NuGet refuse those, which can be deleted without any build failing;
#   - the Dockerfile copying the props file, without which restore inside the image resolves
#     nothing (CI would catch it, but in the image build, minutes later and less legibly).
#
# MSBuild property evaluation does not restore packages. The Dockerfile check stays textual for the
# same reason as verify-dockerfile-restore.sh: what matters there is the shape, and reproducing each
# failure properly costs a full container restore.
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
  echo "$failures central package management invariant(s) failed." >&2
  exit 1
fi

package_props=()
if package_props_listing="$(
  find . \
    \( -path './.git' -o -path './artifacts' -o -name bin -o -name obj \) -prune -o \
    \( -type f -o -type l \) -name Directory.Packages.props -print
)"; then
  while IFS= read -r candidate; do
    [[ -n "$candidate" ]] && package_props+=("${candidate#./}")
  done <<< "$package_props_listing"
else
  fail "could not enumerate Directory.Packages.props files, so their uniqueness is unknown."
fi

if [[ "${#package_props[@]}" -ne 1 || "${package_props[0]:-}" != "$props" ]]; then
  fail "exactly one Directory.Packages.props must exist at the repository root; found:
      ${package_props[*]:-(none)}"
else
  echo "PASS: the repository has one central package file"
fi

projects=()
if projects_listing="$(
  find src tests \
    \( -name bin -o -name obj \) -prune -o \
    -type f -name '*.csproj' -print
)"; then
  while IFS= read -r project; do
    [[ -n "$project" ]] && projects+=("${project#./}")
  done <<< "$projects_listing"
else
  fail "could not enumerate csproj files, so their central settings are unknown."
fi

if [[ "${#projects[@]}" -eq 0 ]]; then
  fail "no csproj files were found, so the central settings were not evaluated anywhere."
elif ! command -v dotnet > /dev/null 2>&1; then
  fail "dotnet is required to evaluate central package settings for every project."
else
  # Counted from here rather than from zero: the PASS below reports on the projects alone.
  # Reading the global counter would hide their result whenever an unrelated check above had
  # already failed, leaving no way to tell whether they were evaluated at all.
  failures_before_projects="$failures"

  for project in "${projects[@]}"; do
    if manage="$(dotnet msbuild "$project" -nologo \
        -getProperty:ManagePackageVersionsCentrally 2>&1)"; then
      manage="${manage//$'\r'/}"
      if [[ "$manage" != "true" ]]; then
        fail "$project evaluates ManagePackageVersionsCentrally as '$manage', expected 'true'."
      fi
    else
      manage="${manage//$'\r'/}"
      fail "could not evaluate ManagePackageVersionsCentrally for $project: $manage"
    fi

    # The escape hatch. Measured before closing it: with StackExchange.Redis pinned at 2.13.17 in
    # the props file, VersionOverride=3.1.31 on one csproj restored cleanly and resolved to 3.1.31,
    # with no error and no warning. Nothing but review would have seen it.
    if override="$(dotnet msbuild "$project" -nologo \
        -getProperty:CentralPackageVersionOverrideEnabled 2>&1)"; then
      override="${override//$'\r'/}"
      if [[ "$override" != "false" ]]; then
        fail "$project evaluates CentralPackageVersionOverrideEnabled as '$override', expected
            'false'. A project could otherwise silently prefer VersionOverride over the central
            version."
      fi
    else
      override="${override//$'\r'/}"
      fail "could not evaluate CentralPackageVersionOverrideEnabled for $project: $override"
    fi
  done

  if [[ "$failures" -eq "$failures_before_projects" ]]; then
    echo "PASS: every project enables central versions and refuses VersionOverride"
  fi
fi

# Ordering, stage, working directory, and destination all matter. A COPY in an unrelated stage or
# to a directory that NuGet will not search is no better than no copy at all.
restore_stage_pattern='^[[:space:]]*FROM([[:space:]]+--[^[:space:]]+)*[[:space:]]+[^[:space:]]+[[:space:]]+AS[[:space:]]+restore([[:space:]]|$)'
restore_stage_count="$(grep -ciE "$restore_stage_pattern" Dockerfile || true)"

if [[ "$restore_stage_count" -ne 1 ]]; then
  fail "Dockerfile must contain exactly one stage named restore; found $restore_stage_count."
else
  restore_stage_start="$(grep -niE "$restore_stage_pattern" Dockerfile | head -1 | cut -d: -f1)"
  restore_stage_end="$(awk -v start="$restore_stage_start" \
    'NR > start && /^[[:space:]]*FROM[[:space:]]/ { print NR; exit }' Dockerfile)"
  if [[ -z "$restore_stage_end" ]]; then
    restore_stage_end=$(( $(wc -l < Dockerfile) + 1 ))
  fi

  workdir_line="$(awk -v start="$restore_stage_start" -v end="$restore_stage_end" \
    'NR > start && NR < end && /^[[:space:]]*WORKDIR[[:space:]]+\/src[[:space:]]*$/ { print NR; exit }' \
    Dockerfile)"
  copy_line="$(awk -v start="$restore_stage_start" -v end="$restore_stage_end" \
    'NR > start && NR < end && /^[[:space:]]*COPY[[:space:]]+Directory\.Packages\.props[[:space:]]+\.\/[[:space:]]*$/ { print NR; exit }' \
    Dockerfile)"
  restore_line="$(awk -v start="$restore_stage_start" -v end="$restore_stage_end" \
    'NR > start && NR < end && /^[^#]*dotnet restore[[:space:]]/ { print NR; exit }' Dockerfile)"

  if [[ -z "$copy_line" ]]; then
    fail "Dockerfile's restore stage does not copy $props to ./. With central package management
        on, restore cannot resolve package versions from any other stage or an unrelated path."
  elif [[ -z "$workdir_line" || "$workdir_line" -gt "$copy_line" ]]; then
    fail "Dockerfile's restore stage does not establish WORKDIR /src before copying $props."
  elif [[ -z "$restore_line" ]]; then
    fail "Dockerfile's restore stage no longer runs 'dotnet restore', so this check cannot tell
        whether $props arrives in time. Update the check with whatever replaced it."
  elif [[ "$copy_line" -gt "$restore_line" ]]; then
    fail "Dockerfile copies $props at line $copy_line, after the restore at line $restore_line."
  else
    echo "PASS: the restore stage copies $props to /src before restoring"
  fi
fi

if [[ "$failures" -ne 0 ]]; then
  echo "$failures central package management invariant(s) failed." >&2
  exit 1
fi

echo "Central package management checks passed."
