using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Reads the indicators whose name and output keys reach <c>SetOutputValues</c> through a shared private
/// helper rather than as literals, which is the one shape the other readers cannot see.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IndicatorOutputMapGenerator"/> reads a calculation that says
/// <c>stockData.IndicatorName = IndicatorName.X</c> and publishes string-literal keys. Four indicators say
/// neither: two pairs share a helper and pass it both the name and the keys, so the helper's own body names
/// a parameter and the callers' bodies name nothing at all. Each pair therefore had no map entry, and the
/// Builder answered every named slot with "Available outputs: none" while the calculation itself was
/// correct. See issue #199.
/// </para>
/// <para>
/// Resolution is by parameter, never by position. The two helpers take their arguments in mirrored orders -
/// <c>(..., IndicatorName indicatorName, string vidya1Key, string vidya2Key)</c> against
/// <c>(..., string outputKey, IndicatorName name)</c> - so an index would read one of them backwards. The
/// semantic model gives the parameter each argument binds to, which also makes named arguments and
/// defaulted parameters fall out for free.
/// </para>
/// <para>
/// A reading carries plain strings and no symbols, so it stays comparable between incremental runs, and
/// anything it cannot resolve to a literal is dropped rather than guessed at. Emitting a wrong key would be
/// worse than emitting none: a wrong one resolves and returns the wrong series, where a missing one raises.
/// </para>
/// </remarks>
public static class HelperRoutedOutputs
{
    /// <summary>One output key, either written literally or named by one of the helper's parameters.</summary>
    public sealed class KeyRef
    {
        public KeyRef(string value, bool isParameter)
        {
            Value = value;
            IsParameter = isParameter;
        }

        /// <summary>The key itself, or the parameter's name when <see cref="IsParameter"/>.</summary>
        public string Value { get; }

        public bool IsParameter { get; }
    }

    /// <summary>A helper that stamps a name or publishes a key it was handed by its caller.</summary>
    public sealed class HelperDeclaration
    {
        public string MethodName { get; set; } = string.Empty;

        /// <summary>The parameter feeding the IndicatorName assignment, empty when it stamps a literal.</summary>
        public string NameParameter { get; set; } = string.Empty;

        /// <summary>The literal it stamps, when it stamps one.</summary>
        public string LiteralName { get; set; } = string.Empty;

        /// <summary>The keys it publishes, in publication order.</summary>
        public List<KeyRef> Keys { get; } = new List<KeyRef>();
    }

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

    /// <summary>What one method contributed: a helper declaration, calls to helpers, or both.</summary>
    public sealed class Reading
    {
        public HelperDeclaration? Declaration { get; set; }

        public List<CallSite> Calls { get; } = new List<CallSite>();
    }

    /// <summary>
    /// Any <c>Calculate</c> method, including the expression-bodied ones the other readers skip because
    /// they test <c>Body is not null</c> - two of the four wrappers here are a single <c>=&gt;</c> call.
    /// </summary>
    public static bool IsCandidate(SyntaxNode node) =>
        node is MethodDeclarationSyntax method
        && method.Identifier.Text.StartsWith("Calculate", StringComparison.Ordinal)
        && (method.Body is not null || method.ExpressionBody is not null);

    /// <summary>Reads one method, or null when it neither declares nor calls a routed helper.</summary>
    public static Reading? Read(GeneratorSyntaxContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        SyntaxNode? body = method.Body;
        body ??= method.ExpressionBody;
        if (body is null)
        {
            return null;
        }

        var parameters = new HashSet<string>(
            method.ParameterList.Parameters.Select(p => p.Identifier.Text), StringComparer.Ordinal);

        var reading = new Reading
        {
            Declaration = ReadDeclaration(method.Identifier.Text, body, parameters)
        };

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var call = ReadCallSite(invocation, context.SemanticModel);
            if (call is not null)
            {
                reading.Calls.Add(call);
            }
        }

