using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AlligatorNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void DelayedLinesPreservePreviewResetAndExtremePrices()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 1063 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0, 0, 0, 0, 0, 0 }, Enumerable.Repeat(double.MaxValue, 30).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new AlligatorJaw(length);
            var expected = BuiltInFormulaReferences.AlligatorOutputs(bars, indicator);
            var batch = Data(bars).CalculateAlligatorIndex(jawLength: length);
            foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
            using var state = new AlligatorIndexState(jawLength: length);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var old in BarsOf(Enumerable.Repeat(99d, 10).ToArray())) state.Update(Native(old), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                    }
                }
            }
        }
    }

    [Fact]
    public void OffsetsHandleZeroNegativeAndMaximumWithoutWrapping()
    {
        var bars = BarsOf(new[] { 2d, 4, 8, 16, 32, 64 });
        foreach (var offset in new[] { -1, 0, 1, 3, int.MaxValue })
        {
            var expected = bars.Select((_, i) => i < Math.Max(0, offset) ? 0 : bars[i - Math.Max(0, offset)].Close).ToArray();
            var batch = Data(bars).CalculateAlligatorIndex(jawLength: 1, jawOffset: offset, teethLength: 1, teethOffset: offset, lipsLength: 1, lipsOffset: offset);
            foreach (var key in new[] { "Jaws", "Teeth", "Lips" }) Assert.Equal(expected, batch.OutputValues[key]);
            using var state = new AlligatorIndexState(jawLength: 1, jawOffset: offset, teethLength: 1, teethOffset: offset, lipsLength: 1, lipsOffset: offset);
            for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAlligatorJawFast(Data(bars), context, 1, offset); Assert.Equal(expected, raw.Span.ToArray());
        }
    }

    [Fact]
    public void CustomerAveragesReceiveMedianPricesInThreeSlots()
    {
        var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 6, 2, 3, 1)).ToArray();
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { Assert.Equal(13, period); Assert.All(values, v => Assert.Equal(4, v)); return values.Select(_ => 9d).ToArray(); },
            (values, period) => { Assert.Equal(8, period); Assert.All(values, v => Assert.Equal(4, v)); return values.Select(_ => 6d).ToArray(); },
            (values, period) => { Assert.Equal(5, period); Assert.All(values, v => Assert.Equal(4, v)); return values.Select(_ => 3d).ToArray(); } };
        using (var armed = ComponentAverage.Arm(callbacks))
        {
            var actual = Data(bars).CalculateAlligatorIndex();
            Assert.Equal(Enumerable.Range(0, 12).Select(i => i < 8 ? 0d : 9), actual.OutputValues["Jaws"]);
            Assert.Equal(Enumerable.Range(0, 12).Select(i => i < 5 ? 0d : 6), actual.OutputValues["Teeth"]);
            Assert.Equal(Enumerable.Range(0, 12).Select(i => i < 3 ? 0d : 3), actual.OutputValues["Lips"]);
            Assert.Equal(3, ComponentAverage.Substitutions);
        }
        using (var armed = ComponentAverage.Arm(callbacks.Take(1).ToArray()))
        using (var context = new ComputeContext())
        {
            using var actual = IndicatorCompute.ComputeAlligatorJawFast(Data(bars), context);
            Assert.Equal(Enumerable.Range(0, 12).Select(i => i < 8 ? 0d : 9), actual.Span.ToArray()); Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new AlligatorIndexState(jawLength: 3);
            IStreamingIndicatorState control = new AlligatorIndexState(jawLength: 3);
            using var lifetime = (IDisposable)state; using var controlLifetime = (IDisposable)control;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AlligatorJaw) || c.IndicatorType == typeof(AlligatorTeeth) || c.IndicatorType == typeof(AlligatorLips)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AlligatorOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
