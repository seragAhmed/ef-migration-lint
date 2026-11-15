namespace EfMigrationLint.Reporting;

/// <summary>Plain-text output for terminals and CI logs.</summary>
public static class ConsoleReporter
{
    private const string Indent = "    ";

    public static void Write(TextWriter output, ScanResult result)
    {
        foreach (var error in result.Errors)
        {
            output.WriteLine(error.Path is null ? $"scan error: {error.Message}" : $"scan error: {error.Path}: {error.Message}");
        }

        if (result.Errors.Count > 0)
        {
            output.WriteLine();
        }

        foreach (var finding in result.Findings)
        {
            var severity = finding.Severity == Severity.Error ? "error" : "warning";
            output.WriteLine($"{severity} {finding.RuleId} {finding.RuleTitle}: {finding.Path}:{finding.Line}:{finding.Column}");
            output.WriteLine($"{Indent}{finding.Message}");
            output.WriteLine($"{Indent}Review: {finding.ReviewAction}");
            output.WriteLine($"{Indent}Docs: {finding.HelpUri}");
            output.WriteLine();
        }

        WriteSummary(output, result);
    }

    public static void WriteSummary(TextWriter output, ScanResult result)
    {
        var migrations = result.Files.Sum(f => f.MigrationCount);
        var withoutMigrations = result.Files.Count(f => f.Error is null && f.MigrationCount == 0);

        var scanned = $"Scanned {Plural(result.Files.Count, "file")}: {Plural(migrations, "migration")}";
        if (withoutMigrations > 0)
        {
            scanned += $", {Plural(withoutMigrations, "file")} without a migration Up() method";
        }

        if (result.Skipped.Count > 0)
        {
            scanned += $". Skipped {Plural(result.Skipped.Count, "designer/snapshot file")}";
        }

        output.WriteLine(scanned + ".");

        var counts = $"{Plural(result.ErrorCount, "error")}, {Plural(result.WarningCount, "warning")}";
        var outcome = result.ExitCode switch
        {
            ExitCodes.ScanIncomplete => $"Result: scan incomplete ({Plural(result.Errors.Count, "scan error")}; {counts}). Exit code 2.",
            ExitCodes.ErrorFindings => $"Result: failed ({counts}). Exit code 1.",
            _ when result.WarningCount > 0 => $"Result: passed with {Plural(result.WarningCount, "warning")} for manual review. Exit code 0.",
            _ => "Result: passed (no findings). Exit code 0.",
        };

        output.WriteLine(outcome);
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
