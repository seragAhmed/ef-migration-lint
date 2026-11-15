using EfMigrationLint.Analysis;
using static EfMigrationLint.Tests.TestHelpers;

namespace EfMigrationLint.Tests;

public class AnalyzerTests
{
    [Fact]
    public void Ignores_operations_in_Down()
    {
        var findings = AnalyzeUp(
            upBody: """migrationBuilder.RenameTable(name: "Orders", newName: "SalesOrders");""",
            downBody: """
                migrationBuilder.DropTable(name: "SalesOrders");
                migrationBuilder.DropColumn(name: "Bio", table: "Customers");
                migrationBuilder.AddColumn<int>(name: "Rank", table: "Players", nullable: false);
                migrationBuilder.Sql("DELETE FROM Customers;");
                """);

        Assert.Empty(findings);
    }

    [Fact]
    public void Reports_the_line_and_column_of_the_call()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class M : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.CreateTable(name: "A", columns: t => new { });

                    migrationBuilder.DropColumn(
                        name: "Bio",
                        table: "Customers");
                }
            }
            """;

        var finding = Assert.Single(AnalyzeSource(source).Findings);
        Assert.Equal(9, finding.Line);
        Assert.Equal(9, finding.Column);
    }

    [Fact]
    public void Follows_the_parameter_name_and_qualified_type()
    {
        var source = """
            public class M : Microsoft.EntityFrameworkCore.Migrations.Migration
            {
                protected override void Up(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder mb)
                {
                    mb.DropTable("Customers");
                }
            }
            """;

        var analysis = AnalyzeSource(source);
        Assert.Equal(1, analysis.MigrationCount);
        Assert.Equal("EFML001", Assert.Single(analysis.Findings).RuleId);
    }

    [Fact]
    public void Ignores_calls_on_other_receivers_and_local_methods_with_matching_names()
    {
        var findings = AnalyzeUp("""
            var other = new Helper();
            other.DropTable("Customers");
            other.DropColumn("Bio", "Customers");
            other.Sql("DELETE FROM Customers;");
            DropTable("Customers");
            this.DropColumn("Bio", "Customers");
            var text = "migrationBuilder.DropTable(\"Customers\")";
            // migrationBuilder.DropTable("Customers");
            """);

        Assert.Empty(findings);
    }

    [Fact]
    public void Ignores_Up_methods_that_do_not_take_a_MigrationBuilder()
    {
        var source = """
            public class Steps
            {
                public void Up() { migrationBuilder.DropTable("A"); }
                public void Up(ModelBuilder migrationBuilder) { migrationBuilder.DropTable("B"); }
                public void Up(MigrationBuilder migrationBuilder, int extra) { migrationBuilder.DropTable("C"); }
                public void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("D"); }
            }
            """;

        var analysis = AnalyzeSource(source);
        Assert.Equal(0, analysis.MigrationCount);
        Assert.Empty(analysis.Findings);
    }

    [Fact]
    public void Analyzes_every_migration_class_in_a_file()
    {
        var source = """
            public class First : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("A");
            }

            public class Second : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("B"); }
            }
            """;

        var analysis = AnalyzeSource(source);
        Assert.Equal(2, analysis.MigrationCount);
        Assert.Equal(2, analysis.Findings.Count);
    }

    [Fact]
    public void Reports_a_syntax_error_instead_of_guessing()
    {
        var source = """
            public class M : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
            <<<<<<< HEAD
                    migrationBuilder.DropTable("A");
            =======
                    migrationBuilder.DropTable("B");
            >>>>>>> feature
                }
            }
            """;

        var analysis = AnalyzeSource(source);
        Assert.NotNull(analysis.Error);
        Assert.Contains("syntax error at line", analysis.Error);
        Assert.Empty(analysis.Findings);
    }

    [Theory]
    [InlineData("20260101000000_Init.Designer.cs", true)]
    [InlineData("AppDbContextModelSnapshot.cs", true)]
    [InlineData("20260101000000_Init.designer.cs", true)]
    [InlineData("20260101000000_Init.cs", false)]
    [InlineData("SnapshotHelpers.cs", false)]
    public void Identifies_generated_files_by_name(string fileName, bool expected)
    {
        Assert.Equal(expected, InputCollector.IsGeneratedFile(fileName));
    }
}
