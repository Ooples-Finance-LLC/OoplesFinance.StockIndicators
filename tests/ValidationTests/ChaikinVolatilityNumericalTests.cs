using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChaikinVolatilityNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("CMF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double v)=>new(DateTime.UnixEpoch.AddMinutes(i),c,h,l,c,v);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void ExtendedRangesAndLaggedAverageReturnsMatchIndependentFractions()
    {
        foreach(var smooth in new[] {0,1,3,10})foreach(var lag in new[] {0,1,3,12,int.MaxValue})
        foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})Check(Enumerable.Range(0,24).Select(i=>Candle(i,(i%5)*scale,-(i%3)*scale,0,1)).ToArray(),smooth,lag,kind);
            Check(new[] {Candle(0,double.MaxValue,-double.MaxValue,0,1),Candle(1,double.MaxValue/2,-double.MaxValue/2,0,1),Candle(2,0,0,0,1),Candle(3,2,0,1,1)},smooth,lag,kind);
        }
        Check(Array.Empty<Bar>(),10,12,MovingAvgType.ExponentialMovingAverage);
        Check(Enumerable.Range(0,6).Select(i=>Candle(i,i+1,0,1,1)).ToArray(),int.MaxValue,2,MovingAvgType.ExponentialMovingAverage);
        var hand=new[] {Candle(0,1,0,0,1),Candle(1,2,0,0,1),Candle(2,4,0,0,1),Candle(3,8,0,0,1)};
        Assert.Equal(new[] {0d,100,100,100},BuiltInFormulaReferences.ChaikinVolatilityOutputs(hand,1,1,3)["Cv"]);Check(hand,1,1,MovingAvgType.ExponentialMovingAverage);
    }
    private static void Check(Bar[] bars,int smooth,int lag,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.ChaikinVolatilityOutputs(bars,smooth,lag,Kind(kind))["Cv"];
        Assert.Equal(expected,Data(bars).CalculateChaikinVolatility(kind,smooth,lag).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeChaikinVolatilityFast(Data(bars),context,smooth,kind,lag);Assert.Equal(expected,raw.Span.ToArray());
        if(kind==MovingAvgType.ExponentialMovingAverage){var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.VolatilityCore.ChaikinVolatility(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),core,smooth,lag);Assert.Equal(expected,core);}
        using var state=new ChaikinVolatilityState(kind,smooth,lag);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,40,-30,20,7)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],result.Outputs!["Cv"]);}}}
    }
    [Fact]
    public void SelectedClosesPreserveRangesAndCustomerAverageReceivesItsPeriod()
    {
        var original=Enumerable.Range(0,8).Select(i=>Candle(i,i+3,-i-1,1,1)).ToArray();var selected=Enumerable.Range(0,8).Select(i=>100d+i).ToArray();var supplied=new[] {1d,3,5,7,2,0,9,1};
        foreach(var batch in new[] {false,true})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.ChaikinVolatilityOutputs(original,3,2,3,external?supplied:null)["Cv"];
            var data=Data(original);data.SetCustomValues(selected.ToList());
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>{Assert.Equal(3,p);Assert.Equal(original.Select(b=>b.High-b.Low),v);return supplied;}}):null;
            if(batch)Assert.Equal(expected,data.CalculateChaikinVolatility(MovingAvgType.ExponentialMovingAverage,3,2).ChainedValues);
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeChaikinVolatilityFast(data,context,3,MovingAvgType.ExponentialMovingAverage,2);Assert.Equal(expected,raw.Span.ToArray());}
            if(external)Assert.Equal(1,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceAverageOrLag()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new ChaikinVolatilityState(length1:2,length2:3);using var control=new ChaikinVolatilityState(length1:2,length2:3);var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("CV",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,i+1,-2,0,i+1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(ChaikinVolatility)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.ChaikinVolatilityOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
