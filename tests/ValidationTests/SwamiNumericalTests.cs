using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SwamiNumericalTests
{
    private static Bar Candle(double close, double high, double low) => new(DateTime.UnixEpoch, close, high, low, close, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SWAMI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SwamiStochastics)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWeightedSums(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.SwamiOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (double[] Line, Signal[] Signals) Check(Bar[] bars, int fast = 1, int slow = 3)
    {
        var expected = BuiltInFormulaReferences.SwamiValues(bars, fast, slow); var batch = Data(bars).CalculateSwamiStochastics(fast, slow);
        var valuesOnly = BuiltInFormulaReferences.SwamiValues(bars, fast, slow, includeSignals: false); Assert.Equal(expected.Line, valuesOnly.Line); Assert.Empty(valuesOnly.Signals);
        Assert.Equal(expected.Line, batch.OutputValues["Ss"]); Assert.Equal(expected.Line, batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeSwamiStochasticsFast(Data(bars), context, fast, slow); Assert.Equal(expected.Line, output.ToArray());
        using var native = new SwamiStochasticsState(fast, slow); using var direct = new SwamiWindow(fast, slow);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Hand().Append(Candle(100, 2, 0))) { native.Update(Native(seed), true, true); direct.Next(seed.Close, seed.High, seed.Low, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var alternate = Candle(-double.MaxValue, double.MaxValue, -double.MaxValue); native.Update(Native(alternate), false, false); direct.Next(alternate.Close, alternate.High, alternate.Low, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = native.Update(Native(b), final, true); var raw = direct.Next(b.Close, b.High, b.Low, final);
                    Assert.Equal(expected.Line[i], point.Value); Assert.Equal(expected.Line[i], point.Outputs!["Ss"]); Assert.Equal(expected.Line[i], raw.Line); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected;
    }
    private static Bar[] Hand() => new[] { 2d, 4, 2, 8, -1, 3 }.Select(v => Candle(v, v + 1, v - 1)).ToArray();
    [Fact]
    public void IndependentHandsCoverBothRecurrencesAndExpiry()
    {
        var result = Check(Hand()); Assert.Equal(new[] { .1, .22, 847d / 3250, 52267d / 146250, 33875419d / 97256250, 42846518179d / 111358406250 }, result.Line);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongBuy, Signal.Buy, Signal.StrongBuy, Signal.StrongSell, Signal.StrongBuy }, result.Signals);
    }
    [Fact]
    public void OverflowingAndSubnormalRangesRetainTheirRatios()
    {
        var m = double.MaxValue; var result = Check(new[] { Candle(0, m, -m), Candle(m, m, -m) }); Assert.Equal(new[] { .1, 37d / 150 }, result.Line);
        Assert.Equal(.2, Check(new[] { Candle(double.Epsilon, double.Epsilon, 0) }).Line[0]);
        Assert.Equal(.1, Check(new[] { Candle(double.Epsilon, 2 * double.Epsilon, 0) }).Line[0]);
        foreach (var scale in new[] { double.Epsilon, 1d, m / 4 }) Check(Enumerable.Range(0, 17).Select(i => Candle((i % 3 - 1) * scale, 2 * scale, -2 * scale)).ToArray());
    }
    [Fact]
    public void ZeroDenominatorResetsPublishedStochasticWhileNumeratorCarries()
    {
        var result = Check(new[] { Candle(2, 2, 0), Candle(0, -1, 0), Candle(2, 2, 0) }, 1, 1); Assert.Equal(new[] { .2, 0, .25 }, result.Line);
        Assert.All(Check(Enumerable.Repeat(Candle(7, 7, 7), 6).ToArray()).Line, v => Assert.Equal(0, v));
    }
    [Fact]
    public void ClampFeedsBackBeforeFollowingBars()
    {
        var upper = Check(new[] { Candle(20, 2, 0), Candle(0, 2, 0), Candle(0, 2, 0), Candle(0, 2, 0) }, 1, 1); Assert.Equal(new[] { 1d, 1, 1, 14d / 15 }, upper.Line);
        var lower = Check(new[] { Candle(-20, 2, 0), Candle(0, 2, 0), Candle(100, 2, 0) }, 1, 1); Assert.Equal(new[] { 0d, 0, 1 }, lower.Line);
    }
    [Fact]
    public void EqualPublishedTailValuesStillHavePositiveExactSlopes()
    {
        var result = Check(Enumerable.Repeat(Candle(0, 1, -1), 200).ToArray(), 1, 1); Assert.Equal(.5, result.Line[^1]); Assert.Equal(.5, result.Line[^2]); Assert.Equal(Signal.Buy, result.Signals[^1]);
        var declining = Check(Enumerable.Range(0, 25).Select(i => Candle(i == 0 ? 1 : 0, 1, 0)).ToArray(), 1, 1); Assert.Contains(Signal.Sell, declining.Signals); Assert.Contains(Signal.StrongSell, declining.Signals);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.Buy }, Check(new[] { Candle(1.25, 2, 0), Candle(1.625, 2, 0), Candle(1.9375, 2, 0) }, 1, 1).Signals);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongSell, Signal.Sell }, Check(new[] { Candle(5, 2, 0), Candle(-2.875, 2, 0), Candle(-.6875, 2, 0) }, 1, 1).Signals);
    }
    [Fact]
    public void EqualReversedNegativeAndExtremePeriodsUseNormalizedLazyWidths()
    {
        foreach (var (fast, slow) in new[] { (1, 1), (8, 2), (int.MinValue, 3), (1, int.MaxValue), (int.MaxValue, int.MinValue), (0, 0) }) Check(Hand(), fast, slow);
        var prices = Enumerable.Range(0, 43).Select(i => Candle(i % 11 - 5, 7 + i % 7, -9 - i % 3)).ToArray(); Check(prices, 2, 6);
    }
    [Fact]
    public void ExplicitFastSelectedInputPreservesOriginalCandles()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 20, 30 + i % 4, 10 - i % 3, 22 + i % 3, 1)).ToArray(); var selected = bars.Select((_, i) => i % 2 == 0 ? -20d : 80d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.SwamiValues(projected, 1, 3).Line;
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeSwamiStochasticsFast(data, context, 1, 3); Assert.Equal(expected, actual.ToArray());
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.Open), data.OpenPrices); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateSwamiStochastics(1, 3).CustomValuesList);
    }
    [Fact]
    public async Task BuilderAndLiveOutsideRangeSelectionPreserveOriginalCandles()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 20, 30 + i % 4, 10 - i % 3, 22 + i % 3, 1)).ToArray();
        var source = new PriceChange(); var indicator = new SwamiStochastics(1, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray(); Assert.All(selected, v => Assert.True(v < 7)); var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.SwamiValues(projected, 1, 3).Line; Assert.Equal(expected, run[indicator].ToArray());
        // The native wrapper applies ICustomInputRangePolicy; the object routes above do not.
        using var native = new CustomInputState(new SwamiStochasticsState(1, 3),
            bar => selected[(int)(bar.EndTime - DateTime.UnixEpoch).TotalMinutes]);
        Assert.Equal(expected, bars.Select(bar => native.Update(Native(bar), true, true).Value));
        var feed = OoplesFinance.StockIndicators.Indicators.Bars.Live(); using var live = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup().ConfigureIndicators(source, indicator).BuildAsync(); foreach (var b in bars) feed.Publish(b); feed.Complete(); var actual = new List<double>();
        await foreach (var snapshot in live) actual.Add(snapshot[indicator]); Assert.Equal(expected, actual);
    }
    [Fact]
    public void VerifiedArmHasNoAverageSlotsAndEmptyInputsWork()
    {
        Assert.Contains(typeof(SwamiStochasticsSpecOptions), BuilderVerifiedArms.Arms);
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Unexpected average"); using var armed = ComponentAverage.Arm(new[] { callback }); Check(Hand()); Check(Array.Empty<Bar>()); Assert.Equal(0, ComponentAverage.Substitutions);
        using var state = new SwamiStochasticsState(); Assert.Null(state.Update(Native(Hand()[0]), true, false).Outputs);
    }
    [Fact]
    public void NonfiniteCandlesCannotAdvanceExtremaOrEitherRecurrence()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new SwamiStochasticsState(1, 3); using var control = new SwamiStochasticsState(1, 3); foreach (var b in Hand()) { state.Update(Native(b), true, true); control.Update(Native(b), true, true); }
            var values = new[] { 1d, 4, 0, 2, 1 }; values[field] = bad; var invalid = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]); Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(invalid), final, true));
            if (field is 1 or 2 or 3)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => Data(new[] { invalid }).CalculateSwamiStochastics()); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeSwamiStochasticsFast(Data(new[] { invalid }), context));
            }
            foreach (var b in Hand()) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
