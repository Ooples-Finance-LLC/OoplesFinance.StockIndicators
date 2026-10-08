using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MovingAverageV3NumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExtrapolationPreservesPreviewResetAndExtremePrices()
    {
        foreach (var lengths in new[] { (1, 3), (2, 1), (3, 2), (7, 3), (1063, 2), (int.MaxValue, int.MaxValue) })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 }, Enumerable.Repeat(double.MaxValue, 20).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new MovingAverageV3(lengths.Item1);
            var expected = BuiltInFormulaReferences.MovingAverageV3Outputs(bars, indicator, lengths.Item2)["Mav3"];
            Assert.Equal(expected, Data(bars).CalculateMovingAverageV3(length1: lengths.Item1, length2: lengths.Item2).CustomValuesList);
            if (lengths.Item2 == 3) { var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.MovingAverageCore.MovingAverageV3(prices, core, lengths.Item1); Assert.Equal(expected, core); }
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeMovingAverageV3Fast(Data(bars), context, lengths.Item1, lengths.Item2); Assert.Equal(expected, raw.Span.ToArray());
            using var state = new MovingAverageV3State(length1: lengths.Item1, length2: lengths.Item2);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(BarsOf(new[] { 99d })[0]), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }

    [Fact]
    public void CustomerAveragesReceiveOriginalPricesInPeriodOrder()
    {
        var bars = BarsOf(new[] { 1d, 2, 4, 8 });
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 10d).ToArray(); },
            (values, period) => { Assert.Equal(3, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 4d).ToArray(); } };
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch) Assert.All(Data(bars).CalculateMovingAverageV3(length1: 2).CustomValuesList, v => Assert.Equal(13, v));
            else { using var actual = IndicatorCompute.ComputeMovingAverageV3Fast(Data(bars), context, 2); Assert.All(actual.Span.ToArray(), v => Assert.Equal(13, v)); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new MovingAverageV3State(length1: 3);
            IStreamingIndicatorState control = new MovingAverageV3State(length1: 3);
            using var lifetime = (IDisposable)state; using var controlLifetime = (IDisposable)control;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MovingAverageV3)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.MovingAverageV3Outputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
