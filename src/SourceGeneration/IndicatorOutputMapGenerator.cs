using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Emits the mapping from an indicator's <c>IndicatorOutput</c> slot to the output key it actually
/// publishes, read out of the calculations themselves.
/// </summary>
/// <remarks>
/// <para>
/// The map this replaces was written by hand and covered about twenty-five of seven hundred indicators.
/// Everything else fell through to a guess - <c>IndicatorOutput.Signal</c> became the literal string
/// "Signal" - and when that key did not exist the caller silently returned the primary series instead.
/// So a result with three distinct bands handed back the same band three times, with nothing to
/// indicate anything had gone wrong. Alligator, Gator, Aroon, Elder Ray and Trix all behaved that way.
/// </para>
/// <para>
/// Reading the keys from the <c>SetOutputValues</c> calls removes the guess. A slot either has a key
/// this indicator genuinely publishes, or it has none and the caller is told so.
/// </para>
/// </remarks>
[Generator]
public class IndicatorOutputMapGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Slots whose name is itself meaningful: when an indicator publishes a key of the same name, that
    /// is what the slot refers to. Remaining keys fill the remaining slots in the order they are
    /// published.
    /// </summary>
    private static readonly string[] NamedSlots =
    {
        "Signal", "Histogram", "UpperBand", "MiddleBand", "LowerBand"
    };

    private static readonly string[] SlotOrder =
    {
        "Primary", "Signal", "Histogram", "UpperBand", "MiddleBand", "LowerBand"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var indicators = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateMethod(node),
                transform: static (ctx, _) => Extract(ctx.Node))
            .Where(static x => x is not null)
            .Collect();

        // The indicators that hand their name and keys to a shared helper instead of naming them here.
        // Extract cannot see those: the helper's body names a parameter and the callers' bodies name
        // nothing, so the join has to happen once everything has been collected. See issue #199.
        var helperRouted = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => HelperRoutedOutputs.IsCandidate(node),
                transform: static (ctx, _) => HelperRoutedOutputs.Read(ctx))
            .Where(static x => x is not null)
            .Collect();

        // Guarded for the same reason as the accessors: this generator ships in the package and runs
        // in consumer compilations too, where emitting GeneratedIndicatorOutputs again would collide
        // with the copy already compiled into this library.
        var guarded = context.CompilationProvider.Combine(indicators).Combine(helperRouted);

        context.RegisterSourceOutput(guarded, static (spc, source) =>
        {
            if (source.Left.Left.AssemblyName != "OoplesFinance.StockIndicators")
            {
                return;
            }

            Emit(spc, source.Left.Right, source.Right);
        });
    }

    /// <summary>
    /// Whether this node is an indicator calculation whose published outputs can be read.
    /// </summary>
    /// <remarks>Shared with the catalog generator so both read the same source of truth.</remarks>
    public static bool IsCalculationMethod(SyntaxNode node) => IsCandidateMethod(node);

    /// <inheritdoc cref="Extract"/>
    public static PublishedOutputs? ReadPublishedOutputs(SyntaxNode node) => Extract(node);

    private static bool IsCandidateMethod(SyntaxNode node) =>
        node is MethodDeclarationSyntax method
        && method.Identifier.Text.StartsWith("Calculate", StringComparison.Ordinal)
        && method.Body is not null;

    /// <summary>
    /// One indicator and the output names it publishes, in the order it publishes them.
    /// </summary>
    public sealed class PublishedOutputs
    {
        public string IndicatorName { get; set; } = string.Empty;

        public List<string> Keys { get; } = new List<string>();
    }

    private static PublishedOutputs? Extract(SyntaxNode node)
    {
        var method = (MethodDeclarationSyntax)node;
        var body = method.Body;
        if (body is null)
        {
            return null;
        }

        string? indicatorName = null;
        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is MemberAccessExpressionSyntax left
                && left.Name.Identifier.Text == "IndicatorName"
                && assignment.Right is MemberAccessExpressionSyntax right
                && right.Expression is IdentifierNameSyntax enumName
                && enumName.Identifier.Text == "IndicatorName")
            {
                indicatorName = right.Name.Identifier.Text;
                break;
            }
        }

        if (indicatorName is null || indicatorName == "None")
        {
            return null;
        }

        var result = new PublishedOutputs { IndicatorName = indicatorName };

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var isSetOutputs = invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text == "SetOutputValues",
                IdentifierNameSyntax identifier => identifier.Identifier.Text == "SetOutputValues",
                _ => false
            };

            if (!isSetOutputs)
            {
                continue;
            }

            foreach (var initializer in invocation.DescendantNodes().OfType<InitializerExpressionSyntax>())
            {
                foreach (var element in initializer.Expressions)
                {
                    if (element is InitializerExpressionSyntax entry
                        && entry.Expressions.Count > 0
                        && entry.Expressions[0] is LiteralExpressionSyntax literal
                        && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        var key = literal.Token.ValueText;
                        if (key.Length > 0 && !result.Keys.Contains(key))
                        {
                            result.Keys.Add(key);
                        }
                    }
                }
            }
        }

        return result.Keys.Count > 0 ? result : null;
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<PublishedOutputs?> items,
        ImmutableArray<HelperRoutedOutputs.Reading?> helperReadings)
    {
        // The routed indicators join in here rather than in Extract, which sees one method at a time and so
        // can never pair a helper's parameter with the literal its caller passed.
        var readings = new List<PublishedOutputs?>(items);
        readings.AddRange(HelperRoutedOutputs.Resolve(helperReadings));

        // One indicator can be published from more than one method; keep the richest set of keys.
        var byIndicator = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var item in readings)
        {
            if (item is null)
            {
                continue;
            }

            if (!byIndicator.TryGetValue(item.IndicatorName, out var existing) || item.Keys.Count > existing.Count)
            {
                byIndicator[item.IndicatorName] = item.Keys;
            }
        }

        if (byIndicator.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using OoplesFinance.StockIndicators.Enums;");
        sb.AppendLine();
        sb.AppendLine("namespace OoplesFinance.StockIndicators.Builder;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Every output key each indicator publishes, read from the SetOutputValues calls in the");
        sb.AppendLine("/// calculations themselves.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// A key that is absent here is one the indicator does not publish. Callers must treat that");
        sb.AppendLine("/// as an error rather than substituting the primary series - doing so is what made");
        sb.AppendLine("/// Alligator, Gator, Aroon, Elder Ray and Trix hand back the same series for every band.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine("public static class GeneratedIndicatorOutputs");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly Dictionary<IndicatorName, string[]> Map = new()");
        sb.AppendLine("    {");

        var pairs = 0;
        foreach (var kvp in byIndicator)
        {
            var indicator = kvp.Key;
            var keys = kvp.Value;
            if (keys.Count == 0)
            {
                continue;
            }

            // Every key the indicator publishes, in publication order. This used to be squeezed into six
            // positional slots: a key matching a slot name claimed it, the rest filled whatever was free, and
            // anything past the sixth was dropped. That is what made IndicatorOutput.UpperBand answer with
            // Ch-2 - a lower channel - for TimeAndMoneyChannel, and what left 11 of CamarillaPivotPoints' 17
            // keys unreachable at any slot. Naming them removes the ceiling rather than raising it. See #219.
            sb.AppendLine($"        {{ IndicatorName.{indicator}, new[] {{ {string.Join(", ", keys.Select(k => $"\"{k}\""))} }} }},");
            pairs += keys.Count;
        }

        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Number of published output keys known from the source: {pairs}.</summary>");
        sb.AppendLine($"    public static int Count => {pairs};");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Whether this indicator publishes an output under this name.</summary>");
        sb.AppendLine("    public static bool Publishes(IndicatorName name, string outputKey) =>");
        sb.AppendLine("        Map.TryGetValue(name, out var keys) && System.Array.IndexOf(keys, outputKey) >= 0;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Every output this indicator publishes, in publication order.</summary>");
        sb.AppendLine("    public static IReadOnlyList<string> KeysFor(IndicatorName name) =>");
        sb.AppendLine("        Map.TryGetValue(name, out var keys) ? keys : System.Array.Empty<string>();");
        sb.AppendLine("}");

        context.AddSource("GeneratedIndicatorOutputs.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
