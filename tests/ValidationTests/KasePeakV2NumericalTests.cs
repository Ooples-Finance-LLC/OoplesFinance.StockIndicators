using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KasePeakV2NumericalTests
{
    private static Bar B(double high, double low, double close) => new(DateTime.UnixEpoch, close, high, low, close, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KPO2", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KasePeakOscillatorV2)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLogWindows(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KasePeakV2Outputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int fast = 1, int slow = 5, int deviation = 2, int average = 2, int smooth = 2, double sensitivity = 40,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1, double[]? selected = null)
    {
        var projected = selected is null ? bars : bars.Select((b, i) => B(b.High, b.Low, selected[i])).ToArray();
        var expected = BuiltInFormulaReferences.KasePeakV2Values(projected, fast, slow, deviation, average, smooth, sensitivity, reference, selected is not null);
        var data = Data(bars); if (selected is not null) data.SetCustomValues(selected.ToList()); var batch = data.CalculateKasePeakOscillatorV2(kind, fast, slow, deviation, average, smoothLength: smooth, sensitivity: sensitivity);
        Assert.Equal(expected.Values, batch.ChainedValues); Assert.Equal(expected.Values, batch.OutputValues["Kpo"]); Assert.Equal(expected.Trades, batch.SignalsList);
        var raw = Data(bars); if (selected is not null) raw.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var computed = IndicatorCompute.ComputeKasePeakOscillatorV2Fast(raw, context, average, kind, fast, slow, deviation, smooth, sensitivity);
        Assert.Equal(expected.Values, computed.ToArray()); if (selected is not null) Assert.Equal(selected, raw.ChainedValues);
        using var state = new KasePeakOscillatorV2State(kind, fast, slow, deviation, average, smoothLength: smooth, sensitivity: sensitivity); using var window = new KasePeakV2Window(kind, fast, slow, deviation, average, smooth, sensitivity);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(B(99, 1, 7)), true, false); window.Next(99, 1, 7, true); state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(99, 1, 7)), false, false); window.Next(99, 1, 7, false);
                var b = projected[i]; var high = b.High; var low = b.Low;
                if (selected is not null && !(b.Close >= low && b.Close <= high)) { high = Math.Max(b.Close, projected[Math.Max(0, i - 1)].Close); low = Math.Min(b.Close, projected[Math.Max(0, i - 1)].Close); }
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(B(high, low, b.Close)), final, true); var direct = window.Next(high, low, b.Close, final);
                    Assert.Equal(expected.Values[i], point.Value); Assert.Equal(point.Value, direct.Value); Assert.Equal(point.Value, point.Outputs!["Kpo"]); Assert.Equal(expected.Trades[i], direct.Trade);
                }
            }
        }
        return expected.Values;
    }
    [Fact]
    public void HandGenuineReturnWindowAndPartialMeansHaveKnownValues()
    {
        var bars = new[] { 1d, 2, 1, 2 }.Select(v => B(v, v, v)).ToArray();
        Assert.Equal(new[] { 0d, 0, -1, 1 }, Check(bars, 1, 2, 2, 1, 1, 1));
        Assert.Equal(new[] { 0d, 0, -.5, 0 }, Check(bars, 1, 2, 2, 1, 2, 1));
        Assert.All(Check(bars, deviation: 1), v => Assert.Equal(0, v));
        Assert.All(Check(bars, 2, 2), v => Assert.Equal(0, v)); Assert.All(Check(bars, 5, 2), v => Assert.Equal(0, v));
    }
    [Fact]
    public void LogContractAgreesWithIndependentRationalTruth()
    {
        var next = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(1d) + 1);
        var pairs = new[] { (1d, 1d), (next, 1d), (1d, next), (1.5, 1d), (1d, 1.5), (3d, 7d), (7d, 3d), (double.MaxValue, double.Epsilon), (double.Epsilon, double.MaxValue), (-7d, -3d), (0d, 2d), (-2d, 2d) };
        foreach (var (a, b) in pairs)
        {
            var expected = a == 0 || b == 0 || Math.Sign(a) != Math.Sign(b) ? 0 : (ReferenceFraction.FromDouble(Math.Abs(a)) / ReferenceFraction.FromDouble(Math.Abs(b))).LogToDouble();
            var actual = KasePeakV2Window.Log(a, b); Assert.True(BuiltInFormulaReferences.LogReturnsBudget.Accepts(expected, actual), $"log({a:R}/{b:R}): {expected:R} versus {actual:R}");
            Assert.Equal(BuiltInFormulaReferences.KasePeakV2LogReference(a, b), actual);
        }
    }
    [Fact]
    public void WideTinySignedAndAdjacentPricesRetainVolatility()
    {
        var basis = Enumerable.Range(0, 19).Select(i => B(i % 5 + 3, i % 5 + 1, i % 5 + 2)).ToArray();
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -500), 1d, double.MaxValue / 8 })
            Check(basis.Select(b => B(b.High * scale, b.Low * scale, b.Close * scale)).ToArray(), kind: kind.Kind, reference: kind.Reference);
        foreach (var kind in Kinds) Check(new[] { B(double.MaxValue, double.MaxValue / 2, double.MaxValue / 2), B(4 * double.Epsilon, double.Epsilon, 2 * double.Epsilon), B(8, 2, 4), B(-1, -4, -2), B(-2, -8, -4), B(0, 0, 0), B(3, 1, 2) }, kind: kind.Kind, reference: kind.Reference);
        var step = Math.Pow(2, -52); Check(Enumerable.Range(0, 18).Select(i => B(1 + (i % 5 + 2) * step, 1 + (i % 5) * step, 1 + (i % 5 + 1) * step)).ToArray());
    }
    [Fact]
    public void SensitivityOverflowRecoversAndSignalsRemainDefined()
    {
        var step = Math.Pow(2, -52); var bars = new[] { B(2, .5, 1), B(4, .5, 1 + step), B(8, .5, 1 + 2 * step), B(1, 1, 1), B(1, 1, 1), B(1, 1, 1), B(1, 1, 1) };
        var values = Check(bars, 1, 3, 2, 1, 1, double.MaxValue); Assert.True(double.IsInfinity(values[2])); Assert.Equal(0, values[^1]);
        Check(bars, 1, 3, 2, 1, 1, -double.MaxValue); Assert.All(Check(bars, sensitivity: 0), v => Assert.Equal(0, v));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var input in new[] { Array.Empty<Bar>(), bars })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(input).CalculateKasePeakOscillatorV2(sensitivity: bad));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeKasePeakOscillatorV2Fast(Data(input), context, sensitivity: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KasePeakOscillatorV2State(sensitivity: bad));
        }
    }
    [Fact]
    public void ExtremePeriodsBoundWorkByAvailableHistory()
    {
        var bars = new[] { 1d, 2, 1, 2, 4 }.Select(v => B(v + 1, v, v)).ToArray();
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        {
            Check(Array.Empty<Bar>(), period, period, period, period, period); Check(bars, fast: period); Check(bars, slow: period); Check(bars, deviation: period); Check(bars, smooth: period);
            foreach (var kind in Kinds) Check(bars, average: period, kind: kind.Kind, reference: kind.Reference);
        }
    }
    [Fact]
    public void HistoryCompactionResetAndPreviewsPreserveEveryLag()
    {
        var bars = Enumerable.Range(0, 45).Select(i => B(i % 11 + 3, i % 7 + 1, i % 5 + 2)).ToArray();
        foreach (var kind in Kinds) Check(bars, kind: kind.Kind, reference: kind.Reference);
    }
    [Fact]
    public void LongHistoryCompactionPreservesEveryAvailableLag()
    {
        var bars = Enumerable.Range(0, 1200).Select(i => B(i % 11 + 3, i % 7 + 1, i % 5 + 2)).ToArray(); Check(bars, 2, 8, 3, 4, 3);
    }
    [Fact]
    public void SelectedRangesAndDeviationCallbackRemainExplicit()
    {
        var bars = Enumerable.Range(0, 15).Select(i => B(i + 2, i - 1, i + 1)).ToArray(); var selected = Enumerable.Range(0, 15).Select(i => (double)(i % 5 - 8)).ToArray(); Check(bars, selected: selected);
        var projected = bars.Select((b, i) => B(b.High, b.Low, selected[i])).ToArray(); var average = Enumerable.Repeat(.5, bars.Length).ToArray();
        var expected = BuiltInFormulaReferences.KasePeakV2Values(projected, 1, 5, 2, 4, 2, 40, 1, true, average); var data = Data(bars); data.SetCustomValues(selected.ToList()); var count = 0;
        using var armed = ComponentAverage.Arm((values, period) => { count++; Assert.Equal(4, period); Assert.Equal(expected.Deviation, values); return average; }); using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeKasePeakOscillatorV2Fast(data, context, 4, fastLength: 1, slowLength: 5, length1: 2, smoothLength: 2); Assert.Equal(expected.Values, result.ToArray()); Assert.Equal(1, count); Assert.Equal(selected, data.ChainedValues);
    }
    [Fact]
    public void RejectedCandlesCannotAdvanceReturnOrPressureWindows()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new KasePeakOscillatorV2State(fastLength: 1, slowLength: 3, length1: 2, length2: 1); using var control = new KasePeakOscillatorV2State(fastLength: 1, slowLength: 3, length1: 2, length2: 1);
            state.Update(Native(B(4, 1, 2)), true, false); control.Update(Native(B(4, 1, 2)), true, false); var v = new[] { 2d, 4, 1, 2, 1 }; v[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in new[] { B(5, 1, 2), B(7, 2, 4), B(3, 1, 1) }) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
    [Fact]
    public void LegacyMeansAndDiscardedPeakParametersKeepTheirContract()
    {
        var bars = Enumerable.Range(0, 30).Select(i => B(i % 5 + 3, i % 5 + 1, i % 5 + 2)).ToArray();
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var expected = Data(bars).CalculateKasePeakOscillatorV2(kind, 1, 5, 2, 3); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeKasePeakOscillatorV2Fast(Data(bars), context, 3, kind, 1, 5, 2);
            Assert.Equal(expected.ChainedValues, fast.ToArray()); using var state = new KasePeakOscillatorV2State(kind, 1, 5, 2, 3);
            for (var i = 0; i < bars.Length; i++) Assert.Equal(expected.ChainedValues[i], state.Update(Native(bars[i]), true, false).Value);
        }
        var baseline = Data(bars).CalculateKasePeakOscillatorV2(fastLength: 1, slowLength: 5, length1: 2, length2: 3).ChainedValues.ToArray();
        foreach (var unused in new[] { double.NaN, double.PositiveInfinity, -2d }) Assert.Equal(baseline, Data(bars).CalculateKasePeakOscillatorV2(fastLength: 1, slowLength: 5, length1: 2, length2: 3, length3: int.MaxValue, devFactor: unused).ChainedValues);
    }
}
