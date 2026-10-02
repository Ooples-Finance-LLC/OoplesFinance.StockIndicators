using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeWeightedRsiNumericalTests
{
    private static Bar B(double close, double volume = 1, int i = 0)
        => new(DateTime.UnixEpoch.AddMinutes(i), close, close, close, close, volume);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VWRSI", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolumeWeightedRsi)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentFlowRatio(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VolumeWeightedRsiOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedPricesRetainOriginalVolumes(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var bars = Enumerable.Range(0, 32).Select(i => B(100 + i, i % 5 - 2, i)).ToArray();
        var selected = bars.Select((_, i) => (double)(i * 7 % 13 - 6)).ToArray();
        var expected = BuiltInFormulaReferences.VolumeWeightedRsiValues(bars.Select((b, i) => B(selected[i], b.Volume, i)).ToArray(), length);
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVolumeWeightedRsiFast(data, context, length);
        Assert.Equal(expected, fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
        data.CalculateVolumeWeightedRelativeStrengthIndex(length: length);
        Assert.Equal(expected, data.CustomValuesList); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        Assert.Equal(bars.Select(b => b.Volume), data.Volumes);
    }
    private static void Equal(double[] expected, double[] actual, bool exact)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(double.IsFinite(actual[i]));
            if (exact) Assert.Equal(expected[i], actual[i]);
            else Assert.True(new IndicatorErrorBudget(0, 2e-14, true).Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}");
        }
    }
    private static double[] Check(Bar[] bars, int length = 2, int smooth = 1, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2
            : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.VolumeWeightedRsiValues(bars, length, smooth, code);
        var exact = true;
        var batch = Data(bars).CalculateVolumeWeightedRelativeStrengthIndex(kind, length, smooth);
        Equal(expected, batch.CustomValuesList.ToArray(), exact); Assert.Equal(batch.CustomValuesList, batch.OutputValues["Vwrsi"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVolumeWeightedRsiFast(Data(bars), context, length, smooth, kind);
        Equal(expected, fast.ToArray(), exact);
        using var state = new VolumeWeightedRelativeStrengthIndexState(kind, length, smooth);
        for (var cycle = 0; cycle < 2; cycle++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                // A different preview must not become the previous finalized price or enter any mean.
                _ = state.Update(Native(B(17, -3)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    Equal(new[] { expected[i] }, new[] { point.Value }, exact);
                    Assert.Equal(point.Value, point.Outputs!["Vwrsi"]);
                }
                var slope = expected[i] - (i > 0 ? expected[i - 1] : 0);
                var previousSlope = (i > 0 ? expected[i - 1] : 0) - (i > 1 ? expected[i - 2] : 0);
                var trade = slope > 0 && slope > previousSlope ? Signal.StrongBuy : slope < 0 && slope < previousSlope ? Signal.StrongSell
                    : slope > 0 ? Signal.Buy : slope < 0 ? Signal.Sell : Signal.None;
                Assert.Equal(trade, batch.SignalsList[i]);
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentHandsIncludeWeightedWarmupAndFinalSmoothing()
    {
        Assert.Equal(new[] { 100d, 100, -60 }, Check(new[] { B(0), B(1, 2), B(0, 4) }));
        Assert.Equal(new[] { 50d, 250d / 3, 20 }, Check(new[] { B(0), B(1, 2), B(0, 4) }, smooth: 3));
        Assert.Equal(new[] { 100d, 100, 100 }, Check(new[] { B(0), B(0), B(0) }));
        Assert.Equal(new[] { 100d, -100, 100 }, Check(new[] { B(0), B(1, -2), B(0, -4) }, length: 1));
        Assert.Equal(new[] { 100d, 100, -60 }, Check(new[] { B(0), B(double.Epsilon, 2 * double.Epsilon), B(0, 4 * double.Epsilon) }));
        Assert.Equal(new[] { 100d, -100, 100d / 3 }, Check(new[] { B(double.MaxValue), B(-double.MaxValue, double.MaxValue), B(double.MaxValue, double.MaxValue) }));
    }
    [Fact]
    public void TinyImbalancesSurviveEachStandardMean()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var volume = kind == MovingAvgType.ExponentialMovingAverage ? .25
                : kind == MovingAvgType.SimpleMovingAverage ? 1 : .5;
            // Centered result is 100*epsilon/(2-epsilon), rounded to 50*epsilon.
            Assert.Equal(new[] { 100d, 100, 50 * double.Epsilon },
                Check(new[] { B(0), B(1), B(double.Epsilon, volume) }, 2, 1, kind));
        }
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void StandardMeansKeepExtremeProductsAndZeroVolume(MovingAvgType kind)
    {
        Check(Enumerable.Range(0, 40).Select(i => B((i * 7 % 13 - 6) * (double.MaxValue / 8),
            i % 7 == 0 ? 0 : i % 2 == 0 ? double.MaxValue : -double.MaxValue, i)).ToArray(), 3, 4, kind);
        Check(new[] { B(0), B(double.Epsilon, double.Epsilon), B(0, 2 * double.Epsilon), B(double.Epsilon, double.Epsilon) }, 3, 2, kind);
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsAllocateOnlyObservedHistory(int length)
    {
        Check(Array.Empty<Bar>(), length, length);
        Check(new[] { B(0), B(1), B(0), B(2) }, length, length);
    }
    [Fact]
    public void CallbackSlotsUseGainLossAndCenteredInputsAndPadShortResults()
    {
        foreach (var fast in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 0d, 2, 0 }, values); return new[] { 0d, 3 }; },
                (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 0d, 0, 4 }, values); return new[] { 0d, 1, 2 }; },
                (values, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 100d, 50, -100 }, values); return new[] { 7d }; }
            });
            var data = Data(new[] { B(0), B(1, 2), B(0, 4) });
            using var context = new ComputeContext();
            if (fast) { using var result = IndicatorCompute.ComputeVolumeWeightedRsiFast(data, context, 2); Assert.Equal(new[] { 7d, 0, 0 }, result.ToArray()); }
            else Assert.Equal(new[] { 7d, 0, 0 }, data.CalculateVolumeWeightedRelativeStrengthIndex(length: 2).CustomValuesList);
            Assert.Equal(3, ComponentAverage.Requests); Assert.Equal(3, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void CoreUsesPublicSmoothingAndValidatesSpansBeforeWriting()
    {
        var bars = new[] { B(0), B(1, 2), B(0, 4) }; var close = bars.Select(b => b.Close).ToArray(); var volume = bars.Select(b => b.Volume).ToArray();
        var output = new[] { -7d, -7, -7, -7, -7 };
        VolumeCore.VolumeWeightedRsi(close, volume, output.AsSpan(1, 3), 2);
        Assert.Equal(new[] { -7d, 50, 250d / 3, 20, -7 }, output);
        VolumeCore.VolumeWeightedRsi(close, volume, close, 2); Assert.Equal(output.Skip(1).Take(3), close);
        VolumeCore.VolumeWeightedRsi(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        var guard = new[] { 123d, 456 };
        Assert.Throws<ArgumentException>(() => VolumeCore.VolumeWeightedRsi(new[] { 1d, 2 }, new[] { 1d }, guard));
        Assert.Throws<ArgumentException>(() => VolumeCore.VolumeWeightedRsi(new[] { 1d, 2 }, new[] { 1d, 1 }, new double[1]));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => VolumeCore.VolumeWeightedRsi(new[] { 1d, bad }, new[] { 1d, 1 }, guard));
            Assert.ThrowsAny<ArgumentException>(() => VolumeCore.VolumeWeightedRsi(new[] { 1d, 2 }, new[] { 1d, bad }, guard));
            Assert.Equal(new[] { 123d, 456 }, guard);
        }
    }
    [Fact]
    public void InvalidNativeAndSelectedOriginalInputsCannotMutateState()
    {
        using var state = new VolumeWeightedRelativeStrengthIndexState(length: 1, smoothLength: 1);
        Assert.Equal(100, state.Update(Native(B(1)), true, false).Value);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(B(0, bad)), true, false));
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, 0, bad, 0, 0, 1)), true, false));
            var data = Data(new[] { B(bad) }); data.SetCustomValues(new List<double> { 20 });
            Assert.ThrowsAny<ArgumentException>(() => data.CalculateVolumeWeightedRelativeStrengthIndex());
            using var context = new ComputeContext();
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVolumeWeightedRsiFast(data, context));
        }
        Assert.Equal(-100, state.Update(Native(B(0)), true, false).Value);
    }
}
