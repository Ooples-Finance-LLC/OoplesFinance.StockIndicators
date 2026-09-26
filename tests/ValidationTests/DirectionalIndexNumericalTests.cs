using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DirectionalIndexNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("DX", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void DirectionalTiesRangesAndSmoothingMatchIndependentFormulas()
    {
        foreach (var length in new[] { 1, 2, 3, 7 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
        {
            var bars = new[] { (2d, 4d, 1d, 3d), (4d, 8d, 2d, 7d), (6d, 7d, 1d, 2d), (2d, 12d, -4d, 3d), (3d, 3d, 3d, 3d), (-3d, -2d, -4d, -3d), (1d, 1d, -1d, 12d), (1d, 1d, -1d, -12d), (1d, 1d, -1d, 12d) }
                .Select((b, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), b.Item1 * scale, b.Item2 * scale, b.Item3 * scale, b.Item4 * scale, 1)).ToArray();
            Check(bars, kind, length);
        }
        foreach (var prices in new[] { Array.Empty<double>(), new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 0 }, new[] { 1d, 2, 3, 4, 5 } }) Check(BarsOf(prices), MovingAvgType.WildersSmoothingMethod, 3);
        var trend = Data(BarsOf(new[] { 1d, 2, 3 })).CalculateAverageDirectionalIndex(length: 1);
        Assert.Equal(new[] { 0d, 100, 100 }, trend.OutputValues["DiPlus"]); Assert.Equal(new[] { 0d, 0, 0 }, trend.OutputValues["DiMinus"]); Assert.Equal(new[] { 0d, 100, 100 }, trend.OutputValues["Adx"]);
    }
    private static void Check(Bar[] bars, MovingAvgType kind, int length)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.DirectionalIndexOutputs(bars, length, code);
        var batch = Data(bars).CalculateAverageDirectionalIndex(kind, length); foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAdxFast(Data(bars), context, length, kind);
        using var alias = IndicatorCompute.ComputeAverageDirectionalIndexFast(Data(bars), context, length, kind);
        using var plus = IndicatorCompute.ComputeDiPlusFast(Data(bars), context, length, kind); using var minus = IndicatorCompute.ComputeDiMinusFast(Data(bars), context, length, kind);
        Assert.Equal(expected["Adx"], raw.Span.ToArray()); Assert.Equal(expected["Adx"], alias.Span.ToArray()); Assert.Equal(expected["DiPlus"], plus.Span.ToArray()); Assert.Equal(expected["DiMinus"], minus.Span.ToArray());
        if (code == 6) { var core = new double[bars.Length]; OoplesFinance.StockIndicators.Core.OscillatorCore.AverageDirectionalIndex(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, length); Assert.Equal(expected["Adx"], core); }
        using var state = new AverageDirectionalIndexState(length, kind);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            foreach (var final in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), final, true); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void SelectedClosesAndFourCustomerStagesKeepTheirOrder()
    {
        var bars = BarsOf(new[] { 1d, 2, 3 }); var selected = new[] { 9d, 2.5, 5 };
        var selectedBars = new[] { new Bar(DateTime.UnixEpoch, 1, 10, 0, 1, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 2, 3, 2, 2, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 3, 6, 3, 3, 1) };
        var data = Data(selectedBars); data.SetCustomValues(selected.ToList());
        var expected = BuiltInFormulaReferences.DirectionalIndexOutputs(selectedBars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(), 1, 6);
        using var context = new ComputeContext(); using var plus = IndicatorCompute.ComputeDiPlusFast(data, context, 1); Assert.Equal(expected["DiPlus"], plus.Span.ToArray());
        foreach (var batch in new[] { false, true })
        {
            var inputs = new[] { new[] { 0d, 1, 1 }, new[] { 0d, 0, 0 }, new[] { 0d, 1, 1 }, new[] { 0d, 0, 0 } };
            var callbacks = inputs.Select(input => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, period) => { Assert.Equal(2, period); Assert.Equal(input, values); return new[] { 1d, 1, 1 }; })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks);
            if (batch) Assert.Equal(new[] { 1d, 1, 1 }, Data(bars).CalculateAverageDirectionalIndex(length: 2).CustomValuesList);
            else { using var value = IndicatorCompute.ComputeAdxFast(Data(bars), context, 2); Assert.Equal(new[] { 1d, 1, 1 }, value.Span.ToArray()); }
            Assert.Equal(4, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new AverageDirectionalIndexState(3); using var control = new AverageDirectionalIndexState(3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("DX", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Adx) || c.IndicatorType == typeof(AverageDirectionalIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.DirectionalIndexOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
