using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DrawdownNumericalTests
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
        foreach (var information in new[] { false, true })
        {
            IBuiltInIndicator indicator = information ? new UlcerIndex(period) : new MartinRatio(period, benchmark, average);
            var bars = BarsOf(prices); var key = information ? "Ui" : "Mr";
            var expected = BuiltInFormulaReferences.DrawdownOutputs(bars, indicator)[key];
            var kind = information ? MovingAvgType.SimpleMovingAverage : ((MartinRatioSpecOptions)indicator.CreateOptions()).MaType;
            var batch = Data(bars);
            Assert.Equal(expected, (information ? batch.CalculateUlcerIndex(period) : batch.CalculateMartinRatio(kind, period, benchmark)).CustomValuesList);
            using var context = new ComputeContext();
            using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
            Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
            Assert.NotNull(selectedArm); Assert.Equal(expected, selectedArm.Value.ToArray());
            Assert.Equal(expected, (information ? selected.CalculateUlcerIndex(period) : selected.CalculateMartinRatio(kind, period, benchmark)).CustomValuesList);
            IStreamingIndicatorState state = information ? new UlcerIndexState(period) : new MartinRatioState(kind, period, benchmark);
            if (information)
            {
                var core = new double[prices.Length];
                OoplesFinance.StockIndicators.Core.VolatilityCore.UlcerIndex(prices, core, period);
                Assert.Equal(expected, core);
            }
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
        var startup = BarsOf(new[] { 8d, 4, 2, 6, 5, 7 });
        var early = BuiltInFormulaReferences.DrawdownOutputs(startup, new UlcerIndex(3))["Ui"];
        Assert.Equal(Math.Sqrt(1250), early[1]);
        // Both enormous percentage return and drawdown cancel before publication.
        var wide = BarsOf(new[] { double.Epsilon, double.Epsilon, -Math.Pow(2, 1023), -Math.Pow(2, 1023) });
        var martin = BuiltInFormulaReferences.DrawdownOutputs(wide, new MartinRatio(2, 0))["Mr"];
        Assert.Equal(-Math.Sqrt(.5), martin[2]);
        Assert.Equal(martin, Data(wide).CalculateMartinRatio(length: 2, bmk: 0).CustomValuesList);
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
            IIndicator indicator = new MartinRatio(3, .05, new ConstantAverage(42));
            var builtIn = (IBuiltInIndicator)indicator;
            var expected = BuiltInFormulaReferences.DrawdownOutputs(bars, builtIn, bars.Select(_ => 42d).ToArray()).Single().Value;
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            using var armed = ComponentAverage.Arm((input, _) => input.Select(_ => 42d).ToArray());
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
            Assert.NotNull(raw); Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(expected, raw.Value.ToArray());
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var information in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => information ? new UlcerIndexState(3) : new MartinRatioState(MovingAvgType.WeightedMovingAverage, 3, .07);
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
            Assert.Throws<ArgumentOutOfRangeException>(() => new MartinRatioState(length: 3, bmk: invalid));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "UlcerIndex", "MartinRatio" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.DrawdownOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
