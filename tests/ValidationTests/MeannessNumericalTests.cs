using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MeannessNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void PartialMediansPaddedPairsAndSmoothingMatchIndependentFormulas()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 5, 3, 8, -3, 1, -5, 0, 0, 0, 0 },
            new[] { double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, -1, 0 },
            new[] { double.MaxValue / 2, double.MaxValue, double.MaxValue / 4, double.MaxValue * .75 },
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 } })
        foreach (var period in new[] { 1, 2, 7, 11 })
        foreach (var average in new IMovingAverage?[] { null, new Sma(), new Ema(), new Wma(), new Smma() })
        {
            IBuiltInIndicator indicator = new MarketMeannessIndex(period, average);
            var kind = ((MarketMeannessIndexSpecOptions)indicator.CreateOptions()).MaType;
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.MeannessOutputs(bars, indicator);
            var batch = Data(bars).CalculateMarketMeannessIndex(kind, period);
            foreach (var key in new[] { "Mmi", "MmiSmoothed" })
            {
                Assert.Equal(expected[key], batch.OutputValues[key]);
                using var context = new ComputeContext();
                using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
                var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
                using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(selectedArm); Assert.Equal(expected[key], selectedArm.Value.ToArray());
                Assert.Equal(expected[key], selected.CalculateMarketMeannessIndex(kind, period).OutputValues[key]);
            }
            using var state = new MarketMeannessIndexState(kind, period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -91d })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var key in new[] { "Mmi", "MmiSmoothed" }) Assert.Equal(expected[key][i], actual.Outputs![key]);
                    }
                }
            }
            Assert.All(expected["Mmi"], v => Assert.InRange(v, 0, 100));
        }
        var equalHuge = Data(BarsOf(new[] { double.MaxValue, double.MaxValue }));
        Assert.Equal(new[] { 0d, 0 }, equalHuge.CalculateMarketMeannessIndex(length: 2).CustomValuesList);
        var descending = Data(BarsOf(new[] { 3d, 1, 2 }));
        Assert.Equal(new[] { 0d, 100, 50 }, descending.CalculateMarketMeannessIndex(length: 3).CustomValuesList);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerAveragesReceivePricesThenTheRawCount()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 });
        var indicator = new MarketMeannessIndex(3, new ConstantAverage(42), new ConstantAverage(7)); var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.MeannessOutputs(bars, builtIn, bars.Select(_ => 7d).ToArray());
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected["Mmi"], run[indicator].ToArray());
        foreach (var key in new[] { "Mmi", "MmiSmoothed" })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, _) => { Assert.Equal(bars.Select(b => b.Close), input); return input.Select(_ => 42d).ToArray(); },
                (input, _) => { Assert.Equal(expected["Mmi"], input); return input.Select(_ => 7d).ToArray(); }
            });
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(raw); Assert.Equal(2, ComponentAverage.Substitutions); Assert.Equal(expected[key], raw.Value.ToArray());
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new MarketMeannessIndexState(MovingAvgType.WeightedMovingAverage, 3); using var control = new MarketMeannessIndexState(MovingAvgType.WeightedMovingAverage, 3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "MarketMeannessIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.MeannessOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
