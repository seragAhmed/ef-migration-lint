namespace EfMigrationLint.Rules;

/// <summary>The rules that run by default. Add new rules here.</summary>
public static class RuleCatalog
{
    public const string DocsBaseUri = "https://github.com/seragAhmed/ef-migration-lint/blob/main/docs/rules/";

    public static IReadOnlyList<IMigrationRule> All { get; } =
    [
        new DropTableRule(),
        new DropColumnRule(),
        new AddNonNullableColumnRule(),
        new RawSqlRule(),
    ];

    public static string HelpUri(IMigrationRule rule) => $"{DocsBaseUri}{rule.Id}.md";
}
