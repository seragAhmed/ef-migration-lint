# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.0] - 2026-11-15

### Added

- `ef-migration-lint` .NET tool (targets `net8.0` and `net10.0`). It reads EF Core migration source
  with Roslyn and never builds, runs code, or connects to a database.
- Rules: EFML001 DropTable (error), EFML002 DropColumn (error), EFML003 AddNonNullableColumn
  (warning), EFML004 Sql (warning).
- Console output and GitHub Actions annotations (`--format github`).
- Exit codes: 0 (no errors), 1 (error findings), 2 (scan incomplete).
- `--warnings-as-errors`, `--list-rules`.
- A GitHub Actions example that lints only migrations added or modified in a pull request.
