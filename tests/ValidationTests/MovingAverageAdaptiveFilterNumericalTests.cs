using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MovingAverageAdaptiveFilterNumericalTests
{
    private static Bar[] Candles(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("MAAF", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MovingAverageAdaptiveFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryExistingRouteMatchesIndependentAdaptiveChanges(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MovingAverageAdaptiveFilterOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.MovingAverageAdaptiveFilterBudget);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Equal(double expected, double actual)
    {
        Assert.False(double.IsNaN(actual));
        if (expected == 0 || double.IsInfinity(expected)) Assert.Equal(expected, actual);
        else { Assert.Equal(Math.Sign(expected), Math.Sign(actual)); Assert.True(Math.Abs((actual - expected) / expected) <= 4e-15, $"Expected {expected:R}, actual {actual:R}"); }
    }
    private static (double[] Values, Signal[] Signals) Check(double[] prices, int length = 3, double filter = .15, double fast = .667, double slow = .0645)
    {
        var bars = Candles(prices); var expected = BuiltInFormulaReferences.MovingAverageAdaptiveFilterValues(prices, length, filter, fast, slow);
        var batch = Data(bars).CalculateMovingAverageAdaptiveFilter(length, filter, fast, slow);
        using var context = new ComputeContext(); using var computed = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.MovingAverageAdaptiveFilter, new MovingAverageAdaptiveFilterSpecOptions(length, filter, fast, slow)), context);
        Assert.NotNull(computed); var fallback = computed.Value.ToArray(); var values = expected.Outputs["Maaf"];
        Assert.Equal(expected.Signals, batch.SignalsList);
        using var native = new MovingAverageAdaptiveFilterState(length, filter, fast, slow); var direct = new MovingAverageAdaptiveFilterWindow(length, filter, fast, slow);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Candles(new[] { 7d, -4, 9 })) { native.Update(Native(seed), true, false); direct.Next(seed.Close, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                Equal(values[i], batch.CustomValuesList[i]); Equal(values[i], batch.OutputValues["Maaf"][i]); Equal(values[i], fallback[i]);
                var decoy = Candles(new[] { -99d })[0]; native.Update(Native(decoy), false, false); direct.Next(-99, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var value = direct.Next(prices[i], final);
                    Equal(values[i], point.Value); Equal(values[i], point.Outputs!["Maaf"]); Equal(values[i], value.Value); Assert.Equal(expected.Signals[i], value.Trade);
                }
            }
        }
        return (values, expected.Signals);
    }
    [Fact]
    public void HandGainBoundariesAndEfficiencyWarmup()
    {
        var prices = new[] { 0d, 2, 4, 6 };
        Assert.All(Check(prices, 2, 2, 0, 0).Values, v => Assert.Equal(0, v));
        Assert.Equal(new[] { 0d, 2, 0, 0 }, Check(prices, 2, 2, 1, 1).Values);
        var warmup = Check(new[] { 0d, 1, 2, 3, 4 }, 2, 1, 1, 0);
        Assert.Equal(new[] { 0d, 0, 1, .5, 0 }, warmup.Values);
        Assert.Equal(Signal.Buy, warmup.Signals[2]); Assert.Equal(Signal.None, warmup.Signals[3]);
    }
    [Fact]
    public void RootScalingPreservesSquaredSubnormalsAndWideDifferences()
    {
        var e = double.Epsilon; var m = double.MaxValue;
        var tiny = Check(new[] { 0d, e, 2 * e, 0, -e, 3 * e }, 2, m, 1, 1);
        Assert.True(tiny.Values[1] > 0); Assert.All(tiny.Values, v => Assert.True(double.IsFinite(v)));
        var wide = Check(new[] { -m, m, -m, m, 0, m }, 2, e, 1, 1);
        Assert.True(wide.Values[1] > 0); Assert.All(wide.Values, v => Assert.True(double.IsFinite(v)));
        Assert.All(Check(new[] { -m, m, -m, m, m, m }, 2, 0, 1, 1).Values, v => Assert.Equal(0, v));
        Check(new[] { -m, m, -m, m, 0, 1, 2, 1, 2, 3, 4 }, 3, m);
    }
    [Fact]
    public void AdaptiveHistoryExpiryAndScaleRetainPopulationDeviation()
    {
        var prices = Enumerable.Range(0, 28).Select(i => (double)(i * 7 % 13 - 6)).ToArray();
        foreach (var scale in new[] { 1d, double.Epsilon, Math.Pow(2, 1019) })
        foreach (var length in new[] { 2, 3, 7 })
        foreach (var alpha in new[] { (Fast: .667, Slow: .0645), (Fast: .1, Slow: .8), (Fast: 1d, Slow: 0d) })
            Check(prices.Select(v => v * scale).ToArray(), length, .15, alpha.Fast, alpha.Slow);
        foreach (var price in new[] { 0d, double.MaxValue, -double.MaxValue, double.Epsilon }) Assert.All(Check(Enumerable.Repeat(price, 32).ToArray()).Values, v => Assert.Equal(0, v));
    }
    [Fact]
    public void ConstantPriceTailRetainsDecayingAdaptiveIncrements()
    {
        var prices = new[] { 0d }.Concat(Enumerable.Repeat(1d, 359)).ToArray();
        var line = Check(prices, 2, 1, .5, .5).Values;
        Assert.Equal(0, line[0]); Assert.Equal(.125, line[1]);
        // Gain 1/4 gives increments (1/4)*(3/4)^(i-1). For two
        // observations, population deviation is half their absolute difference.
        for (var i = 2; i < line.Length; i++)
            Equal(.03125 * Math.Pow(.75, i - 2), line[i]);
    }
    [Fact]
    public void ZeroGainWarmupKeepsSmallMeanAcrossHugeExcursions()
    {
        foreach (var sign in new[] { 1d, -1d })
        {
            var line = Check(new[] { sign, sign * double.MaxValue, 0, sign, 2 * sign }, 3, 1, 1, 0).Values;
            Assert.All(line.Take(4), value => Assert.Equal(0, value));
            // At index 4, ER=(M-2)/(M+2), mean is still +/-1, and
            // increments are [0,0,+/-ER^2]. The deviation rounds to sqrt(2)/3.
            Equal(Math.Sqrt(2) / 3, line[4]);
        }
    }
    [Fact]
    public void ExtremePeriodsGrowOnlyObservedHistory()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(new[] { 1d, 2, -3, 4, 0 }, length); Check(Array.Empty<double>(), length); }
    }
    [Fact]
    public void InvalidCandlesAndParameterDomainsCannotAdvanceState()
    {
        using var actual = new MovingAverageAdaptiveFilterState(2); using var control = new MovingAverageAdaptiveFilterState(2);
        var seed = Candles(new[] { 1d })[0]; actual.Update(Native(seed), true, false); control.Update(Native(seed), true, false);
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var bad = new Bar(seed.Time, value, 3, -1, 2, 1); Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(bad), final, false));
            var badClose = Candles(new[] { value })[0]; Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(badClose), final, false));
        }
        foreach (var next in Candles(new[] { 3d, -2, 4, 0 })) Assert.Equal(control.Update(Native(next), true, true).Value, actual.Update(Native(next), true, true).Value);
        foreach (var args in new[] { (-1d, .5, .1), (double.NaN, .5, .1), (double.PositiveInfinity, .5, .1), (.1, -1d, .1), (.1, 2d, .1), (.1, .5, double.NaN), (.1, .5, -1d), (.1, .5, 2d) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageAdaptiveFilterState(2, args.Item1, args.Item2, args.Item3));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(new[] { seed }).CalculateMovingAverageAdaptiveFilter(2, args.Item1, args.Item2, args.Item3));
        }
    }
    [Fact]
    public void BoundFallbackDoesNotClaimAnUnusedAverageOverride()
    {
        using var scope = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("No public component slot is consumed."));
        using var context = new ComputeContext(); using var result = IndicatorCompute.TryComputeFast(Data(Candles(new[] { 1d, 2, 4, 1 })), new IndicatorSpec(IndicatorName.MovingAverageAdaptiveFilter, new MovingAverageAdaptiveFilterSpecOptions(2)), context);
        Assert.NotNull(result); Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
    }
}
