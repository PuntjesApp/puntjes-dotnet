#!/bin/sh
# Fails when the public API of the SDK no longer matches the latest nuget.org release, unless a breaking commit
# is announced: a PR title like "feat!: ...", a BREAKING CHANGE footer, or such a commit on main since that release.
# Env: TITLE and BODY (the pull request, empty on a push), HEAD_BASE (the commit the history is read up to).
set -eu
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
base=$(curl -fsSL --retry 5 --retry-all-errors https://api.nuget.org/v3-flatcontainer/puntjes.sdk/index.json \
  | jq -r '[.versions[] | select(contains("-") | not)] | last')
test -n "$base" && test "$base" != null

announced() {
  { printf '%s\n' "${TITLE:-}"; printf '%s\n' "${BODY:-}"; git log --format='%s%n%b' "v$base..${HEAD_BASE:-HEAD}"; } \
    | grep -Eq '^([a-z]+(\([^)]*\))?!:|BREAKING[ -]CHANGE)'
}

if dotnet pack src/Puntjes.Sdk --configuration Release --output "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/api-compat" \
  -p:EnablePackageValidation=true -p:PackageValidationBaselineVersion="$base"; then
  echo "The public API matches Puntjes.Sdk $base."
elif announced; then
  echo "::notice::The public API breaks Puntjes.Sdk $base, and a breaking commit announces it."
else
  echo "::error::The public API breaks Puntjes.Sdk $base (the CP errors above list each break). A break needs a major version: retitle the PR to 'feat!: ...' and turn auto-merge off so a person reviews it. A regenerate PR is retitled the same way."
  exit 1
fi
