using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RocketRsiNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = new IndicatorErrorBudget(1e-12, 1e-12, true);
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersRocketRelativeStrengthIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLagDifferences(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), new IndicatorErrorBudget(1e-12, 1e-12, true));
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.Ehlers2PoleSuperSmootherFilterV2, MovingAvgType.WeightedMovingAverage };
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = (EhlersRocketRelativeStrengthIndexSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Errsi", BuiltInFormulaReferences.RocketRsiValues(bars, o.Length1, o.Length2, o.MaType, o.Mult) } }; }
    private static double[] Check(Bar[] bars, int first = 10, int second = 8, MovingAvgType kind = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2, double mult = 1)
    {
        var expected = BuiltInFormulaReferences.RocketRsiValues(bars, first, second, kind, mult); var batch = Data(bars).CalculateEhlersRocketRelativeStrengthIndex(kind, first, second, mult: mult); Equal(expected, batch.CustomValuesList.ToArray()); Equal(expected, batch.OutputValues["Errsi"].ToArray());
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersRocketRsiFast(Data(bars), context, first, second, mult, kind); Equal(expected, fast.ToArray());
        using var state = new EhlersRocketRelativeStrengthIndexState(kind, first, second, mult: mult);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.True(Budget.Accepts(expected[i], point.Value), $"bar {i}: {expected[i]:R} != {point.Value:R}"); Assert.Equal(point.Value, point.Outputs!["Errsi"]); }
            }
        }
        return batch.CustomValuesList.ToArray();
    }
    [Fact]
    public void WideAndTinyChangesRetainFilteredGainLossRatios()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue })
            Check(Enumerable.Range(0, 65).Select(i => Candle(i % 3 == 0 ? -scale : scale)).ToArray(), 5, 8, kind);
        Check(new[] { double.MaxValue, -double.MaxValue, 0, double.Epsilon, -double.Epsilon, double.Epsilon }.Select(v => Candle(v)).ToArray(), 2);
    }
    [Fact]
    public void LagAndFirstChangeUseZeroPrehistory()
    {
        foreach (var kind in Kinds) foreach (var first in new[] { 1, 2, 5, 10 })
        {
            var values = Check(Enumerable.Range(0, 65).Select(i => Candle(i)).ToArray(), first, 8, kind);
            Assert.All(values.Take(first - 1), v => Assert.Equal(0, v));
            if (first == 1) Assert.All(values, v => Assert.Equal(0, v)); else Assert.Contains(values, v => v > 0);
        }
    }
    [Fact]
    public void SubnormalFilterStagesPreservePowerOfTwoInvariance()
    {
        var prices = Enumerable.Range(0, 65).Select(i => (double)(i % 7 - 3)).ToArray();
        foreach (var kind in Kinds)
        {
            var baseline = Check(prices.Select(v => Candle(v)).ToArray(), 5, 8, kind);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) Equal(baseline, Check(prices.Select(v => Candle(v * scale)).ToArray(), 5, 8, kind));
        }
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyConsumedHistory()
    {
        var bars = Enumerable.Range(0, 25).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, length, length, kind); Check(Array.Empty<Bar>(), length, length, kind); }
        var values = Check(bars, 2, int.MaxValue); Assert.Contains(values, v => v != 0);
    }
    [Fact]
    public void SettledImpulseExpiresWithoutResidualGainLoss()
    {
        foreach (var kind in Kinds) Check(Enumerable.Range(0, 150).Select(i => Candle(i == 3 ? 4 : 0)).ToArray(), 3, 4, kind);
    }
    [Fact]
    public void TinyFisherSignalsRetainTheirSign()
    {
        foreach (var sign in new[] { -1d, 1d })
        {
            using var window = new RocketRsiWindow(MovingAvgType.WeightedMovingAverage, 3, 2, 1, true);
            window.Finish(sign, true); window.Finish(0, true); var value = window.Finish(sign * 1e-20, true); Assert.Equal(Math.Sign(sign), Math.Sign(value)); Assert.InRange(Math.Abs(value), 4.99e-21, 5.01e-21);
        }
    }
    [Fact]
    public void SelectedPricesReachBothBatchAndFastPaths()
    {
        var selected = Enumerable.Range(0, 65).Select(i => (double)(i % 7 - 3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.RocketRsiValues(selected.Select(v => Candle(v)).ToArray(), 3, 8, Kinds[0], 1);
        var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersRocketRelativeStrengthIndex(length1: 3); Equal(expected, data.CustomValuesList.ToArray());
        using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected); using var fast = IndicatorCompute.ComputeEhlersRocketRsiFast(data, context, 3); Equal(expected, fast.ToArray());
    }
    [Fact]
    public void InvalidFieldsLeaveFilterAndRollingSumsUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersRocketRelativeStrengthIndexState(length1: 3); using var control = new EhlersRocketRelativeStrengthIndexState(length1: 3); state.Update(Native(Candle(2)), true, false); control.Update(Native(Candle(2)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 25).Select(i => Native(Candle(i % 3 - 1)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
    [Fact]
    public void CallbackReceivesUndifferencedMomentumAverage()
    {
        var bars = new[] { 1d, 3, 2, 6 }.Select(v => Candle(v)).ToArray(); var expectedInput = new[] { 0d, 1, .5, 1.5 }; var output = new[] { 1d, 0, 1e-20, 0 };
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; using var armed = ComponentAverage.Arm((values, period) => { calls++; Assert.Equal(8, period); Assert.Equal(expectedInput, values); return output; });
            using var expectedWindow = new RocketRsiWindow(Kinds[0], 2, 8, 1, true); var expected = output.Select(v => expectedWindow.Finish(v, true)).ToArray();
            if (fast) { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersRocketRsiFast(Data(bars), context, 2); Assert.Equal(expected, result.ToArray()); }
            else Assert.Equal(expected, Data(bars).CalculateEhlersRocketRelativeStrengthIndex(length1: 2).CustomValuesList);
            Assert.Equal(1, calls);
        }
    }
    [Fact]
    public void OtherAverageKindsKeepTheirConfiguredSmoother()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray(); var batch = Data(bars).CalculateEhlersRocketRelativeStrengthIndex(MovingAvgType.SimpleMovingAverage, 3);
        using var state = new EhlersRocketRelativeStrengthIndexState(MovingAvgType.SimpleMovingAverage, 3); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersRocketRsiFast(Data(bars), context, 3, maType: MovingAvgType.SimpleMovingAverage); Equal(batch.CustomValuesList.ToArray(), fast.ToArray());
        foreach (var pair in bars.Select((bar, i) => (bar, i))) Assert.True(Budget.Accepts(batch.CustomValuesList[pair.i], state.Update(Native(pair.bar), true, true).Value));
    }
    [Fact]
    public void MultiplierAndThresholdMustBeFinite()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersRocketRelativeStrengthIndexState(mult: bad)); Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersRocketRelativeStrengthIndexState(obosLevel: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersRocketRelativeStrengthIndex(mult: bad)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersRocketRelativeStrengthIndex(obosLevel: bad));
        }
        foreach (var mult in new[] { -2d, 0, 2d }) Check(Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray(), 3, mult: mult);
    }
}
