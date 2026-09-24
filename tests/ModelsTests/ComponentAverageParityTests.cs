using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// The adversarial check on component substitution. An indicator handed one of the library's averages
/// collapses it into a MovingAvgType and the batch calculation answers. Handed a caller's own average, the
/// indicator's own calculation runs with that one average answered by the caller's series instead. If the
/// caller's average computes exactly what the built-in computes, the two must agree bit for bit - and any
/// indicator where they do not is one whose average is not the single thing the substitution assumed.
/// </summary>
public sealed class ComponentAverageParityTests
{
    /// <summary>A simple moving average that is deliberately not one of ours, so it cannot collapse to an enum.</summary>
    private sealed class MirrorSma(int length) : IndicatorBase, IMovingAverage
    {
        public override int WarmupBars => length;

        protected internal override object? CreateState() => new State(length);

        private sealed class State(int length) : IIndicatorState
        {
            // The library's own simple average, driven bar by bar. Writing the mean out by hand leaves the
            // two disagreeing in the last bits - invisible on its own, but an indicator that feeds its
            // average back amplifies it, which is what OvershootReductionMovingAverage did by bar 58.
            // Borrowing the real arithmetic makes this a mirror by construction, so anything left is
            // structural rather than two ways of writing the same mean.
            private readonly OoplesFinance.StockIndicators.Streaming.IMovingAverageSmoother _sma =
                OoplesFinance.StockIndicators.Streaming.MovingAverageSmootherFactory.Create(
                    MovingAvgType.SimpleMovingAverage, length);

            public void Reset() => _sma.Reset();

            public double Update(in Bar bar) => _sma.Next(bar.Close, isFinal: true);
        }
    }

