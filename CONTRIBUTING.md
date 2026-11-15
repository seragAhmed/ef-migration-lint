# Contributing to EF Migration Lint

Thanks for helping. Small, focused pull requests are the easiest to review and merge. Good first
contributions include:

- a new fixture under `samples/` that shows a real migration the tool handles (or mishandles);
- a clearer message or review action for an existing rule;
- a provider-specific example (SQL Server, PostgreSQL, SQLite, MySQL) in `docs/rules/`.

Issues labelled [`good first issue`](https://github.com/seragAhmed/ef-migration-lint/labels/good%20first%20issue)
have concrete acceptance criteria.

## Before you start: get the issue assigned

1. Find an issue you'd like to work on, or open one for your idea.
2. Comment on it to say you'd like to take it.
3. **Wait until a maintainer assigns the issue to you**, then start. This keeps two people from
   working on the same thing and lets us agree on the approach first.
4. In your pull request description, link the issue with `Closes #<number>`.

A check on every pull request confirms that the linked issue is assigned to the PR author. If you
were assigned after opening the PR, re-run the check or push a new commit.

Questions and ideas that aren't ready to be an issue are welcome in
[Discussions](https://github.com/seragAhmed/ef-migration-lint/discussions).

## Setup

You need the .NET 10 SDK or newer (`global.json` asks for 10.0.100 and rolls forward). The tool targets
`net8.0` and `net10.0`, so avoid APIs that only exist in .NET 9 or later in `src/`, or the build fails.

```bash
dotnet build
dotnet test
dotnet run --project src/EfMigrationLint -f net10.0 -- samples
bash scripts/smoke-test.sh dotnet run --project src/EfMigrationLint -f net10.0 --   # checks exit codes end to end
```

## Layout

```text
src/EfMigrationLint/
  Cli.cs                     argument parsing, exit codes
  Analysis/MigrationAnalyzer.cs  finds Up(MigrationBuilder) and the calls made on its parameter
  Analysis/MigrationOperation.cs one migrationBuilder.X(...) call, plus argument helpers
  Analysis/InputCollector.cs     expands paths, skips Designer/ModelSnapshot files
  Rules/                     one class per rule, plus RuleCatalog.cs
  Reporting/                 console and GitHub Actions output
tests/EfMigrationLint.Tests/ xUnit tests (rules, analyzer, fixtures, CLI)
samples/                     fixture migrations (not compiled)
docs/rules/                  one page per rule
```

## How to add a rule

As a worked example, here is a hypothetical rule for `migrationBuilder.DropSchema(...)`.

### 1. Open an issue first

Use the **Rule proposal** template. Agree on the ID, the severity, and what should and shouldn't be
reported before writing code. Errors are for operations that delete data. Anything that only *might*
be a problem is a warning.

### 2. Write the rule

Create `src/EfMigrationLint/Rules/DropSchemaRule.cs`:

```csharp
using EfMigrationLint.Analysis;
using EfMigrationLint.Reporting;

namespace EfMigrationLint.Rules;

/// <summary>EFML005: <c>migrationBuilder.DropSchema(...)</c> in <c>Up()</c>.</summary>
public sealed class DropSchemaRule : IMigrationRule
{
    public string Id => "EFML005";
    public string Title => "DropSchema";
    public Severity Severity => Severity.Warning;
    public string Summary => "Up() drops a database schema.";

    public RuleMatch? Analyze(MigrationOperation operation)
    {
        if (operation.MethodName != "DropSchema")
        {
            return null;
        }

        // DropSchema(string name): read named or positional argument 0.
        var schema = RuleText.Name(operation.GetStringArgument("name", 0), "a schema");

        return new RuleMatch(
            $"Up() drops schema {schema}.",
            "Confirm the schema is empty in every environment, or that its objects are dropped on purpose.");
    }
}
```

`MigrationOperation` gives you `MethodName`, `GetArgument(name, position)`,
`GetStringArgument(name, position)`, and `HasNonNullArgument(name, position)`. Use the parameter
names and positions from the real `MigrationBuilder` signature in EF Core so that both
`DropSchema("billing")` and `DropSchema(name: "billing")` work.

### 3. Register it

Add `new DropSchemaRule(),` to `RuleCatalog.All` in `src/EfMigrationLint/Rules/RuleCatalog.cs`.

### 4. Add unit tests

In `tests/EfMigrationLint.Tests/RuleTests.cs`, add a test class. `AnalyzeUp` wraps statements in a
migration's `Up()`:

```csharp
public class DropSchemaRuleTests
{
    [Fact]
    public void Reports_warning_with_schema_name()
    {
        var finding = Assert.Single(TestHelpers.AnalyzeUp("""migrationBuilder.DropSchema(name: "billing");"""));
        Assert.Equal("EFML005", finding.RuleId);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("'billing'", finding.Message);
    }

    [Fact]
    public void Ignores_Down()
    {
        Assert.Empty(TestHelpers.AnalyzeUp("", downBody: """migrationBuilder.DropSchema("billing");"""));
    }
}
```

Cover at least: the positive case, named and positional arguments, and one case that must **not**
be reported.

### 5. Add a fixture

Add a realistic scaffolded migration under `samples/`, for example
`samples/NeedsReview/Migrations/20260120090000_DropBillingSchema.cs`, and add a row for it in
`tests/EfMigrationLint.Tests/FixtureTests.cs`. A test fails if a sample file has no expectation row.

### 6. Document it

- Add `docs/rules/EFML005.md`, following the existing pages (what it reports, why, what to do, limits).
- Add a row to the **Rules** table in `README.md`.
- Add a line under *Unreleased* in `CHANGELOG.md`.

## Writing messages

- **Message**: state what the source does, as a fact. Example: `Up() drops column 'Bio' from table 'Customers'.`
- **Review action**: tell the reviewer what to check or change.
- Don't claim an operation *will* fail or lose data unless that is certain from the source alone.
  EFML003 says "may", because the outcome depends on the provider and the data.
- Keep a message to one or two sentences. The docs page carries the detail.

## Rule ID policy

- IDs are `EFML` plus three digits, assigned in order.
- An ID is never renumbered or reused, even if the rule is removed.
- Changing a rule's default severity is a breaking change and needs a changelog entry. Users can
  still set any rule's severity in `ef-migration-lint.json`.

## Pull requests

- One change per PR, for an issue assigned to you. Include tests.
- Run `dotnet test` before pushing. CI builds, tests, packs, and runs the packed tool against
  `samples/` on Linux and Windows.
- The build treats warnings as errors.
- If you used an AI assistant, that's fine. Please review and understand every line you submit.

## Releasing (maintainers)

1. In a PR, bump `<Version>` in `src/EfMigrationLint/EfMigrationLint.csproj` and move the
   *Unreleased* notes in `CHANGELOG.md` under the new version.
2. After it merges, create a GitHub release with a matching tag: `gh release create v0.2.0 --generate-notes`.
3. The `Release` workflow tests and packs that tag, publishes it to NuGet.org with trusted publishing
   (no stored API key), and attaches the `.nupkg` to the release. It fails if the tag and `<Version>` differ.

## Reporting bugs and false positives

Use the **Bug report** template and include the smallest migration snippet that reproduces the
problem. False positives and false negatives are both bugs.
