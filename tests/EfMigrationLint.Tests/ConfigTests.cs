using EfMigrationLint.Configuration;
using EfMigrationLint.Reporting;
using EfMigrationLint.Rules;
using static EfMigrationLint.Tests.TestHelpers;

namespace EfMigrationLint.Tests;

public sealed class ConfigTests : IDisposable
{
    private const string RenameMigration = "samples/Destructive/Migrations/20260111090000_RenameBioToBiography.cs";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"efml-config-{Guid.NewGuid():N}");

    public ConfigTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    /// <summary>Writes a config file in the temp directory and returns its absolute path.</summary>
    private string WriteConfig(string json, string fileName = LintConfig.DefaultFileName)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, json);
        return path;
    }

    private static (int ExitCode, string Stdout, string Stderr) RunIn(string workingDirectory, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Cli.Run(args, stdout, stderr, workingDirectory);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args) => RunIn(RepoRoot, args);

    [Fact]
    public void Warning_raised_to_error_fails()
    {
        var config = WriteConfig("""{ "rules": { "EFML003": "error" } }""");

        var (exitCode, stdout, _) = Run("--config", config, "samples/NeedsReview");

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        Assert.Contains("error EFML003 AddNonNullableColumn:", stdout);
        Assert.Contains("warning EFML004 Sql:", stdout);
        Assert.Contains("Result: failed (1 error, 1 warning). Exit code 1.", stdout);
    }

    [Fact]
    public void Error_lowered_to_warning_passes()
    {
        var config = WriteConfig("""{ "rules": { "EFML002": "warning" } }""");

        var (exitCode, stdout, _) = Run("--config", config, RenameMigration);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("warning EFML002 DropColumn:", stdout);
        Assert.Contains("Result: passed with 1 warning for manual review. Exit code 0.", stdout);
    }

    [Fact]
    public void Rule_set_to_off_does_not_run()
    {
        var config = WriteConfig("""{ "rules": { "EFML002": "off" } }""");

        var (exitCode, stdout, _) = Run("--config", config, RenameMigration);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain("EFML002 DropColumn", stdout);
        Assert.Contains("Result: passed (no findings). Exit code 0.", stdout);
    }

    [Fact]
    public void Summary_names_the_config_and_its_settings()
    {
        var config = WriteConfig("""{ "rules": { "EFML004": "off", "EFML003": "error" } }""");

        var (_, stdout, _) = Run("--config", config, "samples/NeedsReview");

        // Settings are listed in rule ID order, whatever their order in the file.
        Assert.Contains($"Config: {config} (EFML003 error, EFML004 off).", stdout);
    }

    [Fact]
    public void GitHub_format_uses_configured_severity_and_prints_the_config_line()
    {
        var config = WriteConfig("""{ "rules": { "EFML004": "error" } }""");

        var (exitCode, stdout, _) = Run("--format", "github", "--config", config, "samples/NeedsReview");

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        Assert.Contains("::error file=samples/NeedsReview/", stdout);
        Assert.Contains("title=EFML004 Sql::", stdout);
        Assert.Contains("title=EFML003 AddNonNullableColumn::", stdout);
        Assert.Contains("(EFML004 error).", stdout);
    }

    [Fact]
    public void Warnings_as_errors_applies_after_the_config()
    {
        var config = WriteConfig("""{ "rules": { "EFML003": "off" } }""");

        var (exitCode, stdout, _) = Run("--warnings-as-errors", "--config", config, "samples/NeedsReview");

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        Assert.Contains("error EFML004 Sql:", stdout);
        Assert.DoesNotContain("EFML003", stdout.Replace("(EFML003 off)", "", StringComparison.Ordinal));
    }

    [Fact]
    public void Default_file_in_the_working_directory_is_used()
    {
        WriteConfig("""{ "rules": { "EFML002": "off" } }""");

        var (exitCode, stdout, _) = RunIn(_directory, Path.Combine(RepoRoot, RenameMigration));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Config: ef-migration-lint.json (EFML002 off).", stdout);
    }

    [Fact]
    public void Explicit_config_replaces_the_default_file()
    {
        WriteConfig("""{ "rules": { "EFML002": "off" } }""");
        WriteConfig("""{ "rules": { "EFML002": "warning" } }""", "strict.json");

        var (exitCode, stdout, _) = RunIn(_directory, "--config=strict.json", Path.Combine(RepoRoot, RenameMigration));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("warning EFML002 DropColumn:", stdout);
        Assert.Contains("Config: strict.json (EFML002 warning).", stdout);
    }

    [Fact]
    public void Without_a_config_there_is_no_config_line()
    {
        var (exitCode, stdout, _) = RunIn(_directory, Path.Combine(RepoRoot, RenameMigration));

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        Assert.DoesNotContain("Config:", stdout);
    }

    [Fact]
    public void Comments_trailing_commas_and_any_case_are_accepted()
    {
        var config = WriteConfig("""
            {
              // Reviewed in #12: data is copied to the archive table first.
              "rules": {
                "efml002": "Warning", /* lower-case ID, capitalized value */
              },
            }
            """);

        var (exitCode, stdout, _) = Run("--config", config, RenameMigration);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("(EFML002 warning).", stdout);
    }

    [Fact]
    public void Empty_rules_object_changes_nothing()
    {
        var config = WriteConfig("""{ "rules": {} }""");

        var (exitCode, stdout, _) = Run("--config", config, RenameMigration);

        Assert.Equal(ExitCodes.ErrorFindings, exitCode);
        Assert.Contains("(no rule settings).", stdout);
    }

    [Theory]
    [InlineData("""{ "rules": { "EFML999": "error" } }""", "Unknown rule \"EFML999\". Known rules: EFML001, EFML002, EFML003, EFML004.")]
    [InlineData("""{ "rules": { "EFML003": "fatal" } }""", "Rule EFML003 has setting \"fatal\". Use \"error\", \"warning\", or \"off\".")]
    [InlineData("""{ "rules": { "EFML003": false } }""", "Rule EFML003 has setting false.")]
    [InlineData("""{ "rules": { "EFML003": "off", "efml003": "error" } }""", "Rule EFML003 is set more than once.")]
    [InlineData("""{ "rules": {}, "rules": {} }""", "\"rules\" is set more than once.")]
    [InlineData("""{ "rule": { "EFML003": "error" } }""", "Unknown setting \"rule\".")]
    [InlineData("""{ "rules": ["EFML003"] }""", "\"rules\" must be an object")]
    [InlineData("""["EFML003"]""", "The config must be a JSON object")]
    [InlineData("""{ "rules": { "EFML003": "error" """, "Invalid JSON:")]
    [InlineData("", "Invalid JSON:")]
    public void Invalid_config_exits_2_before_scanning(string json, string expected)
    {
        var config = WriteConfig(json);

        var (exitCode, stdout, stderr) = Run("--config", config, RenameMigration);

        Assert.Equal(ExitCodes.ScanIncomplete, exitCode);
        Assert.Contains($"error: {config}: ", stderr);
        Assert.Contains(expected, stderr);
        Assert.Equal("", stdout);
    }

    [Fact]
    public void Invalid_default_file_is_an_error_too()
    {
        WriteConfig("""{ "rules": { "EFML02": "off" } }""");

        var (exitCode, _, stderr) = RunIn(_directory, Path.Combine(RepoRoot, RenameMigration));

        Assert.Equal(ExitCodes.ScanIncomplete, exitCode);
        Assert.Contains("error: ef-migration-lint.json: Unknown rule \"EFML02\".", stderr);
    }

    [Fact]
    public void Invalid_config_in_github_format_writes_an_annotation()
    {
        WriteConfig("""{ "rules": { "EFML02": "off" } }""");

        var (exitCode, stdout, _) = RunIn(_directory, "--format", "github", Path.Combine(RepoRoot, RenameMigration));

        Assert.Equal(ExitCodes.ScanIncomplete, exitCode);
        Assert.StartsWith("::error file=ef-migration-lint.json,title=ef-migration-lint config error::Unknown rule", stdout);
    }

    [Theory]
    [InlineData("missing.json", "Config file missing.json was not found.")]
    [InlineData(".", "Config file . is a directory, not a file.")]
    [InlineData("", "--config needs a file path.")]
    public void Unusable_config_path_exits_2(string path, string expected)
    {
        var (exitCode, _, stderr) = RunIn(_directory, "--config", path, Path.Combine(RepoRoot, RenameMigration));

        Assert.Equal(ExitCodes.ScanIncomplete, exitCode);
        Assert.Contains($"error: {expected}", stderr);
    }

    [Fact]
    public void Config_option_without_a_value_exits_2()
    {
        var (exitCode, _, stderr) = Run("--config");

        Assert.Equal(ExitCodes.ScanIncomplete, exitCode);
        Assert.Contains("--config needs a file path.", stderr);
    }

    [Fact]
    public void List_rules_shows_the_configured_severity()
    {
        WriteConfig("""{ "rules": { "EFML003": "error", "EFML004": "off" } }""");

        var (exitCode, stdout, _) = RunIn(_directory, "--list-rules");

        Assert.Equal(ExitCodes.Success, exitCode);
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.StartsWith("EFML002  error  ", Assert.Single(lines, l => l.StartsWith("EFML002", StringComparison.Ordinal)));
        Assert.DoesNotContain("set in", Assert.Single(lines, l => l.StartsWith("EFML002", StringComparison.Ordinal)));
        Assert.StartsWith("EFML003  error  ", Assert.Single(lines, l => l.StartsWith("EFML003", StringComparison.Ordinal)));
        Assert.EndsWith("(set in ef-migration-lint.json; default warning)", Assert.Single(lines, l => l.StartsWith("EFML003", StringComparison.Ordinal)));
        Assert.StartsWith("EFML004  off  ", Assert.Single(lines, l => l.StartsWith("EFML004", StringComparison.Ordinal)));
    }

    [Fact]
    public void List_rules_with_an_invalid_config_exits_2()
    {
        WriteConfig("""{ "rules": { "EFML003": "fatal" } }""");

        Assert.Equal(ExitCodes.ScanIncomplete, RunIn(_directory, "--list-rules").ExitCode);
    }

    [Fact]
    public void Apply_keeps_rule_identity_and_order()
    {
        var config = LintConfig.Parse("""{ "rules": { "EFML001": "warning", "EFML004": "off" } }""", "c.json", RuleCatalog.All);

        var rules = config.Apply(RuleCatalog.All);

        Assert.Equal(["EFML001", "EFML002", "EFML003"], rules.Select(r => r.Id));
        Assert.Equal([Severity.Warning, Severity.Error, Severity.Warning], rules.Select(r => r.Severity));
        Assert.Equal("DropTable", rules[0].Title);
    }

    [Fact]
    public void Configured_findings_link_to_the_rule_docs()
    {
        var config = LintConfig.Parse("""{ "rules": { "EFML002": "warning" } }""", "c.json", RuleCatalog.All);

        var finding = Assert.Single(Cli.Scan([RenameMigration], RepoRoot, config: config).Findings);

        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("https://github.com/seragAhmed/ef-migration-lint/blob/main/docs/rules/EFML002.md", finding.HelpUri);
    }
}
