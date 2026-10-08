using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TradersDynamicNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TradersDynamicIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentRsiBands(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.TradersDynamicOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveRsiBands(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void HandLengthOneUsesUnsmearedRsiAndZeroWidth()
    {
        var bars=new[]{1d,2,1,1}.Select((v,i)=>B(v,i)).ToArray();
        var result=Data(bars).CalculateTradersDynamicIndex(length1:1,length2:1,length3:1,length4:1);
        foreach(var values in result.OutputValues.Values)Assert.Equal(new[]{100d,100d,0d,100d},values);
        Assert.All(result.SignalsList,signal=>Assert.Equal(Signal.None,signal));
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage,1)]
    [InlineData(MovingAvgType.WeightedMovingAverage,2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage,3)]
    [InlineData(MovingAvgType.WildersSmoothingMethod,6)]
    public void ExtremePricesPreserveAllBandsSignalsPreviewAndReset(MovingAvgType kind,int code)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/8})
        {
            var bars=Enumerable.Range(0,23).Select(i=>B((i%7+1)*(i%3==0?-scale:scale),i)).ToArray();
            var signals=new List<Signal>();var expected=BuiltInFormulaReferences.TradersDynamicOutputs(bars,code,3,4,2,5,signals);
            var result=Data(bars).CalculateTradersDynamicIndex(kind,3,4,2,5);
            foreach(var pair in expected)Assert.Equal(pair.Value,result.OutputValues[pair.Key]);Assert.Equal(signals,result.SignalsList);
            using var state=new TradersDynamicIndexState(kind,3,4,2,5);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);
                    Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true})
                    {
                        var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Tdi"][i],point.Value);
                        foreach(var pair in expected)Assert.Equal(pair.Value[i],point.Outputs![pair.Key]);
                    }
                }
            }
        }
    }
    [Fact]
    public void HugePeriodsKeepOnlyObservedHistory()
    {
        foreach(var kind in new[]{MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            using var state=new TradersDynamicWindow(kind,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue);
            foreach(var value in new[]{double.MaxValue,-double.MaxValue,1d})Assert.True(double.IsFinite(state.Next(value,true).Line));
        }
    }
    [Fact]
    public void FastOutputSelectionPreservesCallerAndInvalidInputDoesNotPublish()
    {
        var bars=Enumerable.Range(0,12).Select(i=>B(1+i%4,i)).ToArray();var data=Data(bars);
        var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.TradersDynamicOutputs(bars,1,3,4,2,5);
        foreach(var series in Enum.GetValues<IndicatorCompute.TradersDynamicSeries>())
        {
            using var context=new ComputeContext();using var result=IndicatorCompute.ComputeTradersDynamicIndexFast(data,context,3,2,MovingAvgType.SimpleMovingAverage,4,5,series);
            var key=series==IndicatorCompute.TradersDynamicSeries.Tdi?"Tdi":series.ToString();Assert.Equal(expected[key],result.ToArray());
        }
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        data.ClosePrices[0]=double.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateTradersDynamicIndex());Assert.Same(outputs,data.OutputValues);
    }
}
