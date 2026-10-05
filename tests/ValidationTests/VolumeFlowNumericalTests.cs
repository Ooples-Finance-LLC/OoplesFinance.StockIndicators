using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeFlowNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolumeFlowIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentCappedFlow(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VolumeFlowOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveBands(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void HandUsesPreviousVolumeCapAndCurrentNormalization()
    {
        var bars=new[]{new Bar(DateTime.UnixEpoch,1,1,1,1,2),new Bar(DateTime.UnixEpoch.AddMinutes(1),2,2,2,2,4),new Bar(DateTime.UnixEpoch.AddMinutes(2),1,1,1,1,8)};
        var result=Data(bars).CalculateVolumeFlowIndicator(length1:1,length2:2,signalLength:2,smoothLength:1,coef:0,vcoef:1);
        Assert.Equal(new[]{0d,.5,-.5},result.OutputValues["Vfi"]);Assert.Equal(new[]{0d,.25,-.25},result.OutputValues["Signal"]);Assert.Equal(new[]{0d,.25,-.25},result.OutputValues["Histogram"]);
    }
    [Fact]
    public void AdjacentLargePricesRetainLogarithmicCutoff()
    {
        var bars=new[]{1e100,Math.BitIncrement(1e100)}.Select((v,i)=>B(v,i)).ToArray();var result=Data(bars).CalculateVolumeFlowIndicator(length1:1,length2:2,signalLength:1,smoothLength:1,coef:4);
        Assert.Equal(new[]{0d,0},result.OutputValues["Vfi"]);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage,1)] [InlineData(MovingAvgType.WeightedMovingAverage,2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage,3)] [InlineData(MovingAvgType.WildersSmoothingMethod,6)]
    public void ExtremePricesAndVolumesPreserveSignalsPreviewAndReset(MovingAvgType kind,int code)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/64})
        {
            var bars=Enumerable.Range(0,23).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),(i%7+1)*scale,(i%7+5)*scale,(i%7)*scale,(i%7+1)*scale,(i%5+1)*scale)).ToArray();
            var signals=new List<Signal>();var expected=BuiltInFormulaReferences.VolumeFlowOutputs(bars,code,3,5,2,3,.2,2.5,signals);
            var actual=Data(bars).CalculateVolumeFlowIndicator(kind,3,5,2,3,.2,2.5);foreach(var pair in expected)Assert.Equal(pair.Value,actual.OutputValues[pair.Key]);Assert.Equal(signals,actual.SignalsList);
            using var state=new VolumeFlowIndicatorState(kind,3,5,2,3,.2,2.5);using var signalState=new VolumeFlowWindow(kind,3,5,2,3,.2,2.5);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();signalState.Next(17,17,1,true);signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    var typical=new ExactMeanAccumulator();typical.Add(bars[i].High);typical.Add(bars[i].Low);typical.Add(bars[i].Close);
                    foreach(var final in new[]{false,false,true})Assert.Equal(signals[i],signalState.Next(typical.Mean(3),bars[i].Close,bars[i].Volume,final).Trade);
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Vfi"][i],point.Value);foreach(var pair in expected)Assert.Equal(pair.Value[i],point.Outputs![pair.Key]);}
                }
            }
        }
    }
    [Fact]
    public void HugePeriodsAndFastSelectionsPreserveCallerState()
    {
        var bars=new[]{1d,3,2}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.VolumeFlowOutputs(bars,1,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,.2,2.5);
        foreach(var series in new[]{IndicatorCompute.MacdSeries.Line,IndicatorCompute.MacdSeries.Signal,IndicatorCompute.MacdSeries.Histogram})
        {
            using var context=new ComputeContext();using var result=IndicatorCompute.ComputeVolumeFlowIndicatorFast(data,context,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,series:series);
            Assert.Equal(expected[series==IndicatorCompute.MacdSeries.Line?"Vfi":series==IndicatorCompute.MacdSeries.Signal?"Signal":"Histogram"],result.ToArray());
        }
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateVolumeFlowIndicator(coef:double.NaN));Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateVolumeFlowIndicator(vcoef:double.PositiveInfinity));Assert.Same(outputs,data.OutputValues);
    }
    [Fact]
    public void ConstantLogReturnHasZeroCenteredVariance()
    {
        var bars=new[]{1d,2,4}.Select((v,i)=>B(v,i)).ToArray();var result=Data(bars).CalculateVolumeFlowIndicator(length1:1,length2:2,signalLength:1,smoothLength:1,coef:2);
        Assert.Equal(new[]{0d,0,1},result.OutputValues["Vfi"]);
    }
    [Fact]
    public void ResetDoesNotInventFirstChangeForSignedVolume()
    {
        using var state=new VolumeFlowWindow(MovingAvgType.SimpleMovingAverage,1,1,1,1,.2,2.5);state.Next(7,7,1,true);state.Reset();
        var actual=state.Next(1,1,-3,true);Assert.Equal(0,actual.Line);Assert.Equal(0,actual.Signal);Assert.Equal(0,actual.Histogram);Assert.Equal(Signal.None,actual.Trade);
    }

}
