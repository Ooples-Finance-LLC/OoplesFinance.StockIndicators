using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SelfAdjustingRsiNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), BarsOf(prices).Select(b => b.Time));

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task BandsMeasureRsiPopulationDeviationWithIndependentSignalPeriod(int exponent)
    {
        var v = Math.ScaleB(1d, exponent); var prices = new[] { -v, v, -v };
        var indicator = new SelfAdjustingRelativeStrengthIndex(2, 1);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(prices))).ConfigureIndicators(indicator).BuildAsync();
        var expected = new[] { new[] { 100d, 100, 50 }, new[] { 100d, 100, 50 }, new[] { 50d, 50, 100 }, new[] { 50d, 50, 0 } };
        for (var slot = 0; slot < expected.Length; slot++) Assert.Equal(expected[slot], result[indicator.Outputs[slot]].ToArray());
        var batch = Data(prices).CalculateSelfAdjustingRelativeStrengthIndex(length: 2, smoothingLength: 1);
        foreach (var pair in new[] { ("SaRsi", 0), ("Signal", 1), ("ObLevel", 2), ("OsLevel", 3) }) Assert.Equal(expected[pair.Item2], batch.OutputValues[pair.Item1]);
    }

    [Fact]
    public void RawArmUsesSelectedPricesForEveryOutput()
    {
        var prices = new[] { 1d, 3, 1, 2 };
        var indicator = new SelfAdjustingRelativeStrengthIndex(2, 3, 1.5);
        var expected = BuiltInFormulaReferences.SelfAdjustingRsiOutputs(BarsOf(prices), indicator);
        var data = Data(new[] { 9d, 9, 9, 9 }); data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext();
        foreach (var pair in new[] { ("SaRsi", IndicatorCompute.SelfAdjustingRsiSeries.SaRsi), ("Signal", IndicatorCompute.SelfAdjustingRsiSeries.Signal), ("ObLevel", IndicatorCompute.SelfAdjustingRsiSeries.ObLevel), ("OsLevel", IndicatorCompute.SelfAdjustingRsiSeries.OsLevel) })
        {
            using var raw = IndicatorCompute.ComputeSelfAdjustingRsiFast(data, context, 2, MovingAvgType.SimpleMovingAverage, 3, 1.5, pair.Item2);
            Assert.Equal(expected[pair.Item1], raw.Span.ToArray());
        }
    }

    [Fact]
    public void NativeSelfAdjustingRsiRejectsInvalidFieldsWithoutAdvancing()
    {
        IStreamingIndicatorState Create() => new SelfAdjustingRelativeStrengthIndexState(length: 2);
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

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "SelfAdjustingRelativeStrengthIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.SelfAdjustingRsiOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
