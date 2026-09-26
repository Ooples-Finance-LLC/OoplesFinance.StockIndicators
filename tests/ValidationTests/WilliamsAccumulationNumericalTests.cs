using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WilliamsAccumulationNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactTotalsPreserveCandleReferencesAndRecoverAfterOverflow()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 5, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { 0d, double.MaxValue, 1, double.MaxValue, double.Epsilon, 0 },
            new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 1, -1, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0 } })
        foreach (var expanded in new[] { false, true })
        foreach (var period in new[] { 1, 3, 7 })
        foreach (var average in new IMovingAverage[] { new Sma(), new Ema(), new Wma(), new Smma() })
        foreach (var smoothed in new[] { false, true })
        {
            IBuiltInIndicator indicator = smoothed ? new SmoothedWilliamsAccumulationDistribution(period, average) : new WilliamsAccumulationDistribution();
            var bars = BarsOf(prices).Select(b => expanded ? new Bar(b.Time, b.Open, Math.Max(10, b.High), Math.Min(-10, b.Low), b.Close, b.Volume) : b).ToArray();
            var expected = BuiltInFormulaReferences.WilliamsAccumulationOutputs(bars, indicator);
            var kind = smoothed ? ((SmoothedWilliamsAccumulationDistributionSpecOptions)indicator.CreateOptions()).MaType : MovingAvgType.SimpleMovingAverage;
            var batch = Data(bars);
            if (smoothed) batch.CalculateSmoothedWilliamsAccumulationDistribution(kind, period); else batch.CalculateWilliamsAccumulationDistribution();
            foreach (var pair in expected) Assert.Equal(pair.Value, batch.OutputValues[pair.Key]);
            var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
            using var context = new ComputeContext();
            foreach (var pair in expected)
            {
                using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), pair.Key), context);
                Assert.NotNull(arm); Assert.Equal(pair.Value, arm.Value.ToArray());
                using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), pair.Key), context);
                Assert.NotNull(selectedArm); Assert.Equal(pair.Value, selectedArm.Value.ToArray());
            }
            if (smoothed) selected.CalculateSmoothedWilliamsAccumulationDistribution(kind, period); else selected.CalculateWilliamsAccumulationDistribution();
            foreach (var pair in expected) Assert.Equal(pair.Value, selected.OutputValues[pair.Key]);
            var core = new double[prices.Length]; var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray();
            OoplesFinance.StockIndicators.Core.OscillatorCore.WilliamsAccumulationDistribution(prices, high, low, core);
            Assert.Equal(expected.First().Value, core);
            OoplesFinance.StockIndicators.Core.VolumeCore.WilliamsAD(high, low, prices, core);
            Assert.Equal(expected.First().Value, core);
            IStreamingIndicatorState state = smoothed ? new SmoothedWilliamsAccumulationDistributionState(kind, period) : new WilliamsAccumulationDistributionState();
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
                    }
                }
            }
        }
        var recovery = BarsOf(new[] { 0d, double.MaxValue, 1, double.MaxValue, double.Epsilon, 0 });
        Assert.Equal(recovery.Select(b => b.Close), Data(recovery).CalculateWilliamsAccumulationDistribution().CustomValuesList);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerAverageControlsOnlySignalAndIsConsumedForEitherOutput()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 });
        var indicator = new SmoothedWilliamsAccumulationDistribution(3, new ConstantAverage(42)); var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.WilliamsAccumulationOutputs(bars, builtIn, bars.Select(_ => 42d).ToArray());
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var slot = 0;
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, run[indicator.Outputs[slot++]].ToArray());
            using var armed = ComponentAverage.Arm((input, _) => input.Select(_ => 42d).ToArray());
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), pair.Key), context);
            Assert.NotNull(raw); Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(pair.Value, raw.Value.ToArray());
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var smoothed in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => smoothed ? new SmoothedWilliamsAccumulationDistributionState(MovingAvgType.WeightedMovingAverage, 3) : new WilliamsAccumulationDistributionState();
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "WilliamsAccumulationDistribution", "SmoothedWilliamsAccumulationDistribution" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.WilliamsAccumulationOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
