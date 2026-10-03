using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RankLogisticNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void WideFitsTiesTinyCorrelationsAndSignedGainsMatchEveryRoute()
    {
        foreach (var period in new[] { 1, 2, 3, 7 })
        foreach (var prices in new[] {
            new[] { 1d, 3, 2, 7, 7, -2, -2, 4, 1, 6, 0 },
            new[] { -double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, 0, -double.MaxValue, 0 },
            new[] { double.Epsilon, 0, -double.Epsilon, 0, double.Epsilon, 3 * double.Epsilon },
            new[] { 1e100, Math.BitIncrement(1e100), 1e100, Math.BitDecrement(1e100), 1e100 } })
        foreach (var indicator in new IBuiltInIndicator[] { new KendallRankCorrelationCoefficient(period),
            new LogisticCorrelation(period, -1000), new LogisticCorrelation(period, -10), new LogisticCorrelation(period, 0),
            new LogisticCorrelation(period, 10), new LogisticCorrelation(period, 1000), new LogisticCorrelation(period, double.MaxValue) })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.RankLogisticOutputs(bars, indicator).Single();
            var options = indicator.CreateOptions(); var spec = new IndicatorSpec(indicator.BatchName, options, expected.Key);
            var budget = indicator.BatchName == IndicatorName.LogisticCorrelation ? BuiltInFormulaReferences.LogisticCorrelationBudget : IndicatorErrorBudget.Exact;
            void Check(double value, double actual) => Assert.True(budget.Accepts(value, actual), $"{indicator.BatchName}/{period}: {value:R} != {actual:R}");
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            var batch = BuilderArmBinding.Compute(Data(bars), spec, target).ToArray();
            using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm);
            var selected = Data(BarsOf(Enumerable.Repeat(42d, prices.Length).ToArray())); selected.CustomValuesList = prices.ToList();
            using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm);
            var selectedBatch = BuilderArmBinding.Compute(selected, spec, target).ToArray();
            for (var i = 0; i < bars.Length; i++)
            {
                Check(expected.Value[i], batch[i]); Check(expected.Value[i], arm.Value.Span[i]);
                Check(expected.Value[i], selectedArm.Value.Span[i]); Check(expected.Value[i], selectedBatch[i]);
            }
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec)! })
            {
                Assert.NotNull(state); using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        state.Update(Native(BarsOf(new[] { 19d })[0]), false, true);
                        foreach (var final in new[] { false, false, true })
                        {
                            var actual = state.Update(Native(bars[i]), final, true);
                            Check(expected.Value[i], actual.Value); Check(expected.Value[i], actual.Outputs![expected.Key]);
                        }
                    }
                }
            }
        }
        using var fit = new ExactLinearFitWindow(3);
        fit.Next(-double.MaxValue, true); fit.Next(double.MaxValue, true);
        var endpoint = fit.Next(double.MaxValue, true);
        Assert.True(double.IsPositiveInfinity(endpoint.Last));
        Assert.True(endpoint.RoundedLastUnits > ExactVarianceWindow.Units(double.MaxValue));
        using var positive = new LogisticCorrelationWindow(3, 10);
        positive.Next(0, true); positive.Next(double.Epsilon, true);
        Assert.True(positive.Next(2 * double.Epsilon, true) > .9999);
        using var negative = new LogisticCorrelationWindow(3, -1000);
        negative.Next(0, true); negative.Next(double.Epsilon, true);
        var floor = negative.Next(2 * double.Epsilon, true);
        Assert.True(floor > 0 && floor < 1e-40);
    }

    [Fact]
    public void NonfiniteGainsAreRejectedBeforeCalculation()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogisticCorrelationSpecOptions(3, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogisticCorrelationState(3, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateLogisticCorrelation(3, invalid));
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 2))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => variant switch { 0 => new KendallRankCorrelationCoefficientState(4), _ => new LogisticCorrelationState(4, -10) };
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "KendallRankCorrelationCoefficient", "LogisticCorrelation" };
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
