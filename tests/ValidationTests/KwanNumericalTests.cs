using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KwanNumericalTests
{
    private static Bar[] Bars(params double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 1, p - 1, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KWAN", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KwanIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRatios(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KwanOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesCandles(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        // Builder/live routing alone does not exercise the explicit fast method.
        var indicator = (IBuiltInIndicator)c.Factory();
        var options = (OoplesFinance.StockIndicators.Builder.Specs.KwanIndicatorSpecOptions)indicator.CreateOptions();
        var bars = Enumerable.Range(0, Math.Max(64, c.Factory().WarmupBars + 8)).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 4, 15 + i % 7, -5 - i % 3, 30 - i % 7, i % 4)).ToArray();
        var selected = bars.Select((_, i) => 2d + i % 11).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.KwanOutputs(projected, indicator)["Ki"];
        Assert.Contains(expected, value => value != 0);
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeKwanIndicatorFast(data, context, options.Length, options.SmoothLength, options.MaType);
        Assert.Equal(expected, actual.ToArray());
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static double[] Check(Bar[] bars, int length, int delay, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod, int reference = 6)
    {
        var expected = BuiltInFormulaReferences.KwanValues(bars, length, delay, reference);
        var batch = Data(bars).CalculateKwanIndicator(kind, length, delay);
        Assert.Equal(expected.Outputs["Ki"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeKwanIndicatorFast(Data(bars), context, length, delay, kind);
        Assert.Equal(expected.Outputs["Ki"], fast.ToArray());
        using var native = new KwanIndicatorState(kind, length, delay); using var window = new KwanWindow(kind, length, delay);
        for (var pass = 0; pass < 2; pass++)
        {
            native.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(999)[0]), false, false); window.Next(1000, 998, 999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var p = native.Update(Native(bars[i]), final, true); var v = window.Next(bars[i].High, bars[i].Low, bars[i].Close, final);
                    Assert.Equal(expected.Outputs["Ki"][i], p.Value); Assert.Equal(p.Value, p.Outputs!["Ki"]);
                    Assert.Equal(p.Value, v.Value); Assert.Equal(expected.Signals[i], v.Trade);
                }
            }
        }
        return expected.Outputs["Ki"];
    }
    [Fact]
    public void HandDelayedIntegralAndSignedRatios()
    {
        Assert.Equal(new[] { 0d, 0, 25 }, Check(Bars(2, 4, 6), 1, 1));
        Assert.Equal(new[] { 0d, 0, -25 }, Check(Bars(-2, 4, 6), 1, 1));
        var data = Bars(2, 4, 6, 8, 10); var values = Check(data, 1, 2);
        Assert.Equal(0, values[2]); Assert.Equal(12.5, values[3]); Assert.Equal((25 + 100d / 3) / 2, values[4]);
        foreach (var kind in Kinds)
        {
            Check(Bars(2, 4, 4, 4, -2, 0, 3, 1, 6), 3, 2, kind.Kind, kind.Reference);
            Check(Bars(2, 4, 1, 1, 1, 3, 4), 3, 1, kind.Kind, kind.Reference);
            var ranges = new[] { (8d, 0d, 4d), (12d, -4d, 6d), (5d, 1d, 3d), (6d, 2d, 4d), (7d, 3d, 5d) };
            Check(ranges.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Item3, v.Item1, v.Item2, v.Item3, 1)).ToArray(), 3, 1, kind.Kind, kind.Reference);
        }
    }
    [Fact]
    public void CompleteQuotientsSurviveOverflowAndSubnormalPrices()
    {
        var m = double.MaxValue; var e = double.Epsilon;
        foreach (var prices in new[] { new[] { -m, m, m / 2, -m / 4, 0d, 1, -1, 2 }, new[] { e, 2 * e, -e, 0d, 3 * e, -2 * e, e }, new[] { -m, e, -e, 1d, -1, 2, 0, 1 } })
        {
            var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, m, -m, p, 1)).ToArray();
            foreach (var kind in Kinds) Check(bars, 2, 1, kind.Kind, kind.Reference);
        }
    }
    [Fact]
    public void ExtremePeriodsStoreOnlyObservedHistory()
    {
        foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Bars(2, 4, 1, 0), period, 2, kind.Kind, kind.Reference); Check(Bars(2, 4, 1, 0), 2, period, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), period, period, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void FlatRangeAndZeroPricesHaveExplicitZeroRatios()
    {
        foreach (var kind in Kinds)
        {
            Assert.All(Check(Bars(0, 0, 0, 0), 1, 1, kind.Kind, kind.Reference), v => Assert.Equal(0, v));
            var bars = Bars(1, 2, 3, 4).Select(b => new Bar(b.Time, b.Close, b.Close, b.Close, b.Close, 1)).ToArray();
            Assert.All(Check(bars, 1, 1, kind.Kind, kind.Reference), v => Assert.Equal(0, v));
        }
    }
    [Fact]
    public void CustomGainAndLossSlotsRetainInputsAndPeriods()
    {
        var requests = 0; var bars = Bars(2, 4, 3, 8); var expected = new[] { new[] { 0d, 2, 0, 5 }, new[] { 0d, 0, 1, 0 } };
        using var armed = ComponentAverage.Arm(Enumerable.Range(0, 2).Select(slot => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((v, period) => { requests++; Assert.Equal(1, period); Assert.Equal(expected[slot], v); return Enumerable.Repeat(slot == 0 ? 1d : 3d, v.Count).ToArray(); })).ToArray());
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeKwanIndicatorFast(Data(bars), context, 1, 1);
        Assert.Equal(new[] { 0d, 0, 6.25, 6.25 + 50d / 3 }, output.ToArray()); Assert.Equal(2, requests);
    }
    [Fact]
    public void LegacyKindsKeepRouteParity()
    {
        var bars = Bars(2, 4, 1, 6, 3, 8, 5);
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateKwanIndicator(kind, 2, 2); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeKwanIndicatorFast(Data(bars), context, 2, 2, kind);
            Assert.Equal(batch.CustomValuesList, output.ToArray()); using var state = new KwanIndicatorState(kind, 2, 2);
            for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, false).Value);
        }
    }
    [Fact]
    public void ExtendedDelayedTermsCancelAfterPublishedOverflow()
    {
        var m = double.MaxValue;
        var triples = new[] { (m, m, m), (2d, 0d, 1d), (0d, -2d, -1d), (0d, -m, -m), (2d, 0d, 1d), (2d, 0d, 1d), (2d, 0d, 1d) };
        var bars = triples.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Item3, v.Item1, v.Item2, v.Item3, 1)).ToArray();
        var expected = new[] { 0d, 0, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, -50, 0 };
        using var window = new KwanWindow(MovingAvgType.WildersSmoothingMethod, 1, 1);
        for (var i = 0; i < bars.Length; i++)
        {
            var p = window.Next(bars[i].High, bars[i].Low, bars[i].Close, true, 100);
            Assert.Equal(expected[i], p.Value);
            if (i == 3) Assert.Equal(Signal.StrongSell, p.Trade);
        }
        using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[]
        { (v, _) => Enumerable.Repeat(1d, v.Count).ToArray(), (v, _) => Enumerable.Repeat(0d, v.Count).ToArray() });
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeKwanIndicatorFast(Data(bars), context, 1, 1);
        Assert.Equal(expected, output.ToArray());
    }
    [Fact]
    public void InvalidBarsCannotAdvanceAnyState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new KwanIndicatorState(length: 2); using var control = new KwanIndicatorState(length: 2);
            foreach (var b in Bars(2, 4, 1)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 2d, 3, 1, 2, 1 }; values[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Bars(3, 6, 0, 1)) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
