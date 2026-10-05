using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VervoortVolatilityNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VervoortVolatilityBands)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentAsymmetricBands(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VervoortVolatilityOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveBands(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void HandUsesDirectionalReachAndDistinctLowerMultiplier()
    {
        var bars=new[]{new Bar(DateTime.UnixEpoch,10,11,9,10,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),12,13,10,12,1),new Bar(DateTime.UnixEpoch.AddMinutes(2),11,12,8,11,1)};
        var result=Data(bars).CalculateVervoortVolatilityBands(length1:1,length2:1,devMult:2,lowBandMult:.5);
        Assert.Equal(new[]{30d,18,19},result.OutputValues["UpperBand"]);Assert.Equal(new[]{10d,12,11},result.OutputValues["MiddleBand"]);Assert.Equal(new[]{0d,9,7},result.OutputValues["LowerBand"]);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage,1)] [InlineData(MovingAvgType.WeightedMovingAverage,2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage,3)] [InlineData(MovingAvgType.WildersSmoothingMethod,6)]
    public void ExtremeRangesPreserveSignalsPreviewResetAndAllBands(MovingAvgType kind,int code)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/64})
        {
            var bars=Enumerable.Range(0,23).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),(i%7-3)*scale,(i%7+5)*scale,-(i%5+5)*scale,(i%7-3)*scale,1)).ToArray();
            var signals=new List<Signal>();var expected=BuiltInFormulaReferences.VervoortVolatilityOutputs(bars,code,3,5,3.55,.9,signals);
            var actual=Data(bars).CalculateVervoortVolatilityBands(kind,3,5,3.55,.9);foreach(var pair in expected)Assert.Equal(pair.Value,actual.OutputValues[pair.Key]);Assert.Equal(signals,actual.SignalsList);
            using var state=new VervoortVolatilityBandsState(kind,3,5,3.55,.9);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["MiddleBand"][i],point.Value);foreach(var pair in expected)Assert.Equal(pair.Value[i],point.Outputs![pair.Key]);}
                }
            }
        }
    }
    [Fact]
    public void ZeroWidthCancelsOverflowingUnpublishedRanges()
    {
        var bars=new[]{double.MaxValue,-double.MaxValue,double.MaxValue}.Select((v,i)=>B(v,i)).ToArray();var actual=Data(bars).CalculateVervoortVolatilityBands(length1:1,length2:1,devMult:0);
        foreach(var values in actual.OutputValues.Values)Assert.Equal(bars.Select(b=>b.Close),values);
    }
    [Fact]
    public void HugePeriodsAndFastSelectionPreserveCallerState()
    {
        var bars=new[]{1d,3,2}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.VervoortVolatilityOutputs(bars,1,int.MaxValue,int.MaxValue,3.55,.9);
        foreach(var band in new[]{IndicatorCompute.ChannelBand.Upper,IndicatorCompute.ChannelBand.Middle,IndicatorCompute.ChannelBand.Lower})
        {
            using var context=new ComputeContext();using var result=IndicatorCompute.ComputeVervoortVolatilityBandsFast(data,context,int.MaxValue,int.MaxValue,maType:MovingAvgType.SimpleMovingAverage,band:band);
            var key=band==IndicatorCompute.ChannelBand.Upper?"UpperBand":band==IndicatorCompute.ChannelBand.Lower?"LowerBand":"MiddleBand";Assert.Equal(expected[key],result.ToArray());
        }
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateVervoortVolatilityBands(devMult:double.NaN));Assert.Same(outputs,data.OutputValues);
    }
}
