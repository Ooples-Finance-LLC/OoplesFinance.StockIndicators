using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EarningLevelsNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("ESR",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EarningSupportResistanceLevels)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void EveryRoutePreservesTheTwoBarLag(IndicatorValidationCase c,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(c,route,BuiltInFormulaReferences.EarningLevelsOutputs,IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheOriginalCandles(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory,MemberData(nameof(Cases))]
    public Task NumericalFixturesAreEnrolled(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void HandLevelsPreserveWarmupSubnormalsAndMaximumMeans()
    {
        foreach(var v in new[]{1d,double.Epsilon,double.MaxValue})
        {
            var bars=Enumerable.Range(0,5).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i),v,v,v,v,1)).ToArray();var expected=new[]{v/2,v/2,v,v,v};
            Assert.Equal(expected,BuiltInFormulaReferences.EarningLevelsOutputs(bars)["Esr"]);
            Assert.Equal(expected,Data(bars).CalculateEarningSupportResistanceLevels().OutputValues["Esr"]);
            using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeEarningLevelsFast(Data(bars),context);Assert.Equal(expected,raw.ToArray());
            using var state=new EarningSupportResistanceLevelsState();
            for(var replay=0;replay<2;replay++)
            {
                state.Reset();
                for(var i=0;i<bars.Length;i++)foreach(var final in new[]{false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,false).Value);
            }
        }
        var hand=new[]{new Bar(DateTime.UnixEpoch,1,8,-4,1,1),new Bar(DateTime.UnixEpoch.AddDays(1),1,10,-2,1,1),new Bar(DateTime.UnixEpoch.AddDays(2),1,12,0,1,1)};
        Assert.Equal(new[]{4d,5,4},Data(hand).CalculateEarningSupportResistanceLevels().OutputValues["Esr"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{false,true})
        {
            using var state=new EarningSupportResistanceLevelsState();using var control=new EarningSupportResistanceLevelsState();
            var first=Native(new Bar(DateTime.UnixEpoch,1,3,0,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;var bad=Native(new Bar(DateTime.UnixEpoch.AddDays(1),v[0],v[1],v[2],v[3],v[4]));Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            for(var i=1;i<5;i++){var bar=Native(new Bar(DateTime.UnixEpoch.AddDays(i),1,i+3,-i,i,1));Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);}
        }
    }
}
