using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KalmanSmootherNumericalTests
{
    private static Bar B(double value, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("HF", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KalmanSmoother)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRationalReference(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KalmanSmootherOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourceRetainsTheFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static StockData Check(double[] prices, int length)
    {
        var bars = prices.Select((v, i) => B(v, i)).ToArray(); var expected = BuiltInFormulaReferences.KalmanSmootherValues(bars, length)["Ks"];
        var data = Data(bars).CalculateKalmanSmoother(length); Assert.Equal(expected, data.OutputValues["Ks"]);
        var state = new KalmanSmootherState(length);
        for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeKalmanSmootherFast(Data(bars), context, length); Assert.Equal(expected, fast.ToArray());
        return data;
    }
    [Fact]
    public void RationalRootAndVelocityHands()
    {
        // q=1/2, g=1: y0=2; d1=2, v1=1, y1=5; d2=-1, v2=1/2, y2=9/2.
        var data = Check(new[] { 2d, 4, 4 }, 5000);
        Assert.Equal(new[] { 2d, 5, 4.5 }, data.OutputValues["Ks"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongSell, Signal.Sell }, data.SignalsList);
        Assert.Equal(new[] { 3d, 3, 3 }, Check(new[] { 3d, 3, 3 }, int.MaxValue).OutputValues["Ks"]);
    }
    [Theory, InlineData(1), InlineData(200), InlineData(5000), InlineData(int.MaxValue)]
    public void CancellationSubnormalsAndUnboundedIntermediates(int length)
    {
        Check(new[] { double.MaxValue, -double.MaxValue, 0, double.Epsilon, 1d, -1d }, length);
        Check(new[] { double.Epsilon, -double.Epsilon, 0, 2 * double.Epsilon }, length);
    }
    [Fact]
    public void ExactQuadraticSignAndMidpointRounding()
    {
        var one = UltimatePowerWeights.Fraction.Of(1);
        var epsilon = UltimatePowerWeights.Fraction.Of(double.Epsilon);
        Assert.Equal(0, new KalmanSmootherWindow.Value(-one, one).Sign(one));
        Assert.Equal(1, new KalmanSmootherWindow.Value(-one + epsilon, one).Sign(one));
        Assert.Equal(-1, new KalmanSmootherWindow.Value(-one - epsilon, one).Sign(one));
        Assert.Equal(double.Epsilon, new KalmanSmootherWindow.Value(-one + epsilon, one).Publish(one));
        Assert.Equal(1d, new KalmanSmootherWindow.Value(one + UltimatePowerWeights.Fraction.Of(Math.ScaleB(1, -53)), 0).Publish(one));
        Assert.Equal(-1, new KalmanSmootherWindow.Value(-one * 3, one * 2).Sign(one * 2));
    }
    [Fact]
    public void PreviewResetAndPrefixInvariance()
    {
        var state = new KalmanSmootherState(200); var control = new KalmanSmootherState(200);
        var prices = new[] { 1d, 3, -4, 7, 2 };
        foreach (var price in prices)
        {
            state.Update(Native(B(double.MaxValue)), false, true);
            state.Update(Native(B(-double.MaxValue)), false, false);
            Assert.Equal(control.Update(Native(B(price)), true, true).Value, state.Update(Native(B(price)), true, true).Value);
        }
        state.Reset(); Assert.Equal(8d, state.Update(Native(B(8)), true, true).Value);
        var full = Check(prices, 200).OutputValues["Ks"];
        for (var n = 1; n < prices.Length; n++) Assert.Equal(full.Take(n), Check(prices.Take(n).ToArray(), 200).OutputValues["Ks"]);
    }
    [Fact]
    public void DirectSelectedInputsBypassCallbacksAndPreserveCandles()
    {
        var bars = new[] { 100d, 101, 99, 103 }.Select((v, i) => B(v, i)).ToArray(); var selected = new[] { 1d, -3, 20, 2 };
        var expected = BuiltInFormulaReferences.KalmanSmootherValues(selected.Select((v, i) => B(v, i)).ToArray(), 200)["Ks"];
        Assert.False(expected.SequenceEqual(BuiltInFormulaReferences.KalmanSmootherValues(bars, 200)["Ks"]));
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using (ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Fixed recurrence")))
        {
            using var fast = IndicatorCompute.ComputeKalmanSmootherFast(data, context, 200); Assert.Equal(expected, fast.ToArray());
            data.CalculateKalmanSmoother(); Assert.Equal(expected, data.OutputValues["Ks"]); Assert.Equal(0, ComponentAverage.Requests);
        }
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        Assert.Equal(Check(selected, 1).OutputValues["Ks"], Check(selected, 0).OutputValues["Ks"]);
    }
    [Fact]
    public void InvalidCandlesDoNotAdvance()
    {
        var state = new KalmanSmootherState(); using var context = new ComputeContext();
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5))
        {
            var v = new[] { 1d, 1, 1, 1, 1 }; v[field] = bad; var bar = new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4]);
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), true, true));
            Assert.ThrowsAny<ArgumentException>(() => Data(new[] { bar }).CalculateKalmanSmoother());
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeKalmanSmootherFast(Data(new[] { bar }), context));
        }
        Assert.Equal(7d, state.Update(Native(B(7)), true, true).Value);
    }
}
