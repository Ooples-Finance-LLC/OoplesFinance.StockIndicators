using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PriceVolumeTrendNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactContributionsPreserveAliasesSelectionAndState()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 5, 3, 8, -3, 1, -5, 0, 2, 4, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, -1, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 } })
        foreach (var scale in new[] { .25, double.MaxValue, double.Epsilon, -2d, 0d })
        foreach (var period in new[] { 1, 2, 7 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var modified in new[] { false, true })
        {
            IMovingAverage average = kind switch { MovingAvgType.SimpleMovingAverage => new Sma(), MovingAvgType.WeightedMovingAverage => new Wma(), MovingAvgType.WildersSmoothingMethod => new Smma(), _ => new Ema() };
            IBuiltInIndicator indicator = modified ? new ModifiedPriceVolumeTrend(period, average) : new PriceVolumeTrend(period, average);
            var bars = BarsOf(prices).Select(b => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, scale)).ToArray();
            var expected = BuiltInFormulaReferences.PriceVolumeTrendOutputs(bars, indicator);
            var key = modified ? "Mpvt" : "Pvt";
            var batch = modified ? Data(bars).CalculateModifiedPriceVolumeTrend(kind, period) : Data(bars).CalculatePriceVolumeTrend(kind, period);
            Assert.Equal(expected[key], batch.CustomValuesList); Assert.Equal(expected["Signal"], batch.OutputValues["Signal"]);
            using var context = new ComputeContext();
            foreach (var output in new[] { key, "Signal" })
            {
                using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), output), context);
                Assert.NotNull(arm); Assert.Equal(expected[output], arm.Value.ToArray());
                var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
                using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), output), context);
                Assert.NotNull(selectedArm); Assert.Equal(expected[output], selectedArm.Value.ToArray());
            }
            if (!modified)
            {
                var core = new double[bars.Length]; var volumes = bars.Select(b => b.Volume).ToArray();
                OoplesFinance.StockIndicators.Core.VolumeCore.PriceVolumeTrend(prices, volumes, core); Assert.Equal(expected[key], core);
                OoplesFinance.StockIndicators.Core.VolumeCore.VolumePriceTrend(prices, volumes, core); Assert.Equal(expected[key], core);
            }
            using var ordinary = new PriceVolumeTrendState(kind, period); using var adjusted = new ModifiedPriceVolumeTrendState(kind, period);
            IStreamingIndicatorState state = modified ? adjusted : ordinary;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        Assert.Equal(expected[key][i], actual.Value); Assert.Equal(expected["Signal"][i], actual.Outputs!["Signal"]);
                    }
                }
            }
        }
        var zeroBars = BarsOf(new[] { 2d, 4, 0, 2, 4 });
        Assert.Equal(new[] { 0d, 1, 0, 0, 1 }, Data(zeroBars).CalculatePriceVolumeTrend().CustomValuesList);
        var recovery = BarsOf(new[] { 1d, 3, 0 }).Select(b => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, double.MaxValue)).ToArray();
        var recovered = Data(recovery).CalculatePriceVolumeTrend(MovingAvgType.SimpleMovingAverage, 2);
        Assert.Equal(double.MaxValue, recovered.CustomValuesList[2]);
        Assert.Equal(double.MaxValue, recovered.OutputValues["Signal"][1]);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerSignalReceivesCumulativeLineForEveryOutputRequest()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 });
        foreach (var indicator in new IIndicator[] { new PriceVolumeTrend(3, new ConstantAverage(42)), new ModifiedPriceVolumeTrend(3, new ConstantAverage(42)), new Pvt(3, new ConstantAverage(42)), new VolumePriceTrend(3, new ConstantAverage(42)) })
        {
            var builtIn = (IBuiltInIndicator)indicator;
            var expected = BuiltInFormulaReferences.PriceVolumeTrendOutputs(bars, builtIn, bars.Select(_ => 42d).ToArray());
            var key = builtIn.BatchName == IndicatorName.ModifiedPriceVolumeTrend ? "Mpvt" : "Pvt";
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected[key], run[indicator].ToArray());
            foreach (var output in new[] { key, "Signal" })
            {
                using var armed = ComponentAverage.Arm((input, _) => { Assert.Equal(expected[key], input); return input.Select(_ => 42d).ToArray(); });
                using var context = new ComputeContext();
                using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), output), context);
                Assert.NotNull(raw); Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(expected[output], raw.Value.ToArray());
            }
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var modified in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var ordinary = new PriceVolumeTrendState(); using var adjusted = new ModifiedPriceVolumeTrendState();
            using var ordinaryControl = new PriceVolumeTrendState(); using var adjustedControl = new ModifiedPriceVolumeTrendState();
            IStreamingIndicatorState state = modified ? adjusted : ordinary, control = modified ? adjustedControl : ordinaryControl;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "Pvt", "PriceVolumeTrend", "VolumePriceTrend", "ModifiedPriceVolumeTrend" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.PriceVolumeTrendOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
