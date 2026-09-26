using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SortinoNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactMomentsNormalizeWideReturnsAndKeepLagAndBenchmark()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 3, 8, -3, 1, -5, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, double.Epsilon, double.MaxValue, -double.MaxValue, 1, 1, 1, 1, 1, 1 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, double.MaxValue, -double.MaxValue, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 } })
        foreach (var period in new[] { 1, 2, 7 })
        foreach (var benchmark in new[] { .05, 0d, -.05, -1d })
        foreach (var average in new IMovingAverage[] { new Sma(), new Ema(), new Wma(), new Smma() })
        foreach (var variant in Enumerable.Range(0, 1))
        {
            IBuiltInIndicator indicator = new SortinoRatio(period, benchmark, average);
            var bars = BarsOf(prices); var key = "Sr";
            var expected = BuiltInFormulaReferences.SortinoOutputs(bars, indicator)[key];
            var kind = ((SortinoRatioSpecOptions)indicator.CreateOptions()).MaType;
            var batch = Data(bars);
            Assert.Equal(expected, batch.CalculateSortinoRatio(kind, period, benchmark).CustomValuesList);
            using var context = new ComputeContext();
            using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
            Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
            Assert.NotNull(selectedArm); Assert.Equal(expected, selectedArm.Value.ToArray());
            Assert.Equal(expected, selected.CalculateSortinoRatio(kind, period, benchmark).CustomValuesList);
            IStreamingIndicatorState state = new SortinoRatioState(kind, period, benchmark);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
            // Recursive means can retain a huge historical numerator after the
            // current-window variance shrinks: a final infinity is then legitimate.
            // Every route must still agree exactly with the independent reference.
            Assert.All(expected, v => Assert.False(double.IsNaN(v)));
        }
        var wide = BarsOf(new[] { double.Epsilon, double.Epsilon, -Math.Pow(2, 1023), -Math.Pow(2, 1023) });
        Assert.Equal(new[] { 0d, 0, -Math.Sqrt(.5), -1 }, BuiltInFormulaReferences.SortinoOutputs(wide, new SortinoRatio(2, 0))["Sr"]);
        var tiny = BarsOf(new[] { 1d, 1, -double.Epsilon, -double.Epsilon });
        var expectedTiny = BuiltInFormulaReferences.SortinoOutputs(tiny, new SortinoRatio(2, -1))["Sr"];
        // The published mean rounds half an epsilon to zero, but a full window
        // of negative epsilon returns must normalize to -1, not lose its square.
        Assert.Equal(-1, expectedTiny[^1]);
        Assert.Equal(expectedTiny, Data(tiny).CalculateSortinoRatio(length: 2, bmk: -1).CustomValuesList);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerMeanControlsNumerator()
    {
        foreach (var variant in Enumerable.Range(0, 1))
        {
            var bars = BarsOf(new[] { 2d, 5, 3, 8, 1, 7, 2, 0, -2 });
            IIndicator indicator = new SortinoRatio(3, .05, new ConstantAverage(42), new ConstantAverage(4));
            var builtIn = (IBuiltInIndicator)indicator;
            var expected = BuiltInFormulaReferences.SortinoOutputs(bars, builtIn, bars.Select(_ => 42d).ToArray(), bars.Select(_ => 4d).ToArray()).Single().Value;
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var averages = new[] { 42d, 4d }.Select(v => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, _) => input.Select(_ => v).ToArray())).ToArray();
            using var armed = ComponentAverage.Arm(averages);
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
            Assert.NotNull(raw); Assert.Equal(2, ComponentAverage.Substitutions); Assert.All(raw.Value.ToArray(), value => Assert.Equal(21, value));
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 1))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => new SortinoRatioState(MovingAvgType.WeightedMovingAverage, 3, .07);
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    [Fact]
    public void InvalidBenchmarksRejectBeforeAllocatingState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -2d })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SortinoRatioState(length: 3, bmk: invalid));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "SortinoRatio" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.SortinoOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
