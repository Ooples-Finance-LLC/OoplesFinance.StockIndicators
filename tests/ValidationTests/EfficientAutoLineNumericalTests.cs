using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EfficientAutoLineNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void EfficiencyOutputsPreservePreviewResetAndExtendedRange()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 1063, int.MaxValue })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, double.Epsilon, -double.Epsilon, 3 * double.Epsilon, 0, 0 }, Enumerable.Repeat(double.MaxValue, 90).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new EfficientAutoLine(length);
            var expected = BuiltInFormulaReferences.EfficiencyDerivedOutputs(bars, indicator)["Eal"];
            Assert.Equal(expected, Data(bars).CalculateEfficientAutoLine(length).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEfficientAutoLine(Data(bars), context, length, .0001, .005); Assert.Equal(expected, raw.Span.ToArray());
            var state = new EfficientAutoLineState(length);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var seed in BarsOf(Enumerable.Repeat(99d, 12).ToArray())) state.Update(Native(seed), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }

    [Fact]
    public void RawAndBatchRespectSelectedInputBeforeCloses()
    {
        var selected = Enumerable.Repeat(1d, 9).Concat(new[] { 1.003, 1.006, 1.009, 1.012, 1.015, 1.018 }).ToArray();
        var original = BarsOf(Enumerable.Repeat(40d, selected.Length).ToArray());
        var expected = BuiltInFormulaReferences.EfficiencyDerivedOutputs(BarsOf(selected), new EfficientAutoLine(3))["Eal"];
        var data = Data(original); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEfficientAutoLine(data, context, 3, .0001, .005);
        Assert.Equal(expected, raw.Span.ToArray());
        Assert.Equal(expected, data.CalculateEfficientAutoLine(3).CustomValuesList);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new EfficientAutoLineState(3);
            IStreamingIndicatorState control = new EfficientAutoLineState(3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    [Fact]
    public void DeadbandIsStrictAndPreservesFiniteExtremeThresholds()
    {
        var bars = BarsOf(Enumerable.Repeat(10d, 9).Concat(new[] { 10.25, 10.5, 10.75, 10.5, 10.25, 10d }).ToArray());
        foreach (var pair in new[] { (.25, .25), (.25, 1d), (double.MaxValue, double.MaxValue), (-double.MaxValue, double.MaxValue), (double.Epsilon, 0d) })
        {
            var indicator = new EfficientAutoLine(2, pair.Item1, pair.Item2);
            var expected = BuiltInFormulaReferences.EfficiencyDerivedOutputs(bars, indicator)["Eal"];
            Assert.Equal(expected, Data(bars).CalculateEfficientAutoLine(2, pair.Item1, pair.Item2).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEfficientAutoLine(Data(bars), context, 2, pair.Item1, pair.Item2);
            Assert.Equal(expected, raw.Span.ToArray());
            using var state = new EfficientAutoLineState(2, pair.Item1, pair.Item2);
            for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
        }
        var constant = Data(bars).CalculateEfficientAutoLine(2, .25, .25).CustomValuesList;
        Assert.Equal(10d, constant[9]); Assert.Equal(10.5, constant[10]); Assert.Equal(10.5, constant[11]);
    }

    [Fact]
    public void InvalidThresholdParametersAreRejected()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EfficientAutoLineState(2, value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EfficientAutoLineState(2, slowAlpha: value));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEfficientAutoLine(2, value));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEfficientAutoLine(2, slowAlpha: value));
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(EfficientAutoLine)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.EfficiencyDerivedOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
