# EFML002: DropColumn

| | |
|---|---|
| Severity | error |
| Reports | `migrationBuilder.DropColumn(...)` inside `Up()` |

## What it reports

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "Bio", table: "Customers");
    migrationBuilder.AddColumn<string>(name: "Biography", table: "Customers", nullable: true);
}
```

```text
error EFML002 DropColumn: Migrations/20260111090000_RenameBioToBiography.cs:13:13
    Up() drops column 'Bio' from table 'Customers'. Every value in the column is deleted.
```

## Why

Dropping a column deletes every value in it. A common cause is a property rename that EF Core
scaffolded as a drop plus an add instead of a rename. Application instances still running the
previous version during a deploy may also read or write the column and fail.

## What to do

- **If it's a rename**, replace the pair with `RenameColumn`:

  ```csharp
  migrationBuilder.RenameColumn(name: "Bio", table: "Customers", newName: "Biography");
  ```

- Otherwise, confirm the values are no longer needed or have been copied elsewhere.
- Make sure no running application version still reads the column. Remove it from the code in one
  release and drop it in a later migration.

## Not reported

- `DropColumn` in `Down()`.
- `DropColumn` called on anything other than the `Up()` parameter.
- Columns dropped through `migrationBuilder.Sql(...)`. EFML004 flags those for review.
