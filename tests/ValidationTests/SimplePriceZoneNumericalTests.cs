using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SimplePriceZoneNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("SPZ",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(double[] values)=>values.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddDays(i),v,v,v,v,1)).ToArray();
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(SimplePriceZone)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentChangeBalance(IndicatorValidationCase c,string route)
    {
        var options=(SimplePriceZoneSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.SimplePriceZoneOutputs(bars,options.Length),IndicatorErrorBudget.Exact);
    }
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormula(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory,MemberData(nameof(Cases))]
    public Task NumericalFixturesAreEnrolled(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(double[] values,int length)
    {
        var bars=Bars(values);var expected=BuiltInFormulaReferences.SimplePriceZoneOutputs(bars,length)["Spz"];
        Assert.Equal(expected,Data(bars).CalculateSimplePriceZone(length).OutputValues["Spz"]);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeSimplePriceZoneFast(Data(bars),context,length);Assert.Equal(expected,raw.ToArray());
        var core=new double[values.Length];OscillatorCore.SimplePriceZone(values,core,length);Assert.Equal(expected,core);
        using var state=new SimplePriceZoneState(length);
        for(var replay=0;replay<2;replay++)
        {
            state.Reset();for(var i=0;i<bars.Length;i++)foreach(var final in new[]{false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,false).Value);
        }
        Assert.All(expected,v=>Assert.InRange(v,-100,100));
    }
    [Fact]
    public void HandChangesPreserveStartupAndExactWindowEviction()
    {
        var normal=new[]{2d,4,3,6,6,2};Assert.Equal(new[]{0d,100,100d/3,50,100,-100},BuiltInFormulaReferences.SimplePriceZoneOutputs(Bars(normal),2)["Spz"]);Check(normal,2);
        Assert.Equal(new[]{0d,100,-100,100,0,-100},BuiltInFormulaReferences.SimplePriceZoneOutputs(Bars(normal),1)["Spz"]);Check(normal,1);
        var tiny=new[]{0d,double.Epsilon,2*double.Epsilon,0};Assert.Equal(new[]{0d,100,100,-100d/3},BuiltInFormulaReferences.SimplePriceZoneOutputs(Bars(tiny),2)["Spz"]);Check(tiny,2);
    }
    [Fact]
    public void ExtendedChangesRemainBoundedAndRecoverAfterLargePrices()
    {
        foreach(var scale in new[]{1d,double.Epsilon,double.MaxValue})foreach(var length in new[]{1,2,5,int.MaxValue})
        {
            Check(Enumerable.Range(0,24).Select(i=>(i%3-1)*scale).Concat(new double[16]).ToArray(),length);
            Check(Enumerable.Repeat(scale,9).ToArray(),length);Check(Array.Empty<double>(),length);
        }
    }
    [Fact]
    public void SelectedRawClosesDriveTheChangeWindow()
    {
        var bars=Bars(Enumerable.Range(0,18).Select(i=>(double)i).ToArray());var selected=Enumerable.Range(0,bars.Length).Select(i=>i%2==0?-100d:i).ToArray();var expected=BuiltInFormulaReferences.SimplePriceZoneOutputs(Bars(selected),3)["Spz"];
        var data=Data(bars);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeSimplePriceZoneFast(data,context,3);Assert.Equal(expected,raw.ToArray());
        data=Data(bars);data.SetCustomValues(selected.ToList());Assert.Equal(expected,data.CalculateSimplePriceZone(3).OutputValues["Spz"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{false,true})
        {
            using var state=new SimplePriceZoneState(3);using var control=new SimplePriceZoneState(3);var first=Native(new Bar(DateTime.UnixEpoch,1,3,0,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;var bad=Native(new Bar(DateTime.UnixEpoch.AddDays(1),v[0],v[1],v[2],v[3],v[4]));Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            for(var i=1;i<12;i++){var bar=Native(new Bar(DateTime.UnixEpoch.AddDays(i),1,i+3,i-2,i,1));Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);}
        }
    }
}
