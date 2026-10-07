using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TunedBypassNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(EhlersDominantCycleTunedBypassFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentHarmonicSum(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.TunedBypassOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveBands(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void CycleSelectionIncludesNeighboringThreeDecibelBins()
    {
        var bars=Enumerable.Range(0,47).Select(i=>B(Math.Sin(2*Math.PI*i/13)+.2*Math.Cos(2*Math.PI*i/7),i)).ToArray();
        var expected=BuiltInFormulaReferences.TunedCycles(bars,8,18,20,1);var state=new TunedCycleWindow(8,18,20,1);
        for(var i=0;i<bars.Length;i++)Assert.Equal(expected[i],state.Next(bars[i].Close,true));
    }
    [Fact]
    public void SingleBinStartupMatchesBandpassImpulse()
    {
        var bars=new[]{B(8)};var output=Data(bars).CalculateEhlersDominantCycleTunedBypassFilter(3,3,9,1);var angle=.99;var alpha=Math.Cos(angle)/(1+Math.Sin(angle));var first=4*(1-alpha);Assert.Equal(first,output.OutputValues["V1"][0]);Assert.Equal((3/Math.PI*2)*first,output.OutputValues["V2"][0]);Assert.Equal(Signal.Buy,output.SignalsList[0]);
    }
    [Fact]
    public void ZeroPricesKeepZeroOutputsAndMinimumCycle()
    {
        var cycle=new TunedCycleWindow(3,8,9,3);using var state=new TunedBypassWindow(3,8,9,3);
        for(var i=0;i<15;i++){Assert.Equal(3,cycle.Next(0,true));var p=state.Next(0,true);Assert.Equal(0,p.First);Assert.Equal(0,p.Second);Assert.Equal(Signal.None,p.Trade);}
    }
    [Fact]
    public void ExtremePricesPreserveHarmonicsSignalsPreviewAndReset()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/64})
        {
            var bars=Enumerable.Range(0,31).Select(i=>B((i%7-3)*scale,i)).ToArray();var signals=new List<Signal>();var expected=BuiltInFormulaReferences.TunedBypassOutputs(bars,3,8,9,3,signals);
            var actual=Data(bars).CalculateEhlersDominantCycleTunedBypassFilter(3,8,9,3);foreach(var pair in expected)Assert.Equal(pair.Value,actual.OutputValues[pair.Key]);Assert.Equal(signals,actual.SignalsList);
            using var state=new EhlersDominantCycleTunedBypassFilterState(3,8,9,3);using var signalState=new TunedBypassWindow(3,8,9,3);
            for(var pass=0;pass<2;pass++)
            {
                for(var i=0;i<7;i++){state.Update(Native(B(17)),true,false);signalState.Next(17,true);}state.Reset();signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["V2"][i],point.Value);foreach(var pair in expected)Assert.Equal(pair.Value[i],point.Outputs![pair.Key]);Assert.Equal(signals[i],signalState.Next(bars[i].Close,final).Trade);}
                }
            }
        }
    }
    [Fact]
    public void HugePeriodsAndFastSelectionsPreserveCallerState()
    {
        var bars=Enumerable.Range(0,11).Select(i=>B(i%7,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.TunedBypassOutputs(bars,int.MaxValue-1,int.MaxValue,int.MaxValue,int.MaxValue);
        foreach(var key in expected.Keys){using var context=new ComputeContext();using var result=IndicatorCompute.ComputeTunedBypassFast(data,context,int.MaxValue-1,int.MaxValue,int.MaxValue,int.MaxValue,key);Assert.Equal(expected[key],result.ToArray());}
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        data.ClosePrices[2]=double.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateEhlersDominantCycleTunedBypassFilter());Assert.Same(outputs,data.OutputValues);
    }
}
