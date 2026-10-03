using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PremierStochasticNumericalTests
{
    private static Bar B(double high, double low, double close) => new(DateTime.UnixEpoch, close, high, low, close, 1);
    private static Bar[] Bars(double[] prices) => prices.Select(p => B(p, p, p)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("PSO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 1;
    private static void Equal(double expected, double actual) => Assert.True(BuiltInFormulaReferences.RsiInverseFisherBudget.Accepts(expected, actual), $"Expected {expected:R}, got {actual:R}");
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(PremierStochastic) || c.IndicatorType == typeof(PremierStochasticOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCenteredGeometry(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.PremierOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.RsiInverseFisherBudget);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 3, int smooth = 10, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.PremierValues(bars, length, smooth, Kind(kind))["Pso"];
        var batch = Data(bars).CalculatePremierStochasticOscillator(kind, length, smooth); var actual = batch.CustomValuesList.ToArray();
        Assert.Equal(actual, batch.OutputValues["Pso"]); Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < actual.Length; i++)
        {
            Equal(expected[i], actual[i]); Assert.InRange(actual[i], -1, 1);
            var current = ReferenceFraction.FromDouble(actual[i]); var previous = ReferenceFraction.FromDouble(i == 0 ? 0 : actual[i - 1]); var before = ReferenceFraction.FromDouble(i < 2 ? 0 : actual[i - 2]);
            var slope = current - previous; var oldSlope = previous - before;
            Assert.Equal(slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None, batch.SignalsList[i]);
        }
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputePremierStochasticFast(Data(bars), context, length, smooth, kind); Assert.Equal(actual, fast.ToArray());
        if (kind == MovingAvgType.ExponentialMovingAverage)
        {
            var output = new double[bars.Length]; OscillatorCore.PremierStochastic(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), output, length, smooth);
            Assert.Equal(actual, output);
        }
        using var state = new PremierStochasticOscillatorState(kind, length, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 7d, -2, 1 })) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(double.MaxValue, -double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(actual[i], point.Value); Assert.Equal(actual[i], point.Outputs!["Pso"]); }
            }
        }
        return actual;
    }
    [Fact]
    public void IndependentMidlineAndSubnormalHands()
    {
        Assert.Equal(0, Check(new[] { B(1, -1, 0) })[0]);
        // Translation must preserve centered geometry; a zero midpoint alone cannot detect a bad price coefficient.
        Assert.Equal(0, Check(new[] { B(3, 1, 2) })[0]);
        Assert.Equal(0, Check(new[] { B(-1, -3, -2) })[0]);
        Equal(ReferenceFraction.FromDouble(1.25).TanhToDouble(), Check(new[] { B(3, 1, 2.5) })[0]);
        Equal(ReferenceFraction.FromDouble(-1.25).TanhToDouble(), Check(new[] { B(1, 3, 2.5) })[0]);
        Assert.Equal(7 * double.Epsilon, Check(new[] { B(1, -1, 3 * double.Epsilon) })[0]);
        Assert.Equal(-7 * double.Epsilon, Check(new[] { B(1, -1, -3 * double.Epsilon) })[0]);
        Equal(ReferenceFraction.FromDouble(2.5).TanhToDouble(), Check(new[] { B(1, -1, 1) })[0]);
        Equal(ReferenceFraction.FromDouble(-2.5).TanhToDouble(), Check(new[] { B(1, 1, 1) })[0]);
        Assert.Equal(4, PremierStochasticWindow.Resolve(10)); Assert.Equal(530, PremierStochasticWindow.Resolve(int.MaxValue));
    }
    [Fact]
    public void HalfTanhResolvesEverySubnormalMidpointAndLargeCallbackValue()
    {
        foreach (var units in Enumerable.Range(0, 65))
        foreach (var sign in new[] { 1d, -1d })
        {
            var value = sign * units * double.Epsilon;
            var expected = sign * (units / 2) * double.Epsilon;
            Assert.Equal(expected, BuiltInFormulaReferences.PremierHalfTanhReference(value));
            Assert.Equal(expected, PremierStochasticWindow.HalfTanh(value));
        }
        foreach (var value in new[] { -double.MaxValue, -40, -5, -.25, -1e-20, 1e-20, .25, 5, 40, double.MaxValue })
            Equal(BuiltInFormulaReferences.PremierHalfTanhReference(value), PremierStochasticWindow.HalfTanh(value));
    }
    [Fact]
    public void ExtremeRangesAndReversedEndpointsStayBounded()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            Check(new[] { B(double.MaxValue, -double.MaxValue, 0), B(double.MaxValue, -double.MaxValue, double.MaxValue / 2), B(1, -1, 3 * double.Epsilon), B(1, -1, -3 * double.Epsilon), B(0, 0, 0), B(1, -1, 0) }, 2, 10, kind);
            Check(new[] { B(1, -1, 2), B(1, -1, -2), B(-1, 1, 0), B(-1, 1, -2), B(-1, 1, 2) }, 1, 2, kind);
            foreach (var scale in new[] { double.Epsilon, 1d, Math.Pow(2, 1020) })
                Check(new[] { B(4 * scale, -scale, scale), B(3 * scale, -2 * scale, 2 * scale), B(scale, -4 * scale, 0) }, 3, 2, kind);
        }
    }
    [Fact]
    public void ExtremePeriodsStayLazyAndExpiryPreservesPreviews()
    {
        var bars = Bars(new[] { 0d, 4, -1, 1, 7, -3, 0, 2, 1, -2 });
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), period, period, kind); Check(bars, period, period, kind); }
        Check(Bars(Enumerable.Range(0, 1065).Select(i => (double)(i % 17 - 6)).ToArray()), 7, int.MaxValue);
    }
    [Fact]
    public void CallbacksPreserveBothStagesAndResolvedPeriod()
    {
        var bars = new[] { B(1, -1, 0), B(1, -1, 1), B(1, -1, -1), B(1, -1, 0) }; var first = new[] { 1d, 2, 3, 4 }; var second = new[] { 3 * double.Epsilon, -3 * double.Epsilon, .25, double.MaxValue };
        var requests = new List<(double[] Values, int Period)>(); var data = Data(bars); var prior = data.ChainedValues.ToArray();
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return requests.Count == 1 ? first : second; };
        using var armed = ComponentAverage.Arm(new[] { callback, callback }); using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputePremierStochasticFast(data, context, 1, 10);
        Assert.Equal(new[] { 0d, 5, -5, 0 }, requests[0].Values); Assert.Equal(first, requests[1].Values); Assert.All(requests, v => Assert.Equal(4, v.Period)); Assert.Equal(2, ComponentAverage.Substitutions); Assert.Equal(prior, data.ChainedValues);
        for (var i = 0; i < second.Length; i++) Equal(BuiltInFormulaReferences.PremierHalfTanhReference(second[i]), actual.Span[i]);
    }
    [Fact]
    public void CoreSpanGuardsAndInPlaceReplay()
    {
        var prices = new[] { 2d, -4, 7, 0, 1 }; var output = Enumerable.Repeat(999d, prices.Length + 2).ToArray();
        Assert.Throws<ArgumentException>(() => OscillatorCore.PremierStochastic(prices, prices, prices, output.AsSpan(0, 2), 2, 10)); Assert.All(output, v => Assert.Equal(999, v));
        Assert.Throws<ArgumentException>(() => OscillatorCore.PremierStochastic(prices.AsSpan(0, 2), prices, prices, output, 2, 10));
        Assert.Throws<ArgumentException>(() => OscillatorCore.PremierStochastic(prices, prices.AsSpan(0, 2), prices, output, 2, 10));
        var expected = Check(Bars(prices), 2, 10); OscillatorCore.PremierStochastic(prices, prices, prices, output, 2, 10); Assert.Equal(expected, output.Take(prices.Length)); Assert.Equal(999, output[^1]);
        var replay = prices.ToArray(); OscillatorCore.PremierStochastic(prices, prices, replay, replay, 2, 10); Assert.Equal(expected, replay);
        OscillatorCore.PremierStochastic(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new PremierStochasticOscillatorState(length: 3, smoothLength: 10); using var control = new PremierStochasticOscillatorState(length: 3, smoothLength: 10);
            foreach (var b in Bars(new[] { 0d, 3, -1 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, -1, 1, 1 }; values[field] = invalid; var bad = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(bad), final, true));
            foreach (var b in Bars(new[] { 1d, -3, 0, 4 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
    [Fact]
    public void LegacySmoothingKeepsBatchFallback()
    {
        var bars = Bars(new[] { 2d, 5, 1, -2, 7, 3, 0 }); var batch = Data(bars).CalculatePremierStochasticOscillator(MovingAvgType.LinearWeightedMovingAverage, 3, 10);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputePremierStochasticFast(Data(bars), context, 3, 10, MovingAvgType.LinearWeightedMovingAverage);
        Assert.Equal(batch.CustomValuesList, fast.ToArray()); Assert.Throws<NotSupportedException>(() => new PremierStochasticOscillatorState(MovingAvgType.LinearWeightedMovingAverage, 3, 10));
    }
}
