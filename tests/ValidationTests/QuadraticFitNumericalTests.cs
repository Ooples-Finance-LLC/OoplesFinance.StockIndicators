using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class QuadraticFitNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void SlidingMomentsAndForecastsMatchIndependentNormalEquations()
    {
        foreach (var length in new[] { 1, 2, 3, 4, 7 })
        foreach (var horizon in new[] { 1, 2, 14 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 0d, 1, 4, 9, 16, 25, 36, 49 }, new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 0 }, new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0, 0, 0, 0 } })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.QuadraticFitOutputs(bars, length, horizon);
            var batch = Data(bars).CalculateQuadraticLeastSquaresMovingAverage(length: length, forecastLength: horizon);
            foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeQuadraticLeastSquaresMovingAverageFast(Data(bars), context, length, horizon);
            using var forecast = IndicatorCompute.ComputeQuadraticLeastSquaresMovingAverageFast(Data(bars), context, length, horizon, series: IndicatorCompute.QuadraticFitSeries.Forecast);
            Assert.Equal(expected["Qlma"], raw.Span.ToArray()); Assert.Equal(expected["Forecast"], forecast.Span.ToArray());
            var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.Get(MovingAvgType.QuadraticLeastSquaresMovingAverage)!.Compute(prices, core, length); Assert.Equal(expected["Qlma"], core);
            using var state = new QuadraticLeastSquaresMovingAverageState(length: length, forecastLength: horizon);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
        var polynomial = Data(BarsOf(new[] { 0d, 1, 4, 9, 16 })).CalculateQuadraticLeastSquaresMovingAverage(length: 3, forecastLength: 2);
        Assert.Equal(new[] { 0d, 0, 4, 9, 16 }, polynomial.OutputValues["Qlma"]);
        Assert.Equal(new[] { 0d, 0, 16, 25, 36 }, polynomial.OutputValues["Forecast"]);
    }
    [Fact]
    public void SelectedInputAndSixCustomerMomentsKeepTheirOrder()
    {
        var data = Data(BarsOf(new[] { 9d, 9, 9 })); data.SetCustomValues(new List<double> { 2, 4, 1 });
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeQuadraticLeastSquaresMovingAverageFast(data, context, 1);
        Assert.Equal(new[] { 2d, 4, 1 }, raw.Span.ToArray());
        var inputs = new[] { new[] { 2d, 4, 1 }, new[] { 0d, 1, 2 }, new[] { 0d, 1, 4 }, new[] { 0d, 4, 4 }, new[] { 0d, 4, 2 }, new[] { 0d, 1, 8 } };
        foreach (var batch in new[] { false, true })
        {
            var callbacks = inputs.Select(expected => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, period) => { Assert.Equal(2, period); Assert.Equal(expected, values); return new double[3]; })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks);
            if (batch) Assert.Equal(new double[3], Data(BarsOf(inputs[0])).CalculateQuadraticLeastSquaresMovingAverage(length: 2).CustomValuesList);
            else { using var result = IndicatorCompute.ComputeQuadraticLeastSquaresMovingAverageFast(Data(BarsOf(inputs[0])), context, 2); Assert.Equal(new double[3], result.Span.ToArray()); }
            Assert.Equal(6, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new QuadraticLeastSquaresMovingAverageState(length: 3); using var control = new QuadraticLeastSquaresMovingAverageState(length: 3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("QF", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(QuadraticLeastSquaresMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.QuadraticFitOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
