using EfMigrationLint.Analysis;
using EfMigrationLint.Reporting;

namespace EfMigrationLint.Rules;

/// <summary>What a rule reports when it matches an operation.</summary>
/// <param name="Message">What the migration does, stated as a fact about the source.</param>
/// <param name="ReviewAction">What a reviewer should check or change.</param>
public sealed record RuleMatch(string Message, string ReviewAction);

/// <summary>
/// A check applied to every <c>migrationBuilder.X(...)</c> call inside <c>Up()</c>.
/// Rule IDs are stable: never reuse or renumber an ID once released.
/// </summary>
public interface IMigrationRule
{
    /// <summary>Stable ID, for example <c>EFML002</c>.</summary>
    string Id { get; }

    /// <summary>Short title, usually the <c>MigrationBuilder</c> method name.</summary>
    string Title { get; }

    Severity Severity { get; }

    /// <summary>One-line description shown by <c>--list-rules</c>.</summary>
    string Summary { get; }

    /// <summary>Returns a match when the operation should be reported, otherwise <c>null</c>.</summary>
    RuleMatch? Analyze(MigrationOperation operation);
}
