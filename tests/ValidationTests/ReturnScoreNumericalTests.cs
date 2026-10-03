using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ReturnScoreNumericalTests
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
            IBuiltInIndicator indicator = information ? new InformationRatio(period, benchmark, average) : new SharpeRatio(period, benchmark, average);
            var bars = BarsOf(prices); var key = information ? "Ir" : "Sr";
            var expected = BuiltInFormulaReferences.ReturnScoreOutputs(bars, indicator)[key];
            var kind = information ? ((InformationRatioSpecOptions)indicator.CreateOptions()).MaType : ((SharpeRatioSpecOptions)indicator.CreateOptions()).MaType;
            var batch = Data(bars);
            Assert.Equal(expected, (information ? batch.CalculateInformationRatio(kind, period, benchmark) : batch.CalculateSharpeRatio(kind, period, benchmark)).CustomValuesList);
            using var context = new ComputeContext();
            using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
            Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
            Assert.NotNull(selectedArm); Assert.Equal(expected, selectedArm.Value.ToArray());
            Assert.Equal(expected, (information ? selected.CalculateInformationRatio(kind, period, benchmark) : selected.CalculateSharpeRatio(kind, period, benchmark)).CustomValuesList);
            IStreamingIndicatorState state = information ? new InformationRatioState(kind, period, benchmark) : new SharpeRatioState(kind, period, benchmark);
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
        var wide = BarsOf(new[] { double.Epsilon, double.Epsilon, double.MaxValue, -double.MaxValue });
        foreach (var indicator in new IBuiltInIndicator[] { new SharpeRatio(2, 0), new InformationRatio(2, 0) })
            Assert.Equal(new[] { 0d, 0, 1, 0 }, BuiltInFormulaReferences.ReturnScoreOutputs(wide, indicator).Single().Value);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerMeanControlsNumerator()
    {
        foreach (var information in new[] { false, true })
        {
            var bars = BarsOf(new[] { 2d, 5, 3, 8, 1, 7, 2, 0, -2 });
            IIndicator indicator = information ? new InformationRatio(3, .05, new ConstantAverage(42)) : new SharpeRatio(3, .05, new ConstantAverage(42));
            var builtIn = (IBuiltInIndicator)indicator;
            var expected = BuiltInFormulaReferences.ReturnScoreOutputs(bars, builtIn, bars.Select(_ => 42d).ToArray()).Single().Value;
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
            IStreamingIndicatorState Create() => information ? new InformationRatioState(MovingAvgType.WeightedMovingAverage, 3, .07) : new SharpeRatioState(MovingAvgType.WeightedMovingAverage, 3, .07);
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
            Assert.Throws<ArgumentOutOfRangeException>(() => new InformationRatioState(length: 3, bmk: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SharpeRatioState(length: 3, bmk: invalid));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "SharpeRatio", "InformationRatio" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ReturnScoreOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
