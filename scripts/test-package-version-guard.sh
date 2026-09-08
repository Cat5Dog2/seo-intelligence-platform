#!/usr/bin/env bash
# Regression tests for scripts/verify-package-versions.sh.
set -euo pipefail

cd "$(dirname "$0")/.."
repo_root="$PWD"
work="$repo_root/artifacts/package-version-guard-test-$$-${RANDOM}"

cleanup() {
  case "$work" in
    "$repo_root"/artifacts/package-version-guard-test-*) rm -rf -- "$work" ;;
    *) echo "REFUSING: unexpected package-version test path: $work" >&2 ;;
  esac
}
trap cleanup EXIT

base="$work/base"
mkdir -p "$base/scripts" "$base/src/Test" "$base/tests"
cp scripts/verify-package-versions.sh "$base/scripts/verify-package-versions.sh"

cat > "$base/Directory.Packages.props" <<'PROPS'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Example.Package" Version="1.0.0" />
  </ItemGroup>
</Project>
PROPS

cat > "$base/src/Test/Test.csproj" <<'PROJECT'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Example.Package" />
  </ItemGroup>
</Project>
PROJECT

cat > "$base/Dockerfile" <<'DOCKERFILE'
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY Directory.Packages.props ./
COPY src/Test/Test.csproj src/Test/
RUN dotnet restore src/Test/Test.csproj
DOCKERFILE

failures=0

pass() {
  echo "ok: $1"
}

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

new_case() {
  local name="$1" case_root="$work/$1"
  cp -R "$base" "$case_root"
  printf '%s' "$case_root"
}

expect_success() {
  local case_root="$1" description="$2" output
  if output="$(cd "$case_root" && bash scripts/verify-package-versions.sh 2>&1)"; then
    pass "$description"
  else
    fail "$description (command failed: $output)"
  fi
}

expect_rejection() {
  local case_root="$1" description="$2" expected="$3" output
  if output="$(cd "$case_root" && bash scripts/verify-package-versions.sh 2>&1)"; then
    fail "$description (command succeeded)"
  elif [[ "$output" == *"$expected"* ]]; then
    pass "$description"
  else
    fail "$description (expected '$expected' in: $output)"
  fi
}

baseline="$(new_case baseline)"
expect_success "$baseline" "the reviewed central-package layout passes"

project_opt_out="$(new_case project-opt-out)"
sed -i '/<TargetFramework>/a\    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>' \
  "$project_opt_out/src/Test/Test.csproj"
sed -i 's#<PackageReference Include="Example.Package" />#<PackageReference Include="Example.Package" Version="1.0.0" />#' \
  "$project_opt_out/src/Test/Test.csproj"
expect_rejection "$project_opt_out" \
  "a project cannot opt out of central package management" \
  "ManagePackageVersionsCentrally as 'false'"

project_override="$(new_case project-override)"
sed -i '/<TargetFramework>/a\    <CentralPackageVersionOverrideEnabled>true</CentralPackageVersionOverrideEnabled>' \
  "$project_override/src/Test/Test.csproj"
sed -i 's#<PackageReference Include="Example.Package" />#<PackageReference Include="Example.Package" VersionOverride="2.0.0" />#' \
  "$project_override/src/Test/Test.csproj"
expect_rejection "$project_override" \
  "a project cannot reopen VersionOverride" \
  "CentralPackageVersionOverrideEnabled as 'true'"

nested_props="$(new_case nested-props)"
cp "$nested_props/Directory.Packages.props" "$nested_props/src/Directory.Packages.props"
expect_rejection "$nested_props" \
  "a nested Directory.Packages.props cannot shadow the repository root" \
  "exactly one Directory.Packages.props"

commented_setting="$(new_case commented-setting)"
sed -i 's#    <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>#    <!-- <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled> -->#' \
  "$commented_setting/Directory.Packages.props"
expect_rejection "$commented_setting" \
  "a commented setting does not satisfy the guard" \
  "CentralPackageVersionOverrideEnabled as"

later_override="$(new_case later-override)"
sed -i '/<CentralPackageVersionOverrideEnabled>false/a\    <CentralPackageVersionOverrideEnabled>true</CentralPackageVersionOverrideEnabled>' \
  "$later_override/Directory.Packages.props"
expect_rejection "$later_override" \
  "a later property assignment cannot reverse the reviewed value" \
  "CentralPackageVersionOverrideEnabled as 'true'"

wrong_destination="$(new_case wrong-destination)"
sed -i 's#COPY Directory.Packages.props ./#COPY Directory.Packages.props /discard/#' \
  "$wrong_destination/Dockerfile"
expect_rejection "$wrong_destination" \
  "the props file must be copied to the restore working directory" \
  "does not copy Directory.Packages.props to ./"

wrong_stage="$(new_case wrong-stage)"
sed -i '/COPY Directory.Packages.props/d' "$wrong_stage/Dockerfile"
sed -i '1iFROM scratch AS unrelated\nCOPY Directory.Packages.props ./' "$wrong_stage/Dockerfile"
expect_rejection "$wrong_stage" \
  "a copy in another Docker stage does not satisfy the restore stage" \
  "restore stage does not copy Directory.Packages.props"

wrong_workdir="$(new_case wrong-workdir)"
sed -i 's#WORKDIR /src#WORKDIR /elsewhere#' "$wrong_workdir/Dockerfile"
expect_rejection "$wrong_workdir" \
  "the props file must be copied under the restore source root" \
  "does not establish WORKDIR /src"

late_copy="$(new_case late-copy)"
sed -i '/COPY Directory.Packages.props/d' "$late_copy/Dockerfile"
sed -i '/RUN dotnet restore/aCOPY Directory.Packages.props ./' "$late_copy/Dockerfile"
expect_rejection "$late_copy" \
  "the props file must be copied before the restore that consumes it" \
  "after the restore"

if [[ "$failures" -ne 0 ]]; then
  echo "$failures package-version guard regression test(s) failed." >&2
  exit 1
fi

echo "Package-version guard regression tests passed."
