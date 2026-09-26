using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ReversalPointsNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void WideMovementSurvivesBothAveragesAndRollingEviction()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 3, 8, 3, 1, 5, 0, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, double.MaxValue, -double.MaxValue, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 },
            Enumerable.Repeat(0d, 20).Concat(new[] { 7d }).Concat(Enumerable.Repeat(7d, 80)).ToArray() })
        foreach (var period in new[] { 1, 3, 7 })
        foreach (var indicator in new IBuiltInIndicator[] { new ReversalPoints(period), new ReversalPoints(period, new Sma()),
            new ReversalPoints(period, new Wma()), new ReversalPoints(period, new Smma()) })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.ReversalPointsOutputs(bars, indicator)["Rp"];
            var options = indicator.CreateOptions(); var spec = new IndicatorSpec(indicator.BatchName, options);
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            Assert.Equal(expected, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray());
            using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm); Assert.Equal(expected, selectedArm.Value.ToArray());
            Assert.Equal(expected, BuilderArmBinding.Compute(selected, spec, target).ToArray());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec)! })
            {
                Assert.NotNull(state); using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                        foreach (var final in new[] { false, false, true })
                            Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                    }
                }
            }
        }
        var firstBar = BuiltInFormulaReferences.ReversalPointsOutputs(BarsOf(new[] { 7d }), new ReversalPoints(3))["Rp"];
        Assert.Equal(1, firstBar[0]);
    }

    [Fact]
    public void CustomerAveragesConsumePriceThenBothMovementStages()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 });
        using var armed = ComponentAverage.Arm(new[] { 19d, 6, 2 }.Select(v =>
            (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, _) => input.Select(_ => v).ToArray())).ToArray());
        using var context = new ComputeContext(); var indicator = (IBuiltInIndicator)new ReversalPoints(3);
        using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions()), context);
        Assert.NotNull(raw); Assert.Equal(3, ComponentAverage.Substitutions);
        Assert.Equal(new[] { 3d, 6, 9, 9, 9 }, raw.Value.ToArray());
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 1))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => new ReversalPointsState(MovingAvgType.WeightedMovingAverage, 3);
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "ReversalPoints" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ReversalPointsOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);

    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);

    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
