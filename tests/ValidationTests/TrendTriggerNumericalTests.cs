using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrendTriggerNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactLaggedRangeRatioPreservesWarmupAndOriginalCandles()
    {
        var levels = new[] { double.MaxValue, double.MaxValue / 2, -double.MaxValue, 0d, double.Epsilon, -double.Epsilon, 1, -1, 0, 0 };
        var regular = BarsOf(new[] { 1d, 2, 3, 1, 5, 2, 0, -2, 1, 3 }).Select(b => new Bar(b.Time, b.Close, b.Close + 2, b.Close - 1, b.Close, 1)).ToArray();
        var extreme = levels.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, Math.Max(v, 0), Math.Min(v, 0), v, 1)).ToArray();
        var narrow = levels.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v / 2, Math.Max(v, v / 2), Math.Min(v, v / 2), v / 2, 1)).ToArray();
        foreach (var bars in new[] { regular, extreme, narrow, BarsOf(levels) })
        foreach (var period in new[] { 1, 2, 7, 11 })
        {
            IBuiltInIndicator indicator = new TrendTriggerFactor(period); var expected = BuiltInFormulaReferences.TrendTriggerOutputs(bars, indicator)["Ttf"];
            Assert.Equal(expected, Data(bars).CalculateTrendTriggerFactor(period).CustomValuesList);
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
            Assert.NotNull(raw); Assert.Equal(expected, raw.Value.ToArray());
            var selected = Data(bars); selected.CustomValuesList = bars.Select((_, i) => i % 2 == 0 ? double.MaxValue : -double.MaxValue).ToList();
            using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
            Assert.NotNull(selectedArm); Assert.Equal(expected, selectedArm.Value.ToArray());
            Assert.Equal(expected, selected.CalculateTrendTriggerFactor(period).CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.OscillatorCore.TrendTriggerFactor(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), core, period); Assert.Equal(expected, core);
            using var state = new TrendTriggerFactorState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(new Bar(DateTime.UnixEpoch, 0, double.MaxValue, 0, 0, 1)), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, 0, 1)), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
        var simple = new[] { 1d, 2, 3 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v, v, 1)).ToArray();
        Assert.Equal(new[] { 600d, 400, 200 }, Data(simple).CalculateTrendTriggerFactor(2).CustomValuesList);
        var huge = new[] { new Bar(DateTime.UnixEpoch, double.MaxValue / 2, double.MaxValue, double.MaxValue / 2, double.MaxValue / 2, 1) };
        Assert.Equal(600, Data(huge).CalculateTrendTriggerFactor(1).CustomValuesList[0]);
        Assert.All(Data(BarsOf(new[] { 2d, 3, 1 })).CalculateTrendTriggerFactor(1).CustomValuesList, v => Assert.Equal(0, v));
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new TrendTriggerFactorState(3); using var control = new TrendTriggerFactorState(3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "TrendTriggerFactor" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.TrendTriggerOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
