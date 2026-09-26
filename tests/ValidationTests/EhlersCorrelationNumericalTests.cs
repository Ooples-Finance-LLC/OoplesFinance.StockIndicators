using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EhlersCorrelationNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactMomentsRetainScalePaddingOrientationAndState()
    {
        foreach (var period in new[] { 1, 2, 4, 7 })
        foreach (var prices in new[] {
            new[] { 1d, 0, -1, 0, 1, 3, 7, -2, 4 },
            new[] { double.MaxValue, -double.MaxValue, 0, double.MaxValue, 1, 0, -1 },
            new[] { double.Epsilon, 0, -double.Epsilon, 0, double.Epsilon, 3 * double.Epsilon },
            new[] { 1e100, Math.BitIncrement(1e100), 1e100, Math.BitDecrement(1e100), 1e100 } })
        foreach (var indicator in new IBuiltInIndicator[] { new EhlersCorrelationTrendIndicator(period), new EhlersCorrelationCycleIndicator(period),
            new EhlersCorrelationAngleIndicator(period), new EhlersMarketStateIndicator(period) })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.EhlersCorrelationOutputs(bars, indicator);
            var options = indicator.CreateOptions();
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            foreach (var pair in expected)
            {
                var spec = new IndicatorSpec(indicator.BatchName, options, pair.Key);
                Check(pair.Value, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray());
                using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Check(pair.Value, arm.Value.ToArray());
                var selected = Data(BarsOf(Enumerable.Repeat(42d, prices.Length).ToArray())); selected.CustomValuesList = prices.ToList();
                using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm); Check(pair.Value, selectedArm.Value.ToArray());
            }
            var core = new double[prices.Length]; var imaginary = new double[prices.Length];
            switch (indicator.BatchName)
            {
                case IndicatorName.EhlersCorrelationTrendIndicator: OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersCorrelationTrendIndicator(prices, core, period); break;
                case IndicatorName.EhlersCorrelationCycleIndicator: OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersCorrelationCycleIndicator(prices, core, imaginary, period); Check(expected["Imag"], imaginary); break;
                case IndicatorName.EhlersCorrelationAngleIndicator: OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersCorrelationAngleIndicator(prices, core, period); break;
                default: OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersMarketStateIndicator(prices, core, period); break;
            }
            Check(expected.First().Value, core);
            var primary = new IndicatorSpec(indicator.BatchName, options);
            foreach (var state in new[] { StatefulIndicatorFactory.Create(primary), StreamingIndicatorFactory.CreateState(primary)! })
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
                            foreach (var pair in expected) Check(new[] { pair.Value[i] }, new[] { actual.Outputs![pair.Key] });
                        }
                    }
                }
            }
        }
        static void Check(double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++) Assert.True(new IndicatorErrorBudget(1e-12, 1e-14).Accepts(expected[i], actual[i]), $"{i}: {expected[i]:R} != {actual[i]:R}");
        }
        using var trend = new EhlersCorrelationWindow(3, true);
        trend.Next(0, true); trend.Next(double.Epsilon, true);
        Assert.Equal(1, trend.Next(2 * double.Epsilon, true).Real);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 4))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => variant switch { 0 => new EhlersCorrelationTrendIndicatorState(4), 1 => new EhlersCorrelationCycleIndicatorState(4), 2 => new EhlersCorrelationAngleIndicatorState(4), _ => new EhlersMarketStateIndicatorState(4) };
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "EhlersCorrelationTrendIndicator", "EhlersCorrelationCycleIndicator", "EhlersCorrelationAngleIndicator", "EhlersMarketStateIndicator" };
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
