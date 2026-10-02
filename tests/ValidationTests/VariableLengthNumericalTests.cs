using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VariableLengthNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TAI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(VariableLengthMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAdaptivePeriods(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.VariableLengthOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var o = (VariableLengthMovingAverageSpecOptions)indicator.CreateOptions();
        var bars = Bars(Enumerable.Range(0, 64).Select(i => 20d + i % 5).ToArray()); var selected = bars.Select((_, i) => i % 7 - 3d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.VariableLengthOutputs(projected, indicator);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var pair in expected)
        { using var actual = IndicatorCompute.ComputeVariableLengthMovingAverageFast(data, context, o.Length, o.MaxLength, o.MaType, outputKey: pair.Key); Assert.Equal(pair.Value, actual.ToArray()); }
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateVariableLengthMovingAverage(o.MaType, o.Length, o.MaxLength);
        Assert.Equal(expected["Vlma"], batch.CustomValuesList); Assert.Equal(expected["Length"], batch.OutputValues["Length"]); Assert.Equal(bars.Select(b => b.High), batch.HighPrices); Assert.Equal(bars.Select(b => b.Low), batch.LowPrices); Assert.Equal(bars.Select(b => b.Close), batch.ClosePrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int minimum = 2, int maximum = 4, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.VariableLengthValues(bars, minimum, maximum, kind); var data = Data(bars).CalculateVariableLengthMovingAverage(kind, minimum, maximum);
        foreach (var pair in expected.Outputs) { Assert.Equal(pair.Value, data.OutputValues[pair.Key]); using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeVariableLengthMovingAverageFast(Data(bars), context, minimum, maximum, kind, pair.Key); Assert.Equal(pair.Value, actual.ToArray()); }
        var core = Enumerable.Repeat(42d, bars.Length + 1).ToArray(); MovingAverageCore.VariableLengthMovingAverage(bars.Select(b => b.Close).ToArray(), core, minimum, maximum, kind); Assert.Equal(expected.Outputs["Vlma"], core.Take(bars.Length)); Assert.Equal(42, core[^1]);
        Assert.Equal(expected.Signals, data.SignalsList); using var native = new VariableLengthMovingAverageState(kind, minimum, maximum); using var raw = new VariableLengthWindow(kind, minimum, maximum);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(1, -2, 3, -4, 5)) { native.Update(Native(b), true, true); raw.Next(b.Close, true); } native.Reset(); raw.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(-17)[0]), false, false); raw.Next(-17, false);
                foreach (var final in new[] { false, false, true }) { var b = bars[i]; var point = native.Update(Native(b), final, true); var direct = raw.Next(b.Close, final); foreach (var pair in expected.Outputs) Assert.Equal(pair.Value[i], point.Outputs![pair.Key]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Outputs["Length"][i], direct.Length); Assert.Equal(expected.Signals[i], direct.Trade); }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentFeedbackHandsAndEveryAverageKind()
    {
        Assert.Equal(1, ((IPrimaryOutputIndicator)new VariableLengthMovingAverage()).PrimaryOutput.Slot);
        var result = Check(Bars(1, 3, 2, 4), 2, 2);
        Assert.Equal(new[] { 2d, 2, 2, 2 }, result.Outputs["Length"]); Assert.Equal(new[] { 1d, 7d / 3, 19d / 9, 91d / 27 }, result.Outputs["Vlma"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell, Signal.StrongBuy }, result.Signals);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod, MovingAvgType.DoubleExponentialMovingAverage }) { Check(Bars(1, 3, 2, 4, -2, 8, 1, 1, 1, 1, 0, 3), 2, 4, kind); Check(Bars(Enumerable.Range(0, 40).Select(i => i % 17 == 0 ? 100d : i % 7 - 3d).ToArray()), 3, 8, kind); }
    }
    [Fact]
    public void ExactInnerOuterTiesAndNeighboringMeansChoosePeriods()
    {
        double[] Periods(double[] prices, double[] means)
        {
            var expected = BuiltInFormulaReferences.VariableLengthValues(Bars(prices), 2, 4, MovingAvgType.SimpleMovingAverage, means); using var raw = new VariableLengthWindow(MovingAvgType.SimpleMovingAverage, 2, 4);
            for (var i = 0; i < prices.Length; i++) { var point = raw.Next(prices[i], true, means[i]); Assert.Equal(expected.Outputs["Length"][i], point.Length); Assert.Equal(expected.Outputs["Vlma"][i], point.Value); Assert.Equal(expected.Signals[i], point.Trade); }
            using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (_, _) => means })) { var data = Data(Bars(prices)).CalculateVariableLengthMovingAverage(minLength: 2, maxLength: 4); Assert.Equal(expected.Outputs["Length"], data.OutputValues["Length"]); Assert.Equal(expected.Outputs["Vlma"], data.CustomValuesList); }
            return expected.Outputs["Length"];
        }
        Assert.Equal(4, Periods(new[] { -1d, -1, 1, 1 }, new[] { 0d, 0, 0, -.75 })[^1]);
        Assert.Equal(3, Periods(new[] { -1d, -1, 1, 1 }, new[] { 0d, 0, 0, Math.BitDecrement(-.75) })[^1]);
        Assert.Equal(new[] { 4d, 4, 4, 3, 4 }, Periods(new[] { -1d, -1, 1, 1, -1 }, new[] { 0d, 0, 0, -1, -1.25 }));
        Assert.Equal(new[] { 4d, 4, 4, 3, 3 }, Periods(new[] { -1d, -1, 1, 1, -1 }, new[] { 0d, 0, 0, -1, Math.BitDecrement(-1.25) }));
    }
    [Fact]
    public void OverflowingVarianceAndSubnormalDecisionsRemainDefined()
    {
        var m = double.MaxValue; var e = double.Epsilon; Check(Bars(m, -m, m, 0, -m, m, 1, 2, -3, 4));
        var normal = Check(Bars(1, 3, 2, 4, -2, 8, 1, 1, 1, 1, 0, 3)); var tiny = Check(Bars(e, 3 * e, 2 * e, 4 * e, -2 * e, 8 * e, e, e, e, e, 0, 3 * e)); Assert.Equal(normal.Outputs["Length"], tiny.Outputs["Length"]); Assert.Equal(normal.Signals, tiny.Signals);
        foreach (var kind in new[] { MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) Check(Bars(m, -m, m, 0, -m, m), 2, 3, kind);
    }
    [Fact]
    public void ExtremeBoundsWarmupAndExpiryUseGrowingHistories()
    {
        foreach (var bounds in new[] { (int.MinValue, 0), (1, 1), (2, 3), (7, 2), (1, int.MaxValue), (int.MaxValue, int.MaxValue) }) Check(Bars(1, -2, 3, -4, 5, 0, 1, 2, 3, 4, 5, 6), bounds.Item1, bounds.Item2);
        Assert.All(Check(Bars(2, 2, 2, 2, 2, 2)).Outputs["Length"], x => Assert.Equal(4, x)); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void CallbackDiscoveryShortMeansAndCallerPricesArePreserved()
    {
        var prices = new[] { -1d, -1, 1, 1, -1 }; var means = new[] { 0d, 0, 0, -1 }; var expected = BuiltInFormulaReferences.VariableLengthValues(Bars(prices), 2, 4, MovingAvgType.SimpleMovingAverage, means);
        foreach (var mode in new[] { "batch", "Vlma", "Length" })
        {
            var data = Data(Bars(9, 9, 9, 9, 9)); data.SetCustomValues(prices.ToList()); using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (values, length) => { Assert.Equal(prices, values); Assert.Equal(4, length); return means; } }))
            {
                if (mode == "batch") { data.CalculateVariableLengthMovingAverage(minLength: 2, maxLength: 4); Assert.Equal(expected.Outputs["Vlma"], data.CustomValuesList); Assert.Equal(expected.Outputs["Length"], data.OutputValues["Length"]); }
                else { using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeVariableLengthMovingAverageFast(data, context, 2, 4, outputKey: mode); Assert.Equal(expected.Outputs[mode], actual.ToArray()); Assert.Equal(prices, data.ChainedValues); }
                Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions); Assert.All(data.ClosePrices, x => Assert.Equal(9, x));
            }
        }
        using (ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>())) { Data(Bars(prices)).CalculateVariableLengthMovingAverage(); Assert.Equal(1, ComponentAverage.Requests); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMeansMomentsOrPeriod()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var actual = new VariableLengthMovingAverageState(minLength: 2, maxLength: 3); using var expected = new VariableLengthMovingAverageState(minLength: 2, maxLength: 3); actual.Update(Native(Bars(1)[0]), true, true); expected.Update(Native(Bars(1)[0]), true, true); var v = new[] { 2d, 2, 2, 2, 1 }; v[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(3, 0, 4, 2)) { var a = actual.Update(Native(b), true, true); var e = expected.Update(Native(b), true, true); Assert.Equal(e.Value, a.Value); Assert.Equal(e.Outputs!["Length"], a.Outputs!["Length"]); }
        }
    }
    [Fact]
    public void CoreAndRegistryAreCausalAndMatchMappedPublicBounds()
    {
        var prices = new[] { 1d, 3, 2, 4, -2, 8, 1, 1, 1, 1, 0, 3 };
        foreach (var length in new[] { int.MinValue, 1, 2, 3, int.MaxValue })
        {
            var minimum = Math.Max(1, length); var maximum = (int)Math.Min(int.MaxValue, 2L * minimum); var expected = BuiltInFormulaReferences.VariableLengthValues(Bars(prices), minimum, maximum, MovingAvgType.SimpleMovingAverage).Outputs["Vlma"];
            var result = new double[prices.Length]; MovingAverageCore.VariableLengthMovingAverage(prices, result, length); Assert.Equal(expected, result);
            new VlmaCore().Compute(prices, result, length); Assert.Equal(expected, result);
            var appended = prices.Concat(new[] { double.MaxValue, -double.MaxValue, 0d }).ToArray(); var extended = new double[appended.Length]; MovingAverageCore.VariableLengthMovingAverage(appended, extended, length); Assert.Equal(expected, extended.Take(prices.Length));
            for (var count = 0; count <= prices.Length; count++) { var prefix = new double[count]; MovingAverageCore.VariableLengthMovingAverage(prices.AsSpan(0, count), prefix, length); Assert.Equal(expected.Take(count), prefix); }
        }
    }
    [Fact]
    public void CoreSpanValidationPreservesOutputsAndSupportsInPlace()
    {
        var prices = new[] { 1d, 3, 2, 4, -2, 8, 1, 1 }; var output = Enumerable.Repeat(42d, prices.Length + 1).ToArray();
        Assert.Throws<ArgumentException>(() => MovingAverageCore.VariableLengthMovingAverage(prices, new double[1], 2, 4));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) { Assert.Throws<ArgumentOutOfRangeException>(() => MovingAverageCore.VariableLengthMovingAverage(new[] { 1d, bad }, output, 2, 4)); Assert.All(output, v => Assert.Equal(42, v)); }
        var expected = BuiltInFormulaReferences.VariableLengthValues(Bars(prices), 2, 4, MovingAvgType.SimpleMovingAverage).Outputs["Vlma"]; var inPlace = prices.ToArray(); MovingAverageCore.VariableLengthMovingAverage(inPlace, inPlace, 2, 4); Assert.Equal(expected, inPlace);
        MovingAverageCore.VariableLengthMovingAverage(prices, output, 2, 4); Assert.Equal(expected, output.Take(prices.Length)); Assert.Equal(42, output[^1]);
    }

}
