using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RelativeVigorNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("VIGOR",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void FourTapCandleLegsRatioAndSignalKeepTheirRoundedStages()
    {
        foreach(var length in new[] {0,1,2,7,14})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Enumerable.Range(0,24).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),(i%3-1)*scale,(i%5+2)*scale,-(i%3+2)*scale,(i%5-2)*scale,1)).ToArray(),length,kind);
        Check(Array.Empty<Bar>(),14,MovingAvgType.SimpleMovingAverage);
        var extreme=Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),-double.MaxValue,double.MaxValue,-double.MaxValue,double.MaxValue,1)).ToArray();Check(extreme,1,MovingAvgType.SimpleMovingAverage);var e=BuiltInFormulaReferences.RelativeVigorOutputs(extreme,1,1);Assert.All(e["Rvi"],v=>Assert.Equal(1d,v));Assert.Equal(new[] {1d/6,.5,5d/6,1,1,1,1,1},e["Signal"]);
        var flat=Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray();Check(flat,2,MovingAvgType.WeightedMovingAverage);Assert.All(BuiltInFormulaReferences.RelativeVigorOutputs(flat,2,2)["Rvi"],v=>Assert.Equal(0d,v));
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.RelativeVigorOutputs(bars,length,Kind(kind));var batch=Data(bars).CalculateRelativeVigorIndex(kind,length);Assert.Equal(expected["Rvi"],batch.ChainedOutputs["Rvi"]);Assert.Equal(expected["Signal"],batch.ChainedOutputs["Signal"]);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeRelativeVigorIndexFast(Data(bars),context,length,kind);using var signal=IndicatorCompute.ComputeRelativeVigorIndexSignalFast(Data(bars),context,length,kind);Assert.Equal(expected["Rvi"],raw.Span.ToArray());Assert.Equal(expected["Signal"],signal.Span.ToArray());using var state=new RelativeVigorIndexState(kind,length);
        if(kind==MovingAvgType.SimpleMovingAverage) {var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.RelativeVigorIndex(bars.Select(b=>b.Open).ToArray(),bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),bars.Select(b=>b.Close).ToArray(),core,length);Assert.Equal(expected["Rvi"],core);}
        for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var output=state.Update(Native(bars[i]),final,true).Outputs!;Assert.Equal(expected["Rvi"][i],output["Rvi"]);Assert.Equal(expected["Signal"][i],output["Signal"]);}}}
    }
    [Fact]
    public void SelectedCloseAndCustomerAveragesPreserveCandleLegsAndFixedSignal()
    {
        var bars=Enumerable.Range(0,4).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,12,-6,0,1)).ToArray();var selected=new[] {1d,2,3,4};var selectedBars=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();var supplied=new[] {new[] {2d,8,0,-4},new[] {2d,4,0,4}};using var context=new ComputeContext();
        foreach(var route in new[] {"batch","raw","signal"})foreach(var external in new[] {false,true})
        {
            var expected=BuiltInFormulaReferences.RelativeVigorOutputs(selectedBars,2,1,external?supplied:null);var data=Data(bars);data.SetCustomValues(selected.ToList());using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>{Assert.Equal(2,p);Assert.Equal(new[] {1d/6,2d/3,1.5,2.5},v);return supplied[0];},(v,p)=>{Assert.Equal(2,p);Assert.Equal(new[] {3d,9,15,18},v);return supplied[1];}}):null;
            if(route=="batch") {var output=data.CalculateRelativeVigorIndex(length:2).ChainedOutputs;foreach(var key in expected.Keys)Assert.Equal(expected[key],output[key]);}else {using var raw=route=="raw"?IndicatorCompute.ComputeRelativeVigorIndexFast(data,context,2):IndicatorCompute.ComputeRelativeVigorIndexSignalFast(data,context,2);Assert.Equal(expected[route=="raw"?"Rvi":"Signal"],raw.Span.ToArray());}if(external)Assert.Equal(2,ComponentAverage.Substitutions);
        }
        // An out-of-candle selected close must not replace the original high/low range.
        var outside=Data(bars);outside.SetCustomValues(new List<double> {99,99,99,99});Assert.All(outside.CalculateRelativeVigorIndex(length:1).ChainedValues,v=>Assert.Equal(5.5,v));
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceLegAveragesOrSignalHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new RelativeVigorIndexState(length:2);using var control=new RelativeVigorIndexState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("VIGOR",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var close in new[] {4d,7,3,9,2,5}) {var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),close-2,close+2,close-1,close,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);}
        }
    }
    [Fact]
    public void ExtendedCustomerRatiosCancelBeforeSignalPublication()
    {
        var bars=Enumerable.Range(0,4).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,2,-2,1,1)).ToArray();var supplied=new[] {new[] {double.MaxValue,-double.MaxValue,0,0},new[] {double.Epsilon,double.Epsilon,1,1}};var expected=BuiltInFormulaReferences.RelativeVigorOutputs(bars,1,1,supplied);Assert.Equal(double.PositiveInfinity,expected["Rvi"][0]);Assert.Equal(double.NegativeInfinity,expected["Rvi"][1]);Assert.Equal(0d,expected["Signal"][2]);using var context=new ComputeContext();
        foreach(var batch in new[] {false,true}) {using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(v,p)=>supplied[0],(v,p)=>supplied[1]});if(batch) {var output=Data(bars).CalculateRelativeVigorIndex(length:1).ChainedOutputs;foreach(var key in expected.Keys)Assert.Equal(expected[key],output[key]);}else {using var raw=IndicatorCompute.ComputeRelativeVigorIndexSignalFast(Data(bars),context,1);Assert.Equal(expected["Signal"],raw.Span.ToArray());}Assert.Equal(2,ComponentAverage.Substitutions);}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RelativeVigorIndex)||c.IndicatorType==typeof(RelativeVigorIndexSignal)||c.IndicatorType==typeof(Rvi)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.RelativeVigorOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
