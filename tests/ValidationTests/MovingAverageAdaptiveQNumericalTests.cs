using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MovingAverageAdaptiveQNumericalTests
{
    private static Bar[] Bars(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("MAAQ", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MovingAverageAdaptiveQ)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryPublicRouteMatchesIndependentAdaptiveQ(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MovingAverageAdaptiveQOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.MovingAverageAdaptiveQBudget);
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
    private static double[] Check(double[] prices, int length = 3, double fast = .667, double slow = .0645)
    {
        var bars = Bars(prices); var expected = BuiltInFormulaReferences.MovingAverageAdaptiveQValues(prices, length, fast, slow); var values = expected.Outputs["Maaq"];
        var batch = Data(bars).CalculateMovingAverageAdaptiveQ(length, fast, slow);
        using var context = new ComputeContext(); using var computed = IndicatorCompute.ComputeMovingAverageAdaptiveQFast(Data(bars), context, length, fast, slow); var actual = computed.ToArray();
        Assert.Equal(expected.Signals, batch.SignalsList);
        using var native = new MovingAverageAdaptiveQState(length, fast, slow); var direct = new MovingAverageAdaptiveQWindow(length, fast, slow);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 7d, -4, 9 })) { native.Update(Native(b), true, false); direct.Next(b.Close, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                Equal(values[i], batch.CustomValuesList[i]); Equal(values[i], batch.OutputValues["Maaq"][i]); Equal(values[i], actual[i]);
                var decoy = Bars(new[] { -99d })[0]; native.Update(Native(decoy), false, false); direct.Next(-99, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var value = direct.Next(prices[i], final);
                    Equal(values[i], point.Value); Equal(values[i], point.Outputs!["Maaq"]); Equal(values[i], value.Value); Assert.Equal(expected.Signals[i], value.Trade);
                }
            }
        }
        return values;
    }
    [Fact]
    public void HandGainAddsSlowInsteadOfSubtractingItFromFast()
    {
        Assert.Equal(new[] { 0d, 1d / 16, 295d / 256, 8977d / 4096 }, Check(new[] { 0d, 1, 2, 3 }, 2, .5, .25));
        Assert.Equal(new[] { 0d, 4, -12 }, Check(new[] { 0d, 1, 0 }, 1, 1, 1));
        Assert.Equal(new[] { 2d, 2, 2 }, Check(new[] { 2d, -7, 4 }, 2, 0, 0));
        Assert.Equal(new[] { 2d, -7, 4 }, Check(new[] { 2d, -7, 4 }, 2, 0, -1));
    }
    [Fact]
    public void HugeExcursionsCannotEraseSmallFrozenMean()
    {
        foreach (var sign in new[] { 1d, -1d })
            Assert.Equal(new[] { sign, sign, sign, sign, 2 * sign }, Check(new[] { sign, sign * double.MaxValue, 0, sign, 2 * sign }, 3, 1, 0));
    }
    [Fact]
    public void FiniteWideGainCanOverflowAndThenRecoverAtUnitGain()
    {
        Assert.Equal(new[] { 0d, 1, double.PositiveInfinity, double.NegativeInfinity, 2 }, Check(new[] { 0d, 1, 2, 0, 2 }, 2, double.MaxValue, 1));
    }
    [Fact]
    public void HistoryExpiryScalingAndPrefixCausalityAreIndependentOfFutureBars()
    {
        var values = Enumerable.Range(0, 25).Select(i => (double)(i * 7 % 13 - 6)).ToArray();
        foreach (var scale in new[] { 1d, double.Epsilon, Math.Pow(2, 1019) }) foreach (var period in new[] { 2, 3, 7 })
            Check(values.Select(v => v * scale).ToArray(), period);
        Check(values, 3, -.7, .2); Check(values, 3, .1, .8);
        var prefix = Data(Bars(values.Take(12).ToArray())).CalculateMovingAverageAdaptiveQ(3).CustomValuesList;
        var full = Data(Bars(values)).CalculateMovingAverageAdaptiveQ(3).CustomValuesList;
        Assert.Equal(prefix, full.Take(12));
    }
    [Fact]
    public void ExtremePeriodsRetainOnlyObservedHistory()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<double>(), period); Check(new[] { 1d, -2, 4, 0, 7 }, period); }
    }
    [Fact]
    public void NonfiniteParametersAndCandlesCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageAdaptiveQState(2, bad, .1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageAdaptiveQState(2, .1, bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateMovingAverageAdaptiveQ(2, bad, .1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateMovingAverageAdaptiveQ(2, .1, bad));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeMovingAverageAdaptiveQFast(Data(Array.Empty<Bar>()), context, 2, bad, .1));
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeMovingAverageAdaptiveQFast(Data(Array.Empty<Bar>()), context, 2, .1, bad));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new MovingAverageAdaptiveQState(2); using var control = new MovingAverageAdaptiveQState(2);
                foreach (var b in Bars(new[] { 1d, -3, 2 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
                var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, false));
                foreach (var b in Bars(new[] { 9d, -1, 0, 7 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
            }
        }
    }
    [Fact]
    public void NoComponentAverageCallbackIsClaimed()
    {
        using var scope = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Adaptive Q has no component slot."));
        Check(new[] { 1d, -3, 4, 2 }); Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
    }
}
