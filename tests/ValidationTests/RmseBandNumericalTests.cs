using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RmseBandNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("RMSE",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void SquaredResidualSmoothingPreservesTinyWidthsAndExtendedRoots()
    {
        foreach(var length in new[] {0,1,2,3,14})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var factor in new[] {-1d,0,.5,1,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,12).Select(i=>(i%11-5)*scale).ToArray()),length,kind,factor);
        Check(Array.Empty<Bar>(),14,MovingAvgType.SimpleMovingAverage,1);
        var tiny=Bars(new[] {double.Epsilon,0d});Check(tiny,2,MovingAvgType.SimpleMovingAverage,1);var small=Data(tiny).CalculateRootMovingAverageSquaredErrorBands(length:2);Assert.Equal(double.Epsilon,small.OutputValues["UpperBand"][1]);Assert.Equal(-double.Epsilon,small.OutputValues["LowerBand"][1]);
        var extreme=Bars(new[] {-double.MaxValue,-double.MaxValue,double.MaxValue});Check(extreme,3,MovingAvgType.SimpleMovingAverage,.5);var big=Data(extreme).CalculateRootMovingAverageSquaredErrorBands(.5,length:3);Assert.All(big.OutputValues["UpperBand"],v=>Assert.True(double.IsFinite(v)));Assert.All(big.OutputValues["LowerBand"],v=>Assert.True(double.IsFinite(v)));
        var batch=Data(Bars(new[] {2d,4,6})).CalculateRootMovingAverageSquaredErrorBands(length:2);Assert.Equal(new[] {0d,3,5},batch.OutputValues["MiddleBand"]);Assert.Equal(6,batch.OutputValues["UpperBand"][2]);Assert.Equal(4,batch.OutputValues["LowerBand"][2]);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind,double factor)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.RmseBandOutputs(bars,length,code,factor);var batch=Data(bars).CalculateRootMovingAverageSquaredErrorBands(factor,kind,length);using var context=new ComputeContext();
        foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeRmseBandsFast(Data(bars),context,length,factor,kind,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new RootMovingAverageSquaredErrorBandsState(factor,kind,length);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesAndCustomerMeanThenSquaredResidualAverageKeepTheirOrder()
    {
        var prices=new[] {2d,4,6};using var context=new ComputeContext();foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var data=Data(Bars(new[] {4d,4,4}));data.SetCustomValues(prices.ToList());var means=new[] {1d,1,1};var variance=new[] {4d,9,-1};var expected=BuiltInFormulaReferences.RmseBandOutputs(Bars(prices),2,1,2,external?means:null,external?variance:null);
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(2,period);Assert.Equal(prices,values);return means;},(values,period)=>{Assert.Equal(2,period);Assert.Equal(new[] {1d,9,25},values);return variance;}}):null;
            if(batch) {var output=data.CalculateRootMovingAverageSquaredErrorBands(2,length:2);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key],output.OutputValues[key]);}else {using var raw=IndicatorCompute.ComputeRmseBandsFast(data,context,2,2,outputKey:"UpperBand");Assert.Equal(expected["UpperBand"],raw.Span.ToArray());}if(external)Assert.Equal(2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsAndFactorsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new RootMovingAverageSquaredErrorBandsState(length:2);using var control=new RootMovingAverageSquaredErrorBandsState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("RMSE",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>new RootMovingAverageSquaredErrorBandsState(invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateRootMovingAverageSquaredErrorBands(invalid));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeRmseBandsFast(Data(Array.Empty<Bar>()),context,factor:invalid));}
    }
    [Fact]
    public void LegacyBatchAveragesKeepBothResidualStages()
    {
        var bars=Bars(new[] {2d,7,4,9,3,5});var kind=MovingAvgType.HullMovingAverage;var mean=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,bars.Select(b=>b.Close).ToList()).ToArray();var squares=BuiltInFormulaReferences.RmseBandOutputs(bars,3,1,1,mean)["RawSquare"];var variance=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,squares.ToList()).ToArray();var expected=BuiltInFormulaReferences.RmseBandOutputs(bars,3,1,1,mean,variance);var batch=Data(bars).CalculateRootMovingAverageSquaredErrorBands(1,kind,3);Assert.Equal(expected["UpperBand"],batch.OutputValues["UpperBand"]);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeRmseBandsFast(Data(bars),context,3,1,kind,"LowerBand");Assert.Equal(expected["LowerBand"],raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RootMovingAverageSquaredErrorBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.RmseBandOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
