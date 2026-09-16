using OoplesFinance.StockIndicators.Builder;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Properties that hold for every indicator regardless of what it computes, checked against all of
/// them at once.
/// </summary>
/// <remarks>
/// <para>
/// These are not comparisons against a reference implementation. Each one is a statement that is true
/// of any correct indicator by construction, so a failure is a defect rather than a disagreement -
/// there is nothing to argue about when a market that never moved produces a moving indicator.
/// </para>
/// <para>
/// The set to check comes from <see cref="IndicatorInvoker.GetSupportedIndicators"/> rather than a
/// list kept here, so an indicator added tomorrow is covered without anyone remembering to add it.
/// </para>
/// <para>
/// Where an indicator is known to violate one of these, it is named in the corresponding set below
/// with the reason, so the count of outstanding defects is visible rather than hidden behind a
/// weakened assertion.
/// </para>
/// </remarks>
public sealed class IndicatorInvariantTests
{
    private const int FlatBars = 1000;
    private const int SettledFrom = 900;

    /// <summary>
    /// Indicators that need a second price series and cannot be invoked with one symbol's bars.
    /// </summary>
    private static readonly HashSet<IndicatorName> NeedsMarketData = new()
    {
        IndicatorName.ComparePriceMomentumOscillator,
        IndicatorName.KaufmanStressIndicator,
        IndicatorName.RSMKIndicator,
        IndicatorName.RelativeNormalizedVolatility,
        IndicatorName.RelativeStrength3DIndicator,
        IndicatorName.SectorRotationModel
    };

    /// <summary>
    /// Indicators that still move on a market that never moved, and do not settle however long it runs.
    /// Each is a defect; see issue #178.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured at 900 bars of 1000 and again at 4900 of 5000. These either hold their spread exactly,
    /// grow, or shrink so slowly that no run settles them - so the movement is not convergence.
    /// </para>
    /// <para>
    /// Three more were found by this test and fixed rather than listed: PercentChangeOscillator divided
    /// by <c>prevValue - 1</c> instead of subtracting one from the ratio, ConditionalAccumulator counted
    /// a gap on every bar because it compared with <c>&gt;=</c>, and IIRLeastSquaresEstimate stored the
    /// previous smoothed value instead of the one it had just computed. IIRLeastSquaresEstimate is in
    /// the convergence set below, not here: the fix was real, and what remains settles by bar 4900.
    /// </para>
    /// <para>
    /// EhlersSpectrumDerivedFilterBank and EhlersRestoringPullIndicator have since moved to that same
    /// convergence set. Every period in the bank now runs its own two-sample recursion rather than
    /// reading one shared per-bar list, and the attenuation the bank takes a logarithm of is held at the
    /// 0.01 floor it can never mathematically fall below - without which an amplitude decayed into the
    /// denormal range made the ratio round to exactly one and put an infinity into both sums. Both
    /// settle to a constant now, they simply need more than a thousand bars to get there. The pull
    /// indicator was never independently broken: it is the bank multiplied by volume.
    /// </para>
    /// <para>
    /// EhlersEnhancedSignalToNoiseRatio is gone from this set entirely rather than reclassified. A ratio
    /// in decibels is only defined for a positive ratio, and on a market with no range the noise estimate
    /// decays to zero and takes the signal with it, so the unguarded logarithm published negative infinity
    /// for the whole series from bar 0. EhlersAlternateSignalToNoiseRatio, the same measurement in the same
    /// family, already guarded its logarithm this way. It now settles to a spread of exactly 0 at a
    /// thousand bars, so this suite holds it to the invariant like any other indicator.
    /// </para>
    /// </remarks>
    private static readonly HashSet<IndicatorName> MovesOnAFlatMarket = new()
    {
        // Spread at 900 -> 4900: unchanged, or larger.
        IndicatorName.EhlersCombFilterSpectralEstimate,   // 25.2476  -> 24.7286
        IndicatorName.FlaggingBands,                      //  6.88303 ->  6.88303 (identical)
        IndicatorName.EhlersDeviationScaledSuperSmoother, //  1.27268 ->  1.27268 (identical)
        IndicatorName.MorphedSineWave,                    //  0.0194986 -> 0.0194986 (identical)

        // Shrinks, but only 5.4x over a 5x longer run - slower than convergence and not yet settled.
        IndicatorName.FastSlowDegreeOscillator            //  1.28917 ->  0.236896
    };

