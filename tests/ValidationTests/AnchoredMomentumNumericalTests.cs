using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AnchoredMomentumNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("ANCHOR",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(IEnumerable<double> values)=>values.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void PartialWindowsAndDerivedPeriodMatchIndependentArithmetic()
    {
        foreach(var momentum in new[] {int.MinValue,0,1,3,265,int.MaxValue})foreach(var smoothing in new[] {0,1,3,7})foreach(var signal in new[] {0,1,4,8})
        foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})Check(Bars(Enumerable.Range(0,16).Select(i=>(i%7-3)*scale)),momentum,smoothing,signal,kind);
        Check(Array.Empty<Bar>(),10,7,8,MovingAvgType.ExponentialMovingAverage);
        Check(Bars(Enumerable.Range(0,540).Select(i=>(double)(i%11+1))),int.MaxValue,7,8,MovingAvgType.ExponentialMovingAverage);
        var hand=Bars(new[] {1d,3,5});Check(hand,0,1,2,MovingAvgType.ExponentialMovingAverage);
        Assert.Equal(new[] {0d,50,25},BuiltInFormulaReferences.AnchoredMomentumOutputs(hand,0,1,2,3)["Amom"]);
        Assert.Equal(new[] {0d,25,37.5},BuiltInFormulaReferences.AnchoredMomentumOutputs(hand,0,1,2,3)["Signal"]);
    }
    private static void Check(Bar[] bars,int momentum,int smoothing,int signal,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.AnchoredMomentumOutputs(bars,momentum,smoothing,signal,Kind(kind));
        var batch=Data(bars).CalculateAnchoredMomentum(kind,smoothing,signal,momentum);foreach(var key in expected.Keys)Assert.Equal(expected[key],batch.ChainedOutputs[key]);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeAnchoredMomentumFast(Data(bars),context,momentum,kind,smoothing,false,signal);using var rawSignal=IndicatorCompute.ComputeAnchoredMomentumFast(Data(bars),context,momentum,kind,smoothing,true,signal);
        Assert.Equal(expected["Amom"],raw.Span.ToArray());Assert.Equal(expected["Signal"],rawSignal.Span.ToArray());
        if(kind==MovingAvgType.ExponentialMovingAverage&&smoothing==7&&signal==8){var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.AnchoredMomentum(bars.Select(b=>b.Close).ToArray(),core,momentum);Assert.Equal(expected["Amom"],core);}
        using var state=new AnchoredMomentumState(kind,smoothing,signal,momentum);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(new Bar(bars[i].Time,1,4,-3,2,1)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Amom"][i],result.Value);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],result.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesAndCustomerSmoothingKeepTheFixedPartialSignal()
    {
        var original=Bars(new[] {1d,1,1,1});var selected=new[] {3d,5,2,9};var supplied=new[] {2d,7,3,11};using var context=new ComputeContext();
        foreach(var route in new[] {"batch","raw","signal"})foreach(var external in new[] {false,true})
        {
            var data=Data(original);data.SetCustomValues(selected.ToList());var expected=BuiltInFormulaReferences.AnchoredMomentumOutputs(Bars(selected),1,2,3,3,external?supplied:null);
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>{Assert.Equal(2,p);Assert.Equal(selected,v);return supplied;}}):null;
            if(route=="batch"){var result=data.CalculateAnchoredMomentum(MovingAvgType.ExponentialMovingAverage,2,3,1);foreach(var key in expected.Keys)Assert.Equal(expected[key],result.ChainedOutputs[key]);}
            else{using var result=IndicatorCompute.ComputeAnchoredMomentumFast(data,context,1,MovingAvgType.ExponentialMovingAverage,2,route=="signal",3);Assert.Equal(expected[route=="raw"?"Amom":"Signal"],result.Span.ToArray());}
            if(external)Assert.Equal(1,ComponentAverage.Substitutions);
        }
        var tiny=Bars(Enumerable.Repeat(double.Epsilon,3));var extreme=new[] {double.MaxValue,-double.MaxValue,0d};
        var overflow=BuiltInFormulaReferences.AnchoredMomentumOutputs(tiny,1,1,2,3,extreme);Assert.Equal(double.PositiveInfinity,overflow["Amom"][0]);Assert.Equal(double.NegativeInfinity,overflow["Amom"][1]);Assert.Equal(0d,overflow["Signal"][1]);
        foreach(var batch in new[] {false,true}){using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>extreme});if(batch){var result=Data(tiny).CalculateAnchoredMomentum(MovingAvgType.ExponentialMovingAverage,1,2,1);foreach(var key in overflow.Keys)Assert.Equal(overflow[key],result.ChainedOutputs[key]);}else{using var result=IndicatorCompute.ComputeAnchoredMomentumFast(Data(tiny),context,1,MovingAvgType.ExponentialMovingAverage,1,true,2);Assert.Equal(overflow["Signal"],result.Span.ToArray());}}
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceAnyAverageOrAnchor()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new AnchoredMomentumState(smoothLength:2,signalLength:3,momentumLength:1);using var control=new AnchoredMomentumState(smoothLength:2,signalLength:3,momentumLength:1);var first=Native(Bars(new[] {1d})[0]);state.Update(first,true,true);control.Update(first,true,true);
            var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("ANCHOR",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var bar in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(bar),true,true).Outputs!,state.Update(Native(bar),true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(AnchoredMomentum)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.AnchoredMomentumOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
