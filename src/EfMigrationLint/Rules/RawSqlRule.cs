using System.Text.RegularExpressions;
using EfMigrationLint.Analysis;
using EfMigrationLint.Reporting;

namespace EfMigrationLint.Rules;

/// <summary>
/// EFML004: <c>migrationBuilder.Sql(...)</c> in <c>Up()</c>. The SQL is not interpreted;
/// the rule only asks a person to read it.
/// </summary>
public sealed partial class RawSqlRule : IMigrationRule
{
    private const int PreviewLength = 80;

    public string Id => "EFML004";

    public string Title => "Sql";

    public Severity Severity => Severity.Warning;

    public string Summary => "Up() runs raw SQL, which this tool does not analyze. Review it by hand.";

    public RuleMatch? Analyze(MigrationOperation operation)
    {
        if (operation.MethodName != "Sql")
        {
            return null;
        }

        // Sql(string sql, bool suppressTransaction = false)
        var sql = operation.GetStringArgument("sql", 0);
        var preview = sql is null ? "" : $": \"{Preview(sql)}\"";

        return new RuleMatch(
            $"Up() runs raw SQL that this tool does not analyze{preview}",
            "Read the SQL and check that it does not delete or overwrite data unexpectedly, "
            + "does not hold long locks on large tables, and runs on every database provider and environment you deploy to.");
    }

    private static string Preview(string sql)
    {
        var singleLine = Whitespace().Replace(sql, " ").Trim();
        return singleLine.Length <= PreviewLength ? singleLine : string.Concat(singleLine.AsSpan(0, PreviewLength), "...");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