        return reading.Declaration is null && reading.Calls.Count == 0 ? null : reading;
    }

    /// <summary>
    /// Joins each call site to the helper it calls, producing the reading the helper would have given if it
    /// had been written with literals.
    /// </summary>
    public static IEnumerable<IndicatorOutputMapGenerator.PublishedOutputs> Resolve(
        ImmutableArray<Reading?> readings)
    {
        var helpers = new Dictionary<string, HelperDeclaration>(StringComparer.Ordinal);
        foreach (var reading in readings)
        {
            var declaration = reading?.Declaration;
            if (declaration is not null && !helpers.ContainsKey(declaration.MethodName))
            {
                helpers[declaration.MethodName] = declaration;
            }
        }

        var resolved = new List<IndicatorOutputMapGenerator.PublishedOutputs>();
        foreach (var reading in readings)
        {
            if (reading is null)
            {
                continue;
            }

            foreach (var call in reading.Calls)
            {
                var published = Resolve(call, helpers);
                if (published is not null)
                {
                    resolved.Add(published);
                }
            }
        }

        return resolved;
    }

    /// <summary>Every output key reachable through a helper, for the accessor generator.</summary>
    public static IEnumerable<string> ResolvedKeys(ImmutableArray<Reading?> readings) =>
        Resolve(readings).SelectMany(published => published.Keys);

    private static IndicatorOutputMapGenerator.PublishedOutputs? Resolve(
        CallSite call, Dictionary<string, HelperDeclaration> helpers)
    {
        if (!helpers.TryGetValue(call.MethodName, out var helper))
        {
            return null;
        }

        var name = helper.LiteralName;
        if (helper.NameParameter.Length > 0
            && !call.IndicatorArguments.TryGetValue(helper.NameParameter, out name))
        {
            // The caller passed something that is not a literal IndicatorName. Say nothing.
            return null;
        }

        if (string.IsNullOrEmpty(name) || name == "None")
        {
            return null;
        }

        var published = new IndicatorOutputMapGenerator.PublishedOutputs { IndicatorName = name };
        foreach (var key in helper.Keys)
        {
            var value = key.Value;
            if (key.IsParameter && !call.StringArguments.TryGetValue(key.Value, out value))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(value) && !published.Keys.Contains(value))
            {
                published.Keys.Add(value);
            }
        }

        return published.Keys.Count > 0 ? published : null;
    }

    private static HelperDeclaration? ReadDeclaration(string methodName, SyntaxNode body, HashSet<string> parameters)
    {
        var declaration = new HelperDeclaration { MethodName = methodName };

        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not MemberAccessExpressionSyntax left
                || left.Name.Identifier.Text != "IndicatorName")
            {
                continue;
            }

            if (assignment.Right is MemberAccessExpressionSyntax literal
                && literal.Expression is IdentifierNameSyntax enumName
                && enumName.Identifier.Text == "IndicatorName")
            {
                declaration.LiteralName = literal.Name.Identifier.Text;
            }
            else if (assignment.Right is IdentifierNameSyntax identifier
                && parameters.Contains(identifier.Identifier.Text))
            {
                declaration.NameParameter = identifier.Identifier.Text;
            }

            break;
        }

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (InvokedName(invocation) != "SetOutputValues")
            {
                continue;
            }

            foreach (var initializer in invocation.DescendantNodes().OfType<InitializerExpressionSyntax>())
            {
                foreach (var element in initializer.Expressions)
                {
                    if (element is not InitializerExpressionSyntax entry || entry.Expressions.Count == 0)
                    {
                        continue;
                    }

                    if (entry.Expressions[0] is LiteralExpressionSyntax key
                        && key.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        declaration.Keys.Add(new KeyRef(key.Token.ValueText, isParameter: false));
                    }
                    else if (entry.Expressions[0] is IdentifierNameSyntax named
                        && parameters.Contains(named.Identifier.Text))
                    {
                        declaration.Keys.Add(new KeyRef(named.Identifier.Text, isParameter: true));
                    }
                }
            }
        }

        // Only the routed shape is this reader's business. A calculation that names itself and its keys
        // outright is already read by IndicatorOutputMapGenerator.Extract, and reading it twice here would
        // put the same entry through the merge a second time.
        var routed = declaration.NameParameter.Length > 0 || declaration.Keys.Any(k => k.IsParameter);
        return routed && declaration.Keys.Count > 0 ? declaration : null;
    }

    private static CallSite? ReadCallSite(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var invoked = InvokedName(invocation);
        if (invoked is null || !invoked.StartsWith("Calculate", StringComparison.Ordinal))
        {
            return null;
        }

        // The parameter each argument binds to is the whole point; without the symbol there is no way to
        // know, and the two helpers' mirrored orders make an index actively wrong.
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

    private static string? ParameterFor(ArgumentSyntax argument, int index, IMethodSymbol symbol)
    {
        var named = argument.NameColon?.Name.Identifier.Text;
        if (named is not null)
        {
            return named;
        }

        return index < symbol.Parameters.Length ? symbol.Parameters[index].Name : null;
    }

    private static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => null
    };
}
