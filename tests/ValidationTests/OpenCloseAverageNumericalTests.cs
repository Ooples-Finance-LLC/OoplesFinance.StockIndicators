using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class OpenCloseAverageNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), -p, Math.Abs(p), -Math.Abs(p), p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BODY", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void RoundedBodyAveragesRecoverAcrossExtremesAndPreserveOriginalOpens()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, 0, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 } })
        foreach (var period in new[] { 1, 2, 3, 7 })
        foreach (var weighted in new[] { false, true })
        foreach (var delta in new[] { false, true })
        {
            IMovingAverage? average = weighted ? new Wma(period) : null;
            IBuiltInIndicator indicator = delta ? new DeltaMovingAverage(period, 2, average) : new ChandeQuickStick(period, average);
            var kind = weighted ? MovingAvgType.WeightedMovingAverage : MovingAvgType.SimpleMovingAverage;
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.OpenCloseAverageOutputs(bars, indicator);
            var batch = Data(bars);
            if (delta) batch.CalculateDeltaMovingAverage(kind, period, 2); else batch.CalculateChandeQuickStick(kind, period);
            foreach (var pair in expected)
            {
                Assert.Equal(pair.Value, batch.OutputValues[pair.Key]);
                var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
                using var context = new ComputeContext();
                using var raw = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), pair.Key), context);
                Assert.NotNull(raw); Assert.Equal(pair.Value, raw.Value.ToArray());
                if (delta) selected.CalculateDeltaMovingAverage(kind, period, 2); else selected.CalculateChandeQuickStick(kind, period);
                Assert.Equal(pair.Value, selected.OutputValues[pair.Key]);
            }
            if (!delta && !weighted)
            {
                var core = new double[bars.Length];
                OoplesFinance.StockIndicators.Core.OscillatorCore.Qstick(bars.Select(b => b.Open).ToArray(), prices, core, period);
                Assert.Equal(expected["Cqs"], core);
                using var context = new ComputeContext(); var selected = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(_ => 42d), bars.Select(b => b.Volume), bars.Select(b => b.Time)); selected.CustomValuesList = prices.ToList();
                using var raw = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, new QstickSpecOptions(period), "Cqs"), context);
                Assert.NotNull(raw); Assert.Equal(expected["Cqs"], raw.Value.ToArray());
            }
            IStreamingIndicatorState state = delta ? new DeltaMovingAverageState(kind, period, 2) : new ChandeQuickStickState(kind, period);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(BarsOf(new[] { double.MaxValue })[0]), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
                    }
                }
            }
        }
        var cancellation = Data(BarsOf(new[] { double.MaxValue, -double.MaxValue, 0, 0 })).CalculateChandeQuickStick(length: 2).CustomValuesList;
        Assert.Equal(0, cancellation[0]); // The SMA publishes zero until its full window exists.
        Assert.True(double.IsPositiveInfinity(Data(BarsOf(new[] { double.MaxValue })).CalculateChandeQuickStick(length: 1).CustomValuesList[0])); Assert.Equal(0, cancellation[1]); Assert.Equal(-double.MaxValue, cancellation[2]); Assert.Equal(0, cancellation[3]);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerAverageConsumesDifferencesAndControlsSignal()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 });
        foreach (var delta in new[] { false, true })
        {
            IIndicator indicator = delta ? new DeltaMovingAverage(3, 2, new ConstantAverage(7)) : new ChandeQuickStick(3, new ConstantAverage(7));
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.All(run[indicator.Outputs[delta ? 1 : 0]].ToArray(), v => Assert.Equal(7, v));
            var builtIn = (IBuiltInIndicator)indicator;
            var changes = bars.Select((b, i) => b.Close - (delta ? i >= 2 ? bars[i - 2].Open : 0 : b.Open)).ToArray();
            foreach (var key in delta ? new[] { "Delta", "Signal", "Histogram" } : new[] { "Cqs" })
            {
                var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (values, period) =>
                { Assert.Equal(3, period); Assert.Equal(changes, values); return values.Select(_ => 7d).ToArray(); } };
                using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
                using var actual = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
                Assert.NotNull(actual); Assert.Equal(1, ComponentAverage.Substitutions);
                Assert.Equal(changes.Select(v => key == "Delta" ? v : key == "Histogram" ? v - 7 : 7).ToArray(), actual.Value.ToArray());
            }
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var delta in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => delta ? new DeltaMovingAverageState(length1: 3, length2: 2) : new ChandeQuickStickState(length: 3);
            var state = Create(); var control = Create(); using var life = state as IDisposable; using var controlLife = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BODY", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "Qstick", "ChandeQuickStick", "DeltaMovingAverage" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.OpenCloseAverageOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
