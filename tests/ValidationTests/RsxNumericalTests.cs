using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RsxNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static Bar[] Prices(IEnumerable<double> values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static OhlcvBar Native(Bar b) => new("RSX", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(JmaRsxClone)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRoundedCascade(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RsxOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static double[] Check(Bar[] bars, int length = 14)
    {
        var expected = BuiltInFormulaReferences.RsxValues(bars, length); var line = expected.Outputs["Rsx"];
        var batch = Data(bars).CalculateJmaRsxClone(length); Assert.Equal(line, batch.ChainedValues);
        Assert.Equal(line, batch.OutputValues["Rsx"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeJmaRsxCloneFast(Data(bars), context, length); Assert.Equal(line, fast.ToArray());
        var core = new double[bars.Length]; OscillatorCore.JmaRsxClone(bars.Select(b => b.Close).ToArray(), core, length); Assert.Equal(line, core);
        var native = new JmaRsxCloneState(length); var window = new RsxWindow(length);
        for (var pass = 0; pass < 2; pass++)
        {
            native.Update(Native(Prices(new[] { 8d })[0]), true, false); window.Next(-4, true); native.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Prices(new[] { -9d })[0]), false, false); window.Next(99, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final);
                    Assert.Equal(line[i], point.Value); Assert.Equal(line[i], point.Outputs!["Rsx"]); Assert.Equal(line[i], direct.Value); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        Assert.All(line, v => Assert.InRange(v, 0, 100)); return line;
    }
    [Fact]
    public void UnitPeriodHasFiveNeutralBarsThenKnownDirections()
    {
        Assert.Equal(new[] { 50d, 50, 50, 50, 50, 100, 0, 50 }, Check(Prices(new[] { 2d, 4, 2, 3, 4, 5, 4, 4 }), 1));
        Assert.All(Check(Prices(new double[12]), 4), v => Assert.Equal(50, v));
    }
    [Fact]
    public void DyadicImpulseWeightsGiveHandCalculatedRatio()
    {
        // At length4, H has gain1/2. (1.5H-.5H^2)^3 has weights
        // w0=125/512 and w5=63/2048. Opposite impulses give6300/563.
        var line = Check(Prices(new[] { 1d, 1, 1, 1, 1, 0 }), 4);
        Assert.Equal(6300d / 563, line[5]); Assert.All(line.Take(5), v => Assert.Equal(50, v));
    }
    [Fact]
    public void LegacyCounterReducesToFiveNeutralObservations()
    {
        foreach (var length in new[] { 1, 2, 5, 6, 14, 31, int.MaxValue })
        {
            long oldThreshold = 0, oldCounter = 0;
            for (var i = 0; i < 20; i++)
            {
                var counter = oldCounter == 0 ? 1 : oldThreshold <= oldCounter ? oldThreshold + 1 : oldCounter + 1;
                var threshold = oldCounter == 0 && length - 1 >= 5 ? length - 1 : 5;
                foreach (var changed in new[] { false, true })
                {
                    var active = threshold >= counter && changed;
                    var effective = threshold == counter && !active ? 0 : counter;
                    Assert.Equal(i >= 5, threshold < effective);
                }
                oldCounter = counter; oldThreshold = threshold;
            }
            Check(Prices(Enumerable.Range(0, 20).Select(i => (double)(i % 5 - 2))), length);
        }
    }
    [Fact]
    public void WideScaledPricesAndSubnormalChangesKeepBoundedRatios()
    {
        var values = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0, 1, double.MaxValue, double.Epsilon, 2 * double.Epsilon, 0 };
        foreach (var length in new[] { 1, 2, 4, 14, 31 }) Check(Prices(values), length);
        Check(Prices(Enumerable.Range(0, 40).Select(i => (i % 7 - 3) * double.Epsilon)), 4);
        Check(Prices(Enumerable.Range(0, 40).Select(i => i % 3 == 0 ? Math.BitIncrement(double.MaxValue / 2) : double.MaxValue / 2)), 4);
        Assert.Equal(100, Check(Prices(values), 1)[8]);
    }
    [Fact]
    public void PeriodNormalizationAndMaximumPeriodUseFinitePositiveGain()
    {
        var bars = Prices(new[] { 2d, -4, 1, 7, 8, -2, 0, 3 });
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Array.Empty<Bar>(), length); Check(bars, length); }
        Assert.Equal(Check(bars, 1), Check(bars, int.MinValue)); Assert.Equal(Check(bars, 1), Check(bars, 0));
    }
    [Fact]
    public void SelectedInputAndNoAverageCallbacksRemainExplicit()
    {
        var bars = Prices(Enumerable.Range(0, 20).Select(i => (double)i));
        var selected = Enumerable.Range(0, 20).Select(i => (double)(i % 7 - 3)).ToArray();
        var expected = BuiltInFormulaReferences.RsxValues(Prices(selected), 4).Outputs["Rsx"];
        using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("RSX has fixed recursive stages."));
        var data = Data(bars); data.SetCustomValues(selected.ToList()); Assert.Equal(expected, data.CalculateJmaRsxClone(4).ChainedValues);
        var raw = Data(bars); raw.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeJmaRsxCloneFast(raw, context, 4);
        Assert.Equal(expected, fast.ToArray()); Assert.Equal(selected, raw.ChainedValues); Assert.Equal(0, ComponentAverage.Requests);
    }
    [Fact]
    public void RejectedCandlesCannotAdvanceRecursiveStages()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            var state = new JmaRsxCloneState(); var control = new JmaRsxCloneState();
            foreach (var b in Prices(new[] { 1d, -2, 3, 8, 9, -1 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 2d, 4, -1, 2, 1 }; values[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Prices(new[] { -4d, 3, 9, 0 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
    [Fact]
    public void LongHistoryRetainsAllTwelveStages()
    {
        var bars = Prices(Enumerable.Range(0, 257).Select(i => (double)(i * 17 % 23 - 11)));
        foreach (var period in new[] { 2, 4, 14, 31 }) Check(bars, period);
    }
    [Fact]
    public void CoreSpanBoundsAndExactInPlaceReplay()
    {
        var prices = new[] { double.MaxValue, 0, -double.MaxValue, 1, 3, -4, 5, 1, 0 }; var output = Enumerable.Repeat(999d, prices.Length + 2).ToArray();
        Assert.Throws<ArgumentException>(() => OscillatorCore.JmaRsxClone(prices, output.AsSpan(0, 2), 4)); Assert.All(output, v => Assert.Equal(999, v));
        OscillatorCore.JmaRsxClone(prices, output, 4); var expected = BuiltInFormulaReferences.RsxValues(Prices(prices), 4).Outputs["Rsx"];
        Assert.Equal(expected, output.Take(prices.Length)); Assert.Equal(999, output[^1]);
        var replay = prices.ToArray(); OscillatorCore.JmaRsxClone(replay, replay, 4); Assert.Equal(expected, replay);
        OscillatorCore.JmaRsxClone(Array.Empty<double>(), Array.Empty<double>());
    }
}
