using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PolynomialCellNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactWindowsPreserveExtremeValuesAndPreviewReset()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 100, int.MaxValue })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, 2, 8, -3, 0, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0, 0, 0 }, Enumerable.Repeat(double.MaxValue, 30).ToArray() })
        {
            var bars = BarsOf(prices); var indicator = new PolynomialLeastSquaresMovingAverage(length);
            var expected = BuiltInFormulaReferences.PolynomialCellOutputs(bars, indicator)["Plsma"];
            Assert.Equal(expected, Data(bars).CalculatePolynomialLeastSquaresMovingAverage(length).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputePolynomialLeastSquaresMovingAverageFast(Data(bars), context, length); Assert.Equal(expected, raw.Span.ToArray());
            using var state = new PolynomialLeastSquaresMovingAverageState(length);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var seed in BarsOf(new[] { 99d, -31, 7 })) state.Update(Native(seed), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }
    [Fact]
    public void IntegratedTwoCellKernelMatchesAnalyticExample()
    {
        // F(.5) = .25 + sin(pi/2) + sin(pi)/2 + sin(3pi/2)/3 = 11/12.
        var bars = BarsOf(new[] { 12d, 0 }); var result = Data(bars).CalculatePolynomialLeastSquaresMovingAverage(2).CustomValuesList;
        Assert.Equal(11d, result[0], 12); Assert.Equal(1d, result[1], 12);
    }
    [Fact]
    public void CellWeightsAgreeWithIndependentSineDifferenceIdentity()
    {
        foreach (var length in new[] { 3, 7, 20, 100 })
        {
            var prices = Enumerable.Range(0, length + 7).Select(i => (i * 17 % 19) - 9d).ToArray();
            var actual = Data(BarsOf(prices)).CalculatePolynomialLeastSquaresMovingAverage(length).CustomValuesList;
            for (var i = 0; i < prices.Length; i++)
            {
                double expected = 0;
                for (var lag = 0; lag < length && lag <= i; lag++)
                {
                    // Integral of 2x + pi * sum(cos(k*pi*x)): no endpoint subtraction.
                    var weight = (2d * lag + 1) / ((double)length * length);
                    for (var k = 1; k <= 3; k++) weight += 2 * Math.Sin(k * Math.PI / (2d * length)) * Math.Cos(k * Math.PI * (2d * lag + 1) / (2d * length)) / k;
                    expected += prices[i - lag] * weight;
                }
                // Independent transcendental evaluations differ by rounding; exact
                // rational accumulation against the coefficient grid is tested separately.
                Assert.InRange(Math.Abs(actual[i] - expected), 0, 2e-11);
            }
        }
    }
    [Fact]
    public void RawAndBatchRespectSelectedInput()
    {
        var selected = new[] { 1d, 3, -2, 4 }; var data = Data(BarsOf(new[] { 20d, 40, 30, 80 })); data.SetCustomValues(selected.ToList());
        var expected = BuiltInFormulaReferences.PolynomialCellOutputs(BarsOf(selected), new PolynomialLeastSquaresMovingAverage(3))["Plsma"];
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputePolynomialLeastSquaresMovingAverageFast(data, context, 3);
        Assert.Equal(expected, raw.Span.ToArray()); Assert.Equal(expected, data.CalculatePolynomialLeastSquaresMovingAverage(3).CustomValuesList);
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new PolynomialLeastSquaresMovingAverageState(3);
            IStreamingIndicatorState control = new PolynomialLeastSquaresMovingAverageState(3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(PolynomialLeastSquaresMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.PolynomialCellOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
