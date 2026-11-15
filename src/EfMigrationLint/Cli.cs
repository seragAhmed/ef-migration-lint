using System.Reflection;
using EfMigrationLint.Analysis;
using EfMigrationLint.Configuration;
using EfMigrationLint.Reporting;
using EfMigrationLint.Rules;

namespace EfMigrationLint;

/// <summary>Command-line entry point, kept free of <see cref="Console"/> so tests can call it directly.</summary>
public static class Cli
{
    private const string Usage = """
        Usage: ef-migration-lint [options] <path>...

        Checks EF Core migration source files for potentially destructive operations in Up().
        Reads .cs files only: it does not build your project, run code, or connect to a database.

        Arguments:
          <path>                  A migration .cs file or a directory to scan recursively.
                                  *.Designer.cs and *ModelSnapshot.cs files are skipped.

        Options:
          --format <console|github>
                                  console (default): readable text.
                                  github: GitHub Actions annotations plus a summary.
          --config <file>         Per-rule settings (JSON). Default: ef-migration-lint.json in the
                                  current directory, if it exists. Example:
                                  { "rules": { "EFML003": "error", "EFML004": "off" } }
          --warnings-as-errors    Treat warnings as errors (exit code 1). Off by default.
                                  Applied after the config.
          --list-rules            Print the rules with their severity after the config, and exit.
          --version               Print the version and exit.
          -h, --help              Print this help and exit.

        Exit codes:
          0  Analysis completed with no errors (warnings are advisory).
          1  Analysis completed and found at least one error.
          2  Scanning could not complete (bad arguments, invalid config, missing path,
             unparsable file).
        """;

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, string workingDirectory)
    {
        var paths = new List<string>();
        var format = "console";
        string? configPath = null;
        var warningsAsErrors = false;
        var listRules = false;
        var optionsEnded = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (optionsEnded || !arg.StartsWith('-') || arg == "-")
            {
                paths.Add(arg);
                continue;
            }

            switch (arg)
            {
                case "--":
                    optionsEnded = true;
                    break;
                case "-h":
                case "--help":
                    stdout.WriteLine(Usage);
                    return ExitCodes.Success;
                case "--version":
                    stdout.WriteLine(Version);
                    return ExitCodes.Success;
                case "--list-rules":
                    // Printed after parsing, so it reflects the config.
                    listRules = true;
                    break;
                case "--warnings-as-errors":
                    warningsAsErrors = true;
                    break;
                case "--format":
                    if (i + 1 >= args.Length)
                    {
                        return UsageError(stderr, "--format needs a value: console or github.");
                    }

                    format = args[++i];
                    break;
                case "--config":
                    if (i + 1 >= args.Length)
                    {
                        return UsageError(stderr, "--config needs a file path.");
                    }

                    configPath = args[++i];
                    break;
                default:
                    if (arg.StartsWith("--format=", StringComparison.Ordinal))
                    {
                        format = arg["--format=".Length..];
                        break;
                    }

                    if (arg.StartsWith("--config=", StringComparison.Ordinal))
                    {
                        configPath = arg["--config=".Length..];
                        break;
                    }

                    return UsageError(stderr, $"Unknown option: {arg}");
            }
        }

        if (format is not ("console" or "github"))
        {
            return UsageError(stderr, $"Unknown format '{format}'. Use console or github.");
        }

        var (config, configError) = LintConfig.Load(configPath, workingDirectory, RuleCatalog.All);
        if (configError is not null)
        {
            return ConfigError(stdout, stderr, configError, format);
        }

        if (listRules)
        {
            WriteRules(stdout, config);
            return ExitCodes.Success;
        }

        if (paths.Count == 0)
        {
            return UsageError(stderr, "No paths given.");
        }

        var result = Scan(paths, workingDirectory, warningsAsErrors, config);

        if (format == "github")
        {
            GitHubReporter.Write(stdout, result);
        }
        else
        {
            ConsoleReporter.Write(stdout, result);
        }

        return result.ExitCode;
    }

    public static ScanResult Scan(IReadOnlyList<string> paths, string workingDirectory, bool warningsAsErrors = false, LintConfig? config = null)
    {
        var inputs = InputCollector.Collect(paths, workingDirectory);
        var analyzer = new MigrationAnalyzer(config?.Apply(RuleCatalog.All) ?? RuleCatalog.All);
        var analyses = new List<FileAnalysis>();
        var errors = inputs.Errors.Select(message => new ScanError(null, message)).ToList();

        foreach (var file in inputs.Files)
        {
            string source;
            try
            {
                source = File.ReadAllText(file.FullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new ScanError(file.DisplayPath, $"Could not read file: {ex.Message}"));
                continue;
            }

            var analysis = analyzer.AnalyzeSource(source, file.DisplayPath);
            analyses.Add(analysis);
            if (analysis.Error is not null)
            {
                errors.Add(new ScanError(file.DisplayPath, analysis.Error));
            }
        }

        var findings = analyses.SelectMany(a => a.Findings);
        if (warningsAsErrors)
        {
            findings = findings.Select(f => f with { Severity = Severity.Error });
        }

        return new ScanResult(analyses, inputs.Skipped, errors, findings.ToList(), config);
    }

    private static string Version =>
        typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "unknown";

    private static void WriteRules(TextWriter output, LintConfig? config)
    {
        foreach (var rule in RuleCatalog.All)
        {
            var defaultSetting = LintConfig.DefaultSetting(rule);
            var setting = config?.EffectiveSetting(rule) ?? defaultSetting;
            var line = $"{rule.Id}  {LintConfig.Name(setting),-7}  {rule.Title,-20}  {rule.Summary}";
            if (config is not null && config.Rules.ContainsKey(rule.Id))
            {
                line += $" (set in {config.DisplayPath}; default {LintConfig.Name(defaultSetting)})";
            }

            output.WriteLine(line);
        }
    }

    private static int ConfigError(TextWriter stdout, TextWriter stderr, ScanError error, string format)
    {
        var message = error.Path is null ? error.Message : $"{error.Path}: {error.Message}";
        if (format == "github")
        {
            var properties = error.Path is null
                ? "title=ef-migration-lint config error"
                : $"file={GitHubReporter.EscapeProperty(error.Path)},title=ef-migration-lint config error";
            stdout.WriteLine($"::error {properties}::{GitHubReporter.EscapeData(error.Message)}");
        }

        stderr.WriteLine($"error: {message}");
        return ExitCodes.ScanIncomplete;
    }

    private static int UsageError(TextWriter stderr, string message)
    {
        stderr.WriteLine($"error: {message}");
        stderr.WriteLine("Run 'ef-migration-lint --help' for usage.");
        return ExitCodes.ScanIncomplete;
    }
}
