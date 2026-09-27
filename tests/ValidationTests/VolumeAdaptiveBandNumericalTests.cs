using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolumeAdaptiveBandNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices,double[]? volumes=null)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,volumes?[i]??2)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("VAB",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void VolumeFloorSeedAndSeparateRecurrencesPreserveExtendedValues()
    {
        foreach(var length in new[] {0,1,2,3,14})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var volume in new[] {0d,.5,2,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,12).Select(i=>(i%11-5)*scale).ToArray(),Enumerable.Range(0,12).Select(i=>i%3==0?0:volume).ToArray()),length,kind);
        Check(Array.Empty<Bar>(),100,MovingAvgType.SimpleMovingAverage);
        var hand=Data(Bars(new[] {2d,4,6})).CalculateVolumeAdaptiveBands(length:1);Assert.Equal(new[] {3d,5.5,8.75},hand.OutputValues["UpperBand"]);Assert.Equal(new[] {1d,3.5,4.25},hand.OutputValues["LowerBand"]);Assert.Equal(new[] {2d,4.5,6.5},hand.OutputValues["MiddleBand"]);
        var extreme=Bars(new[] {double.MaxValue,-double.MaxValue,-double.MaxValue},new[] {0d,0,0});Check(extreme,2,MovingAvgType.SimpleMovingAverage);var batch=Data(extreme).CalculateVolumeAdaptiveBands(length:2);Assert.Equal(double.MaxValue/2,batch.OutputValues["UpperBand"][2]);
        Check(Bars(new[] {double.MaxValue,double.MaxValue},new[] {double.MaxValue,double.MaxValue}),1,MovingAvgType.SimpleMovingAverage);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.VolumeAdaptiveBandOutputs(bars,length,code);var batch=Data(bars).CalculateVolumeAdaptiveBands(kind,length);using var context=new ComputeContext();
        foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeVolumeAdaptiveBandsFast(Data(bars),context,length,kind,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new VolumeAdaptiveBandsState(kind,length);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesKeepOriginalVolumeAndOrderedCustomerAverages()
    {
        var prices=new[] {2d,4,6};var volumes=new[] {3d,5,7};using var context=new ComputeContext();foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var data=Data(Bars(new[] {4d,4,4},volumes));data.SetCustomValues(prices.ToList());var volumeMean=new[] {2d,2,2};var upper=new[] {8d,9,10};var lower=new[] {1d,2,3};var expected=BuiltInFormulaReferences.VolumeAdaptiveBandOutputs(Bars(prices,volumes),2,1,external?volumeMean:null,external?upper:null,external?lower:null);
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(2,period);Assert.Equal(volumes,values);return volumeMean;},(values,period)=>{Assert.Equal(2,period);Assert.Equal(new[] {3d,5.5,8.75},values);return upper;},(values,period)=>{Assert.Equal(2,period);Assert.Equal(new[] {1d,3.5,4.25},values);return lower;}}):null;
            if(batch) {var output=data.CalculateVolumeAdaptiveBands(length:2);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key],output.OutputValues[key]);}else {using var raw=IndicatorCompute.ComputeVolumeAdaptiveBandsFast(data,context,2,outputKey:"MiddleBand");Assert.Equal(expected["MiddleBand"],raw.Span.ToArray());}if(external)Assert.Equal(3,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new VolumeAdaptiveBandsState(length:2);using var control=new VolumeAdaptiveBandsState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("VAB",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchAveragesKeepThreeOrderedStages()
    {
        var bars=Bars(new[] {2d,7,4,9,3,5});var kind=MovingAvgType.HullMovingAverage;var volumes=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,bars.Select(b=>b.Volume).ToList()).ToArray();var recurrence=BuiltInFormulaReferences.VolumeAdaptiveBandOutputs(bars,3,1,volumes);var upper=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,recurrence["RawUp"].ToList()).ToArray();var lower=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,recurrence["RawDown"].ToList()).ToArray();var expected=BuiltInFormulaReferences.VolumeAdaptiveBandOutputs(bars,3,1,volumes,upper,lower);var batch=Data(bars).CalculateVolumeAdaptiveBands(kind,3);Assert.Equal(expected["UpperBand"],batch.OutputValues["UpperBand"]);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeVolumeAdaptiveBandsFast(Data(bars),context,3,kind,"LowerBand");Assert.Equal(expected["LowerBand"],raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(VolumeAdaptiveBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.VolumeAdaptiveBandOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
