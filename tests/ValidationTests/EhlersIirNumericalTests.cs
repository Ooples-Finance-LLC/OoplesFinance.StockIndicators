using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EhlersIirNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SUM", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void RoundedLaggedMomentumFilterPreservesStartupAndRecovery()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 5, 3, 8, -3, 1, -5, 0, 0 },
            new[] { double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0 },
            new[] { double.Epsilon, double.Epsilon, -double.Epsilon, 3 * double.Epsilon, 0, 0 } })
        foreach (var length in new[] { 1, 2, 3, 7 })
        {
            var bars = BarsOf(prices);
            IBuiltInIndicator indicator = new EhlersInfiniteImpulseResponseFilter(length);
            var expected = BuiltInFormulaReferences.EhlersIirOutputs(bars, indicator)["Eiirf"];
            Assert.Equal(expected, Data(bars).CalculateEhlersInfiniteImpulseResponseFilter(length).CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersInfiniteImpulseResponseFilter(prices, core, length); Assert.Equal(expected, core);
            foreach (IBuiltInIndicator alias in new IBuiltInIndicator[] { indicator, new EhlersIirFilter(length) })
            {
                var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
                using var context = new ComputeContext();
                using var actual = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(alias.BatchName, alias.CreateOptions(), "Eiirf"), context);
                Assert.NotNull(actual); Assert.Equal(expected, actual.Value.ToArray());
            }
            using var state = new EhlersInfiniteImpulseResponseFilterState(length);
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
        Assert.Equal(new[] { 1d, 2, 7, 14 }, Data(BarsOf(new[] { 1d, 2, 4, 8 })).CalculateEhlersInfiniteImpulseResponseFilter(1).CustomValuesList);
        var recovery = Data(BarsOf(new[] { double.MaxValue, 0, -double.MaxValue, 0, 0, 0 })).CalculateEhlersInfiniteImpulseResponseFilter(1).CustomValuesList;
        Assert.True(double.IsNegativeInfinity(recovery[2])); Assert.Equal(double.MaxValue, recovery[4]);
        var longBars = BarsOf(Enumerable.Range(0, 534).Select(i => i == 0 ? 3d : 1).ToArray());
        Assert.Equal(BuiltInFormulaReferences.EhlersIirOutputs(longBars, new EhlersIirFilter(2000))["Eiirf"], Data(longBars).CalculateEhlersInfiniteImpulseResponseFilter(2000).CustomValuesList);
        var empty = Array.Empty<double>();
        OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersInfiniteImpulseResponseFilter(empty, empty);
    }

    [Fact]
    public void ExtendedProductsScaleBeforeRoundingAndRetainSubnormalContributions()
    {
        Assert.Equal(Math.Pow(2, -50) * 3, new RocBankValue(3, 1024).Multiply(double.Epsilon).Publish());
        Assert.Equal(-1, new RocBankValue(-.5, 1024).Multiply(Math.Pow(2, -1023)).Publish());
        Assert.Equal(0, new RocBankValue(3, 1024).Multiply(0).Publish());
        Assert.Equal(double.Epsilon, new RocBankValue(double.Epsilon).Multiply(1).Publish());
        Assert.Equal(0, new RocBankValue(double.Epsilon).Multiply(.5).Publish());
        Assert.Equal(2 * double.Epsilon, new RocBankValue(3 * double.Epsilon).Multiply(.5).Publish());
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new EhlersInfiniteImpulseResponseFilterState(3); using var control = new EhlersInfiniteImpulseResponseFilterState(3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("SUM", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "EhlersInfiniteImpulseResponseFilter", "EhlersIirFilter" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.EhlersIirOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
