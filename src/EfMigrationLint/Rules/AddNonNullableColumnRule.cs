using EfMigrationLint.Analysis;
using EfMigrationLint.Reporting;

namespace EfMigrationLint.Rules;

/// <summary>
/// EFML003: <c>migrationBuilder.AddColumn&lt;T&gt;(...)</c> in <c>Up()</c> that adds a non-nullable column
/// without <c>defaultValue</c>, <c>defaultValueSql</c>, or <c>computedColumnSql</c>.
/// </summary>
/// <remarks>
/// This is a review prompt, not a prediction. Whether the statement fails depends on the provider,
/// the column type, and whether the table already has rows.
/// </remarks>
public sealed class AddNonNullableColumnRule : IMigrationRule
{
    // Positions in AddColumn<T>(string name, string table, string? type, bool? unicode, int? maxLength,
    // bool rowVersion, string? schema, bool nullable = false, object? defaultValue,
    // string? defaultValueSql, string? computedColumnSql, ...)
    private const int NamePosition = 0;
    private const int TablePosition = 1;
    private const int NullablePosition = 7;
    private const int DefaultValuePosition = 8;
    private const int DefaultValueSqlPosition = 9;
    private const int ComputedColumnSqlPosition = 10;

    public string Id => "EFML003";

    public string Title => "AddNonNullableColumn";

    public Severity Severity => Severity.Warning;

    public string Summary => "Up() adds a non-nullable column without a default or computed value. Review how existing rows get a value.";

    public RuleMatch? Analyze(MigrationOperation operation)
    {
        if (operation.MethodName != "AddColumn")
        {
            return null;
        }

        var nullableArgument = operation.GetArgument("nullable", NullablePosition);
        if (nullableArgument is not null)
        {
            if (!MigrationOperation.TryGetBoolLiteral(nullableArgument, out var nullable))
            {
                // Not a literal (a variable, a constant, a method call). Source-only analysis cannot tell.
                return null;
            }

            if (nullable)
            {
                return null;
            }
        }

        // When "nullable" is omitted, EF Core's default is nullable: false.
        if (operation.HasNonNullArgument("defaultValue", DefaultValuePosition)
            || operation.HasNonNullArgument("defaultValueSql", DefaultValueSqlPosition)
            || operation.HasNonNullArgument("computedColumnSql", ComputedColumnSqlPosition))
        {
            return null;
        }

        var column = RuleText.Name(operation.GetStringArgument("name", NamePosition), "a column");
        var table = RuleText.Name(operation.GetStringArgument("table", TablePosition), "its table");

        return new RuleMatch(
            $"Up() adds non-nullable column {column} to table {table} without defaultValue, defaultValueSql, or computedColumnSql.",
            "Check whether the table can already contain rows. Depending on the provider and column type, existing rows may make this fail. "
            + "If they can, add an explicit default, or add the column as nullable, backfill it, and make it required in a later migration. "
            + "If the table is known to be empty, say so in the PR.");
    }
}
