using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SellGravitationNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RWI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SellGravitationIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentProduct(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.SellGravitationOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Bar Candle(double open, double high, double low, double close) => new(DateTime.UnixEpoch, open, high, low, close, 1);
    private static Bar[] HandBars() => new[] { Candle(0, 2, 0, 2), Candle(1, 3, 1, 1), Candle(3, 3, 1, 1), Candle(1, 3, 1, 2) };
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.SellGravitationValues(bars, length, kind);
        var batch = Data(bars).CalculateSellGravitationIndex(kind, length);
        foreach (var entry in expected.Outputs) Assert.Equal(entry.Value, batch.OutputValues[entry.Key]);
        Assert.Equal(expected.Outputs["Sgi"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in expected.Outputs.Keys)
        {
            using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeSellGravitationIndexFast(Data(bars), context, length, kind, key);
            Assert.Equal(expected.Outputs[key], actual.ToArray());
        }
        using var state = new SellGravitationIndexState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1, 4, -2, 2)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    foreach (var entry in expected.Outputs) Assert.Equal(entry.Value[i], point.Outputs![entry.Key]);
                    Assert.Equal(expected.Outputs["Sgi"][i], point.Value);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandBodyRatioAndTwoAverages()
    {
        var result = Check(HandBars());
        Assert.Equal(new[] { 0d, .5, -.5, -.25 }, result.Outputs["Sgi"]);
        Assert.Equal(new[] { 0d, .25, 0, -.375 }, result.Outputs["Signal"]);
    }
    [Fact]
    public void CompleteRatiosPreserveExtremeAndSubnormalInputs()
    {
        foreach (var kind in Kinds)
        {
            var ordinary = Check(HandBars(), kind: kind);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1020) })
            {
                var scaled = Check(HandBars().Select(b => Candle(b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale)).ToArray(), kind: kind);
                foreach (var key in ordinary.Outputs.Keys) Assert.Equal(ordinary.Outputs[key], scaled.Outputs[key]);
            }
            var m = double.MaxValue;
            Check(new[] { Candle(-m, m, -m, m), Candle(m, m, -m, -m), Candle(0, m, -m, m), Candle(0, m, double.Epsilon, 1) }, kind: kind);
            Check(new[] { Candle(1, Math.BitIncrement(1), 1, Math.BitIncrement(1)), Candle(Math.BitIncrement(1), Math.BitIncrement(1), 1, 1), Candle(1, 1, Math.BitDecrement(1), Math.BitDecrement(1)) }, kind: kind);
        }
    }
    [Fact]
    public void OverflowingFirstComponentsRetainFiniteSignals()
    {
        var p = Math.Pow(2, 1023);
        var result = Check(new[] { Candle(0, .25, 0, 0), Candle(0, .25, 0, p), Candle(0, .25, 0, -p), Candle(0, .25, 0, 0) });
        Assert.Equal(new[] { 0d, double.PositiveInfinity, 0, double.NegativeInfinity }, result.Outputs["Sgi"]);
        Assert.Equal(new[] { 0d, p, p, -p }, result.Outputs["Signal"]);
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndNormalizeConsistently()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue }) foreach (var kind in Kinds)
        { Check(Array.Empty<Bar>(), period, kind); Check(HandBars(), period, kind); }
    }
    [Fact]
    public void FlatCandlesAndZeroBodiesHaveExplicitZeroRatios()
    {
        foreach (var kind in Kinds)
        {
            Assert.All(Check(new[] { Candle(0, 1, 1, 2), Candle(2, 0, 0, 0), Candle(1, 2, 2, 3) }, kind: kind).Outputs["Sgi"], v => Assert.Equal(0, v));
            Assert.All(Check(HandBars().Select(b => Candle(b.Close, b.High, b.Low, b.Close)).ToArray(), kind: kind).Outputs["Sgi"], v => Assert.Equal(0, v));
        }
    }
    [Fact]
    public void ExpiryAndRecursiveTransitionsPreservePreviewReset()
    {
        foreach (var kind in Kinds) Check(Enumerable.Range(0, 70).Select(i => Candle(i % 3, 12 + i % 7, -3 - i % 4, i % 11 - 2)).ToArray(), 3, kind);
    }
    [Fact]
    public void CallbackGraphConsumesOnlyRequestedStages()
    {
        foreach (var key in new[] { "Sgi", "Signal" })
        {
            var calls = new List<double[]>(); var first = new[] { 2d, 1, -2, 0 };
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
            { Assert.Equal(2, period); calls.Add(values.ToArray()); return calls.Count == 1 ? first : values.Select(v => v / 2).ToArray(); };
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 2).ToArray()); using var context = new ComputeContext();
            var data = Data(HandBars()); var prior = data.ChainedValues.ToArray();
            using var output = IndicatorCompute.ComputeSellGravitationIndexFast(data, context, 2, outputKey: key);
            Assert.Equal(key == "Signal" ? 2 : 1, ComponentAverage.Substitutions); Assert.Equal(new[] { 1d, 0, -1, .5 }, calls[0]);
            if (key == "Signal") Assert.Equal(first, calls[1]);
            Assert.Equal(key == "Signal" ? new[] { 1d, .5, -1, 0 } : first, output.ToArray()); Assert.Equal(prior, data.ChainedValues);
        }
    }
    [Fact]
    public async Task GeneratedApiPreservesBothAverageSlots()
    {
        var indicator = new SellGravitationIndex(2, new Sma(2), new Wma(2));
        using var run = await new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(HandBars())).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(2, indicator.Outputs.Count);
        Assert.Equal(new[] { 0d, .5, -.5, -.25 }, run[indicator.Outputs[0]].ToArray());
        Assert.Equal(new[] { 0d, 1d / 3, -1d / 6, -1d / 3 }, run[indicator.Outputs[1]].ToArray());
    }
    [Fact]
    public void BatchDoesNotConsumeFastOverrideSlots()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Batch must not consume fast slots.");
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 2).ToArray());
        var actual = Data(HandBars()).CalculateSellGravitationIndex(MovingAvgType.SimpleMovingAverage, 2);
        Assert.Equal(new[] { 0d, .5, -.5, -.25 }, actual.CustomValuesList); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void ExplicitFastSelectedPricesPreserveOriginalCandleFields()
    {
        var bars = Enumerable.Range(0, 30).Select(i => Candle(i % 3, 12 + i % 7, -3 - i % 4, i % 5)).ToArray(); var selected = bars.Select((_, i) => 2d + i % 11).ToArray();
        var expected = BuiltInFormulaReferences.SellGravitationValues(bars.Select((b, i) => Candle(b.Open, b.High, b.Low, selected[i])).ToArray(), 3, MovingAvgType.ExponentialMovingAverage);
        foreach (var key in expected.Outputs.Keys)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeSellGravitationIndexFast(data, context, 3, outputKey: key);
            Assert.Equal(expected.Outputs[key], actual.ToArray()); Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.Open), data.OpenPrices); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        }
    }
    [Fact]
    public async Task SelectedSmoothingOutsideCandlePreservesOriginalFields()
    {
        var source = new Sma(3); var indicator = new SellGravitationIndex(2).Of(source);
        var bars = Enumerable.Range(0, 24).Select(i =>
        { var price = i % 2 == 0 ? 2d : 100; return new Bar(DateTime.UnixEpoch.AddMinutes(i), price - .5, price + 1 + i % 3, price - 1, price, 1); }).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        Assert.Contains(projected, b => b.Close < b.Low || b.Close > b.High);
        var expected = BuiltInFormulaReferences.SellGravitationValues(projected, 2, MovingAvgType.ExponentialMovingAverage).Outputs;
        Assert.Equal(expected["Sgi"], run[indicator.Outputs[0]].ToArray()); Assert.Equal(expected["Signal"], run[indicator.Outputs[1]].ToArray());
        var feed = OoplesFinance.StockIndicators.Indicators.Bars.Live();
        using var live = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup().ConfigureIndicators(source, indicator).BuildAsync();
        foreach (var bar in bars) feed.Publish(bar); feed.Complete(); var line = new List<double>(); var signal = new List<double>();
        await foreach (var snapshot in live) { line.Add(snapshot[indicator.Outputs[0]]); signal.Add(snapshot[indicator.Outputs[1]]); }
        Assert.Equal(expected["Sgi"], line); Assert.Equal(expected["Signal"], signal);
    }
    [Fact]
    public void InvalidBarsCannotAdvanceEitherAverage()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new SellGravitationIndexState(length: 2); using var control = new SellGravitationIndexState(length: 2);
            foreach (var b in HandBars()) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 4, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in HandBars())
            { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
