using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RatioOchlNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("ROCHLA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double o)=>new(DateTime.UnixEpoch.AddMinutes(i),o,h,l,c,1);
    [Fact]
    public void CandleWeightAndSeededBlendMatchIndependentFractions()
    {
        foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/4})Check(Enumerable.Range(0,30).Select(i=>Candle(i,3*scale,-3*scale,(i%5-2)*scale,(i%3-1)*scale)).ToArray());
        Check(Array.Empty<Bar>());
        var hand=new[] {Candle(0,4,0,2,0),Candle(1,4,0,4,2),Candle(2,4,0,0,2),Candle(3,1,1,1,1)};Check(hand);Assert.Equal(new[] {2d,3,1.5,1.5},BuiltInFormulaReferences.RatioOchlOutputs(hand)["Rochla"]);
        var wide=new[] {Candle(0,double.MaxValue,-double.MaxValue,double.MaxValue,0),Candle(1,double.MaxValue,-double.MaxValue,-double.MaxValue,0),Candle(2,double.MaxValue,-double.MaxValue,double.MaxValue,-double.MaxValue)};Check(wide);Assert.Equal(new[] {double.MaxValue,0,double.MaxValue},BuiltInFormulaReferences.RatioOchlOutputs(wide)["Rochla"]);
    }
    private static void Check(Bar[] bars)
    {
        var expected=BuiltInFormulaReferences.RatioOchlOutputs(bars)["Rochla"];Assert.Equal(expected,Data(bars).CalculateRatioOCHLAverager().ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeRatioOchlAveragerFast(Data(bars),context);Assert.Equal(expected,raw.Span.ToArray());
        var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.MovingAverageCore.RatioOchlAverager(bars.Select(b=>b.Open).ToArray(),bars.Select(b=>b.Close).ToArray(),bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),core);Assert.Equal(expected,core);
        var state=new RatioOCHLAveragerState();
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,40,-30,20,7)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],result.Outputs!["Rochla"]);}}}
    }
    [Fact]
    public void SelectedPricesRetainOriginalOpenAndRangeAndClampTheGain()
    {
        var bars=Enumerable.Range(0,8).Select(i=>Candle(i,8,-2,1,0)).ToArray();var selected=new[] {100d,99,1,4,2,0,-9,1};var selectedBars=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();var expected=BuiltInFormulaReferences.RatioOchlOutputs(selectedBars)["Rochla"];
        foreach(var raw in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(selected.ToList());
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>throw new InvalidOperationException("No moving average substitution is defined")});
            if(raw){using var context=new ComputeContext();using var result=IndicatorCompute.ComputeRatioOchlAveragerFast(data,context);Assert.Equal(expected,result.Span.ToArray());}
            else Assert.Equal(expected,data.CalculateRatioOCHLAverager().ChainedValues);
            Assert.Equal(0,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceTheRecursiveBlend()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            var state=new RatioOCHLAveragerState();var control=new RatioOCHLAveragerState();var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("ROCHLA",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,i+1,-2,i%3,1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RatioOchlAverager)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.RatioOchlOutputs(bars),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
