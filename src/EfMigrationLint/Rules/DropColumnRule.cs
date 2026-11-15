using EfMigrationLint.Analysis;
using EfMigrationLint.Reporting;

namespace EfMigrationLint.Rules;

/// <summary>EFML002: <c>migrationBuilder.DropColumn(...)</c> in <c>Up()</c>.</summary>
public sealed class DropColumnRule : IMigrationRule
{
    public string Id => "EFML002";

    public string Title => "DropColumn";

    public Severity Severity => Severity.Error;

    public string Summary => "Up() drops a column and the values stored in it.";

    public RuleMatch? Analyze(MigrationOperation operation)
    {
        if (operation.MethodName != "DropColumn")
        {
            return null;
        }

        // DropColumn(string name, string table, string? schema = null)
        var column = RuleText.Name(operation.GetStringArgument("name", 0), "a column");
        var table = RuleText.Name(operation.GetStringArgument("table", 1), "its table");

        return new RuleMatch(
            $"Up() drops column {column} from table {table}. Every value in the column is deleted.",
            "If this is a rename, replace DropColumn + AddColumn with RenameColumn. "
            + "Otherwise confirm the values are no longer needed or have been copied elsewhere, "
            + "and that no running application version still reads the column.");
    }
}
