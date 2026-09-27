using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RangeIndicatorNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RANGE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Bars(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TheRangeIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeGainNormalization(IndicatorValidationCase c, string route)
    {
        var options = (TheRangeIndicatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); var kind = options.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 3;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RangeIndicatorOutputs(bars, options.Length, kind), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.RangeIndicatorOutputs(bars, length, k)["Tri"];
        Assert.Equal(expected, Data(bars).CalculateTheRangeIndicator(kind, length, 999).OutputValues["Tri"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeTheRangeIndicatorFast(Data(bars), context, length, kind); Assert.Equal(expected, raw.ToArray());
        using var state = new TheRangeIndicatorState(kind, length, 1);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false);
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            }
        }
    }
    [Fact]
    public void HandRangeUsesCurrentCloseSeedAndOnlyDividesRisingPrices()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Equal(new[] { 0d, 0, 200d / 3 }, BuiltInFormulaReferences.RangeIndicatorOutputs(bars, 2)["Tri"]); Check(bars, 2);
        Check(Bars(new[] { 10d, 12, 9, 9, 14, 5, 7, 3 }), 3);
    }
    [Fact]
    public void UnrepresentableRangeAndGainRatiosRemainBoundedAndRecover()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 7 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) Check(Bars(new[] { scale, -scale, scale, 0, 0, -scale, scale, 0, 0, 0, 0, 0 }), length, kind);
            var values = new[] { 0d, double.Epsilon, 2 * double.Epsilon, double.Epsilon, 1d, 0, 1, 0, 0, 2, 1, 3 };
            Check(values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, double.MaxValue, -double.MaxValue, v, 1)).ToArray(), length, kind);
            Check(Enumerable.Range(0, 48).Select(i => { var v = 0.5 + 0.4 * Math.Sin(i * 0.37); return new Bar(DateTime.UnixEpoch.AddDays(i), v, 1, 0, v, 1); }).ToArray(), length, kind);
            Check(Array.Empty<Bar>(), length, kind);
        }
    }
    [Fact]
    public void SelectedRawClosesKeepOriginalExtrema()
    {
        var selected = new[] { 1d, 3, 2, 5, 8, 3, 2, 7 }; var original = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddDays(i), 0, 10, -10, 0, 1)).ToArray();
        var projected = original.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RangeIndicatorOutputs(projected, 3)["Tri"];
        var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeTheRangeIndicatorFast(data, context, 3); Assert.Equal(expected, raw.ToArray());
        Assert.Equal(expected, data.CalculateTheRangeIndicator(length: 3).OutputValues["Tri"]);
    }
    [Fact]
    public void CustomerSmootherReceivesNormalizedRangeAndMainPeriodOnce()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + 1, v - 1, v, 1)).ToArray();
        foreach (var batch in new[] { false, true })
        {
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (input, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 0d, 0, 100 }, input); return new[] { 10d, 20, 30 }; };
            using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext();
            if (batch) Assert.Equal(new[] { 10d, 20, 30 }, Data(bars).CalculateTheRangeIndicator(length: 2, smoothLength: 999).OutputValues["Tri"]);
            else { using var raw = IndicatorCompute.ComputeTheRangeIndicatorFast(Data(bars), context, 2); Assert.Equal(new[] { 10d, 20, 30 }, raw.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceRatioHistoryOrSmoothing()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new TheRangeIndicatorState(length: 3); using var control = new TheRangeIndicatorState(length: 3);
            var seed = Native(Bars(new[] { 2d })[0]); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, 4, 3, 0, 8, 2, 9, 3, 0 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
