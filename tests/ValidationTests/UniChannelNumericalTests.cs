using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Band = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ChannelBand;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class UniChannelNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("UNI",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void IndependentAdditiveAndProportionalOffsetsPreserveCancellation()
    {
        foreach(var length in new[] {0,1,3})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var factors in new[] {(.02,.03),(-2d,2d),(0d,0d),(double.MaxValue,-double.MaxValue)})foreach(var additive in new[] {false,true})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,12).Select(i=>(i%11-5)*scale).ToArray()),length,kind,factors.Item1,factors.Item2,additive);
        Check(Array.Empty<Bar>(),10,MovingAvgType.SimpleMovingAverage,.02,.02,false);Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.MaxValue,0d}),2,MovingAvgType.SimpleMovingAverage,-2,2,false);
        var extreme=Data(Bars(new[] {double.MaxValue})).CalculateUniChannel(length:1,ubFac:-2,lbFac:2);Assert.Equal(-double.MaxValue,extreme.OutputValues["UpperBand"][0]);Assert.Equal(-double.MaxValue,extreme.OutputValues["LowerBand"][0]);
        var bars=Bars(new[] {2d,4,6});var proportional=Data(bars).CalculateUniChannel(length:2,ubFac:.5,lbFac:.25);Assert.Equal(new[] {0d,4.5,7.5},proportional.OutputValues["UpperBand"]);Assert.Equal(new[] {0d,2.25,3.75},proportional.OutputValues["LowerBand"]);
        var additiveOutput=Data(bars).CalculateUniChannel(length:2,ubFac:.5,lbFac:.25,type1:true);Assert.Equal(new[] {.5,3.5,5.5},additiveOutput.OutputValues["UpperBand"]);Assert.Equal(new[] {-.25,2.75,4.75},additiveOutput.OutputValues["LowerBand"]);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind,double upper,double lower,bool additive)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.UniChannelOutputs(bars,length,code,upper,lower,additive);var batch=Data(bars).CalculateUniChannel(kind,length,upper,lower,additive);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeUniChannelFast(Data(bars),context,length,upper,lower,additive,kind,key=="UpperBand"?Band.Upper:key=="LowerBand"?Band.Lower:Band.Middle);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new UniChannelState(kind,length,upper,lower,additive);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesAndCustomerMeanFeedBothOffsetModes()
    {
        var prices=new[] {2d,4,6};using var context=new ComputeContext();foreach(var additive in new[] {false,true})foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})foreach(var band in new[] {Band.Upper,Band.Middle,Band.Lower})
        {
            var data=Data(Bars(new[] {4d,4,4}));data.SetCustomValues(prices.ToList());var means=new[] {10d,20,30};var expected=BuiltInFormulaReferences.UniChannelOutputs(Bars(prices),2,1,.5,.25,additive,external?means:null);var key=band==Band.Upper?"UpperBand":band==Band.Lower?"LowerBand":"MiddleBand";
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(2,period);Assert.Equal(prices,values);return means;}}):null;
            if(batch)Assert.Equal(expected[key],data.CalculateUniChannel(length:2,ubFac:.5,lbFac:.25,type1:additive).OutputValues[key]);else {using var raw=IndicatorCompute.ComputeUniChannelFast(data,context,2,.5,.25,additive,band:band);Assert.Equal(expected[key],raw.Span.ToArray());}if(external)Assert.Equal(1,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsAndFactorsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new UniChannelState(length:2);using var control=new UniChannelState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("UNI",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>new UniChannelState(ubFac:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>new UniChannelState(lbFac:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateUniChannel(ubFac:invalid));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeUniChannelFast(Data(Array.Empty<Bar>()),context,lbFac:invalid));}
    }
    [Fact]
    public void LegacyBatchMeanRetainsBothOffsetModes()
    {
        var bars=Bars(new[] {2d,7,4,9,3,5});var kind=MovingAvgType.HullMovingAverage;var mean=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,bars.Select(b=>b.Close).ToList()).ToArray();using var context=new ComputeContext();foreach(var additive in new[] {false,true}) {var expected=BuiltInFormulaReferences.UniChannelOutputs(bars,3,1,.5,.25,additive,mean);var batch=Data(bars).CalculateUniChannel(kind,3,.5,.25,additive);Assert.Equal(expected["UpperBand"],batch.OutputValues["UpperBand"]);using var raw=IndicatorCompute.ComputeUniChannelFast(Data(bars),context,3,.5,.25,additive,kind,Band.Lower);Assert.Equal(expected["LowerBand"],raw.Span.ToArray());}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(UniChannel)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.UniChannelOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
