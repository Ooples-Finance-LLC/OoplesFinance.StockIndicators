using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EaseNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactMidpointRangeVolumeQuotientPreservesFiniteRecovery()
    {
        var normal = BarsOf(new[] { 2d, 5, 3, 8, 1, 1, -3, 0 }).Select((b, i) => new Bar(b.Time, b.Open, b.High + 1, b.Low - 1, b.Close, i % 3 == 0 ? 0 : i % 2 == 0 ? -2 : 2)).ToArray();
        var wide = new[] {
            new Bar(DateTime.UnixEpoch, 0, -double.MaxValue / 2, -double.MaxValue, 0, double.MaxValue),
            new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, double.MaxValue, 0, 0, double.MaxValue),
            new Bar(DateTime.UnixEpoch.AddMinutes(2), 0, double.MaxValue, double.MaxValue / 2, 0, double.MaxValue),
            new Bar(DateTime.UnixEpoch.AddMinutes(3), 0, 0, 0, 0, 0),
            new Bar(DateTime.UnixEpoch.AddMinutes(4), 0, 1, -1, 0, 1) };
        var tiny = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i + 1) * double.Epsilon, (i - 1) * double.Epsilon, 0, i % 2 == 0 ? double.Epsilon : 1)).ToArray();
        foreach (var bars in new[] { Array.Empty<Bar>(), normal, wide, tiny })
        foreach (var divisor in new[] { 1000000d, 1, 0, -2, double.Epsilon, double.MaxValue })
        foreach (var period in new[] { 1, 3, 7 })
        foreach (var average in new IMovingAverage[] { new Sma(), new Ema(), new Wma(), new Smma() })
        {
            var indicator = new EaseOfMovement(period, divisor, average); var builtIn = (IBuiltInIndicator)indicator;
            var expected = BuiltInFormulaReferences.EaseOutputs(bars, builtIn)["Eom"];
            var kind = ((EaseOfMovementSpecOptions)builtIn.CreateOptions()).MaType;
            Assert.Equal(expected, Data(bars).CalculateEaseOfMovement(kind, period, divisor).CustomValuesList);
            using var context = new ComputeContext();
            using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
            Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var selected = Data(bars); selected.CustomValuesList = bars.Select(_ => 42d).ToList();
            using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
            Assert.NotNull(selectedArm); Assert.Equal(expected, selectedArm.Value.ToArray());
            Assert.Equal(expected, selected.CalculateEaseOfMovement(kind, period, divisor).CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.VolumeCore.EaseOfMovement(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Volume).ToArray(), core, divisor);
            Assert.Equal(expected, core);
            var state = new EaseOfMovementState(divisor);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(normal[2]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
            Assert.All(expected, v => Assert.False(double.IsNaN(v)));
        }
        var finite = BuiltInFormulaReferences.EaseOutputs(wide, new EaseOfMovement(1, double.Epsilon))["Eom"];
        Assert.True(double.IsFinite(finite[1]) && finite[1] > 0);
        Assert.Equal(finite, Data(wide).CalculateEaseOfMovement(length: 1, divisor: double.Epsilon).CustomValuesList);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerSignalStagesExecuteInOrderWithoutChangingRawEom()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 }).Select(b => new Bar(b.Time, b.Open, b.High + 1, b.Low - 1, b.Close, 2)).ToArray();
        var indicator = new EaseOfMovement(3, 1, new ConstantAverage(2), new ConstantAverage(4)); var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.EaseOutputs(bars, builtIn)["Eom"];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, run[indicator].ToArray());
        var factories = new[] { 2d, 4d }.Select((v, stage) => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, _) =>
        { Assert.Equal(stage == 0 ? expected : bars.Select(_ => 2d), input); return input.Select(_ => v).ToArray(); })).ToArray();
        using var armed = ComponentAverage.Arm(factories); using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
        Assert.NotNull(raw); Assert.Equal(2, ComponentAverage.Substitutions); Assert.Equal(expected, raw.Value.ToArray());
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var state = new EaseOfMovementState(3); var control = new EaseOfMovementState(3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, 3, 2)); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    [Fact]
    public void NonfiniteDivisorsAreRejected()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new EaseOfMovementState(invalid));
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "EaseOfMovement", "Emv" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.EaseOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
