using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChebyshevWaveNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersChebyshevLowPassFilter)).Select(c => new object[] { c });
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
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator) => BuiltInFormulaReferences.ChebyshevWaveValues(bars);
    private static void Check(Bar[] bars)
    {
        var expected = BuiltInFormulaReferences.ChebyshevWaveValues(bars);
        var batch = Data(bars).CalculateEhlersChebyshevLowPassFilter(); using var context = new ComputeContext();
        for (var wave = 0; wave < 9; wave++)
        {
            var key = "Eclpf"+(wave-2); Assert.Equal(expected[key], batch.OutputValues[key]);
            using var result = IndicatorCompute.ComputeEhlersChebyshevLowPassFilterFast(Data(bars), context, (IndicatorCompute.ChebyshevWave)wave); Assert.Equal(expected[key], result.ToArray());
        }
        var state = new EhlersChebyshevLowPassFilterState();
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(-10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Eclpf-2"][i], point.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void EveryWaveRetainsExtendedUnpublishedStages()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            Check(Enumerable.Range(0, 48).Select(i => Candle(i < 24 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray());
            Check(Enumerable.Range(0, 48).Select(i => Candle(scale)).ToArray());
        }
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void FirstImpulseAndIndependentComplexPolesAgree()
    {
        var impulse = Enumerable.Range(0, 160).Select(i => Candle(i == 0 ? 1 : 0)).ToArray();
        var exact = BuiltInFormulaReferences.ChebyshevWaveValues(impulse);
        var alternate = BuiltInFormulaReferences.ChebyshevTrajectories(impulse.Select(b => b.Close).ToArray());
        double[][] coefficients = { new[] {1.907,.293,.063,.513,.451,.481}, new[] {1.777,.731,.166,.977,1.008,.561}, new[] {1.572,1.026,.282,.356,1.329,.644}, new[] {1.192,1.281,.426,-.384,1.565,.729}, new[] {.681,1.46,.543,-.966,1.703,.793}, new[] {.012,1.606,.65,-1.408,1.801,.848}, new[] {-.669,1.716,.74,-1.685,1.866,.89}, new[] {-1.226,1.8,.811,-1.842,1.91,.922}, new[] {-1.659,1.873,.878,-1.957,1.946,.951} };
        for (var wave = 0; wave < 9; wave++)
        {
            var key = "Eclpf"+(wave-2); var c = coefficients[wave];
            var gain = (1-c[1]+c[2])*(1-c[4]+c[5])/((2+c[0])*(2+c[3]));
            Assert.True(gain > 0); Assert.Equal(gain, exact[key][0]);
            var first = gain*c[0]+c[1]*gain; Assert.Equal((first+c[3]*gain)+c[4]*gain, exact[key][1]);
            // Independent complex-pole expansion is a rounded alternate representation; cancellation near
            // zero precludes relative-only comparison. The rational route checks above remain bit-exact.
            for (var i = 0; i < impulse.Length; i++) Assert.InRange(Math.Abs(exact[key][i]-alternate[key][i]), 0, 1e-12);
        }
        Check(impulse);
    }
    [Fact]
    public void SelectedPricesDriveEveryWave()
    {
        var selected = Enumerable.Range(0, 30).Select(i => (double)(i % 7)).ToList(); var original = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.ChebyshevWaveValues(selected.Select(v => Candle(v)).ToArray());
        var batch = Data(original); batch.SetCustomValues(selected); batch.CalculateEhlersChebyshevLowPassFilter(); using var context = new ComputeContext();
        for (var wave = 0; wave < 9; wave++)
        {
            var key = "Eclpf"+(wave-2); Assert.Equal(expected[key], batch.OutputValues[key]); var data = Data(original); data.SetCustomValues(selected);
            using var result = IndicatorCompute.ComputeEhlersChebyshevLowPassFilterFast(data, context, (IndicatorCompute.ChebyshevWave)wave); Assert.Equal(expected[key], result.ToArray());
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceAnyWave()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new EhlersChebyshevLowPassFilterState(); var control = new EhlersChebyshevLowPassFilterState();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3))))
            {
                var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true);
                foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]);
            }
        }
    }
}
