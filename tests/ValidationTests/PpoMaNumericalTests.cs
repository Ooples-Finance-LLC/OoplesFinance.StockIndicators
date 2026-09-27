using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PpoMaNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("GAP",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(IEnumerable<double> values)=>values.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    [Fact]
    public void FixedExponentialAveragesMatchIndependentArithmetic()
    {
        foreach(var fast in new[] {int.MinValue,0,1,2,7,int.MaxValue})foreach(var slow in new[] {0,1,3,9,int.MaxValue})
        foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,24).Select(i=>(i%7-3)*scale)),fast,slow);
        Check(Array.Empty<Bar>(),12,26);
        var wide=Bars(new[] {-double.MaxValue,-double.MaxValue,double.MaxValue});Check(wide,1,5);
        var last=BuiltInFormulaReferences.AverageGapOutputs(wide,1,5,3,"PpoMa")["PpoMa"][2];Assert.True(double.IsFinite(last));Assert.True(last < -300);
        var equal=Bars(new[] {0d,4,8,-12});Check(equal,2,2);Assert.All(BuiltInFormulaReferences.AverageGapOutputs(equal,2,2,3,"PpoMa")["PpoMa"],v=>Assert.Equal(0d,v));
    }
    private static void Check(Bar[] bars,int fast,int slow)
    {
        var expected=BuiltInFormulaReferences.AverageGapOutputs(bars,fast,slow,3,"PpoMa")["PpoMa"];
        Assert.Equal(expected,Data(bars).CalculatePpoMovingAverage(fast,slow).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePpoMaFast(Data(bars),context,fast,slow);Assert.Equal(expected,raw.Span.ToArray());
        var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.MovingAverageCore.PpoMa(bars.Select(b=>b.Close).ToArray(),core,fast,slow);Assert.Equal(expected,core);
        using var state=new PpoMovingAverageState(fast,slow);
        for(var replay=0;replay<2;replay++)
        {
            state.Reset();for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(Bars(new[] {2d})[0]),false,true);
                foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],Assert.Single(result.Outputs!).Value);}
            }
        }
    }
    [Fact]
    public void SelectedPricesUseFixedEmaWithoutConsumingCustomerCallbacks()
    {
        var bars=Bars(new[] {1d,1,1,1});var selected=new[] {3d,5,2,9};var expected=BuiltInFormulaReferences.AverageGapOutputs(Bars(selected),2,3,3,"PpoMa")["PpoMa"];
        foreach(var raw in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(selected.ToList());
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>throw new InvalidOperationException("Fixed EMA must not consume substitutions")});
            if(raw){using var context=new ComputeContext();using var result=IndicatorCompute.ComputePpoMaFast(data,context,2,3);Assert.Equal(expected,result.Span.ToArray());}
            else Assert.Equal(expected,data.CalculatePpoMovingAverage(2,3).ChainedValues);
            Assert.Equal(0,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceEitherAverage()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            IStreamingIndicatorState Create()=>new PpoMovingAverageState(fastLength:2,slowLength:3);
            var state=Create();var control=Create();using var disposable=(IDisposable)state;using var disposableControl=(IDisposable)control;
            var first=Native(Bars(new[] {1d})[0]);state.Update(first,true,true);control.Update(first,true,true);
            var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("GAP",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var bar in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(bar),true,true).Outputs!,state.Update(Native(bar),true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(PpoMa)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.PpoMaOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
