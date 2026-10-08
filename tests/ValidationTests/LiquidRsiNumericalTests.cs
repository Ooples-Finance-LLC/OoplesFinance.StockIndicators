using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class LiquidRsiNumericalTests
{
    private static Bar[] Bars(params (double Price, double Volume)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Price, v.Price, v.Price, v.Price, v.Volume)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("LRSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(LiquidRelativeStrengthIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesExactWeightedProducts(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.LiquidRsiOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.LiquidRsiBudget);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesVolume(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Equal(double expected, double actual)
    {
        if (expected == 0 || expected == 100 || Math.Abs(expected) <= 16 * double.Epsilon) Assert.Equal(expected, actual);
        else Assert.True(double.IsFinite(actual) && actual > 0 && Math.Abs((actual - expected) / expected) <= 4e-15, $"Expected {expected:R}; actual {actual:R}");
    }
    private static double[] Check(Bar[] bars, int length)
    {
        var expected = BuiltInFormulaReferences.LiquidRsiValues(bars, length); var batch = Data(bars).CalculateLiquidRelativeStrengthIndex(length);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeLiquidRelativeStrengthIndexFast(Data(bars), context, length);
        Assert.Equal(batch.CustomValuesList, fast.ToArray());
        var core = Enumerable.Repeat(-777d, bars.Length + 2).ToArray();
        OscillatorCore.LiquidRelativeStrengthIndex(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), core.AsSpan(1, bars.Length), length);
        Assert.Equal(-777, core[0]); Assert.Equal(-777, core[^1]); Assert.Equal(batch.CustomValuesList, core.Skip(1).Take(bars.Length));
        var native = new LiquidRelativeStrengthIndexState(length); var direct = new LiquidRsiWindow(length);
        for (var pass = 0; pass < 2; pass++)
        {
            native.Update(Native(Bars((99, 8), (100, 9))[0]), true, false); native.Update(Native(Bars((99, 8), (100, 9))[1]), true, false);
            direct.Next(99, 8, true); direct.Next(100, 9, true); native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars((777, -999))[0]), false, false); direct.Next(777, -999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); Equal(expected[i], point.Value);
                    Assert.Equal(batch.CustomValuesList[i], point.Value); Assert.Equal(point.Value, point.Outputs!["Lrsi"]); Assert.Equal(point.Value, direct.Next(bars[i].Close, bars[i].Volume, final));
                }
            }
        }
        return batch.CustomValuesList.ToArray();
    }
    [Fact]
    public void HandProductsStartAtZeroAndRequireBothChangesToRise()
    {
        Assert.Equal(new[] { 0d, 100, 100d / 3 }, Check(Bars((2, 1), (4, 2), (2, 3)), 2));
        Assert.Equal(new[] { 0d, 100, 0, 0, 0, 100 }, Check(Bars((-2, -2), (1, 1), (0, 0), (2, -1), (1, 3), (2, 4)), 1));
        foreach (var period in new[] { 2, 3, 14, 101 }) Check(Bars((-5, 7), (3, 8), (2, 5), (-1, 9), (4, 2), (6, 6), (6, 9), (3, 1), (7, 8)), period);
    }
    [Fact]
    public void SquaredSubnormalsAndOverflowingProductsRetainShares()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 8 })
        {
            var bars = Bars((scale, scale), (2 * scale, 2 * scale), (scale, 3 * scale));
            Assert.Equal(new[] { 0d, 100, 100d / 3 }, Check(bars, 2));
        }
        var m = double.MaxValue; Check(Bars((-m, -m), (m, m), (-m, m / 2), (m / 4, -m), (0, 0), (1, 2)), 3);
        Check(Bars((0, 0), (double.Epsilon, double.Epsilon), (1, -1), (2, 1), (-1, -2)), 2);
    }
    [Fact]
    public void ZeroProductsPreserveShareUntilAnotherProductArrives()
    {
        var values = new List<(double Price, double Volume)> { (2, 1), (4, 2), (2, 3) };
        values.AddRange(Enumerable.Repeat((2d, 3d), 1000)); values.Add((3, 4));
        var result = Check(Bars(values.ToArray()), 14);
        foreach (var value in result.Skip(3).Take(1000)) Assert.Equal(result[2], value);
        Assert.True(result[^1] > result[2]);
        Assert.Equal(new[] { 0d, 100, 0 }, Check(Bars((1, 1), (2, 2), (2, 3)), 1));
        Check(Bars((2, 1), (4, 2), (2, 3), (2, 9), (-1, 9), (3, 10)), 3);
    }
    [Fact]
    public void AdjacentTranslatedChangesDoNotDisappear()
    {
        var x = 1e200; var next = Math.BitIncrement(x); var last = Math.BitIncrement(next);
        Assert.Equal(new[] { 0d, 100, 100d / 3 }, Check(Bars((x, x), (next, next), (x, last)), 2));
        Check(Bars((x, -x), (next, -next), (last, -x), (x, -last)), 7);
    }
    [Fact]
    public void ExtremePeriodsAndEmptyInputNeedNoHistoryAllocation()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Bars((2, 1), (4, 2), (1, 3), (2, 4)), period); Check(Array.Empty<Bar>(), period); }
    }
    [Fact]
    public void PublishedSignalsUseLiquidShareThresholds()
    {
        var bars = Bars((2, 1), (4, 2), (2, 3), (2, 4), (4, 5));
        var data = Data(bars).CalculateLiquidRelativeStrengthIndex(2);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell, Signal.None, Signal.StrongBuy }, data.SignalsList);
    }
    [Fact]
    public void InvalidInputsAndShortSpansCannotPartiallyWriteOrAdvance()
    {
        var output = new[] { -777d, -777d };
        Assert.Throws<ArgumentException>(() => OscillatorCore.LiquidRelativeStrengthIndex(new[] { 1d, 2 }, new[] { 1d }, output)); Assert.Equal(new[] { -777d, -777d }, output);
        Assert.Throws<ArgumentException>(() => OscillatorCore.LiquidRelativeStrengthIndex(new[] { 1d, 2 }, new[] { 1d, 2 }, output.AsSpan(0, 1))); Assert.Equal(new[] { -777d, -777d }, output);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.LiquidRelativeStrengthIndex(new[] { 1d, bad }, new[] { 1d, 2 }, output)); Assert.Equal(new[] { -777d, -777d }, output);
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.LiquidRelativeStrengthIndex(new[] { 1d, 2 }, new[] { 1d, bad }, output)); Assert.Equal(new[] { -777d, -777d }, output);
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                var state = new LiquidRelativeStrengthIndexState(2); var control = new LiquidRelativeStrengthIndexState(2);
                foreach (var b in Bars((2, 1), (4, 2))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
                var values = new[] { 2d, 3, 1, 2, 1 }; values[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
                foreach (var b in Bars((1, 3), (4, 5), (4, 9))) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
            }
        }
    }
}
