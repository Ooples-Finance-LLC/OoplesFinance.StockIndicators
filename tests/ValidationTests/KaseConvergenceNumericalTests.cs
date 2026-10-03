using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KaseConvergenceNumericalTests
{
    private static Bar B(double high, double low, double close) => new(DateTime.UnixEpoch, close, high, low, close, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KCD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KaseConvergenceDivergence)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KaseConvergenceOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 3, int peak = 2, int signal = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1, double[]? selected = null)
    {
        var projected = selected is null ? bars : bars.Select((b, i) => B(b.High, b.Low, selected[i])).ToArray();
        var expected = BuiltInFormulaReferences.KaseConvergenceValues(projected, length, peak, signal, reference, selected is not null);
        var data = Data(bars); if (selected is not null) data.SetCustomValues(selected.ToList());
        var batch = data.CalculateKaseConvergenceDivergence(kind, length, peak, signal);
        Assert.Equal(expected.Values, batch.ChainedValues); Assert.Equal(expected.Values, batch.OutputValues["Kcd"]); Assert.Equal(expected.Trades, batch.SignalsList);
        var raw = Data(bars); if (selected is not null) raw.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeKaseConvergenceDivergenceFast(raw, context, length, peak, signal, kind);
        Assert.Equal(expected.Values, fast.ToArray()); if (selected is not null) Assert.Equal(selected, raw.ChainedValues);
        using var state = new KaseConvergenceDivergenceState(kind, length, peak, signal); using var window = new KaseConvergenceWindow(kind, length, peak, signal);
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
                    Assert.Equal(expected.Values[i], point.Value); Assert.Equal(point.Value, direct.Value); Assert.Equal(point.Value, point.Outputs!["Kcd"]); Assert.Equal(expected.Trades[i], direct.Trade);
                }
            }
        }
        return expected.Values;
    }
    [Fact]
    public void HandLagAndZeroPaddedMeansHaveKnownValues()
    {
        var bars = new[] { B(3, 1, 2), B(4, 2, 3), B(5, 3, 4) };
        Assert.Equal(new[] { 2d, -.5, 0 }, Check(bars, 1, 1, 2));
        Assert.Equal(16, Check(bars.Take(1).ToArray(), 4, 1, 2)[0]);
        Assert.All(Check(bars, 1, 1, 1), value => Assert.Equal(0, value));
        Assert.All(Check(Enumerable.Repeat(B(0, 0, 0), 10).ToArray()), value => Assert.Equal(0, value));
    }
    [Fact]
    public void BothTrueRangeGapsUseThePreviousPrice()
    {
        var bars = new[] { B(1, -1, 0), B(12, 10, 11), B(-10, -12, -11), B(3, 1, 2), B(1, -1, 0) };
        foreach (var kind in Kinds) Check(bars, 3, 2, 2, kind.Kind, kind.Reference);
    }
    [Fact]
    public void WideSubnormalAndPedestalRangesKeepFiniteRatios()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -500), 1d, double.MaxValue / 8 })
            Check(Enumerable.Range(0, 19).Select(i => B((i % 5 + 2) * scale, (i % 5 - 2) * scale, (i % 5 - 1) * scale)).ToArray(), kind: kind.Kind, reference: kind.Reference);
        foreach (var kind in Kinds)
        {
            Check(new[] { B(double.MaxValue, -double.MaxValue, 0), B(double.MaxValue, 0, double.MaxValue), B(2, -2, 1), B(3, -1, 2), B(1, 0, 1), B(2 * double.Epsilon, 0, double.Epsilon), B(4, -2, 3) }, kind: kind.Kind, reference: kind.Reference);
            var pedestal = Math.Pow(2, 900); var step = Math.Pow(2, 849);
            Check(Enumerable.Range(0, 19).Select(i => B(pedestal + (i % 5 + 3) * step, pedestal + (i % 5 - 3) * step, pedestal + (i % 5) * step)).ToArray(), kind: kind.Kind, reference: kind.Reference);
        }
    }
    [Fact]
    public void MaximumAndNormalizedPeriodsUseLazyHistories()
    {
        var bars = new[] { B(3, 1, 2), B(5, -2, 4), B(1, -4, -3), B(2, 0, 1) };
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<Bar>(), period, period, period); Check(bars, period, 2, 2); Check(bars, 2, period, 2); foreach (var kind in Kinds) Check(bars, 2, 2, period, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void ResetPreviewAndLongHistoryRetainEveryStage()
    {
        var bars = Enumerable.Range(0, 180).Select(i => B(i % 11 + 2, i % 7 - 8, i % 5 - 1)).ToArray();
        foreach (var kind in Kinds) Check(bars, 7, 5, 3, kind.Kind, kind.Reference);
    }
    [Fact]
    public void SelectedRangesAndThreeCallbackSlotsRemainExplicit()
    {
        var bars = Enumerable.Range(0, 12).Select(i => B(i + 2, i - 1, i + 1)).ToArray(); var selected = Enumerable.Range(0, 12).Select(i => (double)(i % 5 - 8)).ToArray();
        Check(bars, selected: selected);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); var requests = new List<(double[] Values, int Period)>();
        var atr = Enumerable.Repeat(2d, bars.Length).ToArray(); var peak = Enumerable.Range(0, bars.Length).Select(i => (double)(i + 10)).ToArray(); var signal = peak.Select(x => x - 3).ToArray();
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return requests.Count == 1 ? atr : requests.Count == 2 ? peak : signal; };
        using var armed = ComponentAverage.Arm(new[] { callback, callback, callback });
        using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeKaseConvergenceDivergenceFast(data, context, 4, 2, 3);
        Assert.All(result.ToArray(), v => Assert.Equal(3, v)); Assert.Equal(new[] { 4, 2, 3 }, requests.Select(x => x.Period)); Assert.Equal(peak, requests[2].Values); Assert.Equal(selected, data.ChainedValues);
        var high = selected.Select((v, i) => Math.Max(v, selected[Math.Max(0, i - 1)])).ToArray(); var low = selected.Select((v, i) => Math.Min(v, selected[Math.Max(0, i - 1)])).ToArray();
        Assert.Equal(high.Zip(low, (h, l) => h - l), requests[0].Values);
        Assert.Equal(Enumerable.Range(0, bars.Length).Select(i => high[i] + low[i] - (i < 4 ? 0 : high[i - 4] + low[i - 4])), requests[1].Values);
    }
    [Fact]
    public void RejectedCandlesCannotAdvanceStages()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new KaseConvergenceDivergenceState(length1: 3, length2: 2, length3: 2); using var control = new KaseConvergenceDivergenceState(length1: 3, length2: 2, length3: 2);
            state.Update(Native(B(4, 0, 3)), true, false); control.Update(Native(B(4, 0, 3)), true, false);
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in new[] { B(5, 1, 2), B(7, -2, 1), B(3, -1, 0) }) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
    [Fact]
    public void LegacyAverageKeepsItsPublicSmoothingConvention()
    {
        var bars = Enumerable.Range(0, 17).Select(i => B(i % 5 + 2, i % 5 - 2, i % 5 - 1)).ToArray();
        foreach (var kind in new[] { MovingAvgType.LinearWeightedMovingAverage, MovingAvgType.DoubleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateKaseConvergenceDivergence(kind, 3, 2, 3); using var context = new ComputeContext();
            using var fast = IndicatorCompute.ComputeKaseConvergenceDivergenceFast(Data(bars), context, 3, 2, 3, kind); Assert.Equal(batch.ChainedValues, fast.ToArray());
        }
        Assert.Throws<NotSupportedException>(() => new KaseConvergenceDivergenceState(MovingAvgType.LinearWeightedMovingAverage, 3, 2, 3));
    }
}
