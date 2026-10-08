using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RecursiveMedianOscillatorNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersRecursiveMedianOscillator)).Select(c => new object[] { c });
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
    private static Dictionary<string,double[]> Expected(IReadOnlyList<Bar> bars,IIndicator indicator)
    {var o=(EhlersRecursiveMedianOscillatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions();return new(){{"Ermo",BuiltInFormulaReferences.RecursiveMedianOscillatorValues(bars,o.Length1,o.Length2,o.Length3)}};}
    private static void Check(Bar[] bars,int length,int smoothing=12,int highPass=30)
    {
        var expected=BuiltInFormulaReferences.RecursiveMedianOscillatorValues(bars,length,smoothing,highPass);Assert.Equal(expected,Data(bars).CalculateEhlersRecursiveMedianOscillator(length,smoothing,highPass).CustomValuesList);
        using var context=new ComputeContext();using var fast=IndicatorCompute.ComputeEhlersRecursiveMedianOscillatorFast(Data(bars),context,length,smoothing,highPass);Assert.Equal(expected,fast.ToArray());
        using var state=new EhlersRecursiveMedianOscillatorState(length,smoothing,highPass);
        for(var replay=0;replay<2;replay++)
        {
            state.Update(Native(Candle(10)),true,false);state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(Candle(double.MaxValue)),false,false);
                foreach(var commit in new[]{false,false,true}){var p=state.Update(Native(bars[i]),commit,true);Assert.Equal(expected[i],p.Value);Assert.Equal(expected[i],p.Outputs!["Ermo"]);}
            }
        }
    }
    [Fact]
    public void ExtendedFeedbackHandlesExtremePricesAndIndependentPeriods()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue}) foreach(var period in new[]{-1,0,1,2,14,int.MaxValue})
        {
            var bars=Enumerable.Range(0,40).Select(i=>Candle(i<16?(i%3==0?-scale:scale):0)).ToArray();
            Check(bars,period);Check(bars,5,period);Check(bars,5,12,period);Check(Array.Empty<Bar>(),period,period,period);
            Assert.DoesNotContain(BuiltInFormulaReferences.RecursiveMedianOscillatorValues(bars,period),double.IsNaN);
        }
    }
    [Fact]
    public void FirstImpulseAndSubsequentRecoveryHaveExplicitValues()
    {
        var bars=new[]{Candle(1),Candle(0),Candle(0),Candle(0)};var first=2*Math.PI/12;var alpha1=(Math.Cos(first)+Math.Sin(first)-1)/Math.Cos(first);
        var second=1/Math.Sqrt(2)*2*Math.PI/30;var alpha2=(Math.Cos(second)+Math.Sin(second)-1)/Math.Cos(second);
        var expected=BuiltInFormulaReferences.RecursiveMedianOscillatorValues(bars,1);Assert.Equal(alpha1*Math.Pow(1-alpha2/2,2),expected[0],14);Check(bars,1);
        var extreme=Enumerable.Range(0,120).Select(i=>Candle(i<20?(i%2==0?double.MaxValue:-double.MaxValue):0)).ToArray();Check(extreme,1,1,int.MaxValue);
        Assert.True(double.IsFinite(BuiltInFormulaReferences.RecursiveMedianOscillatorValues(extreme,1,1,int.MaxValue)[^1]));
        var random=new Random(533);Check(Enumerable.Range(0,256).Select(_=>Candle(random.Next(-4,5))).ToArray(),17,7,22);
    }
    [Fact]
    public void SelectedPricesDriveMedianAndFeedback()
    {
        var selected=Enumerable.Range(0,30).Select(i=>(double)(i%7)).ToList();var original=selected.Select(_=>Candle(100)).ToArray();var expected=BuiltInFormulaReferences.RecursiveMedianOscillatorValues(selected.Select(v=>Candle(v)).ToArray(),5);
        var batch=Data(original);batch.SetCustomValues(selected);batch.CalculateEhlersRecursiveMedianOscillator();Assert.Equal(expected,batch.CustomValuesList);
        using var context=new ComputeContext();var data=Data(original);data.SetCustomValues(selected);using var fast=IndicatorCompute.ComputeEhlersRecursiveMedianOscillatorFast(data,context);Assert.Equal(expected,fast.ToArray());
    }
    [Fact]
    public void RejectedFieldsPreserveMedianAndFeedbackHistory()
    {
        foreach(var field in Enumerable.Range(0,5)) foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}) foreach(var final in new[]{false,true})
        {
            using var state=new EhlersRecursiveMedianOscillatorState();using var control=new EhlersRecursiveMedianOscillatorState();var seed=Native(Candle(2));state.Update(seed,true,false);control.Update(seed,true,false);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
            foreach(var bar in Enumerable.Range(0,12).Select(i=>Native(Candle(i%3))))Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);
        }
    }
}
