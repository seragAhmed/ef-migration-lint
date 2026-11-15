using EfMigrationLint.Reporting;
using static EfMigrationLint.Tests.TestHelpers;

namespace EfMigrationLint.Tests;

public class CliTests
{
    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Cli.Run(args, stdout, stderr, RepoRoot);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Safe_directory_passes()
    {
        var (exitCode, stdout, _) = Run("samples/Safe");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Scanned 3 files: 3 migrations. Skipped 2 designer/snapshot files.", stdout);
        Assert.Contains("Result: passed (no findings). Exit code 0.", stdout);
    }

    [Fact]
    public void DropColumn_fails_with_exit_code_1()
    {
        var (exitCode, stdout, _) = Run("samples/Destructive/Migrations/20260111090000_RenameBioToBiography.cs");

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        Assert.Contains("error EFML002 DropColumn: samples/Destructive/Migrations/20260111090000_RenameBioToBiography.cs:13:13", stdout);
        Assert.Contains("Review: If this is a rename, replace DropColumn + AddColumn with RenameColumn.", stdout);
        Assert.Contains("Result: failed (1 error, 0 warnings). Exit code 1.", stdout);
    }

    [Fact]
    public void Warnings_are_advisory_by_default()
    {
        var (exitCode, stdout, _) = Run("samples/NeedsReview");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("warning EFML003 AddNonNullableColumn:", stdout);
        Assert.Contains("warning EFML004 Sql:", stdout);
        Assert.Contains("Result: passed with 2 warnings for manual review. Exit code 0.", stdout);
    }

    [Fact]
    public void Warnings_as_errors_fails()
    {
        var (exitCode, stdout, _) = Run("--warnings-as-errors", "samples/NeedsReview");

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        Assert.Contains("error EFML003", stdout);
    }

    [Fact]
    public void Edge_cases_pass()
    {
        var (exitCode, _, _) = Run("samples/EdgeCases");
        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public void Explicit_generated_files_are_skipped_not_analyzed()
    {
        var (exitCode, stdout, _) = Run(
            "samples/Safe/Migrations/20260110090000_AddCustomerNickname.Designer.cs",
            "samples/Safe/Migrations/ShopDbContextModelSnapshot.cs");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Scanned 0 files: 0 migrations. Skipped 2 designer/snapshot files.", stdout);
    }

    [Fact]
    public void Paths_with_spaces_are_reported_intact()
    {
        var (exitCode, stdout, _) = Run("--format", "github", "samples/Paths With Spaces/Migrations/20260116090000_Drop Audit Log.cs");

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        Assert.Contains(
            "::error file=samples/Paths With Spaces/Migrations/20260116090000_Drop Audit Log.cs,line=14,col=13,title=EFML001 DropTable::Up() drops table 'AuditLog'.",
            stdout);
    }

    [Fact]
    public void GitHub_format_writes_annotations_and_summary()
    {
        var (exitCode, stdout, _) = Run("--format=github", "samples/NeedsReview", "samples/Destructive");

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(2, lines.Count(l => l.StartsWith("::error ", StringComparison.Ordinal)));
        Assert.Equal(2, lines.Count(l => l.StartsWith("::warning ", StringComparison.Ordinal)));
        Assert.Contains("%0AReview: ", stdout);
        Assert.Contains("Result: failed (2 errors, 2 warnings). Exit code 1.", stdout);
    }

    [Fact]
    public void Missing_path_exits_2_even_when_other_files_have_errors()
    {
        var (exitCode, stdout, _) = Run("samples/Destructive", "samples/does-not-exist.cs");

        Assert.Equal(ExitCodes.ScanIncomplete, exitCode);
        Assert.Contains("scan error: Path not found: samples/does-not-exist.cs", stdout);
        Assert.Contains("error EFML002", stdout);
        Assert.Contains("Result: scan incomplete", stdout);
    }

    [Fact]
    public void Unparsable_file_exits_2()
    {
        var path = Path.Combine(Path.GetTempPath(), $"efml-{Guid.NewGuid():N}.cs");
        File.WriteAllText(path, "public class M : Migration { protected override void Up(MigrationBuilder m) { m.DropTable(\"A\" } }");
        try
        {
            var (exitCode, stdout, _) = Run(path);
            Assert.Equal(ExitCodes.ScanIncomplete, exitCode);
            Assert.Contains("syntax error", stdout);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData]
    [InlineData("--format")]
    [InlineData("--format", "sarif", "samples")]
    [InlineData("--strict", "samples")]
    [InlineData("README.md")]
    public void Usage_errors_exit_2(params string[] args)
    {
        var (exitCode, stdout, stderr) = Run(args);

        Assert.Equal(ExitCodes.ScanIncomplete, exitCode);
        Assert.True(stderr.Length > 0 || stdout.Contains("scan error"), "Expected an error message.");
    }

    [Fact]
    public void Help_version_and_rules_exit_0()
    {
        Assert.Equal(ExitCodes.Success, Run("--help").ExitCode);

        var (exitCode, stdout, _) = Run("--version");
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("0.2.0", stdout.Trim());

        var rules = Run("--list-rules");
        Assert.Equal(ExitCodes.Success, rules.ExitCode);
        foreach (var id in new[] { "EFML001", "EFML002", "EFML003", "EFML004" })
        {
            Assert.Contains(id, rules.Stdout);
        }
    }
}
