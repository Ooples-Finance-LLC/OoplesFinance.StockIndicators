using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AnticipateNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAnticipateIndicator)).Select(c => new object[] { c });
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
    [Fact]
    public void ExtendedImpulseCapturePreservesPublicValuesAndExactStage()
    {
        foreach(var kind in new[]{MovingAvgType.EhlersHannMovingAverage,MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            var bars=Enumerable.Range(0,48).Select(i=>Candle(Math.Sin(i*.4)*double.MaxValue)).ToArray();
            var trajectory=new ReferenceFraction[bars.Length];
            var expected=BuiltInFormulaReferences.ImpulseResponseValues(bars,14,1,Kind(kind),trajectory);
            using var window=new ImpulseResponseWindow(kind,14,1);
            for(var i=0;i<bars.Length;i++)
            {
                var value=window.Next(bars[i].Close,true,true); Assert.Equal(expected[i],value);
                var actual=ReferenceFraction.FromDouble(window.ExtendedOutput.Mantissa)*new ReferenceFraction(System.Numerics.BigInteger.One<<window.ExtendedOutput.UpperShift);
                Assert.Equal(0,actual.CompareTo(trajectory[i]));
            }
            window.Reset(); Assert.Equal(0,window.ExtendedOutput.Mantissa); Assert.Equal(0,window.Next(1,true,true));
        }
    }
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = (EhlersAnticipateIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Predict", BuiltInFormulaReferences.AnticipateValues(bars, o.Length, o.Bw, Kind(o.MaType)) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, MovingAvgType.WildersSmoothingMethod => 6, _ => 7 };
    private static void Check(Bar[] bars, int length, double bw, MovingAvgType kind)
    {
        var expected = BuiltInFormulaReferences.AnticipateValues(bars, length, bw, Kind(kind));
        var batch = Data(bars).CalculateEhlersAnticipateIndicator(kind, length, bw); Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersAnticipateIndicatorFast(Data(bars), context, length: length, bw: bw, maType: kind); Assert.Equal(expected, result.ToArray());
        using var state = new EhlersAnticipateIndicatorState(kind, length, bw);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(-10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["Predict"]);
                }
            }
        }
    }
    [Fact]
    public void BandAndSmoothedStagesCoverExtremesAndPeriods()
    {
        foreach (var kind in new[] { MovingAvgType.EhlersHannMovingAverage, MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var length in new[] { 1, 2, 7, 14 })
        {
            var bars = Enumerable.Range(0, 18).Select(i => Candle(i < 12 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray();
            Check(bars, length, 1, kind); Check(Array.Empty<Bar>(), length, 1, kind);
        }
        foreach (var width in new[] { -double.MaxValue, 0, double.MaxValue }) Check(Enumerable.Range(0, 18).Select(i => Candle(i % 3)).ToArray(), 20, width, MovingAvgType.EhlersHannMovingAverage);
    }
    [Fact]
    public void OverflowingFilteredValuesRetainTheirPhase()
    {
        const int count=128,length=14;
        var impulse=Enumerable.Range(0,count).Select(i=>Candle(i==3?1:0)).ToArray();
        var response=BuiltInFormulaReferences.ImpulseResponseValues(impulse,length,double.MaxValue,7);
        var bars=Enumerable.Range(0,count).Select(i=>Candle(i<3?0:Math.Sign(response[count+2-i])*double.MaxValue)).ToArray();
        var stages=new ReferenceFraction[count];
        BuiltInFormulaReferences.ImpulseResponseValues(bars,length,double.MaxValue,7,stages);
        Assert.Contains(stages,v=>double.IsInfinity(v.ToDouble()));
        Check(bars,length,double.MaxValue,MovingAvgType.EhlersHannMovingAverage);
    }
    [Fact]
    public void SelectedInputsAndSignalsPreserveThePredictionContract()
    {
        var bars = Enumerable.Range(0,30).Select(i=>Candle(i%7-3)).ToArray();
        var expected = BuiltInFormulaReferences.AnticipateValues(bars,7,1,7);
        var data = Data(bars.Select(_=>Candle(100)).ToArray()); data.SetCustomValues(bars.Select(b=>b.Close).ToList());
        data.CalculateEhlersAnticipateIndicator(length:7);
        Assert.Equal(expected,data.CustomValuesList);
        Assert.Equal(expected.Select((v,i)=>SignalHelper.GetCompareSignal(v,i==0?0:expected[i-1])),data.SignalsList);
    }
    [Fact]
    public void RejectedFieldsAndBandwidthDoNotAdvanceState()
    {
        foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EhlersAnticipateIndicatorState(bw:invalid));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateEhlersAnticipateIndicator(bw:invalid));
            foreach(var field in Enumerable.Range(0,5)) foreach(var final in new[]{false,true})
            {
                using var state = new EhlersAnticipateIndicatorState(length:7); using var control = new EhlersAnticipateIndicatorState(length:7);
                foreach(var bar in Enumerable.Range(0,6).Select(i=>Native(Candle(i%3)))) { state.Update(bar,true,false); control.Update(bar,true,false); }
                var v = new[]{1d,3,0,2,1}; v[field]=invalid;
                Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
                foreach(var bar in Enumerable.Range(0,8).Select(i=>Native(Candle(i%5)))) Assert.Equal(control.Update(bar,true,true).Value,state.Update(bar,true,true).Value);
            }
        }
    }
    [Fact]
    public void MaximumPeriodStartupUsesOnlyObservedHistory()
    {
        using var state = new EhlersAnticipateIndicatorState(length:4096);
        for(var i=0;i<6;i++) Assert.Equal(0,state.Update(Native(Candle(0)),true,true).Value);
        Assert.All(Data(new[]{Candle(0),Candle(0)}).CalculateEhlersAnticipateIndicator(length:4096).CustomValuesList,v=>Assert.Equal(0,v));
    }
}
