using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KaseRatioNumericalTests
{
    private static Bar B(double high, double low, double close, double volume = 1) => new(DateTime.UnixEpoch, close, high, low, close, volume);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KASE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KaseIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRatios(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KaseRatioOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1, double[]? selected = null)
    {
        var projected = selected is null ? bars : bars.Select((b, i) => B(b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.KaseRatioValues(projected, length, reference, selected is not null);
        var data = Data(bars); if (selected is not null) data.SetCustomValues(selected.ToList());
        var batch = data.CalculateKaseIndicator(kind, length); Assert.Empty(batch.ChainedValues); Assert.Equal(expected.Trades, batch.SignalsList);
        foreach (var key in new[] { "KaseUp", "KaseDn" })
        {
            Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); var raw = Data(bars); if (selected is not null) raw.SetCustomValues(selected.ToList());
            using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeKaseIndicatorFast(raw, context, length, kind, key); Assert.Equal(expected.Outputs[key], fast.ToArray());
            if (selected is not null) Assert.Equal(selected, raw.ChainedValues);
        }
        using var state = new KaseIndicatorState(kind, length); using var window = new KaseRatioWindow(kind, length);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(B(99, -99, -77, 7)), true, false); window.Next(99, -99, -77, 7, true); state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(99, -99, -77, 7)), false, false); window.Next(99, -99, -77, 7, false);
                var b = projected[i]; var high = b.High; var low = b.Low;
                if (selected is not null && !(b.Close >= low && b.Close <= high)) { high = Math.Max(b.Close, projected[Math.Max(0, i - 1)].Close); low = Math.Min(b.Close, projected[Math.Max(0, i - 1)].Close); }
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(B(high, low, b.Close, b.Volume)), final, true); var direct = window.Next(high, low, b.Close, b.Volume, final);
                    Assert.Equal(expected.Outputs["KaseUp"][i], point.Value); Assert.Equal(point.Value, direct.Up); Assert.Equal(point.Value, point.Outputs!["KaseUp"]);
                    Assert.Equal(expected.Outputs["KaseDn"][i], point.Outputs!["KaseDn"]); Assert.Equal(point.Outputs["KaseDn"], direct.Down); Assert.Equal(expected.Trades[i], direct.Trade);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void HandRatiosHoldIndependentlyAtZeroDivisors()
    {
        var values = Check(new[] { B(3, 1, 2), B(5, 3, 4), B(7, 5, 6, 0) }, 1);
        Assert.Equal(new[] { 0d, 1, 1 }, values["KaseUp"]); Assert.Equal(new[] { 0d, 5, 5 }, values["KaseDn"]);
        Check(new[] { B(3, 1, 2), B(4, 0, 2), B(8, 2, 4), B(4, 4, 4), B(5, 3, 4, -1), B(5, -3, -1) }, 1);
    }
    [Fact]
    public void CompleteDenominatorsRecoverOverflowAndUnderflow()
    {
        var large = Math.Pow(2, 1000); var tiny = Math.Pow(2, -1000);
        var values = Check(new[] { B(double.MaxValue, 1, 2, large), B(1, tiny, .5, large) }, 1);
        Assert.Equal(double.MaxValue, values["KaseUp"][1]);
        values = Check(new[] { B(tiny, 0, tiny, tiny), B(2 * large, large, large, tiny) }, 1); Assert.Equal(tiny, values["KaseUp"][1]);
        foreach (var kind in Kinds) Check(Enumerable.Repeat(B(1, tiny, .5, double.MaxValue), 12).ToArray(), 4, kind.Kind, kind.Reference);
        Check(new[] { B(double.MaxValue, 1, 2), B(1, double.Epsilon, .5, double.Epsilon), B(3, 2, 2.5, 0), B(4, 2, 3) }, 1);
    }
    [Fact]
    public void SignedVolumesAndTinyRangesRetainEveryGate()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -500), 1d, double.MaxValue / 8 })
            Check(Enumerable.Range(0, 19).Select(i => B((i % 5 + 2) * scale, (i % 5 - 2) * scale, (i % 5 - 1) * scale, i % 4 - 2)).ToArray(), 3, kind.Kind, kind.Reference);
        Check(new[] { B(double.MaxValue, -double.MaxValue, 0), B(12, 10, 11), B(-10, -12, -11), B(3, 1, 2), B(1, -1, 0) }, 3);
    }
    [Fact]
    public void MaximumAndNormalizedPeriodsUseLazyMeans()
    {
        var bars = new[] { B(3, 1, 2), B(5, -2, 4), B(1, -4, -3), B(2, 0, 1) };
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in Kinds)
        { Check(Array.Empty<Bar>(), period, kind.Kind, kind.Reference); Check(bars, period, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void ResetPreviewAndLongHistoryRetainRatiosAndMeans()
    {
        var bars = Enumerable.Range(0, 180).Select(i => B(i % 11 + 2, i % 7 - 8, i % 5 - 1, i % 5 - 2)).ToArray();
        foreach (var kind in Kinds) Check(bars, 7, kind.Kind, kind.Reference);
    }
    [Fact]
    public void SelectedRangesAndTwoCallbackSlotsRemainExplicit()
    {
        var bars = Enumerable.Range(0, 12).Select(i => B(i + 2, i - 1, i + 1, i + 1)).ToArray(); var selected = Enumerable.Range(0, 12).Select(i => (double)(i % 5 - 8)).ToArray(); Check(bars, selected: selected);
        var high = selected.Select((v, i) => Math.Max(v, selected[Math.Max(0, i - 1)])).ToArray(); var low = selected.Select((v, i) => Math.Min(v, selected[Math.Max(0, i - 1)])).ToArray();
        foreach (var key in new[] { "KaseUp", "KaseDn" })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); var requests = new List<(double[] Values, int Period)>();
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return Enumerable.Repeat(requests.Count == 1 ? 2d : 1d, bars.Length).ToArray(); };
            using var armed = ComponentAverage.Arm(new[] { callback, callback }); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeKaseIndicatorFast(data, context, 4, MovingAvgType.SimpleMovingAverage, key);
            Assert.Equal(2, requests.Count); Assert.All(requests, x => Assert.Equal(4, x.Period)); Assert.Equal(bars.Select(b => b.Volume), requests[0].Values); Assert.Equal(high.Zip(low, (h, l) => h - l), requests[1].Values); Assert.Equal(selected, data.ChainedValues);
            var expected = Enumerable.Range(0, bars.Length).Select(i => i == 0 ? 0 : key == "KaseUp" ? high[i - 1] / (low[i] * 4) : high[i] / (low[i - 1] * 4)).ToArray(); Assert.Equal(expected, result.ToArray());
        }
    }
    [Fact]
    public void RejectedCandlesCannotAdvanceMeansOrHeldValues()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new KaseIndicatorState(length: 3); using var control = new KaseIndicatorState(length: 3);
            state.Update(Native(B(4, 0, 3)), true, false); control.Update(Native(B(4, 0, 3)), true, false); var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in new[] { B(5, 1, 2), B(7, -2, 1), B(3, -1, 0) }) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["KaseDn"], actual.Outputs!["KaseDn"]); }
        }
    }
    [Fact]
    public void LegacyMeanRetainsArrayFallback()
    {
        var bars = Enumerable.Range(0, 17).Select(i => B(i % 5 + 2, i % 5 - 2, i % 5 - 1, i % 3 + 1)).ToArray();
        foreach (var kind in new[] { MovingAvgType.LinearWeightedMovingAverage, MovingAvgType.DoubleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateKaseIndicator(kind, 3); using var context = new ComputeContext();
            foreach (var key in new[] { "KaseUp", "KaseDn" }) { using var fast = IndicatorCompute.ComputeKaseIndicatorFast(Data(bars), context, 3, kind, key); Assert.Equal(batch.OutputValues[key], fast.ToArray()); }
        }
        Assert.Throws<NotSupportedException>(() => new KaseIndicatorState(MovingAvgType.LinearWeightedMovingAverage, 3));
    }
}
