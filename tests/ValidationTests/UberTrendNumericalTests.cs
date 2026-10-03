using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class UberTrendNumericalTests
{
    private static Bar[] Bars(double[] prices, double[]? volumes = null) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, volumes?[i] ?? 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("UT", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(UberTrendIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentNestedRatios(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.UberTrendOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedPricePreservesOriginalVolume(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var length = (int)indicator.CreateOptions().GetType().GetProperty("Length")!.GetValue(indicator.CreateOptions())!;
        var bars = Bars(Enumerable.Range(0, 40).Select(i => 100d + i).ToArray(), Enumerable.Range(0, 40).Select(i => 1d + i % 5).ToArray()); var selected = bars.Select((_, i) => (double)(i * 7 % 13)).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.UberTrendOutputs(projected, indicator)["Uti"];
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeUberTrendIndicatorFast(data, context, length); Assert.Equal(expected, actual.ToArray()); Assert.Equal(selected, data.ChainedValues);
        data.CalculateUberTrendIndicator(length); Assert.Equal(expected, data.CustomValuesList); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices); Assert.Equal(bars.Select(b => b.Volume), data.Volumes);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (double[] Values, Signal[] Signals) Check(Bar[] bars, int length = 2)
    {
        var expected = BuiltInFormulaReferences.UberTrendValues(bars, length); var batch = Data(bars).CalculateUberTrendIndicator(length); Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeUberTrendIndicatorFast(Data(bars), context, length); Assert.Equal(expected.Values, fast.ToArray());
        var output = Enumerable.Repeat(42d, bars.Length + 1).ToArray(); OscillatorCore.UberTrendIndicator(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), output, length); Assert.Equal(expected.Values, output.Take(bars.Length)); Assert.Equal(42, output[^1]);
        using var native = new UberTrendIndicatorState(length); var raw = new UberTrendWindow(length);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 2d, 0, 4, 1, 3 })) { native.Update(Native(b), true, true); raw.Next(b.Close, b.Volume, true); } native.Reset(); raw.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(new[] { -17d })[0]), false, false); raw.Next(-17, 3, false);
                foreach (var final in new[] { false, false, true }) { var b = bars[i]; var point = native.Update(Native(b), final, true); var direct = raw.Next(b.Close, b.Volume, final); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(point.Value, point.Outputs!["Uti"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Trade); }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentDirectionalVolumeAndSignalHand()
    {
        var result = Check(Bars(new[] { 1d, 2, 1, 3, 2, 4 }, new[] { 1d, 2, 3, 4, 5, 6 }));
        Assert.Equal(new[] { -1d, -1, .2, .5, 2d / 3, 7d / 13 }, result.Values);
        Assert.Equal(new[] { Signal.StrongSell, Signal.None, Signal.StrongBuy, Signal.Buy, Signal.Buy, Signal.StrongSell }, result.Signals);
    }
    [Fact]
    public void WideMovesAndSubnormalVolumesPreserveRatios()
    {
        var e = double.Epsilon; var m = double.MaxValue;
        foreach (var prices in new[] { new[] { -m, m, 0, -m, m, 0 }, new[] { 0d, e, 0, 2 * e, e, 3 * e } })
        foreach (var volumes in new[] { new[] { m, e, m, e, m, e }, new[] { e, m, e, m, e, m }, new[] { 1d, 2, 3, 4, 5, 6 } }) Assert.All(Check(Bars(prices, volumes)).Values, x => Assert.True(double.IsFinite(x)));
        var ordinary = Check(Bars(new[] { 1d, 2, 1, 3, 2, 4 }, new[] { 1d, 2, 3, 4, 5, 6 }));
        var tiny = Check(Bars(new[] { e, 2 * e, e, 3 * e, 2 * e, 4 * e }, new[] { e, 2 * e, 3 * e, 4 * e, 5 * e, 6 * e })); Assert.Equal(ordinary.Values, tiny.Values); Assert.Equal(ordinary.Signals, tiny.Signals);
    }
    [Fact]
    public void SignedVolumePoleAndExactNeighborDecisions()
    {
        foreach (var volume in new[] { Math.BitDecrement(-1d), -1d, Math.BitIncrement(-1d), 0d, 1d })
        { var result = Check(Bars(new[] { 0d, 1, 0 }, new[] { 1d, 1, volume })); if (volume == -1) Assert.Equal(0, result.Values[2]); else if (volume < -1) Assert.True(result.Values[2] > 1); else if (volume < 0) Assert.True(result.Values[2] < -1); }
        Check(Bars(new[] { 0d, 1, 0, 1, 0 }, new[] { 1d, 1, 1, -1, 1 }), 4);
        Check(Bars(new[] { 0d, 1, 0, 1, 0 }, new[] { 1d, 1, 1, 1, -1 }), 4);
    }
    [Fact]
    public void ExtremePeriodsZeroConventionsAndExpiry()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 7, int.MaxValue })
        {
            Check(Bars(Enumerable.Range(0, 32).Select(i => (double)(i * 7 % 13)).ToArray()), length);
            Assert.All(Check(Bars(new[] { 1d, 2, 3, 4 }), length).Values, x => Assert.Equal(-1, x)); Assert.All(Check(Bars(new[] { 4d, 3, 2, 1 }), length).Values, x => Assert.Equal(-1, x));
            Assert.All(Check(Bars(new[] { 2d, 2, 2 }), length).Values, x => Assert.Equal(-1, x)); Check(Array.Empty<Bar>(), length);
        }
    }
    [Fact]
    public void CoreSpanGuardsPreserveOutputAndSupportInPlace()
    {
        var prices = new[] { 1d, 2, 1, 3 }; var volumes = new[] { 1d, 2, 3, 4 }; var shortOutput = new[] { 42d, 42 };
        Assert.Throws<ArgumentException>(() => OscillatorCore.UberTrendIndicator(prices, volumes, shortOutput)); Assert.All(shortOutput, x => Assert.Equal(42, x)); var output = Enumerable.Repeat(42d, 5).ToArray();
        Assert.Throws<ArgumentException>(() => OscillatorCore.UberTrendIndicator(prices, new[] { 1d }, output)); Assert.All(output, x => Assert.Equal(42, x));
        var expected = BuiltInFormulaReferences.UberTrendValues(Bars(prices, volumes), 2).Values; var closeCopy = prices.ToArray(); OscillatorCore.UberTrendIndicator(closeCopy, volumes, closeCopy, 2); Assert.Equal(expected, closeCopy);
        var volumeCopy = volumes.ToArray(); OscillatorCore.UberTrendIndicator(prices, volumeCopy, volumeCopy, 2); Assert.Equal(expected, volumeCopy);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.UberTrendIndicator(new[] { 1d, bad }, volumes, output)); Assert.All(output, x => Assert.Equal(42, x));
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.UberTrendIndicator(prices, new[] { 1d, 2, bad, 4 }, output)); Assert.All(output, x => Assert.Equal(42, x));
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceDirectionalHistory()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var actual = new UberTrendIndicatorState(2); using var expected = new UberTrendIndicatorState(2); actual.Update(Native(Bars(new[] { 1d })[0]), true, true); expected.Update(Native(Bars(new[] { 1d })[0]), true, true); var v = new[] { 2d, 2, 2, 2, 1 }; v[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 3d, 0, 4, 2 })) Assert.Equal(expected.Update(Native(b), true, true).Value, actual.Update(Native(b), true, true).Value);
        }
    }
}
