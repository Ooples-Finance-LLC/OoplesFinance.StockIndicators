using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CommodityNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CCI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static void Equal(double expected, double actual, bool inverse) => Assert.True((inverse ? BuiltInFormulaReferences.RsiInverseFisherBudget : IndicatorErrorBudget.Exact).Accepts(expected, actual), $"Expected {expected:R}, got {actual:R}");
    private static void Equal(double[] expected, double[] actual, bool inverse) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Equal(expected[i], actual[i], inverse); }

    [Fact]
    public void AllRoutesPreserveTypicalPricesSelectedInputsAndEveryPeriod()
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, 0d, 1, 4, 2, 5, 3, 8, 1, 6, 2, 7, 0, -1, 3 };
        foreach (var bars in new[] { BarsOf(prices), BarsOf(prices).Select(b => new Bar(b.Time, 0, double.MaxValue, -double.MaxValue, b.Close, 1)).ToArray() })
        foreach (var indicator in new IBuiltInIndicator[] { new Cci(3), new WoodieCommodityChannelIndex(3, 5), new EhlersCommodityChannelIndexInverseFisherTransform(3, 4) })
        {
            var expected = BuiltInFormulaReferences.CommodityOutputs(bars, indicator); var inverse = indicator.BatchName == IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform;
            var options = indicator.CreateOptions(); Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
            using var context = new ComputeContext();
            foreach (var pair in expected)
            {
                var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, options, pair.Key);
                Equal(pair.Value, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray(), inverse);
                using var raw = IndicatorCompute.TryComputeFast(Data(bars), spec, context); Assert.NotNull(raw); Equal(pair.Value, raw.Value.ToArray(), inverse);
            }
            var primary = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, options);
            var native = StatefulIndicatorFactory.Create(primary); var live = StreamingIndicatorFactory.CreateState(primary); Assert.NotNull(live);
            using var nativeLifetime = native as IDisposable; using var liveLifetime = live as IDisposable;
            Replay(native, bars, expected, inverse); Replay(live!, bars, expected, inverse);
        }
        var expectedSelected = BuiltInFormulaReferences.CommodityValues(prices, 3, 1);
        var selected = Data(BarsOf(Enumerable.Repeat(9d, prices.Length).ToArray())); selected.CustomValuesList = prices.ToList();
        using var selectedContext = new ComputeContext(); using var selectedRaw = IndicatorCompute.ComputeCciFast(selected, selectedContext, 3);
        Equal(expectedSelected, selectedRaw.Span.ToArray(), false);
        var core = new double[prices.Length]; OscillatorCore.CommodityChannelIndex(prices, prices, prices, core, 3); Equal(expectedSelected, core, false);
        OscillatorCore.CommodityChannelIndex(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), 3);
        Assert.Throws<ArgumentException>(() => OscillatorCore.CommodityChannelIndex(Array.Empty<double>(), prices, prices, core, 3));
        Assert.Throws<ArgumentException>(() => OscillatorCore.CommodityChannelIndex(prices, prices, prices, Array.Empty<double>(), 3));
    }

    private static void Replay(IStreamingIndicatorState state, Bar[] bars, IReadOnlyDictionary<string, double[]> expected, bool inverse)
    {
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            foreach (var final in new[] { false, true })
            {
                var actual = state.Update(Native(bars[i]), final, true);
                foreach (var pair in expected) Equal(pair.Value[i], actual.Outputs![pair.Key], inverse);
            }
        }
    }

    [Fact]
    public void InvalidConstantsAreRejectedEvenForEmptyInputs()
    {
        foreach (var constant in new[] { 0d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommodityChannelIndexState(constant: constant));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersCommodityChannelIndexInverseFisherTransformState(constant: constant));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateCommodityChannelIndex(constant: constant));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeCciFast(Data(Array.Empty<Bar>()), context, constant: constant));
        }
    }

    [Fact]
    public void RawCustomerMeansAreUsedByBothWoodieComponents()
    {
        var prices = new[] { 1d, 3, 2, 4 };
        var means = new[] { 2d, 4d, 6d, 8d }.Select(v => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)
            ((values, _) => values.Select(_ => v).ToArray())).ToArray();
        using var scope = ComponentAverage.Arm(means); using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeWoodieCommodityChannelIndexFast(Data(BarsOf(prices)), context, 2, MovingAvgType.WeightedMovingAverage, 3, "Histogram");
        var expected = prices.Select(v =>
        {
            var price = ReferenceFraction.FromDouble(v); var constant = ReferenceFraction.FromDouble(.015);
            var fast = ((price - new ReferenceFraction(2)) / (constant * new ReferenceFraction(4))).ToDouble();
            var slow = ((price - new ReferenceFraction(6)) / (constant * new ReferenceFraction(8))).ToDouble();
            return (ReferenceFraction.FromDouble(fast) - ReferenceFraction.FromDouble(slow)).ToDouble();
        }).ToArray();
        Equal(expected, raw.Span.ToArray(), false); Assert.Equal(4, ComponentAverage.Substitutions);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }

    [Theory]
    [InlineData(.25)]
    [InlineData(1e-20)]
    [InlineData(-.25)]
    public async Task AllThreeInverseCustomerStagesAreHonored(double value)
    {
        var indicator = new EhlersCommodityChannelIndexInverseFisherTransform(2, 3, .015, new ConstantAverage(2), new ConstantAverage(4), new ConstantAverage(value));
        var expected = ReferenceFraction.FromDouble(value).TanhToDouble();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(new[] { 1d, 3, 2, 4 }))).ConfigureIndicators(indicator).BuildAsync();
        Assert.All(result[indicator.Outputs[0]].ToArray(), actual => Equal(expected, actual, true));
    }

    [Fact]
    public async Task WoodieRejectsAnIncompleteCustomerStageList()
    {
        var indicator = new WoodieCommodityChannelIndex(2, 3, new ConstantAverage(2));
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(new[] { 1d, 3, 2, 4 }))).ConfigureIndicators(indicator).BuildAsync();
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NativeCommodityRejectsInvalidFieldsWithoutAdvancing(int variant)
    {
        IStreamingIndicatorState Create() => variant == 0 ? new CommodityChannelIndexState(length: 3) : variant == 1 ? new WoodieCommodityChannelIndexState(fastLength: 2, slowLength: 3) : new EhlersCommodityChannelIndexInverseFisherTransformState(length: 2, signalLength: 3);
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var state = Create(); var control = Create();
            using var stateLifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var v in new[] { 3d, 1, 4 })
            {
                var seed = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, v, 1));
                state.Update(seed, true, true); control.Update(seed, true, true);
            }
            var fields = new[] { 2d, 4, 1, 2, 1 }; fields[field] = invalid;
            var bad = new OhlcvBar("GAINLOSS", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, 2, 1));
            var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "Cci", "WoodieCommodityChannelIndex", "EhlersCommodityChannelIndexInverseFisherTransform" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route);

    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);

    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);

    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
