using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HurstCycleNumericalTests
{
    private static readonly string[] Keys={"FastUpperBand","FastMiddleBand","FastLowerBand","SlowUpperBand","SlowMiddleBand","SlowLowerBand","OMed","OShort"};
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices,double spread=1)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v+spread,v-spread,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("HC",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void DelayedMeansAndRoundedEnvelopesPreserveFiniteOscillatorRatios()
    {
        foreach(var lengths in new[] {(0,1),(2,6),(5,9),(10,30)})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var factors in new[] {(1d,3d),(-1d,.5),(0d,0d),(.5,double.MaxValue)})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Bars(Enumerable.Range(0,16).Select(i=>(i%11-5)*scale).ToArray(),scale),lengths.Item1,lengths.Item2,kind,factors.Item1,factors.Item2);
        Check(Array.Empty<Bar>(),10,30,MovingAvgType.WildersSmoothingMethod,1,3);
        foreach(var length in new[] {1058,1059,int.MaxValue})Check(Bars(Enumerable.Range(0,540).Select(i=>(double)(i%7)).ToArray()),length,6,MovingAvgType.SimpleMovingAverage,.5,2);
        var hand=Data(Bars(new[] {2d,4,6,8})).CalculateHurstCycleChannel(MovingAvgType.SimpleMovingAverage,2,6,1,2);Assert.Equal(new[] {2d,4,0,3},hand.OutputValues["FastMiddleBand"]);Assert.Equal(.75,hand.OutputValues["OMed"][3]);Assert.Equal(7d/6,hand.OutputValues["OShort"][3]);
        var extreme=Bars(new[] {0d,0},double.MaxValue);Check(extreme,2,2,MovingAvgType.SimpleMovingAverage,.5,.5);var big=Data(extreme).CalculateHurstCycleChannel(MovingAvgType.SimpleMovingAverage,2,2,.5,.5);Assert.Equal(double.MaxValue,big.OutputValues["SlowUpperBand"][1]);Assert.Equal(-double.MaxValue,big.OutputValues["SlowLowerBand"][1]);Assert.Equal(.5,big.OutputValues["OShort"][1]);
    }
    private static void Check(Bar[] bars,int fast,int slow,MovingAvgType kind,double fastFactor,double slowFactor)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.HurstCycleOutputs(bars,fast,slow,code,fastFactor,slowFactor);var batch=Data(bars).CalculateHurstCycleChannel(kind,fast,slow,fastFactor,slowFactor);using var context=new ComputeContext();
        foreach(var key in Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeHurstCycleChannelFast(Data(bars),context,fast,slow,fastFactor,slowFactor,kind,Enum.Parse<IndicatorCompute.HurstCycleSeries>(key));Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new HurstCycleChannelState(kind,fast,slow,fastFactor,slowFactor);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesAndFourCustomerAveragesKeepAtrThenMeanOrder()
    {
        var prices=new[] {2d,4,6,8};var original=Bars(new[] {4d,4,4,4},10);var selected=original.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,prices[i],b.Volume)).ToArray();using var context=new ComputeContext();foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var data=Data(original);data.SetCustomValues(prices.ToList());var stages=new[] {new[] {1d,2,3,4},new[] {2d,3,4,5},new[] {5d,7,9,11},new[] {8d,10,12,14}};var expected=BuiltInFormulaReferences.HurstCycleOutputs(selected,2,6,1,1,2,external?stages:null);
            using var armed=external?ComponentAverage.Arm(Enumerable.Range(0,4).Select(slot=>(Func<IReadOnlyList<double>,int,IReadOnlyList<double>>)((values,period)=>{Assert.Equal(slot%2==0?2:3,period);Assert.Equal(slot<2?new[] {20d,20,20,20}:prices,values);return stages[slot];})).ToArray()):null;
            if(batch) {var output=data.CalculateHurstCycleChannel(MovingAvgType.SimpleMovingAverage,2,6,1,2);foreach(var key in Keys)Assert.Equal(expected[key],output.OutputValues[key]);}else {using var raw=IndicatorCompute.ComputeHurstCycleChannelFast(data,context,2,6,1,2,MovingAvgType.SimpleMovingAverage,IndicatorCompute.HurstCycleSeries.OMed);Assert.Equal(expected["OMed"],raw.Span.ToArray());}if(external)Assert.Equal(4,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsAndMultipliersNeverAdvanceNativeHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new HurstCycleChannelState(fastLength:2,slowLength:6);using var control=new HurstCycleChannelState(fastLength:2,slowLength:6);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("HC",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var fast in new[] {false,true}) {var f=fast?invalid:1;var s=fast?3:invalid;Assert.Throws<ArgumentOutOfRangeException>(()=>new HurstCycleChannelState(fastMult:f,slowMult:s));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateHurstCycleChannel(fastMult:f,slowMult:s));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeHurstCycleChannelFast(Data(Array.Empty<Bar>()),context,fastMult:f,slowMult:s));}
    }
    [Fact]
    public void LegacyBatchAveragesKeepFourStages()
    {
        var bars=Bars(new[] {2d,7,4,9,3,5});var kind=MovingAvgType.HullMovingAverage;var ranges=CalculationsHelper.GetTrueRangeList(Data(bars));var stages=new[] {CalculationsHelper.GetMovingAverageList(Data(bars),kind,2,ranges).ToArray(),CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,ranges).ToArray(),CalculationsHelper.GetMovingAverageList(Data(bars),kind,2,bars.Select(b=>b.Close).ToList()).ToArray(),CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,bars.Select(b=>b.Close).ToList()).ToArray()};var expected=BuiltInFormulaReferences.HurstCycleOutputs(bars,2,6,1,1,2,stages);var batch=Data(bars).CalculateHurstCycleChannel(kind,2,6,1,2);foreach(var key in Keys)Assert.Equal(expected[key],batch.OutputValues[key]);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeHurstCycleChannelFast(Data(bars),context,2,6,1,2,kind,IndicatorCompute.HurstCycleSeries.OShort);Assert.Equal(expected["OShort"],raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(HurstCycleChannel)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.HurstCycleOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
