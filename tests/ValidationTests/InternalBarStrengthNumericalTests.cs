using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class InternalBarStrengthNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("CMF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double v)=>new(DateTime.UnixEpoch.AddMinutes(i),c,h,l,c,v);
    [Fact]
    public void CandlePercentagesRollingMeanAndZeroSeedSignalMatchIndependentFractions()
    {
        foreach(var length in new[] {0,1,2,14,int.MaxValue})foreach(var smooth in new[] {0,1,3,7,int.MaxValue})
        {
            foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/4})Check(Enumerable.Range(0,20).Select(i=>Candle(i,3*scale,-3*scale,(i%5-2)*scale,1)).ToArray(),length,smooth);
            Check(new[] {Candle(0,double.MaxValue,-double.MaxValue,double.MaxValue,1),Candle(1,double.MaxValue,-double.MaxValue,-double.MaxValue,1),Candle(2,0,0,0,1)},length,smooth);
        }
        Check(Array.Empty<Bar>(),14,3);
        var hand=new[] {Candle(0,2,0,1,1),Candle(1,2,0,2,1),Candle(2,2,0,0,1)};Check(hand,2,3);
        var expected=BuiltInFormulaReferences.InternalBarStrengthOutputs(hand,2,3);Assert.Equal(new[] {50d,75,50},expected["Ibs"]);Assert.Equal(new[] {25d,50,50},expected["Signal"]);
        var cancellation=new[] {Candle(0,1,0,double.MaxValue,1),Candle(1,1,0,-double.MaxValue,1),Candle(2,1,0,1,1)};Check(cancellation,2,3);Assert.Equal(0d,BuiltInFormulaReferences.InternalBarStrengthOutputs(cancellation,2)["Ibs"][1]);
    }
    private static void Check(Bar[] bars,int length,int smooth)
    {
        var expected=BuiltInFormulaReferences.InternalBarStrengthOutputs(bars,length,smooth);var batch=Data(bars).CalculateInternalBarStrengthIndicator(length,smooth);
        foreach(var key in expected.Keys)Assert.Equal(expected[key],batch.OutputValues[key]);
        if(smooth==3)
        {
            using var context=new ComputeContext();foreach(var key in expected.Keys){using var raw=IndicatorCompute.ComputeInternalBarStrengthIndicatorFast(Data(bars),context,length,key);Assert.Equal(expected[key],raw.Span.ToArray());}
            var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.InternalBarStrength(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),bars.Select(b=>b.Close).ToArray(),core,length);Assert.Equal(expected["Ibs"],core);
        }
        using var state=new InternalBarStrengthIndicatorState(length,smooth);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,40,-30,20,7)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Ibs"][i],result.Value);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],result.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesRetainOriginalCandleRange()
    {
        var bars=Enumerable.Range(0,8).Select(i=>Candle(i,8,-2,1,1)).ToArray();var selected=new[] {100d,99,1,4,2,0,-9,1};var selectedBars=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();var expected=BuiltInFormulaReferences.InternalBarStrengthOutputs(selectedBars,3);
        foreach(var raw in new[] {false,true})foreach(var key in expected.Keys)
        {
            var data=Data(bars);data.SetCustomValues(selected.ToList());
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>throw new InvalidOperationException("Fixed averages must not consume substitutions")});
            if(raw){using var context=new ComputeContext();using var result=IndicatorCompute.ComputeInternalBarStrengthIndicatorFast(data,context,3,key);Assert.Equal(expected[key],result.Span.ToArray());}
            else Assert.Equal(expected[key],data.CalculateInternalBarStrengthIndicator(3).OutputValues[key]);
            Assert.Equal(0,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceDistanceOrAtr()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new InternalBarStrengthIndicatorState(length:3);using var control=new InternalBarStrengthIndicatorState(length:3);var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("PCO",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,i+1,-2,i%3,i+1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(InternalBarStrengthIndicator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.InternalBarStrengthOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
