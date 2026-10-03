using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AtrNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ATR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void TrueRangeStartupAveragesAndOverflowRecoveryMatchFractions()
    {
        foreach (var length in new[] { 1, 2, 7 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
                Check(Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray(), length, kind);
            var extremes = Enumerable.Range(0, 16).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, i < 3 ? double.MaxValue : 0, i < 3 ? -double.MaxValue : 0, 0, 1)).ToArray();
            Check(extremes, length, kind);
        }
        Check(Array.Empty<Bar>(), 2, MovingAvgType.WildersSmoothingMethod);
        var bar = new[] { new Bar(DateTime.UnixEpoch, 10, 11, 9, 10, 1) };
        Assert.Equal(1, Data(bar).CalculateAverageTrueRange(length: 2).CustomValuesList[0]);
    }
    private static void Check(Bar[] bars, int length, MovingAvgType kind)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.AtrOutputs(bars, length, code)["Atr"];
        Assert.Equal(expected, Data(bars).CalculateAverageTrueRange(kind, length).CustomValuesList);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAtrFast(Data(bars), context, length, kind); Assert.Equal(expected, raw.Span.ToArray());
        if (code == 6)
        {
            var core = new double[bars.Length]; VolatilityCore.AverageTrueRange(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, length); Assert.Equal(expected, core);
        }
        using var state = new AverageTrueRangeState(length, kind);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true })
            {
                var output = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], output.Value); Assert.Equal(expected[i], output.Outputs!["Atr"]);
            }
        }
    }
    [Fact]
    public void SelectedClosesAndCustomerAverageReceiveActualTrueRanges()
    {
        var highs = new[] { 10d, 5, 7, 4 }; var lows = new[] { -2d, -2, 0, 0 }; var prices = new[] { 9d, -1, 6, 2 };
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 4, highs[i], lows[i], 4, 1)).ToArray();
        var data = Data(bars); data.SetCustomValues(prices.ToList());
        var expected = BuiltInFormulaReferences.AtrOutputs(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray(), 2, 6)["Atr"];
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAtrFast(data, context, 2); Assert.Equal(expected, raw.Span.ToArray());
        foreach (var batch in new[] { false, true })
        {
            var selected = Data(bars); selected.SetCustomValues(prices.ToList());
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 12d, 11, 8, 6 }, values); return new[] { 7d, 8, 9, 10 }; } });
            if (batch) Assert.Equal(new[] { 7d, 8, 9, 10 }, selected.CalculateAverageTrueRange(length: 2).CustomValuesList);
            else { using var result = IndicatorCompute.ComputeAtrFast(selected, context, 2); Assert.Equal(new[] { 7d, 8, 9, 10 }, result.Span.ToArray()); }
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
            using var state = new AverageTrueRangeState(2); using var control = new AverageTrueRangeState(2);
            var first = new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1); state.Update(Native(first), true, true); control.Update(Native(first), true, true);
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("ATR", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            Assert.Equal(control.Update(Native(first), true, true).Value, state.Update(Native(first), true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Atr) || c.IndicatorType == typeof(AverageTrueRange)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AtrOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
