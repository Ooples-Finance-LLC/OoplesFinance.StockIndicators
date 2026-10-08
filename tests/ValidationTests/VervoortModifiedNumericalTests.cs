using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VervoortModifiedNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VervoortModifiedBollingerBandIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentTwoDeviationFormula(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VervoortModifiedOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveBands(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void HandBandUsesPopulationSpreadAndFiftyCenter()
    {
        var bars=new[]{0d,4,8}.Select((v,i)=>B(v,i)).ToArray();
        var actual=Data(bars).CalculateVervoortModifiedBollingerBandIndicator(MovingAvgType.SimpleMovingAverage,2,2,1,2);
        Assert.Equal(new[]{0d,200d/3,200d/3},actual.OutputValues["PercentB"]);Assert.Equal(new[]{50d,50,50},actual.OutputValues["MiddleBand"]);
        Assert.Equal(50+200d/3,actual.OutputValues["UpperBand"][1],12);Assert.Equal(50-200d/3,actual.OutputValues["LowerBand"][1],12);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage,1)] [InlineData(MovingAvgType.WeightedMovingAverage,2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage,3)] [InlineData(MovingAvgType.DoubleExponentialMovingAverage,4)]
    [InlineData(MovingAvgType.TripleExponentialMovingAverage,5)] [InlineData(MovingAvgType.WildersSmoothingMethod,6)]
    public void ExtremeCandlesPreserveAllOutputsSignalsPreviewAndReset(MovingAvgType kind,int code)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/64})
        {
            var bars=Enumerable.Range(0,23).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),(i%7-3)*scale,(i%7+5)*scale,-(i%5+5)*scale,(i%7-3)*scale,1)).ToArray();
            var signals=new List<Signal>();var expected=BuiltInFormulaReferences.VervoortModifiedOutputs(bars,code,3,5,2,1.6,signals);
            var actual=Data(bars).CalculateVervoortModifiedBollingerBandIndicator(kind,3,5,2,1.6);foreach(var pair in expected)Assert.Equal(pair.Value,actual.OutputValues[pair.Key]);Assert.Equal(signals,actual.SignalsList);
            using var state=new VervoortModifiedBollingerBandIndicatorState(kind,3,5,2,1.6);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["PercentB"][i],point.Value);foreach(var pair in expected)Assert.Equal(pair.Value[i],point.Outputs![pair.Key]);}
                }
            }
        }
    }
    [Fact]
    public void HugePeriodsUseObservedHistoryAndInvalidInputsPreserveCaller()
    {
        var bars=new[]{1d,3,2}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);
        var expected=BuiltInFormulaReferences.VervoortModifiedOutputs(bars,5,int.MaxValue,int.MaxValue,int.MaxValue,2);
        var actual=data.CalculateVervoortModifiedBollingerBandIndicator(length1:int.MaxValue,length2:int.MaxValue,smoothLength:int.MaxValue,stdDevMult:2);
        foreach(var pair in expected)Assert.Equal(pair.Value,actual.OutputValues[pair.Key]);
        var outputs=data.OutputValues;Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateVervoortModifiedBollingerBandIndicator(stdDevMult:double.NaN));Assert.Same(outputs,data.OutputValues);
    }
    [Fact]
    public void SettledCandleRetainsSmallSpreadCutoff()
    {
        var bars=Enumerable.Range(0,100).Select(i=>B(1,i)).ToArray();var result=Data(bars).CalculateVervoortModifiedBollingerBandIndicator(MovingAvgType.SimpleMovingAverage,3,3,1);
        Assert.NotEqual(0,result.OutputValues["PercentB"][10]);Assert.Equal(0,result.OutputValues["PercentB"][99]);
    }

    [Fact]
    public void FastSelectionsPreserveCallerState()
    {
        var bars=Enumerable.Range(0,13).Select(i=>B(i%7,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.VervoortModifiedOutputs(bars,1,3,5,2,1.6);
        foreach(var key in expected.Keys){using var context=new ComputeContext();using var result=IndicatorCompute.ComputeVervoortModifiedBandsFast(data,context,MovingAvgType.SimpleMovingAverage,3,5,2,1.6,key);Assert.Equal(expected[key],result.ToArray());}
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
    }

    [Fact]
    public void AlgebraicBoundsResolveBothSidesOfHalfUlp()
    {
        var one=PeriodicChannelWindow.Number.Integer(1);var tiny=one.Divide(PeriodicChannelWindow.Number.Integer(System.Numerics.BigInteger.One<<80));var variance=PeriodicChannelWindow.Number.Integer(System.Numerics.BigInteger.One<<96);
        Assert.Equal(Math.BitIncrement(50d),VervoortModifiedWindow.OffsetRoot(one+tiny,variance));
        Assert.Equal(50d,VervoortModifiedWindow.OffsetRoot(one-tiny,variance));
        Assert.Equal(Math.BitDecrement(50d),VervoortModifiedWindow.OffsetRoot((one+tiny).Times(-1),variance));
        Assert.Equal(50d,VervoortModifiedWindow.OffsetRoot(one,variance));
    }

}
