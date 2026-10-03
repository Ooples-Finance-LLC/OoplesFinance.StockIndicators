using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DailyDeltaNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("CMF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double v)=>new(DateTime.UnixEpoch.AddMinutes(i),c,h,l,c,v);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void SeparateAveragesAndExtendedBandsMatchIndependentFractions()
    {
        foreach(var length in new[] {0,1,3,21})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
                Check(Enumerable.Range(0,36).Select(i=>Candle(i,(i%5+1)*scale,-(i%3+1)*scale,0,1)).ToArray(),length,kind);
            Check(new[] {Candle(0,double.MaxValue,-double.MaxValue,0,1),Candle(1,2,0,1,1),Candle(2,0,0,0,1)},length,kind);
        }
        Check(Array.Empty<Bar>(),21,MovingAvgType.SimpleMovingAverage);
        var hand=new[] {Candle(0,2,0,1,1),Candle(1,4,2,3,1),Candle(2,8,4,6,1)};
        var expected=BuiltInFormulaReferences.DailyDeltaOutputs(hand,2,1);Assert.Equal(new[] {2d,6,11},expected["UpperBand"]);Assert.Equal(new[] {0d,0,1},expected["LowerBand"]);Check(hand,2,MovingAvgType.SimpleMovingAverage);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.DailyDeltaOutputs(bars,length,Kind(kind));var batch=Data(bars).CalculateDailyAveragePriceDelta(kind,length);
        foreach(var key in expected.Keys)Assert.Equal(expected[key],batch.ChainedOutputs[key]);
        using var context=new ComputeContext();foreach(var lower in new[] {false,true}){using var raw=IndicatorCompute.ComputeDailyAveragePriceDeltaFast(Data(bars),context,length,kind,lower);Assert.Equal(expected[lower?"LowerBand":"UpperBand"],raw.Span.ToArray());}
        using var state=new DailyAveragePriceDeltaState(kind,length);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,40,-30,20,7)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["UpperBand"][i],result.Value);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],result.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedClosesDoNotReplaceRangesAndBothCustomerStagesAreHonored()
    {
        var original=Enumerable.Range(0,6).Select(i=>Candle(i,i+3,-i-1,1,1)).ToArray();var selected=Enumerable.Range(0,6).Select(i=>100d+i).ToArray();var suppliedHigh=new[] {2d,7,3,4,8,1};var suppliedLow=new[] {-2d,1,-1,0,2,-3};
        foreach(var route in new[] {"batch","upper","lower"})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.DailyDeltaOutputs(original,3,3,external?suppliedHigh:null,external?suppliedLow:null);
            var data=Data(original);data.SetCustomValues(selected.ToList());
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>{Assert.Equal(3,p);Assert.Equal(original.Select(b=>b.High),v);return suppliedHigh;},(v,p)=>{Assert.Equal(3,p);Assert.Equal(original.Select(b=>b.Low),v);return suppliedLow;}}):null;
            if(route=="batch"){var result=data.CalculateDailyAveragePriceDelta(MovingAvgType.ExponentialMovingAverage,3);foreach(var key in expected.Keys)Assert.Equal(expected[key],result.ChainedOutputs[key]);}
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeDailyAveragePriceDeltaFast(data,context,3,MovingAvgType.ExponentialMovingAverage,route=="lower");Assert.Equal(expected[route=="lower"?"LowerBand":"UpperBand"],raw.Span.ToArray());}
            if(external)Assert.Equal(2,ComponentAverage.Substitutions);
        }
        var extremes=new[] {Candle(0,-double.MaxValue,-double.MaxValue,-double.MaxValue,1),Candle(1,double.MaxValue,double.MaxValue,double.MaxValue,1)};
        var ah=new[] {double.MaxValue,double.MaxValue};var al=new[] {-double.MaxValue,-double.MaxValue};var wanted=BuiltInFormulaReferences.DailyDeltaOutputs(extremes,1,1,ah,al);
        Assert.Equal(double.MaxValue,wanted["UpperBand"][0]);Assert.Equal(-double.MaxValue,wanted["LowerBand"][1]);
        foreach(var route in new[] {"batch","upper","lower"})
        {
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>ah,(v,p)=>al});
            if(route=="batch"){var result=Data(extremes).CalculateDailyAveragePriceDelta(length:1);foreach(var key in wanted.Keys)Assert.Equal(wanted[key],result.ChainedOutputs[key]);}
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeDailyAveragePriceDeltaFast(Data(extremes),context,1,MovingAvgType.SimpleMovingAverage,route=="lower");Assert.Equal(wanted[route=="lower"?"LowerBand":"UpperBand"],raw.Span.ToArray());}
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceEitherAverage()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new DailyAveragePriceDeltaState(length:3);using var control=new DailyAveragePriceDeltaState(length:3);var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("DAPD",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,4,-2,i%5-1,i+1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(DailyAveragePriceDelta)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.DailyDeltaOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
