using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RickerNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactWindowsPreserveExtremeValuesAndPreviewReset()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 100, 203 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, 2, 8, -3, 0, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0, 0, 0 }, Enumerable.Repeat(double.MaxValue, 30).ToArray() })
        {
            var bars = BarsOf(prices); var indicator = new RightSidedRickerMovingAverage(length);
            var expected = BuiltInFormulaReferences.RickerOutputs(bars, indicator)["Rsrma"];
            Assert.Equal(expected, Data(bars).CalculateRightSidedRickerMovingAverage(length).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeRightSidedRickerMovingAverageFast(Data(bars), context, length); Assert.Equal(expected, raw.Span.ToArray());
            using var state = new RightSidedRickerMovingAverageState(length);
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
    public void SignedGaussianDerivativeNormalizesConstantInputs()
    {
        foreach (var length in new[] { 1, 3, 7, 20 })
        {
            var prices = Enumerable.Range(0, length + 7).Select(i => (i * 17 % 19) - 9d).ToArray();
            var actual = Data(BarsOf(prices)).CalculateRightSidedRickerMovingAverage(length).CustomValuesList;
            var width = .6 * length;
            var weights = Enumerable.Range(0, length).Select(j => (width * width - j * (double)j) * Math.Exp(-.5 * Math.Pow(j / width, 2))).ToArray();
            for (var i = 0; i < prices.Length; i++)
            {
                var expected = Enumerable.Range(0, Math.Min(length, i + 1)).Sum(j => prices[i - j] * weights[j]) / weights.Sum();
                Assert.InRange(Math.Abs(actual[i] - expected), 0, 2e-11);
            }
            Assert.Equal(50, Data(BarsOf(Enumerable.Repeat(50d, length).ToArray())).CalculateRightSidedRickerMovingAverage(length).CustomValuesList[^1]);
        }
    }
    [Fact]
    public void WidthLimitsRespectTheDeclaredKernel()
    {
        foreach (var width in new[] { 0, double.Epsilon, -double.Epsilon, .1, 100d / (3 * 38), 60, -60, double.MaxValue })
        {
            var bars = BarsOf(new[] { double.MaxValue, -double.MaxValue, 1d, 3, 0, 0, 0 });
            var expected = BuiltInFormulaReferences.RickerOutputs(bars, new RightSidedRickerMovingAverage(3), width)["Rsrma"];
            Assert.Equal(expected, Data(bars).CalculateRightSidedRickerMovingAverage(3, width).CustomValuesList);
            using var state = new RightSidedRickerMovingAverageState(3, width);
            var actual = bars.Select(b => state.Update(Native(b), true, true).Value).ToArray(); Assert.Equal(expected, actual);
            var core = new double[bars.Length]; OoplesFinance.StockIndicators.Core.MovingAverageCore.RightSidedRickerMovingAverage(bars.Select(b => b.Close).ToArray(), core, 3, width); Assert.Equal(expected, core);
        }
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new RightSidedRickerMovingAverageState(3, invalid));
    }
    [Fact]
    public void RawAndBatchRespectSelectedInput()
    {
        var selected = new[] { 1d, 3, -2, 4 }; var data = Data(BarsOf(new[] { 20d, 40, 30, 80 })); data.SetCustomValues(selected.ToList());
        var expected = BuiltInFormulaReferences.RickerOutputs(BarsOf(selected), new RightSidedRickerMovingAverage(3))["Rsrma"];
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeRightSidedRickerMovingAverageFast(data, context, 3);
        Assert.Equal(expected, raw.Span.ToArray()); Assert.Equal(expected, data.CalculateRightSidedRickerMovingAverage(3).CustomValuesList);
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new RightSidedRickerMovingAverageState(3);
            IStreamingIndicatorState control = new RightSidedRickerMovingAverageState(3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RightSidedRickerMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.RickerOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
