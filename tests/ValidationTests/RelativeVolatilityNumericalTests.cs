using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RelativeVolatilityNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("RVI",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    private static StockData Batch(StockData data,bool paired,int length,int smooth,MovingAvgType kind)=>paired?data.CalculateRelativeVolatilityIndexV2(kind,length,smooth):data.CalculateRelativeVolatilityIndexV1(kind,length,smooth);
    private static ComputeBuffer Raw(StockData data,ComputeContext context,bool paired,int length,int smooth,MovingAvgType kind)=>paired?IndicatorCompute.ComputeRelativeVolatilityIndexV2Fast(data,context,length,smooth,kind):IndicatorCompute.ComputeRelativeVolatilityIndexFast(data,context,length,smooth,kind);
    [Fact]
    public void PopulationDeviationDirectionalAveragesAndPairedMeanStayBounded()
    {
        foreach(var paired in new[] {false,true})foreach(var length in new[] {0,1,2,3,10})foreach(var smooth in new[] {0,1,3,14})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Enumerable.Range(0,24).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,(i%5+2)*scale,-(i%3+2)*scale,(i%3-1)*scale,1)).ToArray(),paired,length,smooth,kind);
        foreach(var paired in new[] {false,true}) {Check(Array.Empty<Bar>(),paired,10,14,MovingAvgType.WildersSmoothingMethod);var flat=Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray();Check(flat,paired,3,2,MovingAvgType.ExponentialMovingAverage);Assert.All(Batch(Data(flat),paired,3,2,MovingAvgType.ExponentialMovingAverage).ChainedValues,v=>Assert.Equal(100d,v));}
        var reversal=new[] {1d,3,1}.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();Check(reversal,false,2,1,MovingAvgType.WildersSmoothingMethod);Assert.Equal(new[] {100d,100,0},Batch(Data(reversal),false,2,1,MovingAvgType.WildersSmoothingMethod).ChainedValues);
        var split=Enumerable.Range(0,4).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,2+i,-2-i,0,1)).ToArray();Check(split,true,3,1,MovingAvgType.WildersSmoothingMethod);Assert.Equal(new[] {100d,100,50,50},Batch(Data(split),true,3,1,MovingAvgType.WildersSmoothingMethod).ChainedValues);
    }
    private static void Check(Bar[] bars,bool paired,int length,int smooth,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.RelativeVolatilityOutputs(bars,paired,length,smooth,Kind(kind))["Rvi"];Assert.All(expected,v=>Assert.InRange(v,0d,100d));Assert.Equal(expected,Batch(Data(bars),paired,length,smooth,kind).ChainedValues);using var context=new ComputeContext();using var raw=Raw(Data(bars),context,paired,length,smooth,kind);Assert.Equal(expected,raw.Span.ToArray());IStreamingIndicatorState state=paired?new RelativeVolatilityIndexV2State(kind,length,smooth):new RelativeVolatilityIndexV1State(kind,length,smooth);using var disposable=(IDisposable)state;
        for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,true).Outputs!["Rvi"]);}}
    }
    [Fact]
    public void SelectedCloseAndFourCustomerLegsPreserveTheirSourcesAndOrder()
    {
        var bars=Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,20+i,-20-2*i,0,1)).ToArray();var selected=new[] {2d,7,3,8,4,9,1,5};var selectedBars=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();using var context=new ComputeContext();var supplied=new[] {new[] {1d,2,3,4,5,6,7,8},new[] {8d,7,6,5,4,3,2,1},new[] {2d,3,4,5,6,7,8,9},new[] {9d,8,7,6,5,4,3,2}};
        foreach(var paired in new[] {false,true})foreach(var batch in new[] {false,true})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.RelativeVolatilityOutputs(selectedBars,paired,2,3,6,external?supplied:null)["Rvi"];var data=Data(bars);data.SetCustomValues(selected.ToList());var callbacks=new List<Func<IReadOnlyList<double>,int,IReadOnlyList<double>>>();
            for(var k=0;k<(paired?4:2);k++) {var leg=k;callbacks.Add((v,p)=>{Assert.Equal(3,p);var values=paired?bars.Select(b=>leg<2?b.High:b.Low).ToArray():selected;var movements=values.Select((x,i)=>i==0?0:(leg%2==0?x>values[i-1]:x<values[i-1])?Math.Abs(x-values[i-1])/2:0).ToArray();Assert.Equal(movements,v);return supplied[leg];});}
            using var armed=external?ComponentAverage.Arm(callbacks):null;if(batch)Assert.Equal(expected,Batch(data,paired,2,3,MovingAvgType.WildersSmoothingMethod).ChainedValues);else {using var raw=Raw(data,context,paired,2,3,MovingAvgType.WildersSmoothingMethod);Assert.Equal(expected,raw.Span.ToArray());}if(external)Assert.Equal(paired?4:2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceDeviationDirectionOrSmoothers()
    {
        foreach(var paired in new[] {false,true})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            IStreamingIndicatorState state=paired?new RelativeVolatilityIndexV2State(length:2,smoothLength:3):new RelativeVolatilityIndexV1State(length:2,smoothLength:3);IStreamingIndicatorState control=paired?new RelativeVolatilityIndexV2State(length:2,smoothLength:3):new RelativeVolatilityIndexV1State(length:2,smoothLength:3);using var d1=(IDisposable)state;using var d2=(IDisposable)control;var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("RVI",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var close in new[] {4d,7,3,9,2,5}) {var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),close,close+2,close-1,close,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);}
        }
    }
    [Fact]
    public void DirectInertiaConsumerKeepsBatchRawAndNativeParity()
    {
        var bars=Enumerable.Range(0,40).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),10,12+Math.Sin(i*.7),8+Math.Cos(i*.4),10,1)).ToArray();using var context=new ComputeContext();foreach(var length in new[] {1,3,20})foreach(var smooth in new[] {1,4,14}) {var expected=Data(bars).CalculateInertiaIndicator(length:length,rviLength:smooth).ChainedValues;using var raw=IndicatorCompute.ComputeInertiaFast(Data(bars),context,length,smooth);Assert.Equal(expected,raw.Span.ToArray());using var state=new InertiaIndicatorState(length:length,rviLength:smooth);Assert.Equal(expected,bars.Select(b=>state.Update(Native(b),true,false).Value).ToArray());}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RelativeVolatilityIndex)||c.IndicatorType==typeof(RviVolatility)||c.IndicatorType==typeof(RelativeVolatilityIndexV2)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.RelativeVolatilityOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
