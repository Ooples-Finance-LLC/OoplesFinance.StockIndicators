using System.Reflection;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// Every typed Builder spec computes the batch indicator it stands for.
/// </summary>
/// <remarks>
/// <para>
/// A typed spec (<c>RsiSpecOptions</c> and the 843 others) used to run a fast arm of its own, written apart
/// from the batch indicator and never compared with it. Of the arms that could be compared, over half
/// disagreed: RSI, ATR, ADX and the stochastic among them, by a warmup convention, a different formula, or by
/// computing another indicator altogether. The Builder now serves an arm only when it is in
/// <see cref="BuilderVerifiedArms"/>, and computes every other typed spec with its batch indicator.
/// </para>
/// <para>
/// This holds every spec, at two parameter sets, through the Builder's public path to its batch indicator; a
/// verified arm that disagrees at either set fails here. The only specs it does not hold are those whose
/// indicator the library does not have yet, named one by one in <c>AwaitingPromotion</c>.
/// </para>
/// </remarks>
public sealed class BuilderArmTests : GlobalTestData
{
    /// <summary>
    /// Every typed spec a caller can still use: a spec marked obsolete stands for nothing this can hold it to.
    /// </summary>
    /// <remarks>
    /// <c>MultiStockIndicatorOptions</c> names no single indicator - it carries the parameters of whichever
    /// two-series indicator a caller builds with it - and the Compare Price Momentum Oscillator's own spec is
    /// obsolete for the same reason: its batch indicator takes a market series this path cannot pass.
    /// </remarks>
    internal static readonly List<Type> OptionTypes = typeof(IIndicatorSpecOptions).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(IIndicatorSpecOptions).IsAssignableFrom(t)
            && t != typeof(GenericIndicatorOptions) && t != typeof(MultiStockIndicatorOptions)
            && t.GetCustomAttribute<ObsoleteAttribute>() is null)
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// The typed specs whose indicator the library does not have yet, each to be written in both engines.
    /// </summary>
    /// <remarks>
    /// These 9 arms compute something no batch indicator computes - the average day range, Yang-Zhang
    /// volatility, a zig zag - so there is nothing to bind them to and nothing to hold them to.
    /// Every one is promoted to a real indicator, batch and streaming, in the follow-up; the list is here so
    /// that it shrinks visibly and cannot grow unnoticed.
    /// </remarks>
    private static readonly HashSet<string> AwaitingPromotion = new(StringComparer.Ordinal)
    {
       
       
       
       
       
       
       
       
       
       
       
       
       
       
       
       
       
    };

    [Fact]
    public void EveryTypedSpecStandsForABatchIndicator()
    {
        var unbound = OptionTypes.Where(t => !BuilderArmBinding.TryGetTarget(t, out _)).Select(t => t.Name).ToList();

        using var scope = new AssertionScope();
        unbound.Except(AwaitingPromotion).Should().BeEmpty(
            "every typed spec names the batch indicator it computes, unless its indicator is still to be written");
        AwaitingPromotion.Except(unbound).Should().BeEmpty(
            "a spec promoted to a real indicator leaves the list waiting for one");
    }

    [Fact]
    public void EveryOptionReachesTheBatchIndicator()
    {
        var ignored = new List<string>();
        foreach (var type in OptionTypes)
        {
            if (!BuilderArmBinding.TryGetTarget(type, out var target))
            {
                continue;
            }

            if (BuilderArmBinding.UnmappedProperties(type, target) is { Count: > 0 } unmapped)
            {
                ignored.Add($"{type.Name} -> {target.Name}: {string.Join(",", unmapped)}");
            }

            // A declared argument that names nothing would silently pass nothing.
            ignored.AddRange(BuilderArmBinding.InvalidArguments(type, target).Select(invalid => $"{type.Name} -> {target.Name}: {invalid}"));
        }

        ignored.Should().BeEmpty($"a spec option the batch indicator never sees is silently ignored: {string.Join(" | ", ignored)}");
    }

    [Fact]
    public void EveryTypedSpecComputesItsBatchIndicator()
    {
        var tickers = StockTestData.ToList();
        var failures = new List<string>();
        var compared = 0;
        foreach (var type in OptionTypes)
        {
            if (!BuilderArmBinding.TryGetTarget(type, out var target))
            {
                continue;
            }

            foreach (var alternate in new[] { false, true })
            {
                var options = Create(type, alternate);
                if (options is null)
                {
                    failures.Add($"{type.Name}: no constructor this test can call");
                    break;
                }

                double[]? primary = null;

                // The indicator's own series first, then every key it publishes. This used to walk the six
                // IndicatorOutput slots and skip the ones the indicator had no key for; the published keys are
                // the same question asked directly, and they reach the outputs no slot could name. See #219.
                foreach (var outputKey in OwnSeriesThenPublishedKeys(target.Name))
                {
                    var spec = outputKey is null
                        ? new IndicatorSpec(target.Name, options)
                        : new IndicatorSpec(target.Name, options, outputKey);
                    double[]? arm;
                    try
                    {
                        arm = Run(IndicatorCompute.ComputeArm, tickers, spec);
                    }
                    catch (Exception ex)
                    {
                        // A verified arm IS served, so an arm that throws on a key the indicator publishes is a
                        // broken served path, not an absent one - and skipping it below would retire the only
                        // assertion that would have caught it. This used to be safe by accident: the loop walked
                        // six slots and most nulls were slots with no key at all, whereas every key reaching here
                        // now is one the indicator publishes. Raised by review on PR #230.
                        if (outputKey is not null && BuilderVerifiedArms.Arms.Contains(type))
                        {
                            var thrown = ex.InnerException ?? ex;
                            failures.Add($"{type.Name} {outputKey}: verified arm threw {thrown.GetType().Name} {thrown.Message}");
                        }

                        // An arm that cannot run is not served unless verified; the served path is checked below.
                        arm = outputKey is null ? Array.Empty<double>() : null;
                    }

                    if (arm is null || (outputKey is not null && primary is not null && Same(primary, arm)))
                    {
                        continue;
                    }

                    if (outputKey is null)
                    {
                        primary = arm;
                    }

                    compared++;
                    var label = $"{type.Name} {outputKey ?? "own series"}{(alternate ? " (alternate parameters)" : string.Empty)}";
                    try
                    {
                        var served = Run(IndicatorCompute.TryComputeFast, tickers, spec)
                            ?? throw new InvalidOperationException("the Builder served nothing");
                        var expected = BuilderArmBinding.Compute(new StockData(tickers), spec, target);
                        var first = Enumerable.Range(0, Math.Min(served.Length, expected.Count)).FirstOrDefault(i => !IsClose(expected[i], served[i]), -1);
                        if (served.Length != expected.Count)
                        {
                            failures.Add($"{label}: {served.Length} values, batch {expected.Count}");
                        }
                        else if (first >= 0)
                        {
                            failures.Add($"{label} bar {first}: Builder {served[first]}, batch {target.Name} {expected[first]}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        failures.Add($"{label}: {inner.GetType().Name} {inner.Message}");
                    }
                }
            }
        }

        compared.Should().BeGreaterThan(700, "every bound typed spec is compared");
        failures.Should().BeEmpty($"{compared} spec outputs compared: {string.Join(" | ", failures)}");
    }

    /// <summary>
    /// Every bound arm whose result differs from the batch call its spec is bound to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured, not chosen. Driving <c>IndicatorCompute.ComputeArm</c> over every bound spec at two parameter
    /// sets, each option type named here returns something other than <see cref="BuilderArmBinding"/> does for
    /// the same spec, at one or both of those sets. A type is listed once however many of its parameter sets
    /// disagree. That is close to every arm outside <see cref="BuilderVerifiedArms"/>, which is why #229
    /// stopped serving them: the Builder computes those specs with their batch indicator, so callers get
    /// correct numbers today.
    /// </para>
    /// <para>
    /// "Disagrees with its bound call" is the claim, and it is deliberately weaker than "computes a different
    /// indicator". Two causes reach this list. One is a genuinely different formula:
    /// <c>AutoLineSpecOptions</c> maps its <c>Length</c> straight onto <c>CalculateAutoLine(length)</c>, so its
    /// arm was compared like for like and still disagreed. The other is parameterisation:
    /// <c>UltimateMovingAverageSpecOptions.Length</c> is <c>[Obsolete]</c> because the indicator has no
    /// parameter it could set, <c>MapArguments</c> skips obsolete properties, so the bound call ran at
    /// <c>minLength: 5, maxLength: 50</c> while the arm ran at the option's length. Both make an arm unsafe to
    /// verify; only the first means the arm implements the wrong maths.
    /// </para>
    /// <para>
    /// An <c>[Obsolete]</c> option does not decide which of the two a given arm is, and reading the attribute
    /// instead of the arm is how the first batch was mis-sorted. <c>AveragePriceSpecOptions.Length</c> is
    /// obsolete and its arm did ignore it, yet the arm still disagreed, because it averaged all four prices
    /// where the indicator averages the open and the close. Only the arm's own body says whether it reads the
    /// option, so each one has to be opened. None has an unmapped non-obsolete option;
    /// <see cref="EveryOptionReachesTheBatchIndicator"/> already forbids that.
    /// </para>
    /// <para>
    /// Every entry here is a repair, never a removal. A <c>Compute*Fast</c> arm is the zero-allocation batch
    /// path - it rents a pooled buffer and writes through spans - so deleting one because it disagrees trades
    /// a wrong fast path for no fast path, and every indicator is meant to have one. An arm leaves this list
    /// by computing what its bound call computes; then it can be verified, and then it is served.
    /// </para>
    /// <para>
    /// Named one by one rather than counted, so the list shrinks visibly as arms are repaired and so a reader
    /// can tell at a glance whether a given spec's arm is trustworthy. Both directions are asserted - an arm
    /// that starts disagreeing must be added, and an arm that is fixed must be removed - so the list cannot go
    /// stale in either direction. See issue #233.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> ArmsDisagreeingWithTheirBoundCall = new(StringComparer.Ordinal)
    {
        "AbsoluteStrengthIndexSpecOptions",
        "AbsoluteStrengthMTFIndicatorSpecOptions",
        "AdaptiveAutonomousRecursiveMovingAverageSpecOptions",
        "AdaptiveAutonomousRecursiveTrailingStopSpecOptions",
        "AdaptiveEmaSpecOptions",
        "AdaptiveErgodicCandlestickOscillatorSpecOptions",
        "AdaptiveLeastSquaresSpecOptions",
        "AdaptivePriceZoneIndicatorSpecOptions",
        "AdaptiveStochasticSpecOptions",
        "AdaptiveTrailingStopSpecOptions",
        "AhrensMovingAverageSpecOptions",
        "AlphaDecreasingEmaSpecOptions",
        "AmaSpecOptions",
        "ApirineSlowRelativeStrengthIndexSpecOptions",
        "AroonSpecOptions",
        "AsymmetricalRsiSpecOptions",
        "AtrChannelWidthSpecOptions",
        "AtrFilteredEmaSpecOptions",
        "AtrFilteredExponentialMovingAverageSpecOptions",
        "AtrPercentSpecOptions",
        "AutoDispersionBandsSpecOptions",
        "AutoLineSpecOptions",
        "AutoLineWithDriftSpecOptions",
        "AutonomousRecursiveMaSpecOptions",
        "BollingerBandsAtrSpecOptions",
        "BollingerBandsSpecOptions",
        "BollingerBandsWidthSpecOptions",
        "BreakoutRsiSpecOptions",
        "BryantAdaptiveMovingAverageSpecOptions",
        "BuffAverageSpecOptions",
        "ButterworthFilterSpecOptions",
        "CalmarRatioSpecOptions",
        "CciSpecOptions",
        "CenterOfLinearitySpecOptions",
        "ConnorsRsiSpecOptions",
        "ConstanceBrownCompositeIndexSpecOptions",
        "DeMarkerSpecOptions",
        "DecisionPointBreadthSwenlinTradingOscillatorSpecOptions",
        "DecisionPointPriceMomentumOscillatorSpecOptions",
        "Dema2LinesSpecOptions",
        "DetrendedSyntheticPriceSpecOptions",
        "DiNapoliPercentagePriceOscillatorSpecOptions",
        "DiNapoliPreferredStochasticOscillatorSpecOptions",
        "DoubleExponentialSmoothingSpecOptions",
        "DoubleSmoothedRelativeStrengthIndexSpecOptions",
        "DpoSpecOptions",
        "DynamicSupportAndResistanceSpecOptions",
        "DynamicallyAdjustableFilterSpecOptions",
        "DynamicallyAdjustableMovingAverageSpecOptions",
        "EhlersAdaptiveCenterOfGravityOscillatorSpecOptions",
        "EhlersAdaptiveCommodityChannelIndexV2SpecOptions",
        "EhlersAdaptiveLaguerreFilterSpecOptions",
        "EhlersAdaptiveRelativeStrengthIndexV2SpecOptions",
        "EhlersAdaptiveRsiFisherTransformV2SpecOptions",
        "EhlersAdaptiveStochasticIndicatorV2SpecOptions",
        "EhlersBetterExponentialMovingAverageSpecOptions",
        "EhlersChebyshevLowPassFilterSpecOptions",
        "EhlersFisherizedDeviationScaledOscillatorSpecOptions",
        "EhlersFramaSpecOptions",
        "EhlersHilbertOscillatorSpecOptions",
        "EhlersKaufmanAdaptiveMovingAverageSpecOptions",
        "EhlersMedianAverageAdaptiveFilterSpecOptions",
        "EhlersMesaAdaptiveMovingAverageSpecOptions",
        "EhlersMesaPredictIndicatorV2SpecOptions",
        "EhlersModifiedStochasticIndicatorSpecOptions",
        "EhlersOptimumEllipticFilterSpecOptions",
        "EhlersRelativeVigorIndexSpecOptions",
        "EhlersReverseEmaIndicatorV2SpecOptions",
        "EhlersRocketRelativeStrengthIndexSpecOptions",
        "EhlersSmoothedAdaptiveMomentumSpecOptions",
        "EhlersStochasticCenterOfGravityOscillatorSpecOptions",
        "EhlersStochasticSpecOptions",
        "EhlersTripleDelayLineDetrenderSpecOptions",
        "EhlersUniversalOscillatorSpecOptions",
        "EhlersVariableIndexDynamicAverageSpecOptions",
        "EhlersZeroLagEmaSpecOptions",
        "EhlersZeroLagExponentialMovingAverageSpecOptions",
        "ElasticVolumeWeightedMovingAverageV1SpecOptions",
        "ElderMarketThermometerSpecOptions",
        "ElderRayIndexSpecOptions",
        "ElderSafeZoneStopsSpecOptions",
        "EmvSpecOptions",
        "EndPointMovingAverageSpecOptions",
        "EnhancedIndexSpecOptions",
        "EnhancedWilliamsRSpecOptions",
        "EquityMovingAverageSpecOptions",
        "ErgodicCandlestickOscillatorSpecOptions",
        "ErgodicCommoditySelectionIndexSpecOptions",
        "ErgodicMeanDeviationIndicatorSpecOptions",
        "ErgodicPercentagePriceOscillatorSpecOptions",
        "ErgodicTrueStrengthIndexV1SpecOptions",
        "ErgodicTrueStrengthIndexV2SpecOptions",
        "FXSniperIndicatorSpecOptions",
        "FallingRisingFilterSpecOptions",
        "FastSlowDegreeOscillatorSpecOptions",
        "FastSlowKurtosisOscillatorSpecOptions",
        "FastSlowRsiOscillatorSpecOptions",
        "FastSlowStochasticOscillatorSpecOptions",
        "FastZScoreSpecOptions",
        "FastandSlowKurtosisOscillatorSpecOptions",
        "FearAndGreedIndicatorSpecOptions",
        "FibonacciRetraceSpecOptions",
        "FiniteVolumeElementsSpecOptions",
        "FireflyOscillatorSpecOptions",
        "FisherLeastSquaresMovingAverageSpecOptions",
        "FisherTransformSpecOptions",
        "FisherTransformStochasticOscillatorSpecOptions",
        "FoldedRelativeStrengthIndexSpecOptions",
        "FollowingAdaptiveMovingAverageSpecOptions",
        "ForceIndexSpecOptions",
        "ForecastOscillatorSpecOptions",
        "FramaSpecOptions",
        "FreedomOfMovementSpecOptions",
        "FunctionToCandlesSpecOptions",
        "GOscillatorSpecOptions",
        "GainLossMovingAverageSpecOptions",
        "GannHiLoActivatorSpecOptions",
        "GannTrendOscillatorSpecOptions",
        "GarmanKlassVolatilitySpecOptions",
        "GatorOscillatorSpecOptions",
        "GeneralFilterEstimatorSpecOptions",
        "GeneralizedDoubleEmaSpecOptions",
        "GopalakrishnanRangeIndexSpecOptions",
        "GroverLlorensActivatorSpecOptions",
        "GroverLlorensCycleOscillatorSpecOptions",
        "GuppyDistanceIndicatorSpecOptions",
        "GuppyMultipleMovingAverageSpecOptions",
        "HalfTrendSpecOptions",
        "HammingMaSpecOptions",
        "HampelFilterSpecOptions",
        "HighLowBandsSpecOptions",
        "HighLowIndexSpecOptions",
        "HighLowMovingAverageSpecOptions",
        "HirashimaSugitaRSSpecOptions",
        "HistoricalVolatilitySpecOptions",
        "HmaSpecOptions",
        "HoltExponentialMovingAverageSpecOptions",
        "HullEstimateSpecOptions",
        "HullMovingAverageSpecOptions",
        "HurstCycleChannelSpecOptions",
        "HybridConvolutionFilterSpecOptions",
        "IIRLeastSquaresEstimateSpecOptions",
        "ImpulsePercentagePriceOscillatorSpecOptions",
        "InertiaIndicatorSpecOptions",
        "InertiaSpecOptions",
        "InformationRatioSpecOptions",
        "InsyncIndexSpecOptions",
        "IntradayMomentumIndexSpecOptions",
        "InverseDistanceWeightedMovingAverageSpecOptions",
        "InverseFisherFastZScoreSpecOptions",
        "InverseFisherTransformCoreSpecOptions",
        "InverseFisherZScoreSpecOptions",
        "JapaneseCorrelationCoefficientSpecOptions",
        "JmaRsxCloneSpecOptions",
        "JmaSpecOptions",
        "JrcFractalDimensionSpecOptions",
        "JsaMovingAverageSpecOptions",
        "KalmanSmootherSpecOptions",
        "KarobeinOscillatorSpecOptions",
        "KaseConvergenceDivergenceSpecOptions",
        "KaseDevStopV2SpecOptions",
        "KaseIndicatorSpecOptions",
        "KasePeakOscillatorV1SpecOptions",
        "KasePeakOscillatorV2SpecOptions",
        "KaufmanAdaptiveCorrelationOscillatorSpecOptions",
        "KaufmanAdaptiveLeastSquaresMovingAverageSpecOptions",
        "KeltnerChannelMiddleSpecOptions",
        "KeltnerChannelWidthSpecOptions",
        "KeltnerChannelsSpecOptions",
        "KlingerSignalSpecOptions",
        "KnowSureThingSpecOptions",
        "KstSpecOptions",
        "KurtosisIndicatorSpecOptions",
        "KvoSpecOptions",
        "KwanIndicatorSpecOptions",
        "LBRPaintBarsSpecOptions",
        "LinRegInterceptSpecOptions",
        "LindaRaschke310OscillatorSpecOptions",
        "LinearExtrapolationSpecOptions",
        "LinearQuadraticConvergenceDivergenceOscillatorSpecOptions",
        "LinearRegressionInterceptSpecOptions",
        "LinearRegressionLineSpecOptions",
        "LiquidRelativeStrengthIndexSpecOptions",
        "LsmaSpecOptions",
        "MacZIndicatorSpecOptions",
        "MacZVwapIndicatorSpecOptions",
        "MarketMeannessIndexSpecOptions",
        "MartinRatioSpecOptions",
        "MassIndexCoreSpecOptions",
        "MassIndexSpecOptions",
        "MassThrustIndicatorSpecOptions",
        "MassThrustOscillatorSpecOptions",
        "MassThrustSpecOptions",
        "McClellanOscillatorSpecOptions",
        "McNichollMovingAverageSpecOptions",
        "MiddleHighLowMovingAverageSpecOptions",
        "MidpointOscillatorSpecOptions",
        "MirroredPercentagePriceOscillatorSpecOptions",
        "MobilityOscillatorSpecOptions",
        "ModifiedGannHiloActivatorSpecOptions",
        "ModifiedMaSpecOptions",
        "ModifiedPriceVolumeTrendSpecOptions",
        "ModularFilterSpecOptions",
        "MomentumOscillatorSpecOptions",
        "MomentumSpecOptions",
        "MovingAverageAdaptiveQSpecOptions",
        "MovingAverageV3SpecOptions",
        "MultiDepthZeroLagExponentialMovingAverageSpecOptions",
        "MultiVoteOnBalanceVolumeSpecOptions",
        "NarrowSidewaysChannelSpecOptions",
        "NatrSpecOptions",
        "NaturalDirectionalComboSpecOptions",
        "NaturalDirectionalIndexSpecOptions",
        "NaturalMaSpecOptions",
        "NaturalMarketComboSpecOptions",
        "NaturalMarketMirrorSpecOptions",
        "NaturalMarketRiverSpecOptions",
        "NaturalStochasticIndicatorSpecOptions",
        "NegativeVolumeDisparityIndicatorSpecOptions",
        "NegativeVolumeIndexSpecOptions",
        "OCHistogramSpecOptions",
        "OceanIndicatorSpecOptions",
        "OnBalanceVolumeModifiedSpecOptions",
        "OnBalanceVolumeReflexSpecOptions",
        "OneLCLeastSquaresMovingAverageSpecOptions",
        "OptimalWeightedMovingAverageSpecOptions",
        "OscOscillatorSpecOptions",
        "OvershootReductionMovingAverageSpecOptions",
        "ParabolicSarSpecOptions",
        "ParabolicWmaSpecOptions",
        "ParametricCorrectiveLinearMovingAverageSpecOptions",
        "ParametricKalmanFilterSpecOptions",
        "PeakValleyEstimationSpecOptions",
        "PentupleExponentialMovingAverageSpecOptions",
        "PercentChangeOscillatorSpecOptions",
        "PercentagePriceOscillatorLeaderSpecOptions",
        "PercentageTrendSpecOptions",
        "PfeSpecOptions",
        "PgoSpecOptions",
        "PhaseChangeIndexSpecOptions",
        "PivotDetectorOscillatorSpecOptions",
        "PivotPointAverageSpecOptions",
        "PmoSpecOptions",
        "PolarizedFractalEfficiencySpecOptions",
        "PolynomialLeastSquaresMovingAverageSpecOptions",
        "PositiveVolumeIndexSpecOptions",
        "PoweredKaufmanAdaptiveMovingAverageSpecOptions",
        "PremierStochasticOscillatorSpecOptions",
        "PrettyGoodOscillatorSpecOptions",
        "PriceChannelLowerSpecOptions",
        "PriceChannelMiddleSpecOptions",
        "PriceChannelUpperSpecOptions",
        "PriceCurveChannelSpecOptions",
        "PriceCycleOscillatorSpecOptions",
        "PriceLineChannelSpecOptions",
        "PriceMomentumOscillatorSpecOptions",
        "PriceOscillatorPercentSpecOptions",
        "PriceOscillatorSpecOptions",
        "PriceVolumeOscillatorSpecOptions",
        "PriceVolumeRankSpecOptions",
        "PriceZoneOscillatorSpecOptions",
        "PrimeNumberOscillatorSpecOptions",
        "PringSpecialKSpecOptions",
        "ProjectionBandwidthSpecOptions",
        "ProjectionOscillatorSpecOptions",
        "PzoSpecOptions",
        "QuadraticLeastSquaresMovingAverageSpecOptions",
        "QuadraticMovingAverageSpecOptions",
        "QuadraticRegressionSpecOptions",
        "QuadrupleExponentialMovingAverageSpecOptions",
        "QuickMovingAverageSpecOptions",
        "R2AdaptiveRegressionSpecOptions",
        "RSINGIndicatorSpecOptions",
        "RahulMohindarOscillatorSpecOptions",
        "RainbowOscillatorSpecOptions",
        "RandomWalkIndexSpecOptions",
        "RapidRelativeStrengthIndexSpecOptions",
        "RatioOchlAveragerSpecOptions",
        "ReallySimpleIndicatorSpecOptions",
        "RecursiveDifferenciatorSpecOptions",
        "RecursiveMovingTrendAverageSpecOptions",
        "RecursiveRelativeStrengthIndexSpecOptions",
        "RecursiveStochasticSpecOptions",
        "RegressionOscillatorSpecOptions",
        "RegularizedEmaSpecOptions",
        "RelativeDifferenceOfSquaresOscillatorSpecOptions",
        "RelativeMomentumIndexSpecOptions",
        "RelativeVigorIndexSignalSpecOptions",
        "RelativeVigorIndexSpecOptions",
        "RelativeVolatilityIndexSpecOptions",
        "RelativeVolatilityIndexV2SpecOptions",
        "RepulseSpecOptions",
        "RepulsionMovingAverageSpecOptions",
        "RetentionAccelerationFilterSpecOptions",
        "ReversalPointsSpecOptions",
        "RexOscillatorSpecOptions",
        "RightSidedRickerMovingAverageSpecOptions",
        "RobustWeightingOscillatorSpecOptions",
        "RunningEquitySpecOptions",
        "RviSpecOptions",
        "RviVolatilitySpecOptions",
        "SMIErgodicIndicatorSpecOptions",
        "ScalpersChannelSpecOptions",
        "SelfAdjustingRelativeStrengthIndexSpecOptions",
        "SellGravitationIndexSpecOptions",
        "SentimentZoneOscillatorSpecOptions",
        "SequentiallyFilteredMovingAverageSpecOptions",
        "SettingLessTrendStepFilteringSpecOptions",
        "ShapeshiftingMovingAverageSpecOptions",
        "SharpModifiedMovingAverageSpecOptions",
        "SharpeRatioSpecOptions",
        "SigmaSpikesSpecOptions",
        "SimpleCycleSpecOptions",
        "SimpleLinesSpecOptions",
        "SimplifiedLeastSquaresMovingAverageSpecOptions",
        "SimplifiedWeightedMovingAverageSpecOptions",
        "SlowSmoothedMovingAverageSpecOptions",
        "SmmaSpecOptions",
        "SmoothedDeltaRatioOscillatorSpecOptions",
        "SmoothedRateOfChangeSpecOptions",
        "SmoothedRocSpecOptions",
        "SortinoRatioSpecOptions",
        "SpearmanIndicatorSpecOptions",
        "SpecialKSpecOptions",
        "SqueezeMomentumIndicatorSpecOptions",
        "StandardDevationSpecOptions",
        "StandardDeviationChannelSpecOptions",
        "StationaryExtrapolatedLevelsOscillatorSpecOptions",
        "StationaryExtrapolatedLevelsSpecOptions",
        "StatisticalVolatilitySpecOptions",
        "StcSpecOptions",
        "StiffnessIndicatorSpecOptions",
        "StochRsiSpecOptions",
        "StochasticConnorsRelativeStrengthIndexSpecOptions",
        "StochasticCustomOscillatorSpecOptions",
        "StochasticFastOscillatorSpecOptions",
        "StochasticMacdOscillatorSpecOptions",
        "StochasticMomentumIndexSpecOptions",
        "StochasticOscillatorSpecOptions",
        "StochasticRelativeStrengthIndexSpecOptions",
        "StochasticRsiOscillatorSpecOptions",
        "StrengthOfMovementSpecOptions",
        "SuperSmootherSpecOptions",
        "SuperTrendSpecOptions",
        "SurfaceRoughnessEstimatorSpecOptions",
        "SvamaSpecOptions",
        "SwingIndexSpecOptions",
        "T3SpecOptions",
        "TFSMboPercentagePriceOscillatorSpecOptions",
        "TFSTetherLineIndicatorSpecOptions",
        "TFSVolumeOscillatorSpecOptions",
        "TStepLeastSquaresMovingAverageSpecOptions",
        "TechnicalRatingsSpecOptions",
        "TheRangeIndicatorSpecOptions",
        "ThreeHmaSpecOptions",
        "TickLineMomentumOscillatorSpecOptions",
        "TillsonIE2SpecOptions",
        "TillsonT3MovingAverageSpecOptions",
        "TmaSpecOptions",
        "TotalPowerIndicatorSpecOptions",
        "TradeVolumeIndexSpecOptions",
        "TradersDynamicIndexSpecOptions",
        "TradingMadeMoreSimplerOscillatorSpecOptions",
        "TrendAnalysisIndexSpecOptions",
        "TrendAnalysisIndicatorSpecOptions",
        "TrendContinuationFactorSpecOptions",
        "TrendDetectionSpecOptions",
        "TrendDirectionForceIndexSpecOptions",
        "TrendExhaustionIndicatorSpecOptions",
        "TrendIntensityIndexSpecOptions",
        "TrendPersistenceRateSpecOptions",
        "TrendScoreSpecOptions",
        "TrendTraderBandsSpecOptions",
        "TrenderSpecOptions",
        "TriangularMovingAverageSpecOptions",
        "TrigonometricOscillatorSpecOptions",
        "TrimeanSpecOptions",
        "TrixSpecOptions",
        "TrueStrengthIndexSpecOptions",
        "TsiSpecOptions",
        "TurboScalerSpecOptions",
        "TurboStochasticsFastSpecOptions",
        "TurboStochasticsSlowSpecOptions",
        "TurboTriggerSpecOptions",
        "TwiggsMoneyFlowSpecOptions",
        "UlcerIndexSpecOptions",
        "UltimateMovingAverageBandsSpecOptions",
        "UltimateMovingAverageSpecOptions",
        "UltimateOscillatorSpecOptions",
        "UltimateTraderOscillatorSpecOptions",
        "ValueChartIndicatorSpecOptions",
        "VaradiOscillatorSpecOptions",
        "VariableAdaptiveMovingAverageSpecOptions",
        "VariableLengthMovingAverageSpecOptions",
        "VariableMovingAverageBandsSpecOptions",
        "VerticalHorizontalFilterSpecOptions",
        "VerticalHorizontalMovingAverageSpecOptions",
        "VervoortHeikenAshiCandlestickOscillatorSpecOptions",
        "VervoortHeikenAshiLongTermCandlestickOscillatorSpecOptions",
        "VervoortSmoothedOscillatorSpecOptions",
        "VervoortVolatilityBandsSpecOptions",
        "VhfSpecOptions",
        "VolatilityBasedMomentumSpecOptions",
        "VolatilityMovingAverageSpecOptions",
        "VolatilityQualityIndexSpecOptions",
        "VolatilityRatioSpecOptions",
        "VolatilityStopSpecOptions",
        "VolatilitySwitchIndicatorSpecOptions",
        "VolatilityWaveMovingAverageSpecOptions",
        "VolumeAccumulationOscillatorSpecOptions",
        "VolumeAdaptiveBandsSpecOptions",
        "VolumeAdjustedMaSpecOptions",
        "VolumeAdjustedMovingAverageSpecOptions",
        "VolumeFlowIndicatorSpecOptions",
        "VolumeWeightedMovingAverageSpecOptions",
        "VolumeWeightedRsiSpecOptions",
        "VpciSpecOptions",
        "WamiOscillatorSpecOptions",
        "WaveTrendOscillatorSpecOptions",
        "WellRoundedMovingAverageSpecOptions",
        "WildersSummationMethodSpecOptions",
        "WilliamsFractalDownSpecOptions",
        "WilliamsFractalUpSpecOptions",
        "WilsonRelativePriceChannelSpecOptions",
        "WindowedVolumeWeightedMovingAverageSpecOptions",
        "WoodieCommodityChannelIndexSpecOptions",
        "ZScoreSpecOptions",
        "ZeroLowLagMovingAverageSpecOptions",
        "ZigZagSpecOptions",
        "_1LCLeastSquaresMovingAverageSpecOptions",
        "_3HMASpecOptions",
    };

    /// <summary>
    /// The four arms issue #233 identified by name, each computing a different indicator entirely.
    /// </summary>
    /// <remarks>
    /// <c>TrendCore.AutoLine</c> and <c>AutoLineWithDrift</c> are adaptive exponential averages where the
    /// automatic line holds its level until price escapes a band; <c>UltimateMovingAverage</c> is a T3 of a T3
    /// where the indicator is a money-flow weighted average; <c>VariableLengthMovingAverage</c> interpolates a
    /// length from a normalised deviation where the indicator steps the length one bar at a time against four
    /// levels. Held here to "not served", which stays true once someone fixes the arm.
    /// </remarks>
    private static readonly string[] ArmsNamedInIssue233 =
    {
        "AutoLineSpecOptions",
        "AutoLineWithDriftSpecOptions",
        "UltimateMovingAverageSpecOptions",
        "VariableLengthMovingAverageSpecOptions",
    };

    /// <summary>Keeps the first divergence recorded for a type, so the default parameter set is reported.</summary>
    private static void Record(Dictionary<string, string> divergence, string name, string detail)
    {
        if (!divergence.ContainsKey(name))
        {
            divergence[name] = detail;
        }
    }

    /// <summary>The recorded divergence for each named type, or a note that none was captured.</summary>
    private static string Describe(Dictionary<string, string> divergence, IEnumerable<string> names)
    {
        var described = names
            .Select(name => divergence.TryGetValue(name, out var detail) ? $"{name} -> {detail}" : $"{name} -> no divergence captured")
            .ToList();

        return described.Count == 0 ? "nothing to describe" : string.Join(" | ", described);
    }

    /// <summary>
    /// No arm the Builder serves computes a different indicator, and the set that does cannot grow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EveryTypedSpecComputesItsBatchIndicator"/> drives <c>TryComputeFast</c>, the served path. For
    /// a spec outside <see cref="BuilderVerifiedArms"/> that call routes to <see cref="BuilderArmBinding"/> and
    /// is then compared against <see cref="BuilderArmBinding"/> - the same batch indicator on both sides, so it
    /// agrees by construction. That is right for what it guards, but it leaves an unserved arm compared to
    /// nothing at all.
    /// </para>
    /// <para>
    /// This drives <c>IndicatorCompute.ComputeArm</c>, the arm itself, unchecked. The property that matters is
    /// not "every arm is correct" - hundreds are not - but that <b>verification and agreement cannot come
    /// apart</b>:
    /// adding an options type to <see cref="BuilderVerifiedArms"/> starts serving its arm immediately, and
    /// before this test nothing on that path would have noticed the arm computed something else. Promote one of
    /// the listed types and the first assertion below fails.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoServedArmDisagreesWithItsBoundCall()
    {
        var tickers = StockTestData.ToList();
        var disagreed = new SortedSet<string>(StringComparer.Ordinal);

        // Where each one first parts company with its bound call. Detection alone leaves the next author
        // reading two implementations side by side to find out why; the bar index and the two values say
        // which one to look at and from where, which is the difference between a list and a work queue.
        var divergence = new Dictionary<string, string>(StringComparer.Ordinal);
        var stats = new Dictionary<string, string>(StringComparer.Ordinal);
        var compared = 0;

        foreach (var type in OptionTypes)
        {
            if (!BuilderArmBinding.TryGetTarget(type, out var target))
            {
                continue;
            }

            foreach (var alternate in new[] { false, true })
            {
                var options = Create(type, alternate);
                if (options is null)
                {
                    break;
                }

                var spec = new IndicatorSpec(target.Name, options);

                double[]? arm;
                try
                {
                    arm = Run(IndicatorCompute.ComputeArm, tickers, spec);
                }
                catch
                {
                    // No runnable arm, so there is nothing to hold to anything. Whether an arm that throws
                    // matters is decided by the served sweep, which is where it would be served.
                    continue;
                }

                if (arm is null)
                {
                    // ComputeArm returns null for an options type with no arm at all.
                    continue;
                }

                compared++;
                var label = alternate ? "alternate parameters" : "default parameters";
                try
                {
                    var expected = BuilderArmBinding.Compute(new StockData(tickers), spec, target);
                    if (arm.Length != expected.Count)
                    {
                        disagreed.Add(type.Name);
                        Record(divergence, type.Name,
                            $"{label}: arm produced {arm.Length} values, {target.Name} produced {expected.Count}");
                        continue;
                    }

                    var bar = Enumerable.Range(0, arm.Length).FirstOrDefault(i => !IsClose(expected[i], arm[i]), -1);
                    if (bar >= 0 && !stats.ContainsKey(type.Name))
                    {
                        var diffs = Enumerable.Range(0, arm.Length).Where(i => !IsClose(expected[i], arm[i])).ToList();
                        var tailStart = Math.Min(60, arm.Length);
                        double tailMax = 0;
                        for (var i = tailStart; i < arm.Length; i++)
                        {
                            if (!IsClose(expected[i], arm[i]))
                            {
                                var denom = Math.Max(Math.Abs(expected[i]), Math.Abs(arm[i]));
                                tailMax = Math.Max(tailMax, denom > 0 ? Math.Abs(expected[i] - arm[i]) / denom : 1);
                            }
                        }

                        stats[type.Name] = $"{arm.Length}	{diffs.Count}	{diffs[0]}	{diffs[^1]}	{tailMax:R}";
                    }

                    if (bar >= 0)
                    {
                        disagreed.Add(type.Name);
                        Record(divergence, type.Name,
                            $"{label}: first differs at bar {bar} - arm {arm[bar]:R}, {target.Name} {expected[bar]:R}");
                    }
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    disagreed.Add(type.Name);
                    Record(divergence, type.Name, $"{label}: {inner.GetType().Name} {inner.Message}");
                }
            }
        }

        // The assertion message can only carry the arms that changed, which is the right size for a
        // failure but the wrong size for planning: with hundreds of arms still to repair, the question is
        // which of them share a cause. Set ARM_DIVERGENCE_DUMP to a path and the whole set is written as
        // tab-separated name, length, differing bars, first, last, worst relative difference from bar 60
        // onwards, and the sentence above - enough to tell a run-in convention apart from different
        // arithmetic without opening a single file. Unset, which is how CI runs, this does nothing.
        var dumpPath = Environment.GetEnvironmentVariable("ARM_DIVERGENCE_DUMP");
        if (!string.IsNullOrWhiteSpace(dumpPath))
        {
            File.WriteAllLines(dumpPath, disagreed.Select(name =>
                string.Join("	",
                    name,
                    stats.TryGetValue(name, out var stat) ? stat : "			",
                    divergence.TryGetValue(name, out var detail) ? detail : "no divergence captured")));
        }

        var servedAndWrong = disagreed.Where(name => BuilderVerifiedArms.Arms.Any(t => t.Name == name)).ToList();
        var unexpected = disagreed.Except(ArmsDisagreeingWithTheirBoundCall).ToList();

        using var scope = new AssertionScope();
        compared.Should().BeGreaterThan(200, "every bound spec with an arm is driven through that arm");

        // The promotion guard. A verified arm IS served, so a verified arm that computes something else is
        // wrong numbers reaching callers - which is exactly what promoting a listed type would do.
        servedAndWrong.Should().BeEmpty(
            "an arm in BuilderVerifiedArms is served, so it must compute the indicator it is named for. "
            + $"Where each one parts company: {Describe(divergence, servedAndWrong)}");

        unexpected.Should().BeEmpty(
            "an arm that has started disagreeing with its bound call is a blocker for ever verifying it. "
            + $"Where each one parts company: {Describe(divergence, unexpected)}");
        ArmsDisagreeingWithTheirBoundCall.Except(disagreed).Should().BeEmpty(
            "an arm repaired to compute its batch indicator leaves the list, so the list shrinks visibly");

        foreach (var named in ArmsNamedInIssue233)
        {
            BuilderVerifiedArms.Arms.Should().NotContain(t => t.Name == named,
                $"{named} computes a different indicator from the one it is named for (#233), so serving it "
                + "would publish the wrong series");
        }
    }

    [Fact]
    public void EveryVerifiedArmIsABoundSpec()
    {
        using var scope = new AssertionScope();

        // Arms was a set of (options type, slot) pairs and is now a set of options types: a verified arm is
        // verified for the indicator, not for one of six slots of it. See issue #219.
        foreach (var options in BuilderVerifiedArms.Arms)
        {
            BuilderArmBinding.TryGetTarget(options, out _).Should().BeTrue($"{options.Name} is verified against a batch indicator it must name");
        }
    }

    private static double[]? Run(Func<StockData, IndicatorSpec, ComputeContext, ComputeBuffer?> compute, List<TickerData> tickers, IndicatorSpec spec)
    {
        using var context = new ComputeContext();
        var result = compute(new StockData(tickers), spec, context);
        if (result is null)
        {
            return null;
        }

        using var buffer = result.Value;
        return buffer.ToArray();
    }

    private static bool Same(double[] a, double[] b) => a.Length == b.Length && !a.Where((v, i) => !IsClose(v, b[i])).Any();

    /// <summary>A null for the indicator's own series, then each key it publishes.</summary>
    private static IEnumerable<string?> OwnSeriesThenPublishedKeys(IndicatorName name)
    {
        yield return null;

        foreach (var key in GeneratedIndicatorOutputs.KeysFor(name))
        {
            yield return key;
        }
    }

    internal static IIndicatorSpecOptions? Create(Type type, bool alternate)
    {
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().All(p => p.HasDefaultValue || Value(p.ParameterType, null, false) is not null));
        if (ctor is null)
        {
            return null;
        }

        var args = ctor.GetParameters()
            .Select(p => Value(p.ParameterType, p.HasDefaultValue ? p.DefaultValue : null, alternate))
            .ToArray();
        return (IIndicatorSpecOptions)ctor.Invoke(args);
    }

    // Defaults as declared, and an alternate set that moves every length and multiplier off its default, so an arm
    // that matches only at its defaults does not count as verified.
    //
    // Internal rather than private because the alternate set is the same idea the streaming parity sweep needs for
    // indicators with no typed spec, and two sets of perturbation rules that drifted apart would make the two
    // suites disagree about what "alternate parameters" even means.
    internal static object? Value(Type type, object? declared, bool alternate)
    {
        if (type == typeof(int))
        {
            var value = declared is int i ? i : 14;
            return alternate ? value + 3 : value;
        }

        if (type == typeof(double))
        {
            var value = declared is double d ? d : 2.0;
            return alternate ? (value == 0 ? 0.5 : value * 1.5) : value;
        }

        if (type == typeof(MovingAvgType))
        {
            return alternate ? MovingAvgType.ExponentialMovingAverage : declared ?? MovingAvgType.SimpleMovingAverage;
        }

        if (type == typeof(InputName))
        {
            return declared ?? InputName.Close;
        }

        if (type == typeof(bool))
        {
            var value = declared is bool b && b;
            return alternate ? !value : value;
        }

        if (type.IsEnum)
        {
            return declared ?? Enum.GetValues(type).GetValue(0);
        }

        return declared;
    }
}