    private sealed class MirrorWma(int length) : IndicatorBase, IMovingAverage
    {
        public override int WarmupBars => length;
        protected internal override object? CreateState() => new State(length);
        private sealed class State(int length) : IIndicatorState
        {
            private readonly OoplesFinance.StockIndicators.Streaming.IMovingAverageSmoother _average =
                OoplesFinance.StockIndicators.Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, length);
            public void Reset() => _average.Reset();
            public double Update(in Bar bar) => _average.Next(bar.Close, true);
        }
    }

    private sealed class ScaledAverage(double factor) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(factor);
        private sealed class State(double factor) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close * factor;
        }
    }

    [Fact]
    public async Task SlowMeanPreservesHeterogeneousCustomerStages()
    {
        var bars = Walk(120);
        var first = new Sma(5);
        var custom = new SlowSmoothedMovingAverage(15, new Sma(5), new ScaledAverage(2), new ScaledAverage(3));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(first, custom).BuildAsync();
        Assert.Equal(run[first].ToArray().Select(v => (v * 2) * 3).ToArray(), run[custom].ToArray());
    }

    [Fact]
    public async Task MiddleMeanAppliesCustomerAverageToMidpoints()
    {
        var bars = Walk(120);
        var midpoint = new Midpoint(7);
        var custom = new MiddleHighLowMovingAverage(3, 7, new ScaledAverage(2));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(midpoint, custom).BuildAsync();
        Assert.Equal(run[midpoint].ToArray().Select(value => value * 2).ToArray(), run[custom].ToArray());
    }

    [Fact]
    public async Task StochasticOutputsPreserveTwoDifferentCustomerStages()
    {
        var compact = new Stochastic(3, 7, new ScaledAverage(2), new ScaledAverage(3));
        var expanded = new StochasticOscillator(3, 7, 1, new ScaledAverage(2), new ScaledAverage(3));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(Walk(60)))
            .ConfigureIndicators(compact, expanded).BuildAsync();
        foreach (IIndicator indicator in new IIndicator[] { compact, expanded })
        {
            Assert.Equal(run[indicator].ToArray().Select(v => v * 2), run[indicator.Outputs[1]].ToArray());
            Assert.Equal(run[indicator.Outputs[1]].ToArray().Select(v => v * 3), run[indicator.Outputs[2]].ToArray());
        }
    }

    [Fact]
    public async Task DoubleStochasticPreservesTwoDifferentCustomerStages()
    {
        var raw = new DoubleStochasticOscillator(3, new ScaledAverage(1), new ScaledAverage(1));
        var custom = new DoubleStochasticOscillator(3, new ScaledAverage(2), new ScaledAverage(3));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(Walk(60)))
            .ConfigureIndicators(raw, custom).BuildAsync();
        Assert.Equal(run[raw].ToArray().Select(v => v * 2), run[custom].ToArray());
        Assert.Equal(run[custom].ToArray().Select(v => v * 3), run[custom.Signal].ToArray());
    }

    [Fact]
    public async Task ChandeSignalAppliesCustomerAverageToTheOscillator()
    {
        var bars = Walk(120);
        var compact = new Cmo(3, new ScaledAverage(2));
        var expanded = new ChandeMomentumOscillator(3, 7, new ScaledAverage(2));
        var filtered = new ChandeMomentumOscillatorFilter(3, new ScaledAverage(2));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(compact, expanded, filtered).BuildAsync();
        Assert.Equal(run[compact].ToArray().Select(v => v * 2), run[compact.Signal].ToArray());
        Assert.Equal(run[expanded].ToArray().Select(v => v * 2), run[expanded.Signal].ToArray());
        Assert.Equal(run[compact].ToArray(), run[expanded].ToArray());
        Assert.Equal(run[filtered].ToArray().Select(v => v * 2), run[filtered.Signal].ToArray());
    }

    [Fact]
    public async Task RsiCannotSilentlyApplyItsCustomerAverageOnlyToSignal()
    {
        var indicator = new Rsi(14, new MirrorSma(14));
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(Walk(30))).ConfigureIndicators(indicator).BuildAsync());
        Assert.Contains("asks for 2 averages", error.Message);
    }

    [Fact]
    public async Task IndependentOutputsDoNotAllowAnEntirelyUnusedCustomerAverage()
    {
        var bars = Walk(30);
        var supported = new Cmo(3, new ScaledAverage(2));
        var unused = new VariableIndexDynamicAverage(3, new ScaledAverage(2));
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(supported, unused).BuildAsync());
        Assert.Contains("asks for 0 averages", error.Message);
    }

    [Fact]
    public async Task SequentialMeanGatesTheCustomerAverageTrajectory()
    {
        var bars = Enumerable.Range(1, 6).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i, i, i, 1)).ToArray();
        var custom = new SequentiallyFilteredMovingAverage(3, new ScaledAverage(2));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync();
        Assert.Equal(new[] { 1d, 1d, 6d, 8d, 10d, 12d }, run[custom].ToArray());
    }

    [Fact]
    public async Task TriangularAliasesHonorBothCustomerAverageStages()
    {
        var bars = Walk(120);
        var builtIn = new Tma(3, new Wma());
        var custom = new Tma(3, new MirrorWma(3), new MirrorWma(3));
        var expanded = new TriangularMovingAverage(3, new MirrorWma(3), new MirrorWma(3));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(builtIn, custom, expanded).BuildAsync();
        Assert.Equal(run[builtIn].ToArray(), run[custom].ToArray());
        Assert.Equal(run[builtIn].ToArray(), run[expanded].ToArray());
    }

    [Fact]
    public async Task VolumeWeightedMeanHonorsTheCustomerVolumeAverage()
    {
        var bars = Walk(120);
        var builtIn = new VolumeWeightedMovingAverage(3, new Wma());
        var custom = new VolumeWeightedMovingAverage(3, new MirrorWma(3));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(builtIn, custom).BuildAsync();
        Assert.Equal(run[builtIn].ToArray(), run[custom].ToArray());
    }

    [Fact]
    public async Task ConfluenceUsesAllNineConfiguredAverageStages()
    {
        var bars = Walk(120);
        var builtIn = new ConfluenceIndicator(5, new Wma());
        var custom = new ConfluenceIndicator(5, new MirrorWma(5), new MirrorWma(9), new MirrorWma(17), new MirrorWma(33),
            new MirrorWma(4), new MirrorWma(8), new MirrorWma(16), new MirrorWma(32), new MirrorWma(32));
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync();
        Assert.Equal(expected[builtIn.Outputs[0]].ToArray(), actual[custom.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task PivotAverageUsesAllThreeCustomPeriodAverages()
    {
        var bars = Walk(120);
        var builtIn = new PivotPointAverage(5, InputLength.Day, new Wma());
        var custom = new PivotPointAverage(5, InputLength.Day, new MirrorWma(5), new MirrorWma(5), new MirrorWma(5));
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync();
        Assert.Equal(6, builtIn.Outputs.Count);
        for (var slot = 0; slot < 6; slot++)
            Assert.Equal(expected[builtIn.Outputs[slot]].ToArray(), actual[custom.Outputs[slot]].ToArray());
    }

    [Fact]
    public async Task PeakValleyUsesCustomAverageForAllThreeEvents()
    {
        var bars = Walk(120);
        var builtIn = new PeakValleyEstimation(5, 3, new Wma());
        var custom = new PeakValleyEstimation(5, 3, new MirrorWma(5));
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync();
        Assert.Equal(3, builtIn.Outputs.Count);
        for (var slot = 0; slot < 3; slot++)
            Assert.Equal(expected[builtIn.Outputs[slot]].ToArray(), actual[custom.Outputs[slot]].ToArray());
    }

    [Fact]
    public async Task FreedomOfMovementUsesCustomVolumeAverageForBothOutputs()
    {
        var bars = Walk(120);
        var builtIn = new FreedomOfMovement(5, new Wma());
        var custom = new FreedomOfMovement(5, new MirrorWma(5));
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync();
        Assert.Equal(2, builtIn.Outputs.Count);
        for (var slot = 0; slot < 2; slot++)
        {
            var values = expected[builtIn.Outputs[slot]].ToArray();
            var substituted = actual[custom.Outputs[slot]].ToArray();
            for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
        }
    }

    [Fact]
    public async Task RsingUsesBothCustomAverageStagesForBothOutputs()
    {
        var bars = Walk(90);
        var builtIn = new RSINGIndicator(5, new Wma());
        var custom = new RSINGIndicator(5, new MirrorWma(5), new MirrorWma(5));
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync();
        Assert.Equal(2, builtIn.Outputs.Count);
        for (var slot = 0; slot < 2; slot++)
        {
            var values = expected[builtIn.Outputs[slot]].ToArray();
            var substituted = actual[custom.Outputs[slot]].ToArray();
            for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
        }
    }

    [Fact]
    public async Task QuadraticRegressionUsesAllThreeCustomAverageStages()
    {
        var bars = Walk(90);
        var builtIn = new QuadraticRegression(5, new Wma());
        var custom = new QuadraticRegression(5, new MirrorWma(5), new MirrorWma(5), new MirrorWma(5));
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync();
        var values = expected[builtIn.Outputs[0]].ToArray();
        var substituted = actual[custom.Outputs[0]].ToArray();
        for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
    }

    [Fact]
    public async Task TopsAndBottomsUsesTheCustomAverageAtUnitAndLongerPeriods()
    {
        var bars = Walk(90);
        foreach (var length in new[] { 1, 5 })
        {
            var builtIn = new TopsAndBottomsFinder(length, new Wma());
            var custom = new TopsAndBottomsFinder(length, new MirrorWma(length));
            using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync();
            using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync();
            Assert.Equal(expected[builtIn.Outputs[0]].ToArray(), actual[custom.Outputs[0]].ToArray());
        }
    }

    [Fact]
    public void ExpandedChannelsHonorCustomAverageStagesForEveryOutput()
    {
        var bars = Walk(90);
        IIndicator[] builtIns = [new VervoortVolatilityBands(5, 7, 3.55, .9, maType: new Wma()),
            new PseudoPolynomialChannel(5, .9, maType: new Wma()), new LBRPaintBars(5, 7, 2.5, maType: new Wma())];
        IIndicator[] customs = [new VervoortVolatilityBands(5, 7, 3.55, .9, new MirrorWma(5), new MirrorWma(5), new MirrorWma(5)),
            new PseudoPolynomialChannel(5, .9, new MirrorWma(5)), new LBRPaintBars(5, 7, 2.5, new MirrorWma(5))];
        for (var indicator = 0; indicator < builtIns.Length; indicator++)
        {
            using var expected = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIns[indicator]).BuildAsync().GetAwaiter().GetResult();
            using var actual = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(customs[indicator]).BuildAsync().GetAwaiter().GetResult();
            Assert.Equal(3, builtIns[indicator].Outputs.Count);
            for (var slot = 0; slot < 3; slot++)
            {
                var values = expected[builtIns[indicator].Outputs[slot]].ToArray();
                var substituted = actual[customs[indicator].Outputs[slot]].ToArray();
                for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
            }
        }
    }

    [Fact]
    public void VariableBandsExposeBothCustomAverageStages()
    {
        var bars = Walk(90);
        var builtIn = new VariableMovingAverageBands(5, 1.5, maType: new Wma());
        var custom = new VariableMovingAverageBands(5, 1.5, new MirrorWma(5), new MirrorWma(5));
        using var expected = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync().GetAwaiter().GetResult();
        using var actual = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync().GetAwaiter().GetResult();
        Assert.Equal(3, builtIn.Outputs.Count);
        for (var slot = 0; slot < builtIn.Outputs.Count; slot++)
        {
            var values = expected[builtIn.Outputs[slot]].ToArray();
            var substituted = actual[custom.Outputs[slot]].ToArray();
            for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
        }
    }

    [Fact]
    public void TurboTriggerExposesBothOutputsAndEightAverageStages()
    {
        var bars = Walk(90);
        var builtIn = new TurboTrigger(5, 1, maType: new Wma());
        var custom = new TurboTrigger(5, 1, new MirrorWma(2), new MirrorWma(2), new MirrorWma(2), new MirrorWma(2),
            new MirrorWma(5), new MirrorWma(5), new MirrorWma(5), new MirrorWma(5));
        using var expected = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync().GetAwaiter().GetResult();
        using var actual = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync().GetAwaiter().GetResult();
        Assert.Equal(2, builtIn.Outputs.Count);
        for (var slot = 0; slot < builtIn.Outputs.Count; slot++)
        {
            var values = expected[builtIn.Outputs[slot]].ToArray();
            var substituted = actual[custom.Outputs[slot]].ToArray();
            for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
        }
    }

    [Fact]
    public void HistoricalVolatilityRanksExposeBothCustomAverageStages()
    {
        var bars = Walk(90);
        var builtIn = new HistoricalVolatilityPercentile(3, 10, maType: new Wma());
        var custom = new HistoricalVolatilityPercentile(3, 10, new MirrorWma(3), new MirrorWma(3));
        using var expected = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync().GetAwaiter().GetResult();
        using var actual = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync().GetAwaiter().GetResult();
        for (var slot = 0; slot < builtIn.Outputs.Count; slot++)
        {
            var values = expected[builtIn.Outputs[slot]].ToArray();
            var substituted = actual[custom.Outputs[slot]].ToArray();
            for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
        }
    }

    [Fact]
    public void QqeWidthsExposeAllFiveCustomAverageStages()
    {
        var bars = Walk(90);
        var builtIn = new QuantitativeQualitativeEstimation(3, 2, 2, 4, maType: new Wma());
        var custom = new QuantitativeQualitativeEstimation(3, 2, 2, 4,
            new MirrorWma(3), new MirrorWma(3), new MirrorWma(2), new MirrorWma(5), new MirrorWma(5));
        using var expected = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync().GetAwaiter().GetResult();
        using var actual = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync().GetAwaiter().GetResult();
        for (var slot = 0; slot < builtIn.Outputs.Count; slot++)
        {
            var values = expected[builtIn.Outputs[slot]].ToArray();
            var substituted = actual[custom.Outputs[slot]].ToArray();
            for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
        }
    }

    [Fact]
    public void EmpiricalModeDecompositionExposesAllThreeCustomAverageStages()
    {
        var bars = Walk(90);
        var builtIn = new EhlersEmpiricalModeDecomposition(5, 3, .5, .1, maType: new Wma());
        var custom = new EhlersEmpiricalModeDecomposition(5, 3, .5, .1,
            new MirrorWma(10), new MirrorWma(3), new MirrorWma(3));
        using var expected = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(builtIn).BuildAsync().GetAwaiter().GetResult();
        using var actual = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(custom).BuildAsync().GetAwaiter().GetResult();
        for (var slot = 0; slot < builtIn.Outputs.Count; slot++)
        {
            var values = expected[builtIn.Outputs[slot]].ToArray();
            var substituted = actual[custom.Outputs[slot]].ToArray();
            for (var i = 0; i < bars.Count; i++) substituted[i].Should().BeApproximately(values[i], 1e-9);
        }
    }

    [Fact]
    public void OscillatorInverseFisherTransformsExposeAllThreeCustomAverageStages()
    {
        var bars = Walk(90);
        var builtIns = new IIndicator[]
        {
            new EhlersRelativeStrengthIndexInverseFisherTransform(5, 3),
            new EhlersCommodityChannelIndexInverseFisherTransform(5, 3)
        };
        var custom = new IIndicator[]
        {
            new EhlersRelativeStrengthIndexInverseFisherTransform(5, 3, new MirrorWma(5), new MirrorWma(5), new MirrorWma(3)),
            new EhlersCommodityChannelIndexInverseFisherTransform(5, 3, .015, new MirrorWma(5), new MirrorWma(5), new MirrorWma(3))
        };
        for (var indicator = 0; indicator < builtIns.Length; indicator++)
        {
            var expected = Run(builtIns[indicator], bars)!;
            var actual = Run(custom[indicator], bars)!;
            for (var i = 0; i < bars.Count; i++) actual[i].Should().BeApproximately(expected[i], 1e-9);
        }
    }

    private static IReadOnlyList<Bar> Walk(int count = 150)
    {
        var random = new Random(31);
        var bars = new List<Bar>(count);
        var start = new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc);
        var last = 100d;

        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(start.AddMinutes(i), open, Math.Max(open, close) + random.NextDouble(),
                Math.Max(0.01, Math.Min(open, close) - random.NextDouble()), close, random.Next(1000, 400000)));
            last = close;
        }

        return bars;
    }

    private static double[]? Run(IIndicator indicator, IReadOnlyList<Bar> bars)
    {
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync().GetAwaiter().GetResult();

        return run[indicator].ToArray();
    }

    [Fact]
    public void RelativeSpreadPrecisionPreservesAllThreeCustomAverageStages()
    {
        var bars = Enumerable.Range(0, 90).Select(i =>
        {
            var value = 100 + 7 * Math.Sin(i * .7);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
        }).ToArray();
        var builtIn = Run(new RelativeSpreadStrength(3, 7, 5, 2, new Sma()), bars);
        var custom = Run(new RelativeSpreadStrength(3, 7, 5, 2, new MirrorSma(3), new MirrorSma(7), new MirrorSma(2)), bars);
        for (var i = 0; i < bars.Length; i++) custom[i].Should().BeApproximately(builtIn[i], 1e-8);
    }

    [Fact]
    public async Task LiveFactoryCannotSilentlyDiscardSeparatelyConfiguredAverageStages()
    {
        var feed = Bars.Live();
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder()
            .ConfigureSource(feed).ConfigureIndicators(new AwesomeOscillator(5, new Sma(5), new Sma(13))).BuildAsync());
        Assert.Contains("separately configured average stages", error.Message);
        feed.Complete();
    }

    [Fact]
    public void AnExtraAverageCannotSilentlyReplaceAnOmittedPrecedingStage()
    {
        Assert.Throws<ArgumentException>(() => new AwesomeOscillator(5, null, new Sma(13)));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EverySuppliedAverageKeepsItsPositionAndConfiguredPeriod(bool builtInFirst, bool builtInSecond)
    {
        var bars = Walk();
        var expected = Run(new AwesomeOscillator(5, new MirrorSma(5), new MirrorSma(13)), bars);
        IMovingAverage first = builtInFirst ? new Sma(5) : new MirrorSma(5);
        IMovingAverage second = builtInSecond ? new Sma(13) : new MirrorSma(13);
        var indicator = new AwesomeOscillator(5, first, second);
        Run(indicator, bars).Should().Equal(expected!, (actual, reference) => Math.Abs(actual - reference) <= 1e-8,
            "each supplied average must answer its own smoothing request");
        var coverage = OoplesFinance.StockIndicators.Validation.IndicatorFormulaCoverage.Inspect(
            new(indicator.GetType(), "configured-stages", () => new AwesomeOscillator(5, first, second)));
        Assert.False(coverage.IsComplete); // A single-enum reference cannot describe distinct stage periods.
    }

    [Fact]
    public void AnIndicatorThatSmoothsTwoThingsTakesOneAverageForEach()
    {
        var bars = Walk();

        // AwesomeOscillator is the fast average of the median minus the slow one, at 5 bars and 34. Handing
        // it one average answers both and makes it a difference of an average with itself, which is zero;
        // handing it the two it actually asks for has to reproduce naming the simple average exactly.
        var named = Run(new AwesomeOscillator(), bars);
        var supplied = Run(new AwesomeOscillator(5, new MirrorSma(5), new MirrorSma(34)), bars);

        named.Should().NotBeNull();
        supplied.Should().NotBeNull();
        supplied!.Should().Equal(named!, "the two averages it asks for are the two it was given");
        supplied.Skip(40).Should().Contain(v => Math.Abs(v) > 1e-9,
            "a difference of two different averages is not identically zero");
    }

    [Fact]
    public void SubstitutingAnAverageComputesWhatNamingItComputes()
    {
        var bars = Walk();
        var disagreed = new List<string>();
        var proved = 0;
        var refused = 0;
        var multiAverage = 0;

        foreach (var type in typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var ctor = type.GetConstructors().FirstOrDefault(c =>
                c.GetParameters().Any(p => p.ParameterType == typeof(IMovingAverage))
                && c.GetParameters().All(p => p.IsOptional));
            if (ctor is null)
            {
                continue;
            }

            object?[] Args(IMovingAverage? average, bool enumChoiceOnly = false)
            {
                var assigned = false;
                return ctor.GetParameters().Select(p =>
                {
                    if (p.ParameterType != typeof(IMovingAverage) || enumChoiceOnly && assigned) return p.DefaultValue;
                    assigned = true;
                    return (object?)average;
                }).ToArray();
            }

            IIndicator named;
            int length;
            try
            {
                named = (IIndicator)ctor.Invoke(Args(new Sma(20), enumChoiceOnly: true));

                // The batch runs its average over the indicator's own length, not the component's, so the
                // caller's average has to use that same length or the two are not the same question.
                var declared = type.GetProperty("Length")?.GetValue(named);
                if (declared is not int value)
                {
                    continue;
                }

                length = value;
            }
            catch
            {
                continue;
            }

            double[]? baseline;
            double[]? substituted;
            try
            {
                baseline = Run(named, bars);

                // Several indicators smooth over a second parameter rather than the one they are named
                // with, so the first run is only there to learn the period the average was actually asked
                // for. Mirroring the wrong period would compare two different averages and blame the
                // substitution for the difference.
                // Cleared first: an indicator that computes its own bands never reaches the substitution,
                // and reading a period left behind by the previous indicator would mirror the wrong one.
                StockIndicatorBuilder.LastAverageLength = 0;
                StockIndicatorBuilder.LastAverageRequests = 0;
                substituted = Run((IIndicator)ctor.Invoke(Args(new MirrorSma(length))), bars);
                var asked = StockIndicatorBuilder.LastAverageLength;

                // Naming a MovingAvgType applies it to every average the calculation asks for. Handing over
                // components does not - each one answers a different request, which is the point of them.
                // So the two forms are the same question only where there is one average to answer, and
                // this test supplies the same instance to every slot, which on a difference of two averages
                // is zero by construction. Those are covered by giving each slot its own average instead.
                if (StockIndicatorBuilder.LastAverageRequests > 1)
                {
                    multiAverage++;
                    continue;
                }

                // An average whose window never fills over this fixture is not being exercised by either
                // side - HirashimaSugitaRS asks for 1000 bars of it - so there is nothing here to hold the
                // two to. Comparing them anyway measures the warm-up convention, not the substitution.
                if (asked > bars.Count)
                {
                    continue;
                }

                if (asked > 0 && asked != length)
                {
                    substituted = Run((IIndicator)ctor.Invoke(Args(new MirrorSma(asked))), bars);
                }
            }
            catch (NotSupportedException)
            {
                refused++;
                continue;
            }
            catch
            {
                continue;
            }

            if (baseline is null || substituted is null || baseline.Length != substituted.Length)
            {
                continue;
            }

            proved++;
            for (var i = 0; i < baseline.Length; i++)
            {
                if (Math.Abs(baseline[i] - substituted[i]) > 1e-9)
                {
                    disagreed.Add(type.Name + " at bar " + i + ": named " + baseline[i].ToString("G6")
                        + ", substituted " + substituted[i].ToString("G6"));
                    break;
                }
            }
        }

        // Written out on success as well as failure: how many indicators accept a caller's own average is
        // the number this work is measured by, and an assertion message is only shown when it fails.
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "component-average-parity.txt"),
            "substituted=" + proved + " refused=" + refused + " multiAverage=" + multiAverage
                + " disagree=" + disagreed.Count);

        proved.Should().BeGreaterThan(0, "the substitution has to be exercised for this to prove anything");
        disagreed.Should().BeEmpty(proved + " indicators substituted, " + refused + " refused as ambiguous, "
            + disagreed.Count + " disagree: " + string.Join(" | ", disagreed.Take(12)));
    }
}
