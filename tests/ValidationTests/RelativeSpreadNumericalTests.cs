using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RelativeSpreadNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RRSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(RelativeSpreadStrength)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentSpread(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RelativeSpreadOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int fast = 2, int slow = 3, int length = 2, int smooth = 2, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.RelativeSpreadValues(bars, fast, slow, length, smooth, kind)["Rss"];
        var batch = Data(bars).CalculateRelativeSpreadStrength(kind, fast, slow, length, smooth);
        Assert.Equal(expected, batch.OutputValues["Rss"]); Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeRelativeSpreadStrengthFast(Data(bars), context, fast, slow, length, smooth, kind);
        Assert.Equal(expected, actual.ToArray());
        using var state = new RelativeSpreadStrengthState(kind, fast, slow, length, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Bars(7)[0]), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(double.MaxValue)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["Rss"]); }
            }
        }
        return expected;
    }
    [Fact]
    public void HandSpreadStrengthAndSignalBoundary()
    {
        // SMA1-SMA2 on [0,2,0,2] is [0,1,-1,1]. Wilder2 gains/losses
        // are [0,1/2,1/4,9/8] and [0,0,1,1/2], hence RSI [100,100,20,900/13].
        Assert.Equal(new[] { 100d, 100, 20, 900d / 13 }, Check(Bars(0, 2, 0, 2), 1, 2, 2, 1, MovingAvgType.SimpleMovingAverage));
        Assert.Equal(new[] { 0d, 100, 60, (20 + 900d / 13) / 2 }, Check(Bars(0, 2, 0, 2), 1, 2, 2, 2, MovingAvgType.SimpleMovingAverage));
    }
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod, MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage };
    [Fact]
    public void ExtremeAndSubnormalSpreadChangesRemainVisible()
    {
        var prices = new[] { 0d, 2, -1, 3, 0, -2, 1, 4 };
        foreach (var kind in Kinds)
        {
            var ordinary = Check(Bars(prices), kind: kind);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1020) }) Assert.Equal(ordinary, Check(Bars(prices.Select(v => v * scale).ToArray()), kind: kind));
            Check(Bars(double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, -1, double.Epsilon), kind: kind);
            Check(Bars(1, Math.BitIncrement(1), Math.BitDecrement(1), 1, Math.BitIncrement(1)), kind: kind);
        }
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndNormalizeConsistently()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue }) foreach (var kind in Kinds)
        { Check(Array.Empty<Bar>(), period, period, period, period, kind); Check(Bars(1, 4, -2, 3, 0), period, 3, period, period, kind); }
    }
    [Fact]
    public void TiesExpiryAndTransitionsPreservePreviewsAndReset()
    {
        foreach (var kind in Kinds)
        { Check(Bars(Enumerable.Range(0, 48).Select(i => (double)(i % 9 - 4)).ToArray()), kind: kind); Check(Bars(Enumerable.Repeat(2d, 12).ToArray()), kind: kind); }
    }
    [Fact]
    public void CallbackGraphPreservesFastSlowAndSignalSlots()
    {
        var calls = new List<(double[] Values, int Period)>();
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (v, n) =>
        { calls.Add((v.ToArray(), n)); return calls.Count == 1 ? new[] { 0d, double.MaxValue, -double.MaxValue } : calls.Count == 2 ? new[] { 0d, -double.MaxValue, double.MaxValue } : v.ToArray(); };
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray()); using var context = new ComputeContext();
        var data = Data(Bars(1, 4, -2)); var prior = data.ChainedValues.ToArray();
        using var result = IndicatorCompute.ComputeRelativeSpreadStrengthFast(data, context, 2, 3, 2, 4);
        Assert.Equal(3, ComponentAverage.Substitutions); Assert.Equal(new[] { 2, 3, 4 }, calls.Select(v => v.Period));
        Assert.Equal(new[] { 1d, 4, -2 }, calls[0].Values); Assert.Equal(calls[0].Values, calls[1].Values);
        Assert.Equal(new[] { 100d, 100, 20 }, calls[2].Values); Assert.Equal(calls[2].Values, result.ToArray()); Assert.Equal(prior, data.ChainedValues);
    }
    [Fact]
    public void BatchDoesNotConsumeAverageOverrides()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Batch cannot consume fast slots.");
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray());
        var bars = Bars(1, 3, 2, 4); var actual = Data(bars).CalculateRelativeSpreadStrength();
        Assert.Equal(BuiltInFormulaReferences.RelativeSpreadValues(bars, 10, 40, 14, 5, MovingAvgType.ExponentialMovingAverage)["Rss"], actual.CustomValuesList);
        Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void InvalidBarsCannotAdvanceState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new RelativeSpreadStrengthState(); using var control = new RelativeSpreadStrengthState();
            foreach (var b in Bars(1, 3, -1)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 3, -1, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(0, 3, -2)) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
}
