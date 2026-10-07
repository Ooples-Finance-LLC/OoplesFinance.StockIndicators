using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RecursiveMedianNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersRecursiveMedianFilter)).Select(c => new object[] { c });
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
    {var o=(EhlersRecursiveMedianFilterSpecOptions)((IBuiltInIndicator)indicator).CreateOptions();return new(){{"Ermf",BuiltInFormulaReferences.RecursiveMedianValues(bars,o.Length)}};}
    private static void Check(Bar[] bars,int length,int smoothing=12)
    {
        var expected=BuiltInFormulaReferences.RecursiveMedianValues(bars,length,smoothing);Assert.Equal(expected,Data(bars).CalculateEhlersRecursiveMedianFilter(length,smoothing).CustomValuesList);
        using var context=new ComputeContext();using var fast=IndicatorCompute.ComputeEhlersRecursiveMedianFilterFast(Data(bars),context,length,smoothing);Assert.Equal(expected,fast.ToArray());
        using var state=new EhlersRecursiveMedianFilterState(length,smoothing);
        for(var replay=0;replay<2;replay++)
        {
            state.Update(Native(Candle(10)),true,false);state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(Candle(double.MaxValue)),false,false);
                foreach(var commit in new[]{false,false,true}){var p=state.Update(Native(bars[i]),commit,true);Assert.Equal(expected[i],p.Value);Assert.Equal(expected[i],p.Outputs!["Ermf"]);}
            }
        }
    }
    [Fact]
    public void ExactMedianAndFeedbackHandleExtremePricesAndIndependentPeriods()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue}) foreach(var length in new[]{-1,0,1,2,5,14,int.MaxValue}) foreach(var smoothing in new[]{-1,0,1,7,12,int.MaxValue})
        {
            var bars=Enumerable.Range(0,24).Select(i=>Candle(i%3==0?-scale:scale)).ToArray();Check(bars,length,smoothing);Check(Array.Empty<Bar>(),length,smoothing);
            Assert.All(BuiltInFormulaReferences.RecursiveMedianValues(bars,length,smoothing),v=>Assert.InRange(v,-scale,scale));
        }
    }
    [Fact]
    public void SortedWindowAndFeedbackHaveExplicitHandValues()
    {
        var bars=new[]{Candle(1),Candle(3),Candle(2),Candle(8)};
        var angle=2*Math.PI/12;var alpha=(Math.Cos(angle)+Math.Sin(angle)-1)/Math.Cos(angle);
        var previous=0d;var medians=new[]{1d,2,2,3};var expected=BuiltInFormulaReferences.RecursiveMedianValues(bars,3);
        for(var i=0;i<bars.Length;i++){previous=alpha*medians[i]+(1-alpha)*previous;Assert.Equal(previous,expected[i],14);}
        Check(bars,3);
        foreach(var value in new[]{double.Epsilon,7d,double.MaxValue}) Check(Enumerable.Range(0,20).Select(_=>Candle(value)).ToArray(),2);
        var random=new Random(532);Check(Enumerable.Range(0,256).Select(_=>Candle(random.Next(-4,5))).ToArray(),17);
    }
    [Fact]
    public void SelectedPricesDriveMedianAndFeedback()
    {
        var selected=Enumerable.Range(0,30).Select(i=>(double)(i%7)).ToList();var original=selected.Select(_=>Candle(100)).ToArray();var expected=BuiltInFormulaReferences.RecursiveMedianValues(selected.Select(v=>Candle(v)).ToArray(),5);
        var batch=Data(original);batch.SetCustomValues(selected);batch.CalculateEhlersRecursiveMedianFilter();Assert.Equal(expected,batch.CustomValuesList);
        using var context=new ComputeContext();var data=Data(original);data.SetCustomValues(selected);using var fast=IndicatorCompute.ComputeEhlersRecursiveMedianFilterFast(data,context);Assert.Equal(expected,fast.ToArray());
    }
    [Fact]
    public void RejectedFieldsPreserveMedianAndFeedbackHistory()
    {
        foreach(var field in Enumerable.Range(0,5)) foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}) foreach(var final in new[]{false,true})
        {
            using var state=new EhlersRecursiveMedianFilterState();using var control=new EhlersRecursiveMedianFilterState();var seed=Native(Candle(2));state.Update(seed,true,false);control.Update(seed,true,false);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
            foreach(var bar in Enumerable.Range(0,12).Select(i=>Native(Candle(i%3))))Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);
        }
    }
}
