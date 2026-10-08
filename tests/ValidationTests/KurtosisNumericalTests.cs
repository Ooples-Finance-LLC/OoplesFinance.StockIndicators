using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KurtosisNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KurtosisIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLagDifferences(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KurtosisOutputs(bars), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int first = 3, int second = 1, int fast = 3, int slow = 65)
    {
        var expected = BuiltInFormulaReferences.KurtosisOutputs(bars, first, second, fast, slow); var batch = Data(bars).CalculateKurtosisIndicator(first, second, fast, slow);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        if (first == 3 && second == 1 && fast == 3 && slow == 65)
            foreach (var key in expected.Keys) { using var raw = IndicatorCompute.ComputeKurtosisIndicatorFast(Data(bars), context, key); Assert.Equal(expected[key], raw.ToArray()); }
        using var state = new KurtosisIndicatorState(first, second, fast, slow);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Fk"][i], actual.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void HandStartupUsesTwoLagDifferencesThenEmaAndWma()
    {
        var bars = new[] { 1d, 2, 4, 8, 16 }.Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.KurtosisOutputs(bars, 1, 1, 1, 1); Assert.Equal(new[] { 0d, 1, 1, 2, 4 }, expected["Fk"]); Assert.Equal(expected["Fk"], expected["Signal"]); Check(bars, 1, 1, 1, 1);
        var startup = BuiltInFormulaReferences.KurtosisOutputs(bars.Take(4).ToArray()); Assert.Equal(new[] { 0d, 0, 0, 1.75 }, startup["Fk"]); Assert.Equal(new[] { 0d, 0, 0, .875 }, startup["Signal"]); Check(bars);
    }
    [Fact]
    public void ExtremeDifferencesAndPublishedOverflowDoNotPoisonSmoothing()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 160).Select(i => Candle(i < 80 ? (i % 3 - 1) * scale : 1)).ToArray(); Check(bars); Check(bars, 1, 2, 2, 3);
        }
        var recovery = new[] { -double.MaxValue, double.MaxValue, -double.MaxValue }.Concat(Enumerable.Repeat(1d, 20)).Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.KurtosisOutputs(recovery, 1, 1, 1, 1); Assert.True(double.IsPositiveInfinity(expected["Fk"][1])); Assert.True(double.IsNegativeInfinity(expected["Fk"][2])); Assert.Equal(0, expected["Fk"][^1]); Check(recovery, 1, 1, 1, 1); Check(recovery, 1, 1, 2, 1);
        Check(recovery, int.MaxValue, int.MaxValue, 1, int.MaxValue); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void SelectedPricesDriveBothOutputsAndCompatibilityLengthIsUnused()
    {
        var selected = Enumerable.Range(0, 80).Select(i => i % 5 == 0 ? double.MaxValue : i % 5 == 1 ? -double.MaxValue : 1 + i % 7).ToArray(); var bars = selected.Select(_ => Candle(0)).ToArray(); var expected = BuiltInFormulaReferences.KurtosisOutputs(selected.Select(v => Candle(v)).ToArray());
        using var context = new ComputeContext();
        foreach (var length in new[] { 0, 1, 7, int.MaxValue }) foreach (var key in expected.Keys)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.KurtosisIndicator, new KurtosisIndicatorSpecOptions(length), key), context); Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
            Assert.Contains(expected[key], value => value != 0); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var directRaw = IndicatorCompute.ComputeKurtosisIndicatorFast(direct, context, key); Assert.Equal(expected[key], directRaw.ToArray());
        }
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); var result = batch.CalculateKurtosisIndicator(); foreach (var key in expected.Keys) Assert.Equal(expected[key], result.OutputValues[key]);
    }
    [Fact]
    public void CustomerAveragesKeepEmaThenWmaOrder()
    {
        var bars = Enumerable.Range(1, 8).Select(i => Candle(i)).ToArray(); var changes = new[] { 0d, 0, 0, 3, 0, 0, 0, 0 };
        foreach (var batch in new[] { false, true }) foreach (var key in new[] { "Fk", "Signal" })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(65, period); Assert.Equal(changes, input); return Enumerable.Repeat(17d, input.Count).ToArray(); },
                (input, period) => { Assert.Equal(3, period); Assert.All(input, value => Assert.Equal(17, value)); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var expected = Enumerable.Repeat(key == "Fk" ? 17d : 42d, bars.Length).ToArray();
            if (batch) Assert.Equal(expected, Data(bars).CalculateKurtosisIndicator().OutputValues[key]);
            else { using var raw = IndicatorCompute.ComputeKurtosisIndicatorFast(Data(bars), context, key); Assert.Equal(expected, raw.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new KurtosisIndicatorState(); using var control = new KurtosisIndicatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
