using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DecyclerOscillatorNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExtremeAndMaximumPeriodsPreserveBothLanes()
    {
        foreach (var length in new[] { 1, 2, 7, 100, int.MaxValue })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, -2, 0, 0, 9 }, new[] { double.MaxValue, -double.MaxValue, 0, double.Epsilon, 1 }, Enumerable.Repeat(double.MaxValue, 40).ToArray() })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.DecyclerOscillatorOutputs(bars, new EhlersDecyclerOscillatorV1(length));
            var state = EhlersDecyclerOscillatorV1State.ForPeriod(length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true })
                {
                    var result = state.Update(Native(bars[i]), final, true);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], result.Outputs![key]);
                }
            }
            var options = ((IBuiltInIndicator)new EhlersDecyclerOscillatorV1(length)).CreateOptions();
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
            foreach (var key in expected.Keys)
                Assert.Equal(expected[key], BuilderArmBinding.Compute(Data(bars), new IndicatorSpec(target.Name, options, key), target));
            using var context = new ComputeContext();
            using var fast = IndicatorCompute.ComputeEhlersDecyclerOscillatorV1Fast(Data(bars), context, length);
            using var slow = IndicatorCompute.ComputeEhlersDecyclerOscillatorV1Fast(Data(bars), context, length, 1, 2);
            Assert.Equal(expected["FastEdo"], fast.Span.ToArray()); Assert.Equal(expected["SlowEdo"], slow.Span.ToArray());
            var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersDecyclerOscillatorV1(prices, core, length);
            Assert.Equal(expected["FastEdo"], core);
            foreach (var mult in new[] { 0d, -1d, 1.2, double.MaxValue })
            {
                var batch = Data(bars).CalculateEhlersDecyclerOscillatorV1(length, length, mult, mult);
                var reference = BuiltInFormulaReferences.DecyclerOscillatorLane(bars, length, mult);
                Assert.Equal(reference, batch.OutputValues["FastEdo"]); Assert.Equal(reference, batch.OutputValues["SlowEdo"]);
            }
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = EhlersDecyclerOscillatorV1State.ForPeriod(3);
            IStreamingIndicatorState control = EhlersDecyclerOscillatorV1State.ForPeriod(3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(EhlersDecyclerOscillatorV1)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.DecyclerOscillatorOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
