using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FibonacciRetraceNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FibonacciRetrace)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentInterpolation(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FibonacciRetraceOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int mean = 3, int range = 4, double factor = .382, MovingAvgType kind = MovingAvgType.WeightedMovingAverage, int reference = 2)
    {
        var expected = BuiltInFormulaReferences.FibonacciRetraceValues(bars, mean, range, factor, reference); var batch = Data(bars).CalculateFibonacciRetrace(kind, mean, range, factor);
        Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "UpperBand", "LowerBand" })
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFibonacciRetraceFast(Data(bars), context, range, factor, key == "LowerBand" ? IndicatorCompute.ChannelBand.Lower : IndicatorCompute.ChannelBand.Upper); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new FibonacciRetraceState(kind, mean, range, factor); using var window = new FibonacciRetraceWindow(kind, mean, range, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles((8, 1, 4), (3, -1, 2), (9, 3, 6), (7, -3, 1))) { state.Update(Native(b), true, false); window.Next(b.High, b.Low, b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, -999, 999))[0]), false, false); window.Next(999, -999, 999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final);
                    Assert.Equal(expected.Outputs["UpperBand"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["UpperBand"]); Assert.Equal(point.Value, direct.Upper); Assert.Equal(expected.Signals[i], direct.Trade);
                    Assert.Equal(expected.Outputs["LowerBand"][i], point.Outputs["LowerBand"]); Assert.Equal(point.Outputs["LowerBand"], direct.Lower);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandInterpolationExpiryAndSignalMargins()
    {
        var result = Check(Candles((8, 0, 4), (12, -4, 6), (5, 1, 3), (6, 2, 4)), 1, 2, .25, MovingAvgType.SimpleMovingAverage, 1);
        Assert.Equal(new[] { 6d, 8, 8, 4.75 }, result.Outputs["UpperBand"]); Assert.Equal(new[] { 2d, 0, 0, 2.25 }, result.Outputs["LowerBand"]);
        // With three bars, the second observation remains the extremum when a
        // later weaker observation arrives; two-bar windows cannot expose reversed pruning.
        var retained = Check(Candles((8, 0, 4), (12, -4, 6), (5, 1, 3), (6, 2, 4), (7, 3, 5)), 1, 3, .25, MovingAvgType.SimpleMovingAverage, 1);
        Assert.Equal(new[] { 6d, 8, 8, 8, 5.5 }, retained.Outputs["UpperBand"]);
        Assert.Equal(new[] { 2d, 0, 0, 0, 2.5 }, retained.Outputs["LowerBand"]);
        var signals = Check(Candles(new[] { 1d, 9, 9, 8, 1 }.Select(v => (10d, 0d, v)).ToArray()), 1, 1, .25, MovingAvgType.SimpleMovingAverage, 1);
        Assert.Equal(new[] { Signal.StrongSell, Signal.StrongBuy, Signal.Buy, Signal.Buy, Signal.StrongSell }, signals.Signals);
        Assert.All(Check(Candles((10, 0, 5)), factor: -.5).Outputs["UpperBand"], v => Assert.Equal(15, v));
        Assert.All(Check(Candles((10, 0, 5)), factor: -.5).Outputs["LowerBand"], v => Assert.Equal(-5, v));
    }
    [Fact]
    public void WideProductsMeansAndExpiryMatchFractions()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Candles(Enumerable.Range(0, 21).Select(i => ((i % 7 + 1) * scale, -(i % 5 + 1) * scale, (i % 3 - 1) * scale)).ToArray()), kind: kind.Kind, reference: kind.Reference);
        foreach (var factor in new[] { -double.MaxValue, -.5, 0, double.Epsilon, .25, .5, 1, 2, double.MaxValue })
            Check(Candles((double.MaxValue, -double.MaxValue, 0), (double.MaxValue, 0, double.MaxValue), (4, -2, 1), (2, -1, 0), (1, -1, 1), (2, 0, 2)), factor: factor);
        Check(Candles((Math.BitIncrement(1), 1, 1), (Math.BitIncrement(1), 1, 1)), factor: double.MaxValue);
    }
    [Fact]
    public void CompleteInterpolationCancelsBeforeRounding()
    {
        var m = double.MaxValue;
        foreach (var factor in new[] { -m, 0, .382, .5, 1, m })
        { var flat = Check(Candles((m, m, m)), factor: factor); Assert.Equal(m, flat.Outputs["UpperBand"][0]); Assert.Equal(m, flat.Outputs["LowerBand"][0]); }
        var midpoint = Check(Candles((m, -m, 0)), factor: .5); Assert.Equal(0, midpoint.Outputs["UpperBand"][0]); Assert.Equal(0, midpoint.Outputs["LowerBand"][0]);
        var quarter = Check(Candles((m, -m, 0)), factor: .25); Assert.Equal(m / 2, quarter.Outputs["UpperBand"][0]); Assert.Equal(-m / 2, quarter.Outputs["LowerBand"][0]);
        var tiny = Check(Candles((double.Epsilon, 0, 0)), factor: m); Assert.True(double.IsFinite(tiny.Outputs["UpperBand"][0])); Assert.True(double.IsFinite(tiny.Outputs["LowerBand"][0])); Assert.True(tiny.Outputs["LowerBand"][0] > 0);
        var subnormalTie = Check(Candles((double.Epsilon, 0, 0)), factor: .5); Assert.Equal(0, subnormalTie.Outputs["UpperBand"][0]); Assert.Equal(0, subnormalTie.Outputs["LowerBand"][0]);
    }
    [Fact]
    public void EndpointReflectionAndMeanOptionsDoNotChangeBands()
    {
        var bars = Candles((8, -2, 4), (12, -4, 6), (5, 1, 3), (6, 2, 4));
        var upper = Check(bars, range: 1, factor: 0); var lower = Check(bars, range: 1, factor: 1);
        Assert.Equal(bars.Select(b => b.High), upper.Outputs["UpperBand"]); Assert.Equal(bars.Select(b => b.Low), upper.Outputs["LowerBand"]);
        Assert.Equal(upper.Outputs["UpperBand"], lower.Outputs["LowerBand"]); Assert.Equal(upper.Outputs["LowerBand"], lower.Outputs["UpperBand"]);
        var normal = Check(bars, factor: .25); var reflected = Check(Candles(bars.Select(b => (-b.Low, -b.High, -b.Close)).ToArray()), factor: .25);
        Assert.Equal(normal.Outputs["UpperBand"].Select(v => -v), reflected.Outputs["LowerBand"]); Assert.Equal(normal.Outputs["LowerBand"].Select(v => -v), reflected.Outputs["UpperBand"]);
        foreach (var kind in Kinds) { var changed = Check(bars, mean: 7, factor: .25, kind: kind.Kind, reference: kind.Reference); foreach (var key in normal.Outputs.Keys) Assert.Equal(normal.Outputs[key], changed.Outputs[key]); }
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var slot in Enumerable.Range(0, 2))
        { var periods = new[] { 3, 4 }; periods[slot] = length; Check(Candles((4, -2, 1), (4, -2, -1), (7, 0, 3), (1, -1, 0)), periods[0], periods[1], kind: kind.Kind, reference: kind.Reference); Check(Array.Empty<Bar>(), periods[0], periods[1], kind: kind.Kind, reference: kind.Reference); }
    }
    [Fact]
    public void SelectedPricesApplyPerBarRangesAndConsumeNoOverrides()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var prices = new[] { 5d, 12, 6, 15 };
        var selectedBars = bars.Select((b, i) => new Bar(b.Time, b.Open, prices[i] >= b.Low && prices[i] <= b.High ? b.High : Math.Max(i == 0 ? prices[i] : prices[i - 1], prices[i]), prices[i] >= b.Low && prices[i] <= b.High ? b.Low : Math.Min(i == 0 ? prices[i] : prices[i - 1], prices[i]), prices[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.FibonacciRetraceValues(selectedBars, 3, 2, .25, 2); var calls = 0;
        using var armed = ComponentAverage.Arm((v, _) => { calls++; return v; });
        var batch = Data(bars); batch.SetCustomValues(prices.ToList()); batch.CalculateFibonacciRetrace(length1: 3, length2: 2, factor: .25);
        Assert.Equal(expected.Signals, batch.SignalsList); Assert.Empty(batch.CustomValuesList);
        foreach (var key in expected.Outputs.Keys)
        {
            Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); var selected = Data(bars); selected.SetCustomValues(prices.ToList()); using var context = new ComputeContext();
            using var output = IndicatorCompute.ComputeFibonacciRetraceFast(selected, context, 2, .25, key == "LowerBand" ? IndicatorCompute.ChannelBand.Lower : IndicatorCompute.ChannelBand.Upper); Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(prices, selected.ChainedValues);
        }
        Assert.Equal(0, calls);
    }
    [Fact]
    public void LegacyMeanStillControlsOnlyTradingSignals()
    {
        var bars = Candles((4, 1, 2), (6, 0, 3), (5, 2, 2), (8, 1, 7), (2, -3, -1), (4, 0, 3));
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateFibonacciRetrace(kind, 2, 4, .25); using var state = new FibonacciRetraceState(kind, 2, 4, .25); using var window = new FibonacciRetraceWindow(kind, 2, 4, .25);
            for (var i = 0; i < bars.Length; i++) { var b = bars[i]; var point = state.Update(Native(b), true, true); var direct = window.Next(b.High, b.Low, b.Close, true); foreach (var key in batch.OutputValues.Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]); Assert.Equal(batch.SignalsList![i], direct.Trade); }
        }
    }
    [Fact]
    public void NonfiniteFactorsAreRejectedEvenOnEmptyInput()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var bars in new[] { Array.Empty<Bar>(), Candles((4, 0, 2)) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FibonacciRetraceState(factor: factor));
            var data = Data(bars); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateFibonacciRetrace(factor: factor)); Assert.Empty(data.OutputValues);
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeFibonacciRetraceFast(Data(bars), context, factor: factor));
        }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FibonacciRetraceState(length1: 3, length2: 4); using var control = new FibonacciRetraceState(length1: 3, length2: 4);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["LowerBand"], actual.Outputs!["LowerBand"]); }
        }
    }
}
