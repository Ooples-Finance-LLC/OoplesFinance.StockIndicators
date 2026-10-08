using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class InstantaneousTrendNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersInstantaneousTrendlineV2)).Select(c => new object[] { c });
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
        => BuiltInFormulaReferences.InstantaneousTrendOutputs(bars, ((EhlersInstantaneousTrendlineV2SpecOptions)((IBuiltInIndicator)indicator).CreateOptions()).Alpha);
    private static void Check(Bar[] bars, double alpha)
    {
        var expected = BuiltInFormulaReferences.InstantaneousTrendOutputs(bars, alpha);
        var batch = Data(bars).CalculateEhlersInstantaneousTrendlineV2(alpha);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            using var fast = IndicatorCompute.ComputeEhlersInstantaneousTrendlineV2Fast(Data(bars), context, alpha, key); Assert.Equal(expected[key], fast.ToArray());
        }
        var core = new double[bars.Length]; OscillatorCore.EhlersInstantaneousTrendlineV2(bars.Select(b => b.Close).ToArray(), core, alpha); Assert.Equal(expected["Eit"], core);
        var state = new EhlersInstantaneousTrendlineV2State(alpha);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var p = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Eit"][i], p.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], p.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void WidePricesAndCoefficientsPreserveStartupAndFeedback()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue}) foreach(var alpha in new[]{-double.MaxValue,0d,.07,1,2,double.MaxValue})
        {Check(Enumerable.Range(0,14).Select(i=>Candle(i<10?(i%3==0?-scale:scale):0)).ToArray(),alpha);Check(Array.Empty<Bar>(),alpha);}
    }
    [Fact]
    public void SeventhBarUsesStartupAndEighthBarUsesFeedback()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i == 6 ? 1 : 0)).ToArray(); var expected = BuiltInFormulaReferences.InstantaneousTrendOutputs(bars, 0);
        Assert.All(expected["Eit"].Take(6), v => Assert.Equal(0, v)); Assert.Equal(.25, expected["Eit"][6]); Assert.Equal(.5, expected["Eit"][7]); Check(bars, 0);
    }
    [Fact]
    public void WideSignalCancelsBeforePublicationAndFeedbackRecovers()
    {
        var bars = Enumerable.Range(0, 16).Select(i => Candle(i < 7 ? double.MaxValue : 0)).ToArray();
        var expected = BuiltInFormulaReferences.InstantaneousTrendOutputs(bars, 1);
        Assert.Equal(double.MaxValue, expected["Eit"][6]); Assert.Equal(double.MaxValue, expected["Signal"][6]);
        Assert.Equal(0, expected["Eit"][10]); Assert.Equal(0, expected["Signal"][12]); Check(bars, 1);
    }
    [Fact]
    public void UnpublishedOverflowRetainsLaterFiniteRecovery()
    {
        var bars = Enumerable.Range(0, 110).Select(i => Candle(i < 10 ? -double.MaxValue : i < 60 ? double.MaxValue : 0)).ToArray();
        var expected = BuiltInFormulaReferences.InstantaneousTrendOutputs(bars, .07);
        Assert.Contains(expected["Eit"], double.IsInfinity); Assert.True(double.IsFinite(expected["Eit"][100]));
        Assert.Contains(expected["Signal"], double.IsInfinity); Assert.True(double.IsFinite(expected["Signal"][100])); Check(bars, .07);
    }
    [Fact]
    public void SelectedPricesDriveBothOutputsAndTypedFactoryUsesAlpha()
    {
        var selected = Enumerable.Range(0, 30).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var alpha in new[] { 0d, .07, 1d, 2d })
        {
            var expected = BuiltInFormulaReferences.InstantaneousTrendOutputs(selected.Select(v => Candle(v)).ToArray(), alpha);
            var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersInstantaneousTrendlineV2(alpha);
            using var context = new ComputeContext();
            foreach (var key in expected.Keys)
            {
                Assert.Equal(expected[key], batch.OutputValues[key]); var data = Data(bars); data.SetCustomValues(selected);
                using var fast = IndicatorCompute.ComputeEhlersInstantaneousTrendlineV2Fast(data, context, alpha, key); Assert.Equal(expected[key], fast.ToArray());
                var spec = new IndicatorSpec(IndicatorName.EhlersInstantaneousTrendlineV2, new EhlersInstantaneousTrendlineV2SpecOptions(alpha), key);
                var dispatched = Data(bars); dispatched.SetCustomValues(selected); using var arm = IndicatorCompute.TryComputeFast(dispatched, spec, context); Assert.NotNull(arm); Assert.Equal(expected[key], arm.Value.ToArray());
                var state = StatefulIndicatorFactory.Create(spec); Assert.IsType<EhlersInstantaneousTrendlineV2State>(state);
                for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[key][i], state.Update(Native(Candle(selected[i])), true, true).Outputs![key]);
            }
        }
    }
    [Fact]
    public void NonfiniteAlphaIsRejectedBeforeCalculation()
    {
        foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EhlersInstantaneousTrendlineV2State(invalid));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateEhlersInstantaneousTrendlineV2(invalid));
            using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeEhlersInstantaneousTrendlineV2Fast(Data(Array.Empty<Bar>()),context,invalid));
        }
    }
    [Fact]
    public void InvalidFieldsLeaveTrendHistoryUnchanged()
    {
        foreach(var field in Enumerable.Range(0,5)) foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}) foreach(var final in new[]{false,true})
        {
            var state=new EhlersInstantaneousTrendlineV2State();var control=new EhlersInstantaneousTrendlineV2State();var seed=Native(Candle(2));state.Update(seed,true,false);control.Update(seed,true,false);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
            foreach(var bar in Enumerable.Range(0,12).Select(i=>Native(Candle(i%3))))Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);
        }
    }
}
