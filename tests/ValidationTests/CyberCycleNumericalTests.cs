using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CyberCycleNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersCyberCycle) || c.IndicatorType == typeof(EhlersCyberCycleOscillator)).Select(c => new object[] { c });
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
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = ((IBuiltInIndicator)indicator).CreateOptions(); var alpha = o is EhlersCyberCycleSpecOptions p ? 2d/(p.Length+1d) : ((EhlersCyberCycleOscillatorSpecOptions)o).Alpha; return new() { { "Ecc", BuiltInFormulaReferences.CyberCycleValues(bars,alpha) } }; }
    private static void Check(Bar[] bars, double alpha)
    {
        var expected = BuiltInFormulaReferences.CyberCycleValues(bars,alpha);
        Assert.Equal(expected,Data(bars).CalculateEhlersCyberCycle(alpha).CustomValuesList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersCyberCycleOscillatorFast(Data(bars),context,alpha); Assert.Equal(expected,fast.ToArray());
        var prices=bars.Select(b=>b.Close).ToArray(); var core=new double[bars.Length];
        MovingAverageCore.EhlersCyberCycle(prices,core,alpha); Assert.Equal(expected,core);
        OscillatorCore.EhlersCyberCycleOscillator(prices,core,alpha); Assert.Equal(expected,core);
        var state=new EhlersCyberCycleState(alpha);
        for(var replay=0;replay<2;replay++)
        {
            state.Update(Native(Candle(10)),true,false);state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(Candle(double.MaxValue)),false,false);
                foreach(var commit in new[]{false,false,true}) {var p=state.Update(Native(bars[i]),commit,true);Assert.Equal(expected[i],p.Value);Assert.Equal(expected[i],p.Outputs!["Ecc"]);}
            }
        }
    }
    [Fact]
    public void WidePricesAndCoefficientsPreserveStartupAndFeedback()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue}) foreach(var alpha in new[]{-double.MaxValue,0d,.07,1,2,double.MaxValue})
        {Check(Enumerable.Range(0,14).Select(i=>Candle(i<10?(i%3==0?-scale:scale):0)).ToArray(),alpha);Check(Array.Empty<Bar>(),alpha);}
    }
    [Fact]
    public void SeventhBarUsesPublishedStartupFeedback()
    {
        var bars=Enumerable.Range(0,12).Select(i=>Candle(i==6?1:0)).ToArray();var expected=BuiltInFormulaReferences.CyberCycleValues(bars,0);
        Assert.All(expected.Take(6),v=>Assert.Equal(0,v));Assert.Equal(.25,expected[6]);Assert.Equal(.5,expected[7]);Check(bars,0);
    }
    [Fact]
    public void SelectedSourcesAndMaximumPeriodAgreeAcrossTypedRoutes()
    {
        var selected=Enumerable.Range(0,30).Select(i=>(double)(i%7)).ToList();var original=selected.Select(_=>Candle(100)).ToArray();
        foreach(var length in new[]{1,14,int.MaxValue})
        {
            var expected=BuiltInFormulaReferences.CyberCycleValues(selected.Select(v=>Candle(v)).ToArray(),2d/(length+1d));
            using var context=new ComputeContext();var data=Data(original);data.SetCustomValues(selected);
            using var fast=IndicatorCompute.ComputeEhlersCyberCycleFast(data,context,length);Assert.Equal(expected,fast.ToArray());
            var options=new EhlersCyberCycleSpecOptions(length);var spec=new IndicatorSpec(IndicatorName.EhlersCyberCycle,options,"Ecc");
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(),out var target));data=Data(original);data.SetCustomValues(selected);
            Assert.Equal(expected,BuilderArmBinding.Compute(data,spec,target).ToArray());
            var state=StatefulIndicatorFactory.Create(spec);for(var i=0;i<selected.Count;i++)Assert.Equal(expected[i],state.Update(Native(Candle(selected[i])),true,true).Value);
        }
        var alphaExpected=BuiltInFormulaReferences.CyberCycleValues(selected.Select(v=>Candle(v)).ToArray(),.07);
        var batch=Data(original);batch.SetCustomValues(selected);batch.CalculateEhlersCyberCycle();Assert.Equal(alphaExpected,batch.CustomValuesList);
        using var ctx=new ComputeContext();var alias=Data(original);alias.SetCustomValues(selected);using var output=IndicatorCompute.ComputeEhlersCyberCycleOscillatorFast(alias,ctx);Assert.Equal(alphaExpected,output.ToArray());
    }
    [Fact]
    public void NonfiniteAlphaIsRejectedBeforeCalculation()
    {
        foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EhlersCyberCycleState(invalid));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateEhlersCyberCycle(invalid));
            using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeEhlersCyberCycleOscillatorFast(Data(Array.Empty<Bar>()),context,invalid));
        }
    }
    [Fact]
    public void InvalidFieldsLeaveSmoothingAndCycleHistoryUnchanged()
    {
        foreach(var field in Enumerable.Range(0,5)) foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}) foreach(var final in new[]{false,true})
        {
            var state=new EhlersCyberCycleState();var control=new EhlersCyberCycleState();var seed=Native(Candle(2));state.Update(seed,true,false);control.Update(seed,true,false);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
            foreach(var bar in Enumerable.Range(0,12).Select(i=>Native(Candle(i%3))))Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);
        }
    }
}
