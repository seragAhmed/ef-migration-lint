# Prior art and scope

Researched on 2026-11-15 by searching GitHub and NuGet.org.

## Name

The working name was "EF Migration Guard". NuGet already has a family of `MigrationGuard.*` packages
for EF Core migration safety (see below), so that name would be confusing. "EF Migration Lint"
(`EfMigrationLint` on NuGet, `ef-migration-lint` command) had no matching package or repository.

## Closest existing tools

| Tool | How it works | Difference from this project |
|---|---|---|
| [ef-migration-safety](https://github.com/sayinbrahim/ef-migration-safety) (`EfMigrationSafety.Cli` 0.2.0) | Roslyn parsing of migration `.cs` files; CLI with a `--strict` mode; a library package too. | **The closest overlap.** Same source-only approach. This project focuses on the PR loop: changed files only, GitHub annotations, a fixed exit-code contract (0/1/2), and very conservative wording. |
| [MigrationGuard](https://github.com/Mefju08/Migration-Analyzer) (`MigrationGuard.Tool` / `.Core` / `.Testing` 0.3.0) | Analyzes a **compiled** migrations assembly, or runs as a unit-test assertion. Provider-aware: lock and table-rewrite rules, plus `Down()` data loss. | Goes deeper, but needs a build. This project never builds or runs the scanned code, so it is safe and fast on any PR. |
| `Doka.EntityFrameworkCore.SafeMigrations` | Runtime `MigrationBuilder` extension methods for idempotent, provider-specific operations. | A way to author migrations, not a review check. Complementary. |
| `SafeMigrations.EFCore` 0.1.0 | NuGet package: "Inspect EF Core migrations for production safety risks." No repository linked. | Not enough public information to compare. |
| EF Core itself | `dotnet ef migrations add` prints "An operation was scaffolded that may result in the loss of data." | Shown once, in the author's terminal. Reviewers never see it. |
| [strong_migrations](https://github.com/ankane/strong_migrations) (Rails), [Squawk](https://github.com/sbdchd/squawk) (PostgreSQL SQL) | Mature migration linters in other ecosystems. | Inspiration for rule wording and the review-oriented approach. |

## Scope of v0.1

In scope:

- Source-only analysis of `Up()` in migration `.cs` files. No build, no code execution, no database.
- Four rules: DropTable and DropColumn (errors); non-nullable AddColumn without a default, and raw SQL
  (warnings).
- Console output and GitHub Actions annotations; exit codes 0/1/2.
- A workflow example that checks only files added or modified in the PR and is safe for fork PRs.

Out of scope for now:

- Provider-specific lock and rewrite analysis. If you need that, use MigrationGuard.
- Interpreting raw SQL.
- Following helper methods or evaluating non-literal values.
- `Down()` analysis.

If a team needs deep, provider-aware analysis, MigrationGuard is a better fit. If it wants a
source-only CLI, ef-migration-safety is also worth evaluating. This project's bet is that a small,
predictable check with a clear contribution path is useful in its own right.
