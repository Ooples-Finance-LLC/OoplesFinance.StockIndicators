using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TrueRangeAdjustedNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TAI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TrueRangeAdjustedExponentialMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAdaptiveRecurrence(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TrueRangeAdjustedOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var o = (TrueRangeAdjustedExponentialMovingAverageSpecOptions)indicator.CreateOptions();
        var bars = Bars(Enumerable.Range(0, 64).Select(i => 20d + i % 5).ToArray()); var selected = bars.Select((_, i) => i % 7 - 3d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.TrueRangeAdjustedOutputs(projected, indicator);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var pair in expected)
        { using var actual = IndicatorCompute.ComputeTrueRangeAdjustedExponentialMovingAverageFast(data, context, o.Length, o.Mult); Assert.Equal(pair.Value, actual.ToArray()); }
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateTrueRangeAdjustedExponentialMovingAverage(o.Length, o.Mult);
        Assert.Equal(expected["Trema"], batch.CustomValuesList); Assert.Equal(bars.Select(b => b.High), batch.HighPrices); Assert.Equal(bars.Select(b => b.Low), batch.LowPrices); Assert.Equal(bars.Select(b => b.Close), batch.ClosePrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (double[] Line, Signal[] Signals) Check(Bar[] bars, int length = 3, double mult = 1.5)
    {
        var expected = BuiltInFormulaReferences.TrueRangeAdjustedValues(bars, length, mult); var data = Data(bars).CalculateTrueRangeAdjustedExponentialMovingAverage(length, mult);
        Assert.Equal(expected.Line, data.CustomValuesList); Assert.Equal(expected.Line, data.OutputValues["Trema"]); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeTrueRangeAdjustedExponentialMovingAverageFast(Data(bars), context, length, mult); Assert.Equal(expected.Line, fast.ToArray());
        var prices = bars.Select(b => b.Close).ToArray(); var highs = bars.Select(b => b.High).ToArray(); var lows = bars.Select(b => b.Low).ToArray(); var core = new double[bars.Length + 1]; core[^1] = 42;
        MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(prices, highs, lows, core, length, mult); Assert.Equal(expected.Line, core.Take(bars.Length)); Assert.Equal(42, core[^1]);
        if (mult == 1.5) { new TraemaCore().ComputeOhlc(highs, lows, prices, core, length); Assert.Equal(expected.Line, core.Take(bars.Length)); }
        var native = new TrueRangeAdjustedExponentialMovingAverageState(length, mult); var raw = new TrueRangeAdjustedWindow(length, mult);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Bars(3, -2, 7)) { native.Update(Native(seed), true, true); raw.Next(seed.Close, seed.High, seed.Low, true); }
            native.Reset(); raw.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(-17)[0]), false, false); raw.Next(-17, 0, -17, false);
                foreach (var final in new[] { false, false, true })
                { var b = bars[i]; var next = native.Update(Native(b), final, true); var direct = raw.Next(b.Close, b.High, b.Low, final); Assert.Equal(expected.Line[i], next.Value); Assert.Equal(next.Value, next.Outputs!["Trema"]); Assert.Equal(next.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Trade); }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentGainAndSignalHands()
    {
        var result = Check(Bars(2, 4, 2, 8), 1, .5);
        Assert.Equal(new[] { 2d, 3, 2.5, 5.25 }, result.Line);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.StrongSell, Signal.StrongBuy }, result.Signals);
        Assert.Equal(new[] { 2d, 2, 2, 2 }, Check(Bars(2, 4, 2, 8), 3, 0).Line);
        Assert.Equal(new[] { 2d, 3, 3.5 }, Check(Bars(2, 4, 4), 1, .5).Line);
        Check(Bars(2, 4, 2, 8, 1, 7, 3, 5), 3, -.5);
        Check(Bars(0, 0, 0, 3, 3, 3, -2, 0, 0), 3, 1.5);
    }
    [Fact]
    public void OverflowingRangesAndFeedbackRecoverWithoutReset()
    {
        var m = double.MaxValue;
        Assert.Equal(new[] { m, 0d, m / 2 }, Check(Bars(m, -m, m), 1, .5).Line);
        Assert.Equal(new[] { m, double.NegativeInfinity, m }, Check(Bars(m, -m, -m), 1, 2).Line);
        var bars = Bars(m, -m, m / 2, 0, m / 4, -m / 2, 1, 2).Select(b => new Bar(b.Time, b.Open, m, -m, b.Close, 1)).ToArray(); Check(bars, 4);
    }
    [Fact]
    public void SubnormalGainAndUnpublishedSlopesSurvive()
    {
        var e = double.Epsilon; var m = double.MaxValue;
        Assert.Equal(new[] { 0d, e * m }, Check(Bars(0, m), 1, e).Line);
        var result = Check(Bars(0, e, 0), 1, .5); Assert.Equal(new[] { 0d, 0, 0 }, result.Line);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell }, result.Signals);
    }
    [Fact]
    public void ExtremePeriodsAndMultiplierBoundariesRemainDefined()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 7, int.MaxValue }) Check(Bars(0, 2, 4, 0, -2, 8, 1), period);
        foreach (var mult in new[] { -double.MaxValue, double.Epsilon, Math.BitDecrement(2d), 2d, Math.BitIncrement(2d), double.MaxValue }) Check(Bars(1, 2, 0, 1), 1, mult);
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void CoreSpanGuardsPreserveOutputAndAllowInPlaceInput()
    {
        var output = new[] { 19d, 19 }; var p = new[] { 2d, 4, 2 };
        Assert.Throws<ArgumentException>(() => MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(p, p, p, output)); Assert.Equal(new[] { 19d, 19 }, output);
        var longer = new[] { 19d, 19, 19, 19 };
        Assert.Throws<ArgumentException>(() => MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(p, new[] { 2d }, p, longer)); Assert.All(longer, x => Assert.Equal(19, x));
        Assert.Throws<ArgumentException>(() => MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(p, p, new[] { 2d }, longer)); Assert.All(longer, x => Assert.Equal(19, x));
        var expected = BuiltInFormulaReferences.TrueRangeAdjustedValues(Bars(p), 3, 1.5).Line; var high = p.ToArray(); var low = p.ToArray(); MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(p, high, low, p, 3); Assert.Equal(expected, p);
    }
    [Fact]
    public void InvalidCandlesAndMultipliersCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrueRangeAdjustedWindow(2, bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Bars(1)).CalculateTrueRangeAdjustedExponentialMovingAverage(2, bad));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                var actual = new TrueRangeAdjustedExponentialMovingAverageState(2); var expected = new TrueRangeAdjustedExponentialMovingAverageState(2);
                actual.Update(Native(Bars(1)[0]), true, true); expected.Update(Native(Bars(1)[0]), true, true);
                var v = new[] { 2d, 2, 2, 2, 1 }; v[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
                foreach (var b in Bars(2, -3, 4)) Assert.Equal(expected.Update(Native(b), true, true).Value, actual.Update(Native(b), true, true).Value);
            }
            var p = new[] { 1d, bad }; var valid = new[] { 1d, 2 }; var output = new[] { 42d, 42 };
            Assert.Throws<ArgumentOutOfRangeException>(() => MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(p, valid, valid, output)); Assert.Equal(new[] { 42d, 42 }, output);
            Assert.Throws<ArgumentOutOfRangeException>(() => MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(valid, p, valid, output)); Assert.Equal(new[] { 42d, 42 }, output);
            Assert.Throws<ArgumentOutOfRangeException>(() => MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(valid, valid, p, output)); Assert.Equal(new[] { 42d, 42 }, output);
        }
    }
}
