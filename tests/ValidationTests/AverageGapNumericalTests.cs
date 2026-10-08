using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AverageGapNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("GAP",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(IEnumerable<double> values)=>values.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void TwoRoundedAveragesAndExactPercentGapMatchIndependentArithmetic()
    {
        foreach(var fast in new[] {0,1,2,7})foreach(var slow in new[] {0,1,3,9})
        foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,24).Select(i=>(i%7-3)*scale)),fast,slow,kind);
        Check(Array.Empty<Bar>(),7,65,MovingAvgType.SimpleMovingAverage);
        var hand=Bars(new[] {0d,4,8,12});Check(hand,2,3,MovingAvgType.SimpleMovingAverage);
        Assert.Equal(new[] {0d,0,50,25},BuiltInFormulaReferences.AverageGapOutputs(hand,2,3,1,"Ravi")["Ravi"]);
        var flat=Bars(Enumerable.Repeat(double.MaxValue,20));Check(flat,2,3,MovingAvgType.SimpleMovingAverage);
        Assert.All(BuiltInFormulaReferences.AverageGapOutputs(flat,2,3,1,"Ravi")["Ravi"],v=>Assert.Equal(0d,v));
    }
    private static void Check(Bar[] bars,int fast,int slow,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.AverageGapOutputs(bars,fast,slow,Kind(kind),"Ravi")["Ravi"];
        Assert.Equal(expected,Data(bars).CalculateRangeActionVerificationIndex(kind,fast,slow).ChainedValues);
        Assert.Equal(expected,Data(bars).CalculateEhlersMovingAverageDifferenceIndicator(kind,fast,slow).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeRangeActionVerificationIndexFast(Data(bars),context,fast,slow,kind);using var ehlers=IndicatorCompute.ComputeEhlersMovingAverageDifferenceFast(Data(bars),context,fast,slow,kind);
        Assert.Equal(expected,raw.Span.ToArray());Assert.Equal(expected,ehlers.Span.ToArray());
        if(kind==MovingAvgType.SimpleMovingAverage){var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.RangeActionVerificationIndex(bars.Select(b=>b.Close).ToArray(),core,fast,slow);Assert.Equal(expected,core);}
        using var a=new RangeActionVerificationIndexState(kind,fast,slow);using var b=new EhlersMovingAverageDifferenceIndicatorState(kind,fast,slow);
        foreach(var state in new IStreamingIndicatorState[] {a,b})for(var replay=0;replay<2;replay++)
        {
            state.Reset();for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(new Bar(bars[i].Time,1,4,-3,2,1)),false,true);
                foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],Assert.Single(result.Outputs!).Value);}
            }
        }
    }
    [Fact]
    public void SelectedPricesAndTwoCustomerStagesPreserveFiniteExtremeGaps()
    {
        var bars=Bars(new[] {1d,1,1,1});var selected=new[] {3d,5,2,9};var selectedBars=Bars(selected);
        var supplied=new[] {new[] {double.MaxValue,0,double.MaxValue,-double.MaxValue},new[] {-double.MaxValue,0,double.MaxValue,double.MaxValue}};
        var hand=new[] {-200d,0,0,-200};Assert.Equal(hand,BuiltInFormulaReferences.AverageGapOutputs(bars,2,3,1,"Ravi",supplied)["Ravi"]);
        using var context=new ComputeContext();
        foreach(var route in new[] {"ravi-batch","ehlers-batch","ravi-raw","ehlers-raw"})foreach(var external in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(selected.ToList());var expected=external?hand:BuiltInFormulaReferences.AverageGapOutputs(selectedBars,2,3,1,"Ravi")["Ravi"];
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (v,p)=>{Assert.Equal(2,p);Assert.Equal(selected,v);return supplied[0];},(v,p)=>{Assert.Equal(3,p);Assert.Equal(selected,v);return supplied[1];}}):null;
            if(route.EndsWith("batch")){var result=route.StartsWith("ravi")?data.CalculateRangeActionVerificationIndex(MovingAvgType.SimpleMovingAverage,2,3):data.CalculateEhlersMovingAverageDifferenceIndicator(MovingAvgType.SimpleMovingAverage,2,3);Assert.Equal(expected,result.ChainedValues);}
            else{using var result=route.StartsWith("ravi")?IndicatorCompute.ComputeRangeActionVerificationIndexFast(data,context,2,3,MovingAvgType.SimpleMovingAverage):IndicatorCompute.ComputeEhlersMovingAverageDifferenceFast(data,context,2,3,MovingAvgType.SimpleMovingAverage);Assert.Equal(expected,result.Span.ToArray());}
            if(external)Assert.Equal(2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceEitherAverage()
    {
        foreach(var ehlers in new[] {false,true})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            IStreamingIndicatorState Create()=>ehlers?new EhlersMovingAverageDifferenceIndicatorState(fastLength:2,slowLength:3):new RangeActionVerificationIndexState(fastLength:2,slowLength:3);
            var state=Create();var control=Create();using var disposable=(IDisposable)state;using var disposableControl=(IDisposable)control;
            var first=Native(Bars(new[] {1d})[0]);state.Update(first,true,true);control.Update(first,true,true);
            var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("GAP",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var bar in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(bar),true,true).Outputs!,state.Update(Native(bar),true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RangeActionVerificationIndex)||c.IndicatorType==typeof(EhlersMovingAverageDifferenceIndicator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.AverageGapOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
