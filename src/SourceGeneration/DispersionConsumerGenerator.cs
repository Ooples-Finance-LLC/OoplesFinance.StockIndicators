using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Every indicator that takes its dispersion from <c>CalculateStandardDeviationVolatility</c>, and the
/// series each one asks for it on, read out of the calculations themselves.
/// </summary>
/// <remarks>
/// <para>
/// That method is not a standard deviation. It squares each bar's distance from the moving average at
/// that bar and averages those, which is the mean squared residual from the moving-average line - about
/// 55% wider than the deviation of the window about its own mean on a typical price series, as
/// GetStandardDeviationList's own remarks record. An indicator that builds a band at k sigma, or divides
/// by sigma, wants the second one. See issue #190.
/// </para>
/// <para>
/// This exists because counting these by hand went wrong. The issue reports 79 call sites; there are 41,
/// and the difference is doc comments, a see-also in another method's remarks, and helper files that
/// only name the method in prose. A list read from the syntax cannot drift from the code, cannot be
/// miscounted, and covers an indicator added tomorrow without anyone remembering to add it - the same
/// reason IndicatorInvariantTests takes its set from GetSupportedIndicators rather than a list of names.
/// </para>
/// <para>
/// The chained series is the point of the second field. 25 of the sites set CustomValuesList to some
/// other series immediately before asking for the dispersion - a true range, a log return, an on balance
/// volume - which is the chaining mechanism used deliberately, in the sense #173 settled. Those are
/// asking for the dispersion OF THAT SERIES, so their replacement is the windowed deviation of the same
/// list and not of the price input. Recording which is which is what stops the migration from quietly
/// redirecting an indicator onto the wrong series.
/// </para>
/// <para>
/// It emits an inventory, not a verdict. Whether a given consumer should move is a judgement about that
/// indicator's published definition, and nothing here can make it: a reference implementation derived
/// from the code under test would agree with the code's own mistakes.
/// </para>
/// </remarks>
[Generator]
public class DispersionConsumerGenerator : IIncrementalGenerator
{
    private const string Dispersion = "CalculateStandardDeviationVolatility";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var consumers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateMethod(node),
                transform: static (ctx, _) => Extract(ctx.Node))
            .Where(static x => x is not null)
            .Collect();

        // Guarded for the same reason as the other generators here: this ships in the package and runs in
        // consumer compilations too, where emitting the type again would collide with the copy already
        // compiled into this library.
        var guarded = context.CompilationProvider.Combine(consumers);

        context.RegisterSourceOutput(guarded, static (spc, source) =>
        {
            if (source.Left.AssemblyName != "OoplesFinance.StockIndicators")
            {
                return;
            }

            Emit(spc, source.Right);
        });
    }

    /// <summary>One indicator, and the series it takes the dispersion of at one call site.</summary>
    public sealed class DispersionUse
    {
        public string IndicatorName { get; set; } = string.Empty;

        /// <summary>The list chained in before the call, or empty when it reads the resolved input.</summary>
        public string ChainedSeries { get; set; } = string.Empty;
    }

    private static bool IsCandidateMethod(SyntaxNode node) =>
        node is MethodDeclarationSyntax method
        && method.Identifier.Text.StartsWith("Calculate", System.StringComparison.Ordinal)
        && method.Body is not null;

    private static ImmutableArray<DispersionUse>? Extract(SyntaxNode node)
    {
        var method = (MethodDeclarationSyntax)node;
        var body = method.Body;
        if (body is null || method.Identifier.Text == Dispersion)
        {
            return null;
        }

        var indicatorName = ReadIndicatorName(body);
        if (indicatorName is null)
        {
            return null;
        }

        // Source order, so the chained series in force at each call is the last one set before it.
        var chained = string.Empty;
        var uses = ImmutableArray.CreateBuilder<DispersionUse>();

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = InvokedName(invocation);
            if (name == "SetCustomValues")
            {
                var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;

                // Clearing it is the guard against a leaked average, not a chain onto something.
                chained = argument is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax
                    ? string.Empty
                    : argument?.ToString() ?? string.Empty;
                continue;
            }

            if (name == "WithValues")
            {
                chained = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString() ?? string.Empty;
                continue;
            }

            if (name == Dispersion)
            {
                uses.Add(new DispersionUse { IndicatorName = indicatorName, ChainedSeries = chained });
            }
        }

        return uses.Count > 0 ? uses.ToImmutable() : null;
    }

    private static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => null
    };

    private static string? ReadIndicatorName(BlockSyntax body)
    {
        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is MemberAccessExpressionSyntax left
                && left.Name.Identifier.Text == "IndicatorName"
                && assignment.Right is MemberAccessExpressionSyntax right
                && right.Expression is IdentifierNameSyntax enumName
                && enumName.Identifier.Text == "IndicatorName")
            {
                var value = right.Name.Identifier.Text;
                return value == "None" ? null : value;
            }
        }

        return null;
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<ImmutableArray<DispersionUse>?> items)
    {
        var all = items
            .SelectMany(item => item ?? ImmutableArray<DispersionUse>.Empty)
            .ToList();

        if (all.Count == 0)
        {
            return;
        }

        var ordered = all
            .OrderBy(u => u.IndicatorName, System.StringComparer.Ordinal)
            .ThenBy(u => u.ChainedSeries, System.StringComparer.Ordinal)
            .ToList();

        var chainedCount = ordered.Count(u => u.ChainedSeries.Length > 0);

        var sb = GeneratedSource.OpenStaticClass(
            new[] { "System.Collections.Generic", "OoplesFinance.StockIndicators.Enums" },
            "OoplesFinance.StockIndicators.Builder",
            new[]
            {
                "Every indicator taking its dispersion from CalculateStandardDeviationVolatility, and the",
                "series each asks for it on. Read from the calculations, so it cannot drift from them."
            },
            "GeneratedDispersionConsumers");
        sb.AppendLine("    /// <summary>One call site: the indicator, and the series chained in before the call.</summary>");
        sb.AppendLine("    public readonly struct Use");
        sb.AppendLine("    {");
        sb.AppendLine("        public Use(IndicatorName indicator, string chainedSeries)");
        sb.AppendLine("        {");
        sb.AppendLine("            Indicator = indicator;");
        sb.AppendLine("            ChainedSeries = chainedSeries;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public IndicatorName Indicator { get; }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Empty when the call reads the resolved input rather than a chained series.</summary>");
        sb.AppendLine("        public string ChainedSeries { get; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static readonly Use[] Sites =");
        sb.AppendLine("    {");

        foreach (var use in ordered)
        {
            sb.AppendLine($"        new Use(IndicatorName.{use.IndicatorName}, \"{use.ChainedSeries}\"),");
        }

        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Every call site, ordered by indicator.</summary>");
        sb.AppendLine("    public static IReadOnlyList<Use> All => Sites;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Call sites read from the source: {ordered.Count}.</summary>");
        sb.AppendLine($"    public static int Count => {ordered.Count};");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Sites asking for the dispersion of a series chained in first: "
            + chainedCount + ".</summary>");
        sb.AppendLine($"    public static int ChainedCount => {chainedCount};");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Every distinct indicator that consumes it.</summary>");
        sb.AppendLine("    public static IReadOnlyList<IndicatorName> Indicators");
        sb.AppendLine("    {");
        sb.AppendLine("        get");
        sb.AppendLine("        {");
        sb.AppendLine("            var seen = new List<IndicatorName>();");
        sb.AppendLine("            foreach (var site in Sites)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!seen.Contains(site.Indicator))");
        sb.AppendLine("                {");
        sb.AppendLine("                    seen.Add(site.Indicator);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return seen;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("GeneratedDispersionConsumers.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
