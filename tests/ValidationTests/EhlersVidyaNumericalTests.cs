using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EhlersVidyaNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(EhlersVariableIndexDynamicAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentResidualRmsRatio(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.EhlersVidyaOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveRmsRatio(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void EqualWindowsHaveTheHandCalculatedPointTwoGain()
    {
        var bars=new[]{10d,20,15}.Select((v,i)=>B(v,i)).ToArray();var result=Data(bars).CalculateEhlersVariableIndexDynamicAverage(MovingAvgType.SimpleMovingAverage,2,2);
        Assert.Equal(new[]{10d,12,12.6},result.CustomValuesList);Assert.Equal(result.CustomValuesList,BuiltInFormulaReferences.EhlersVidyaOutputs(bars,1,2,2)["Evidya"]);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage,1)] [InlineData(MovingAvgType.WeightedMovingAverage,2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage,3)] [InlineData(MovingAvgType.WildersSmoothingMethod,6)]
    public void ExtremePricesPreserveExactEnergySignalsPreviewAndReset(MovingAvgType kind,int code)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/8})
        {
            var bars=Enumerable.Range(0,23).Select(i=>B((i%7+1)*(i%3==0?-scale:scale),i)).ToArray();var signals=new List<Signal>();
            var expected=BuiltInFormulaReferences.EhlersVidyaOutputs(bars,code,3,5,signals)["Evidya"];
            var actual=Data(bars).CalculateEhlersVariableIndexDynamicAverage(kind,3,5);Assert.Equal(expected,actual.CustomValuesList);Assert.Equal(signals,actual.SignalsList);
            using var state=new EhlersVariableIndexDynamicAverageState(kind,3,5);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs!["Evidya"]);}
                }
            }
        }
    }
    [Fact]
    public void GainClampsAndZeroEnergyRemainDistinct()
    {
        var bars=new[]{0d,100,100,100.000000001,50,50,50}.Select((v,i)=>B(v,i)).ToArray();
        foreach(var periods in new[]{(30,2),(1,3),(3,1)})
        {
            var expected=BuiltInFormulaReferences.EhlersVidyaOutputs(bars,1,periods.Item1,periods.Item2)["Evidya"];
            Assert.Equal(expected,Data(bars).CalculateEhlersVariableIndexDynamicAverage(MovingAvgType.SimpleMovingAverage,periods.Item1,periods.Item2).CustomValuesList);
        }
        Assert.Equal(new double[bars.Length],Data(bars).CalculateEhlersVariableIndexDynamicAverage(MovingAvgType.SimpleMovingAverage,3,1).CustomValuesList);
    }
    [Fact]
    public void HugePeriodsAndFastCalculationPreserveCallerState()
    {
        var bars=new[]{double.MaxValue,-double.MaxValue,1d}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);
        var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        using var context=new ComputeContext();using var result=IndicatorCompute.ComputeEhlersVariableIndexDynamicAverageFast(data,context,int.MaxValue,int.MaxValue,MovingAvgType.WeightedMovingAverage);
        Assert.Equal(BuiltInFormulaReferences.EhlersVidyaOutputs(bars,2,int.MaxValue,int.MaxValue)["Evidya"],result.ToArray());
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        data.ClosePrices[0]=double.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateEhlersVariableIndexDynamicAverage());Assert.Same(outputs,data.OutputValues);
    }
}
