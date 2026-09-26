using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MoneyFlowAccumulationNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactMoneyFlowProductsPreserveWideRangesAndBothAverages()
    {
        var regular = BarsOf(new[] { 2d, 5, 5, 3, -3, 8, 1, -5, 0, 0 }).Select((b, i) =>
            new Bar(b.Time, b.Open, Math.Max(10, b.High), Math.Min(-10, b.Low), b.Close, i % 3 == 0 ? -i : i + 1)).ToArray();
        var huge = new[] {
            new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, double.MaxValue / 2, double.MaxValue),
            new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, double.MaxValue, -double.MaxValue, -double.MaxValue / 2, double.MaxValue),
            new Bar(DateTime.UnixEpoch.AddMinutes(2), 0, double.Epsilon, 0, double.Epsilon, double.MaxValue),
            new Bar(DateTime.UnixEpoch.AddMinutes(3), 0, double.Epsilon, 0, 0, double.MaxValue),
            new Bar(DateTime.UnixEpoch.AddMinutes(4), 0, 1, -1, 1, double.Epsilon),
            new Bar(DateTime.UnixEpoch.AddMinutes(5), 0, 0, 0, 0, double.MaxValue) };
        var overflow = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, -1, i < 4 ? 1 : -1, double.MaxValue)).ToArray();
        foreach (var bars in new[] { regular, huge, overflow })
        foreach (var period in new[] { 1, 3, 7 })
        foreach (var slow in new[] { 1, 4, 9 })
        foreach (var average in new IMovingAverage[] { new Sma(), new Ema(), new Wma(), new Smma() })
        foreach (var oscillator in new[] { false, true })
        {
            IBuiltInIndicator indicator = oscillator ? new ChaikinOscillator(period, slow, average) : new AccumulationDistributionLine(period, average);
            var expected = BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(bars, indicator);
            var kind = oscillator ? ((ChaikinOscillatorSpecOptions)indicator.CreateOptions()).MaType : ((AccumulationDistributionLineSpecOptions)indicator.CreateOptions()).MaType;
            var batch = Data(bars);
            if (oscillator) batch.CalculateChaikinOscillator(kind, period, slow); else batch.CalculateAccumulationDistributionLine(kind, period);
            foreach (var pair in expected) Assert.Equal(pair.Value, batch.OutputValues[pair.Key]);
            var prices = bars.Select(b => b.Close).ToArray();
            var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
            using var context = new ComputeContext();
            foreach (var pair in expected)
            {
                using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), pair.Key), context);
                Assert.NotNull(arm); Assert.Equal(pair.Value, arm.Value.ToArray());
                using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), pair.Key), context);
                Assert.NotNull(selectedArm); Assert.Equal(pair.Value, selectedArm.Value.ToArray());
            }
            if (oscillator) selected.CalculateChaikinOscillator(kind, period, slow); else selected.CalculateAccumulationDistributionLine(kind, period);
            foreach (var pair in expected) Assert.Equal(pair.Value, selected.OutputValues[pair.Key]);
            var core = new double[prices.Length]; var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray(); var volume = bars.Select(b => b.Volume).ToArray();
            if (oscillator) OoplesFinance.StockIndicators.Core.VolumeCore.ChaikinOscillator(high, low, prices, volume, core, period, slow, kind);
            else OoplesFinance.StockIndicators.Core.VolumeCore.AccumulationDistributionLine(high, low, prices, volume, core);
            Assert.Equal(expected.First().Value, core);
            IStreamingIndicatorState state = oscillator ? new ChaikinOscillatorState(kind, period, slow) : new AccumulationDistributionLineState(kind, period);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(regular[2]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
                    }
                }
            }
        }
        var expectedLine = BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(huge, new AccumulationDistributionLine(3))["Adl"];
        Assert.Equal(new[] { double.MaxValue / 2, 0, double.MaxValue, 0, double.Epsilon, double.Epsilon }, expectedLine);
        var cancelled = BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(overflow, new ChaikinOscillator(3, 3))["ChaikinOsc"];
        Assert.All(cancelled, v => Assert.Equal(0, v));
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerAveragesReceiveTheLineAndKeepTheirOrder()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 }).Select(b => new Bar(b.Time, b.Open, 10, -10, b.Close, 2)).ToArray();
        foreach (var oscillator in new[] { false, true })
        {
            IIndicator indicator = oscillator ? new ChaikinOscillator(3, 7, new ConstantAverage(42), new ConstantAverage(4)) : new AccumulationDistributionLine(3, new ConstantAverage(42));
            var builtIn = (IBuiltInIndicator)indicator;
            var expected = BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(bars, builtIn, bars.Select(_ => 42d).ToArray(), bars.Select(_ => 4d).ToArray());
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            var slot = 0;
            foreach (var pair in expected)
            {
                Assert.Equal(pair.Value, run[indicator.Outputs[slot++]].ToArray());
                var values = oscillator ? new[] { 42d, 4d } : new[] { 42d };
                var expectedInput = BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(bars, new AccumulationDistributionLine(3))["Adl"];
                var factories = values.Select(v => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, _) =>
                { Assert.Equal(expectedInput, input); return input.Select(_ => v).ToArray(); })).ToArray();
                using var armed = ComponentAverage.Arm(factories); using var context = new ComputeContext();
                using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), pair.Key), context);
                Assert.NotNull(raw); Assert.Equal(values.Length, ComponentAverage.Substitutions); Assert.Equal(pair.Value, raw.Value.ToArray());
            }
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var oscillator in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => oscillator ? new ChaikinOscillatorState(MovingAvgType.WeightedMovingAverage, 3, 7) : new AccumulationDistributionLineState(MovingAvgType.WeightedMovingAverage, 3);
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, 3, 2)); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "AccumulationDistributionLine", "ChaikinOscillator" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
