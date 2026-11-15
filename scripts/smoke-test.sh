#!/usr/bin/env bash
# Runs ef-migration-lint against samples/ and checks the exit codes.
#
# Usage (from the repository root):
#   bash scripts/smoke-test.sh .tools/ef-migration-lint
#   bash scripts/smoke-test.sh dotnet run --project src/EfMigrationLint -f net10.0 --
set -u

if [ "$#" -eq 0 ]; then
  echo "usage: $0 <ef-migration-lint command...>" >&2
  exit 2
fi

tool=("$@")
failures=0

expect() {
  local expected=$1
  shift
  "${tool[@]}" "$@" > /dev/null
  local actual=$?
  if [ "$actual" -eq "$expected" ]; then
    echo "ok    exit $actual  $*"
  else
    echo "FAIL  exit $actual (expected $expected)  $*"
    failures=$((failures + 1))
  fi
}

expect 0 samples/Safe
expect 1 samples/Destructive/Migrations/20260111090000_RenameBioToBiography.cs
expect 0 samples/Safe/Migrations/20260111090000_RenameBioToBiography.cs
expect 0 samples/NeedsReview
expect 1 --warnings-as-errors samples/NeedsReview
expect 0 samples/EdgeCases
expect 1 "samples/Paths With Spaces/Migrations/20260116090000_Drop Audit Log.cs"
expect 1 --format github samples
expect 2 samples/does-not-exist

if [ "$failures" -gt 0 ]; then
  echo "$failures smoke test(s) failed."
  exit 1
fi
echo "All smoke tests passed."
