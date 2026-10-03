using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class GrandForecastNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(GrandTrendForecasting)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLaggedForecast(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.GrandForecastOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly string[] Keys = { "Gtf", "UpperBand", "MiddleBand", "LowerBand" };
    private static Bar[] Bars(params double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, int horizon = 5, double mult = 2)
    {
        var expected = BuiltInFormulaReferences.GrandForecastValues(bars, length, horizon, mult); var batch = Data(bars).CalculateGrandTrendForecasting(length, horizon, mult);
        Assert.Equal(expected.Outputs["Gtf"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in Keys)
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeGrandTrendForecastingFast(Data(bars), context, length, horizon, mult, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new GrandTrendForecastingState(length, horizon, mult); var window = new GrandForecastWindow(length, horizon, mult);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(8, -3, 1, 5)) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(-999)[0]), false, false); window.Next(999, false);
                foreach (var final in new[] { false, false, true })
                { var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Gtf"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); Assert.Equal(point.Value, direct.Trend); Assert.Equal(expected.Signals[i], direct.Trade); }
            }
        }
        return expected;
    }
    [Fact]
    public void HandStartupForecastErrorsAndPartialMeans()
    {
        var first = Check(Bars(2), 1, 1); Assert.Equal(1.8, first.Outputs["Gtf"][0]); Assert.Equal(3.6, first.Outputs["MiddleBand"][0]); Assert.Equal(7.6, first.Outputs["UpperBand"][0]); Assert.Equal(3.6 - 4, first.Outputs["LowerBand"][0]); Assert.Equal(Signal.None, first.Signals[0]);
        var result = Check(Bars(10, 20, 30, 40), 2, 2, 1);
        var expected = new Dictionary<string, double[]> { ["Gtf"] = new[] { 9d, 13.5, 14.1, 14.3 }, ["MiddleBand"] = new[] { 18d, 36, 11.4, 18.8 }, ["UpperBand"] = new[] { 28d, 51, 27.4, 26.8 }, ["LowerBand"] = new[] { 8d, 21, -4.6, 10.8 } };
        foreach (var key in Keys) for (var i = 0; i < 4; i++) Assert.Equal(expected[key][i], result.Outputs[key][i], 12);
        Assert.Equal(new[] { Signal.None, Signal.None, Signal.StrongBuy, Signal.StrongBuy }, result.Signals);
        foreach (var values in Check(Bars(0, 0, 0, 0), 2, 3).Outputs.Values) Assert.All(values, v => Assert.Equal(0, v));
    }
    [Fact]
    public void WideSubnormalLaggedStagesRecoverAfterOverflow()
    {
        foreach (var periods in new[] { (1, 1), (2, 5), (5, 2), (3, 7) }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Bars(Enumerable.Range(0, 27).Select(i => (i % 9 - 4) * scale).ToArray()), periods.Item1, periods.Item2);
        var wide = Check(Bars(new[] { double.MaxValue, -double.MaxValue, double.MaxValue }.Concat(Enumerable.Repeat(0d, 80)).ToArray()), 1, 2);
        Assert.True(wide.Outputs.Values.Any(values => values.Any(double.IsInfinity))); foreach (var values in wide.Outputs.Values) { Assert.All(values, v => Assert.False(double.IsNaN(v))); Assert.True(double.IsFinite(values[^1])); }
        Check(Bars(double.Epsilon, double.MaxValue, -double.MaxValue, double.Epsilon, 0, 1, 2, 1), 2, 3, double.Epsilon);
    }
    [Fact]
    public void MultiplierProductsRemainCompleteAndZeroBandsCollapse()
    {
        foreach (var mult in new[] { 0d, double.Epsilon, .5, 2, double.MaxValue }) Check(Bars(double.Epsilon, 2 * double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, 2), 2, 3, mult);
        var zero = Check(Bars(double.MaxValue, -double.MaxValue, 1), 1, 1, 0); Assert.Equal(zero.Outputs["MiddleBand"], zero.Outputs["UpperBand"]); Assert.Equal(zero.Outputs["MiddleBand"], zero.Outputs["LowerBand"]);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    { foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var horizon in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Bars(4, -2, 7), length, horizon); Check(Array.Empty<Bar>(), length, horizon); } }
    [Fact]
    public void LagExpiryAndCompactionRetainCorrectForecastHistory()
    {
        var bars = Bars(Enumerable.Range(0, 2080).Select(i => (double)((i * 7) % 13 - 6)).ToArray());
        foreach (var periods in new[] { (2, 7), (7, 2) })
        { var expected = BuiltInFormulaReferences.GrandForecastValues(bars, periods.Item1, periods.Item2, 2); var window = new GrandForecastWindow(periods.Item1, periods.Item2, 2); for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, true }) { var point = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Gtf"][i], point.Trend); Assert.Equal(expected.Outputs["MiddleBand"][i], point.Middle); Assert.Equal(expected.Outputs["UpperBand"][i], point.Upper); Assert.Equal(expected.Outputs["LowerBand"][i], point.Lower); Assert.Equal(expected.Signals[i], point.Trade); } }
    }
    [Fact]
    public void SelectedPricesAndZeroCallbackContractCoverEveryOutput()
    {
        var bars = Candles((6, -3, 1), (10, -2, 4), (5, -4, -1), (8, -3, 2)); var selected = new[] { 20d, -10, 0, 3 }; var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.GrandForecastValues(projected, 2, 3, 2);
        using var armed = ComponentAverage.Arm((v, n) => throw new InvalidOperationException("No component-average slots"));
        var data = Data(bars); data.SetCustomValues(selected.ToList()); data.CalculateGrandTrendForecasting(2, 3, 2); Assert.Equal(expected.Signals, data.SignalsList);
        foreach (var key in Keys)
        { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var fastData = Data(bars); fastData.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeGrandTrendForecastingFast(fastData, context, 2, 3, 2, key); Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(selected, fastData.ChainedValues); Assert.Equal(bars.Select(b => b.Close), fastData.ClosePrices); }
        Assert.Equal(0, ComponentAverage.Requests);
    }
    [Fact]
    public void InvalidMultiplierIsRejectedOnAllRoutesIncludingEmptyInput()
    {
        foreach (var mult in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ((IBuiltInIndicator)new GrandTrendForecasting(2, 3, mult)).CreateOptions()); Assert.Throws<ArgumentOutOfRangeException>(() => new GrandTrendForecastingState(2, 3, mult));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateGrandTrendForecasting(2, 3, mult));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeGrandTrendForecastingFast(Data(Array.Empty<Bar>()), context, 2, 3, mult));
        }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new GrandTrendForecastingState(length: 3); using var control = new GrandTrendForecastingState(length: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["LowerBand"], actual.Outputs!["LowerBand"]); }
        }
    }
}
