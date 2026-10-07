using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class OneBarReturnNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("RETURN",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static StockData Batch(StockData d,bool cumulative,int length,MovingAvgType kind)=>cumulative?d.CalculatePercentChangeOscillator(kind,length):d.CalculateForecastOscillator(kind,length);
    private static ComputeBuffer Raw(StockData d,ComputeContext c,bool cumulative,int length,MovingAvgType kind,bool signal)=>cumulative?IndicatorCompute.ComputePercentChangeOscillatorFast(d,c,length,kind,signal):IndicatorCompute.ComputeForecastOscillatorFast(d,c,length,kind,signal);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void CurrentAndPreviousDenominatorsKeepDistinctZeroAndCumulativeRules()
    {
        foreach(var cumulative in new[] {false,true})foreach(var length in new[] {0,1,2,3,20})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})Check(Bars(Enumerable.Range(0,32).Select(i=>(i%11-5)*scale).ToArray()),cumulative,length,kind);
        foreach(var cumulative in new[] {false,true}) {Check(Array.Empty<Bar>(),cumulative,14,MovingAvgType.WeightedMovingAverage);Check(Bars(new[] {double.Epsilon,double.MaxValue,double.Epsilon,-double.MaxValue,1d,0}),cumulative,3,MovingAvgType.SimpleMovingAverage);}
        var reset=Bars(new[] {2d,4,0,8,16});Assert.Equal(new[] {0d,1,0,0,1},Batch(Data(reset),true,1,MovingAvgType.SimpleMovingAverage).OutputValues["Pcco"]);Assert.Equal(new[] {0d,50,0,100,50},Batch(Data(reset),false,1,MovingAvgType.SimpleMovingAverage).OutputValues["Fo"]);
        Assert.Equal(1d/3,Batch(Data(Bars(new[] {3d,4})),true,1,MovingAvgType.SimpleMovingAverage).OutputValues["Pcco"][1]);
        var cancellation=Bars(new[] {double.MaxValue,double.Epsilon,-double.MaxValue,double.Epsilon});Check(cancellation,false,3,MovingAvgType.SimpleMovingAverage);var bands=Batch(Data(cancellation),false,3,MovingAvgType.SimpleMovingAverage);Assert.Equal(double.NegativeInfinity,bands.OutputValues["Fo"][1]);Assert.Equal(double.PositiveInfinity,bands.OutputValues["Fo"][3]);Assert.Equal(100d/3,bands.OutputValues["Signal"][3]);
    }
    private static void Check(Bar[] bars,bool cumulative,int length,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.OneBarReturnOutputs(bars,cumulative,length,Kind(kind));var batch=Batch(Data(bars),cumulative,length,kind);using var context=new ComputeContext();foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=Raw(Data(bars),context,cumulative,length,kind,key=="Signal");Assert.Equal(expected[key],raw.Span.ToArray());}
        IStreamingIndicatorState state=cumulative?new PercentChangeOscillatorState(kind,length):new ForecastOscillatorState(kind,length);using var disposable=(IDisposable)state;for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesAndCustomerAverageKeepPrimaryReturnsIndependent()
    {
        var prices=new[] {2d,7,3,8,4,9,1};var means=Enumerable.Range(1,7).Select(i=>(double)i).ToArray();using var context=new ComputeContext();foreach(var cumulative in new[] {false,true})foreach(var batch in new[] {false,true})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.OneBarReturnOutputs(Bars(prices),cumulative,3,3,external?means:null);var data=Data(Bars(Enumerable.Repeat(4d,prices.Length).ToArray()));data.SetCustomValues(prices.ToList());using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>{Assert.Equal(3,p);Assert.Equal(expected[cumulative?"Pcco":"Fo"],v);return means;}}):null;
            if(batch) {var output=Batch(data,cumulative,3,MovingAvgType.ExponentialMovingAverage);foreach(var key in expected.Keys)Assert.Equal(expected[key],output.OutputValues[key]);}else {using var output=Raw(data,context,cumulative,3,MovingAvgType.ExponentialMovingAverage,true);Assert.Equal(expected["Signal"],output.Span.ToArray());}if(external)Assert.Equal(1,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvancePriceCumulativeValueOrSignal()
    {
        foreach(var cumulative in new[] {false,true})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            IStreamingIndicatorState state=cumulative?new PercentChangeOscillatorState(length:3):new ForecastOscillatorState(length:3);IStreamingIndicatorState control=cumulative?new PercentChangeOscillatorState(length:3):new ForecastOscillatorState(length:3);using var d1=(IDisposable)state;using var d2=(IDisposable)control;var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("RETURN",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var b in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(b),true,true).Outputs!,state.Update(Native(b),true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(ForecastOscillator)||c.IndicatorType==typeof(PercentChangeOscillator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.OneBarReturnOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
