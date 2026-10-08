using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MesaV1NumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EhlersMesaPredictIndicatorV1)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentSparseBurg(IndicatorValidationCase c,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.MesaPredictionValues(bars,(EhlersMesaPredictIndicatorV1SpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandleFields(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory,MemberData(nameof(Cases))]
    public Task EveryConfigurationPassesNumericalFixtures(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Bar B(double value)=>new(DateTime.UnixEpoch,value,value,value,value,1);
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("MESA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static void Check(Bar[] bars,int horizon,int order,int smooth,int window)
    {
        var options=new EhlersMesaPredictIndicatorV1SpecOptions(horizon,order,smooth,window);
        var expected=BuiltInFormulaReferences.MesaPredictionValues(bars,options);
        var batch=Data(bars).CalculateEhlersMesaPredictIndicatorV1(horizon,order,73,smooth,window);
        foreach(var p in expected)
        {
            Assert.Equal(p.Value,batch.OutputValues[p.Key]);
            using var context=new ComputeContext();using var fast=IndicatorCompute.ComputeMesaPredictV1Fast(Data(bars),context,options,p.Key);Assert.Equal(p.Value,fast.ToArray());
        }
        using var state=new EhlersMesaPredictIndicatorV1State(horizon,order,1,smooth,window);
        for(var pass=0;pass<2;pass++)
        {
            state.Update(Native(B(7)),true,false);state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(B(-double.MaxValue)),false,false);
                foreach(var final in new[]{false,false,true})
                {
                    var actual=state.Update(Native(bars[i]),final,true);foreach(var p in expected)Assert.Equal(p.Value[i],actual.Outputs![p.Key]);
                }
            }
        }
    }
    [Theory]
    [InlineData(1,1,1,2)]
    [InlineData(3,20,4,7)]
    [InlineData(5,4,12,54)]
    public void ExtendedFilterFitAndForecastPreservePreviewAndReset(int horizon,int order,int smooth,int window)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue})
        {
            var bars=Enumerable.Range(0,24).Select(i=>B(i<16?(i%3==0?-scale:scale):0)).ToArray();Check(bars,horizon,order,smooth,window);
        }
        Check(Array.Empty<Bar>(),horizon,order,smooth,window);
    }
    [Fact]
    public void PartialHistoryCanReachHigherBurgOrders()
    {
        var bars=Enumerable.Range(0,14).Select(i=>B((i%5)-2d)).ToArray();
        Check(bars,5,12,3,31);
    }
    [Fact]
    public void RelativeAmplitudeCutoffRemainsAFormulaDecision()
    {
        var tiny=Enumerable.Range(0,32).Select(i=>B(Math.Pow(2,50)+(i%3==0?-1:1))).ToArray();
        var larger=Enumerable.Range(0,32).Select(i=>B(Math.Pow(2,50)+(i%3==0?-1024:1024))).ToArray();
        var small=Data(tiny).CalculateEhlersMesaPredictIndicatorV1(3,2,1,4,7);
        Assert.Contains(small.OutputValues["Ssf"],v=>v!=0);Assert.All(small.OutputValues["PrePredict"],v=>Assert.Equal(0,v));
        Assert.Contains(Data(larger).CalculateEhlersMesaPredictIndicatorV1(3,2,1,4,7).OutputValues["PrePredict"],v=>v!=0);
        Check(tiny,3,2,4,7);Check(larger,3,2,4,7);
        foreach(var amplitude in new[]{8,16,32,64,128})Check(Enumerable.Range(0,24).Select(i=>B(Math.Pow(2,50)+(i%3==0?-amplitude:amplitude))).ToArray(),3,2,4,7);
    }
    [Fact]
    public void OneObservedImpulseCannotFitAZeroPaddedLagPair()
    {
        var data=Data(Enumerable.Range(0,5).Select(i=>B(i==4?1:0)).ToArray()).CalculateEhlersMesaPredictIndicatorV1(1,1,1,1,2);
        Assert.All(data.OutputValues["Ssf"].Take(4),v=>Assert.Equal(0,v));Assert.True(data.OutputValues["Ssf"][4]>0);
        Assert.All(data.OutputValues["PrePredict"],v=>Assert.Equal(0,v));
    }
    [Fact]
    public void SelectedPricesAndSignalsUseTheFilteredForecast()
    {
        var selected=Enumerable.Range(0,32).Select(i=>B((i%7-3)*1e300)).ToArray();var original=selected.Select(_=>B(100)).ToArray();
        var expected=BuiltInFormulaReferences.MesaPredictionValues(selected,new(3,2,4,7));
        var data=Data(original);data.SetCustomValues(selected.Select(b=>b.Close).ToList());data.CalculateEhlersMesaPredictIndicatorV1(3,2,1,4,7);
        foreach(var p in expected)
        {
            Assert.Equal(p.Value,data.OutputValues[p.Key]);using var context=new ComputeContext();var source=Data(original);source.SetCustomValues(selected.Select(b=>b.Close).ToList());
            using var fast=IndicatorCompute.ComputeMesaPredictV1Fast(source,context,new(3,2,4,7),p.Key);Assert.Equal(p.Value,fast.ToArray());
        }
        var previous=new ReferenceFraction(0);
        for(var i=0;i<selected.Length;i++)
        {
            var delta=ReferenceFraction.FromDouble(expected["Ssf"][i])-ReferenceFraction.FromDouble(expected["Predict"][i]);
            var sign=delta.Sign>0?delta.CompareTo(previous)>0?Signal.StrongBuy:Signal.Buy:delta.Sign<0?delta.CompareTo(previous)<0?Signal.StrongSell:Signal.Sell:Signal.None;
            Assert.Equal(sign,data.SignalsList[i]);previous=delta;
        }
    }
    [Fact]
    public void MaximumPeriodsAllocateOnlyObservedZeroHistory()
    {
        using var state=new EhlersMesaPredictIndicatorV1State(int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue);
        for(var i=0;i<8;i++)Assert.Equal(0,state.Update(Native(B(0)),true,true).Value);
        Check(new[]{B(0),B(0),B(0)},int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue);
    }
    [Fact]
    public void InvalidInputDoesNotAdvanceAnyStage()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{false,true})
        {
            using var state=new EhlersMesaPredictIndicatorV1State(3,2,1,4,7);using var control=new EhlersMesaPredictIndicatorV1State(3,2,1,4,7);
            foreach(var bar in Enumerable.Range(0,8).Select(i=>Native(B(i%3)))){state.Update(bar,true,false);control.Update(bar,true,false);}
            var v=new[]{1d,2,0,1,1};v[field]=invalid;
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
            foreach(var bar in Enumerable.Range(0,8).Select(i=>Native(B(i%5))))Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);
        }
    }
}
