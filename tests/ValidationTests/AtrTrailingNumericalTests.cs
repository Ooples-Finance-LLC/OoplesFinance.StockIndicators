using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AtrTrailingNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ATRTS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void HalfLineProjectionPreservesStartupTiesReversalsAndExtremeState()
    {
        foreach (var trendLength in new[] { 1, 3, 63 }) foreach (var rangeLength in new[] { 1, 2, 7 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var multiplier in new[] { -2d, 0, .5, double.MaxValue }) foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 16).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray(), trendLength, rangeLength, kind, multiplier);
        Check(Array.Empty<Bar>(), 63, 2, MovingAvgType.ExponentialMovingAverage, 3);
        var extreme = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, (i % 2 == 0 ? 1 : -1) * double.MaxValue, 1)).ToArray();
        Check(extreme, 3, 2, MovingAvgType.ExponentialMovingAverage, -double.MaxValue);
        var tie = new[] { new Bar(DateTime.UnixEpoch, 10, 11, 9, 10, 1) };
        Assert.Equal(6, Data(tie).CalculateAverageTrueRangeTrailingStops(length1: 1, length2: 1, factor: -2).CustomValuesList[0]);
    }
    private static void Check(Bar[] bars, int trendLength, int rangeLength, MovingAvgType kind, double multiplier)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.AtrTrailingOutputs(bars, trendLength, rangeLength, code, multiplier)["Atrts"];
        Assert.Equal(expected, Data(bars).CalculateAverageTrueRangeTrailingStops(kind, trendLength, rangeLength, multiplier).CustomValuesList);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAverageTrueRangeTrailingStopsFast(Data(bars), context, rangeLength, multiplier, kind, trendLength); Assert.Equal(expected, raw.Span.ToArray());
        if (trendLength == 63)
        {
            using var alias = IndicatorCompute.ComputeAtrTrailingStopsFast(Data(bars), context, rangeLength, multiplier, kind); Assert.Equal(expected, alias.Span.ToArray());
            if (code == 3)
            {
                var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray(); var close = bars.Select(b => b.Close).ToArray(); var core = new double[bars.Length];
                TrendCore.AtrTrailingStops(high, low, close, core, rangeLength, multiplier); Assert.Equal(expected, core);
                MovingAverageCore.AverageTrueRangeTrailingStops(close, high, low, core, rangeLength, multiplier); Assert.Equal(expected, core);
            }
        }
        using var state = new AverageTrueRangeTrailingStopsState(kind, trendLength, rangeLength, multiplier);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(new Bar(bars[i].Time, 3, 4, -2, -1, 7)), false, true);
                foreach (var final in new[] { false, false, true })
                { var output = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], output.Value); Assert.Equal(expected[i], output.Outputs!["Atrts"]); }
            }
        }
    }
    [Fact]
    public void SelectedClosesAndCustomerRangeThenTrendKeepTheirOrder()
    {
        var highs = new[] { 10d, 5, 7, 4 }; var lows = new[] { -2d, -2, 0, 0 }; var prices = new[] { 9d, -1, 6, 2 };
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 4, highs[i], lows[i], 4, 1)).ToArray();
        var expected = BuiltInFormulaReferences.AtrTrailingOutputs(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray(), 3, 2, 3, 1)["Atrts"];
        var data = Data(bars); data.SetCustomValues(prices.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAverageTrueRangeTrailingStopsFast(data, context, 2, 1, length1: 3); Assert.Equal(expected, raw.Span.ToArray());
        foreach (var batch in new[] { false, true })
        {
            var selected = Data(bars); selected.SetCustomValues(prices.ToList());
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 12d, 11, 8, 6 }, values); return new[] { 2d, 2, 2, 2 }; },
                (values, period) => { Assert.Equal(3, period); Assert.Equal(prices, values); return new[] { 0d, 0, 0, 0 }; } });
            var target = new[] { 9d, 1, 4, 4 };
            if (batch) Assert.Equal(target, selected.CalculateAverageTrueRangeTrailingStops(length1: 3, length2: 2, factor: 1).CustomValuesList);
            else { using var result = IndicatorCompute.ComputeAverageTrueRangeTrailingStopsFast(selected, context, 2, 1, length1: 3); Assert.Equal(target, result.Span.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void LegacyBatchOnlyAveragesPreserveTheSameStopProjection()
    {
        var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10 + i, 13 + i, 8 + i, 11 + i % 5, 1)).ToArray();
        var prices = bars.Select(b => b.Close).ToList(); var kind = MovingAvgType.HullMovingAverage;
        var trend = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 4, prices);
        var ranges = bars.Select((b, i) => Math.Max(b.High - b.Low, Math.Max(Math.Abs(b.High - bars[i == 0 ? 0 : i - 1].Close), Math.Abs(b.Low - bars[i == 0 ? 0 : i - 1].Close)))).ToList();
        var atr = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 3, ranges);
        var expected = new double[bars.Length]; var stop = prices[0];
        for (var i = 0; i < bars.Length; i++) { stop = prices[i] > trend[i] ? Math.Max(stop, prices[i] - 2 * atr[i]) : Math.Min(stop, prices[i] + 2 * atr[i]); expected[i] = stop; }
        var batch = Data(bars).CalculateAverageTrueRangeTrailingStops(kind, 4, 3, 2).CustomValuesList;
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAverageTrueRangeTrailingStopsFast(Data(bars), context, 3, 2, kind, 4);
        for (var i = 0; i < bars.Length; i++) { Assert.Equal(expected[i], batch[i], 12); Assert.Equal(expected[i], raw.Span[i], 12); }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceState()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new AverageTrueRangeTrailingStopsState(length1: 2, length2: 2); using var control = new AverageTrueRangeTrailingStopsState(length1: 2, length2: 2);
            var first = new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1); state.Update(Native(first), true, true); control.Update(Native(first), true, true);
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("ATRTS", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var next = Native(new Bar(DateTime.UnixEpoch.AddMinutes(1), 4, 5, 3, 4, 1)); Assert.Equal(control.Update(next, true, true).Value, state.Update(next, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AtrTrailingStops) || c.IndicatorType == typeof(AverageTrueRangeTrailingStops)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AtrTrailingOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
