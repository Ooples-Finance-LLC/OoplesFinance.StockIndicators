using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CandlePowerNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("POWER",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static StockData Batch(StockData d,bool bull,int length,MovingAvgType kind)=>bull?d.CalculateBullPowerIndicator(kind,length):d.CalculateBearPowerIndicator(kind,length);
    private static ComputeBuffer Raw(StockData d,ComputeContext c,bool bull,int length,MovingAvgType kind,bool signal)=>bull?IndicatorCompute.ComputeBullPowerFast(d,c,length,kind,signal):IndicatorCompute.ComputeBearPowerFast(d,c,length,kind,signal);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void CandleDirectionsGapPrecedenceAndWickTiesKeepExactSignals()
    {
        foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
        {
            var bars=new List<Bar>();foreach(var previous in new[] {-2d,0,2})foreach(var close in new[] {-1d,0,1})foreach(var high in new[] {2d,3})foreach(var low in new[] {-2d,-3}) {bars.Add(new Bar(DateTime.UnixEpoch.AddMinutes(bars.Count),previous*scale,4*scale,-4*scale,previous*scale,1));bars.Add(new Bar(DateTime.UnixEpoch.AddMinutes(bars.Count),0,high*scale,low*scale,close*scale,1));}
            foreach(var bull in new[] {false,true})foreach(var length in new[] {0,1,3,14})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})Check(bars.ToArray(),bull,length,kind);
        }
        foreach(var bull in new[] {false,true})Check(Array.Empty<Bar>(),bull,14,MovingAvgType.ExponentialMovingAverage);
        var overflow=new[] {new Bar(DateTime.UnixEpoch,0,double.MaxValue,-double.MaxValue,-1,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),0,0,0,0,1),new Bar(DateTime.UnixEpoch.AddMinutes(2),0,0,0,0,1),new Bar(DateTime.UnixEpoch.AddMinutes(3),0,0,0,0,1)};Check(overflow,false,4,MovingAvgType.SimpleMovingAverage);var bands=Batch(Data(overflow),false,4,MovingAvgType.SimpleMovingAverage);Assert.Equal(double.PositiveInfinity,bands.OutputValues["BearPower"][0]);Assert.Equal(double.MaxValue/2,bands.OutputValues["Signal"][3]);
        var tie=new[] {new Bar(DateTime.UnixEpoch,double.Epsilon,1,-1,double.Epsilon,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),double.Epsilon,1,-1,double.Epsilon,1)};Check(tie,true,1,MovingAvgType.SimpleMovingAverage);Assert.Equal(2d,Batch(Data(tie),true,1,MovingAvgType.SimpleMovingAverage).OutputValues["BullPower"][1]);
        var upperTie=new[] {new Bar(DateTime.UnixEpoch,-double.Epsilon,1,-1,-double.Epsilon,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),-double.Epsilon,1,-1,-double.Epsilon,1)};Check(upperTie,true,1,MovingAvgType.SimpleMovingAverage);Assert.Equal(1d,Batch(Data(upperTie),true,1,MovingAvgType.SimpleMovingAverage).OutputValues["BullPower"][1]);
    }
    private static void Check(Bar[] bars,bool bull,int length,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.CandlePowerOutputs(bars,bull,length,Kind(kind));var batch=Batch(Data(bars),bull,length,kind);using var context=new ComputeContext();foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=Raw(Data(bars),context,bull,length,kind,key=="Signal");Assert.Equal(expected[key],raw.Span.ToArray());}
        IStreamingIndicatorState state=bull?new BullPowerIndicatorState(kind,length):new BearPowerIndicatorState(kind,length);using var disposable=(IDisposable)state;
        for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedCloseKeepsOriginalFieldsAndCustomerSignalControlsOnlyAverage()
    {
        var bars=Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,10,-10,0,1)).ToArray();var prices=new[] {2d,-7,0,8,-4,0,9,1};var selected=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,prices[i],b.Volume)).ToArray();var means=Enumerable.Range(1,8).Select(i=>(double)i).ToArray();using var context=new ComputeContext();
        foreach(var bull in new[] {false,true})foreach(var batch in new[] {false,true})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.CandlePowerOutputs(selected,bull,3,3,external?means:null);var data=Data(bars);data.SetCustomValues(prices.ToList());using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>{Assert.Equal(3,p);Assert.Equal(expected[bull?"BullPower":"BearPower"],v);return means;}}):null;
            if(batch) {var result=Batch(data,bull,3,MovingAvgType.ExponentialMovingAverage);foreach(var key in expected.Keys)Assert.Equal(expected[key],result.OutputValues[key]);}else {using var result=Raw(data,context,bull,3,MovingAvgType.ExponentialMovingAverage,true);Assert.Equal(expected["Signal"],result.Span.ToArray());}if(external)Assert.Equal(1,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvancePreviousCloseOrSignal()
    {
        foreach(var bull in new[] {false,true})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            IStreamingIndicatorState state=bull?new BullPowerIndicatorState(length:3):new BearPowerIndicatorState(length:3);IStreamingIndicatorState control=bull?new BullPowerIndicatorState(length:3):new BearPowerIndicatorState(length:3);using var d1=(IDisposable)state;using var d2=(IDisposable)control;var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("POWER",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),0,4,-3,-2,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(BullPower)||c.IndicatorType==typeof(BearPower)||c.IndicatorType==typeof(BullPowerIndicator)||c.IndicatorType==typeof(BearPowerIndicator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.CandlePowerOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
