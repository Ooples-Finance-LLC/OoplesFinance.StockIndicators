using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DistanceCoefficientNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersDistanceCoefficientFilter)).Select(c => new object[] { c });
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
    {var o=(EhlersDistanceCoefficientFilterSpecOptions)((IBuiltInIndicator)indicator).CreateOptions();return new(){{"Edcf",BuiltInFormulaReferences.DistanceCoefficientValues(bars,o.Length)}};}
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.DistanceCoefficientValues(bars,length);Assert.Equal(expected,Data(bars).CalculateEhlersDistanceCoefficientFilter(length).CustomValuesList);
        using var context=new ComputeContext();using var fast=IndicatorCompute.ComputeEhlersDistanceCoefficientFilterFast(Data(bars),context,length);Assert.Equal(expected,fast.ToArray());
        using var state=new EhlersDistanceCoefficientFilterState(length);
        for(var replay=0;replay<2;replay++)
        {
            state.Update(Native(Candle(10)),true,false);state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(Candle(double.MaxValue)),false,false);
                foreach(var commit in new[]{false,false,true}){var p=state.Update(Native(bars[i]),commit,true);Assert.Equal(expected[i],p.Value);Assert.Equal(expected[i],p.Outputs!["Edcf"]);}
            }
        }
    }
    [Fact]
    public void ExactSquaredWeightsHandleExtremesAndPeriods()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue}) foreach(var length in new[]{-1,0,1,2,15,int.MaxValue})
        {
            var bars=Enumerable.Range(0,24).Select(i=>Candle(i%3==0?-scale:scale)).ToArray();Check(bars,length);Check(Array.Empty<Bar>(),length);
            Assert.All(BuiltInFormulaReferences.DistanceCoefficientValues(bars,length),v=>Assert.InRange(v,-scale,scale));
        }
    }
    [Fact]
    public void HandWeightedMeanAndFlatFallbackAreExplicit()
    {
        var bars=new[]{Candle(1),Candle(3),Candle(2)};Assert.Equal(new[]{1d,13d/5,14d/5},BuiltInFormulaReferences.DistanceCoefficientValues(bars,2));Check(bars,2);
        foreach(var value in new[]{double.Epsilon,7d,double.MaxValue})
        {var flat=Enumerable.Range(0,12).Select(_=>Candle(value)).ToArray();Assert.All(BuiltInFormulaReferences.DistanceCoefficientValues(flat,2),v=>Assert.Equal(value,v));Check(flat,2);}
    }
    [Fact]
    public void SelectedPricesDriveWeightsAndWeightedValues()
    {
        var selected=Enumerable.Range(0,30).Select(i=>(double)(i%7)).ToList();var original=selected.Select(_=>Candle(100)).ToArray();var expected=BuiltInFormulaReferences.DistanceCoefficientValues(selected.Select(v=>Candle(v)).ToArray(),14);
        var batch=Data(original);batch.SetCustomValues(selected);batch.CalculateEhlersDistanceCoefficientFilter();Assert.Equal(expected,batch.CustomValuesList);
        using var context=new ComputeContext();var data=Data(original);data.SetCustomValues(selected);using var fast=IndicatorCompute.ComputeEhlersDistanceCoefficientFilterFast(data,context);Assert.Equal(expected,fast.ToArray());
    }
    [Fact]
    public void RejectedFieldsPreservePriceAndWeightHistory()
    {
        foreach(var field in Enumerable.Range(0,5)) foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}) foreach(var final in new[]{false,true})
        {
            using var state=new EhlersDistanceCoefficientFilterState();using var control=new EhlersDistanceCoefficientFilterState();var seed=Native(Candle(2));state.Update(seed,true,false);control.Update(seed,true,false);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
            foreach(var bar in Enumerable.Range(0,12).Select(i=>Native(Candle(i%3))))Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);
        }
    }
}
