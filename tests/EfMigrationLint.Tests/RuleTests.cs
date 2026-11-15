using EfMigrationLint.Reporting;
using static EfMigrationLint.Tests.TestHelpers;

namespace EfMigrationLint.Tests;

public class DropTableRuleTests
{
    [Fact]
    public void Reports_error_with_table_name()
    {
        var finding = Assert.Single(AnalyzeUp("""migrationBuilder.DropTable(name: "Customers");"""));

        Assert.Equal("EFML001", finding.RuleId);
        Assert.Equal("DropTable", finding.RuleTitle);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Contains("'Customers'", finding.Message);
        Assert.EndsWith("docs/rules/EFML001.md", finding.HelpUri);
    }

    [Fact]
    public void Reads_positional_name()
    {
        var finding = Assert.Single(AnalyzeUp("""migrationBuilder.DropTable("Customers", "sales");"""));
        Assert.Contains("'Customers'", finding.Message);
    }

    [Fact]
    public void Still_reports_when_name_is_not_a_literal()
    {
        var finding = Assert.Single(AnalyzeUp("""migrationBuilder.DropTable(name: TableNames.Customers);"""));
        Assert.Contains("drops table a table", finding.Message);
    }
}

public class DropColumnRuleTests
{
    [Fact]
    public void Reports_error_with_column_and_table()
    {
        var finding = Assert.Single(AnalyzeUp("""
            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Customers");
            """));

        Assert.Equal("EFML002", finding.RuleId);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Contains("column 'Bio' from table 'Customers'", finding.Message);
        Assert.Contains("RenameColumn", finding.ReviewAction);
    }

    [Fact]
    public void Reads_positional_and_reordered_named_arguments()
    {
        var findings = AnalyzeUp("""
            migrationBuilder.DropColumn("Bio", "Customers");
            migrationBuilder.DropColumn(table: "Orders", name: "Notes");
            """);

        Assert.Collection(
            findings,
            f => Assert.Contains("column 'Bio' from table 'Customers'", f.Message),
            f => Assert.Contains("column 'Notes' from table 'Orders'", f.Message));
    }
}

public class AddNonNullableColumnRuleTests
{
    [Fact]
    public void Warns_for_non_nullable_column_without_default()
    {
        var finding = Assert.Single(AnalyzeUp("""
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false);
            """));

        Assert.Equal("EFML003", finding.RuleId);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("'TenantId'", finding.Message);
        Assert.Contains("'Orders'", finding.Message);
        Assert.DoesNotContain("will fail", finding.Message + finding.ReviewAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Warns_when_nullable_is_omitted_because_the_api_default_is_false()
    {
        var finding = Assert.Single(AnalyzeUp("""migrationBuilder.AddColumn<int>(name: "Rank", table: "Players");"""));
        Assert.Equal("EFML003", finding.RuleId);
    }

    [Fact]
    public void Warns_when_default_is_explicitly_null()
    {
        var finding = Assert.Single(AnalyzeUp("""
            migrationBuilder.AddColumn<int>(name: "Rank", table: "Players", nullable: false, defaultValue: null, defaultValueSql: null);
            """));
        Assert.Equal("EFML003", finding.RuleId);
    }

    [Fact]
    public void Reads_positional_nullable_argument()
    {
        // AddColumn<T>(name, table, type, unicode, maxLength, rowVersion, schema, nullable, defaultValue, ...)
        Assert.Single(AnalyzeUp("""migrationBuilder.AddColumn<int>("Rank", "Players", "int", null, null, false, null, false);"""));
        Assert.Empty(AnalyzeUp("""migrationBuilder.AddColumn<int>("Rank", "Players", "int", null, null, false, null, false, 0);"""));
    }

    [Theory]
    [InlineData("""migrationBuilder.AddColumn<string>(name: "Nickname", table: "Customers", nullable: true);""")]
    [InlineData("""migrationBuilder.AddColumn<int>(name: "Rank", table: "Players", nullable: false, defaultValue: 0);""")]
    [InlineData("""migrationBuilder.AddColumn<string>(name: "Code", table: "Players", nullable: false, defaultValue: "");""")]
    [InlineData("""migrationBuilder.AddColumn<DateTime>(name: "At", table: "Players", nullable: false, defaultValueSql: "GETUTCDATE()");""")]
    [InlineData("""migrationBuilder.AddColumn<int>(name: "Total", table: "Orders", nullable: false, computedColumnSql: "[A] + [B]");""")]
    [InlineData("""migrationBuilder.AddColumn<int>(name: "Rank", table: "Players", nullable: isNullable);""")]
    public void Does_not_warn(string statement)
    {
        Assert.Empty(AnalyzeUp(statement));
    }

    [Fact]
    public void Still_warns_when_annotations_are_chained()
    {
        var finding = Assert.Single(AnalyzeUp("""
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Players",
                type: "varchar(10)",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
            """));
        Assert.Equal("EFML003", finding.RuleId);
    }
}

public class RawSqlRuleTests
{
    [Fact]
    public void Warns_and_includes_a_single_line_preview()
    {
        var finding = Assert.Single(AnalyzeUp("""
            migrationBuilder.Sql(@"UPDATE Customers
                                   SET Email = LOWER(Email);");
            """));

        Assert.Equal("EFML004", finding.RuleId);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("\"UPDATE Customers SET Email = LOWER(Email);\"", finding.Message);
    }

    [Fact]
    public void Truncates_long_sql()
    {
        var sql = "UPDATE Customers SET Notes = '" + new string('x', 200) + "';";
        var finding = Assert.Single(AnalyzeUp($"""migrationBuilder.Sql("{sql}");"""));
        Assert.EndsWith("...\"", finding.Message);
    }

    [Fact]
    public void Reads_raw_string_literals()
    {
        var finding = Assert.Single(AnalyzeUp(""""
            migrationBuilder.Sql("""
                DELETE FROM Sessions;
                """);
            """"));
        Assert.Contains("\"DELETE FROM Sessions;\"", finding.Message);
    }

    [Fact]
    public void Warns_without_preview_when_sql_is_not_a_literal()
    {
        var finding = Assert.Single(AnalyzeUp("""migrationBuilder.Sql(Scripts.Load("backfill.sql"), suppressTransaction: true);"""));
        Assert.Equal("Up() runs raw SQL that this tool does not analyze", finding.Message);
    }
}
