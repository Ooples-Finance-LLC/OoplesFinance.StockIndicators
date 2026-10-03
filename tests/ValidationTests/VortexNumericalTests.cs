using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VortexNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void AllAliasesAndCoresPreserveWideMovementAndSelectedPreviousPrices()
    {
        var fixtures = new[] {
            Array.Empty<Bar>(),
            BarsOf(new[] { 1d, 4, 2, 5, 5, -1, 0, 0, 0, 0 }).Select((b, i) => new Bar(b.Time, b.Open, b.High + 2, b.Low - 1, b.Close, i)).ToArray(),
            BarsOf(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, -1, 0, 0 }),
            BarsOf(new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0, 0, 0 }),
            Enumerable.Range(0, 9).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, i % 2 == 0 ? double.MaxValue : 0, i % 2 == 0 ? -double.MaxValue : 0, i % 3 == 0 ? double.MaxValue : 0, 1)).ToArray() };
        foreach (var bars in fixtures)
        foreach (var period in new[] { 1, 3, 7 })
        {
            var expected = BuiltInFormulaReferences.VortexOutputs(bars, new VortexPositive(period));
            var batch = Data(bars).CalculateVortexIndicator(period);
            foreach (var pair in expected) Assert.Equal(pair.Value, batch.OutputValues[pair.Key]);
            var prices = bars.Select(b => b.Close).ToArray(); var highs = bars.Select(b => b.High).ToArray(); var lows = bars.Select(b => b.Low).ToArray();
            var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
            foreach (var indicator in new IBuiltInIndicator[] { new VortexPositive(period), new VortexNegative(period), new VortexPlus(period), new VortexMinus(period), new VortexIndicatorPlus(period), new VortexIndicatorMinus(period) })
            {
                var key = indicator.BatchOutputKey!;
                using var context = new ComputeContext();
                using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
                using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(selectedArm); Assert.Equal(expected[key], selectedArm.Value.ToArray());
            }
            selected.CalculateVortexIndicator(period);
            foreach (var pair in expected) Assert.Equal(pair.Value, selected.OutputValues[pair.Key]);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.TrendCore.VortexPositive(highs, lows, prices, core, period); Assert.Equal(expected["ViPlus"], core);
            OoplesFinance.StockIndicators.Core.TrendCore.VortexNegative(highs, lows, prices, core, period); Assert.Equal(expected["ViMinus"], core);
            OoplesFinance.StockIndicators.Core.OscillatorCore.VortexPlus(highs, lows, prices, core, period); Assert.Equal(expected["ViPlus"], core);
            OoplesFinance.StockIndicators.Core.OscillatorCore.VortexMinus(highs, lows, prices, core, period); Assert.Equal(expected["ViMinus"], core);
            OoplesFinance.StockIndicators.Core.OscillatorCore.VortexIndicatorPlus(highs, lows, prices, core, period); Assert.Equal(expected["ViPlus"], core);
            OoplesFinance.StockIndicators.Core.OscillatorCore.VortexIndicatorMinus(highs, lows, prices, core, period); Assert.Equal(expected["ViMinus"], core);
            using var state = new VortexIndicatorState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
                    }
                }
            }
            foreach (var pair in expected)
            {
                Assert.All(pair.Value, v => Assert.True(v >= 0 && double.IsFinite(v)));
                if (bars.Length > 0) Assert.Equal(0, pair.Value[0]);
            }
        }
        var wide = BarsOf(new[] { double.MaxValue, -double.MaxValue });
        var result = Data(wide).CalculateVortexIndicator(1);
        Assert.Equal(1, result.OutputValues["ViPlus"][1]); Assert.Equal(1, result.OutputValues["ViMinus"][1]);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new VortexIndicatorState(3); using var control = new VortexIndicatorState(3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "VortexPositive", "VortexNegative", "VortexPlus", "VortexMinus", "VortexIndicatorPlus", "VortexIndicatorMinus" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.VortexOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
