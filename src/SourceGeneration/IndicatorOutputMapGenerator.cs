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

        // Guarded for the same reason as the accessors: this generator ships in the package and runs
        // in consumer compilations too, where emitting GeneratedIndicatorOutputs again would collide
        // with the copy already compiled into this library.
        var guarded = context.CompilationProvider.Combine(indicators);

        context.RegisterSourceOutput(guarded, static (spc, source) =>
        {
            if (source.Left.AssemblyName != "OoplesFinance.StockIndicators")
            {
                return;
            }

            Emit(spc, source.Right);
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

    private static void Emit(SourceProductionContext context, ImmutableArray<PublishedOutputs?> items)
    {
        // One indicator can be published from more than one method; keep the richest set of keys.
        var byIndicator = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var item in items)
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
        sb.AppendLine("/// Which output key each indicator publishes for each output slot, read from the");
        sb.AppendLine("/// SetOutputValues calls in the calculations themselves.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// A slot that is absent here is one the indicator does not publish. Callers must treat that");
        sb.AppendLine("/// as an error rather than substituting the primary series - doing so is what made");
        sb.AppendLine("/// Alligator, Gator, Aroon, Elder Ray and Trix hand back the same series for every band.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine("public static class GeneratedIndicatorOutputs");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly Dictionary<(IndicatorName, IndicatorOutput), string> Map = new()");
        sb.AppendLine("    {");

        var pairs = 0;
        foreach (var kvp in byIndicator)
        {
            var indicator = kvp.Key;
            var keys = kvp.Value;
            var assigned = new Dictionary<string, string>(StringComparer.Ordinal);
            var remaining = new List<string>(keys);

            // A key that names a slot claims that slot.
            foreach (var slot in NamedSlots)
            {
                var match = remaining.FirstOrDefault(k => string.Equals(k, slot, StringComparison.Ordinal));
                if (match is not null)
                {
                    assigned[slot] = match;
                    remaining.Remove(match);
                }
            }

            // Everything else fills the slots still free, in publication order.
            foreach (var key in remaining)
            {
                var slot = SlotOrder.FirstOrDefault(s => !assigned.ContainsKey(s));
                if (slot is null)
                {
                    break;
                }

                assigned[slot] = key;
            }

            if (assigned.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"        // {indicator}: publishes {string.Join(", ", keys)}");
            foreach (var slot in SlotOrder)
            {
                if (assigned.TryGetValue(slot, out var key))
                {
                    sb.AppendLine($"        {{ (IndicatorName.{indicator}, IndicatorOutput.{slot}), \"{key}\" }},");
                    pairs++;
                }
            }
        }

        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Number of indicator and slot pairs known from the source: {pairs}.</summary>");
        sb.AppendLine($"    public static int Count => {pairs};");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets the key this indicator publishes for this slot, if it publishes one.</summary>");
        sb.AppendLine("    public static bool TryGetKey(IndicatorName name, IndicatorOutput output, out string key) =>");
        sb.AppendLine("        Map.TryGetValue((name, output), out key!);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets every slot this indicator publishes, for diagnostics.</summary>");
        sb.AppendLine("    public static IReadOnlyList<string> KeysFor(IndicatorName name)");
        sb.AppendLine("    {");
        sb.AppendLine("        var found = new List<string>();");
        sb.AppendLine("        foreach (var entry in Map)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (entry.Key.Item1 == name)");
        sb.AppendLine("            {");
        sb.AppendLine("                found.Add(entry.Value);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return found;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("GeneratedIndicatorOutputs.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
