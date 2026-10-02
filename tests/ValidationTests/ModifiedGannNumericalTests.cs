using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ModifiedGannNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static Bar Candle(int i, double high, double low, double open, double close) => new(DateTime.UnixEpoch.AddMinutes(i), open, high, low, close, 1);
    private static OhlcvBar Native(Bar b) => new("GANN", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ModifiedGannHiloActivator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentEnvelopeEvents(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ModifiedGannOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.ModifiedGannBudget);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory();
        var o = (OoplesFinance.StockIndicators.Builder.Specs.ModifiedGannHiloActivatorSpecOptions)indicator.CreateOptions();
        var bars = Enumerable.Range(0, Math.Max(64, c.Factory().WarmupBars + 8)).Select(i => Candle(i, 40, -5, 20, 30)).ToArray();
        var selected = bars.Select((_, i) => i % 2 == 0 ? -20d : 80d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.ModifiedGannOutputs(projected, indicator)["Ghla"];
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeModifiedGannHiloActivatorFast(data, context, o.Length, 1, o.MaType);
        var values = actual.ToArray(); for (var i = 0; i < values.Length; i++) Equal(expected[i], values[i]);
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.Open), data.OpenPrices);
        Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Equal(double expected, double actual)
    {
        Assert.False(double.IsNaN(actual));
        if (expected == 0 || double.IsInfinity(expected)) Assert.Equal(expected, actual);
        else { Assert.Equal(Math.Sign(expected), Math.Sign(actual)); Assert.True(Math.Abs((actual - expected) / expected) <= 4e-15, $"Expected {expected:R}, actual {actual:R}"); }
    }
    private static double[] Check(Bar[] bars, int length, double mult, MovingAvgType kind)
    {
        var referenceKind = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
        var expected = BuiltInFormulaReferences.ModifiedGannValues(bars, length, referenceKind, mult);
        var batch = Data(bars).CalculateModifiedGannHiloActivator(kind, length, mult);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeModifiedGannHiloActivatorFast(Data(bars), context, length, mult, kind);
        var values = fast.ToArray(); var line = expected.Outputs["Ghla"];
        Assert.Equal(expected.Signals, batch.SignalsList);
        using var native = new ModifiedGannHiloActivatorState(kind, length, mult);
        using var direct = new ModifiedGannWindow(kind, length, mult);
        for (var pass = 0; pass < 2; pass++)
        {
            var seed = Candle(0, 9, -9, 2, 3); native.Update(Native(seed), true, false); direct.Next(9, -9, 2, 3, true);
            native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                Equal(line[i], batch.CustomValuesList[i]); Equal(line[i], batch.OutputValues["Ghla"][i]); Equal(line[i], values[i]);
                native.Update(Native(seed), false, false); direct.Next(9, -9, 2, 3, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true);
                    var raw = direct.Next(bars[i].High, bars[i].Low, bars[i].Open, bars[i].Close, final);
                    Equal(line[i], point.Value); Equal(line[i], point.Outputs!["Ghla"]); Equal(line[i], raw.Value); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return line;
    }
    [Fact]
    public void AffineExtensionsCancelOverflowBeforeAveraging()
    {
        var m = double.MaxValue;
        var bars = new[] { Candle(0, m, -m, -m, -m), Candle(1, m, -m, m, m), Candle(2, m, -m, 0, 0), Candle(3, 4, -4, 0, 1) };
        Assert.Equal(m, Check(bars, 1, 1, MovingAvgType.SimpleMovingAverage)[0]);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var mult in new[] { 0d, 1, 2, -1, double.Epsilon, m }) Check(bars, 2, mult, kind);
    }
    [Fact]
    public void TinyCandleDistancesAndRollingExpiryRetainSwitches()
    {
        var prices = new[] { 1d, 4, -2, 3, -4, -1, 7, 2, -3, 0, 5, -2 };
        foreach (var scale in new[] { 1d, double.Epsilon, Math.Pow(2, 1019) })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 5 })
            Check(prices.Select((v, i) => Candle(i, (v + 2) * scale, (v - 2) * scale, (v - 1) * scale, v * scale)).ToArray(), length, .75, kind);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistory()
    {
        var bars = new[] { Candle(0, 3, -1, 0, 1), Candle(1, 6, 2, 3, 4), Candle(2, 1, -4, 0, -2) };
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(bars, length, 1, kind); Check(Array.Empty<Bar>(), length, 1, kind); }
    }
    [Fact]
    public void ComponentOverridesRetainBothEnvelopeSlots()
    {
        var bars = new[] { Candle(0, 3, -1, 1, 1), Candle(1, 6, 2, 4, 4), Candle(2, 1, -4, -2, -2), Candle(3, 5, 1, 3, 3) };
        foreach (var shortOutput in new[] { false, true })
        {
            IReadOnlyList<double>[] custom = { shortOutput ? new[] { 4d, 3 } : new[] { 4d, 3, 0, 6 }, shortOutput ? new[] { -1d, 2 } : new[] { -1d, 2, -3, 1 } };
            using var scope = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[]
            {
                (source, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 3d, 6, 6, 5 }, source); return custom[0]; },
                (source, period) => { Assert.Equal(2, period); Assert.Equal(new[] { -1d, -1, -4, -4 }, source); return custom[1]; }
            });
            using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeModifiedGannHiloActivatorFast(Data(bars), context, 2);
            Assert.Equal(BuiltInFormulaReferences.ModifiedGannValues(bars, 2, 1, 1, custom).Outputs["Ghla"], actual.ToArray());
            Assert.Equal(2, ComponentAverage.Requests); Assert.Equal(2, ComponentAverage.Substitutions); Assert.Equal(2, ComponentAverage.LengthAsked);
        }
        using var discovery = ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>());
        using var discoveryContext = new ComputeContext(); using var defaultOutput = IndicatorCompute.ComputeModifiedGannHiloActivatorFast(Data(bars), discoveryContext, 2);
        Assert.Equal(2, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void ResetClearsTheDirectionBeforeNegativeStartup()
    {
        var bars = new[] { Candle(0, -8, -12, -11, -10), Candle(1, -18, -22, -21, -20) };
        // Rolling highs are -8, -8; their first full-window mean is -8.
        Assert.Equal(new[] { 0d, -8 }, Check(bars, 2, 1, MovingAvgType.SimpleMovingAverage));
    }
    [Fact]
    public void InvalidNativeCandlesCannotAdvanceEnvelopeOrDirection()
    {
        var seed = Candle(0, 3, -1, 0, 1); var next = Candle(1, 6, 2, 3, 4);
        using var actual = new ModifiedGannHiloActivatorState(length: 2); using var control = new ModifiedGannHiloActivatorState(length: 2);
        actual.Update(Native(seed), true, false); control.Update(Native(seed), true, false);
        foreach (var bad in new[] { Candle(1, 6, 2, double.NaN, 4), Candle(1, double.PositiveInfinity, 2, 3, 4), Candle(1, 6, double.NaN, 3, 4), Candle(1, 6, 2, 3, double.NegativeInfinity) })
        foreach (var final in new[] { false, true }) Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(bad), final, false));
        Assert.Equal(control.Update(Native(next), true, true).Value, actual.Update(Native(next), true, true).Value);
        foreach (var mult in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        { Assert.Throws<ArgumentOutOfRangeException>(() => new ModifiedGannHiloActivatorState(mult: mult)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(new[] { seed }).CalculateModifiedGannHiloActivator(mult: mult)); }
    }
}
