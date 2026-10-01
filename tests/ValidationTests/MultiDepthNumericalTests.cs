using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MultiDepthNumericalTests
{
    private static Bar[] Bars(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("MD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MultiDepthZeroLagExponentialMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryPublicRouteMatchesIndependentPoles(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MultiDepthOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.MultiDepthBudget);
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
    private static Dictionary<string, double[]> Check(double[] prices, int length = 3)
    {
        var bars = Bars(prices); var expected = BuiltInFormulaReferences.MultiDepthValues(prices, length);
        var batch = Data(bars).CalculateMultiDepthZeroLagExponentialMovingAverage(length);
        using var context = new ComputeContext(); var computed = new Dictionary<string, double[]>();
        foreach (var pole in new[] { IndicatorCompute.MultiDepthPole.OnePole, IndicatorCompute.MultiDepthPole.TwoPole, IndicatorCompute.MultiDepthPole.ThreePole })
        {
            using var buffer = IndicatorCompute.ComputeMultiDepthZeroLagExponentialMovingAverageFast(Data(bars), context, length, pole);
            computed["Md" + ((int)pole + 1) + "Pole"] = buffer.ToArray();
        }
        Assert.Equal(expected.Signals, batch.SignalsList);
        var native = new MultiDepthZeroLagExponentialMovingAverageState(length); var direct = new MultiDepthWindow(length);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 7d, -4, 9 })) { native.Update(Native(b), true, false); direct.Next(b.Close, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var decoy = Bars(new[] { -99d })[0]; native.Update(Native(decoy), false, false); direct.Next(-99, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var value = direct.Next(prices[i], final);
                    Equal(expected.Outputs["Md2Pole"][i], point.Value); Equal(expected.Outputs["Md2Pole"][i], batch.CustomValuesList[i]);
                    Assert.Equal(expected.Signals[i], value.Trade);
                    foreach (var key in expected.Outputs.Keys)
                    {
                        Equal(expected.Outputs[key][i], batch.OutputValues[key][i]); Equal(expected.Outputs[key][i], computed[key][i]); Equal(expected.Outputs[key][i], point.Outputs![key]);
                        Equal(expected.Outputs[key][i], key == "Md1Pole" ? value.One : key == "Md2Pole" ? value.Two : value.Three);
                    }
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void OnePoleHasIndependentDyadicHandValues()
    {
        Assert.Equal(new[] { 0d, .75, 1.75, 2.8125 }, Check(new[] { 0d, 1, 2, 3 }, 3)["Md1Pole"]);
        var prices = new[] { double.MaxValue, -double.MaxValue, 0, double.Epsilon, -double.Epsilon };
        Assert.Equal(prices, Check(prices, 1)["Md1Pole"]);
    }
    [Fact]
    public void ConstantPricesRetainExactUnityAcrossAllPoles()
    {
        foreach (var price in new[] { 0d, 1, -1, double.MaxValue, -double.MaxValue, double.Epsilon })
        foreach (var period in new[] { 1, 50, int.MaxValue })
        {
            var prices = Enumerable.Repeat(price, 8).ToArray(); var outputs = Check(prices, period);
            foreach (var line in outputs.Values) Assert.Equal(prices, line);
        }
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndPreserveFeedGain()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<double>(), period); Check(new[] { 0d, 1, 0, -2, 7, 3 }, period); }
        // After zero history has filled, the impulse response is g*(1+(1-g)/depth).
        // Direct subtraction rounds the two-pole feed gain to zero here.
        var impulse = Check(new[] { 0d, 0, 0, 1 }, int.MaxValue);
        Assert.InRange(impulse["Md2Pole"][3], 1.28407758252731e-17, 1.28407758252734e-17);
        Assert.InRange(impulse["Md3Pole"][3], 3.33955610653479e-26, 3.33955610653484e-26);
    }
    [Fact]
    public void WideAlternatingPricesRecoverWithoutNaN()
    {
        Check(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0d, 1, -1, 0 }, 2);
        var pattern = Enumerable.Range(0, 24).Select(i => (double)(i * 7 % 13 - 6)).ToArray();
        foreach (var scale in new[] { double.Epsilon, 1d, Math.Pow(2, 1019) }) Check(pattern.Select(v => v * scale).ToArray(), 7);
    }
    [Fact]
    public void IsolatedSpikeCancelsExactlyAtTheClosedFormZero()
    {
        // With gain g=2/(length+1), the one-pole correction's impulse
        // response is g*(1-g)^(n-1)*(2-n*g). Its zero is n=length+1.
        foreach (var period in new[] { 12, 37 }) foreach (var baseline in new[] { 0d, 1d })
        {
            var prices = Enumerable.Repeat(baseline, period + 2).ToArray(); prices[1] = double.MaxValue;
            Assert.Equal(baseline, Check(prices, period)["Md1Pole"][period + 1]);
        }
    }
    [Fact]
    public void FutureBarsCannotChangeEarlierOutputs()
    {
        var prices = Enumerable.Range(0, 20).Select(i => (double)(i * 11 % 17)).ToArray();
        var prefix = Data(Bars(prices.Take(9).ToArray())).CalculateMultiDepthZeroLagExponentialMovingAverage(5);
        var full = Data(Bars(prices)).CalculateMultiDepthZeroLagExponentialMovingAverage(5);
        foreach (var key in prefix.OutputValues.Keys) Assert.Equal(prefix.OutputValues[key], full.OutputValues[key].Take(9));
    }
    [Fact]
    public void NonfiniteCandlesCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            var state = new MultiDepthZeroLagExponentialMovingAverageState(2); var control = new MultiDepthZeroLagExponentialMovingAverageState(2);
            foreach (var b in Bars(new[] { 1d, -3, 2 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, false));
            foreach (var b in Bars(new[] { 9d, -1, 0, 7 })) Assert.Equal(control.Update(Native(b), true, true).Outputs, state.Update(Native(b), true, true).Outputs);
        }
    }
    [Fact]
    public void NoComponentAverageCallbackIsClaimed()
    {
        using var scope = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Multi-Depth has no component slot."));
        Check(new[] { 1d, -3, 4, 2 }); Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
    }
}
