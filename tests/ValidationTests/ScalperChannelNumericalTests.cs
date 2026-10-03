using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ScalperChannelNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("SCALPER",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static void Equal(string key,double expected,double actual) {if(key=="Scalper")Assert.True(BuiltInFormulaReferences.ScalperBudget.Accepts(expected,actual),$"{expected:R} != {actual:R}");else Assert.Equal(expected,actual);}
    [Fact]
    public void ExtendedPiAtrLogarithmAndIndependentRangePeriodMatchReference()
    {
        foreach(var rangeLength in new[] {1,3,7})foreach(var averageLength in new[] {1,2,5})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,(i%5+2)*scale,-(i%3+1)*scale,(i%7-3)*scale,1)).ToArray(),rangeLength,averageLength,kind);
        Check(Array.Empty<Bar>(),15,20,MovingAvgType.SimpleMovingAverage);
        Check(Enumerable.Range(0,4).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,double.MaxValue,-double.MaxValue,0,1)).ToArray(),2,2,MovingAvgType.SimpleMovingAverage);
        Check(Enumerable.Range(0,4).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray(),2,2,MovingAvgType.SimpleMovingAverage);
        var bars=new[] {new Bar(DateTime.UnixEpoch,10,12,8,10,1)};var batch=Data(bars).CalculateScalpersChannel(length1:1,length2:1);
        Assert.Equal(12,batch.OutputValues["UpperBand"][0]);Assert.Equal(8,batch.OutputValues["LowerBand"][0]);Assert.Equal(10,batch.OutputValues["MiddleBand"][0]);Equal("Scalper",10-Math.Log(4*Math.PI),batch.OutputValues["Scalper"][0]);
    }
    private static void Check(Bar[] bars,int rangeLength,int averageLength,MovingAvgType kind)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.ScalperChannelOutputs(bars,rangeLength,averageLength,code);var batch=Data(bars).CalculateScalpersChannel(kind,rangeLength,averageLength);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {using var raw=IndicatorCompute.ComputeScalpersChannelFast(Data(bars),context,averageLength,kind,rangeLength,key);for(var i=0;i<bars.Length;i++) {Equal(key,expected[key][i],batch.OutputValues[key][i]);Equal(key,expected[key][i],raw.Span[i]);}}
        using var state=new ScalpersChannelState(kind,rangeLength,averageLength);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Equal(key,expected[key][i],actual.Outputs![key]);Assert.Equal(actual.Value,actual.Outputs!["Scalper"]);}}}
    }
    [Fact]
    public void SelectedCloseAndCustomerAverageAtrStagesRemainIndependent()
    {
        var prices=new[] {9d,-1,6,2};var bars=Enumerable.Range(0,4).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),4,new[] {10d,5,7,4}[i],new[] {-2d,-2,0,0}[i],4,1)).ToArray();var selected=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,prices[i],b.Volume)).ToArray();using var context=new ComputeContext();
        foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(prices.ToList());var average=new[] {1d,2,3,4};var atr=new[] {7d,8,9,10};var expected=BuiltInFormulaReferences.ScalperChannelOutputs(selected,3,2,6,external?average:null,external?atr:null);
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(2,period);Assert.Equal(prices,values);return average;},(values,period)=>{Assert.Equal(2,period);Assert.Equal(new[] {12d,11,8,6},values);return atr;}}):null;
            if(batch) {var result=data.CalculateScalpersChannel(MovingAvgType.WildersSmoothingMethod,3,2);foreach(var key in expected.Keys)for(var i=0;i<bars.Length;i++)Equal(key,expected[key][i],result.OutputValues[key][i]);}
            else {using var raw=IndicatorCompute.ComputeScalpersChannelFast(data,context,2,MovingAvgType.WildersSmoothingMethod,3);for(var i=0;i<bars.Length;i++)Equal("Scalper",expected["Scalper"][i],raw.Span[i]);}if(external)Assert.Equal(2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new ScalpersChannelState(length1:3,length2:2);using var control=new ScalpersChannelState(length1:3,length2:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("SCALPER",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchAverageAndAtrRemainSupported()
    {
        var bars=Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),10,12,8,9+i%3,1)).ToArray();var kind=MovingAvgType.HullMovingAverage;var average=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,bars.Select(b=>b.Close).ToList()).ToArray();var atr=Data(bars).CalculateAverageTrueRange(kind,3).CustomValuesList.ToArray();var expected=BuiltInFormulaReferences.ScalperChannelOutputs(bars,2,3,1,average,atr);var batch=Data(bars).CalculateScalpersChannel(kind,2,3);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeScalpersChannelFast(Data(bars),context,3,kind,2);for(var i=0;i<bars.Length;i++) {Equal("Scalper",expected["Scalper"][i],batch.OutputValues["Scalper"][i]);Equal("Scalper",expected["Scalper"][i],raw.Span[i]);}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(ScalpersChannel)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.ScalperChannelOutputs(bars,(IBuiltInIndicator)testCase.Factory()),BuiltInFormulaReferences.ScalperBudget);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
