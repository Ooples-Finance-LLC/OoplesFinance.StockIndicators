using System.Numerics;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class JrcNumericalTests
{
    private static Bar B(double high, double low, double close) => new(DateTime.UnixEpoch, close, high, low, close, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("JRC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(JrcFractalDimension)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangesAndMeans(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.JrcOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Raw) Check(Bar[] bars, int length = 3, int scale = 2, int smoothing = 2,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1, double[]? selected = null)
    {
        var projected = selected is null ? bars : bars.Select((b, i) => B(b.High, b.Low, selected[i])).ToArray();
        var expected = BuiltInFormulaReferences.JrcValues(projected, length, scale, smoothing, reference, selected is not null);
        var data = Data(bars); if (selected is not null) data.SetCustomValues(selected.ToList());
        var batch = data.CalculateJrcFractalDimension(kind, length, scale, smoothing);
        Assert.Equal(expected.Outputs["Jrcfd"], batch.ChainedValues); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Jrcfd", "Signal" })
        {
            Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); var raw = Data(bars); if (selected is not null) raw.SetCustomValues(selected.ToList());
            using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeJrcFractalDimensionFast(raw, context, length, scale, smoothing, kind, key == "Signal");
            Assert.Equal(expected.Outputs[key], fast.ToArray()); if (selected is not null) Assert.Equal(selected, raw.ChainedValues);
        }
        using var state = new JrcFractalDimensionState(kind, length, scale, smoothing); using var window = new JrcWindow(kind, length, scale, smoothing);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(B(99, -99, -77)), true, false); window.Next(99, -99, -77, true); state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(99, -99, -77)), false, false); window.Next(99, -99, -77, false);
                var b = projected[i]; var high = b.High; var low = b.Low;
                if (selected is not null && !(b.Close >= low && b.Close <= high))
                { high = Math.Max(b.Close, projected[Math.Max(0, i - 1)].Close); low = Math.Min(b.Close, projected[Math.Max(0, i - 1)].Close); }
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(B(high, low, b.Close)), final, true); var direct = window.Next(high, low, b.Close, final);
                    Assert.Equal(expected.Outputs["Jrcfd"][i], point.Value); Assert.Equal(point.Value, direct.Line);
                    Assert.Equal(expected.Outputs["Signal"][i], point.Outputs!["Signal"]); Assert.Equal(point.Outputs["Signal"], direct.SignalLine); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandStartupExpiryAndScaleOneHaveKnownValues()
    {
        var bars = Enumerable.Repeat(B(11, 9, 10), 30).ToArray();
        foreach (var length in new[] { 1, 2 }) { var values = Check(bars, length, 2, 1); Assert.Equal(1, values.Raw[0]); Assert.Equal(2, values.Raw[^1]); }
        Assert.All(Check(bars, 3, 1, 1).Outputs["Jrcfd"], value => Assert.Equal(0, value));
        Assert.All(Check(Enumerable.Repeat(B(0, 0, 0), 8).ToArray(), smoothing: 1).Outputs["Jrcfd"], value => Assert.Equal(2, value));
    }
    [Fact]
    public void RationalLogRetainsExtremeAndNearUnityRatios()
    {
        var unit = BigInteger.One; var large = unit << 100;
        var pairs = new[] { (unit, unit), (new BigInteger(3), new BigInteger(2)), (new BigInteger(2), new BigInteger(3)), (new BigInteger(2), unit), (large + 1, large), (large - 1, large), (unit << 2100, unit), (unit, unit << 2100), (new BigInteger(3), new BigInteger(7)), (new BigInteger(7), new BigInteger(3)) };
        var budget = new IndicatorErrorBudget(0, 4e-15, true);
        foreach (var (n, d) in pairs)
        {
            var expected = (new ReferenceFraction(n) / new ReferenceFraction(d)).LogToDouble(); var actual = JrcWindow.LogRatio(n, d);
            Assert.True(budget.Accepts(expected, actual), $"log({n}/{d}): {expected:R} versus {actual:R}");
        }
    }
    [Fact]
    public void WideTinyAndExpiringRangesRetainTheLogarithmicRatio()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -500), 1d, double.MaxValue / 8 })
            Check(Enumerable.Range(0, 19).Select(i => B((i % 5 + 2) * scale, (i % 5 - 2) * scale, (i % 5 - 1) * scale)).ToArray(), kind: kind.Kind, reference: kind.Reference);
        foreach (var kind in Kinds) Check(new[] { B(double.MaxValue, -double.MaxValue, double.MaxValue), B(double.MaxValue, -double.MaxValue, -double.MaxValue), B(2, -2, 1), B(3, -1, 2), B(1, 0, 1), B(2 * double.Epsilon, 0, double.Epsilon), B(4, -2, 3) }, kind: kind.Kind, reference: kind.Reference);
    }
    [Fact]
    public void WidenedPeriodProductsPreserveClampAndLazyHistory()
    {
        var bars = new[] { B(3, 1, 2), B(5, -2, 4), B(1, -4, -3), B(2, 0, 1) };
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var scale in new[] { int.MinValue, 0, 1, 2, int.MaxValue })
        { Check(Array.Empty<Bar>(), length, scale, int.MaxValue); Check(bars, length, scale, 1); }
        foreach (var kind in Kinds) Check(bars, 3, 2, int.MaxValue, kind.Kind, kind.Reference);
        Assert.Equal(530, JrcWindow.BoundedProduct(int.MaxValue, int.MaxValue)); Assert.Equal(2, JrcWindow.BoundedProduct(1, 0));
    }
    [Fact]
    public void SelectedRangesAndBothCallbackSlotsRemainExplicit()
    {
        var bars = Enumerable.Range(0, 12).Select(i => B(i + 2, i - 1, i + 1)).ToArray(); var selected = Enumerable.Range(0, 12).Select(i => (double)(i % 5 - 8)).ToArray();
        Check(bars, selected: selected);
        var projected = bars.Select((b, i) => B(b.High, b.Low, selected[i])).ToArray(); var raw = BuiltInFormulaReferences.JrcValues(projected, 3, 2, 4, 1, true).Raw;
        var line = Enumerable.Range(0, bars.Length).Select(i => (double)(i + 10)).ToArray(); var signal = line.Select(x => x - 3).ToArray();
        foreach (var outputSignal in new[] { false, true })
        {
            var requests = new List<(double[] Values, int Period)>(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return requests.Count == 1 ? line : signal; };
            using var armed = ComponentAverage.Arm(outputSignal ? new[] { callback, callback } : new[] { callback });
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeJrcFractalDimensionFast(data, context, 3, 2, 4, MovingAvgType.SimpleMovingAverage, outputSignal);
            Assert.Equal(outputSignal ? signal : line, output.ToArray()); Assert.Equal(outputSignal ? 2 : 1, requests.Count);
            Assert.All(requests, request => Assert.Equal(4, request.Period)); Assert.Equal(raw, requests[0].Values); if (outputSignal) Assert.Equal(line, requests[1].Values); Assert.Equal(selected, data.ChainedValues);
        }
    }
    [Fact]
    public void LegacyAverageStillUsesBothPublicComponents()
    {
        var bars = Enumerable.Range(0, 17).Select(i => B(i % 5 + 2, i % 5 - 2, i % 5 - 1)).ToArray();
        foreach (var kind in new[] { MovingAvgType.LinearWeightedMovingAverage, MovingAvgType.DoubleExponentialMovingAverage })
        {
            var expected = Data(bars).CalculateJrcFractalDimension(kind, 3, 2, 3);
            using var context = new ComputeContext(); foreach (var signal in new[] { false, true })
            { using var fast = IndicatorCompute.ComputeJrcFractalDimensionFast(Data(bars), context, 3, 2, 3, kind, signal); Assert.Equal(expected.OutputValues[signal ? "Signal" : "Jrcfd"], fast.ToArray()); }
        }
        Assert.Throws<NotSupportedException>(() => new JrcFractalDimensionState(MovingAvgType.LinearWeightedMovingAverage, 3, 2, 3));
        var nativeExpected = Data(bars).CalculateJrcFractalDimension(MovingAvgType.DoubleExponentialMovingAverage, 3, 2, 3);
        using var state = new JrcFractalDimensionState(MovingAvgType.DoubleExponentialMovingAverage, 3, 2, 3);
        for (var i = 0; i < bars.Length; i++) { var actual = state.Update(Native(bars[i]), true, true); Assert.Equal(nativeExpected.OutputValues["Jrcfd"][i], actual.Value); Assert.Equal(nativeExpected.OutputValues["Signal"][i], actual.Outputs!["Signal"]); }
    }
    [Fact]
    public void RejectedCandlesCannotAdvanceRangeOrMeans()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new JrcFractalDimensionState(length1: 3, length2: 2, smoothLength: 2); using var control = new JrcFractalDimensionState(length1: 3, length2: 2, smoothLength: 2);
            state.Update(Native(B(4, 0, 3)), true, false); control.Update(Native(B(4, 0, 3)), true, false);
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in new[] { B(5, 1, 2), B(7, -2, 1), B(3, -1, 0) })
            { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
    [Fact]
    public void ResetClearsDeepOldRangesBeforeDifferentHistory()
    {
        var bars = new[] { B(3, 1, 2), B(5, 2, 4), B(4, 2, 3), B(9, 7, 8) };
        foreach (var kind in Kinds) Check(bars, 3, 3, 2, kind.Kind, kind.Reference);
        Check(Enumerable.Range(0, 180).Select(i => B(i % 11 + 2, i % 7 - 8, i % 5 - 1)).ToArray(), 7, 5, 3);
    }
}
