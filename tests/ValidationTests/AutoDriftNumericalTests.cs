using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AutoDriftNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("DRIFT",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(double[] values)=>values.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddDays(i),v,v,v,v,1)).ToArray();
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(AutoLineWithDrift)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentPopulationBandsAndDrift(IndicatorValidationCase c,string route)
    {
        var options=(AutoLineWithDriftSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.AutoDriftOutputs(bars,options.Length),IndicatorErrorBudget.Exact);
    }
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormula(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory,MemberData(nameof(Cases))]
    public Task NumericalFixturesAreEnrolled(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(double[] values,int length)
    {
        var bars=Bars(values);var expected=BuiltInFormulaReferences.AutoDriftOutputs(bars,length)["Alwd"];
        Assert.Equal(expected,Data(bars).CalculateAutoLineWithDrift(length).OutputValues["Alwd"]);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeAutoLineWithDriftFast(Data(bars),context,length);Assert.Equal(expected,raw.ToArray());
        Assert.All(expected,v=>Assert.False(double.IsNaN(v)));
        using var state=new AutoLineWithDriftState(length);
        for(var replay=0;replay<2;replay++)
        {
            state.Reset();for(var i=0;i<bars.Length;i++)foreach(var final in new[]{false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,false).Value);
        }
        Assert.All(expected,v=>Assert.False(double.IsNaN(v)));
    }
    [Fact]
    public void HandDriftUsesTheLineFromLengthPlusOneBarsEarlier()
    {
        var values=new[]{0d,2,2.5,2,2,2};Assert.Equal(new[]{0d,2,2,2,7d/3,2},BuiltInFormulaReferences.AutoDriftOutputs(Bars(values),3)["Alwd"]);Check(values,3);
        var boundary=new[]{0d,2,2.5,2.35};Assert.Equal(new[]{0d,2,2,2.35},BuiltInFormulaReferences.AutoDriftOutputs(Bars(boundary),3)["Alwd"]);Check(boundary,3);
        Check(new[]{1.25,1.25},3);
        var fractional=new[]{0.25,0.25,0.25};Assert.Equal(new[]{0.25,7d/24,0.25},BuiltInFormulaReferences.AutoDriftOutputs(Bars(fractional),3)["Alwd"]);Check(fractional,3);
    }
    [Fact]
    public void FullDefaultAndMaximumPeriodsRetainFiniteInternalHistory()
    {
        foreach(var length in new[]{1,2,14,500})
        {
            Check(Enumerable.Range(0,length+20).Select(i=>10+Math.Sin(i)*3).ToArray(),length);
            foreach(var scale in new[]{double.Epsilon,double.MaxValue})Check(Enumerable.Range(0,length+10).Select(i=>(i%3-1)*scale).ToArray(),length);
            Check(Array.Empty<double>(),length);
        }
    }
    [Fact]
    public void MaximumPeriodUsesPositiveGainAndObservedHistory()
    {
        Check(new[]{0.25,0.25,0.25,1d,1,0.25},int.MaxValue);Check(new[]{0d,double.MaxValue,-double.MaxValue,1d},int.MaxValue);
    }
    [Fact]
    public void UnrepresentablePublishedDriftDoesNotPoisonLaterValues()
    {
        var values=new[]{0d,double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue};var expected=BuiltInFormulaReferences.AutoDriftOutputs(Bars(values),1)["Alwd"];
        Assert.Equal(double.PositiveInfinity,expected[2]);Assert.Equal(double.MaxValue,expected[3]);Check(values,1);
    }
    [Fact]
    public void SelectedRawClosesDefineBandsAndHeldValues()
    {
        var bars=Bars(Enumerable.Range(0,18).Select(i=>(double)i).ToArray());var selected=Enumerable.Range(0,bars.Length).Select(i=>i%2==0?-100d:i).ToArray();var expected=BuiltInFormulaReferences.AutoDriftOutputs(Bars(selected),3)["Alwd"];
        var data=Data(bars);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeAutoLineWithDriftFast(data,context,3);Assert.Equal(expected,raw.ToArray());
        data=Data(bars);data.SetCustomValues(selected.ToList());Assert.Equal(expected,data.CalculateAutoLineWithDrift(3).OutputValues["Alwd"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{false,true})
        {
            using var state=new AutoLineWithDriftState(3);using var control=new AutoLineWithDriftState(3);var first=Native(new Bar(DateTime.UnixEpoch,1,3,0,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;var bad=Native(new Bar(DateTime.UnixEpoch.AddDays(1),v[0],v[1],v[2],v[3],v[4]));Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            for(var i=1;i<12;i++){var bar=Native(new Bar(DateTime.UnixEpoch.AddDays(i),1,i+3,i-2,i,1));Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);}
        }
    }
}
