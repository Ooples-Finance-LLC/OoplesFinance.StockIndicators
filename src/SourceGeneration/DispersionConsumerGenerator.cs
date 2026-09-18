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
/// This exists because counting these by hand went wrong. Issue #190 reports 79 call sites; the count is
/// nowhere near that, and the difference is doc comments, a see-also in another method's remarks, and
/// helper files that only name the method in prose. A list read from the syntax cannot drift from the
/// code, cannot be miscounted, and covers an indicator added tomorrow without anyone remembering to add
/// it - the same reason IndicatorInvariantTests takes its set from GetSupportedIndicators rather than a
/// list of names.
/// </para>
/// <para>
/// The counts themselves live on the emitted type, as <c>Count</c> and <c>ChainedCount</c>, and not in
/// this remark. A number written here is a second copy that goes stale the moment an indicator is added:
/// this comment claimed 41 sites and 25 chained while the generator was emitting 32 and 19. See #222.
/// </para>
/// <para>
/// The chained series is the point of the second field. Many of the sites set CustomValuesList to some
/// other series immediately before asking for the dispersion - a true range, a log return, an on balance
/// volume - which is the chaining mechanism used deliberately, in the sense #173 settled. Those are
/// asking for the dispersion OF THAT SERIES, so their replacement is the windowed deviation of the same
/// list and not of the price input. Recording which is which is what stops the migration from quietly
/// redirecting an indicator onto the wrong series.
/// </para>
/// <para>
/// Indicators that route the call through a shared private helper are read too. Two wrappers over
/// <c>CalculateVolatilityIndexDynamicAverage</c> hand it their name as an argument, so the helper's body
/// names a parameter and neither wrapper's body names anything - the same blind spot #199 fixed for the
/// output map. One dispersion call inside that helper therefore belongs to both indicators, and is
/// recorded once for each. See #222.
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
                predicate: static (node, _) => HelperCallSites.IsCandidate(node),
                transform: static (ctx, _) => Read(ctx))
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

    /// <summary>A helper's dispersion calls, and the parameter its indicator name arrives in.</summary>
    public sealed class RoutedHelper
    {
        public string MethodName { get; set; } = string.Empty;

        /// <summary>The parameter the caller passes the indicator name in.</summary>
        public string NameParameter { get; set; } = string.Empty;

        /// <summary>The series chained in at each dispersion call, in source order, one entry per call.</summary>
        public List<string> ChainedSeries { get; } = new List<string>();
    }

    /// <summary>What one method contributed: its own uses, a routed helper, or calls to one.</summary>
    public sealed class Reading
    {
        /// <summary>Uses whose indicator this method names outright.</summary>
        public List<DispersionUse> Uses { get; } = new List<DispersionUse>();

        /// <summary>Set when this method takes its indicator name as a parameter and uses the dispersion.</summary>
        public RoutedHelper? Helper { get; set; }

        /// <summary>Calls this method makes to a helper, and the literals it passed.</summary>
        public List<HelperCallSites.CallSite> Calls { get; } = new List<HelperCallSites.CallSite>();
    }

    private static Reading? Read(GeneratorSyntaxContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Identifier.Text == Dispersion)
        {
            return null;
        }

        var body = HelperCallSites.BodyOf(method);
        if (body is null)
        {
            return null;
        }

        var reading = new Reading();

        // A wrapper's body is often nothing but the call to its helper, so the calls are read whether or not
        // this method uses the dispersion itself.
        reading.Calls.AddRange(HelperCallSites.ReadCalls(body, context.SemanticModel));

        var chainedAt = ReadDispersionChains(body);
        if (chainedAt.Count > 0)
        {
            var parameters = new HashSet<string>(
                method.ParameterList.Parameters.Select(p => p.Identifier.Text), System.StringComparer.Ordinal);
            var (literalName, nameParameter) = ReadIndicatorName(body, parameters);

            if (literalName is not null)
            {
                foreach (var chained in chainedAt)
                {
                    reading.Uses.Add(new DispersionUse { IndicatorName = literalName, ChainedSeries = chained });
                }
            }
            else if (nameParameter is not null)
            {
                // The helper cannot say which indicator it is; only its callers can. Recorded now, joined
                // once every method has been seen. See issue #222.
                var helper = new RoutedHelper
                {
                    MethodName = method.Identifier.Text,
                    NameParameter = nameParameter
                };
                helper.ChainedSeries.AddRange(chainedAt);
                reading.Helper = helper;
            }
        }

        return reading.Uses.Count == 0 && reading.Helper is null && reading.Calls.Count == 0 ? null : reading;
    }

    /// <summary>The series chained in at each dispersion call in this body, in source order.</summary>
    private static List<string> ReadDispersionChains(SyntaxNode body)
    {
        // Source order, so the chained series in force at each call is the last one set before it.
        var chained = string.Empty;
        var chains = new List<string>();

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = HelperCallSites.InvokedName(invocation);
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
                chains.Add(chained);
            }
        }

        return chains;
    }

    /// <summary>The indicator this body stamps: a literal, or the parameter it is handed.</summary>
    private static (string? Literal, string? Parameter) ReadIndicatorName(SyntaxNode body, HashSet<string> parameters)
    {
        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not MemberAccessExpressionSyntax left
                || left.Name.Identifier.Text != "IndicatorName")
            {
                continue;
            }

            if (assignment.Right is MemberAccessExpressionSyntax right
                && right.Expression is IdentifierNameSyntax enumName
                && enumName.Identifier.Text == "IndicatorName")
            {
                var value = right.Name.Identifier.Text;
                return (value == "None" ? null : value, null);
            }

            if (assignment.Right is IdentifierNameSyntax identifier
                && parameters.Contains(identifier.Identifier.Text))
            {
                return (null, identifier.Identifier.Text);
            }

            break;
        }

        return (null, null);
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<Reading?> items)
    {
        // The helpers first, because a call site can appear before the helper it calls.
        var helpers = new Dictionary<string, RoutedHelper>(System.StringComparer.Ordinal);
        foreach (var reading in items)
        {
            var helper = reading?.Helper;
            if (helper is not null && !helpers.ContainsKey(helper.MethodName))
            {
                helpers[helper.MethodName] = helper;
            }
        }

        var all = new List<DispersionUse>();
        foreach (var reading in items)
        {
            if (reading is null)
            {
                continue;
            }

            all.AddRange(reading.Uses);

            foreach (var call in reading.Calls)
            {
                if (!helpers.TryGetValue(call.MethodName, out var helper)
                    || !call.IndicatorArguments.TryGetValue(helper.NameParameter, out var name)
                    || string.IsNullOrEmpty(name)
                    || name == "None")
                {
                    // Not a routed dispersion helper, or the caller passed something that is not a literal
                    // IndicatorName. Say nothing rather than guess.
                    continue;
                }

                // One call inside the helper belongs to every indicator that routes through it, so it is
                // recorded once per caller. Two wrappers over one helper means two entries. See issue #222.
                foreach (var chained in helper.ChainedSeries)
                {
                    all.Add(new DispersionUse { IndicatorName = name, ChainedSeries = chained });
                }
            }
        }

        // Emitted even when nothing is left to record. This used to return early, from a time when an empty
        // inventory could only mean the reader had stopped seeing - the #222 failure, where two helper-routed
        // indicators went missing and every test passed. Now that #190's conversion is complete, empty is the
        // expected end state, and returning would delete the very type that says so: the inventory would
        // vanish, its tests would not compile, and nothing would notice a consumer reintroducing the old
        // quantity. An empty inventory that exists is a regression guard; one that is absent is silence.
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
