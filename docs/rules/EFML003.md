# EFML003: AddNonNullableColumn

| | |
|---|---|
| Severity | warning (advisory, doesn't fail the run unless `--warnings-as-errors` is used) |
| Reports | `migrationBuilder.AddColumn<T>(...)` inside `Up()` that is non-nullable and has no `defaultValue`, `defaultValueSql`, or `computedColumnSql` |

## What it reports

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<Guid>(
        name: "TenantId",
        table: "Orders",
        type: "uniqueidentifier",
        nullable: false);
}
```

```text
warning EFML003 AddNonNullableColumn: Migrations/20260114090000_AddRequiredTenantId.cs:15:13
    Up() adds non-nullable column 'TenantId' to table 'Orders' without defaultValue, defaultValueSql, or computedColumnSql.
```

A column counts as non-nullable when the source says `nullable: false`, or when `nullable` is
omitted, because `AddColumn`'s default is `false`. An explicit `defaultValue: null` counts as no default.

## Why it is a warning, not an error

If the table already has rows, those rows need a value for the new column. What happens next depends
on the provider, the column type, and the data. Some databases reject the statement. Others fill in a
type default. If the table is empty, nothing happens. Source code alone can't tell, so the rule asks
for review instead of predicting a failure.

EF Core normally scaffolds a `defaultValue` (for example `defaultValue: 0` or `defaultValue: ""`) for
new non-nullable columns. So this rule usually fires on hand-edited migrations, or on types where no
default was scaffolded.

## What to do

- If the table can contain rows, add an explicit `defaultValue` or `defaultValueSql`, **or**
- add the column as nullable, backfill it (in code or with `Sql`), and make it non-nullable in a later
  migration, **or**
- if the table is known to be empty in every environment, say so in the PR.

## Not reported

- `nullable: true`.
- Any non-null `defaultValue`, `defaultValueSql`, or `computedColumnSql`.
- A `nullable:` argument that isn't a literal (a variable or a constant). The value can't be known
  from the source.
- Columns added through `CreateTable(...)`. A new table has no existing rows.
