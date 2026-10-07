using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class R2AdaptiveNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(R2AdaptiveRegression)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentThreeEstimateBlend(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.R2AdaptiveOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveBands(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void GainWaitsForFullWindow()
    {
        var bars=new[]{1d,2,4,3,7,2}.Select((v,i)=>B(v,i)).ToArray();
        var expected=BuiltInFormulaReferences.R2AdaptiveOutputs(bars,5,1)["R2ar"];
        var actual=Data(bars).CalculateR2AdaptiveRegression(length:5);
        Assert.Equal(expected,actual.OutputValues["R2ar"]);
    }
    [Fact]
    public void HandPreservesPartialFitAndFullVarianceWarmup()
    {
        var bars=new[]{1d,2,3}.Select((v,i)=>B(v,i)).ToArray();var result=Data(bars).CalculateR2AdaptiveRegression(length:3);
        Assert.Equal(1,result.OutputValues["R2ar"][0]);Assert.Equal(2,result.OutputValues["R2ar"][1]);
        var reference=BuiltInFormulaReferences.R2AdaptiveOutputs(bars,3,1);Assert.Equal(reference["R2ar"],result.OutputValues["R2ar"]);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage,1)] [InlineData(MovingAvgType.WeightedMovingAverage,2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage,3)] [InlineData(MovingAvgType.WildersSmoothingMethod,6)]
    public void ExtremePricesPreserveBlendSignalsPreviewAndReset(MovingAvgType kind,int code)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/64})
        {
            var bars=Enumerable.Range(0,23).Select(i=>B((i%7-3)*scale,i)).ToArray();var signals=new List<Signal>();var expected=BuiltInFormulaReferences.R2AdaptiveOutputs(bars,3,code,signals)["R2ar"];
            var actual=Data(bars).CalculateR2AdaptiveRegression(kind,3);Assert.Equal(expected,actual.OutputValues["R2ar"]);Assert.Equal(signals,actual.SignalsList);
            using var state=new R2AdaptiveRegressionState(kind,3);using var signalState=new R2AdaptiveWindow(kind,3);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();signalState.Next(17,true);signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs!["R2ar"]);Assert.Equal(signals[i],signalState.Next(bars[i].Close,final).Trade);}
                }
            }
        }
    }
    [Fact]
    public void CoreRetainsItsSeparateRegressionResidualFormula()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/64})
        {
            var prices=Enumerable.Range(0,17).Select(i=>(i%7-3)*scale).ToArray();var expected=BuiltInFormulaReferences.R2CoreReference(prices.Select((v,i)=>B(v,i)).ToArray(),3);var output=new double[prices.Length];
            MovingAverageCore.R2AdaptiveRegression(prices,output,3);Assert.Equal(expected,output);new R2arCore().Compute(prices,output,3);Assert.Equal(expected,output);MovingAverageCore.R2AdaptiveRegression(prices,prices,3);Assert.Equal(expected,prices);
        }
        MovingAverageCore.R2AdaptiveRegression(Array.Empty<double>(),Array.Empty<double>(),3);var invalid=new[]{1d,double.NaN};var untouched=new[]{17d,19d};Assert.Throws<ArgumentOutOfRangeException>(()=>MovingAverageCore.R2AdaptiveRegression(invalid,untouched,3));Assert.Equal(new[]{17d,19d},untouched);
    }
    [Fact]
    public void HugePeriodsAndFastCalculationPreserveCallerState()
    {
        var bars=new[]{1d,3,2}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.R2AdaptiveOutputs(bars,int.MaxValue,1)["R2ar"];
        using var context=new ComputeContext();using var result=IndicatorCompute.ComputeR2AdaptiveRegressionFast(data,context,int.MaxValue);Assert.Equal(expected,result.ToArray());
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
    }
}
