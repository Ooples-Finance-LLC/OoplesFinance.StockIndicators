using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EhlersFirNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("FIR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static EhlersFiniteImpulseResponseFilterState State(double[] c) => new(c[0], c[1], c[2], c[3], c[4], c[5], c[6]);
    private static StockData Batch(Bar[] bars, double[] c) => Data(bars).CalculateEhlersFiniteImpulseResponseFilter(c[0], c[1], c[2], c[3], c[4], c[5], c[6]);
    private static ComputeBuffer Raw(StockData data, ComputeContext context, double[] c) => IndicatorCompute.ComputeEhlersFiniteImpulseResponseFilterFast(data, context, c[0], c[1], c[2], c[3], c[4], c[5], c[6]);

    [Fact]
    public void ExactSevenTapNormalizationHandlesCancellationAndCustomCoefficients()
    {
        foreach (var c in new[] {
            new[] { 1d, 3.5, 4.5, 3, .5, -.5, -1.5 },
            new[] { 2d, -1, 0, 3, 0, -2, 1 },
            new[] { double.MaxValue, double.MaxValue, 0, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, 1, 0, 0, 0, 0 },
            new[] { -double.Epsilon, -double.Epsilon, 0, 0, 0, 0, 0 } })
        foreach (var prices in new[] {
            new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0, 0, 0 },
            Enumerable.Repeat(double.MaxValue, 20).ToArray() })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.EhlersFirOutputs(bars, c)["Efirf"];
            Assert.Equal(expected, Batch(bars, c).CustomValuesList);
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var context = new ComputeContext(); using var raw = Raw(selected, context, c);
            Assert.Equal(expected, raw.Span.ToArray());
            using var state = State(c);
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
        var impulse = BarsOf(new[] { 21d, 0, 0, 0, 0, 0, 0, 0 });
        Assert.Equal(new[] { 2d, 7, 9, 6, 1, -1, -3, 0 }, Data(impulse).CalculateEhlersFiniteImpulseResponseFilter().CustomValuesList);
        foreach (var prices in new[] { impulse.Select(b => b.Close).ToArray(), Enumerable.Repeat(double.MaxValue, 20).ToArray(), Array.Empty<double>() })
        foreach (var ignoredLength in new[] { 1, 3, 20 })
        {
            var core = new double[prices.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersFiniteImpulseResponseFilter(prices, core, ignoredLength);
            Assert.Equal(BuiltInFormulaReferences.EhlersFirOutputs(BarsOf(prices))["Efirf"], core);
        }
        Assert.All(Data(BarsOf(Enumerable.Repeat(double.MaxValue, 20).ToArray())).CalculateEhlersFiniteImpulseResponseFilter().CustomValuesList.Skip(6), value => Assert.Equal(double.MaxValue, value));
    }

    [Fact]
    public void InvalidCoefficientsRejectBeforeAnyWindowIsCreated()
    {
        var bars = BarsOf(new[] { 1d, 2 });
        foreach (var index in Enumerable.Range(0, 7))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var c = Enumerable.Repeat(1d, 7).ToArray(); c[index] = invalid;
            Assert.Equal($"coef{index + 1}", Assert.Throws<ArgumentOutOfRangeException>(() => State(c)).ParamName);
            Assert.Throws<ArgumentOutOfRangeException>(() => Batch(bars, c));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => Raw(Data(bars), context, c));
        }
        foreach (var c in new[] { new double[7], new[] { double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 0 } })
        {
            Assert.Throws<ArgumentException>(() => State(c)); Assert.Throws<ArgumentException>(() => Batch(bars, c));
            using var context = new ComputeContext(); Assert.Throws<ArgumentException>(() => Raw(Data(bars), context, c));
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new EhlersFiniteImpulseResponseFilterState(); using var control = new EhlersFiniteImpulseResponseFilterState();
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("FIR", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(EhlersFirFilter) || c.IndicatorType == typeof(EhlersFiniteImpulseResponseFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.EhlersFirOutputs(bars), IndicatorErrorBudget.Exact);
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
