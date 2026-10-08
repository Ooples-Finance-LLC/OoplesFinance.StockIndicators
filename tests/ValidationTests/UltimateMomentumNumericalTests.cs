using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class UltimateMomentumNumericalTests
{
    private static Bar B(double price,int i=0,double volume=1)=>new(DateTime.UnixEpoch.AddMinutes(i),price,price,price,price,volume);
    private static StockData Data(IReadOnlyList<Bar> bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("UTM",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(UltimateMomentumIndicator)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void RoutesMatchIndependentBlend(IndicatorValidationCase c,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.UltimateMomentumOutputs(bars,(IBuiltInIndicator)c.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveBlend(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void FlatBlendKeepsRsiConventionAndFullSimpleWarmup()
    {
        var bars=Enumerable.Range(0,7).Select(i=>B(0,i)).ToArray();var actual=Data(bars).CalculateUltimateMomentumIndicator(length1:3,length2:3,length3:3,length4:3,length5:3).OutputValues["Utm"];
        Assert.Equal(new[]{0d,0,100,100,100,100,100},actual);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage,1)] [InlineData(MovingAvgType.WeightedMovingAverage,2)] [InlineData(MovingAvgType.ExponentialMovingAverage,3)] [InlineData(MovingAvgType.WildersSmoothingMethod,6)]
    public void ExtremePricesVolumesAndBandsPreserveSignalsPreviewAndReset(MovingAvgType kind,int code)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/8})
        {
            var bars=Enumerable.Range(0,19).Select(i=>B((i%7-3)*scale,i,i%3==0?-scale:scale)).ToArray();var signals=new List<Signal>();var expected=BuiltInFormulaReferences.UltimateMomentumOutputs(bars,code,3,3,4,5,3,.5,false,signals)["Utm"];var actual=Data(bars).CalculateUltimateMomentumIndicator(kind,3,3,4,5,3,200,.5);
            Assert.Equal(expected,actual.OutputValues["Utm"]);Assert.Equal(signals,actual.SignalsList);
            using var state=new UltimateMomentumIndicatorState(kind,3,3,4,5,3,200,.5);using var signalState=new UltimateMomentumWindow(kind,3,3,4,5,3,.5);
            for(var pass=0;pass<2;pass++)
            {
                for(var i=0;i<7;i++){state.Update(Native(B(17)),true,false);signalState.Next(17,17,1,true);}state.Reset();signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs!["Utm"]);Assert.Equal(signals[i],signalState.Next(bars[i].Close,bars[i].Close,bars[i].Volume,final).Trade);}
                }
            }
        }
    }
    [Fact]
    public void TinyMultiplierKeepsFiniteStrengthFromOverflowingBandPosition()
    {
        var bars=new[]{1d,3,2,4,1,7,2,5}.Select((v,i)=>B(v,i)).ToArray();var expected=BuiltInFormulaReferences.UltimateMomentumOutputs(bars,1,2,2,3,4,2,double.Epsilon)["Utm"];
        var actual=Data(bars).CalculateUltimateMomentumIndicator(length1:2,length2:2,length3:3,length4:4,length5:2,stdDevMult:double.Epsilon).OutputValues["Utm"];Assert.Equal(expected,actual);Assert.All(actual,v=>Assert.InRange(v,0,100));
    }
    [Fact]
    public void HugePeriodsAndFastRoutePreserveCallerState()
    {
        var bars=new[]{1d,3,2,-1,0,4,2,7}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.UltimateMomentumOutputs(bars,1,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,1.5)["Utm"];
        using var context=new ComputeContext();using var result=IndicatorCompute.ComputeUltimateMomentumFast(data,context,MovingAvgType.SimpleMovingAverage,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,1.5);Assert.Equal(expected,result.ToArray());
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateUltimateMomentumIndicator(stdDevMult:double.NaN));Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);
    }
}
