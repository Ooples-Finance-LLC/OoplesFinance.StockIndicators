using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Compares every series the builder publishes against the same key from the indicator's v1 batch.
/// </summary>
/// <remarks>
/// <para>
/// The rest of the suite checks invariants - a constant series in, that constant out - and route parity,
/// which compares the fast path against the stateful twin and the batch. None of that compares a published
/// series against v1 KEY BY KEY, so three routes agreeing on the wrong number stayed green. That is how a
/// defect where every named output answered with the indicator's primary series survived: the primary was
/// usually right, so every route agreed, and nothing ever asked what UpperBand or Signal actually held.
/// </para>
/// <para>
/// The known divergences below are an INVERTED allow-list. A divergence that is not listed fails the test,
/// so a new one cannot be introduced; and a listed entry that now agrees ALSO fails it, so the list cannot
/// go stale and has to shrink as each is fixed. Never add an entry to silence a new failure - the entry is
/// a record of a defect that was already there when the sweep was first committed.
/// </para>
/// </remarks>
public sealed class PublishedOutputParityTests
{
    // Every entry is a series the builder publishes that does not match its v1 batch. Delete an entry when
    // the indicator is fixed; the test tells you to.
    private static readonly HashSet<string> KnownDivergences = new(StringComparer.Ordinal)
    {
        "AccumulativeSwingIndex.Signal",
        "AdaptiveErgodicCandlestickOscillator.Signal",
        "Adl.AdlSignal",
        "AnchoredMomentum.Signal",
        "BalanceOfPower.BopSignal",
        "BearPower.Signal",
        "BearPowerIndicator.Signal",
        "BilateralStochasticOscillator.Bear",
        "BilateralStochasticOscillator.Signal",
        "BullPower.Signal",
        "BullPowerIndicator.Signal",
        "ChandeCompositeMomentumIndex.Signal",
        "ChandeMomentumOscillator.Signal",
        "ChandeMomentumOscillatorFilter.Signal",
        "Cmo.Signal",
        "CommoditySelectionIndex.Signal",
        "ConditionalAccumulator.Signal",
        "ConstanceBrownCompositeIndex.FastSignal",
        "ConstanceBrownCompositeIndex.SlowSignal",
        "DecisionPointBreadthSwenlinTradingOscillator.Signal",
        "DeltaMovingAverage.Histogram",
        "DeltaMovingAverage.Signal",
        "DiNapoliPreferredStochasticOscillator.Signal",
        "DonchianChannelWidth.Signal",
        "DoubleSmoothedMomenta.Signal",
        "DoubleSmoothedStochastic.Signal",
        "DoubleStochasticOscillator.Signal",
        "DTOscillator.Signal",
        "EhlersAdaptiveCommodityChannelIndexV2.Signal",
        "EhlersAdaptiveRelativeStrengthIndexV2.Signal",
        "EhlersAdaptiveStochasticIndicatorV2.Signal",
        "EhlersAMDetector.Signal",
        "EhlersBandPassFilterV1.Signal",
        "EhlersHammingWindowIndicator.Roc",
        "EhlersHannWindowIndicator.Roc",
        "EhlersInstantaneousTrendlineV2.Signal",
        "EhlersPhaseCalculation.Signal",
        "EhlersRelativeVigorIndex.Signal",
        "EhlersRestoringPullIndicator.Rpi",
        "EhlersRestoringPullIndicator.Signal",
        "EhlersSimpleClipIndicator.Signal",
        "EhlersSimpleDerivIndicator.Signal",
        "EhlersSimpleWindowIndicator.Roc",
        "EhlersStochasticCyberCycle.Signal",
        "EhlersSuperPassbandFilter.LowerBand",
        "EhlersSuperPassbandFilter.UpperBand",
        "EhlersTrendExtraction.Bp",
        "EhlersTriangleWindowIndicator.Roc",
        "EhlersTripleDelayLineDetrender.Signal",
        "EhlersUniversalOscillator.Signal",
        "EhlersUniversalTradingFilter.LowerBand",
        "EhlersUniversalTradingFilter.UpperBand",
        "ElderMarketThermometer.Signal",
        "ElliottWaveOscillator.Histogram",
        "ElliottWaveOscillator.Signal",
        "EnhancedIndex.Signal",
        "EnhancedWilliamsR.Signal",
        "ErgodicCandlestickOscillator.Signal",
        "ErgodicCommoditySelectionIndex.Signal",
        "ErgodicMeanDeviationIndicator.Signal",
        "ErgodicPercentagePriceOscillator.Histogram",
        "ErgodicPercentagePriceOscillator.Signal",
        "ErgodicTrueStrengthIndexV1.Signal",
        "FastandSlowKurtosisOscillator.Signal",
        "FastSlowKurtosisOscillator.Signal",
        "FearAndGreedIndicator.Signal",
        "FireflyOscillator.Signal",
        "FoldedRelativeStrengthIndex.Signal",
        "ForecastOscillator.Signal",
        "FreedomOfMovement.Dpl",
        "GainLossMovingAverage.Signal",
        "GarmanKlassVolatility.Signal",
        "GopalakrishnanRangeIndex.Signal",
        "ImpulsePercentagePriceOscillator.Histogram",
        "ImpulsePercentagePriceOscillator.Signal",
        "InternalBarStrengthIndicator.Signal",
        "JrcFractalDimension.Signal",
        "KasePeakOscillatorV1.Pk",
        "KlingerVolumeOscillator.KvoHistogram",
        "KlingerVolumeOscillator.KvoSignal",
        "KnowSureThing.Signal",
        "Kst.Signal",
        "Kvo.KvoHistogram",
        "Kvo.KvoSignal",
        "MacdLine.Histogram",
        "MacdLine.Signal",
        "MacZIndicator.Histogram",
        "MacZIndicator.Signal",
        "MarketMeannessIndex.MmiSmoothed",
        "MassIndex.Signal",
        "MassIndexCore.Signal",
        "MassThrust.Signal",
        "MassThrustIndicator.Signal",
        "MassThrustOscillator.Signal",
        "MidpointOscillator.Signal",
        "ModifiedPriceVolumeTrend.Signal",
        "Momentum.Signal",
        "MultiVoteOnBalanceVolume.Signal",
        "NegativeVolumeDisparityIndicator.Signal",
        "NegativeVolumeIndex.NviSignal",
        "Nvi.NviSignal",
        "Obv.ObvSignal",
        "OceanIndicator.Signal",
        "OnBalanceVolume.ObvSignal",
        "OnBalanceVolumeModified.Signal",
        "OnBalanceVolumeReflex.Signal",
        "PeakValleyEstimation.Sign2",
        "PeakValleyEstimation.Sign3",
        "PercentagePriceOscillator.Histogram",
        "PercentagePriceOscillator.Signal",
        "PercentagePriceOscillatorLeader.Histogram",
        "PercentagePriceOscillatorLeader.Signal",
        "PercentageVolumeOscillator.Histogram",
        "PercentageVolumeOscillator.Signal",
        "PercentChangeOscillator.Signal",
        "PhaseChangeIndex.Signal",
        "Pmo.Signal",
        "PositiveVolumeIndex.PviSignal",
        "Ppo.Histogram",
        "Ppo.Signal",
        "PriceChange.Signal",
        "PriceMomentumOscillator.Signal",
        "PriceOscillatorPercent.Histogram",
        "PriceOscillatorPercent.Signal",
        "PricePosition.FastD",
        "PricePosition.SlowD",
        "PriceVolumeRank.FastSignal",
        "PriceVolumeRank.SlowSignal",
        "PriceVolumeTrend.Signal",
        "ProjectionBandwidth.Signal",
        "ProjectionOscillator.Signal",
        "Pvi.PviSignal",
        "Pvo.Histogram",
        "Pvo.Signal",
        "Pvt.Signal",
        "QuasiWhiteNoise.WhiteNoiseMa",
        "QuasiWhiteNoise.WhiteNoiseStdDev",
        "QuasiWhiteNoise.WhiteNoiseVariance",
        "RainbowOscillator.LowerBand",
        "RainbowOscillator.UpperBand",
        "RapidRelativeStrengthIndex.Signal",
        "ReallySimpleIndicator.Signal",
        "RelativeMomentumIndex.Histogram",
        "RelativeMomentumIndex.Signal",
        "RelativeVigorIndex.Signal",
        "RelativeVolumeIndicator.Dpl",
        "Repulse.Signal",
        "ReverseMovingAverageConvergenceDivergence.Histogram",
        "ReverseMovingAverageConvergenceDivergence.Signal",
        "RexOscillator.Signal",
        "Rsi.Histogram",
        "Rsi.Signal",
        "RSINGIndicator.Signal",
        "Rvi.Signal",
        "SellGravitationIndex.Signal",
        "SigmaSpikes.Signal",
        "SMIErgodicIndicator.Signal",
        "SmoothedWilliamsAccumulationDistribution.Signal",
        "StatisticalVolatility.Signal",
        "StochasticConnorsRelativeStrengthIndex.Signal",
        "StochasticCustomOscillator.Signal",
        "StochasticFastOscillator.Signal",
        "StochasticK.FastD",
        "StochasticK.SlowD",
        "StochasticMacdOscillator.Histogram",
        "StochasticMacdOscillator.Signal",
        "StochasticMomentumIndex.Signal",
        "StochasticOscillator.FastD",
        "StochasticOscillator.SlowD",
        "StochasticRegular.Signal",
        "StochasticRsiOscillator.Signal",
        "TFSMboIndicator.Histogram",
        "TFSMboIndicator.Signal",
        "TotalPowerIndicator.BearCount",
        "TotalPowerIndicator.BullCount",
        "TraderPressureIndex.Bears",
        "TraderPressureIndex.Bulls",
        "TradeVolumeIndex.Signal",
        "TrendAnalysisIndex.Signal",
        "TrendAnalysisIndicator.Signal",
        "TrendDetectionIndex.TdiDirection",
        "TrendExhaustionIndicator.Signal",
        "TrueStrengthIndex.Signal",
        "Tsi.Signal",
        "TurboStochasticsFast.Signal",
        "TurboStochasticsSlow.Signal",
        "UltimateTraderOscillator.Signal",
        "VerticalHorizontalFilter.Signal",
        "Vhf.Signal",
        "VolatilityBasedMomentum.Signal",
        "VolatilityQualityIndex.FastSignal",
        "VolatilityQualityIndex.SlowSignal",
        "VolumePriceTrend.Signal",
        "Vpci.Signal",
        "WaveTrendOscillator.Signal",
    };

