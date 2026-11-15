namespace EfMigrationLint.Reporting;

public enum Severity
{
    Warning,
    Error,
}

/// <summary>One rule match at one location in a migration source file.</summary>
public sealed record Finding(
    string RuleId,
    string RuleTitle,
    Severity Severity,
    string Path,
    int Line,
    int Column,
    string Message,
    string ReviewAction,
    string HelpUri);
