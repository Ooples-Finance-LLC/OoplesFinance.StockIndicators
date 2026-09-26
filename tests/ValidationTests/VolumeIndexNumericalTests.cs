using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeIndexNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void DirectExponentRoundingMatchesIndependentStepwiseReduction()
    {
        var factor = ReferenceFraction.FromDouble(Math.Pow(2, 512));
        foreach (var initial in new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1d, -1d, 0d })
        foreach (var denominator in new[] { 1, 3, 11 })
        {
            var original = ReferenceFraction.FromDouble(initial) / new ReferenceFraction(denominator);
            for (var power = 0; power < 16; power++)
            {
                var reduced = original; var scale = new ReferenceFraction(1);
                while (double.IsInfinity(reduced.ToDouble())) { reduced /= factor; scale *= factor; }
                var expected = ReferenceFraction.FromDouble(reduced.ToDouble()) * scale;
                Assert.Equal(0, expected.CompareTo(original.RoundExtendedBinary64()));
                original *= factor;
            }
        }
    }

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
        foreach (var positive in new[] { false, true })
        foreach (var initial in new[] { -1000, 0, 1, 1000 })
        {
            IMovingAverage average = kind switch { MovingAvgType.SimpleMovingAverage => new Sma(), MovingAvgType.WeightedMovingAverage => new Wma(), MovingAvgType.WildersSmoothingMethod => new Smma(), _ => new Ema() };
            IBuiltInIndicator indicator = positive ? new PositiveVolumeIndex(period, initial, average) : new NegativeVolumeIndex(period, initial, average);
            var bars = BarsOf(prices).Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, i % 3 == 0 ? scale : i % 3 == 1 ? scale / 2 : 0)).ToArray();
            var expected = BuiltInFormulaReferences.VolumeIndexOutputs(bars, indicator);
            var key = positive ? "Pvi" : "Nvi";
            var batch = positive ? Data(bars).CalculatePositiveVolumeIndex(kind, period, initial) : Data(bars).CalculateNegativeVolumeIndex(kind, period, initial);
            Assert.Equal(expected[key], batch.CustomValuesList); Assert.Equal(expected[key + "Signal"], batch.OutputValues[key + "Signal"]);
            using var context = new ComputeContext();
            foreach (var output in new[] { key, key + "Signal" })
            {
                using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), output), context);
                Assert.NotNull(arm); Assert.Equal(expected[output], arm.Value.ToArray());
                var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
                using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), output), context);
                Assert.NotNull(selectedArm); Assert.Equal(expected[output], selectedArm.Value.ToArray());
            }
            if (initial == 1000)
            {
                var core = new double[bars.Length]; var volumes = bars.Select(b => b.Volume).ToArray();
                if (positive) OoplesFinance.StockIndicators.Core.VolumeCore.PositiveVolumeIndex(prices, volumes, core);
                else OoplesFinance.StockIndicators.Core.VolumeCore.NegativeVolumeIndex(prices, volumes, core); Assert.Equal(expected[key], core);
            }
            using var ordinary = new NegativeVolumeIndexState(kind, period, initial); using var adjusted = new PositiveVolumeIndexState(kind, period, initial);
            IStreamingIndicatorState state = positive ? adjusted : ordinary;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        Assert.Equal(expected[key][i], actual.Value); Assert.Equal(expected[key + "Signal"][i], actual.Outputs![key + "Signal"]);
                    }
                }
            }
        }
        foreach (var positive in new[] { false, true })
        {
            var recovery = BarsOf(new[] { 1d, double.MaxValue, 1 }).Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, positive ? i + 1 : 3 - i)).ToArray();
            var recovered = positive ? Data(recovery).CalculatePositiveVolumeIndex(MovingAvgType.SimpleMovingAverage, 1) : Data(recovery).CalculateNegativeVolumeIndex(MovingAvgType.SimpleMovingAverage, 1);
            Assert.True(double.IsPositiveInfinity(recovered.CustomValuesList[1]));
            Assert.Equal(1000, recovered.CustomValuesList[2]);
            var zero = BarsOf(new[] { 1d, 0, 2 }).Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, positive ? i + 1 : 3 - i)).ToArray();
            var zeroLine = positive ? Data(zero).CalculatePositiveVolumeIndex() : Data(zero).CalculateNegativeVolumeIndex();
            Assert.Equal(new[] { 1000d, 0, 0 }, zeroLine.CustomValuesList);
        }
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
        foreach (var indicator in new IIndicator[] { new NegativeVolumeIndex(3, maType: new ConstantAverage(42)), new PositiveVolumeIndex(3, maType: new ConstantAverage(42)), new Nvi(3, new ConstantAverage(42)), new Pvi(3, new ConstantAverage(42)) })
        {
            var builtIn = (IBuiltInIndicator)indicator;
            var expected = BuiltInFormulaReferences.VolumeIndexOutputs(bars, builtIn, bars.Select(_ => 42d).ToArray());
            var key = builtIn.BatchName == IndicatorName.PositiveVolumeIndex ? "Pvi" : "Nvi";
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected[key], run[indicator].ToArray());
            foreach (var output in new[] { key, key + "Signal" })
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
        foreach (var positive in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var ordinary = new NegativeVolumeIndexState(); using var adjusted = new PositiveVolumeIndexState();
            using var ordinaryControl = new NegativeVolumeIndexState(); using var adjustedControl = new PositiveVolumeIndexState();
            IStreamingIndicatorState state = positive ? adjusted : ordinary, control = positive ? adjustedControl : ordinaryControl;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "Nvi", "Pvi", "NegativeVolumeIndex", "PositiveVolumeIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.VolumeIndexOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
