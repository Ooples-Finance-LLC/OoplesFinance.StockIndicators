using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ShapeshiftingNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void SignedWeightsAndZeroPaddingPreserveAllScales()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 20 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, -2, 0, 9, 9, 9, 9 }, new[] { double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 0, 0, 0, 0, 0 }, new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0 }, new[] { -4d, -2, -1, 1, -1, 2 } })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.ShapeshiftingOutputs(bars, length)["Sma"];
            Assert.Equal(expected, Data(bars).CalculateShapeshiftingMovingAverage(length).CustomValuesList);
            var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.Get(MovingAvgType.ShapeshiftingMovingAverage)!.Compute(prices, core, length);
            Assert.Equal(expected, core);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeShapeshiftingMovingAverageFast(Data(bars), context, length);
            Assert.Equal(expected, raw.Span.ToArray());
            using var state = new ShapeshiftingMovingAverageState(length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            }
        }
        // At length two the second weight vanishes, so the filter is identity.
        var sample = new[] { 1d, -2, double.Epsilon, double.MaxValue };
        Assert.Equal(sample, Data(BarsOf(sample)).CalculateShapeshiftingMovingAverage(2).CustomValuesList);
    }
    [Fact]
    public void SelectedInputFeedsTheFilter()
    {
        var data = Data(BarsOf(new[] { 9d, 9, 9 })); data.SetCustomValues(new List<double> { 1, 2, 4 });
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeShapeshiftingMovingAverageFast(data, context, 2);
        Assert.Equal(new[] { 1d, 2, 4 }, raw.Span.ToArray());
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new ShapeshiftingMovingAverageState(length: 3);
            IStreamingIndicatorState control = new ShapeshiftingMovingAverageState(length: 3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ShapeshiftingMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ShapeshiftingOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
