namespace EfMigrationLint.Rules;

internal static class RuleText
{
    /// <summary>Formats a name taken from a string literal, with a fallback when the source does not use a literal.</summary>
    public static string Name(string? value, string fallback) =>
        string.IsNullOrEmpty(value) ? fallback : $"'{value}'";
}