    [Fact]
    public void EveryPublishedSeriesMatchesItsBatchExceptTheKnownDivergences()
    {
        var bars = Walk(150);

        StockData Batch() => new(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList());

        var calculations = typeof(StockData).Assembly.GetTypes()
            .Where(t => t.IsAbstract && t.IsSealed && t.Name == "Calculations")
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.Name.StartsWith("Calculate", StringComparison.Ordinal))
            .GroupBy(m => m.Name)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        static bool Same(double[] mine, List<double> theirs) =>
            mine.Length == theirs.Count && !mine.Where((x, i) => Math.Abs(x - theirs[i]) > 1e-8).Any();

        var diverged = new HashSet<string>(StringComparer.Ordinal);
        var swallowed = new List<string>();
        var compared = 0;

        foreach (var type in typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .Where(typeof(MultiOutputIndicatorBase).IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var constructor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().All(p => p.IsOptional));
            if (constructor is null) { continue; }

            IIndicator indicator;
            IBuiltInIndicator builtIn;
            try
            {
                indicator = (IIndicator)constructor.Invoke(constructor.GetParameters().Select(p => p.DefaultValue).ToArray());
                if (indicator is not IBuiltInIndicator built) { continue; }
                builtIn = built;
            }
            catch (TargetInvocationException ex)
            {
                swallowed.Add(type.Name + ": " + (ex.InnerException ?? ex).GetType().Name);
                continue;
            }

            if (!calculations.TryGetValue("Calculate" + builtIn.BatchName, out var method)) { continue; }

            Dictionary<string, List<double>> published;
            try
            {
                var arguments = method.GetParameters()
                    .Select((p, i) => i == 0 ? (object?)Batch() : Type.Missing).ToArray();
                if (method.Invoke(null, arguments) is not StockData result) { continue; }
                published = result.OutputValues;
            }
            catch (TargetInvocationException ex)
            {
                swallowed.Add(type.Name + ": " + (ex.InnerException ?? ex).GetType().Name);
                continue;
            }

            double[][] mine;
            try
            {
                using var run = new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator)
                    .BuildAsync().GetAwaiter().GetResult();
                mine = indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
            }
            catch (Exception ex)
            {
                swallowed.Add(type.Name + ": " + ex.GetType().Name);
                continue;
            }

