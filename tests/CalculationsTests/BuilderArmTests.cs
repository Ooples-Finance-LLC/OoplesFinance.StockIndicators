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
    /// sets, these option types return something other than <see cref="BuilderArmBinding"/> does for the same
    /// spec, at one or both of those sets. A type is listed once however many of its parameter sets disagree.
    /// <para>
    /// "Disagrees with its bound call" is the claim, and it is deliberately weaker than "computes a different
    /// indicator". Two causes reach this list. One is a genuinely different formula:
    /// <c>AutoLineSpecOptions</c> maps its <c>Length</c> straight onto <c>CalculateAutoLine(length)</c>, so its
    /// arm was compared like for like and still disagreed. The other is parameterisation:
    /// <c>UltimateMovingAverageSpecOptions.Length</c> is <c>[Obsolete]</c> because the indicator has no
    /// parameter it could set, <c>MapArguments</c> skips obsolete properties, so the bound call ran at
    /// <c>minLength: 5, maxLength: 50</c> while the arm ran at the option's length. Both make an arm unsafe to
    /// verify, which is what this guards; only the first means the arm implements the wrong maths.
    /// </para>
    /// <para>
    /// The second cause is now empty here. The 51 arms whose options type carried an <c>[Obsolete]</c>
    /// "has no effect" option were deleted rather than listed: an arm honouring an option its indicator has no
    /// parameter for can never agree with the bound call, so it could never be verified, and none was served.
    /// What remains are 538 whose every option maps to a real, non-obsolete parameter - compared like for like,
    /// so the arm's maths is what differs. None has an unmapped non-obsolete option;
    /// <see cref="EveryOptionReachesTheBatchIndicator"/> already forbids that. That 538 is still an upper bound
    /// on "wrong maths" rather than a count of it: an arm can also disagree by ignoring an option it was
    /// handed, which this partition cannot see from the outside.
    /// </para>
    /// That is
    /// close to every arm outside <see cref="BuilderVerifiedArms"/>, which is why #229 stopped serving them:
    /// the Builder computes those specs with their batch indicator, so callers get correct numbers today.
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
        "AcceleratorOscillatorSpecOptions",
        "AccumulativeSwingIndexSpecOptions",
        "AdaptiveAutonomousRecursiveMovingAverageSpecOptions",
        "AdaptiveAutonomousRecursiveTrailingStopSpecOptions",
        "AdaptiveEmaSpecOptions",
        "AdaptiveErgodicCandlestickOscillatorSpecOptions",
        "AdaptiveExponentialMovingAverageSpecOptions",
        "AdaptiveLeastSquaresSpecOptions",
        "AdaptivePriceZoneIndicatorSpecOptions",
        "AdaptiveRelativeStrengthIndexSpecOptions",
        "AdaptiveStochasticSpecOptions",
        "AdaptiveTrailingStopSpecOptions",
        "AdxSpecOptions",
        "AhrensMovingAverageSpecOptions",
        "AlligatorJawSpecOptions",
        "AlligatorLipsSpecOptions",
        "AlligatorTeethSpecOptions",
        "AlmaSpecOptions",
        "AmaSpecOptions",
        "AnchoredMomentumSpecOptions",
        "ApirineSlowRelativeStrengthIndexSpecOptions",
        "AroonSpecOptions",
        "AtrChannelWidthSpecOptions",
        "AtrFilteredEmaSpecOptions",
        "AtrFilteredExponentialMovingAverageSpecOptions",
        "AtrPercentSpecOptions",
        "AtrSpecOptions",
        "AtrTrailingStopsSpecOptions",
        "AutoDispersionBandsSpecOptions",
        "AutoFilterSpecOptions",
        "AutonomousRecursiveMaSpecOptions",
        "AverageDirectionalIndexSpecOptions",
        "AverageMoneyFlowOscillatorSpecOptions",
        "AverageTrueRangeChannelSpecOptions",
        "AverageTrueRangeSpecOptions",
        "AverageTrueRangeTrailingStopsSpecOptions",
        "AwesomeOscillatorSpecOptions",
        "BayesianOscillatorSpecOptions",
        "BearPowerIndicatorSpecOptions",
        "BearPowerSpecOptions",
        "BilateralStochasticOscillatorSpecOptions",
        "BollingerBandsAtrSpecOptions",
        "BollingerBandsAvgTrueRangeSpecOptions",
        "BollingerBandsSpecOptions",
        "BollingerBandsWidthSpecOptions",
        "BreakoutRsiSpecOptions",
        "BryantAdaptiveMovingAverageSpecOptions",
        "BuffAverageSpecOptions",
        "BullPowerIndicatorSpecOptions",
        "BullPowerSpecOptions",
        "ButterworthFilterSpecOptions",
        "CCTStochRSISpecOptions",
        "CalmarRatioSpecOptions",
        "CamarillaPivotPointSpecOptions",
        "CciSpecOptions",
        "CenterOfLinearitySpecOptions",
        "ChaikinVolatilitySpecOptions",
        "ChandeIntradayMomentumIndexSpecOptions",
        "ChandeKrollRSquaredIndexSpecOptions",
        "ChandeMomentumOscillatorAbsoluteSpecOptions",
        "ChandeMomentumOscillatorFilterSpecOptions",
        "ChandeMomentumOscillatorSignalSpecOptions",
        "ChandeMomentumOscillatorSpecOptions",
        "ChandeQuickStickSpecOptions",
        "ChandeTrendScoreSpecOptions",
        "ChandelierExitLongSpecOptions",
        "ChandelierExitShortSpecOptions",
        "ChandelierExitSpecOptions",
        "ChartmillValueIndicatorSpecOptions",
        "ChopZoneSpecOptions",
        "ChoppinessIndexSpecOptions",
        "CmfSpecOptions",
        "CmoSpecOptions",
        "CommoditySelectionIndexSpecOptions",
        "CompoundRatioMovingAverageSpecOptions",
        "ConditionalAccumulatorSpecOptions",
        "ConnorsRsiSpecOptions",
        "ConstanceBrownCompositeIndexSpecOptions",
        "CoppockCurveSpecOptions",
        "CorrectedMovingAverageSpecOptions",
        "CubedWeightedMovingAverageSpecOptions",
        "CubicWmaSpecOptions",
        "DMIStochasticSpecOptions",
        "DTOscillatorSpecOptions",
        "DailyAveragePriceDeltaSpecOptions",
        "DampedSineWaveWeightedFilterSpecOptions",
        "DampingIndexSpecOptions",
        "DeMarkerSpecOptions",
        "DecisionPointBreadthSwenlinTradingOscillatorSpecOptions",
        "DecisionPointPriceMomentumOscillatorSpecOptions",
        "DeltaMovingAverageSpecOptions",
        "Dema2LinesSpecOptions",
        "DemarkPivotPointSpecOptions",
        "DemarkPressureRatioV1SpecOptions",
        "DemarkPressureRatioV2SpecOptions",
        "DemarkRangeExpansionIndexSpecOptions",
        "DemarkerSpecOptions",
        "DerivativeOscillatorSpecOptions",
        "DetrendedPriceOscillatorSpecOptions",
        "DetrendedSyntheticPriceSpecOptions",
        "DiNapoliPreferredStochasticOscillatorSpecOptions",
        "DidiIndexSpecOptions",
        "DirectionalTrendIndexSpecOptions",
        "DisparityIndexSpecOptions",
        "DonchianChannelSpecOptions",
        "DonchianChannelWidthSpecOptions",
        "DoubleSmoothedMomentaSpecOptions",
        "DoubleSmoothedStochasticSpecOptions",
        "DoubleStochasticOscillatorSpecOptions",
        "DpoSpecOptions",
        "DynamicMomentumOscillatorSpecOptions",
        "DynamicSupportAndResistanceSpecOptions",
        "DynamicallyAdjustableFilterSpecOptions",
        "DynamicallyAdjustableMovingAverageSpecOptions",
        "EaseOfMovementSpecOptions",
        "EdgePreservingFilterSpecOptions",
        "EhlersAdaptiveCenterOfGravityOscillatorSpecOptions",
        "EhlersAdaptiveCommodityChannelIndexV2SpecOptions",
        "EhlersAdaptiveLaguerreFilterSpecOptions",
        "EhlersAdaptiveRelativeStrengthIndexV2SpecOptions",
        "EhlersAdaptiveRsiFisherTransformV2SpecOptions",
        "EhlersAdaptiveStochasticIndicatorV2SpecOptions",
        "EhlersAllPassPhaseShifterSpecOptions",
        "EhlersAverageErrorFilterSpecOptions",
        "EhlersBandPassFilterV1SpecOptions",
        "EhlersBetterExponentialMovingAverageSpecOptions",
        "EhlersCenterOfGravityOscillatorSpecOptions",
        "EhlersClassicHilbertTransformerSpecOptions",
        "EhlersCorrelationTrendIndicatorSpecOptions",
        "EhlersDecyclerOscillatorV1SpecOptions",
        "EhlersDecyclerOscillatorV2SpecOptions",
        "EhlersDistanceCoefficientFilterSpecOptions",
        "EhlersFilterSpecOptions",
        "EhlersFisherizedDeviationScaledOscillatorSpecOptions",
        "EhlersFramaSpecOptions",
        "EhlersGaussianFilterSpecOptions",
        "EhlersHammingWindowIndicatorSpecOptions",
        "EhlersHannWindowIndicatorSpecOptions",
        "EhlersHighPassFilterV2SpecOptions",
        "EhlersHilbertOscillatorSpecOptions",
        "EhlersIirFilterSpecOptions",
        "EhlersImpulseResponseSpecOptions",
        "EhlersInfiniteImpulseResponseFilterSpecOptions",
        "EhlersInverseFisherTransformSpecOptions",
        "EhlersKaufmanAdaptiveMovingAverageSpecOptions",
        "EhlersMedianAverageAdaptiveFilterSpecOptions",
        "EhlersMesaPredictIndicatorV2SpecOptions",
        "EhlersModifiedStochasticIndicatorSpecOptions",
        "EhlersPhaseCalculationSpecOptions",
        "EhlersReflexSpecOptions",
        "EhlersRelativeVigorIndexSpecOptions",
        "EhlersReverseEmaIndicatorV2SpecOptions",
        "EhlersRocketRelativeStrengthIndexSpecOptions",
        "EhlersRoofingFilterSpecOptions",
        "EhlersRoofingFilterV1SpecOptions",
        "EhlersSimpleClipIndicatorSpecOptions",
        "EhlersSimpleDerivIndicatorSpecOptions",
        "EhlersSimpleWindowIndicatorSpecOptions",
        "EhlersSmoothedAdaptiveMomentumSpecOptions",
        "EhlersStochasticCenterOfGravityOscillatorSpecOptions",
        "EhlersStochasticSpecOptions",
        "EhlersTrendExtractionSpecOptions",
        "EhlersTriangleWindowIndicatorSpecOptions",
        "EhlersTripleDelayLineDetrenderSpecOptions",
        "EhlersUniversalOscillatorSpecOptions",
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
        "FastZScoreSpecOptions",
        "FastandSlowKurtosisOscillatorSpecOptions",
        "FearAndGreedIndicatorSpecOptions",
        "FibonacciPivotPointSpecOptions",
        "FibonacciRetraceSpecOptions",
        "FibonacciWeightedMovingAverageSpecOptions",
        "FiniteVolumeElementsSpecOptions",
        "FireflyOscillatorSpecOptions",
        "FisherLeastSquaresMovingAverageSpecOptions",
        "FisherTransformSpecOptions",
        "FisherTransformStochasticOscillatorSpecOptions",
        "FloorPivotPointR1SpecOptions",
        "FloorPivotPointS1SpecOptions",
        "FloorPivotPointSpecOptions",
        "FoldedRelativeStrengthIndexSpecOptions",
        "ForceIndexSpecOptions",
        "ForecastOscillatorSpecOptions",
        "FramaSpecOptions",
        "FreedomOfMovementSpecOptions",
        "FunctionToCandlesSpecOptions",
        "GOscillatorSpecOptions",
        "GainLossMovingAverageSpecOptions",
        "GannHiLoActivatorSpecOptions",
        "GannSwingOscillatorSpecOptions",
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
        "IchimokuKijunSenSpecOptions",
        "IchimokuSenkouSpanASpecOptions",
        "IchimokuSenkouSpanBSpecOptions",
        "IchimokuTenkanSenSpecOptions",
        "ImpulsePercentagePriceOscillatorSpecOptions",
        "InertiaIndicatorSpecOptions",
        "InertiaSpecOptions",
        "InformationRatioSpecOptions",
        "InsyncIndexSpecOptions",
        "IntradayMomentumIndexSpecOptions",
        "InverseDistanceWeightedMovingAverageSpecOptions",
        "InverseFisherFastZScoreSpecOptions",
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
        "KeltnerChannelsSpecOptions",
        "KlingerSignalSpecOptions",
        "KnowSureThingSpecOptions",
        "KstSpecOptions",
        "KvoSpecOptions",
        "KwanIndicatorSpecOptions",
        "LBRPaintBarsSpecOptions",
        "LeoMovingAverageSpecOptions",
        "LightLeastSquaresMovingAverageSpecOptions",
        "LinRegInterceptSpecOptions",
        "LinRegSlopeSpecOptions",
        "LindaRaschke310OscillatorSpecOptions",
        "LinearExtrapolationSpecOptions",
        "LinearQuadraticConvergenceDivergenceOscillatorSpecOptions",
        "LinearRegressionInterceptSpecOptions",
        "LinearRegressionLineSpecOptions",
        "LinearRegressionSlopeSpecOptions",
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
        "McNichollMovingAverageSpecOptions",
        "MfiCoreSpecOptions",
        "MfiSpecOptions",
        "MiddleHighLowMovingAverageSpecOptions",
        "MidpointOscillatorSpecOptions",
        "MidpointSpecOptions",
        "MidpriceSpecOptions",
        "MirroredPercentagePriceOscillatorSpecOptions",
        "MobilityOscillatorSpecOptions",
        "ModifiedMaSpecOptions",
        "ModifiedPriceVolumeTrendSpecOptions",
        "ModularFilterSpecOptions",
        "MomentumSpecOptions",
        "MovingAverageAdaptiveQSpecOptions",
        "MovingAverageV3SpecOptions",
        "MultiDepthZeroLagExponentialMovingAverageSpecOptions",
        "MultiVoteOnBalanceVolumeSpecOptions",
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
        "NthOrderDifferencingOscillatorSpecOptions",
        "OCHistogramSpecOptions",
        "OceanIndicatorSpecOptions",
        "OnBalanceVolumeModifiedSpecOptions",
        "OnBalanceVolumeReflexSpecOptions",
        "OneLCLeastSquaresMovingAverageSpecOptions",
        "OptimalWeightedMovingAverageSpecOptions",
        "OscOscillatorSpecOptions",
        "OvershootReductionMovingAverageSpecOptions",
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
        "PivotPointAverageSpecOptions",
        "PmoSpecOptions",
        "PolarizedFractalEfficiencySpecOptions",
        "PolynomialLeastSquaresMovingAverageSpecOptions",
        "PositiveVolumeIndexSpecOptions",
        "PoweredKaufmanAdaptiveMovingAverageSpecOptions",
        "PremierStochasticOscillatorSpecOptions",
        "PremierStochasticSpecOptions",
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
        "QuadraticWmaSpecOptions",
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
        "RelativeSpreadStrengthSpecOptions",
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
        "RsiSpecOptions",
        "RunningEquitySpecOptions",
        "RviSpecOptions",
        "RviVolatilitySpecOptions",
        "SMIErgodicIndicatorSpecOptions",
        "ScalpersChannelSpecOptions",
        "SelfAdjustingRelativeStrengthIndexSpecOptions",
        "SellGravitationIndexSpecOptions",
        "SentimentZoneOscillatorSpecOptions",
        "SequentiallyFilteredMovingAverageSpecOptions",
        "ShapeshiftingMovingAverageSpecOptions",
        "SharpModifiedMovingAverageSpecOptions",
        "SharpeRatioSpecOptions",
        "SigmaSpikesSpecOptions",
        "SimpleCycleSpecOptions",
        "SimpleLinesSpecOptions",
        "SimplifiedLeastSquaresMovingAverageSpecOptions",
        "SimplifiedWeightedMovingAverageSpecOptions",
        "SineWmaSpecOptions",
        "SlowSmoothedMovingAverageSpecOptions",
        "SmmaSpecOptions",
        "SmoothedDeltaRatioOscillatorSpecOptions",
        "SmoothedRateOfChangeSpecOptions",
        "SmoothedRocSpecOptions",
        "SmoothedWilliamsAccumulationDistributionSpecOptions",
        "SortinoRatioSpecOptions",
        "SpearmanIndicatorSpecOptions",
        "SquareRootWeightedMovingAverageSpecOptions",
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
        "StochasticDSpecOptions",
        "StochasticFastOscillatorSpecOptions",
        "StochasticKSpecOptions",
        "StochasticMacdOscillatorSpecOptions",
        "StochasticMomentumIndexSpecOptions",
        "StochasticOscillatorSpecOptions",
        "StochasticRegularSpecOptions",
        "StochasticRelativeStrengthIndexSpecOptions",
        "StochasticRsiOscillatorSpecOptions",
        "StochasticSpecOptions",
        "StrengthOfMovementSpecOptions",
        "SuperSmootherSpecOptions",
        "SuperTrendSpecOptions",
        "SurfaceRoughnessEstimatorSpecOptions",
        "SvamaSpecOptions",
        "SwingIndexSpecOptions",
        "T3SpecOptions",
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
        "TrendDetectionIndexSpecOptions",
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
        "TurboStochasticsFastSpecOptions",
        "TurboStochasticsSlowSpecOptions",
        "TwiggsMoneyFlowSpecOptions",
        "UlcerIndexSpecOptions",
        "UltimateOscillatorSpecOptions",
        "UltimateTraderOscillatorSpecOptions",
        "VaradiOscillatorSpecOptions",
        "VariableAdaptiveMovingAverageSpecOptions",
        "VariableMovingAverageBandsSpecOptions",
        "VerticalHorizontalFilterSpecOptions",
        "VerticalHorizontalMovingAverageSpecOptions",
        "VervoortHeikenAshiCandlestickOscillatorSpecOptions",
        "VervoortHeikenAshiLongTermCandlestickOscillatorSpecOptions",
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
        "VortexIndicatorMinusSpecOptions",
        "VortexIndicatorPlusSpecOptions",
        "VortexMinusSpecOptions",
        "VortexNegativeSpecOptions",
        "VortexPlusSpecOptions",
        "VortexPositiveSpecOptions",
        "VpciSpecOptions",
        "VwmaSpecOptions",
        "WamiOscillatorSpecOptions",
        "WaveTrendOscillatorSpecOptions",
        "WellRoundedMovingAverageSpecOptions",
        "WildersSummationMethodSpecOptions",
        "WilliamsAccumulationDistributionSpecOptions",
        "WilliamsFractalDownSpecOptions",
        "WilliamsFractalUpSpecOptions",
        "WilliamsRSpecOptions",
        "WilsonRelativePriceChannelSpecOptions",
        "WindowedVolumeWeightedMovingAverageSpecOptions",
        "WoodieCommodityChannelIndexSpecOptions",
        "WoodiePivotPointSpecOptions",
        "ZScoreSpecOptions",
        "ZeroLowLagMovingAverageSpecOptions",
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
                try
                {
                    var expected = BuilderArmBinding.Compute(new StockData(tickers), spec, target);
                    if (arm.Length != expected.Count || !Same(arm, expected.ToArray()))
                    {
                        disagreed.Add(type.Name);
                    }
                }
                catch
                {
                    disagreed.Add(type.Name);
                }
            }
        }

        var servedAndWrong = disagreed.Where(name => BuilderVerifiedArms.Arms.Any(t => t.Name == name)).ToList();

        using var scope = new AssertionScope();
        compared.Should().BeGreaterThan(200, "every bound spec with an arm is driven through that arm");

        // The promotion guard. A verified arm IS served, so a verified arm that computes something else is
        // wrong numbers reaching callers - which is exactly what promoting a listed type would do.
        servedAndWrong.Should().BeEmpty(
            "an arm in BuilderVerifiedArms is served, so it must compute the indicator it is named for");

        disagreed.Except(ArmsDisagreeingWithTheirBoundCall).Should().BeEmpty(
            "an arm that has started computing something other than its batch indicator joins the list, and is "
            + "a blocker for ever verifying it");
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
