using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ZScoreNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ZSCORE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static IndicatorErrorBudget Budget(IndicatorName name) => name == IndicatorName.InverseFisherZScore ? BuiltInFormulaReferences.ZScoreLogisticBudget
        : name == IndicatorName.InverseFisherFastZScore ? BuiltInFormulaReferences.RsiInverseFisherBudget : IndicatorErrorBudget.Exact;
    private static void Equal(double expected, double actual, IndicatorName name) => Assert.True(Budget(name).Accepts(expected, actual), $"{name}: expected {expected:R}, got {actual:R}");

    [Fact]
    public void DirectRoutesPreserveExactNormalizationPeriodsAndSelectedInputs()
    {
        var prices = new[] { 0d, double.Epsilon, -double.Epsilon, 2 * double.Epsilon, double.MaxValue, -double.MaxValue, 0d, double.MaxValue, 4, 2, 5, 1, 7, 3, 9, 2, 8, 1 };
        var bars = BarsOf(prices);
        foreach (var length in new[] { 1, 2, 3, 7 })
        foreach (var indicator in new IBuiltInIndicator[] { new ZScore(length), new FastZScore(length), new InverseFisherZScore(length), new InverseFisherFastZScore(length) })
        {
            var name = indicator.BatchName; var expected = BuiltInFormulaReferences.ZScoreOutputs(bars, indicator).Single();
            using var context = new ComputeContext();
            var selected = Data(BarsOf(Enumerable.Repeat(42d, prices.Length).ToArray())); selected.CustomValuesList = prices.ToList();
            var spec = new IndicatorSpec(name, indicator.CreateOptions(), expected.Key);
            using var raw = IndicatorCompute.TryComputeFast(selected, spec, context); Assert.NotNull(raw);
            Assert.True(BuilderArmBinding.TryGetTarget(spec.Options.GetType(), out var target));
            var batch = BuilderArmBinding.Compute(Data(bars), spec, target).ToArray();
            var native = StatefulIndicatorFactory.Create(spec); var live = StreamingIndicatorFactory.CreateState(spec); Assert.NotNull(live);
            using var nativeLifetime = native as IDisposable; using var liveLifetime = live as IDisposable;
            for (var i = 0; i < prices.Length; i++) { Equal(expected.Value[i], raw.Value.Span[i], name); Equal(expected.Value[i], batch[i], name); }
            foreach (var state in new[] { native, live! })
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { 100d })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        Equal(expected.Value[i], actual.Value, name); Equal(expected.Value[i], actual.Outputs![expected.Key], name);
                    }
                }
            }
            if (name is IndicatorName.ZScore or IndicatorName.FastZScore)
            {
                var core = new double[prices.Length];
                if (name == IndicatorName.ZScore) OscillatorCore.ZScore(prices, core, length); else OscillatorCore.FastZScore(prices, core, length);
                for (var i = 0; i < core.Length; i++) Equal(expected.Value[i], core[i], name);
            }
        }
        using var exact = new StandardizedScoreWindow(2, false);
        Assert.Equal(0, exact.Next(0, 0, true, true));
        Assert.Equal(1, exact.Next(double.Epsilon, 0, true, true));
        Assert.Equal(-1, exact.Next(0, 0, true, true));
        OscillatorCore.ZScore(Array.Empty<double>(), Array.Empty<double>());
        OscillatorCore.FastZScore(Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OscillatorCore.ZScore(prices, Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => OscillatorCore.FastZScore(prices, Array.Empty<double>()));
    }

    [Fact]
    public void AllConvexMeansMatchIndependentNormalizationIncludingOverflow()
    {
        var prices = Enumerable.Repeat(-double.MaxValue, 8).Concat(new[] { double.MaxValue, 0d, double.Epsilon, -double.Epsilon, 1d, 1 + 1e-12, 1, 1, 1 + 1e-12, 2, 7, 4 }).ToArray();
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 3, 7 })
        foreach (var fast in new[] { false, true })
        {
            var referenceKind = kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
            var expected = BuiltInFormulaReferences.StandardizedValues(prices, length, referenceKind, fast);
            IStreamingIndicatorState state = fast ? new FastZScoreState(kind, length) : new ZScoreState(kind, length);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < prices.Length; i++)
                foreach (var final in new[] { false, true })
                    Assert.Equal(expected[i], state.Update(Native(BarsOf(prices)[i]), final, true).Value);
            }
        }
    }

    [Fact]
    public void InverseTransformsPreserveTinySignalsAndNegativeLogisticTails()
    {
        foreach (var score in new[] { -400d, -374, -372, -355, -350, -100, -25, -1e-20, 0, 1e-20, .25, 25, 400 })
        {
            var expected = ReferenceFraction.FromDouble(score).LogisticPercentToDouble();
            Equal(expected, StandardizedScoreWindow.Inverse(score, false), IndicatorName.InverseFisherZScore);
            Equal(ReferenceFraction.FromDouble(5 * score).TanhToDouble(), StandardizedScoreWindow.Inverse(score, true), IndicatorName.InverseFisherFastZScore);
        }
        Assert.True(StandardizedScoreWindow.Inverse(-374, false) > 0);
    }

    private sealed class SequenceAverage(double[] values) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(values);
        private sealed class State(double[] values) : IIndicatorState { private int _index; public void Reset() => _index = 0; public double Update(in Bar bar) => values[_index++]; }
    }
    [Fact]
    public async Task CustomerAverageIsUsedExactlyOnceAndKeepsTheOriginalVarianceSeries()
    {
        var prices = new[] { 1d, 3, 2, 4, 0, 6 }; var bars = BarsOf(prices);
        foreach (var fast in new[] { false, true })
        foreach (var inverse in new[] { false, true })
        {
            var means = new[] { 2d, 8, 1, 7, 3, 6 }; var mean = new SequenceAverage(means);
            IIndicator indicator = fast ? inverse ? new InverseFisherFastZScore(3, mean) : new FastZScore(3, mean)
                : inverse ? new InverseFisherZScore(3, mean) : new ZScore(3, mean);
            var expected = BuiltInFormulaReferences.StandardizedValues(prices, 3, 1, fast, means);
            var name = ((IBuiltInIndicator)indicator).BatchName;
            using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            var actual = result[indicator.Outputs[0]].ToArray();
            for (var i = 0; i < expected.Length; i++)
            {
                var value = inverse ? fast ? ReferenceFraction.FromDouble(5 * expected[i]).TanhToDouble() : ReferenceFraction.FromDouble(expected[i]).LogisticPercentToDouble() : expected[i];
                Equal(value, actual[i], name);
            }
            using var armed = ComponentAverage.Arm((values, _) => means); using var context = new ComputeContext();
            var builtIn = (IBuiltInIndicator)indicator;
            using var raw = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(name, builtIn.CreateOptions()), context);
            Assert.NotNull(raw); Assert.Equal(1, ComponentAverage.Substitutions);
            for (var i = 0; i < actual.Length; i++) Equal(actual[i], raw.Value.Span[i], name);
        }
    }

    [Fact]
    public void NativeRejectsAllInvalidFieldsWithoutAdvancing()
    {
        foreach (var variant in Enumerable.Range(0, 4))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => variant switch { 0 => new ZScoreState(length: 3), 1 => new FastZScoreState(length: 3), 2 => new InverseFisherZScoreState(length: 3), _ => new InverseFisherFastZScoreState(length: 3) };
            var state = Create(); var control = Create(); using var stateLifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("ZSCORE", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "ZScore", "FastZScore", "InverseFisherZScore", "InverseFisherFastZScore" };
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
