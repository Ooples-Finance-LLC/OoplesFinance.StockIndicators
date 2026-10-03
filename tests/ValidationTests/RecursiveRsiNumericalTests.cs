using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RecursiveRsiNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RRSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(RecursiveRelativeStrengthIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCascade(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RecursiveRsiOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 14, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.RecursiveRsiValues(bars, length, kind);
        var batch = Data(bars).CalculateRecursiveRelativeStrengthIndex(kind, length);
        Assert.Equal(expected.Outputs["Rrsi"], batch.OutputValues["Rrsi"]); Assert.Equal(expected.Outputs["Rrsi"], batch.CustomValuesList);
        Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeRecursiveRelativeStrengthIndexFast(Data(bars), context, length, kind);
        Assert.Equal(expected.Outputs["Rrsi"], fast.ToArray());
        using var state = new RecursiveRelativeStrengthIndexState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(3, -1, 8)) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(double.MaxValue)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Rrsi"][i], point.Value);
                    Assert.Equal(point.Value, point.Outputs!["Rrsi"]);
                }
            }
        }
        return expected.Outputs["Rrsi"];
    }
    [Fact]
    public void IndependentDelayedVoteHandsExcludeCurrentVote()
    {
        Assert.Equal(new[] { 100d, 100, 100, 0 }, Check(Bars(2, 4, 2, 4), 1));
        Assert.Equal(new[] { 100d, 100, 100, 100, 100, 100, 100, 200d / 3, 200d / 3, 100d / 3 }, Check(Bars(2, 4, 1, 3, 0, 5, 2, 2, 7, 1), 3));
    }
    [Fact]
    public void ExtremeAndSubnormalChangesRetainExactDirection()
    {
        var prices = new[] { 2d, 4, 1, 3, 0, 5, 2, 2, 7, 1, -3, 6, -1 };
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var ordinary = Check(Bars(prices), 3, kind);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1020) })
                Assert.Equal(ordinary, Check(Bars(prices.Select(v => v * scale).ToArray()), 3, kind));
            Check(Bars(double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, -1, double.Epsilon, -double.Epsilon, 2), 2, kind);
            Check(Bars(1, Math.BitIncrement(1), 1, Math.BitDecrement(1), 1, Math.BitIncrement(1), 1), 2, kind);
        }
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndNormalizeConsistently()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), period, kind); Check(Bars(1, 4, -2, 3, 0), period, kind); }
    }
    [Fact]
    public void TiesExpiryAndRecursiveTransitionsPreservePreviewsAndReset()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            Check(Bars(Enumerable.Range(0, 90).Select(i => (double)(i % 13 - 5)).ToArray()), 3, kind);
            Assert.All(Check(Bars(Enumerable.Repeat(2d, 12).ToArray()), 3, kind), v => Assert.Equal(100, v));
        }
    }
    [Fact]
    public void CallbackGraphUsesSourceThenGainThenLoss()
    {
        var requests = new List<(double[] Values, int Period)>(); var source = new[] { -200d, 100, -100, 200, 0, 50 };
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
        { requests.Add((values.ToArray(), period)); return requests.Count == 1 ? source : Enumerable.Repeat(requests.Count == 2 ? 3d : 1d, values.Count).ToArray(); };
        var data = Data(Bars(1, 3, 2, 5, 4, 6)); var prior = data.ChainedValues.ToArray();
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray()); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeRecursiveRelativeStrengthIndexFast(data, context, 2);
        Assert.Equal(3, ComponentAverage.Substitutions); Assert.Equal(3, requests.Count); Assert.All(requests, v => Assert.Equal(2, v.Period));
        Assert.Equal(new[] { 0d, 0, 1, 2, 2, 1 }, requests[0].Values);
        Assert.Equal(new[] { 0d, 300, 0, 300, 0, 50 }, requests[1].Values); Assert.Equal(new[] { 0d, 0, 200, 0, 200, 0 }, requests[2].Values);
        Assert.Equal(new[] { 0d, 100, 50, 100, 100, 100 }, actual.ToArray()); Assert.Equal(prior, data.ChainedValues);
    }
    [Fact]
    public void CallbackGainLossSumCannotOverflowBeforeBoundedStrength()
    {
        var requests = 0;
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, _) => Enumerable.Repeat(++requests == 1 ? -25d : double.MaxValue, values.Count).ToArray();
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray()); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeRecursiveRelativeStrengthIndexFast(Data(Bars(1, 3, 2, 5, 4, 6)), context, 2);
        Assert.All(actual.ToArray(), v => Assert.Equal(100, v)); Assert.Equal(3, ComponentAverage.Substitutions);
    }
    [Fact]
    public void BatchDoesNotConsumeStandardAverageOverrides()
    {
        var bars = Bars(2, 4, 1, 3, 0, 5, 2, 2, 7, 1);
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Batch must not consume fast override slots.");
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray());
        Assert.Equal(BuiltInFormulaReferences.RecursiveRsiValues(bars, 3, MovingAvgType.SimpleMovingAverage).Outputs["Rrsi"], Data(bars).CalculateRecursiveRelativeStrengthIndex(length: 3).CustomValuesList);
        Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void InvalidBarsCannotAdvanceState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new RecursiveRelativeStrengthIndexState(); using var control = new RecursiveRelativeStrengthIndexState();
            foreach (var b in Bars(1, 3, -1)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 3, -1, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(0, 3, -2)) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
}
