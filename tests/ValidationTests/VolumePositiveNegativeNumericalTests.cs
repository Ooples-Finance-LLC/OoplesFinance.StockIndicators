using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolumePositiveNegativeNumericalTests
{
    private static Bar B(double price, double volume = 1, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, volume);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VPN", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolumePositiveNegativeIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void OutputsMatchIndependentRationalReference(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.VolumePositiveNegativeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedPricesRetainVolumesAndRangePolicy(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static StockData Check(Bar[] bars, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int length = 2, int smooth = 2)
    {
        var expected = BuiltInFormulaReferences.VolumePositiveNegativeValues(bars, kind, length, smooth);
        var data = Data(bars).CalculateVolumePositiveNegativeIndicator(kind, length, smooth);
        foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList);
        using var state = new VolumePositiveNegativeIndicatorState(kind, length, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(999, 81)), false, false); var preview = state.Update(Native(bars[i]), false, true); var point = state.Update(Native(bars[i]), true, true);
                foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); Assert.Equal(point.Outputs[key], preview.Outputs![key]); }
            }
        }
        return data;
    }
    [Fact]
    public void IndependentVotesVolumeNormalizationAndSignalHands()
    {
        var prices = new[] { 10d, 20, 10, 10, 30 }; var volume = new[] { 2d, 4, 8, 0, 4 };
        var data = Check(prices.Select((p, i) => B(p, volume[i], i)).ToArray());
        Assert.Equal(new[] { 50d, 100, -600d / 19, -3600d / 19, 5400d / 91 }, data.OutputValues["Vpni"]);
        Assert.Equal(new[] { 50d, 75, 75d / 19, -2375d / 19, -10925d / 5187 }, data.OutputValues["Signal"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongBuy, Signal.Buy, Signal.StrongSell, Signal.Sell }, data.SignalsList);
    }
    [Fact]
    public void ExactAtrThresholdTiesAndSmallestMovesRemainDistinct()
    {
        // HLC3 moves from5 to6 while ATR=10: equality is neither positive nor negative.
        var first = new Bar(DateTime.UnixEpoch, 5, 10, 0, 5, 1);
        var tie = new Bar(DateTime.UnixEpoch.AddMinutes(1), 6, 11, 1, 6, 1);
        Assert.Equal(new[] { 100d, 0 }, Check(new[] { first, tie }, length: 1, smooth: 1).OutputValues["Vpni"]);
        foreach (var price in new[] { Math.BitIncrement(6d), Math.BitDecrement(6d) })
        {
            var next = new Bar(tie.Time, 6, 11, 1, price, 1); var data = Check(new[] { first, next }, length: 1, smooth: 1);
            Assert.Equal(price > 6 ? 100 : 0, data.OutputValues["Vpni"][1]);
        }
        Assert.Equal(new[] { 100d, -100 }, Check(new[] { B(double.Epsilon), B(0) }, length: 1, smooth: 1).OutputValues["Vpni"]);
    }
    [Fact]
    public void TypicalMovementUsesOriginalCloseForTrueRange()
    {
        var first = new Bar(DateTime.UnixEpoch, 0, 12, 0, 0, 1);
        Assert.Equal(new[] { 100d, 0 }, Check(new[] { first, B(4.25) }, length: 1, smooth: 1).OutputValues["Vpni"]);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void ExtremePricesVolumesAndExpiryKeepExactRatios(MovingAvgType kind)
    {
        var prices = new[] { 1d, -1, 0, 1, 1, -1 }; var volumes = new[] { 1d, 1, 0, -1, 1, 1 };
        foreach (var scale in new[] { double.Epsilon, double.MaxValue }) Check(prices.Select((p, i) => B(p * scale, volumes[i] * scale, i)).ToArray(), kind, 3, 2);
        Check(Enumerable.Range(0, 15).Select(i => B(i * 7 % 11 - 5, i % 5, i)).ToArray(), kind, 4, 3);
    }
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ExtremePeriodsUseOnlyObservedSamples(int length)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), kind, length, length); Check(new[] { B(1, 2), B(2, 4), B(-1, 8) }, kind, length, length); }
    }
    [Fact]
    public void ComponentCallbacksKeepVolumeAtrSignalOrderAndPadding()
    {
        var bars = new[] { B(10, 2), B(20, 4), B(10, 8) }; var calls = 0;
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { calls++; Assert.Equal(2, period); Assert.Equal(new[] { 2d, 4, 8 }, values); return new[] { 1d, 2, 4 }; },
            (values, period) => { calls++; Assert.Equal(2, period); Assert.Equal(new[] { 0d, 10, 10 }, values); return new[] { 0d }; },
            (values, period) => { calls++; Assert.Equal(3, period); Assert.Equal(new[] { 100d, 150, -50 }, values); return new[] { 7d }; }
        }))
        {
            var data = Data(bars).CalculateVolumePositiveNegativeIndicator(length: 2, smoothLength: 3);
            Assert.Equal(new[] { 100d, 150, -50 }, data.OutputValues["Vpni"]); Assert.Equal(new[] { 7d, 0, 0 }, data.OutputValues["Signal"]); Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Requests);
        }
    }
    [Fact]
    public void DirectSelectedPricesAndRejectedCandlesPreserveState()
    {
        // Selected first price100 clears range999/10, but not a spurious range1000/10 from a zero previous close.
        var first = new Bar(DateTime.UnixEpoch, 100, 1000, 1, 100, 1);
        var firstData = Data(new[] { first }); firstData.SetCustomValues(new List<double> { 100 });
        firstData.CalculateVolumePositiveNegativeIndicator(length: 1, smoothLength: 1); Assert.Equal(100, firstData.OutputValues["Vpni"][0]);
        using (var selectedState = new CustomInputState(new VolumePositiveNegativeIndicatorState(length: 1, smoothLength: 1), bar => bar.Close))
            Assert.Equal(100, selectedState.Update(Native(first), true, true).Value);
        var bars = new[] { B(100, 2), B(101, 4), B(99, 8) }; var selected = new[] { 1d, -2, 3 };
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.VolumePositiveNegativeValues(projected, length: 2, smooth: 3, selected: true);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); data.CalculateVolumePositiveNegativeIndicator(length: 2, smoothLength: 3);
        foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        using var state = new VolumePositiveNegativeIndicatorState(); using var control = new VolumePositiveNegativeIndicatorState();
        state.Update(Native(B(2)), true, false); control.Update(Native(B(2)), true, false);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            var values = new[] { 1d, 1, 1, 1, 1 }; values[field] = bad; var invalid = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(invalid), final, true)); var batch = Data(new[] { invalid }); batch.SetCustomValues(new List<double> { 1 }); Assert.ThrowsAny<ArgumentException>(() => batch.CalculateVolumePositiveNegativeIndicator());
        }
        foreach (var b in bars) { var a = state.Update(Native(b), true, true); var e = control.Update(Native(b), true, true); Assert.Equal(e.Value, a.Value); Assert.Equal(e.Outputs!["Signal"], a.Outputs!["Signal"]); }
    }
}
