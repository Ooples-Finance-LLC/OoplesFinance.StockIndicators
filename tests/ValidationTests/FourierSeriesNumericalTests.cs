using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FourierSeriesNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(EhlersFourierSeriesAnalysis)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentHarmonicSum(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.FourierSeriesOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveBands(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void HandSingleResolvedHarmonicPreservesStartupAndRoc()
    {
        var bars=new[]{0d,0,0,0,8}.Select((v,i)=>B(v,i)).ToArray();var result=Data(bars).CalculateEhlersFourierSeriesAnalysis(4,.1);var angle=.1*2*Math.PI/4;var pole=Math.Cos(angle)/(1+Math.Sin(angle));var wave=4*(1-pole);
        Assert.Equal(new[]{0d,0,0,0,wave},result.OutputValues["Wave"]);Assert.Equal(wave/Math.PI,result.OutputValues["Roc"][4],14);Assert.Equal(Signal.StrongBuy,result.SignalsList[4]);
    }
    [Fact]
    public void UnresolvedHarmonicsAndZeroBandwidthStayZero()
    {
        var bars=Enumerable.Range(0,13).Select(i=>B(i%2==0?double.MaxValue:-double.MaxValue,i)).ToArray();
        foreach(var pair in new[]{(1,.1),(2,.1),(20,0d)}){var result=Data(bars).CalculateEhlersFourierSeriesAnalysis(pair.Item1,pair.Item2);foreach(var values in result.OutputValues.Values)Assert.All(values,v=>Assert.Equal(0,v));}
    }
    [Fact]
    public void ExtremePricesPreserveHarmonicsSignalsPreviewAndReset()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/64})
        {
            var bars=Enumerable.Range(0,31).Select(i=>B((i%7-3)*scale,i)).ToArray();var signals=new List<Signal>();var expected=BuiltInFormulaReferences.FourierSeriesOutputs(bars,7,.2,signals);
            var actual=Data(bars).CalculateEhlersFourierSeriesAnalysis(7,.2);foreach(var pair in expected)Assert.Equal(pair.Value,actual.OutputValues[pair.Key]);Assert.Equal(signals,actual.SignalsList);
            using var state=new EhlersFourierSeriesAnalysisState(7,.2);using var signalState=new FourierSeriesWindow(7,.2);
            for(var pass=0;pass<2;pass++)
            {
                for(var i=0;i<7;i++){state.Update(Native(B(17)),true,false);signalState.Next(17,true);}state.Reset();signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Wave"][i],point.Value);foreach(var pair in expected)Assert.Equal(pair.Value[i],point.Outputs![pair.Key]);Assert.Equal(signals[i],signalState.Next(bars[i].Close,final).Trade);}
                }
            }
        }
    }
    [Fact]
    public void HugePeriodsAndFastSelectionsPreserveCallerState()
    {
        var bars=Enumerable.Range(0,11).Select(i=>B(i%7,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.FourierSeriesOutputs(bars,int.MaxValue,.1);
        foreach(var key in expected.Keys){using var context=new ComputeContext();using var result=IndicatorCompute.ComputeFourierSeriesFast(data,context,int.MaxValue,.1,key);Assert.Equal(expected[key],result.ToArray());}
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateEhlersFourierSeriesAnalysis(bw:double.NaN));Assert.Same(outputs,data.OutputValues);
    }
}
