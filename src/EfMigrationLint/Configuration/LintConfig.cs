using System.Text.Json;
using EfMigrationLint.Analysis;
using EfMigrationLint.Reporting;
using EfMigrationLint.Rules;

namespace EfMigrationLint.Configuration;

/// <summary>What the config file sets for one rule.</summary>
public enum RuleSetting
{
    Off,
    Warning,
    Error,
}

/// <summary>The result of looking for and reading a config file.</summary>
/// <param name="Config">The loaded config, or <c>null</c> when there is none or it is invalid.</param>
/// <param name="Error">Set when a config file was named or found but could not be used.</param>
public sealed record ConfigLoadResult(LintConfig? Config, ScanError? Error);

/// <summary>
/// Per-rule settings from <c>ef-migration-lint.json</c>:
/// <code>{ "rules": { "EFML003": "error", "EFML004": "off" } }</code>
/// </summary>
/// <param name="DisplayPath">The config file's path as shown in output.</param>
/// <param name="Rules">Settings by canonical rule ID, in ID order. Rules not listed keep their default severity.</param>
public sealed record LintConfig(string DisplayPath, IReadOnlyDictionary<string, RuleSetting> Rules)
{
    /// <summary>The file read from the working directory when <c>--config</c> is not given.</summary>
    public const string DefaultFileName = "ef-migration-lint.json";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Reads <paramref name="explicitPath"/> when given (it must exist), otherwise
    /// <see cref="DefaultFileName"/> in <paramref name="workingDirectory"/> if it exists.
    /// </summary>
    public static ConfigLoadResult Load(string? explicitPath, string workingDirectory, IReadOnlyList<IMigrationRule> rules)
    {
        string fullPath;
        if (explicitPath is null)
        {
            fullPath = Path.Combine(workingDirectory, DefaultFileName);
            if (!File.Exists(fullPath))
            {
                return new ConfigLoadResult(null, null);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(explicitPath))
            {
                return new ConfigLoadResult(null, new ScanError(null, "--config needs a file path."));
            }

            fullPath = Path.GetFullPath(explicitPath, workingDirectory);
            if (!File.Exists(fullPath))
            {
                var problem = Directory.Exists(fullPath) ? "is a directory, not a file" : "was not found";
                return new ConfigLoadResult(null, new ScanError(null, $"Config file {explicitPath} {problem}."));
            }
        }

        var displayPath = InputCollector.ToDisplayPath(fullPath, workingDirectory);

        string json;
        try
        {
            json = File.ReadAllText(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ConfigLoadResult(null, new ScanError(displayPath, $"Could not read config file: {ex.Message}"));
        }

        try
        {
            return new ConfigLoadResult(Parse(json, displayPath, rules), null);
        }
        catch (ConfigException ex)
        {
            return new ConfigLoadResult(null, new ScanError(displayPath, ex.Message));
        }
    }

    /// <summary>Parses config JSON. Comments and trailing commas are allowed; anything unrecognized is an error.</summary>
    /// <exception cref="ConfigException">The JSON is invalid or names an unknown setting, rule, or severity.</exception>
    public static LintConfig Parse(string json, string displayPath, IReadOnlyList<IMigrationRule> rules)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Invalid JSON: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ConfigException("The config must be a JSON object, for example { \"rules\": { \"EFML004\": \"error\" } }.");
            }

            var settings = new SortedDictionary<string, RuleSetting>(StringComparer.Ordinal);
            var sawRules = false;

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "rules")
                {
                    throw new ConfigException($"Unknown setting \"{property.Name}\". The only top-level setting is \"rules\".");
                }

                if (sawRules)
                {
                    throw new ConfigException("\"rules\" is set more than once.");
                }

                sawRules = true;

                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new ConfigException("\"rules\" must be an object that maps rule IDs to \"error\", \"warning\", or \"off\".");
                }

                foreach (var ruleProperty in property.Value.EnumerateObject())
                {
                    var rule = rules.FirstOrDefault(r => string.Equals(r.Id, ruleProperty.Name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ConfigException(
                            $"Unknown rule \"{ruleProperty.Name}\". Known rules: {string.Join(", ", rules.Select(r => r.Id))}.");

                    if (settings.ContainsKey(rule.Id))
                    {
                        throw new ConfigException($"Rule {rule.Id} is set more than once.");
                    }

                    settings[rule.Id] = ParseSetting(rule.Id, ruleProperty.Value);
                }
            }

            return new LintConfig(displayPath, settings);
        }
    }

    /// <summary>The rules to run: rules set to off are removed, others get their configured severity.</summary>
    public IReadOnlyList<IMigrationRule> Apply(IReadOnlyList<IMigrationRule> rules) =>
        [.. rules
            .Where(rule => EffectiveSetting(rule) != RuleSetting.Off)
            .Select(rule => Rules.TryGetValue(rule.Id, out var setting) ? new ConfiguredRule(rule, ToSeverity(setting)) : rule)];

    public RuleSetting EffectiveSetting(IMigrationRule rule) =>
        Rules.TryGetValue(rule.Id, out var setting) ? setting : DefaultSetting(rule);

    public static RuleSetting DefaultSetting(IMigrationRule rule) =>
        rule.Severity == Severity.Error ? RuleSetting.Error : RuleSetting.Warning;

    public static string Name(RuleSetting setting) => setting.ToString().ToLowerInvariant();

    /// <summary>One line for the run summary, so CI logs show which rules a config changed.</summary>
    public string Describe() =>
        Rules.Count == 0
            ? $"Config: {DisplayPath} (no rule settings)."
            : $"Config: {DisplayPath} ({string.Join(", ", Rules.Select(r => $"{r.Key} {Name(r.Value)}"))}).";

    private static RuleSetting ParseSetting(string ruleId, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return text?.ToLowerInvariant() switch
        {
            "error" => RuleSetting.Error,
            "warning" => RuleSetting.Warning,
            "off" => RuleSetting.Off,
            _ => throw new ConfigException(
                $"Rule {ruleId} has setting {value.GetRawText()}. Use \"error\", \"warning\", or \"off\"."),
        };
    }

    private static Severity ToSeverity(RuleSetting setting) =>
        setting == RuleSetting.Error ? Severity.Error : Severity.Warning;

    /// <summary>A rule with its severity replaced by the config.</summary>
    private sealed class ConfiguredRule(IMigrationRule inner, Severity severity) : IMigrationRule
    {
        public string Id => inner.Id;

        public string Title => inner.Title;

        public Severity Severity => severity;

        public string Summary => inner.Summary;

        public RuleMatch? Analyze(MigrationOperation operation) => inner.Analyze(operation);
    }
}

/// <summary>The config file is invalid. The message says what to change.</summary>
public sealed class ConfigException(string message) : Exception(message);
