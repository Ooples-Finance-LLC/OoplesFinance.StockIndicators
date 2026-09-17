using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Roslyn source generator that creates typed indicator methods for IndicatorCatalog
/// and optimized compute methods for IndicatorCompute.
/// Scans the IndicatorName enum and generates methods for indicators that don't have hand-written overloads.
/// </summary>
[Generator]
public class IndicatorCatalogGenerator : IIncrementalGenerator
{
    // Hand-written indicators that should not be generated for IndicatorCatalog
    private static readonly HashSet<string> HandWrittenIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "SimpleMovingAverage",
        "ExponentialMovingAverage",
        "RelativeStrengthIndex",
        "MovingAverageConvergenceDivergence",
        "BollingerBands",
        "AverageTrueRange",
        "AverageDirectionalIndex",
        "StochasticOscillator",
        "WeightedMovingAverage",
        "HullMovingAverage",
        "TripleExponentialMovingAverage",
        "DoubleExponentialMovingAverage",
        "CommodityChannelIndex",
        "WilliamsR",
        "RateOfChange",
        "MomentumOscillator",
        "ParabolicSAR",
        "KeltnerChannels",
        "DonchianChannels",
        "VolumeWeightedAveragePrice",
        "OnBalanceVolume",
        "MoneyFlowIndex",
        "IchimokuCloud",
        "StandardDeviation",
        "TrueStrengthIndex"
    };


    // Which indicators publish more than one output, and under what names, is read out of the
    // SetOutputValues calls in the calculations rather than listed here. The list that used to live
    // here had drifted from the code in five of its six entries: it claimed AlligatorIndex publishes
    // "Jaw" (it publishes "Jaws", and in a different order), that GatorOscillator publishes
    // Upper/Lower (it publishes Top/Bottom), and that Trix and AroonOscillator have a second output at
    // all - they each publish exactly one. Handles were generated for outputs that do not exist, and
    // resolving them silently returned the primary series, so every band of a multi-output result came
    // back identical.

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Always generate a test file to verify the generator is running
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource("IndicatorCatalog.Generated.Marker.g.cs", SourceText.From(
                "// Generator is running - marker file\n" +
                "namespace OoplesFinance.StockIndicators.Builder.Catalogs;\n" +
                "public sealed partial class IndicatorCatalog { /* Generated marker */ }\n",
                Encoding.UTF8));
        });

        // Find enum declarations named "IndicatorName"
        var enumDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateEnum(s),
                transform: static (ctx, _) => GetEnumSemanticTarget(ctx))
            .Where(static m => m is not null);

        // Read each indicator's published output names out of its calculation, so the catalog's
        // multi-output result types cannot describe outputs the indicators do not have.
        var publishedOutputs = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IndicatorOutputMapGenerator.IsCalculationMethod(node),
                transform: static (ctx, _) => IndicatorOutputMapGenerator.ReadPublishedOutputs(ctx.Node))
            .Where(static x => x is not null)
            .Collect();

        // Combine with compilation
        var compilationAndEnums = context.CompilationProvider
            .Combine(enumDeclarations.Collect())
            .Combine(publishedOutputs);

        // Generate source
        context.RegisterSourceOutput(compilationAndEnums, static (spc, source) =>
            Execute(source.Left.Left, source.Left.Right!, BuildMultiOutputMap(source.Right), spc));
    }

    /// <summary>
    /// Collapses the per-method readings into one entry per indicator, keeping only those that publish
    /// more than one output - those are the ones that need a result type with named members.
    /// </summary>
    private static Dictionary<string, string[]> BuildMultiOutputMap(
        ImmutableArray<IndicatorOutputMapGenerator.PublishedOutputs?> readings)
    {
        var best = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var reading in readings)
        {
            if (reading is null)
            {
                continue;
            }

            if (!best.TryGetValue(reading.IndicatorName, out var existing) || reading.Keys.Count > existing.Count)
            {
                best[reading.IndicatorName] = reading.Keys;
            }
        }

        // Only the indicators that already had result types are given one. The names and the order now
        // come from the code instead of from a list, which is the point - but emitting a result type
        // for every one of the several hundred indicators that publish more than one output would
        // change the shape of the catalog far beyond fixing what was wrong, and the emitter below was
        // written for a handful of hand-picked cases. Widening that is a separate decision.
        var eligible = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AroonOscillator", "ElderRayIndex", "AlligatorIndex", "GatorOscillator", "Trix"
        };

        // PercentagePriceOscillator is deliberately absent. The old list keyed it as "PPO", which never
        // matched the IndicatorName member, so it has always returned a plain handle - adding it now
        // would be a new API change rather than a repair.

        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in best)
        {
            // An indicator publishing a single output is not multi-output, whatever a list once said.
            // Trix and AroonOscillator each publish exactly one, so they get a plain handle and the
            // fabricated .Signal, .Up and .Down members disappear rather than resolving to the primary.
            if (entry.Value.Count > 1 && eligible.Contains(entry.Key))
            {
                map[entry.Key] = entry.Value.ToArray();
            }
        }

        return map;
    }

    private static bool IsCandidateEnum(SyntaxNode node)
    {
        return node is EnumDeclarationSyntax enumDecl && enumDecl.Identifier.Text == "IndicatorName";
    }

    private static EnumDeclarationSyntax? GetEnumSemanticTarget(GeneratorSyntaxContext context)
    {
        var enumDecl = (EnumDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(enumDecl);

        if (symbol?.ContainingNamespace?.ToDisplayString() == "OoplesFinance.StockIndicators.Enums")
        {
            return enumDecl;
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<EnumDeclarationSyntax?> enums,
        Dictionary<string, string[]> multiOutputIndicators, SourceProductionContext context)
    {
        if (enums.IsDefaultOrEmpty)
        {
            return;
        }

        var enumDecl = enums.FirstOrDefault(e => e is not null);
        if (enumDecl is null)
        {
            return;
        }

        // Collect all indicator names for both generators
        var indicatorNames = new List<string>();
        foreach (var member in enumDecl.Members)
        {
            var name = member.Identifier.Text;
            if (name != "None")
            {
                indicatorNames.Add(name);
            }
        }

        // Generate IndicatorCatalog.g.cs
        GenerateIndicatorCatalog(context, indicatorNames, multiOutputIndicators);
    }

    private static void GenerateIndicatorCatalog(SourceProductionContext context, List<string> indicatorNames,
        Dictionary<string, string[]> MultiOutputIndicators)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using OoplesFinance.StockIndicators.Builder.Specs;");
        sb.AppendLine("using OoplesFinance.StockIndicators.Enums;");
        sb.AppendLine();
        sb.AppendLine("namespace OoplesFinance.StockIndicators.Builder.Catalogs;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated typed indicator methods for IndicatorCatalog.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed partial class IndicatorCatalog");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Turns the catalog's single optional length knob into the parameter array the streaming");
        sb.AppendLine("    /// state factory reads.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// An omitted length must produce an EMPTY array, not a placeholder value. Every state");
        sb.AppendLine("    /// constructor already declares its own default - 13 bars for the Alligator's jaw, 20 for a");
        sb.AppendLine("    /// Bollinger basis, 100 for the long-horizon Ehlers filters - and the factory falls back to it");
        sb.AppendLine("    /// only when the array is short. Passing a value here would override every one of them; the");
        sb.AppendLine("    /// catalog previously passed a hardcoded 14, which silently disagreed with the batch");
        sb.AppendLine("    /// Calculate* default for 390 of the 545 indicators that take an int length.");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    private static object[] LengthParameters(int? length) =>");
        sb.AppendLine("        length.HasValue ? new object[] { length.Value } : System.Array.Empty<object>();");

        var generatedCount = 0;
        foreach (var indicatorName in indicatorNames)
        {
            // Skip hand-written indicators
            if (HandWrittenIndicators.Contains(indicatorName))
            {
                continue;
            }

            // Generate method for this indicator
            var methodName = GetMethodName(indicatorName);
            var hasMultiOutput = MultiOutputIndicators.TryGetValue(indicatorName, out var outputs);

            sb.AppendLine();
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Calculates {FormatIndicatorName(indicatorName)}.");
            sb.AppendLine($"    /// </summary>");

            if (hasMultiOutput && outputs is not null)
            {
                // Multi-output indicator - generate method returning result type
                var resultTypeName = $"{methodName}Result";
                sb.AppendLine($"    /// <param name=\"length\">The lookback length, or <see langword=\"null\"/> to use this indicator's own default.</param>");
                sb.AppendLine($"    public {resultTypeName} {methodName}(int? length = null, SeriesHandle? input = null)");
                sb.AppendLine("    {");
                sb.AppendLine("        var series = input ?? Price();");
                sb.AppendLine("        var seriesKey = _builder.ResolveSeriesKey(series);");
                sb.AppendLine($"        var opts = new GenericIndicatorOptions(LengthParameters(length));");

                // Each handle names the key it wants. This used to assign slots by position - the first output
                // Primary, the second Signal, everything after that Histogram - so an indicator publishing four
                // keys gave its third and fourth handles the same slot and therefore the same series. Ichimoku
                // was exactly that: its Chikou span handle resolved to Senkou Span A. See issue #219.
                foreach (var output in outputs)
                {
                    sb.AppendLine($"        var {output.ToLowerInvariant()} = _builder.AddIndicator(IndicatorSpecs.Create(IndicatorName.{indicatorName}, opts, \"{output}\"), series, seriesKey, null);");
                }

                var outputArgs = string.Join(", ", outputs.Select(o => o.ToLowerInvariant()));
                sb.AppendLine($"        return new {resultTypeName}({outputArgs});");
                sb.AppendLine("    }");
            }
            else
            {
                // Single-output indicator
                sb.AppendLine($"    /// <param name=\"length\">The lookback length, or <see langword=\"null\"/> to use this indicator's own default.</param>");
                sb.AppendLine($"    public SeriesHandle {methodName}(int? length = null, SeriesHandle? input = null, IndicatorKey? key = null)");
                sb.AppendLine("    {");
                sb.AppendLine("        var series = input ?? Price();");
                sb.AppendLine($"        var spec = IndicatorSpecs.Create(IndicatorName.{indicatorName}, new GenericIndicatorOptions(LengthParameters(length)));");
                sb.AppendLine("        return _builder.AddIndicator(spec, series, _builder.ResolveSeriesKey(series), key);");
                sb.AppendLine("    }");
            }

            generatedCount++;
        }

        sb.AppendLine("}");

        // Generate result types for multi-output indicators
        if (MultiOutputIndicators.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// Multi-output result types");
            foreach (var kvp in MultiOutputIndicators)
            {
                if (HandWrittenIndicators.Contains(kvp.Key))
                {
                    continue;
                }

                var methodName = GetMethodName(kvp.Key);
                var outputs = kvp.Value;
                var resultTypeName = $"{methodName}Result";

                sb.AppendLine();
                sb.AppendLine($"/// <summary>");
                sb.AppendLine($"/// {FormatIndicatorName(kvp.Key)} multi-output result.");
                sb.AppendLine($"/// </summary>");
                sb.AppendLine($"public readonly struct {resultTypeName}");
                sb.AppendLine("{");

                // Constructor
                var ctorParams = string.Join(", ", outputs.Select(o => $"SeriesHandle {o.ToLowerInvariant()}"));
                sb.AppendLine($"    public {resultTypeName}({ctorParams})");
                sb.AppendLine("    {");
                foreach (var output in outputs)
                {
                    sb.AppendLine($"        {output} = {output.ToLowerInvariant()};");
                }
                sb.AppendLine("    }");
                sb.AppendLine();

                // Properties
                foreach (var output in outputs)
                {
                    sb.AppendLine($"    /// <summary>Gets the {output} series.</summary>");
                    sb.AppendLine($"    public SeriesHandle {output} {{ get; }}");
                }

                sb.AppendLine("}");
            }
        }

        context.AddSource("IndicatorCatalog.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }


    private static string GetMethodName(string indicatorName)
    {
        // Remove common prefixes/suffixes for cleaner method names
        var name = indicatorName;

        // Handle special naming conventions
        if (name.StartsWith("_"))
        {
            name = name.TrimStart('_');
        }

        // Handle numbered prefixes like "3HMA" -> "Hma3"
        if (char.IsDigit(name[0]))
        {
            var numberPart = new string(name.TakeWhile(char.IsDigit).ToArray());
            var restPart = name.Substring(numberPart.Length);
            name = restPart + numberPart;
        }

        return name;
    }

    private static string FormatIndicatorName(string indicatorName)
    {
        // Convert PascalCase to space-separated words
        var sb = new StringBuilder();
        foreach (var c in indicatorName)
        {
            if (char.IsUpper(c) && sb.Length > 0)
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
