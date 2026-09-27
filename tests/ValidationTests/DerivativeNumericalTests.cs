using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DerivativeNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("DO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Bars(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DerivativeOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRsiSmoothingAndPartialMean(IndicatorValidationCase c, string route)
    {
        var options = (DerivativeOscillatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); var kind = options.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 3;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Do", BuiltInFormulaReferences.DerivativeOutputs(bars, options.Length, 9, 5, 3, kind) } }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int rsi, int mean, int first, int second, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.DerivativeOutputs(bars, rsi, mean, first, second, k);
        Assert.Equal(expected, Data(bars).CalculateDerivativeOscillator(kind, rsi, mean, first, second).OutputValues["Do"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeDerivativeOscillatorFast(Data(bars), context, rsi, kind, mean, first, second); Assert.Equal(expected, raw.ToArray());
        using var state = new DerivativeOscillatorState(kind, rsi, mean, first, second);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Bars(new[] { double.MaxValue })[0]), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Do"]); }
            }
        }
    }
    [Fact]
    public void HandPartialMeanAndZeroOriginAreDistinct()
    {
        var bars = Bars(new[] { 2d, 4, 2 }); var expected = BuiltInFormulaReferences.DerivativeOutputs(bars, 1, 2, 1, 1);
        Assert.Equal(new[] { 0d, 0, -50 }, expected); Check(bars, 1, 2, 1, 1);
        Assert.All(BuiltInFormulaReferences.DerivativeOutputs(bars, 1, 1, 1, 1), v => Assert.Equal(0, v));
    }
    [Fact]
    public void ExtremeAndSubnormalChangesRetainAllSmoothingStages()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var periods in new[] { (1, 1, 1, 1), (2, 3, 4, 2), (14, 9, 5, 3) })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Bars(Enumerable.Range(0, 64).Select(i => (i % 3 - 1) * scale).ToArray()), periods.Item1, periods.Item2, periods.Item3, periods.Item4, kind);
            Check(Bars(Enumerable.Range(0, 64).Select(i => .5 + .4 * Math.Sin(i * .37)).ToArray()), periods.Item1, periods.Item2, periods.Item3, periods.Item4, kind);
            Check(Bars(new[] { 1d, 2, 1, 1, 1, 1, 1, 1 }), periods.Item1, periods.Item2, periods.Item3, periods.Item4, kind);
            Check(Array.Empty<Bar>(), periods.Item1, periods.Item2, periods.Item3, periods.Item4, kind);
        }
    }
    [Fact]
    public void RawSelectedPricesFeedRsiBeforeSmoothing()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 }; var bars = Bars(Enumerable.Repeat(0d, selected.Length).ToArray());
        var expected = BuiltInFormulaReferences.DerivativeOutputs(Bars(selected), 2, 9, 5, 3);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.DerivativeOscillator, new DerivativeOscillatorSpecOptions(2)), context); Assert.NotNull(raw); Assert.Equal(expected, raw.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateDerivativeOscillator(length1: 2).OutputValues["Do"]);
    }
    [Fact]
    public void CustomerAveragesReceiveRsiThenFirstStage()
    {
        var bars = Bars(new[] { 2d, 4, 2 });
        foreach (var batch in new[] { false, true })
        {
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] callbacks = {
                (input, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 100d, 100, 0 }, input); return new[] { 2d, 4, 6 }; },
                (input, period) => { Assert.Equal(4, period); Assert.Equal(new[] { 2d, 4, 6 }, input); return new[] { 3d, 6, 9 }; }
            };
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch) Assert.Equal(new[] { 0d, 1.5, 1.5 }, Data(bars).CalculateDerivativeOscillator(length1: 1, length2: 2, length3: 3, length4: 4).OutputValues["Do"]);
            else { using var raw = IndicatorCompute.ComputeDerivativeOscillatorFast(Data(bars), context, 1, length2: 2, length3: 3, length4: 4); Assert.Equal(new[] { 0d, 1.5, 1.5 }, raw.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceRsiOrMeans()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new DerivativeOscillatorState(length1: 2); using var control = new DerivativeOscillatorState(length1: 2);
            var seed = Native(Bars(new[] { 2d })[0]); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, 4, 3, 0, 8, 2, 9, 3, 0 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
