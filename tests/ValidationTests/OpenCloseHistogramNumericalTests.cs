using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class OpenCloseHistogramNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("OC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double open, double close) => new(DateTime.UnixEpoch, open, Math.Max(open, close), Math.Min(open, close), close, 1);

    [Fact]
    public void SeparateAveragesPreserveRoundingAndExtremeCancellation()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 } })
        foreach (var period in new[] { 1, 2, 3, 7 })
        foreach (var weighted in new[] { false, true })
        {
            IBuiltInIndicator indicator = new OCHistogram(period, weighted ? new Wma(period) : null);
            var kind = weighted ? MovingAvgType.WeightedMovingAverage : MovingAvgType.ExponentialMovingAverage;
            var bars = prices.Select((p, i) => Candle(i % 2 == 0 ? p : -p, p)).ToArray();
            var expected = BuiltInFormulaReferences.OpenCloseHistogramOutputs(bars, indicator)["OcHistogram"];
            Assert.Equal(expected, Data(bars).CalculateOCHistogram(kind, period).CustomValuesList);
            var selected = Data(bars.Select(b => Candle(b.Open, 42)).ToArray()); selected.CustomValuesList = prices.ToList();
            using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), "OcHistogram"), context);
            Assert.NotNull(actual); Assert.Equal(expected, actual.Value.ToArray());
            Assert.Equal(expected, selected.CalculateOCHistogram(kind, period).CustomValuesList);
            using var state = new OCHistogramState(kind, period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(Candle(double.MaxValue, -double.MaxValue)), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(Candle(-double.MaxValue, double.MaxValue)), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
        var equal = Enumerable.Repeat(Candle(double.MaxValue, double.MaxValue), 5).ToArray();
        Assert.All(Data(equal).CalculateOCHistogram(length: 2).CustomValuesList, value => Assert.Equal(0, value));
        var plain = new[] { Candle(1, 3), Candle(2, 8), Candle(4, 2) };
        Assert.Equal(new[] { 2d, 4, 0 }, Data(plain).CalculateOCHistogram(length: 2).CustomValuesList);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerOpenAndCloseAveragesAreIndependentAndOrdered()
    {
        var bars = new[] { Candle(1, 3), Candle(2, 8), Candle(4, 2) };
        var indicator = new OCHistogram(2, new ConstantAverage(5), new ConstantAverage(9));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.All(run[indicator].ToArray(), value => Assert.Equal(4, value));
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Open), values); return values.Select(_ => 5d).ToArray(); },
            (values, period) => { Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 9d).ToArray(); } };
        using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext(); var builtIn = (IBuiltInIndicator)indicator;
        using var actual = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), "OcHistogram"), context);
        Assert.NotNull(actual); Assert.Equal(2, ComponentAverage.Substitutions); Assert.All(actual.Value.ToArray(), value => Assert.Equal(4, value));
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new OCHistogramState(length: 3); using var control = new OCHistogramState(length: 3);
            foreach (var bar in new[] { Candle(1, 3), Candle(2, 8) }) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("OC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(Candle(2, 4)); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(OCHistogram)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.OpenCloseHistogramOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
