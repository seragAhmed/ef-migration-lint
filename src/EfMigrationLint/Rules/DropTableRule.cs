using EfMigrationLint.Analysis;
using EfMigrationLint.Reporting;

namespace EfMigrationLint.Rules;

/// <summary>EFML001: <c>migrationBuilder.DropTable(...)</c> in <c>Up()</c>.</summary>
public sealed class DropTableRule : IMigrationRule
{
    public string Id => "EFML001";

    public string Title => "DropTable";

    public Severity Severity => Severity.Error;

    public string Summary => "Up() drops a table and every row in it.";

    public RuleMatch? Analyze(MigrationOperation operation)
    {
        if (operation.MethodName != "DropTable")
        {
            return null;
        }

        // DropTable(string name, string? schema = null)
        var table = RuleText.Name(operation.GetStringArgument("name", 0), "a table");

        return new RuleMatch(
            $"Up() drops table {table}. Every row in the table is deleted.",
            "Confirm the data is no longer needed or has been backed up or copied elsewhere. "
            + "If running application code still uses the table, ship that code change first and drop the table in a later migration.");
    }
}
