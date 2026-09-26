using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FoldedRsiNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task FoldedRsiSumsTheDistanceFromFifty(int exponent)
    {
        var v = Math.ScaleB(1d, exponent); var prices = new[] { -v, v, -v, 0d, v, v };
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        var indicator = new FoldedRelativeStrengthIndex(1);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var expected = Enumerable.Repeat(100d, prices.Length).ToArray();
        Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
        var core = new double[prices.Length]; OscillatorCore.FoldedRelativeStrengthIndex(prices, core, 1); Assert.Equal(expected, core);
    }

    [Fact]
    public void CoreUsesThePublishedRollingSumAndRawArmUsesSelectedPrices()
    {
        var prices = new[] { 1d, 3, 1, 1 };
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        var expected = BuiltInFormulaReferences.FoldedRsiOutputs(bars, new FoldedRelativeStrengthIndex(2))["Frsi"];
        Assert.Equal(new[] { 100d, 200, 160, 120 }, expected);
        var core = new double[prices.Length]; OscillatorCore.FoldedRelativeStrengthIndex(prices, core, 2); Assert.Equal(expected, core);
        var stored = new[] { 9d, 9, 9, 9 };
        var data = new StockData(stored, stored, stored, stored, Enumerable.Repeat(1d, 4), bars.Select(b => b.Time)); data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeFoldedRsiFast(data, context, 2); Assert.Equal(expected, raw.Span.ToArray());
        OscillatorCore.FoldedRelativeStrengthIndex(Array.Empty<double>(), Array.Empty<double>(), 2);
        Assert.Throws<ArgumentException>(() => OscillatorCore.FoldedRelativeStrengthIndex(prices, Array.Empty<double>(), 2));
    }

    [Fact]
    public void NativeFoldedRsiRejectsInvalidFieldsWithoutAdvancing()
    {
        IStreamingIndicatorState Create() => new FoldedRelativeStrengthIndexState(length: 2);
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var state = Create(); var control = Create();
            using var stateLifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var v in new[] { 3d, 1, 4 })
            {
                var seed = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, v, 1));
                state.Update(seed, true, true); control.Update(seed, true, true);
            }
            var fields = new[] { 2d, 4, 1, 2, 1 }; fields[field] = invalid;
            var bad = new OhlcvBar("GAINLOSS", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, 2, 1));
            var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    private static OhlcvBar Native(Bar b) => new("FOLDED", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "FoldedRelativeStrengthIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.FoldedRsiOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
