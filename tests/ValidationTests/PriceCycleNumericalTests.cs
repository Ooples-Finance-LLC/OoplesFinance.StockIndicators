using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PriceCycleNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("CMF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double v)=>new(DateTime.UnixEpoch.AddMinutes(i),c,h,l,c,v);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void ExtendedAverageDistanceAndAtrMatchIndependentFractions()
    {
        foreach(var length in new[] {0,1,2,22})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/4})Check(Enumerable.Range(0,36).Select(i=>Candle(i,3*scale,-3*scale,(i%5-2)*scale,1)).ToArray(),length,kind);
            Check(new[] {Candle(0,double.MaxValue,-double.MaxValue,double.MaxValue,1),Candle(1,double.MaxValue,-double.MaxValue,-double.MaxValue,1),Candle(2,2,-2,0,1),Candle(3,0,0,0,1)},length,kind);
        }
        Check(Array.Empty<Bar>(),22,MovingAvgType.SimpleMovingAverage);
        var hand=new[] {Candle(0,2,0,1,1),Candle(1,4,2,3,1),Candle(2,3,1,2,1)};Assert.Equal(new[] {0d,40,40},BuiltInFormulaReferences.PriceCycleOutputs(hand,2,1)["Pco"]);Check(hand,2,MovingAvgType.SimpleMovingAverage);
        var wide=new[] {Candle(0,double.MaxValue,-double.MaxValue,double.MaxValue,1),Candle(1,double.MaxValue,-double.MaxValue,-double.MaxValue,1)};
        Assert.Equal(new[] {0d,50},BuiltInFormulaReferences.PriceCycleOutputs(wide,2,1)["Pco"]);Check(wide,2,MovingAvgType.SimpleMovingAverage);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.PriceCycleOutputs(bars,length,Kind(kind))["Pco"];
        Assert.Equal(expected,Data(bars).CalculatePriceCycleOscillator(kind,length).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePriceCycleOscillatorFast(Data(bars),context,length,kind);Assert.Equal(expected,raw.Span.ToArray());
        if(kind==MovingAvgType.SimpleMovingAverage)
        {
            var close=bars.Select(b=>b.Close).ToArray();var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.PriceCycleOscillator(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),close,core,length);Assert.Equal(expected,core);
            OoplesFinance.StockIndicators.Core.OscillatorCore.PriceCycleOscillator(close,core,length);Assert.All(core,v=>Assert.Equal(0d,v));
        }
        using var state=new PriceCycleOscillatorState(kind,length);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,40,-30,20,7)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],result.Outputs!["Pco"]);}}}
    }
    [Fact]
    public void SelectedPricesKeepOriginalCandlesAndAtrComesBeforeDistance()
    {
        var original=Enumerable.Range(0,8).Select(i=>Candle(i,8,-2,1,1)).ToArray();var selected=new[] {100d,99,1,4,2,0,-9,1};var selectedBars=original.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();var suppliedDistance=new[] {1d,3,5,7,2,0,9,1};var suppliedAtr=new[] {2d,4,1,2,0,1,3,1};
        foreach(var batch in new[] {false,true})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.PriceCycleOutputs(selectedBars,3,3,external?suppliedAtr:null,external?suppliedDistance:null)["Pco"];
            var data=Data(original);data.SetCustomValues(selected.ToList());
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>{Assert.Equal(3,p);Assert.Equal(BuiltInFormulaReferences.PrettyGoodRanges(selectedBars).Select(v=>v.ToDouble()),v);return suppliedAtr;},(v,p)=>{Assert.Equal(3,p);Assert.Equal(selectedBars.Select(b=>b.Close-b.Low),v);return suppliedDistance;}}):null;
            if(batch)Assert.Equal(expected,data.CalculatePriceCycleOscillator(MovingAvgType.ExponentialMovingAverage,3).ChainedValues);
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePriceCycleOscillatorFast(data,context,3,MovingAvgType.ExponentialMovingAverage);Assert.Equal(expected,raw.Span.ToArray());}
            if(external)Assert.Equal(2,ComponentAverage.Substitutions);
        }
        foreach(var batch in new[] {false,true})
        {
            var bars=new[] {Candle(0,0,0,0,1)};
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>new[] {100d},(v,p)=>new[] {double.Epsilon}});
            if(batch)Assert.Equal(new[] {double.Epsilon},Data(bars).CalculatePriceCycleOscillator(length:1).ChainedValues);
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePriceCycleOscillatorFast(Data(bars),context,1);Assert.Equal(new[] {double.Epsilon},raw.Span.ToArray());}
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceDistanceOrAtr()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new PriceCycleOscillatorState(length:3);using var control=new PriceCycleOscillatorState(length:3);var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("PCO",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,i+1,-2,i%3,i+1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(PriceCycleOscillator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.PriceCycleOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