    /// <summary>
    /// Indicators that settle, but need more bars than this test runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not defects. Each was measured again at 4900 bars of 5000: the ones marked "settles" fall below
    /// 1e-9, and the rest are still shrinking by an order of magnitude or more. A filter converging
    /// geometrically can sit well above the threshold at bar 900 and be perfectly correct - three of
    /// these read 100, 50 and 24.8 there, and settle completely by 4900.
    /// </para>
    /// <para>
    /// They stay excluded so the suite passes at 1000 bars, but they are separated from the defects
    /// above so the outstanding count is six rather than twenty-five.
    /// </para>
    /// </remarks>
    private static readonly HashSet<IndicatorName> SettlesAfterMoreBarsThanThisTestRuns = new()
    {
        IndicatorName.EhlersRestoringPullIndicator,           // 3262.55   -> settles
        IndicatorName.StationaryExtrapolatedLevelsOscillator, // 100       -> settles
        IndicatorName.StationaryExtrapolatedLevels,           //  50       -> settles
        IndicatorName.LinearExtrapolation,                    //  24.7996  -> settles
        IndicatorName.EhlersSpectrumDerivedFilterBank,        //   0.878421-> settles
        IndicatorName.SimpleCycle,                            //   0.0537  -> settles
        IndicatorName.DoubleExponentialSmoothing,             //   0.0183  -> settles
        IndicatorName.GChannels,                              //   0.00809 -> settles
        IndicatorName.EhlersDeviationScaledMovingAverage,     //   0.00736 -> settles
        IndicatorName.AdaptiveMovingAverage,                  //   7.95e-6 -> settles
        IndicatorName.IIRLeastSquaresEstimate,                //   1.51e-6 -> settles

        IndicatorName.PseudoPolynomialChannel,                //  66.8218  -> 0.668654
        IndicatorName.GrandTrendForecasting,                  //  18.6118  -> 0.733483
        IndicatorName.VervoortModifiedBollingerBandIndicator, //   4.61997 -> 1.24724e-07
        IndicatorName.PeriodicChannel,                        //   2.10085 -> 0.113543
        IndicatorName.ReversalPoints,                         //   0.272132-> 0.0102399
        IndicatorName.MeanAbsoluteErrorBands,                 //   0.143143-> 0.00525411
        IndicatorName.QuasiWhiteNoise,                        //   0.0115285->0.000268438
        IndicatorName.TrendForceHistogram                     //   5.51e-5 -> 2.02e-6
    };

    /// <summary>
    /// Indicators that cannot settle on a flat market, because they grow without bound by definition.
    /// </summary>
    /// <remarks>
    /// Not a defect, unlike <see cref="MovesOnAFlatMarket"/>: a running total of a constant price grows by
    /// that price on every bar, for ever, and an indicator that settled instead would be the broken one.
    /// This is not a licence for accumulators generally - the ones that accumulate a CHANGE rather than a
    /// level, such as on balance volume, the accumulation distribution line and the cumulative volume
    /// index, add nothing on a bar that did not move, so they settle and are held to the invariant.
    /// </remarks>
    private static readonly HashSet<IndicatorName> UnboundedByDefinition = new()
    {
        IndicatorName.CumulativeSum
    };

