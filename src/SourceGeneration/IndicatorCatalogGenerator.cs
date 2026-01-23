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

    // Indicators with hand-written fast path in IndicatorCompute.cs
    // These use span-based Core implementations for zero-allocation compute
    private static readonly HashSet<string> FastPathIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        // Moving Averages - MovingAverageCore
        "SimpleMovingAverage",
        "ExponentialMovingAverage",
        "WeightedMovingAverage",
        "DoubleExponentialMovingAverage",
        "TripleExponentialMovingAverage",
        "HullMovingAverage",
        "TriangularMovingAverage",
        "WellesWilderMovingAverage",
        "LinearRegression",
        "KaufmanAdaptiveMovingAverage",
        "ZeroLagExponentialMovingAverage",
        "SmoothedMovingAverage",
        "McGinleyDynamic",
        "T3MovingAverage",
        "VariableIndexDynamicAverage",
        "VariableMovingAverage",

        // Oscillators - OscillatorCore
        "RelativeStrengthIndex",
        "RateOfChange",
        "MomentumOscillator",
        "WilliamsR",
        "CommodityChannelIndex",
        "StochasticOscillator",
        "AverageDirectionalIndex",
        "ChandeMomentumOscillator",
        "PercentagePriceOscillator",
        "AbsolutePriceOscillator",
        "UltimateOscillator",
        "TrueStrengthIndex",
        "StochasticRsi",
        "AroonOscillator",
        "DetrendedPriceOscillator",
        "Trix",
        "MassIndex",

        // Volume - VolumeCore
        "OnBalanceVolume",
        "AccumulationDistributionLine",
        "ChaikinMoneyFlow",
        "ForceIndex",
        "VolumeRateOfChange",
        "NegativeVolumeIndex",
        "PositiveVolumeIndex",
        "PriceVolumeTrend",
        "VolumeWeightedAveragePrice",
        "ChaikinOscillator",
        "EaseOfMovement",

        // Volatility - VolatilityCore
        "AverageTrueRange",
        "StandardDeviation",
        "HistoricalVolatility",
        "ChaikinVolatility",
        "UlcerIndex",
        "NormalizedAverageTrueRange",
        "Variance",
        "CoefficientOfVariation",
        "TrueRange",
        "BollingerBands",

        // Volume - VolumeCore (Additional)
        "KlingerVolumeOscillator",
        "VolumePriceConfirmationIndicator",

        // Oscillators - OscillatorCore (Additional Batch 1)
        "MoneyFlowIndex",
        "BalanceOfPower",
        "RelativeVigorIndex",
        "AroonUp",
        "AroonDown",

        // Oscillators - OscillatorCore (Additional Batch 2)
        "StochasticFastD",
        "AwesomeOscillator",
        "AcceleratorOscillator",
        "PercentageVolumeOscillator",
        "FisherTransform",
        "ConnorsRsi",
        "PriceMomentumOscillator",
        "KnowSureThing",
        "PercentRank",
        "ChoppinessIndex",

        // Trend - TrendCore
        "ParabolicSAR",
        "SuperTrend",
        "DonchianChannels",
        "HighestHigh",
        "LowestLow",
        "AverageDayRange",
        "TypicalPrice",
        "MedianPrice",
        "WeightedClose",
        "PercentageChange",
        "LinearRegressionSlope",
        "RSquared",
        "StandardError",
        "VerticalHorizontalFilter",
        "KeltnerChannels",
        "TrendDetection",
        "PriceChannels",

        // Oscillators - OscillatorCore (Additional Batch 3)
        "AbsoluteStrengthIndex",
        "RelativeMomentumIndex",
        "IntradayMomentumIndex",
        "SwingIndex",
        "AccumulativeSwingIndex",

        // Oscillators - OscillatorCore (Additional Batch 4)
        "CoppockCurve",
        "ChandeForecastOscillator",
        "BullPowerIndicator",
        "BearPowerIndicator",
        "PolarizedFractalEfficiency",
        "SchaffTrendCycle",
        "PriceZoneOscillator",
        "ElderForceIndex",
        "PrettyGoodOscillator",
        "RelativeVolatilityIndex",
        "QstickIndicator",
        "SpecialK",

        // Moving Averages - MovingAverageCore (Additional Batch 2)
        "ArnaudLegouxMovingAverage",
        "LeastSquaresMovingAverage",
        "FractalAdaptiveMovingAverage",
        "AdaptiveMovingAverage",
        "SineWeightedMovingAverage",
        "HammingMovingAverage",
        "GeometricMovingAverage",
        "RegularizedExponentialMovingAverage",
        "ModifiedMovingAverage",

        // Trend - TrendCore (Additional Batch 2)
        "ZigZag",
        "ChandelierExit",
        "TrendIntensityIndex",
        "AveragePrice",
        "Midpoint",
        "Midprice",
        "PivotPointAverage",

        // Volume - VolumeCore (Additional Batch 2)
        "TradeVolumeIndex",
        "VolumeOscillator",
        "VolumeWeightedMovingAverage",
        "TwiggsMoneyFlow",
        "VolumeZoneOscillator",
        "DemandIndex",

        // Oscillators - OscillatorCore (Additional Batch 5)
        "DisparityIndex",
        "DirectionalTrendIndex",
        "DoubleSmoothedStochastic",
        "DynamicMomentumIndex",
        "AdaptiveErgodicCandlestickOscillator",
        "Demarker",
        "SmoothedRateOfChange",

        // Volatility - VolatilityCore (Additional)
        "KeltnerChannelWidth",
        "BollingerBandsWidth",
        "DonchianChannelWidth",
        "CloseToCloseVolatility",
        "ParkinsonVolatility",
        "GarmanKlassVolatility",

        // Moving Averages - MovingAverageCore (Additional Batch 3)
        "JurikMovingAverage",
        "ButterworthFilter",
        "SuperSmootherFilter",
        "EndPointMovingAverage",
        "CubedWeightedMovingAverage",
        "NaturalMovingAverage",

        // Oscillators - OscillatorCore (Additional Batch 6)
        "ElliottWaveOscillator",
        "ForecastOscillator",
        "DerivativeOscillator",
        "GatorOscillator",
        "FractalChaosOscillator",
        "RahulMohindarOscillator",
        "PremierStochasticOscillator",
        "Repulse",

        // Trend - TrendCore (Additional Batch 3)
        "GannHiLoActivator",
        "HalfTrend",
        "VortexIndicator",
        "LinearRegressionIntercept",
        "ElderImpulseSystem",
        "IchimokuTenkanSen",
        "IchimokuKijunSen",
        "MassThrust",

        // Volume - VolumeCore (Additional Batch 3)
        "WilliamsAD",
        "NetVolume",
        "CumulativeVolumeIndex",
        "VolumeMomentum",
        "VolumePriceTrend",
        "ElderRayBullPower",
        "ElderRayBearPower",
        "NormalizedVolume",
        "VolumeWeightedRsi",

        // Oscillators - OscillatorCore (Additional Batch 7)
        "ChandeCompositeMomentumIndex",
        "ChandeKrollRSquaredIndex",
        "BayesianOscillator",
        "AnchoredMomentum",
        "ChartmillValueIndicator",
        "CenterOfLinearity",
        "BreakoutRelativeStrengthIndex",
        "AsymmetricalRelativeStrengthIndex",
        "AdaptiveStochastic",
        "AdaptiveRelativeStrengthIndex",

        // Trend - TrendCore (Additional Batch 4)
        "ChandeTrendScore",
        "ChopZone",
        "AutoLine",
        "AutoLineWithDrift",
        "AutoFilter",
        "BuffAverage",
        "BryantAdaptiveMovingAverage",
        "AverageTrueRangeTrailingStops",
        "CompoundRatioMovingAverage",
        "ConditionalAccumulator",
        "AhrensMovingAverage",

        // Volatility - VolatilityCore (Additional Batch 2)
        "RogersSatchellVolatility",
        "YangZhangVolatility",
        "CalmarRatio",
        "DownsideDeviation",
        "AtrChannelWidth",
        "CommoditySelectionIndex",

        // Moving Averages - MovingAverageCore (Additional Batch 4)
        "AlphaDecreasingExponentialMovingAverage",
        "AdaptiveExponentialMovingAverage",
        "AutonomousRecursiveMovingAverage",
        "AdaptiveLeastSquares",
        "AtrFilteredExponentialMovingAverage",
        "ParabolicWeightedMovingAverage",
        "VolumeAdjustedMovingAverage",

        // Oscillators - OscillatorCore (Additional Batch 8)
        "TrendContinuationFactor",
        "TrendPersistenceRate",
        "InertiaIndicator",

        // Volatility - VolatilityCore (Additional Batch 3)
        "StandardDeviationChannel",
        "StandardDeviationVolatility",
        "AverageTrueRangeChannel",
        "VolatilityRatio",

        // Trend - TrendCore (Additional Batch 4)
        "TrendDetectionIndex",

        // Oscillators - Additional Batch 9
        "ChandeIntradayMomentumIndex",

        // Volume - Additional Batch 5
        "WilliamsAccumulationDistribution",

        // Additional Enum Name Aliases (enum uses different name than abbreviated fast path)
        "ConnorsRelativeStrengthIndex",
        "StochasticRelativeStrengthIndex",
        "VolumeWeightedRelativeStrengthIndex",
        "EhlersSuperSmootherFilter",
        "ErgodicCandlestickOscillator",
        "StochasticFastOscillator",
        "MassThrustIndicator",
        "McGinleyDynamicIndicator",
    };

    // Indicators that have non-standard naming and need special handling or should be skipped
    // These don't follow the simple Calculate{Name}() pattern
    private static readonly HashSet<string> SkipForCompute = new(StringComparer.OrdinalIgnoreCase)
    {
        // Underscore prefixed indicators with numeric prefixes
        // These have non-standard Calculate method names (e.g., Calculate4MovingAverageConvergenceDivergence instead of Calculate_4MovingAverageConvergenceDivergence)
        "_1LCLeastSquaresMovingAverage",
        "_3HMA",
        "_4MovingAverageConvergenceDivergence",
        "_4PercentagePriceOscillator",

        // Indicators with mismatched method names (enum name doesn't match Calculate method name)
        "BollingerBandsAverageTrueRange",
        "CCTStochRelativeStrengthIndex",
        "EhlersSmoothedAdaptiveMomentumIndicator",
        "VolatilityIndexDynamicAverageIndicator",
        "ZDistanceFromVwap",

        // Indicators that require multiple StockData parameters (market comparison indicators)
        "ComparePriceMomentumOscillator",
        "KaufmanStressIndicator",
        "RelativeNormalizedVolatility",
        "RelativeStrength3DIndicator",
        "RSMKIndicator",
        "SectorRotationModel",
    };

    // Multi-output indicators that need special Result types
    private static readonly Dictionary<string, string[]> MultiOutputIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        // Format: IndicatorName -> Output property names
        ["AroonOscillator"] = new[] { "Up", "Down", "Oscillator" },
        ["ElderRayIndex"] = new[] { "BullPower", "BearPower" },
        ["AlligatorIndex"] = new[] { "Jaw", "Teeth", "Lips" },
        ["GatorOscillator"] = new[] { "Upper", "Lower" },
        ["Trix"] = new[] { "Trix", "Signal" },
        ["PPO"] = new[] { "Ppo", "Signal", "Histogram" },
    };

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

        // Combine with compilation
        var compilationAndEnums = context.CompilationProvider.Combine(enumDeclarations.Collect());

        // Generate source
        context.RegisterSourceOutput(compilationAndEnums, static (spc, source) => Execute(source.Left, source.Right!, spc));
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

    private static void Execute(Compilation compilation, ImmutableArray<EnumDeclarationSyntax?> enums, SourceProductionContext context)
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
        GenerateIndicatorCatalog(context, indicatorNames);

        // Generate IndicatorCompute.g.cs
        GenerateIndicatorCompute(context, indicatorNames);
    }

    private static void GenerateIndicatorCatalog(SourceProductionContext context, List<string> indicatorNames)
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
                sb.AppendLine($"    public {resultTypeName} {methodName}(int length = 14, SeriesHandle? input = null)");
                sb.AppendLine("    {");
                sb.AppendLine("        var series = input ?? Price();");
                sb.AppendLine("        var seriesKey = _builder.ResolveSeriesKey(series);");
                sb.AppendLine($"        var opts = new GenericIndicatorOptions(new object[] {{ length }});");

                for (int i = 0; i < outputs.Length; i++)
                {
                    var outputName = outputs[i].ToLowerInvariant();
                    var outputType = i == 0 ? "Primary" : (i == 1 ? "Signal" : "Histogram");
                    sb.AppendLine($"        var {outputName} = _builder.AddIndicator(IndicatorSpecs.Create(IndicatorName.{indicatorName}, opts, IndicatorOutput.{outputType}), series, seriesKey, null);");
                }

                var outputArgs = string.Join(", ", outputs.Select(o => o.ToLowerInvariant()));
                sb.AppendLine($"        return new {resultTypeName}({outputArgs});");
                sb.AppendLine("    }");
            }
            else
            {
                // Single-output indicator
                sb.AppendLine($"    public SeriesHandle {methodName}(int length = 14, SeriesHandle? input = null, IndicatorKey? key = null)");
                sb.AppendLine("    {");
                sb.AppendLine("        var series = input ?? Price();");
                sb.AppendLine($"        var spec = IndicatorSpecs.Create(IndicatorName.{indicatorName}, new GenericIndicatorOptions(new object[] {{ length }}), IndicatorOutput.Primary);");
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

    private static void GenerateIndicatorCompute(SourceProductionContext context, List<string> indicatorNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#pragma warning disable CS0618 // Suppress obsolete warnings for Calculate* method calls");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using OoplesFinance.StockIndicators.Models;");
        sb.AppendLine();
        sb.AppendLine("namespace OoplesFinance.StockIndicators.Builder.Compute;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated compute methods for all indicators.");
        sb.AppendLine("/// These methods extract indicator results to pooled buffers for zero-allocation access.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static partial class IndicatorCompute");
        sb.AppendLine("{");

        // Generate dispatch method that routes to specific compute methods
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Computes an indicator by name and extracts results to a pooled buffer.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"data\">The stock data to compute on.</param>");
        sb.AppendLine("    /// <param name=\"indicatorName\">The indicator name.</param>");
        sb.AppendLine("    /// <param name=\"context\">The compute context for buffer management.</param>");
        sb.AppendLine("    /// <param name=\"length\">The indicator length parameter.</param>");
        sb.AppendLine("    /// <returns>A ComputeBuffer containing the indicator values.</returns>");
        sb.AppendLine("    public static ComputeBuffer ComputeByName(StockData data, string indicatorName, ComputeContext context, int length = 14)");
        sb.AppendLine("    {");
        sb.AppendLine("        return indicatorName switch");
        sb.AppendLine("        {");

        foreach (var indicatorName in indicatorNames)
        {
            // Skip indicators with non-standard Calculate method naming
            if (SkipForCompute.Contains(indicatorName))
            {
                continue;
            }

            var methodName = GetMethodName(indicatorName);
            // Fast path indicators have hand-written optimized methods with "Fast" suffix
            if (FastPathIndicators.Contains(indicatorName))
            {
                var fastMethodName = GetFastPathMethodName(indicatorName);
                sb.AppendLine($"            \"{indicatorName}\" => {fastMethodName}(data, context, length),");
            }
            else
            {
                sb.AppendLine($"            \"{indicatorName}\" => Compute{methodName}(data, context, length),");
            }
        }

        sb.AppendLine("            _ => throw new System.NotSupportedException($\"Indicator '{indicatorName}' not supported.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");

        // Generate individual compute methods for each indicator (excluding fast path and skipped indicators)
        foreach (var indicatorName in indicatorNames)
        {
            // Skip indicators that have hand-written fast paths
            if (FastPathIndicators.Contains(indicatorName))
            {
                continue;
            }

            // Skip indicators with non-standard Calculate method naming
            if (SkipForCompute.Contains(indicatorName))
            {
                continue;
            }

            var methodName = GetMethodName(indicatorName);
            var calculateMethodName = GetCalculateMethodName(indicatorName);

            sb.AppendLine();
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Computes {FormatIndicatorName(indicatorName)} and extracts results to a pooled buffer.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static ComputeBuffer Compute{methodName}(StockData data, ComputeContext context, int length = 14)");
            sb.AppendLine("    {");
            sb.AppendLine($"        // Call with default parameters - length parameter passed but ignored for indicators that don't support it");
            sb.AppendLine($"        _ = length; // Suppress unused warning");
            sb.AppendLine($"        var result = data.{calculateMethodName}();");
            sb.AppendLine("        return ExtractToPooledBuffer(result, context);");
            sb.AppendLine("    }");
        }

        // Add helper method for extracting results
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Extracts indicator output to a pooled buffer.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    private static ComputeBuffer ExtractToPooledBuffer(StockData result, ComputeContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        var values = result.CustomValuesList;");
        sb.AppendLine("        if (values.Count == 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            return context.Rent(0);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var buffer = context.Rent(values.Count);");
        sb.AppendLine("        var span = buffer.WritableSpan;");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < values.Count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            span[i] = values[i];");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return buffer;");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        context.AddSource("IndicatorCompute.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string GetCalculateMethodName(string indicatorName)
    {
        // Map indicator name to Calculate method name
        // Most follow the pattern Calculate{IndicatorName}
        return $"Calculate{indicatorName}";
    }

    private static string GetFastPathMethodName(string indicatorName)
    {
        // Map indicator name to the hand-written fast path method name
        return indicatorName switch
        {
            // Moving Averages
            "SimpleMovingAverage" => "ComputeSmaFast",
            "ExponentialMovingAverage" => "ComputeEmaFast",
            "WeightedMovingAverage" => "ComputeWmaFast",
            "DoubleExponentialMovingAverage" => "ComputeDemaFast",
            "TripleExponentialMovingAverage" => "ComputeTemaFast",
            "HullMovingAverage" => "ComputeHmaFast",
            "TriangularMovingAverage" => "ComputeTmaFast",
            "WellesWilderMovingAverage" => "ComputeWwmaFast",
            "LinearRegression" => "ComputeLinRegFast",
            "KaufmanAdaptiveMovingAverage" => "ComputeKamaFast",
            "ZeroLagExponentialMovingAverage" => "ComputeZlemaFast",
            "SmoothedMovingAverage" => "ComputeSmmaFast",
            "McGinleyDynamic" => "ComputeMcGinleyDynamicFast",
            "T3MovingAverage" => "ComputeT3Fast",
            "VariableIndexDynamicAverage" => "ComputeVidyaFast",
            "VariableMovingAverage" => "ComputeVmaFast",

            // Oscillators
            "RelativeStrengthIndex" => "ComputeRsiFast",
            "RateOfChange" => "ComputeRocFast",
            "MomentumOscillator" => "ComputeMomentumFast",
            "WilliamsR" => "ComputeWilliamsRFast",
            "CommodityChannelIndex" => "ComputeCciFast",
            "StochasticOscillator" => "ComputeStochasticKFast",
            "AverageDirectionalIndex" => "ComputeAdxFast",
            "ChandeMomentumOscillator" => "ComputeCmoFast",
            "PercentagePriceOscillator" => "ComputePpoFast",
            "AbsolutePriceOscillator" => "ComputeApoFast",
            "UltimateOscillator" => "ComputeUltimateOscillatorFast",
            "TrueStrengthIndex" => "ComputeTsiFast",
            "StochasticRsi" => "ComputeStochRsiFast",
            "MovingAverageConvergenceDivergence" => "ComputeMacdLineFast",
            "AroonOscillator" => "ComputeAroonOscillatorFast",
            "DetrendedPriceOscillator" => "ComputeDpoFast",
            "Trix" => "ComputeTrixFast",
            "MassIndex" => "ComputeMassIndexFast",

            // Volume
            "OnBalanceVolume" => "ComputeObvFast",
            "AccumulationDistributionLine" => "ComputeAdlFast",
            "ChaikinMoneyFlow" => "ComputeCmfFast",
            "ForceIndex" => "ComputeForceIndexFast",
            "VolumeRateOfChange" => "ComputeVrocFast",
            "NegativeVolumeIndex" => "ComputeNviFast",
            "PositiveVolumeIndex" => "ComputePviFast",
            "PriceVolumeTrend" => "ComputePvtFast",
            "VolumeWeightedAveragePrice" => "ComputeVwapFast",
            "ChaikinOscillator" => "ComputeChaikinOscillatorFast",
            "EaseOfMovement" => "ComputeEmvFast",

            // Volatility
            "AverageTrueRange" => "ComputeAtrFast",
            "StandardDeviation" => "ComputeStdDevFast",
            "HistoricalVolatility" => "ComputeHistoricalVolatilityFast",
            "ChaikinVolatility" => "ComputeChaikinVolatilityFast",
            "UlcerIndex" => "ComputeUlcerIndexFast",
            "NormalizedAverageTrueRange" => "ComputeNormalizedAtrFast",
            "Variance" => "ComputeVarianceFast",
            "CoefficientOfVariation" => "ComputeCoefficientOfVariationFast",
            "TrueRange" => "ComputeTrueRangeFast",
            "BollingerBands" => "ComputeBollingerBandsFast",

            // Volume (Additional)
            "KlingerVolumeOscillator" => "ComputeKlingerVolumeFast",
            "VolumePriceConfirmationIndicator" => "ComputeVpciFast",

            // Oscillators (Additional Batch 1)
            "MoneyFlowIndex" => "ComputeMoneyFlowIndexFast",
            "BalanceOfPower" => "ComputeBalanceOfPowerFast",
            "RelativeVigorIndex" => "ComputeRelativeVigorIndexFast",
            "AroonUp" => "ComputeAroonUpFast",
            "AroonDown" => "ComputeAroonDownFast",

            // Oscillators (Additional Batch 2)
            "StochasticFastD" => "ComputeStochasticDFast",
            "AwesomeOscillator" => "ComputeAwesomeOscillatorFast",
            "AcceleratorOscillator" => "ComputeAcceleratorOscillatorFast",
            "PercentageVolumeOscillator" => "ComputePvoFast",
            "FisherTransform" => "ComputeFisherTransformFast",
            "ConnorsRsi" => "ComputeConnorsRsiFast",
            "PriceMomentumOscillator" => "ComputePmoFast",
            "KnowSureThing" => "ComputeKstFast",
            "PercentRank" => "ComputePercentRankFast",
            "ChoppinessIndex" => "ComputeChoppinessIndexFast",

            // Trend
            "ParabolicSAR" => "ComputeParabolicSarFast",
            "SuperTrend" => "ComputeSuperTrendFast",
            "DonchianChannels" => "ComputeDonchianChannelFast",
            "HighestHigh" => "ComputeHighestHighFast",
            "LowestLow" => "ComputeLowestLowFast",
            "AverageDayRange" => "ComputeAdrFast",
            "TypicalPrice" => "ComputeTypicalPriceFast",
            "MedianPrice" => "ComputeMedianPriceFast",
            "WeightedClose" => "ComputeWeightedCloseFast",
            "PercentageChange" => "ComputePercentageChangeFast",
            "LinearRegressionSlope" => "ComputeLinRegSlopeFast",
            "RSquared" => "ComputeRSquaredFast",
            "StandardError" => "ComputeStandardErrorFast",
            "VerticalHorizontalFilter" => "ComputeVhfFast",
            "KeltnerChannels" => "ComputeKeltnerChannelMiddleFast",
            "TrendDetection" => "ComputeTrendDetectionFast",
            "PriceChannels" => "ComputePriceChannelMiddleFast",

            // Oscillators (Additional Batch 3)
            "AbsoluteStrengthIndex" => "ComputeAbsoluteStrengthIndexFast",
            "RelativeMomentumIndex" => "ComputeRelativeMomentumIndexFast",
            "IntradayMomentumIndex" => "ComputeIntradayMomentumIndexFast",
            "SwingIndex" => "ComputeSwingIndexFast",
            "AccumulativeSwingIndex" => "ComputeAccumulativeSwingIndexFast",

            // Oscillators (Additional Batch 4)
            "CoppockCurve" => "ComputeCoppockCurveFast",
            "ChandeForecastOscillator" => "ComputeChandeForecastOscillatorFast",
            "BullPowerIndicator" => "ComputeBullPowerFast",
            "BearPowerIndicator" => "ComputeBearPowerFast",
            "PolarizedFractalEfficiency" => "ComputePolarizedFractalEfficiencyFast",
            "SchaffTrendCycle" => "ComputeSchaffTrendCycleFast",
            "PriceZoneOscillator" => "ComputePriceZoneOscillatorFast",
            "ElderForceIndex" => "ComputeElderForceIndexFast",
            "PrettyGoodOscillator" => "ComputePrettyGoodOscillatorFast",
            "RelativeVolatilityIndex" => "ComputeRelativeVolatilityIndexFast",
            "QstickIndicator" => "ComputeQstickFast",
            "SpecialK" => "ComputeSpecialKFast",

            // Moving Averages (Additional Batch 2)
            "ArnaudLegouxMovingAverage" => "ComputeAlmaFast",
            "LeastSquaresMovingAverage" => "ComputeLsmaFast",
            "FractalAdaptiveMovingAverage" => "ComputeFramaFast",
            "AdaptiveMovingAverage" => "ComputeAmaFast",
            "SineWeightedMovingAverage" => "ComputeSineWmaFast",
            "HammingMovingAverage" => "ComputeHammingMaFast",
            "GeometricMovingAverage" => "ComputeGeoMaFast",
            "RegularizedExponentialMovingAverage" => "ComputeRegularizedEmaFast",
            "ModifiedMovingAverage" => "ComputeModifiedMaFast",

            // Trend (Additional Batch 2)
            "ZigZag" => "ComputeZigZagFast",
            "ChandelierExit" => "ComputeChandelierExitLongFast",
            "TrendIntensityIndex" => "ComputeTrendIntensityIndexFast",
            "AveragePrice" => "ComputeAveragePriceFast",
            "Midpoint" => "ComputeMidpointFast",
            "Midprice" => "ComputeMidpriceFast",
            "PivotPointAverage" => "ComputePivotPointFast",

            // Volume (Additional Batch 2)
            "TradeVolumeIndex" => "ComputeTradeVolumeIndexFast",
            "VolumeOscillator" => "ComputeVolumeOscillatorFast",
            "VolumeWeightedMovingAverage" => "ComputeVwmaFast",
            "TwiggsMoneyFlow" => "ComputeTwiggsMoneyFlowFast",
            "VolumeZoneOscillator" => "ComputeVolumeZoneOscillatorFast",
            "DemandIndex" => "ComputeDemandIndexFast",

            // Oscillators (Additional Batch 5)
            "DisparityIndex" => "ComputeDisparityIndexFast",
            "DirectionalTrendIndex" => "ComputeDirectionalTrendIndexFast",
            "DoubleSmoothedStochastic" => "ComputeDoubleSmoothedStochasticFast",
            "DynamicMomentumIndex" => "ComputeDynamicMomentumIndexFast",
            "AdaptiveErgodicCandlestickOscillator" => "ComputeErgodicCandlestickOscillatorFast",
            "Demarker" => "ComputeDemarkerFast",
            "SmoothedRateOfChange" => "ComputeSmoothedRocFast",

            // Volatility (Additional)
            "KeltnerChannelWidth" => "ComputeKeltnerChannelWidthFast",
            "BollingerBandsWidth" => "ComputeBollingerBandsWidthFast",
            "DonchianChannelWidth" => "ComputeDonchianChannelWidthFast",
            "CloseToCloseVolatility" => "ComputeCloseToCloseVolatilityFast",
            "ParkinsonVolatility" => "ComputeParkinsonVolatilityFast",
            "GarmanKlassVolatility" => "ComputeGarmanKlassVolatilityFast",

            // Moving Averages (Additional Batch 3)
            "JurikMovingAverage" => "ComputeJmaFast",
            "ButterworthFilter" => "ComputeButterworthFilterFast",
            "SuperSmootherFilter" => "ComputeSuperSmootherFast",
            "EndPointMovingAverage" => "ComputeEndPointMovingAverageFast",
            "CubedWeightedMovingAverage" => "ComputeCubicWmaFast",
            "NaturalMovingAverage" => "ComputeNaturalMaFast",

            // Oscillators (Additional Batch 6)
            "ElliottWaveOscillator" => "ComputeElliottWaveOscillatorFast",
            "ForecastOscillator" => "ComputeForecastOscillatorFast",
            "DerivativeOscillator" => "ComputeDerivativeOscillatorFast",
            "GatorOscillator" => "ComputeGatorOscillatorFast",
            "FractalChaosOscillator" => "ComputeFractalChaosOscillatorFast",
            "RahulMohindarOscillator" => "ComputeRahulMohindarOscillatorFast",
            "PremierStochasticOscillator" => "ComputePremierStochasticFast",
            "Repulse" => "ComputeRepulseFast",

            // Trend (Additional Batch 3)
            "GannHiLoActivator" => "ComputeGannHiLoActivatorFast",
            "HalfTrend" => "ComputeHalfTrendFast",
            "VortexIndicator" => "ComputeVortexPositiveFast",
            "LinearRegressionIntercept" => "ComputeLinRegInterceptFast",
            "ElderImpulseSystem" => "ComputeElderImpulseSystemFast",
            "IchimokuTenkanSen" => "ComputeIchimokuTenkanSenFast",
            "IchimokuKijunSen" => "ComputeIchimokuKijunSenFast",
            "MassThrust" => "ComputeMassThrustFast",

            // Volume (Additional Batch 3)
            "WilliamsAD" => "ComputeWilliamsADFast",
            "NetVolume" => "ComputeNetVolumeFast",
            "CumulativeVolumeIndex" => "ComputeCumulativeVolumeIndexFast",
            "VolumeMomentum" => "ComputeVolumeMomentumFast",
            "VolumePriceTrend" => "ComputeVolumePriceTrendFast",
            "ElderRayBullPower" => "ComputeElderRayBullPowerFast",
            "ElderRayBearPower" => "ComputeElderRayBearPowerFast",
            "NormalizedVolume" => "ComputeNormalizedVolumeFast",
            "VolumeWeightedRsi" => "ComputeVolumeWeightedRsiFast",

            // Oscillators (Additional Batch 7)
            "ChandeCompositeMomentumIndex" => "ComputeChandeCompositeMomentumIndexFast",
            "ChandeKrollRSquaredIndex" => "ComputeChandeKrollRSquaredIndexFast",
            "BayesianOscillator" => "ComputeBayesianOscillatorFast",
            "AnchoredMomentum" => "ComputeAnchoredMomentumFast",
            "ChartmillValueIndicator" => "ComputeChartmillValueIndicatorFast",
            "CenterOfLinearity" => "ComputeCenterOfLinearityFast",
            "BreakoutRelativeStrengthIndex" => "ComputeBreakoutRsiFast",
            "AsymmetricalRelativeStrengthIndex" => "ComputeAsymmetricalRsiFast",
            "AdaptiveStochastic" => "ComputeAdaptiveStochasticFast",
            "AdaptiveRelativeStrengthIndex" => "ComputeAdaptiveRsiFast",

            // Trend (Additional Batch 4)
            "ChandeTrendScore" => "ComputeChandeTrendScoreFast",
            "ChopZone" => "ComputeChopZoneFast",
            "AutoLine" => "ComputeAutoLineFast",
            "AutoLineWithDrift" => "ComputeAutoLineWithDriftFast",
            "AutoFilter" => "ComputeAutoFilterFast",
            "BuffAverage" => "ComputeBuffAverageFast",
            "BryantAdaptiveMovingAverage" => "ComputeBryantAdaptiveMovingAverageFast",
            "AverageTrueRangeTrailingStops" => "ComputeAtrTrailingStopsFast",
            "CompoundRatioMovingAverage" => "ComputeCompoundRatioMovingAverageFast",
            "ConditionalAccumulator" => "ComputeConditionalAccumulatorFast",
            "AhrensMovingAverage" => "ComputeAhrensMovingAverageFast",

            // Volatility (Additional Batch 2)
            "RogersSatchellVolatility" => "ComputeRogersSatchellVolatilityFast",
            "YangZhangVolatility" => "ComputeYangZhangVolatilityFast",
            "CalmarRatio" => "ComputeCalmarRatioFast",
            "DownsideDeviation" => "ComputeDownsideDeviationFast",
            "AtrChannelWidth" => "ComputeAtrChannelWidthFast",
            "CommoditySelectionIndex" => "ComputeCommoditySelectionIndexFast",

            // Moving Averages (Additional Batch 4)
            "AlphaDecreasingExponentialMovingAverage" => "ComputeAlphaDecreasingEmaFast",
            "AdaptiveExponentialMovingAverage" => "ComputeAdaptiveEmaFast",
            "AutonomousRecursiveMovingAverage" => "ComputeAutonomousRecursiveMaFast",
            "AdaptiveLeastSquares" => "ComputeAdaptiveLeastSquaresFast",
            "AtrFilteredExponentialMovingAverage" => "ComputeAtrFilteredEmaFast",
            "ParabolicWeightedMovingAverage" => "ComputeParabolicWmaFast",
            "VolumeAdjustedMovingAverage" => "ComputeVolumeAdjustedMaFast",

            // Oscillators (Additional Batch 8)
            "TrendContinuationFactor" => "ComputeTrendContinuationFactorFast",
            "TrendPersistenceRate" => "ComputeTrendPersistenceRateFast",
            "InertiaIndicator" => "ComputeInertiaFast",

            // Volatility (Additional Batch 3)
            "StandardDeviationChannel" => "ComputeStandardDeviationChannelFast",
            "StandardDeviationVolatility" => "ComputeStandardDeviationVolatilityFast",
            "AverageTrueRangeChannel" => "ComputeAverageTrueRangeChannelFast",
            "VolatilityRatio" => "ComputeVolatilityRatioFast",

            // Trend (Additional Batch 4)
            "TrendDetectionIndex" => "ComputeTrendDetectionFast",

            // Oscillators (Additional Batch 9)
            "ChandeIntradayMomentumIndex" => "ComputeIntradayMomentumIndexFast",

            // Volume (Additional Batch 5)
            "WilliamsAccumulationDistribution" => "ComputeWilliamsADFast",

            // Additional Enum Name Aliases
            "ConnorsRelativeStrengthIndex" => "ComputeConnorsRsiFast",
            "StochasticRelativeStrengthIndex" => "ComputeStochRsiFast",
            "VolumeWeightedRelativeStrengthIndex" => "ComputeVolumeWeightedRsiFast",
            "EhlersSuperSmootherFilter" => "ComputeSuperSmootherFast",
            "ErgodicCandlestickOscillator" => "ComputeErgodicCandlestickOscillatorFast",
            "StochasticFastOscillator" => "ComputeStochasticKFast",
            "MassThrustIndicator" => "ComputeMassThrustFast",
            "McGinleyDynamicIndicator" => "ComputeMcGinleyDynamicFast",

            _ => $"Compute{GetMethodName(indicatorName)}Fast"
        };
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
