using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BetterVolumeNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(BetterVolumeIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAllocation(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.BetterVolumeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static Bar[] Candles(double priceScale = 1, double volumeScale = 1, int count = 48)
    {
        var random = new Random(612);
        return Enumerable.Range(0, count).Select(i => { var basis = random.Next(-4, 4); var open = basis + random.Next(0, 4); var close = basis + random.Next(0, 4); var low = Math.Min(open, close) - random.Next(0, 3); var high = Math.Max(open, close) + random.Next(0, 3); return new Bar(DateTime.UnixEpoch.AddMinutes(i), open * priceScale, high * priceScale, low * priceScale, close * priceScale, random.Next(-4, 5) * volumeScale); }).ToArray();
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 8, int lookback = 2)
    {
        var expected = BuiltInFormulaReferences.BetterVolumeValues(bars, length, lookback); var batch = Data(bars).CalculateBetterVolumeIndicator(length, lookback);
        Assert.Equal(expected.Outputs["Bvi"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Bvi"], batch.OutputValues["Bvi"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeBetterVolumeFast(Data(bars), context, length, lookback); Assert.Equal(expected.Outputs["Bvi"], output.ToArray());
        var state = new BetterVolumeIndicatorState(length, lookback); var window = new BetterVolumeWindow(length, lookback);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Candles(count: 7)) { state.Update(Native(bar), true, false); window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i]; var noise = Candles(count: 1)[0]; state.Update(Native(noise), false, false); window.Next(noise.Open, noise.High, noise.Low, noise.Close, noise.Volume, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(b), final, true); Assert.Equal(expected.Outputs["Bvi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Bvi"]); var direct = window.Next(b.Open, b.High, b.Low, b.Close, b.Volume, final); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Signal); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideAllocationRangesAndSignalsMatchIndependentRoundedStages()
    {
        foreach (var priceScale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var volumeScale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) foreach (var length in new[] { 2, 7 }) foreach (var lookback in new[] { 1, 4 })
        { var bars = Candles(priceScale, volumeScale); var expected = Check(bars, length, lookback); for (var i = 0; i < bars.Length; i++) Assert.InRange(Math.Abs(expected.Outputs["Bvi"][i]), 0, Math.Abs(bars[i].Volume)); }
    }
    [Fact]
    public void HandAllocationsDojiTiesAndZeroDenominatorKeepDefinedSeeds()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 0, 2, 0, 2, 10), new Bar(DateTime.UnixEpoch.AddMinutes(1), 2, 2, 0, 0, 10), new Bar(DateTime.UnixEpoch.AddMinutes(2), 0, 1, -1, 0, 10) };
        var expected = Check(bars, 1, 1); Assert.Equal(new[] { 10d, 0, 5 }, expected.Outputs["Bvi"]); Assert.Equal(new[] { Signal.Buy, Signal.Sell, Signal.None }, expected.Signals);
        Assert.Equal(0, Check(new[] { new Bar(DateTime.UnixEpoch, 0, 1, 1, 2, 10) }).Outputs["Bvi"][0]);
        var tiny = Math.Pow(2, -53); Assert.Equal(double.MaxValue, Check(new[] { new Bar(DateTime.UnixEpoch, -tiny, 1, -tiny, 1, double.MaxValue) }).Outputs["Bvi"][0]);
        Assert.Equal(-5, Check(new[] { new Bar(DateTime.UnixEpoch, 1, 1, 1, 1, -10) }).Outputs["Bvi"][0]);
        Assert.Equal(0, Check(new[] { new Bar(DateTime.UnixEpoch, 1, 1, 1, 1, double.Epsilon) }).Outputs["Bvi"][0]);
    }
    [Fact]
    public void FirstCloseAndBothTrueRangeGapsRetainTheirOwnAllocation()
    {
        var shapes = new[] { (1d, 3d, 0d, 2d), (11d, 14d, 10d, 13d), (-8d, -5d, -10d, -9d), (2d, 4d, 0d, 3d) };
        var bars = shapes.Select((b, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), b.Item1, b.Item2, b.Item3, b.Item4, 100)).ToArray();
        Assert.Equal(60, Check(new[] { new Bar(DateTime.UnixEpoch, 101, 103, 100, 102, 100) }).Outputs["Bvi"][0]);
        var expected = Check(bars, 3, 2).Outputs["Bvi"]; Assert.Equal(60, expected[0]); Assert.Equal(1200d / 22, expected[1]); Assert.Equal(2200d / 45, expected[2]);
    }
    [Fact]
    public void BothExtremePeriodsNormalizeAndGrowOnlyWithObservedHistory()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var position in new[] { 0, 1 })
        { var length = position == 0 ? period : 3; var lookback = position == 1 ? period : 2; Check(Array.Empty<Bar>(), length, lookback); Check(Candles(count: 13), length, lookback); }
    }
    [Fact]
    public void SelectedPricesKeepPerBarRangesOriginalOpensAndSignedVolume()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var bars = Enumerable.Range(0, 6).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i % 2 == 0 ? 10 : -7)).ToArray();
        var effective = bars.Select((b, i) => new Bar(b.Time, b.Open, selected[i] >= b.Low && selected[i] <= b.High ? b.High : Math.Max(i == 0 ? selected[i] : selected[i - 1], selected[i]), selected[i] >= b.Low && selected[i] <= b.High ? b.Low : Math.Min(i == 0 ? selected[i] : selected[i - 1], selected[i]), selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.BetterVolumeValues(effective, 3, 2); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateBetterVolumeIndicator(3, 2); Assert.Equal(expected.Outputs["Bvi"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> forbidden = (_, _) => throw new InvalidOperationException("Better Volume has no average components."); using var armed = ComponentAverage.Arm(new[] { forbidden }); using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeBetterVolumeFast(data, context, 3, 2); Assert.Equal(expected.Outputs["Bvi"], output.ToArray()); Assert.Equal(0, ComponentAverage.Requests);
    }
    [Fact]
    public void SignalHistoryPeriodsChangeSignalsWithoutChangingAllocation()
    {
        var bars = Candles(count: 128); var first = Check(bars, 2, 1); var longer = Check(bars, 9, 5); Assert.Equal(first.Outputs["Bvi"], longer.Outputs["Bvi"]); Assert.NotEqual(first.Signals, longer.Signals);
        Assert.Contains(Signal.Buy, longer.Signals); Assert.Contains(Signal.Sell, longer.Signals); Assert.Contains(Signal.None, longer.Signals);
        var lookbackOnly = Check(bars, 9, 1); Assert.NotEqual(lookbackOnly.Signals, longer.Signals);
    }
    [Fact]
    public void ExpiredWideMomentsAndZeroVolumeRecoverEverySignalWindow()
    {
        var wide = Candles(double.MaxValue / 16, double.MaxValue / 8, 5); var tail = Candles(count: 40); var all = wide.Concat(tail.Select((b, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i + 5), b.Open, b.High, b.Low, b.Close, b.Volume))).ToArray();
        var output = Check(all, 4, 3); var expected = Check(tail, 4, 3); Assert.Equal(expected.Outputs["Bvi"].Skip(10), output.Outputs["Bvi"].Skip(15)); Assert.Equal(expected.Signals.Skip(10), output.Signals.Skip(15));
        Check(Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 2, 0, i % 3, 0)).ToArray(), 3, 2);
    }
    [Fact]
    public void InvalidCandlesCannotAdvancePriceAllocationOrRangeHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new BetterVolumeIndicatorState(3, 2); var control = new BetterVolumeIndicatorState(3, 2);
            foreach (var bar in Candles(count: 4)) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Candles(count: 10)) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
