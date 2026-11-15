# EF Migration Lint

[![CI](https://github.com/seragAhmed/ef-migration-lint/actions/workflows/ci.yml/badge.svg)](https://github.com/seragAhmed/ef-migration-lint/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EfMigrationLint.svg)](https://www.nuget.org/packages/EfMigrationLint)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Catch potentially destructive EF Core migration operations during pull request review.

`ef-migration-lint` reads your migration `.cs` files with Roslyn and reports `DropTable`, `DropColumn`,
risky `AddColumn` calls, and raw SQL in `Up()`. It **does not build your project, run your code, or
connect to a database**, so the same check runs in a second on a laptop and on an untrusted fork PR in
GitHub Actions.

> Status: v0.1, first release. Four rules, deliberately conservative. Feedback and rule ideas are welcome.

## Example: a rename that would lose data

You rename `Customer.Bio` to `Customer.Biography`. EF Core can't always tell a rename from a drop plus an
add, so it may scaffold this:

```csharp
// Migrations/20260111090000_RenameBioToBiography.cs (before)
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "Bio",
        table: "Customers");

    migrationBuilder.AddColumn<string>(
        name: "Biography",
        table: "Customers",
        type: "nvarchar(max)",
        nullable: true);
}
```

On the pull request, the check fails and GitHub shows an annotation on the `DropColumn` line:

```text
Error: EFML002 DropColumn
Up() drops column 'Bio' from table 'Customers'. Every value in the column is deleted.
Review: If this is a rename, replace DropColumn + AddColumn with RenameColumn. Otherwise confirm the
values are no longer needed or have been copied elsewhere, and that no running application version
still reads the column.
Docs: https://github.com/seragAhmed/ef-migration-lint/blob/main/docs/rules/EFML002.md
```

That annotation comes from this line in the job log, a standard
[GitHub Actions workflow command](https://docs.github.com/actions/reference/workflows-and-actions/workflow-commands):

```text
::error file=samples/Destructive/Migrations/20260111090000_RenameBioToBiography.cs,line=13,col=13,title=EFML002 DropColumn::Up() drops column 'Bio' from table 'Customers'. Every value in the column is deleted.%0AReview: If this is a rename, ...
```

The fix keeps the data:

```csharp
// Migrations/20260111090000_RenameBioToBiography.cs (after)
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.RenameColumn(
        name: "Bio",
        table: "Customers",
        newName: "Biography");
}
```

```text
$ ef-migration-lint samples/Safe/Migrations/20260111090000_RenameBioToBiography.cs
Scanned 1 file: 1 migration.
Result: passed (no findings). Exit code 0.
```

Both versions are in [`samples/`](samples), so you can reproduce this yourself.

## Install

`ef-migration-lint` is a .NET tool on [NuGet.org](https://www.nuget.org/packages/EfMigrationLint). It
runs on the .NET 8 runtime or newer, whatever your application targets.

```bash
dotnet tool install --global EfMigrationLint

ef-migration-lint path/to/YourProject/Migrations
```

To pin the version for your whole team, install it as a local tool in your repository instead. The
version is recorded in the tool manifest (`dotnet-tools.json`, or `.config/dotnet-tools.json` with
older SDKs), and `dotnet tool restore` installs it on another machine or in CI:

```bash
dotnet new tool-manifest   # once per repository
dotnet tool install EfMigrationLint

dotnet tool run ef-migration-lint path/to/YourProject/Migrations
```

Update with `dotnet tool update --global EfMigrationLint` (or without `--global` for a local tool).

## Quickstart (from a fresh clone)

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) or newer to build. The installed tool
runs on the .NET 8 runtime or newer, whatever your application targets.

```bash
git clone https://github.com/seragAhmed/ef-migration-lint.git
cd ef-migration-lint
dotnet test

# Fails (exit code 1): DropColumn in Up()
dotnet run --project src/EfMigrationLint -f net10.0 -- samples/Destructive/Migrations/20260111090000_RenameBioToBiography.cs

# Passes (exit code 0): RenameColumn
dotnet run --project src/EfMigrationLint -f net10.0 -- samples/Safe/Migrations/20260111090000_RenameBioToBiography.cs
```

To try unreleased changes from `main` on your own project, install the tool from a local package:

```bash
dotnet pack src/EfMigrationLint -c Release -o artifacts
dotnet tool install --global EfMigrationLint --add-source ./artifacts --version 0.1.0

ef-migration-lint path/to/YourProject/Migrations
```

If an earlier version is installed, run `dotnet tool uninstall --global EfMigrationLint` first.

## Usage

```text
ef-migration-lint [options] <path>...

  <path>                     A migration .cs file or a directory to scan recursively.
  --format <console|github>  console (default) or GitHub Actions annotations.
  --config <file>            Per-rule settings. Default: ef-migration-lint.json in the current
                             directory, if it exists. See Configuration below.
  --warnings-as-errors       Make warnings fail the run (exit code 1). Off by default.
                             Applied after the config.
  --list-rules               Print the rules with their severity after the config.
  --version, --help
```

| Exit code | Meaning |
|---|---|
| `0` | Analysis completed with no errors. Warnings, if any, are advisory. |
| `1` | Analysis completed and found at least one error (or a warning with `--warnings-as-errors`). |
| `2` | Scanning could not complete: bad arguments, an invalid config file, a missing or unreadable path, a non-`.cs` file, or a file with C# syntax errors (for example, unresolved merge conflict markers). Takes precedence over `1`. |

Each finding includes the rule ID, severity, file path, line and column, a description, a suggested
review action, and a link to the rule's documentation.

## Use it in GitHub Actions

Copy [`examples/github-actions/ef-migration-lint.yml`](examples/github-actions/ef-migration-lint.yml) to
`.github/workflows/` in your repository. It:

- runs on `pull_request` with `contents: read` and no secrets, so it works the same for fork PRs;
- lints only migration files **added or modified** in the PR (`git diff --diff-filter=AM`), so old
  migrations that already ran never block new work;
- handles file names with spaces (`git diff -z` into a bash array);
- prints annotations with `--format github`;
- skips the check when the PR has the `allow-destructive-migration` label. Use that label for a drop
  your team has reviewed and accepted. Adding or removing the label re-runs the workflow.

### Run the same check locally

From your repository root, against the branch you will merge into:

```bash
# bash (Linux, macOS, Git Bash)
mapfile -d '' files < <(git diff --name-only -z --diff-filter=AM origin/main...HEAD -- ':(glob)**/Migrations/**/*.cs')
[ ${#files[@]} -eq 0 ] || ef-migration-lint -- "${files[@]}"
```

```powershell
# PowerShell
$files = @(git diff --name-only --diff-filter=AM origin/main...HEAD -- ':(glob)**/Migrations/**/*.cs')
if ($files.Count -gt 0) { ef-migration-lint -- @files }
```

If your migrations aren't in a folder named `Migrations`, change the path pattern in both places.

## Rules

| ID | Title | Severity | Reports in `Up()` |
|---|---|---|---|
| [EFML001](docs/rules/EFML001.md) | DropTable | error | `migrationBuilder.DropTable(...)` |
| [EFML002](docs/rules/EFML002.md) | DropColumn | error | `migrationBuilder.DropColumn(...)` |
| [EFML003](docs/rules/EFML003.md) | AddNonNullableColumn | warning | `migrationBuilder.AddColumn<T>(...)` that is non-nullable (`nullable: false`, or `nullable` omitted, since EF Core's default is `false`) and has no `defaultValue`, `defaultValueSql`, or `computedColumnSql` |
| [EFML004](docs/rules/EFML004.md) | Sql | warning | `migrationBuilder.Sql(...)` |

Rule IDs are stable. They are never renumbered or reused.

The severity column is the default. You can change it per rule; see [Configuration](#configuration).

EFML003 is a prompt for review, not a prediction. Whether adding the column fails depends on your
provider, the column type, and whether the table has rows. EFML004 does not try to understand the
SQL. It asks a person to read it.

## Configuration

> Not released yet: this is on `main` and ships in the next release. The 0.1.0 package on NuGet.org
> doesn't read a config file or accept `--config`.

Each rule's severity can be changed, or the rule turned off, in `ef-migration-lint.json`:

```jsonc
{
  "rules": {
    // A multi-tenant app: an unreviewed raw SQL statement runs against every tenant database.
    "EFML004": "error",
    // Our migrations always set defaults in a later step, so this one is noise for us.
    "EFML003": "off"
  }
}
```

| Setting | Effect |
|---|---|
| `"error"` | Findings fail the run (exit code 1). |
| `"warning"` | Findings are reported but don't fail the run, unless `--warnings-as-errors` is given. |
| `"off"` | The rule doesn't run. |

- The tool reads `ef-migration-lint.json` from the **current directory** if it exists. Pass
  `--config <file>` to use a different file. It doesn't search parent directories.
- Rules you don't list keep their default severity. Comments and trailing commas are allowed.
- A mistake in the file (an unknown rule ID, a misspelled setting, invalid JSON) stops the run with
  exit code 2, so a typo can never quietly turn a rule off.
- `--warnings-as-errors` applies after the config: it turns every remaining warning into an error.
- Every run that uses a config prints it in the summary, so CI logs show what was changed:

  ```text
  Config: ef-migration-lint.json (EFML003 off, EFML004 error).
  ```

- `ef-migration-lint --list-rules` prints each rule's effective severity.

The config file is part of your repository, so a pull request can change it like any other file.
Review changes to it the way you review the workflow itself. A `CODEOWNERS` entry for
`ef-migration-lint.json` makes that review required.

## What gets scanned

- Every `Up(MigrationBuilder x)` method in each file, whatever its class or modifiers.
- Only calls made directly on that parameter, such as `x.DropTable(...)`. Calls on other objects,
  local helpers with the same names, comments, and strings are ignored.
- `Down()` is ignored. Reverting a migration is expected to undo what `Up()` created.
- `*.Designer.cs` and `*ModelSnapshot.cs` files are skipped, even when you pass them explicitly.
- When scanning a directory, `bin`, `obj`, `.git`, and `node_modules` are skipped.

## Limitations of source-only analysis

The tool sees the C# syntax of one file at a time and never compiles it. That keeps it fast and safe
to run on any PR, but it means:

- **It can miss operations (false negatives).** Calls made through a helper method, an extension
  method, a loop over table names, or a custom `MigrationBuilder` wrapper are not followed. Code
  inside an inactive `#if` block is not seen.
- **It can't evaluate values.** If a name or flag is a variable or constant rather than a literal,
  the name is shown generically. EFML003 stays silent when `nullable:` is not a literal.
- **It doesn't know your database.** It can't tell whether a table is empty, how large it is, which
  provider you use, or whether an operation takes a long lock.
- **It doesn't read SQL.** `migrationBuilder.Sql(...)` is flagged for manual review, whatever it contains.
- **It treats any `Up(MigrationBuilder)` method as a migration**, including ones in helper classes.
- **It is not a substitute for review**, backups, or testing migrations against a copy of production data.

## Related tools

There are other good options with different trade-offs, such as compiled-assembly analysis,
provider-aware lock detection, or SQL-level linting. See [docs/prior-art.md](docs/prior-art.md).
This project's focus is a small, source-only check that fits the PR review loop.

## Roadmap

- **v0.2**: per-rule severity config (done on `main`). Add an inline suppression comment that requires
  a reason. Write a Markdown job summary (`$GITHUB_STEP_SUMMARY`).
- **More rules**: `RenameColumn`/`RenameTable` (they break app versions still running during a rolling
  deploy), `AlterColumn` changes from nullable to non-nullable, and `DropSchema`/`DropSequence`.
- **Integrations**: SARIF output for code scanning and a reusable composite action.

Ideas and votes are welcome in [issues](https://github.com/seragAhmed/ef-migration-lint/issues).

## Contributing

Small contributions are very welcome: a new fixture, a clearer message, or a provider-specific docs
example. [CONTRIBUTING.md](CONTRIBUTING.md) explains how to build, test, and add a rule step by step.

## License

[MIT](LICENSE)
