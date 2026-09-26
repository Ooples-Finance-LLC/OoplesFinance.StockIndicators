using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LightLeastSquaresNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void AlgebraicCancellationPreservesExtremeAndSubnormalPrices()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 20 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, -2, 0, 9, 9, 9, 9 }, new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, double.Epsilon, 1 }, new[] { double.Epsilon, -double.Epsilon, 3 * double.Epsilon, 0, 0, 0 }, Enumerable.Repeat(double.MaxValue, 24).ToArray() })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var bars = BarsOf(prices); var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
            var expected = BuiltInFormulaReferences.LightLeastSquaresOutputs(bars, length, code)["Llsma"];
            Assert.Equal(expected, Data(bars).CalculateLightLeastSquaresMovingAverage(kind, length).CustomValuesList);
            using var state = new LightLeastSquaresMovingAverageState(kind, length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            }
            if (kind == MovingAvgType.SimpleMovingAverage)
            {
                var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.Get(MovingAvgType.LightLeastSquaresMovingAverage)!.Compute(prices, core, length); Assert.Equal(expected, core);
                using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeLightLeastSquaresMovingAverageFast(Data(bars), context, length); Assert.Equal(expected, raw.Span.ToArray());
            }
        }
    }
    [Fact]
    public void HalfPeriodCapAndSelectedInputMatchTheBatchContract()
    {
        var prices = Enumerable.Range(0, 1100).Select(i => (double)(i % 13 - 6)).ToArray(); var bars = BarsOf(prices);
        var expected = BuiltInFormulaReferences.LightLeastSquaresOutputs(bars, new LightLeastSquaresMovingAverage(1063))["Llsma"];
        using var state = new LightLeastSquaresMovingAverageState(length: 1063);
        Assert.Equal(expected, bars.Select(b => state.Update(Native(b), true, true).Value));
        var data = Data(BarsOf(prices.Select(_ => 42d).ToArray())); data.SetCustomValues(prices.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeLightLeastSquaresMovingAverageFast(data, context, 1063);
        Assert.Equal(expected, raw.Span.ToArray()); Assert.Equal(expected, data.CalculateLightLeastSquaresMovingAverage(length: 1063).CustomValuesList);
        Assert.Equal(new[] { 0d, 2d, 4d }, Data(BarsOf(new[] { 1d, 3, 5 })).CalculateLightLeastSquaresMovingAverage(length: 2).CustomValuesList);
    }
    [Fact]
    public void CustomerAveragesReceiveTheirOwnInputsAndPeriods()
    {
        var prices = new[] { 1d, 2, 4, 8 }; var bars = BarsOf(prices);
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { Assert.Equal(3, period); Assert.Equal(prices, values); return new[] { 2d, 2, 2, 2 }; },
            (values, period) => { Assert.Equal(2, period); Assert.Equal(prices, values); return new[] { 4d, 4, 4, 4 }; },
            (values, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 0d, 1, 2, 3 }, values); return new[] { 0d, 0, 0, 0 }; } };
        var deviation = ReferenceFraction.FromDouble((new ReferenceFraction(2) / new ReferenceFraction(3)).SqrtToDouble());
        var expected = new[] { 2d, 2, (new ReferenceFraction(2) + new ReferenceFraction(4) / deviation).ToDouble(), (new ReferenceFraction(2) + new ReferenceFraction(6) / deviation).ToDouble() };
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch) Assert.Equal(expected, Data(bars).CalculateLightLeastSquaresMovingAverage(length: 3).CustomValuesList);
            else { using var actual = IndicatorCompute.ComputeLightLeastSquaresMovingAverageFast(Data(bars), context, 3); Assert.Equal(expected, actual.Span.ToArray()); }
            Assert.Equal(3, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new LightLeastSquaresMovingAverageState(length: 3);
            IStreamingIndicatorState control = new LightLeastSquaresMovingAverageState(length: 3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(LightLeastSquaresMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.LightLeastSquaresOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
