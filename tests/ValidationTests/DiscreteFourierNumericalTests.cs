using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DiscreteFourierNumericalTests
{
    private static Bar B(double price,int i=0)=>new(DateTime.UnixEpoch.AddMinutes(i),price,price,price,price,1);
    private static StockData Data(IReadOnlyList<Bar> bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("DFT",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EhlersDiscreteFourierTransform)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void RoutesMatchIndependentSpectrum(IndicatorValidationCase c,string route)
        =>new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.DiscreteFourierOutputs(bars,(IBuiltInIndicator)c.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveSpectrum(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void DyadicSpectrumMatchesOriginalRationalQuadrature()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/8})
        {
            var bars=Enumerable.Range(0,11).Select(i=>B((i%7-3)*scale,i)).ToArray();
            var original=BuiltInFormulaReferences.DiscreteFourierOutputs(bars,3,7,9,rationalQuadrature:true)["Edft"];
            Assert.Equal(original,BuiltInFormulaReferences.DiscreteFourierOutputs(bars,3,7,9)["Edft"]);
        }
    }
    [Fact]
    public void SingleSampleWeightsAllBinsEquallyAndZeroHasNoCycle()
    {
        Assert.Equal(5,Data(new[]{B(1)}).CalculateEhlersDiscreteFourierTransform(3,7,40).OutputValues["Edft"][0]);
        Assert.Equal(0,Data(new[]{B(0)}).CalculateEhlersDiscreteFourierTransform(3,7,40).OutputValues["Edft"][0]);
        using var state=new DiscreteFourierCycle(3,7,40);state.Next(double.Epsilon,true,out _);Assert.Equal(Signal.StrongBuy,state.LastSignal);
    }
    [Fact]
    public void ExtremePricesPreserveSpectrumSignalsPreviewAndReset()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/4})
        {
            var bars=Enumerable.Range(0,19).Select(i=>B((i%7-3)*scale,i)).ToArray();var signals=new List<Signal>();var expected=BuiltInFormulaReferences.DiscreteFourierOutputs(bars,3,8,9,signals)["Edft"];
            var actual=Data(bars).CalculateEhlersDiscreteFourierTransform(3,8,9);Assert.Equal(expected,actual.OutputValues["Edft"]);Assert.Equal(signals,actual.SignalsList);
            using var state=new EhlersDiscreteFourierTransformState(3,8,9);using var signalState=new DiscreteFourierCycle(3,8,9);
            for(var pass=0;pass<2;pass++)
            {
                for(var i=0;i<9;i++){state.Update(Native(B(17)),true,false);signalState.Next(17,true,out _);}state.Reset();signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs!["Edft"]);signalState.Next(bars[i].Close,final,out _);Assert.Equal(signals[i],signalState.LastSignal);}
                }
            }
        }
    }
    [Fact]
    public void NarrowHugeBinRangeUsesObservedHistoryAndPreservesCaller()
    {
        var bars=new[]{1d,3,2,-1,0,4,2,7}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.DiscreteFourierOutputs(bars,int.MaxValue-1,int.MaxValue,int.MaxValue)["Edft"];
        using var context=new ComputeContext();using var result=IndicatorCompute.ComputeDiscreteFourierFast(data,context,int.MaxValue-1,int.MaxValue,int.MaxValue);Assert.Equal(expected,result.ToArray());
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        data.ClosePrices[2]=double.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateEhlersDiscreteFourierTransform());Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);
    }
    [Fact]
    public void HighPassRetainsPoleDriveAndSixTapCleanup()
    {
        using var state=new DiscreteFourierCycle(3,3,40);foreach(var value in new[]{0d,0,0,0,0,0})state.Next(value,true,out _);
        Assert.Equal(3,state.Next(8,true,out var hp));var angle=2*Math.PI/40;var pole=Math.Cos(angle)/(1+Math.Sin(angle));Assert.Equal(8*((1+pole)/2),hp);
        state.Next(8,true,out var next);Assert.Equal(pole*hp,next);
    }
}