            if (mine.Length < 2) { continue; }

            // Outputs are in the order the batch publishes its keys, so the two line up by position.
            var keys = published.Keys.ToList();
            for (var slot = 0; slot < mine.Length && slot < keys.Count; slot++)
            {
                compared++;
                if (!Same(mine[slot], published[keys[slot]]))
                {
                    diverged.Add(type.Name + "." + keys[slot]);
                }
            }
        }

        // A positive control: the sweep has to be reaching enough series for its verdict to mean anything.
        compared.Should().BeGreaterThan(500, "the sweep must actually compare series for its result to mean anything");
        // An indicator whose construction or batch call THROWS was dropped before it could be compared,
        // and a count of what was compared cannot show that. Measured at 0 today; the ceiling is a ratchet.
        swallowed.Should().HaveCountLessThanOrEqualTo(0,
            "an indicator that throws is never compared: " + string.Join(", ", swallowed));

        var appeared = diverged.Except(KnownDivergences).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var fixedSince = KnownDivergences.Except(diverged).OrderBy(x => x, StringComparer.Ordinal).ToList();

        appeared.Should().BeEmpty(
            "these series stopped matching their v1 batch - a published output must equal the key it is named for");

        fixedSince.Should().BeEmpty(
            "these series now match their batch, so delete them from KnownDivergences - the list has to shrink");
    }

    private static List<Bar> Walk(int count)
    {
        var random = new Random(31);
        var bars = new List<Bar>(count);
        var last = 100d;
        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc).AddMinutes(i),
                open, Math.Max(open, close) + random.NextDouble(),
                Math.Max(0.01, Math.Min(open, close) - random.NextDouble()), close, random.Next(1000, 400000)));
            last = close;
        }

        return bars;
    }
}