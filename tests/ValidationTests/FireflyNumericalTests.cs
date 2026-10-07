using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FireflyNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FireflyOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStandardizationAndSmoothing(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FireflyOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.ZeroLagExponentialMovingAverage, 4), (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, int smooth = 2, MovingAvgType kind = MovingAvgType.ZeroLagExponentialMovingAverage, int reference = 4)
    {
        var expected = BuiltInFormulaReferences.FireflyValues(bars, length, smooth, reference); var batch = Data(bars).CalculateFireflyOscillator(kind, length, smooth);
        Assert.Equal(expected.Outputs["Fo"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Fo", "Signal" })
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFireflyOscillatorFast(Data(bars), context, length, smooth, kind, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new FireflyOscillatorState(kind, length, smooth); using var window = new FireflyWindow(kind, length, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles((8, 1, 4), (3, -1, 2), (9, 3, 6), (7, -3, 1))) { state.Update(Native(b), true, false); window.Next(b.High, b.Low, b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, -999, 999))[0]), false, false); window.Next(999, -999, 999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final);
                    Assert.Equal(expected.Outputs["Fo"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Fo"]); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Trade);
                    Assert.Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]); Assert.Equal(point.Outputs["Signal"], direct.SignalLine);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandStandardizationMidpointAndSlopeSignals()
    {
        var result = Check(Candles(new[] { 0d, 2, 0, 2, 2 }.Select(v => (v, v, v)).ToArray()), 2, 1, MovingAvgType.SimpleMovingAverage, 1);
        Assert.Equal(new[] { 46d, 71, 46, 46, 71 }, result.Outputs["Fo"]); Assert.Equal(result.Outputs["Fo"], result.Outputs["Signal"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.StrongSell, Signal.None, Signal.StrongBuy }, result.Signals);
        foreach (var kind in Kinds)
        { var identity = Check(Candles((double.MaxValue, -double.MaxValue, 0), (4, -2, 1), (7, 1, 5)), 1, 3, kind.Kind, kind.Reference); Assert.All(identity.Outputs.Values.SelectMany(v => v), v => Assert.Equal(46, v)); }
        Check(Candles((11, -3, 5), (17, 1, 9), (4, -8, -1)), 2, 1);
    }
    [Fact]
    public void WideWeightedPricesVarianceAndFourMeansMatchFractions()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Candles(Enumerable.Range(0, 19).Select(i => ((i % 7 + 1) * scale, -(i % 5 + 1) * scale, (i % 3 - 1) * scale)).ToArray()), kind: kind.Kind, reference: kind.Reference);
        foreach (var kind in Kinds) Check(Candles((double.MaxValue, -double.MaxValue, double.MaxValue), (double.MaxValue, -double.MaxValue, -double.MaxValue), (double.MaxValue, double.MaxValue, double.MaxValue), (4, -2, 1), (2, -1, 0), (1, -1, 1), (2, 0, 2)), kind: kind.Kind, reference: kind.Reference);
        Check(Candles(Enumerable.Range(0, 12).Select(i => { var v = i % 2 == 0 ? 1d : Math.BitIncrement(1); return (v, v, v); }).ToArray()), 2, 2);
    }
    [Fact]
    public void SubnormalDeviationDoesNotTakeTheZeroVarianceBranch()
    {
        var result = Check(Candles((0, 0, 0), (double.Epsilon, double.Epsilon, double.Epsilon)), 2, 1, MovingAvgType.SimpleMovingAverage, 1);
        Assert.Equal(new[] { 46d, 96 }, result.Outputs["Fo"]);
        using var window = new FireflyWindow(MovingAvgType.SimpleMovingAverage, 2, 1);
        Assert.Equal(0, window.Normalize(0, new RocBankValue(0), true).Publish());
        Assert.Equal(200, window.Normalize(double.Epsilon, new RocBankValue(0), false).Publish());
        Assert.Equal(200, window.Normalize(double.Epsilon, new RocBankValue(0), true).Publish());
        window.Reset(); window.Normalize(0, new RocBankValue(0), true); Assert.Equal(200, window.Normalize(double.MaxValue, new RocBankValue(0), true).Publish());
    }
    [Fact]
    public void RollingMaximumAndSlopeHistoryAreIndependent()
    {
        using var window = new FireflyWindow(MovingAvgType.SimpleMovingAverage, 2, 3, false);
        var lines = new[] { 0d, 4, 6, 7, 7, 3, 1, 0 }; var maxima = new[] { 0d, 4, 6, 7, 7, 7, 7, 3 };
        var signals = new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.Buy, Signal.None, Signal.StrongSell, Signal.Sell, Signal.Sell };
        for (var replay = 0; replay < 2; replay++)
        {
            window.Reset();
            for (var i = 0; i < lines.Length; i++)
            { window.Finish(new RocBankValue(999), false); foreach (var final in new[] { false, true }) { var point = window.Finish(new RocBankValue(2 * (lines[i] - 46)), final); Assert.Equal(lines[i], point.Line); Assert.Equal(maxima[i], point.SignalLine); Assert.Equal(signals[i], point.Trade); } }
        }
    }
    [Fact]
    public void ExtendedStartupHistoryRecoversAndExhaustedOverridesStayExact()
    {
        var bars = Candles(Enumerable.Repeat((double.MaxValue, double.MaxValue, double.MaxValue), 16).ToArray());
        var expected = Check(bars, 3, 2, MovingAvgType.WeightedMovingAverage, 2);
        Assert.Contains(expected.Outputs["Fo"], double.IsPositiveInfinity); Assert.Equal(46, expected.Outputs["Fo"][^1]); Assert.Equal(46, expected.Outputs["Signal"][^1]);
        foreach (var key in expected.Outputs.Keys)
        {
            var calls = 0; using var armed = ComponentAverage.Arm((v, _) => { calls++; return v; }); Assert.NotNull(ComponentAverage.Take(new[] { 1d }, 1));
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFireflyOscillatorFast(Data(bars), context, 3, 2, MovingAvgType.WeightedMovingAverage, key);
            Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(1, calls); Assert.Equal(5, ComponentAverage.Requests);
        }
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var slot in Enumerable.Range(0, 2))
        { var periods = new[] { 3, 2 }; periods[slot] = length; Check(Candles((4, -2, 1), (4, -2, -1), (7, 0, 3), (1, -1, 0)), periods[0], periods[1], kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), periods[0], periods[1], kind.Kind, kind.Reference); }
    }
    [Fact]
    public void FourComponentSlotsPreserveStageInputsAndPeriods()
    {
        var bars = Candles(new[] { 1d, 2, 3, 4 }.Select(v => (v, v, v)).ToArray());
        foreach (var route in new[] { "batch", "Fo", "Signal" })
        {
            var inputs = new[] { new[] { 1d, 2, 3, 4 }, new[] { 100d, 400, 600, 800 }, new[] { 2d, 4, 6, 8 }, new[] { 1d, 3, 5, 7 } };
            var supplied = new[] { new[] { 0d, 0, 0, 0 }, inputs[2], inputs[3], new[] { 0d, 100, -100, 0 } }; var periods = new[] { 2, 3, 3, 2 }; var calls = 0;
            using var armed = ComponentAverage.Arm(Enumerable.Range(0, 4).Select(slot => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((v, period) => { calls++; Assert.Equal(periods[slot], period); Assert.Equal(inputs[slot], v); return supplied[slot]; })).ToArray());
            if (route == "batch") { var actual = Data(bars).CalculateFireflyOscillator(length: 2, smoothLength: 3); var expected = BuiltInFormulaReferences.FireflyValues(bars, 2, 3, 4); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], actual.OutputValues[key]); }
            else { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFireflyOscillatorFast(Data(bars), context, 2, 3, outputKey: route); Assert.Equal(route == "Fo" ? new[] { 46d, 96, -4, 46 } : new[] { 46d, 96, 96, 96 }, output.ToArray()); }
            Assert.Equal(route == "batch" ? 0 : 4, calls);
        }
    }
    [Fact]
    public void SelectedPricesUsePerBarRanges()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var prices = new[] { 5d, 12, 6, 15 };
        var selectedBars = bars.Select((b, i) => new Bar(b.Time, b.Open, prices[i] >= b.Low && prices[i] <= b.High ? b.High : Math.Max(i == 0 ? prices[i] : prices[i - 1], prices[i]), prices[i] >= b.Low && prices[i] <= b.High ? b.Low : Math.Min(i == 0 ? prices[i] : prices[i - 1], prices[i]), prices[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.FireflyValues(selectedBars, 2, 3, 4); var batch = Data(bars); batch.SetCustomValues(prices.ToList()); batch.CalculateFireflyOscillator(length: 2);
        foreach (var key in expected.Outputs.Keys)
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); var data = Data(bars); data.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFireflyOscillatorFast(data, context, 2, outputKey: key); Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(prices, data.ChainedValues); }
    }
    [Fact]
    public void CoreUsesPublicWeightedPriceFormulaAndProtectsSpans()
    {
        var bars = Candles((9, -1, 2), (11, -3, 4), (7, 1, 5), (2, -7, -3), (9, -2, 1)); var highs = bars.Select(b => b.High).ToArray(); var lows = bars.Select(b => b.Low).ToArray();
        foreach (var length in new[] { 0, 1, 3, int.MaxValue }) foreach (var smooth in new[] { 0, 2, int.MaxValue })
        {
            var expected = BuiltInFormulaReferences.FireflyValues(bars, length, smooth, 4).Outputs["Fo"]; var input = bars.Select(b => b.Close).ToArray(); var output = Enumerable.Repeat(97d, bars.Length + 1).ToArray();
            OscillatorCore.FireflyOscillator(highs, lows, input, output, length, smooth); Assert.Equal(expected, output.Take(bars.Length)); Assert.Equal(97, output[^1]);
            if (length <= 1) Assert.All(output.Take(bars.Length), v => Assert.Equal(46, v));
            OscillatorCore.FireflyOscillator(highs, lows, input, input, length, smooth); Assert.Equal(expected, input);
        }
        OscillatorCore.FireflyOscillator(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
        var unchanged = Enumerable.Repeat(97d, bars.Length).ToArray(); var close = bars.Select(b => b.Close).ToArray();
        Assert.Throws<ArgumentException>(() => OscillatorCore.FireflyOscillator(highs, lows, close, Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => OscillatorCore.FireflyOscillator(Array.Empty<double>(), lows, close, unchanged)); Assert.All(unchanged, v => Assert.Equal(97, v));
        Assert.Throws<ArgumentException>(() => OscillatorCore.FireflyOscillator(highs, Array.Empty<double>(), close, unchanged)); Assert.All(unchanged, v => Assert.Equal(97, v));
    }
    [Fact]
    public void LegacyAveragesKeepPublicRouteAgreement()
    {
        var bars = Candles((4, 1, 2), (6, 0, 3), (5, 2, 2), (8, 1, 7), (2, -3, -1), (4, 0, 3));
        var kind = MovingAvgType.TripleExponentialMovingAverage; var batch = Data(bars).CalculateFireflyOscillator(kind, 3, 2); using var state = new FireflyOscillatorState(kind, 3, 2); using var context = new ComputeContext();
        foreach (var key in new[] { "Fo", "Signal" }) { using var output = IndicatorCompute.ComputeFireflyOscillatorFast(Data(bars), context, 3, 2, kind, key); Assert.Equal(batch.OutputValues[key], output.ToArray()); }
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); foreach (var key in batch.OutputValues.Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]); }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FireflyOscillatorState(length: 3, smoothLength: 2); using var control = new FireflyOscillatorState(length: 3, smoothLength: 2);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
