using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AutoDispersionNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("ADB",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void ChangeRmsRollingExtremaAndTwoSmoothingStagesPreserveFiniteBands()
    {
        foreach(var length in new[] {0,1,2,3,14})foreach(var smooth in new[] {0,1,2,5})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,16).Select(i=>(i%11-5)*scale).ToArray()),length,smooth,kind);
        Check(Array.Empty<Bar>(),90,140,MovingAvgType.WeightedMovingAverage);
        var hand=Data(Bars(new[] {2d,4,6})).CalculateAutoDispersionBands(MovingAvgType.SimpleMovingAverage,1,1);Assert.Equal(new[] {2d,6,8},hand.OutputValues["UpperBand"]);Assert.Equal(new[] {2d,2,4},hand.OutputValues["LowerBand"]);Assert.Equal(new[] {2d,4,6},hand.OutputValues["MiddleBand"]);
        var tiny=Bars(new[] {0d,0,double.Epsilon});Check(tiny,2,1,MovingAvgType.SimpleMovingAverage);Assert.Equal(double.Epsilon,Data(tiny).CalculateAutoDispersionBands(MovingAvgType.SimpleMovingAverage,2,1).OutputValues["UpperBand"][2]);
        var amplitude=Math.ScaleB(1d,1023);var extreme=Bars(new[] {-amplitude,amplitude});Check(extreme,1,2,MovingAvgType.SimpleMovingAverage);var big=Data(extreme).CalculateAutoDispersionBands(MovingAvgType.SimpleMovingAverage,1,2);Assert.Equal(amplitude,big.OutputValues["UpperBand"][1]);Assert.Equal(-amplitude,big.OutputValues["LowerBand"][1]);Assert.Equal(0,big.OutputValues["MiddleBand"][1]);
        Check(Bars(new[] {8d,7,6,5,4,3,2,1,0,8,0,8}),3,2,MovingAvgType.WeightedMovingAverage);Check(Bars(new[] {-8d,-7,-6,-5,-4,-3,-2,-1,0,-8,0,-8}),3,2,MovingAvgType.WeightedMovingAverage);
    }
    private static void Check(Bar[] bars,int length,int smooth,MovingAvgType kind)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.AutoDispersionOutputs(bars,length,smooth,code);var batch=Data(bars).CalculateAutoDispersionBands(kind,length,smooth);using var context=new ComputeContext();
        foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeAutoDispersionBandsFast(Data(bars),context,length,smooth,kind,key=="UpperBand"?IndicatorCompute.ChannelBand.Upper:key=="LowerBand"?IndicatorCompute.ChannelBand.Lower:IndicatorCompute.ChannelBand.Middle);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new AutoDispersionBandsState(kind,length,smooth);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesAndFourCustomerAveragesKeepBothSmoothingLengths()
    {
        var prices=new[] {2d,4,6};using var context=new ComputeContext();foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var data=Data(Bars(new[] {4d,4,4}));data.SetCustomValues(prices.ToList());var stages=new[] {new[] {5d,7,9},new[] {10d,12,14},new[] {1d,2,3},new[] {0d,1,2}};var expected=BuiltInFormulaReferences.AutoDispersionOutputs(Bars(prices),1,2,1,external?stages:null);
            using var armed=external?ComponentAverage.Arm(Enumerable.Range(0,4).Select(slot=>(Func<IReadOnlyList<double>,int,IReadOnlyList<double>>)((values,period)=>{Assert.Equal(slot%2==0?1:2,period);Assert.Equal(slot==0?new[] {2d,6,8}:slot==2?new[] {2d,2,4}:stages[slot-1],values);return stages[slot];})).ToArray()):null;
            if(batch) {var output=data.CalculateAutoDispersionBands(MovingAvgType.SimpleMovingAverage,1,2);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key],output.OutputValues[key]);}else {using var raw=IndicatorCompute.ComputeAutoDispersionBandsFast(data,context,1,2,MovingAvgType.SimpleMovingAverage,IndicatorCompute.ChannelBand.Upper);Assert.Equal(expected["UpperBand"],raw.Span.ToArray());}if(external)Assert.Equal(4,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new AutoDispersionBandsState(length:2,smoothLength:3);using var control=new AutoDispersionBandsState(length:2,smoothLength:3);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("ADB",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchAveragesKeepFourStages()
    {
        var bars=Bars(new[] {2d,7,4,9,3,5,8,10});var kind=MovingAvgType.HullMovingAverage;var rawValues=BuiltInFormulaReferences.AutoDispersionOutputs(bars,2,3,1);var firstUpper=CalculationsHelper.GetMovingAverageList(Data(bars),kind,2,rawValues["Maxima"].ToList()).ToArray();var upper=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,firstUpper.ToList()).ToArray();var firstLower=CalculationsHelper.GetMovingAverageList(Data(bars),kind,2,rawValues["Minima"].ToList()).ToArray();var lower=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,firstLower.ToList()).ToArray();var expected=BuiltInFormulaReferences.AutoDispersionOutputs(bars,2,3,1,new[] {firstUpper,upper,firstLower,lower});var batch=Data(bars).CalculateAutoDispersionBands(kind,2,3);Assert.Equal(expected["UpperBand"],batch.OutputValues["UpperBand"]);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeAutoDispersionBandsFast(Data(bars),context,2,3,kind,IndicatorCompute.ChannelBand.Lower);Assert.Equal(expected["LowerBand"],raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(AutoDispersionBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.AutoDispersionOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
