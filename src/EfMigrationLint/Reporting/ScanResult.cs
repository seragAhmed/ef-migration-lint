using EfMigrationLint.Analysis;

namespace EfMigrationLint.Reporting;

/// <summary>A scan error tied to a file when one is known.</summary>
public sealed record ScanError(string? Path, string Message);

/// <summary>Everything a reporter needs to print.</summary>
public sealed class ScanResult
{
    public ScanResult(IReadOnlyList<FileAnalysis> files, IReadOnlyList<string> skipped, IReadOnlyList<ScanError> errors, IReadOnlyList<Finding> findings)
    {
        Files = files;
        Skipped = skipped;
        Errors = errors;
        Findings = findings;
    }

    public IReadOnlyList<FileAnalysis> Files { get; }

    public IReadOnlyList<string> Skipped { get; }

    public IReadOnlyList<ScanError> Errors { get; }

    public IReadOnlyList<Finding> Findings { get; }

    public int ErrorCount => Findings.Count(f => f.Severity == Severity.Error);

    public int WarningCount => Findings.Count(f => f.Severity == Severity.Warning);

    public int ExitCode =>
        Errors.Count > 0 ? ExitCodes.ScanIncomplete
        : ErrorCount > 0 ? ExitCodes.ErrorFindings
        : ExitCodes.Success;
}

public static class ExitCodes
{
    /// <summary>Analysis completed with no error-severity findings. Warnings may be present.</summary>
    public const int Success = 0;

    /// <summary>Analysis completed and found at least one error-severity finding.</summary>
    public const int ErrorFindings = 1;

    /// <summary>Invalid arguments, a missing or unreadable path, or a file that could not be parsed.</summary>
    public const int ScanIncomplete = 2;
}
