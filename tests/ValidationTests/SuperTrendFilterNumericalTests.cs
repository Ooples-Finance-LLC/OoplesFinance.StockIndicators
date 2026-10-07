using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SuperTrendFilterNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SuperTrendFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStopSegments(IndicatorValidationCase c, string route)
    {
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Stf", BuiltInFormulaReferences.SuperTrendFilterOutputs(bars, (IBuiltInIndicator)c.Factory()) } }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 3, double factor = .9)
    {
        var expected = BuiltInFormulaReferences.SuperTrendFilterValues(bars, length, factor); var batch = Data(bars).CalculateSuperTrendFilter(length, factor); Assert.Equal(expected.Values, batch.OutputValues["Stf"]); Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeSuperTrendFilterFast(Data(bars), context, length, factor); Assert.Equal(expected.Values, fast.ToArray()); var state = new SuperTrendFilterState(length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs!["Stf"]); } }
        }
        return expected.Values;
    }
    [Fact]
    public void WideFeedbackKeepsBandsBlendsDirectionsAndSignalsExact()
    {
        foreach (var length in new[] { 1, 3, 13 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var factor in new[] { -2d, 0, .5, .9, 1, 2 })
            Check(Bars(Enumerable.Range(0, 23).Select(i => (i % 7 - 3) * scale)), length, factor);
    }
    [Fact]
    public void HandFeedbackUsesPreviousBandsAndRetainsEqualityDirection()
    {
        Assert.Equal(new[] { 2d, 4, 7, 7 }, Check(Bars(new[] { 2d, 4, 1, 8 }), 1, 0));
        Assert.Equal(new[] { -4d, -4, -4, -2 }, Check(Bars(new[] { -4d, -4, -4, -2 }), 1, 0));
        Assert.Equal(new[] { -4d, -2, -2, -4 }, Check(Bars(new[] { -4d, -2, -2, -4 }), 1, 0));
        Assert.Equal(new[] { 0d, 0, 0 }, Check(Bars(new[] { 0d, 0, 0 }), 1, .9));
        foreach (var initial in new[] { -2d, 0, 2 }) { Assert.Equal(new[] { initial }, Check(Bars(new[] { initial }), 1, 0)); Assert.Equal(Signal.None, Data(Bars(new[] { initial })).CalculateSuperTrendFilter(1, 0).SignalsList[0]); }
        Check(Bars(new[] { -4d, -2, -2, -4, -4, 0, 0, 2, -4, -4, 4, 0, -2 }), 1, 0);
    }
    [Fact]
    public void UnpublishedOverflowAndInfiniteOutputDoNotPoisonLaterFeedback()
    {
        var max = double.MaxValue; var power = Math.Pow(2, 1023); Assert.Equal(new[] { power, -power, double.NegativeInfinity, power }, Check(Bars(new[] { power, -power, 0d, power }), 1, 0));
        foreach (var factor in new[] { 0d, 1, double.MaxValue, -double.MaxValue }) Check(Bars(new[] { max, -max, 0d, max, 0, -max, 1 }), 3, factor);
    }
    [Fact]
    public void EmptyAndExtremePeriodsClampWithoutAllocatingPeriodHistory()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Array.Empty<Bar>(), length); var bars = Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }); var actual = Check(bars, length); if (length < 1) Assert.Equal(Check(bars, 1), actual); }
    }
    [Fact]
    public void SelectedPricesDriveFeedbackAndSignalsWithoutCandleSubstitution()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray(); var expected = BuiltInFormulaReferences.SuperTrendFilterValues(Bars(prices), 3, .9);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateSuperTrendFilter(3); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Values, data.CustomValuesList); using var context = new ComputeContext(); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var result = IndicatorCompute.ComputeSuperTrendFilterFast(source, context, 3); Assert.Equal(expected.Values, result.ToArray()); Assert.Equal(bars.Select(b => b.Close), source.ClosePrices);
    }
    [Fact]
    public void PowerOfTwoRescalingPreservesFeedbackAndPreviewState()
    {
        var prices = new[] { 2d, 4, -1, 8, 0, -8, 2, 3, -4 }; foreach (var factor in new[] { 0d, .5, 1 })
        { var baseline = Check(Bars(prices), 3, factor); foreach (var scale in new[] { Math.Pow(2, -900), Math.Pow(2, 900) }) Assert.Equal(baseline.Select(v => v * scale), Check(Bars(prices.Select(v => v * scale)), 3, factor)); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceFeedbackBandsOrDirection()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new SuperTrendFilterState(2); var control = new SuperTrendFilterState(2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
    [Fact]
    public void NonfiniteFactorsRejectBeforeMutatingSelectedPrices()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateSuperTrendFilter(factor: factor)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new SuperTrendFilterState(factor: factor)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeSuperTrendFilterFast(data, context, factor: factor)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
}
