using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RepulsionNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void CascadesPreserveStagesCancellationPreviewAndReset()
    {
        foreach (var length in new[] { 1, 2, 3, 5, 7, 1063 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 },
            Enumerable.Repeat(double.MaxValue, 10).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new RepulsionMovingAverage(length);
            var expected = BuiltInFormulaReferences.RepulsionOutputs(bars, indicator).Values.Single();
            var batch = Data(bars).CalculateRepulsionMovingAverage(length: length);
            Assert.Equal(expected, batch.CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.RepulsionMovingAverage(prices, core, length);
            Assert.Equal(expected, core);
            Assert.Equal(expected, CalculationsHelper.GetMovingAverageList(Data(bars), MovingAvgType.RepulsionMovingAverage, length, prices.ToList()));
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), "Rma"), context);
            Assert.NotNull(actual); Assert.Equal(expected, actual.Value.ToArray());
            IStreamingIndicatorState state = new RepulsionMovingAverageState(length: length);
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
    public void CustomerAveragesReceiveOriginalInputsIndependently()
    {
        var bars = BarsOf(new[] { 1d, 2, 4, 8 });
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { Assert.Equal(5, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 5d).ToArray(); },
            (values, period) => { Assert.Equal(10, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 9d).ToArray(); },
            (values, period) => { Assert.Equal(15, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 2d).ToArray(); } };
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch) Assert.All(Data(bars).CalculateRepulsionMovingAverage(length: 5).CustomValuesList, value => Assert.Equal(6, value));
            else { using var actual = IndicatorCompute.ComputeRepulsionMovingAverageFast(Data(bars), context, 5); Assert.All(actual.Span.ToArray(), value => Assert.Equal(6, value)); }
            Assert.Equal(3, ComponentAverage.Substitutions);
        }
    }

    [Fact]
    public void DerivedPeriodsDoNotWrapAndFiniteTermsCancelBeforePublication()
    {
        foreach (var prices in new[] { Enumerable.Repeat(double.MaxValue, 4).ToArray(), new[] { 1d, 3, -2, 7, 0 } })
        foreach (var length in new[] { 1, int.MaxValue })
        {
            var bars = BarsOf(prices);
            var expected = BuiltInFormulaReferences.RepulsionOutputs(bars, length, 3).Values.Single();
            Assert.Equal(expected, Data(bars).CalculateRepulsionMovingAverage(MovingAvgType.ExponentialMovingAverage, length).CustomValuesList);
            using var state = new RepulsionMovingAverageState(MovingAvgType.ExponentialMovingAverage, length);
            for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new RepulsionMovingAverageState(length: 3);
            IStreamingIndicatorState control = new RepulsionMovingAverageState(length: 3);
            using var lifetime = (IDisposable)state; using var controlLifetime = (IDisposable)control;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RepulsionMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.RepulsionOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
