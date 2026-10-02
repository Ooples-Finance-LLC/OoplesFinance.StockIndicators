using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VariableAdaptiveNumericalTests
{
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b) => new("VAMA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar B(double open,double high,double low,double close,int index=0) => new(DateTime.UnixEpoch.AddMinutes(index),open,high,low,close,1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly})
        .Where(c=>c.IndicatorType==typeof(VariableAdaptiveMovingAverage)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void RoutesMatchIndependentFourMeans(IndicatorValidationCase c,string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c,route,
        bars=>BuiltInFormulaReferences.VariableAdaptiveOutputs(bars,(IBuiltInIndicator)c.Factory()),BuiltInFormulaReferences.VariableAdaptiveBudget);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory,MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory,MemberData(nameof(Cases))]
    public async Task SelectedPricesUseProjectedRanges(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options=((IBuiltInIndicator)c.Factory()).CreateOptions();var length=(int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var kind=(MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!;
        var bars=Enumerable.Range(0,24).Select(i=>B(100+i,103+i,97+i,100+i,i)).ToArray();
        var selected=bars.Select((_,i)=>(double)(i*7%13-6)).ToArray();var projected=bars.Select((b,i)=>B(b.Open,b.High,b.Low,selected[i],i)).ToArray();
        var expected=BuiltInFormulaReferences.VariableAdaptiveValues(projected,length,kind,true);
        var data=Data(bars);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();
        using var fast=IndicatorCompute.ComputeVariableAdaptiveMovingAverageFast(data,context,length,kind);
        for(var i=0;i<bars.Length;i++)Assert.True(BuiltInFormulaReferences.VariableAdaptiveBudget.Accepts(expected.Outputs["Vama"][i],fast.Span[i]));
        Assert.Equal(selected,data.ChainedValues);data.CalculateVariableAdaptiveMovingAverage(kind,length);
        for(var i=0;i<bars.Length;i++)Assert.True(BuiltInFormulaReferences.VariableAdaptiveBudget.Accepts(expected.Outputs["Vama"][i],data.CustomValuesList[i]));
        Assert.Equal(expected.Signals,data.SignalsList);Assert.Equal(bars.Select(b=>b.Close),data.ClosePrices);Assert.Equal(bars.Select(b=>b.Open),data.OpenPrices);
    }
    private static void Check(Bar[] bars,double[] expected,int length=1,MovingAvgType kind=MovingAvgType.SimpleMovingAverage)
    {
        var reference=BuiltInFormulaReferences.VariableAdaptiveValues(bars,length,kind);Assert.Equal(expected,reference.Outputs["Vama"]);
        var batch=Data(bars).CalculateVariableAdaptiveMovingAverage(kind,length);Assert.Equal(expected,batch.CustomValuesList);Assert.Equal(reference.Signals,batch.SignalsList);
        using var context=new ComputeContext();using var fast=IndicatorCompute.ComputeVariableAdaptiveMovingAverageFast(Data(bars),context,length,kind);Assert.Equal(expected,fast.ToArray());
        using var state=new VariableAdaptiveMovingAverageState(kind,length);
        for(var cycle=0;cycle<2;cycle++)
        {
            state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(B(0,10,-10,-9)),false,false);
                foreach(var final in new[]{false,false,true})
                {var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs!["Vama"]);}
            }
        }
    }
    [Fact]
    public void HalfGainHasIndependentHandValues()
        => Check(new[]{B(0,4,0,2),B(2,6,2,4,1),B(-2,2,-2,0,2)},new[]{2d,3,1.5});
    [Fact]
    public void GainLimitsHaveIndependentHands()
    {
        // A zero body with a nonzero range uses the lower limit; a full body
        // uses the upper limit. The first output is the initial price, two.
        Check(new[]{B(2,4,0,2),B(4,6,2,4,1)},new[]{2d,2.02});
        Check(new[]{B(0,4,0,2),B(0,4,0,4,1)},new[]{2d,3.98});
        // A negative range gives a negative raw ratio and the lower gain.
        Check(new[]{B(2,4,0,2),B(3,2,6,4,1)},new[]{2d,2.02});
    }
    [Fact]
    public void OverflowingRangeStillHasHalfGain()
        => Check(new[]{B(-double.MaxValue,double.MaxValue,-double.MaxValue,0),B(0,double.MaxValue,-double.MaxValue,double.MaxValue,1)},new[]{0d,double.MaxValue/2});
    [Fact]
    public void PublishedZeroRetainsSubnormalFeedback()
        => Check(new[]{B(0,2*double.Epsilon,0,double.Epsilon),B(double.Epsilon,2*double.Epsilon,0,0,1),B(0,2*double.Epsilon,0,double.Epsilon,2)},new[]{double.Epsilon,0,double.Epsilon});
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsAreLazyAndFlatCandlesHoldFirstPrice(int length)
        => Check(new[]{B(2,2,2,2),B(-3,-3,-3,-3,1),B(5,5,5,5,2)},new[]{2d,2,2},length);
    [Fact]
    public void FourCallbackSlotsAndShortReplacementArePreserved()
    {
        var bars=Enumerable.Range(0,6).Select(i=>B(i,i+4,i-2,i+1,i)).ToArray();
        IReadOnlyList<double>[] replacements={new[]{1d,2,3,4},new[]{0d,1,1},new[]{4d,5,6,7,8,9},new[]{0d,1,2,3,4}};
        var expected=BuiltInFormulaReferences.VariableAdaptiveValues(bars,3,MovingAvgType.SimpleMovingAverage,replacements:replacements);
        foreach(var fast in new[]{false,true})
        {
            var streams=new[]{bars.Select(b=>b.Close).ToArray(),bars.Select(b=>b.Open).ToArray(),bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray()};
            var callbacks=Enumerable.Range(0,4).Select(slot=>(Func<IReadOnlyList<double>,int,IReadOnlyList<double>>)((values,length)=>{Assert.Equal(3,length);Assert.Equal(streams[slot],values);return replacements[slot];})).ToArray();
            using var armed=ComponentAverage.Arm(callbacks);var data=Data(bars);
            if(fast){using var context=new ComputeContext();using var output=IndicatorCompute.ComputeVariableAdaptiveMovingAverageFast(data,context,3);Assert.Equal(expected.Outputs["Vama"],output.ToArray());}
            else{data.CalculateVariableAdaptiveMovingAverage(length:3);Assert.Equal(expected.Outputs["Vama"],data.CustomValuesList);Assert.Equal(expected.Signals,data.SignalsList);}
            Assert.Equal(4,ComponentAverage.Requests);Assert.Equal(4,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidBarCannotAdvanceState()
    {
        using var state=new VariableAdaptiveMovingAverageState(length:1);var good=Native(B(0,4,0,2));
        Assert.Equal(2,state.Update(good,true,false).Value);
        var bad=new OhlcvBar("VAMA",BarTimeframe.Minutes(1),good.StartTime,good.EndTime,0,4,0,3,double.NaN,true);
        Assert.ThrowsAny<ArgumentException>(()=>state.Update(bad,true,true));
        Assert.Equal(3,state.Update(Native(B(2,6,2,4)),true,false).Value);
        var invalidOriginal=Data(new[]{B(0,double.NaN,0,2)});invalidOriginal.SetCustomValues(new List<double>{1});
        Assert.ThrowsAny<ArgumentException>(()=>invalidOriginal.CalculateVariableAdaptiveMovingAverage(length:1));
        using var context=new ComputeContext();
        Assert.ThrowsAny<ArgumentException>(()=>IndicatorCompute.ComputeVariableAdaptiveMovingAverageFast(invalidOriginal,context,1));
    }
}
