# EFML001: DropTable

| | |
|---|---|
| Default severity | error ([configurable](../../README.md#configuration)) |
| Reports | `migrationBuilder.DropTable(...)` inside `Up()` |

## What it reports

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropTable(name: "LegacyOrders", schema: "archive");
}
```

```text
error EFML001 DropTable: Migrations/20260113090000_DropLegacyOrders.cs:14:13
    Up() drops table 'LegacyOrders'. Every row in the table is deleted.
```

## Why

Dropping a table deletes its data. `Down()` can recreate the table, but it can't bring the rows back.
During a rolling deploy, application instances still running the previous version may also query the
table and fail.

## What to do

- Confirm the data is no longer needed, or that it has been backed up or copied elsewhere.
- If running code still uses the table, ship the code change that stops using it first. Drop the
  table in a later migration ("expand, then contract").
- If the drop is intentional and reviewed, merge it with your team's override. The example workflow
  uses the `allow-destructive-migration` PR label.

## Not reported

- `DropTable` in `Down()`.
- `DropTable` called on anything other than the `Up()` parameter, such as a helper object.
- Tables dropped through `migrationBuilder.Sql("DROP TABLE ...")`. EFML004 flags those for review.
