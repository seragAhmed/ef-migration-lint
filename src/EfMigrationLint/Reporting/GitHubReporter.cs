namespace EfMigrationLint.Reporting;

/// <summary>
/// GitHub Actions workflow commands (<c>::error file=...,line=...::message</c>), which GitHub shows
/// as annotations on the PR's "Files changed" tab, followed by the plain summary.
/// </summary>
/// <remarks>Escaping follows the actions/toolkit <c>command.ts</c> implementation.</remarks>
public static class GitHubReporter
{
    public static void Write(TextWriter output, ScanResult result)
    {
        foreach (var error in result.Errors)
        {
            var properties = error.Path is null
                ? "title=ef-migration-lint scan error"
                : $"file={EscapeProperty(error.Path)},title=ef-migration-lint scan error";
            output.WriteLine($"::error {properties}::{EscapeData(error.Message)}");
        }

        foreach (var finding in result.Findings)
        {
            var command = finding.Severity == Severity.Error ? "error" : "warning";
            var properties = string.Join(
                ',',
                $"file={EscapeProperty(finding.Path)}",
                $"line={finding.Line}",
                $"col={finding.Column}",
                $"title={EscapeProperty($"{finding.RuleId} {finding.RuleTitle}")}");
            var message = $"{finding.Message}\nReview: {finding.ReviewAction}\nDocs: {finding.HelpUri}";
            output.WriteLine($"::{command} {properties}::{EscapeData(message)}");
        }

        ConsoleReporter.WriteSummary(output, result);
    }

    public static string EscapeData(string value) =>
        value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");

    public static string EscapeProperty(string value) =>
        EscapeData(value).Replace(":", "%3A").Replace(",", "%2C");
}
