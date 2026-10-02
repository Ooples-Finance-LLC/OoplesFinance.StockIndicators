using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class StationaryLevelsNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RWI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(StationaryExtrapolatedLevels)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentProduct(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.StationaryLevelsOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static IndicatorCompute.ExtrapolatedLevelSeries Series(string key) => key switch
    { "UpperBand" => IndicatorCompute.ExtrapolatedLevelSeries.Upper, "MiddleBand" => IndicatorCompute.ExtrapolatedLevelSeries.Middle, "LowerBand" => IndicatorCompute.ExtrapolatedLevelSeries.Lower, _ => IndicatorCompute.ExtrapolatedLevelSeries.Deviation };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.StationaryLevelsValues(bars, length, kind); var batch = Data(bars).CalculateStationaryExtrapolatedLevels(kind, length);
        foreach (var entry in expected.Outputs)
        {
            Assert.Equal(entry.Value, batch.OutputValues[entry.Key]); using var context = new ComputeContext();
            using var fast = IndicatorCompute.ComputeStationaryExtrapolatedLevelsFast(Data(bars), context, length, kind, Series(entry.Key)); Assert.Equal(entry.Value, fast.ToArray());
        }
        Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var state = new StationaryExtrapolatedLevelsState(kind, length); using var direct = new StationaryLevelsWindow(kind, length);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var alternate = StrengthWindow.Supports(kind) ? -double.MaxValue : -9d; state.Update(Native(Bars(alternate)[0]), false, false); direct.Next(alternate, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var raw = direct.Next(bars[i].Close, final);
                    foreach (var entry in expected.Outputs) Assert.Equal(entry.Value[i], point.Outputs![entry.Key]);
                    Assert.Equal(expected.Outputs["Deviation"][i], point.Value); Assert.Equal(point.Value, raw.Deviation); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected;
    }
    private static Bar[] Hand() => Bars(2, 4, 2, 8, 1, 7, 3, 5);
    [Fact]
    public void IndependentLagProjectionAndTwoWindowHands()
    {
        var result = Check(Hand());
        Assert.Equal(new[] { 2d, 1, -1, 3, -3.5, 3, -2, 1 }, result.Outputs["Deviation"]);
        Assert.Equal(new[] { 0d, 0, 0, 1.5, 1.5, 2.5, 2.5, 2.5 }, result.Outputs["UpperBand"]);
        Assert.Equal(new[] { 0d, 0, 0, 0, -2, -2, -3, -3 }, result.Outputs["LowerBand"]);
        Assert.Equal(new[] { 0d, 0, 0, .75, -.25, .25, -.25, -.25 }, result.Outputs["MiddleBand"]);
    }
    [Fact]
    public void OpposingOverflowingBandsHaveFiniteMidpoint()
    {
        var m = double.MaxValue; var result = Check(Bars(m, -m, -m, m, m, -m));
        Assert.True(double.IsPositiveInfinity(result.Outputs["UpperBand"][5])); Assert.True(double.IsNegativeInfinity(result.Outputs["LowerBand"][5])); Assert.Equal(0, result.Outputs["MiddleBand"][5]);
    }
    [Fact]
    public void UnpublishedSubnormalDifferencePreventsFalseLagTie()
    {
        var result = Check(Bars(0, 1, double.Epsilon, 1, 0, 0, 0));
        Assert.Equal(.25, result.Outputs["UpperBand"][6]);
        var exactTie = Check(Bars(0, 1, 0, 1, 0, 0, 0)); Assert.Equal(0, exactTie.Outputs["UpperBand"][6]);
    }
    [Fact]
    public void ExactIntermediatesRetainExtremeAndSubnormalData()
    {
        foreach (var kind in Kinds)
        {
            Check(Bars(Enumerable.Range(0, 13).Select(i => (i % 3 - 1) * double.MaxValue).ToArray()), 3, kind);
            Check(Bars(Enumerable.Range(0, 13).Select(i => (i % 7 - 3) * double.Epsilon).ToArray()), 3, kind);
            Check(Bars(1, Math.BitIncrement(1), Math.BitDecrement(1), 1, Math.BitIncrement(1), 1, Math.BitDecrement(1)), 2, kind);
        }
    }
    [Fact]
    public void ExpiryAndRecursiveTransitionsPreservePreviewReset()
    { foreach (var kind in Kinds) Check(Bars(Enumerable.Range(0, 43).Select(i => (double)(i % 3 == 0 ? i % 7 : -i % 11)).ToArray()), 4, kind); }
    [Fact]
    public void ExtremePeriodsUseLongWidthsAndLazyHistory()
    { foreach (var kind in Kinds) foreach (var length in new[] { 1, int.MaxValue / 2 + 1, int.MaxValue }) Check(Hand(), length, kind); }
    [Fact]
    public void LegacyCompositeSmootherRemainsConsistent()
    { Check(Hand(), 3, MovingAvgType.DoubleExponentialMovingAverage); }
    [Fact]
    public void ExplicitFastSelectedInputIsPreserved()
    {
        var bars = Bars(Enumerable.Range(0, 39).Select(i => 20d + i % 7).ToArray()); var selected = bars.Select((_, i) => 1d + i % 11).ToArray();
        var expected = BuiltInFormulaReferences.StationaryLevelsValues(Bars(selected), 3, MovingAvgType.SimpleMovingAverage);
        foreach (var entry in expected.Outputs)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeStationaryExtrapolatedLevelsFast(data, context, 3, series: Series(entry.Key)); Assert.Equal(entry.Value, actual.ToArray()); Assert.Equal(selected, data.ChainedValues);
        }
    }
    [Fact]
    public void CallbackConsumesOneSelectedPriceStage()
    {
        var bars = Hand(); var expected = BuiltInFormulaReferences.StationaryLevelsValues(bars, 2, MovingAvgType.SimpleMovingAverage);
        foreach (var entry in expected.Outputs)
        {
            var count = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
            { count++; Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Close), values); return new[] { 0d, 3, 3, 5, 4.5, 4, 5, 4 }; };
            using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext(); var data = Data(bars);
            using var output = IndicatorCompute.ComputeStationaryExtrapolatedLevelsFast(data, context, 2, series: Series(entry.Key)); Assert.Equal(entry.Value, output.ToArray()); Assert.Equal(1, count); Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public async Task GeneratedApiRetainsItsAverageSlotAndOutputOrder()
    {
        var indicator = new StationaryExtrapolatedLevels(2, new Wma(2));
        using var run = await new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(Hand())).ConfigureIndicators(indicator).BuildAsync();
        var expected = BuiltInFormulaReferences.StationaryLevelsValues(Hand(), 2, MovingAvgType.WeightedMovingAverage).Outputs;
        var keys = new[] { "UpperBand", "MiddleBand", "LowerBand", "Deviation" }; Assert.Equal(4, indicator.Outputs.Count);
        for (var i = 0; i < keys.Length; i++) Assert.Equal(expected[keys[i]], run[indicator.Outputs[i]].ToArray());
    }
    [Fact]
    public void BatchDoesNotConsumeFastSlots()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Batch callback");
        using var armed = ComponentAverage.Arm(new[] { callback });
        Assert.Equal(new[] { 0d, 0, 0, .75, -.25, .25, -.25, -.25 }, Data(Hand()).CalculateStationaryExtrapolatedLevels(length: 2).OutputValues["MiddleBand"]); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void NonfiniteCandlesCannotAdvanceEitherLagOrExtrema()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new StationaryExtrapolatedLevelsState(length: 2); using var control = new StationaryExtrapolatedLevelsState(length: 2);
            foreach (var b in Hand()) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 4, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Hand())
            { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); foreach (var entry in expected.Outputs!) Assert.Equal(entry.Value, actual.Outputs![entry.Key]); }
        }
    }
}
