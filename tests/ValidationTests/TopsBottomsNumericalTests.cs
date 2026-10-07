using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TopsBottomsNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TOPS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TopsAndBottomsFinder)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentEndpoints(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TopsBottomsOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var o = (TopsAndBottomsFinderSpecOptions)indicator.CreateOptions();
        var bars = Bars(Enumerable.Range(0, 64).Select(i => 20d + i % 5).ToArray()); var selected = bars.Select((_, i) => i % 7 - 3d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.TopsBottomsOutputs(projected, indicator)["Tabf"];
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeTopsAndBottomsFinderFast(data, context, o.Length, o.MaType);
        Assert.Equal(expected, actual.ToArray()); Assert.Equal(selected, data.ChainedValues);
        Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 2, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.TopsBottomsValues(bars, length, kind); var batch = Data(bars).CalculateTopsAndBottomsFinder(kind, length);
        Assert.Equal(expected, batch.OutputValues["Tabf"]); Assert.Equal(expected, batch.CustomValuesList);
        Assert.Equal(expected.Select(v => v > 0 ? Signal.Buy : v < 0 ? Signal.Sell : Signal.None), batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeTopsAndBottomsFinderFast(Data(bars), context, length, kind); Assert.Equal(expected, fast.ToArray());
        using var native = new TopsAndBottomsFinderState(kind, length); using var raw = new TopsBottomsWindow(kind, length);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Bars(8, -2, 5, 3, 9)) { native.Update(Native(seed), true, true); raw.Next(seed.Close, true); }
            native.Reset(); raw.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(-17)[0]), false, false); raw.Next(-17, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["Tabf"]); Assert.Equal(expected[i], raw.Next(bars[i].Close, final));
                }
            }
        }
        return expected;
    }
    [Fact]
    public void ExactEndpointHandsAndExpiry()
    {
        Assert.Equal(new[] { 0d, 1, -1, 0, 0, 0, 0, 0 }, Check(Bars(2, 4, 2, 8, 1, 7, 3, 5)));
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.WildersSmoothingMethod, MovingAvgType.DoubleExponentialMovingAverage })
            Check(Bars(2, 4, 2, 8, 1, 7, 3, 5, 5, 5, 5, 0, 0, 0, -2, -2, -2, 9), 3, kind);
    }
    [Fact]
    public void SubnormalAndOverflowingVariancePreserveEndpointDecisions()
    {
        var prices = new[] { 2d, 4, 2, 8, 1, 7, 3, 5, 0, -2, -2, 4 };
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var expected = Check(Bars(prices), 2, kind);
            Assert.Equal(expected, Check(Bars(prices.Select(v => v * double.Epsilon).ToArray()), 2, kind));
            Assert.Equal(expected, Check(Bars(prices.Select(v => v * Math.ScaleB(1d, 1020)).ToArray()), 2, kind));
        }
    }
    [Fact]
    public void RoundedMovingAverageTiesCannotCreateFalseEndpoints()
    {
        var prices = new[] { 1d }.Concat(Enumerable.Repeat(2d, 120)).Concat(new[] { 3d }).ToArray();
        var expected = Check(Bars(prices)); Assert.Equal(1, expected[1]); Assert.All(expected.Skip(2), v => Assert.Equal(0, v));
    }
    [Fact]
    public void ZeroMeansConstantWindowsAndLazyExtremePeriods()
    {
        Assert.Equal(new[] { 0d, 0, 1, 0, 1 }, Check(Bars(1, 1, 0, -1, 0), 1));
        foreach (var period in new[] { int.MinValue, 0, 1, 3, int.MaxValue })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage })
                Check(Bars(0, 1, 1, 1, -1, -1, 0, 4), period, kind);
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void OneAverageCallbackIsUsedAndCallerSeriesIsPreserved()
    {
        var bars = Bars(Enumerable.Repeat(9d, 8).ToArray()); var forced = new[] { 1d, 1, 2, 2, 0, 0, 3, 3 }; var expected = new[] { 0d, 1, 0, 0, -1, 0, 0, 0 };
        foreach (var fast in new[] { false, true })
        {
            var data = Data(bars); var selected = Enumerable.Repeat(7d, bars.Length).ToArray(); data.SetCustomValues(selected.ToList());
            using var armed = ComponentAverage.Arm((input, length) => { Assert.Equal(selected, input); Assert.Equal(2, length); return forced; });
            if (fast)
            { using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeTopsAndBottomsFinderFast(data, context, 2); Assert.Equal(expected, actual.ToArray()); Assert.Equal(selected, data.ChainedValues); }
            else Assert.Equal(expected, data.CalculateTopsAndBottomsFinder(length: 2).CustomValuesList);
            Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions);
        }
        Assert.Contains(typeof(TopsAndBottomsFinderSpecOptions), BuilderVerifiedArms.Arms);
        using var state = new TopsAndBottomsFinderState(); Assert.Null(state.Update(Native(bars[0]), true, false).Outputs);
    }
    [Fact]
    public void NonfiniteCandlesCannotAdvanceMeansOrEndpointHistory()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var actual = new TopsAndBottomsFinderState(length: 2); using var expected = new TopsAndBottomsFinderState(length: 2);
            actual.Update(Native(Bars(2)[0]), true, true); expected.Update(Native(Bars(2)[0]), true, true);
            var values = new[] { 4d, 4, 4, 4, 1 }; values[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Bars(4, 2, 8, 1, 7)) Assert.Equal(expected.Update(Native(bar), true, true).Value, actual.Update(Native(bar), true, true).Value);
        }
    }
}
