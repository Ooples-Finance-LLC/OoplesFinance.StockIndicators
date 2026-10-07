using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class HerrickPayoffNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(HerrickPayoffIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    private static Bar B(double price, double open = 2, double close = 4, double volume = 3)
        => new(DateTime.UnixEpoch, open, price, price, close, volume);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select((_,i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static OhlcvBar Native(Bar b) => new("HPI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentPayoff(IndicatorValidationCase c, string route)
    {
        var o = (HerrickPayoffIndexSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => new Dictionary<string, double[]> { ["Hpi"] = BuiltInFormulaReferences.HerrickPayoffValues(bars, o.PointValue) }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcesPreserveOriginalCandles(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void PublishedOutputRejectsFaults(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    [Fact]
    public void DirectPayoffHasIndependentSignedOpeningHands()
    {
        var bars = new[] { B(1, 2, 4), B(3, 4, 6), B(2, 1, 8), B(4, 0, 9) };
        var expected = new[] { 0d, 9, 0, 6 };
        Assert.Equal(expected, BuiltInFormulaReferences.HerrickPayoffValues(bars, 1));
        var batch = Data(bars).CalculateHerrickPayoffIndex(1);
        Assert.Equal(expected, batch.OutputValues["Hpi"]);
        var negative = new[] { B(1, -2, 4), B(3, -1, 6) };
        Assert.Equal(3d, Data(negative).CalculateHerrickPayoffIndex(1).OutputValues["Hpi"][1]);
        Assert.Equal(new[] { 0d, -9, 0, -6 }, Data(bars).CalculateHerrickPayoffIndex(-1).OutputValues["Hpi"]);
        var state = new HerrickPayoffIndexState(1);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(91)), false, false);
                foreach (var final in new[] { false, false, true })
                    Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Outputs!["Hpi"]);
            }
        }
    }

    [Fact]
    public void IntermediateOverflowAndUnderflowDoNotCorruptFinitePayoff()
    {
        var large = new[] { B(-double.MaxValue, 0, 1, 0), B(double.MaxValue, 0, 1, double.Epsilon) };
        var tiny = new[] { B(0, 0, 1, 0), B(double.Epsilon, 0, 1, double.MaxValue) };
        foreach (var (bars, factor) in new[] { (large, 1d), (tiny, .5) })
        {
            var expected = BuiltInFormulaReferences.HerrickPayoffValues(bars, factor);
            Assert.True(double.IsFinite(expected[1]) && expected[1] > 0);
            Assert.Equal(expected, Data(bars).CalculateHerrickPayoffIndex(factor).OutputValues["Hpi"]);
            var state = new HerrickPayoffIndexState(factor);
            for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(Native(bars[i]), true, false).Value);
        }
        Assert.Equal(new[] { 0d, 0 }, Data(large).CalculateHerrickPayoffIndex(0).OutputValues["Hpi"]);
    }

    [Fact]
    public void RejectedFieldsDoNotAdvanceHistory()
    {
        var actual = new HerrickPayoffIndexState(1); var control = new HerrickPayoffIndexState(1);
        actual.Update(Native(B(1)), true, false); control.Update(Native(B(1)), true, false);
        foreach (var bad in new[] { B(double.NaN), B(2, double.PositiveInfinity), B(2, close: double.NaN), B(2, volume: double.NegativeInfinity) })
        foreach (var final in new[] { false, true })
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(bad), final, true));
        foreach (var bar in new[] { B(3), B(-1, -2, 6), B(5, 0, -4) })
            Assert.Equal(control.Update(Native(bar), true, false).Value, actual.Update(Native(bar), true, false).Value);
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HerrickPayoffIndexState(factor));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data([]).CalculateHerrickPayoffIndex(factor));
        }
        Assert.Empty(Data([]).CalculateHerrickPayoffIndex().OutputValues["Hpi"]);
    }
}
