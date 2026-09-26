using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class StochasticMomentumNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void DoubleSmoothedDistanceAndRangePreserveEveryPeriodAndCandle()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 5, 3, 8, -3, 1, -5, 0, 0, 0, 0 },
            new[] { double.MaxValue, double.MaxValue / 2, -double.MaxValue, double.MaxValue, 0, 1, -1, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 } })
        foreach (var rangePeriod in new[] { 1, 2, 5 })
        foreach (var firstPeriod in new[] { 1, 3 })
        foreach (var secondPeriod in new[] { 1, 2 })
        foreach (var signalPeriod in new[] { 2, 5 })
        foreach (var average in new IMovingAverage[] { new Sma(), new Ema(), new Wma(), new Smma() })
        {
            IBuiltInIndicator indicator = new StochasticMomentumIndex(rangePeriod, firstPeriod, secondPeriod, signalPeriod, average);
            var kind = ((StochasticMomentumIndexSpecOptions)indicator.CreateOptions()).MaType;
            var bars = BarsOf(prices).Select(b => new Bar(b.Time, b.Open, Math.Max(b.Close, 0), Math.Min(b.Close, 0), b.Close, 1)).ToArray();
            var expected = BuiltInFormulaReferences.StochasticMomentumOutputs(bars, indicator);
            var batch = Data(bars).CalculateStochasticMomentumIndex(kind, rangePeriod, firstPeriod, secondPeriod, signalPeriod);
            foreach (var key in new[] { "Smi", "Signal" })
            {
                Assert.Equal(expected[key], batch.OutputValues[key]);
                using var context = new ComputeContext();
                using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
                var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
                using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(selectedArm); Assert.Equal(expected[key], selectedArm.Value.ToArray());
                Assert.Equal(expected[key], selected.CalculateStochasticMomentumIndex(kind, rangePeriod, firstPeriod, secondPeriod, signalPeriod).OutputValues[key]);
            }
            if (kind == MovingAvgType.ExponentialMovingAverage)
            {
                var core = new double[bars.Length];
                OoplesFinance.StockIndicators.Core.OscillatorCore.StochasticMomentumIndex(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), prices, core, rangePeriod, firstPeriod, secondPeriod);
                Assert.Equal(expected["Smi"], core);
            }
            using var state = new StochasticMomentumIndexState(kind, rangePeriod, firstPeriod, secondPeriod, signalPeriod);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(new Bar(DateTime.UnixEpoch, 0, double.MaxValue, 0, 0, 1)), true, true);
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var key in new[] { "Smi", "Signal" }) Assert.Equal(expected[key][i], actual.Outputs![key]);
                    }
                }
            }
            Assert.All(expected.Values.SelectMany(v => v), v => Assert.InRange(v, -100, 100));
        }
        var flatExtreme = StochasticMomentumWindow.Components(double.MaxValue, double.MaxValue, double.MaxValue);
        Assert.Equal(0, flatExtreme.Distance.Publish()); Assert.Equal(0, flatExtreme.Range.Publish());
        Assert.Equal(100, StochasticMomentumWindow.Ratio(new(double.Epsilon), new(double.Epsilon)));
        Assert.Equal(0, StochasticMomentumWindow.Ratio(new(1), new(0)));
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerComponentsConsumeAllFiveStagesInOrder()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 }).Select(b => new Bar(b.Time, b.Open, b.Close + 2, b.Close - 1, b.Close, 1)).ToArray();
        var indicator = new StochasticMomentumIndex(1, 3, 2, 4, new ConstantAverage(1), new ConstantAverage(8), new ConstantAverage(2), new ConstantAverage(10), new ConstantAverage(7));
        var builtIn = (IBuiltInIndicator)indicator;
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.All(run[indicator].ToArray(), v => Assert.Equal(40, v));
        foreach (var key in new[] { "Smi", "Signal" })
        {
            var sources = new[] { -.5, 3, 1, 8, 40 }; var results = new[] { 1d, 8, 2, 10, 7 }; var periods = new[] { 3, 3, 2, 2, 4 };
            var callbacks = Enumerable.Range(0, 5).Select(stage => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, period) =>
            { Assert.Equal(periods[stage], period); Assert.All(input, v => Assert.Equal(sources[stage], v)); return input.Select(_ => results[stage]).ToArray(); })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(raw); Assert.Equal(5, ComponentAverage.Substitutions); Assert.All(raw.Value.ToArray(), v => Assert.Equal(key == "Signal" ? 7 : 40, v));
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new StochasticMomentumIndexState(MovingAvgType.WeightedMovingAverage, 3); using var control = new StochasticMomentumIndexState(MovingAvgType.WeightedMovingAverage, 3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "StochasticMomentumIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.StochasticMomentumOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
