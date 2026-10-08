using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TrendAnalysisIndexNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TAI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TrendAnalysisIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeQuotients(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TrendAnalysisIndexOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var o = (TrendAnalysisIndexSpecOptions)indicator.CreateOptions();
        var bars = Bars(Enumerable.Range(0, 64).Select(i => 20d + i % 5).ToArray()); var selected = bars.Select((_, i) => i % 7 - 3d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.TrendAnalysisIndexOutputs(projected, indicator);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var pair in expected)
        { using var actual = IndicatorCompute.ComputeTrendAnalysisIndexFast(data, context, o.Length1, o.Length2, o.MaType, pair.Key); Assert.Equal(pair.Value, actual.ToArray()); }
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length1 = 2, int length2 = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.TrendAnalysisIndexValues(bars, length1, length2, kind); var batch = Data(bars).CalculateTrendAnalysisIndex(kind, length1, length2);
        foreach (var pair in expected.Outputs)
        {
            Assert.Equal(pair.Value, batch.OutputValues[pair.Key]); using var context = new ComputeContext();
            using var fast = IndicatorCompute.ComputeTrendAnalysisIndexFast(Data(bars), context, length1, length2, kind, pair.Key); Assert.Equal(pair.Value, fast.ToArray());
        }
        Assert.Equal(expected.Outputs["Tai"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var native = new TrendAnalysisIndexState(kind, length1, length2); using var raw = new TrendAnalysisIndexWindow(kind, length1, length2);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Bars(8, -2, 5, 3, 9)) { native.Update(Native(seed), true, true); raw.Next(seed.Close, true); }
            native.Reset(); raw.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(-17)[0]), false, false); raw.Next(-17, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var direct = raw.Next(bars[i].Close, final);
                    foreach (var pair in expected.Outputs) Assert.Equal(pair.Value[i], point.Outputs![pair.Key]);
                    Assert.Equal(expected.Outputs["Tai"][i], point.Value); Assert.Equal(point.Value, direct.Line);
                    Assert.Equal(expected.Outputs["Signal"][i], direct.SignalLine); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentRangeSmoothingAndSignalHands()
    {
        var result = Check(Bars(2, 4, 2, 8, 1, 7, 3, 5));
        Assert.Equal(new[] { 0d, 75, 0, 25, 50, 50d / 7, 100d / 3, 20 }, result.Outputs["Tai"]);
        Assert.Equal(new[] { 0d, 37.5, 37.5, 12.5, 37.5, 200d / 7, 425d / 21, 80d / 3 }, result.Outputs["Signal"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.None, Signal.StrongBuy, Signal.StrongSell, Signal.None, Signal.StrongSell, Signal.None }, result.Signals);
        foreach (var kind in new[] { MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod, MovingAvgType.DoubleExponentialMovingAverage })
            Check(Bars(2, 4, 2, 8, 1, 7, 3, 5, 0, -2, 0, 3), 3, 2, kind);
    }
    [Fact]
    public void OverflowingRangesAndSubnormalMeansHaveFiniteQuotients()
    {
        var m = double.MaxValue; Assert.Equal(new[] { 0d, 200 }, Check(Bars(-m, m), 1).Outputs["Tai"]);
        Assert.Equal(new[] { 0d, -200 }, Check(Bars(m, -m), 1).Outputs["Tai"]);
        var e = double.Epsilon; var tiny = Check(Bars(e, 2 * e, e)); Assert.Equal(new[] { 0d, 75, 0 }, tiny.Outputs["Tai"]);
        Assert.Equal(new[] { 0d, 37.5, 37.5 }, tiny.Outputs["Signal"]);
        foreach (var kind in new[] { MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
            Check(Bars(-m, m, 0, m / 2, -m / 4, e, 3 * e, 0), 2, 3, kind);
    }
    [Fact]
    public void OpposingInfinitePublishedRatiosHaveFiniteSignalAndRecover()
    {
        var result = Check(Bars(double.MaxValue, double.Epsilon, -double.Epsilon, double.Epsilon, 1, 2, 3), 1, 3);
        Assert.True(double.IsPositiveInfinity(result.Outputs["Tai"][1])); Assert.True(double.IsNegativeInfinity(result.Outputs["Tai"][2]));
        Assert.Equal(-200d / 3, result.Outputs["Signal"][2]); Assert.Equal(0, result.Outputs["Signal"][3]);
        Assert.All(result.Outputs["Signal"].Skip(5), v => Assert.True(double.IsFinite(v)));
    }
    [Fact]
    public void ExactMeanExtremaAndThresholdTiesRemainObservable()
    {
        var result = Check(Bars(1, Math.BitIncrement(1d), 1, 1), 2, 2);
        Assert.Equal(100 * Math.ScaleB(1d, -53), result.Outputs["Tai"][3]);
        var single = Check(Bars(2, 4, 2, 8), 2, 1); Assert.All(single.Outputs["Tai"], v => Assert.Equal(0, v));
        Assert.Contains(Signal.StrongBuy, single.Signals); Assert.Contains(Signal.StrongSell, single.Signals);
    }
    [Fact]
    public void WindowExpiryZeroPriceAndExtremePeriodsRemainLazy()
    {
        var bars = Bars(2, 4, 0, -2, 0, 7, 3, 5, 5, 5, 5, 0, 0, 0, 3);
        foreach (var (first, second) in new[] { (1, 1), (int.MinValue, 2), (0, 0), (3, 4), (int.MaxValue, 2), (2, int.MaxValue) })
            Check(bars, first, second);
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void CallbackSlotsFollowRequestedOutputAndPreserveCallerInput()
    {
        var bars = Bars(9, 9, 9, 9); var selected = new[] { 4d, 4, 4, 4 }; var means = new[] { 2d, 1, 3, 2 }; var line = new[] { 0d, 25, 50, 25 }; var thresholds = new[] { 8d, 7, 6, 5 };
        foreach (var mode in new[] { "batch", "Tai", "Signal", "partial" })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> first = (input, length) => { Assert.Equal(selected, input); Assert.Equal(2, length); return means; };
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> second = (input, length) => { Assert.Equal(line, input); Assert.Equal(2, length); return thresholds; };
            using var armed = ComponentAverage.Arm(mode == "partial" ? new[] { first } : new[] { first, second });
            if (mode == "batch")
            { var result = data.CalculateTrendAnalysisIndex(length1: 2, length2: 2); Assert.Equal(line, result.CustomValuesList); Assert.Equal(thresholds, result.OutputValues["Signal"]); }
            else
            {
                using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeTrendAnalysisIndexFast(data, context, 2, 2, outputKey: mode == "partial" ? "Signal" : mode);
                Assert.Equal(mode == "Tai" ? line : mode == "partial" ? new[] { 0d, 12.5, 37.5, 37.5 } : thresholds, actual.ToArray()); Assert.Equal(selected, data.ChainedValues);
            }
            Assert.Equal(mode == "Tai" ? 1 : 2, ComponentAverage.Requests); Assert.Equal(mode is "Tai" or "partial" ? 1 : 2, ComponentAverage.Substitutions);
        }
        Assert.Contains(typeof(TrendAnalysisIndexSpecOptions), BuilderVerifiedArms.Arms);
        using var state = new TrendAnalysisIndexState(); Assert.Null(state.Update(Native(bars[0]), true, false).Outputs);
    }
    [Fact]
    public void NonfiniteCandlesCannotAdvanceMeansExtremaOrSignal()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var actual = new TrendAnalysisIndexState(length1: 2, length2: 2); using var expected = new TrendAnalysisIndexState(length1: 2, length2: 2);
            actual.Update(Native(Bars(2)[0]), true, true); expected.Update(Native(Bars(2)[0]), true, true);
            var values = new[] { 4d, 4, 4, 4, 1 }; values[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Bars(4, 2, 8, 1, 7))
            { var next = expected.Update(Native(bar), true, true); var got = actual.Update(Native(bar), true, true); Assert.Equal(next.Value, got.Value); Assert.Equal(next.Outputs!["Signal"], got.Outputs!["Signal"]); }
        }
    }
}
