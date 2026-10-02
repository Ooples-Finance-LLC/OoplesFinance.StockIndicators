using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolatilityQualityNumericalTests
{
    private static readonly string[] Keys = { "Vqi", "FastSignal", "SlowSignal" };
    private static Bar B(double open, double high, double low, double close, int i = 0)
        => new(DateTime.UnixEpoch.AddMinutes(i), open, high, low, close, 1);
    private static Bar[] Hands => new[] { B(0, 2, 0, 2), B(2, 4, 1, 3, 1), B(1, 3, 3, 3, 2) };
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VQI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolatilityQualityIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void ThreeOutputsMatchIndependentCombinedFractions(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.VolatilityQualityOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedPricesRetainCandleFieldsAndLocalRangeRule(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int fast = 2, int slow = 3)
    {
        var expected = BuiltInFormulaReferences.VolatilityQualityValues(bars, kind, fast, slow);
        var data = Data(bars).CalculateVolatilityQualityIndex(kind, fast, slow);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Outputs["Vqi"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext();
        foreach (var key in Keys.Concat(new string[] { null! }))
        {
            using var output = IndicatorCompute.ComputeVolatilityQualityIndexFast(Data(bars), context, fast, slow, kind, key);
            Assert.Equal(expected.Outputs[key ?? "Vqi"], output.ToArray());
        }
        using var state = new VolatilityQualityIndexState(kind, fast, slow);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(B(1, 5, -2, 3)), true, false); state.Update(Native(B(3, 6, -4, -2)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(-3, 8, -9, 5)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Vqi"][i], point.Value);
                    foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandQualityCarryAndExactMarginTieDetermineSignals()
    {
        // Quality=[1/2,1/3,1/3], contributions=[1/2,1/3,1/3]; the flat third candle carries quality.
        var result = Check(Hands);
        Assert.Equal(new[] { .5, 5d / 6, 7d / 6 }, result.Outputs["Vqi"]);
        Assert.Equal(new[] { 0d, 2d / 3, 1 }, result.Outputs["FastSignal"]);
        Assert.Equal(new[] { 0d, 0, 5d / 6 }, result.Outputs["SlowSignal"]);
        // Both final margins are exactly1/6; subtracting published outputs creates a false StrongBuy.
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.Buy }, result.Signals);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void StandardMeansRetainExtendedTotalsAndSubnormalContributions(MovingAvgType kind)
    {
        var max = double.MaxValue;
        var result = Check(new[] { B(-max, max, -max, max), B(max, max, -max, -max), B(-max, max, -max, max), B(0, max, -max, 0) }, kind);
        Assert.Equal(max / 2, result.Outputs["Vqi"][0]); Assert.Equal(double.NegativeInfinity, result.Outputs["Vqi"][1]);
        Assert.Equal(max / 2, result.Outputs["Vqi"][2]);
        Check(Hands.Select(b => B(b.Open * double.Epsilon, b.High * double.Epsilon, b.Low * double.Epsilon, b.Close * double.Epsilon)).ToArray(), kind);
        Check(Enumerable.Range(0, 12).Select(i => B(i % 3 - 1, 4, -4, i * 5 % 7 - 3, i)).ToArray(), kind, 3, 4);
    }
    [Fact]
    public void ZeroRangeCarryIncludesFirstBarAndGaps()
    {
        Assert.Equal(new[] { 0d, 0, 0 }, Check(new[] { B(0, 0, 0, 0), B(0, 1, 1, 1), B(1, -1, -1, -1) }).Outputs["Vqi"]);
        Check(new[] { B(0, 2, 0, 2), B(0, 3, 3, 3), B(3, 1, 1, 1), B(1, 4, -2, 0) });
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsAllocateOnlyObservedHistory(int length)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), kind, length, length); Check(Hands, kind, length, length); }
    }
    [Fact]
    public void FastSignalsConsumeOneCallbackAndBatchBypassesRegisteredCallbacks()
    {
        foreach (var key in Keys)
        {
            using var armed = ComponentAverage.Arm((values, period) =>
            { Assert.Equal(key == "FastSignal" ? 2 : 3, period); Assert.Equal(new[] { .5, 5d / 6, 7d / 6 }, values); return new[] { .25 }; });
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeVolatilityQualityIndexFast(Data(Hands), context, 2, 3, outputKey: key);
            Assert.Equal(key == "Vqi" ? new[] { .5, 5d / 6, 7d / 6 } : new[] { .25, 0, 0 }, output.ToArray());
            Assert.Equal(key == "Vqi" ? 0 : 1, ComponentAverage.Requests); Assert.Equal(ComponentAverage.Requests, ComponentAverage.Substitutions);
        }
        using (ComponentAverage.Arm((v, p) => throw new InvalidOperationException("Batch callbacks must be bypassed")))
        { Data(Hands).CalculateVolatilityQualityIndex(fastLength: 2, slowLength: 3); Assert.Equal(0, ComponentAverage.Requests); }
    }
    [Fact]
    public void DirectSelectedInputsUsePerBarRangesAndOriginalOpen()
    {
        var bars = Hands; var selected = new[] { 20d, -10, 5 }; var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.VolatilityQualityValues(projected, fast: 2, slow: 3, selected: true);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var key in Keys)
        {
            using var output = IndicatorCompute.ComputeVolatilityQualityIndexFast(data, context, 2, 3, outputKey: key);
            Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(selected, data.ChainedValues);
        }
        data.CalculateVolatilityQualityIndex(fastLength: 2, slowLength: 3);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(bars.Select(b => b.Open), data.OpenPrices);
    }
    [Fact]
    public void NativeSelectedPricesUseSyntheticRangesAndPreview()
    {
        var bars = Hands; var prices = new[] { 20d, -10, 5 };
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.VolatilityQualityValues(projected, fast: 2, slow: 3, selected: true);
        using var state = new VolatilityQualityIndexState(fastLength: 2, slowLength: 3);
        ((ICustomInputConsumer)state).ReadCloseAsInput();
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < projected.Length; i++)
            {
                state.Update(Native(B(1, 2, 0, 50)), false, false);
                foreach (var final in new[] { false, true })
                {
                    var point = state.Update(Native(projected[i]), final, true);
                    foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceQualityOrMeans()
    {
        using var state = new VolatilityQualityIndexState(fastLength: 2, slowLength: 3); using var control = new VolatilityQualityIndexState(fastLength: 2, slowLength: 3);
        state.Update(Native(Hands[0]), true, true); control.Update(Native(Hands[0]), true, true);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5))
        {
            var fields = new[] { 1d, 1, 1, 1, 1 }; fields[field] = bad;
            var bar = new Bar(DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4]);
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), true, true));
            var data = Data(new[] { bar }); data.SetCustomValues(new List<double> { 10 }); using var context = new ComputeContext();
            Assert.ThrowsAny<ArgumentException>(() => data.CalculateVolatilityQualityIndex());
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVolatilityQualityIndexFast(data, context));
        }
        Assert.Equal(control.Update(Native(Hands[1]), true, true).Value, state.Update(Native(Hands[1]), true, true).Value);
    }
}
