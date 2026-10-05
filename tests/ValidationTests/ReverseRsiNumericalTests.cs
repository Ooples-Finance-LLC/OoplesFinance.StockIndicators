using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ReverseRsiNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i),price,price,price,price,1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b) => new("RERSI",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly})
        .Where(c=>c.IndicatorType==typeof(ReverseEngineeringRsi)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void RoutesMatchIndependentSeededGainLossInverse(IndicatorValidationCase c,string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.ReverseRsiOutputs(bars,(IBuiltInIndicator)c.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveTheInverse(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void IndependentHandSolvesTheNextWilderStep()
    {
        var bars=new[]{B(1),B(4,1),B(2,2)};
        Assert.Equal(new[]{1d,2,2},Data(bars).CalculateReverseEngineeringRelativeStrengthIndex(3,50).CustomValuesList);
        Assert.Equal(new[]{1d,2,2},BuiltInFormulaReferences.ReverseRsiOutputs(bars,3,50)["Rersi"]);
    }
    [Theory]
    [InlineData(.000001)] [InlineData(50)] [InlineData(99.999999)]
    public void ExtremeValuesAndInteriorTargetsPreserveSignalsPreviewAndReset(double target)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/8})
        {
            var bars=Enumerable.Range(0,24).Select(i=>B((i%7+1)*(i%3==0?-scale:scale),i)).ToArray();
            var signals=new List<Signal>();var expected=BuiltInFormulaReferences.ReverseRsiOutputs(bars,3,target,signals)["Rersi"];
            var result=Data(bars).CalculateReverseEngineeringRelativeStrengthIndex(3,target);
            Assert.Equal(expected,result.CustomValuesList);Assert.Equal(signals,result.SignalsList);
            var state=new ReverseEngineeringRelativeStrengthIndexState(3,target);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);
                    Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true})
                    {
                        var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs!["Rersi"]);
                    }
                }
            }
        }
    }
    [Fact]
    public void AmbiguousCancellationUsesExactReplay()
    {
        var high=Math.Pow(2,500);
        foreach(var sign in new[]{1,-1})
        {
            var bars=new[]{B(0),B(high,1),B(1,2),B(sign*high/4,3)};
            var signals=new List<Signal>();var expected=BuiltInFormulaReferences.ReverseRsiOutputs(bars,2,50,signals)["Rersi"];
            var state=new ReverseRsiWindow(2,50);
            for(var i=0;i<bars.Length;i++)
            {
                var preview=state.Next(bars[i].Close,false);var point=state.Next(bars[i].Close,true);
                Assert.Equal(expected[i],point.Value);Assert.Equal(signals[i],point.Trade);Assert.Equal(point,preview);
            }
            Assert.True(state.ExactReplayUpdates>0);
            Assert.Equal(sign>0?Signal.Sell:Signal.StrongSell,signals[3]);
            if(sign<0) Assert.Equal(.25,expected[3]);
        }
    }
    [Fact]
    public void LongConvergingTrendRetainsExactAccelerationWithoutReplay()
    {
        var state=new ReverseRsiWindow(2,50);
        for(var i=0;i<2048;i++)
        {
            var point=state.Next(i,true);
            Assert.Equal(i==0?0d:i-1+Math.Pow(.5,i),point.Value);
            Assert.Equal(i==0?Signal.None:Signal.StrongBuy,point.Trade);
        }
        Assert.Equal(0,state.ExactReplayUpdates);
    }
    [Fact]
    public void CoreAndRegistryPreserveFormulaAndOverlappingSpans()
    {
        var prices=new[]{double.MaxValue,-double.MaxValue,1d,-1d,5d,2d,7d};
        var expected=BuiltInFormulaReferences.ReverseRsiOutputs(prices.Select((v,i)=>B(v,i)).ToArray(),3,50)["Rersi"];
        var overlap=new double[prices.Length+1];prices.CopyTo(overlap,0);
        MovingAverageCore.ReverseEngineeringRsi(overlap.AsSpan(0,prices.Length),overlap.AsSpan(1),3);
        Assert.Equal(expected,overlap.Skip(1));
        var actual=new double[prices.Length];MovingAverageRegistry.GetRequired(MovingAvgType.ReverseEngineeringRelativeStrengthIndex).Compute(prices,actual,3);
        Assert.Equal(expected,actual);
    }
    [Fact]
    public void UndefinedRequestsAreRejectedBeforePublishingOrWriting()
    {
        foreach(var (length,target) in new[]{(0,50d),(1,50d),(-1,50d),(3,0d),(3,100d),(3,-1d),(3,101d),(3,double.NaN),(3,double.PositiveInfinity),(3,double.NegativeInfinity)})
        {
            var data=Data(new[]{B(1),B(2,1)});var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
            Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateReverseEngineeringRelativeStrengthIndex(length,target));
            Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
            var output=new[]{17d,17d};Assert.Throws<ArgumentOutOfRangeException>(()=>MovingAverageCore.ReverseEngineeringRsi(new[]{1d,2d},output,length,target));Assert.Equal(new[]{17d,17d},output);
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ReverseEngineeringRelativeStrengthIndexState(length,target));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ReverseEngineeringRsiSpecOptions(length,target));
            Assert.Throws<ArgumentOutOfRangeException>(()=>((IBuiltInIndicator)new ReverseEngineeringRsi(length,target)).CreateOptions());
            using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeReverseEngineeringRsiFast(data,context,length,target));
        }
        var untouched=new[]{17d,17d};Assert.Throws<ArgumentOutOfRangeException>(()=>MovingAverageCore.ReverseEngineeringRsi(new[]{1d,double.NaN},untouched,3));Assert.Equal(new[]{17d,17d},untouched);
    }
    [Fact]
    public void HugePeriodsAndSubnormalTargetsRetainDefinedInverse()
    {
        var bars=new[]{B(1),B(2,1),B(-3,2)};
        foreach(var target in new[]{double.Epsilon,25d,50d,99d})
            Assert.Equal(BuiltInFormulaReferences.ReverseRsiOutputs(bars,int.MaxValue,target)["Rersi"],Data(bars).CalculateReverseEngineeringRelativeStrengthIndex(int.MaxValue,target).CustomValuesList);
    }
}
