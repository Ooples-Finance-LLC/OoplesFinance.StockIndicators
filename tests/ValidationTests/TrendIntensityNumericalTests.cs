using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrendIntensityNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactResidualSharesKeepBothPeriodsAndWideDifferences()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 3, 8, -3, 1, -5, 0, 0, 0, 0, 0 },
            Enumerable.Repeat(-double.MaxValue, 64).Concat(new[] { double.MaxValue, 0d, double.MaxValue, -double.MaxValue, 0, 0 }).ToArray(),
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 } })
        foreach (var period in new[] { 1, 3, 7 })
        foreach (var slow in new[] { 1, 3, 60 })
        foreach (var indicator in new IBuiltInIndicator[] { new TrendIntensityIndex(period), new TrendIntensityIndex(period, new Ema()),
            new TrendIntensityIndex(period, new Wma()), new TrendIntensityIndex(period, new Smma()) })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.TrendIntensityOutputs(bars, indicator, slow)["Tii"];
            var options = (TrendIntensityIndexSpecOptions)indicator.CreateOptions();
            Assert.Equal(expected, Data(bars).CalculateTrendIntensityIndex(options.MaType, period, slow).CustomValuesList);
            using var context = new ComputeContext();
            using var arm = IndicatorCompute.ComputeTrendIntensityIndexFast(Data(bars), context, period, options.MaType, slow);
            Assert.Equal(expected, arm.ToArray());
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var selectedArm = IndicatorCompute.ComputeTrendIntensityIndexFast(selected, context, period, options.MaType, slow);
            Assert.Equal(expected, selectedArm.ToArray());
            Assert.Equal(expected, selected.CalculateTrendIntensityIndex(options.MaType, period, slow).CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.TrendCore.TrendIntensityIndex(prices, core, period, slow, options.MaType);
            Assert.Equal(expected, core);
            using var state = new TrendIntensityIndexState(options.MaType, period, slow);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
            Assert.All(expected, v => Assert.InRange(v, 0, 100));
        }
        var first = BuiltInFormulaReferences.TrendIntensityOutputs(BarsOf(new[] { 7d }), new TrendIntensityIndex(3))["Tii"];
        Assert.Equal(100, first[0]);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerMeanControlsResidualSigns()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 }); var indicator = new TrendIntensityIndex(3, new ConstantAverage(42));
        var expected = BuiltInFormulaReferences.TrendIntensityOutputs(bars, (IBuiltInIndicator)indicator, customerMeans: bars.Select(_ => 42d).ToArray())["Tii"];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, run[indicator].ToArray()); Assert.All(expected, v => Assert.Equal(0, v));
        using var armed = ComponentAverage.Arm((input, _) => input.Select(_ => 42d).ToArray());
        using var context = new ComputeContext(); var builtIn = (IBuiltInIndicator)indicator;
        using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
        Assert.NotNull(raw); Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(expected, raw.Value.ToArray());
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 1))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => new TrendIntensityIndexState(MovingAvgType.WeightedMovingAverage, 3, 7);
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "TrendIntensityIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.TrendIntensityOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
