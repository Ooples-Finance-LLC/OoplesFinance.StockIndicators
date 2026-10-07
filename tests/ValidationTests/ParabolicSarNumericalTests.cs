using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ParabolicSarNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SAR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ParabolicSar)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentTrendSegments(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ParabolicSarOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, double start = .02, double increment = .02, double maximum = .2)
    {
        var expected = BuiltInFormulaReferences.ParabolicSarValues(bars, start, increment, maximum); var data = Data(bars).CalculateParabolicSAR(start, increment, maximum);
        Assert.Equal(expected.Values, data.OutputValues["Sar"]); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeParabolicSarFast(Data(bars), context, start, increment, maximum); Assert.Equal(expected.Values, fast.ToArray());
        using var state = new ParabolicSARState(start, increment, maximum);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs!["Sar"]); } }
        }
        return expected.Values;
    }
    [Fact]
    public void WideStepsPreserveClampsReversalsAndAcceleration()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var start in new[] { 0d, .125, 1, double.MaxValue })
            Check(Enumerable.Range(0, 31).Select(i => { var price = (i % 9 - 4) * scale; return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + 2 * scale, price - scale, price, 1); }).ToArray(), start, start, start);
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.MaxValue }), .125, .125, .5);
        Check(Bars(new[] { 0d, 2, 4, 6, 8, 10, 12, 14, 3, 0, -2, -4, -8, -10, -12, 1, 3, 5, 7 }), .125, .125, .5);
        Check(Bars(new[] { 0d, 2, 2, 3, 4, 0, -2, -2, -3, -4, 0, 1, 2 }), .125, .125, .5);
    }
    [Fact]
    public void HandStopsKeepSeedEqualityTwoBarClampAndReversalExtremes()
    {
        var bars = new[] { 2d, 4, 6, 4, 2, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); Assert.Equal(new[] { 1d, 1, 1, 2.8, 7, 6.4, 1 }, Check(bars, .1, .1, .3));
        Assert.Equal(new[] { 1d, 1, 1, 2, 2 }, Check(Bars(new[] { 1d, 1, 2, 0, 1 }), .5, 0, .5));
        Assert.Equal(new[] { -2d, -2, -2, -2, -2, -2 }, Check(Bars(new[] { -2d, -2, -2, -2, 0, -2 }), .5, .25, 1));
        Assert.Equal(new[] { -2d, -2, -2, -2, 0, 0 }, Check(Bars(new[] { -2d, -2, 0, 0, -2, 0 }), .5, .25, 1));
        Check(Bars(new[] { 0d, 0, 2, 1, 0, -2, -2, 0, 1, 1, -1, 2 }), .5, .25, 1);
    }
    [Fact]
    public void EmptyZeroAccelerationAndOverflowingIncrementRemainDefined()
    {
        Assert.Empty(Check(Array.Empty<Bar>())); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), 0, 0, 0);
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, -double.MaxValue, 0, double.MaxValue }), 0, double.MaxValue, double.MaxValue);
        Check(Bars(new[] { 1d, 3, 5, 7, 9, 11, -2, -4 }), double.MaxValue / 2, double.MaxValue, double.MaxValue);
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveHighLowRangesAndSignals()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.ParabolicSarValues(effective, .02, .02, .2);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateParabolicSAR(); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); using var context = new ComputeContext(); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var fast = IndicatorCompute.ComputeParabolicSarFast(source, context); Assert.Equal(expected.Values, fast.ToArray());
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceExtremesOrAcceleration()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new ParabolicSARState(); using var control = new ParabolicSARState(); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
    [Fact]
    public void InvalidParametersRejectBeforeMutatingSelectedInput()
    {
        var cases = new[] { new[] { double.NaN, .02, .2 }, new[] { .02, double.PositiveInfinity, .2 }, new[] { .02, .02, double.NegativeInfinity }, new[] { -1d, .02, .2 }, new[] { .02, -1d, .2 }, new[] { .3, .02, .2 } };
        foreach (var values in cases)
        { var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateParabolicSAR(values[0], values[1], values[2])); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new ParabolicSARState(values[0], values[1], values[2])); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeParabolicSarFast(data, context, values[0], values[1], values[2])); Assert.Equal(selected, data.CustomValuesList); }
    }
}
