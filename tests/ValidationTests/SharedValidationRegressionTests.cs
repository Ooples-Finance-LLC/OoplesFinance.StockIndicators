using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SharedValidationRegressionTests
{
    [Theory]
    [InlineData(-2d)]
    [InlineData(0d)]
    [InlineData(2d)]
    public async Task OnBalanceVolumeUsesThePublishedSeedForEveryPriceSign(double first)
    {
        var prices = new[] { first, first + 1, first + 1, first - 1 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var batch = Stock(bars).CalculateOnBalanceVolume(length: 2);
        var seed = Math.Sign(first);
        Assert.Equal(new[] { (double)seed, seed + 1d, seed + 1d, seed }, batch.OutputValues["Obv"]);
        await CompareRoutes(new OnBalanceVolume(2), batch, new OnBalanceVolumeState(2), bars);
    }

    [Fact]
    public async Task LongButterworthStartupRetainsPrecisionThroughPreviewAndReset()
    {
        var bars = Enumerable.Range(0, 700).Select(i =>
        {
            var value = i % 2 == 0 ? 80d : 120d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1000);
        }).ToArray();
        await CompareRoutes(new Ehlers3PoleButterworthFilterV2(800),
            Stock(bars).CalculateEhlers3PoleButterworthFilterV2(800), new Ehlers3PoleButterworthFilterV2State(800), bars);
        await CompareRoutes(new Ehlers3PoleSuperSmootherFilter(800),
            Stock(bars).CalculateEhlers3PoleSuperSmootherFilter(800), new Ehlers3PoleSuperSmootherFilterState(800), bars);
    }

    [Fact]
    public async Task AdaptiveEhlersV2OutputsSupportShortWindowsAndPreviewReset()
    {
        foreach (var bars in new[] { Market(90), Market(90, 100), Market(90, 0) })
        foreach (var period in new[] { 1, 4, 12 })
        {
            await CompareRoutes(new EhlersAdaptiveRelativeStrengthIndexV2(period, 3, 1),
                Stock(bars).CalculateEhlersAdaptiveRelativeStrengthIndexV2(length1: period, length2: 3, length3: 1),
                new EhlersAdaptiveRelativeStrengthIndexV2State(length1: period, length2: 3, length3: 1), bars);
            await CompareRoutes(new EhlersAdaptiveStochasticIndicatorV2(period, 3, 1),
                Stock(bars).CalculateEhlersAdaptiveStochasticIndicatorV2(length1: period, length2: 3, length3: 1),
                new EhlersAdaptiveStochasticIndicatorV2State(length1: period, length2: 3, length3: 1), bars);
            await CompareRoutes(new EhlersAdaptiveCommodityChannelIndexV2(period, 3, 1),
                Stock(bars).CalculateEhlersAdaptiveCommodityChannelIndexV2(length1: period, length2: 3, length3: 1),
                new EhlersAdaptiveCommodityChannelIndexV2State(length1: period, length2: 3, length3: 1), bars);
        }
    }

    [Fact]
    public async Task AdaptiveFisherTransformsUseNormalizedInputsAndPreservePreviewReset()
    {
        foreach (var period in new[] { 1, 4, 12 })
        {
            var bars = Market(90);
            var rsi = Stock(bars).CalculateEhlersAdaptiveRelativeStrengthIndexV2(length1: period, length2: 3, length3: 1);
            var fisher = Stock(bars).CalculateEhlersAdaptiveRsiFisherTransformV2(length1: period, length2: 3, length3: 1);
            for (var i = 0; i < bars.Length; i++)
            {
                var x = Math.Clamp(3 * (rsi.CustomValuesList[i] - .5), -.999, .999);
                Close((Math.Log(1 + x) - Math.Log(1 - x)) / 2, fisher.CustomValuesList[i]);
            }
            Assert.Contains(fisher.CustomValuesList, value => value > 0);
            await CompareRoutes(new EhlersAdaptiveRsiFisherTransformV2(period, 3, 1), fisher,
                new EhlersAdaptiveRsiFisherTransformV2State(length1: period, length2: 3, length3: 1), bars);
            await CompareRoutes(new EhlersAdaptiveStochasticInverseFisherTransform(period, 3, 1),
                Stock(bars).CalculateEhlersAdaptiveStochasticInverseFisherTransform(length1: period, length2: 3, length3: 1),
                new EhlersAdaptiveStochasticInverseFisherTransformState(length1: period, length2: 3, length3: 1), bars);
        }
    }

    [Fact]
    public async Task DemarkPatternsSupportMinimumPeriodsAndPreviewReset()
    {
        foreach (var bars in new[] { Market(90), Market(90, 0) })
        foreach (var period in new[] { 1, 2, 9 })
        {
            await CompareRoutes(new DemarkSetupIndicator(period), Stock(bars).CalculateDemarkSetupIndicator(period),
                new DemarkSetupIndicatorState(period), bars);
            await CompareRoutes(new DemarkReversalPoints(period, 2), Stock(bars).CalculateDemarkReversalPoints(period, 2),
                new DemarkReversalPointsState(period, 2), bars);
            await CompareRoutes(new DemarkRangeExpansionIndex(period), Stock(bars).CalculateDemarkRangeExpansionIndex(period),
                new DemarkRangeExpansionIndexState(period), bars);
        }
    }

    [Fact]
    public async Task PressureAndRankReferencesMatchAllRoutesIncludingGapDownsAndTies()
    {
        var gap = new[] { new Bar(DateTime.UnixEpoch, 9, 11, 8, 10, 1),
            new Bar(DateTime.UnixEpoch.AddMinutes(1), 8, 9, 6, 7, 1) };
        var pressure = Stock(gap).CalculateDemarkPressureRatioV1(2);
        Close(100d / 7, pressure.CustomValuesList[1]);
        await CompareRoutes(new DemarkPressureRatioV1(2), pressure, new DemarkPressureRatioV1State(2), gap);
        foreach (var bars in new[] { Market(90), Market(90, 100) })
        foreach (var period in new[] { 1, 2, 10 })
        {
            await CompareRoutes(new DemarkPressureRatioV1(period), Stock(bars).CalculateDemarkPressureRatioV1(period),
                new DemarkPressureRatioV1State(period), bars);
            await CompareRoutes(new SpearmanIndicator(period, 3), Stock(bars).CalculateSpearmanIndicator(length: period),
                new SpearmanIndicatorState(length: period), bars);
            await CompareRoutes(new SentimentZoneOscillator(period), Stock(bars).CalculateSentimentZoneOscillator(fastLength: period),
                new SentimentZoneOscillatorState(fastLength: period), bars);
        }
        await CompareRoutes(new DemandIndex(1), Stock(gap).CalculateDemandIndex(), new DemandIndexState(), gap);
    }

    [Fact]
    public async Task DailyPivotsRemainFixedWithinSessionsAndSupportPreviewReset()
    {
        var bars = Enumerable.Range(0, 15).Select(i =>
        {
            var value = 10d + i;
            return new Bar(DateTime.UnixEpoch.AddDays(i / 3).AddHours(i % 3), value, value + 2, value - 1, value + 1, 1);
        }).ToArray();
        var standard = Stock(bars).CalculateStandardPivotPoints();
        foreach (var values in standard.OutputValues.Values)
            for (var i = 0; i < bars.Length; i++)
                Close(values[i / 3 * 3], values[i]);
        await CompareRoutes(new StandardPivotPoints(), standard, new StandardPivotPointsState(), bars);
        await CompareRoutes(new DynamicPivotPoints(), Stock(bars).CalculateDynamicPivotPoints(), new DynamicPivotPointsState(), bars);
    }

    [Fact]
    public async Task ChannelOutputsPreserveDistinctLinesAndMinimumWindows()
    {
        foreach (var bars in new[] { Market(90), Market(90, 100) })
        foreach (var period in new[] { 1, 2, 12 })
        {
            await CompareRoutes(new StationaryExtrapolatedLevels(period), Stock(bars).CalculateStationaryExtrapolatedLevels(length: period),
                new StationaryExtrapolatedLevelsState(length: period), bars);
            await CompareRoutes(new ScalpersChannel(period, 3), Stock(bars).CalculateScalpersChannel(length1: period, length2: 3),
                new ScalpersChannelState(length1: period, length2: 3), bars);
            await CompareRoutes(new RateOfChangeBands(period, 3), Stock(bars).CalculateRateOfChangeBands(length: period, smoothLength: 3),
                new RateOfChangeBandsState(length: period, smoothLength: 3), bars);
        }
    }

    [Fact]
    public async Task PriceEnvelopesContainPriceAndPreserveAllOutputs()
    {
        foreach (var bars in new[] { Market(90), Market(90, 100) })
        foreach (var period in new[] { 1, 2, 10 })
        {
            var line = Stock(bars).CalculatePriceLineChannel(length: period);
            var curve = Stock(bars).CalculatePriceCurveChannel(length: period);
            foreach (var channel in new[] { line, curve })
                for (var i = 0; i < bars.Length; i++)
                {
                    Assert.True(channel.OutputValues["UpperBand"][i] >= bars[i].Close);
                    Assert.True(channel.OutputValues["LowerBand"][i] <= bars[i].Close);
                }
            await CompareRoutes(new PriceLineChannel(period), line, new PriceLineChannelState(length: period), bars);
            await CompareRoutes(new PriceCurveChannel(period), curve, new PriceCurveChannelState(length: period), bars);
        }
    }

    [Fact]
    public async Task RelativeMotionOutputsMatchAcrossPeriodsAndPreviewReset()
    {
        foreach (var bars in new[] { Market(90), Market(90, 100) })
        foreach (var period in new[] { 1, 2, 10 })
        {
            await CompareRoutes(new RandomWalkIndex(period), Stock(bars).CalculateRandomWalkIndex(length: period),
                new RandomWalkIndexState(length: period), bars);
            await CompareRoutes(new RunningEquity(period), Stock(bars).CalculateRunningEquity(length: period),
                new RunningEquityState(length: period), bars);
            await CompareRoutes(new RegressionOscillator(period), Stock(bars).CalculateRegressionOscillator(period),
                new RegressionOscillatorState(period), bars);
            await CompareRoutes(new RecursiveDifferenciator(period), Stock(bars).CalculateRecursiveDifferenciator(length: period),
                new RecursiveDifferenciatorState(length: period), bars);
            await CompareRoutes(new RelativeSpreadStrength(2, 4, period, 2),
                Stock(bars).CalculateRelativeSpreadStrength(fastLength: 2, slowLength: 4, length: period, smoothLength: 2),
                new RelativeSpreadStrengthState(fastLength: 2, slowLength: 4, length: period, smoothLength: 2), bars);
            await CompareRoutes(new ReversalPoints(period), Stock(bars).CalculateReversalPoints(length: period),
                new ReversalPointsState(length: period), bars);
            await CompareRoutes(new SimpleCycle(period), Stock(bars).CalculateSimpleCycle(period), new SimpleCycleState(period), bars);
            await CompareRoutes(new SimpleLines(period), Stock(bars).CalculateSimpleLines(period),
                new SimpleLinesState(period), bars);
        }
    }

    [Fact]
    public async Task RatioRetentionAndTrigonometricFiltersMatchAcrossRoutes()
    {
        foreach (var bars in new[] { Market(90), Market(90, 100) })
        foreach (var period in new[] { 1, 2, 10 })
        {
            await CompareRoutes(new RetentionAccelerationFilter(period), Stock(bars).CalculateRetentionAccelerationFilter(period),
                new RetentionAccelerationFilterState(period), bars);
            await CompareRoutes(new TrigonometricOscillator(period), Stock(bars).CalculateTrigonometricOscillator(period),
                new TrigonometricOscillatorState(period), bars);
        }
        var changingBodies = Enumerable.Range(0, 90).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 12, 8, 8 + i % 5, i)).ToArray();
        await CompareRoutes(new RatioOchlAverager(), Stock(changingBodies).CalculateRatioOCHLAverager(),
            new RatioOCHLAveragerState(), changingBodies);
    }

    [Fact]
    public async Task PrecisionSpreadSupportsSimpleAndMultipleExponentialStages()
    {
        var cases = new (MovingAvgType Kind, IMovingAverage Average)[] {
            (MovingAvgType.SimpleMovingAverage, new Sma()),
            (MovingAvgType.DoubleExponentialMovingAverage, new Dema()),
            (MovingAvgType.TripleExponentialMovingAverage, new Tema())
        };
        foreach (var (kind, average) in cases)
        {
            var bars = Market(90);
            var indicator = new RelativeSpreadStrength(3, 7, 5, 2, average);
            var batch = Stock(bars).CalculateRelativeSpreadStrength(kind, 3, 7, 5, 2);
            using var state = new RelativeSpreadStrengthState(kind, 3, 7, 5, 2);
            await CompareRoutes(indicator, batch, state, bars);
            var rule = Assert.Single(BuiltInFormulaReferences.For(indicator));
            rule.Check(new IndicatorValidationContext("precision-stages", bars, [batch.CustomValuesList], 0));
        }
    }

    [Fact]
    public async Task KaseStopsScaleAsPricesAndEveryPublishedOutputMatchesAcrossRoutes()
    {
        foreach (var period in new[] { 1, 2, 10 })
        {
            var bars = Market(90);
            var scaled = bars.Select(b => new Bar(b.Time, b.Open * 3, b.High * 3, b.Low * 3, b.Close * 3, b.Volume)).ToArray();
            var batch = Stock(bars).CalculateKaseDevStopV2(fastLength: 2, slowLength: 5, length: period);
            var changedUnits = Stock(scaled).CalculateKaseDevStopV2(fastLength: 2, slowLength: 5, length: period);
            foreach (var pair in batch.OutputValues)
                for (var i = 0; i < bars.Length; i++) Close(3 * pair.Value[i], changedUnits.OutputValues[pair.Key][i]);
            await CompareRoutes(new KaseDevStopV2(2, 5, period), batch,
                new KaseDevStopV2State(fastLength: 2, slowLength: 5, length: period), bars);
            await CompareRoutes(new KaseIndicator(period), Stock(bars).CalculateKaseIndicator(length: period),
                new KaseIndicatorState(length: period), bars);
        }
    }

    [Fact]
    public async Task KasePeakVolatilityReturnsToZeroAfterASpikeLeavesItsWindows()
    {
        var bars = Enumerable.Range(0, 300).Select(i =>
        {
            var close = i == 150 ? 200d : 100d;
            var open = i == 151 ? 200d : 100d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), open, Math.Max(open, close) + .5,
                Math.Min(open, close) - .5, close, 1000);
        }).ToArray();
        var batch = Stock(bars).CalculateKasePeakOscillatorV2(length2: 7);
        Assert.All(batch.CustomValuesList.Skip(175), value => Close(0, value));
        await CompareRoutes(new KasePeakOscillatorV2(7), batch, new KasePeakOscillatorV2State(length2: 7), bars);
    }

    [Fact]
    public async Task EvenBetterSineWaveRetainsItsAlternatingTailAcrossPriceScales()
    {
        var bars = Enumerable.Range(0, 256).Select(i =>
        {
            var price = i % 2 == 0 ? 80d : 120d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
        }).ToArray();
        var expected = Stock(bars).CalculateEhlersEvenBetterSineWaveIndicator(20, 5);
        var pole = Math.Cos(2 * Math.PI / 20) / (1 + Math.Sin(2 * Math.PI / 20));
        var limit = (1 + pole + pole * pole) / Math.Sqrt(3 * (1 + pole * pole + Math.Pow(pole, 4)));
        Close(limit, expected.CustomValuesList[255]);
        foreach (var scale in new[] { 1d, Math.Pow(2, 600), Math.Pow(2, -600) })
        {
            var scaled = bars.Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, b.Volume)).ToArray();
            var actual = Stock(scaled).CalculateEhlersEvenBetterSineWaveIndicator(20, 5);
            for (var i = 0; i < bars.Length; i++) Close(expected.CustomValuesList[i], actual.CustomValuesList[i]);
            await CompareRoutes(new EhlersEvenBetterSineWaveIndicator(20, 5), actual,
                new EhlersEvenBetterSineWaveIndicatorState(20, 5), scaled);
        }
    }

    [Fact]
    public async Task VariableAverageSettledRanksRemainAccurateAcrossPriceOffsets()
    {
        foreach (var period in new[] { 3, 6, 15 })
            foreach (var shape in new[] { "rising", "falling", "alternating" })
                foreach (var offset in new[] { 0d, 1000000 })
                {
                    var bars = Enumerable.Range(0, 600).Select(i =>
                    {
                        var price = offset + (shape == "rising" ? 100 + .1 * i : shape == "falling" ? 100 / (1 + .002 * i) : i % 2 == 0 ? 80 : 120);
                        return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1000);
                    }).ToArray();
                    var indicator = new Vma(period);
                    var expected = Stock(bars).CalculateVariableMovingAverage(period);
                    Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("settled-variable-rank", bars,
                        new[] { (IReadOnlyList<double>)expected.OutputValues["Vma"] }, 0));
                    await CompareRoutes(indicator, expected, new VariableMovingAverageState(period), bars);
                }
    }

    [Theory]
    [InlineData(.1, .2)]
    [InlineData(1e200, 1e-200)]
    [InlineData(-3.141592653589793, 2.718281828459045)]
    [InlineData(1.0000000000000002, 1.0000000000000004)]
    public void VariableAverageLegacyProductResidualMatchesFusedArithmetic(double left, double right)
    {
        var product = left * right;
        Assert.Equal(Math.FusedMultiplyAdd(left, right, -product), VariableAverageNumber.ProductResidual(left, right, product));
    }

    [Fact]
    public async Task VolatilityAdaptivePairsMatchBothOutputs()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 20 })
        {
            await CompareRoutes(new ChandeVolatilityIndexDynamicAverageIndicator(period, .2, .04), Stock(bars).CalculateChandeVolatilityIndexDynamicAverageIndicator(length: period),
                new ChandeVolatilityIndexDynamicAverageIndicatorState(length: period), bars);
            await CompareRoutes(new VolatilityIndexDynamicAverageIndicator(period, .2, .04), Stock(bars).CalculateVolatilityIndexDynamicAverageIndicator(length: period),
                new VolatilityIndexDynamicAverageIndicatorState(length: period), bars);
            await CompareRoutes(new UhlMaCrossoverSystem(period), Stock(bars).CalculateUhlMaCrossoverSystem(length: period),
                new UhlMaCrossoverSystemState(length: period), bars);
        }
    }

    [Fact]
    public async Task VariableAveragesAndBandsMatchAllOutputs()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 6 })
        {
            await CompareRoutes(new Vma(period), Stock(bars).CalculateVariableMovingAverage(period), new VariableMovingAverageState(period), bars);
            await CompareRoutes(new Svama(period), Stock(bars).CalculateSvama(period), new SvamaState(period), bars);
            await CompareRoutes(new VariableMovingAverageBands(period, 1.5), Stock(bars).CalculateVariableMovingAverageBands(length: period),
                new VariableMovingAverageBandsState(length: period), bars);
            await CompareRoutes(new UltimateMovingAverageBands(period, 20, 2), Stock(bars).CalculateUltimateMovingAverageBands(minLength: period, maxLength: 20),
                new UltimateMovingAverageBandsState(minLength: period, maxLength: 20), bars);
        }
    }

    [Fact]
    public async Task CycleNoiseAndTrendlineMatchEveryPublishedOutput()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 6 })
        {
            await CompareRoutes(new EhlersAlternateSignalToNoiseRatio(period), Stock(bars).CalculateEhlersAlternateSignalToNoiseRatio(period),
                new EhlersAlternateSignalToNoiseRatioState(period), bars);
            await CompareRoutes(new EhlersEnhancedSignalToNoiseRatio(period), Stock(bars).CalculateEhlersEnhancedSignalToNoiseRatio(period),
                new EhlersEnhancedSignalToNoiseRatioState(period), bars);
            await CompareRoutes(new EhlersSignalToNoiseRatioV1(period), Stock(bars).CalculateEhlersSignalToNoiseRatioV1(length: period),
                new EhlersSignalToNoiseRatioV1State(length: period), bars);
            await CompareRoutes(new EhlersSignalToNoiseRatioV2(period, MovingAvgType.ExponentialMovingAverage), Stock(bars).CalculateEhlersSignalToNoiseRatioV2(period),
                new EhlersSignalToNoiseRatioV2State(period), bars);
        }
        await CompareRoutes(new EhlersInstantaneousTrendlineV1(), Stock(bars).CalculateEhlersInstantaneousTrendlineV1(),
            new EhlersInstantaneousTrendlineV1State(), bars);
    }

    [Fact]
    public async Task AdaptiveV1OscillatorsMatchAllOutputsAndLongCycleFractions()
    {
        var bars = Enumerable.Range(0, 320).Select(i =>
        {
            var price = 100 + 7 * Math.Sin(i * .1);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + 1, price - 1, price, 1000);
        }).ToArray();
        foreach (var fraction in new[] { .25, .5, 1, 3 })
        {
            await CompareRoutes(new EhlersAdaptiveRelativeStrengthIndexV1(fraction), Stock(bars).CalculateEhlersAdaptiveRelativeStrengthIndexV1(fraction),
                new EhlersAdaptiveRelativeStrengthIndexV1State(fraction), bars);
            await CompareRoutes(new EhlersAdaptiveStochasticIndicatorV1(fraction), Stock(bars).CalculateEhlersAdaptiveStochasticIndicatorV1(fraction),
                new EhlersAdaptiveStochasticIndicatorV1State(fraction), bars);
            await CompareRoutes(new EhlersAdaptiveCommodityChannelIndexV1(fraction, .015), Stock(bars).CalculateEhlersAdaptiveCommodityChannelIndexV1(fraction),
                new EhlersAdaptiveCommodityChannelIndexV1State(fraction), bars);
        }
        await CompareRoutes(new EhlersAdaptiveRsiFisherTransformV1(), Stock(bars).CalculateEhlersAdaptiveRsiFisherTransformV1(),
            new EhlersAdaptiveRsiFisherTransformV1State(), bars);
    }

    [Fact]
    public async Task MamaMatchesAllEightOutputsForDifferentAdaptiveGains()
    {
        var bars = Market(180);
        foreach (var fast in new[] { .2, .5, .8 })
            foreach (var slow in new[] { .01, .05 })
                await CompareRoutes(new EhlersMesaAdaptiveMovingAverage(20, fast, slow), Stock(bars).CalculateEhlersMotherOfAdaptiveMovingAverages(fast, slow),
                    new EhlersMotherOfAdaptiveMovingAveragesState(fast, slow), bars);
    }

    [Fact]
    public async Task PredictiveFiltersMatchAllOutputsAndOrderBounds()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 20 })
        {
            foreach (var prediction in new[] { 0d, 3, 200 })
                await CompareRoutes(new EhlersVossPredictiveFilter(period, prediction, .25), Stock(bars).CalculateEhlersVossPredictiveFilter(period, prediction, .25),
                    new EhlersVossPredictiveFilterState(period, prediction, .25), bars);
            foreach (var cutoff in new[] { 1, 2, 10 })
                await CompareRoutes(new EhlersTruncatedBandPassFilter(period, cutoff, .1), Stock(bars).CalculateEhlersTruncatedBandPassFilter(period, cutoff, .1),
                    new EhlersTruncatedBandPassFilterState(period, cutoff, .1), bars);
        }
    }

    [Fact]
    public async Task SwissArmyMatchesAllNineOutputsAndReset()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 20, 100 })
            foreach (var bandwidth in new[] { 0d, .1, .3 })
                await CompareRoutes(new EhlersSwissArmyKnife(period, bandwidth), Stock(bars).CalculateEhlersSwissArmyKnifeIndicator(period, bandwidth),
                    new EhlersSwissArmyKnifeIndicatorState(period, bandwidth), bars);
    }

    [Fact]
    public async Task CombSpectralEstimateKeepsIndependentPeriodHistories()
    {
        var bars = Market(100);
        foreach (var pair in new[] { (1, 1), (4, 1), (10, 3), (24, 7), (2, 5) })
            foreach (var bandwidth in new[] { 0d, .3, .8 })
            {
                var indicator = new EhlersCombFilterSpectralEstimate(pair.Item1, pair.Item2, bandwidth);
                var batch = Stock(bars).CalculateEhlersCombFilterSpectralEstimate(pair.Item1, pair.Item2, bandwidth);
                var mirrored = bars.Select(b => new Bar(b.Time, -2 * b.Open, -2 * b.Low, -2 * b.High, -2 * b.Close, b.Volume)).ToArray();
                var reflected = Stock(mirrored).CalculateEhlersCombFilterSpectralEstimate(pair.Item1, pair.Item2, bandwidth);
                for (var i = 0; i < bars.Length; i++)
                {
                    Close(batch.CustomValuesList[i], reflected.CustomValuesList[i]);
                    Assert.True(batch.CustomValuesList[i] == 0 || batch.CustomValuesList[i] >= pair.Item2 - 1e-9 && batch.CustomValuesList[i] <= pair.Item1 + 1e-9);
                }
                await CompareRoutes(indicator, batch, new EhlersCombFilterSpectralEstimateState(pair.Item1, pair.Item2, bandwidth), bars);
                Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("comb-poles", bars, [batch.CustomValuesList], 0));
            }
    }

    [Fact]
    public async Task FourierSpectralEstimateUsesTheCompleteSpectrumAcrossRoutes()
    {
        var bars = Market(100);
        foreach (var pair in new[] { (1, 1), (4, 1), (10, 3), (24, 7), (2, 5) })
        {
            var indicator = new EhlersDiscreteFourierTransformSpectralEstimate(pair.Item1, pair.Item2);
            var batch = Stock(bars).CalculateEhlersDiscreteFourierTransformSpectralEstimate(pair.Item1, pair.Item2);
            await CompareRoutes(indicator, batch, new EhlersDiscreteFourierTransformSpectralEstimateState(pair.Item1, pair.Item2), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("spectral-complete", bars, [batch.CustomValuesList], 0));
        }
    }

    [Fact]
    public async Task MesaPredictionMatchesAllRoutesAndCompanionMatrix()
    {
        var bars = Market(100);
        foreach (var history in new[] { 1, 2, 5, 9 })
            foreach (var horizon in new[] { 1, 4, 12 })
            {
                var indicator = new EhlersMesaPredictIndicatorV2(history, 30, 7, horizon);
                var batch = Stock(bars).CalculateEhlersMesaPredictIndicatorV2(length1: history, length2: 30, length3: 7, length4: horizon);
                await CompareRoutes(indicator, batch, new EhlersMesaPredictIndicatorV2State(length1: history, length2: 30, length3: 7, length4: horizon), bars);
                foreach (var rule in BuiltInFormulaReferences.For(indicator))
                    rule.Check(new IndicatorValidationContext("mesa-matrix", bars, batch.OutputValues.Values.Cast<IReadOnlyList<double>>().ToArray(), 0));
            }
    }

    [Fact]
    public async Task PeakValleyPublishesAllThreeEventsAcrossRoutes()
    {
        var bars = Market(240);
        foreach (var period in new[] { 1, 2, 5, 50 })
            foreach (var smooth in new[] { 1, 7 })
                await CompareRoutes(new PeakValleyEstimation(period, smooth), Stock(bars).CalculatePeakValleyEstimation(length: period, smoothLength: smooth),
                    new PeakValleyEstimationState(length: period, smoothLength: smooth), bars);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    [InlineData(100, false)]
    [InlineData(100, true)]
    public async Task StationaryExtrapolationMatchesAllRoutes(int period, bool weighted)
    {
        var bars = Market(240);
        var kind = weighted ? MovingAvgType.WeightedMovingAverage : MovingAvgType.SimpleMovingAverage;
        IMovingAverage average = weighted ? new Wma() : new Sma();
        await CompareRoutes(new StationaryExtrapolatedLevelsOscillator(period, average), Stock(bars).CalculateStationaryExtrapolatedLevelsOscillator(kind, period),
            new StationaryExtrapolatedLevelsOscillatorState(kind, period), bars);
    }

    [Fact]
    public async Task FreedomOfMovementMatchesAllRoutesAndItsNormalization()
    {
        var bars = Market(220);
        foreach (var period in new[] { 1, 2, 3, 10, 60 })
        {
            var indicator = new FreedomOfMovement(period);
            var batch = Stock(bars).CalculateFreedomOfMovement(length: period);
            await CompareRoutes(indicator, batch, new FreedomOfMovementState(length: period), bars);
            foreach (var rule in BuiltInFormulaReferences.For(indicator))
                rule.Check(new IndicatorValidationContext("freedom-ranges", bars, batch.OutputValues.Values.Cast<IReadOnlyList<double>>().ToArray(), 0));
        }
    }

    [Fact]
    public async Task AdaptiveBandpassMatchesIndependentImpulsePropagationAndNormalizedBounds()
    {
        var bars = Market(100);
        foreach (var periods in new[] { (1, 1, 1), (10, 3, 2), (48, 10, 3), (2, 5, 3) })
            foreach (var width in new[] { 0d, .3, .8 })
            {
                var indicator = new EhlersAdaptiveBandPassFilter(periods.Item1, periods.Item2, periods.Item3, width);
                var batch = Stock(bars).CalculateEhlersAdaptiveBandPassFilter(periods.Item1, periods.Item2, periods.Item3, width);
                await CompareRoutes(indicator, batch, new EhlersAdaptiveBandPassFilterState(periods.Item1, periods.Item2, periods.Item3, width), bars);
                var rules = BuiltInFormulaReferences.For(indicator).ToArray();
                Assert.Equal(2, rules.Length);
                foreach (var rule in rules) rule.Check(new IndicatorValidationContext("adaptive-band-impulses", bars, batch.OutputValues.Values.Cast<IReadOnlyList<double>>().ToArray(), 0));
                Assert.All(batch.CustomValuesList, v => Assert.InRange(v, -1, 1));
            }
    }

    [Fact]
    public async Task CycleTunedRsiMatchesExpandedAdaptiveWeights()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 5, 20 })
        {
            var indicator = new DominantCycleTunedRelativeStrengthIndex(period);
            var batch = Stock(bars).CalculateDominantCycleTunedRelativeStrengthIndex(period);
            await CompareRoutes(indicator, batch, new DominantCycleTunedRelativeStrengthIndexState(period), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("cycle-rsi-weights", bars, [batch.CustomValuesList], 0));
            Assert.All(batch.CustomValuesList, value => Assert.InRange(value, 0, 100));
        }
    }

    [Fact]
    public async Task ZeroCrossingCycleMatchesIndependentIntervalsAndAmplitudeScaling()
    {
        var bars = Market(140);
        var reflected = bars.Select(b => new Bar(b.Time, -2 * b.Open, -2 * b.Low, -2 * b.High, -2 * b.Close, b.Volume)).ToArray();
        foreach (var period in new[] { 1, 2, 5, 20 })
            foreach (var bandwidth in new[] { 0d, .3, .7 })
            {
                var indicator = new EhlersZeroCrossingsDominantCycle(period, bandwidth);
                var batch = Stock(bars).CalculateEhlersZeroCrossingsDominantCycle(period, bandwidth);
                await CompareRoutes(indicator, batch, new EhlersZeroCrossingsDominantCycleState(period, bandwidth), bars);
                Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("cycle-crossings", bars, [batch.CustomValuesList], 0));
                Assert.Equal(batch.CustomValuesList, Stock(reflected).CalculateEhlersZeroCrossingsDominantCycle(period, bandwidth).CustomValuesList);
            }
    }

    [Fact]
    public void ConfluenceVotesRequireResolvedChangesOnBothAxes()
    {
        Assert.Equal(0, ConfluenceVotes.Score(1, 1 + 1e-13, 0));
        Assert.Equal(0, ConfluenceVotes.Score(1, 0, 1 - 1e-13));
        Assert.Equal(3, ConfluenceVotes.Score(1, 1 - 1e-8, 1 - 1e-8));
        Assert.Equal(-3, ConfluenceVotes.Score(-1, -1 + 1e-8, -1 + 1e-8));
        Assert.Equal(.3, ConfluenceVotes.Publish(3, -1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(20)]
    public async Task PercentageTrendMatchesIndependentSuffixExtrema(int period)
    {
        foreach (var percentage in new[] { 0d, .15, .5 })
        {
            var bars = Market(180);
            var indicator = new PercentageTrend(period, percentage);
            var batch = Stock(bars).CalculatePercentageTrend(period, percentage);
            await CompareRoutes(indicator, batch, new PercentageTrendState(period, percentage), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("percentage-suffix", bars, [batch.CustomValuesList], 0));
        }
    }

    [Fact]
    public void PercentageTrendHasKnownOneStepLookback()
    {
        var bars = new[] { 2d, 4, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var actual = Stock(bars).CalculatePercentageTrend(1, .25).CustomValuesList;
        Assert.Equal(new[] { 0d, 2.5, 5 }, actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(14)]
    public async Task RecursiveRsiMatchesIndependentDelayedDirectionCounts(int period)
    {
        var bars = Market(240);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            var indicator = new RecursiveRelativeStrengthIndex(period, average);
            var batch = Stock(bars).CalculateRecursiveRelativeStrengthIndex(kind, period);
            await CompareRoutes(indicator, batch, new RecursiveRelativeStrengthIndexState(kind, period), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("recursive-rsi-directions", bars, [batch.CustomValuesList], 0));
            Assert.All(batch.CustomValuesList, value => Assert.InRange(value, 0, 100));
        }
    }

    [Fact]
    public void RecursiveRsiPublishesThePreviousWindowOfDirectionVotes()
    {
        var bars = new[] { 2d, 4, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = new[] { 100d, 100, 100, 0 };
        Assert.Equal(expected, Stock(bars).CalculateRecursiveRelativeStrengthIndex(length: 1).CustomValuesList);
        Assert.Single(BuiltInFormulaReferences.For(new RecursiveRelativeStrengthIndex(1))).Check(new IndicatorValidationContext("recursive-rsi-hand", bars, [expected], 0));
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(2, 3, 1, 2)]
    [InlineData(7, 10, 2, 3)]
    [InlineData(18, 30, 2, 3)]
    public async Task VervoortSmoothedUsesPercentageBandPosition(int band, int range, int cascade, int smooth)
    {
        var bars = Market(240);
        foreach (var multiplier in new[] { 0d, 1, 2 })
        {
            var indicator = new VervoortSmoothedOscillator(14, band, range, cascade, smooth, multiplier);
            var batch = Stock(bars).CalculateVervoortSmoothedOscillator(band, range, cascade, smooth, multiplier);
            await CompareRoutes(indicator, batch, new VervoortSmoothedOscillatorState(band, range, cascade, smooth, multiplier), bars);
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(2, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("vervoort-percent", bars, [batch.OutputValues["Vso"], batch.OutputValues["Sk"]], 0));
        }
    }

    [Fact]
    public void VervoortSmoothedHasHandCalculatedBandPercentAndStochastic()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v+1, v-1, v, 1)).ToArray();
        var batch = Stock(bars).CalculateVervoortSmoothedOscillator(2, 2, 1, 1, 2);
        Assert.Equal(0, batch.OutputValues["Vso"][0]);
        Close(200d/3, batch.OutputValues["Vso"][1]);
        Close(100d/3, batch.OutputValues["Vso"][2]);
        Assert.Equal(100, batch.OutputValues["Sk"][0]);
        Assert.Equal(100, batch.OutputValues["Sk"][1]);
        Close(100d/3, batch.OutputValues["Sk"][2]);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 3, 2)]
    [InlineData(7, 3, 5)]
    [InlineData(80, 40, 48)]
    public async Task EhlersConvolutionMatchesIndependentFilterAndLagCorrelation(int high, int low, int window)
    {
        var bars = Market(180);
        var indicator = new EhlersConvolutionIndicator(high, low, window);
        var batch = Stock(bars).CalculateEhlersConvolutionIndicator(high, low, window);
        await CompareRoutes(indicator, batch, new EhlersConvolutionIndicatorState(high, low, window), bars);
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("convolution-lag", bars, [batch.OutputValues["Eci"], batch.OutputValues["Slope"]], 0));
        Assert.All(batch.OutputValues["Eci"], value => Assert.InRange(value, .5/(1+Math.Exp(3))-1e-12, .5/(1+Math.Exp(-3))+1e-12));
    }

    [Fact]
    public void EhlersConvolutionHasNeutralZeroHistoryAndNoOnePointCorrelation()
    {
        var bars = new[] { 0d, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var batch = Stock(bars).CalculateEhlersConvolutionIndicator(length3: 1);
        Assert.Equal(new[] { .25, .25, .25 }, batch.OutputValues["Eci"]);
        Assert.Equal(new[] { 1d, 1, -1 }, batch.OutputValues["Slope"]);
    }

    [Theory]
    [InlineData(1, 2, 2, 1, 1)]
    [InlineData(2, 5, 3, 2, 3)]
    [InlineData(23, 50, 10, 3, 3)]
    public async Task SchaffShkMatchesTwoIndependentStochasticPasses(int fast, int slow, int cycle, int first, int second)
    {
        var bars = Market(240);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            var indicator = new SchaffTrendCycleShk(fast, slow, cycle, first, second, average);
            var batch = Stock(bars).CalculateSchaffTrendCycleShk(kind, fast, slow, cycle, first, second);
            await CompareRoutes(indicator, batch, new SchaffTrendCycleShkState(kind, fast, slow, cycle, first, second), bars);
            var rules = BuiltInFormulaReferences.For(indicator).ToArray();
            Assert.Equal(2, rules.Length);
            foreach (var rule in rules)
                rule.Check(new IndicatorValidationContext("schaff-double", bars, [batch.OutputValues["Stc"], batch.OutputValues["Macd"]], 0));
            Assert.All(batch.OutputValues["Stc"], value => Assert.InRange(value, 0, 100));
        }
    }

    [Fact]
    public void SchaffShkHasKnownUnsmoothenedTurnsAndRetainsUnresolvedRanges()
    {
        Assert.Equal(37, SchaffRange.Normalize(1+1e-14, 1, 1+1e-14, 2, 37));
        Assert.Equal(100, SchaffRange.Normalize(1+1e-8, 1, 1+1e-8, 2, 37));
        var bars = new[] { 1d, 2, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        Assert.Equal(new[] { 0d, 100, 0 }, Stock(bars).CalculateSchaffTrendCycleShk(fastLength: 1, slowLength: 2, cycleLength: 2, d1Length: 1, d2Length: 1).CustomValuesList);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task ZigZagInterpolatesItsFinalExtremaAndHoldsTheLastEndpoint(double deviation)
    {
        foreach (var bars in new[] { Market(180), Market(180).Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, b.Volume)).ToArray() })
        {
            var indicator = new ZigZag(14, deviation);
            var batch = Stock(bars).CalculateZigZag(deviation);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            var actual = run[indicator.Outputs[0]].ToArray();
            for (var i = 0; i < bars.Length; i++) Close(batch.CustomValuesList[i], actual[i]);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("zigzag-knots", bars, [actual], 0));
        }
    }

    [Fact]
    public void ZigZagRedrawsExtendedLegsInsteadOfLeavingSeedValues()
    {
        var prices = new[] { 10d, 12, 14, 13, 10, 8, 9, 11 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = new[] { 10d, 12, 14, 12, 10, 8, 9.5, 11 };
        Assert.Equal(expected, Stock(bars).CalculateZigZag(10).CustomValuesList);
        var indicator = new ZigZag(14, 10);
        Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("zigzag-hand", bars, [expected], 0));
        var increasing = bars.Take(3).ToArray();
        Assert.Equal(new[] { 10d, 12, 14 }, Stock(increasing).CalculateZigZag(10).CustomValuesList);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(20)]
    public async Task TechnicalRankWeightsPercentageReturnsOnce(int period)
    {
        var bars = Market(180);
        var indicator = new TechnicalRank(period, period, period, period, period, period+1, period, period, period);
        var batch = Stock(bars).CalculateTechnicalRank(period, period, period, period, period, period+1, period, period, period);
        await CompareRoutes(indicator, batch, new TechnicalRankState(period, period, period, period, period, period+1, period, period, period), bars);
        Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("technical-rank", bars, [batch.CustomValuesList], 0));
        Assert.All(batch.CustomValuesList, value => Assert.InRange(value, 0, 100));
    }

    [Fact]
    public void TechnicalRankHasHandCalculatedPercentageContributions()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var actual = Stock(bars).CalculateTechnicalRank(1, 1, 1, 1, 1, 1, 1, 1, 1).CustomValuesList;
        Assert.Equal(new[] { 5d, 50, 0 }, actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(100)]
    public async Task R2RegressionMatchesIndependentFeedbackWeights(int period)
    {
        var bars = Market(240);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            var indicator = new R2AdaptiveRegression(period, average);
            var batch = Stock(bars).CalculateR2AdaptiveRegression(kind, period);
            await CompareRoutes(indicator, batch, new R2AdaptiveRegressionState(kind, period), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("r2-feedback", bars, [batch.CustomValuesList], 0));
        }
    }

    [Fact]
    public void R2RegressionHasHandCalculatedTwoPeriodFeedback()
    {
        var bars = new[] { 2d, 4, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var actual = Stock(bars).CalculateR2AdaptiveRegression(length: 2).CustomValuesList;
        Assert.Equal(2, actual[0]);
        Assert.Equal(5, actual[1]);
        Assert.Equal(9+2*Math.Sqrt(2), actual[2], 10);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(50)]
    public async Task QuadraticLeastSquaresFitsAndForecastsIndependentPolynomial(int period)
    {
        var bars = Market(400);
        var indicator = new QuadraticLeastSquaresMovingAverage(period);
        var batch = Stock(bars).CalculateQuadraticLeastSquaresMovingAverage(length: period);
        await CompareRoutes(indicator, batch, new QuadraticLeastSquaresMovingAverageState(length: period), bars);
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        Assert.Equal(2, rules.Length);
        foreach (var rule in rules)
            rule.Check(new IndicatorValidationContext("quadratic-fit", bars, [batch.OutputValues["Qlma"], batch.OutputValues["Forecast"]], 0));
    }

    [Fact]
    public void QuadraticLeastSquaresReproducesKnownParabolaAndDefinesRankDeficiency()
    {
        var bars = new[] { 2d, 9, 24 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var batch = Stock(bars).CalculateQuadraticLeastSquaresMovingAverage(length: 3);
        Assert.Equal(0, batch.OutputValues["Qlma"][0]);
        Assert.Equal(0, batch.OutputValues["Qlma"][1]);
        Assert.Equal(24, batch.OutputValues["Qlma"][2], 10);
        Assert.Equal(0, batch.OutputValues["Forecast"][0]);
        Assert.Equal(0, batch.OutputValues["Forecast"][1]);
        Assert.Equal(1074, batch.OutputValues["Forecast"][2], 10);
        var singular = Stock(bars).CalculateQuadraticLeastSquaresMovingAverage(length: 2);
        Assert.Equal(new[] { 0d, 5.5, 16.5 }, singular.OutputValues["Qlma"]);
        Assert.Equal(singular.OutputValues["Qlma"], singular.OutputValues["Forecast"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(14)]
    [InlineData(31)]
    public async Task AnticipateUsesPeriodicPhaseAndConfiguredBandwidth(int period)
    {
        var bars = Market(120);
        foreach (var width in new[] { 0d, .3, 1, 2 })
        {
            var indicator = new EhlersAnticipateIndicator(period, width);
            var batch = Stock(bars).CalculateEhlersAnticipateIndicator(length: period, bw: width);
            await CompareRoutes(indicator, batch, new EhlersAnticipateIndicatorState(length: period, bw: width), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("anticipate-phase", bars, [batch.CustomValuesList], 0));
            Assert.All(batch.CustomValuesList, value => Assert.InRange(value, -1, 1));
        }
    }

    [Fact]
    public void AnticipateIdentifiesThePositiveQuarterCycleOfAnImpulse()
    {
        var matcher = new EhlersAnticipatePhase(4);
        Assert.Equal(0, matcher.Predict(new[] { 1d, 1+1e-13, 1, 1 }));
        Assert.Equal(1, matcher.Predict(new[] { 1+1e-8, 1d, 1, 1 }));
        Assert.Equal(1, matcher.Predict(new[] { 1e-20, 0d, 0, 0 }));
        var bars = new[] { 0d, 0, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        Assert.Equal(new[] { 0d, 0, 0, 1 }, Stock(bars).CalculateEhlersAnticipateIndicator(length: 4).CustomValuesList);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(50)]
    public async Task GannAndRobustWeightingMatchIndependentEnvelopeAndRegression(int period)
    {
        var bars = Market(180);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            var gann = new ModifiedGannHiloActivator(3, period, average);
            var gannBatch = Stock(bars).CalculateModifiedGannHiloActivator(kind, period);
            await CompareRoutes(gann, gannBatch, new ModifiedGannHiloActivatorState(kind, period), bars);
            Assert.Single(BuiltInFormulaReferences.For(gann)).Check(new IndicatorValidationContext("gann-envelope", bars, [gannBatch.CustomValuesList], 0));
            var robust = new RobustWeightingOscillator(period, average);
            var robustBatch = Stock(bars).CalculateRobustWeightingOscillator(kind, period);
            await CompareRoutes(robust, robustBatch, new RobustWeightingOscillatorState(kind, period), bars);
            Assert.Single(BuiltInFormulaReferences.For(robust)).Check(new IndicatorValidationContext("robust-regression", bars, [robustBatch.CustomValuesList], 0));
        }
    }

    [Fact]
    public void GannEnvelopeAndRobustTransformHaveHandCalculatedExamples()
    {
        var bars = new[] { 2d, 4, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Equal(new[] { 3d, 5, 9 }, Stock(bars).CalculateModifiedGannHiloActivator(length: 1).CustomValuesList);
        Assert.Equal(new[] { 0d, -2, -1 }, Stock(bars).CalculateRobustWeightingOscillator(length: 2).CustomValuesList);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task ConfluenceMatchesIndependentProjectionAndVoting(int period)
    {
        var bars = Market(180);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            var indicator = new ConfluenceIndicator(period, average);
            var batch = Stock(bars).CalculateConfluenceIndicator(kind, period);
            await CompareRoutes(indicator, batch, new ConfluenceIndicatorState(kind, period), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("confluence-projections", bars, [batch.CustomValuesList], 0));
        }
    }

    [Fact]
    public async Task VervoortCandlestickOscillatorsMatchIndependentTrendConfirmation()
    {
        var bars = Market(240);
        foreach (var period in new[] { 1, 2, 7, 34, 55 })
        {
            var shortIndicator = new VervoortHeikenAshiCandlestickOscillator(period);
            var longIndicator = new VervoortHeikenAshiLongTermCandlestickOscillator(period);
            var shortBatch = Stock(bars).CalculateVervoortHeikenAshiCandlestickOscillator(length: period);
            var longBatch = Stock(bars).CalculateVervoortHeikenAshiLongTermCandlestickOscillator(length: period);
            await CompareRoutes(shortIndicator, shortBatch, new VervoortHeikenAshiCandlestickOscillatorState(length: period), bars);
            await CompareRoutes(longIndicator, longBatch, new VervoortHeikenAshiLongTermCandlestickOscillatorState(length: period), bars);
            Assert.Single(BuiltInFormulaReferences.For(shortIndicator)).Check(new IndicatorValidationContext("vervoort-short-trends", bars, [shortBatch.CustomValuesList], 0));
            Assert.Single(BuiltInFormulaReferences.For(longIndicator)).Check(new IndicatorValidationContext("vervoort-long-trends", bars, [longBatch.CustomValuesList], 0));
            Assert.All(shortBatch.CustomValuesList.Concat(longBatch.CustomValuesList), value => Assert.Contains(value, new[] { -1d, 0, 1 }));
        }
    }

    [Fact]
    public void InsyncVotesDistinguishThresholdCrossingsFromNumericalTies()
    {
        Assert.Equal(0, InsyncVotes.Band(100 + 1e-13, -100, 100));
        Assert.Equal(5, InsyncVotes.Band(100 + 1e-8, -100, 100));
        Assert.Equal(-5, InsyncVotes.Band(-100 - 1e-8, -100, 100));
        Assert.Equal(5, InsyncVotes.Direction(1 - 1e-13, 1));
        Assert.Equal(0, InsyncVotes.Direction(1 - 1e-8, 1));
        Assert.Equal(-5, InsyncVotes.InverseDirection(-1 + 1e-13, -1));
        Assert.Equal(0, InsyncVotes.InverseDirection(-1 + 1e-8, -1));
    }

    [Fact]
    public async Task InsyncIndexMatchesIndependentVotesAndDelayedScores()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 5, 14 })
        {
            var indicator = new InsyncIndex(fastLength: period, slowLength: period + 1, mfiLength: period, bbLength: period,
                cciLength: period, dpoLength: period, rocLength: period, rsiLength: period, stochLength: period,
                stochKLength: 1, stochDLength: 3, smaLength: period);
            var batch = Stock(bars).CalculateInsyncIndex(fastLength: period, slowLength: period + 1, mfiLength: period, bbLength: period,
                cciLength: period, dpoLength: period, rocLength: period, rsiLength: period, stochLength: period,
                stochKLength: 1, stochDLength: 3, smaLength: period);
            await CompareRoutes(indicator, batch, new InsyncIndexState(fastLength: period, slowLength: period + 1, mfiLength: period, bbLength: period,
                cciLength: period, dpoLength: period, rocLength: period, rsiLength: period, stochLength: period,
                stochKLength: 1, stochDLength: 3, smaLength: period), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("insync-votes", bars, [batch.CustomValuesList], 0));
            Assert.All(batch.CustomValuesList, value => Assert.Equal(0, value % 5));
        }
    }

    [Fact]
    public void SarCatalogMapsMaximumToTheAccelerationCap()
    {
        var bars = Market(90);
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(Stock(bars)));
        SeriesHandle? handle = null;
        builder.ConfigureIndicators(catalog => handle = catalog.Sar(.03, .09));
        using var runtime = builder.Build();
        runtime.Start(); runtime.Subscribe(handle!.Value);
        var actual = runtime.GetSeries(handle.Value).ToArray();
        var expected = Stock(bars).CalculateParabolicSAR(.03, .03, .09).CustomValuesList;
        Assert.Equal(expected.Count, actual.Length);
        for (var i = 0; i < actual.Length; i++) Close(expected[i], actual[i]);
    }

    [Fact]
    public async Task ParabolicSarRetainsExtremesAndAccelerationAcrossRoutes()
    {
        var bars = Market(180);
        foreach (var parameters in new[] { (.02, .02, .2), (.1, .1, .3), (0d, 0d, 0d), (.02, 0d, .02), (.01, .03, .4) })
        {
            var indicator = new ParabolicSar(start: parameters.Item1, increment: parameters.Item2, maximum: parameters.Item3);
            var batch = Stock(bars).CalculateParabolicSAR(parameters.Item1, parameters.Item2, parameters.Item3);
            await CompareRoutes(indicator, batch, new ParabolicSARState(parameters.Item1, parameters.Item2, parameters.Item3), bars);
            Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("sar-records", bars, [batch.CustomValuesList], 0));
        }
        var rising = Enumerable.Range(0, 20).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), i + 2, i + 3, i + 1, i + 2, 1)).ToArray();
        var fixedGain = Stock(rising).CalculateParabolicSAR(.02, 0, .2).CustomValuesList;
        var accelerated = Stock(rising).CalculateParabolicSAR(.02, .02, .2).CustomValuesList;
        Assert.True(accelerated.Last() > fixedGain.Last());
        for (var i = 2; i < rising.Length; i++)
            Assert.True(accelerated[i] <= Math.Min(rising[i - 1].Low, rising[i - 2].Low));
    }

    [Fact]
    public async Task HalfTrendConfirmsReversalsAndRetainsExtremaAcrossRoutes()
    {
        var bars = Market(240);
        foreach (var period in new[] { 1, 2, 7, 30 })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
            {
                IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
                var indicator = new HalfTrend(period, average);
                var batch = Stock(bars).CalculateHalfTrend(kind, period);
                await CompareRoutes(indicator, batch, new HalfTrendState(kind, period), bars);
                Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("half-trend-segments", bars, [batch.CustomValuesList], 0));
            }
        var prices = new[] { 2d, 4, 6, 4, 2, 4, 6 };
        var reversals = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + .5, v - .5, v, 1)).ToArray();
        var actual = Stock(reversals).CalculateHalfTrend(length: 1).CustomValuesList;
        Assert.Equal(new[] { 1.5, 3.5, 5.5, 5.5, 2.5, 2.5, 5.5 }, actual);
    }

    [Fact]
    public async Task TrenderMatchesIndependentDirectionalEventsForEveryOutput()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 7, 30 })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
                foreach (var multiplier in new[] { 0d, 2, 4 })
                {
                    IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
                    var indicator = new Trender(period, multiplier, average);
                    var batch = Stock(bars).CalculateTrender(kind, period, multiplier);
                    await CompareRoutes(indicator, batch, new TrenderState(kind, period, multiplier), bars);
                    var rules = BuiltInFormulaReferences.For(indicator).ToArray();
                    Assert.Equal(3, rules.Length);
                    foreach (var rule in rules)
                        rule.Check(new IndicatorValidationContext("trender-events", bars, batch.OutputValues.Values.Cast<IReadOnlyList<double>>().ToArray(), 0));
                }
    }

    [Fact]
    public async Task PopulationDeviationIsIndependentOfSignalAverageAndPriceOrigin()
    {
        var bars = Market(100);
        foreach (var period in new[] { 1, 2, 5, 20 })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
            {
                var indicator = new StandardDevation(period, kind);
                var batch = Stock(bars).CalculateStandardDevation(kind, period);
                var baseline = Stock(bars).CalculateStandardDevation(length: period);
                Assert.Equal(baseline.CustomValuesList, batch.CustomValuesList);
                await CompareRoutes(indicator, batch, new StandardDeviationState(kind, period), bars);
                foreach (var rule in BuiltInFormulaReferences.For(indicator))
                    rule.Check(new IndicatorValidationContext("population-signal", bars, batch.OutputValues.Values.Cast<IReadOnlyList<double>>().ToArray(), 0));
                var shifted = bars.Select(b => new Bar(b.Time, b.Open + 10000, b.High + 10000, b.Low + 10000, b.Close + 10000, b.Volume)).ToArray();
                var translated = Stock(shifted).CalculateStandardDevation(kind, period);
                for (var i = 0; i < bars.Length; i++) Close(batch.CustomValuesList[i], translated.CustomValuesList[i]);
            }
    }

    [Theory]
    [InlineData(InputLength.Minute)]
    [InlineData(InputLength.Hour)]
    [InlineData(InputLength.Day)]
    [InlineData(InputLength.Week)]
    [InlineData(InputLength.Month)]
    [InlineData(InputLength.Year)]
    public async Task PivotAveragesUseCalendarPeriodsAcrossAllRoutes(InputLength inputLength)
    {
        var start = new DateTime(2023, 12, 29, 23, 59, 0);
        var bars = Market(100).Select((b, i) => new Bar(start.AddDays(i / 4).AddMinutes(i % 4 * 60), b.Open, b.High, b.Low, b.Close, b.Volume)).ToArray();
        foreach (var period in new[] { 1, 3, 7 })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage })
            {
                IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : new Wma();
                var indicator = new PivotPointAverage(period, inputLength, average);
                var batch = Stock(bars).CalculatePivotPointAverage(kind, period, inputLength);
                await CompareRoutes(indicator, batch, new PivotPointAverageState(kind, period, inputLength), bars);
                var rules = BuiltInFormulaReferences.For(indicator).ToArray();
                Assert.Equal(6, rules.Length);
                foreach (var rule in rules)
                    rule.Check(new IndicatorValidationContext("calendar-pivots", bars, batch.OutputValues.Values.Cast<IReadOnlyList<double>>().ToArray(), 0));
            }
    }

    [Fact]
    public async Task SqueezeMomentumMatchesIndependentResidualRegressionAcrossRoutes()
    {
        var bars = Market(180).Select((b, i) => new Bar(b.Time, b.Open, b.Close + 1 + i % 5, b.Close - .5 - i % 3, b.Close, b.Volume)).ToArray();
        foreach (var period in new[] { 1, 2, 3, 20, 50 })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
            {
                IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
                var indicator = new SqueezeMomentumIndicator(period, average);
                var batch = Stock(bars).CalculateSqueezeMomentumIndicator(kind, period);
                await CompareRoutes(indicator, batch, new SqueezeMomentumIndicatorState(kind, period), bars);
                Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("squeeze-residual-fit", bars, [batch.CustomValuesList], 0));
            }
    }

    [Fact]
    public async Task RsingMatchesAllRoutesWithVaryingRangesAndVolume()
    {
        var bars = Market(180).Select((b, i) => new Bar(b.Time, b.Open, b.Close + 1 + i % 5, b.Close - .5 - i % 3, b.Close, b.Volume)).ToArray();
        foreach (var period in new[] { 1, 2, 5, 20 })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
            {
                IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
                await CompareRoutes(new RSINGIndicator(period, average), Stock(bars).CalculateRSINGIndicator(kind, period), new RSINGIndicatorState(kind, period), bars);
            }
    }

    [Fact]
    public async Task QuadraticFitsMatchAllRoutesAndIndependentProjection()
    {
        var bars = Market(360);
        foreach (var period in new[] { 1, 2, 3, 7, 50 })
            await CompareRoutes(new LinearQuadraticConvergenceDivergenceOscillator(period), Stock(bars).CalculateLinearQuadraticConvergenceDivergenceOscillator(length: period),
                new LinearQuadraticConvergenceDivergenceOscillatorState(length: period), bars);
        foreach (var period in new[] { 1, 2, 3, 7, 50 })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
            {
                IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
                var indicator = new QuadraticRegression(period, average);
                var batch = Stock(bars).CalculateQuadraticRegression(kind, period);
                await CompareRoutes(indicator, batch, new QuadraticRegressionState(kind, period), bars);
                Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("quadratic-projection", bars, [batch.CustomValuesList], 0));
            }
    }

    [Fact]
    public async Task PivotAndSwingEventsMatchAllRoutes()
    {
        var bars = Market(230);
        foreach (var pointValue in new[] { 0d, 1, 100, -2 })
            await CompareRoutes(new HerrickPayoffIndex(pointValue), Stock(bars).CalculateHerrickPayoffIndex(pointValue), new HerrickPayoffIndexState(pointValue), bars);
        await CompareRoutes(new TTMScalperIndicator(), Stock(bars).CalculateTTMScalperIndicator(), new TTMScalperIndicatorState(), bars);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma() : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            await CompareRoutes(new PivotDetectorOscillator(200, average), Stock(bars).CalculatePivotDetectorOscillator(kind), new PivotDetectorOscillatorState(kind), bars);
            foreach (var period in new[] { 1, 2, 5, 50 })
                await CompareRoutes(new TopsAndBottomsFinder(period, average), Stock(bars).CalculateTopsAndBottomsFinder(kind, period), new TopsAndBottomsFinderState(kind, period), bars);
        }
    }

    [Fact]
    public async Task HybridAndOneLcMatchAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 5, 14, 50 })
        {
            await CompareRoutes(new HybridConvolutionFilter(period), Stock(bars).CalculateHybridConvolutionFilter(period), new HybridConvolutionFilterState(period), bars);
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage })
                await CompareRoutes(new OneLCLeastSquaresMovingAverage(period, kind), Stock(bars).Calculate1LCLeastSquaresMovingAverage(kind, period),
                    new _1LCLeastSquaresMovingAverageState(kind, period), bars);
        }
    }

    [Fact]
    public async Task JurikFiltersMatchIndependentContractsAcrossAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 5, 14, 50 })
        {
            await CompareRoutes(new JmaRsxClone(period), Stock(bars).CalculateJmaRsxClone(period), new JmaRsxCloneState(period), bars);
            await CompareRoutes(new Jma(period), Stock(bars).CalculateJurikMovingAverage(length: period), new JurikMovingAverageState(length: period), bars);
        }
    }

    [Fact]
    public async Task GrandForecastAndVanillaPatternsMatchAllRoutes()
    {
        var bars = Market(180);
        await CompareRoutes(new VanillaABCDPattern(), Stock(bars).CalculateVanillaABCDPattern(), new VanillaABCDPatternState(), bars);
        foreach (var period in new[] { 1, 2, 10, 100 })
            await CompareRoutes(new GrandTrendForecasting(period, 7, 2), Stock(bars).CalculateGrandTrendForecasting(period, 7, 2),
                new GrandTrendForecastingState(period, 7, 2), bars);
    }

    [Fact]
    public async Task SteppedRegressionAndAdaptiveMomentumMatchAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 5, 30 })
        {
            await CompareRoutes(new TStepLeastSquaresMovingAverage(period), Stock(bars).CalculateTStepLeastSquaresMovingAverage(length: period),
                new TStepLeastSquaresMovingAverageState(length: period), bars);
            await CompareRoutes(new EhlersSmoothedAdaptiveMomentum(period, 8, MovingAvgType.ExponentialMovingAverage), Stock(bars).CalculateEhlersSmoothedAdaptiveMomentum(length1: period),
                new EhlersSmoothedAdaptiveMomentumIndicatorState(length1: period), bars);
        }
    }

    [Fact]
    public async Task AdaptiveMedianUsesActualWindowAndSignedPriceSymmetry()
    {
        var positive = Market(100);
        var negative = positive.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, b.Volume)).ToArray();
        foreach (var period in new[] { 1, 2, 3, 6, 39 })
            foreach (var threshold in new[] { .002, .15, .25 })
            {
                var plus = Stock(positive).CalculateEhlersMedianAverageAdaptiveFilter(period, threshold);
                var minus = Stock(negative).CalculateEhlersMedianAverageAdaptiveFilter(period, threshold);
                for (var i = 0; i < positive.Length; i++) Close(plus.OutputValues["Maaf"][i], -minus.OutputValues["Maaf"][i]);
                await CompareRoutes(new EhlersMedianAverageAdaptiveFilter(period, threshold), plus,
                    new EhlersMedianAverageAdaptiveFilterState(period, threshold), positive);
                await CompareRoutes(new EhlersMedianAverageAdaptiveFilter(period, threshold), minus,
                    new EhlersMedianAverageAdaptiveFilterState(period, threshold), negative);
            }
    }

    [Fact]
    public void RocketChangeWindowForgetsExpiredImpulseAndPreviewsWithoutMutation()
    {
        foreach (var period in new[] { 1, 3, 10 })
        {
            var sum = new RocketChangeSum(period);
            var values = new Queue<double>();
            for (var i = 0; i < 600; i++)
            {
                var value = i == 0 ? 1e100 : Math.Pow(.5, i);
                values.Enqueue(value);
                if (values.Count > period) values.Dequeue();
                var expected = values.Sum();
                Assert.InRange(Math.Abs(sum.Next(value, false) / expected - 1), 0, 1e-14);
                Assert.InRange(Math.Abs(sum.Next(value, true) / expected - 1), 0, 1e-14);
            }
            sum.Reset();
            Assert.Equal(2, sum.Next(2, false));
            Assert.Equal(3, sum.Next(3, true));
        }
    }

    [Fact]
    public async Task ElderSafeZoneMatchesAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 22, 63 })
            foreach (var factor in new[] { 0d, 1, 2.5 })
                await CompareRoutes(new ElderSafeZoneStops(period, factor), Stock(bars).CalculateElderSafeZoneStops(length2: period, factor: factor),
                    new ElderSafeZoneStopsState(length2: period, factor: factor), bars);
    }

    [Fact]
    public async Task LiquidAndFisherLeastSquaresMatchAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 14, 100 })
        {
            await CompareRoutes(new LiquidRelativeStrengthIndex(period), Stock(bars).CalculateLiquidRelativeStrengthIndex(period),
                new LiquidRelativeStrengthIndexState(period), bars);
            await CompareRoutes(new FisherLeastSquaresMovingAverage(period), Stock(bars).CalculateFisherLeastSquaresMovingAverage(length: period),
                new FisherLeastSquaresMovingAverageState(length: period), bars);
        }
    }

    [Fact]
    public async Task RocketAndZeroMeanRoofingMatchAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 10, 48 })
        {
            await CompareRoutes(new EhlersZeroMeanRoofingFilter(period, 10), Stock(bars).CalculateEhlersZeroMeanRoofingFilter(period, 10),
                new EhlersZeroMeanRoofingFilterState(period, 10), bars);
            await CompareRoutes(new EhlersRocketRelativeStrengthIndex(period, 8, 2, 1), Stock(bars).CalculateEhlersRocketRelativeStrengthIndex(length1: period),
                new EhlersRocketRelativeStrengthIndexState(length1: period), bars);
        }
    }

    [Fact]
    public async Task SuperTrendAndSniperMatchAllRoutes()
    {
        var bars = Market(160);
        foreach (var period in new[] { 1, 2, 9, 30 })
        {
            await CompareRoutes(new SuperTrend(period), Stock(bars).CalculateSuperTrend(length: period), new SuperTrendState(length: period), bars);
            foreach (var factor in new[] { 0d, .618, 1 })
                await CompareRoutes(new FXSniperIndicator(period, 5, factor), Stock(bars).CalculateFXSniperIndicator(cciLength: period, b: factor),
                    new FXSniperIndicatorState(cciLength: period, b: factor), bars);
        }
    }

    [Fact]
    public async Task PaintPseudoAndVervoortBandsMatchAllRoutes()
    {
        var bars = Market(160);
        foreach (var period in new[] { 1, 2, 9, 30 })
        {
            await CompareRoutes(new LBRPaintBars(period, period, 2.5), Stock(bars).CalculateLBRPaintBars(length: period, lbLength: period),
                new LBRPaintBarsState(length: period, lbLength: period), bars);
            await CompareRoutes(new PseudoPolynomialChannel(period, .9), Stock(bars).CalculatePseudoPolynomialChannel(length: period),
                new PseudoPolynomialChannelState(length: period), bars);
            await CompareRoutes(new VervoortVolatilityBands(period, period + 1, 3.55, .9), Stock(bars).CalculateVervoortVolatilityBands(length1: period, length2: period + 1),
                new VervoortVolatilityBandsState(length1: period, length2: period + 1), bars);
        }
    }

    [Fact]
    public async Task AdaptiveChangeSwamiAndHilbertMatchAllRoutes()
    {
        var bars = Market(160);
        foreach (var period in new[] { 1, 2, 7, 30 })
        {
            await CompareRoutes(new MovingAverageAdaptiveFilter(period), Stock(bars).CalculateMovingAverageAdaptiveFilter(period),
                new MovingAverageAdaptiveFilterState(period), bars);
            await CompareRoutes(new SwamiStochastics(period, period + 5), Stock(bars).CalculateSwamiStochastics(period, period + 5),
                new SwamiStochasticsState(period, period + 5), bars);
            await CompareRoutes(new SwamiStochastics(period, period), Stock(bars).CalculateSwamiStochastics(period, period),
                new SwamiStochasticsState(period, period), bars);
            await CompareRoutes(new EhlersHilbertOscillator(period), Stock(bars).CalculateEhlersHilbertOscillator(period),
                new EhlersHilbertOscillatorState(period), bars);
        }
    }

    [Fact]
    public async Task RecursiveAndFisherStochasticContractsMatchAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 6, 30 })
        {
            foreach (var alpha in new[] { 0d, .1, .5, 1 })
                await CompareRoutes(new RecursiveStochastic(period, alpha), Stock(bars).CalculateRecursiveStochastic(period, alpha),
                    new RecursiveStochasticState(period, alpha), bars);
            await CompareRoutes(new FisherTransformStochasticOscillator(period), Stock(bars).CalculateFisherTransformStochasticOscillator(length: period),
                new FisherTransformStochasticOscillatorState(length: period), bars);
        }
    }

    [Fact]
    public async Task MultiDepthAndSineContractsMatchAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 6, 50 })
        {
            await CompareRoutes(new MultiDepthZeroLagExponentialMovingAverage(period), Stock(bars).CalculateMultiDepthZeroLagExponentialMovingAverage(period),
                new MultiDepthZeroLagExponentialMovingAverageState(period), bars);
            await CompareRoutes(new MorphedSineWave(period), Stock(bars).CalculateMorphedSineWave(period),
                new MorphedSineWaveState(period), bars);
        }
    }

    [Fact]
    public async Task SecondOrderEstimatorContractsMatchAllRoutes()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 6, 100, 200 })
        {
            await CompareRoutes(new KalmanSmoother(period), Stock(bars).CalculateKalmanSmoother(period),
                new KalmanSmootherState(period), bars);
            await CompareRoutes(new IIRLeastSquaresEstimate(period), Stock(bars).CalculateIIRLeastSquaresEstimate(period),
                new IIRLeastSquaresEstimateState(period), bars);
        }
    }

    [Fact]
    public async Task EstimatorAndGroverContractsMatchAllRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 6, 100 })
        {
            await CompareRoutes(new GeneralFilterEstimator(period), Stock(bars).CalculateGeneralFilterEstimator(period),
                new GeneralFilterEstimatorState(period), bars);
            await CompareRoutes(new GroverLlorensCycleOscillator(period), Stock(bars).CalculateGroverLlorensCycleOscillator(length: period),
                new GroverLlorensCycleOscillatorState(length: period), bars);
            foreach (var multiplier in new[] { 0d, 1, 5 })
                await CompareRoutes(new GroverLlorensActivator(period, multiplier), Stock(bars).CalculateGroverLlorensActivator(length: period, mult: multiplier),
                    new GroverLlorensActivatorState(length: period, mult: multiplier), bars);
        }
    }

    [Fact]
    public async Task AdaptiveAndRankFiltersMatchAllRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 20 })
        {
            await CompareRoutes(new MovingAverageAdaptiveQ(period), Stock(bars).CalculateMovingAverageAdaptiveQ(period),
                new MovingAverageAdaptiveQState(period), bars);
            await CompareRoutes(new OscarIndicator(period), Stock(bars).CalculateOscarIndicator(period),
                new OscarIndicatorState(period), bars);
            await CompareRoutes(new KarobeinOscillator(period), Stock(bars).CalculateKarobeinOscillator(length: period),
                new KarobeinOscillatorState(length: period), bars);
            foreach (var beta in new[] { 0d, .8, 1 })
                await CompareRoutes(new ModularFilter(period, beta, .5), Stock(bars).CalculateModularFilter(period, beta),
                    new ModularFilterState(period, beta), bars);
        }
    }

    [Fact]
    public async Task RangeAndSmoothingCompositesMatchAllRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 15 })
        {
            await CompareRoutes(new TillsonIE2(period), Stock(bars).CalculateTillsonIE2(length: period),
                new TillsonIE2State(length: period), bars);
            await CompareRoutes(new TheRangeIndicator(period, 3), Stock(bars).CalculateTheRangeIndicator(length: period),
                new TheRangeIndicatorState(length: period), bars);
            await CompareRoutes(new TurboTrigger(period, 1), Stock(bars).CalculateTurboTrigger(length: period),
                new TurboTriggerState(length: period), bars);
            await CompareRoutes(new TradingMadeMoreSimplerOscillator(period), Stock(bars).CalculateTradingMadeMoreSimplerOscillator(length1: period),
                new TradingMadeMoreSimplerOscillatorState(length1: period), bars);
        }
    }

    [Fact]
    public async Task PriceStepContractsMatchAllRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 100 })
        {
            await CompareRoutes(new RetrospectiveCandlestickChart(period), Stock(bars).CalculateRetrospectiveCandlestickChart(period),
                new RetrospectiveCandlestickChartState(period), bars);
            await CompareRoutes(new TrendImpulseFilter(period, 3), Stock(bars).CalculateTrendImpulseFilter(length1: period, length2: 3),
                new TrendImpulseFilterState(length1: period, length2: 3), bars);
        }
        await CompareRoutes(new SettingLessTrendStepFiltering(1), Stock(bars).CalculateSettingLessTrendStepFiltering(),
            new SettingLessTrendStepFilteringState(), bars);
    }

    [Fact]
    public async Task RsiAndRahulCompositesMatchEveryOutputAcrossRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 14 })
        {
            await CompareRoutes(new RahulMohindarOscillator(period), Stock(bars).CalculateRahulMohindarOscillator(length2: period),
                new RahulMohindarOscillatorState(length2: period), bars);
            foreach (var target in new[] { 20d, 50, 80 })
                await CompareRoutes(new ReverseEngineeringRsi(period, target), Stock(bars).CalculateReverseEngineeringRelativeStrengthIndex(period, target),
                    new ReverseEngineeringRelativeStrengthIndexState(period, target), bars);
            await CompareRoutes(new TradersDynamicIndex(period, 3, 2, 4), Stock(bars).CalculateTradersDynamicIndex(length1: period, length2: 3, length3: 2, length4: 4),
                new TradersDynamicIndexState(length1: period, length2: 3, length3: 2, length4: 4), bars);
        }
    }

    [Fact]
    public async Task HistoricalVolatilityPercentilePreviewsEvictOldValues()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 21 })
            foreach (var annual in new[] { 1, 2, 30, 252 })
                await CompareRoutes(new HistoricalVolatilityPercentile(period, annual),
                    Stock(bars).CalculateHistoricalVolatilityPercentile(length: period, annualLength: annual),
                    new HistoricalVolatilityPercentileState(length: period, annualLength: annual), bars);
    }

    [Fact]
    public async Task KaufmanBinaryWaveMatchesAllRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 20 })
            foreach (var threshold in new[] { 0d, 10, 100 })
                await CompareRoutes(new KaufmanBinaryWave(period, .6022, .0645, threshold),
                    Stock(bars).CalculateKaufmanBinaryWave(period, .6022, .0645, threshold),
                    new KaufmanBinaryWaveState(period, .6022, .0645, threshold), bars);
    }

    [Fact]
    public async Task QqeWidthsMatchAllRoutesAndScaleWithTheirFactors()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 14 })
        {
            var expected = Stock(bars).CalculateQuantitativeQualitativeEstimation(length: period, smoothLength: 3, fastFactor: 2, slowFactor: 4);
            for (var i = 0; i < bars.Length; i++) Assert.Equal(2 * expected.OutputValues["FastAtrRsi"][i], expected.OutputValues["SlowAtrRsi"][i]);
            await CompareRoutes(new QuantitativeQualitativeEstimation(period, 3, 2, 4), expected,
                new QuantitativeQualitativeEstimationState(length: period, smoothLength: 3, fastFactor: 2, slowFactor: 4), bars);
        }
    }

    [Fact]
    public async Task PrimeIndicatorsHandleSmallPrimesAndCompositeSquaresAcrossRoutes()
    {
        var bars = new[] { 1d, 2, 3, 4, 9, 49, 121, 127, 119 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, Math.Max(0, v - 1), v, 1)).ToArray();
        foreach (var period in new[] { 1, 2, 5, 100 })
        {
            await CompareRoutes(new PrimeNumberOscillator(period), Stock(bars).CalculatePrimeNumberOscillator(period),
                new PrimeNumberOscillatorState(period), bars);
            await CompareRoutes(new PrimeNumberBands(period), Stock(bars).CalculatePrimeNumberBands(period),
                new PrimeNumberBandsState(period), bars);
        }
        var small = Stock(bars.Skip(1).Take(2).ToArray()).CalculatePrimeNumberOscillator(5);
        Assert.All(small.CustomValuesList, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task SuperTrendFilterMatchesAllRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 200 })
            foreach (var factor in new[] { 0d, .9, 1 })
                await CompareRoutes(new SuperTrendFilter(period, factor), Stock(bars).CalculateSuperTrendFilter(period, factor),
                    new SuperTrendFilterState(period, factor), bars);
    }

    [Fact]
    public async Task PeriodicChannelMatchesAllEightOutputsAcrossRoutes()
    {
        var bars = Market(160);
        foreach (var period in new[] { 1, 2, 500 })
            foreach (var correlation in new[] { 1, 2, 5 })
                await CompareRoutes(new PeriodicChannel(period, correlation), Stock(bars).CalculatePeriodicChannel(period, correlation),
                    new PeriodicChannelState(period, correlation), bars);
    }

    [Fact]
    public async Task VolatilityStopVixAndVostroMatchAllRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 14 })
        {
            await CompareRoutes(new VolatilityStop(period, 1.5), Stock(bars).CalculateVolatilityStop(period, 1.5),
                new VolatilityStopState(period, 1.5), bars);
            await CompareRoutes(new VixTradingSystem(period), Stock(bars).CalculateVixTradingSystem(length: period),
                new VixTradingSystemState(length: period), bars);
            await CompareRoutes(new VostroIndicator(period, 20, 8), Stock(bars).CalculateVostroIndicator(length1: period, length2: 20),
                new VostroIndicatorState(length1: period, length2: 20), bars);
        }
    }

    [Fact]
    public async Task DirectionalCompositesMatchAllRoutes()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 14 })
        {
            await CompareRoutes(new DynamicMomentumOscillator(period), Stock(bars).CalculateDynamicMomentumOscillator(length1: period),
                new DynamicMomentumOscillatorState(length1: period), bars);
            await CompareRoutes(new ErgodicCommoditySelectionIndex(period, 2, 3), Stock(bars).CalculateErgodicCommoditySelectionIndex(length: period, smoothLength: 2, pointValue: 3),
                new ErgodicCommoditySelectionIndexState(length: period, smoothLength: 2, pointValue: 3), bars);
            await CompareRoutes(new CommoditySelectionIndex(period), Stock(bars).CalculateCommoditySelectionIndex(length: period),
                new CommoditySelectionIndexState(length: period), bars);
            await CompareRoutes(new DirectionalTrendIndex(period), Stock(bars).CalculateDirectionalTrendIndex(length1: period),
                new DirectionalTrendIndexState(length1: period), bars);
            await CompareRoutes(new DMIStochastic(period), Stock(bars).CalculateDMIStochastic(length1: period),
                new DMIStochasticState(length1: period), bars);
        }
    }

    [Fact]
    public async Task AutocorrelationReversalsUseOnlyObservedPairs()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 24 })
            foreach (var lag in new[] { 0, 1, 3 })
                await CompareRoutes(new EhlersAutoCorrelationReversals(period, 10, lag),
                    Stock(bars).CalculateEhlersAutoCorrelationReversals(length1: period, length2: 10, length3: lag),
                    new EhlersAutoCorrelationReversalsState(length1: period, length2: 10, length3: lag), bars);
    }

    [Fact]
    public async Task AdaptiveLaguerreDoesNotAmplifyPriceOffsetRoundoff()
    {
        var bars = Enumerable.Range(0, 180).Select(i =>
        {
            var value = 100 + .1 * i;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
        }).ToArray();
        var baseline = Stock(bars).CalculateEhlersAdaptiveLaguerreFilter(14);
        foreach (var offset in new[] { 0d, 1000, 1000000 })
        {
            var shifted = bars.Select(b => new Bar(b.Time, b.Open + offset, b.High + offset, b.Low + offset, b.Close + offset, b.Volume)).ToArray();
            var actual = Stock(shifted).CalculateEhlersAdaptiveLaguerreFilter(14);
            for (var i = 0; i < bars.Length; i++) Assert.InRange(Math.Abs(actual.CustomValuesList[i] - offset - baseline.CustomValuesList[i]), 0, 1e-7);
            await CompareRoutes(new EhlersAdaptiveLaguerreFilter(14), actual, new EhlersAdaptiveLaguerreFilterState(14), shifted);
        }
    }

    [Fact]
    public async Task AdaptiveLaguerreMatchesAllRoutes()
    {
        var bars = Market(160);
        foreach (var period in new[] { 1, 2, 14 })
            await CompareRoutes(new EhlersAdaptiveLaguerreFilter(period),
                Stock(bars).CalculateEhlersAdaptiveLaguerreFilter(period),
                new EhlersAdaptiveLaguerreFilterState(period), bars);
    }

    [Fact]
    public async Task AdaptiveCyberAndGravityMatchAllRoutes()
    {
        var bars = Market(160);
        foreach (var period in new[] { 1, 2, 5 })
        {
            foreach (var alpha in new[] { .07, .2, .8 })
                await CompareRoutes(new EhlersAdaptiveCyberCycle(period, alpha),
                    Stock(bars).CalculateEhlersAdaptiveCyberCycle(period, alpha),
                    new EhlersAdaptiveCyberCycleState(period, alpha), bars);
            await CompareRoutes(new EhlersAdaptiveCenterOfGravityOscillator(period),
                Stock(bars).CalculateEhlersAdaptiveCenterOfGravityOscillator(period),
                new EhlersAdaptiveCenterOfGravityOscillatorState(period), bars);
        }
    }

    [Fact]
    public async Task SquelchMatchesAllRoutesAndNeverInventsAnUnmeasuredCycle()
    {
        var bars = Market(180);
        foreach (var period in new[] { 1, 2, 6 })
        {
            var expected = Stock(bars).CalculateEhlersSquelchIndicator(period, 10, 40);
            Assert.All(expected.CustomValuesList, v => Assert.True(v == 0 || v == 1));
            await CompareRoutes(new EhlersSquelchIndicator(period, 10, 40), expected,
                new EhlersSquelchIndicatorState(period, 10, 40), bars);
        }
        // Five advances, each at most sixty degrees, cannot make a complete cycle.
        var shortHorizon = Stock(bars).CalculateEhlersSquelchIndicator(6, 1, 4);
        Assert.All(shortHorizon.CustomValuesList, v => Assert.Equal(0, v));
    }

    [Fact]
    public async Task DeviationAveragesMatchAllRoutes()
    {
        var bars = Market(160);
        foreach (var period in new[] { 1, 2, 20 })
        {
            await CompareRoutes(new EhlersDeviationScaledSuperSmoother(period, 2),
                Stock(bars).CalculateEhlersDeviationScaledSuperSmoother(length1: period),
                new EhlersDeviationScaledSuperSmootherState(length1: period), bars);
            await CompareRoutes(new EhlersDeviationScaledMovingAverage(period),
                Stock(bars).CalculateEhlersDeviationScaledMovingAverage(fastLength: period, slowLength: 2 * period),
                new EhlersDeviationScaledMovingAverageState(fastLength: period, slowLength: 2 * period), bars);
            await CompareRoutes(new EhlersFisherizedDeviationScaledOscillator(period),
                Stock(bars).CalculateEhlersFisherizedDeviationScaledOscillator(fastLength: period),
                new EhlersFisherizedDeviationScaledOscillatorState(fastLength: period), bars);
        }
    }

    [Fact]
    public async Task EmpiricalModeDecompositionPublishesAllThreeOutputs()
    {
        var bars = Market(140);
        foreach (var period in new[] { 1, 2, 20 })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage })
                await CompareRoutes(new EhlersEmpiricalModeDecomposition(period, 3, .5, .25,
                        maType: kind == MovingAvgType.SimpleMovingAverage ? new Sma() : new Wma()),
                    Stock(bars).CalculateEhlersEmpiricalModeDecomposition(kind, period, 3, .5, .25),
                    new EhlersEmpiricalModeDecompositionState(kind, period, 3, .5, .25), bars);
    }

    [Fact]
    public async Task HurstAndRecursiveMedianMatchAllRoutes()
    {
        var bars = Market(120);
        foreach (var period in new[] { 1, 2, 5, 30 })
        {
            await CompareRoutes(new EhlersHurstCoefficient(period, period),
                Stock(bars).CalculateEhlersHurstCoefficient(period, period),
                new EhlersHurstCoefficientState(period, period), bars);
            await CompareRoutes(new EhlersRecursiveMedianOscillator(period, period, period),
                Stock(bars).CalculateEhlersRecursiveMedianOscillator(period, period, period),
                new EhlersRecursiveMedianOscillatorState(period, period, period), bars);
        }
    }

    [Fact]
    public async Task DemodulatorSineWaveAndMarketStateMatchAllRoutes()
    {
        var bars = Market(120);
        foreach (var period in new[] { 1, 2, 20 })
        {
            await CompareRoutes(new EhlersFMDemodulatorIndicator(2, period),
                Stock(bars).CalculateEhlersFMDemodulatorIndicator(fastLength: 2, slowLength: period),
                new EhlersFMDemodulatorIndicatorState(fastLength: 2, slowLength: period), bars);
            var wave = Stock(bars).CalculateEhlersEvenBetterSineWaveIndicator(period, period);
            Assert.All(wave.CustomValuesList, value => Assert.InRange(value, -1 - 1e-12, 1 + 1e-12));
            await CompareRoutes(new EhlersEvenBetterSineWaveIndicator(period, period), wave,
                new EhlersEvenBetterSineWaveIndicatorState(period, period), bars);
            await CompareRoutes(new EhlersMarketStateIndicator(period), Stock(bars).CalculateEhlersMarketStateIndicator(period),
                new EhlersMarketStateIndicatorState(period), bars);
        }
    }

    [Fact]
    public async Task ModifiedOscillatorsPreserveDecayingRoofingTails()
    {
        var bars = Enumerable.Range(0, 256).Select(i =>
        {
            var price = i % 2 == 0 ? 80d : 120d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + .5, price - .5, price, 1000);
        }).ToArray();
        var rsi = Stock(bars).CalculateEhlersModifiedRelativeStrengthIndex(1, 1, 1);
        foreach (var value in rsi.OutputValues["Emrsi"].Skip(200)) Close(1, value);
        await CompareRoutes(new EhlersModifiedRelativeStrengthIndex(1, 1, 1), rsi,
            new EhlersModifiedRelativeStrengthIndexState(1, 1, 1), bars);
        var stochastic = Stock(bars).CalculateEhlersModifiedStochasticIndicator(length1: 24, length2: 5, length3: 10);
        foreach (var value in stochastic.CustomValuesList.Skip(200)) Close(100, value);
        await CompareRoutes(new EhlersModifiedStochasticIndicator(24, 5, 10), stochastic,
            new EhlersModifiedStochasticIndicatorState(length1: 24, length2: 5, length3: 10), bars);
    }

    [Fact]
    public async Task ModifiedEhlersOscillatorsUseMatchedWindowsAndPublishSignals()
    {
        var bars = Market(120);
        foreach (var period in new[] { 1, 2, 10 })
        {
            var batch = Stock(bars).CalculateEhlersModifiedRelativeStrengthIndex(48, period, 8);
            var radius = Math.Exp(-Math.Sqrt(2) * Math.PI / period);
            var c2 = 2 * radius * Math.Cos(Math.Min(Math.Sqrt(2) * Math.PI / period, .99));
            var c3 = -radius * radius; var c1 = 1 - c2 - c3;
            var rsi = batch.OutputValues["Emrsi"];
            for (var i = 1; i < bars.Length; i++)
            {
                var normalizedDrive = (rsi[i] - c2 * rsi[i - 1] - c3 * (i < 2 ? 0 : rsi[i - 2])) / c1;
                Assert.InRange(normalizedDrive, -1e-10, 1 + 1e-10);
            }
            await CompareRoutes(new EhlersModifiedRelativeStrengthIndex(48, period, 8), batch,
                new EhlersModifiedRelativeStrengthIndexState(48, period, 8), bars);
            await CompareRoutes(new EhlersModifiedStochasticIndicator(48, 10, period),
                Stock(bars).CalculateEhlersModifiedStochasticIndicator(length1: 48, length2: 10, length3: period),
                new EhlersModifiedStochasticIndicatorState(length1: 48, length2: 10, length3: period), bars);
        }
    }

    [Fact]
    public async Task OscillatorInverseFisherTransformsMatchAllRoutesAndStayBounded()
    {
        var bars = Market(100);
        foreach (var period in new[] { 1, 2, 20 })
        {
            await CompareRoutes(new EhlersRelativeStrengthIndexInverseFisherTransform(period, 2),
                Stock(bars).CalculateEhlersRelativeStrengthIndexInverseFisherTransform(length: period, signalLength: 2),
                new EhlersRelativeStrengthIndexInverseFisherTransformState(length: period, signalLength: 2), bars);
            foreach (var constant in new[] { .015, .00001 })
            {
                var batch = Stock(bars).CalculateEhlersCommodityChannelIndexInverseFisherTransform(length: period, signalLength: 2, constant: constant);
                Assert.All(batch.CustomValuesList, value => Assert.InRange(value, -1, 1));
                await CompareRoutes(new EhlersCommodityChannelIndexInverseFisherTransform(period, 2, constant), batch,
                    new EhlersCommodityChannelIndexInverseFisherTransformState(length: period, signalLength: 2, constant: constant), bars);
            }
        }
    }

    [Fact]
    public async Task StochasticGravityAndImpulseFiltersMatchAllRoutes()
    {
        var bars = Market(100);
        foreach (var period in new[] { 1, 2, 20 })
        {
            await CompareRoutes(new EhlersStochasticCenterOfGravityOscillator(period),
                Stock(bars).CalculateEhlersStochasticCenterOfGravityOscillator(period),
                new EhlersStochasticCenterOfGravityOscillatorState(period), bars);
            await CompareRoutes(new EhlersImpulseResponse(period), Stock(bars).CalculateEhlersImpulseResponse(length: period),
                new EhlersImpulseResponseState(length: period), bars);
            await CompareRoutes(new EhlersImpulseReaction(period, period), Stock(bars).CalculateEhlersImpulseReaction(period, period),
                new EhlersImpulseReactionState(period, period), bars);
        }
    }

    [Fact]
    public async Task BandPassAndStochasticCyberCyclePublishTheirOwnTriggers()
    {
        var bars = Market(100);
        foreach (var period in new[] { 1, 2, 20 })
        {
            await CompareRoutes(new EhlersBandPassFilterV1(period), Stock(bars).CalculateEhlersBandPassFilterV1(period),
                new EhlersBandPassFilterV1State(period), bars);
            await CompareRoutes(new EhlersStochasticCyberCycle(period), Stock(bars).CalculateEhlersStochasticCyberCycle(period),
                new EhlersStochasticCyberCycleState(period), bars);
        }
    }

    [Fact]
    public async Task OriginalRoofingAndEarlyOnsetTrendMatchAllRoutes()
    {
        var bars = Market(100);
        foreach (var period in new[] { 1, 2, 40 })
        {
            await CompareRoutes(new EhlersRoofingFilterIndicator(period, period),
                Stock(bars).CalculateEhlersRoofingFilterIndicator(period, period),
                new EhlersRoofingFilterIndicatorState(period, period), bars);
            await CompareRoutes(new EhlersEarlyOnsetTrendIndicator(period, period),
                Stock(bars).CalculateEhlersEarlyOnsetTrendIndicator(period, period),
                new EhlersEarlyOnsetTrendIndicatorState(period, period), bars);
        }
    }

    [Fact]
    public async Task RoofingAliasesAndRoofedStochasticMatchAllRoutes()
    {
        var bars = Market(100);
        foreach (var period in new[] { 1, 2, 20 })
        {
            var roof = Stock(bars).CalculateEhlersRoofingFilterV1(length1: period, length2: 10);
            await CompareRoutes(new EhlersRoofingFilterV1(period, 10), roof,
                new EhlersRoofingFilterV1State(length1: period, length2: 10), bars);
            await CompareRoutes(new EhlersRoofingFilter(period, 10), roof,
                new EhlersRoofingFilterV1State(length1: period, length2: 10), bars);
            await CompareRoutes(new EhlersStochastic(period), Stock(bars).CalculateEhlersStochastic(length2: period),
                new EhlersStochasticState(length2: period), bars);
        }
    }

    [Fact]
    public async Task ReverseEmaWavesUseIndependentPriceInputsAndRoofingMatchesAcrossRoutes()
    {
        var bars = Market(100);
        foreach (var alpha in new[] { .01, .1, .5, .99 })
        {
            await CompareRoutes(new EhlersReverseEmaIndicatorV1(alpha),
                Stock(bars).CalculateEhlersReverseExponentialMovingAverageIndicatorV1(alpha),
                new EhlersReverseExponentialMovingAverageIndicatorV1State(alpha), bars);
            var both = Stock(bars).CalculateEhlersReverseExponentialMovingAverageIndicatorV2(alpha, .3);
            var cycle = Stock(bars).CalculateEhlersReverseExponentialMovingAverageIndicatorV1(.3);
            for (var i = 0; i < bars.Length; i++) Close(cycle.CustomValuesList[i], both.OutputValues["EremaCycle"][i]);
            await CompareRoutes(new EhlersReverseEmaIndicatorV2(alpha, .3), both,
                new EhlersReverseExponentialMovingAverageIndicatorV2State(alpha, .3), bars);
        }
        foreach (var period in new[] { 1, 2, 48 })
            await CompareRoutes(new EhlersHpLpRoofingFilter(period, 10), Stock(bars).CalculateEhlersHpLpRoofingFilter(period, 10),
                new EhlersHpLpRoofingFilterState(period, 10), bars);
    }

    [Fact]
    public async Task SupportUberAndKwanMatchAllRoutes()
    {
        var bars = Market(90);
        await CompareRoutes(new SupportAndResistanceOscillator(1), Stock(bars).CalculateSupportAndResistanceOscillator(),
            new SupportAndResistanceOscillatorState(), bars);
        foreach (var period in new[] { 1, 2, 14 })
        {
            await CompareRoutes(new UberTrendIndicator(period), Stock(bars).CalculateUberTrendIndicator(period),
                new UberTrendIndicatorState(period), bars);
            await CompareRoutes(new KwanIndicator(period, 2), Stock(bars).CalculateKwanIndicator(length: period, smoothLength: 2),
                new KwanIndicatorState(length: period, smoothLength: 2), bars);
        }
    }

    [Fact]
    public async Task SerialDependencyPriceCycleAndPhaseChangeMatchAllRoutes()
    {
        var bars = Market(90);
        foreach (var period in new[] { 1, 2, 14 })
        {
            await CompareRoutes(new KaseSerialDependencyIndex(period), Stock(bars).CalculateKaseSerialDependencyIndex(period),
                new KaseSerialDependencyIndexState(period), bars);
            await CompareRoutes(new PriceCycleOscillator(period), Stock(bars).CalculatePriceCycleOscillator(length: period),
                new PriceCycleOscillatorState(length: period), bars);
            await CompareRoutes(new PhaseChangeIndex(period, 2), Stock(bars).CalculatePhaseChangeIndex(length: period, smoothLength: 2),
                new PhaseChangeIndexState(length: period, smoothLength: 2), bars);
        }
    }

    [Fact]
    public async Task KasePeakOutputsAndConvergenceMatchAllRoutes()
    {
        var bars = Market(120);
        foreach (var period in new[] { 1, 2, 30 })
        {
            await CompareRoutes(new KasePeakOscillatorV1(period, 2),
                Stock(bars).CalculateKasePeakOscillatorV1(period, 2),
                new KasePeakOscillatorV1State(period, 2), bars);
            await CompareRoutes(new KaseConvergenceDivergence(period, 2, 3),
                Stock(bars).CalculateKaseConvergenceDivergence(length1: period, length2: 2, length3: 3),
                new KaseConvergenceDivergenceState(length1: period, length2: 2, length3: 3), bars);
            await CompareRoutes(new KasePeakOscillatorV2(period),
                Stock(bars).CalculateKasePeakOscillatorV2(length2: period),
                new KasePeakOscillatorV2State(length2: period), bars);
        }
    }

    [Theory]
    [InlineData(2, 10, 10, false)]
    [InlineData(8, 32, 32, false)]
    [InlineData(5, 21, 150, true)]
    public async Task KaseV1EqualTrendMeansUseTheLongStop(int fast, int slow, int index, bool weighted)
    {
        var bars = new List<Bar>();
        var previous = 100d;
        for (var i = 0; i < 180; i++)
        {
            var price = weighted ? i == 128 ? 200 : 100 : i % 2 == 0 ? 80 : 120;
            bars.Add(new Bar(DateTime.UnixEpoch.AddMinutes(i), previous,
                Math.Max(previous, price) + .5, Math.Min(previous, price) - .5, price, 1000));
            previous = price;
        }
        var kind = weighted ? MovingAvgType.WeightedMovingAverage : MovingAvgType.SimpleMovingAverage;
        var batch = Stock(bars.ToArray()).CalculateKaseDevStopV1(kind, fast, slow, 1);
        var bar = bars[index];
        var range = Math.Max(bar.High - bars[index - 2].Low,
            Math.Max(Math.Abs(bar.High - bars[index - 2].Close), Math.Abs(bar.Low - bars[index - 2].Close)));
        var expected = (bar.High + bar.Low + bar.Close) / 3 - range;
        foreach (var output in batch.OutputValues.Values) Close(expected, output[index]);
        await CompareRoutes(new KaseDevStopV1(fast, slow, 1, maType: weighted ? new Wma(7) : null), batch,
            new KaseDevStopV1State(kind, fast, slow, 1), bars.ToArray());
    }

    [Fact]
    public async Task KaseV1SupportsAllLevelsAndMultiLevelDoesNotLoseOpenDifferencesToLargeCloses()
    {
        foreach (var period in new[] { 1, 2, 10 })
        {
            var bars = Market(90);
            await CompareRoutes(new KaseDevStopV1(2, 5, period),
                Stock(bars).CalculateKaseDevStopV1(fastLength: 2, slowLength: 5, length: period),
                new KaseDevStopV1State(fastLength: 2, slowLength: 5, length: period), bars);
        }
        var large = Enumerable.Range(0, 4).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 10 + i, 1e16, 10, 1e16, 1)).ToArray();
        var multi = Stock(large).CalculateMultiLevelIndicator(1, 2);
        Assert.Equal(new[] { -20d, -2, -2, -2 }, multi.CustomValuesList);
        await CompareRoutes(new MultiLevelIndicator(1, 2), multi, new MultiLevelIndicatorState(1, 2), large);
    }

    [Fact]
    public async Task CalmarMinimumPeriodAndDrawdownRecoveryMatchAcrossRoutes()
    {
        foreach (var period in new[] { 1, 2, 30 })
        {
            var bars = Market(90);
            await CompareRoutes(new CalmarRatio(period), Stock(bars).CalculateCalmarRatio(period),
                new CalmarRatioState(period), bars);
        }
    }

    [Fact]
    public async Task AutocorrelationAndPeriodogramSupportDegenerateWindowsAndPreviewReset()
    {
        foreach (var bars in new[] { Market(90), Market(90, 100) })
        foreach (var period in new[] { 1, 4, 12 })
        {
            var correlation = Stock(bars).CalculateEhlersAutoCorrelationIndicator(period, 3);
            Assert.All(correlation.OutputValues["Eaci"], value => Assert.InRange(value, 0, 1));
            if (period == 1) Assert.All(correlation.OutputValues["Eaci"], value => Assert.Equal(0, value));
            await CompareRoutes(new EhlersAutoCorrelationIndicator(period, 3), correlation,
                new EhlersAutoCorrelationIndicatorState(period, 3), bars);
            await CompareRoutes(new EhlersAutoCorrelationPeriodogram(period, 3, 0), Stock(bars).CalculateEhlersAutoCorrelationPeriodogram(period, 3, 0),
                new EhlersAutoCorrelationPeriodogramState(period, 3, 0), bars);
        }
    }

    [Fact]
    public async Task ResidualBandOutputsMatchAndVortexWidthsCannotBecomeNegative()
    {
        var spike = Enumerable.Range(0, 180).Select(i =>
        {
            var value = i == 20 ? 200d : 100;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
        }).ToArray();
        foreach (var bars in new[] { Market(90), spike })
        foreach (var period in new[] { 1, 2, 20 })
        {
            var vortex = Stock(bars).CalculateVortexBands(length: period);
            for (var i = 0; i < bars.Length; i++)
            {
                Assert.True(vortex.OutputValues["UpperBand"][i] >= vortex.OutputValues["MiddleBand"][i]);
                Assert.True(vortex.OutputValues["MiddleBand"][i] >= vortex.OutputValues["LowerBand"][i]);
            }
            await CompareRoutes(new VortexBands(period), vortex, new VortexBandsState(length: period), bars);
            await CompareRoutes(new HirashimaSugitaRS(period), Stock(bars).CalculateHirashimaSugitaRS(length: period),
                new HirashimaSugitaRSState(length: period), bars);
        }
    }

    [Fact]
    public async Task HurstBandsTreatZeroAsDataAndTimeMoneyWidthReturnsToZeroAfterASpike()
    {
        foreach (var bars in new[] { Market(90), Enumerable.Range(0, 90).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), i % 3 == 0 ? 0 : 10, i % 3 == 0 ? 0 : 10,
                i % 3 == 0 ? 0 : 10, i % 3 == 0 ? 0 : 10, 1)).ToArray() })
        {
            await CompareRoutes(new HurstBands(2), Stock(bars).CalculateHurstBands(2), new HurstBandsState(2), bars);
            await CompareRoutes(new HurstCycleChannel(4, 8), Stock(bars).CalculateHurstCycleChannel(fastLength: 4, slowLength: 8),
                new HurstCycleChannelState(fastLength: 4, slowLength: 8), bars);
            await CompareRoutes(new HighLowMovingAverage(3), Stock(bars).CalculateHighLowMovingAverage(length: 3),
                new HighLowMovingAverageState(length: 3), bars);
        }
        var spike = Enumerable.Range(0, 80).Select(i =>
        {
            var value = i == 20 ? 201d : 100;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
        }).ToArray();
        var channel = Stock(spike).CalculateTimeAndMoneyChannel(length1: 3, length2: 7);
        for (var i = 50; i < spike.Length; i++) Assert.Equal(0, channel.OutputValues["Median"][i]);
        await CompareRoutes(new TimeAndMoneyChannel(3, 7), channel, new TimeAndMoneyChannelState(length1: 3, length2: 7), spike);
    }

    [Fact]
    public async Task UltimateTraderHasCorrectEndpointsAndDoesNotDependOnPriceOrVolumeUnits()
    {
        var bullish = Enumerable.Range(0, 30).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 2, i + 1, i + 2, 1)).ToArray();
        var endpoint = Stock(bullish).CalculateUltimateTraderOscillator();
        Close(100, endpoint.OutputValues["Uto"].Last());
        Close(100, endpoint.OutputValues["Signal"].Last());
        var flat = Market(30, 0);
        var baseline = Market(90).Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, 100 + i % 7)).ToArray();
        var expected = Stock(baseline).CalculateUltimateTraderOscillator();
        foreach (var fixture in new[] { bullish, flat, baseline,
            baseline.Select(b => new Bar(b.Time, b.Open * 100, b.High * 100, b.Low * 100, b.Close * 100, b.Volume)).ToArray(),
            baseline.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, b.Volume * 1000)).ToArray() })
        {
            var batch = Stock(fixture).CalculateUltimateTraderOscillator();
            await CompareRoutes(new UltimateTraderOscillator(10), batch, new UltimateTraderOscillatorState(), fixture);
            if (fixture.Length == baseline.Length)
                foreach (var key in new[] { "Uto", "Signal" })
                    for (var i = 0; i < fixture.Length; i++) Close(expected.OutputValues[key][i], batch.OutputValues[key][i]);
        }
    }

    [Fact]
    public async Task EveryPriceCoordinateAndChannelBandMatchesAcrossRoutes()
    {
        var bars = Market(100);
        foreach (var period in new[] { 1, 2, 5 })
        {
            await CompareRoutes(new ValueChartIndicator(period, 8), Stock(bars).CalculateValueChartIndicator(length: period),
                new ValueChartIndicatorState(length: period), bars);
            await CompareRoutes(new WilsonRelativePriceChannel(period, 2), Stock(bars).CalculateWilsonRelativePriceChannel(length: period, smoothLength: 2),
                new WilsonRelativePriceChannelState(length: period, smoothLength: 2), bars);
            await CompareRoutes(new TimeAndMoneyChannel(period, 3), Stock(bars).CalculateTimeAndMoneyChannel(length1: period, length2: 3),
                new TimeAndMoneyChannelState(length1: period, length2: 3), bars);
        }
    }

    [Fact]
    public async Task PressureComponentsAndMovementMinimumPeriodsMatchAcrossRoutes()
    {
        var bars = Market(80);
        foreach (var period in new[] { 1, 2, 7 })
        {
            await CompareRoutes(new TraderPressureIndex(period, 2, 3), Stock(bars).CalculateTraderPressureIndex(length1: period),
                new TraderPressureIndexState(length1: period), bars);
            await CompareRoutes(new TrendTriggerFactor(period), Stock(bars).CalculateTrendTriggerFactor(period),
                new TrendTriggerFactorState(period), bars);
            foreach (var lagPeriod in new[] { 1, 2, 3 })
            {
                var movement = Stock(bars).CalculateStrengthOfMovement(length1: period, length2: lagPeriod);
                Assert.All(movement.OutputValues["Som"], value => Assert.True(double.IsFinite(value)));
                await CompareRoutes(new StrengthOfMovement(period, lagPeriod), movement,
                    new StrengthOfMovementState(length1: period, length2: lagPeriod), bars);
            }
        }
    }

    [Fact]
    public async Task StochasticSecondaryOutputsAndSignedTurboWindowsMatchAcrossRoutes()
    {
        var bars = Market(80);
        await CompareRoutes(new StochasticCustomOscillator(3), Stock(bars).CalculateStochasticCustomOscillator(length1: 3),
            new StochasticCustomOscillatorState(length1: 3), bars);
        await CompareRoutes(new StochasticMacdOscillator(3), Stock(bars).CalculateStochasticMovingAverageConvergenceDivergenceOscillator(length: 3),
            new StochasticMovingAverageConvergenceDivergenceOscillatorState(length: 3), bars);
        foreach (var turbo in new[] { -10, -2, 0, 2, 10 })
        {
            await CompareRoutes(new TurboStochasticsFast(3, 2, turbo), Stock(bars).CalculateTurboStochasticsFast(length1: 3, length2: 2, turboLength: turbo),
                new TurboStochasticsFastState(length1: 3, length2: 2, turboLength: turbo), bars);
            var slow = Stock(bars).CalculateTurboStochasticsSlow(length1: 3, length2: 2, turboLength: turbo);
            Assert.Equal(bars.Length, slow.SignalsList.Count);
            await CompareRoutes(new TurboStochasticsSlow(3, 2, turbo), slow,
                new TurboStochasticsSlowState(length1: 3, length2: 2, turboLength: turbo), bars);
        }
    }

    [Fact]
    public async Task EhlersCorrelationsRejectDcAndPublishBothCycleCoordinates()
    {
        foreach (var period in new[] { 1, 2, 4, 20 })
        foreach (var bars in new[] { Market(60), Market(60, 100), Enumerable.Range(0, 60).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 1e12 + i, 1e12 + i, 1e12 + i, 1e12 + i, 1)).ToArray() })
        {
            var trend = Stock(bars).CalculateEhlersCorrelationTrendIndicator(period);
            await CompareRoutes(new EhlersCorrelationTrendIndicator(period), trend, new EhlersCorrelationTrendIndicatorState(period), bars);
            await CompareRoutes(new EhlersCorrelationCycleIndicator(period), Stock(bars).CalculateEhlersCorrelationCycleIndicator(period),
                new EhlersCorrelationCycleIndicatorState(period), bars);
            await CompareRoutes(new EhlersCorrelationAngleIndicator(period), Stock(bars).CalculateEhlersCorrelationAngleIndicator(period),
                new EhlersCorrelationAngleIndicatorState(period), bars);
            var core = new double[bars.Length];
            OscillatorCore.EhlersCorrelationTrendIndicator(bars.Select(b => b.Close).ToArray(), core, period);
            for (var i = 0; i < bars.Length; i++) Close(trend.OutputValues["Ecti"][i], core[i]);
            if (period > 1 && bars[0].Close == 1e12)
                for (var i = period - 1; i < bars.Length; i++) Close(1, core[i]);
        }
    }

    [Fact]
    public async Task RainbowBandsAndFireflyRollingMaximumMatchAcrossRoutes()
    {
        var bars = Market(90);
        foreach (var period in new[] { 1, 2, 10 })
        {
            await CompareRoutes(new RainbowOscillator(period), Stock(bars).CalculateRainbowOscillator(length1: period),
                new RainbowOscillatorState(length1: period), bars);
            var firefly = Stock(bars).CalculateFireflyOscillator(length: period);
            for (var i = 0; i < bars.Length; i++)
                Close(firefly.OutputValues["Fo"].Skip(Math.Max(0, i - 2)).Take(Math.Min(i + 1, 3)).Max(), firefly.OutputValues["Signal"][i]);
            await CompareRoutes(new FireflyOscillator(period), firefly, new FireflyOscillatorState(length: period), bars);
        }
    }

    [Fact]
    public async Task PhaseIsScaleAndTranslationInvariantAfterACompleteWindow()
    {
        Bar[] Wave(double scale, double offset) => Enumerable.Range(0, 20).Select(i =>
        {
            var v = offset + scale * (i % 4 == 0 ? 1 : i % 4 == 2 ? -1 : 0);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1);
        }).ToArray();
        foreach (var bars in new[] { Wave(1, 0), Wave(1e-8, 0), Wave(1, 1000), Wave(0, 1000) })
        {
            var batch = Stock(bars).CalculateEhlersPhaseCalculation(MovingAvgType.SimpleMovingAverage, 4);
            for (var i = 3; i < bars.Length; i++)
                Close(bars.All(b => b.Close == bars[0].Close) ? 90 : (90 + 90 * i) % 360, batch.OutputValues["Phase"][i]);
            await CompareRoutes(new EhlersPhaseCalculation(4, new Sma()), batch,
                new EhlersPhaseCalculationState(MovingAvgType.SimpleMovingAverage, 4), bars);
        }
        var shortPeriod = Wave(1, 0);
        await CompareRoutes(new EhlersPhaseCalculation(1), Stock(shortPeriod).CalculateEhlersPhaseCalculation(length: 1),
            new EhlersPhaseCalculationState(length: 1), shortPeriod);
    }

    [Fact]
    public async Task EhlersTrendAndTradingFiltersPublishTheirDistinctBandAndRmsOutputs()
    {
        var bars = Market(90);
        await CompareRoutes(new EhlersTrendExtraction(4), Stock(bars).CalculateEhlersTrendExtraction(length: 4),
            new EhlersTrendExtractionState(length: 4), bars);
        await CompareRoutes(new EhlersUniversalTradingFilter(4, 5), Stock(bars).CalculateEhlersUniversalTradingFilter(length1: 4, length2: 5),
            new EhlersUniversalTradingFilterState(length1: 4, length2: 5), bars);
        await CompareRoutes(new EhlersSnakeUniversalTradingFilter(4, 5), Stock(bars).CalculateEhlersSnakeUniversalTradingFilter(length1: 4, length2: 5),
            new EhlersSnakeUniversalTradingFilterState(length1: 4, length2: 5), bars);
    }

    [Fact]
    public async Task UniversalOscillatorStaysNormalizedAndEhlersSecondaryOutputsMatchTheirFormulas()
    {
        var bars = Enumerable.Range(0, 400).Select(i =>
        {
            var v = 100 + Math.Sin(i * .025);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1);
        }).ToArray();
        var universal = Stock(bars).CalculateEhlersUniversalOscillator(length: 20);
        Assert.All(universal.OutputValues["Euo"], value => Assert.InRange(value, -1, 1));
        await CompareRoutes(new EhlersUniversalOscillator(20), universal, new EhlersUniversalOscillatorState(length: 20), bars);
        var market = Market(80);
        await CompareRoutes(new EhlersSuperPassbandFilter(2, 4, 1, 3),
            Stock(market).CalculateEhlersSuperPassbandFilter(2, 4, 1, 3), new EhlersSuperPassbandFilterState(2, 4, 1, 3), market);
        await CompareRoutes(new EhlersTripleDelayLineDetrender(3), Stock(market).CalculateEhlersTripleDelayLineDetrender(length: 3),
            new EhlersTripleDelayLineDetrenderState(length: 3), market);
    }

    [Fact]
    public async Task KlingerUsesWholeBarTrendAndAccumulatedRangeAcrossRoutesAndCore()
    {
        var bars = Enumerable.Range(0, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 12 + i, 8 + i, 10, 1)).ToArray();
        var batch = Stock(bars).CalculateKlingerVolumeOscillator(fastLength: 1, slowLength: 2, signalLength: 2);
        Close(100d / 9, batch.OutputValues["Kvo"][2]);
        await CompareRoutes(new KlingerVolumeOscillator(1, 2, 2), batch,
            new KlingerVolumeOscillatorState(fastLength: 1, slowLength: 2, signalLength: 2), bars);
        foreach (var fixture in new[] { bars, Market(80), Market(80, 100) })
        {
            var expected = Stock(fixture).CalculateKlingerVolumeOscillator(fastLength: 1, slowLength: 2, signalLength: 2);
            var actual = new double[fixture.Length];
            VolumeCore.KlingerVolumeOscillator(fixture.Select(b => b.High).ToArray(), fixture.Select(b => b.Low).ToArray(),
                fixture.Select(b => b.Close).ToArray(), fixture.Select(b => b.Volume).ToArray(), actual, 1, 2);
            for (var i = 0; i < fixture.Length; i++) Close(expected.OutputValues["Kvo"][i], actual[i]);
        }
        await CompareRoutes(new Kvo(1), Stock(bars).CalculateKlingerVolumeOscillator(fastLength: 1),
            new KlingerVolumeOscillatorState(fastLength: 1), bars);
    }

    [Fact]
    public async Task VolumeDisparityIsOneForProportionalPriceAndVolumeIndices()
    {
        var prices = new[] { 1d, 2, 4 };
        var positive = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, i == 2 ? 2 : 1)).ToArray();
        var fallingVolume = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 3 - i)).ToArray();
        var obv = Stock(positive).CalculateOnBalanceVolumeDisparityIndicator(length: 2, signalLength: 2);
        var nvi = Stock(fallingVolume).CalculateNegativeVolumeDisparityIndicator(length: 2, signalLength: 2);
        Assert.All(obv.OutputValues["Obvdi"], value => Close(1, value));
        Assert.All(nvi.OutputValues["Nvdi"], value => Close(1, value));
        await CompareRoutes(new OnBalanceVolumeDisparityIndicator(2, 2), obv,
            new OnBalanceVolumeDisparityIndicatorState(length: 2, signalLength: 2), positive);
        await CompareRoutes(new NegativeVolumeDisparityIndicator(2, 2), nvi,
            new NegativeVolumeDisparityIndicatorState(length: 2, signalLength: 2), fallingVolume);
    }

    [Fact]
    public async Task VolumeFlowSuppressesSubthresholdMovesAndTradeSignalUsesItsPeriod()
    {
        var bars = new[] { 100d, 200, 201, 200 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 10)).ToArray();
        foreach (var cutoff in new[] { 0d, 100d })
        {
            var batch = Stock(bars).CalculateVolumeFlowIndicator(length1: 2, length2: 2, signalLength: 1, smoothLength: 1, coef: cutoff);
            Close(cutoff == 0 ? 1 : 0, batch.OutputValues["Vfi"][2]);
            await CompareRoutes(new VolumeFlowIndicator(2, 2, 1, 1, cutoff), batch,
                new VolumeFlowIndicatorState(length1: 2, length2: 2, signalLength: 1, smoothLength: 1, coef: cutoff), bars);
        }
        var ticks = new[] { 1d, 1.5, 2.25, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 10)).ToArray();
        var trade = Stock(ticks).CalculateTradeVolumeIndex(length: 2);
        Close(20, trade.OutputValues["Tvi"][2]);
        Close(15, trade.OutputValues["Signal"][2]);
        await CompareRoutes(new TradeVolumeIndex(2), trade, new TradeVolumeIndexState(length: 2), ticks);
    }

    [Fact]
    public async Task VpciSignalAndVolumeAdaptiveOuterBandsAreDistinctAndCorrectAcrossRoutes()
    {
        var bars = Market(80);
        var confirmation = Stock(bars).CalculateVolumePriceConfirmationIndicator(length: 3);
        Assert.True(confirmation.OutputValues["Vpci"].Zip(confirmation.OutputValues["Signal"], (a, b) => Math.Abs(a - b)).Max() > 1e-6);
        await CompareRoutes(new Vpci(3), confirmation, new VolumePriceConfirmationIndicatorState(length: 3), bars);
        var bands = new VolumeAdaptiveBands(3);
        Assert.Equal(3, bands.Outputs.Count);
        await CompareRoutes(bands, Stock(bars).CalculateVolumeAdaptiveBands(length: 3), new VolumeAdaptiveBandsState(length: 3), bars);
    }

    [Fact]
    public async Task MinimumExtremaWindowsAgreeAcrossNativeAndBatchRoutes()
    {
        var bars = new[] { 80d, 120, 80, 120, 80 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        await CompareRoutes(new TrendForceHistogram(1), Stock(bars).CalculateTrendForceHistogram(1), new TrendForceHistogramState(1), bars);
        await CompareRoutes(new WellesWilderVolatilitySystem(1, 1, 1),
            Stock(bars).CalculateWellesWilderVolatilitySystem(length1: 1, length2: 1, factor: 1),
            new WellesWilderVolatilitySystemState(length1: 1, length2: 1, factor: 1), bars);
    }

    [Fact]
    public async Task TrendForceUsesTheActualObservationCountAndExplosionWidthSurvivesLargePrices()
    {
        var ramp = new[] { 1d, 2, 4, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var force = Stock(ramp).CalculateTrendForceHistogram(2);
        double[] expected = [0, .25, .5, .75];
        for (var i = 0; i < expected.Length; i++) Close(expected[i], force.OutputValues["Tfh"][i]);
        await CompareRoutes(new TrendForceHistogram(2), force, new TrendForceHistogramState(2), ramp);
        var large = Enumerable.Range(0, 60).Select(i =>
        {
            var v = 1e12 + i % 2;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1);
        }).ToArray();
        var explosion = Stock(large).CalculateWaddahAttarExplosion(2, 4);
        for (var i = 1; i < large.Length; i++) Close(2, explosion.OutputValues["E1"][i]);
        await CompareRoutes(new WaddahAttarExplosion(2, 4), explosion, new WaddahAttarExplosionState(2, 4), large);
    }

    [Fact]
    public async Task DEnvelopeRetainsItsLimitWithoutInvertedBandsAndReversalKeepsBearishState()
    {
        var bars = Enumerable.Range(0, 80).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, i == 20 ? 100 : 1, 1, i == 20 ? 100 : 1, 100)).ToArray();
        foreach (var length in new[] { 1, 2, 20 })
        {
            var envelope = Stock(bars).CalculateDEnvelope(length);
            for (var i = 0; i < bars.Length; i++)
                Assert.True(envelope.OutputValues["UpperBand"][i] >= envelope.OutputValues["LowerBand"][i]);
            await CompareRoutes(new DEnvelope(length), envelope, new DEnvelopeState(length), bars);
        }
        var falling = new[] { 100d, 90, 89, 88, 90 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 100)).ToArray();
        var reversal = Stock(falling).CalculateNickRypockTrailingReverse(2);
        Close(89.76, reversal.OutputValues["Nrtr"][3]);
        await CompareRoutes(new NickRypockTrailingReverse(2), reversal, new NickRypockTrailingReverseState(2), falling);
    }

    [Fact]
    public async Task MotionAttractionCannotOvershootAndLinearStopKeepsItsSideInsideBands()
    {
        var sequence = new[] { 1d, 2, 4, 1.75, .1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 100)).ToArray();
        var linear = Stock(sequence).CalculateLinearTrailingStop(2, 1);
        Close(1.5, linear.OutputValues["Ts"][3]);
        Close(2, linear.OutputValues["Ts"][4]);
        await CompareRoutes(new LinearTrailingStop(2, 1), linear, new LinearTrailingStopState(2, 1), sequence);
        foreach (var direction in new[] { -1d, 1d })
        {
            var bars = Enumerable.Range(0, 80).Select(i =>
            {
                var price = 100 + direction * i;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 100);
            }).ToArray();
            var channels = Stock(bars).CalculateMotionToAttractionChannels(2);
            for (var i = 0; i < bars.Length; i++)
                Assert.True(channels.OutputValues["UpperBand"][i] >= channels.OutputValues["LowerBand"][i]);
            await CompareRoutes(new MotionToAttractionChannels(2), channels, new MotionToAttractionChannelsState(2), bars);
            var stop = Stock(bars).CalculateMotionToAttractionTrailingStop(2);
            await CompareRoutes(new MotionToAttractionTrailingStop(2), stop, new MotionToAttractionTrailingStopState(2), bars);
        }
    }

    [Fact]
    public async Task PercentageStopDistanceIsExpressedInPercentagePoints()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 100, 110, 90, 100, 100) };
        var batch = Stock(bars).CalculatePercentageTrailingStops(1, 10);
        Close(99, batch.OutputValues["LongStop"][0]);
        Close(99, batch.OutputValues["ShortStop"][0]);
        await CompareRoutes(new PercentageTrailingStops(1, 10), batch, new PercentageTrailingStopsState(1, 10), bars);
    }

    [Fact]
    public async Task VolumeLocationRetainsSellingPressureAndBothPriceVolumeLegsArePublished()
    {
        var bars = Enumerable.Range(0, 80).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 1, 100 + i)).ToArray();
        var accumulation = Stock(bars).CalculateVolumeAccumulationPercent(2);
        Assert.All(accumulation.OutputValues["Vapc"], value => Close(-100, value));
        await CompareRoutes(new VolumeAccumulationPercent(2), accumulation, new VolumeAccumulationPercentState(2), bars);
        var legs = Stock(bars).CalculatePriceVolumeOscillator(length1: 2);
        Assert.Equal(1, legs.OutputValues["Vo"][14]);
        Assert.Equal(0, legs.OutputValues["Po"][14]);
        await CompareRoutes(new PriceVolumeOscillator(2), legs, new PriceVolumeOscillatorState(2, 14), bars);
        var hawkeye = Stock(bars).CalculateHawkeyeVolumeIndicator();
        await CompareRoutes(new HawkeyeVolumeIndicator(), hawkeye, new HawkeyeVolumeIndicatorState(), bars);
    }

    [Fact]
    public async Task OvershootCorrectionUsesAStableRegressionSlope()
    {
        foreach (var direction in new[] { -1d, 1d })
        {
            var bars = Enumerable.Range(0, 300).Select(i =>
            {
                var price = 100 + direction * .1 * i;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 100);
            }).ToArray();
            var batch = Stock(bars).CalculateOvershootReductionMovingAverage(length: 14);
            Assert.Single(BuiltInFormulaReferences.For(new OvershootReductionMovingAverage(14))).Check(
                new IndicatorValidationContext("linear-price-regression", bars, new[] { batch.OutputValues["Orma"].ToArray() }, 0));
            await CompareRoutes(new OvershootReductionMovingAverage(14), batch, new OvershootReductionMovingAverageState(length: 14), bars);
            var rule = Assert.Single(BuiltInFormulaReferences.For(new OvershootReductionMovingAverage(14)));
            foreach (var index in new[] { 0, 20, 149 })
            {
                var corrupted = batch.OutputValues["Orma"].ToArray();
                corrupted[index] += 1;
                Assert.Throws<InvalidOperationException>(() => rule.Check(
                    new IndicatorValidationContext("corrupted-feedback-step", bars, new[] { corrupted }, 0)));
            }
        }
    }

    [Fact]
    public async Task ExtendedRecursiveBandsCannotInvertForSmallPeriods()
    {
        var bars = Market(80);
        foreach (var length in new[] { 1, 2, 3, 5 })
        {
            var batch = Stock(bars).CalculateExtendedRecursiveBands(length);
            for (var i = 0; i < bars.Length; i++)
                Assert.True(batch.OutputValues["UpperBand"][i] >= batch.OutputValues["LowerBand"][i]);
            await CompareRoutes(new ExtendedRecursiveBands(length), batch, new ExtendedRecursiveBandsState(length), bars);
        }
    }

    [Fact]
    public async Task DecyclerPublishesBothPeriodsAndPreservesItsFastPrimary()
    {
        var bars = Market(150);
        var indicator = new EhlersDecyclerOscillatorV1(10);
        Assert.Equal(2, indicator.Outputs.Count);
        var batch = Stock(bars).CalculateEhlersDecyclerOscillatorV1(fastLength: 10, slowLength: 20);
        await CompareRoutes(indicator, batch, new EhlersDecyclerOscillatorV1State(10, 20), bars);
    }

    [Fact]
    public async Task EdgePreservingRegressionPlateausDoNotCauseSpuriousResets()
    {
        foreach (var alternating in new[] { false, true })
        {
            var bars = Enumerable.Range(0, 300).Select(i =>
            {
                var price = alternating ? i % 2 == 0 ? 80d : 120d : 50 + .3 * i;
                return new Bar(new DateTime(2021, 1, 4).AddDays(i), price, price, price, price, 100);
            }).ToArray();
            var batch = Stock(bars).CalculateEdgePreservingFilter(length: 100);
            Assert.Single(BuiltInFormulaReferences.For(new EdgePreservingFilter(100))).Check(
                new IndicatorValidationContext("regression-plateau", bars, new[] { batch.OutputValues["Epf"].ToArray() }, 0));
            await CompareRoutes(new EdgePreservingFilter(100), batch, new EdgePreservingFilterState(length: 100), bars);
        }
    }

    [Fact]
    public async Task GChannelsCannotInvertAtTheMinimumPeriod()
    {
        foreach (var price in new[] { -100d, 100d })
        {
            var bars = Market(60, price);
            var batch = Stock(bars).CalculateGChannels(1);
            for (var i = 0; i < bars.Length; i++)
                Assert.True(batch.OutputValues["UpperBand"][i] >= batch.OutputValues["LowerBand"][i]);
            await CompareRoutes(new GChannels(1), batch, new GChannelsState(1), bars);
        }
    }

    [Fact]
    public async Task MinimumPeriodsAgreeAcrossBatchBuilderAndStreaming()
    {
        var bars = Market(70);
        var cases = new (IIndicator Indicator, StockData Batch, IStreamingIndicatorState State)[]
        {
            (new ChoppinessIndex(1), Stock(bars).CalculateChoppinessIndex(length: 1), new ChoppinessIndexState(1)),
            (new McNichollMovingAverage(1), Stock(bars).CalculateMcNichollMovingAverage(length: 1), new McNichollMovingAverageState(length: 1)),
            (new HullMovingAverage(1), Stock(bars).CalculateHullMovingAverage(length: 1), new HullMovingAverageState(length: 1)),
            (new SlowSmoothedMovingAverage(1), Stock(bars).CalculateSlowSmoothedMovingAverage(length: 1), new SlowSmoothedMovingAverageState(length: 1)),
            (new ZeroLowLagMovingAverage(1), Stock(bars).CalculateZeroLowLagMovingAverage(length: 1), new ZeroLowLagMovingAverageState(1)),
            (new FallingRisingFilter(1), Stock(bars).CalculateFallingRisingFilter(1), new FallingRisingFilterState(1)),
            (new ShapeshiftingMovingAverage(1), Stock(bars).CalculateShapeshiftingMovingAverage(1), new ShapeshiftingMovingAverageState(1)),
            (new NarrowBandpassFilter(1), Stock(bars).CalculateNarrowBandpassFilter(1), new NarrowBandpassFilterState(1)),
            (new ParametricKalmanFilter(1), Stock(bars).CalculateParametricKalmanFilter(1), new ParametricKalmanFilterState(1)),
            (new ReverseMovingAverageConvergenceDivergence(1, 1), Stock(bars).CalculateReverseMovingAverageConvergenceDivergence(fastLength: 1, slowLength: 1), new ReverseMovingAverageConvergenceDivergenceState(fastLength: 1, slowLength: 1))
        };
        try
        {
            foreach (var item in cases)
                await CompareRoutes(item.Indicator, item.Batch, item.State, bars);
        }
        finally
        {
            foreach (var item in cases) (item.State as IDisposable)?.Dispose();
        }
        var input = bars.Select(b => b.Close).ToArray();
        var result = new double[input.Length];
        MovingAverageCore.SimpleMovingAverage(input, result, 1);
        Assert.Equal(input, result);
        Assert.Equal(2, new OoplesFinance.StockIndicators.Builder.Specs.ChoppinessIndexSpecOptions(1).Length);
        Assert.Equal(2, new OoplesFinance.StockIndicators.Builder.Specs.McNichollMovingAverageSpecOptions(1).Length);
    }

    [Theory]
    [InlineData("Force")]
    [InlineData("RelativeVolatility")]
    [InlineData("WilliamsR")]
    [InlineData("Macd")]
    [InlineData("VolumeMomentum")]
    [InlineData("PriceChange")]
    [InlineData("VolumeZone")]
    [InlineData("CloseVolatility")]
    public void EmptyCoreInputsDoNotWriteEvenWhenOutputHasCapacity(string calculation)
    {
        var empty = Array.Empty<double>();
        var output = new[] { 12345d };
        switch (calculation)
        {
            case "Force": OscillatorCore.ElderForceIndex(empty, empty, output); break;
            case "RelativeVolatility": OscillatorCore.RelativeVolatilityIndex(empty, output); break;
            case "WilliamsR": OscillatorCore.SmoothedWilliamsR(empty, empty, empty, output); break;
            case "Macd": OscillatorCore.NormalizedMacd(empty, output); break;
            case "VolumeMomentum": OscillatorCore.VolumeMomentumOscillator(empty, output); break;
            case "PriceChange": OscillatorCore.PriceChange(empty, output); break;
            case "VolumeZone": VolumeCore.VolumeZoneOscillator(empty, empty, output); break;
            case "CloseVolatility": VolatilityCore.CloseToCloseVolatility(empty, output); break;
        }
        Assert.Equal(12345d, output[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task FractalCenterCannotRequireFutureBars(int length)
    {
        var bars = Market(150);
        var batch = Stock(bars).CalculateWilliamsFractals(length: 2);
        Stock(bars).CalculateWilliamsFractals(length).OutputValues["UpFractal"].Should().Equal(batch.OutputValues["UpFractal"]);
        using var state = new WilliamsFractalsState(length);
        await CompareRoutes(new WilliamsFractals(length), batch, state, bars);
        foreach (var indicator in new IIndicator[] { new WilliamsFractalUp(length), new WilliamsFractalsUp(length),
            new WilliamsFractalDown(length), new WilliamsFractalsDown(length) })
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).BuildAsync();
            var key = indicator.GetType().Name.EndsWith("Down", StringComparison.Ordinal) ? "DnFractal" : "UpFractal";
            run[indicator].ToArray().Should().Equal(batch.OutputValues[key]);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task MesaPredictorTreatsUnavailableAutoregressionHistoryAsZero(int length)
    {
        var bars = Market(150);
        var batch = Stock(bars).CalculateEhlersMesaPredictIndicatorV2(length1: length);
        using var state = new EhlersMesaPredictIndicatorV2State(length1: length);
        await CompareRoutes(new EhlersMesaPredictIndicatorV2(length1: length), batch, state, bars);
        Assert.Equal(0, batch.OutputValues["Predict"][0]);
    }

    [Theory]
    [InlineData(50d)]
    [InlineData(100d)]
    public async Task VidyaSeedsFromTheObservedPriceWhenDispersionIsZero(double price)
    {
        var bars = Market(150, price);
        var batch = Stock(bars).CalculateEhlersVariableIndexDynamicAverage();
        batch.CustomValuesList.Should().OnlyContain(v => v == price);
        using var state = new EhlersVariableIndexDynamicAverageState();
        await CompareRoutes(new EhlersVariableIndexDynamicAverage(14), batch, state, bars);
    }

    [Fact]
    public async Task VidyaStillAgreesOnAMovingMarketIncludingPreviewAndReset()
    {
        var bars = Market(150);
        using var state = new EhlersVariableIndexDynamicAverageState();
        await CompareRoutes(new EhlersVariableIndexDynamicAverage(14),
            Stock(bars).CalculateEhlersVariableIndexDynamicAverage(), state, bars);
    }

    [Theory]
    [InlineData(50d)]
    [InlineData(100d)]
    public async Task EveryChebyshevWaveHasUnitGainAtConstantInput(double price)
    {
        var bars = Market(4000, price);
        var batch = Stock(bars).CalculateEhlersChebyshevLowPassFilter();
        foreach (var output in batch.OutputValues.Values)
            Assert.All(output.Skip(3000), value => Assert.True(Math.Abs(value - price) < 1e-6));
        var state = new EhlersChebyshevLowPassFilterState();
        await CompareRoutes(new EhlersChebyshevLowPassFilter(14), batch, state, bars);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandardDeviationVolatilityUsesTheRequestedAverageForAllThreeStages(bool weighted)
    {
        const int length = 7;
        var bars = Market(150);
        var maType = weighted ? MovingAvgType.WeightedMovingAverage : MovingAvgType.SimpleMovingAverage;
        var batch = Stock(bars).CalculateStandardDeviationVolatility(maType, length);
        var residuals = new List<double>();
        var deviations = new List<double>();
        var prices = bars.Select(b => b.Close).ToArray();
        for (var i = 0; i < bars.Length; i++)
        {
            var residual = prices[i] - Average(prices, i);
            residuals.Add(residual * residual);
            var variance = Average(residuals, i);
            deviations.Add(Math.Sqrt(variance));
            Close(variance, batch.OutputValues["Variance"][i]);
            Close(deviations[i], batch.OutputValues["StdDev"][i]);
            Close(Average(deviations, i), batch.OutputValues["Signal"][i]);
        }
        using var state = new StandardDeviationVolatilityState(maType, length);
        await CompareRoutes(new StandardDeviationVolatility(length, 252, weighted ? new Wma(length) : null), batch, state, bars);

        double Average(IReadOnlyList<double> values, int end)
        {
            // The library publishes zero until SMA has a full window. WMA uses a
            // full triangular denominator with unavailable leading samples equal to zero.
            if (!weighted && end < length - 1) return 0;
            var start = Math.Max(0, end - length + 1);
            double sum = 0;
            for (var j = start; j <= end; j++)
            {
                var weight = weighted ? length - (end - j) : 1;
                sum += values[j] * weight;
            }
            return sum / (weighted ? length * (length + 1) / 2d : length);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ElasticVolumeAveragesHoldThroughNoVolumeAndResumeFromTheirLastValue(bool secondVariant)
    {
        var prices = new[] { 10d, 20, 30, 40, 50 };
        var volumes = new[] { 0d, 2, 0, 0, 4 };
        var expected = secondVariant ? new[] { 10d, 20, 20, 20, 50 }
            : new[] { 10d, 11, 11, 11, 14.9 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, volumes[i])).ToArray();
        var batch = secondVariant ? Stock(bars).CalculateElasticVolumeWeightedMovingAverageV2(2)
            : Stock(bars).CalculateElasticVolumeWeightedMovingAverageV1(length: 2);
        for (var i = 0; i < expected.Length; i++) Close(expected[i], batch.OutputValues["Evwma"][i]);
        IIndicator indicator = secondVariant ? new ElasticVolumeWeightedMovingAverageV2(2)
            : new ElasticVolumeWeightedMovingAverageV1(2);
        using var state = secondVariant ? (IDisposable)new ElasticVolumeWeightedMovingAverageV2State(2)
            : new ElasticVolumeWeightedMovingAverageV1State(length: 2);
        await CompareRoutes(indicator, batch, (IStreamingIndicatorState)state, bars);
        var output = new double[prices.Length];
        if (secondVariant) MovingAverageCore.ElasticVolumeWeightedMovingAverageV2(prices, volumes, output, 2);
        else MovingAverageCore.ElasticVolumeWeightedMovingAverageV1(prices, volumes, output, 2);
        for (var i = 0; i < expected.Length; i++) Close(expected[i], output[i]);
        var untouched = new[] { 12345d };
        if (secondVariant) MovingAverageCore.ElasticVolumeWeightedMovingAverageV2(Array.Empty<double>(), Array.Empty<double>(), untouched, 2);
        else MovingAverageCore.ElasticVolumeWeightedMovingAverageV1(Array.Empty<double>(), Array.Empty<double>(), untouched, 2);
        Assert.Equal(12345d, untouched[0]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DoubleSmoothedRsiPreservesStrengthAboveFiftyAndPriceReflection(int rangeLength)
    {
        var prices = new[] { 10d, 13, 12, 15, 14, 17 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var reflected = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 40 - v, 40 - v, 40 - v, 40 - v, 1)).ToArray();
        var batch = Stock(bars).CalculateDoubleSmoothedRelativeStrengthIndex(length1: rangeLength);
        var opposite = Stock(reflected).CalculateDoubleSmoothedRelativeStrengthIndex(length1: rangeLength);
        for (var i = 1; i < bars.Length; i++) Close(100, batch.OutputValues["Dsrsi"][i] + opposite.OutputValues["Dsrsi"][i]);
        Assert.InRange(batch.OutputValues["Dsrsi"].Last(), 50.001, 99.999);
        using var state = new DoubleSmoothedRelativeStrengthIndexState(length1: rangeLength);
        await CompareRoutes(new DoubleSmoothedRelativeStrengthIndex(2), batch, state, bars);
        var core = new double[bars.Length];
        OscillatorCore.DoubleSmoothedRelativeStrengthIndex(prices, core, length1: rangeLength);
        for (var i = 0; i < core.Length; i++) Close(batch.OutputValues["Dsrsi"][i], core[i]);
    }

    [Theory]
    [InlineData(.01)]
    [InlineData(100)]
    public async Task DynamicMomentumPeriodsDoNotDependOnPriceUnits(double scale)
    {
        var bars = Market(180);
        var scaled = bars.Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, b.Volume)).ToArray();
        var batch = Stock(bars).CalculateDynamicMomentumIndex();
        var other = Stock(scaled).CalculateDynamicMomentumIndex();
        foreach (var key in batch.OutputValues.Keys)
            for (var i = 0; i < bars.Length; i++) Close(batch.OutputValues[key][i], other.OutputValues[key][i]);
        using var state = new DynamicMomentumIndexState();
        await CompareRoutes(new DynamicMomentumIndex(14), other, state, scaled);
        var core = new double[bars.Length];
        OscillatorCore.DynamicMomentumIndex(scaled.Select(b => b.Close).ToArray(), core);
        for (var i = 0; i < core.Length; i++) Close(other.OutputValues["Dmi"][i], core[i]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(800)]
    public async Task HighPassV2CoreUsesThePublishedTwoPoleFilterAndDoubleSmoothing(int length)
    {
        var bars = Market(180);
        var batch = Stock(bars).CalculateEhlersHighPassFilterV2(length: length);
        var core = new double[bars.Length];
        MovingAverageCore.EhlersHighPassFilterV2(bars.Select(b => b.Close).ToArray(), core, length);
        for (var i = 0; i < core.Length; i++) Close(batch.OutputValues["Ehpf"][i], core[i]);
        using var state = new EhlersHighPassFilterV2State(length: length);
        await CompareRoutes(new EhlersHighPassFilterV2(length), batch, state, bars);
        MovingAverageCore.EhlersHighPassFilterV2(Array.Empty<double>(), Array.Empty<double>(), length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(50)]
    public async Task DampedSineRetainsItsNegativeLobeAndNormalizesDegeneratePeriods(int length)
    {
        var bars = Market(120);
        var batch = Stock(bars).CalculateDampedSineWaveWeightedFilter(length);
        var core = new double[bars.Length];
        MovingAverageCore.DampedSineWaveWeightedFilter(bars.Select(b => b.Close).ToArray(), core, length);
        for (var i = 0; i < core.Length; i++) Close(batch.OutputValues["Dswwf"][i], core[i]);
        using var state = new DampedSineWaveWeightedFilterState(length);
        await CompareRoutes(new DampedSineWaveWeightedFilter(length), batch, state, bars);
        var indicator = new DampedSineWaveWeightedFilter(length);
        await IndicatorValidation.ValidateAndThrowAsync(new IndicatorValidationCase(indicator.GetType(),
            "sinc-" + length, () => new DampedSineWaveWeightedFilter(length)), new() { RequireFormulaReference = true });
        var impulse = new double[60];
        impulse[0] = 1;
        var response = new double[60];
        MovingAverageCore.DampedSineWaveWeightedFilter(impulse, response, length);
        Assert.Contains(response, value => value < -1e-6);
        Close(1, response.Sum());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public async Task ConnorsRanksThePriorWindowStrictlyAcrossEveryRoute(int rankLength)
    {
        var bars = Market(160);
        var batch = Stock(bars).CalculateConnorsRelativeStrengthIndex(length3: rankLength);
        using var state = new ConnorsRelativeStrengthIndexState(length3: rankLength);
        await CompareRoutes(new ConnorsRelativeStrengthIndex(length3: rankLength), batch, state, bars);
        var prices = bars.Select(b => b.Close).ToArray();
        var core = new double[bars.Length];
        var alias = new double[bars.Length];
        OscillatorCore.ConnorsRsi(prices, core, rocLength: rankLength);
        OscillatorCore.ConnorsRelativeStrengthIndex(prices, alias, rankLength: rankLength);
        for (var i = 0; i < core.Length; i++)
        {
            Close(batch.OutputValues["ConnorsRsi"][i], core[i]);
            Close(core[i], alias[i]);
        }
        OscillatorCore.ConnorsRsi(Array.Empty<double>(), Array.Empty<double>(), rocLength: rankLength);
    }

    [Fact]
    public void ConnorsTiesRankZeroAndTheOldestPriorObservationStillCounts()
    {
        var prices = new[] { 100d, 100, 100, 200, 200, 400 };
        var bars = prices.Select((price, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1)).ToArray();
        var rank = Stock(bars).CalculateConnorsRelativeStrengthIndex(length3: 3).OutputValues["PctRank"];
        var expected = new[] { 0d, 0, 0, 100, 0, 200d / 3 };
        for (var i = 0; i < expected.Length; i++) Close(expected[i], rank[i]);
        var turn = new[] { 100d, 110, 100 }.Select((price, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1)).ToArray();
        Close(20, Stock(turn).CalculateConnorsRelativeStrengthIndex().OutputValues["StreakRsi"][2]);
    }

    [Fact]
    public async Task StochasticConnorsUsesOnlyItsOscillatorRange()
    {
        var bars = Market(180);
        var batch = Stock(bars).CalculateStochasticConnorsRelativeStrengthIndex();
        using var state = new StochasticConnorsRelativeStrengthIndexState();
        await CompareRoutes(new StochasticConnorsRelativeStrengthIndex(), batch, state, bars);
        // Alter candle ranges without changing closes: an oscillator-of-oscillator cannot depend on them.
        var wider = bars.Select(b => new Bar(b.Time, b.Open, 1000, 0, b.Close, b.Volume)).ToArray();
        var other = Stock(wider).CalculateStochasticConnorsRelativeStrengthIndex();
        foreach (var key in batch.OutputValues.Keys)
            for (var i = 0; i < bars.Length; i++) Close(batch.OutputValues[key][i], other.OutputValues[key][i]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(13)]
    public async Task DtOscillatorRangesRsiAndAgreesDuringStartup(int length)
    {
        var bars = Market(160);
        var batch = Stock(bars).CalculateDTOscillator(length1: length);
        using var state = new DTOscillatorState(length1: length);
        await CompareRoutes(new DTOscillator(length), batch, state, bars);
        var core = new double[bars.Length];
        OscillatorCore.DTOscillator(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray(), core, rsiLength: length);
        for (var i = 0; i < core.Length; i++) Close(batch.OutputValues["Dto"][i], core[i]);
        var rising = Enumerable.Range(0, 80).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 100 + i, 101 + i, 99 + i, 100 + i, 1)).ToArray();
        // With no losses RSI is constantly 100, so its stochastic range is zero.
        var oneDirection = Stock(rising).CalculateDTOscillator(length1: length);
        Assert.All(oneDirection.OutputValues.Values.SelectMany(values => values), value => Close(0, value));
    }

    [Fact]
    public async Task MidpointVolumeContributionIsZeroAndFlatBarStrengthMatchesItsContract()
    {
        var bars = Market(40); // Symmetric high/low about each close.
        var batch = Stock(bars).CalculateVolumeAccumulationOscillator();
        Assert.All(batch.OutputValues["Vao"], value => Close(0, value));
        using var state = new VolumeAccumulationOscillatorState();
        await CompareRoutes(new VolumeAccumulationOscillator(14), batch, state, bars);
        var core = new double[bars.Length];
        OscillatorCore.VolumeAccumulationOscillator(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), core);
        Assert.All(core, value => Close(0, value));
        var flat = Market(40, 100);
        var flatBatch = Stock(flat).CalculateInternalBarStrengthIndicator();
        using var flatState = new InternalBarStrengthIndicatorState();
        await CompareRoutes(new InternalBarStrengthIndicator(14), flatBatch, flatState, flat);
        Assert.All(flatBatch.OutputValues.Values.SelectMany(values => values), value => Close(0, value));
    }

    [Fact]
    public async Task MeannessPreservesFractionalPercentagesAndOneBarVhfHasNoRange()
    {
        var bars = Market(150);
        var batch = Stock(bars).CalculateMarketMeannessIndex(length: 4);
        Assert.Contains(batch.OutputValues["Mmi"], value => Math.Abs(value - Math.Round(value)) > .1);
        using var state = new MarketMeannessIndexState(length: 4);
        await CompareRoutes(new MarketMeannessIndex(4), batch, state, bars);
        var vhf = Stock(bars).CalculateVerticalHorizontalFilter(length: 1);
        Assert.All(vhf.OutputValues.Values.SelectMany(values => values), value => Close(0, value));
        using var vhfState = new VerticalHorizontalFilterState(length: 1);
        await CompareRoutes(new VerticalHorizontalFilter(1), vhf, vhfState, bars);
    }

    [Theory]
    [InlineData(new double[] { 10, 9, 8 }, 0)]
    [InlineData(new double[] { 8, 8, 10, 10, 9, 11 }, 0)]
    [InlineData(new double[] { 8, 8, 10, 10, 9, 8 }, 1)]
    [InlineData(new double[] { 12, 8, 10, 9, 10, 9, 10, 9, 8 }, 0)]
    [InlineData(new double[] { 8, 8, 10, 9, 10, 9, 10, 9, 8 }, 1)]
    public async Task WilliamsFractalsRequireRealHistoryAndBothConfirmingBars(double[] highs, double expected)
    {
        var bars = highs.Select((high, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), high - 1, high, high - 2, high - 1, 1)).ToArray();
        var batch = Stock(bars).CalculateWilliamsFractals();
        Close(expected, batch.OutputValues["UpFractal"][^1]);
        using var state = new WilliamsFractalsState();
        // These typed aliases each publish one side of the same two-output state.
        var up = new double[bars.Length];
        var down = new double[bars.Length];
        OscillatorCore.WilliamsFractals(highs, highs.Select(value => value - 2).ToArray(), up, down);
        for (var i = 0; i < bars.Length; i++)
        {
            Close(batch.OutputValues["UpFractal"][i], up[i]);
            Close(batch.OutputValues["DnFractal"][i], down[i]);
        }
        await CompareRoutes(new WilliamsFractals(2), batch, state, bars);
        var reflected = bars.Select(b => new Bar(b.Time, 20 - b.Open, 20 - b.Low, 20 - b.High, 20 - b.Close, b.Volume)).ToArray();
        var mirrored = Stock(reflected).CalculateWilliamsFractals();
        for (var i = 0; i < bars.Length; i++) Close(up[i], mirrored.OutputValues["DnFractal"][i]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(1100)]
    public async Task TripleHullAliasesUseTheSameUnclampedDerivedPeriods(int length)
    {
        var bars = Market(160);
        var batch = Stock(bars).Calculate3HMA(length: length);
        var prices = bars.Select(b => b.Close).ToArray();
        var core = new double[bars.Length];
        MovingAverageCore.ThreeHMA(prices, core, length);
        for (var i = 0; i < core.Length; i++) Close(batch.OutputValues["3hma"][i], core[i]);
        using var state = new _3HMAState(length: length);
        await CompareRoutes(new ThreeHMA(length), batch, state, bars);
        using var otherState = new _3HMAState(length: length);
        await CompareRoutes(new TripleHullMovingAverage(length), batch, otherState, bars);
        if (length == 1)
            for (var i = 0; i < core.Length; i++) Close(prices[i], core[i]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(125)]
    [InlineData(1000)]
    public async Task EhlersCutoffTracksPeriodAcrossItsSamplingDomain(int length)
    {
        var bars = Market(160);
        var prices = bars.Select(b => b.Close).ToArray();
        var highPass = Stock(bars).CalculateEhlersHighPassFilterV1(length);
        var lowPass = Stock(bars).CalculateEhlersDecycler(length);
        var highCore = new double[bars.Length];
        var lowCore = new double[bars.Length];
        MovingAverageCore.EhlersHighPassFilterV1(prices, highCore, length);
        MovingAverageCore.EhlersDecycler(prices, lowCore, length);
        for (var i = 0; i < bars.Length; i++)
        {
            Close(highPass.OutputValues["Hp"][i], highCore[i]);
            Close(lowPass.OutputValues["Ed"][i], lowCore[i]);
            if (length == 1) Close(0, highCore[i]);
            if (length <= 2) Close(prices[i], lowCore[i]);
            if (length == 4) Close((prices[i] + (i == 0 ? 0 : prices[i - 1])) / 2, lowCore[i]);
        }
        await CompareRoutes(new EhlersHighPassFilterV1(length), highPass, new EhlersHighPassFilterV1State(length), bars);
        await CompareRoutes(new EhlersDecycler(length), lowPass, new EhlersDecyclerState(length), bars);
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(EhlersHighPassFilterV1), "cutoff",
            () => new EhlersHighPassFilterV1(length)), new() { RequireFormulaReference = true });
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(EhlersDecycler), "cutoff",
            () => new EhlersDecycler(length)), new() { RequireFormulaReference = true });
        // The first high-pass impulse sample must continue approaching unity at long periods.
        if (length == 1000) Assert.True(highCore[0] / prices[0] > .99);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(20)]
    public async Task PolarizedEfficiencyMeasuresTheSameGeometricPathAtEveryPeriod(int length)
    {
        foreach (var slope in new[] { -0.1, 0, 0.1 })
        {
            var bars = Enumerable.Range(0, 60).Select(i =>
            {
                var price = 100 + slope * i;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
            }).ToArray();
            var batch = Stock(bars).CalculatePolarizedFractalEfficiency(length: length, smoothLength: 1);
            var core = new double[bars.Length];
            OscillatorCore.PolarizedFractalEfficiency(bars.Select(b => b.Close).ToArray(), core, length, 1);
            for (var i = 0; i < bars.Length; i++)
            {
                var expected = i < length ? 0 : Math.Sign(slope) * 100;
                Close(expected, batch.OutputValues["Pfe"][i]);
                Close(expected, core[i]);
            }
            using var state = new PolarizedFractalEfficiencyState(length: length, smoothLength: 1);
            await CompareRoutes(new PolarizedFractalEfficiency(length, 1), batch, state, bars);
        }
        var market = Market(160);
        var values = Stock(market).CalculatePolarizedFractalEfficiency(length: length, smoothLength: 1).OutputValues["Pfe"];
        Assert.All(values, value => Assert.InRange(value, -100.000000001, 100.000000001));
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(PolarizedFractalEfficiency), "geometric",
            () => new PolarizedFractalEfficiency(length, 1)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task JrcRangeWindowDoesNotRetainAnExtraOpeningRange(int length)
    {
        var bars = Enumerable.Range(0, 30).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 11, 9, 10, 1)).ToArray();
        var batch = Stock(bars).CalculateJrcFractalDimension(length1: length, length2: 2, smoothLength: 1);
        Close(1, batch.OutputValues["Jrcfd"][0]);
        // After all startup observations expire, both range scales equal two.
        Close(2, batch.OutputValues["Jrcfd"][^1]);
        using var state = new JrcFractalDimensionState(length1: length, length2: 2, smoothLength: 1);
        await CompareRoutes(new JrcFractalDimension(length, 2, 1), batch, state, bars);
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(JrcFractalDimension), "range-window",
            () => new JrcFractalDimension(length, 2, 1)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public async Task SchaffCycleDoesNotAmplifyConstantMacdRoundoff(int cycle)
    {
        var bars = Enumerable.Range(0, 160).Select(i =>
        {
            var price = 100 + .1 * i;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + 1, price - 1, price, 1);
        }).ToArray();
        var batch = Stock(bars).CalculateSchaffTrendCycle(fastLength: 4, slowLength: 7, cycleLength: cycle);
        // Once the arithmetic EMA seeds fill, a linear price trend has a constant MACD.
        Assert.All(batch.OutputValues["Stc"].Skip(7 + cycle), value => Close(0, value));
        using var state = new SchaffTrendCycleState(fastLength: 4, slowLength: 7, cycleLength: cycle);
        await CompareRoutes(new SchaffTrendCycle(cycle, 4, 7), batch, state, bars);
        var varying = Stock(Market(160)).CalculateSchaffTrendCycle(fastLength: 4, slowLength: 7, cycleLength: cycle);
        if (cycle == 1) Assert.All(varying.OutputValues["Stc"], value => Close(0, value));
        else Assert.Contains(varying.OutputValues["Stc"], value => value > 1);
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(SchaffTrendCycle), "resolved-range",
            () => new SchaffTrendCycle(cycle, 4, 7)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task StochasticMomentumOneBarUsesTheCurrentCandleMidpoint()
    {
        var bars = Market(80);
        var batch = Stock(bars).CalculateStochasticMomentumIndex(length1: 1, length2: 4, smoothLength1: 2, smoothLength2: 2);
        Assert.All(batch.OutputValues["Smi"], value => Close(0, value));
        using var state = new StochasticMomentumIndexState(length1: 1, length2: 4, smoothLength1: 2, smoothLength2: 2);
        await CompareRoutes(new StochasticMomentumIndex(1, 4, 2, 2), batch, state, bars);
    }

    [Fact]
    public void ReallySimpleSignalsCompareRawOscillatorWithItsSmoothing()
    {
        var bars = new[] { 10d, 20, 10 }.Select((price, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1)).ToArray();
        var batch = Stock(bars).CalculateReallySimpleIndicator(length: 2, smoothLength: 2);
        // Raw values [0,25,-50/3], signal [0,12.5,-125/18].
        // Their differences change from zero to positive to negative.
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell }, batch.SignalsList);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(100)]
    public async Task WindowedVolumeWeightsDependOnlyOnTheTrailingWindow(int length)
    {
        var bars = Market(220);
        var prefix = Market(37, 900);
        var batch = Stock(bars).CalculateWindowedVolumeWeightedMovingAverage(length);
        var prefixed = Stock(prefix.Concat(bars).ToArray()).CalculateWindowedVolumeWeightedMovingAverage(length);
        var core = new double[bars.Length];
        MovingAverageCore.WindowedVolumeWeightedMovingAverage(bars.Select(b => b.Close).ToArray(),
            bars.Select(b => b.Volume).ToArray(), core, length);
        for (var i = 0; i < bars.Length; i++)
        {
            var value = batch.OutputValues["Wvwma"][i];
            Close(value, core[i]);
            if (i >= length - 1) Close(value, prefixed.OutputValues["Wvwma"][i + prefix.Length]);
            if (length == 1) Close(bars[i].Close, value);
            else if (i > 0)
            {
                var contributing = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i, length - 1));
                Assert.InRange(value, contributing.Min(b => b.Close) - 1e-9, contributing.Max(b => b.Close) + 1e-9);
            }
            else Close(0, value);
        }
        var zeroVolume = bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, 0)).ToArray();
        Assert.All(Stock(zeroVolume).CalculateWindowedVolumeWeightedMovingAverage(length).OutputValues["Wvwma"],
            value => Close(0, value));
        using var state = new WindowedVolumeWeightedMovingAverageState(length);
        await CompareRoutes(new WindowedVolumeWeightedMovingAverage(length), batch, state, bars);
        await CompareRoutes(new WindowedVolumeWeightedMovingAverage(length),
            Stock(zeroVolume).CalculateWindowedVolumeWeightedMovingAverage(length), state, zeroVolume);
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(WindowedVolumeWeightedMovingAverage), "window",
            () => new WindowedVolumeWeightedMovingAverage(length)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task WaveTrendResidualRetainsItsGeometricDecayOnAFlatTail(int length)
    {
        var bars = Enumerable.Range(0, 300).Select(i =>
        {
            var value = i < 20 ? 100d : 120d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
        }).ToArray();
        var batch = Stock(bars).CalculateWaveTrendOscillator(length1: length);
        // A positive step has a positive EMA residual which decays without a sign reversal.
        Assert.All(batch.OutputValues["Wto"], value => Assert.True(value >= 0));
        if (length == 1) Assert.All(batch.OutputValues["Wto"], value => Close(0, value));
        using var state = new WaveTrendOscillatorState(length1: length);
        await CompareRoutes(new WaveTrendOscillator(length), batch, state, bars);
        var translated = bars.Select(b => new Bar(b.Time, b.Open + 1000000, b.High + 1000000,
            b.Low + 1000000, b.Close + 1000000, b.Volume)).ToArray();
        var shifted = Stock(translated).CalculateWaveTrendOscillator(length1: length);
        foreach (var key in batch.OutputValues.Keys)
            for (var i = 0; i < bars.Length; i++) Close(batch.OutputValues[key][i], shifted.OutputValues[key][i]);
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(WaveTrendOscillator), "stable-residual",
            () => new WaveTrendOscillator(length)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task ExpandedIndicatorsKeepTheirValueAndValidateEveryPublishedSeries()
    {
        var bars = Market(140);
        var bands = new MovingAverageBands(5, 17);
        Assert.Equal(4, bands.Outputs.Count);
        Assert.Same(bands.Value, bands.PrimaryOutput);
        Assert.Same(bands.Value, bands.Outputs[0]);
        using var bandState = new MovingAverageBandsState(fastLength: 5, slowLength: 17);
        await CompareRoutes(bands, Stock(bars).CalculateMovingAverageBands(fastLength: 5, slowLength: 17), bandState, bars);
        var continuation = new TrendContinuationFactor(9);
        Assert.Equal(2, continuation.Outputs.Count);
        Assert.Same(continuation.Value, continuation.PrimaryOutput);
        using var continuationState = new TrendContinuationFactorState(9);
        await CompareRoutes(continuation, Stock(bars).CalculateTrendContinuationFactor(9), continuationState, bars);
        var woodie = new WoodieCommodityChannelIndex(5, 17);
        Assert.Equal(3, woodie.Outputs.Count);
        Assert.Same(woodie.Value, woodie.PrimaryOutput);
        using var woodieState = new WoodieCommodityChannelIndexState(fastLength: 5, slowLength: 17);
        await CompareRoutes(woodie, Stock(bars).CalculateWoodieCommodityChannelIndex(fastLength: 5, slowLength: 17), woodieState, bars);
        foreach (var indicator in new IIndicator[] { bands, continuation, woodie })
            Assert.Equal(indicator.Outputs.Count, IndicatorFormulaCoverage.Inspect(new(indicator.GetType(), "expanded",
                () => indicator)).ReferencedOutputSlots.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpandedChannelsExposeBandsAndKeepTheMiddleAsValue(bool absolute)
    {
        var bars = Market(100);
        var channel = new UniChannel(7, .4, .2, absolute);
        Assert.Same(channel.Value, channel.Outputs[1]);
        Assert.Same(channel.Value, channel.PrimaryOutput);
        using var state = new UniChannelState(length: 7, ubFac: .4, lbFac: .2, type1: absolute);
        var batch = Stock(bars).CalculateUniChannel(length: 7, ubFac: .4, lbFac: .2, type1: absolute);
        await CompareRoutes(channel, batch, state, bars);
        for (var i = 0; i < bars.Length; i++)
        {
            var middle = batch.OutputValues["MiddleBand"][i];
            Close(absolute ? .4 : .4 * middle, batch.OutputValues["UpperBand"][i] - middle);
            Close(absolute ? .2 : .2 * middle, middle - batch.OutputValues["LowerBand"][i]);
        }
        foreach (var indicator in new IIndicator[] { channel, new AdaptivePriceZoneIndicator(7),
            new NarrowSidewaysChannel(7, .03), new TrendTraderBands(7), new MovingAverageEnvelope(7) })
        {
            Assert.Equal(3, indicator.Outputs.Count);
            Assert.Same(indicator.Outputs[1], ((IPrimaryOutputIndicator)indicator).PrimaryOutput);
            Assert.Equal(3, IndicatorFormulaCoverage.Inspect(new(indicator.GetType(), "expanded",
                () => indicator)).ReferencedOutputSlots.Count);
        }
    }

    [Fact]
    public async Task OnePeriodRetracementAndDynamicLevelsUseOnlyTheCurrentCandleRange()
    {
        var bars = Market(60);
        var fib = Stock(bars).CalculateFibonacciRetrace(length1: 1, length2: 1, factor: .25);
        var dynamic = Stock(bars).CalculateDynamicSupportAndResistance(length: 1);
        for (var i = 0; i < bars.Length; i++)
        {
            Close(.75 * bars[i].High + .25 * bars[i].Low, fib.OutputValues["UpperBand"][i]);
            Close(.25 * bars[i].High + .75 * bars[i].Low, fib.OutputValues["LowerBand"][i]);
            Close((bars[i].High + bars[i].Low) / 2, dynamic.OutputValues["MiddleBand"][i]);
        }
        using var fibState = new FibonacciRetraceState(length1: 1, length2: 1, factor: .25);
        using var dynamicState = new DynamicSupportAndResistanceState(length: 1);
        await CompareRoutes(new FibonacciRetrace(1, 1, .25), fib, fibState, bars);
        await CompareRoutes(new DynamicSupportAndResistance(1), dynamic, dynamicState, bars);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(100)]
    public async Task KaufmanRegressionUsesOneWeightMeasureForAllMoments(int length)
    {
        var bars = Market(240);
        var correlation = Stock(bars).CalculateKaufmanAdaptiveCorrelationOscillator(length: length);
        var fit = Stock(bars).CalculateKaufmanAdaptiveLeastSquaresMovingAverage(length: length);
        using var correlationState = new KaufmanAdaptiveCorrelationOscillatorState(length: length);
        using var fitState = new KaufmanAdaptiveLeastSquaresMovingAverageState(length: length);
        await CompareRoutes(new KaufmanAdaptiveCorrelationOscillator(length), correlation, correlationState, bars);
        await CompareRoutes(new KaufmanAdaptiveLeastSquaresMovingAverage(length), fit, fitState, bars);
        Assert.All(correlation.OutputValues["Kaco"], value => Assert.InRange(value, -1, 1));
        var affine = bars.Select(b => new Bar(b.Time, 3*b.Open+1000, 3*b.High+1000,
            3*b.Low+1000, 3*b.Close+1000, b.Volume)).ToArray();
        var transformedCorrelation = Stock(affine).CalculateKaufmanAdaptiveCorrelationOscillator(length: length);
        var transformedFit = Stock(affine).CalculateKaufmanAdaptiveLeastSquaresMovingAverage(length: length);
        for (var i = 0; i < bars.Length; i++)
        {
            Close(correlation.OutputValues["Kaco"][i], transformedCorrelation.OutputValues["Kaco"][i]);
            Close(3*correlation.OutputValues["SrcSt"][i], transformedCorrelation.OutputValues["SrcSt"][i]);
            Close(correlation.OutputValues["IndexSt"][i], transformedCorrelation.OutputValues["IndexSt"][i]);
            Close(3*fit.OutputValues["Kalsma"][i]+1000, transformedFit.OutputValues["Kalsma"][i]);
        }
        var line = bars.Select((b, i) => new Bar(b.Time, 2+3*i, 2+3*i, 2+3*i, 2+3*i, b.Volume)).ToArray();
        var linearFit = Stock(line).CalculateKaufmanAdaptiveLeastSquaresMovingAverage(length: length);
        var linearCorrelation = Stock(line).CalculateKaufmanAdaptiveCorrelationOscillator(length: length);
        for (var i = 0; i < line.Length; i++)
        {
            Close(line[i].Close, linearFit.OutputValues["Kalsma"][i]);
            Close(i < length ? 0 : 1, linearCorrelation.OutputValues["Kaco"][i]);
        }
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(KaufmanAdaptiveCorrelationOscillator), "shared-moments",
            () => new KaufmanAdaptiveCorrelationOscillator(length)), new() { RequireFormulaReference = true });
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(KaufmanAdaptiveLeastSquaresMovingAverage), "shared-moments",
            () => new KaufmanAdaptiveLeastSquaresMovingAverage(length)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(30)]
    public async Task OptimizedTrendTrackerRetainsDirectionAndTrailsBothStops(int length)
    {
        foreach (var average in new[] { MovingAvgType.VariableIndexDynamicAverage, MovingAvgType.SimpleMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            var bars = Market(180);
            IMovingAverage? component = average == MovingAvgType.VariableIndexDynamicAverage ? null
                : average == MovingAvgType.SimpleMovingAverage ? new Sma() : average == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            using var state = new OptimizedTrendTrackerState(average, length, 1.4);
            await CompareRoutes(new OptimizedTrendTracker(length, 1.4, component),
                Stock(bars).CalculateOptimizedTrendTracker(average, length, 1.4), state, bars);
            await IndicatorValidation.ValidateAndThrowAsync(new(typeof(OptimizedTrendTracker), "trailing-stops",
                () => new OptimizedTrendTracker(length, 1.4, component)), new() { RequireFormulaReference = true });
        }
    }

    [Fact]
    public void OptimizedTrendTrackerHandPathHoldsTheBandAndReverses()
    {
        var prices = new[] { 100d, 105, 103, 94, 90, 94, 105 };
        var bars = Market(prices.Length).Select((b, i) => new Bar(b.Time, prices[i], prices[i], prices[i], prices[i], 1)).ToArray();
        var actual = Stock(bars).CalculateOptimizedTrendTracker(MovingAvgType.SimpleMovingAverage, 1, 10).CustomValuesList;
        var expected = new[] { 94.5, 99.225, 99.225, 98.23, 94.05, 94.05, 99.225 };
        for (var i = 0; i < expected.Length; i++) Close(expected[i], actual[i]);
        var flat = Stock(Market(8, 100)).CalculateOptimizedTrendTracker(MovingAvgType.SimpleMovingAverage, 1, 10);
        Assert.All(flat.CustomValuesList, value => Close(94.5, value));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 3, 1)]
    [InlineData(7, 19, 4)]
    [InlineData(18, 200, 8)]
    public async Task VervoortModifiedBandsExposeAndValidateAllFourOutputs(int band, int outer, int smooth)
    {
        foreach (var kind in new[] { MovingAvgType.TripleExponentialMovingAverage, MovingAvgType.SimpleMovingAverage,
            MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage? component = kind == MovingAvgType.TripleExponentialMovingAverage ? null
                : kind == MovingAvgType.SimpleMovingAverage ? new Sma() : new Wma();
            var indicator = new VervoortModifiedBollingerBandIndicator(band, outer, smooth, 1.6, component);
            var bars = Market(240);
            var batch = Stock(bars).CalculateVervoortModifiedBollingerBandIndicator(kind, band, outer, smooth, 1.6);
            using var state = new VervoortModifiedBollingerBandIndicatorState(kind, band, outer, smooth, 1.6);
            await CompareRoutes(indicator, batch, state, bars);
            Assert.Equal(4, indicator.Outputs.Count);
            for (var i = 0; i < bars.Length; i++)
            {
                Close(50, batch.OutputValues["MiddleBand"][i]);
                Close(100, batch.OutputValues["UpperBand"][i]+batch.OutputValues["LowerBand"][i]);
            }
            await IndicatorValidation.ValidateAndThrowAsync(new(typeof(VervoortModifiedBollingerBandIndicator), "double-dispersion",
                () => new VervoortModifiedBollingerBandIndicator(band, outer, smooth, 1.6, component)), new() { RequireFormulaReference = true });
        }
    }

    [Theory]
    [InlineData(1, .07)]
    [InlineData(5, .07)]
    [InlineData(12, .2)]
    public async Task EhlersSineWavesUseTheFullFourierBasis(int length, double alpha)
    {
        var bars = Market(240);
        using var first = new EhlersSineWaveIndicatorV1State();
        using var second = new EhlersSineWaveIndicatorV2State(length, alpha);
        var v1 = Stock(bars).CalculateEhlersSineWaveIndicatorV1();
        var v2 = Stock(bars).CalculateEhlersSineWaveIndicatorV2(length, alpha);
        await CompareRoutes(new EhlersSineWaveIndicatorV1(), v1, first, bars);
        await CompareRoutes(new EhlersSineWaveIndicatorV2(length, alpha), v2, second, bars);
        foreach (var batch in new[] { v1, v2 })
        for (var i = 0; i < bars.Length; i++)
        {
            var sine = batch.OutputValues["Sine"][i]; var lead = batch.OutputValues["LeadSine"][i];
            Assert.InRange(sine, -1, 1); Assert.InRange(lead, -1, 1);
            Close(1, sine*sine+Math.Pow(Math.Sqrt(2)*lead-sine, 2));
        }
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(EhlersSineWaveIndicatorV2), "full-phase",
            () => new EhlersSineWaveIndicatorV2(length, alpha)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(13)]
    [InlineData(30)]
    public async Task SelfAdjustingLaguerreUsesFiniteGainsAndExpandedAllPassStages(int length)
    {
        var bars = Market(180);
        var indicator = new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha(length);
        var batch = Stock(bars).CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha(length);
        using var state = new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaState(length);
        await CompareRoutes(indicator, batch, state, bars);
        Assert.All(batch.CustomValuesList, value => Assert.InRange(value, 0, 1));
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha), "expanded-stages",
            () => new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha(length)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(3, 3, 3, 1)]
    [InlineData(3, 12, 10, 3)]
    [InlineData(8, 50, 40, 10)]
    public async Task DominantCycleTunedBypassPublishesBothIndependentFilterOutputs(int minimum, int maximum, int cutoff, int median)
    {
        var bars = Market(120);
        var indicator = new EhlersDominantCycleTunedBypassFilter(minimum, maximum, cutoff, median);
        var batch = Stock(bars).CalculateEhlersDominantCycleTunedBypassFilter(minimum, maximum, cutoff, median);
        using var state = new EhlersDominantCycleTunedBypassFilterState(minimum, maximum, cutoff, median);
        await CompareRoutes(indicator, batch, state, bars);
        Assert.Equal(2, indicator.Outputs.Count);
        foreach (var rule in BuiltInFormulaReferences.For(indicator))
            rule.Check(new IndicatorValidationContext("tuned-bypass", bars, batch.OutputValues.Values.Cast<IReadOnlyList<double>>().ToArray(), 0));
        var zero = Stock(Market(90, 0)).CalculateEhlersDominantCycleTunedBypassFilter(minimum, maximum, cutoff, median);
        foreach (var values in zero.OutputValues.Values) Assert.All(values, value => Close(0, value));
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(12, 4, 3, 24)]
    [InlineData(48, 20, 10, 40)]
    public async Task EhlersCycleEstimatorsMatchIndependentComplexPhaseMeasurements(int upper, int lower, int minimum, int horizon)
    {
        var bars = Market(180);
        var indicators = new IIndicator[] { new EhlersDualDifferentiatorDominantCycle(upper, lower, minimum),
            new EhlersHomodyneDominantCycle(upper, lower, minimum), new EhlersPhaseAccumulationDominantCycle(upper, lower, minimum, horizon) };
        var batches = new[] { Stock(bars).CalculateEhlersDualDifferentiatorDominantCycle(upper, lower, minimum),
            Stock(bars).CalculateEhlersHomodyneDominantCycle(upper, lower, minimum),
            Stock(bars).CalculateEhlersPhaseAccumulationDominantCycle(upper, lower, minimum, horizon) };
        using var dual = new EhlersDualDifferentiatorDominantCycleState(upper, lower, minimum);
        using var homodyne = new EhlersHomodyneDominantCycleState(upper, lower, minimum);
        using var phase = new EhlersPhaseAccumulationDominantCycleState(upper, lower, minimum, horizon);
        var states = new IStreamingIndicatorState[] { dual, homodyne, phase };
        for (var i = 0; i < indicators.Length; i++)
        {
            await CompareRoutes(indicators[i], batches[i], states[i], bars);
            var indicator = indicators[i];
            await IndicatorValidation.ValidateAndThrowAsync(new(indicator.GetType(), "complex-phase", () => i == 0
                ? new EhlersDualDifferentiatorDominantCycle(upper, lower, minimum)
                : i == 1 ? new EhlersHomodyneDominantCycle(upper, lower, minimum)
                : new EhlersPhaseAccumulationDominantCycle(upper, lower, minimum, horizon)),
                new() { RequireFormulaReference = true });
        }
    }

    [Theory]
    [InlineData(3, 3, 1)]
    [InlineData(3, 12, 10)]
    [InlineData(8, 50, 40)]
    public async Task DiscreteFourierUsesCurrentPeriodBinsAndSupportsPreview(int minimum, int maximum, int cutoff)
    {
        var bars = Market(180);
        var indicator = new EhlersDiscreteFourierTransform(minimum, maximum, cutoff);
        var batch = Stock(bars).CalculateEhlersDiscreteFourierTransform(minimum, maximum, cutoff);
        using var state = new EhlersDiscreteFourierTransformState(minimum, maximum, cutoff);
        await CompareRoutes(indicator, batch, state, bars);
        Assert.All(batch.CustomValuesList, value => Assert.True(value == 0 || value >= minimum-1e-9 && value <= maximum+1e-9));
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(EhlersDiscreteFourierTransform), "current-spectrum",
            () => new EhlersDiscreteFourierTransform(minimum, maximum, cutoff)),
            new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void DiscreteFourierRecoversKnownPeriods(int period)
    {
        var bars = Market(400).Select((b, i) =>
        {
            var value = Math.Sin(2*Math.PI*i/period);
            return new Bar(b.Time, value, value, value, value, 1);
        }).ToArray();
        // Five or more complete cycles give enough frequency resolution for this accuracy check.
        var cycle = Stock(bars).CalculateEhlersDiscreteFourierTransform(8, 100, 40).CustomValuesList;
        Assert.All(cycle.Skip(300), value => Assert.InRange(value, period-.6, period+.6));
        Assert.All(Stock(Market(30, 0)).CalculateEhlersDiscreteFourierTransform().CustomValuesList, value => Close(0, value));
    }

    [Theory]
    [InlineData(1, 1, 1, 1, 1)]
    [InlineData(2, 3, 4, 7, 9)]
    [InlineData(13, 19, 21, 39, 50)]
    public async Task UltimateMomentumMatchesTheIndependentComponentBlend(int rsi, int fast, int flow, int slow, int band)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma()
                : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            var bars = Market(240);
            var indicator = new UltimateMomentumIndicator(rsi, fast, flow, slow, band, 1.5, average);
            using var state = new UltimateMomentumIndicatorState(kind, rsi, fast, flow, slow, band);
            await CompareRoutes(indicator, Stock(bars).CalculateUltimateMomentumIndicator(kind, rsi, fast, flow, slow, band), state, bars);
            await IndicatorValidation.ValidateAndThrowAsync(new(typeof(UltimateMomentumIndicator), "component-blend-"+kind,
                () => new UltimateMomentumIndicator(rsi, fast, flow, slow, band, 1.5, average)), new() { RequireFormulaReference = true });
        }
    }

    [Theory]
    [InlineData(1, .1)]
    [InlineData(2, .1)]
    [InlineData(3, .1)]
    [InlineData(7, .5)]
    [InlineData(20, .1)]
    [InlineData(20, 0)]
    [InlineData(20, 2)]
    public async Task FourierHarmonicsHaveStablePolesAndFrequencyScaledDerivatives(int length, double bandwidth)
    {
        var bars = Market(180);
        var indicator = new EhlersFourierSeriesAnalysis(length, bandwidth);
        var batch = Stock(bars).CalculateEhlersFourierSeriesAnalysis(length, bandwidth);
        using var state = new EhlersFourierSeriesAnalysisState(length, bandwidth);
        await CompareRoutes(indicator, batch, state, bars);
        if (length <= 2 || bandwidth == 0)
            foreach (var values in batch.OutputValues.Values) Assert.All(values, value => Close(0, value));
        for (var i = 0; i < bars.Length; i++)
            Close(length/(4*Math.PI)*(batch.OutputValues["Wave"][i]-(i < 2 ? 0 : batch.OutputValues["Wave"][i-2])), batch.OutputValues["Roc"][i]);
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(EhlersFourierSeriesAnalysis), "harmonic-transfer",
            () => new EhlersFourierSeriesAnalysis(length, bandwidth)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public void MultiMarketBuilderAndBatchPublishIndependentFormulaOutputs()
    {
        var primary = Market(180);
        var market = primary.Select((bar, i) => new Bar(bar.Time, 60+Math.Sin(i*.17), 64+Math.Sin(i*.17),
            57+Math.Sin(i*.17), 61+Math.Sin(i*.17), 2000)).ToArray();
        foreach (var testCase in IndicatorValidationDiscovery.DiscoverMultiSeries(new[] { typeof(IIndicator).Assembly }).Where(c => c.Name == "default"))
        {
            var state = testCase.Factory(MultiSeriesIndicatorValidation.PrimaryKey, MultiSeriesIndicatorValidation.BenchmarkKey);
            var name = state.Name; (state as IDisposable)?.Dispose();
            var expected = testCase.Reference!(primary, market);
            var stock = Stock(primary); var benchmark = Stock(market);
            var batch = name switch
            {
                IndicatorName.ComparePriceMomentumOscillator => stock.CalculateComparePriceMomentumOscillator(benchmark),
                IndicatorName.KaufmanStressIndicator => stock.CalculateKaufmanStressIndicator(benchmark),
                IndicatorName.RSMKIndicator => stock.CalculateRSMKIndicator(benchmark),
                IndicatorName.RelativeNormalizedVolatility => stock.CalculateRelativeNormalizedVolatility(benchmark),
                IndicatorName.RelativeStrength3DIndicator => stock.CalculateRelativeStrength3DIndicator(benchmark),
                IndicatorName.SectorRotationModel => stock.CalculateSectorRotationModel(benchmark),
                _ => throw new InvalidOperationException()
            };
            foreach (var key in expected.Keys)
                for (var i = 0; i < primary.Length; i++) Close(expected[key][i], batch.OutputValues[key][i], name+"/batch/"+key+"/"+i);
            var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(Stock(primary)));
            builder.AddDataSource("benchmark", IndicatorDataSource.FromBatch(Stock(market)));
            var handles = new Dictionary<string, SeriesHandle>();
            builder.ConfigureIndicators(catalog =>
            {
                var marketPrice = catalog.Price("benchmark");
                var handle = name switch
                {
                    IndicatorName.ComparePriceMomentumOscillator => catalog.ComparePriceMomentumOscillator(marketPrice),
                    IndicatorName.KaufmanStressIndicator => catalog.KaufmanStressIndicator(marketPrice),
                    IndicatorName.RSMKIndicator => catalog.RSMKIndicator(marketPrice),
                    IndicatorName.RelativeNormalizedVolatility => catalog.RelativeNormalizedVolatility(marketPrice),
                    IndicatorName.RelativeStrength3DIndicator => catalog.RelativeStrength3DIndicator(marketPrice),
                    IndicatorName.SectorRotationModel => catalog.SectorRotationModel(marketPrice),
                    _ => throw new InvalidOperationException()
                };
                handles.Add(testCase.OutputKeys[0], handle);
                if (name == IndicatorName.SectorRotationModel) handles.Add("Signal", catalog.SectorRotationModel(marketPrice, "Signal"));
            });
            using var runtime = builder.Build(); runtime.Start();
            foreach (var pair in handles)
            {
                runtime.Subscribe(pair.Value); var actual = runtime.GetSeries(pair.Value);
                Assert.Equal(primary.Length, actual.Count);
                for (var i = 0; i < primary.Length; i++) Close(expected[pair.Key][i], actual[i], name+"/builder/"+pair.Key+"/"+i);
            }
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 2)]
    [InlineData(3, 20, 4, 7)]
    [InlineData(5, 4, 12, 54)]
    public async Task MesaPredictionKeepsSeparateLagHistories(int horizon, int order, int smooth, int window)
    {
        var bars = Market(140);
        var batch = Stock(bars).CalculateEhlersMesaPredictIndicatorV1(horizon, order, 1, smooth, window);
        using var state = new EhlersMesaPredictIndicatorV1State(horizon, order, 1, smooth, window);
        await CompareRoutes(new EhlersMesaPredictIndicatorV1(horizon, order, smooth, window), batch, state, bars);
        for (var i = 0; i < bars.Length; i++) Close((batch.OutputValues["PrePredict"][i]+
            (i == 0 ? 0 : batch.OutputValues["PrePredict"][i-1]))/2, batch.OutputValues["Predict"][i]);
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(EhlersMesaPredictIndicatorV1), "burg-and-hann",
            () => new EhlersMesaPredictIndicatorV1(horizon, order, smooth, window)), new() { RequireFormulaReference = true });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task MobilityIntegratesACommonProbabilityDistribution(int length)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage Average() => kind == MovingAvgType.SimpleMovingAverage ? new Sma()
                : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            var bars = Market(120);
            var batch = Stock(bars).CalculateMobilityOscillator(kind, length2: length);
            using var state = new MobilityOscillatorState(kind, length2: length);
            await CompareRoutes(new MobilityOscillator(length, Average()), batch, state, bars);
            foreach (var values in batch.OutputValues.Values) Assert.All(values, value => Assert.InRange(value, -100, 100));
            await IndicatorValidation.ValidateAndThrowAsync(new(typeof(MobilityOscillator), "probability-mass-"+kind,
                () => new MobilityOscillator(length, Average())), new() { RequireFormulaReference = true });
        }
    }

    [Fact]
    public void MobilityModeTracksTheDensestBinAndPointCandles()
    {
        // Current two-candle window has all probability mass at 9. The lagged close 1 is below its mode.
        var prices = new[] { 1d, 9, 9 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1000)).ToArray();
        var flatWindow = Stock(bars).CalculateMobilityOscillator(length1: 2, length2: 2, signalLength: 1);
        Assert.Equal(0, flatWindow.CustomValuesList[2]);
        // One candle occupies [0,10], the other is a point at 9: the upper bin is the mode.
        bars[1] = new Bar(bars[1].Time, 9, 10, 0, 9, 1000);
        var mixed = Stock(bars).CalculateMobilityOscillator(length1: 2, length2: 2, signalLength: 1);
        Close(100*(1-0.5/1.5), mixed.CustomValuesList[2]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(21)]
    public async Task GuppyCountBackUsesActualHistoryAndSuccessiveExtrema(int length)
    {
        var bars = Market(90);
        using var state = new GuppyCountBackLineState(length);
        await CompareRoutes(new GuppyCountBackLine(length), Stock(bars).CalculateGuppyCountBackLine(length), state, bars);
        await IndicatorValidation.ValidateAndThrowAsync(new(typeof(GuppyCountBackLine), "count-back",
            () => new GuppyCountBackLine(length)), new() { RequireFormulaReference = true });
    }

    [Fact]
    public async Task GuppyCountBackIgnoresInsideBarsAndMirrorsDirection()
    {
        var levels = new[] { 8d, 9, 9, 10, 12 };
        foreach (var sign in new[] { 1d, -1d })
        {
            var bars = levels.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), sign*v,
                sign*v+0.5, sign*v-0.5, sign*v, 1000)).ToArray();
            var batch = Stock(bars).CalculateGuppyCountBackLine(5);
            var expected = sign > 0 ? new[] { 8d, 9, 9, 7.5, 8.5 } : new[] { -8d, -9, -9, -7.5, -8.5 };
            Assert.Equal(expected, batch.OutputValues["Cbl"]);
            using var state = new GuppyCountBackLineState(5);
            await CompareRoutes(new GuppyCountBackLine(5), batch, state, bars);
        }
    }

    [Theory]
    [InlineData(100, 100.000000000001, 0)]
    [InlineData(100, 100.00000001, -1)]
    [InlineData(-100, -100.00000001, 1)]
    [InlineData(0, 0.0000000000001, 0)]
    [InlineData(0, 0.00000000001, -1)]
    public void TechnicalRatingVotesSeparateNumericalTiesFromRealCrossings(double left, double right, int expected)
    {
        Assert.Equal(expected, TechnicalRatingComparison.Compare(left, right));
        Assert.Equal(-expected, TechnicalRatingComparison.Compare(right, left));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(14)]
    public async Task TechnicalRatingsScoreIndependentMovingAndOscillatorVotes(int period)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            IMovingAverage average = kind == MovingAvgType.SimpleMovingAverage ? new Sma()
                : kind == MovingAvgType.ExponentialMovingAverage ? new Ema() : new Wma();
            var bars = Market(260);
            var indicator = new TechnicalRatings(period, period+3, period, period, 3, 3, average);
            using var state = new TechnicalRatingsState(kind, period, period+3, period, period, 3, 3);
            var batch = Stock(bars).CalculateTechnicalRatings(kind, period, period+3, period, period, 3, 3);
            await CompareRoutes(indicator, batch, state, bars);
            foreach (var values in batch.OutputValues.Values) Assert.All(values, value => Assert.InRange(value, -1, 1));
            for (var i = 0; i < bars.Length; i++) Close((batch.OutputValues["Mr"][i]+batch.OutputValues["Or"][i])/2, batch.OutputValues["Tr"][i]);
            await IndicatorValidation.ValidateAndThrowAsync(new(typeof(TechnicalRatings), "independent-votes-"+kind,
                () => new TechnicalRatings(period, period+3, period, period, 3, 3, average)), new() { RequireFormulaReference = true });
        }
    }

    private static async Task CompareRoutes(IIndicator indicator, StockData batch,
        IStreamingIndicatorState state, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync();
        var outputs = batch.OutputValues.Values.ToArray();
        for (var slot = 0; slot < indicator.Outputs.Count; slot++)
        {
            var actual = run[indicator.Outputs[slot]].ToArray();
            Assert.Equal(bars.Length, actual.Length);
            for (var i = 0; i < bars.Length; i++) Close(outputs[slot][i], actual[i], $"{indicator.GetType().Name} builder output {slot}, bar {i}");
        }
        for (var pass = 0; pass < 2; pass++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Minutes(1), b.Time, b.Time.AddMinutes(1),
                    b.Open, b.High, b.Low, b.Close, b.Volume, true);
                var preview = state.Update(bar, isFinal: false, includeOutputs: true);
                var committed = state.Update(bar, isFinal: true, includeOutputs: true);
                foreach (var pair in batch.OutputValues)
                {
                    Close(pair.Value[i], preview.Outputs![pair.Key], $"{state.Name} preview {pair.Key}, bar {i}");
                    Close(pair.Value[i], committed.Outputs![pair.Key], $"{state.Name} commit {pair.Key}, bar {i}");
                }
            }
        }
    }

    private static void Close(double expected, double actual, string? context = null) => Assert.True(double.IsFinite(actual)
        && Math.Abs(expected - actual) <= 1e-9 * Math.Max(1, Math.Abs(expected)), $"{context}: Expected {expected:R}, got {actual:R}");

    private static Bar[] Market(int count, double? flat = null) => Enumerable.Range(0, count).Select(i =>
    {
        var price = flat ?? 100 + 7 * Math.Sin(i * 0.7) + i * 0.03;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + (flat.HasValue ? 0 : 1),
            price - (flat.HasValue ? 0 : 1), price, 1000 + i);
    }).ToArray();

    private static StockData Stock(Bar[] bars) => new(bars.Select(b => b.Open).ToList(),
        bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
        bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
}
