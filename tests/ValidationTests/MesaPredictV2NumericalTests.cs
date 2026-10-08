using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MesaPredictV2NumericalTests
{
    private static Bar B(double price,int i=0)=>new(DateTime.UnixEpoch.AddMinutes(i),price,price,price,price,1);
    private static StockData Data(IReadOnlyList<Bar> bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("MESA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EhlersMesaPredictIndicatorV2)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void RoutesMatchIndependentForecast(IndicatorValidationCase c,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.MesaPredictV2Outputs(bars,(IBuiltInIndicator)c.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveForecast(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void StartupIgnoresFourthSampleCurvature()
    {
        var bars=new[]{0d,0,0,8,0}.Select((v,i)=>B(v,i)).ToArray();var actual=Data(bars).CalculateEhlersMesaPredictIndicatorV2(MovingAvgType.WeightedMovingAverage,1,9,1,1).OutputValues;
        Assert.All(actual["Ssf"].Take(4),v=>Assert.Equal(0,v));Assert.NotEqual(0,actual["Ssf"][4]);
    }
    [Fact]
    public void SingleHistoryKeepsOneStepForecastAndLinearExtrapolation()
    {
        var bars=new[]{0d,0,0,0,8,4,2}.Select((v,i)=>B(v,i)).ToArray();var output=Data(bars).CalculateEhlersMesaPredictIndicatorV2(MovingAvgType.WeightedMovingAverage,1,9,1,7).OutputValues;
        Assert.All(output["Predict"].Take(4),v=>Assert.Equal(0,v));Assert.True(output["Ssf"][4]>0);for(var i=4;i<bars.Length;i++){Assert.Equal(4.525*output["Ssf"][i],output["Predict"][i]);Assert.Equal(2*output["Ssf"][i],output["Extrap"][i]);}
    }
    [Theory]
    [InlineData(MovingAvgType.EhlersHannMovingAverage,true)] [InlineData(MovingAvgType.WeightedMovingAverage,false)]
    public void ExtremePricesPreserveForecastSignalsPreviewAndReset(MovingAvgType kind,bool hann)
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/8})
        {
            var bars=Enumerable.Range(0,23).Select(i=>B((i%7-3)*scale,i)).ToArray();var signals=new List<Signal>();var expected=BuiltInFormulaReferences.MesaPredictV2Outputs(bars,5,9,3,3,hann,signals);var actual=Data(bars).CalculateEhlersMesaPredictIndicatorV2(kind,5,9,3,3);
            foreach(var pair in expected)Assert.Equal(pair.Value,actual.OutputValues[pair.Key]);Assert.Equal(signals,actual.SignalsList);
            using var state=new EhlersMesaPredictIndicatorV2State(kind,5,9,3,3);using var signalState=new MesaPredictV2Window(kind,5,9,3,3);
            for(var pass=0;pass<2;pass++)
            {
                for(var i=0;i<8;i++){state.Update(Native(B(17)),true,false);signalState.Next(17,true);}state.Reset();signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Predict"][i],point.Value);foreach(var pair in expected)Assert.Equal(pair.Value[i],point.Outputs![pair.Key]);Assert.Equal(signals[i],signalState.Next(bars[i].Close,final).Trade);}
                }
            }
        }
    }
    [Fact]
    public void HugeHistoryAndSmoothingUseObservedSamplesAndPreserveCaller()
    {
        var bars=new[]{1d,3,2,-1,0,4,2,7}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.MesaPredictV2Outputs(bars,int.MaxValue,int.MaxValue,int.MaxValue,4,true);
        foreach(var pair in new[]{("Ssf",IndicatorCompute.MesaPredictSeries.Filter),("Predict",IndicatorCompute.MesaPredictSeries.Predict),("Extrap",IndicatorCompute.MesaPredictSeries.Extrapolate)}){using var context=new ComputeContext();using var result=IndicatorCompute.ComputeEhlersMesaPredictIndicatorV2Fast(data,context,int.MaxValue,int.MaxValue,int.MaxValue,4,MovingAvgType.EhlersHannMovingAverage,pair.Item2);Assert.Equal(expected[pair.Item1],result.ToArray());}
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        data.ClosePrices[2]=double.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateEhlersMesaPredictIndicatorV2());Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);
        using var zero=new MesaPredictV2Window(MovingAvgType.EhlersHannMovingAverage,int.MaxValue,135,12,int.MaxValue);Assert.Equal(0,zero.Next(0,true).Predict);
    }
}
