using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Reads calls to a shared calculation helper, and which literal each argument was bound to, by parameter
/// rather than by position.
/// </summary>
/// <remarks>
/// <para>
/// Several indicators are expression-bodied wrappers over one private helper, handing it the things a
/// generator needs to read - the indicator's name, its output keys - as <i>arguments</i>. The helper's body
/// then names a parameter and the wrapper's body names nothing, so a reader looking at one method at a time
/// sees neither. Pairing a helper's parameter with the literal its caller passed is the step that makes
/// those indicators visible, and it is the same step whatever the reader intends to do with the result.
/// </para>
/// <para>
/// By parameter, never by position. Helpers take their arguments in whatever order suits them -
/// <c>(..., IndicatorName indicatorName, string vidya1Key, string vidya2Key)</c> against
/// <c>(..., string outputKey, IndicatorName name)</c> - so an index reads one of them backwards. Asking the
/// semantic model which parameter an argument binds to also makes named arguments and defaulted parameters
/// fall out for free.
/// </para>
/// <para>
/// This is shared rather than copied. It was written for the output map (#199) and is needed again by the
/// dispersion inventory (#222), and two copies of an argument-matching rule are two things to keep in step
/// the next time a helper appears in a new shape.
/// </para>
/// <para>
/// A call site carries plain strings and no symbols, so it stays comparable between incremental runs, and
/// an argument that is not a literal is left out rather than guessed at.
/// </para>
/// </remarks>
public static class HelperCallSites
{
    /// <summary>One call to a helper, and the literals it passed, by parameter name.</summary>
    public sealed class CallSite
    {
        public string MethodName { get; set; } = string.Empty;

        /// <summary>Parameter name to the <c>IndicatorName</c> member passed for it.</summary>
        public Dictionary<string, string> IndicatorArguments { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Parameter name to the string literal passed for it.</summary>
        public Dictionary<string, string> StringArguments { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Any <c>Calculate</c> method, including the expression-bodied ones a reader testing
    /// <c>Body is not null</c> skips - a wrapper over a helper is often a single <c>=&gt;</c> call.
    /// </summary>
    public static bool IsCandidate(SyntaxNode node) =>
        node is MethodDeclarationSyntax method
        && method.Identifier.Text.StartsWith("Calculate", StringComparison.Ordinal)
        && (method.Body is not null || method.ExpressionBody is not null);

    /// <summary>The body of a method, whether it is written as a block or as an expression.</summary>
    public static SyntaxNode? BodyOf(MethodDeclarationSyntax method)
    {
        SyntaxNode? body = method.Body;
        return body ?? method.ExpressionBody;
    }

    /// <summary>Every call to a <c>Calculate</c> helper in this body, with the literals it passed.</summary>
    public static List<CallSite> ReadCalls(SyntaxNode body, SemanticModel model)
    {
        var calls = new List<CallSite>();

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var call = ReadCall(invocation, model);
            if (call is not null)
            {
                calls.Add(call);
            }
        }

        return calls;
    }

    /// <summary>One call, or null when it is not a helper call or passed no literal worth recording.</summary>
    public static CallSite? ReadCall(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var invoked = InvokedName(invocation);
        if (invoked is null || !invoked.StartsWith("Calculate", StringComparison.Ordinal))
        {
            return null;
        }

        // The parameter each argument binds to is the whole point; without the symbol there is no way to
        // know, and helpers with mirrored orders make an index actively wrong.
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol symbol)
        {
            return null;
        }

        var call = new CallSite { MethodName = symbol.Name };
        var arguments = invocation.ArgumentList.Arguments;

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var parameter = ParameterFor(argument, i, symbol);
            if (parameter is null)
            {
                continue;
            }

            if (argument.Expression is MemberAccessExpressionSyntax member
                && member.Expression is IdentifierNameSyntax enumName
                && enumName.Identifier.Text == "IndicatorName")
            {
                call.IndicatorArguments[parameter] = member.Name.Identifier.Text;
            }
            else if (argument.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                call.StringArguments[parameter] = literal.Token.ValueText;
            }
        }

        return call.IndicatorArguments.Count == 0 && call.StringArguments.Count == 0 ? null : call;
    }

    /// <summary>The name of the method an invocation calls, ignoring what it is called on.</summary>
    public static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => null
    };

    private static string? ParameterFor(ArgumentSyntax argument, int index, IMethodSymbol symbol)
    {
        var named = argument.NameColon?.Name.Identifier.Text;
        if (named is not null)
        {
            return named;
        }

        return index < symbol.Parameters.Length ? symbol.Parameters[index].Name : null;
    }
}
