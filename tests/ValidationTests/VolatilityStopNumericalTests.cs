using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolatilityStopNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(VolatilityStop)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentTrendSegmentExtrema(IndicatorValidationCase c, string route)
    {
        var options = (VolatilityStopSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.VolatilityStopValues(bars, options.Length, options.Multiplier).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 3, double factor = 2)
    {
        var expected = BuiltInFormulaReferences.VolatilityStopValues(bars, length, factor); var batch = Data(bars).CalculateVolatilityStop(length, factor); Assert.Equal(new[] { "Vs" }, batch.OutputValues.Keys); Assert.Equal(expected.Outputs["Vs"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVolatilityStopFast(Data(bars), context, length, factor); Assert.Equal(expected.Outputs["Vs"], fast.ToArray()); var core = new double[bars.Length]; VolatilityCore.VolatilityStop(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, length, factor); Assert.Equal(expected.Outputs["Vs"], core);
        using var state = new VolatilityStopState(length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Vs"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Vs"]); } }
        }
        return expected.Outputs["Vs"];
    }
    [Fact]
    public void WideRangesPreserveStrictCrossingAndBothRatchets()
    {
        foreach (var length in new[] { 1, 2, 7 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var factor in new[] { -2d, 0, .5, 2 })
            Check(Enumerable.Range(0, 25).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, factor);
        var bars = new[] { new Bar(DateTime.UnixEpoch, 15, 16, 14, 15, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 5, 8, 2, 5, 1) }; Assert.Equal(new[] { 15d, 18 }, Check(bars, 1, 1));
    }
    [Fact]
    public void ExtendedAtrCancelsBeforePublicationAndNeverPoisonsZeroFactors()
    {
        var prices = new[] { 0d, -double.MaxValue, double.MaxValue, 0 }; var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, p, 1)).ToArray();
        Assert.Equal(new double[4], Check(bars, 1, .5)); Assert.Equal(prices, Check(bars, 1, 0)); Check(bars, 2, double.MaxValue); Check(Bars(new[] { double.Epsilon, 2 * double.Epsilon, 0d, double.Epsilon }), 2, double.MaxValue);
    }
    [Fact]
    public void HandCrossingsEqualityAndFirstPriceSeedRemainExplicit()
    {
        var bars = new[] { 2d, 4, 2, 2, 1, 3, 4 }.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 1, p - 1, p, 1)).ToArray(); Assert.Equal(new[] { 2d, 2, 2, 2, 3, 3, 2 }, Check(bars, 1, 1));
        Assert.Equal(new[] { 2d, 2, 5, 0 }, Check(new[] { 2d, 4, 1, 8 }.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 1, p - 1, p, 1)).ToArray(), 1, 1));
        var outside = new[] { new Bar(DateTime.UnixEpoch, 15, 11, 9, 15, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, 0, 0, 0, 1) }; Assert.Equal(new[] { 15d, 9 }, Check(outside, 2, 1));
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepConstantSizeHistory()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Array.Empty<Bar>(), length); Assert.All(Check(Bars(new double[9]), length), v => Assert.Equal(0d, v)); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length); }
    }
    [Fact]
    public void SelectedPricesDriveTrueRangeCrossingsAndSignals()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.VolatilityStopValues(effective, 3, 2);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateVolatilityStop(3); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Outputs["Vs"], data.CustomValuesList);
        var source = Data(bars); source.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeVolatilityStopFast(source, context, 3); Assert.Equal(expected.Outputs["Vs"], result.ToArray());
    }
    [Fact]
    public void PowerOfTwoScalingPreservesStopsAndCoreBoundsProtectOutput()
    {
        var prices = new[] { 2d, 4, 1, 8, 3, -2, 9, 0, 4 }; var expected = Check(Bars(prices), 2, .5); foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) Assert.Equal(expected.Select(v => v * scale), Check(Bars(prices.Select(v => v * scale)), 2, .5));
        var output = new[] { 7d, 8 }; Assert.Throws<ArgumentException>(() => VolatilityCore.VolatilityStop(new[] { 1d }, new[] { 1d, 2 }, new[] { 1d, 2 }, output)); Assert.Equal(new[] { 7d, 8 }, output); Assert.Throws<ArgumentException>(() => VolatilityCore.VolatilityStop(new[] { 1d, 2 }, new[] { 1d }, new[] { 1d, 2 }, output)); Assert.Equal(new[] { 7d, 8 }, output); Assert.Throws<ArgumentException>(() => VolatilityCore.VolatilityStop(new[] { 1d, 2 }, new[] { 1d, 2 }, new[] { 1d, 2 }, Array.Empty<double>()));
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRangeStopOrTrend()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new VolatilityStopState(2); using var control = new VolatilityStopState(2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
    [Fact]
    public void NonfiniteFactorsRejectBeforeMutatingSelectedPricesOrCoreOutput()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateVolatilityStop(multiplier: factor)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new VolatilityStopState(multiplier: factor)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeVolatilityStopFast(data, context, multiplier: factor)); Assert.Equal(selected, data.CustomValuesList); var output = new[] { 7d, 8 }; Assert.Throws<ArgumentOutOfRangeException>(() => VolatilityCore.VolatilityStop(new[] { 1d, 2 }, new[] { 1d, 2 }, new[] { 1d, 2 }, output, multiplier: factor)); Assert.Equal(new[] { 7d, 8 }, output);
        }
    }
}
