using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TripleDelayNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersTripleDelayLineDetrender)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLagDifferences(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var options = (EhlersTripleDelayLineDetrenderSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.TripleDelayOutputs(bars, options.Length, Kind(options.MaType)); }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, MovingAvgType.EhlersModifiedOptimumEllipticFilter => 7, _ => 6 };
    private static void Check(Bar[] bars, int length = 14, MovingAvgType kind = MovingAvgType.EhlersModifiedOptimumEllipticFilter)
    {
        var expected = BuiltInFormulaReferences.TripleDelayOutputs(bars, length, Kind(kind)); var batch = Data(bars).CalculateEhlersTripleDelayLineDetrender(kind, length);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys) { using var raw = IndicatorCompute.ComputeEhlersTripleDelayLineDetrenderFast(Data(bars), context, length, kind, key == "Signal"); Assert.Equal(expected[key], raw.ToArray()); }
        using var state = new EhlersTripleDelayLineDetrenderState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Etdld"][i], actual.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void SixBarImpulseAndTwoSmoothingStages()
    {
        var bars=Enumerable.Range(0,25).Select(i=>Candle(i==0?1:0)).ToArray();var expected=BuiltInFormulaReferences.TripleDelayOutputs(bars,1,1);
        Assert.Equal(1,expected["Etdld"][0]);Assert.All(expected["Etdld"].Skip(1).Take(5),v=>Assert.Equal(0,v));
        Assert.Equal(-1.712,expected["Etdld"][6],12);Assert.Equal(-.010656,expected["Etdld"][12],12);Assert.Equal(expected["Etdld"],expected["Signal"]);Check(bars,1,MovingAvgType.SimpleMovingAverage);
        foreach(var kind in new[]{MovingAvgType.EhlersModifiedOptimumEllipticFilter,MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        foreach(var period in new[]{-1,0,1,2,5,int.MaxValue}){Check(bars,period,kind);Check(Array.Empty<Bar>(),period,kind);}
    }
    [Fact]
    public void ExtendedDelayAndEllipticStagesRecoverAfterExtremePrices()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue})
        foreach(var kind in new[]{MovingAvgType.EhlersModifiedOptimumEllipticFilter,MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            var bars=Enumerable.Range(0,120).Select(i=>Candle(i<24?(i%12<6?scale:-scale):0)).ToArray();Check(bars,3,kind);
            var expected=BuiltInFormulaReferences.TripleDelayOutputs(bars,3,Kind(kind));Assert.True(double.IsFinite(expected["Signal"][^1]));
        }
    }
    [Fact]
    public void SelectedPricesDriveBothOutputs()
    {
        var bars=Enumerable.Range(0,35).Select(_=>Candle(100)).ToArray();var selected=Enumerable.Range(0,bars.Length).Select(i=>(double)(i%7)).ToArray();
        var expected=BuiltInFormulaReferences.TripleDelayOutputs(selected.Select(v=>Candle(v)).ToArray(),14,7);using var context=new ComputeContext();
        foreach(var key in expected.Keys)
        {
            var data=Data(bars);data.SetCustomValues(selected.ToList());using var fast=IndicatorCompute.ComputeEhlersTripleDelayLineDetrenderFast(data,context,signal:key=="Signal");Assert.Equal(expected[key],fast.ToArray());
            var dispatched=Data(bars);dispatched.SetCustomValues(selected.ToList());using var arm=IndicatorCompute.TryComputeFast(dispatched,new IndicatorSpec(IndicatorName.EhlersTripleDelayLineDetrender,new EhlersTripleDelayLineDetrenderSpecOptions(14),key),context);Assert.NotNull(arm);Assert.Equal(expected[key],arm.Value.ToArray());
        }
        var batch=Data(bars);batch.SetCustomValues(selected.ToList());batch.CalculateEhlersTripleDelayLineDetrender();foreach(var key in expected.Keys)Assert.Equal(expected[key],batch.OutputValues[key]);
    }
    [Fact]
    public void CustomerCallbacksReceiveDetrendedAndSmoothedValuesInOrder()
    {
        var bars=new[]{Candle(1),Candle(3),Candle(2),Candle(0)};
        foreach(var batch in new[]{false,true}) foreach(var key in new[]{"Etdld","Signal"})
        {
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[]{
                (input,period)=>{Assert.Equal(3,period);Assert.Equal(new[]{1d,3,2,0},input);return Enumerable.Repeat(17d,input.Count).ToArray();},
                (input,period)=>{Assert.Equal(3,period);Assert.All(input,v=>Assert.Equal(17d,v));return Enumerable.Repeat(42d,input.Count).ToArray();}});
            var expected=Enumerable.Repeat(key=="Etdld"?17d:42d,bars.Length).ToArray();
            if(batch)Assert.Equal(expected,Data(bars).CalculateEhlersTripleDelayLineDetrender(length:3).OutputValues[key]);
            else{using var context=new ComputeContext();using var output=IndicatorCompute.ComputeEhlersTripleDelayLineDetrenderFast(Data(bars),context,3,signal:key=="Signal");Assert.Equal(expected,output.ToArray());}
            Assert.Equal(2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersTripleDelayLineDetrenderState(); using var control = new EhlersTripleDelayLineDetrenderState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
