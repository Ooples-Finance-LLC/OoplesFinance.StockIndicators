using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChopZoneNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ChopZone)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeLogarithm(IndicatorValidationCase c, string route)
    {
        var options = (ChopZoneSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Cz", BuiltInFormulaReferences.ChopZoneOutputs(bars, options.Length, kind: options.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 3) } }, Budget);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length = 5, int smoothing = 3, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, bool selected = false)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.ChopZoneOutputs(bars, length, smoothing, k, selected);
        StockData Input() { var data = Data(bars); if (selected) data.SetCustomValues(bars.Select(b => b.Close).ToList()); return data; }
        Equal(expected, Input().CalculateChopZone(kind, length, smoothing).OutputValues["Cz"].ToArray());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeChopZoneFast(Input(), context, length, kind, smoothing); Equal(expected, raw.ToArray());
        using var state = new ChopZoneState(kind, length, smoothing); if (selected) ((ICustomInputConsumer)state).ReadCloseAsInput();
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue, double.MaxValue, -double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue, double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Cz"]); }
            }
        }
    }
    [Fact]
    public void HandSignedAnglesRoundToWholeDegrees()
    {
        var positive = Enumerable.Repeat(Candle(1.5, 2, 1), 4).ToArray(); var negative = Enumerable.Repeat(Candle(-1.5, -1, -2), 4).ToArray();
        Assert.Equal(new[] { 88d, 0, 0, 0 }, BuiltInFormulaReferences.ChopZoneOutputs(positive, 2, 1));
        Assert.Equal(new[] { -89d, 0, 0, 0 }, BuiltInFormulaReferences.ChopZoneOutputs(negative, 2, 1));
        Check(positive, 2, 1); Check(negative, 2, 1); Check(Enumerable.Repeat(Candle(0, 1, -1), 8).ToArray());
        Check(Enumerable.Repeat(Candle(1, 1, 1), 8).ToArray());
    }
    [Fact]
    public void ExtremeNormalizedSlopesAndAveragesRecover()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 5 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
            {
                Check(Enumerable.Range(0, 48).Select(i => Candle((i % 3 - 1) * scale, scale, -scale)).ToArray(), length, 3, kind);
                Check(Enumerable.Range(0, 48).Select(i => Candle(scale, scale, i % 2 == 0 ? scale : 0)).ToArray(), length, 3, kind);
            }
            Check(Enumerable.Range(0, 48).Select(i => Candle(.5 + .4 * Math.Sin(i * .37), .9, .1)).ToArray(), length, 3, kind);
            Check(Array.Empty<Bar>(), length, 3, kind);
        }
        var recovery = Enumerable.Range(0, 40).Select(i => i < 3 ? Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue, 2, 1) : Candle(1.5, 2, 1)).ToArray();
        Check(recovery, 2, 1, selected: true); Assert.Equal(0, BuiltInFormulaReferences.ChopZoneOutputs(recovery, 2, 1, selected: true)[^1]);
        Check(new[] { Candle(double.Epsilon, 2, 1), Candle(double.MaxValue, 2, 1), Candle(-double.Epsilon, 2, 1) }, 2, 1, selected: true);
    }
    [Fact]
    public void SelectedClosesReplaceTypicalPriceButPreserveOriginalExtrema()
    {
        var bars = Enumerable.Range(0, 48).Select(i => Candle(i % 2 == 0 ? 9 : -3, 2 + i % 3, 1)).ToArray(); Check(bars, 2, selected: true);
        Assert.NotEqual(BuiltInFormulaReferences.ChopZoneOutputs(bars, 2), BuiltInFormulaReferences.ChopZoneOutputs(bars, 2, selected: true));
        var original = bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 0, b.Volume)).ToArray(); var data = Data(original); data.SetCustomValues(bars.Select(b => b.Close).ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.ChopZone, new ChopZoneSpecOptions(2)), context);
        Assert.NotNull(raw); Equal(BuiltInFormulaReferences.ChopZoneOutputs(bars, 2, selected: true), raw.Value.ToArray());
    }
    [Fact]
    public void CustomerAverageReceivesCloseAndItsOwnSmoothingPeriod()
    {
        var bars = Enumerable.Range(0, 9).Select(i => Candle(i + 1, 4, 1)).ToArray(); var external = Enumerable.Repeat(0d, bars.Length).ToArray();
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (input, period) => { Assert.Equal(3, period); Assert.Equal(bars.Select(b => b.Close), input); return external; } });
            using var context = new ComputeContext();
            if (batch) Assert.All(Data(bars).CalculateChopZone(length1: 2, length2: 3).OutputValues["Cz"], value => Assert.Equal(0, value));
            else { using var raw = IndicatorCompute.ComputeChopZoneFast(Data(bars), context, 2, length2: 3); Assert.All(raw.ToArray(), value => Assert.Equal(0, value)); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedRejectionContractChecksStateAfterRejectedBars(bool minimumPeriods) =>
        new StreamingTests.DiscoveredNativeInputRejectionTests().EveryDiscoveredNativeStateRejectsInvalidBarsWithoutAdvancing(nameof(ChopZoneState), minimumPeriods);
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new ChopZoneState(); var control = new ChopZoneState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
