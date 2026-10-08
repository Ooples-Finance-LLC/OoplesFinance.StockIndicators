using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ImpulseResponseNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersImpulseResponse)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWindow(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = (EhlersImpulseResponseSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Eir", BuiltInFormulaReferences.ImpulseResponseValues(bars, o.Length, o.Bw, Kind(o.MaType)) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, MovingAvgType.WildersSmoothingMethod => 6, _ => 7 };
    private static void Check(Bar[] bars, int length, double bw, MovingAvgType kind)
    {
        var expected = BuiltInFormulaReferences.ImpulseResponseValues(bars, length, bw, Kind(kind));
        var batch = Data(bars).CalculateEhlersImpulseResponse(kind, length, bw); Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersImpulseResponseFast(Data(bars), context, length, bw, kind); Assert.Equal(expected, result.ToArray());
        using var state = new EhlersImpulseResponseState(kind, length, bw);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(-10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["Eir"]);
                }
            }
        }
    }
    [Fact]
    public void BandAndSmoothedStagesCoverExtremesAndPeriods()
    {
        foreach (var kind in new[] { MovingAvgType.EhlersHannMovingAverage, MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var length in new[] { 1, 2, 20, int.MaxValue })
        {
            var bars = Enumerable.Range(0, 24).Select(i => Candle(i < 15 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray();
            Check(bars, length, 1, kind); Check(Array.Empty<Bar>(), length, 1, kind);
        }
        foreach (var width in new[] { -double.MaxValue, 0, double.MaxValue }) Check(Enumerable.Range(0, 24).Select(i => Candle(i % 3)).ToArray(), 20, width, MovingAvgType.EhlersHannMovingAverage);
    }
    [Fact]
    public void FirstImpulsePreservesThreeStartupZerosAndHannMass()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i == 3 ? 1 : 0)).ToArray();
        var cosine = Math.Cos(.99); var decay = 1/cosine-Math.Sqrt(1/(cosine*cosine)-1); var drive = .5*(1-decay);
        var firstWeight = 1-Math.Cos(2*Math.PI*(1d/3)); var secondWeight = 1-Math.Cos(2*Math.PI*(2d/3));
        var numerator = ReferenceFraction.FromDouble(drive)*ReferenceFraction.FromDouble(firstWeight);
        var denominator = ReferenceFraction.FromDouble(firstWeight)+ReferenceFraction.FromDouble(secondWeight);
        var expected = BuiltInFormulaReferences.ImpulseResponseValues(bars, 1, 1, 7);
        Assert.All(expected.Take(3), value => Assert.Equal(0,value)); Assert.Equal((numerator/denominator).ToDouble(), expected[3]); Assert.True(expected[3]>0);
        Check(bars,1,1,MovingAvgType.EhlersHannMovingAverage);
    }
    [Fact]
    public void SelectedPricesDriveTheSmoothedImpulse()
    {
        var selected = Enumerable.Range(0, 30).Select(i => (double)(i % 7)).ToList(); var original = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.ImpulseResponseValues(selected.Select(v => Candle(v)).ToArray(), 20, 1, 7);
        var batch = Data(original); batch.SetCustomValues(selected); batch.CalculateEhlersImpulseResponse(); Assert.Equal(expected,batch.CustomValuesList);
        using var context = new ComputeContext(); var data = Data(original); data.SetCustomValues(selected);
        using var result = IndicatorCompute.ComputeEhlersImpulseResponseFast(data,context); Assert.Equal(expected,result.ToArray());
    }
    [Fact]
    public void NonfiniteBandwidthIsRejectedBeforeCalculation()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EhlersImpulseResponseState(bw:invalid));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateEhlersImpulseResponse(bw:invalid));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeEhlersImpulseResponseFast(Data(Array.Empty<Bar>()),context,bw:invalid));
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceBandOrSmoothing()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersImpulseResponseState(); using var control = new EhlersImpulseResponseState();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);
        }
    }
}
