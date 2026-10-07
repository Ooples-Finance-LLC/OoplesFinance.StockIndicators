using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ZeroLagTripleNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void CascadesPreserveStagesCancellationPreviewAndReset()
    {
        foreach (var length in new[] { 1, 2, 3, 7 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 },
            Enumerable.Repeat(double.MaxValue, 10).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new ZeroLagTripleExponentialMovingAverage(length);
            var expected = BuiltInFormulaReferences.ZeroLagTripleOutputs(bars, indicator).Values.Single();
            var batch = Data(bars).CalculateZeroLagTripleExponentialMovingAverage(length: length);
            Assert.Equal(expected, batch.CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.ZeroLagTripleExponentialMovingAverage(prices, core, length);
            Assert.Equal(expected, core);
            using var smoother = MovingAverageSmootherFactory.Create(MovingAvgType.ZeroLagTripleExponentialMovingAverage, length);
            Assert.Equal(expected, prices.Select(p => smoother.Next(p, true)).ToArray());
            smoother.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                smoother.Next(-double.MaxValue, false);
                Assert.Equal(expected[i], smoother.Next(prices[i], false));
                Assert.Equal(expected[i], smoother.Next(prices[i], true));
            }
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), "Ztema"), context);
            Assert.NotNull(actual); Assert.Equal(expected, actual.Value.ToArray());
            IStreamingIndicatorState state = new ZeroLagTripleExponentialMovingAverageState(length: length);
            using var lifetime = (IDisposable)state;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(BarsOf(new[] { double.MaxValue })[0]), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }

    [Fact]
    public void CustomerStagesRemainSequential()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 });
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { Assert.Equal(3, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 5d).ToArray(); },
            (values, period) => { Assert.Equal(3, period); Assert.All(values, value => Assert.Equal(5, value)); return values.Select(_ => 9d).ToArray(); } };
        using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeZeroLagTripleExponentialMovingAverageFast(Data(bars), context, 3);
        Assert.Equal(2, ComponentAverage.Substitutions); Assert.All(actual.Span.ToArray(), value => Assert.Equal(1, value));
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new ZeroLagTripleExponentialMovingAverageState(length: 3);
            IStreamingIndicatorState control = new ZeroLagTripleExponentialMovingAverageState(length: 3);
            using var lifetime = (IDisposable)state; using var controlLifetime = (IDisposable)control;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ZeroLagTripleExponentialMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ZeroLagTripleOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
