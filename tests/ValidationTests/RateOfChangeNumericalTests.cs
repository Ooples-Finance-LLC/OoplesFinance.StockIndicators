using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RateOfChangeNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Roc) || c.IndicatorType == typeof(RateOfChange) || c.IndicatorType == typeof(Vroc))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryAliasAndPeriodReceivesNumericalAndSignedOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        var prefix = testCase.IndicatorType == typeof(Vroc) ? "vroc-" : "roc-";
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == prefix + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Fact]
    public void FinalRatioAvoidsIntermediateOverflowAndPreservesTheZeroDenominatorPolicy()
    {
        Assert.Equal(-200, RoundedPercentageChange.Of(double.MaxValue, -double.MaxValue));
        Assert.Equal(-200, RoundedPercentageChange.Of(-double.MaxValue, double.MaxValue));
        Assert.Equal(-50, RoundedPercentageChange.Of(double.MaxValue / 2, double.MaxValue));
        Assert.Equal(100, RoundedPercentageChange.Of(2 * double.Epsilon, double.Epsilon));
        Assert.Equal(0, RoundedPercentageChange.Of(double.MaxValue, 0));
        Assert.Equal(double.PositiveInfinity, RoundedPercentageChange.Of(double.MaxValue, double.Epsilon));
        Assert.Equal(double.NegativeInfinity, RoundedPercentageChange.Of(-double.MaxValue, double.Epsilon));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public async Task EveryRouteMatchesTheIndependentExactRatio(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedRateOfChange(bars, length);
            var data = Data(bars);
            data.CalculateRateOfChange(length);
            Assert.Equal(expected, data.OutputValues["Roc"]);
            var core = new double[bars.Count];
            OscillatorCore.RateOfChange(bars.Select(b => b.Close).ToArray(), core, length);
            Assert.Equal(expected, core);
            foreach (IIndicator indicator in new IIndicator[] { new Roc(length), new RateOfChange(length) })
            {
                if (expected.Any(double.IsInfinity))
                    await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
                        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
                else
                {
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                    Assert.Equal(expected, run[indicator].ToArray());
                }
                var builtIn = (IBuiltInIndicator)indicator;
                var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
                foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
                {
                    Assert.NotNull(state);
                    using var lifetime = state as IDisposable;
                    for (var replay = 0; replay < 2; replay++)
                    {
                        state.Reset();
                        for (var i = 0; i < bars.Count; i++)
                        foreach (var commit in new[] { false, true })
                        {
                            var b = bars[i];
                            var native = new OhlcvBar("ROC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public async Task VolumeRateUsesVolumeAcrossAllRoutes(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedRateOfChange(bars, length, volume: true);
            var data = Data(bars);
            data.CalculateVolumeRateOfChange(length);
            Assert.Equal(expected, data.OutputValues["Vroc"]);
            var core = new double[bars.Count];
            VolumeCore.VolumeRateOfChange(bars.Select(b => b.Volume).ToArray(), core, length);
            Assert.Equal(expected, core);
            var indicator = new Vroc(length);
            if (expected.Any(double.IsInfinity))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator].ToArray());
            }
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("VROC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task VolumeRatePreservesVolumeWhenCloseIsChainedAndColumnsAreUpdated()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, Math.Pow(2, i))).ToArray();
        var source = new Sma(2);
        var indicator = new Vroc(1).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d, 100, 100, 100 }, run[indicator].ToArray());
        var data = Data(bars);
        _ = data.TickerDataList;
        data.Volumes = new() { 3, 3, 3, 3 };
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeVrocFast(data, context, 1);
        Assert.Equal(new double[4], result.ToArray());
    }

    [Fact]
    public async Task TypedAliasesAndLegacySelectionUseTheSelectedPrices()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var source = new Sma(2);
        var compact = new Roc(1).Of(source);
        var expanded = new RateOfChange(1).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, compact, expanded).BuildAsync();
        var projected = run[source].ToArray().Select((v, i) => new Bar(bars[i].Time, v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedRateOfChange(projected, 1);
        Assert.Equal(expected, run[compact].ToArray());
        Assert.Equal(expected, run[expanded].ToArray());
        var data = Data(bars);
        data.InputValues = new() { 1, 2, 4, 8 };
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeRocFast(data, context, 1);
        Assert.Equal(new[] { 0d, 100, 100, 100 }, result.ToArray());
    }

    [Fact]
    public void DirectConsumersPreserveTheCorrectedRateAcrossPreviewAndReset()
    {
        var bars = Enumerable.Range(0, 80).Select(i => {
            var v = 20 + 7 * Math.Sin(i * 1.7);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1);
        }).ToArray();
        var cases = new (IStreamingIndicatorState State, Action<StockData> Batch)[] {
            (new CoppockCurveState(length: 3, fastLength: 2, slowLength: 5),
                d => d.CalculateCoppockCurve(length: 3, fastLength: 2, slowLength: 5)),
            (new TickLineMomentumOscillatorState(length: 3, smoothLength: 5),
                d => d.CalculateTickLineMomentumOscillator(length: 3, smoothLength: 5)),
            // Unit SMA stages isolate ROC arithmetic; the legacy multi-period SMA paths
            // have a bounded rounding contract rather than a bit-identity contract.
            (new PringSpecialKState(length1: 1, length2: 1, length3: 1, length4: 1, length5: 1,
                length6: 1, length7: 1, length8: 1, length9: 1, length10: 1, length11: 1,
                length12: 1, length13: 1, length14: 1, smoothLength: 1),
                d => d.CalculatePringSpecialK(length1: 1, length2: 1, length3: 1, length4: 1, length5: 1,
                    length6: 1, length7: 1, length8: 1, length9: 1, length10: 1, length11: 1,
                    length12: 1, length13: 1, length14: 1, smoothLength: 1)),
            (new PringSpecialKState(maType: MovingAvgType.ExponentialMovingAverage, length1: 2, length2: 3, length3: 4, length4: 5, length5: 6,
                length6: 7, length7: 8, length8: 9, length9: 10, length10: 11, length11: 12,
                length12: 13, length13: 14, length14: 15, smoothLength: 3),
                d => d.CalculatePringSpecialK(maType: MovingAvgType.ExponentialMovingAverage, length1: 2, length2: 3, length3: 4, length4: 5, length5: 6,
                    length6: 7, length7: 8, length8: 9, length9: 10, length10: 11, length11: 12,
                    length12: 13, length13: 14, length14: 15, smoothLength: 3))
        };
        foreach (var (state, batch) in cases)
        {
            using var lifetime = state as IDisposable;
            var data = Data(bars);
            batch(data);
            Assert.Contains(data.OutputValues.Values.SelectMany(v => v), value => value != 0);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var commit in new[] { false, true })
                {
                    var b = bars[i];
                    var native = new OhlcvBar("ROC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    var result = state.Update(native, commit, true);
                    foreach (var (key, values) in data.OutputValues)
                        Assert.True(values[i] == result.Outputs![key], $"{state.Name}.{key}, bar {i}: {values[i]:R} != {result.Outputs[key]:R}");
                }
            }
        }
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}
