using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TechnicalRankNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TechnicalRank)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentWeightedComponents(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.TechnicalRankOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveComponents(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void HandWeightsReturnsAndClampsOnlyTheFinalSum()
    {
        var bars=new[]{1d,2,1,1}.Select((v,i)=>B(v,i)).ToArray();
        var result=Data(bars).CalculateTechnicalRank(1,1,1,1,1,1,1,1,1);
        Assert.Equal(new[]{5d,50d,0d,5d},result.CustomValuesList);
        Assert.Equal(new[]{Signal.StrongBuy,Signal.StrongBuy,Signal.StrongSell,Signal.StrongBuy},result.SignalsList);
        Assert.Equal(result.CustomValuesList,BuiltInFormulaReferences.TechnicalRankOutputs(bars,Enumerable.Repeat(1,9).ToArray())["Tr"]);
    }
    [Fact]
    public void ExtendedComponentsPreservePreviewResetAndExactSignals()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/8})
        {
            var bars=Enumerable.Range(0,27).Select(i=>B((i%7+1)*(i%3==0?-scale:scale),i)).ToArray();
            var signals=new List<Signal>();var expected=BuiltInFormulaReferences.TechnicalRankOutputs(bars,new[]{3,2,4,3,2,5,3,2,4},signals)["Tr"];
            var actual=Data(bars).CalculateTechnicalRank(3,2,4,3,2,5,3,2,4);
            Assert.Equal(expected,actual.CustomValuesList);Assert.Equal(signals,actual.SignalsList);
            using var state=new TechnicalRankState(3,2,4,3,2,5,3,2,4);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);
                    Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true})
                    {
                        var point=state.Update(Native(bars[i]),final,true);
                        Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs!["Tr"]);
                    }
                }
            }
        }
    }
    [Fact]
    public void OverflowingReturnStillHasAFiniteSaturatedRank()
    {
        var bars=new[]{double.Epsilon,double.MaxValue,-double.MaxValue,double.Epsilon}.Select((v,i)=>B(v,i)).ToArray();
        var result=Data(bars).CalculateTechnicalRank(1,1,1,1,1,1,1,1,1);
        Assert.Equal(new[]{5d,100d,0d,0d},result.CustomValuesList);
    }
    [Fact]
    public void HugePeriodsUseObservedHistoryAndFastPreservesCaller()
    {
        var bars=new[]{1d,3,2}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);
        var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        using var context=new ComputeContext();using var result=IndicatorCompute.ComputeTechnicalRankFast(data,context,
            int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue);
        Assert.Equal(BuiltInFormulaReferences.TechnicalRankOutputs(bars,Enumerable.Repeat(int.MaxValue,9).ToArray())["Tr"],result.ToArray());
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
    }
    [Fact]
    public void WindowResetRestoresSignalHistory()
    {
        using var fresh=new TechnicalRankWindow(1,1,1,1,1,1,1,1,1);
        using var reset=new TechnicalRankWindow(1,1,1,1,1,1,1,1,1);
        reset.Next(double.Epsilon,true);Assert.Equal(100,reset.Next(double.MaxValue,true).Value);reset.Reset();
        foreach(var value in new[]{1d,2,1})Assert.Equal(fresh.Next(value,true),reset.Next(value,true));
    }
    [Fact]
    public void InvalidInputDoesNotPublishPartialOutputs()
    {
        var data=Data(new[]{B(1),B(double.PositiveInfinity,1)});var outputs=data.OutputValues;var values=data.CustomValuesList;
        Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateTechnicalRank());
        Assert.Same(outputs,data.OutputValues);Assert.Same(values,data.CustomValuesList);
    }
}
