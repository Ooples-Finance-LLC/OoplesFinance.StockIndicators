using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PrettyGoodNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("CMF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double v)=>new(DateTime.UnixEpoch.AddMinutes(i),c,h,l,c,v);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void PriceDistanceAndExtendedAtrMatchIndependentFractions()
    {
        foreach(var length in new[] {0,1,2,14})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/4})Check(Enumerable.Range(0,36).Select(i=>Candle(i,3*scale,-3*scale,(i%5-2)*scale,1)).ToArray(),length,kind);
            Check(new[] {Candle(0,double.MaxValue,-double.MaxValue,double.MaxValue,1),Candle(1,double.MaxValue,-double.MaxValue,-double.MaxValue,1),Candle(2,2,-2,0,1),Candle(3,0,0,0,1)},length,kind);
        }
        Check(Array.Empty<Bar>(),14,MovingAvgType.SimpleMovingAverage);
        var hand=new[] {Candle(0,2,0,1,1),Candle(1,4,2,3,1),Candle(2,3,1,2,1)};Assert.Equal(new[] {0d,.4,-.2},BuiltInFormulaReferences.PrettyGoodOutputs(hand,2,1)["Pgo"]);Check(hand,2,MovingAvgType.SimpleMovingAverage);
        var wide=new[] {Candle(0,double.MaxValue,-double.MaxValue,double.MaxValue,1),Candle(1,double.MaxValue,-double.MaxValue,-double.MaxValue,1)};
        Assert.Equal(new[] {0d,-.5},BuiltInFormulaReferences.PrettyGoodOutputs(wide,2,1)["Pgo"]);Check(wide,2,MovingAvgType.SimpleMovingAverage);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.PrettyGoodOutputs(bars,length,Kind(kind))["Pgo"];
        Assert.Equal(expected,Data(bars).CalculatePrettyGoodOscillator(kind,length).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePrettyGoodOscillatorFast(Data(bars),context,length,kind);Assert.Equal(expected,raw.Span.ToArray());
        if(kind==MovingAvgType.SimpleMovingAverage){var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.PrettyGoodOscillator(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),bars.Select(b=>b.Close).ToArray(),core,length);Assert.Equal(expected,core);}
        using var state=new PrettyGoodOscillatorState(kind,length);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,40,-30,20,7)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],result.Outputs!["Pgo"]);}}}
    }
    [Fact]
    public void SelectedPricesKeepOriginalCandlesAndBothCustomerStages()
    {
        var original=Enumerable.Range(0,8).Select(i=>Candle(i,8,-2,1,1)).ToArray();var selected=new[] {100d,99,1,4,2,0,-9,1};var selectedBars=original.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();var suppliedAverage=new[] {1d,3,5,7,2,0,9,1};var suppliedAtr=new[] {2d,4,1,2,0,1,3,1};
        foreach(var batch in new[] {false,true})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.PrettyGoodOutputs(selectedBars,3,3,external?suppliedAverage:null,external?suppliedAtr:null)["Pgo"];
            var data=Data(original);data.SetCustomValues(selected.ToList());
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>{Assert.Equal(3,p);Assert.Equal(selected,v);return suppliedAverage;},(v,p)=>{Assert.Equal(3,p);Assert.Equal(BuiltInFormulaReferences.PrettyGoodRanges(selectedBars).Select(v=>v.ToDouble()),v);return suppliedAtr;}}):null;
            if(batch)Assert.Equal(expected,data.CalculatePrettyGoodOscillator(MovingAvgType.ExponentialMovingAverage,3).ChainedValues);
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePrettyGoodOscillatorFast(data,context,3,MovingAvgType.ExponentialMovingAverage);Assert.Equal(expected,raw.Span.ToArray());}
            if(external)Assert.Equal(2,ComponentAverage.Substitutions);
        }
        foreach(var sign in new[] {-1d,1d})foreach(var batch in new[] {false,true})
        {
            var price=sign*double.MaxValue;var bars=new[] {Candle(0,price,price,price,1)};
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>new[] {-price},(v,p)=>new[] {double.MaxValue}});
            if(batch)Assert.Equal(new[] {sign*2},Data(bars).CalculatePrettyGoodOscillator(length:1).ChainedValues);
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePrettyGoodOscillatorFast(Data(bars),context,1);Assert.Equal(new[] {sign*2},raw.Span.ToArray());}
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvancePriceOrAtr()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new PrettyGoodOscillatorState(length:3);using var control=new PrettyGoodOscillatorState(length:3);var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("PGO",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,i+1,-2,i%3,i+1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(Pgo)||c.IndicatorType==typeof(PrettyGoodOscillator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.PrettyGoodOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
