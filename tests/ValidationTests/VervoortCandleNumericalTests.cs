using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VervoortCandleNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => (c.IndicatorType == typeof(VervoortHeikenAshiCandlestickOscillator) || c.IndicatorType == typeof(VervoortHeikenAshiLongTermCandlestickOscillator))).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentCandleLatches(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VervoortCandleOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveCandleLatches(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void ShortHandLatchesOnlyWhenTheOppositeRunEnds()
    {
        var bars=new[]{4d,1,8,0,-1,9}.Select((v,i)=>B(v,i)).ToArray();
        var result=Data(bars).CalculateVervoortHeikenAshiCandlestickOscillator(length:1);
        Assert.Equal(new[]{0d,0,1,1,-1,1},result.CustomValuesList);
        Assert.Equal(new[]{Signal.None,Signal.None,Signal.StrongBuy,Signal.Buy,Signal.StrongSell,Signal.StrongBuy},result.SignalsList);
        Assert.Equal(result.CustomValuesList,BuiltInFormulaReferences.VervoortCandleOutputs(bars,false,1)["Vhaco"]);
        Assert.Equal(new double[bars.Length],Data(bars).CalculateVervoortHeikenAshiLongTermCandlestickOscillator(length:1).CustomValuesList);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExtremeCandlesPreserveComparisonsPreviewResetAndSignals(bool longTerm)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/16})
        {
            var bars=Enumerable.Range(0,36).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),(i%3-1)*scale,(i%5+3)*scale,-(i%7+1)*scale,(i%9-4)*scale,1)).ToArray();
            var signals=new List<Signal>();var key=longTerm?"Vhaltco":"Vhaco";
            var expected=BuiltInFormulaReferences.VervoortCandleOutputs(bars,longTerm,3,1.1,signals)[key];
            var result=longTerm?Data(bars).CalculateVervoortHeikenAshiLongTermCandlestickOscillator(length:3):Data(bars).CalculateVervoortHeikenAshiCandlestickOscillator(length:3);
            Assert.Equal(expected,result.CustomValuesList);Assert.Equal(signals,result.SignalsList);
            var state=longTerm?(IStreamingIndicatorState)new VervoortHeikenAshiLongTermCandlestickOscillatorState(length:3):new VervoortHeikenAshiCandlestickOscillatorState(length:3);
            using var lifetime=state as IDisposable;
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs![key]);}
                }
            }
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CoreMatchesPublicLatchAndProtectsOverlappingSpans(bool longTerm)
    {
        var scale=double.MaxValue/16;
        var bars=Enumerable.Range(0,36).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),(i%3-1)*scale,(i%5+3)*scale,-(i%7+1)*scale,(i%9-4)*scale,1)).ToArray();
        var prices=bars.Select(b=>b.Close).ToArray();var highs=bars.Select(b=>b.High).ToArray();var lows=bars.Select(b=>b.Low).ToArray();var opens=bars.Select(b=>b.Open).ToArray();
        var expected=BuiltInFormulaReferences.VervoortCandleOutputs(bars,longTerm,1)[longTerm?"Vhaltco":"Vhaco"];Assert.Contains(expected,v=>v!=0);
        var overlap=new double[prices.Length+1];prices.CopyTo(overlap,0);
        if(longTerm)OscillatorCore.VervoortHeikenAshiLongTermCandlestickOscillator(highs,lows,overlap.AsSpan(0,prices.Length),opens,overlap.AsSpan(1),1);
        else OscillatorCore.VervoortHeikenAshiCandlestickOscillator(highs,lows,overlap.AsSpan(0,prices.Length),opens,overlap.AsSpan(1),1);
        Assert.Equal(expected,overlap.Skip(1));
        if(longTerm)OscillatorCore.VervoortHeikenAshiLongTermCandlestickOscillator(Array.Empty<double>(),Array.Empty<double>(),Array.Empty<double>(),Array.Empty<double>(),Array.Empty<double>(),1);
        else OscillatorCore.VervoortHeikenAshiCandlestickOscillator(Array.Empty<double>(),Array.Empty<double>(),Array.Empty<double>(),Array.Empty<double>(),Array.Empty<double>(),1);
    }
    [Fact]
    public void LongCustomFactorPreservesDownTrendGate()
    {
        var bars=Enumerable.Range(0,36).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),i%3-1,i%5+3,-(i%7+1),i%9-4,1)).ToArray();
        var expected=BuiltInFormulaReferences.VervoortCandleOutputs(bars,true,3,.1)["Vhaltco"];
        var actual=Data(bars).CalculateVervoortHeikenAshiLongTermCandlestickOscillator(length:3,factor:.1).CustomValuesList;
        Assert.Equal(expected,actual);Assert.False(expected.SequenceEqual(BuiltInFormulaReferences.VervoortCandleOutputs(bars,true,3,1.1)["Vhaltco"]));
    }
    [Fact]
    public void HugePeriodAndNonfiniteFactorAreHandledBeforePublication()
    {
        using var shortState=new VervoortCandleWindow(false,MovingAvgType.ZeroLagTripleExponentialMovingAverage,int.MaxValue);
        using var longState=new VervoortCandleWindow(true,MovingAvgType.TripleExponentialMovingAverage,int.MaxValue);
        foreach(var price in new[]{double.MaxValue,-double.MaxValue,1d})
        {Assert.True(double.IsFinite(shortState.Next(price,price,price,price,price,true).Value));Assert.True(double.IsFinite(longState.Next(price,price,price,price,price,true).Value));}
        var data=Data(new[]{B(1)});var values=data.CustomValuesList;var outputs=data.OutputValues;
        Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateVervoortHeikenAshiLongTermCandlestickOscillator(factor:double.NaN));Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);
    }
}
