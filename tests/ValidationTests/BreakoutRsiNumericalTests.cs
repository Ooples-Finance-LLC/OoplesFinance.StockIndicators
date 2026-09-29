using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BreakoutRsiNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(BreakoutRsi)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentPowerWindows(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.BreakoutRsiOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static Bar[] Candles(double priceScale = 1, double volumeScale = 1, int count = 31)
    {
        var random = new Random(613); return Enumerable.Range(0, count).Select(i => { var basis = random.Next(-4, 4); var open = basis + random.Next(0, 4); var close = basis + random.Next(0, 4); var low = Math.Min(open, close) - random.Next(0, 3); var high = Math.Max(open, close) + random.Next(0, 3); return new Bar(DateTime.UnixEpoch.AddMinutes(i), open * priceScale, high * priceScale, low * priceScale, close * priceScale, random.Next(-4, 5) * volumeScale); }).ToArray();
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, int volumeLength = 2)
    {
        var expected = BuiltInFormulaReferences.BreakoutRsiValues(bars, length, volumeLength); var batch = Data(bars).CalculateBreakoutRelativeStrengthIndex(length, volumeLength);
        Assert.Equal(expected.Outputs["Brsi"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Brsi"], batch.OutputValues["Brsi"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeBreakoutRsiFast(Data(bars), context, length, volumeLength); Assert.Equal(expected.Outputs["Brsi"], output.ToArray());
        using var state = new BreakoutRelativeStrengthIndexState(length, volumeLength); var window = new BreakoutRsiWindow(length, volumeLength);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles(count: 7)) { state.Update(Native(b), true, false); window.Next(BreakoutRsiWindow.Price(b.Open, b.High, b.Low, b.Close), b.Open, b.High, b.Low, b.Close, b.Volume, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i]; var noise = Candles(count: 1)[0]; state.Update(Native(noise), false, false); window.Next(BreakoutRsiWindow.Price(noise.Open, noise.High, noise.Low, noise.Close), noise.Open, noise.High, noise.Low, noise.Close, noise.Volume, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(b), final, true); Assert.Equal(expected.Outputs["Brsi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Brsi"]); var direct = window.Next(BreakoutRsiWindow.Price(b.Open, b.High, b.Low, b.Close), b.Open, b.High, b.Low, b.Close, b.Volume, final); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Signal); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideSignedPowerAndIndependentVolumeAndEvidenceWindowsStayBounded()
    {
        foreach (var priceScale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var volumeScale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) foreach (var length in new[] { 2, 5 }) foreach (var volumeLength in new[] { 1, 4 })
            Assert.All(Check(Candles(priceScale, volumeScale), length, volumeLength).Outputs["Brsi"], value => Assert.InRange(value, 0, 100));
    }
    [Fact]
    public void HandPowerMassesStrictTiesAndZeroLossPriorityKeepSeeds()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 0, 2, 0, 2, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, 2, 0, 2, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 2, 2, 0, 0, 1), new Bar(DateTime.UnixEpoch.AddMinutes(3), 2, 2, 0, 0, 1) };
        var result = Check(bars, 3, 1); Assert.Equal(new[] { 100d, 100, 50, 0 }, result.Outputs["Brsi"]); Assert.Equal(new[] { Signal.StrongBuy, Signal.None, Signal.StrongSell, Signal.Sell }, result.Signals);
        Assert.Equal(new[] { 100d, 100, 60, 50 }, Check(bars, 3, 2).Outputs["Brsi"]);
        var reversed = Check(bars.Select(b => new Bar(b.Time, b.Close, b.High, b.Low, b.Open, b.Volume)).ToArray(), 3, 1); Assert.Equal(new[] { 0d, 0, 50, 100 }, reversed.Outputs["Brsi"]); Assert.Equal(new[] { Signal.None, Signal.None, Signal.StrongBuy, Signal.Buy }, reversed.Signals);
        Assert.Equal(new[] { 100d, 100, 100 }, Check(Bars(new[] { 1d, 2, -3 }), 1, 1).Outputs["Brsi"]);
    }
    [Fact]
    public void TinyStrengthMustSurviveUntilHugeVolumeRescalesItsPower()
    {
        var u = Math.Pow(2, -52); Assert.Equal(.5 + u / 2, BreakoutRsiWindow.Price(1, 1, u, u)); var bars = new[] { new Bar(DateTime.UnixEpoch, 1, double.MaxValue, -double.MaxValue, 1 + u, double.MaxValue), new Bar(DateTime.UnixEpoch.AddMinutes(1), 1 + u, double.MaxValue, -double.MaxValue, 1, double.MaxValue) };
        Assert.Equal(new[] { 100d, 100d / 3 }, Check(bars, 2, 2).Outputs["Brsi"]);
        var tinyMean = new[] { new Bar(DateTime.UnixEpoch, double.Epsilon, 2 * double.Epsilon, 0, double.Epsilon, double.MaxValue), new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, 2 * double.Epsilon, 0, 2 * double.Epsilon, double.MaxValue) }; Check(tinyMean, 2, 2);
    }
    [Fact]
    public void BothExtremePeriodsGrowOnlyWithObservedHistory()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var position in new[] { 0, 1 })
        { var length = position == 0 ? period : 3; var volumeLength = position == 1 ? period : 2; Check(Array.Empty<Bar>(), length, volumeLength); Check(Candles(count: 9), length, volumeLength); }
    }
    [Fact]
    public void SelectedPricesReplaceTypicalPriceAndCloseWithoutAverageCallbacks()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var bars = Enumerable.Range(0, 6).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i % 2 == 0 ? 10 : -7)).ToArray();
        var effective = bars.Select((b, i) => new Bar(b.Time, b.Open, selected[i] >= b.Low && selected[i] <= b.High ? b.High : Math.Max(i == 0 ? selected[i] : selected[i - 1], selected[i]), selected[i] >= b.Low && selected[i] <= b.High ? b.Low : Math.Min(i == 0 ? selected[i] : selected[i - 1], selected[i]), selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.BreakoutRsiValues(effective, 3, 2, true); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateBreakoutRelativeStrengthIndex(3, 2); Assert.Equal(expected.Outputs["Brsi"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> forbidden = (_, _) => throw new InvalidOperationException("Breakout RSI has no average components."); using var armed = ComponentAverage.Arm(new[] { forbidden }); using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeBreakoutRsiFast(data, context, 3, 2); Assert.Equal(expected.Outputs["Brsi"], output.ToArray()); Assert.Equal(0, ComponentAverage.Requests);
        using var wrapped = new CustomInputState(new BreakoutRelativeStrengthIndexState(3, 2), b => selected[(int)(b.StartTime - DateTime.UnixEpoch).TotalMinutes]);
        for (var replay = 0; replay < 2; replay++) { wrapped.Reset(); for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true }) Assert.Equal(expected.Outputs["Brsi"][i], wrapped.Update(Native(bars[i]), final, true).Value); }
    }
    [Fact]
    public void ExpiredExtremePowerAndSignedVolumeCancellationRecover()
    {
        var wide = Candles(double.MaxValue / 16, double.MaxValue / 8, 5); var tail = Candles(count: 35); var all = wide.Concat(tail.Select((b, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i + 5), b.Open, b.High, b.Low, b.Close, b.Volume))).ToArray();
        var output = Check(all, 4, 3); var expected = Check(tail, 4, 3); Assert.Equal(expected.Outputs["Brsi"].Skip(12), output.Outputs["Brsi"].Skip(17)); Assert.Equal(expected.Signals.Skip(12), output.Signals.Skip(17));
        var cancellation = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 2, 0, 2, i % 2 == 0 ? double.MaxValue : -double.MaxValue)).ToArray(); Check(cancellation, 3, 2);
    }
    [Fact]
    public void SignalAccelerationAndBothLookbacksRemainIndependent()
    {
        var bars = Candles(count: 80); var first = Check(bars, 2, 1); var longer = Check(bars, 7, 4); Assert.NotEqual(first.Outputs["Brsi"], longer.Outputs["Brsi"]); Assert.NotEqual(Check(bars, 7, 1).Outputs["Brsi"], longer.Outputs["Brsi"]);
        Assert.Contains(Signal.StrongBuy, longer.Signals); Assert.Contains(Signal.StrongSell, longer.Signals); Assert.Contains(Signal.Buy, longer.Signals); Assert.Contains(Signal.Sell, longer.Signals);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceEitherWindowOrPreviousPower()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new BreakoutRelativeStrengthIndexState(3, 2); using var control = new BreakoutRelativeStrengthIndexState(3, 2);
            foreach (var bar in Candles(count: 4)) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Candles(count: 10)) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