    /// <summary>
    /// Indicators publishing an upper band below their middle, or a middle below their lower. Each is
    /// a defect; see issue #178.
    /// </summary>
    /// <remarks>
    /// Two mechanisms account for most of them. FractalChaosBands computes its middle as the mean of
    /// the other two, which is between them by construction - so the only way it can fail is for the
    /// upper band to sit below the lower one, and both start at zero because GetLastOrDefault returns
    /// zero until the first fractal forms. ScalpersChannel labels three unrelated quantities as bands:
    /// a rolling high, a rolling low, and <c>sma - log(pi * atr)</c>, which has no reason to lie
    /// between them.
    /// </remarks>
    private static readonly HashSet<IndicatorName> BandsOutOfOrder = new()
    {
        IndicatorName.AverageTrueRangeChannel,
        IndicatorName.DEnvelope,
        IndicatorName.FractalChaosBands,
        IndicatorName.LBRPaintBars,
        IndicatorName.MovingAverageBands,
        IndicatorName.PriceCurveChannel,
        IndicatorName.PriceLineChannel,
        IndicatorName.RateOfChangeBands,
        IndicatorName.ScalpersChannel,
        IndicatorName.StationaryExtrapolatedLevels,
        IndicatorName.VervoortModifiedBollingerBandIndicator,
        IndicatorName.VortexBands
    };

