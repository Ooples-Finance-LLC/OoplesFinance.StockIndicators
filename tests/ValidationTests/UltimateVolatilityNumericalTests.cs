using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UltimateVolatilityNumericalTests
{
    private static Bar[] Bars(double[] close, double[] open) => close.Select((c,i) => new Bar(DateTime.UnixEpoch.AddMinutes(i),open[i],Math.Max(c,open[i]),Math.Min(c,open[i]),c,1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b) => new("UVI",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c=>c.IndicatorType==typeof(UltimateVolatilityIndicator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentAbsoluteBodySums(IndicatorValidationCase c,string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.UltimateVolatilityOutputs(bars,(IBuiltInIndicator)c.Factory()),new IndicatorErrorBudget(0,0,requireSameSign:true));
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedFastPricesRetainOriginalOpen(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator=(IBuiltInIndicator)c.Factory();var options=indicator.CreateOptions();var length=(int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var bars=Bars(Enumerable.Repeat(100d,24).ToArray(),Enumerable.Range(0,24).Select(i=>90d+i).ToArray());
        var selected=bars.Select((_,i)=>(double)(i*7%13-6)).ToArray();
        var projected=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();
        var expected=BuiltInFormulaReferences.UltimateVolatilityOutputs(projected,indicator)["Uvi"];
        var data=Data(bars);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();
        using var fast=IndicatorCompute.ComputeUltimateVolatilityIndicatorFast(data,context,length);Assert.Equal(expected,fast.ToArray());Assert.Equal(selected,data.ChainedValues);
        data.CalculateUltimateVolatilityIndicator(length:length);Assert.Equal(expected,data.CustomValuesList);Assert.Equal(bars.Select(b=>b.Open),data.OpenPrices);
    }
    private static void Check(Bar[] bars,int length,double[] expected)
    {
        Assert.Equal(expected,BuiltInFormulaReferences.UltimateVolatilityValues(bars,length));
        Assert.Equal(expected,Data(bars).CalculateUltimateVolatilityIndicator(length:length).CustomValuesList);
        using var context=new ComputeContext();using var fast=IndicatorCompute.ComputeUltimateVolatilityIndicatorFast(Data(bars),context,length);Assert.Equal(expected,fast.ToArray());
        using var state=new UltimateVolatilityIndicatorState(length:length);
        for(var pass=0;pass<2;pass++)
        {
            state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(Bars(new[]{-17d},new[]{3d})[0]),false,false);
                var preview=state.Update(Native(bars[i]),false,true);Assert.Equal(expected[i],preview.Value);Assert.Equal(expected[i],preview.Outputs!["Uvi"]);
                Assert.Equal(expected[i],state.Update(Native(bars[i]),true,false).Value);
            }
        }
    }
    [Fact]
    public void WarmupExpiryAndPreviewMatchHand() => Check(Bars(new[]{3d,6,0,9},new double[4]),3,new[]{1d,3,3,5});
    [Fact]
    public void WideSubtractionExpiresWithoutLosingSubnormals()
    {
        var e=double.Epsilon;Check(Bars(new[]{double.MaxValue,0,e,3*e},new[]{-double.MaxValue,0,0,0}),2,new[]{double.MaxValue,double.MaxValue,0,2*e});
        Check(Bars(new[]{double.MaxValue},new[]{-double.MaxValue}),1,new[]{double.PositiveInfinity});
    }
    [Fact]
    public void ExtremePeriodIsLazyAndNonpositivePeriodsNormalize()
    {
        Check(Bars(new[]{1d,2},new double[2]),int.MaxValue,new[]{1d/int.MaxValue,3d/int.MaxValue});
        Check(Bars(new[]{1d,2},new double[2]),int.MinValue,new[]{1d,2});
    }
    [Fact]
    public void ThresholdUsesExactMeanBeforePublication()
    {
        var window=new UltimateVolatilityWindow(2);Assert.False(window.Next(1,0,true).Active);
        var point=window.Next(Math.BitDecrement(1d),0,true);Assert.Equal(1,point.Value);Assert.False(point.Active);
        window.Reset();window.Next(1,0,true);Assert.True(window.Next(1,0,true).Active);
    }
    [Fact]
    public void CallbackAndExactVolatilityGateControlSignals()
    {
        // The second body is exactly 1-epsilon. Two such-window means round to
        // one but stay below the signal threshold; the last window equals one.
        var close=new[]{2d,1d,2d,3d};var data=Data(Bars(close,new[]{1d,double.Epsilon,1d,2d}));
        using(ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[]{
            (_,_)=>throw new InvalidOperationException("Unexpected component callback")}))
        {
            data.CalculateUltimateVolatilityIndicator(MovingAvgType.SimpleMovingAverage,2);
            Assert.Equal(new[]{Signal.None,Signal.None,Signal.None,Signal.Buy},data.SignalsList);
            Assert.Equal(0,ComponentAverage.Requests);Assert.Equal(0,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void ExistingStreamingParityRegression()
    {
        var spec = StreamingTests.StreamingStatefulParityTests.StatefulIndicators.Select(row =>
            (StreamingTests.StreamingStatefulParityTests.StatefulIndicatorSpec)row[0])
            .Single(candidate => candidate.Name == "UltimateVolatilityIndicator");
        new StreamingTests.StreamingStatefulParityTests().StatefulStreamingMatchesBatchOutputs(spec);
    }
    [Fact]
    public void InvalidBarCannotAdvanceNativeState()
    {
        using var state=new UltimateVolatilityIndicatorState(length:2);var good=Native(Bars(new[]{2d},new[]{0d})[0]);
        Assert.Equal(1,state.Update(good,true,false).Value);
        var bad=new OhlcvBar("UVI",BarTimeframe.Minutes(1),good.StartTime,good.EndTime,0,double.NaN,0,8,1,true);
        Assert.ThrowsAny<ArgumentException>(()=>state.Update(bad,true,false));Assert.Equal(2,state.Update(good,true,false).Value);
    }
}
