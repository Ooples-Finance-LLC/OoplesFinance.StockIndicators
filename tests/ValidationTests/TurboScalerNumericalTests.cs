using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TurboScalerNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TAI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TurboScaler)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentBlendedRanges(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TurboScalerOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var o = (TurboScalerSpecOptions)indicator.CreateOptions();
        var bars = Bars(Enumerable.Range(0, 64).Select(i => 20d + i % 5).ToArray()); var selected = bars.Select((_, i) => i % 7 - 3d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.TurboScalerOutputs(projected, indicator);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var pair in expected)
        { using var actual = IndicatorCompute.ComputeTurboScalerFast(data, context, o.Length, o.MaType, outputKey: pair.Key); Assert.Equal(pair.Value, actual.ToArray()); }
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateTurboScaler(o.MaType, o.Length);
        Assert.Equal(expected["Ts"], batch.CustomValuesList); Assert.Equal(expected["Trigger"], batch.OutputValues["Trigger"]); Assert.Equal(bars.Select(b => b.High), batch.HighPrices); Assert.Equal(bars.Select(b => b.Low), batch.LowPrices); Assert.Equal(bars.Select(b => b.Close), batch.ClosePrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 2, double alpha = .5, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.TurboScalerValues(bars, length, kind, alpha); var data = Data(bars).CalculateTurboScaler(kind, length, alpha);
        foreach (var pair in expected.Outputs)
        {
            Assert.Equal(pair.Value, data.OutputValues[pair.Key]); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeTurboScalerFast(Data(bars), context, length, kind, alpha, pair.Key); Assert.Equal(pair.Value, fast.ToArray());
        }
        Assert.Equal(expected.Outputs["Ts"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var native = new TurboScalerState(kind, length, alpha); using var raw = new TurboScalerWindow(kind, length, alpha);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Bars(3, -2, 7, 1, 4)) { native.Update(Native(seed), true, true); raw.Next(seed.Close, true); }
            native.Reset(); raw.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(-17)[0]), false, false); raw.Next(-17, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var next = native.Update(Native(b), final, true); var direct = raw.Next(b.Close, final);
                    foreach (var pair in expected.Outputs) Assert.Equal(pair.Value[i], next.Outputs![pair.Key]);
                    Assert.Equal(expected.Outputs["Ts"][i], next.Value); Assert.Equal(next.Value, direct.Line); Assert.Equal(expected.Outputs["Trigger"][i], direct.Trigger); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentRawRangesAndSmoothedSignalHands()
    {
        var result = Check(Bars(2, 4, 2, 8, 1, 7));
        Assert.Equal(new[] { 0d, 6d / 5, -.5, 11d / 8, -7d / 15, 17d / 11 }, result.Outputs["Ts"]);
        Assert.Equal(new[] { 0d, 4d / 3, 1, 4d / 3, 0, -.25 }, result.Outputs["Trigger"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongSell, Signal.StrongSell, Signal.Sell, Signal.Sell, Signal.StrongBuy }, result.Signals);
        foreach (var kind in new[] { MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod, MovingAvgType.DoubleExponentialMovingAverage })
            Check(Bars(1, 2, 4, 2, 8, -1, 3, 0, 7, 7, 7, 7, 7, 2, -3, 4), 3, .5, kind);
    }
    [Fact]
    public void OverflowingBlendsAndRangesRetainFiniteRatios()
    {
        var m = double.MaxValue;
        foreach (var alpha in new[] { -.5, .5, 1.5, double.MaxValue, -double.MaxValue })
            Check(Bars(m, -m, m / 2, -m / 4, 0, double.Epsilon, -double.Epsilon, m, -m, 0), 3, alpha);
        foreach (var kind in new[] { MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
            Check(Bars(m, -m, m / 2, -m / 4, 0, m, -m), 3, .5, kind);
    }
    [Fact]
    public void SubnormalBlendsAndOpposingInfiniteRatiosRetainSignal()
    {
        var e = double.Epsilon; var tiny = Check(Bars(2 * e, 4 * e, 2 * e, 8 * e, e, 7 * e));
        var ordinary = Check(Bars(2, 4, 2, 8, 1, 7)); foreach (var pair in ordinary.Outputs) Assert.Equal(pair.Value, tiny.Outputs[pair.Key]); Assert.Equal(ordinary.Signals, tiny.Signals);
        var result = Check(Bars(1, -1, 1, 0, 2, 3, 4, 5), 4, e);
        Assert.True(double.IsNegativeInfinity(result.Outputs["Ts"][1])); Assert.True(double.IsPositiveInfinity(result.Outputs["Ts"][2]));
        Assert.Equal(Signal.StrongSell, result.Signals[3]); Assert.All(result.Outputs["Ts"].Skip(3), value => Assert.True(double.IsFinite(value)));
    }
    [Fact]
    public void ExtremePeriodsAlphaEndpointsAndExpiryRemainDefined()
    {
        var bars = Bars(1, 2, 4, 2, 8, -1, 3, 3, 3, 3, 3, 3, 2, 0);
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 7, int.MaxValue }) Check(bars, length);
        foreach (var alpha in new[] { -1d, 0, double.Epsilon, .5, Math.BitDecrement(1d), 1d, Math.BitIncrement(1d), 2d }) Check(bars, 3, alpha);
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void CallbackSlotsDiscoveryShortArraysAndCallerInputArePreserved()
    {
        var prices = new[] { 2d, 4, 2, 8 }; var firstValues = new[] { 0d, 3, 3, 5 }; var secondValues = new[] { 0d, 1.5, 3, 4 };
        var line = new[] { 0d, 6d / 5, -.5, 11d / 8 }; var trigger = new[] { 0d, 4d / 3, 1, 4d / 3 };
        foreach (var mode in new[] { "batch", "Ts", "Trigger" })
        {
            var data = Data(Bars(9, 9, 9, 9)); data.SetCustomValues(prices.ToList());
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> first = (values, length) => { Assert.Equal(prices, values); Assert.Equal(2, length); return firstValues; };
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> second = (values, length) => { Assert.Equal(firstValues, values); Assert.Equal(2, length); return secondValues; };
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> third = (values, length) => { Assert.Equal(line, values); Assert.Equal(2, length); return new[] { 1d, 2, 3, 4 }; };
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> fourth = (values, length) => { Assert.Equal(trigger, values); Assert.Equal(2, length); return new[] { 0d, 2, 4, 2 }; };
            using var scope = ComponentAverage.Arm(new[] { first, second, third, fourth });
            if (mode == "batch")
            { var actual = data.CalculateTurboScaler(length: 2); Assert.Equal(line, actual.CustomValuesList); Assert.Equal(trigger, actual.OutputValues["Trigger"]); Assert.Equal(new[] { Signal.StrongBuy, Signal.None, Signal.StrongSell, Signal.StrongBuy }, actual.SignalsList); }
            else { using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeTurboScalerFast(data, context, 2, outputKey: mode); Assert.Equal(mode == "Ts" ? line : trigger, actual.ToArray()); Assert.Equal(prices, data.ChainedValues); }
            Assert.Equal(mode == "batch" ? 4 : 2, ComponentAverage.Requests); Assert.Equal(ComponentAverage.Requests, ComponentAverage.Substitutions); Assert.Equal(new[] { 9d, 9, 9, 9 }, data.ClosePrices);
        }
        using (ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>()))
        { Data(Bars(prices)).CalculateTurboScaler(length: 2); Assert.Equal(4, ComponentAverage.Requests); }
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (_, _) => new[] { 0d } }))
        { var result = Data(Bars(prices)).CalculateTurboScaler(length: 2); Assert.Equal(new[] { 0d, 3, 1, 7d / 3 }, result.CustomValuesList); Assert.All(result.OutputValues["Trigger"], v => Assert.Equal(0, v)); Assert.Equal(4, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions); }
    }
    [Fact]
    public void InvalidCandlesAndAlphaCannotAdvanceMeansOrExtrema()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TurboScalerWindow(MovingAvgType.SimpleMovingAverage, 2, bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Bars(1)).CalculateTurboScaler(length: 2, alpha: bad));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeTurboScalerFast(Data(Bars(1)), context, 2, alpha: bad));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var actual = new TurboScalerState(length: 3); using var expected = new TurboScalerState(length: 3);
                actual.Update(Native(Bars(1)[0]), true, true); expected.Update(Native(Bars(1)[0]), true, true);
                var v = new[] { 2d, 2, 2, 2, 1 }; v[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
                foreach (var b in Bars(2, 4, -3, 4)) { var a = actual.Update(Native(b), true, true); var e = expected.Update(Native(b), true, true); Assert.Equal(e.Value, a.Value); Assert.Equal(e.Outputs!["Trigger"], a.Outputs!["Trigger"]); }
            }
        }
    }
}
