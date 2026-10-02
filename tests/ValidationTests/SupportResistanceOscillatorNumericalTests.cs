using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SupportResistanceOscillatorNumericalTests
{
    private static Bar Candle(double open, double high, double low, double close) => new(DateTime.UnixEpoch, open, high, low, close, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SRO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SupportAndResistanceOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRatio(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, BuiltInFormulaReferences.SupportResistanceOscillatorOutputs, IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (double[] Line, Signal[] Signals) Check(Bar[] bars)
    {
        var expected = BuiltInFormulaReferences.SupportResistanceOscillatorValues(bars); var batch = Data(bars).CalculateSupportAndResistanceOscillator();
        Assert.Equal(expected.Line, batch.OutputValues["Sro"]); Assert.Equal(expected.Line, batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeSupportAndResistanceOscillatorFast(Data(bars), context);
        Assert.Equal(expected.Line, fast.ToArray());
        var core = new double[bars.Length]; OscillatorCore.SupportAndResistanceOscillator(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core); Assert.Equal(expected.Line, core);
        var state = new SupportAndResistanceOscillatorState(); var direct = new SupportResistanceOscillatorWindow();
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(2, 3, 1, 2)), true, true); direct.Next(2, 3, 1, 2, true); state.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i]; state.Update(Native(Candle(-double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue)), false, false); direct.Next(-double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(b), final, true); var raw = direct.Next(b.Open, b.High, b.Low, b.Close, final);
                    Assert.Equal(expected.Line[i], point.Value); Assert.Equal(expected.Line[i], point.Outputs!["Sro"]); Assert.Equal(expected.Line[i], raw.Line); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected;
    }
    private static Bar[] Hand() => new[] { Candle(2, 3, 1, 2), Candle(4, 5, 3, 4), Candle(2, 3, 1, 2), Candle(-8, 2, 0, 2), Candle(8, 2, 0, 0), Candle(0, 0, 0, 0) };
    [Fact]
    public void IndependentHandsCoverGapsClampsAndZeroRange()
    {
        var result = Check(Hand()); Assert.Equal(new[] { .5, 1d / 3, 1d / 3, 1, 0, 0 }, result.Line);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongSell, Signal.None, Signal.StrongBuy, Signal.StrongSell, Signal.None }, result.Signals);
        Assert.Equal(new[] { 0d }, Check(new[] { Candle(0, -1, 1, 0) }).Line);
    }
    [Fact]
    public void OverflowingRangesAndNumeratorsRetainBoundedRatios()
    {
        var m = double.MaxValue;
        var result = Check(new[] { Candle(0, m, -m, 0), Candle(-m, m, -m, m), Candle(m, m, -m, -m), Candle(0, m, 0, m) });
        Assert.Equal(new[] { .5, 1d, 0, .5 }, result.Line);
        foreach (var scale in new[] { double.Epsilon, 1d, m / 8 })
            Check(new[] { Candle(0, scale, 0, scale), Candle(scale, 2 * scale, -scale, 0), Candle(-scale, scale, -2 * scale, scale) });
    }
    [Fact]
    public void SubnormalSlopeSurvivesEqualPublishedValues()
    {
        var result = Check(new[] { Candle(0, 1, 0, 0), Candle(0, 1, 0, double.Epsilon), Candle(0, 1, 0, 0) });
        Assert.Equal(new[] { .5, .5, .5 }, result.Line); Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.StrongSell }, result.Signals);
        var declining = Check(new[] { Candle(0, 1, 0, 1), Candle(0, 1, 0, .25), Candle(0, 1, 0, 0) }); Assert.Equal(Signal.Sell, declining.Signals[2]);
        Assert.Equal(Signal.Buy, Check(new[] { Candle(0, 1, 0, 0), Candle(0, 1, 0, .5), Candle(0, 1, 0, 1) }).Signals[2]);
        Assert.Equal(Signal.Sell, Check(new[] { Candle(0, 1, 0, 1), Candle(0, 1, 0, .5), Candle(0, 1, 0, 0) }).Signals[2]);
    }
    [Fact]
    public void CoreHandlesEmptyInPlaceTailAndShortSpans()
    {
        var bars = Hand(); var opens = bars.Select(b => b.Open).ToArray(); var highs = bars.Select(b => b.High).ToArray(); var lows = bars.Select(b => b.Low).ToArray(); var closes = bars.Select(b => b.Close).ToArray(); var expected = BuiltInFormulaReferences.SupportResistanceOscillatorValues(bars).Line;
        var output = Enumerable.Repeat(123d, bars.Length + 2).ToArray(); OscillatorCore.SupportAndResistanceOscillator(opens, highs, lows, closes, output); Assert.Equal(expected, output.Take(bars.Length)); Assert.Equal(new[] { 123d, 123 }, output.Skip(bars.Length));
        OscillatorCore.SupportAndResistanceOscillator(opens, highs, lows, closes, closes); Assert.Equal(expected, closes);
        OscillatorCore.SupportAndResistanceOscillator(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), output); Assert.Equal(expected, output.Take(bars.Length));
        Assert.Throws<ArgumentException>(() => OscillatorCore.SupportAndResistanceOscillator(opens, highs, lows, closes, new double[1]));
        Assert.Throws<ArgumentException>(() => OscillatorCore.SupportAndResistanceOscillator(new double[1], highs, lows, closes, output));
        Assert.Throws<ArgumentException>(() => OscillatorCore.SupportAndResistanceOscillator(opens, new double[1], lows, closes, output));
        Assert.Throws<ArgumentException>(() => OscillatorCore.SupportAndResistanceOscillator(opens, highs, new double[1], closes, output));
    }
    [Fact]
    public void SelectedPricesOutsideCandlePreserveAllOriginalFields()
    {
        var bars = Enumerable.Range(0, 40).Select(i => Candle(20 + i % 3, 30, 10, 21 + i % 5)).ToArray(); var selected = bars.Select((_, i) => i % 2 == 0 ? -8d : 80d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.SupportResistanceOscillatorValues(projected).Line;
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateSupportAndResistanceOscillator().CustomValuesList);
        foreach (var length in new[] { 0, 1, 14, int.MaxValue })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeSupportAndResistanceOscillatorFast(data, context, length); Assert.Equal(expected, actual.ToArray());
            Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.Open), data.OpenPrices); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices); Assert.Equal(bars.Select(b => b.Volume), data.Volumes);
        }
    }
    [Fact]
    public async Task BuilderAndLiveSelectedSourceOutsideCandleRetainRanges()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 20 + i % 3, 30, 10, 21 + i % 5, 1)).ToArray();
        var source = new PriceChange(); var indicator = new SupportAndResistanceOscillator(1).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray(); Assert.All(selected, v => Assert.True(v < 10));
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.SupportResistanceOscillatorValues(projected).Line;
        Assert.Equal(expected, run[indicator].ToArray());
        var feed = OoplesFinance.StockIndicators.Indicators.Bars.Live(); using var live = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup().ConfigureIndicators(source, indicator).BuildAsync();
        foreach (var b in bars) feed.Publish(b); feed.Complete(); var actual = new List<double>();
        await foreach (var snapshot in live) actual.Add(snapshot[indicator]); Assert.Equal(expected, actual);
    }
    [Fact]
    public void NoAverageSlotsAreConsumedAndEmptyRoutesWork()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Unexpected average");
        using var armed = ComponentAverage.Arm(new[] { callback }); Check(Hand()); Check(Array.Empty<Bar>()); Assert.Equal(0, ComponentAverage.Substitutions);
        var state = new SupportAndResistanceOscillatorState(); Assert.Null(state.Update(Native(Hand()[0]), true, false).Outputs);
    }
    [Fact]
    public void NonfiniteCandlesCannotAdvancePreviewOrFinalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            var state = new SupportAndResistanceOscillatorState(); var control = new SupportAndResistanceOscillatorState();
            foreach (var b in Hand()) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 4, 0, 2, 1 }; values[field] = bad; var invalid = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(invalid), final, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(new[] { invalid }).CalculateSupportAndResistanceOscillator());
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeSupportAndResistanceOscillatorFast(Data(new[] { invalid }), context));
            foreach (var b in Hand()) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
        foreach (var field in Enumerable.Range(0, 4))
        {
            var values = new[] { 1d, 4, 0, 2 }; values[field] = double.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.SupportAndResistanceOscillator(new[] { values[0] }, new[] { values[1] }, new[] { values[2] }, new[] { values[3] }, new double[1]));
        }
    }
}
