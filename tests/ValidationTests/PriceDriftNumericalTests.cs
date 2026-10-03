using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Band = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ChannelBand;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PriceDriftNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("DRIFT",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void RetainedAtrAndEventAgesMatchIndependentRationalDrift()
    {
        foreach(var curve in new[] {false,true})foreach(var length in new[] {0,1,2,3,100})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Enumerable.Range(0,16).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,(i%5+2)*scale,-(i%3+1)*scale,(i%7-3)*scale,1)).ToArray(),length,kind,curve);
        foreach(var curve in new[] {false,true})
        {
            // Recursive ATR needs constant storage even for periods whose square exceeds Int32.
            Check(Bars(new[] {2d,6,4,4,-2,0}),int.MaxValue,MovingAvgType.WildersSmoothingMethod,curve);
            Check(Bars(new[] {2d,6,4,4,-2,0}),int.MaxValue,MovingAvgType.ExponentialMovingAverage,curve);
            foreach(var sign in new[] {-1d,1d})
                Check(new[] {new Bar(DateTime.UnixEpoch,0,2,-2,0,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),sign*4,sign*4+1,sign*4-1,sign*4,1)},2,MovingAvgType.WildersSmoothingMethod,curve);
            Check(Array.Empty<Bar>(),100,MovingAvgType.WildersSmoothingMethod,curve);
            Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.MaxValue,0d}),3,MovingAvgType.SimpleMovingAverage,curve);
            Check(Bars(Enumerable.Repeat(double.MaxValue,8).ToArray()),3,MovingAvgType.WildersSmoothingMethod,curve);
            Check(Bars(new[] {0d,-4,-4,-4,4,4,4,0,0,0}),3,MovingAvgType.SimpleMovingAverage,curve);
            Check(Bars(new[] {0d,4,4,4,-4,-4,-4,0,0,0}),3,MovingAvgType.SimpleMovingAverage,curve);
        }
        var bars=Bars(new[] {2d,6,4,4});var line=Data(bars).CalculatePriceLineChannel(length:2);var curveBatch=Data(bars).CalculatePriceCurveChannel(length:2);
        Assert.Equal(new[] {2d,6,5,4},line.OutputValues["UpperBand"]);Assert.Equal(new[] {2d,2,2,2},line.OutputValues["LowerBand"]);
        Assert.Equal(new[] {2d,6,5.5,4.5},curveBatch.OutputValues["UpperBand"]);Assert.Equal(new[] {2d,3.5,4,4},curveBatch.OutputValues["LowerBand"]);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind,bool curve)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.PriceDriftOutputs(bars,length,code,curve);var batch=curve?Data(bars).CalculatePriceCurveChannel(kind,length):Data(bars).CalculatePriceLineChannel(kind,length);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);var band=key=="UpperBand"?Band.Upper:key=="LowerBand"?Band.Lower:Band.Middle;using var raw=curve?IndicatorCompute.ComputePriceCurveChannelFast(Data(bars),context,length,kind,band):IndicatorCompute.ComputePriceLineChannelFast(Data(bars),context,length,kind,band);Assert.Equal(expected[key],raw.Span.ToArray());}
        IStreamingIndicatorState state=curve?new PriceCurveChannelState(kind,length):new PriceLineChannelState(kind,length);
        using var cleanup=(IDisposable)state;
        for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);Assert.InRange(bars[i].Close,actual.Outputs!["LowerBand"],actual.Outputs["UpperBand"]);}}}
    }
    [Fact]
    public void SelectedCloseAndCustomerAtrDriveRetainedSizes()
    {
        var prices=new[] {9d,-1,6,2};var bars=Enumerable.Range(0,4).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),4,new[] {10d,5,7,4}[i],new[] {-2d,-2,0,0}[i],4,1)).ToArray();
        var selected=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,prices[i],b.Volume)).ToArray();using var context=new ComputeContext();
        foreach(var curve in new[] {false,true})foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(prices.ToList());var atr=new[] {7d,8,9,10};var expected=BuiltInFormulaReferences.PriceDriftOutputs(selected,2,6,curve,external?atr:null);
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(2,period);Assert.Equal(new[] {12d,11,8,6},values);return atr;}}):null;
            if(batch) {var result=curve?data.CalculatePriceCurveChannel(length:2):data.CalculatePriceLineChannel(length:2);foreach(var key in expected.Keys)Assert.Equal(expected[key],result.OutputValues[key]);}
            else {using var raw=curve?IndicatorCompute.ComputePriceCurveChannelFast(data,context,2):IndicatorCompute.ComputePriceLineChannelFast(data,context,2);Assert.Equal(expected["MiddleBand"],raw.Span.ToArray());}if(external)Assert.Equal(1,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidInputsNeverAdvanceEitherNativeState()
    {
        foreach(var curve in new[] {false,true})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            IStreamingIndicatorState state=curve?new PriceCurveChannelState(length:2):new PriceLineChannelState(length:2);IStreamingIndicatorState control=curve?new PriceCurveChannelState(length:2):new PriceLineChannelState(length:2);using var cleanup=(IDisposable)state;using var cleanupControl=(IDisposable)control;
            var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("DRIFT",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchAveragesRetainTheirAtrStage()
    {
        var bars=Bars(new[] {2d,7,4,9,3,5});var kind=MovingAvgType.HullMovingAverage;var atr=Data(bars).CalculateAverageTrueRange(kind,3).CustomValuesList.ToArray();using var context=new ComputeContext();
        foreach(var curve in new[] {false,true}) {var expected=BuiltInFormulaReferences.PriceDriftOutputs(bars,3,6,curve,atr);var batch=curve?Data(bars).CalculatePriceCurveChannel(kind,3):Data(bars).CalculatePriceLineChannel(kind,3);Assert.Equal(expected["MiddleBand"],batch.OutputValues["MiddleBand"]);using var raw=curve?IndicatorCompute.ComputePriceCurveChannelFast(Data(bars),context,3,kind):IndicatorCompute.ComputePriceLineChannelFast(Data(bars),context,3,kind);Assert.Equal(expected["MiddleBand"],raw.Span.ToArray());}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(PriceLineChannel)||c.IndicatorType==typeof(PriceCurveChannel)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.PriceDriftOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
