using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Catalogue-wide structural checks and conditional mathematical properties.
/// </summary>
/// <remarks>
/// <para>
/// Constant price does not imply a constant output for every formula: time-dependent
/// statistics and normalized filter transients need their own contracts. Such cases
/// use an appropriate convergence horizon or an independent trajectory reference.
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
    /// <para>
    /// FlaggingBands has gone the same way, and its band ordering is now guaranteed rather than lucky.
    /// Each band was written against its own value two and three bars back, which split it into odd and
    /// even subsequences that never interact, so a band that stopped moving held two different values
    /// for ever: the spread was 6.88303 at a thousand bars and still exactly 6.88303 at five thousand
    /// and at twenty thousand - a period-2 cycle, not convergence needing more bars. Each band now
    /// carries forward from its own previous value, and its decay stops at price, so the upper cannot
    /// drift down through price nor the lower up through it. Without that clamp both seed at the first
    /// close and the first non-zero decay crossed them at bar 15. It settles to a spread of 0, and
    /// a >= price >= b makes BandsAreOrderedUpperMiddleLower hold by construction.
    /// </para>
    /// <para>
    /// EhlersDeviationScaledSuperSmoother was the clearest of them once measured over a long enough run.
    /// Its momentum scaled by its own RMS is zero on a market that never moved, and zero is the single
    /// value its coefficients cannot take: a1 becomes exp(0) = 1, so c2 = 2, c3 = -1 and c1 = 0, leaving
    /// a double integrator with both poles at z = 1 that stops reading its input and carries whatever
    /// straight line it already held. The output drifted linearly at 0.01286 per bar - 112.65 at bar
    /// 1000, 164.07 at 5000, 356.90 at 20000 - which a hundred-bar window reads as a fixed spread of
    /// 1.27268 at every length, so it looked frozen rather than divergent. The scaling now falls back to
    /// a magnitude of one when there is no deviation to scale by, which is the nominal period and gives
    /// exactly the coefficients CalculateEhlersSuperSmootherFilter uses.
    /// </para>
    /// <para>
    /// EhlersCombFilterSpectralEstimate carried the same defect as the spectrum derived filter bank, in
    /// both of the places it kept state. Each period's two-sample recursion read a single shared list
    /// holding one value per bar - whichever period the loop finished on, always the longest - and the
    /// power sum that picks the dominant cycle read that same list at every lag, so the power was summed
    /// over a mixture of periods rather than over the one being measured. A test on prevBp / j also
    /// dropped every bar where the bandpass ran negative, half of them for a filter centred on zero,
    /// from what is by definition a sum of squares. Each period now runs its own recursion over its own
    /// history, and the spread falls from 25.2476 to 0 at a thousand bars, and is 0 at five and at
    /// twenty thousand as well.
    /// </para>
    /// <para>
    /// Its settled value still depends on how long it has run: 29 at a thousand bars, 30.5 at five
    /// thousand, and 0 at twenty thousand, once the amplitudes have decayed far enough that every power
    /// underflows and no period clears the half-power test. That is deliberately left alone rather than
    /// clamped into the scanned band the way the bank's dominant cycle is. The bank needed that clamp
    /// because EhlersRestoringPullIndicator divides into its output as 2*pi/domCyc, so a zero propagated
    /// into everything downstream; nothing reads this one, and a market with no cycle in it reporting no
    /// cycle is an honest answer rather than a defect to paper over.
    /// </para>
    /// </remarks>
    private static readonly HashSet<IndicatorName> MovesOnAFlatMarket = new()
    {
        // Empty. Every indicator that was here has been fixed, or shown by measurement to belong in one
        // of the two sets below - which are not defects. The set is kept so that the next one found has
        // somewhere to go and a count that stays visible.
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
    /// above so that the count of outstanding defects stays visible rather than being buried among the
    /// indicators that are merely slow. That count is now zero.
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

        // Converges as 1/n, which is real but harmonic: measured across the last hundred bars of runs of
        // 1000, 5000 and 20000 the spread is 1.11986, 0.20589 and 0.0507055, and multiplying each by its
        // own bar count gives 1119.9, 1029.5 and 1014.1 - a constant. Its fast and slow halves use the
        // same two polynomial terms, which cancel exactly, leaving rolling sums of sin(x)/(i+1) whose
        // terms fall off as 1/i. So it does reach zero, but no run this suite could afford gets it to
        // 1e-6; that needs of the order of a billion bars. Listed here rather than above because the
        // limit is right and the rate is a property of the weighting, not a defect.
        IndicatorName.FastSlowDegreeOscillator,               //   1.11986 -> 1/n

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
    /// Indicators that oscillate on any market at all, because the oscillation is not read from the bars.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a defect, and distinct from <see cref="UnboundedByDefinition"/>: nothing here grows without
    /// bound, it simply never stops moving. MorphedSineWave adds a sine wave to price to morph the two
    /// together, and the sine's argument is the bar index alone - not any quantity taken from the bars -
    /// so the same carrier rides on a flat market as on any other, and no market can settle it.
    /// </para>
    /// <para>
    /// The amplitude is the arithmetic rather than an observation: the published value is
    /// <c>price + sin(i / p) / power</c>, so a full swing is 2/power, which at the default power of 100
    /// is 0.02. Measured across the last hundred bars it is 0.0194986 - a hundred samples of a sine not
    /// quite reaching both extremes - and identical at 1000, 5000 and 20000 bars.
    /// </para>
    /// </remarks>
    private static readonly HashSet<IndicatorName> OscillatesByConstruction = new()
    {
        IndicatorName.MorphedSineWave
    };

    /// <summary>
    /// Indicators publishing an upper band below their middle, or a middle below their lower. Each would
    /// be a defect; see issue #178. The set is now empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each mechanism below is measured on the AAPL fixture rather than assumed. DEnvelope is fixed and
    /// gone from this set: its centre line used McNicholl's zero-lag form grouped as
    /// <c>(2 - alpha) * (mt - ut)</c>, which cannot reproduce a constant - at a constant price mt and ut
    /// are both that price and the centre came out 0. It published a middle band of -13.86 for a stock
    /// trading at 145, and the same grouping drove the width negative on 112 of the 251 bars, inverting
    /// both bands. Corrected, its centre line agrees to every digit with CalculateMcNichollMovingAverage
    /// reached by an entirely separate path.
    /// </para>
    /// <para>
    /// VortexBands is fixed and gone from this set as well. It is a variation on that same Better
    /// Bollinger Bands construction, and it measured its half-width as twice the mean of the signed
    /// deviation from the basis. That mean sits near zero for a series oscillating about its own
    /// average, and turns negative on every bar where price is below it, which inverted the two bands
    /// on 135 of the 251 bars - its own signal line gave the game away by testing both orderings.
    /// Measured as a distance instead, it agrees with DEnvelope to seven figures on all three bands at
    /// bar 200, through code the two share none of. The Builder serves this one from a verified fast
    /// path, so that third implementation moves with the other two.
    /// </para>
    /// <para>
    /// PriceLineChannel and PriceCurveChannel are fixed and gone from this set as well, and they broke
    /// for the reason FlaggingBands did. Both bands seed at the first close and then step away from it,
    /// the upper decaying down and the lower rising up, so the upper ends bar 0 below the lower before
    /// the channel has any width. At that bar the second previous values are still zero, which makes
    /// prevA1 - prevA2 the whole price and positive: it sets the upper band's step to a full average
    /// true range while the lower band's test for a negative difference fails. Each is an envelope of
    /// price, so its drift now stops at price, which keeps a >= price >= b and puts their mean between
    /// them by construction. Each showed exactly one violation of each kind, both at bar 0.
    /// </para>
    /// <para>
    /// Seven more published a MiddleBand holding a different quantity from the one their upper and lower
    /// bands bracket, and all seven are fixed. Each now publishes the centre its own bands are drawn
    /// around, and the displaced series keeps its own name rather than being dropped:
    /// AverageTrueRangeChannel moved its moving average to Sma; MovingAverageBands publishes the slow
    /// average its bands are built from and moved the fast one to FastMa; RateOfChangeBands is centred on
    /// zero, since its bands are plus and minus an RMS, and moved the rate of change to Roc;
    /// ScalpersChannel publishes the midpoint of its rolling high and low and moved
    /// <c>sma - log(pi * atr)</c> to Scalper; StationaryExtrapolatedLevels moved the deviation of price
    /// from its average to Deviation; and VervoortModifiedBollingerBandIndicator publishes the mean of
    /// its bands - which it already computed and never published - and moved %b to PercentB.
    /// </para>
    /// <para>
    /// LBRPaintBars is the one of those seven with no centre to publish. Its bands are a squeeze, the
    /// rolling high minus an ATR multiple against the rolling low plus one, and they genuinely cross:
    /// measured on the fixture the upper band is below the lower on 171 of the 251 bars. Any series put
    /// between them would be wrong on those bars, so its width moved to Aatr and it publishes no middle
    /// band at all. This invariant reads the published names, so it no longer applies to that indicator -
    /// which is the honest outcome rather than an exclusion.
    /// </para>
    /// <para>
    /// FractalChaosBands was not the zero seed it was first taken for either: its first violation was at
    /// bar 60, where a down fractal at 172 sat above an up fractal at 163.41 - not a band left at zero.
    /// That was read at the time as bands of this shape genuinely crossing in a trend. It was narrower than
    /// that: the indicator was finding three-bar pivots rather than five-bar fractals, and re-anchoring on
    /// wiggles is what let one band overtake the other. Corrected in #202, after which the same fixture
    /// crosses on none of its 251 bars.
    /// </para>
    /// </remarks>
    private static readonly HashSet<IndicatorName> BandsOutOfOrder = new()
    {
        // Empty. Every indicator that was here has been fixed, or shown by measurement to belong in the
        // set below - which is not a defect. The set is kept so that the next one found has somewhere to
        // go and a count that stays visible.
    };

    /// <summary>
    /// Indicators whose upper and lower bands genuinely cross, so that no series can lie between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a defect. FractalChaosBands publishes the last up fractal as its upper band and the last down
    /// fractal as its lower one, and those two anchors move independently: a down fractal forming later, at
    /// a higher level after an upward gap, sits above an up fractal still being carried from earlier. No
    /// width of pattern prevents that, so no series can be guaranteed to lie between them. Its middle band
    /// is the mean of the other two, so it lies between them whenever they are ordered and cannot itself be
    /// at fault - this invariant is simply not a statement about an indicator built this way.
    /// </para>
    /// <para>
    /// The numbers once recorded here were inflated by a separate defect and are worth correcting rather
    /// than repeating. They were 2 crossings in 251 bars, first at bar 60 where a down fractal at 172 sat
    /// above an up fractal at 163.41 - measured when the indicator was comparing each candidate against one
    /// bar on each side, which is a three-bar pivot rather than a five-bar fractal. Re-anchoring on every
    /// single-bar wiggle is what dragged one band past the other that often. Given the pattern a fractal
    /// actually is, the same fixture now crosses on none of its 251 bars (#202).
    /// </para>
    /// <para>
    /// That measurement is why the entry stays rather than why it would go. Zero crossings on one fixture
    /// shows the old count was an artefact; it does not show the bands cannot cross, and the structure
    /// above says they can. An exclusion resting on the shape of the indicator outlives any particular set
    /// of bars.
    /// </para>
    /// </remarks>
    private static readonly HashSet<IndicatorName> BandsThatGenuinelyCross = new()
    {
        IndicatorName.FractalChaosBands
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
    public async Task SettlesToAConstantOnAFlatMarket(IndicatorName name)
    {
        if (name is IndicatorName.EhlersAdaptiveCommodityChannelIndexV2 or IndicatorName.EhlersCombFilterSpectralEstimate)
        {
            // Both divide a decaying filter transient by its own scale (RMS or peak
            // power). Vanishing magnitude therefore does not imply a constant ratio.
            // Check the complete declared trajectory, including startup, instead of
            // imposing a stationarity property these formulas do not possess.
            IIndicator Create() => name == IndicatorName.EhlersAdaptiveCommodityChannelIndexV2
                ? new EhlersAdaptiveCommodityChannelIndexV2() : new EhlersCombFilterSpectralEstimate();
            var flat = Enumerable.Range(0, FlatBars).Select(i =>
                new Bar(new DateTime(2024, 1, 1).AddMinutes(i), 100, 100, 100, 100, 1000)).ToArray();
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(Create().GetType(), "normalized-flat", Create),
                new IndicatorValidationOptions { AdditionalFixtures = new[] { new IndicatorValidationFixture("normalized-flat", flat) } });
            return;
        }
        if (MovesOnAFlatMarket.Contains(name) || SettlesAfterMoreBarsThanThisTestRuns.Contains(name)
            || UnboundedByDefinition.Contains(name) || OscillatesByConstruction.Contains(name))
        {
            return;
        }

        // Every published series, not just the primary one. 117 of the 775 indicators here leave
        // the primary empty and publish only named outputs, and this returned before looking at any
        // of them - so a band that never settles passed as long as its primary series was absent.
        // Kaufman's flat-market gain is (2/31)^2: its time-variance transient
        // needs substantially more than 900 observations. The tuned bypass also
        // retains a slow decaying pole. Keep the same tolerance at a longer horizon.
        var bars = name == IndicatorName.KaufmanAdaptiveCorrelationOscillator ? 8000
            : name == IndicatorName.EhlersDominantCycleTunedBypassFilter ? 2000 : FlatBars;
        var settledFrom = bars - (FlatBars - SettledFrom);
        var published = AllSeries(IndicatorInvoker.Invoke(Market.Flat(bars), name));

        foreach (var series in published)
        {
            var values = series.Value;
            if (values.Count != bars)
            {
                continue;
            }

            var settled = values.Skip(settledFrom).ToList();
            settled.Should().OnlyContain(v => !double.IsNaN(v) && !double.IsInfinity(v),
                $"a flat market cannot produce an undefined reading in '{series.Key}'");

            var spread = settled.Max() - settled.Min();
            spread.Should().BeLessThan(1e-6,
                $"{name} still moves '{series.Key}' by {spread:G6} after {settledFrom} identical bars");
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
        if (BandsOutOfOrder.Contains(name) || BandsThatGenuinelyCross.Contains(name))
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
