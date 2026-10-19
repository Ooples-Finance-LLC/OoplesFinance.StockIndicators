using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class GuppyOverflowBoundaryTests
{
    // Match the generated facade defaults (46/49), not the spec-options defaults (50/55).
    private static Bar[] InputBars()
    {
        double[] levels = [double.MaxValue, -double.MaxValue, -double.MaxValue, 0, double.Epsilon, 1, 1, -1];
        return Enumerable.Range(0, 64).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), levels[i % 8], levels[i % 8], levels[i % 8], levels[i % 8], 1)).ToArray();
    }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static IReadOnlyDictionary<string, double[]> Reference(Bar[] bars) =>
        BuiltInFormulaReferences.RoundedGuppy(bars, ((IBuiltInIndicator)new GuppyMultipleMovingAverage()).CreateOptions());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicExecutionRejectsProvenOutputInsteadOfThrowingInsideTheMean(bool fallback)
    {
        var bars = InputBars(); var expected = Reference(bars)["SuperGmmaOsc"];
        var first = Array.FindIndex(expected, double.IsInfinity); Assert.True(first >= 0);
        var root = new GuppyMultipleMovingAverage();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        if (fallback) builder.ConfigureBehavior(_ => { });
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
        Assert.Equal(typeof(GuppyMultipleMovingAverage), error.IndicatorType);
        Assert.Equal(0, error.OutputSlot); Assert.Equal(first, error.BarIndex);
        Assert.Equal(expected[first], error.Value); Assert.Equal(IndicatorStartupPolicy.Finite, error.Policy);
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void OverflowPreviewDoesNotInvalidateALaterFiniteCommit()
    {
        var bars = InputBars(); var expected = Reference(bars)["SuperGmmaOsc"];
        var first = Array.FindIndex(expected, double.IsInfinity); Assert.True(first > 0);
        using var control = new GuppyMultipleMovingAverageState(length25: 46, length26: 49);
        using var probed = new GuppyMultipleMovingAverageState(length25: 46, length26: 49);
        OhlcvBar Native(Bar bar) => new("GUPPY", BarTimeframe.Minutes(1), bar.Time, bar.Time,
            bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
        foreach (var bar in bars.Take(first))
        { control.Update(Native(bar), true, true); probed.Update(Native(bar), true, true); }
        Assert.True(double.IsNaN(probed.Update(Native(bars[first]), false, true).Outputs!["SuperGmmaSignal"]));
        foreach (var price in new[] { double.MaxValue, -double.MaxValue, 1d, 0d, 1e100 })
        {
            var candidate = Native(new Bar(bars[first].Time, price, price, price, price, 1));
            var clean = control.Update(candidate, false, true).Outputs!;
            if (!clean.Values.All(double.IsFinite)) continue;
            var actual = probed.Update(candidate, true, true).Outputs!;
            foreach (var key in clean.Keys) Assert.Equal(clean[key], actual[key]);
            return;
        }
        Assert.Fail("The regression fixture must include a finite alternative after the overflow preview.");
    }

    [Fact]
    public void RawRoutesPreserveIndependentOverflowAndSignalInvalidation()
    {
        var bars = InputBars(); var expected = Reference(bars);
        Assert.Contains(expected["SuperGmmaOsc"], double.IsInfinity);
        var batch = Data(bars).CalculateGuppyMultipleMovingAverage(length25: 46, length26: 49);
        using var context = new ComputeContext();
        foreach (var (key, series) in new[] { ("SuperGmmaOsc", IndicatorCompute.MacdSeries.Line), ("SuperGmmaSignal", IndicatorCompute.MacdSeries.Signal) })
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            using var output = IndicatorCompute.ComputeGuppyMultipleMaFast(Data(bars), context, length25: 46, length26: 49, series: series);
            Assert.Equal(expected[key], output.ToArray());
        }
        using var state = new GuppyMultipleMovingAverageState(length25: 46, length26: 49);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var (bar, i) in bars.Select((bar, i) => (bar, i)))
            {
                var native = new OhlcvBar("GUPPY", BarTimeframe.Minutes(1), bar.Time, bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
                foreach (var commit in new[] { false, false, true })
                {
                    var outputs = state.Update(native, commit, true).Outputs!;
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], outputs[key]);
                }
            }
            state.Reset();
        }
    }
}
