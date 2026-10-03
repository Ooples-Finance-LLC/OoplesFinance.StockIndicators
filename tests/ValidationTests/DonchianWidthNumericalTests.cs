using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DonchianWidthNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("DCW",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void ExtendedWidthsRecoverAfterEvictionAndHonorIndependentSignalPeriods()
    {
        foreach(var length in new[] {1,2,7})foreach(var smooth in new[] {1,3,22})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Enumerable.Range(0,18).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,(i%5+2)*scale,-(i%3+1)*scale,(i%7-3)*scale,1)).ToArray(),length,smooth,kind);
        Check(Array.Empty<Bar>(),20,22,MovingAvgType.SimpleMovingAverage);
        var bars=Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,i==0?double.MaxValue:2,i==0?-double.MaxValue:0,1,1)).ToArray();
        Check(bars,2,3,MovingAvgType.SimpleMovingAverage);
        var batch=Data(bars).CalculateDonchianChannelWidth(length:2,smoothLength:3); Assert.Equal(double.PositiveInfinity,batch.OutputValues["Dcw"][0]); Assert.True(double.IsFinite(batch.OutputValues["Signal"][3]));Assert.Equal(2,batch.OutputValues["Signal"][4]);
        var core=new double[bars.Length];VolatilityCore.DonchianChannelWidth(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),core,0);Assert.Equal(double.PositiveInfinity,core[0]);Assert.All(core.Skip(1),v=>Assert.Equal(2,v));
    }
    private static void Check(Bar[] bars,int length,int smooth,MovingAvgType kind)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.DonchianWidthOutputs(bars,length,smooth,code);var batch=Data(bars).CalculateDonchianChannelWidth(kind,length,smooth);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeDonchianChannelWidthFast(Data(bars),context,length,kind,key=="Signal",smooth);Assert.Equal(expected[key],raw.Span.ToArray());}
        var core=new double[bars.Length];VolatilityCore.DonchianChannelWidth(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),core,length);Assert.Equal(expected["Dcw"],core);
        using var state=new DonchianChannelWidthState(kind,length,smooth);
        for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,0,100,-90,20,1)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void CustomerPriceThenWidthStagesUseSelectedClosesAndSeparatePeriods()
    {
        var bars=new[] {new Bar(DateTime.UnixEpoch,4,10,-2,4,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,-2,4,1),new Bar(DateTime.UnixEpoch.AddMinutes(2),4,7,0,4,1)};var prices=new[] {9d,-1,6};using var context=new ComputeContext();
        foreach(var batch in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(prices.ToList());using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (values,period)=>{Assert.Equal(2,period);Assert.Equal(prices,values);return new[] {1d,1,1};},
                (values,period)=>{Assert.Equal(3,period);Assert.Equal(new[] {12d,12,9},values);return new[] {7d,7,7};}});
            if(batch)Assert.Equal(new[] {7d,7,7},data.CalculateDonchianChannelWidth(length:2,smoothLength:3).OutputValues["Signal"]);
            else {using var raw=IndicatorCompute.ComputeDonchianChannelWidthFast(data,context,2,signalOutput:true,smoothLength:3);Assert.Equal(new[] {7d,7,7},raw.Span.ToArray());}Assert.Equal(2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new DonchianChannelWidthState(length:2,smoothLength:2);using var control=new DonchianChannelWidthState(length:2,smoothLength:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("DCW",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchOnlyAveragesStillSmoothTheWidth()
    {
        var bars=Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),10,12,8,9+i%3,1)).ToArray();var kind=MovingAvgType.HullMovingAverage;
        var expected=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,Enumerable.Repeat(4d,bars.Length).ToList());var batch=Data(bars).CalculateDonchianChannelWidth(kind,4,3);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeDonchianChannelWidthFast(Data(bars),context,4,kind,true,3);Assert.Equal(expected,batch.OutputValues["Signal"]);Assert.Equal(expected,raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(DonchianChannelWidth)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.DonchianWidthOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
