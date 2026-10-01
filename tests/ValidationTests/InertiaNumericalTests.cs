using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class InertiaNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("INERTIA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Prices(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.LinearRegression, 0), (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(Inertia) || c.IndicatorType == typeof(InertiaIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentHighLowRviRegression(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.InertiaOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 3, int rviLength = 1, MovingAvgType kind = MovingAvgType.LinearRegression, int reference = 0)
    {
        var expected = BuiltInFormulaReferences.InertiaValues(bars, length, rviLength, reference); var line = expected.Outputs["Inertia"];
        var batch = Data(bars).CalculateInertiaIndicator(kind, length, rviLength);
        Assert.Equal(line, batch.ChainedValues); Assert.Equal(line, batch.OutputValues["Inertia"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeInertiaFast(Data(bars), context, length, rviLength, kind); Assert.Equal(line, fast.ToArray());
        using var state = new InertiaIndicatorState(kind, length, rviLength);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(new Bar(DateTime.UnixEpoch, 1, 2, -1, 1, 1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(new Bar(DateTime.UnixEpoch, 99, 101, -99, 99, 1)), false, false);
                foreach (var final in new[] { false, false, true })
                { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(line[i], point.Value); Assert.Equal(line[i], point.Outputs!["Inertia"]); }
            }
        }
        return line;
    }
    [Fact]
    public void FallingHighWithFlatCloseCannotBecomeNeutralRvi()
    {
        var bars = Enumerable.Range(0, 14).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 20 - i, -10, 0, 1)).ToArray();
        var line = Check(bars, 1);
        // The first full ten-bar deviation assigns high-series volatility to
        // losses: high RVI is 0, flat-low RVI is 100, and their mean is 50.
        Assert.All(line.Take(9), v => Assert.Equal(100, v));
        Assert.All(line.Skip(9), v => Assert.Equal(50, v));
    }

    [Fact]
    public void HandHighLowSplitAndPartialRegressionDetermineEndpoints()
    {
        var bars = Enumerable.Range(0, 14).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i, -10 - i, 0, 1)).ToArray();
        var line = Check(bars); Assert.All(line.Take(9), v => Assert.Equal(100, v));
        Assert.Equal(175d / 3, line[9]); Assert.Equal(125d / 3, line[10]); Assert.All(line.Skip(11), v => Assert.Equal(50, v));
        var unsmoothed = Check(bars, 1); Assert.All(unsmoothed.Skip(9), v => Assert.Equal(50, v));
        Assert.All(Check(Prices(Enumerable.Repeat(4d, 18))), v => Assert.Equal(100, v));
    }
    [Fact]
    public void WideSubnormalAndNarrowPedestalPricesPreserveDirectionalVolatility()
    {
        var sequences = new[] {
            Enumerable.Range(0, 35).Select(i => i % 4 == 0 ? double.MaxValue : i % 4 == 1 ? -double.MaxValue : (double)(i % 7 - 3)),
            Enumerable.Range(0, 35).Select(i => (i % 7 - 3) * double.Epsilon),
            Enumerable.Range(0, 35).Select(i => i % 3 == 0 ? Math.BitIncrement(double.MaxValue / 2) : double.MaxValue / 2)
        };
        foreach (var pair in Kinds) foreach (var prices in sequences) Check(Prices(prices), 5, 3, pair.Kind, pair.Reference);
        var bars = Enumerable.Range(0, 35).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, i % 3 == 0 ? double.MaxValue : 1, i % 5 == 0 ? -double.MaxValue : -1, 0, 1)).ToArray();
        Check(bars, 7, 4);
    }
    [Fact]
    public void ExtremePeriodsRetainOnlyObservedHistory()
    {
        var bars = Prices(Enumerable.Range(0, 22).Select(i => (double)(i * 7 % 13 - 6)));
        foreach (var pair in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<Bar>(), length, length, pair.Kind, pair.Reference); Check(bars, length, 3, pair.Kind, pair.Reference); Check(bars, 3, length, pair.Kind, pair.Reference); }
    }
    [Fact]
    public void RviAndOutputCallbackSlotsKeepTheirOrderAndExhaustionDefaults()
    {
        var bars = Prices(Enumerable.Range(0, 16).Select(i => (double)(i % 5))); var supplied = Enumerable.Range(0, 4).Select(k => Enumerable.Range(0, bars.Length).Select(i => (double)(k + i % 3 + 1)).ToArray()).ToArray();
        foreach (var batch in new[] { false, true }) foreach (var count in new[] { 4, 5 })
        {
            var expected = BuiltInFormulaReferences.InertiaValues(bars, 3, 4, 0, supplied); var callbacks = new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>();
            for (var k = 0; k < 4; k++) { var slot = k; callbacks.Add((input, period) => { Assert.Equal(4, period); Assert.Equal(bars.Length, input.Count); return supplied[slot]; }); }
            var output = Enumerable.Range(0, bars.Length).Select(i => (double)(i - 7)).ToArray();
            if (count == 5) callbacks.Add((input, period) => { Assert.Equal(3, period); Assert.Equal(expected.Rvi, input); return output; });
            using var armed = ComponentAverage.Arm(callbacks);
            if (batch) Assert.Equal(expected.Outputs["Inertia"], Data(bars).CalculateInertiaIndicator(length: 3, rviLength: 4).ChainedValues);
            else { using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeInertiaFast(Data(bars), context, 3, 4); Assert.Equal(count == 5 ? output : expected.Outputs["Inertia"], fast.ToArray()); }
            Assert.Equal(batch ? 4 : 5, ComponentAverage.Requests); Assert.Equal(batch ? 4 : count, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void RejectedCandlesCannotAdvanceRviOrRegression()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new InertiaIndicatorState(length: 3, rviLength: 2); using var control = new InertiaIndicatorState(length: 3, rviLength: 2);
            foreach (var b in Prices(Enumerable.Range(0, 12).Select(i => (double)i))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, -1, 1, 1 }; values[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Prices(new[] { 9d, -2, 3, 0, 7 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
    [Fact]
    public void RegressionAndMovingAverageExpiryRetainChronologicalWeights()
    {
        var bars = Prices(Enumerable.Range(0, 91).Select(i => (double)(i * 17 % 23 - 11)));
        foreach (var pair in Kinds) foreach (var length in new[] { 2, 7, 20 }) Check(bars, length, 5, pair.Kind, pair.Reference);
    }
    [Fact]
    public void SignalComparisonRetainsExactDifferences()
    {
        Assert.Equal(Signal.StrongBuy, InertiaSmoother.Trade(double.MaxValue, -double.MaxValue, double.MaxValue));
        Assert.Equal(Signal.StrongSell, InertiaSmoother.Trade(-double.MaxValue, double.MaxValue, -double.MaxValue));
        Assert.Equal(Signal.Buy, InertiaSmoother.Trade(3, 2, 1)); Assert.Equal(Signal.None, InertiaSmoother.Trade(2, 2, 1));
    }
}
