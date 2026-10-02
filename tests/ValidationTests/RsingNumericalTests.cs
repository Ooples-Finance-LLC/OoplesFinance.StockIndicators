using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RsingNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RWI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(RSINGIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentProduct(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RsingOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Bar Candle(double high, double low, double close, double volume = 1) => new(DateTime.UnixEpoch, close, high, low, close, volume);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 2, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        var expected = BuiltInFormulaReferences.RsingValues(bars, length, kind);
        var batch = Data(bars).CalculateRSINGIndicator(kind, length);
        foreach (var entry in expected.Outputs) Assert.Equal(entry.Value, batch.OutputValues[entry.Key]);
        Assert.Equal(expected.Outputs["Rsing"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in expected.Outputs.Keys)
        {
            using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeRSINGIndicatorFast(Data(bars), context, length, kind, key);
            Assert.Equal(expected.Outputs[key], actual.ToArray());
        }
        using var state = new RSINGIndicatorState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(4, -2, 1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue, -double.MaxValue, 0)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    foreach (var entry in expected.Outputs) Assert.Equal(entry.Value[i], point.Outputs![entry.Key]);
                    Assert.Equal(expected.Outputs["Rsing"][i], point.Value);
                }
            }
        }
        return expected;
    }
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static Bar[] HandBars() => new[] { Candle(1, 0, 1, 1), Candle(2, 0, 2, 2), Candle(4, 0, 4, 1), Candle(3, 1, 3, 3) };
    [Fact]
    public void HandVolumeRangeAndLagProduct()
    {
        var result = Check(HandBars());
        Assert.Equal(new[] { 0d, 0, 9, 18d / 7 }, result.Outputs["Rsing"]);
        Assert.Equal(new[] { 0d, 0, 6, 33d / 7 }, result.Outputs["Signal"]);
    }
    [Fact]
    public void CompleteProductsPreserveExtremeAndSubnormalInputs()
    {
        foreach (var kind in Kinds)
        {
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1020) })
                Check(HandBars().Select(b => Candle(b.High * scale, b.Low * scale, b.Close * scale, b.Volume * scale)).ToArray(), kind: kind);
            var m = double.MaxValue;
            Check(new[] { Candle(m, -m, -m, m), Candle(m, 0, m, m), Candle(m, -m, 0, m), Candle(0, -m, -m, m), Candle(m, -m, m, m) }, kind: kind);
            // An exact tiny change in a very wide range must survive both
            // subtraction and several extended-exponent normalization steps.
            Check(new[] { Candle(m, -m, -m), Candle(m, 0, 0), Candle(m, double.Epsilon, m) }, kind: kind);
            Check(new[] { Candle(1, 0, 0), Candle(Math.BitIncrement(1), 0, 1), Candle(1, 0, 2), Candle(Math.BitDecrement(1), 0, 1) }, kind: kind);
        }
    }
    [Fact]
    public void OverflowingOscillatorComponentsCanHaveFiniteSignalAndCancel()
    {
        var p = Math.Pow(2, 1023); var a = Math.Pow(2, 970);
        var result = Check(new[] { Candle(2 * a, 0, 0, 5), Candle(a, 0, 0, 5), Candle(p + 2 * a, p - a, p, 5), Candle(-p + 2 * a, -p - 4 * a, -p, 3) }, kind: MovingAvgType.SimpleMovingAverage);
        Assert.Equal(new[] { 0d, 0, double.PositiveInfinity, double.NegativeInfinity }, result.Outputs["Rsing"]);
        Assert.Equal(new[] { 0d, 0, 1.5 * p, 0 }, result.Outputs["Signal"]);
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndNormalizeConsistently()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue }) foreach (var kind in Kinds)
        { Check(Array.Empty<Bar>(), period, kind); Check(HandBars(), period, kind); }
    }
    [Fact]
    public void ZeroVarianceAndZeroVolumeHaveZeroProducts()
    {
        foreach (var kind in Kinds)
        {
            Assert.All(Check(Enumerable.Range(0, 12).Select(i => Candle(i + 1, i, i, 1)).ToArray(), kind: kind).Outputs["Rsing"], v => Assert.Equal(0, v));
            Assert.All(Check(HandBars().Select(b => Candle(b.High, b.Low, b.Close, 0)).ToArray(), kind: kind).Outputs["Rsing"], v => Assert.Equal(0, v));
            Assert.All(Check(HandBars(), 1, kind).Outputs["Rsing"], v => Assert.Equal(0, v));
        }
    }
    [Fact]
    public void ExpiryAndRecursiveTransitionsPreservePreviewReset()
    {
        foreach (var kind in Kinds) Check(Enumerable.Range(0, 70).Select(i => Candle(12 + i % 7, -3 - i % 4, i % 11 - 2, i % 9)).ToArray(), 3, kind);
    }
    [Fact]
    public void CallbackGraphUsesVolumeThenOscillator()
    {
        foreach (var key in new[] { "Rsing", "Signal" })
        {
            var calls = new List<double[]>();
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
            { Assert.Equal(2, period); calls.Add(values.ToArray()); return calls.Count == 1 ? Enumerable.Repeat(2d, values.Count).ToArray() : values.Select(v => v + 1).ToArray(); };
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 2).ToArray()); using var context = new ComputeContext();
            var data = Data(HandBars()); var prior = data.ChainedValues.ToArray();
            using var output = IndicatorCompute.ComputeRSINGIndicatorFast(data, context, 2, outputKey: key);
            Assert.Equal(2, ComponentAverage.Substitutions); Assert.Equal(new[] { 1d, 2, 1, 3 }, calls[0]); Assert.Equal(new[] { 0d, 0, 6, 3 }, calls[1]);
            Assert.Equal(key == "Signal" ? new[] { 1d, 1, 7, 4 } : calls[1], output.ToArray()); Assert.Equal(prior, data.ChainedValues);
        }
    }
    [Fact]
    public void BatchDoesNotConsumeFastOverrideSlots()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Batch must not consume fast slots.");
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 2).ToArray());
        var actual = Data(HandBars()).CalculateRSINGIndicator(length: 2);
        Assert.Equal(new[] { 0d, 0, 9, 18d / 7 }, actual.CustomValuesList); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void ExplicitFastSelectedPricesPreserveCandleRanges()
    {
        var bars = Enumerable.Range(0, 30).Select(i => Candle(12 + i % 7, -3 - i % 4, i % 5, 1 + i % 3)).ToArray(); var selected = bars.Select((_, i) => 2d + i % 11).ToArray();
        var expected = BuiltInFormulaReferences.RsingValues(bars.Select((b, i) => Candle(b.High, b.Low, selected[i], b.Volume)).ToArray(), 3, MovingAvgType.WeightedMovingAverage);
        foreach (var key in expected.Outputs.Keys)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeRSINGIndicatorFast(data, context, 3, outputKey: key);
            Assert.Equal(expected.Outputs[key], actual.ToArray()); Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        }
    }
    [Fact]
    public void InvalidBarsCannotAdvanceState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new RSINGIndicatorState(); using var control = new RSINGIndicatorState();
            foreach (var b in Bars(1, 3, -1)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 3, -1, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(0, 3, -2)) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
}
