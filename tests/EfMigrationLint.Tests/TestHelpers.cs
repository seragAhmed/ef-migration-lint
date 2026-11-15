using EfMigrationLint.Analysis;
using EfMigrationLint.Reporting;

namespace EfMigrationLint.Tests;

internal static class TestHelpers
{
    /// <summary>Wraps statements in a scaffolded-looking migration and analyzes it.</summary>
    public static IReadOnlyList<Finding> AnalyzeUp(string upBody, string downBody = "")
    {
        var analysis = AnalyzeSource(MigrationSource(upBody, downBody));
        Assert.Null(analysis.Error);
        Assert.Equal(1, analysis.MigrationCount);
        return analysis.Findings;
    }

    public static FileAnalysis AnalyzeSource(string source, string path = "Migrations/Test.cs") =>
        new MigrationAnalyzer().AnalyzeSource(source, path);

    public static string MigrationSource(string upBody, string downBody = "") => $$"""
        using Microsoft.EntityFrameworkCore.Migrations;

        namespace Test.Migrations
        {
            public partial class TestMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
        {{upBody}}
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
        {{downBody}}
                }
            }
        }
        """;

    /// <summary>The repository root, found by walking up to the solution file.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string SamplePath(params string[] parts) =>
        Path.Combine([RepoRoot, "samples", .. parts]);

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EfMigrationLint.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find EfMigrationLint.sln above the test output directory.");
    }
}
