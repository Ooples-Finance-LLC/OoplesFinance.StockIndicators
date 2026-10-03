using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AmDetectorNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAMDetector)).Select(c => new object[] { c });
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
    { var options = (EhlersAMDetectorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.AmDetectorOutputs(bars, options.Length1, options.Length2, Kind(options.MaType)); }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length1 = 4, int length2 = 8, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.AmDetectorOutputs(bars, length1, length2, Kind(kind)); var batch = Data(bars).CalculateEhlersAMDetector(kind, length1, length2);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys) { using var raw = IndicatorCompute.ComputeEhlersAMDetectorFast(Data(bars), context, length1, length2, kind, key == "Signal"); Assert.Equal(expected[key], raw.ToArray()); }
        using var state = new EhlersAMDetectorState(kind, length1, length2);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Eamd"][i], actual.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void HandEnvelopeAndTwoSmoothingStages()
    {
        var bars=new[]{Candle(1),Candle(3),Candle(2),Candle(0)};
        var expected=BuiltInFormulaReferences.AmDetectorOutputs(bars,2,2,1);
        Assert.Equal(new[]{0d,2,3,2.5},expected["Eamd"]);Assert.Equal(new[]{0d,1,2.5,2.75},expected["Signal"]);Check(bars,2,2);
        foreach(var kind in new[]{MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        foreach(var period in new[]{-1,0,1,2,5,int.MaxValue})
        {Check(bars,period,3,kind);Check(bars,3,period,kind);Check(Array.Empty<Bar>(),period,period,kind);}
    }
    [Fact]
    public void ExtendedEnvelopeSurvivesSmoothingAndRecovers()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue})
        foreach(var kind in new[]{MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            var bars=Enumerable.Range(0,40).Select(i=>new Bar(DateTime.UnixEpoch,i<8?-scale:0,scale,-scale,i<8?scale:0,1)).ToArray();Check(bars,2,3,kind);
            var expected=BuiltInFormulaReferences.AmDetectorOutputs(bars,2,3,Kind(kind));Assert.True(double.IsFinite(expected["Signal"][^1]));
        }
        var wide=new[]{new Bar(DateTime.UnixEpoch,-double.MaxValue,double.MaxValue,-double.MaxValue,double.MaxValue,1),Candle(0),Candle(0),Candle(0)};
        var narrowed=BuiltInFormulaReferences.AmDetectorOutputs(wide,1,2,1);Assert.Equal(double.MaxValue,narrowed["Eamd"][1]);Check(wide,1,2);
    }
    [Fact]
    public void SelectedPricesPreserveOriginalOpenAcrossBothOutputs()
    {
        var bars=Enumerable.Range(0,30).Select(i=>new Bar(DateTime.UnixEpoch,i%5,10,0,2,1)).ToArray();var selected=Enumerable.Range(0,bars.Length).Select(i=>(double)(i%7)).ToArray();
        var expected=BuiltInFormulaReferences.AmDetectorOutputs(bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray(),4,8,1);
        using var context=new ComputeContext();
        foreach(var key in expected.Keys)
        {
            var data=Data(bars);data.SetCustomValues(selected.ToList());using var fast=IndicatorCompute.ComputeEhlersAMDetectorFast(data,context,signal:key=="Signal");Assert.Equal(expected[key],fast.ToArray());
            var dispatched=Data(bars);dispatched.SetCustomValues(selected.ToList());using var arm=IndicatorCompute.TryComputeFast(dispatched,new IndicatorSpec(IndicatorName.EhlersAMDetector,new EhlersAMDetectorSpecOptions(4,8),key),context);Assert.NotNull(arm);Assert.Equal(expected[key],arm.Value.ToArray());
        }
        var batch=Data(bars);batch.SetCustomValues(selected.ToList());batch.CalculateEhlersAMDetector();foreach(var key in expected.Keys)Assert.Equal(expected[key],batch.OutputValues[key]);
    }
    [Fact]
    public void CustomerCallbacksReceivePriceEnvelopeAndLineInOrder()
    {
        var bars=new[]{Candle(1),Candle(3),Candle(2),Candle(0)};
        foreach(var batch in new[]{false,true}) foreach(var key in new[]{"Eamd","Signal"})
        {
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[]{
                (input,period)=>{Assert.Equal(2,period);Assert.Equal(new[]{1d,3,2,0},input);return Enumerable.Repeat(9d,input.Count).ToArray();},
                (input,period)=>{Assert.Equal(3,period);Assert.Equal(new[]{1d,3,3,2},input);return Enumerable.Repeat(17d,input.Count).ToArray();},
                (input,period)=>{Assert.Equal(3,period);Assert.All(input,v=>Assert.Equal(17d,v));return Enumerable.Repeat(42d,input.Count).ToArray();}});
            var expected=Enumerable.Repeat(key=="Eamd"?17d:42d,bars.Length).ToArray();
            if(batch)Assert.Equal(expected,Data(bars).CalculateEhlersAMDetector(length1:2,length2:3).OutputValues[key]);
            else{using var context=new ComputeContext();using var output=IndicatorCompute.ComputeEhlersAMDetectorFast(Data(bars),context,2,3,signal:key=="Signal");Assert.Equal(expected,output.ToArray());}
            Assert.Equal(3,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersAMDetectorState(); using var control = new EhlersAMDetectorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
