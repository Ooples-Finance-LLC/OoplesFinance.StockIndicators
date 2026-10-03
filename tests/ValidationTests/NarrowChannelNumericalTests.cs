using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Band = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ChannelBand;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class NarrowChannelNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("NARROW",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void AliasUsesRoundedMeanAndExactPopulationDeviation()
    {
        foreach(var length in new[] {0,1,2,5})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,16).Select(i=>(i%11-5)*scale).ToArray()),length,kind);
        Check(Array.Empty<Bar>(),14,MovingAvgType.SimpleMovingAverage);Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.MaxValue,0d}),2,MovingAvgType.SimpleMovingAverage);
        Check(Bars(Enumerable.Repeat(double.MaxValue,8).ToArray()),3,MovingAvgType.SimpleMovingAverage);
        var bars=Bars(new[] {2d,4,6});var batch=Data(bars).CalculateNarrowSidewaysChannel(length:2);Assert.Equal(new[] {0d,6,8},batch.OutputValues["UpperBand"]);Assert.Equal(new[] {0d,3,5},batch.OutputValues["MiddleBand"]);Assert.Equal(new[] {0d,0,2},batch.OutputValues["LowerBand"]);
        foreach(var multiplier in new[] {-1d,0,.5,3,double.MaxValue}) {var expected=BuiltInFormulaReferences.RoundedBollinger(bars,2,1,multiplier);var output=Data(bars).CalculateNarrowSidewaysChannel(length:2,stdDevMult:multiplier);using var state=new NarrowSidewaysChannelState(length:2,stdDevMult:multiplier);for(var i=0;i<bars.Length;i++) {var native=state.Update(Native(bars[i]),true,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key][i],output.OutputValues[key][i]);Assert.Equal(expected[key][i],native.Outputs![key]);}}}
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.RoundedBollinger(bars,Math.Max(1,length),code,3);var batch=Data(bars).CalculateNarrowSidewaysChannel(kind,length);using var context=new ComputeContext();
        foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeNarrowSidewaysChannelFast(Data(bars),context,length,kind,key=="UpperBand"?Band.Upper:key=="LowerBand"?Band.Lower:Band.Middle);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new NarrowSidewaysChannelState(kind,length);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesAndCustomerMeanKeepDeviationOnOriginalSelectedPrices()
    {
        var bars=Bars(new[] {4d,4,4});var prices=new[] {2d,4,6};using var context=new ComputeContext();
        foreach(var batch in new[] {false,true})foreach(var band in new[] {Band.Upper,Band.Middle,Band.Lower})
        {
            var data=Data(bars);data.SetCustomValues(prices.ToList());using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(2,period);Assert.Equal(prices,values);return new[] {10d,20,30};}});
            var expected=band==Band.Upper?new[] {10d,23,33}:band==Band.Lower?new[] {10d,17,27}:new[] {10d,20,30};
            if(batch)Assert.Equal(expected,data.CalculateNarrowSidewaysChannel(length:2).OutputValues[band==Band.Upper?"UpperBand":band==Band.Lower?"LowerBand":"MiddleBand"]);
            else {using var raw=IndicatorCompute.ComputeNarrowSidewaysChannelFast(data,context,2,band:band);Assert.Equal(expected,raw.Span.ToArray());}Assert.Equal(1,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void ObsoletePercentageDoesNotReplaceTheThreeDeviationContract()
    {
        var bars=Bars(new[] {2d,4,6});var expected=BuiltInFormulaReferences.RoundedBollinger(bars,2,1,3);using var context=new ComputeContext();
        foreach(var pct in new[] {0d,.03,10}) {var spec=new IndicatorSpec(IndicatorName.NarrowSidewaysChannel,new NarrowSidewaysChannelSpecOptions(2,pct),"UpperBand");using var raw=IndicatorCompute.TryComputeFast(Data(bars),spec,context);Assert.NotNull(raw);Assert.Equal(expected["UpperBand"],raw.Value.Span.ToArray());using var state=(NarrowSidewaysChannelState)StatefulIndicatorFactory.Create(spec);for(var i=0;i<bars.Length;i++)Assert.Equal(expected["UpperBand"][i],state.Update(Native(bars[i]),true,true).Outputs!["UpperBand"]);}
    }
    [Fact]
    public void InvalidFieldsAndMultipliersAreRejectedBeforeStateChanges()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new NarrowSidewaysChannelState(length:2);using var control=new NarrowSidewaysChannelState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("NARROW",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>new NarrowSidewaysChannelState(stdDevMult:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateNarrowSidewaysChannel(stdDevMult:invalid));}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(NarrowSidewaysChannel)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.NarrowSidewaysOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
