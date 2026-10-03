using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AdaptiveCandleNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void AdaptiveFiltersColdBoundaryAndExtendedRatiosMatchFractions()
    {
        foreach (var smooth in new[] { 1, 2, 7 })
        foreach (var stoch in new[] { 1, 4 })
        foreach (var signal in new[] { 2, 3 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
        {
            var bars = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray();
            Check(bars, kind, smooth, stoch, signal);
        }
        Check(Array.Empty<Bar>(), MovingAvgType.ExponentialMovingAverage, 2, 2, 2);
        Check(Enumerable.Range(0, 16).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), -double.MaxValue, double.MaxValue, -double.MaxValue, i % 2 == 0 ? double.MaxValue : 0, 1)).ToArray(), MovingAvgType.ExponentialMovingAverage, 2, 2, 2);
        Check(Enumerable.Range(0, 16).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i % 2 == 0 ? double.MaxValue : -double.MaxValue, double.Epsilon, 0, 0, 1)).ToArray(), MovingAvgType.ExponentialMovingAverage, 2, 2, 2);
        var hand = Data(Enumerable.Range(1, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 4, 0, i, 1)).ToArray()).CalculateAdaptiveErgodicCandlestickOscillator(smoothLength: 2, stochLength: 2, signalLength: 2);
        Assert.Equal(new[] { 25d, 50, 75 }, hand.OutputValues["Eco"]);
    }
    private static void Check(Bar[] bars, MovingAvgType kind, int smooth, int stoch, int signal)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.AdaptiveCandleOutputs(bars, smooth, stoch, signal, code);
        var batch = Data(bars).CalculateAdaptiveErgodicCandlestickOscillator(kind, smooth, stoch, signal); foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var outputSignal in new[] { false, true }) { using var raw = IndicatorCompute.ComputeAdaptiveErgodicCandlestickOscillatorFast(Data(bars), context, smooth, stoch, signal, kind, outputSignal); Assert.Equal(expected[outputSignal ? "Signal" : "Eco"], raw.Span.ToArray()); }
        using var state = new AdaptiveErgodicCandlestickOscillatorState(kind, smooth, stoch, signal);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            foreach (var final in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), final, true); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void SelectedClosesAndCustomerSignalPreserveTheirInputs()
    {
        var bars = Enumerable.Range(1, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 4, 0, i, 1)).ToArray();
        var prices = new[] { 3d, 1, 2 }; var data = Data(bars); data.SetCustomValues(prices.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAdaptiveErgodicCandlestickOscillatorFast(data, context, 2, 2); Assert.Equal(new[] { 75d, 25, 50 }, raw.Span.ToArray());
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 25d, 50, 75 }, values); return new[] { 7d, 8, 9 }; });
            if (batch) Assert.Equal(new[] { 7d, 8, 9 }, Data(bars).CalculateAdaptiveErgodicCandlestickOscillator(smoothLength: 2, stochLength: 2, signalLength: 2).OutputValues["Signal"]);
            else { using var result = IndicatorCompute.ComputeAdaptiveErgodicCandlestickOscillatorFast(Data(bars), context, 2, 2, 2, signal: true); Assert.Equal(new[] { 7d, 8, 9 }, result.Span.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new AdaptiveErgodicCandlestickOscillatorState(smoothLength: 2, stochLength: 2); using var control = new AdaptiveErgodicCandlestickOscillatorState(smoothLength: 2, stochLength: 2);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("AC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AdaptiveErgodicCandlestickOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AdaptiveCandleOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
