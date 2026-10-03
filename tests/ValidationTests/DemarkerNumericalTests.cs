using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DemarkerNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("DM",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void SeparateMovementsAndAveragesPreserveBoundedExtremeRatios()
    {
        foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})foreach(var length in new[] {0,1,2,3,20})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
            Check(Enumerable.Range(0,40).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,(i%7+1)*scale,-(i%5+1)*scale,0,1)).ToArray(),length,kind);
        foreach(var length in new[] {1,2,3})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            Check(new[] {new Bar(DateTime.UnixEpoch,-double.MaxValue,-double.MaxValue,-double.MaxValue,-double.MaxValue,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),0,double.MaxValue,-double.MaxValue,0,1)},length,kind);
            Check(new[] {new Bar(DateTime.UnixEpoch,0,0,0,0,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),0,double.MaxValue,-double.MaxValue,0,1)},length,kind);
        }
        Check(Array.Empty<Bar>(),20,MovingAvgType.SimpleMovingAverage);var tiny=new[] {new Bar(DateTime.UnixEpoch,0,0,0,0,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),0,double.Epsilon,-3*double.Epsilon,0,1)};Check(tiny,1,MovingAvgType.SimpleMovingAverage);Assert.Equal(new[] {0d,25},Data(tiny).CalculateDemarker(length:1).ChainedValues);
        var constant=Enumerable.Range(0,6).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray();Check(constant,1,MovingAvgType.SimpleMovingAverage);Assert.All(Data(constant).CalculateDemarker(length:1).ChainedValues,v=>Assert.Equal(0d,v));
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.DemarkerOutputs(bars,length,Kind(kind))["Dm"];Assert.Equal(expected,Data(bars).CalculateDemarker(kind,length).ChainedValues);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeDemarkerFast(Data(bars),context,length,kind);using var alias=IndicatorCompute.ComputeDeMarkerFast(Data(bars),context,length,kind);Assert.Equal(expected,raw.Span.ToArray());Assert.Equal(expected,alias.Span.ToArray());Assert.All(expected,v=>Assert.InRange(v,0d,100d));
        if(kind==MovingAvgType.SimpleMovingAverage) {var core=new double[bars.Length];OscillatorCore.DeMarker(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),core,length);Assert.Equal(expected,core);OscillatorCore.Demarker(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),core,length);Assert.Equal(expected,core);}
        using var state=new DemarkerState(kind,length);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,true).Outputs!["Dm"]);}}
    }
    [Fact]
    public void OriginalHighLowAndOrderedCustomerAveragesDetermineRatio()
    {
        var bars=Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,10+i,-10-2*i,0,1)).ToArray();var prices=new[] {2d,-7,0,8,-4,0,9,1};var up=new[] {3d,-1,2,1,0,2,7,3};var down=new[] {-1d,3,-2,1,0,2,3,7};using var context=new ComputeContext();
        foreach(var batch in new[] {false,true})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.DemarkerOutputs(bars,3,1,external?up:null,external?down:null)["Dm"];var data=Data(bars);data.SetCustomValues(prices.ToList());var calls=0;
            Func<IReadOnlyList<double>,int,IReadOnlyList<double>> callback=(v,p)=>{Assert.Equal(3,p);Assert.Equal(calls==0?new[] {0d,1,1,1,1,1,1,1}:new[] {0d,2,2,2,2,2,2,2},v);return calls++==0?up:down;};using var armed=external?ComponentAverage.Arm(new[] {callback,callback}):null;
            if(batch)Assert.Equal(expected,data.CalculateDemarker(length:3).ChainedValues);else {using var result=IndicatorCompute.ComputeDemarkerFast(data,context,3);Assert.Equal(expected,result.Span.ToArray());}if(external) {Assert.Equal(2,ComponentAverage.Substitutions);Assert.Equal(100,expected[0]);Assert.Equal(0,expected[1]);Assert.Equal(0,expected[2]);}
        }
    }
    [Fact]
    public void InvalidFieldsAndShortCoreSpansNeverAdvanceState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new DemarkerState(length:2);using var control=new DemarkerState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("DM",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),0,4,-3,-2,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        Assert.Throws<ArgumentException>(()=>OscillatorCore.DeMarker(new[] {1d},Array.Empty<double>(),new double[1]));Assert.Throws<ArgumentException>(()=>OscillatorCore.Demarker(new[] {1d},new[] {0d},Array.Empty<double>()));
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(Demarker)||c.IndicatorType==typeof(DeMarker)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.DemarkerOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
