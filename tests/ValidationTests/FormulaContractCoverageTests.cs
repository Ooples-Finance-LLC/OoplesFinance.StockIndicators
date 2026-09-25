using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FormulaContractCoverageTests
{
    [Fact]
    public void AdaptiveEhlersV2ReferencesHaveIndependentFirstBarAndSignalSeeds()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 1, 1, 1, 1, 1) };
        foreach (IIndicator indicator in new IIndicator[] {
            new EhlersAdaptiveRelativeStrengthIndexV2(4, 2, 1),
            new EhlersAdaptiveStochasticIndicatorV2(4, 2, 1),
            new EhlersAdaptiveCommodityChannelIndexV2(4, 2, 1) })
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(2, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("adaptive-first", bars, [[0], [0]], 0));
        }
    }

    [Fact]
    public void AdaptiveWindowMeanForgetsLargeDepartedValuesAndDoesNotCommitPreview()
    {
        var mean = new AdaptiveWindowMean(3);
        Assert.Equal(1e100, mean.Next(1e100, 1, true));
        Assert.Equal(2, mean.Next(2, 1, true));
        Assert.Equal(3, mean.Next(4, 2, false));
        Assert.Equal(3, mean.Next(4, 2, true));
        Assert.Equal(4, mean.Next(6, 3, true));
        Assert.Equal(6, mean.Next(8, 3, true));
        mean.Reset();
        Assert.Equal(2, mean.Next(2, 3, false));
        Assert.Equal(4, mean.Next(4, 3, true));
    }

    [Fact]
    public void DemarkPatternsRequireEveryLaggedComparisonToAgree()
    {
        var bars = new[] { 1d, 2, 3, 2, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var examples = new (IIndicator Indicator, double[] Expected)[] {
            (new DemarkSetupIndicator(2), [0, 2, 3, 0, 0]),
            (new DemarkReversalPoints(2, 1), [0, 2, 3, 0, 1]),
            (new DemarkRangeExpansionIndex(2), [0, 0, 50, 100, -100])
        };
        foreach (var (indicator, expected) in examples)
        {
            var rule = Assert.Single(BuiltInFormulaReferences.For(indicator));
            rule.Check(new IndicatorValidationContext("demark-hand", bars, [expected], 0));
        }
    }

    [Fact]
    public void PressureRatioCountsGapDownSellingInTheDenominator()
    {
        Bar[] bars = [
            new(DateTime.UnixEpoch, 9, 11, 8, 10, 1),
            new(DateTime.UnixEpoch.AddMinutes(1), 8, 9, 6, 7, 1)];
        var rule = Assert.Single(BuiltInFormulaReferences.For(new DemarkPressureRatioV1(2)));
        rule.Check(new IndicatorValidationContext("pressure-gap", bars, [[100, 100d / 7]], 0));
    }

    [Fact]
    public void SpearmanReferenceAveragesTiesAndDemandReferenceSplitsVolume()
    {
        var prices = new[] { 1d, 1, 2, 1 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new SpearmanIndicator(3, 1)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("spearman-ties", bars, [[0, 0, 100, -50], [0, 0, 100, -50]], 0));
        bars = new[] { 2d, 3, 1, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 4, 0, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new DemandIndex(1))).Check(
            new IndicatorValidationContext("demand-split", bars, [[0, 2, -2d / 3, 0, 0]], 0));
    }

    [Fact]
    public void DailyPivotReferencesUseTheCompletedSessionsOpenExtremesAndClose()
    {
        Bar[] bars = [
            new(DateTime.UnixEpoch, 2, 4, 1, 3, 1),
            new(DateTime.UnixEpoch.AddHours(1), 3, 6, 2, 5, 1),
            new(DateTime.UnixEpoch.AddDays(1), 8, 10, 7, 9, 1)];
        var examples = new (IIndicator Indicator, double[] Levels)[] {
            (new StandardPivotPoints(), [3.5, 1, -1.5, -1.5, 6, 8.5, 8.5, -1.5, -.25, 2.25, 4.75, 7.25, 8.5]),
            (new DynamicPivotPoints(), [4, 2, 7])
        };
        foreach (var (indicator, levels) in examples)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(levels.Length, rules.Length);
            var expected = levels.Select(v => new[] { 0d, 0, v }).ToArray();
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("daily-pivot", bars, expected, 0));
        }
    }

    [Fact]
    public void ExtrapolatedAndRocBandsSeparateTheirCentresFromTheirIndicatorLines()
    {
        var bars = new[] { 1d, 2, 4, 8, 16 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new StationaryExtrapolatedLevels(2)).ToArray();
        Assert.Equal(4, rules.Length);
        double[][] expected = [[0, 0, 0, .75, .75], [0, 0, 0, .375, .375], [0, 0, 0, 0, 0], [1, .5, 1, 2, 4]];
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("extrapolation-hand", bars, expected, 0));
        rules = BuiltInFormulaReferences.For(new RateOfChangeBands(1, 1)).ToArray();
        Assert.Equal(4, rules.Length);
        expected = [[0, 100, 100, 100, 100], [0, 0, 0, 0, 0], [0, -100, -100, -100, -100], [0, 100, 100, 100, 100]];
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("roc-bands-hand", bars, expected, 0));
    }

    [Fact]
    public void PriceEnvelopeReferencesDistinguishLinearAndAcceleratedDrift()
    {
        var bars = new[] { 2d, 1, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var examples = new (IIndicator Indicator, double[][] Expected)[] {
            (new PriceLineChannel(2), [[2, 1.75, 1.5], [2, 1.375, 1.25], [2, 1, 1]]),
            (new PriceCurveChannel(2), [[2, 1.875, 1.75], [2, 1.4375, 1.375], [2, 1, 1]])
        };
        foreach (var (indicator, expected) in examples)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(3, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("price-envelope", bars, expected, 0));
        }
    }

    [Fact]
    public void RelativeMotionReferencesHaveHandCalculatedDirectionAndFeedback()
    {
        var bars = new[] { 1d, 2, 1, 3 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var examples = new (IIndicator Indicator, double[][] Expected)[] {
            (new RandomWalkIndex(1), [[0, 1, -1, 1], [0, -1, 1, -1]]),
            (new RunningEquity(2), [[0, 1, 0, -3]]),
            (new RegressionOscillator(2), [[0, 0, 0, 0]]),
            (new RecursiveDifferenciator(1), [[1, 1, 0, .2]]),
            (new RelativeSpreadStrength(1, 1, 1, 1), [[100, 100, 100, 100]]),
            (new SimpleLines(1, 0), [[1, 1, 1, 2]]),
            (new SimpleCycle(1), [[1, 2, 0, 0]])
        };
        foreach (var (indicator, expected) in examples)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("relative-motion-hand", bars, expected, 0));
        }
    }

    [Fact]
    public void RatioAndTrigonometricReferencesHaveExplicitSeedAndDirection()
    {
        var bars = new[] { 10d, 12, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 12, 8, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new RatioOchlAverager())).Check(
            new IndicatorValidationContext("body-ratio", bars, [[10, 11, 9.5]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new TrigonometricOscillator(1))).Check(
            new IndicatorValidationContext("trig-directions", bars, [[Math.Atan(Math.PI), Math.Atan(Math.PI), Math.Atan(-3 * Math.PI)]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new RetentionAccelerationFilter(1))).Check(
            new IndicatorValidationContext("retention-equal-range", bars, [[10, 10, 10]], 0));
    }

    [Fact]
    public void SpreadPrecisionRetainsBinaryInputChangesAfterTwoLargeAveragesCancel()
    {
        // Independently calculated with 65-digit decimal arithmetic from exact IEEE-754 inputs.
        foreach (var (kind, expected) in new[] {
            (MovingAvgType.ExponentialMovingAverage, 99.9999999635433),
            (MovingAvgType.WeightedMovingAverage, 16.81064998602621) })
        {
            using var kernel = new RelativeSpreadKernel(kind, 10, 40, 14, 5);
            for (var pass = 0; pass < 2; pass++)
            {
                kernel.Reset();
                double actual = 0;
                for (var i = 0; i <= 190; i++)
                {
                    var preview = kernel.Next(100 + i * .1, false);
                    actual = kernel.Next(100 + i * .1, true);
                    Assert.Equal(preview, actual);
                }
                Assert.InRange(Math.Abs(actual - expected), 0, 1e-10);
            }
        }
    }

    [Fact]
    public void SimpleLinesDoesNotAccumulateFractionalStepError()
    {
        var kernel = new SimpleLinesKernel(5, 0);
        Assert.Equal(80, kernel.Next(80, true));
        for (var i = 1; i <= 1000; i++)
            Assert.Equal(80 + i / 5d, kernel.Next(1000, true));
        kernel.Reset();
        Assert.Equal(3, kernel.Next(3, false));
        Assert.Equal(4, kernel.Next(4, true));
    }

    [Fact]
    public void KaseReferencesUsePriceOffsetsAndHoldWhenVolumeIsUnavailable()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, i == 2 ? 0 : 1)).ToArray();
        var stops = BuiltInFormulaReferences.For(new KaseDevStopV2(1, 2, 1)).ToArray();
        Assert.Equal(4, stops.Length);
        foreach (var rule in stops) rule.Check(new IndicatorValidationContext("kase-stops-hand", bars,
            [[0, 0, 2], [0, 0, 2], [0, 0, 2], [0, 0, 2]], 0));
        var ratios = BuiltInFormulaReferences.For(new KaseIndicator(1)).ToArray();
        Assert.Equal(2, ratios.Length);
        foreach (var rule in ratios) rule.Check(new IndicatorValidationContext("kase-ratios-hand", bars, [[0, 1, 1], [0, 5, 5]], 0));
    }

    [Fact]
    public void CombSpectralEstimateHasNoEnergyBeforeItsFirstCompletedObservation()
    {
        var bars = new[] { 1d, 0, 0 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersCombFilterSpectralEstimate(1, 1))).Check(
            new IndicatorValidationContext("comb-single-bin-hand", bars, [[0, 1, 1]], 0));
    }

    [Fact]
    public void FourierSpectralEstimateWeightsAnImpulseEquallyAcrossPeriods()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 1, 1, 1, 1, 1) };
        Assert.Single(BuiltInFormulaReferences.For(new EhlersDiscreteFourierTransformSpectralEstimate(4, 1))).Check(
            new IndicatorValidationContext("spectral-impulse-hand", bars, [[2.5]], 0));
    }

    [Fact]
    public void MesaPredictRetainsTheFirstAutoregressionCoefficient()
    {
        var bars = new[] { 0d, 0, 0, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var radius = Math.Exp(-.99); var b = 2 * radius * Math.Cos(.99);
        var filtered = (1 + b + radius * radius) / 4 * (1 - b + radius * radius) / 2;
        var rules = BuiltInFormulaReferences.For(new EhlersMesaPredictIndicatorV2(1, 1, 1, 1)).ToArray();
        Assert.Equal(3, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("mesa-impulse-hand", bars,
            [[0, 0, 0, 0, filtered], [0, 0, 0, 0, 4.525 * filtered], [0, 0, 0, 0, 2 * filtered]], 0));
    }

    [Fact]
    public void AdaptiveBandpassNormalizesItsFirstImpulseAndDelaysTheSignal()
    {
        var bars = new[] { 0d, 0, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new EhlersAdaptiveBandPassFilter()).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("adaptive-band-hand", bars, [[0, 0, 0, 1], [0, 0, 0, 0]], 0));
    }

    [Fact]
    public void CycleTunedRsiHasHandCalculatedOpeningGainAndLoss()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var thirdPeriod = .21091455 * 63.3318;
        Assert.Single(BuiltInFormulaReferences.For(new DominantCycleTunedRelativeStrengthIndex())).Check(
            new IndicatorValidationContext("cycle-rsi-hand", bars, [[100, 100, 100 * (1 - 1 / thirdPeriod)]], 0));
    }

    [Fact]
    public void ZeroCrossingCycleClampsTheFirstMeasuredHalfCycle()
    {
        var bars = new[] { 0d, 0, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersZeroCrossingsDominantCycle())).Check(
            new IndicatorValidationContext("zero-crossing-hand", bars, [[6, 6, 6, 7.5]], 0));
    }

    [Fact]
    public void ConfluenceHasHandCalculatedZeroLagUnitPeriodVotes()
    {
        var bars = Enumerable.Range(0, 3).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 2, 2, 2, 2, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new ConfluenceIndicator(1))).Check(new IndicatorValidationContext("confluence-unit-hand", bars, [[0, 0, 0]], 0));
    }

    [Fact]
    public void VervoortCandleTrendsHaveHandCalculatedLatchedReversals()
    {
        var bars = new[] { 2d, 4, 2, 8, 2, 1, 0 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + 1, v - 1, v, 1)).ToArray();
        foreach (var indicator in new IIndicator[] { new VervoortHeikenAshiCandlestickOscillator(1), new VervoortHeikenAshiLongTermCandlestickOscillator(1) })
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("vervoort-candle-hand", bars, [[0, 0, 0, 1, 1, 1, -1]], 0));
    }

    [Fact]
    public void InsyncIndexHasHandCalculatedNeutralPricesAndDelayedVotes()
    {
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 100, 100, 100, 100, 1)).ToArray();
        var indicator = new InsyncIndex(fastLength: 1, slowLength: 1, mfiLength: 1, bbLength: 1, cciLength: 1,
            dpoLength: 1, rocLength: 1, rsiLength: 1, stochLength: 1, stochKLength: 1, stochDLength: 1, smaLength: 1);
        Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("insync-hand", bars, [[45, 40, 40, 45]], 0));
    }

    [Fact]
    public void ParabolicSarHasHandCalculatedAccelerationAndReversals()
    {
        var bars = new[] { 2d, 4, 6, 4, 2, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new ParabolicSar(start: .1, increment: .1, maximum: .3))).Check(new IndicatorValidationContext("sar-hand", bars,
            [[1, 1, 1, 2.8, 7, 6.4, 1]], 0));
    }

    [Fact]
    public void HalfTrendHasHandCalculatedConfirmedReversals()
    {
        var bars = new[] { 2d, 4, 6, 4, 2, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + .5, v - .5, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new HalfTrend(1))).Check(new IndicatorValidationContext("half-trend-hand", bars,
            [[1.5, 3.5, 5.5, 5.5, 2.5, 2.5, 5.5]], 0));
    }

    [Fact]
    public void TrenderHasHandCalculatedUnitPeriodReversals()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new Trender(1)).ToArray();
        Assert.Equal(3, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("trender-hand", bars,
            [[2, 4, 4, 3], [0, 0, 3, 3], [2, 4, 3, 3]], 0));
    }

    [Fact]
    public void PopulationDeviationUsesArithmeticMeanWithWeightedSignal()
    {
        var bars = new[] { 2d, 4, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new StandardDevation(2, MovingAvgType.WeightedMovingAverage)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("population-hand", bars,
            [[0, 1, 2], [0, 2d / 3, 5d / 3]], 0));
    }

    [Fact]
    public void PivotAverageUsesPreviousCompletedMonthAndCurrentOpeningPrice()
    {
        var dates = new[] { new DateTime(2024, 1, 30), new DateTime(2024, 1, 31), new DateTime(2024, 2, 1) };
        var bars = new[] { 2d, 4, 8 }.Select((v, i) => new Bar(dates[i], v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new PivotPointAverage(2, InputLength.Month)).ToArray();
        Assert.Equal(6, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("monthly-pivot-hand", bars,
            [[0, 0, 10d / 3], [0, 0, 5d / 3], [.5, .5, 4.5], [0, 0, 2.5], [2d / 3, 2d / 3, 14d / 3], [0, 0, 8d / 3]], 0));
    }

    [Fact]
    public void SqueezeMomentumHasHandCalculatedResidualRegression()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        // SMA is zero until full. L=3 residuals: 1, 5/2, -5/6, 19/6. The three-point endpoint is (-a+2b+5c)/6.
        Assert.Single(BuiltInFormulaReferences.For(new SqueezeMomentumIndicator(3))).Check(
            new IndicatorValidationContext("squeeze-hand", bars, [[1, 2.5, -1d / 36, 35d / 18]], 0));
    }

    [Fact]
    public void PeakValleyHasThreeDistinctHandCalculatedEventSeries()
    {
        var bars = new[] { 2d, 4, 2, 8, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new PeakValleyEstimation(2, 1)).ToArray();
        Assert.Equal(3, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("peak-valley-hand", bars,
            [[-1, 0, 1, 0, 0, 0], [0, -1, 0, 0, 0, -1], [0, -1, 0, 0, 0, -1]], 0));
    }

    [Fact]
    public void StationaryExtrapolationHasHandCalculatedRanks()
    {
        var bars = new[] { 2d, 4, 2, 8, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new StationaryExtrapolatedLevelsOscillator(2))).Check(
            new IndicatorValidationContext("stationary-rank-hand", bars, [[0, 0, 100, 50, 0, 100]], 0));
    }

    [Fact]
    public void FreedomOfMovementNormalizesBothRangesToOneThroughTen()
    {
        var prices = new[] { 1d, 2, 6, 6 }; var volumes = new[] { 1d, 2, 3, 6 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, volumes[i])).ToArray();
        var rules = BuiltInFormulaReferences.For(new FreedomOfMovement(3)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("freedom-hand", bars, [[0, 0, Math.Sqrt(2), 19 / Math.Sqrt(182)], [1, 1, 1, 1]], 0));
    }

    [Fact]
    public void RsingHasHandCalculatedVolumeAndRangeScaling()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + i + 1, v - i - 1, v, i + 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new RSINGIndicator(2)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("rsing-hand", bars, [[0, 0, 27], [0, 0, 18]], 0));
    }

    [Fact]
    public void QuadraticRegressionFitsThreeHandCalculatedPoints()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new QuadraticRegression(3))).Check(
            new IndicatorValidationContext("quadratic-hand", bars, [[0, 0, 2, 8]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new LinearQuadraticConvergenceDivergenceOscillator(3))).Check(
            new IndicatorValidationContext("linear-quadratic-hand", bars, [[-2, -4, -2d / 3, 4d / 3]], 0));
    }

    [Fact]
    public void PivotAndSwingEventsHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2, 1, 3, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new HerrickPayoffIndex(100))).Check(
            new IndicatorValidationContext("payoff-hand", bars.Take(3).ToArray(), [[0, 300, -100]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new TTMScalperIndicator())).Check(
            new IndicatorValidationContext("scalper-hand", bars, [[0, 5, 5, 0, 0, 5]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new TopsAndBottomsFinder(2))).Check(
            new IndicatorValidationContext("tops-bottoms-hand", bars.Take(3).ToArray(), [[0, 1, -1]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new PivotDetectorOscillator(200))).Check(
            new IndicatorValidationContext("pivot-seed-hand", bars.Take(1).ToArray(), [[130]], 0));
    }

    [Fact]
    public void HybridAndOneLcHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new HybridConvolutionFilter(2))).Check(
            new IndicatorValidationContext("hybrid-hand", bars, [[2, 2.5, 2.375]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new OneLCLeastSquaresMovingAverage(2, MovingAvgType.SimpleMovingAverage))).Check(
            new IndicatorValidationContext("one-lc-hand", bars, [[0, 4.7, 1.3]], 0));
    }

    [Fact]
    public void JurikFiltersHaveHandCalculatedUnitPeriodValues()
    {
        var prices = new[] { 2d, 4, 2, 3, 4, 5, 4, 4 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new JmaRsxClone(1))).Check(
            new IndicatorValidationContext("rsx-unit-hand", bars, [[50, 50, 50, 50, 50, 100, 0, 50]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new Jma(1))).Check(
            new IndicatorValidationContext("jma-unit-hand", bars, [prices], 0));
    }

    [Fact]
    public void GrandForecastAndVanillaPatternsHaveHandCalculatedValues()
    {
        var bars = new[] { 3d, 1, 2, 0, 3 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new VanillaABCDPattern())).Check(
            new IndicatorValidationContext("vanilla-break-hand", bars, [[0, 0, 0, 1, -1]], 0));
        var first = new[] { new Bar(DateTime.UnixEpoch, 2, 2, 2, 2, 1) };
        var rules = BuiltInFormulaReferences.For(new GrandTrendForecasting(1, 1, 2)).ToArray();
        Assert.Equal(4, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("grand-forecast-hand", first, [[1.8], [7.6], [3.6], [-.4]], 0));
    }

    [Fact]
    public void SteppedRegressionAndAdaptiveMomentumHaveHandCalculatedSeeds()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new TStepLeastSquaresMovingAverage(2))).Check(
            new IndicatorValidationContext("step-regression-hand", bars, [[0, 3, 3]], 0));
        var rules = BuiltInFormulaReferences.For(new EhlersSmoothedAdaptiveMomentum(5, 8, MovingAvgType.ExponentialMovingAverage)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("adaptive-momentum-seed", bars.Take(1).ToArray(), [[0], [0]], 0));
    }

    [Fact]
    public void AdaptiveMedianHasHandCalculatedShortWindowValues()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersMedianAverageAdaptiveFilter(3, .002))).Check(
            new IndicatorValidationContext("adaptive-median-hand", bars, [[1d / 6, .75, 37d / 24]], 0));
    }

    [Fact]
    public void ElderSafeZoneHasHandCalculatedDirectionalNoise()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new ElderSafeZoneStops(2, 1))).Check(
            new IndicatorValidationContext("elder-safe-hand", bars, [[0, 1, 3]], 0));
    }

    [Fact]
    public void LiquidAndFisherLeastSquaresHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, i + 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new LiquidRelativeStrengthIndex(2))).Check(
            new IndicatorValidationContext("liquid-hand", bars, [[0, 100, 100d / 3]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new FisherLeastSquaresMovingAverage(2))).Check(
            new IndicatorValidationContext("fisher-least-hand", bars.Take(2).ToArray(), [[0, 3 + Math.Tanh(1)]], 0));
    }

    [Fact]
    public void RocketAndZeroMeanRoofingHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersRocketRelativeStrengthIndex(2, 1, 2, 1, new Wma()))).Check(
            new IndicatorValidationContext("rocket-hand", bars, [[0, Math.Log(1999) / 2, 0]], 0));
        var constant = bars.Select(b => new Bar(b.Time, 2, 2, 2, 2, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersZeroMeanRoofingFilter(48, 10))).Check(
            new IndicatorValidationContext("zero-mean-dc", constant, [[0, 0, 0]], 0));
    }

    [Fact]
    public void SuperTrendAndSniperHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new SuperTrend(1))).Check(
            new IndicatorValidationContext("supertrend-hand", bars, [[-4, -4, -4, -4]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new FXSniperIndicator(2, 1, 0))).Check(
            new IndicatorValidationContext("sniper-identity-hand", bars.Take(3).ToArray(), [[0, 200d / 3, -200d / 3]], 0));
    }

    [Fact]
    public void PaintPseudoAndVervoortBandsHaveHandCalculatedSeeds()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1) };
        var paint = BuiltInFormulaReferences.For(new LBRPaintBars(1, 1, 2.5)).ToArray();
        Assert.Equal(3, paint.Length);
        foreach (var rule in paint) rule.Check(new IndicatorValidationContext("paint-hand", bars, [[-2], [6], [5]], 0));
        var pseudo = BuiltInFormulaReferences.For(new PseudoPolynomialChannel(1, .9)).ToArray();
        Assert.Equal(3, pseudo.Length);
        foreach (var rule in pseudo) rule.Check(new IndicatorValidationContext("pseudo-hand", bars, [[0], [0], [0]], 0));
        var vervoort = BuiltInFormulaReferences.For(new VervoortVolatilityBands(1, 1, 2, .5)).ToArray();
        Assert.Equal(3, vervoort.Length);
        foreach (var rule in vervoort) rule.Check(new IndicatorValidationContext("vervoort-hand", bars, [[6], [2], [0]], 0));
    }

    [Fact]
    public void AdaptiveChangeSwamiAndHilbertHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new MovingAverageAdaptiveFilter(2, 1, 1, 1))).Check(
            new IndicatorValidationContext("adaptive-change-hand", bars, [[0, 1, 2]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new SwamiStochastics(1, 3))).Check(
            new IndicatorValidationContext("swami-hand", bars.Take(1).ToArray(), [[.1]], 0));
        var q = .4 * (.1759 * .396 + .4607);
        var hilbert = BuiltInFormulaReferences.For(new EhlersHilbertOscillator(7)).ToArray();
        Assert.Equal(2, hilbert.Length);
        foreach (var rule in hilbert) rule.Check(new IndicatorValidationContext("hilbert-osc-hand", bars.Take(1).ToArray(), [[1.57 * q], [1.25 * q]], 0));
    }

    [Fact]
    public void RecursiveAndFisherStochasticHaveHandCalculatedEndpoints()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new RecursiveStochastic(2, 1))).Check(
            new IndicatorValidationContext("recursive-stoch-hand", bars, [[0, 100, 0, 100]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new FisherTransformStochasticOscillator(1))).Check(
            new IndicatorValidationContext("fisher-stoch-initial", bars.Take(1).ToArray(), [[100 / (1 + Math.Exp(10))]], 0));
    }

    [Fact]
    public void MultiDepthAndSineReferencesHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new MorphedSineWave(4))).Check(
            new IndicatorValidationContext("sine-hand", bars, [[2, 4.01, 2, 7.99]], 0));
        var rules = BuiltInFormulaReferences.For(new MultiDepthZeroLagExponentialMovingAverage(1)).ToArray();
        Assert.Equal(3, rules.Length);
        rules[1].Check(new IndicatorValidationContext("one-pole-period-one", bars, [[], [2, 4, 2, 8], []], 0));
        // At period two, the one-pole correction has transfer function 2H-H^2, H=(2/3)/(1-z^-1/3).
        var onePole = BuiltInFormulaReferences.For(new MultiDepthZeroLagExponentialMovingAverage(2)).ToArray()[1];
        onePole.Check(new IndicatorValidationContext("one-pole-depth-hand", bars.Take(3).ToArray(), [[], [2, 34d / 9, 62d / 27], []], 0));
    }

    [Fact]
    public void SecondOrderEstimatorsHaveHandCalculatedStartup()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new KalmanSmoother(200))).Check(
            new IndicatorValidationContext("kalman-hand", bars, [[2, 2.44, 2.3832]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new IIRLeastSquaresEstimate(2))).Check(
            new IndicatorValidationContext("iir-hand", bars, [[8d / 3, 40d / 9, 74d / 27]], 0));
    }

    [Fact]
    public void VolatilityAdaptivePairsHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        foreach (IIndicator indicator in new IIndicator[] { new ChandeVolatilityIndexDynamicAverageIndicator(2, .2, .04), new VolatilityIndexDynamicAverageIndicator(2, .2, .04) })
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(2, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("volatility-adaptive-hand", bars.Take(3).ToArray(),
                [[2, 2.8, 2.608], [2, 2.16, 2.15232]], 0));
        }
        var uhl = BuiltInFormulaReferences.For(new UhlMaCrossoverSystem(2)).ToArray();
        Assert.Equal(2, uhl.Length);
        foreach (var rule in uhl) rule.Check(new IndicatorValidationContext("uhl-hand", bars, [[2, 4, 2, 47d / 6], [0, 3, 3, 4.5]], 0));
    }

    [Fact]
    public void VariableAveragesHaveHandCalculatedSeedsAndBandWidths()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, i == 1 ? .5 : 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new Vma(1))).Check(new IndicatorValidationContext("vma-single-window-hand", bars, [[2, 2, 2]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new Svama(1))).Check(new IndicatorValidationContext("svama-volume-hand", bars, [[2, 3, 2]], 0));
        var bands = BuiltInFormulaReferences.For(new VariableMovingAverageBands(1, 1.5)).ToArray();
        Assert.Equal(3, bands.Length);
        foreach (var rule in bands) rule.Check(new IndicatorValidationContext("variable-bands-hand", bars, [[5, 5, 5], [2, 2, 2], [-1, -1, -1]], 0));
        var ultimate = BuiltInFormulaReferences.For(new UltimateMovingAverageBands(1, 1, 2)).ToArray();
        Assert.Equal(3, ultimate.Length);
        foreach (var rule in ultimate) rule.Check(new IndicatorValidationContext("ultimate-bands-hand", bars, [[2, 4, 2], [2, 4, 2], [2, 4, 2]], 0));
    }

    [Fact]
    public void CycleNoiseAndTrendlineHaveHandCalculatedInitialValues()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1) };
        Assert.Single(BuiltInFormulaReferences.For(new EhlersAlternateSignalToNoiseRatio(6))).Check(
            new IndicatorValidationContext("alternate-noise-hand", bars, [[1.5]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersSignalToNoiseRatioV1(7))).Check(
            new IndicatorValidationContext("noise-v1-hand", bars, [[.475]], 0));
        var trend = BuiltInFormulaReferences.For(new EhlersInstantaneousTrendlineV1()).ToArray();
        Assert.Equal(2, trend.Length);
        foreach (var rule in trend) rule.Check(new IndicatorValidationContext("cycle-trend-hand", bars, [[2], [.8]], 0));
        var q = .4 * (.1759 * .396 + .4607);
        var real = 1.57 * q;
        var enhanced = BuiltInFormulaReferences.For(new EhlersEnhancedSignalToNoiseRatio(6)).ToArray();
        Assert.Equal(4, enhanced.Length);
        foreach (var rule in enhanced) rule.Check(new IndicatorValidationContext("enhanced-noise-hand", bars,
            [[.33 * 10 * Math.Log10((q * q + real * real) / .1)], [real], [q], [.396]], 0));
    }

    [Fact]
    public void AdaptiveV1OscillatorsHaveHandCalculatedInitialOutputs()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1) };
        var rsi = BuiltInFormulaReferences.For(new EhlersAdaptiveRelativeStrengthIndexV1(.5)).ToArray();
        var stochastic = BuiltInFormulaReferences.For(new EhlersAdaptiveStochasticIndicatorV1(.5)).ToArray();
        var cci = BuiltInFormulaReferences.For(new EhlersAdaptiveCommodityChannelIndexV1(1, .015)).ToArray();
        Assert.Equal(2, rsi.Length); Assert.Equal(2, stochastic.Length); Assert.Equal(2, cci.Length);
        foreach (var rule in rsi) rule.Check(new IndicatorValidationContext("adaptive-rsi-hand", bars, [[100], [99]], 0));
        foreach (var rule in stochastic) rule.Check(new IndicatorValidationContext("adaptive-stochastic-hand", bars, [[50], [49.5]], 0));
        foreach (var rule in cci) rule.Check(new IndicatorValidationContext("adaptive-cci-hand", bars, [[0], [0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersAdaptiveRsiFisherTransformV1())).Check(
            new IndicatorValidationContext("adaptive-fisher-hand", bars, [[Math.Log(1999) / 2]], 0));
    }

    [Fact]
    public void MamaHasEightHandCalculatedInitialOutputs()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 2, 2, 2, 1) };
        var rules = BuiltInFormulaReferences.For(new EhlersMesaAdaptiveMovingAverage(20, .5, .05)).ToArray();
        Assert.Equal(8, rules.Length);
        var q = .8 * .0962 * .54 * .0962 * .54;
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("mama-initial-hand", bars,
            [[.25], [1], [0], [q], [.396], [.8], [0], [0]], 0));
    }

    [Fact]
    public void PredictiveFiltersHaveHandCalculatedImpulseResponses()
    {
        var bars = new[] { 0d, 0, 0, 0, 0, 0, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var decay = Math.Sqrt(2) - 1;
        var rules = BuiltInFormulaReferences.For(new EhlersVossPredictiveFilter(2, 0, .25)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("voss-impulse-hand", bars,
            [[0, 0, 0, 0, 0, 0, 2.5 * (1 - decay)], [0, 0, 0, 0, 0, 0, 1 - decay]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersTruncatedBandPassFilter(2, 1, .25))).Check(
            new IndicatorValidationContext("truncated-impulse-hand", bars, [[0, 0, 0, 0, 0, 0, 1 - decay]], 0));
    }

    [Fact]
    public void SwissArmyFiltersHaveNineHandCalculatedStartupOutputs()
    {
        var bars = new[] { 2d, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new EhlersSwissArmyKnife(1, .1)).ToArray();
        Assert.Equal(9, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("swiss-startup-hand", bars,
            [[2, 4], [2, 4], [2, 4], [2, 4], [.5, 2], [0, 0], [0, 0], [2, 4], [2, 4]], 0));
    }

    [Fact]
    public void EstimatorAndGroverContractsHaveHandCalculatedSteps()
    {
        var bars = new[] { 2d, 4, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new GeneralFilterEstimator(6))).Check(
            new IndicatorValidationContext("delayed-estimator-hand", bars, [[2, 2, 5]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new GroverLlorensActivator(1, 1))).Check(
            new IndicatorValidationContext("grover-activator-hand", bars, [[2, -1, -6]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new GroverLlorensCycleOscillator(1))).Check(
            new IndicatorValidationContext("grover-cycle-hand", bars, [[100, 100, 100]], 0));
    }

    [Fact]
    public void AdaptiveAndRankFiltersHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new OscarIndicator(1))).Check(
            new IndicatorValidationContext("oscar-hand", bars, [[50d / 3, 175d / 9, 1075d / 54]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new KarobeinOscillator(1))).Check(
            new IndicatorValidationContext("karobein-hand", bars, [[0, 1, 0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new ModularFilter(1, .8, .5))).Check(
            new IndicatorValidationContext("modular-hand", bars, [[2, 4, 2]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new MovingAverageAdaptiveQ(1))).Check(
            new IndicatorValidationContext("adaptive-q-hand", bars.Take(2).ToArray(), [[2, 2 + 2 * .7315 * .7315]], 0));
    }

    [Fact]
    public void RangeAndSmoothingCompositesHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new TillsonIE2(1))).Check(
            new IndicatorValidationContext("tillson-hand", bars, [[3, 5, 1]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new TheRangeIndicator(2, 3))).Check(
            new IndicatorValidationContext("range-hand", bars, [[0, 0, 200d / 3]], 0));
        var turbo = BuiltInFormulaReferences.For(new TurboTrigger(1, 1)).ToArray();
        Assert.Equal(2, turbo.Length);
        foreach (var rule in turbo) rule.Check(new IndicatorValidationContext("turbo-hand", bars, [[0, 1, 1], [0, 0, 0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new TradingMadeMoreSimplerOscillator(1))).Check(
            new IndicatorValidationContext("trading-hand", bars.Take(1).ToArray(), [[0]], 0));
    }

    [Fact]
    public void PriceStepContractsHaveHandCalculatedMoves()
    {
        var bars = new[] { 2d, 4, 3, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new TrendImpulseFilter(2, 1))).Check(
            new IndicatorValidationContext("trend-step-hand", bars, [[2, 4, 4, 8]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new RetrospectiveCandlestickChart(2))).Check(
            new IndicatorValidationContext("retrospective-hand", bars, [[2, 2, 2, 8]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new SettingLessTrendStepFiltering(1))).Check(
            new IndicatorValidationContext("settingless-hand", bars.Take(3).ToArray(), [[2, 2, 2]], 0));
    }

    [Fact]
    public void RsiCompositeContractsHaveHandCalculatedValues()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var dynamic = BuiltInFormulaReferences.For(new TradersDynamicIndex(1, 1, 1, 1)).ToArray();
        Assert.Equal(5, dynamic.Length);
        foreach (var rule in dynamic) rule.Check(new IndicatorValidationContext("traders-dynamic-hand", bars,
            [[100, 100, 0], [100, 100, 0], [100, 100, 0], [100, 100, 0], [100, 100, 0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new ReverseEngineeringRsi(2, 50))).Check(
            new IndicatorValidationContext("reverse-rsi-hand", bars, [[2, 3, 2.5]], 0));
        var flat = bars.Take(2).ToArray();
        var wave = BuiltInFormulaReferences.For(new RahulMohindarOscillator(10)).ToArray();
        Assert.Equal(4, wave.Length);
        foreach (var rule in wave) rule.Check(new IndicatorValidationContext("rahul-flat-hand", flat, [[0, 85.0146484375], [0, 170.029296875], [0, 85.0146484375], [0, 42.50732421875]], 0));
    }

    [Fact]
    public void HistoricalVolatilityRanksHaveHandCalculatedEvictions()
    {
        var bars = new[] { 1d, 2, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new HistoricalVolatilityPercentile(2, 2)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("historical-ranks-hand", bars,
            [[0, 50, 50, 0], [0, 25, 125d / 3, 125d / 9]], 0));
    }

    [Fact]
    public void KaufmanBinaryWaveHasHandCalculatedTurningLevels()
    {
        var bars = new[] { 2d, 4, 1, 3 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new KaufmanBinaryWave(1, 1, 0, 0))).Check(
            new IndicatorValidationContext("kaufman-wave-hand", bars, [[1, 1, -1, 1]], 0));
    }

    [Fact]
    public void QqeWidthsHaveHandCalculatedRsiMovements()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new QuantitativeQualitativeEstimation(1, 1, 2, 3)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("qqe-widths-hand", bars, [[200, 0, 200], [300, 0, 300]], 0));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(49, false)]
    [InlineData(121, false)]
    [InlineData(2147483647, true)]
    public void PrimeSearchUsesTheCandidatesOwnDivisors(long candidate, bool expected)
        => Assert.Equal(expected, OoplesFinance.StockIndicators.Helpers.PrimeNumberSearch.IsPrime(candidate));

    [Fact]
    public void PrimeOffsetsAndBandsHaveHandCalculatedPrimeAndSquareCases()
    {
        var bars = new[] { 2d, 3, 4, 9, 49, 121 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new PrimeNumberOscillator(100))).Check(
            new IndicatorValidationContext("prime-offsets-hand", bars, [[0, 0, -1, -2, -2, 6]], 0));
        var rules = BuiltInFormulaReferences.For(new PrimeNumberBands(100)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("prime-bands-hand", bars,
            [[0, 0, 0, 0, 0, 6], [0, 0, -1, -2, -2, -2]], 0));
    }

    [Fact]
    public void SuperTrendFilterUsesPreviousBandsToChooseItsSide()
    {
        var bars = new[] { 2d, 4, 1, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new SuperTrendFilter(1, 0))).Check(
            new IndicatorValidationContext("super-trend-filter-hand", bars, [[2, 4, 7, 7]], 0));
    }

    [Fact]
    public void PeriodicChannelHasEightHandCalculatedOutputs()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new PeriodicChannel(500, 1)).ToArray();
        Assert.Equal(8, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("periodic-channel-hand", bars,
            [[0, 6, 10], [0, 4, 4], [0, 10, 14], [0, 14, 18], [0, 18, 22], [0, 2, 6], [0, -2, 2], [0, -6, -2]], 0));
    }

    [Fact]
    public void VolatilityStopVixAndVostroHaveIndependentHandCalculations()
    {
        var bars = new[] { 2d, 4, 1, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new VolatilityStop(1, 1))).Check(
            new IndicatorValidationContext("volatility-stop-hand", bars, [[2, 2, 5, 0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new VixTradingSystem(1))).Check(
            new IndicatorValidationContext("vix-counter-hand", bars, [[-1, -2, -3, -4]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new VostroIndicator(1, 1, 8))).Check(
            new IndicatorValidationContext("vostro-hand", bars, [[90, 0, 0, 0]], 0));
    }

    [Fact]
    public void DirectionalCompositesHaveIndependentHandCalculations()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var scaled = 300 * 100 * 50 / (Math.Sqrt(3000) * 160);
        var rules = BuiltInFormulaReferences.For(new CommoditySelectionIndex(1)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("commodity-selection-hand", bars,
            [[0, scaled, scaled], [0, scaled, scaled]], 0));
        var ergodic = BuiltInFormulaReferences.For(new ErgodicCommoditySelectionIndex(1, 1, 1)).ToArray();
        Assert.Equal(2, ergodic.Length);
        foreach (var rule in ergodic) rule.Check(new IndicatorValidationContext("ergodic-selection-hand", bars,
            [[0, 3750d / 151, 15000d / 151], [0, 3750d / 151, 15000d / 151]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new DynamicMomentumOscillator(1))).Check(
            new IndicatorValidationContext("dynamic-momentum-hand", bars, [[100, 100, 100]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new DirectionalTrendIndex(1))).Check(
            new IndicatorValidationContext("directional-trend-opening", bars.Take(1).ToArray(), [[100]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new DMIStochastic(1))).Check(
            new IndicatorValidationContext("dmi-stochastic-opening", bars, [[0, 0, 100d / 9]], 0));
    }

    [Fact]
    public void OneSampleAutocorrelationCannotProduceReversals()
    {
        var bars = new[] { 2d, 4, 1, 7 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersAutoCorrelationReversals(1, 1, 1))).Check(
            new IndicatorValidationContext("autocorrelation-reversals-unit", bars, [[0, 0, 0, 0]], 0));
    }

    [Fact]
    public void AdaptiveLaguerreSeedsItsStagesAtTheOpeningPrice()
    {
        var bars = new[] { 2d, 2, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersAdaptiveLaguerreFilter(14))).Check(
            new IndicatorValidationContext("adaptive-laguerre-flat", bars, [[2, 2, 2]], 0));
        var rising = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersAdaptiveLaguerreFilter(1))).Check(
            new IndicatorValidationContext("adaptive-laguerre-unit-window", rising, [[2, 7d / 3, 10d / 3]], 0));
    }

    [Fact]
    public void AdaptiveCyberStartsWithASecondDifferenceAndASeededPeriod()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 2, 2, 2, 1) };
        var initialPeriod = .15 * .33 * (6.28318 / .1 + .5);
        var rules = BuiltInFormulaReferences.For(new EhlersAdaptiveCyberCycle()).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("adaptive-cyber-opening", bars,
            [[.5], [initialPeriod]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersAdaptiveCenterOfGravityOscillator())).Check(
            new IndicatorValidationContext("adaptive-gravity-opening", bars, [[0]], 0));
    }

    [Fact]
    public void SquelchCannotMeasureACycleBeforeSixPhaseAdvances()
    {
        var bars = new[] { 2d, 4, 1, 7, 3 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersSquelchIndicator(1, 1, 40))).Check(
            new IndicatorValidationContext("squelch-opening", bars, [[0, 0, 0, 0, 0]], 0));
    }

    [Fact]
    public void DeviationAverageAndFisherTransformHaveHandCalculatedStartup()
    {
        var bars = new[] { 2d, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var angle = Math.Sqrt(2) * Math.PI / 12;
        var radius = Math.Exp(-angle);
        Assert.Single(BuiltInFormulaReferences.For(new EhlersDeviationScaledSuperSmoother(12, 2))).Check(
            new IndicatorValidationContext("deviation-super-hand", bars.Take(1).ToArray(),
                [[1 - 2 * radius * Math.Cos(angle) + radius * radius]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersDeviationScaledMovingAverage())).Check(
            new IndicatorValidationContext("deviation-average-hand", bars, [[.02, .0598]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersFisherizedDeviationScaledOscillator())).Check(
            new IndicatorValidationContext("fisher-deviation-hand", bars, [[Math.Atanh(.01), Math.Atanh(.0299)]], 0));
    }

    [Fact]
    public void EmpiricalModeDecompositionStartsWithZeroTrendAndExtrema()
    {
        var bars = new[] { 2d, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new EhlersEmpiricalModeDecomposition()).ToArray();
        Assert.Equal(3, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("decomposition-hand", bars, [[0, 0], [0, 0], [0, 0]], 0));
    }

    [Fact]
    public void HurstAndRecursiveMedianHaveIndependentOpeningCalculations()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 2, 2, 2, 1) };
        var radius = Math.Exp(-Math.Sqrt(2) * Math.PI / 20);
        var gain = 1 - 2 * radius * Math.Cos(Math.Sqrt(2) * Math.PI / 20) + radius * radius;
        Assert.Single(BuiltInFormulaReferences.For(new EhlersHurstCoefficient())).Check(
            new IndicatorValidationContext("hurst-hand", bars, [[gain]], 0));
        var p1 = Math.Cos(2 * Math.PI / 12) / (1 + Math.Sin(2 * Math.PI / 12));
        var p2 = Math.Cos(Math.Sqrt(2) * Math.PI / 30) / (1 + Math.Sin(Math.Sqrt(2) * Math.PI / 30));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersRecursiveMedianOscillator())).Check(
            new IndicatorValidationContext("recursive-median-hand", bars, [[2 * (1 - p1) * Math.Pow((1 + p2) / 2, 2)]], 0));
    }

    [Fact]
    public void DemodulatorSineWaveAndMarketStateHaveIndependentHandCalculations()
    {
        var bars = new[] { 2d, 4, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, Math.Max(2, v), Math.Min(2, v), v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersFMDemodulatorIndicator(2, 1, maType: new Wma(1)))).Check(
            new IndicatorValidationContext("demodulator-hand", bars, [[0, 1, -1]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersEvenBetterSineWaveIndicator())).Check(
            new IndicatorValidationContext("sine-wave-hand", bars.Take(2).ToArray(), [[0, 1 / Math.Sqrt(3)]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersMarketStateIndicator(1))).Check(
            new IndicatorValidationContext("market-state-hand", bars, [[0, 1, 1]], 0));
    }

    [Fact]
    public void ModifiedRsiNormalizesTheWholeGainWindowBeforeFiltering()
    {
        var bars = Enumerable.Range(0, 2).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 2, 2, 2, 1)).ToArray();
        var radius = Math.Exp(-Math.Sqrt(2) * Math.PI / 10);
        var gain = 1 - 2 * radius * Math.Cos(Math.Sqrt(2) * Math.PI / 10) + radius * radius;
        var rules = BuiltInFormulaReferences.For(new EhlersModifiedRelativeStrengthIndex()).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("modified-rsi-hand", bars,
            [[0, gain], [0, gain * gain / 2]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersModifiedStochasticIndicator(1, 2, 2))).Check(
            new IndicatorValidationContext("modified-stochastic-degenerate", bars, [[0, 0]], 0));
    }

    [Fact]
    public void OscillatorInverseFisherTransformsHaveIndependentHandCalculations()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var limit = Math.Tanh(5);
        Assert.Single(BuiltInFormulaReferences.For(new EhlersRelativeStrengthIndexInverseFisherTransform(1, 1))).Check(
            new IndicatorValidationContext("rsi-inverse-hand", bars, [[limit, limit, -limit]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersCommodityChannelIndexInverseFisherTransform(1, 1))).Check(
            new IndicatorValidationContext("cci-inverse-hand", bars, [[-limit, -limit, -limit]], 0));
    }

    [Fact]
    public void GravityAndImpulseFiltersHaveIndependentOpeningCalculations()
    {
        var bars = new[] { 2d, 4, 6, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersStochasticCenterOfGravityOscillator(1))).Check(
            new IndicatorValidationContext("gravity-trigger-hand", bars, [[.0192, 0, 0, 0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersImpulseReaction(1, 2, 0))).Check(
            new IndicatorValidationContext("reaction-hand", bars, [[50, 25, 100d / 6, 12.5]], 0));
        var decay = Math.Cos(.99) / (1 + Math.Sin(.99));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersImpulseResponse(1))).Check(
            new IndicatorValidationContext("impulse-response-hand", bars, [[0, 0, 0, 1 - decay]], 0));
    }

    [Fact]
    public void BandPassAndCyberCycleTriggersHaveIndependentOpeningCalculations()
    {
        var rising = new[] { 2d, 4, 6, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var angle = 1.5 * .3 * 2 * Math.PI / 20;
        var triggerGain = 1 + (Math.Cos(angle) + Math.Sin(angle) - 1) / (2 * Math.Cos(angle));
        var band = BuiltInFormulaReferences.For(new EhlersBandPassFilterV1()).ToArray();
        Assert.Equal(2, band.Length);
        foreach (var rule in band) rule.Check(new IndicatorValidationContext("bandpass-hand", rising,
            [[0, 0, 0, 1], [0, 0, 0, triggerGain]], 0));
        var prices = new[] { 2d, 4, 1, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var cyber = BuiltInFormulaReferences.For(new EhlersStochasticCyberCycle(1)).ToArray();
        Assert.Equal(2, cyber.Length);
        foreach (var rule in cyber) rule.Check(new IndicatorValidationContext("cyber-stoch-hand", prices,
            [[-1, -1, -1, -.2], [.0192, -.9408, -.9408, -.9408]], 0));
    }

    [Fact]
    public void EarlyOnsetUsesItsNormalizedPeakAndOriginalRoofingHasZeroResponseToZero()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 2, 2, 2, 1) };
        Assert.Single(BuiltInFormulaReferences.For(new EhlersEarlyOnsetTrendIndicator())).Check(
            new IndicatorValidationContext("onset-positive-opening", bars, [[1]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersEarlyOnsetTrendIndicator(1, 1))).Check(
            new IndicatorValidationContext("onset-degenerate", bars, [[.85]], 0));
        var zero = new[] { new Bar(DateTime.UnixEpoch, 0, 0, 0, 0, 0) };
        Assert.Single(BuiltInFormulaReferences.For(new EhlersRoofingFilterIndicator())).Check(
            new IndicatorValidationContext("original-roofing-zero", zero, [[0]], 0));
    }

    [Fact]
    public void RoofingRejectsTheDegenerateHighPassAndStochasticStartsAtZero()
    {
        var bars = new[] { 2d, 4, 1, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        foreach (var indicator in new IIndicator[] { new EhlersRoofingFilter(1, 2), new EhlersRoofingFilterV1(1, 2) })
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(
                new IndicatorValidationContext("roofing-degenerate", bars, [[0, 0, 0, 0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersStochastic(2))).Check(
            new IndicatorValidationContext("stochastic-opening", bars.Take(3).ToArray(), [[0, .5, .5]], 0));
    }

    [Fact]
    public void ReverseEmaPolynomialAndRoofingHaveIndependentOpeningValues()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 2, 2, 2, 1) };
        const double alpha = .1;
        var opening = 2 * alpha * (1 - alpha * Math.Pow(1 - alpha, 255));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersReverseEmaIndicatorV1(alpha))).Check(
            new IndicatorValidationContext("reverse-ema-hand", bars, [[opening]], 0));
        var both = BuiltInFormulaReferences.For(new EhlersReverseEmaIndicatorV2(alpha, alpha)).ToArray();
        Assert.Equal(2, both.Length);
        foreach (var rule in both) rule.Check(new IndicatorValidationContext("reverse-waves-hand", bars, [[opening], [opening]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersHpLpRoofingFilter())).Check(
            new IndicatorValidationContext("roofing-opening", bars, [[0]], 0));
    }

    [Fact]
    public void SupportUberAndKwanHaveIndependentHandCalculations()
    {
        var rising = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new SupportAndResistanceOscillator(1))).Check(
            new IndicatorValidationContext("support-hand", rising, [[.5, 1d / 3, 1d / 3]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new KwanIndicator(1, 1))).Check(
            new IndicatorValidationContext("kwan-hand", rising, [[0, 0, 25]], 0));
        var mixed = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new UberTrendIndicator(2))).Check(
            new IndicatorValidationContext("uber-hand", mixed, [[-1, -1, 0]], 0));
    }

    [Fact]
    public void SerialDependencyPriceCycleAndPhaseHaveIndependentHandCalculations()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var serial = BuiltInFormulaReferences.For(new KaseSerialDependencyIndex(2)).ToArray();
        Assert.Equal(2, serial.Length);
        var deviation = Math.Log(4d / 3) / 2;
        foreach (var rule in serial) rule.Check(new IndicatorValidationContext("serial-hand", bars,
            [[0, 0, Math.Log(7) / deviation], [0, 0, Math.Log(5d / 3) / deviation]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new PriceCycleOscillator(1))).Check(
            new IndicatorValidationContext("price-cycle-hand", bars, [[50, 100d / 3, 100d / 3]], 0));
        var phase = BuiltInFormulaReferences.For(new PhaseChangeIndex(2, 1)).ToArray();
        Assert.Equal(2, phase.Length);
        foreach (var rule in phase) rule.Check(new IndicatorValidationContext("phase-hand", bars, [[0, 100, 0], [0, 100, 0]], 0));
    }

    [Fact]
    public void KasePeakAndConvergenceHaveIndependentHandCalculations()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new KasePeakOscillatorV1(1, 1)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("kase-peak-hand", bars,
            [[2.08, 2.08, 2.08], [2, 4d / 3, 4d / 3]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new KaseConvergenceDivergence(1, 1, 2))).Check(
            new IndicatorValidationContext("kase-convergence-hand", bars, [[2, -1d / 3, 0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new KasePeakOscillatorV2(1))).Check(
            new IndicatorValidationContext("kase-volatility-warmup", bars, [[0, 0, 0]], 0));
    }

    [Fact]
    public void FirstKaseStopsAndMultiLevelHaveIndependentHandCalculations()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new KaseDevStopV1(1, 2, 1)).ToArray();
        Assert.Equal(4, rules.Length);
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("kase-v1-hand", bars,
            [[-1, -1, 0], [-1, -1, 0], [-1, -1, 0], [-1, -1, 0]], 0));
        Assert.Single(BuiltInFormulaReferences.For(new MultiLevelIndicator(1, 2))).Check(
            new IndicatorValidationContext("multi-level-hand", bars, [[-4, -4, -4]], 0));
    }

    [Fact]
    public void CalmarReferenceMeasuresRollingDrawdownWithItsPublishedAnnualization()
    {
        var bars = new[] { 2d, 1, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new CalmarRatio(2)).ToArray();
        Assert.Single(rules);
        double[][] expected = [[0, 0, 2 * (1d / 4096 - 1)]];
        rules[0].Check(new IndicatorValidationContext("calmar-hand", bars, expected, 0));
    }

    [Fact]
    public void RoofingKernelPreservesItsImpulseGainAndAnnihilatesNyquistWithoutARoundingFloor()
    {
        var kernel = new EhlersRoofingFilterV2Kernel(24, 5);
        var pole = Math.Cos(Math.Sqrt(2) * Math.PI / 24) / (1 + Math.Sin(Math.Sqrt(2) * Math.PI / 24));
        var radius = Math.Exp(-Math.Sqrt(2) * Math.PI / 5);
        var gain = 1 - 2 * radius * Math.Cos(Math.Sqrt(2) * Math.PI / 5) + radius * radius;
        var first = gain * pole * pole / 8;
        Assert.Equal(first, kernel.Next(1, false), 14);
        Assert.Equal(first, kernel.Next(1, true), 14);
        kernel.Reset();
        double last = 0;
        for (var i = 0; i < 512; i++)
        {
            var value = i % 2 == 0 ? 80 : 120;
            var preview = kernel.Next(value, false);
            last = kernel.Next(value, true);
            Assert.Equal(preview, last);
        }
        Assert.InRange(Math.Abs(last), 0, 1e-30);
        kernel.Reset();
        Assert.Equal(first, kernel.Next(1, true), 14);
    }

    [Fact]
    public void AutocorrelationReferenceRecognizesAlignedOpposedAndDegenerateWindows()
    {
        Check([1, 2, 3, 4], 2, [0, 0, 1, 1]);
        Check([1, 2, 1, 0], 2, [0, 0, 0, 0]);
        Check([1, 2, 3], 1, [0, 0, 0]);
        void Check(double[] values, int period, double[] expected)
        {
            var actual = BuiltInFormulaReferences.AutocorrelationTrajectory(values, period);
            for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], actual[i], 12);
        }
    }

    [Fact]
    public void ResidualBandsExposeTheirHandCalculatedWidths()
    {
        var bars = new[] { 1d, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Check(new VortexBands(2, new Sma()), bars, [0, 3, 4.5], [0, 1.5, 3], [0, 0, 1.5]);
        Check(new HirashimaSugitaRS(2), bars.Take(2).ToArray(), [1, 7d / 3], [1, 8d / 3], [1, 2], [1, 5d / 3], [1, 4d / 3]);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("residual-band-hand", input, expected, 0));
        }
    }

    [Fact]
    public void HurstAndHighLowBandsHaveHandCalculatedCentersIncludingRealZeroPrices()
    {
        var one = new[] { new Bar(DateTime.UnixEpoch, 1, 3, 1, 2, 1) };
        Check(new HighLowMovingAverage(1), one, [3], [2], [1]);
        Check(new HurstCycleChannel(4, 4), one, [3], [5], [2], [2], [1], [-1], [.5], [.5]);
        var bars = new[] { 2d, 0, 6, 0, 0 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        double[] center = [0, 0, 1, 1, 3];
        Check(new HurstBands(2, 10, 20, 30), bars, center.Select(v => v * 1.3).ToArray(), center.Select(v => v * 1.2).ToArray(),
            center.Select(v => v * 1.1).ToArray(), center, center.Select(v => v * .7).ToArray(), center.Select(v => v * .8).ToArray(), center.Select(v => v * .9).ToArray());
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("hurst-displaced-mean", input, expected, 0));
        }
    }

    [Fact]
    public void UltimateTraderAllBullishScoresReachThePositiveEndpoint()
    {
        var bars = Enumerable.Range(0, 15).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 2, i + 1, i + 2, 1)).ToArray();
        // WMA5 of a constant 100, then WMA4 twice, has these first two startup values.
        var rules = BuiltInFormulaReferences.For(new UltimateTraderOscillator(10)).ToArray();
        Assert.Equal(2, rules.Length);
        double[][] expected = [[40d / 3, 34], [16d / 3, 88d / 5]];
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("ultimate-bull-endpoint", bars.Take(2).ToArray(), expected, 0));
    }

    [Fact]
    public void PriceCoordinateReferencesHaveHandCalculatedCentersAndDelayedWidths()
    {
        Check(new ValueChartIndicator(1, 8), [new Bar(DateTime.UnixEpoch, 1, 3, 1, 2, 1)], [0], [-12.5], [12.5], [-12.5]);
        Check(new WilsonRelativePriceChannel(1), [new Bar(DateTime.UnixEpoch, 10, 10, 10, 10, 1)], [3], [4.5], [7], [5.5]);
        var bars = new[] { 1d, 1, 2, 2, 2, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Check(new TimeAndMoneyChannel(1, 2), bars, [1, 1, 2, 2, 3, 2], [1, 1, 2, 2, 1, 2],
            [1, 1, 2, 2, 4, 2], [1, 1, 2, 2, 0, 2], [1, 1, 2, 2, 5, 2], [1, 1, 2, 2, -1, 2], [0, 0, 0, 0, 50, 0]);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("price-coordinate-hand", input, expected, 0));
        }
    }

    [Fact]
    public void PressureAndMovementReferencesHaveHandCalculatedResponses()
    {
        var bars = new[] { 1d, 2, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v, v, 1)).ToArray();
        Check(new TraderPressureIndex(1, 1, 1), bars, [100, 100, -100], [100, 100, 0], [0, 0, 100]);
        Check(new TrendTriggerFactor(1), bars, [600, 200, -200]);
        var rising = new[] { 1d, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Check(new StrengthOfMovement(1, 2), rising, [-50, 100d / 6, -100d / 3]);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("pressure-movement-hand", input, expected, 0));
        }
    }

    [Fact]
    public void TurboStochasticFitsRawAndSmoothedSeriesSeparately()
    {
        var prices = new[] { 0d, 1, 0 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, 1, 0, v, 1)).ToArray();
        Check(new TurboStochasticsFast(2, 2, 0), [0, 100, 0], [0, 50, 50]);
        Check(new TurboStochasticsSlow(2, 2, 0), [0, 50, 50], [0, 25, 50]);
        Check(new StochasticCustomOscillator(2), [0, 0, 100d / 3], [0, 0, 0]);
        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("stochastic-smoothing-order", bars, expected, 0));
        }
    }

    [Fact]
    public void EhlersCorrelationUsesAFullCycleAndResolvesAxisAlignedAngles()
    {
        var bars = new[] { 1d, 0, -1, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Check(new EhlersCorrelationCycleIndicator(4), [Math.Sqrt(2d / 3), 0, -1, 0, 1], [0, -Math.Sqrt(2d / 3), 0, 1, 0]);
        Check(new EhlersCorrelationAngleIndicator(4), [0, 90, 180, -90, 0]);
        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("correlation-quarter-cycle", bars, expected, 0));
        }
    }

    [Fact]
    public void RainbowStartupHasTenGeometricLayersAndFireflyFlatInputHasItsDefinedMidpoint()
    {
        var bars = new[] { 1d, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Check(new RainbowOscillator(2, new Sma()), bars, [0, 170.029296875], [0, 149.70703125], [0, -149.70703125]);
        var flat = bars.Select(b => new Bar(b.Time, 1, 1, 1, 1, 1)).ToArray();
        Check(new FireflyOscillator(1), flat, [46, 46], [46, 46]);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("rainbow-firefly-hand", input, expected, 0));
        }
    }

    [Fact]
    public void PhaseReferenceResolvesAllFourQuadrantsAndSmoothsTheSignal()
    {
        var bars = new[] { 1d, 0, -1, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new EhlersPhaseCalculation(4, new Sma())).ToArray();
        Assert.Equal(2, rules.Length);
        double[][] expected = [[90, 180, 270, 0, 90], [0, 0, 0, 135, 135]];
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("phase-quadrants", bars, expected, 0));
    }

    [Fact]
    public void UniversalTradingAndTrendFiltersHaveHandCalculatedFirstResponses()
    {
        var bars = new[] { 1d, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Check(new EhlersUniversalTradingFilter(1, 2, 2), bars, [1, 2, 3], [1, Math.Sqrt(2.5), Math.Sqrt(6.5)], [-1, -Math.Sqrt(2.5), -Math.Sqrt(6.5)]);
        var decay = Math.Tan(Math.PI / 4 - .99 / 2);
        var first = 1.5 * (1 - decay);
        Check(new EhlersTrendExtraction(1, 1), bars, [0, 0, first / 2], [0, 0, first]);
        var four = bars.Append(new Bar(DateTime.UnixEpoch.AddMinutes(3), 8, 8, 8, 8, 1)).ToArray();
        var snake = 3 * (1 - decay);
        Check(new EhlersSnakeUniversalTradingFilter(1, 2, 1), four,
            [0, 0, 0, snake / Math.Sqrt(2)], [0, 0, 0, snake], [0, 0, 0, -snake / Math.Sqrt(2)]);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("trading-filter-startup", input, expected, 0));
        }
    }

    [Fact]
    public void KlingerEmaDifferencePreservesSmallResponsesOnLargeConstantForce()
    {
        var state = new KlingerEmaDifference(51, 55);
        for (var i = 0; i < 200; i++) Assert.Equal(0, state.Next(1e7, true));
        for (var age = 1; age <= 100; age++)
        {
            var expected = Math.Pow(1 - 2d / 56, age) - Math.Pow(1 - 2d / 52, age);
            Assert.InRange(Math.Abs(expected - state.Next(1e7 + 1, false)), 0, 1e-9);
            Assert.InRange(Math.Abs(expected - state.Next(1e7 + 1, true)), 0, 1e-9);
        }
        state.Reset();
        Assert.Equal(0, state.Next(1e7, true));
    }

    [Fact]
    public void EhlersFilterReferencesExposeTheirDelayImpulseEnergyAndNormalizedSignal()
    {
        var bars = new[] { 1d, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Check(new EhlersUniversalOscillator(20), bars, [0, 0, 1], [0, 0, 1d / 3]);
        Check(new EhlersSuperPassbandFilter(2, 4, 1, 2), bars, [.25, .5625, 1.109375],
            [.25, Math.Sqrt(97d / 512), Math.Sqrt(6337d / 8192)], [-.25, -Math.Sqrt(97d / 512), -Math.Sqrt(6337d / 8192)]);
        var impulse = Enumerable.Range(0, 7).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i == 0 ? 1 : 0,
            i == 0 ? 1 : 0, i == 0 ? 1 : 0, i == 0 ? 1 : 0, 1)).ToArray();
        Check(new EhlersTripleDelayLineDetrender(1, new Sma()), impulse, [1, 0, 0, 0, 0, 0, -1.712], [1, 0, 0, 0, 0, 0, -1.712]);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("ehlers-delay-energy-peak", input, expected, 0));
        }
    }

    [Fact]
    public void KlingerMeasuresChangesInTheWholeBarEvenWhenCloseIsFlat()
    {
        var bars = Enumerable.Range(0, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 12 + i, 8 + i, 10, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(new KlingerVolumeOscillator(1, 2, 2)).ToArray();
        Assert.Equal(3, rules.Length);
        double[][] expected = [[0, 0, 100d / 9], [0, 0, 200d / 27], [0, 0, 100d / 27]];
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("klinger-flat-close-rising-range", bars, expected, 0));
    }

    [Fact]
    public void VolumeDisparityUsesTheSameBandCoordinateForProportionalSeries()
    {
        var date = new DateTime(2021, 1, 4);
        var prices = new[] { 1d, 2, 4 };
        Check(new OnBalanceVolumeDisparityIndicator(2, 2), [1, 1, 2]);
        Check(new NegativeVolumeDisparityIndicator(2, 2), [3, 2, 1]);
        void Check(IIndicator indicator, double[] volumes)
        {
            var bars = prices.Select((v, i) => new Bar(date, v, v, v, v, volumes[i])).ToArray();
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(2, rules.Length);
            double[][] expected = [[1, 1, 1], [0, 1, 1]];
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("proportional-band-coordinates", bars, expected, 0));
        }
    }

    [Fact]
    public void VolumeFlowCutoffAndTradeTickThresholdHaveHandCalculatedEffects()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 100d, 200, 201, 200 }.Select(v => new Bar(date, v, v, v, v, 10)).ToArray();
        Check(new VolumeFlowIndicator(2, 2, 1, 1, 0), bars, [0, 0, 1, 0], [0, 0, 1, 0], [0, 0, 0, 0]);
        Check(new VolumeFlowIndicator(2, 2, 1, 1, 100), bars, [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]);
        var ticks = new[] { 1d, 1.5, 2.25, 1 }.Select(v => new Bar(date, v, v, v, v, 10)).ToArray();
        Check(new TradeVolumeIndex(2), ticks, [10, 10, 20, 10], [0, 10, 15, 15]);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("volume-thresholds", input, expected, 0));
        }
    }

    [Fact]
    public void VolumeAndRangeReferencesDistinguishSignedFlowStartupAndBandFeedback()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(date, 0, v + 1, v - 1, v, 1)).ToArray();
        Check(new UltimateVolatilityIndicator(2), bars, [.5, 1.5, 3]);
        Check(new VolatilityRatio(2), bars, [0, 1, 1.5]);
        Check(new VolumeAdaptiveBands(1), bars, [2, 4, 8], [1, 3, 5], [0, 2, 2]);
        var reversing = new[] { 1d, 2, 1 }.Select(v => new Bar(date, v, v, v, v, 1)).ToArray();
        Check(new UpsideDownsideVolume(2), reversing, [0, 0, -1]);
        var weighted = Enumerable.Range(1, 20).Select(v => new Bar(date, v, v, v, v, v)).ToArray();
        var line = new double[20];
        var signal = new double[20];
        line[19] = 3097d / 567;
        signal[19] = line[19] / 2;
        Check(new Vpci(2), weighted, line, signal);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("volume-and-range", input, expected, 0));
        }
    }

    [Fact]
    public void EventAndDistanceReferencesHaveExplicitAgeResidualAndCrossingExamples()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(date, v, v, v, v, 1)).ToArray();
        Check(new DrunkardWalk(3), bars, [0, 1, Math.Sqrt(2)], [0, 0, 0]);
        Check(new ZDistanceFromVwap(2), bars, [0, Math.Sqrt(2), Math.Sqrt(8d / 5)]);
        var crossing = new[] { 80d, 100, 50, 120 }.Select(v => new Bar(date, v, v, v, v, 1)).ToArray();
        Check(new UtBotAlerts(1), crossing, [80, 80, 100, 50], [0, 0, -1, 1], [0, 1, 0, 1], [0, 0, 1, 0]);
        Check(new WellesWilderVolatilitySystem(1, 1, 1), crossing, [80, 100, 100, 120]);
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("events-and-distance", input, expected, 0));
        }
    }

    [Fact]
    public void FourOscillatorReferencesSeparateDifferenceAndPercentageDenominators()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Check(new _4MovingAverageConvergenceDivergence(1, 1, 2, 2, 1, 1, 4.3, 1.4, new Sma()),
            [1, .5, 1], [1, .5, 1], [0, 0, 0], [-1, -.5, -1], [-1, -.5, -1], [0, 0, 0]);
        Check(new _4PercentagePriceOscillator(1, 1, 2, 2, 1, 1, 4.3, 1.4, new Sma()),
            [0, 100d / 3, 100d / 3], [0, 100d / 3, 100d / 3], [0, 0, 0],
            [-100, -25, -25], [-100, -25, -25], [0, 0, 0]);
        Check(new WaddahAttarExplosion(1, 2, 1), [0, 0, 1d / 3], [0, 0, 1d / 3], [0, 0, 0], [0, 0, 1d / 3], [0, 0, 0]);
        Check(new TrendForceHistogram(2), [0, .25, .5]);
        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("four-oscillators", bars, expected, 0));
        }
    }

    [Fact]
    public void MacdVariantsHaveDistinctHandCalculatedStartupAndMirrorValues()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(date, 0, v, v, v, 100)).ToArray();
        Check(new DiNapoliMovingAverageConvergenceDivergence(3, 1, 3),
            [1, 2, 4], [.5, 1.25, 2.625], [.5, .75, 1.375], [.25, .5, .9375], [.25, .25, .4375]);
        Check(new MovingAverageConvergenceDivergenceLeader(1, 2, 2, new Sma()),
            [1, -.25, .25], [1, 2, 4], [0, 2.25, 3.75]);
        Check(new MirroredMovingAverageConvergenceDivergence(1, 2, new Sma()),
            [1, 2, 4], [0, 1.5, 3], [1, .5, 1], [-1, -2, -4], [0, -1.5, -3], [-1, -.5, -1]);
        Check(new ImpulseMovingAverageConvergenceDivergence(2, 2),
            [.5, .5, 85d / 72], [.5, .5, 121d / 144], [0, 0, 49d / 144]);
        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("macd-variants", bars, expected, 0));
        }
    }

    [Fact]
    public void HilbertTransformReferenceExpandsItsLaggedFeedback()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4, 8, 16, 32 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        // With both feedback coefficients zero, the outputs are simply lag-two and 1.25*lag-four differences.
        var rules = BuiltInFormulaReferences.For(new EhlersHilbertTransformIndicator(1, 0, 0)).ToArray();
        Assert.Equal(2, rules.Length);
        double[][] expected = [ [0, 0, 0, 1, 2, 4], [0, 0, 0, 0, 0, 1.25] ];
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("hilbert-delays", bars, expected, 0));
        var phaseRules = BuiltInFormulaReferences.For(new EhlersInstantaneousPhaseIndicator(1, 1)).ToArray();
        Assert.Single(phaseRules);
        phaseRules[0].Check(new IndicatorValidationContext("insufficient-phase-horizon", bars, [new double[bars.Length]], 0));
    }

    [Fact]
    public void DEnvelopeHasAFiniteOnePeriodLimitAndReversalStatePersists()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Check(new DEnvelope(1), bars, new[] { 6d, 5, 12 }, new[] { 2d, 3, 6 }, new[] { -2d, 1, 0 });
        var prices = new[] { 100d, 90, 89, 88, 90 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Check(new NickRypockTrailingReverse(2), prices, new[] { 98d, 91.8, 90.78, 89.76, 88.2 });
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("singular-limit-and-reversal", input, expected, 0));
        }
    }

    [Fact]
    public void SteppedChannelsAndStopsUseBoundedAttractionAndBandCrossings()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4, 1.75, .1 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Check(new LinearChannels(2, 1), bars, new[] { 0d, 0, 2, 2.5, 2 }, new[] { 0d, 0, 1, 1.5, 1 });
        Check(new LinearTrailingStop(2, 1), bars, new[] { 0d, 0, 1, 1.5, 2 });
        var rising = new[] { 1d, 2, 4, 8 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Check(new MotionToAttractionChannels(2), rising, new[] { 1d, 2, 4, 8 }, new[] { 1d, 1.625, 3.25, 6.25 }, new[] { 1d, 1.25, 2.5, 4.5 });
        Check(new MotionToAttractionTrailingStop(2), rising, new[] { 1d, 1.25, 2.5, 4.5 });
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("stepped-channel-crossings", input, expected, 0));
        }
    }

    [Fact]
    public void AdaptiveChannelsAndPercentageStopsHaveExplicitBoundaryExamples()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Check(new KaufmanAdaptiveBands(2), bars, new[] { 0d, 0, 4 }, new[] { 0d, 0, 4 }, new[] { 0d, 0, 4 });
        Check(new EfficientTrendStepChannel(2, 2, 3), bars, new[] { 1d, 2, 4 }, new[] { 1d, 2, 2 }, new[] { 1d, 2, 0 });
        var candles = new[] { new Bar(date, 9, 10, 8, 9, 100), new Bar(date, 11, 12, 9, 11, 100), new Bar(date, 8, 11, 7, 8, 100) };
        Check(new PercentageTrailingStops(2, 10), candles, new[] { 9d, 10.8, 10.8 }, new[] { 8.8, 8.8, 7.7 });
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("adaptive-channel-boundary", input, expected, 0));
        }
    }

    [Fact]
    public void VolumeLocationAndHawkeyeReferencesHaveSignedHandExamples()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { new Bar(date, 2, 3, 1, 1, 100), new Bar(date.AddDays(1), 2, 3, 1, 3, 300),
            new Bar(date.AddDays(2), 2, 3, 1, 2, 0) };
        Check(new VolumeAccumulationPercent(2), new[] { -100d, 50, 100 });
        Check(new BetterVolumeIndicator(2), new[] { 100d / 3, 200, 0 });
        Check(new EarningSupportResistanceLevels(), new[] { 1.5, 1.5, 2 });
        Check(new HawkeyeVolumeIndicator(2, 2), new[] { 0d, 3, 3 }, new[] { 0d, 1, 1 });
        Check(new HawkeyeVolumeIndicator(2, 0), new[] { 0d, 2, 2 }, new[] { 0d, 2, 2 });
        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("signed-volume-location", bars, expected, 0));
        }
    }

    [Fact]
    public void NaturalMarketReferencesUseLogChangesAndSignedGeometricCombinations()
    {
        var bars = Enumerable.Range(1, 3).Select(i =>
        {
            var price = Math.Exp(i);
            return new Bar(new DateTime(2021, 1, 4), price, price, price, price, 100);
        }).ToArray();
        Check(new NaturalMarketRiver(1), new[] { 1000d, 1000, 1000 });
        Check(new NaturalMarketMirror(1), new[] { 100000d, 100000, 100000 });
        Check(new NaturalMarketCombo(1, 1), new[] { 10000d, 10000, 10000 });
        Check(new NaturalDirectionalIndex(1, 1), new double[3]);
        Check(new NaturalStochasticIndicator(1, 1), new[] { -100d, -100, -100 });
        Check(new NaturalDirectionalCombo(1, 1), new double[3]);
        Check(new NaturalMarketRiver(2), new[] { 1000d, 500 * (1 + Math.Sqrt(2)), (500 + 2500 * Math.Sqrt(2)) / 3 });
        void Check(IIndicator indicator, double[] expected) => Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(
            new IndicatorValidationContext("natural-log-ramp", bars, new[] { expected }, 0));
    }

    [Fact]
    public void FractalRecursiveAndRegressionCycleReferencesHaveDistinctHandExamples()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Check(new FlaggingBands(2), bars, new[] { 1d, 2, 4 }, new[] { 1d, 1.25, 3.375 }, new[] { 1d, 1, 1.5 }, new[] { 1d, 2, 1.5 });
        Check(new ExtendedRecursiveBands(3), bars, new[] { 1d, 1.5, 2.75 }, new[] { 1d, 1.5, 2.75 }, new[] { 1d, 1.5, 2.75 });
        Check(new ExtendedRecursiveBands(5), bars, new[] { 1d, 5d / 3, 29d / 9 }, new[] { 1d, 1.5, 49d / 18 }, new[] { 1d, 4d / 3, 20d / 9 });
        var cycle = 343d / 46656;
        Check(new ZeroLagSmoothedCycle(3), bars, new[] { 0d, 0, cycle }, new[] { 0d, 0, -cycle / 2 });
        var fractal = new[] { 1d, 2, 5, 2, 1, 2, 3 }.Select(v => new Bar(date, 0, v, -v, 0, 100)).ToArray();
        Check(new FractalChaosBands(), fractal, new[] { 0d, 0, 0, 0, 5, 5, 5 }, new double[7], new[] { 0d, 0, 0, 0, -5, -5, -5 });
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("fractal-recursive-regression", input, expected, 0));
        }
    }

    [Fact]
    public void BreadthTimingAndVigorReferencesHandleTiesAndRangeNormalization()
    {
        var date = new DateTime(2021, 1, 4);
        var bars = new[] { 1d, 2, 1, 1, 3 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Check(new ZweigMarketBreadthIndicator(2), bars, new[] { 1d, 1, 2d / 3, 2d / 9, 20d / 27 });
        Check(new TimePriceIndicator(2), bars, new[] { -.5, -.5, 0, .5, -.5 }, new[] { 0d, .5, .5, .5, .5 });
        var candles = Enumerable.Range(0, 3).Select(_ => new Bar(date, 2, 4, 0, 3, 100)).ToArray();
        Check(new NormalizedRelativeVigorIndex(2), candles, new[] { 25d, 25, 25 }, new[] { 12.5, 25, 25 });
        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("breadth-timing-vigor", input, expected, 0));
        }
    }

    [Fact]
    public async Task RangeProjectionLevelsUseTheFullRangeAtEveryPeriod()
    {
        var bars = Enumerable.Range(0, 3).Select(i => new Bar(new DateTime(2021, 1, 4).AddDays(i), 5, 10, 1, 7, 100)).ToArray();
        Check(new TironeLevels(2), new[] { 7d, 5.5, 4, 6, 15, -3, 11, 2 });
        Check(new ProjectedSupportAndResistance(2), new[] { -1.25, -3.5, 12.25, 14.5, 5.5 });
        foreach (var factory in new Func<IIndicator>[] { () => new TironeLevels(1), () => new ProjectedSupportAndResistance(1) })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(factory().GetType(), "minimum-range", factory),
                new() { RequireFormulaReference = true });
        void Check(IIndicator indicator, double[] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("range-projection", bars, expected.Select(v => Enumerable.Repeat(v, 3).ToArray()).ToArray(), 0));
        }
    }

    [Fact]
    public void EhlersCycleReferencesRespectStartupAndDelayedImpulseResponse()
    {
        var date = new DateTime(2021, 1, 4);
        var impulse = Enumerable.Range(0, 10).Select(i => new Bar(date, i == 7 ? 1 : 0, i == 7 ? 1 : 0,
            i == 7 ? 1 : 0, i == 7 ? 1 : 0, 100)).ToArray();
        var expected = new[] { 0d, 0, 0, 0, 0, 0, 0, 3d / 32, 3d / 32, -3d / 128 };
        foreach (var indicator in new IIndicator[] { new EhlersCyberCycle(3), new EhlersSimpleCycleIndicator(.5) })
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("delayed-impulse", impulse, new[] { expected }, 0));
        var bars = new[] { 1d, 2, 4, 8 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        var decay = Math.Cos(.01) / (1 + Math.Sin(.01));
        var gain = (1 - decay) / 2;
        var coefficient = Math.Cos(.99) * (1 + decay);
        Assert.Single(BuiltInFormulaReferences.For(new EhlersBandPassFilterV2(4, 0))).Check(
            new IndicatorValidationContext("three-bar-startup", bars, new[] { new[] { 0d, 0, 0, 6 * gain } }, 0));
        Assert.Single(BuiltInFormulaReferences.For(new EhlersCycleBandPassFilter(4, 0))).Check(
            new IndicatorValidationContext("two-bar-startup", bars, new[] { new[] { 0d, 0, 3 * gain, 6 * gain + coefficient * 3 * gain } }, 0));
    }

    [Fact]
    public void AdaptiveAndFiniteFiltersHaveHandCalculatedBoundaryResponses()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new ShapeshiftingMovingAverage(2), new[] { 1d, 2, 4 });
        Check(new ShapeshiftingMovingAverage(3), new[] { 17d / 18, 35d / 18, 70d / 18 });
        Check(new NarrowBandpassFilter(3), new[] { 0d, Math.Sqrt(3) / 2, Math.Sqrt(3) });
        Check(new ParametricKalmanFilter(2), new[] { 1d, 1, 1.75 });
        Check(new ParametricCorrectiveLinearMovingAverage(2), new[] { 0d, 0, 23d / 36 });
        Check(new OvershootReductionMovingAverage(2), new[] { 0d, 2, 4 });
        Check(new EdgePreservingFilter(1), new[] { 1d, 4d / 3, 2 });
        Check(new EhlersAllPassPhaseShifter(4), new[] { .25, .5, 1.9375 });
        var gain = .0645 * .0645;
        Check(new EhlersKaufmanAdaptiveMovingAverage(1), new[] { gain, 3 * gain - gain * gain, 7 * gain - 4 * gain * gain + gain * gain * gain });
        void Check(IIndicator indicator, double[] expected) => Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(
            new IndicatorValidationContext("filter-hand-example", bars, new[] { expected }, 0));
    }

    [Fact]
    public void PriceMotionReferencesUseRelativeRangeVarianceRatioAndLogFits()
    {
        var date = new DateTime(2021, 1, 4);
        var rangeBars = Enumerable.Range(0, 3).Select(_ => new Bar(date, 2, 4, 1, 2, 100)).ToArray();
        var distance = Math.Sqrt(1 - Math.Sqrt(.8));
        Assert.Single(BuiltInFormulaReferences.For(new ClosedFormDistanceVolatility(2))).Check(
            new IndicatorValidationContext("fixed-high-low-ratio", rangeBars, new[] { new[] { distance, distance, distance } }, 0));
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(date, v, v, v, v, 100)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new MotionSmoothnessIndex(2))).Check(
            new IndicatorValidationContext("dispersion-ratio", bars, new[] { new[] { 0d, 1, .5 } }, 0));
        var exponential = Enumerable.Range(1, 3).Select(i => new Bar(date, Math.Exp(i), Math.Exp(i), Math.Exp(i), Math.Exp(i), 100)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new NaturalMarketSlope(2))).Check(
            new IndicatorValidationContext("linear-log-prices", exponential, new[] { Enumerable.Repeat(1000 * Math.Log(2), 3).ToArray() }, 0));
    }

    [Fact]
    public void RecursiveChannelsAndStepsMatchTheirResponseEquations()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new GChannels(2), new[] { 1d, 1.5, 3.5 }, new[] { .5, 1, 2.25 }, new[] { 0d, .5, 1 });
        Check(new SmartEnvelope(2), new[] { 1d, 1.5, 3 }, new[] { 1d, 1.5, 1.75 }, new[] { 1d, 1.5, .5 });
        Check(new TrendStep(2), new[] { 1d, 2, 2 });
        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("recursive-channel", bars, expected, 0));
        }
    }

    [Fact]
    public async Task ErrorEnvelopesDistinguishAbsoluteDeviationResidualErrorAndSquaredError()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new MeanAbsoluteDeviationBands(3, 1), new[] { 0d, .5, 31d / 9 }, new[] { 0d, 0, 7d / 3 }, new[] { 0d, -.5, 11d / 9 });
        // The center is a published binary64 mean; accumulate errors against that rounded center.
        var maeCenter = ReferenceFraction.FromDouble(7d / 3);
        var maeWidth = (new ReferenceFraction(7) - maeCenter) / new ReferenceFraction(3);
        Check(new MeanAbsoluteErrorBands(3), new[] { 1d, 1.5, (maeCenter + maeWidth).ToDouble() },
            new[] { 0d, 0, 7d / 3 }, new[] { -1d, -1.5, (maeCenter - maeWidth).ToDouble() });
        var rms = Math.Sqrt(.625);
        Check(new RootMovingAverageSquaredErrorBands(2), new[] { 0d, 1.5 + rms, 3 + rms }, new[] { 0d, 1.5, 3 }, new[] { 0d, 1.5 - rms, 3 - rms });
        Check(new InterquartileRangeBands(3, 1), new[] { 1d, 3, 7 }, new[] { 1d, 1.5, 2.5 }, new[] { 1d, 0, -2 });
        Check(new TimeSeriesForecast(3), new[] { 1d, 2, 35d / 9 }, new[] { 1d, 2, 23d / 6 }, new[] { 1d, 2, 34d / 9 });
        foreach (var factory in new Func<IIndicator>[] { () => new MeanAbsoluteDeviationBands(1), () => new MeanAbsoluteErrorBands(1),
            () => new RootMovingAverageSquaredErrorBands(1), () => new InterquartileRangeBands(1), () => new TimeSeriesForecast(1) })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(factory().GetType(),
                "minimum-period", factory), new() { RequireFormulaReference = true });

        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(3, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("error-envelopes", bars, expected, 0));
        }
    }

    [Fact]
    public async Task RangeChannelsUseRollingExtremaAndStrictContainment()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v + 2, v - 1, v, 100)).ToArray();
        Check(new RangeBands(2), new[] { 0d, 3, 4.5 }, new[] { 0d, 1.5, 3 }, new[] { 0d, 0, 1.5 });
        Check(new RangeIdentifier(), new[] { 3d, 3, 6 }, new[] { 1.5, 1.5, 4.5 }, new[] { 0d, 0, 3 });
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(RangeBands),
            "one-value-zero-width", () => new RangeBands(1)), new() { RequireFormulaReference = true });

        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(3, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("range-channel", bars, expected, 0));
        }
    }

    [Fact]
    public async Task PriceSpreadAndDisplacementMatchTheirWindowDefinitions()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new QmaSmaDifference(2))).Check(
            new IndicatorValidationContext("rms-minus-mean", bars,
                new[] { new[] { 1d, Math.Sqrt(2.5) - 1.5, Math.Sqrt(10) - 3 } }, 0));
        var expected = new[] { new[] { 0d, 1.1, 1.65 }, new[] { 0d, 1, 1.5 }, new[] { 0d, .9, 1.35 } };
        var rules = BuiltInFormulaReferences.For(new MovingAverageDisplacedEnvelope(2, 1, 10)).ToArray();
        Assert.Equal(3, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("one-bar-displacement", bars, expected, 0));
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(QmaSmaDifference),
            "minimum-period", () => new QmaSmaDifference(1)), new() { RequireFormulaReference = true });
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(MovingAverageDisplacedEnvelope),
            "minimum-period", () => new MovingAverageDisplacedEnvelope(1, 1)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task TransformedPriceContractsMatchHandCalculatedWavesAndCandles()
    {
        var waveBars = new[] { 1d, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new EmaWaveIndicator(1, 2, 3, 1), waveBars,
            new[] { 0d, 0, 0 }, new[] { 0d, .5, 5d / 6 }, new[] { 0d, .5, 5d / 3 });
        var candleBars = new[] { 1d, 4, 3 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new FunctionToCandles(2), candleBars,
            new[] { 100d, 100, 60 }, new[] { 100d, 100, 60 }, new[] { 100d, 100, 60 }, new[] { 100d, 100, 60 });
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(EmaWaveIndicator),
            "minimum-period", () => new EmaWaveIndicator(1, 1, 1, 1)), new() { RequireFormulaReference = true });
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(FunctionToCandles),
            "minimum-period", () => new FunctionToCandles(1)), new() { RequireFormulaReference = true });

        static void Check(IIndicator indicator, Bar[] bars, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("transformed-price", bars, expected, 0));
        }
    }

    [Fact]
    public async Task VolumePressureReferenceCountsOnlyThresholdCrossingFlow()
    {
        var bars = new[] { 1d, 2, 1 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        var expected = new[] { new[] { 50d, 100, 0 }, new[] { 50d, 75, 50 } };
        var rules = BuiltInFormulaReferences.For(new VolumePositiveNegativeIndicator(2, 3)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("signed-volume", bars, expected, 0));
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(VolumePositiveNegativeIndicator),
            "minimum-period", () => new VolumePositiveNegativeIndicator(1, 1)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task ProjectionContractsMatchLaggedFitEnvelopesAndSignals()
    {
        var bars = new[] { 1d, 2, 3 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Check(new ProjectionBands(2), new[] { 1d, 2, 4 }, new[] { .5, 1.5, 3 }, new[] { 0d, 1, 2 });
        Check(new ProjectionOscillator(2), new[] { 100d, 100, 50 }, new[] { 40d, 70, 70 });
        Check(new ProjectionBandwidth(2), new[] { 200d, 200d / 3, 200d / 3 }, new[] { 400d / 3, 1000d / 9, 200d / 3 });
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(ProjectionBands),
            "single-period", () => new ProjectionBands(1)), new() { RequireFormulaReference = true });

        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("lagged-projection", bars, expected, 0));
        }
    }

    [Fact]
    public async Task SpectrumBankReportsItsOnlyChannelAndStaysWithinItsCycleLimits()
    {
        var bars = new[] { 1d, 2, 1, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EhlersSpectrumDerivedFilterBank(12, 12, 40, 3))).Check(
            new IndicatorValidationContext("single-channel", bars, new[] { new[] { 12d, 12, 12, 12 } }, 0));
        foreach (var maximum in new[] { 8, 24 })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(EhlersSpectrumDerivedFilterBank),
                "cycle-limits", () => new EhlersSpectrumDerivedFilterBank(8, maximum, 40, 3),
                IndicatorValidationRule.Bounds(0, 8, maximum)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task RsiVariantsRetainMixedTrendStrengthAndAdaptiveLevels()
    {
        var bars = new[] { 1d, 4, 3, 6 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Check(new MomentaRelativeStrengthIndex(2, 3), new[] { 100d, 100, 75, 1200d / 13 },
            new[] { 100d, 100, 275d / 3, 7175d / 78 });
        Check(new SelfAdjustingRelativeStrengthIndex(2, 2, 2), new[] { 100d, 100, 75, 75 },
            new[] { 0d, 100, 87.5, 75 }, new[] { 50d, 50, 75, 50 }, new[] { 50d, 50, 25, 50 });
        foreach (var period in new[] { 1, 2 })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(MomentaRelativeStrengthIndex),
                "mixed-trend", () => new MomentaRelativeStrengthIndex(period, 3)), new() { RequireFormulaReference = true });

        void Check(IIndicator indicator, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("rsi-variants", bars, expected, 0));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(55)]
    [InlineData(80)]
    public async Task MarketDirectionSupportsShortEqualAndReversedPeriods(int period)
    {
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(MarketDirectionIndicator),
            "period-order", () => new MarketDirectionIndicator(period)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public void JapaneseCorrelationReferenceUsesTheSmoothedPriceRange()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new JapaneseCorrelationCoefficient(2))).Check(
            new IndicatorValidationContext("smoothed-range", bars, new[] { new[] { 0d, .6, 6d / 7 } }, 0));
    }

    [Fact]
    public void DegreeReferenceCancelsTheSharedPolynomialTerms()
    {
        var bars = new[] { 1d, 2, 3, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        var expected = new[] { new[] { 0d, 0, 2, 2 }, new[] { 0d, 0, 2d / 3, 1 }, new[] { 0d, 0, 4d / 3, 1 } };
        var rules = BuiltInFormulaReferences.For(new FastSlowDegreeOscillator(2)).ToArray();
        Assert.Equal(3, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("polynomial-cancellation", bars, expected, 0));
    }

    [Fact]
    public async Task KendallReferenceCountsConcordantPairsAndHandlesNoPairs()
    {
        var bars = new[] { 1d, 2, 3, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new KendallRankCorrelationCoefficient(3))).Check(
            new IndicatorValidationContext("concordant-pairs", bars, new[] { new[] { 2d / 3, 1, 1, 1 } }, 0));
        foreach (var length in new[] { 1, 3 })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(KendallRankCorrelationCoefficient),
                "short-period", () => new KendallRankCorrelationCoefficient(length)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task EveryDidiLineIsInvariantUnderPriceScaling()
    {
        var times = Enumerable.Range(0, 6).Select(i => new DateTime(2021, 1, 4).AddDays(i)).ToList();
        IReadOnlyDictionary<string, List<double>> Calculate(double scale)
        {
            var values = new[] { 1d, 2, 4, 3, 2, 5 }.Select(v => v * scale).ToList();
            var data = new StockData(values, values, values, values, Enumerable.Repeat(1d, values.Count).ToList(), times);
            return data.CalculateDidiIndex(length1: 1, length2: 2, length3: 3).OutputValues;
        }
        var baseline = Calculate(1);
        foreach (var scale in new[] { .01, 100d })
        {
            var scaled = Calculate(scale);
            foreach (var key in new[] { "Curta", "Media", "Longa" })
                for (var i = 0; i < times.Count; i++)
                    Assert.InRange(Math.Abs(baseline[key][i] - scaled[key][i]), 0, 1e-12);
        }
        Assert.Equal(new[] { 0d, 1, 1, 1, 1, 1 }, baseline["Media"]);
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(DidiIndex),
            "all-lines", () => new DidiIndex(1, 2, 3)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task TargetReturnRatiosMatchGainLossAndBetaExamples()
    {
        var bars = new[] { 100d, 100, 200, 50 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Check(new OmegaRatio(2, 0), new[] { 0d, 0, 0, 2 });
        Check(new UpsidePotentialRatio(2, 0), new[] { 0d, 0, 0, Math.Sqrt(2) });
        Check(new TreynorRatio(2, 2, 0), new[] { 0d, 0, .25, .125 });
        foreach (var factory in new Func<IIndicator>[] { () => new OmegaRatio(1, 0),
                     () => new UpsidePotentialRatio(1, 0), () => new TreynorRatio(1, 0, 0) })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(factory().GetType(),
                "short-period-and-zero-beta", factory), new() { RequireFormulaReference = true });

        void Check(IIndicator indicator, double[] expected) => Assert.Single(BuiltInFormulaReferences.For(indicator))
            .Check(new IndicatorValidationContext("hand-calculated-ratios", bars, new[] { expected }, 0));
    }

    [Fact]
    public async Task RiskRatiosUseTargetDownsideAndPriceDrawdowns()
    {
        var bars = new[] { 100d, 100, 80, 80 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Check(new MartinRatio(2, 0), new[] { 0d, 0, -1 / Math.Sqrt(2), -Math.Sqrt(2) });
        Check(new SortinoRatio(2, 0), new[] { 0d, 0, -1 / Math.Sqrt(2), -1 });
        Check(new SharpeRatio(2, 0), new[] { 0d, 0, -1, 0 });
        Check(new InformationRatio(2, 0), new[] { 0d, 0, -1, 0 });
        foreach (var length in new[] { 1, 2, 7 })
        {
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(MartinRatio),
                "drawdown", () => new MartinRatio(length, 0)), new() { RequireFormulaReference = true });
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(SortinoRatio),
                "target-downside", () => new SortinoRatio(length, 0)), new() { RequireFormulaReference = true });
        }
        var positive = new[] { 100d, 100, 200, 150 };
        var data = new StockData(positive.ToList(), positive.ToList(), positive.ToList(), positive.ToList(),
            Enumerable.Repeat(1d, positive.Length).ToList(), bars.Select(b => b.Time).ToList());
        Assert.All(data.CalculateSortinoRatio(length: 2, bmk: 0).CustomValuesList, v => Assert.Equal(0, v));

        void Check(IIndicator indicator, double[] expected) => Assert.Single(BuiltInFormulaReferences.For(indicator))
            .Check(new IndicatorValidationContext("hand-calculated-risk", bars, new[] { expected }, 0));
    }

    [Fact]
    public void AtrPercentageBandsMatchTheirSeededFractionalRange()
    {
        var bars = new[] { 1d, 2 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        var expected = new[] { new[] { 3d, 10d / 3 }, new[] { 1d, 2 }, new[] { -1d, 2d / 3 } };
        var rules = BuiltInFormulaReferences.For(new BollingerBandsWithAtrPct(1, 1, 1)).ToArray();
        Assert.Equal(3, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("seeded-range", bars, expected, 0));
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    public async Task LogisticCorrelationMapsNeutralAndPerfectTrends(double gain)
    {
        var bars = new[] { 2d, 4, 6, 8 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        var expected = new[] { .5, 1 / (1 + Math.Exp(-gain)), 1 / (1 + Math.Exp(-gain)), 1 / (1 + Math.Exp(-gain)) };
        Assert.Single(BuiltInFormulaReferences.For(new LogisticCorrelation(3, gain))).Check(
            new IndicatorValidationContext("perfect-trend", bars, new[] { expected }, 0));
        foreach (var length in new[] { 1, 3 })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(LogisticCorrelation),
                "short-period", () => new LogisticCorrelation(length, gain)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(14)]
    [InlineData(40)]
    public async Task VaradiRestoresFullRankAfterASpikeLeavesBothWindows(int length)
    {
        var prices = Enumerable.Range(0, 240).Select(i => i == 60 ? 200d : 100).ToArray();
        var times = Enumerable.Range(0, prices.Length).Select(i => new DateTime(2021, 1, 4).AddMinutes(i)).ToList();
        var highs = prices.Select(v => v + 1).ToList();
        var lows = Enumerable.Repeat(99d, prices.Length).ToList();
        var data = new StockData(prices.ToList(), highs, lows, prices.ToList(),
            Enumerable.Repeat(1d, prices.Length).ToList(), times);
        var expected = data.CalculateVaradiOscillator(MovingAvgType.WeightedMovingAverage, length).OutputValues["Vo"];
        Assert.All(expected.Skip(61 + 2 * length), v => Assert.Equal(100, v));
        using var state = new VaradiOscillatorState(MovingAvgType.WeightedMovingAverage, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, times[i], times[i], prices[i], highs[i], lows[i], prices[i], 1, true);
                Assert.InRange(Math.Abs(expected[i] - state.Update(bar, false, true).Value), 0, 1e-10);
                Assert.InRange(Math.Abs(expected[i] - state.Update(bar, true, true).Value), 0, 1e-10);
            }
        }
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(VaradiOscillator),
            "rank-ties", () => new VaradiOscillator(length, new Wma(length))), new() { RequireFormulaReference = true });
    }

    [Fact]
    public void RankAndLogMomentumReferencesIncludeStartupAndTies()
    {
        var bars = new[] { 1d, 2, 1, 2 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, 3, 1, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new VaradiOscillator(2))).Check(
            new IndicatorValidationContext("rank-ties", bars, new[] { new[] { 50d, 100, 100, 100 } }, 0));
        var scale = 100000 * Math.Log(2) / Math.Sqrt(2);
        var expected = new[] { new[] { 0d, scale, 0, 0 }, new[] { 0d, scale / 2, scale / 6, scale / 18 } };
        var rules = BuiltInFormulaReferences.For(new OceanIndicator(2)).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("log-momentum", bars, expected, 0));
    }

    [Fact]
    public void RangeMomentumReferencesMatchHandCalculatedSignals()
    {
        var prices = new[] { 1d, 2, 1, 3 };
        var bars = prices.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Check(new VolatilityBasedMomentum(2, 2), bars, new[] { 0d, 0, 0, 8d / 11 }, new[] { 0d, 0, 0, 4d / 11 });
        var candles = prices.Take(3).Select(v => new Bar(new DateTime(2021, 1, 4), v - .5, v + 1, v - 1, v, 1)).ToArray();
        Check(new VolatilityQualityIndex(2, 3), candles, new[] { 1d / 32, 10d / 32, 9d / 32 },
            new[] { 0d, 11d / 64, 19d / 64 }, new[] { 0d, 0, 20d / 96 });

        static void Check(IIndicator indicator, Bar[] bars, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("hand-calculated", bars, expected, 0));
        }
    }

    [Fact]
    public async Task EfficiencyIndicatorsUseTravelAndDeadbandRatherThanPriceScale()
    {
        var prices = new[] { 1d, 2, 3, 2, 1, 2 };
        var bars = prices.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EfficientPrice(2))).Check(
            new IndicatorValidationContext("travel", bars, new[] { new[] { 0d, 0, 2, 2, 0, 0 } }, 0));
        var deadbandPrices = Enumerable.Repeat(10d, 9).Concat(new[] { 10.25, 10.75, 11d }).ToArray();
        var deadbandBars = deadbandPrices.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new EfficientAutoLine(2, .5, .5))).Check(
            new IndicatorValidationContext("deadband", deadbandBars,
                new[] { Enumerable.Repeat(10d, 10).Concat(new[] { 10.75, 10.75 }).ToArray() }, 0));
        foreach (var factory in new Func<IIndicator>[] { () => new EfficientPrice(1), () => new EfficientAutoLine(1, .5, .5) })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(factory().GetType(),
                "minimum-period", factory), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task TrendAndMassReferencesMatchSmallWindowsAndSignals()
    {
        var bars = new[] { 1d, 2, 1 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1e6)).ToArray();
        Check(new MassThrustIndicator(length: 2), new[] { 0d, 1, 0 }, new[] { 0d, .5, 1d / 6 });
        Check(new MassThrust(length: 2), new[] { 0d, 1, 0 }, new[] { 0d, .5, 1d / 6 });
        Check(new MassThrustOscillator(length: 2), new[] { 0d, 100, 0 }, new[] { 0d, 50, 50d / 3 });
        Check(new TrendAnalysisIndex(length1: 2, length2: 2), new[] { 0d, 75, 0 }, new[] { 0d, 37.5, 37.5 });
        Check(new TrendAnalysisIndicator(length1: 2, length2: 2), new[] { 0d, .75, 0 }, new[] { 0d, .375, .375 });
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(TrendAnalysisIndex),
            "one-period-range", () => new TrendAnalysisIndex(length1: 2, length2: 1)), new() { RequireFormulaReference = true });

        void Check(IIndicator indicator, double[] line, double[] signal)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(2, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("hand-calculated", bars, new[] { line, signal }, 0));
        }
    }

    private static IReadOnlyList<IndicatorValidationCase> Discover() =>
        IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly });

    public static IEnumerable<object[]> ReferencedCases => Discover().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(ReferencedCases))]
    public Task EveryReferencedConfigurationPassesItsFormula(IndicatorValidationCase testCase) =>
        IndicatorValidation.ValidateAndThrowAsync(testCase, new()
        {
            RequireFormulaReference = true
        });

    [Fact]
    public void EveryMissingConfigurationAndOutputMustBeExplicitlyTracked()
    {
        using var stream = typeof(FormulaContractCoverageTests).Assembly.GetManifestResourceStream("FormulaReferenceBacklog.txt");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var expected = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var actual = Discover().SelectMany(c => IndicatorFormulaCoverage.Inspect(c).MissingOutputSlots
            .Select(slot => c + "/output-" + slot)).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        Assert.Empty(expected);
        Assert.Empty(actual);
    }

    [Fact]
    public void CollidingOutputNamesPreserveEverySeriesAndThePrimary()
    {
        Check(new EhlersChebyshevLowPassFilter(20), 9, 0);
        Check(new VariableLengthMovingAverage(), 2, 1);

        static void Check(IIndicator indicator, int count, int primarySlot)
        {
            Assert.Equal(count, indicator.Outputs.Count);
            Assert.Equal(Enumerable.Range(0, count), indicator.Outputs.Select(o => o.Slot));
            Assert.Equal(primarySlot, Assert.IsAssignableFrom<IPrimaryOutputIndicator>(indicator).PrimaryOutput.Slot);
            var members = indicator.GetType().GetProperties()
                .Where(p => typeof(IIndicatorOutput).IsAssignableFrom(p.PropertyType) && p.Name != "PrimaryOutput")
                .Select(p => ((IIndicatorOutput)p.GetValue(indicator)!).Slot).OrderBy(slot => slot).ToArray();
            Assert.Equal(Enumerable.Range(0, count), members);
            var testCase = new IndicatorValidationCase(indicator.GetType(), "outputs", () => indicator);
            Assert.True(IndicatorFormulaCoverage.Inspect(testCase).IsComplete);
        }
    }

    [Fact]
    public async Task APropertyCannotSatisfyFormulaCoverage()
    {
        var testCase = new IndicatorValidationCase(typeof(SharedIndicatorValidationTests.BadAverage), "property",
            () => new SharedIndicatorValidationTests.BadAverage(), IndicatorValidationRule.Bounds(0, -1000, 1000));
        var report = await IndicatorValidation.ValidateAsync(testCase, new() { RequireFormulaReference = true });
        Assert.Contains(report.Failures, f => f.Rule == "FormulaReference");
        Assert.Equal(new[] { 0 }, report.FormulaCoverage!.MissingOutputSlots);
    }

    [Fact]
    public async Task AReferenceForThePrimaryOutputDoesNotCoverTheSecondary()
    {
        var testCase = new IndicatorValidationCase(typeof(FiniteSecondary), "partial", () => new FiniteSecondary(),
            IndicatorValidationRule.Reference(0, bars => bars.Select(b => b.Close).ToArray()));
        var report = await IndicatorValidation.ValidateAsync(testCase, new() { RequireFormulaReference = true });
        Assert.Equal(new[] { 1 }, report.FormulaCoverage!.MissingOutputSlots);
        Assert.Contains(report.Failures, f => f.Rule == "FormulaReference");
    }

    [Fact]
    public async Task AFiniteWrongSecondaryFormulaThrowsEvenWithCompleteDeclaredCoverage()
    {
        var testCase = new IndicatorValidationCase(typeof(FiniteSecondary), "wrong", () => new FiniteSecondary(),
            IndicatorValidationRule.Reference(0, bars => bars.Select(b => b.Close).ToArray()),
            IndicatorValidationRule.Reference(1, bars => bars.Select(b => 2 * b.Close).ToArray()));
        var report = await IndicatorValidation.ValidateAsync(testCase, new() { RequireFormulaReference = true });
        Assert.True(report.FormulaCoverage!.IsComplete);
        Assert.Contains(report.Failures, f => f.Rule == "Reference[1]");
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
    }

    [Fact]
    public void InvalidSlotsCannotClaimCoverage()
    {
        var testCase = new IndicatorValidationCase(typeof(Sma), "invalid", () => new Sma(),
            IndicatorValidationRule.Reference(1, _ => Array.Empty<double>()));
        Assert.Throws<InvalidOperationException>(() => IndicatorFormulaCoverage.Inspect(testCase));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public async Task EmaUsesItsStandardCoefficientAtPreviouslyClampedPeriods(int length)
    {
        var testCase = new IndicatorValidationCase(typeof(Ema), "standard-alpha", () => new Ema(length));
        await IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });
        var input = Enumerable.Repeat(100d, length).Concat(new[] { 110d }).ToArray();
        var expected = 100 + 20d / (length + 1d);
        var output = new double[input.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.ExponentialMovingAverage(input, output, length);
        var state = new OoplesFinance.StockIndicators.Streaming.EmaState(length);
        foreach (var value in input.Take(length)) state.GetNext(value, true);
        Assert.InRange(Math.Abs(output[length] - expected), 0, 1e-12);
        Assert.InRange(Math.Abs(state.GetNext(110, false) - expected), 0, 1e-12);
        Assert.InRange(Math.Abs(state.GetNext(110, true) - expected), 0, 1e-12);
    }

    [Fact]
    public void CustomComponentGraphsCannotInheritAnUnrelatedBuiltInFormula()
    {
        var testCase = new IndicatorValidationCase(typeof(Ppo), "component", () => new Ppo(maType: new SharedIndicatorValidationTests.BadAverage()));
        Assert.Equal(new[] { 0, 1, 2 }, IndicatorFormulaCoverage.Inspect(testCase).MissingOutputSlots);
    }

    [Fact]
    public void BasicAverageReferencesMatchHandCalculatedValuesIncludingInitialization()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(DateTime.UtcNow, v, v, v, v, 1)).ToArray();
        Check(new Sma(2), new[] { 0d, 1.5, 3 });
        Check(new Wma(2), new[] { 2d / 3, 5d / 3, 10d / 3 });
        Check(new Ema(2), new[] { 1d, 1.5, 19d / 6 });
        Check(new Dema(2), new[] { 1d, 1.75, 137d / 36 });
        Check(new Tema(2), new[] { 1d, 1.875, 859d / 216 });

        void Check(IIndicator indicator, double[] expected)
        {
            var rule = Assert.Single(BuiltInFormulaReferences.For(indicator));
            rule.Check(new IndicatorValidationContext("hand-calculated", bars,
                new IReadOnlyList<double>[] { expected }, 0));
            Assert.Throws<InvalidOperationException>(() => rule.Check(new IndicatorValidationContext("mutated", bars,
                new IReadOnlyList<double>[] { expected.Select(v => v + 1).ToArray() }, 0)));
        }
    }

    [Fact]
    public void PriceReferencesUseTheDocumentedFieldsOnAsymmetricBars()
    {
        var bars = new[] { new Bar(new DateTime(2021, 1, 4), 10, 19, 7, 12, 100) };
        Check(new TypicalPrice(1), 38d / 3);
        Check(new MedianPrice(1), 13);
        Check(new WeightedClose(1), 12.5);
        Check(new AveragePrice(1), 11);
        Check(new OoplesFinance.StockIndicators.Indicators.Range(1), 12);

        void Check(IIndicator indicator, double expected) => Assert.Single(BuiltInFormulaReferences.For(indicator))
            .Check(new IndicatorValidationContext("asymmetric", bars,
                new IReadOnlyList<double>[] { new[] { expected } }, 0));
    }

    [Fact]
    public void OscillatorReferencesMatchHandCalculatedLinesSignalsAndHistograms()
    {
        var bars = new[] { 1d, 2, 4, 8 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        // The two means and signal round at their published stages before the next equation.
        var fast = new[] { 1d, 1.5, (new ReferenceFraction(19) / new ReferenceFraction(6)).ToDouble(), 0 };
        fast[3] = ((new ReferenceFraction(16) + ReferenceFraction.FromDouble(fast[2])) / new ReferenceFraction(3)).ToDouble();
        var slow = new[] { 1d, 1.5, (new ReferenceFraction(7) / new ReferenceFraction(3)).ToDouble(), 0 };
        slow[3] = ((new ReferenceFraction(8) + ReferenceFraction.FromDouble(slow[2])) / new ReferenceFraction(2)).ToDouble();
        var macdLine = fast.Select((value, i) => (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(slow[i])).ToDouble()).ToArray();
        var macdSignal = new double[4];
        for (var i = 2; i < 4; i++) macdSignal[i] = ((new ReferenceFraction(2) * ReferenceFraction.FromDouble(macdLine[i]) +
            ReferenceFraction.FromDouble(macdSignal[i - 1])) / new ReferenceFraction(3)).ToDouble();
        var macdHistogram = macdLine.Select((value, i) => (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(macdSignal[i])).ToDouble()).ToArray();
        Check(new Macd(2, 3, 2), new[] { macdLine, macdSignal, macdHistogram });
        var line = fast.Select((value, i) => (new ReferenceFraction(100) *
            (ReferenceFraction.FromDouble(value) / ReferenceFraction.FromDouble(slow[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
        var signal = new double[4];
        for (var i = 2; i < 4; i++) signal[i] = ((new ReferenceFraction(2) * ReferenceFraction.FromDouble(line[i]) +
            ReferenceFraction.FromDouble(signal[i - 1])) / new ReferenceFraction(3)).ToDouble();
        var histogram = line.Select((value, i) => (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
        Check(new Ppo(2, 3, signalLength: 2), new[] { line, signal, histogram });

        void Check(IIndicator indicator, double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(3, rules.Length);
            foreach (var rule in rules)
            {
                rule.Check(new IndicatorValidationContext("hand-calculated", bars, expected, 0));
                var mutated = expected.Select(values => values.Select(v => v + 1).ToArray()).ToArray();
                Assert.Throws<InvalidOperationException>(() => rule.Check(
                    new IndicatorValidationContext("mutated", bars, mutated, 0)));
            }
        }
    }

    [Fact]
    public void FoundationReferencesMatchIndependentSmallExamples()
    {
        var prices = new[] { 1d, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new Wwma(2), prices, new[] { .5, 1.25, 2.625 });
        Check(new Tma(2), prices, new[] { 0d, .75, 2.25 });
        Check(new Hma(2), prices, new[] { 4d / 3, 7d / 3, 14d / 3 });
        Check(new BollingerBands(2), prices, new[] { 0d, 2.5, 5 }, new[] { 0d, 1.5, 3 }, new[] { 0d, .5, 1 });
        Check(new Variance(2), prices, new[] { 0d, .25, 1 });
        Check(new PriceMomentumOscillator(2, 2, 2), prices,
            new[] { 0d, 1000, 1000 }, new[] { 0d, 500, 2500d / 3 });
        Check(new RelativeMomentumIndex(2, 1), prices,
            new[] { 100d, 100, 100 }, new[] { 50d, 75, 87.5 }, new[] { 50d, 25, 12.5 });
        Check(new Trimean(3), prices, new[] { 1d, 1.25, 2.25 },
            new[] { 1d, 1, 1 }, new[] { 1d, 1, 2 }, new[] { 1d, 2, 4 });
        Check(new StandardDeviationChannel(2), prices, new[] { 1d, 3, 6 }, new[] { 1d, 2, 4 }, new[] { 1d, 1, 2 });
        Check(new StollerAverageRangeChannels(2), prices, new[] { 0d, 2.5, 6 }, new[] { 0d, 1.5, 3 }, new[] { 0d, .5, 0 });
        Check(new HighLowBands(2), prices, new[] { 0d, .7575, 2.2725 }, new[] { 0d, .75, 2.25 }, new[] { 0d, .7425, 2.2275 });
        Check(new HighLowMovingAverage(2), prices, new[] { 2d / 3, 5d / 3, 10d / 3 },
            new[] { 2d / 3, 4d / 3, 2.5 }, new[] { 2d / 3, 1, 5d / 3 });
        Check(new StochasticFastOscillator(2, 2, 2), prices,
            new[] { 0d, 50, 250d / 3 }, new[] { 0d, 25, 575d / 9 });
        Check(new StochasticRegular(2, 2), prices, new[] { 0d, 100, 100 }, new[] { 0d, 50, 100 });
        Check(new VolumeZoneOscillator(2), prices, new[] { 0d, 50, 250d / 3 });
        Check(new PriceZoneOscillator(2), prices, new[] { 0d, 200d / 3, 1800d / 19 });
        Check(new GopalakrishnanRangeIndex(2), prices, new[] { 0d, 0, 1 }, new[] { 0d, 0, 2d / 3 });

        var bars = new[]
        {
            new Bar(new DateTime(2021, 1, 4), 10, 12, 9, 11, 100),
            new Bar(new DateTime(2021, 1, 5), 15, 17, 14, 16, 200),
            new Bar(new DateTime(2021, 1, 6), 13, 14, 10, 12, 50)
        };
        Check(new PriceVolumeRank(1, 2), bars, new[] { 1d, 1, 3 }, new[] { 0d, 1, 2 }, new[] { 1d, 1, 3 });
        Check(new AverageMoneyFlowOscillator(2), prices, new[] { -50d, 50d / 3, 200d / 3 });
        Check(new PriceChannel(2), prices, new[] { 1d, 1.5, 19d / 6 });
        Check(new MovingAverageChannel(2), bars, new[] { 0d, 14.5, 15.5 },
            new[] { 0d, 13, 13.75 }, new[] { 0d, 11.5, 12 });
        Check(new MovingAverageBands(1, 2), prices, new[] { 1d, 2, 4 },
            new[] { 1d, 1.5, 19d / 6 }, new[] { 1d, 1, 7d / 3 }, new[] { 1d, 2, 4 });
        Check(new KirshenbaumBands(2, 3), prices, new[] { 1d, 1.5, 19d / 6 + 1 / (6 * Math.Sqrt(3)) },
            new[] { 1d, 1.5, 19d / 6 }, new[] { 1d, 1.5, 19d / 6 - 1 / (6 * Math.Sqrt(3)) });
        Check(new AverageTrueRangeChannel(2, 2), bars, new[] { 11d, 25, 24 },
            new[] { 11d, 16, 12 }, new[] { 11d, 7, 0 }, new[] { 0d, 13.5, 14 });
        Check(new PolynomialLeastSquaresMovingAverage(2), prices, new[] { 11d / 12, 23d / 12, 23d / 6 });
        Check(new LightLeastSquaresMovingAverage(3), prices, new[] { 0d, 0, 7d / 3 + Math.Sqrt(2d / 3) });
        var anchoredPrices = new[] { 1d, 2, 4, 8 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new AnchoredMomentum(1), anchoredPrices, new[] { 0d, 0, 0, -275d / 14 }, new[] { 0d, 0, 0, -275d / 56 });
        Check(new TrueRangeAdjustedExponentialMovingAverage(2, 1), prices, new[] { 1d, 7d / 3, 103d / 27 });
        Check(new StatisticalVolatility(2, 2), prices, new[] { 0d, .6 * Math.Log(2), .6 * Math.Log(2) },
            new[] { 0d, .3 * Math.Log(2), .5 * Math.Log(2) });
        Check(new CompoundRatioMovingAverage(2), prices, new[] { .75, 1.75, 3.5 });
        Check(new SequentiallyFilteredMovingAverage(2), prices, new[] { 1d, 1, 3 });
        Check(new PsychologicalLine(2), prices, new[] { 0d, 50, 100 });
        Check(new TFSTetherLine(2), bars, new[] { 10.5, 13, 13.5 });
        Check(new TFSTetherLineIndicator(2), bars, new[] { 10.5, 13, 13.5 });
        Check(new ChandelierExit(2, 3), bars, new[] { 7.5, 5.75, 2.375 });
        Check(new ChandelierExitLong(2), bars, new[] { 7.5, 5.75, 2.375 });
        Check(new ChandelierExitShort(2), bars, new[] { 13.5, 20.25, 24.625 });
        var thresholdBars = Enumerable.Range(1, 5).Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, v == 5 ? 5 : 0)).ToArray();
        Check(new RelativeVolumeIndicator(5), thresholdBars, new[] { 0d, 0, 0, 0, 2 }, new[] { 1d, 1, 1, 1, 4 });
        Check(new VolumeAdjustedMovingAverage(2), bars, new[] { 0d, 16, 196d / 13 });
        Check(new VolumeAdjustedMa(2), bars, new[] { 0d, 16, 196d / 13 });
        Check(new VariableAdaptiveMovingAverage(2), bars, new[] { 11d, 38d / 3, 633d / 50 });
        Check(new AbsoluteStrengthMTFIndicator(2, 2), prices, new[] { 0d, .5, 1.25 }, new double[3]);
        // First probability is one. Its zero-seeded mean leaves 10/11; the two
        // signal filters remove a fraction 4/35 of that residual on the first bar.
        Check(new AbsoluteStrengthIndex(2), prices.Take(1).ToArray(), new[] { 62d / 77 });
        Check(new SwingIndex(), bars, new[] { 0d, 2300d / 19, -425d / 7 });
        Check(new AccumulativeSwingIndex(0, 2), bars, new[] { 0d, 2300d / 19, 8025d / 133 },
            new[] { 0d, 1150d / 19, 24125d / 266 });
        Check(new AdaptiveEma(2), anchoredPrices, new[] { 0d, 1.5, 3, 29d / 3 });
        Check(new AdaptiveExponentialMovingAverage(2), anchoredPrices, new[] { 0d, 1.5, 3, 29d / 3 });
        Check(new AdaptiveAutonomousRecursiveMovingAverage(2, 1), prices, new[] { 0d, 1, 2 }, new[] { 1d, 1, 6 });
        Check(new AdaptiveAutonomousRecursiveTrailingStop(2, 1), prices, new[] { 1d, 0, 4 });
        var reversal = new[] { 1d, 2, 1 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new AdaptiveRelativeStrengthIndex(2), reversal, new[] { 1d, 2, 5d / 3 });
        Check(new AdaptivePriceZoneIndicator(4), prices, new[] { 1d, 1.25, 91d / 36 },
            new[] { 1d, 1.25, 91d / 36 }, new[] { 1d, 1.25, 91d / 36 });
        Check(new AdaptiveErgodicCandlestickOscillator(2, 2, 2), bars,
            new[] { 100d / 3, 100d / 3, -25 }, new[] { 100d / 3, 100d / 3, -50d / 9 });
        Check(new ApirineSlowRelativeStrengthIndex(2, 2), reversal, new[] { 100d, 100, 80 });
        Check(new Ama(2), prices, new[] { 4d / 225, 364d / 405, 1660d / 729 });
        Check(new PoweredKaufmanAdaptiveMovingAverage(2), prices, new[] { 0d, 0, 1 }, new[] { 1d, 1, 4 });
        Check(new AdaptiveStochastic(2, 3), prices, new[] { 0d, 1, 1 });
        Check(new AsymmetricalRelativeStrengthIndex(3), reversal, new[] { 100d, 100, 100d / 3 });
        Check(new AsymmetricalRsi(3), reversal, new[] { 100d, 100, 100d / 3 });
        Check(new AdaptiveTrailingStop(2, 3), prices, new[] { 0d, 2, 2 });
        Check(new AutoDispersionBands(2, 1), prices, new[] { 2d / 3, 5d / 3, 10d / 3 + Math.Sqrt(2) },
            new[] { 2d / 3, 4d / 3, 19d / 6 }, new[] { 2d / 3, 1, 3 - Math.Sqrt(2) });
        var holdPrices = new[] { 1d, 2, 4, 3.4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new AverageAbsoluteErrorNormalization(2), reversal, new[] { 0d, 1, -.2 });
        Check(new AtrTrailingStops(2, .5), reversal, new[] { 1d, 1.75, 17d / 12 });
        Check(new AverageTrueRangeTrailingStops(2, .5), reversal, new[] { 1d, 1.75, 17d / 12 });
        Check(new BryantAdaptiveMovingAverage(2), reversal, new[] { 1d, 2, 1 });
        Check(new BreakoutRsi(2), reversal, new[] { 100d, 100, 100 });
        Check(new BilateralStochasticOscillator(2), reversal,
            new[] { 0d, 2, 0 }, new[] { 0d, 0, 0 }, new[] { 0d, 2, 0 }, new[] { 0d, 0, 0 });
        Check(new ChandeMomentumOscillatorAbsolute(2), reversal, new[] { 0d, 0, 0 });
        Check(new ChandeMomentumOscillatorFilter(2), reversal, new[] { 0d, 100, 0 }, new[] { 0d, 200d / 3, 100d / 3 });
        // Each window rounds its unit ratio (1/3) before the final scaled mean.
        Check(new ChandeMomentumOscillatorAverage(2), reversal, new[] { 100d, 100, 33.33333333333333 });
        Check(new ChandeMomentumOscillatorAbsoluteAverage(2), reversal, new[] { 100d, 100, 33.33333333333333 });
        Check(new CenterOfLinearity(2), reversal, new[] { 0d, -2, -5 });
        Check(new ChandeKrollRSquaredIndex(2), reversal, new[] { 0d, 0, 2d / 3 });
        Check(new ChartmillValueIndicator(2), reversal,
            new[] { 0d, 1 / Math.Sqrt(2), -1 / (2 * Math.Sqrt(2)) }, new[] { 0d, 1 / Math.Sqrt(2), -1 / (2 * Math.Sqrt(2)) },
            new[] { 0d, 1 / Math.Sqrt(2), -1 / (2 * Math.Sqrt(2)) }, new[] { 0d, 1 / Math.Sqrt(2), -1 / (2 * Math.Sqrt(2)) });
        var compositeTrend = Enumerable.Range(1, 6).Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Check(new ChandeCompositeMomentumIndex(), compositeTrend, new[] { 0d, 0, 0, 0, 50, 75 }, new[] { 0d, 0, 0, 0, 20, 40 });
        Check(new ConditionalAccumulator(2), reversal, new[] { 0d, 1, 0 }, new[] { 0d, .5, 1d / 6 });
        Check(new ChandeMomentumOscillatorAverageDisparityIndex(2), reversal, new[] { 0d, 25, -100d / 3 });
        Check(new ChopZone(2), reversal, new[] { 0d, 81, -77 });
        Check(new ContractHigh(), reversal, new[] { 1d, 2, 2 });
        Check(new ContractLow(), reversal, new[] { 1d, 1, 1 });
        var correctedTrend = Enumerable.Range(1, 4).Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Check(new CorrectedMovingAverage(2), correctedTrend, new[] { 0d, 1.5, 2.25, 3.3 });
        Check(new CoralTrendIndicator(1), reversal, new[] { 1d, 2, 1 });
        Check(new CoralTrendIndicator(5), reversal.Take(1).ToArray(), new[] { .216 });
        Check(new DeMarker(2), reversal, new[] { 0d, 100, 50 });
        var dampingBars = Enumerable.Range(0, 8).Select(i => new Bar(new DateTime(2021, 1, 4), 10, 11 + i, 9, 10, 1)).ToArray();
        Check(new DampingIndex(2), dampingBars, new[] { 0d, 0, 0, 0, 0, 0, 0, 3 });
        Check(new DeltaMovingAverage(2, 1), reversal, new[] { 1d, 1, -1 }, new[] { 0d, 1, 0 }, new[] { 1d, 0, -1 });
        Check(new Dema2Lines(2, 3), reversal, new[] { 1d, 1.25, 43d / 36 }, new[] { 1d, 1.25, 23d / 18 });
        Check(new FibonacciRetrace(2, 2, .25), reversal, new[] { 1d, 1.75, 1.75 }, new[] { 1d, 1.25, 1.25 });
        Check(new FullTypicalPrice(14), reversal, new[] { 1d, 2, 1 });
        Check(new FareySequenceWeightedMovingAverage(2), reversal, new[] { 2d / 3, 5d / 3, 4d / 3 });
        Check(new FallingRisingFilter(2), reversal, new[] { 0d, 5d / 3, 19d / 9 });
        Check(new ForecastOscillator(2), reversal, new[] { 0d, 50, -100 }, new[] { 0d, 25, -25 });
        Check(new DynamicSupportAndResistance(2), reversal,
            new[] { 1d, 2 - .5 * Math.Sqrt(2), 2 - .75 * Math.Sqrt(2) },
            new[] { 1d, 1 + .5 * Math.Sqrt(2), 1 + .75 * Math.Sqrt(2) }, new[] { 1d, 1.5, 1.5 });
        Check(new FiniteVolumeElements(2), reversal, new[] { 0d, 50, 0 });
        Check(new FoldedRelativeStrengthIndex(2), reversal, new[] { 100d, 200, 160 }, new[] { 100d, 150, 470d / 3 });
        var fractalPrices = new[] { 1d, 2, 4, 2, 1, 2, 4 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        Check(new FractalChaosOscillator(14), fractalPrices, new[] { 0d, 0, 0, 0, 1, 0, -1 });
        Check(new BuffAverage(2), reversal, new[] { 1d, 1.5, 1.5 }, new[] { 1d, 1.5, 4d / 3 });
        Check(new BollingerBandsAvgTrueRange(2, 2, 2, MovingAvgType.SimpleMovingAverage), reversal, new[] { 0d, .25, .5 });
        var timingBars = Enumerable.Range(0, 5).Select(_ => new Bar(new DateTime(2021, 1, 4), 10, 11, 9, 10, 100)).ToArray();
        Check(new DailyAveragePriceDelta(2), timingBars, new[] { 11d, 13, 13, 13, 13 }, new[] { 9d, 7, 7, 7, 7 });
        Check(new BelkhayateTiming(5), timingBars, new[] { 100d, 37.5, 50d / 3, 6.25, 0 });
        Check(new AutoLine(3), holdPrices, new[] { 1d, 2, 4, 4 });
        Check(new AutoLineWithDrift(3), holdPrices, new[] { 1d, 2, 4, 25d / 6 });
        Check(new AutoFilter(3), holdPrices, new[] { 0d, 0, 4, 3.7 });
        Check(new LinearRegressionLine(3), prices, new[] { 0d, 0, 23d / 6 });
        Check(new LinearExtrapolation(1), prices, new[] { 0d, 1, 3 });
        Check(new EhlersLaguerreFilter(1), prices, new[] { 1d, 7d / 6, 11d / 6 });
        Check(new DidiIndex(1, 2, 3), prices, new[] { 0d, 4d / 3, 4d / 3 },
            new[] { 0d, 1, 1 }, new[] { 0d, 0, (ReferenceFraction.FromDouble((new ReferenceFraction(7) / new ReferenceFraction(3)).ToDouble()) / new ReferenceFraction(3)).ToDouble() });
        Check(new DetrendedSyntheticPrice(3), prices, new[] { 0d, .25, .6875 });
        var tiedPrices = new[] { 1d, 2, 2, 1, 3 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new PercentRank(2), tiedPrices, new[] { 0d, 0, 50, 0, 100 });
        Check(new ChandeIntradayMomentumIndex(3), bars, new[] { 100d, 100, 200d / 3 });
        Check(new CumulativeVolumeIndex(3), bars, new[] { 0d, 200, 150 });
        Check(new EhlersCenterofGravityOscillator(3), prices, new[] { 1d, 2d / 3, 3d / 7 });
        Check(new EhlersCenterOfGravityOscillator(3), prices, new[] { 1d, 2d / 3, 3d / 7 });
        Check(new Atr(2), bars, new[] { 1.5, 3.75, 4.875 });
        Check(new Mfi(2), bars, new[] { 100d, 100, 1175d / 14 });
        Check(new VolumeMomentum(1), bars, new[] { 0d, 100, -150 });
        Check(new NormalizedVolume(2), bars, new[] { 0d, 4d / 3, .4 });
        Check(new MarketFacilitationIndex(1), bars, new[] { .03, .015, .08 });
        Check(new TFSVolumeOscillator(2), bars, new[] { 50d, 150, 75 });
        var volumeSlow1 = (new ReferenceFraction(500) / new ReferenceFraction(3)).ToDouble();
        var volumeSlow2 = ((new ReferenceFraction(100) + ReferenceFraction.FromDouble(volumeSlow1)) / new ReferenceFraction(3)).ToDouble();
        Check(new VolumeMomentumOscillator(1, 2), bars, new[] { 0d,
            (new ReferenceFraction(100) * (new ReferenceFraction(200) / ReferenceFraction.FromDouble(volumeSlow1) - new ReferenceFraction(1))).ToDouble(),
            (new ReferenceFraction(100) * (new ReferenceFraction(50) / ReferenceFraction.FromDouble(volumeSlow2) - new ReferenceFraction(1))).ToDouble() });
        Check(new AverageDayRange(2), bars, new[] { 0d, 3, 3.5 });
        Check(new HighestHigh(2), bars, new[] { 12d, 17, 17 });
        Check(new LowestLow(2), bars, new[] { 9d, 9, 10 });
        Check(new AtrChannelWidth(2), bars, new[] { 6d, 15, 19.5 });
        Check(new PrettyGoodOscillator(2), bars, new[] { 0d, 5d / 9, -1d / 3 });
        Check(new MayerMultiple(2), prices, new[] { 0d, 4d / 3, 4d / 3 });
        Check(new QuadraticMovingAverage(2), prices, new[] { 1d, Math.Sqrt(2.5), Math.Sqrt(10) });
        var tfsSlow = (new ReferenceFraction(7) / new ReferenceFraction(3)).ToDouble();
        var tfsDifference = (new ReferenceFraction(3) - ReferenceFraction.FromDouble(tfsSlow)).ToDouble();
        var tfsSignal = ((ReferenceFraction.FromDouble(1.5) + ReferenceFraction.FromDouble(tfsDifference)) / new ReferenceFraction(2)).ToDouble();
        Check(new TFSMboIndicator(2, 3, 2), prices,
            new[] { 0d, 1.5, tfsDifference }, new[] { 0d, .75, tfsSignal }, new[] { 0d, .75, tfsDifference - tfsSignal });
        var ergodicFast = (new ReferenceFraction(19) / new ReferenceFraction(6)).ToDouble();
        var ergodicSlow = (new ReferenceFraction(7) / new ReferenceFraction(3)).ToDouble();
        var ergodicDifference = (ReferenceFraction.FromDouble(ergodicFast) - ReferenceFraction.FromDouble(ergodicSlow)).ToDouble();
        var ergodicSignal = (new ReferenceFraction(2) * ReferenceFraction.FromDouble(ergodicDifference) / new ReferenceFraction(3)).ToDouble();
        Check(new ErgodicMovingAverageConvergenceDivergence(2, 3, 2), prices,
            new[] { 0d, 0, ergodicDifference }, new[] { 0d, 0, ergodicSignal },
            new[] { 0d, 0, (ReferenceFraction.FromDouble(ergodicDifference) - ReferenceFraction.FromDouble(ergodicSignal)).ToDouble() });
        var ergodicRatio = (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(ergodicSlow) / ReferenceFraction.FromDouble(ergodicFast) - new ReferenceFraction(1))).ToDouble();
        var ergodicRatioSignal = (ReferenceFraction.FromDouble(ergodicRatio) / new ReferenceFraction(3)).ToDouble();
        Check(new ErgodicPercentagePriceOscillator(2), prices,
            new[] { 0d, 0, ergodicRatio }, new[] { 0d, 0, ergodicRatioSignal },
            new[] { 0d, 0, (ReferenceFraction.FromDouble(ergodicRatio) - ReferenceFraction.FromDouble(ergodicRatioSignal)).ToDouble() });
        var longPrices = Enumerable.Repeat(10d, 200).Append(20d)
            .Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        var tfsRatio = (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(10.4) / ReferenceFraction.FromDouble(10.05) - new ReferenceFraction(1))).ToDouble();
        var tfsRatioSignal = (ReferenceFraction.FromDouble(tfsRatio) / new ReferenceFraction(18)).ToDouble();
        Check(new TFSMboPercentagePriceOscillator(14), longPrices,
            Enumerable.Repeat(0d, 200).Append(tfsRatio).ToArray(),
            Enumerable.Repeat(0d, 200).Append(tfsRatioSignal).ToArray(),
            Enumerable.Repeat(0d, 200).Append(tfsRatio - tfsRatioSignal).ToArray());
        Check(new MirroredPercentagePriceOscillator(2), bars.Take(1).ToArray(),
            new[] { 10d }, new[] { 10d }, new[] { 0d },
            new[] { -100d / 11 }, new[] { -100d / 11 }, new[] { 0d });
        var leaderPrices = Enumerable.Repeat(10d, 26).Append(20d)
            .Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        // After 26 constant prices, each EMA and its residual EMA has completed startup.
        double LeaderLeg(long divisor)
        {
            var mean = (new ReferenceFraction(10) + new ReferenceFraction(20) / new ReferenceFraction(divisor)).ToDouble();
            var residual = (new ReferenceFraction(20) - ReferenceFraction.FromDouble(mean)).ToDouble();
            var correction = (new ReferenceFraction(2) * ReferenceFraction.FromDouble(residual) / new ReferenceFraction(divisor)).ToDouble();
            return (ReferenceFraction.FromDouble(mean) + ReferenceFraction.FromDouble(correction)).ToDouble();
        }
        var leaderSpike = (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(LeaderLeg(13)) / ReferenceFraction.FromDouble(LeaderLeg(27)) - new ReferenceFraction(1))).ToDouble();
        var leaderSignal = (new ReferenceFraction(2) * ReferenceFraction.FromDouble(leaderSpike) / new ReferenceFraction(3)).ToDouble();
        Check(new PercentagePriceOscillatorLeader(2), leaderPrices,
            Enumerable.Repeat(0d, 26).Append(leaderSpike).ToArray(),
            Enumerable.Repeat(0d, 26).Append(leaderSignal).ToArray(),
            Enumerable.Repeat(0d, 26).Append(leaderSpike - leaderSignal).ToArray());
        var diNapoliFast = ReferenceFraction.FromDouble(2 / (1 + 8.3896));
        var diNapoliSlow = ReferenceFraction.FromDouble(2 / (1 + 17.5185));
        var diNapoliFirst = (new ReferenceFraction(100) * (diNapoliFast / diNapoliSlow - new ReferenceFraction(1))).ToDouble();
        var diNapoliSignal = (ReferenceFraction.FromDouble(diNapoliFirst) * ReferenceFraction.FromDouble(2 / (1 + 9.0503))).ToDouble();
        Check(new DiNapoliPercentagePriceOscillator(14), prices.Take(1).ToArray(),
            new[] { diNapoliFirst }, new[] { diNapoliSignal }, new[] { diNapoliFirst - diNapoliSignal });
        Check(new TrendDetectionIndex(1, 2), prices, new[] { 0d, 1, 1 }, new[] { 0d, 1, 2 });
        var descending = new[] { 4d, 2, 1 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new TrendDetectionIndex(1, 2), descending, new[] { 0d, 2, -1 }, new[] { 0d, -2, -1 });
        Check(new TrendIntensityIndex(2), prices, new[] { 100d, 100, 100 });
        var scorePrices = Enumerable.Repeat(10d, 11).Concat(new[] { 1d, 1 })
            .Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new ChandeTrendScore(12), scorePrices, Enumerable.Repeat(2d, 11).Concat(new[] { 0d, -2 }).ToArray());
        Check(new WildersSummationMethod(2), prices, new[] { 1d, 2.5, 5.25 });
        Check(new PpoMa(2, 3), prices, new[] { 0d, 0, 250d / 7 });
        Check(new PriceMomentum(1), prices, new[] { 0d, 1, 2 });
        Check(new QuickMovingAverage(3), prices, new[] { .25, 1, 2.25 });
        Check(new QuickMovingAverage(1), prices, new[] { 1d / 3, 4d / 3, 8d / 3 });
        var rickerLagWeight = 11d / 36 * Math.Exp(-25d / 72);
        Check(new RightSidedRickerMovingAverage(2), prices,
            new[] { 1 / (1 + rickerLagWeight), (2 + rickerLagWeight) / (1 + rickerLagWeight), (4 + 2 * rickerLagWeight) / (1 + rickerLagWeight) });
        var impulse = new[] { 1d, 0, 0, 0, 0, 0, 0 }
            .Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Check(new EhlersFiniteImpulseResponseFilter(7), impulse, new[] { 2d, 7, 9, 6, 1, -1, -3 }.Select(v => v / 21).ToArray());
        Check(new EhlersFirFilter(7), impulse, new[] { 2d, 7, 9, 6, 1, -1, -3 }.Select(v => v / 21).ToArray());
        Check(new EhlersInfiniteImpulseResponseFilter(3), prices, new[] { .5, 1.25, 4.125 });
        Check(new EhlersIirFilter(3), prices, new[] { .5, 1.25, 4.125 });
        Check(new EhlersOptimumEllipticFilter(3), impulse.Take(3).ToArray(), new[] { .13785, .167539855, .2735318915065 });
        Check(new EhlersModifiedOptimumEllipticFilter(3), prices, new[] { 1d, 1.2757, 2.02432971 });
        Check(new HendersonWeightedMovingAverage(7), impulse,
            new[] { -42d, 42, 210, 295, 210, 42, -42 }.Select(v => v / 715).ToArray());
        Check(new HendersonWeightedMovingAverage(3), impulse,
            new[] { -21d / 223, 84d / 223, 160d / 223, 0, 0, 0, 0 });
        Check(new DistanceWeightedMovingAverage(3), prices, new[] { .2, 1, 103d / 47 });
        Check(new MiddleHighLowMovingAverage(2, 2), prices, new[] { 1d, 1.25, 29d / 12 });
        Check(new RepulsionMovingAverage(1), prices, new[] { -1d, -.5, 4d / 3 });
        Check(new MovingAverageV3(2), prices, new[] { 1d, 1.5, 43d / 12 });
        Check(new SelfWeightedMovingAverage(2), prices, new[] { 0d, 0, 4 });
        Check(new HoltExponentialMovingAverage(2), prices, new[] { 1d, 2, 11d / 3 });
        Check(new HullEstimate(3), prices, new[] { 0d, 2, 11d / 3 });
        Check(new RecursiveMovingTrendAverage(2), prices, new[] { 11d / 9, 67d / 27, 137d / 27 });

        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules)
            {
                rule.Check(new IndicatorValidationContext("hand-calculated", input, expected, 0));
                var corrupted = expected.Select(values => values.Select(v => v + 1).ToArray()).ToArray();
                Assert.Throws<InvalidOperationException>(() => rule.Check(
                    new IndicatorValidationContext("mutated", input, corrupted, 0)));
            }
        }
    }

    [Fact]
    public void WeightedAverageForgetsDepartedValuesWhenItsWindowBecomesZero()
    {
        const int length = 20;
        var input = Enumerable.Range(0, 80).Select(i => i < length - 1
            ? Math.Pow(100 - 100d * (i + 1) * (i + 2) / (length * (length + 1)), 2) : 0).ToArray();
        var batch = new double[input.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.WeightedMovingAverage(input, batch, length);
        using var state = new OoplesFinance.StockIndicators.Streaming.WmaState(length);
        for (var i = 0; i < input.Length; i++)
        {
            // Discarding a preview must not change the window or its nonzero count.
            state.GetNext(12345, false);
            var preview = state.GetNext(input[i], false);
            var committed = state.GetNext(input[i], true);
            Assert.Equal(preview, committed);
            Assert.Equal(batch[i], committed);
            if (i >= 2 * length - 2) Assert.Equal(0, committed);
        }
        state.Reset();
        Assert.Equal(0, state.GetNext(0, false));
        Assert.Equal(0, state.GetNext(0, true));
        Assert.True(state.GetNext(1e-20, true) > 0); // This is not an epsilon clamp.
    }

    [Fact]
    public void WindowAndRegressionReferencesMatchHandCalculatedExamples()
    {
        var time = new DateTime(2021, 1, 4);
        var impulse = new[] { 1d, 0, 0 }.Select(v => new Bar(time, v, v, v, v, 1)).ToArray();
        Check(new CubicWma(3), impulse, new[] { 27d / 36, 8d / 36, 1d / 36 });
        Check(new ParabolicWma(3), impulse, new[] { 9d / 14, 4d / 14, 1d / 14 });
        var coreParabolic = new double[3];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.ParabolicWeightedMovingAverage(new[] { 1d, 0, 0 }, coreParabolic, 3);
        Assert.Equal(new[] { 9d / 14, 4d / 14, 1d / 14 }, coreParabolic);
        // The public contract preserves each rounded sine tap; the two edge
        // coefficients need not equal the same rounded sqrt(2)/2 expression.
        var sineTaps = new[] { ReferenceFraction.FromDouble(Math.Sin(Math.PI / 4)),
            new ReferenceFraction(1), ReferenceFraction.FromDouble(Math.Sin(3 * Math.PI / 4)) };
        var sineMass = sineTaps[0] + sineTaps[1] + sineTaps[2];
        Check(new SineWma(3), impulse, sineTaps.Select(tap => (tap / sineMass).ToDouble()).ToArray());
        var prices = new[] { 1d, 2, 4 }.Select(v => new Bar(time, v, v, v, v, 1)).ToArray();
        Check(new LinReg(3), prices, new[] { 1d, 2, 23d / 6 }, new[] { 1d, 3, 16d / 3 },
            new[] { 0d, 1, 1.5 }, new[] { 1d, 1, 5d / 6 });
        Check(new Cmo(2), prices, new[] { 0d, 100, 100 }, new[] { 0d, 50, 200d / 3 });
        Check(new Stochastic(2, 2), prices, new[] { 0d, 100, 100 }, new[] { 0d, 50, 100 }, new[] { 0d, 0, 50 });

        void Check(IIndicator indicator, Bar[] bars, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("hand-calculated", bars, expected, 0));
        }
    }

    [Fact]
    public async Task StandardCciUsesOneCurrentWindowMeanInEveryEngine()
    {
        var prices = new[] { 1d, 2, 4, 8 };
        var expected = new[] { 0d, 0, 100, 100 };
        var bars = prices.Select((v, i) => new Bar(new DateTime(2021, 1, 4).AddDays(i), v, v, v, v, 1)).ToArray();
        var rule = Assert.Single(BuiltInFormulaReferences.For(new Cci(3)));
        rule.Check(new IndicatorValidationContext("hand-calculated", bars, new[] { expected }, 0));
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(Cci), "standard", () => new Cci(3)),
            new() { RequireFormulaReference = true });
        var core = new double[4];
        OoplesFinance.StockIndicators.Core.OscillatorCore.CommodityChannelIndex(prices, prices, prices, core, 3);
        using var streaming = new OoplesFinance.StockIndicators.Streaming.CommodityChannelIndexState(length: 3);
        for (var i = 0; i < bars.Length; i++)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, bars[i].Time, bars[i].Time,
                prices[i], prices[i], prices[i], prices[i], 1, true);
            var preview = streaming.Update(bar, false, true);
            var committed = streaming.Update(bar, true, true);
            Assert.Equal(preview.Value, committed.Value);
            Assert.InRange(Math.Abs(core[i] - expected[i]), 0, 1e-10);
            Assert.InRange(Math.Abs(committed.Value - expected[i]), 0, 1e-10);
        }
        streaming.Reset();
        var resetBar = new OhlcvBar("TEST", BarTimeframe.Tick, bars[0].Time, bars[0].Time, 1, 1, 1, 1, 1, true);
        Assert.Equal(0, streaming.Update(resetBar, true, false).Value);
        streaming.Reset();
        for (var i = 0; i < 20; i++)
        {
            var price = i == 4 ? 200d : 100.1;
            var flatBar = new OhlcvBar("TEST", BarTimeframe.Tick, bars[0].Time, bars[0].Time,
                price, price, price, price, 1, true);
            var value = streaming.Update(flatBar, true, false).Value;
            if (i >= 7) Assert.Equal(0, value);
        }
    }

    [Fact]
    public void VolumeReferencesMatchCompoundingAndWeightedCashFlowExamples()
    {
        var prices = new[] { 100d, 110, 121, 108.9 };
        var volumes = new[] { 1000d, 500, 2000, 1000 };
        var bars = prices.Select((v, i) => new Bar(new DateTime(2021, 1, 4).AddDays(i), v, v, v, v, volumes[i])).ToArray();
        Check(new Nvi(2), bars, new[] { 1000d, 1100, 1100, 990 }, new[] { 1000d, 1050, 3250d / 3, 9190d / 9 });
        Check(new Pvi(2), bars, new[] { 1000d, 1000, 1100, 1100 }, new[] { 1000d, 1000, 3200d / 3, 9800d / 9 });
        var asymmetric = new[]
        {
            new Bar(new DateTime(2021, 1, 4), 10, 12, 9, 11, 100),
            new Bar(new DateTime(2021, 1, 5), 15, 17, 14, 16, 200),
            new Bar(new DateTime(2021, 1, 6), 13, 14, 10, 12, 50)
        };
        Check(new Vwap(3), asymmetric, new[] { 32d / 3, 14, 96d / 7 });
        Check(new Cmf(2), asymmetric, new[] { 1d / 3, 1d / 3, 4d / 15 });
        Check(new ForceIndex(2), asymmetric, new[] { 0d, 500, 100d / 3 });
        Check(new EaseOfMovement(2), asymmetric, new[] { 0d, 75000, -280000 });
        Check(new Adx(2), asymmetric, new[] { 0d, 200d / 3, 1000d / 39 },
            new[] { 0d, 0, 1600d / 39 }, new[] { 0d, 50, 475d / 13 });

        void Check(IIndicator indicator, Bar[] input, params double[][] expected)
        {
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(expected.Length, rules.Length);
            foreach (var rule in rules) rule.Check(new IndicatorValidationContext("hand-calculated", input, expected, 0));
        }
    }

    [Fact]
    public void GarmanKlassUsesWindowVarianceAndPreservesPreviewAndReset()
    {
        var level = Math.Sqrt(126) * Math.Log(11d / 9);
        var bars = Enumerable.Range(0, 30).Select(i => new Bar(new DateTime(2021, 1, 4).AddDays(i),
            100, 110, 90, 100, 1)).ToArray();
        var expected = bars.Select((_, i) => i < 2 ? 0 : level).ToArray();
        var reference = BuiltInFormulaReferences.For(new GarmanKlassVolatility(3)).First(r => r.ReferenceOutputSlot == 0);
        reference.Check(new IndicatorValidationContext("stationary-range", bars, new[] { expected }, 0));
        using var state = new GarmanKlassVolatilityState(length: 3);
        var core = new double[bars.Length];
        OoplesFinance.StockIndicators.Core.VolatilityCore.GarmanKlassVolatility(
            bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, 3);
        for (var i = 0; i < bars.Length; i++)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, bars[i].Time, bars[i].Time, 100, 110, 90, 100, 1, true);
            var discarded = new OhlcvBar("TEST", BarTimeframe.Tick, bars[i].Time, bars[i].Time, 100, 150, 50, 100, 1, true);
            state.Update(discarded, false, true);
            var preview = state.Update(bar, false, true);
            var actual = state.Update(bar, true, true);
            Assert.Equal(preview.Value, actual.Value);
            Assert.InRange(Math.Abs(actual.Value - expected[i]), 0, 1e-12);
            Assert.InRange(Math.Abs(core[i] - expected[i]), 0, 1e-12);
        }
        var flat = new OhlcvBar("TEST", BarTimeframe.Tick, bars[0].Time, bars[0].Time, 100, 100, 100, 100, 1, true);
        for (var i = 0; i < 3; i++) state.Update(flat, true, true);
        Assert.Equal(0, state.Update(flat, false, true).Value);
        state.Reset();
        Assert.Equal(0, state.Update(flat, true, true).Value);
    }

    [Fact]
    public async Task GopalakrishnanUsesTheSmallestValidLogarithmBase()
    {
        var testCase = new IndicatorValidationCase(typeof(GopalakrishnanRangeIndex), "period-one",
            () => new GopalakrishnanRangeIndex(1));
        await IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });
        using var state = new GopalakrishnanRangeIndexState(length: 1);
        var time = new DateTime(2021, 1, 4);
        var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, 1, 3, 1, 2, 100, true);
        Assert.Equal(1d, state.Update(bar, false, true).Value);
        var actual = state.Update(bar, true, true);
        Assert.Equal(1d, actual.Value);
        Assert.InRange(Math.Abs(actual.Outputs!["Signal"] - 2d / 3), 0, 1e-12);
        state.Reset();
        Assert.Equal(1d, state.Update(bar, true, true).Value);
    }

    [Fact]
    public void ApirinePreservesVanishingWilderResiduals()
    {
        using var state = new ApirineSlowRelativeStrengthIndexState(length: 4, smoothLength: 2);
        var time = new DateTime(2021, 1, 4);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < 350; i++)
            {
                var price = i == 1 ? 2d : 1;
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, price, price, price, price, 1, true);
                var discarded = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, 99, 99, 99, 99, 1, true);
                state.Update(discarded, false, true);
                // After bar two, gains decay as (3/4)^k; losses are the convolution
                // of (3/4)^k with a negative residual decaying as (1/2)^k.
                var expected = i < 2 ? 100 : 900 / (13 - 4 * Math.Pow(2d / 3, i - 1));
                Assert.InRange(Math.Abs(state.Update(bar, false, true).Value - expected), 0, 1e-10);
                Assert.InRange(Math.Abs(state.Update(bar, true, true).Value - expected), 0, 1e-10);
            }
        }
    }

    [Fact]
    public void PsychologicalLineDoesNotInventAnUpBarAtStartup()
    {
        using var state = new PsychologicalLineState(2);
        var time = new DateTime(2021, 1, 4);
        foreach (var price in new[] { 10d, 0, -10 })
        {
            state.Reset();
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, price, price, price, price, 100, true);
            for (var i = 0; i < 4; i++)
            {
                Assert.Equal(0d, state.Update(bar, false, true).Value);
                Assert.Equal(0d, state.Update(bar, true, true).Value);
            }
            var up = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, price + 1, price + 1, price + 1, price + 1, 100, true);
            Assert.Equal(50d, state.Update(up, false, true).Value);
            Assert.Equal(0d, state.Update(bar, true, true).Value);
        }
    }

    [Fact]
    public void IntradayMomentumCountsEachCandleOnceAndExpiresIt()
    {
        var closes = new[] { 11d, 11, 9, 10, 10, 10 };
        var expected = new[] { 100d, 100, 200d / 3, 50, 0, 0 };
        var actual = new double[closes.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.ChandeIntradayMomentumIndex(
            Enumerable.Repeat(10d, closes.Length).ToArray(), closes, actual, 3);
        using var state = new ChandeIntradayMomentumIndexState(3);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < closes.Length; i++)
            {
                var time = new DateTime(2021, 1, 4).AddMinutes(i);
                var discarded = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, 10, 99, 10, 99, 100, true);
                state.Update(discarded, false, true);
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, 10, 11, 9, closes[i], 100, true);
                Assert.InRange(Math.Abs(state.Update(bar, false, true).Value - expected[i]), 0, 1e-12);
                Assert.InRange(Math.Abs(state.Update(bar, true, true).Value - expected[i]), 0, 1e-12);
                Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, 1e-12);
            }
        }
    }

    [Fact]
    public void ElderImpulseRequiresBothComponentsToMoveInTheSameDirection()
    {
        var prices = Enumerable.Repeat(10d, 30).Concat(new[] { 20d, 5 }).ToArray();
        var expected = Enumerable.Repeat(0d, 30).Concat(new[] { 1d, -1 }).ToArray();
        var bars = prices.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new ElderImpulseSystem(13))).Check(
            new IndicatorValidationContext("flat-rise-fall", bars, new IReadOnlyList<double>[] { expected }, 0));
        var actual = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.TrendCore.ElderImpulseSystem(prices, actual);
        Assert.Equal(expected, actual);
        var state = new ElderImpulseSystemState();
        for (var i = 0; i < prices.Length; i++)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, bars[i].Time, bars[i].Time,
                prices[i], prices[i], prices[i], prices[i], 100, true);
            Assert.Equal(expected[i], state.Update(bar, false, true).Value);
            Assert.Equal(expected[i], state.Update(bar, true, true).Value);
        }
        state.Reset();
        var first = new OhlcvBar("TEST", BarTimeframe.Tick, bars[0].Time, bars[0].Time, 10, 10, 10, 10, 100, true);
        Assert.Equal(0d, state.Update(first, true, true).Value);
        // Equal MACD periods make its histogram identically zero, even while the EMA falls.
        OoplesFinance.StockIndicators.Core.TrendCore.ElderImpulseSystem(new[] { 10d, 8, 6 }, actual, macdFastLength: 2, macdSlowLength: 2);
        Assert.All(actual.Take(3), value => Assert.Equal(0d, value));
    }

    [Theory]
    [InlineData(2d, 1.25, 1.125, 3.0625)]
    [InlineData(1d, .75, .875, .9375)]
    public void HampelMeasuresEveryDeviationFromTheCurrentMedian(double second, double expectedSecond, double expectedThird, double expectedLast)
    {
        var prices = new[] { 1d, second, 1, 5 };
        var expected = new[] { .5, expectedSecond, expectedThird, expectedLast };
        var bars = prices.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 100)).ToArray();
        Assert.Single(BuiltInFormulaReferences.For(new HampelFilter(3))).Check(
            new IndicatorValidationContext("current-window-mad", bars, new IReadOnlyList<double>[] { expected }, 0));
        using var state = new HampelFilterState(3);
        for (var i = 0; i < prices.Length; i++)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, bars[i].Time, bars[i].Time,
                prices[i], prices[i], prices[i], prices[i], 100, true);
            var discarded = new OhlcvBar("TEST", BarTimeframe.Tick, bars[i].Time, bars[i].Time, 99, 99, 99, 99, 100, true);
            state.Update(discarded, false, true);
            Assert.Equal(expected[i], state.Update(bar, false, true).Value);
            Assert.Equal(expected[i], state.Update(bar, true, true).Value);
        }
        state.Reset();
        var first = new OhlcvBar("TEST", BarTimeframe.Tick, bars[0].Time, bars[0].Time, 1, 1, 1, 1, 100, true);
        Assert.Equal(.5, state.Update(first, true, true).Value);
    }

    [Fact]
    public void NonlinearAndAffineAverageReferencesMatchHandExamples()
    {
        Check(new GeometricMeanMovingAverage(2), new[] { 1d, 4, 16 }, new[] { 1d, 2, 8 });
        Check(new GeometricMeanMovingAverage(2), new[] { -1d, 0, 4 }, new[] { -1d, 0, 4 });
        Check(new HarmonicMeanMovingAverage(2), new[] { 1d, 4, 16 }, new[] { 1d, 8d / 5, 32d / 5 });
        Check(new HarmonicMeanMovingAverage(2), new[] { -1d, 1, 0 }, new[] { -1d, 0, 1 });
        Check(new LeoMovingAverage(2), new[] { 1d, 2, 4 }, new[] { 2 * (2d / 3), 2 * (5d / 3) - 1.5, 2 * (10d / 3) - 3 });
        Check(new EndPointMovingAverage(3), new[] { 1d, 2, 4 }, new[] { 1d / 6, 2d / 3, 11d / 6 });
        Check(new EndPointMovingAverage(7), new[] { 1d, 2, 4 }, new[] { 0d, 0, 0 });
        Check(new GeneralizedDoubleExponentialMovingAverage(length: 2, volumeFactor: .5),
            new[] { 1d, 2, 4 }, new[] { 1d, 13d / 8, 251d / 72 });
        Check(new SimplifiedWeightedMovingAverage(2), new[] { 1d, 2, 4 }, new[] { 2d / 3, 5d / 3, 10d / 3 });
        Check(new SimplifiedLeastSquaresMovingAverage(2), new[] { 1d, 2, 4 }, new[] { 1d, 2, 4 });
        Check(new RegularizedEma(3), new[] { 1d, 2, 4 }, new[] { 1d / 3, 1, 20d / 9 });
        Check(new ZeroLowLagMovingAverage(2), new[] { 1d, 2, 4 }, new[] { .5, 1.8, 3.74 });
        Check(new Spencer15PointMovingAverage(15), new[] { 1d, 0, 0 }, new[] { -3d / 320, -6d / 320, -5d / 320 });
        Check(new Spencer21PointMovingAverage(21), new[] { 1d, 0, 0 }, new[] { -1d / 350, -3d / 350, -5d / 350 });
        Check(new AlphaDecreasingEma(14), new[] { 1d, 2, 4 }, new[] { 2d, 2, 10d / 3 });
        Check(new AhrensMovingAverage(2), new[] { 1d, 2, 4 }, new[] { .25, .6875, 2.453125 });
        Check(new SharpModifiedMovingAverage(2), new[] { 1d, 2, 4 }, new[] { .5, 2, 4 });
        Check(new SlowSmoothedMovingAverage(3), new[] { 1d, 2, 4 }, new[] { 1d, 2, 4 });
        Check(new MedianValue(3), new[] { 1d, 8, 4, 2 }, new[] { 1d, 8, 4, 4 });
        Check(new MedianValue(2), new[] { 1d, 8, 4 }, new[] { 1d, 4.5, 6 });
        Check(new Skewness(3), new[] { 1d, 1, 4 }, new[] { 0d, 0, Math.Sqrt(0.5) });
        Check(new TypicalPriceVolatility(2), new[] { 1d, 4, 8 }, new[] { 0d, 1.5, 2 });

        static void Check(IIndicator indicator, double[] input, double[] expected)
        {
            var bars = input.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
            var rule = Assert.Single(BuiltInFormulaReferences.For(indicator));
            rule.Check(new IndicatorValidationContext("hand-calculated", bars,
                new IReadOnlyList<double>[] { expected }, 0));
            Assert.Throws<InvalidOperationException>(() => rule.Check(new IndicatorValidationContext("wrong-formula", bars,
                new IReadOnlyList<double>[] { expected.Select(v => v + 1).ToArray() }, 0)));
        }
    }

    [Fact]
    public void StatisticalAndAroonReferencesMatchHandExamples()
    {
        var bars = new[] { 1d, 2, 4, 2 }.Select((v, i) => new Bar(new DateTime(2021, 1, 4).AddDays(i), v, v, v, v, 1)).ToArray();
        Check(new StandardError(3), new[] { 0d, 0, Math.Sqrt(1d / 18), Math.Sqrt(8d / 9) });
        Check(new ZScore(3), new[] { 0d, 0, 5 / Math.Sqrt(14), -1 / Math.Sqrt(2) });
        Check(new UlcerIndex(3), new[] { 0d, 0, 0, 50 / Math.Sqrt(3) });
        Check(new AroonUp(2), new[] { 0d, 0, 100, 50 });
        Check(new AroonDown(2), new[] { 0d, 0, 0, 100 });

        void Check(IIndicator indicator, double[] expected) => Assert.Single(BuiltInFormulaReferences.For(indicator))
            .Check(new IndicatorValidationContext("hand-calculated", bars, new[] { expected }, 0));
    }

    [Fact]
    public void PopulationDeviationDistinguishesConstantWindowsFromSmallRealChanges()
    {
        const int length = 101;
        var prices = Enumerable.Repeat(100.1, length + 3).ToArray();
        prices[0] = 200;
        prices[length + 2] += 1e-8;
        var output = new double[prices.Length];
        var variance = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.VolatilityCore.StandardDeviation(prices, output, length);
        OoplesFinance.StockIndicators.Core.VolatilityCore.Variance(prices, variance, length);
        using var streaming = new RollingStandardDeviation(length);
        for (var i = 0; i < prices.Length; i++)
        {
            streaming.Next(300, false);
            var preview = streaming.Next(prices[i], false);
            var actual = streaming.Next(prices[i], true);
            Assert.Equal(actual, preview);
            Assert.Equal(output[i], actual);
            Assert.InRange(Math.Abs(output[i] * output[i] - variance[i]), 0, 1e-12);
        }
        Assert.Equal(0, output[length]);
        Assert.Equal(0, output[length + 1]);
        Assert.True(output[length + 2] > 0);
        streaming.Reset();
        Assert.Equal(0, streaming.Next(100.1, true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void DirectionalOscillatorsIgnorePriceTranslationFromTheFirstBar(double shift)
    {
        var high = new[] { 12d, 17, 14 }.Select(v => v + shift).ToArray();
        var low = new[] { 9d, 14, 10 }.Select(v => v + shift).ToArray();
        var close = new[] { 11d, 16, 12 }.Select(v => v + shift).ToArray();
        var expectedUltimate = new[] { 200d / 3, 700d / 9, 700d / 12 };
        var expectedPlus = new[] { 0d, 8d / 9, 2d / 3 };
        var expectedMinus = new[] { 0d, 2d / 9, 3d / 4 };
        var ultimate = new double[3];
        var plus = new double[3];
        var minus = new double[3];
        OoplesFinance.StockIndicators.Core.OscillatorCore.UltimateOscillator(high, low, close, ultimate, 2, 2, 2);
        OoplesFinance.StockIndicators.Core.TrendCore.VortexPositive(high, low, close, plus, 2);
        OoplesFinance.StockIndicators.Core.TrendCore.VortexNegative(high, low, close, minus, 2);
        using var uo = new UltimateOscillatorState(2, 2, 2);
        using var vortex = new VortexIndicatorState(2);
        for (var i = 0; i < 3; i++)
        {
            var time = new DateTime(2021, 1, 4).AddDays(i);
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, close[i], high[i], low[i], close[i], 1, true);
            var uoPreview = uo.Update(bar, false, true).Value;
            var uoActual = uo.Update(bar, true, true).Value;
            Assert.Equal(uoPreview, uoActual);
            Assert.InRange(Math.Abs(uoActual - expectedUltimate[i]), 0, 1e-12);
            var preview = vortex.Update(bar, false, true);
            var actual = vortex.Update(bar, true, true);
            Assert.Equal(preview.Value, actual.Value);
            Assert.InRange(Math.Abs(ultimate[i] - expectedUltimate[i]), 0, 1e-12);
            Assert.InRange(Math.Abs(plus[i] - expectedPlus[i]), 0, 1e-12);
            Assert.InRange(Math.Abs(minus[i] - expectedMinus[i]), 0, 1e-12);
            Assert.InRange(Math.Abs(actual.Value - expectedPlus[i]), 0, 1e-12);
        }
        uo.Reset();
        vortex.Reset();
        var first = new OhlcvBar("TEST", BarTimeframe.Tick, DateTime.UtcNow, DateTime.UtcNow, close[0], high[0], low[0], close[0], 1, true);
        Assert.InRange(Math.Abs(uo.Update(first, true, false).Value - expectedUltimate[0]), 0, 1e-12);
        Assert.Equal(0, vortex.Update(first, true, false).Value);
    }

    [Fact]
    public void RelativeVigorUsesTheWholeRangeAtEveryLag()
    {
        var bars = Enumerable.Range(0, 8).Select(i => new Bar(new DateTime(2021, 1, 4).AddDays(i), 10, 14, 8, 12, 1)).ToArray();
        var expected = bars.Select((_, i) => i == 0 ? 0 : 1d / 3).ToArray();
        var signal = new[] { 0d, 1d / 18, 1d / 6, 5d / 18, 1d / 3, 1d / 3, 1d / 3, 1d / 3 };
        foreach (var rule in BuiltInFormulaReferences.For(new Rvi(2)))
            rule.Check(new IndicatorValidationContext("constant-body-and-range", bars, new[] { expected, signal }, 0));
        var core = new double[bars.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.RelativeVigorIndex(bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, 2);
        using var streaming = new RelativeVigorIndexState(length: 2);
        for (var i = 0; i < bars.Length; i++)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, bars[i].Time, bars[i].Time, 10, 14, 8, 12, 1, true);
            var preview = streaming.Update(bar, false, true);
            var actual = streaming.Update(bar, true, true);
            Assert.Equal(preview.Value, actual.Value);
            Assert.InRange(Math.Abs(actual.Value - expected[i]), 0, 1e-12);
            Assert.InRange(Math.Abs(core[i] - expected[i]), 0, 1e-12);
            Assert.InRange(Math.Abs(actual.Outputs!["Signal"] - signal[i]), 0, 1e-12);
        }
        streaming.Reset();
        var first = new OhlcvBar("TEST", BarTimeframe.Tick, bars[0].Time, bars[0].Time, 10, 14, 8, 12, 1, true);
        Assert.Equal(0, streaming.Update(first, true, true).Value);
    }

    [Fact]
    public void DailyPivotReferencesUseOnlyTheCompletedSession()
    {
        var day = new DateTime(2021, 1, 4);
        var bars = new[]
        {
            new Bar(day.AddHours(9), 10, 12, 9, 11, 1),
            new Bar(day.AddHours(10), 11, 15, 8, 14, 1),
            new Bar(day.AddDays(1).AddHours(9), 14, 16, 13, 15, 1),
            new Bar(day.AddDays(1).AddHours(10), 15, 30, 2, 20, 1)
        };
        var levels = new Dictionary<string, double>
        {
            ["Pivot"] = 37d / 3, ["S1"] = 29d / 3, ["S2"] = 16d / 3, ["S3"] = 8d / 3,
            ["R1"] = 50d / 3, ["R2"] = 58d / 3, ["R3"] = 71d / 3,
            ["M1"] = 4, ["M2"] = 7.5, ["M3"] = 11, ["M4"] = 14.5, ["M5"] = 18, ["M6"] = 21.5
        };
        var keys = OoplesFinance.StockIndicators.Builder.GeneratedIndicatorOutputs.KeysFor(IndicatorName.FloorPivotPoints);
        var expected = keys.Select(k => new[] { 0d, 0, levels[k], levels[k] }).ToArray();
        foreach (var rule in BuiltInFormulaReferences.For(new FloorPivotPoint()))
            rule.Check(new IndicatorValidationContext("two-sessions", bars, expected, 0));

        var camarillaKeys = OoplesFinance.StockIndicators.Builder.GeneratedIndicatorOutputs.KeysFor(IndicatorName.CamarillaPivotPoints);
        var supportSlot = Array.IndexOf(camarillaKeys.ToArray(), "S1");
        var camarillaExpected = camarillaKeys.Select(_ => new[] { 0d, 0, 1603d / 120, 1603d / 120 }).ToArray();
        BuiltInFormulaReferences.For(new CamarillaPivotPoint()).Single(r => r.ReferenceOutputSlot == supportSlot)
            .Check(new IndicatorValidationContext("two-sessions", bars, camarillaExpected, 0));
    }

    [Fact]
    public void StochasticRsiDropsTheOldExtremeAtTheRequestedLookback()
    {
        // RSI(2, Wilder) is 100, 100, 100/3, 500/7, 100/3.
        // With lookback 2 and no smoothing the stochastic is therefore 0,0,0,100,0.
        var prices = new[] { 10d, 11, 10, 11, 10 }.Concat(Enumerable.Repeat(10d, 30)).ToArray();
        var expected = new[] { 0d, 0, 0, 100, 0 }.Concat(new double[30]).ToArray();
        var bars = prices.Select((v, i) => new Bar(new DateTime(2021, 1, 4).AddDays(i), v, v, v, v, 1)).ToArray();
        var indicator = new StochasticRelativeStrengthIndex(2, 1, 1);
        foreach (var rule in BuiltInFormulaReferences.For(indicator))
            rule.Check(new IndicatorValidationContext("two-rsi-observations", bars, new[] { expected, expected }, 0));
        using var state = new StochasticRelativeStrengthIndexState(length: 2, smoothLength1: 1, smoothLength2: 1, stochLength: 2);
        for (var i = 0; i < bars.Length; i++)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, bars[i].Time, bars[i].Time, prices[i], prices[i], prices[i], prices[i], 1, true);
            var preview = state.Update(bar, false, true);
            var actual = state.Update(bar, true, true);
            Assert.Equal(preview.Value, actual.Value);
            Assert.InRange(Math.Abs(actual.Value - expected[i]), 0, 1e-10);
        }
        state.Reset();
        var first = new OhlcvBar("TEST", BarTimeframe.Tick, bars[0].Time, bars[0].Time, 10, 10, 10, 10, 1, true);
        Assert.Equal(0, state.Update(first, true, true).Value);
    }

    [Theory]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    public void RsiPreservesItsRatioWhenGainAndLossDecayTogether(MovingAvgType kind)
    {
        using var state = new RsiState(kind, 14);
        state.Next(100, true);
        state.Next(110, true);
        var last = state.Next(100, true);
        for (var i = 0; i < 200; i++)
        {
            state.Next(300, false);
            Assert.Equal(last, state.Next(100, false));
            Assert.Equal(last, state.Next(100, true));
        }
        Assert.NotEqual(last, state.Next(100.00000001, true));
        state.Reset();
        Assert.Equal(100, state.Next(100, true));
        using var periodOne = new RsiState(kind, 1);
        periodOne.Next(110, true);
        Assert.Equal(0, periodOne.Next(100, true));
        Assert.Equal(100, periodOne.Next(100, true));
    }

    [Fact]
    public void AtrFilteredEmaResumesSmoothingWhenNormalizedRangeBecomesConstant()
    {
        var bars = Enumerable.Range(0, 100).Select(i =>
        {
            var price = i % 2 == 0 ? 1d : 2d;
            return new Bar(new DateTime(2021, 1, 4).AddDays(i), price, price, price, price, 100);
        }).ToArray();
        var expected = new double[bars.Length];
        for (var i = 0; i < expected.Length; i++)
            expected[i] = i < 3 ? 1 : (expected[i - 1] + bars[i].Close) / 2;
        // ATR(2) is 0,1/4,3/4,3/4,...; its deviation(2) vanishes at bar 3.
        var indicator = new AtrFilteredExponentialMovingAverage(3, 2, 2, 2);
        BuiltInFormulaReferences.For(indicator).Single().Check(
            new IndicatorValidationContext("constant-normalized-range", bars, new[] { expected }, 0));
        using var state = new AtrFilteredExponentialMovingAverageState(length: 3, atrLength: 2, stdDevLength: 2, lbLength: 2);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, b.Time, b.Time, b.Close, b.Close, b.Close, b.Close, 100, true);
                var discarded = new OhlcvBar("TEST", BarTimeframe.Tick, b.Time, b.Time, 9, 9, 9, 9, 100, true);
                state.Update(discarded, false, true);
                Assert.Equal(expected[i], state.Update(bar, false, true).Value, 12);
                Assert.Equal(expected[i], state.Update(bar, true, true).Value, 12);
            }
        }
    }

    [Fact]
    public void CctReferencesDistinguishRawAndSmoothedOutputs()
    {
        var bars = new[] { 1d, 2, 1, 2 }.Select(v => new Bar(new DateTime(2021, 1, 4), v, v, v, v, 1)).ToArray();
        var keys = OoplesFinance.StockIndicators.Builder.GeneratedIndicatorOutputs.KeysFor(IndicatorName.CCTStochRelativeStrengthIndex);
        var expected = keys.Select(key => new[] { 0d, 0, 0, key switch
        {
            "Type1" or "Type2" or "Type3" => 100d / 3,
            "Type4" or "Signal" => 25d / 3,
            _ => 50d / 3
        } }).ToArray();
        var rules = BuiltInFormulaReferences.For(new CCTStochRelativeStrengthIndex(14)).ToArray();
        Assert.Equal(8, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("four-bar-reversal", bars, expected, 0));
    }

    [Fact]
    public void BrownCompositeAddsRsiMomentumInsteadOfTheDelayedRsiLevel()
    {
        var prices = new[] { 1d, 2, 1, 2 };
        var bars = prices.Select((v, i) => new Bar(new DateTime(2021, 1, 4).AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = new[] { new[] { 0d, 100, 0, 1900d / 21 }, new[] { 0d, 50, 50, 950d / 21 }, new[] { 0d, 0, 100d / 3, 4000d / 63 } };
        var indicator = new ConstanceBrownCompositeIndex(2, 3, 2, 1, 2);
        foreach (var rule in BuiltInFormulaReferences.For(indicator))
            rule.Check(new IndicatorValidationContext("rsi-momentum-reversal", bars, expected, 0));
        using var state = new ConstanceBrownCompositeIndexState(fastLength: 2, slowLength: 3, length1: 2, length2: 1, smoothLength: 2);
        var keys = new[] { "Cbci", "FastSignal", "SlowSignal" };
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, b.Time, b.Time, b.Close, b.Close, b.Close, b.Close, 1, true);
                var discarded = new OhlcvBar("TEST", BarTimeframe.Tick, b.Time, b.Time, 9, 9, 9, 9, 1, true);
                state.Update(discarded, false, true);
                var preview = state.Update(bar, false, true);
                var final = state.Update(bar, true, true);
                for (var slot = 0; slot < keys.Length; slot++)
                {
                    Assert.InRange(Math.Abs(preview.Outputs![keys[slot]] - expected[slot][i]), 0, 1e-10);
                    Assert.InRange(Math.Abs(final.Outputs![keys[slot]] - expected[slot][i]), 0, 1e-10);
                }
            }
        }
    }

    [Fact]
    public void CorrectedAverageHasExactlyZeroGainAtTheVarianceBoundary()
    {
        using var state = new CorrectedMovingAverageState(length: 2);
        var prices = new[] { 1d, 3 }.Concat(Enumerable.Range(0, 100).Select(i => i % 2 == 0 ? 2d : 4d)).ToArray();
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var t = new DateTime(2021, 1, 4).AddDays(i);
                var value = prices[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, t, t, value, value, value, value, 1, true);
                var discarded = new OhlcvBar("TEST", BarTimeframe.Tick, t, t, 9, 9, 9, 9, 1, true);
                state.Update(discarded, false, true);
                Assert.Equal(i == 0 ? 0d : 2, state.Update(bar, false, true).Value);
                Assert.Equal(i == 0 ? 0d : 2, state.Update(bar, true, true).Value);
            }
        }
    }

    [Fact]
    public async Task AutonomousRecursiveAverageUsesItsMomentumLagWithShortSmoothingWindows()
    {
        var prices = new[] { 1d, 2, 10 };
        var expected = new[] { 1d, 1, 4.375 };
        var bars = prices.Select((v, i) => new Bar(new DateTime(2021, 1, 4).AddDays(i), v, v, v, v, 1)).ToArray();
        BuiltInFormulaReferences.For(new AutonomousRecursiveMa(2)).Single().Check(
            new IndicatorValidationContext("shorter-than-momentum-lag", bars, new[] { expected }, 0));
        var core = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.AutonomousRecursiveMovingAverage(prices, core, 2);
        Assert.Equal(expected, core);
        OoplesFinance.StockIndicators.Core.MovingAverageCore.AutonomousRecursiveMovingAverage(Array.Empty<double>(), Array.Empty<double>(), 2);
        using var state = new AutonomousRecursiveMovingAverageState(length: 2);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var t = bars[i].Time;
                var v = prices[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, t, t, v, v, v, v, 1, true);
                Assert.Equal(expected[i], state.Update(bar, false, true).Value);
                Assert.Equal(expected[i], state.Update(bar, true, true).Value);
            }
        }
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(AutonomousRecursiveMa), "period-two",
            () => new AutonomousRecursiveMa(2)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public void BayesianOscillatorNormalizesBothEvidenceProducts()
    {
        var prices = new[] { 1d, 2, 1, 2 };
        var bars = prices.Select((v, i) => new Bar(new DateTime(2021, 1, 4).AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = new[] { new[] { 1d, 1, .8, .5 }, new[] { 0d, 0, .2, .5 }, new[] { 0d, 0, .5, .5 } };
        var rules = BuiltInFormulaReferences.For(new BayesianOscillator(3)).ToArray();
        Assert.Equal(3, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("normalized-evidence", bars, expected, 0));
        using var state = new BayesianOscillatorState(length: 3);
        var keys = new[] { "SigmaProbsDown", "SigmaProbsUp", "ProbPrime" };
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var t = bars[i].Time;
                var v = prices[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, t, t, v, v, v, v, 1, true);
                state.Update(new OhlcvBar("TEST", BarTimeframe.Tick, t, t, 9, 9, 9, 9, 1, true), false, true);
                var preview = state.Update(bar, false, true);
                var final = state.Update(bar, true, true);
                for (var slot = 0; slot < keys.Length; slot++)
                {
                    Assert.InRange(final.Outputs![keys[slot]], 0, 1);
                    Assert.InRange(Math.Abs(final.Outputs[keys[slot]] - expected[slot][i]), 0, 1e-12);
                    Assert.Equal(final.Outputs[keys[slot]], preview.Outputs![keys[slot]]);
                }
            }
        }
        Assert.Equal(0, OoplesFinance.StockIndicators.Helpers.BayesianProbability.Combine(0, 1));
        Assert.Equal(1, OoplesFinance.StockIndicators.Helpers.BayesianProbability.Combine(1, 1));
        Assert.Equal(.5, OoplesFinance.StockIndicators.Helpers.BayesianProbability.Combine(.5, .5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(500)]
    public void AdaptiveLeastSquaresReproducesAffinePricesWithVariableWeights(int length)
    {
        var prices = Enumerable.Range(0, 300).Select(i => 10000 + 2.5 * i).ToArray();
        var core = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.AdaptiveLeastSquares(prices, core, length);
        OoplesFinance.StockIndicators.Core.MovingAverageCore.AdaptiveLeastSquares(Array.Empty<double>(), Array.Empty<double>(), length);
        using var state = new AdaptiveLeastSquaresState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var time = new DateTime(2021, 1, 4).AddDays(i);
                var price = prices[i];
                var width = 1 + i % 23;
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, price, price + width, price - width, price, 1, true);
                state.Update(new OhlcvBar("TEST", BarTimeframe.Tick, time, time, 9, 9, 9, 9, 1, true), false, true);
                var preview = state.Update(bar, false, true).Value;
                var actual = state.Update(bar, true, true).Value;
                Assert.InRange(Math.Abs(price - actual), 0, 1e-8);
                Assert.Equal(actual, preview);
                Assert.InRange(Math.Abs(price - core[i]), 0, 1e-8);
            }
        }
    }

    [Fact]
    public async Task OneSampleHammingWindowIsIdentityAndHasAFiniteDerivative()
    {
        var prices = new[] { 3d, -2, 0, 7 };
        var result = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersHammingMovingAverage(prices, result, 1);
        Assert.Equal(prices, result);
        using var state = new EhlersHammingMovingAverageState(length: 1);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var time = new DateTime(2021, 1, 4).AddDays(i);
                var v = prices[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, v, v, v, v, 1, true);
                Assert.Equal(v, state.Update(bar, false, true).Value);
                Assert.Equal(v, state.Update(bar, true, true).Value);
            }
        }
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(EhlersHammingWindowIndicator),
            "one-sample", () => new EhlersHammingWindowIndicator(1)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task EhlersSpearmanUsesCompleteRanksAndCorrectTieNormalization()
    {
        var cases = new (double[] Prices, double Expected)[]
        {
            (new[] { 1d, 2, 3, 4 }, 1),
            (new[] { 4d, 3, 2, 1 }, -1),
            (new[] { 1d, 3, 2, 4 }, .8),
            (new[] { 1d, 1, 2, 3 }, 3 / Math.Sqrt(10)),
            (new[] { 7d, 7, 7, 7 }, 0),
            (new[] { 7d }, 0)
        };
        foreach (var (prices, expected) in cases)
        {
            var core = new double[prices.Length];
            OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersSpearmanRankIndicator(prices, core, prices.Length);
            Assert.InRange(Math.Abs(core[core.Length - 1] - expected), 0, 1e-12);
            using var state = new EhlersSpearmanRankIndicatorState(prices.Length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < prices.Length; i++)
                {
                    var time = new DateTime(2021, 1, 4).AddDays(i);
                    var v = prices[i];
                    var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, v, v, v, v, 1, true);
                    state.Update(new OhlcvBar("TEST", BarTimeframe.Tick, time, time, -9, -9, -9, -9, 1, true), false, true);
                    var preview = state.Update(bar, false, true).Value;
                    var actual = state.Update(bar, true, true).Value;
                    Assert.Equal(core[i], actual);
                    Assert.Equal(actual, preview);
                }
            }
        }
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(EhlersSpearmanRankIndicator),
            "one-sample", () => new EhlersSpearmanRankIndicator(1)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task EhlersZeroLagEmaUsesTheActualHalfPeriodDelay()
    {
        var prices = new[] { 1d, 2, 3, 4 };
        var expected = new[] { 1d, 2, 8d / 3, 23d / 6 };
        var result = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersZeroLagExponentialMovingAverage(prices, result, 3);
        using var state = new EhlersZeroLagExponentialMovingAverageState(length: 3);
        for (var i = 0; i < prices.Length; i++)
        {
            var time = new DateTime(2021, 1, 4).AddDays(i);
            var v = prices[i];
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, v, v, v, v, 1, true);
            Assert.InRange(Math.Abs(expected[i] - result[i]), 0, 1e-12);
            Assert.InRange(Math.Abs(expected[i] - state.Update(bar, false, true).Value), 0, 1e-12);
            Assert.InRange(Math.Abs(expected[i] - state.Update(bar, true, true).Value), 0, 1e-12);
        }
        OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersZeroLagExponentialMovingAverage(prices, result, 1);
        Assert.Equal(prices, result);
        foreach (var length in new[] { 1, 3 })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(EhlersZeroLagEma),
                "short-period", () => new EhlersZeroLagEma(length)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task FramaUsesEqualHalvesAndPublishedStartup(int requestedLength)
    {
        var prices = new[] { 1d, 3, 2, 4, 6 };
        var highs = Enumerable.Repeat(10d, prices.Length).ToArray();
        var lows = new double[prices.Length];
        var expected = new[] { 1d, 3, 2, 4, 4 + 2 * Math.Exp(-4.6) };
        var actual = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.FractalAdaptiveMovingAverage(highs, lows, prices, actual, requestedLength);
        using var state = new EhlersFractalAdaptiveMovingAverageState(requestedLength);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var time = new DateTime(2021, 1, 4).AddDays(i);
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, prices[i], highs[i], lows[i], prices[i], 1, true);
                state.Update(new OhlcvBar("TEST", BarTimeframe.Tick, time, time, 100, 100, 100, 100, 1, true), false, true);
                Assert.InRange(Math.Abs(expected[i] - actual[i]), 0, 1e-12);
                Assert.InRange(Math.Abs(expected[i] - state.Update(bar, false, true).Value), 0, 1e-12);
                Assert.InRange(Math.Abs(expected[i] - state.Update(bar, true, true).Value), 0, 1e-12);
            }
        }
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(Frama), "odd-or-even",
            () => new Frama(requestedLength)), new() { RequireFormulaReference = true });
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(EhlersFrama), "minimum",
            () => new EhlersFrama(1)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task FisherTransformIsNeutralOnFlatPricesAndOddUnderReflection()
    {
        var prices = new[] { 10d, 10, 11, 9, 12, 12, 12, 12, 12, 12 };
        var reflected = prices.Select(v => 20 - v).ToArray();
        var forward = new double[prices.Length];
        var reverse = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersFisherTransform(prices, forward, 3);
        OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersFisherTransform(reflected, reverse, 3);
        Assert.Equal(0, forward[0]);
        Assert.Equal(0, forward[1]);
        Assert.InRange(Math.Abs(forward[2] - .5 * Math.Log(1.33 / .67)), 0, 1e-12);
        for (var i = 0; i < prices.Length; i++)
            Assert.InRange(Math.Abs(forward[i] + reverse[i]), 0, 1e-12);
        var flat = new double[100];
        OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersFisherTransform(Enumerable.Repeat(10d, 100).ToArray(), flat, 3);
        Assert.All(flat, v => Assert.Equal(0, v));
        foreach (var length in new[] { 1, 3 })
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(typeof(FisherTransform), "neutral-flat",
                () => new FisherTransform(length)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public void LaguerreRsiCancelsTheCommonPriceLevelBeforeFiltering()
    {
        var prices = new[] { 0d, 1, 0, 3, 3, -1, 0, 0 };
        var shifted = prices.Select(v => v + 1e9).ToArray();
        var expected = new double[prices.Length];
        var actual = new double[prices.Length];
        var gamma = 1 - 2d / 15;
        OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersLaguerreRelativeStrengthIndex(prices, expected, gamma);
        OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersLaguerreRelativeStrengthIndex(shifted, actual, gamma);
        Assert.Equal(expected, actual);
        var flat = new double[100];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersLaguerreRelativeStrengthIndex(Enumerable.Repeat(10.1, 100).ToArray(), flat, gamma);
        Assert.All(flat, value => Assert.Equal(0, value));
        var state = new EhlersLaguerreRelativeStrengthIndexState(gamma);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var time = new DateTime(2021, 1, 4).AddDays(i);
                var v = shifted[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, v, v, v, v, 1, true);
                Assert.Equal(expected[i], state.Update(bar, false, true).Value);
                Assert.Equal(expected[i], state.Update(bar, true, true).Value);
            }
        }
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(2, 4)]
    [InlineData(8, 1)]
    [InlineData(8, 4)]
    [InlineData(64, 1)]
    [InlineData(64, 4)]
    public void GaussianFilterHasHalfPowerAtItsConfiguredCutoff(int length, int poles)
    {
        var prices = Enumerable.Range(0, 40 * length).Select(i => Math.Cos(2 * Math.PI * i / length)).ToArray();
        var filtered = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersGaussianFilter(prices, filtered, length, poles);
        var inputPower = prices.Skip(20 * length).Sum(v => v * v);
        var outputPower = filtered.Skip(20 * length).Sum(v => v * v);
        Assert.InRange(Math.Abs(outputPower / inputPower - .5), 0, 1e-10);
        using var state = new EhlersGaussianFilterState(length, poles);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < 10; i++)
            {
                var time = new DateTime(2021, 1, 4).AddDays(i);
                var v = prices[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, v, v, v, v, 1, true);
                state.Update(new OhlcvBar("TEST", BarTimeframe.Tick, time, time, 99, 99, 99, 99, 1, true), false, true);
                Assert.Equal(filtered[i], state.Update(bar, false, true).Value);
                Assert.Equal(filtered[i], state.Update(bar, true, true).Value);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(800)]
    public async Task PoleFiltersMatchTheirTransferFunctionsBeyondFormerCoefficientClamps(int length)
    {
        Func<IIndicator>[] factories =
        {
            () => new Ehlers2PoleButterworthFilterV1(length),
            () => new Ehlers2PoleButterworthFilterV2(length),
            () => new Ehlers3PoleButterworthFilterV1(length),
            () => new Ehlers3PoleButterworthFilterV2(length),
            () => new Ehlers2PoleSuperSmootherFilterV1(length),
            () => new Ehlers2PoleSuperSmootherFilterV2(length),
            () => new Ehlers3PoleSuperSmootherFilter(length)
        };
        foreach (var factory in factories)
            await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(factory().GetType(),
                "unclamped-period-" + length, factory), new() { RequireFormulaReference = true });
    }

    [Fact]
    public void TwoPoleButterworthV2RejectsNyquistWithItsSymmetricInputKernel()
    {
        var prices = Enumerable.Range(0, 512).Select(i => i % 2 == 0 ? 1d : -1d).ToArray();
        var filtered = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.Ehlers2PoleButterworthFilterV2(prices, filtered, 14);
        Assert.All(filtered.Skip(400), value => Assert.InRange(Math.Abs(value), 0, 1e-12));
        using var state = new Ehlers2PoleButterworthFilterV2State(14);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var time = new DateTime(2021, 1, 4).AddDays(i);
                var value = prices[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, time, time, value, value, value, value, 1, true);
                state.Update(new OhlcvBar("TEST", BarTimeframe.Tick, time, time, 77, 77, 77, 77, 1, true), false, true);
                Assert.Equal(filtered[i], state.Update(bar, false, true).Value);
                Assert.Equal(filtered[i], state.Update(bar, true, true).Value);
            }
        }
    }

    public sealed class FiniteSecondary : MultiOutputIndicatorBase
    {
        public FiniteSecondary() : base(2) { }
        protected internal override object CreateState() => new State();
        private sealed class State : IMultiOutputState
        {
            public void Reset() { }
            public void Update(in Bar bar, Span<double> outputs)
            { outputs[0] = bar.Close; outputs[1] = 2 * bar.Close + 1; }
        }
    }
}

