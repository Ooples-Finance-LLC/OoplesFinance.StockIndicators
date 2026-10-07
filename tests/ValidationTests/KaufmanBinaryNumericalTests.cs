using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KaufmanBinaryNumericalTests
{
    private static Bar[] Bars(params double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KBW", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KaufmanBinaryWave)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void ExistingRoutesMatchIndependentRationalReference(IndicatorValidationCase c, string route)
    {
        var o = (KaufmanBinaryWaveSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KaufmanBinaryValues(bars, o.Length, o.FastSc, o.SlowSc, o.FilterPct), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcesPreserveFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void PublishedOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static void Check(Bar[] bars, int length, double fast, double slow, double filter, double[]? hand = null)
    {
        var expected = BuiltInFormulaReferences.KaufmanBinaryValues(bars, length, fast, slow, filter)["Kbw"];
        if (hand is not null) Assert.Equal(hand, expected);
        var batch = Data(bars).CalculateKaufmanBinaryWave(length, fast, slow, filter);
        Assert.Equal(expected, batch.OutputValues["Kbw"]);
        using var state = new KaufmanBinaryWaveState(length, fast, slow, filter);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Bars(7)[0]), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(-double.MaxValue)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Kbw"]);
                }
            }
        }
        var signals = expected.Select((v, i) =>
        {
            var previous = i == 0 ? 0 : expected[i - 1];
            return v > 0 && v > previous ? Signal.StrongBuy : v < 0 && v < previous ? Signal.StrongSell : v > 0 ? Signal.Buy : v < 0 ? Signal.Sell : Signal.None;
        });
        Assert.Equal(signals, batch.SignalsList);
    }

    [Fact]
    public void IndependentEfficiencyHandAndExactThresholdTies()
    {
        Check(Bars(0, 1, 3, 2), 2, 1, 0, 10, [0, 0, 1, 0]);
        Check(Bars(-2, -2, -1, -2, 2), 2, 1, 0, 10, [-1, -1, 0, 0, 1]);
        Check(Bars(-2, -2, 0, -2, 1), 2, 1, 0, 10, [-1, -1, 0, 0, 1]);
        Check(Bars(1, 2, 1), 2, 0, 1, 100, [1, 1, 0]);
        Check(Bars(1, 2, 1), 2, 0, 1, Math.BitDecrement(100), [1, 1, -1]);
        Check(Bars(1, 2, 1), 2, 0, 1, Math.BitIncrement(100), [1, 1, 0]);
        Check(Bars(1, 2, 1), 2, 0, 1, -100, [1, 1, 1]);
        Check(Bars(-2, -1), 2, 0, 1, -100, [-1, -1]);
        Check(Bars(-2, -1), 2, 0, 1, -200, [-1, -1]);
        Check(Bars(-2, -1), 2, 0, 1, Math.BitDecrement(-200), [-1, 1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(2147483647)]
    public void SignedExtremePricesAndPeriodsRetainBoundedDecisions(int length)
    {
        var m = double.MaxValue;
        Check(Bars(m, -m, m, 0, double.Epsilon, -double.Epsilon, -1, 1), length, .6022, .0645, 10);
        Check(Bars(1, -1, 2, -2), length, m, -m, -m);
        Check(Bars(), length, 0, 1, 0);
    }

    [Fact]
    public void DirectChainedInputIsUsedAndCandlesArePreserved()
    {
        var data = Data(Bars(9, 8, 7, 6)); data.SetCustomValues(new List<double> { 0, 1, 3, 2 });
        data.CalculateKaufmanBinaryWave(2, 1, 0, 10);
        Assert.Equal(new[] { 0d, 0, 1, 0 }, data.OutputValues["Kbw"]);
        Assert.Equal(new[] { 9d, 8, 7, 6 }, data.ClosePrices);
    }

    [Fact]
    public void NonfiniteParametersAndPricesAreRejectedWithoutAdvancingState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KaufmanBinaryWaveState(fastSc: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KaufmanBinaryWaveState(slowSc: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KaufmanBinaryWaveState(filterPct: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Bars(1)).CalculateKaufmanBinaryWave(fastSc: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Bars(invalid)).CalculateKaufmanBinaryWave());
            using var actual = new KaufmanBinaryWaveState(2, 1, 0, 10); using var expected = new KaufmanBinaryWaveState(2, 1, 0, 10);
            var seed = Native(Bars(0)[0]); actual.Update(seed, true, false); expected.Update(seed, true, false);
            foreach (var final in new[] { false, true }) Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(Bars(invalid)[0]), final, true));
            foreach (var bar in Bars(1, 3, 2)) Assert.Equal(expected.Update(Native(bar), true, false).Value, actual.Update(Native(bar), true, false).Value);
        }
    }
}
