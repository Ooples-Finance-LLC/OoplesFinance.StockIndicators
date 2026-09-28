using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ReflexNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersReflex) || c.IndicatorType == typeof(EhlersReflexIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWindow(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static int Length(IIndicator indicator)
    { var options = ((IBuiltInIndicator)indicator).CreateOptions(); return options is EhlersReflexSpecOptions alias ? alias.Length : ((EhlersReflexIndicatorSpecOptions)options).Length; }
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator) => new() { { "Eri", BuiltInFormulaReferences.ReflexValues(bars, Length(indicator)) } };
    private static void Check(Bar[] bars, int length)
    {
        var expected = BuiltInFormulaReferences.ReflexValues(bars, length);
        Assert.All(expected, v => Assert.True(double.IsFinite(v) && Math.Abs(v) <= 5.000000000001));
        Assert.Equal(expected, Data(bars).CalculateEhlersReflexIndicator(length).CustomValuesList);
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeEhlersReflexFast(Data(bars), context, length); Assert.Equal(expected, fast.ToArray());
        using var other = IndicatorCompute.ComputeEhlersReflexIndicatorFast(Data(bars), context, length); Assert.Equal(expected, other.ToArray());
        var prices = bars.Select(b => b.Close).ToArray(); var output = new double[bars.Length];
        OscillatorCore.EhlersReflex(prices, output, length); Assert.Equal(expected, output);
        OscillatorCore.EhlersReflexIndicator(prices, output, length); Assert.Equal(expected, output);
        using var state = new EhlersReflexIndicatorState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var p = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], p.Value); Assert.Equal(expected[i], p.Outputs!["Eri"]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyEnergyRetainsFiniteNormalization()
    {
        foreach (var scale in new[] { double.Epsilon, 1e-160, 1d, 1e160, double.MaxValue }) foreach (var length in new[] { 1, 2, 7, 20 })
            Check(Enumerable.Range(0, 35).Select(i => Candle(i < 20 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), length);
    }
    [Fact]
    public void FirstImpulseHasUnitEnergyNormalizedMagnitudeFive()
    {
        foreach (var scale in new[] { 1e-160, 1d, 1e160, double.MaxValue }) foreach (var sign in new[] { -1, 1 }) foreach (var length in new[] { 20, int.MaxValue })
        {
            var bars = new[] { Candle(sign * scale) }; var expected = BuiltInFormulaReferences.ReflexValues(bars, length);
            Assert.InRange(Math.Abs(expected[0] - sign * 5), 0, 2e-14); Check(bars, length);
        }
    }
    [Fact]
    public void OneBarChordHasNoDeviationAtAnyScale()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 24).Select(i => Candle(i % 3 == 0 ? -scale : scale)).ToArray();
            Assert.All(BuiltInFormulaReferences.ReflexValues(bars, 1), v => Assert.Equal(0, v)); Check(bars, 1);
        }
    }
    [Fact]
    public void MinimumPeriodRetainsBothFeedbackValuesAndRollingEviction()
    {
        var bars = Enumerable.Range(0, 30).Select(i => Candle(i % 5 == 0 ? 3 : i % 4)).ToArray();
        foreach (var length in new[] { 1, 2, 3 }) Check(bars, length);
    }
    [Fact]
    public void PeriodsNormalizeWithoutAllocatingTheRequestedHistory()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 3)).ToArray();
        foreach (var length in new[] { int.MinValue, -1, 0, 1, int.MaxValue }) { Check(bars, length); Check(Array.Empty<Bar>(), length); }
        Assert.Equal(BuiltInFormulaReferences.ReflexValues(bars, 1), Data(bars).CalculateEhlersReflexIndicator(0).CustomValuesList);
    }
    [Fact]
    public void SelectedPricesAndBothTypedOptionsUseTheRequestedPeriod()
    {
        var selected = Enumerable.Range(0, 30).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var length in new[] { 1, 3, 20, int.MaxValue })
        {
            var expected = BuiltInFormulaReferences.ReflexValues(selected.Select(v => Candle(v)).ToArray(), length);
            var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersReflexIndicator(length); Assert.Equal(expected, batch.CustomValuesList);
            using var context = new ComputeContext();
            foreach (IIndicatorSpecOptions options in new IIndicatorSpecOptions[] { new EhlersReflexSpecOptions(length), new EhlersReflexIndicatorSpecOptions(length) })
            {
                var spec = new IndicatorSpec(IndicatorName.EhlersReflexIndicator, options, "Eri"); var data = Data(bars); data.SetCustomValues(selected);
                using var output = IndicatorCompute.TryComputeFast(data, spec, context); Assert.NotNull(output); Assert.Equal(expected, output.Value.ToArray());
                using var state = Assert.IsType<EhlersReflexIndicatorState>(StatefulIndicatorFactory.Create(spec));
                for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(Native(Candle(selected[i])), true, true).Value);
            }
        }
    }
    [Fact]
    public void InvalidFieldsLeaveFilterEnergyAndHistoryUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersReflexIndicatorState(3); using var control = new EhlersReflexIndicatorState(3);
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
