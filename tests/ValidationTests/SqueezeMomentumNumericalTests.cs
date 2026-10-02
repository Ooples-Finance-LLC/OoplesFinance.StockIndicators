using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SqueezeMomentumNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RWI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SqueezeMomentumIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentProduct(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.SqueezeMomentumOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var source = new Sma(3); var indicator = ((SqueezeMomentumIndicator)c.Factory()).Of(source);
        var bars = Enumerable.Range(0, Math.Max(64, indicator.WarmupBars + 8)).Select(i =>
        { var price = i % 2 == 0 ? 2d : 100; return new Bar(DateTime.UnixEpoch.AddMinutes(i), price - .5, price + 1 + i % 3, price - 1, price, 1 + i % 4); }).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        Assert.Contains(projected, b => b.Close < b.Low || b.Close > b.High);
        var expected = BuiltInFormulaReferences.SqueezeMomentumOutputs(projected, (IBuiltInIndicator)c.Factory());
        var keys = new[] { "Smi" };
        for (var slot = 0; slot < keys.Length; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
        var feed = OoplesFinance.StockIndicators.Indicators.Bars.Live();
        using var live = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup().ConfigureIndicators(source, indicator).BuildAsync();
        foreach (var bar in bars) feed.Publish(bar); feed.Complete();
        var actual = indicator.Outputs.Select(_ => new List<double>()).ToArray();
        await foreach (var snapshot in live)
            for (var slot = 0; slot < actual.Length; slot++) actual[slot].Add(snapshot[indicator.Outputs[slot]]);
        for (var slot = 0; slot < actual.Length; slot++) Assert.Equal(expected[keys[slot]], actual[slot]);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.SqueezeMomentumValues(bars, length, kind);
        var batch = Data(bars).CalculateSqueezeMomentumIndicator(kind, length);
        Assert.Equal(expected.Outputs["Smi"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeSqueezeMomentumIndicatorFast(Data(bars), context, length, kind);
        Assert.Equal(expected.Outputs["Smi"], fast.ToArray());
        using var state = new SqueezeMomentumIndicatorState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(-double.MaxValue)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Smi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Smi"]); }
            }
        }
        return expected;
    }
    [Fact]
    public void HandResidualAndCenteredRegression()
    {
        var result = Check(Bars(2, 4, 2, 8));
        Assert.Equal(new[] { 1d, 2.5, -1d / 36, 35d / 18 }, result.Outputs["Smi"]);
    }
    [Fact]
    public void ExactIntermediatesPreserveExtremeAndSubnormalPrices()
    {
        foreach (var kind in Kinds)
        {
            Check(Bars(double.MaxValue, double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.MaxValue), kind: kind);
            Check(Bars(double.Epsilon, 4 * double.Epsilon, -double.Epsilon, 8 * double.Epsilon, 0, 2 * double.Epsilon), kind: kind);
            Check(Bars(1, Math.BitIncrement(1), Math.BitDecrement(1), 1, Math.BitIncrement(1)), kind: kind);
            Check(Enumerable.Range(0, 13).Select(i => new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, i % 2 == 0 ? double.Epsilon : -double.Epsilon, 1)).ToArray(), kind: kind);
        }
    }
    [Fact]
    public void PreviewResetAndWindowExpiryMatchDirectFormula()
    {
        var bars = Enumerable.Range(0, 37).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 9 + i % 7, -4 - i % 5, i % 2 == 0 ? i % 11 : -i % 7, 1)).ToArray();
        foreach (var kind in Kinds) Check(bars, 4, kind);
    }
    [Fact]
    public void HugePeriodsAllocateOnlyObservedHistory()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { 1, int.MaxValue }) Check(Bars(2, 4, 2, 8), length, kind);
    }
    [Fact]
    public void LegacyAverageFallbackRemainsConsistent()
    { Check(Bars(2, 4, 2, 8, 3, 7, 5, 1), 3, MovingAvgType.DoubleExponentialMovingAverage); }
    [Fact]
    public void ExplicitFastSelectedInputPreservesOriginalCandles()
    {
        var bars = Enumerable.Range(0, 30).Select(i => new Bar(DateTime.UnixEpoch, 1, 4 + i % 3, -2 - i % 2, 2, 1)).ToArray();
        var prices = Enumerable.Range(0, bars.Length).Select(i => i % 2 == 0 ? 100d + i : -100d - i).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.SqueezeMomentumValues(projected, 3, MovingAvgType.SimpleMovingAverage);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeSqueezeMomentumIndicatorFast(data, context, 3);
        Assert.Equal(expected.Outputs["Smi"], output.ToArray()); Assert.Equal(prices, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
    }
    [Fact]
    public void CallbackUsesOnePriceAverageAndRestoresInput()
    {
        var bars = Bars(2, 4, 2, 8); var calls = 0;
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
        { calls++; Assert.Equal(3, period); Assert.Equal(bars.Select(b => b.Close), values); return new double[values.Count]; };
        using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext(); var data = Data(bars); var before = data.ChainedValues.ToArray();
        using var output = IndicatorCompute.ComputeSqueezeMomentumIndicatorFast(data, context, 3);
        // Zero average gives residuals 1, 5/2, 1/2, 11/2; the final two endpoints are 13/12 and 13/3.
        Assert.Equal(new[] { 1d, 2.5, 13d / 12, 13d / 3 }, output.ToArray()); Assert.Equal(1, calls); Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(before, data.ChainedValues);
    }
    [Fact]
    public async Task GeneratedApiRetainsConfiguredAverage()
    {
        var indicator = new SqueezeMomentumIndicator(3, new Sma(3));
        using var run = await new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(Bars(2, 4, 2, 8))).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 1d, 2.5, -1d / 36, 35d / 18 }, run[indicator.Outputs[0]].ToArray());
    }
    [Fact]
    public void BatchDoesNotConsumeFastCallback()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Batch must not consume fast slots.");
        using var armed = ComponentAverage.Arm(new[] { callback });
        Assert.Equal(new[] { 1d, 2.5, -1d / 36, 35d / 18 }, Data(Bars(2, 4, 2, 8)).CalculateSqueezeMomentumIndicator(length: 3).CustomValuesList); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void InvalidBarsCannotAdvanceAnyStage()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new SqueezeMomentumIndicatorState(length: 3); using var control = new SqueezeMomentumIndicatorState(length: 3);
            foreach (var b in Bars(2, 4, 2, 8)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 4, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(5, 1, 6, 0)) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
