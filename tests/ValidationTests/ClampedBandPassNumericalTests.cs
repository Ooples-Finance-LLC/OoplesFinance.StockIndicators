using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ClampedBandPassNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("BPF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static StockData Batch(StockData data,int length,double bandwidth,int variant)=>variant==0?data.CalculateEhlersBandPassFilterV1(length,bandwidth):variant==1?data.CalculateEhlersBandPassFilterV2(length,bandwidth):data.CalculateEhlersCycleBandPassFilter(length,bandwidth);
    private static IStreamingIndicatorState State(int length,double bandwidth,int variant)=>variant==0?new EhlersBandPassFilterV1State(length,bandwidth):variant==1?new EhlersBandPassFilterV2State(length,bandwidth):new EhlersCycleBandPassFilterState(length,bandwidth);
    private static ComputeBuffer Raw(StockData data,ComputeContext context,int length,double bandwidth,int variant,string? key=null)=>variant==0?IndicatorCompute.ComputeEhlersBandPassFilterV1Fast(data,context,length,bandwidth,key):variant==1?IndicatorCompute.ComputeEhlersBandPassFilterV2Fast(data,context,length,bandwidth):IndicatorCompute.ComputeEhlersCycleBandPassFilterFast(data,context,length,bandwidth);
    private static void Core(double[] input,double[] output,int length,double bandwidth,int variant) {if(variant==0)OscillatorCore.EhlersBandPassFilterV1(input,output,length,bandwidth);else if(variant==1)OscillatorCore.EhlersBandPassFilterV2(input,output,length,bandwidth);else OscillatorCore.EhlersCycleBandPassFilter(input,output,length,bandwidth);}
    [Fact]
    public void DistinctStartupAndPeakNormalizationPreserveExtendedRecurrences()
    {
        foreach(var variant in new[] {0,1,2})foreach(var length in new[] {0,1,2,3,20,int.MaxValue})foreach(var bandwidth in new[] {-1d,0,.1,.3,2,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,12).Select(i=>(i%11-5)*scale).ToArray()),length,bandwidth,variant);
        foreach(var variant in new[] {0,1,2}) {Check(Array.Empty<Bar>(),20,.3,variant);Check(Bars(Enumerable.Repeat(double.MaxValue,12).ToArray()),20,.3,variant);Check(Bars(new[] {-double.MaxValue,double.MaxValue,double.MaxValue,-double.MaxValue,double.MaxValue,-double.MaxValue,0d,0,0,0}),20,.3,variant);}
        var impulse=Bars(new[] {0d,0,1,0,0,0,0});var normalized=Batch(Data(impulse),20,.3,0).OutputValues["Ebpf"];var plain=Batch(Data(impulse),20,.3,1).OutputValues["Ebpf"];var cycle=Batch(Data(impulse),20,.1,2).OutputValues["Ecbpf"];Assert.Equal(new[] {0d,0,0},normalized.Take(3));Assert.Equal(-1,normalized[3]);Assert.Equal(new[] {0d,0,0},plain.Take(3));Assert.True(plain[4]<0);Assert.True(cycle[2]>0);
        var extreme=Batch(Data(Bars(Enumerable.Range(0,64).Select(i=>i%3==0?double.MaxValue:-double.MaxValue).ToArray())),20,.3,0);Assert.All(extreme.OutputValues["Ebpf"],v=>Assert.InRange(v,-1d,1d));Assert.All(extreme.OutputValues["Signal"],v=>Assert.True(double.IsFinite(v)));
    }
    private static void Check(Bar[] bars,int length,double bandwidth,int variant)
    {
        var expected=BuiltInFormulaReferences.ClampedBandPassOutputs(bars,length,bandwidth,variant);var batch=Batch(Data(bars),length,bandwidth,variant);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=Raw(Data(bars),context,length,bandwidth,variant,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        var core=new double[bars.Length];Core(bars.Select(b=>b.Close).ToArray(),core,length,bandwidth,variant);Assert.Equal(expected[variant==2?"Ecbpf":"Ebpf"],core);
        var state=State(length,bandwidth,variant);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesDriveBothFilterAndTrigger()
    {
        var prices=new[] {2d,7,3,8,4,9,1};using var context=new ComputeContext();foreach(var variant in new[] {0,1,2})
        {
            var expected=BuiltInFormulaReferences.ClampedBandPassOutputs(Bars(prices),3,.3,variant);foreach(var key in expected.Keys) {var data=Data(Bars(Enumerable.Repeat(4d,prices.Length).ToArray()));data.SetCustomValues(prices.ToList());var batch=Batch(data,3,.3,variant);Assert.Equal(expected[key],batch.OutputValues[key]);data=Data(Bars(Enumerable.Repeat(4d,prices.Length).ToArray()));data.SetCustomValues(prices.ToList());using var raw=Raw(data,context,3,.3,variant,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        }
    }
    [Fact]
    public void InvalidFieldsAndBandwidthNeverAdvanceStateOrEnterEmptyExecution()
    {
        foreach(var variant in new[] {0,1,2})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            var state=State(3,.3,variant);var control=State(3,.3,variant);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("BPF",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var b in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(b),true,true).Outputs!,state.Update(Native(b),true,true).Outputs!);
        }
        foreach(var variant in new[] {0,1,2})
        {
            foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>State(20,invalid,variant));Assert.Throws<ArgumentOutOfRangeException>(()=>Batch(Data(Array.Empty<Bar>()),20,invalid,variant));Assert.Throws<ArgumentOutOfRangeException>(()=>Core(Array.Empty<double>(),Array.Empty<double>(),20,invalid,variant));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>Raw(Data(Array.Empty<Bar>()),context,20,invalid,variant));}
            Assert.Throws<ArgumentException>(()=>Core(new[] {1d},Array.Empty<double>(),20,.3,variant));
        }
    }
    [Fact]
    public void ExistingCycleAmplitudeConsumersKeepBatchCoreAndNativeParity()
    {
        var prices=Enumerable.Range(0,40).Select(i=>10+Math.Sin(i*.7)*4).ToArray();var bars=Bars(prices);foreach(var length in new[] {1,3,20}) {var expected=Data(bars).CalculateEhlersCycleAmplitude(length).ChainedValues;var core=new double[prices.Length];OscillatorCore.EhlersCycleAmplitude(prices,core,length);Assert.Equal(expected,core);var state=new EhlersCycleAmplitudeState(length);var native=bars.Select(b=>state.Update(Native(b),true,false).Value).ToArray();Assert.Equal(expected,native);}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EhlersBandPassFilterV1)||c.IndicatorType==typeof(EhlersBandPassFilterV2)||c.IndicatorType==typeof(EhlersCycleBandPassFilter)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.ClampedBandPassOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
