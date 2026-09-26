using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SwingNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void WilderBranchesAndCumulativeSignalPreserveAllScales()
    {
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
        foreach (var limit in new[] { 0d, double.Epsilon, 3d, double.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var bars = new[] { (2d, 4d, 1d, 3d), (4d, 8d, 2d, 7d), (6d, 7d, 1d, 2d), (2d, 12d, -4d, 3d), (3d, 3d, 3d, 3d), (-3d, -2d, -4d, -3d) }
                .Select((b, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), b.Item1 * scale, b.Item2 * scale, b.Item3 * scale, b.Item4 * scale, 1)).ToArray();
            var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
            var swing = BuiltInFormulaReferences.SwingOutputs(bars, false, 3, code, limit)["Si"];
            var expected = BuiltInFormulaReferences.SwingOutputs(bars, true, 3, code, limit);
            Assert.Equal(swing, Data(bars).CalculateSwingIndex(limit).CustomValuesList);
            var batch = Data(bars).CalculateAccumulativeSwingIndex(kind, 3, limit);
            foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeSwingIndexFast(Data(bars), context, limit);
            using var accumulated = IndicatorCompute.ComputeAccumulativeSwingIndexFast(Data(bars), context, limit, 3, kind);
            using var signal = IndicatorCompute.ComputeAccumulativeSwingIndexFast(Data(bars), context, limit, 3, kind, true);
            Assert.Equal(swing, raw.Span.ToArray()); Assert.Equal(expected["Asi"], accumulated.Span.ToArray()); Assert.Equal(expected["Signal"], signal.Span.ToArray());
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.OscillatorCore.SwingIndex(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, limit);
            Assert.Equal(swing, core);
            OoplesFinance.StockIndicators.Core.OscillatorCore.AccumulativeSwingIndex(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, limit);
            Assert.Equal(expected["Asi"], core);
            using var state = new AccumulativeSwingIndexState(kind, 3, limit); var single = new SwingIndexState(limit);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset(); single.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true })
                {
                    Assert.Equal(swing[i], single.Update(Native(bars[i]), final, true).Value);
                    var actual = state.Update(Native(bars[i]), final, true);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void EmptyCoreAndBatchInputsReturnEmptyOutputs()
    {
        var empty = Array.Empty<double>();
        OoplesFinance.StockIndicators.Core.OscillatorCore.SwingIndex(empty, empty, empty, empty, empty);
        OoplesFinance.StockIndicators.Core.OscillatorCore.AccumulativeSwingIndex(empty, empty, empty, empty, empty);
        Assert.Empty(Data(Array.Empty<Bar>()).CalculateSwingIndex().CustomValuesList);
        Assert.Empty(Data(Array.Empty<Bar>()).CalculateAccumulativeSwingIndex().CustomValuesList);
    }
    [Fact]
    public void CustomerSignalReceivesTheCumulativeSeries()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 1, 3, 0, 2, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 2, 5, 1, 4, 1) };
        var expected = BuiltInFormulaReferences.SwingOutputs(bars, true, 3, 1, 0)["Asi"];
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(3, period); Assert.Equal(expected, values); return new[] { 7d, 8 }; } });
            if (batch) Assert.Equal(new[] { 7d, 8 }, Data(bars).CalculateAccumulativeSwingIndex(length: 3).OutputValues["Signal"]);
            else { using var context = new ComputeContext(); using var value = IndicatorCompute.ComputeAccumulativeSwingIndexFast(Data(bars), context, 0, 3, MovingAvgType.SimpleMovingAverage, true); Assert.Equal(new[] { 7d, 8 }, value.Span.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        foreach (var cumulative in new[] { false, true })
        {
            IStreamingIndicatorState state = cumulative ? new AccumulativeSwingIndexState(length: 3) : new SwingIndexState();
            IStreamingIndicatorState control = cumulative ? new AccumulativeSwingIndexState(length: 3) : new SwingIndexState();
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("SW", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(SwingIndex) || c.IndicatorType == typeof(AccumulativeSwingIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.SwingOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
