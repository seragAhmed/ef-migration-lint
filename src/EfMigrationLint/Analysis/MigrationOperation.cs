using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EfMigrationLint.Analysis;

/// <summary>
/// A single <c>migrationBuilder.X(...)</c> call found inside a migration's <c>Up()</c> method.
/// Rules receive these and decide whether to report a finding.
/// </summary>
public sealed class MigrationOperation
{
    internal MigrationOperation(string methodName, InvocationExpressionSyntax invocation)
    {
        MethodName = methodName;
        Invocation = invocation;
        var position = invocation.GetLocation().GetLineSpan().StartLinePosition;
        Line = position.Line + 1;
        Column = position.Character + 1;
    }

    /// <summary>The <c>MigrationBuilder</c> method name, without generic arguments (for example <c>AddColumn</c>).</summary>
    public string MethodName { get; }

    /// <summary>The invocation syntax, for rules that need more than the helpers below.</summary>
    public InvocationExpressionSyntax Invocation { get; }

    /// <summary>1-based line of the invocation.</summary>
    public int Line { get; }

    /// <summary>1-based column of the invocation.</summary>
    public int Column { get; }

    /// <summary>
    /// Returns the argument passed for a parameter, matching a named argument first
    /// (<c>table: "Customers"</c>) and then a positional argument at <paramref name="position"/>.
    /// Returns <c>null</c> when the argument is not passed.
    /// </summary>
    /// <param name="name">The parameter name in the <c>MigrationBuilder</c> method signature.</param>
    /// <param name="position">The zero-based parameter position in that signature.</param>
    public ExpressionSyntax? GetArgument(string name, int position)
    {
        var arguments = Invocation.ArgumentList.Arguments;

        foreach (var argument in arguments)
        {
            if (argument.NameColon?.Name.Identifier.ValueText == name)
            {
                return argument.Expression;
            }
        }

        if (position < 0 || position >= arguments.Count)
        {
            return null;
        }

        // A positional match only counts while every argument up to it is positional.
        for (var i = 0; i <= position; i++)
        {
            if (arguments[i].NameColon is not null)
            {
                return null;
            }
        }

        return arguments[position].Expression;
    }

    /// <summary>Returns the value of a string literal argument, or <c>null</c> when it is missing or not a literal.</summary>
    public string? GetStringArgument(string name, int position) =>
        GetArgument(name, position) is { } expression ? GetStringLiteral(expression) : null;

    /// <summary>True when the argument is passed and is not the literal <c>null</c>.</summary>
    public bool HasNonNullArgument(string name, int position) =>
        GetArgument(name, position) is { } expression && !expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns the value of a string literal (regular, verbatim, or raw), or <c>null</c> for any other expression.</summary>
    public static string? GetStringLiteral(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;

    /// <summary>Reads a <c>true</c>/<c>false</c> literal. Returns <c>false</c> for any other expression.</summary>
    public static bool TryGetBoolLiteral(ExpressionSyntax expression, out bool value)
    {
        switch (expression.Kind())
        {
            case SyntaxKind.TrueLiteralExpression:
                value = true;
                return true;
            case SyntaxKind.FalseLiteralExpression:
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }
}
