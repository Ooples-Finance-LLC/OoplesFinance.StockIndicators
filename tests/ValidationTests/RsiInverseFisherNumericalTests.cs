using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RsiInverseFisherNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), BarsOf(prices).Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("FISHER", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static void Equal(double expected, double actual) => Assert.True(BuiltInFormulaReferences.RsiInverseFisherBudget.Accepts(expected, actual), $"Expected {expected:R}, got {actual:R}");
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Equal(expected[i], actual[i]); }

    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(1d, 0.7615941559557649)]
    [InlineData(5d, 0.9999092042625951)]
    [InlineData(20d, 1d)]
    [InlineData(1e-20, 1e-20)]
    [InlineData(4.9406564584124654E-324, 4.9406564584124654E-324)]
    public void IndependentTransformMatchesKnownValuesAndOddSymmetry(double value, double expected)
    {
        Assert.Equal(expected, ReferenceFraction.FromDouble(value).TanhToDouble());
        Assert.Equal(-expected, ReferenceFraction.FromDouble(-value).TanhToDouble());
    }

    [Fact]
    public void NearZeroSignalsSurviveBatchRawCoreAndNativeTransforms()
    {
        var prices = new[] { 0d, 1, 0, Math.BitIncrement(1d) }; var bars = BarsOf(prices);
        var indicator = new EhlersRelativeStrengthIndexInverseFisherTransform(2, 1, new Sma(2), new Sma(2), new Sma(1));
        var expected = BuiltInFormulaReferences.RsiInverseFisherOutputs(bars, indicator)["Eiftrsi"];
        Assert.InRange(expected[^1], double.Epsilon, 1e-12);
        Equal(expected, Data(prices).CalculateEhlersRelativeStrengthIndexInverseFisherTransform(MovingAvgType.SimpleMovingAverage, 2, 1).CustomValuesList.ToArray());
        Equal(expected, Data(prices).CalculateEhlersInverseFisherTransform(MovingAvgType.SimpleMovingAverage, 2, 1).CustomValuesList.ToArray());
        using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeEhlersInverseFisherTransformFast(Data(prices), context, 2, 1, MovingAvgType.SimpleMovingAverage); Equal(expected, raw.Span.ToArray());
        var core = new double[prices.Length]; OscillatorCore.InverseFisherTransform(prices, core, 2, 1, MovingAvgType.SimpleMovingAverage); Equal(expected, core);
        using var state = new EhlersInverseFisherTransformState(MovingAvgType.SimpleMovingAverage, 2, 1); Replay(state, bars, expected);
        using var relative = new EhlersRelativeStrengthIndexInverseFisherTransformState(MovingAvgType.SimpleMovingAverage, 2, 1); Replay(relative, bars, expected);
    }

    [Fact]
    public void DirectCoreRawAliasesAndFactoriesPreserveSelectedPricesAndAllPeriods()
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, 0d, double.Epsilon, 1, 4, 2, 5, 3, 8, 1, 1, 6 }; var bars = BarsOf(prices);
        using var context = new ComputeContext();
        var data = Data(Enumerable.Repeat(9d, prices.Length).ToArray()); data.CustomValuesList = prices.ToList();
        foreach (var indicator in new IBuiltInIndicator[] { new EhlersInverseFisherTransform(3), new InverseFisherTransformCore(2),
            new EhlersRelativeStrengthIndexInverseFisherTransform(3, 4) })
        {
            var expected = BuiltInFormulaReferences.RsiInverseFisherOutputs(bars, indicator).Single().Value;
            var options = indicator.CreateOptions();
            var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, options);
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
            Equal(expected, BuilderArmBinding.Compute(Data(prices), spec, target).ToArray());
            using var raw = IndicatorCompute.TryComputeFast(data, spec, context); Assert.NotNull(raw); Equal(expected, raw.Value.ToArray());
            var native = StatefulIndicatorFactory.Create(spec); var live = StreamingIndicatorFactory.CreateState(spec); Assert.NotNull(live);
            using var nativeLifetime = native as IDisposable; using var liveLifetime = live as IDisposable;
            Replay(native, bars, expected); Replay(live!, bars, expected);
        }
        var baseline = BuiltInFormulaReferences.RsiInverseFisherOutputs(bars, new InverseFisherTransformCore(2))["Eift"];
        var core = new double[prices.Length]; OscillatorCore.InverseFisherTransform(prices, core); Equal(baseline, core);
        OscillatorCore.InverseFisherTransform(Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OscillatorCore.InverseFisherTransform(prices, Array.Empty<double>()));
        using var alias = IndicatorCompute.ComputeInverseFisherTransformCoreFast(data, context); Equal(baseline, alias.Span.ToArray());
    }

    private static void Replay(IStreamingIndicatorState state, Bar[] bars, double[] expected)
    {
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            foreach (var final in new[] { false, true }) Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
        }
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
    public async Task AllThreeCustomerStagesAreHonored(double value)
    {
        var indicator = new EhlersRelativeStrengthIndexInverseFisherTransform(2, 3, new ConstantAverage(2), new ConstantAverage(6), new ConstantAverage(value));
        var prices = new[] { 1d, 3, 2, 4 }; var expected = ReferenceFraction.FromDouble(value).TanhToDouble();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(prices))).ConfigureIndicators(indicator).BuildAsync();
        Assert.All(result[indicator.Outputs[0]].ToArray(), actual => Equal(expected, actual));
        var averages = new[] { 2d, 6d, value }.Select(v => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)
            ((values, _) => values.Select(_ => v).ToArray())).ToArray();
        using var scope = ComponentAverage.Arm(averages); using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeEhlersInverseFisherTransformFast(Data(prices), context, 2, 3);
        Assert.All(raw.Span.ToArray(), actual => Equal(expected, actual)); Assert.Equal(3, ComponentAverage.Substitutions);
    }

    [Fact]
    public async Task AliasRejectsIncompleteCustomerStageList()
    {
        var indicator = new EhlersInverseFisherTransform(2, new ConstantAverage(2));
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(new[] { 1d, 3, 2, 4 }))).ConfigureIndicators(indicator).BuildAsync();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeInverseFisherRejectsInvalidFieldsWithoutAdvancing(bool stochastic)
    {
        IStreamingIndicatorState Create() => stochastic ? new EhlersRelativeStrengthIndexInverseFisherTransformState(length: 2, signalLength: 3) : new EhlersInverseFisherTransformState(length1: 2, length2: 3);
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

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "EhlersInverseFisherTransform", "InverseFisherTransformCore", "EhlersRelativeStrengthIndexInverseFisherTransform" };
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
