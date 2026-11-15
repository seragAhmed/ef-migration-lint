# EFML004: Sql

| | |
|---|---|
| Default severity | warning (advisory, doesn't fail the run unless `--warnings-as-errors` is used; [configurable](../../README.md#configuration)) |
| Reports | `migrationBuilder.Sql(...)` inside `Up()` |

## What it reports

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        @"UPDATE Customers
          SET Email = LOWER(LTRIM(RTRIM(Email)))
          WHERE Email IS NOT NULL;");
}
```

```text
warning EFML004 Sql: Migrations/20260114093000_NormalizeCustomerEmails.cs:13:13
    Up() runs raw SQL that this tool does not analyze: "UPDATE Customers SET Email = LOWER(LTRIM(RTRIM(Email))) WHERE Email IS NOT NULL;"
```

When the SQL is a string literal (regular, verbatim `@"..."`, or raw `"""..."""`), the message shows
its first 80 characters on one line. Otherwise there is no preview.

## Why

Raw SQL can do anything: delete or overwrite data, take long locks, or use syntax that only one
provider supports. v0.1 deliberately does not try to interpret it. Guessing the effect of arbitrary
SQL would produce confident but wrong answers.

## What to do

Read the SQL and check that it:

- doesn't delete or overwrite data you need;
- doesn't hold long locks on large tables;
- runs on every provider and environment you deploy to, including local development databases.

## Not reported

- `Sql(...)` in `Down()`.
- `Sql(...)` called on anything other than the `Up()` parameter.
