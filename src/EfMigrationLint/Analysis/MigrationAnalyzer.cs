using System.Globalization;
using EfMigrationLint.Reporting;
using EfMigrationLint.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EfMigrationLint.Analysis;

/// <summary>Result of analyzing one source file.</summary>
/// <param name="Path">Display path used in output.</param>
/// <param name="MigrationCount">Number of <c>Up(MigrationBuilder)</c> methods found.</param>
/// <param name="Findings">Rule matches, ordered by location.</param>
/// <param name="Error">Set when the file could not be analyzed.</param>
public sealed record FileAnalysis(string Path, int MigrationCount, IReadOnlyList<Finding> Findings, string? Error);

/// <summary>
/// Parses C# source with Roslyn (syntax only: no compilation, no project build, no database)
/// and runs rules against the <c>migrationBuilder</c> calls inside each <c>Up(MigrationBuilder)</c> method.
/// </summary>
public sealed class MigrationAnalyzer
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    private readonly IReadOnlyList<IMigrationRule> _rules;

    public MigrationAnalyzer(IReadOnlyList<IMigrationRule>? rules = null)
    {
        _rules = rules ?? RuleCatalog.All;
    }

    public FileAnalysis AnalyzeSource(string source, string displayPath)
    {
        var tree = CSharpSyntaxTree.ParseText(source, ParseOptions, path: displayPath);

        var syntaxError = tree.GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
        if (syntaxError is not null)
        {
            var line = syntaxError.Location.GetLineSpan().StartLinePosition.Line + 1;
            var message = syntaxError.GetMessage(CultureInfo.InvariantCulture);
            return new FileAnalysis(displayPath, 0, [], $"C# syntax error at line {line} ({syntaxError.Id}: {message}). The file was not analyzed.");
        }

        var findings = new List<Finding>();
        var migrationCount = 0;

        foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!TryGetMigrationBuilderParameter(method, "Up", out var builderName))
            {
                continue;
            }

            migrationCount++;

            foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                // Only calls made directly on the Up() parameter: migrationBuilder.DropTable(...).
                // Calls on any other receiver, or local methods that happen to share a name, are ignored.
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax receiver,
                    } memberAccess
                    || receiver.Identifier.ValueText != builderName)
                {
                    continue;
                }

                var operation = new MigrationOperation(memberAccess.Name.Identifier.ValueText, invocation);

                foreach (var rule in _rules)
                {
                    if (rule.Analyze(operation) is { } match)
                    {
                        findings.Add(new Finding(
                            rule.Id,
                            rule.Title,
                            rule.Severity,
                            displayPath,
                            operation.Line,
                            operation.Column,
                            match.Message,
                            match.ReviewAction,
                            RuleCatalog.HelpUri(rule)));
                    }
                }
            }
        }

        findings.Sort((a, b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : a.Column.CompareTo(b.Column));
        return new FileAnalysis(displayPath, migrationCount, findings, null);
    }

    /// <summary>
    /// Matches <c>Up(MigrationBuilder x)</c> by name and parameter type, whatever the modifiers.
    /// <c>Down()</c>, designer <c>BuildTargetModel()</c>, and snapshot <c>BuildModel()</c> do not match.
    /// </summary>
    private static bool TryGetMigrationBuilderParameter(MethodDeclarationSyntax method, string methodName, out string parameterName)
    {
        parameterName = "";

        if (method.Identifier.ValueText != methodName || method.ParameterList.Parameters.Count != 1)
        {
            return false;
        }

        var parameter = method.ParameterList.Parameters[0];
        if (!IsMigrationBuilderType(parameter.Type))
        {
            return false;
        }

        parameterName = parameter.Identifier.ValueText;
        return true;
    }

    private static bool IsMigrationBuilderType(TypeSyntax? type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "MigrationBuilder",
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == "MigrationBuilder",
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText == "MigrationBuilder",
        _ => false,
    };
}
