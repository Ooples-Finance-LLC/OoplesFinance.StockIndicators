using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Band = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ChannelBand;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SmoothedVolatilityNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SVB",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void IndependentPeriodsAndAsymmetricWidthsSurviveExtremeProducts()
    {
        foreach(var length1 in new[] {1,3}) foreach(var length2 in new[] {1,4})
        foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        foreach(var deviation in new[] {0d,2.4,double.MaxValue}) foreach(var adjust in new[] {-2d,0,.9}) foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),(i%7-3)*scale,(i%5+2)*scale,-(i%3+1)*scale,(i%9-4)*scale,1)).ToArray(),length1,length2,kind,deviation,adjust);
        Check(Array.Empty<Bar>(),20,21,MovingAvgType.ExponentialMovingAverage,2.4,.9);
        var bars=Enumerable.Range(0,6).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,double.MaxValue,-double.MaxValue,i%2==0?double.MaxValue:-double.MaxValue,1)).ToArray();
        Check(bars,1,3,MovingAvgType.ExponentialMovingAverage,.5,.5);
        var simple=new[] {new Bar(DateTime.UnixEpoch,10,12,8,10,1)}; var batch=Data(simple).CalculateSmoothedVolatilityBands(length1:1,length2:1,deviation:2,bandAdjust:.5);
        Assert.Equal(18,batch.OutputValues["UpperBand"][0]); Assert.Equal(6,batch.OutputValues["LowerBand"][0]); Assert.Equal(10,batch.OutputValues["MiddleBand"][0]);
    }
    private static void Check(Bar[] bars,int length1,int length2,MovingAvgType kind,double deviation,double adjust)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.SmoothedVolatilityOutputs(bars,length1,length2,code,deviation,adjust); var batch=Data(bars).CalculateSmoothedVolatilityBands(kind,length1,length2,deviation,adjust);
        using var context=new ComputeContext();
        foreach(var key in expected.Keys)
        {
            Assert.Equal(expected[key],batch.OutputValues[key]);
            using var raw=IndicatorCompute.ComputeSmoothedVolatilityBandsFast(Data(bars),context,length1,length2,deviation,adjust,kind,key=="UpperBand"?Band.Upper:key=="LowerBand"?Band.Lower:Band.Middle); Assert.Equal(expected[key],raw.Span.ToArray());
        }
        using var state=new SmoothedVolatilityBandsState(kind,length1,length2,deviation,adjust);
        for(var replay=0;replay<2;replay++)
        {
            state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);
                foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}
            }
        }
    }
    [Fact]
    public void CustomerStagesConsumeRangeThenBasisThenMiddleWithSelectedCloses()
    {
        var bars=new[] {new Bar(DateTime.UnixEpoch,4,10,-2,4,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,-2,4,1),new Bar(DateTime.UnixEpoch.AddMinutes(2),4,7,0,4,1)}; var prices=new[] {9d,-1,6};
        using var context=new ComputeContext();
        foreach(var batch in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(prices.ToList());
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (values,period)=>{Assert.Equal(5,period);Assert.Equal(new[] {12d,11,8},values);return new[] {2d,2,2};},
                (values,period)=>{Assert.Equal(3,period);Assert.Equal(prices,values);return new[] {9d,9,9};},
                (values,period)=>{Assert.Equal(4,period);Assert.Equal(prices,values);return new[] {7d,7,7};}});
            if(batch) {var output=data.CalculateSmoothedVolatilityBands(length1:3,length2:4,deviation:1,bandAdjust:.5);Assert.Equal(new[] {11d,-9,12},output.OutputValues["UpperBand"]);Assert.Equal(new[] {7d,7,7},output.OutputValues["MiddleBand"]);}
            else {using var raw=IndicatorCompute.ComputeSmoothedVolatilityBandsFast(data,context,3,4,1,.5,band:Band.Upper);Assert.Equal(new[] {11d,-9,12},raw.Span.ToArray());}
            Assert.Equal(3,ComponentAverage.Substitutions);
        }
        var expected=BuiltInFormulaReferences.SmoothedVolatilityOutputs(bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,prices[i],b.Volume)).ToArray(),2,3,3,2.4,.9);
        var selected=Data(bars);selected.SetCustomValues(prices.ToList());using var result=IndicatorCompute.ComputeSmoothedVolatilityBandsFast(selected,context,2,3,band:Band.Upper);Assert.Equal(expected["UpperBand"],result.Span.ToArray());
    }
    [Fact]
    public void InvalidInputsAndUnrepresentableDerivedPeriodsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SmoothedVolatilityBandsState(length1:int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateSmoothedVolatilityBands(length1:int.MaxValue));
        using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeSmoothedVolatilityBandsFast(Data(Array.Empty<Bar>()),context,int.MaxValue));
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new SmoothedVolatilityBandsState(length1:2,length2:2);using var control=new SmoothedVolatilityBandsState(length1:2,length2:2);
            var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("SVB",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchOnlyAveragesRetainAllThreeStages()
    {
        var bars=Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),10,12,8,9+i%3,1)).ToArray();var prices=bars.Select(b=>b.Close).ToList();var kind=MovingAvgType.HullMovingAverage;
        var atr=CalculationsHelper.GetMovingAverageList(Data(bars),kind,5,Enumerable.Repeat(4d,bars.Length).ToList());var basis=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,prices);var middle=CalculationsHelper.GetMovingAverageList(Data(bars),kind,4,prices);
        var batch=Data(bars).CalculateSmoothedVolatilityBands(kind,3,4,1,.5);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeSmoothedVolatilityBandsFast(Data(bars),context,3,4,1,.5,kind,Band.Upper);
        Assert.Equal(middle,batch.OutputValues["MiddleBand"]);for(var i=0;i<bars.Length;i++) {Assert.Equal(basis[i]+basis[i]*atr[i]/prices[i],batch.OutputValues["UpperBand"][i],12);Assert.Equal(batch.OutputValues["UpperBand"][i],raw.Span[i]);}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(SmoothedVolatilityBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.SmoothedVolatilityOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
