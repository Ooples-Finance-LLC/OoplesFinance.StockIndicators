using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RetrospectiveCandleNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("RCC",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RetrospectiveCandlestickChart)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentWeightsAndCandleStages(IndicatorValidationCase c,string route)
    {
        var options=(RetrospectiveCandlestickChartSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.RetrospectiveCandleOutputs(bars,options.Length),IndicatorErrorBudget.Exact);
    }
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory,MemberData(nameof(Cases))]
    public Task NumericalFixturesAreEnrolled(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.RetrospectiveCandleOutputs(bars,length)["Rcc"];
        Assert.Equal(expected,Data(bars).CalculateRetrospectiveCandlestickChart(length).OutputValues["Rcc"]);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeRetrospectiveCandleFast(Data(bars),context,length);Assert.Equal(expected,raw.ToArray());
        using var state=new RetrospectiveCandlestickChartState(length);
        for(var replay=0;replay<2;replay++)
        {
            state.Reset();for(var i=0;i<bars.Length;i++)foreach(var final in new[]{false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,false).Value);
        }
        Assert.All(expected,v=>Assert.True(double.IsFinite(v)));
    }
    [Fact]
    public void HandWeightsPreserveThePreviousSmoothedClose()
    {
        var bars=new[]{new Bar(DateTime.UnixEpoch,0,4,-2,1,1),new Bar(DateTime.UnixEpoch.AddDays(1),2,6,0,4,1),new Bar(DateTime.UnixEpoch.AddDays(2),4,8,2,6,1)};
        Assert.Equal(new[]{0.75,3,4.5},BuiltInFormulaReferences.RetrospectiveCandleOutputs(bars,3)["Rcc"]);Check(bars,3);
        Assert.Equal(new[]{0.75,1,1},BuiltInFormulaReferences.RetrospectiveCandleOutputs(bars,1)["Rcc"]);Check(bars,1);
    }
    [Fact]
    public void ExtendedChangesAndConvexMeansRemainFinite()
    {
        foreach(var scale in new[]{1d,double.Epsilon,double.MaxValue/4})foreach(var length in new[]{1,2,5,int.MaxValue})
        {
            var bars=Enumerable.Range(0,24).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i),(i%3-1)*scale,(i%5-1)*scale,(i%7-3)*scale,(i%5-2)*scale,1)).ToArray();Check(bars,length);Check(Array.Empty<Bar>(),length);
        }
        Check(Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray(),3);
        Check(Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i),0,double.MaxValue,-double.MaxValue,i%2==0?double.MaxValue:-double.MaxValue,1)).ToArray(),3);
    }
    [Fact]
    public void SelectedRawClosesKeepOriginalOpenHighAndLow()
    {
        var bars=Enumerable.Range(0,18).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i),i+2,i+3,i+1,i+2,1)).ToArray();var selected=Enumerable.Range(0,bars.Length).Select(i=>i%2==0?-100d:i).ToArray();
        var projected=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();var expected=BuiltInFormulaReferences.RetrospectiveCandleOutputs(projected,3)["Rcc"];
        var data=Data(bars);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeRetrospectiveCandleFast(data,context,3);Assert.Equal(expected,raw.ToArray());
        data=Data(bars);data.SetCustomValues(selected.ToList());Assert.Equal(expected,data.CalculateRetrospectiveCandlestickChart(3).OutputValues["Rcc"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceWeightsOrClose()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{false,true})
        {
            using var state=new RetrospectiveCandlestickChartState(3);using var control=new RetrospectiveCandlestickChartState(3);var first=Native(new Bar(DateTime.UnixEpoch,1,3,0,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;var bad=Native(new Bar(DateTime.UnixEpoch.AddDays(1),v[0],v[1],v[2],v[3],v[4]));Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            for(var i=1;i<12;i++){var bar=Native(new Bar(DateTime.UnixEpoch.AddDays(i),1,i+3,i-2,i,1));Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);}
        }
    }
}
