using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class StochasticCyberNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersStochasticCyberCycle)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWindow(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string,double[]> Expected(IReadOnlyList<Bar> bars,IIndicator indicator)
    { var o=(EhlersStochasticCyberCycleSpecOptions)((IBuiltInIndicator)indicator).CreateOptions();return BuiltInFormulaReferences.StochasticCyberValues(bars,o.Length,o.Alpha); }
    private static void Check(Bar[] bars,int length,double alpha)
    {
        var expected=BuiltInFormulaReferences.StochasticCyberValues(bars,length,alpha);var batch=Data(bars).CalculateEhlersStochasticCyberCycle(length,alpha);
        using var context=new ComputeContext();foreach(var key in expected.Keys)
        { Assert.Equal(expected[key],batch.OutputValues[key]);using var fast=IndicatorCompute.ComputeEhlersStochasticCyberCycleFast(Data(bars),context,length,alpha,key);Assert.Equal(expected[key],fast.ToArray()); }
        var core=new double[bars.Length];OscillatorCore.EhlersStochasticCyberCycle(bars.Select(b=>b.Close).ToArray(),core,length,alpha);Assert.Equal(expected["Escc"],core);
        using var state=new EhlersStochasticCyberCycleState(length,alpha);
        for(var replay=0;replay<2;replay++)
        {
            state.Update(Native(Candle(10)),true,false);state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(Candle(double.MaxValue)),false,false);
                foreach(var commit in new[]{false,false,true})
                {var p=state.Update(Native(bars[i]),commit,true);Assert.Equal(expected["Escc"][i],p.Value);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],p.Outputs![key]);}
            }
        }
    }
    [Fact]
    public void NormalizationRetainsExtendedCyclesForExtremePricesAndCoefficients()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue}) foreach(var length in new[]{1,2,14,int.MaxValue}) foreach(var alpha in new[]{-double.MaxValue,0d,.7,2,double.MaxValue})
        {
            var bars=Enumerable.Range(0,14).Select(i=>Candle(i%3==0?-scale:scale)).ToArray();Check(bars,length,alpha);Check(Array.Empty<Bar>(),length,alpha);
            foreach(var values in BuiltInFormulaReferences.StochasticCyberValues(bars,length,alpha).Values)Assert.All(values,v=>Assert.InRange(v,-1,1));
        }
    }
    [Fact]
    public void FirstNonzeroRankAndLaggedTriggerHaveExplicitValues()
    {
        var bars=new[]{Candle(0),Candle(4),Candle(0)};var expected=BuiltInFormulaReferences.StochasticCyberValues(bars,1,.7);
        Assert.Equal(new[]{-1d,2*(.4-.5),2*(.3-.5)},expected["Escc"]);Assert.Equal(.96*.02,expected["Signal"][0]);Assert.Equal(.96*(-1+.02),expected["Signal"][1]);Check(bars,1,.7);
    }
    [Fact]
    public void SelectedPricesDriveBothOutputs()
    {
        var selected=Enumerable.Range(0,30).Select(i=>(double)(i%7)).ToList();var original=selected.Select(_=>Candle(100)).ToArray();var expected=BuiltInFormulaReferences.StochasticCyberValues(selected.Select(v=>Candle(v)).ToArray(),14,.7);
        var batch=Data(original);batch.SetCustomValues(selected);batch.CalculateEhlersStochasticCyberCycle();using var context=new ComputeContext();
        foreach(var key in expected.Keys)
        {Assert.Equal(expected[key],batch.OutputValues[key]);var data=Data(original);data.SetCustomValues(selected);using var fast=IndicatorCompute.ComputeEhlersStochasticCyberCycleFast(data,context,outputKey:key);Assert.Equal(expected[key],fast.ToArray());}
    }
    [Fact]
    public void NonfiniteAlphaIsRejectedBeforeCalculation()
    {
        foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EhlersStochasticCyberCycleState(alpha:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateEhlersStochasticCyberCycle(alpha:invalid));
            using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeEhlersStochasticCyberCycleFast(Data(Array.Empty<Bar>()),context,alpha:invalid));
        }
    }
    [Fact]
    public void RejectedFieldsDoNotAdvanceCyclesExtremaOrTrigger()
    {
        foreach(var field in Enumerable.Range(0,5)) foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}) foreach(var final in new[]{false,true})
        {
            using var state=new EhlersStochasticCyberCycleState();using var control=new EhlersStochasticCyberCycleState();var seed=Native(Candle(2));state.Update(seed,true,false);control.Update(seed,true,false);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
            foreach(var bar in Enumerable.Range(0,12).Select(i=>Native(Candle(i%3)))) {var expected=control.Update(bar,true,true);var actual=state.Update(bar,true,true);foreach(var key in expected.Outputs!.Keys)Assert.Equal(expected.Outputs[key],actual.Outputs![key]);}
        }
    }
}