    public static TheoryData<IndicatorName> AllIndicators
    {
        get
        {
            var data = new TheoryData<IndicatorName>();
            foreach (var name in IndicatorInvoker.GetSupportedIndicators())
            {
                if (!NeedsMarketData.Contains(name))
                {
                    data.Add(name);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Every indicator produces one value per bar, on the primary series or on a named output.
    /// </summary>
    /// <remarks>
    /// An indicator with several outputs publishes an empty primary on purpose, which is how
    /// GetInputValuesList knows it cannot be chained from. That is not a missing result, so the named
    /// outputs are what gets measured in that case.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void ProducesOneValuePerBar(IndicatorName name)
    {
        var data = Market.Real();
        var result = IndicatorInvoker.Invoke(data, name);

        if (result.CustomValuesList.Count > 0)
        {
            result.CustomValuesList.Should().HaveCount(data.Count);
            return;
        }

        result.OutputValues.Should().NotBeEmpty(
            "an indicator with no primary series must publish named outputs, or it produced nothing");

        foreach (var output in result.OutputValues)
        {
            output.Value.Should().HaveCount(data.Count,
                $"the '{output.Key}' series has to line up with the bars");
        }
    }

    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void ProducesTheSameValuesEveryTime(IndicatorName name)
    {
        var first = AllSeries(IndicatorInvoker.Invoke(Market.Real(), name));
        var second = AllSeries(IndicatorInvoker.Invoke(Market.Real(), name));

        second.Keys.Should().BeEquivalentTo(first.Keys, "the same bars must publish the same series");

        foreach (var series in first)
        {
            second[series.Key].Should().Equal(series.Value,
                $"the same bars must give the same '{series.Key}'");
        }
    }

    /// <summary>
    /// Every series an indicator publishes: the primary one, and each named output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reading <c>CustomValuesList</c> alone leaves a sixth of the catalogue untested. Measured
    /// across the 775 indicators reachable through <see cref="IndicatorInvoker"/>: 117 leave the
    /// primary series EMPTY and publish only named outputs - every Bollinger variant, the Ehlers
    /// oscillators that emit two components, anything band-shaped. For those, comparing primary
    /// series compares two empty lists and passes whatever the indicator does.
    /// </para>
    /// <para>
    /// The primary series is kept under its own key rather than merged, so a regression that empties
    /// it is a missing key rather than a silently shorter comparison.
    /// </para>
    /// </remarks>
    private static Dictionary<string, List<double>> AllSeries(StockData result)
    {
        var series = new Dictionary<string, List<double>>(StringComparer.Ordinal)
        {
            ["<primary>"] = result.CustomValuesList,
        };

        foreach (var output in result.OutputValues)
        {
            series[output.Key] = output.Value;
        }

        return series;
    }

    /// <summary>
    /// An indicator reads the bars; it does not get to change them.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void DoesNotModifyTheBarsItWasGiven(IndicatorName name)
    {
        var data = Market.Real();
        var open = new List<double>(data.OpenPrices);
        var high = new List<double>(data.HighPrices);
        var low = new List<double>(data.LowPrices);
        var close = new List<double>(data.ClosePrices);
        var volume = new List<double>(data.Volumes);

        IndicatorInvoker.Invoke(data, name);

        data.OpenPrices.Should().Equal(open);
        data.HighPrices.Should().Equal(high);
        data.LowPrices.Should().Equal(low);
        data.ClosePrices.Should().Equal(close);
        data.Volumes.Should().Equal(volume);
    }

    /// <summary>
    /// A market that never moved cannot produce a moving indicator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The strongest of these, because it needs no reference and admits no interpretation. Given a
    /// thousand identical bars, there is no trend, no volatility and no momentum, so every indicator
    /// has to settle to some constant - whatever constant it considers neutral. One that is still
    /// moving at bar 900 is reading a signal that is not in the data.
    /// </para>
    /// <para>
    /// A thousand bars rather than a few hundred, because an adaptive filter converges geometrically
    /// and a shorter run cannot tell slow convergence from divergence. At 251 bars this flagged 84
    /// indicators; at a thousand it flags 18, and the difference was all convergence.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void SettlesToAConstantOnAFlatMarket(IndicatorName name)
    {
        if (MovesOnAFlatMarket.Contains(name) || SettlesAfterMoreBarsThanThisTestRuns.Contains(name)
            || UnboundedByDefinition.Contains(name))
        {
            return;
        }

        // Every published series, not just the primary one. 117 of the 775 indicators here leave
        // the primary empty and publish only named outputs, and this returned before looking at any
        // of them - so a band that never settles passed as long as its primary series was absent.
        var published = AllSeries(IndicatorInvoker.Invoke(Market.Flat(FlatBars), name));

        foreach (var series in published)
        {
            var values = series.Value;
            if (values.Count != FlatBars)
            {
                continue;
            }

            var settled = values.Skip(SettledFrom).ToList();
            settled.Should().OnlyContain(v => !double.IsNaN(v) && !double.IsInfinity(v),
                $"a flat market cannot produce an undefined reading in '{series.Key}'");

            var spread = settled.Max() - settled.Min();
            spread.Should().BeLessThan(1e-6,
                $"{name} still moves '{series.Key}' by {spread:G6} after {SettledFrom} identical bars");
        }
    }

    /// <summary>
    /// Every indicator that names an upper, middle and lower band keeps them in that order.
    /// </summary>
    /// <remarks>
    /// Read from the published output names rather than a list of band indicators, so this covers any
    /// indicator that calls its outputs by those names, including ones added later.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AllIndicators))]
    public void BandsAreOrderedUpperMiddleLower(IndicatorName name)
    {
        if (BandsOutOfOrder.Contains(name))
        {
            return;
        }

        var data = Market.Real();
        var outputs = IndicatorInvoker.Invoke(data, name).OutputValues;

        if (!outputs.TryGetValue("UpperBand", out var upper)
            || !outputs.TryGetValue("MiddleBand", out var middle)
            || !outputs.TryGetValue("LowerBand", out var lower))
        {
            return;
        }

        for (var i = 0; i < data.Count; i++)
        {
            if (double.IsNaN(upper[i]) || double.IsNaN(middle[i]) || double.IsNaN(lower[i]))
            {
                continue;
            }

            upper[i].Should().BeGreaterThanOrEqualTo(middle[i], $"upper band at index {i}");
            middle[i].Should().BeGreaterThanOrEqualTo(lower[i], $"middle band at index {i}");
        }
    }

    /// <summary>
    /// Bars for the invariants above.
    /// </summary>
    private static class Market
    {
        private static readonly List<TickerData> Real_ = TestData.GlobalTestData.StockTestData;

        public static StockData Real() => new(Real_);

        /// <summary>
        /// A market with no movement at all: every price identical, volume constant.
        /// </summary>
        public static StockData Flat(int bars)
        {
            var price = Enumerable.Repeat(100.0, bars).ToList();
            var volume = Enumerable.Repeat(1_000_000.0, bars).ToList();
            var dates = Enumerable.Range(0, bars)
                .Select(i => new DateTime(2015, 1, 1).AddDays(i))
                .ToList();

            return new StockData(price, price, price, price, volume, dates);
        }
    }
}
