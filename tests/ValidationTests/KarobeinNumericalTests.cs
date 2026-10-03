using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KarobeinNumericalTests
{
    private static Bar B(double value, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("HF", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KarobeinOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRationalReference(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KarobeinOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourceRetainsTheFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static StockData Check(double[] prices, int length = 2, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var bars = prices.Select((v, i) => B(v, i)).ToArray(); var expected = BuiltInFormulaReferences.KarobeinValues(bars, length, Kind(kind))["Ko"];
        var data = Data(bars).CalculateKarobeinOscillator(kind, length); Assert.Equal(expected, data.OutputValues["Ko"]);
        using var state = new KarobeinOscillatorState(kind, length);
        for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeKarobeinOscillatorFast(Data(bars), context, length, kind); Assert.Equal(expected, fast.ToArray());
        return data;
    }
    [Fact]
    public void IndependentRatioAndSmoothingHands()
    {
        var data = Check(new[] { 1d, 2, 1 }, 1);
        Assert.Equal(new[] { 0d, 1, 0 }, data.OutputValues["Ko"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell }, data.SignalsList);
        // EMA2 prices [1,3/2,7/6], ratios [0,3/2,7/9], last fall14/27 and rise1/4.
        Assert.Equal(new[] { 0d, 1, 385d / 1169 }, Check(new[] { 1d, 2, 1 }).OutputValues["Ko"]);
        Assert.Equal(new[] { 0d, 0, 0 }, Check(new[] { 0d, 0, 0 }).OutputValues["Ko"]);
        Assert.Equal(new[] { 0d, 1, 1 }, Check(new[] { 2d, 2, 2 }).OutputValues["Ko"]);
    }
    [Fact]
    public void SmaWarmupRequiresTheCompleteWindow()
    {
        // SMA(2) prices are [0,3/2,3/2], so ratios are [0,0,1].
        // No mean change is nonzero once a valid prior mean exists: both
        // directional averages stay zero and the final nonzero ratio folds to 1.
        Assert.Equal(new[] { 0d, 0, 1 }, Check(new[] { 1d, 2, 1 }, 2, MovingAvgType.SimpleMovingAverage).OutputValues["Ko"]);
    }
    [Theory, InlineData(1), InlineData(2), InlineData(int.MaxValue)]
    public void ExtremeRatiosAndPeriodsStayBounded(int length)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var data = Check(new[] { double.Epsilon, double.MaxValue, double.Epsilon, double.MaxValue / 2, 1d }, length, kind);
            Assert.All(data.OutputValues["Ko"], value => Assert.InRange(value, 0, 1));
            Check(Array.Empty<double>(), length, kind);
        }
    }
    [Theory, InlineData(false), InlineData(true)]
    public void BothExactPolesAreRejectedWithoutCommittingAnyStage(bool second)
    {
        var prices = second ? new[] { -3d, 2, -1, -1, -1 } : new[] { 1d, 1, 3, -7 };
        var bars = prices.Select((v, i) => B(v, i)).ToArray();
        Assert.Throws<ArgumentException>(() => BuiltInFormulaReferences.KarobeinValues(bars, 2, 1));
        var data = Data(bars); var prior = data.ChainedValues.ToArray(); using var context = new ComputeContext();
        Assert.Throws<ArgumentException>(() => data.CalculateKarobeinOscillator(MovingAvgType.SimpleMovingAverage, 2)); Assert.Equal(prior, data.ChainedValues);
        Assert.Throws<ArgumentException>(() => IndicatorCompute.ComputeKarobeinOscillatorFast(data, context, 2, MovingAvgType.SimpleMovingAverage));
        using var state = new KarobeinOscillatorState(MovingAvgType.SimpleMovingAverage, 2); using var control = new KarobeinOscillatorState(MovingAvgType.SimpleMovingAverage, 2);
        foreach (var bar in bars.Take(bars.Length - 1)) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
        foreach (var final in new[] { false, true, false }) Assert.Throws<ArgumentException>(() => state.Update(Native(bars[^1]), final, true));
        foreach (var value in new[] { 4d, 7, 8 }) Assert.Equal(control.Update(Native(B(value)), true, true).Value, state.Update(Native(B(value)), true, true).Value);
    }
    [Fact]
    public void NearPolesAreNotCollapsedIntoTies()
    {
        foreach (var last in new[] { Math.BitIncrement(-7d), Math.BitDecrement(-7d) }) Check(new[] { 1d, 1, 3, last }, 2, MovingAvgType.SimpleMovingAverage);
        foreach (var last in new[] { Math.BitIncrement(-1d), Math.BitDecrement(-1d) }) Check(new[] { -3d, 2, -1, -1, last }, 2, MovingAvgType.SimpleMovingAverage);
    }
    [Fact]
    public void PreviewResetAndSignedInputsRemainCausal()
    {
        var prices = new[] { -8d, 2, -5, 9, 0, 4 }; var full = Check(prices, 3).OutputValues["Ko"];
        using var state = new KarobeinOscillatorState(length: 3); using var control = new KarobeinOscillatorState(length: 3);
        foreach (var price in prices)
        {
            state.Update(Native(B(double.MaxValue)), false, true);
            Assert.Equal(control.Update(Native(B(price)), true, true).Value, state.Update(Native(B(price)), true, true).Value);
        }
        state.Reset(); Assert.Equal(0d, state.Update(Native(B(8)), true, true).Value);
        for (var n = 1; n < prices.Length; n++) Assert.Equal(full.Take(n), Check(prices.Take(n).ToArray(), 3).OutputValues["Ko"]);
    }
    [Fact]
    public void ThreeCallbacksPreserveStageOrderShortMeansAndSelectedInput()
    {
        var bars = new[] { 100d, 101, 102 }.Select((v, i) => B(v, i)).ToArray(); var selected = new[] { 1d, 2, 1 }; var data = Data(bars); data.SetCustomValues(selected.ToList());
        var requests = new List<(double[] Values, int Period)>();
        IReadOnlyList<double> Callback(IReadOnlyList<double> values, int period) { requests.Add((values.ToArray(), period)); return requests.Count == 1 ? selected : Array.Empty<double>(); }
        using var context = new ComputeContext();
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { Callback, Callback, Callback }))
        {
            using var fast = IndicatorCompute.ComputeKarobeinOscillatorFast(data, context, 3); Assert.Equal(new[] { 0d, 1, 1 }, fast.ToArray());
            Assert.Equal(selected, requests[0].Values); Assert.Equal(new[] { 0d, 0, .5 }, requests[1].Values); Assert.Equal(new[] { 0d, 2, 0 }, requests[2].Values);
            Assert.All(requests, request => Assert.Equal(3, request.Period)); Assert.Equal(3, ComponentAverage.Substitutions);
        }
        var expected = Check(selected, 3).OutputValues["Ko"]; using var nativeFast = IndicatorCompute.ComputeKarobeinOscillatorFast(data, context, 3); Assert.Equal(expected, nativeFast.ToArray());
        data.CalculateKarobeinOscillator(length: 3); Assert.Equal(expected, data.OutputValues["Ko"]); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    [Fact]
    public void InvalidCandlesDoNotAdvance()
    {
        using var state = new KarobeinOscillatorState(); using var context = new ComputeContext();
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5))
        {
            var v = new[] { 1d, 1, 1, 1, 1 }; v[field] = bad; var bar = new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4]);
            foreach (var final in new[] { false, true }) Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), final, true));
            Assert.ThrowsAny<ArgumentException>(() => Data(new[] { bar }).CalculateKarobeinOscillator());
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeKarobeinOscillatorFast(Data(new[] { bar }), context));
        }
        Assert.Equal(0d, state.Update(Native(B(7)), true, true).Value);
    }
}
