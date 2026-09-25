using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

/// <summary>Deliberately direct reference arithmetic; never calls calculation engines or states.</summary>
internal static partial class BuiltInFormulaReferences
{
    private static double ExactPriceMean(params double[] values)
    {
        var sum = new ReferenceFraction(0);
        foreach (var value in values) sum += ReferenceFraction.FromDouble(value);
        return (sum / new ReferenceFraction(values.Length)).ToDouble();
    }

    internal static IEnumerable<IndicatorValidationRule> For(IIndicator indicator)
    {
        if (indicator is not IBuiltInIndicator builtIn || !UniformBuiltInComponents(indicator)) yield break;
        if (builtIn.BatchName == IndicatorName.EhlersRestoringPullIndicator)
        {
            var pullOptions = builtIn.CreateOptions();
            var pullKeys = new[] { "Rpi", "Signal" };
            for (var slot = 0; slot < pullKeys.Length; slot++)
            {
                var key = pullKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RestoringPullOutputs(bars, pullOptions)[key], new IndicatorErrorBudget(1e-9, 1e-9));
            }
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.WeightedMovingAverage or IndicatorName.LinearWeightedMovingAverage or IndicatorName.SimplifiedWeightedMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length");
            yield return IndicatorValidationRule.Reference(0, bars => ExactWeightedWindow(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.ParabolicWeightedMovingAverage or IndicatorName.CubedWeightedMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            var power = builtIn.BatchName == IndicatorName.ParabolicWeightedMovingAverage ? 2 : 3;
            yield return IndicatorValidationRule.Reference(0, bars => RoundedPowerMean(bars, period, power), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.VariableIndexDynamicAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedVidya(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.ChandeMomentumOscillatorAbsolute)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 9);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedAbsoluteChande(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.ChandeMomentumOscillatorAverage or IndicatorName.ChandeMomentumOscillatorAbsoluteAverage)
        {
            yield return IndicatorValidationRule.Reference(0, bars => RoundedChandeAverage(bars,
                absolute: builtIn.BatchName == IndicatorName.ChandeMomentumOscillatorAbsoluteAverage), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasRoundedEnvelope(builtIn))
        {
            var envelopeOptions = builtIn.CreateOptions();
            var envelopeKeys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "MiddleBand" }
                : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            for (var slot = 0; slot < envelopeKeys.Count; slot++)
            {
                var key = envelopeKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedEnvelope(bars, Integer(envelopeOptions, "Length", 20), BoundedMeanKind(envelopeOptions, 1), Number(envelopeOptions, .025, "Pct", "Mult"))[key],
                    IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (HasRoundedPriceChannel(builtIn))
        {
            var channelOptions = builtIn.CreateOptions();
            var channelKeys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "MiddleChannel" }
                : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            for (var slot = 0; slot < channelKeys.Count; slot++)
            {
                var key = channelKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedPriceChannel(bars, Integer(channelOptions, "Length", 21), BoundedMeanKind(channelOptions, 3), Number(channelOptions, .06, "Pct"))[key],
                    IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.LogReturns)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 1);
            yield return IndicatorValidationRule.Reference(0, bars => ReferenceLogReturns(bars, period), LogReturnsBudget);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.MoveTracker)
        {
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedMoveTracker(bars, false), IndicatorErrorBudget.Exact);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(1,
                bars => RoundedMoveTracker(bars, true), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.MoneyFlowIndex)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedMoneyFlow(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.AverageDayRange)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedAverageDayRange(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.CumulativeSum or IndicatorName.CumulativeVolumeIndex)
        {
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedCumulative(bars, builtIn.BatchName == IndicatorName.CumulativeVolumeIndex), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.SimpleReturns)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 1);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedSimpleReturns(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.VolumeZoneOscillator)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return period > 1 ? IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedVolumeZone(bars, period), IndicatorErrorBudget.Exact)
                : IndicatorValidationRule.Reference(0, bars => RoundedVolumeZone(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.NetVolume)
        {
            yield return IndicatorValidationRule.Reference(0, RoundedNetVolume, IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.NormalizedVolume)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 20);
            // One/two finite terms cannot cancel enough to overflow this ratio.
            yield return period >= 3 ? IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedNormalizedVolume(bars, period), IndicatorErrorBudget.Exact)
                : IndicatorValidationRule.Reference(0, bars => RoundedNormalizedVolume(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.Range or IndicatorName.TrueRange)
        {
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedBarRange(bars, builtIn.BatchName == IndicatorName.TrueRange), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.PriceMomentum or IndicatorName.VolumeMomentum)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 10);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedLaggedDifference(bars, period, builtIn.BatchName == IndicatorName.VolumeMomentum), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.MarketFacilitationIndex)
        {
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                RoundedMarketFacilitation, IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasRoundedObv(builtIn))
        {
            var obvOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0, RoundedObv, IndicatorErrorBudget.Exact);
            yield return IndicatorValidationRule.Reference(1,
                bars => RoundedObvSignal(bars, Integer(obvOptions, "Length", 20), BoundedMeanKind(obvOptions, 3)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasRoundedHighLowIndex(builtIn))
        {
            var highLowOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.Reference(0,
                bars => RoundedHighLowIndex(bars, Integer(highLowOptions, "Length", 10), BoundedMeanKind(highLowOptions, 3)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasRoundedBalanceOfPower(builtIn))
        {
            var balanceOptions = builtIn.CreateOptions();
            var period = Integer(balanceOptions, "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                RoundedBalanceOfPowerLine, IndicatorErrorBudget.Exact);
            yield return IndicatorValidationRule.Reference(1,
                bars => RoundedBalanceOfPowerSignal(bars, period, BoundedMeanKind(balanceOptions, 3)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasRoundedMomentum(builtIn))
        {
            var momentumOptions = builtIn.CreateOptions();
            var period = Integer(momentumOptions, "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedMomentum(bars, period), IndicatorErrorBudget.Exact);
            yield return IndicatorValidationRule.Reference(1,
                bars => RoundedMomentumSignal(bars, period, BoundedMeanKind(momentumOptions, 2)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.VolumeRateOfChange)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 12);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedRateOfChange(bars, period, volume: true), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.RateOfChange)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 12);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedRateOfChange(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.WilliamsR)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedWilliams(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.DiNapoliPreferredStochasticOscillator)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 8);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedDiNapoli(bars, period)["Dpso"], IndicatorErrorBudget.Exact);
            yield return IndicatorValidationRule.Reference(1, bars => RoundedDiNapoli(bars, period)["Signal"], IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasBoundedStochastic(builtIn))
        {
            foreach (var rule in StochasticNumericalRules(indicator, builtIn)) yield return rule;
            yield break;
        }
        if (HasBoundedFilteredChande(builtIn))
        {
            var filterOptions = builtIn.CreateOptions();
            var period = Integer(filterOptions, "Length", 9);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedFilteredChande(bars, period), IndicatorErrorBudget.Exact);
            yield return IndicatorValidationRule.Reference(1, bars => RoundedFilteredChandeSignal(bars, period,
                BoundedMeanKind(filterOptions, 2)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasBoundedChande(builtIn))
        {
            var chandeOptions = builtIn.CreateOptions();
            var period = Integer(chandeOptions, "Length", 14);
            var signalOnly = builtIn.BatchOutputKey == "Signal";
            if (!signalOnly)
                yield return IndicatorValidationRule.Reference(0, bars => RoundedChande(bars, period), IndicatorErrorBudget.Exact);
            yield return IndicatorValidationRule.Reference(signalOnly ? 0 : 1, bars => RoundedChandeSignal(bars, period,
                Integer(chandeOptions, "SignalLength", 3), BoundedMeanKind(chandeOptions, 3)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.EhlersHannMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 20);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedHannMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.DoubleExponentialMovingAverage or IndicatorName.TripleExponentialMovingAverage or IndicatorName.ZeroLagExponentialMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            var triple = builtIn.BatchName == IndicatorName.TripleExponentialMovingAverage;
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedExponentialExtrapolation(bars, period, triple), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasRoundedDpo(builtIn))
        {
            var dpoOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedDpo(bars, Integer(dpoOptions, "Length", 20), BoundedMeanKind(dpoOptions, 1)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasRoundedElderRay(builtIn))
        {
            var elderOptions = builtIn.CreateOptions();
            var elderKeys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "BullPower" }
                : new[] { "BullPower", "BearPower" };
            for (var slot = 0; slot < elderKeys.Length; slot++)
            {
                var bull = elderKeys[slot] == "BullPower";
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedElderRay(bars, Integer(elderOptions, "Length", 13), BoundedMeanKind(elderOptions, 3), bull),
                    IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (HasRoundedApo(builtIn))
        {
            var apoOptions = builtIn.CreateOptions();
            var apoFast = apoOptions is PriceOscillatorSpecOptions priceFast ? priceFast.ShortLength : Integer(apoOptions, "FastLength");
            var apoSlow = apoOptions is PriceOscillatorSpecOptions priceSlow ? priceSlow.LongLength : Integer(apoOptions, "SlowLength");
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedApo(bars, apoFast, apoSlow, BoundedMeanKind(apoOptions, 3)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.EhlersHammingMovingAverage)
        {
            var hammingOptions = builtIn.CreateOptions();
            var period = Integer(hammingOptions, "Length", 20);
            var pedestal = Number(hammingOptions, 3, "Pedestal");
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedHammingMean(bars, period, pedestal), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasRoundedBollinger(builtIn))
        {
            var bandOptions = builtIn.CreateOptions();
            var bandKeys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? (builtIn.BatchName == IndicatorName.BollingerBandsWidth ? "BbWidth" : builtIn.BatchName == IndicatorName.BollingerBandsPercentB ? "PctB" : "MiddleBand") }
                : new[] { "UpperBand", "MiddleBand", "LowerBand" };
            for (var slot = 0; slot < bandKeys.Length; slot++)
            {
                var key = bandKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedBollinger(bars, Integer(bandOptions, "Length", 20), BoundedMeanKind(bandOptions, 1), Number(bandOptions, 2, "StdDevMult", "Multiplier"))[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.StandardDeviationChannel)
        {
            var channelOptions = builtIn.CreateOptions();
            var channelKeys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
            for (var slot = 0; slot < channelKeys.Length; slot++)
            {
                var key = channelKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedRegressionChannel(bars, Integer(channelOptions, "Length", 40), Number(channelOptions, 2, "StdDevMult"))[key],
                    IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.MovingAverageDisplacedEnvelope && BoundedMeanKind(builtIn.CreateOptions(), 3) is 2 or 3)
        {
            var displacedOptions = builtIn.CreateOptions();
            var displacedKeys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
            for (var slot = 0; slot < displacedKeys.Length; slot++)
            {
                var key = displacedKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedDisplacedEnvelope(bars, displacedOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.MirroredMovingAverageConvergenceDivergence or IndicatorName.MirroredPercentagePriceOscillator)
        {
            var mirroredOptions = builtIn.CreateOptions();
            var percentage = builtIn.BatchName == IndicatorName.MirroredPercentagePriceOscillator;
            var stem = percentage ? "Ppo" : "Macd";
            var mirroredKeys = new[] { stem, "Signal", "Histogram", "Mirror" + stem, "MirrorSignal", "MirrorHistogram" };
            for (var slot = 0; slot < mirroredKeys.Length; slot++)
            {
                var key = mirroredKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedMirrored(bars, mirroredOptions, percentage)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.LindaRaschke3_10Oscillator)
        {
            var lindaOptions = builtIn.CreateOptions();
            var lindaKeys = new[] { "LindaMacd", "LindaMacdSignal", "LindaMacdHistogram", "LindaPpo", "LindaPpoSignal", "LindaPpoHistogram" };
            for (var slot = 0; slot < lindaKeys.Length; slot++)
            {
                var key = lindaKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedLinda(bars, lindaOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName is IndicatorName._4MovingAverageConvergenceDivergence or IndicatorName._4PercentagePriceOscillator)
        {
            var fourOptions = builtIn.CreateOptions();
            var percentage = builtIn.BatchName == IndicatorName._4PercentagePriceOscillator;
            var stem = percentage ? "Ppo" : "Macd";
            var fourKeys = new[] { stem + "1", "Signal1", "Histogram1", stem + "2", "Signal2", "Histogram2" };
            for (var slot = 0; slot < fourKeys.Length; slot++)
            {
                var key = fourKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedFourOscillator(bars, fourOptions, percentage)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.TrendImpulseFilter)
        {
            var impulseOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedTrendImpulseReference(bars, impulseOptions), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.DetrendedSyntheticPrice)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedSyntheticPriceReference(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.TrendForceHistogram)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedTrendForceReference(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.AwesomeOscillator or IndicatorName.AcceleratorOscillator)
        {
            var awesomeOptions = builtIn.CreateOptions();
            var accelerator = builtIn.BatchName == IndicatorName.AcceleratorOscillator;
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedAwesomeReference(bars, awesomeOptions, accelerator), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.ImpulseMovingAverageConvergenceDivergence or IndicatorName.ImpulsePercentagePriceOscillator)
        {
            var impulseOptions = builtIn.CreateOptions();
            var percentage = builtIn.BatchName == IndicatorName.ImpulsePercentagePriceOscillator;
            var impulseKeys = new[] { percentage ? "Ppo" : "Macd", "Signal", "Histogram" };
            for (var slot = 0; slot < impulseKeys.Length; slot++)
            {
                var key = impulseKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedImpulseReference(bars, impulseOptions, percentage)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.StochasticMovingAverageConvergenceDivergenceOscillator)
        {
            var stochasticOptions = builtIn.CreateOptions();
            var stochasticKeys = new[] { "Macd", "Signal", "Histogram" };
            for (var slot = 0; slot < stochasticKeys.Length; slot++)
            {
                var key = stochasticKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedStochasticMacdReference(bars, stochasticOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.TFSMboIndicator or IndicatorName.TFSMboPercentagePriceOscillator)
        {
            var tfsOptions = builtIn.CreateOptions();
            var percentage = builtIn.BatchName == IndicatorName.TFSMboPercentagePriceOscillator;
            var tfsKeys = new[] { percentage ? "Ppo" : "TfsMob", "Signal", "Histogram" };
            for (var slot = 0; slot < tfsKeys.Length; slot++)
            {
                var key = tfsKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedTfsOscillator(bars, tfsOptions, percentage)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.MovingAverageConvergenceDivergenceLeader or IndicatorName.PercentagePriceOscillatorLeader)
        {
            var leaderOptions = builtIn.CreateOptions();
            var percentage = builtIn.BatchName == IndicatorName.PercentagePriceOscillatorLeader;
            var leaderKeys = percentage ? new[] { "Ppo", "Signal", "Histogram" } : new[] { "Macd", "I1", "I2" };
            for (var slot = 0; slot < leaderKeys.Length; slot++)
            {
                var key = leaderKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedLeaderOscillator(bars, leaderOptions, percentage)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.ReverseMovingAverageConvergenceDivergence)
        {
            var reverseOptions = builtIn.CreateOptions();
            var reverseKeys = new[] { "Rmacd", "Signal", "Histogram" };
            for (var slot = 0; slot < reverseKeys.Length; slot++)
            {
                var key = reverseKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedReverseMacdReference(bars, reverseOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.DiNapoliMovingAverageConvergenceDivergence or IndicatorName.DiNapoliPercentagePriceOscillator)
        {
            var diNapoliOptions = builtIn.CreateOptions();
            var percentage = builtIn.BatchName == IndicatorName.DiNapoliPercentagePriceOscillator;
            var diNapoliKeys = percentage ? new[] { "Ppo", "Signal", "Histogram" } : new[] { "FastS", "SlowS", "Macd", "Signal", "Histogram" };
            for (var slot = 0; slot < diNapoliKeys.Length; slot++)
            {
                var key = diNapoliKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedDiNapoliOscillator(bars, diNapoliOptions, percentage)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.DidiIndex)
        {
            var didiOptions = builtIn.CreateOptions();
            var didiKeys = new[] { "Curta", "Media", "Longa" };
            for (var slot = 0; slot < didiKeys.Length; slot++)
            {
                var key = didiKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedDidi(bars, didiOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.ErgodicMovingAverageConvergenceDivergence or IndicatorName.ErgodicPercentagePriceOscillator)
        {
            var ergodicOptions = builtIn.CreateOptions();
            var ergodicKeys = new[] { builtIn.BatchName == IndicatorName.ErgodicPercentagePriceOscillator ? "Ppo" : "Macd", "Signal", "Histogram" };
            for (var slot = 0; slot < ergodicKeys.Length; slot++)
            {
                var key = ergodicKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedErgodic(bars, ergodicOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.ElliottWaveOscillator)
        {
            var elliottOptions = builtIn.CreateOptions();
            var elliottKeys = new[] { "Ewo", "Signal", "Histogram" };
            for (var slot = 0; slot < elliottKeys.Length; slot++)
            {
                var key = elliottKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedElliott(bars, elliottOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.VolumeMomentumOscillator)
        {
            var volumeMomentumOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedVolumeMomentumOscillator(bars, volumeMomentumOptions), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.VolumeOscillator)
        {
            var volumeOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedVolumeOscillator(bars, volumeOptions), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.NormalizedMacd)
        {
            var normalizedOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedNormalizedMacd(bars, normalizedOptions), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.MovingAverageConvergenceDivergence)
        {
            var macdOptions = builtIn.CreateOptions();
            var macdKeys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "Macd" } : new[] { "Macd", "Signal", "Histogram" };
            for (var slot = 0; slot < macdKeys.Length; slot++)
            {
                var key = macdKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedMacd(bars, macdOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.PercentageVolumeOscillator && BoundedMeanKind(builtIn.CreateOptions(), 3) is 2 or 3)
        {
            var pvoOptions = builtIn.CreateOptions();
            var pvoKeys = new[] { "Pvo", "Signal", "Histogram" };
            for (var slot = 0; slot < pvoKeys.Length; slot++)
            {
                var key = pvoKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedPvo(bars, pvoOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.PercentagePriceOscillator && BoundedMeanKind(builtIn.CreateOptions(), 3) is 2 or 3)
        {
            var ppoOptions = builtIn.CreateOptions();
            var ppoKeys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "Ppo" } : new[] { "Ppo", "Signal", "Histogram" };
            for (var slot = 0; slot < ppoKeys.Length; slot++)
            {
                var key = ppoKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedPpo(bars, ppoOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.DisparityIndex && BoundedMeanKind(builtIn.CreateOptions(), 1) is 1 or 2)
        {
            var disparityOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedDisparity(bars, disparityOptions), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.PerformanceIndex)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedLaggedPercentage(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.PercentageTrailingStops)
        {
            var stopOptions = builtIn.CreateOptions();
            var stopKeys = new[] { "LongStop", "ShortStop" };
            for (var slot = 0; slot < stopKeys.Length; slot++)
            {
                var key = stopKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedPercentageStops(bars, stopOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.NickRypockTrailingReverse)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 2);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedTrailingReverse(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.QmaSmaDifference)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedQmaSmaDifference(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.ChandeForecastOscillator)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0,
                bars => RoundedForecastOscillator(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.MeanAbsoluteErrorBands && BoundedMeanKind(builtIn.CreateOptions(), 1) is 1 or 2)
        {
            var maeOptions = builtIn.CreateOptions();
            var maeKeys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
            for (var slot = 0; slot < maeKeys.Length; slot++)
            {
                var key = maeKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedMaeBands(bars, maeOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.MeanAbsoluteDeviationBands && BoundedMeanKind(builtIn.CreateOptions(), 1) is 1 or 2)
        {
            var madOptions = builtIn.CreateOptions();
            var madKeys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
            for (var slot = 0; slot < madKeys.Length; slot++)
            {
                var key = madKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedMadBands(bars, madOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.InterquartileRangeBands)
        {
            var quartileOptions = builtIn.CreateOptions();
            var quartileKeys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
            for (var slot = 0; slot < quartileKeys.Length; slot++)
            {
                var key = quartileKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedQuartileBands(bars, Integer(quartileOptions, "Length", 14), Number(quartileOptions, 1.5, "Mult"))[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.RangeBands && BoundedMeanKind(builtIn.CreateOptions(), 1) is 1 or 2)
        {
            var rangeOptions = builtIn.CreateOptions();
            var rangeKeys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
            for (var slot = 0; slot < rangeKeys.Length; slot++)
            {
                var key = rangeKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedRangeBands(bars, rangeOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.MidpointOscillator && BoundedMeanKind(builtIn.CreateOptions(), 3) is 2 or 3)
        {
            var midpointOptions = builtIn.CreateOptions();
            var midpointKeys = new[] { "Mo", "Signal" };
            for (var slot = 0; slot < midpointKeys.Length; slot++)
            {
                var key = midpointKeys[slot];
                yield return IndicatorValidationRule.Reference(slot,
                    bars => RoundedMidpoint(bars, Integer(midpointOptions, "Length", 26), BoundedMeanKind(midpointOptions, 3))[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.GuppyDistanceIndicator && BoundedMeanKind(builtIn.CreateOptions(), 3) is 2 or 3)
        {
            var distanceOptions = builtIn.CreateOptions();
            var distanceKeys = new[] { "FastDistance", "SlowDistance" };
            for (var slot = 0; slot < distanceKeys.Length; slot++)
            {
                var key = distanceKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedGuppyDistance(bars, distanceOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.GuppyMultipleMovingAverage && BoundedMeanKind(builtIn.CreateOptions(), 3) is 2 or 3)
        {
            var guppyOptions = builtIn.CreateOptions();
            var guppyKeys = new[] { "SuperGmmaOsc", "SuperGmmaSignal" };
            for (var slot = 0; slot < guppyKeys.Length; slot++)
            {
                var key = guppyKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedGuppy(bars, guppyOptions)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.TypicalPriceVolatility)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedTypicalVolatility(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.DownsideDeviation)
        {
            var downsideOptions = builtIn.CreateOptions();
            var period = Integer(downsideOptions, "Length", 20);
            var target = Number(downsideOptions, 0, "TargetReturn");
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0, bars => RoundedDownside(bars, period, target), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.CoefficientOfVariation)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 20);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0, bars => RoundedCoefficient(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.Skewness)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedSkewness(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.RSquared)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedRSquared(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.LinearRegression)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            var regressionKeys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "LinearRegression" }
                : new[] { "LinearRegression", "PredictedTomorrow", "Slope", "Intercept" };
            for (var slot = 0; slot < regressionKeys.Length; slot++)
            {
                var key = regressionKeys[slot];
                yield return IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
                    bars => RoundedLinearRegression(bars, period)[key], IndicatorErrorBudget.Exact);
            }
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.StandardError or IndicatorName.StandardErrorOfTheMean)
        {
            var regression = builtIn.BatchName == IndicatorName.StandardError;
            var period = Integer(builtIn.CreateOptions(), "Length", regression ? 14 : 20);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedStandardError(bars, period, regression), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.Variance)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 20);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0, bars => RoundedPopulationVariance(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.SimplifiedLeastSquaresMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0, bars => RoundedSimplifiedLeastSquares(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.LeastSquaresMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 25);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0, bars => RoundedLeastSquaresMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.LeoMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.ReferenceWithOverflowRejection(0, bars => RoundedLeoMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.ArnaudLegouxMovingAverage)
        {
            var almaOptions = builtIn.CreateOptions();
            var period = Integer(almaOptions, "Length", 9);
            var offset = Number(almaOptions, .85, "Offset");
            var sigma = Number(almaOptions, 6, "Sigma");
            yield return IndicatorValidationRule.Reference(0, bars => RoundedAlmaMean(bars, period, offset, sigma), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.SineWeightedMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedSineMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.NaturalMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 40);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedNaturalMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.QuickMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedQuickMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.InverseDistanceWeightedMovingAverage or IndicatorName.DistanceWeightedMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedDistanceMassMean(bars, period, builtIn.BatchName == IndicatorName.DistanceWeightedMovingAverage), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.FareySequenceWeightedMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 5);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedFareyMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.FibonacciWeightedMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedFibonacciMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.SquareRootWeightedMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedSquareRootMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.WellesWilderMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedWilderTrajectory(Closes(bars), period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName is IndicatorName.SymmetricallyWeightedMovingAverage or IndicatorName.EhlersTriangleMovingAverage or IndicatorName.JsaMovingAverage)
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0,
                bars => builtIn.BatchName == IndicatorName.JsaMovingAverage ? RoundedJsaMean(bars, period) : RoundedSymmetricMean(bars, period),
                IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasBoundedSequentialMean(builtIn))
        {
            var sequentialOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.Reference(0, bars => RoundedSequentialMean(bars,
                Integer(sequentialOptions, "Length", 50), BoundedMeanKind(sequentialOptions, 1)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasBoundedMiddleMean(builtIn))
        {
            var middleOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.Reference(0, bars => RoundedMiddleMean(bars,
                Integer(middleOptions, "Length1", 14), Integer(middleOptions, "Length2", 10), BoundedMeanKind(middleOptions, 3)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasBoundedSlowMean(builtIn))
        {
            var slowOptions = builtIn.CreateOptions();
            yield return IndicatorValidationRule.Reference(0,
                bars => RoundedSlowMean(bars, Integer(slowOptions, "Length", 15), BoundedMeanKind(slowOptions, 2)), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasBoundedTriangularMean(builtIn))
        {
            var triangularOptions = builtIn.CreateOptions();
            var triangularKind = BoundedMeanKind(triangularOptions, 1);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedTriangularMean(bars, Integer(triangularOptions, "Length", 20), triangularKind),
                IndicatorErrorBudget.Exact);
            yield break;
        }
        if (HasSimpleVolumeMean(builtIn))
        {
            var period = Integer(builtIn.CreateOptions(), "Length", 14);
            yield return IndicatorValidationRule.Reference(0, bars => RoundedRollingVolumeMean(bars, period), IndicatorErrorBudget.Exact);
            yield break;
        }
        if (builtIn.BatchName == IndicatorName.OvershootReductionMovingAverage)
        {
            var trajectory = OvershootTrajectory(builtIn);
            if (trajectory is not null) yield return trajectory;
            yield break;
        }
        var foundation = MesaPredictionFormula(builtIn) ?? MobilityFormula(builtIn) ?? GuppyCountBackFormula(builtIn) ?? TechnicalRatingsFormula(builtIn) ?? FourierHarmonicsFormula(builtIn) ?? UltimateMomentumFormula(builtIn) ?? DiscreteFourierFormula(builtIn) ?? DominantCycleFormula(builtIn) ?? TunedBypassFormula(builtIn) ?? SelfAdjustingLaguerreFormula(builtIn) ?? SineWaveFormula(builtIn) ?? OptimizedTrendFormula(builtIn) ?? KaufmanRegressionFormula(builtIn) ?? ZigZagFormula(builtIn) ?? QuadraticFit(builtIn) ?? RemainingTrends(builtIn) ?? ConfluenceFormula(builtIn) ?? CandleTrends(builtIn) ?? VariableAverages(builtIn) ?? CycleNoise(builtIn) ?? AdaptiveV1(builtIn) ?? MamaFormulas(builtIn) ?? PredictiveFilters(builtIn) ?? SwissArmy(builtIn) ?? AdaptiveCyberFormulas(builtIn) ?? RelativeMotion(builtIn) ?? DemarkPatterns(builtIn) ?? AdaptiveEhlersV2(builtIn) ?? Autocorrelation(builtIn) ?? PriceCoordinates(builtIn) ?? EhlersCorrelations(builtIn) ?? EventAndDistance(builtIn) ?? HilbertFormulas(builtIn) ?? SteppedChannels(builtIn) ?? NaturalMarkets(builtIn) ?? RiskRatios(builtIn) ?? RangeMomentum(builtIn) ?? TrendMass(builtIn) ?? WindowFormula(builtIn) ?? PoleFilters(builtIn) ?? EhlersLinear(builtIn) ?? Filters(builtIn) ?? AdaptiveAverages(builtIn) ?? Statistics(builtIn) ?? Oscillators(builtIn)
            ?? DiscoveredOutputs(builtIn) ?? CompositeMomentum(builtIn) ?? MacdComposites(builtIn) ?? MomentumWindows(builtIn) ?? TrendCounts(builtIn) ?? OscillatorSignals(builtIn) ?? MarketStructure(builtIn) ?? Channels(builtIn) ?? Pivots(builtIn) ?? Volatility(builtIn) ?? VolumeAndReturns(builtIn) ?? Foundation(builtIn);
        if (foundation is not null)
        {
            // All output rules for a fixture share one independent reference evaluation.
            var fixtures = new System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyList<Bar>, IReadOnlyDictionary<string, double[]>>();
            var outputKeys = indicator.Outputs.Count == 1
                ? new[] { builtIn.BatchOutputKey ?? foundation.Primary }
                : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            for (var slot = 0; slot < indicator.Outputs.Count; slot++)
            {
                var key = outputKeys[slot];
                if (foundation.Keys.Contains(key))
                    yield return builtIn.BatchName is IndicatorName.StandardDeviation or IndicatorName.Trimean or IndicatorName.MedianValue
                        or IndicatorName.AroonUp or IndicatorName.AroonDown or IndicatorName.AroonOscillator
                        or IndicatorName.PsychologicalLine or IndicatorName.ChandeTrendScore
                        or IndicatorName.VolumeWeightedAveragePrice or IndicatorName.WindowedVolumeWeightedMovingAverage or IndicatorName.DonchianChannels or IndicatorName.RangeIdentifier or IndicatorName.WilliamsFractals or IndicatorName.GannSwingOscillator or IndicatorName.GannTrendOscillator or IndicatorName.TFSTetherLineIndicator or IndicatorName.TTMScalperIndicator
                        or IndicatorName.GeometricMeanMovingAverage or IndicatorName.GeometricMovingAverage or IndicatorName.QuadraticMovingAverage or IndicatorName.KaufmanAdaptiveMovingAverage or IndicatorName.Midpoint or IndicatorName.Midprice or IndicatorName.IchimokuCloud or IndicatorName.IchimokuChikouSpan
                        or IndicatorName.HighestHigh or IndicatorName.LowestLow or IndicatorName.RollingMax or IndicatorName.RollingMin or IndicatorName.PercentRank
                        ? IndicatorValidationRule.Reference(slot, bars => fixtures.GetValue(bars, b => foundation.Compute(b))[key],
                            builtIn.BatchName is IndicatorName.PsychologicalLine or IndicatorName.ChandeTrendScore
                                or IndicatorName.VolumeWeightedAveragePrice or IndicatorName.WindowedVolumeWeightedMovingAverage or IndicatorName.DonchianChannels or IndicatorName.RangeIdentifier or IndicatorName.WilliamsFractals or IndicatorName.GannSwingOscillator or IndicatorName.GannTrendOscillator or IndicatorName.TFSTetherLineIndicator or IndicatorName.TTMScalperIndicator
                        or IndicatorName.GeometricMeanMovingAverage or IndicatorName.GeometricMovingAverage or IndicatorName.QuadraticMovingAverage or IndicatorName.KaufmanAdaptiveMovingAverage or IndicatorName.Midpoint or IndicatorName.Midprice or IndicatorName.IchimokuCloud or IndicatorName.IchimokuChikouSpan or IndicatorName.MedianValue or IndicatorName.HighestHigh or IndicatorName.LowestLow or IndicatorName.RollingMax or IndicatorName.RollingMin or IndicatorName.PercentRank
                                || builtIn.BatchName == IndicatorName.Trimean && key != "Trimean"
                                ? IndicatorErrorBudget.Exact : new IndicatorErrorBudget(0, 1e-9, requireSameSign: true))
                        : FullReference(slot, bars => fixtures.GetValue(bars, b => foundation.Compute(b))[key]);
            }
            yield break;
        }
        var name = builtIn.BatchName;
        Func<IReadOnlyList<Bar>, IReadOnlyList<double>>? price = name switch
        {
            IndicatorName.FullTypicalPrice => bars => bars.Select(b => ExactPriceMean(b.Open, b.High, b.Low, b.Close)).ToArray(),
            IndicatorName.TypicalPrice => bars => bars.Select(b => ExactPriceMean(b.High, b.Low, b.Close)).ToArray(),
            IndicatorName.MedianPrice => bars => bars.Select(b => ExactPriceMean(b.High, b.Low)).ToArray(),
            IndicatorName.AveragePrice => bars => bars.Select(b => ExactPriceMean(b.Open, b.Close)).ToArray(),
            IndicatorName.WeightedClose => bars => bars.Select(b => ExactPriceMean(b.High, b.Low, b.Close, b.Close)).ToArray(),
            IndicatorName.Range => bars => bars.Select(b => b.High - b.Low).ToArray(),
            _ => null
        };
        if (price is not null)
        {
            yield return name == IndicatorName.Range ? FullReference(0, price)
                : IndicatorValidationRule.Reference(0, price, new IndicatorErrorBudget(0, 1e-9, requireSameSign: true));
            yield break;
        }

        var average = name switch
        {
            IndicatorName.SimpleMovingAverage => 1,
            IndicatorName.WeightedMovingAverage => 2,
            IndicatorName.ExponentialMovingAverage => 3,
            IndicatorName.DoubleExponentialMovingAverage => 4,
            IndicatorName.TripleExponentialMovingAverage => 5,
            _ => 0
        };
        if (average != 0)
        {
            var length = Integer(builtIn.CreateOptions(), "Length");
            // EMA uses its standard coefficient at every positive period.
            if (average >= 3 && !StandardEmaPeriod(length)) yield break;
            if (average == 1)
                yield return IndicatorValidationRule.Reference(0, bars => ExactSma(bars, length),
                    new IndicatorErrorBudget(0, 1e-9, requireSameSign: true));
            else if (average == 3)
                yield return IndicatorValidationRule.Reference(0, bars => RoundedEma(bars, length),
                    IndicatorErrorBudget.Exact);
            else
                yield return FullReference(0,
                    bars => Average(bars.Select(b => b.Close).ToArray(), length, average));
            yield break;
        }

        if (name != IndicatorName.MovingAverageConvergenceDivergence && name != IndicatorName.PercentagePriceOscillator)
            yield break;
        var options = builtIn.CreateOptions();
        var fast = options is PriceOscillatorPercentSpecOptions percent ? percent.ShortLength : Integer(options, "FastLength");
        var slow = options is PriceOscillatorPercentSpecOptions percentSlow ? percentSlow.LongLength : Integer(options, "SlowLength");
        var signal = Integer(options, "SignalLength", 9);
        var kind = AverageKind(options, 3);
        if (kind == 0) yield break;
        if (kind >= 3 && (!StandardEmaPeriod(fast) || !StandardEmaPeriod(slow) || !StandardEmaPeriod(signal))) yield break;
        var keys = indicator.Outputs.Count == 1
            ? new[] { builtIn.BatchOutputKey ?? (name == IndicatorName.PercentagePriceOscillator ? "Ppo" : "Macd") }
            : GeneratedIndicatorOutputs.KeysFor(name);
        for (var slot = 0; slot < indicator.Outputs.Count; slot++)
        {
            var key = keys[slot];
            if (key != "Macd" && key != "Ppo" && key != "Signal" && key != "Histogram") continue;
            yield return FullReference(slot, bars =>
            {
                var closes = bars.Select(b => b.Close).ToArray();
                var fastAverage = Average(closes, fast, kind);
                var slowAverage = Average(closes, slow, kind);
                var line = closes.Select((_, i) => name == IndicatorName.PercentagePriceOscillator
                    ? slowAverage[i] == 0 ? 0 : 100 * (fastAverage[i] / slowAverage[i] - 1)
                    : fastAverage[i] - slowAverage[i]).ToArray();
                if (key == "Macd" || key == "Ppo") return line;
                var smoothed = Average(line, signal, kind);
                return key == "Signal" ? smoothed : line.Select((v, i) => v - smoothed[i]).ToArray();
            });
        }
    }

    private static bool StandardEmaPeriod(int length) => length >= 1;

    private static int Integer(object options, string name, int? fallback = null) =>
        options.GetType().GetProperty(name)?.GetValue(options) is int value ? value
        : fallback ?? throw new InvalidOperationException("Formula options missing " + name);

    private static double[] ExactWeightedWindow(IReadOnlyList<Bar> bars, int length)
    {
        var result = new double[bars.Count];
        var denominator = new ReferenceFraction((long)length * (length + 1L) / 2);
        for (var i = 0; i < result.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                sum += ReferenceFraction.FromDouble(bars[j].Close) * new ReferenceFraction(length - i + j);
            result[i] = (sum / denominator).ToDouble();
        }
        return result;
    }

    // Independent trajectory: rational correction form, quantized after each complete update.
    // Earlier predicted values come from this reference, never observed production outputs.
    private static double[] RoundedEma(IReadOnlyList<Bar> bars, int length)
        => RoundedEma(bars.Select(bar => bar.Close).ToArray(), length);

    private static double[] RoundedEma(IReadOnlyList<double> input, int length)
    {
        var values = input.Select(ReferenceFraction.FromDouble).ToArray();
        var result = new double[values.Length];
        var rate = new ReferenceFraction(2) / new ReferenceFraction((long)length + 1);
        var previous = new ReferenceFraction(0);
        var prefix = new ReferenceFraction(0);
        for (var i = 0; i < values.Length; i++)
        {
            ReferenceFraction next;
            if (i < length)
            {
                prefix += values[i];
                next = prefix / new ReferenceFraction(i + 1);
            }
            else next = previous + rate * (values[i] - previous);
            result[i] = next.ToDouble();
            previous = ReferenceFraction.FromDouble(result[i]);
        }
        return result;
    }

    private static double[] ExactSma(IReadOnlyList<Bar> bars, int length)
    {
        var input = bars.Select(bar => ReferenceFraction.FromDouble(bar.Close)).ToArray();
        var result = new double[input.Length];
        for (var i = length - 1; i < input.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var j = i - length + 1; j <= i; j++) sum += input[j];
            result[i] = (sum / new ReferenceFraction(length)).ToDouble();
        }
        return result;
    }

    internal static double[] Average(IReadOnlyList<double> values, int length, int kind)
    {
        // Bounded means of finite inputs have representable results even when their
        // sums or corrections overflow binary64. Round each independent exact stage.
        // Preserve the existing IEEE propagation for nonfinite upstream stages.
        if ((kind is 1 or 2 or 3 or 6) && values.All(value => !double.IsNaN(value) && !double.IsInfinity(value)))
            return RoundedBoundedStage(values, length, kind);

        if (kind == 4 || kind == 5)
        {
            var first = Average(values, length, 3);
            var second = Average(first, length, 3);
            if (kind == 4) return first.Select((v, i) => 2 * v - second[i]).ToArray();
            var third = Average(second, length, 3);
            return first.Select((v, i) => 3 * (v - second[i]) + third[i]).ToArray();
        }
        var result = new double[values.Count];
        for (var i = 0; i < result.Length; i++)
        {
            if (kind == 6)
            {
                var previous = i == 0 ? 0 : result[i - 1];
                result[i] = previous + (values[i] - previous) / length;
                continue;
            }
            if (kind == 3 && i >= length)
            {
                result[i] = result[i - 1] + 2d / (length + 1d) * (values[i] - result[i - 1]);
                continue;
            }
            if (kind == 1 && i + 1 < length) continue;
            double sum = 0;
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                sum += values[j] * (kind == 2 ? length - i + j : 1);
            result[i] = sum / (kind == 2 ? length * (length + 1d) / 2
                : kind == 3 ? i + 1 : length);
        }
        return result;
    }
}
