using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HawkeyeNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(HawkeyeVolumeIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentThresholdFractions(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.HawkeyeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, double divisor = 3.6)
    {
        var expected = BuiltInFormulaReferences.HawkeyeValues(bars, length, divisor);
        var batch = Data(bars).CalculateHawkeyeVolumeIndicator(length, divisor); Assert.Empty(batch.CustomValuesList);
        Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Up", "Dn" })
        {
            Assert.Equal(expected.Outputs[key], batch.OutputValues[key]);
            using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeHawkeyeVolumeFast(Data(bars), context, length, divisor, key == "Dn"); Assert.Equal(expected.Outputs[key], fast.ToArray());
        }
        var state = new HawkeyeVolumeIndicatorState(length, divisor); var window = new HawkeyeWindow(length, divisor);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candles((9, -3, 2))[0]), true, false); window.Next(3, 9, -3, 2, 1, true); state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, -999, 0))[0]), false, false); window.Next(0, 999, -999, 0, 999, false);
                var b = bars[i]; var midpoint = ((ReferenceFraction.FromDouble(b.High) + ReferenceFraction.FromDouble(b.Low)) / new ReferenceFraction(2)).ToDouble();
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(b), final, true); var direct = window.Next(midpoint, b.High, b.Low, b.Close, b.Volume, final);
                    Assert.Equal(expected.Outputs["Up"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Up"]); Assert.Equal(expected.Outputs["Dn"][i], point.Outputs["Dn"]);
                    Assert.Equal(point.Value, direct.Up); Assert.Equal(point.Outputs["Dn"], direct.Down); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandLevelsHavePreviousBarTimingAndSignedDivisors()
    {
        var bars = Candles((3, 1, 2), (8, 2, 5), (2, -4, -1), (7, 1, 4));
        var positive = Check(bars, 2, 2); Assert.Equal(new[] { 0d, 3, 8, 2 }, positive.Outputs["Up"]); Assert.Equal(new[] { 0d, 1, 2, -4 }, positive.Outputs["Dn"]);
        var negative = Check(bars, 2, -2); Assert.Equal(positive.Outputs["Up"], negative.Outputs["Dn"]); Assert.Equal(positive.Outputs["Dn"], negative.Outputs["Up"]);
        var zero = Check(bars, 2, 0); Assert.Equal(new[] { 0d, 2, 5, -1 }, zero.Outputs["Up"]); Assert.Equal(zero.Outputs["Up"], zero.Outputs["Dn"]);
        Assert.Equal(new[] { Signal.Buy, Signal.Buy, Signal.Sell, Signal.Buy }, positive.Signals);
    }
    [Fact]
    public void CompleteThresholdRecoversAfterOverflowAndKeepsSubnormalRanges()
    {
        var m = double.MaxValue;
        var wide = Check(Candles((m, -m, 0), (0, -m, -m / 2), (m, 0, m / 2), (2, 0, 1)), 2, .5);
        Assert.True(double.IsPositiveInfinity(wide.Outputs["Up"][1])); Assert.True(double.IsNegativeInfinity(wide.Outputs["Dn"][1]));
        var recovered = Check(Candles((0, -m, -m / 2), (2, 0, 1)), 2, .75);
        Assert.True(double.IsFinite(recovered.Outputs["Up"][1]));
        foreach (var divisor in new[] { double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1d, 3.6 })
            Check(Candles((3 * double.Epsilon, double.Epsilon, 2 * double.Epsilon), (double.Epsilon, -double.Epsilon, 0), (3, 1, 2), (5, -7, -1)), 3, divisor);
    }
    [Fact]
    public void RollingClassificationUsesExactWidePartialMeans()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, Math.ScaleB(1d, 1019) })
        {
            var bars = new[] { (10d, 0d, 5d, 10d), (11d, 10d, 10d, 1d), (0d, -1d, -1d, 20d), (12d, -2d, 4d, 30d), (3d, 1d, 2d, 2d), (2d, 0d, 1d, 1d) }
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Item3 * scale, v.Item1 * scale, v.Item2 * scale, v.Item3 * scale, v.Item4 * scale)).ToArray();
            foreach (var period in new[] { 1, 2, 3, 9 }) Check(bars, period, -3.6);
        }
        Check(Enumerable.Range(0, 25).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, 0, i % 2 == 0 ? double.MaxValue : -double.MaxValue)).ToArray(), 4, 2);
    }
    [Fact]
    public void HandClassificationDistinguishesRollingVolumeAndRangeBranches()
    {
        Bar[] Series(double high, double low, double close, double volume) => new[]
        {
            new Bar(DateTime.UnixEpoch, 0, 100, -100, 0, 10),
            new Bar(DateTime.UnixEpoch, 5, 10, 0, 5, 10),
            new Bar(DateTime.UnixEpoch, close, high, low, close, volume)
        };
        Assert.Equal(Signal.Buy, Check(Series(11, 4, 4, 1), 3, 2).Signals[2]);
        Assert.Equal(Signal.Sell, Check(Series(11, 4, 4, 1), 2, 2).Signals[2]);
        Assert.Equal(Signal.Sell, Check(Series(11, 4, 4, 10), 3, 2).Signals[2]);
        Assert.Equal(Signal.Buy, Check(Series(1, -1, 0, 20), 3, 2).Signals[2]);
        Assert.Equal(Signal.Sell, Check(Series(1, -1, 0, 10), 3, 2).Signals[2]);
        var falling = new[] { new Bar(DateTime.UnixEpoch, 5, 10, 0, 5, 1), new Bar(DateTime.UnixEpoch, 0, 20, -20, 0, 10) };
        Assert.Equal(Signal.Buy, Check(falling, 2, -1).Signals[1]);
        Assert.Equal(Signal.Sell, Check(falling, 2, -2).Signals[1]);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistory()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<Bar>(), length); Check(Candles((4, 0, 2), (9, 1, 5), (1, -7, -3), (6, 2, 4)), length); }
    }
    [Fact]
    public void SelectedPricesUseCausalOutsideRangesOnBothOutputs()
    {
        var bars = Candles((9, 1, 4), (6, 2, 4), (8, 0, 3), (5, 1, 2)); var selected = new[] { 20d, 3, -4, 2 };
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.HawkeyeValues(projected, 2, 2, true);
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateHawkeyeVolumeIndicator(2, 2);
        Assert.Equal(new[] { 0d, 20, 5, -.5 }, expected.Outputs["Up"]); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Up", "Dn" })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeHawkeyeVolumeFast(data, context, 2, 2, key == "Dn");
            Assert.Equal(expected.Outputs[key], fast.ToArray()); Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(selected, data.ChainedValues);
        }
    }
    [Fact]
    public void NoAverageCallbacksAreConsumed()
    {
        var calls = 0; using var armed = ComponentAverage.Arm((values, _) => { calls++; return values; });
        Check(Candles((3, 1, 2), (7, -1, 4), (5, -3, 0)), 2, 2); Assert.Equal(0, calls);
    }
    [Fact]
    public void NonfiniteDivisorsAreRejectedEvenForEmptyInput()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HawkeyeVolumeIndicatorState(2, bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => ((IBuiltInIndicator)new HawkeyeVolumeIndicator(2, bad)).CreateOptions());
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateHawkeyeVolumeIndicator(2, bad));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeHawkeyeVolumeFast(Data(Array.Empty<Bar>()), context, 2, bad));
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceEitherThreshold()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            var state = new HawkeyeVolumeIndicatorState(2, 2); var control = new HawkeyeVolumeIndicatorState(2, 2);
            foreach (var b in Candles((7, -1, 2), (4, 0, 1))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 2d, 4, 0, 2, 1 }; values[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((9, -3, 4), (1, -5, -2))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Dn"], actual.Outputs!["Dn"]); }
        }
    }
}
