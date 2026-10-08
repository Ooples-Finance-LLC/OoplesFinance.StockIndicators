using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AtrFilterNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ATR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(AtrFilteredEma) || c.IndicatorType == typeof(AtrFilteredExponentialMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentNormalizedRangeDeviation(IndicatorValidationCase c, string route)
    {
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions(); var settings = options is AtrFilteredExponentialMovingAverageSpecOptions full ? (full.Length, full.AtrLength, full.StdDevLength, full.LbLength, full.Min) : options is AtrFilteredEmaSpecOptions alias ? (alias.Length, 20, 10, 20, 5d) : throw new InvalidOperationException();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AtrFilterValues(bars, settings.Item1, settings.Item2, settings.Item3, settings.Item4, settings.Item5).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 7, int atrLength = 3, int deviationLength = 2, int lookback = 4, double cap = 5, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.AtrFilterValues(bars, length, atrLength, deviationLength, lookback, cap, Kind(kind)); var batch = Data(bars).CalculateAtrFilteredExponentialMovingAverage(kind, length, atrLength, deviationLength, lookback, cap); Assert.Equal(new[] { "Afp" }, batch.OutputValues.Keys); Assert.Equal(expected.Outputs["Afp"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeAtrFilteredExponentialMovingAverageFast(Data(bars), context, length, atrLength, deviationLength, lookback, cap, kind); Assert.Equal(expected.Outputs["Afp"], fast.ToArray());
        if (kind == MovingAvgType.SimpleMovingAverage) { var output = new double[bars.Length]; MovingAverageCore.AtrFilteredExponentialMovingAverage(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), output, length, atrLength, deviationLength, lookback, cap); Assert.Equal(expected.Outputs["Afp"], output); }
        using var state = new AtrFilteredExponentialMovingAverageState(kind, length, atrLength, deviationLength, lookback, cap);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 1d, 8, -2, 4, 3, 7 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Afp"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Afp"]); } }
        }
        return expected.Outputs["Afp"];
    }
    [Fact]
    public void WideAndSubnormalRangesRetainAdaptiveDeviation()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        { Check(Bars(Enumerable.Range(0, 43).Select(i => (i % 7 - 3d) / 4 * scale)), kind: kind); Check(Bars(Enumerable.Repeat(scale, 5).Concat(Enumerable.Repeat(0d, 17)).Concat(new[] { scale, -scale, 0d })), kind: kind); }
    }
    [Fact]
    public void UnpublishedNormalizedRangesAndSquaresExceedDoubleMaximum()
    {
        var ordinary = Enumerable.Range(0, 25).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 7, -10 - i % 3, i % 5 - 2, 1)).ToArray(); Assert.Equal(-1.75, Check(ordinary)[2]);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var bars = Enumerable.Range(0, 23).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue / (i % 3 + 1), -double.MaxValue, i % 3 == 0 ? 0 : i % 2 == 0 ? double.Epsilon : -double.Epsilon, 1)).ToArray(); Assert.All(Check(bars, kind: kind), v => Assert.True(double.IsFinite(v)));
        }
    }
    [Fact]
    public void EmptyZeroAndExtremePeriodsKeepLazyHistories()
    {
        foreach (var periods in new[] { (0, -1, int.MinValue, 0), (1, 1, 1, 1), (int.MaxValue, 3, 2, 4), (7, int.MaxValue, 2, 4), (7, 3, int.MaxValue, 4), (7, 3, 2, int.MaxValue) })
        { Check(Array.Empty<Bar>(), periods.Item1, periods.Item2, periods.Item3, periods.Item4); Check(Bars(new double[8]), periods.Item1, periods.Item2, periods.Item3, periods.Item4); Check(Bars(new[] { 1d, 3, 0, -2, 7, 5, 1, 2 }), periods.Item1, periods.Item2, periods.Item3, periods.Item4); }
    }
    [Fact]
    public void GainCapsRetainLongPeriodDriveAndNegativeExtrapolation()
    {
        var prices = Bars(new[] { 0d, 1, -2, 3, -4, 5 });
        foreach (var cap in new[] { -2d, 0, .1, 1, 5, double.MaxValue }) Check(prices, 3, 1, 1, 1, cap);
        var longPeriod = Check(Bars(new[] { 0d, 1 }), int.MaxValue, 1, 1, 1); Assert.Equal(Math.Pow(2, -30), longPeriod[1]);
        Check(Bars(new[] { 0d, 1, -2, 3 }), 1, 1, 1, 1, -double.MaxValue);
    }
    [Fact]
    public void HandExampleSeedsAtPriceAndRecoversAfterZeroDeviation()
    {
        var prices = Enumerable.Range(0, 18).Select(i => i % 2 == 0 ? 1d : 2d).ToArray(); var expected = new double[prices.Length]; for (var i = 0; i < prices.Length; i++) expected[i] = i < 3 ? 1 : (expected[i - 1] + prices[i]) / 2;
        var firstRange = Check(Bars(new[] { 1d, 4, 1.25, 3, 2, .5, 2 }), 3, 2, 2, 2); Assert.True(firstRange[2] > 1 && firstRange[2] < 1.1);
        Assert.Equal(expected, Check(Bars(prices), 3, 2, 2, 2)); Assert.All(Check(Bars(Enumerable.Repeat(double.MaxValue, 9)), 3, 2, 2, 2), v => Assert.Equal(double.MaxValue, v));
    }
    [Fact]
    public void PowerOfTwoScalingPreservesThePriceTrajectory()
    {
        var prices = Enumerable.Range(0, 39).Select(i => 2 + Math.Sin(i * .37)).ToArray(); var expected = Check(Bars(prices)); foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) Assert.Equal(expected.Select(v => v * scale), Check(Bars(prices.Select(v => v * scale))));
    }
    [Fact]
    public void SelectedPricesPreserveOriginalOrSyntheticRangesAndSignals()
    {
        var prices = new[] { .5, 8, -.25, -9, 1d, 6, -1, 2, 9, 0, 4, -2 }; var original = prices.Select((_, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 2, -2, 1, 1)).ToArray();
        var effective = prices.Select((price, i) => new Bar(original[i].Time, 0, price >= -2 && price <= 2 ? 2 : Math.Max(price, i == 0 ? price : prices[i - 1]), price >= -2 && price <= 2 ? -2 : Math.Min(price, i == 0 ? price : prices[i - 1]), price, 1)).ToArray(); var expected = BuiltInFormulaReferences.AtrFilterValues(effective, 5, 2, 2, 2);
        var data = Data(original); data.SetCustomValues(prices.ToList()); data.CalculateAtrFilteredExponentialMovingAverage(length: 5, atrLength: 2, stdDevLength: 2, lbLength: 2); Assert.Equal(expected.Outputs["Afp"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        var source = Data(original); source.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeAtrFilteredExponentialMovingAverageFast(source, context, 5, 2, 2, 2); Assert.Equal(expected.Outputs["Afp"], result.ToArray());
    }
    [Fact]
    public void BothCustomAveragesAreConsumedInOrderIncludingSecondMoment()
    {
        var bars = Enumerable.Range(0, 25).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 4, 0, i % 3 + 1, 1)).ToArray(); var normalized = bars.Select(b => 4 / b.Close).ToArray(); var first = bars.Select((_, i) => (double)(i % 3 + 1)).ToArray(); var second = bars.Select((_, i) => (i % 4 + 4) * 100d).ToArray(); var expected = BuiltInFormulaReferences.AtrFilterValues(bars, 5, 3, 2, 2, externalRanges: first, externalSquares: second);
        foreach (var route in new[] { "batch", "fast" })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> component = (values, period) => { var current = calls++; Assert.Equal(current == 0 ? 3 : 2, period); Assert.Equal(current == 0 ? normalized : first.Select(v => v * v).ToArray(), values); return current == 0 ? first : second; }; using var armed = ComponentAverage.Arm(new[] { component, component });
            if (route == "batch") { var result = Data(bars).CalculateAtrFilteredExponentialMovingAverage(length: 5, atrLength: 3, stdDevLength: 2, lbLength: 2); Assert.Equal(expected.Outputs["Afp"], result.CustomValuesList); Assert.Equal(expected.Signals, result.SignalsList); }
            else { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeAtrFilteredExponentialMovingAverageFast(Data(bars), context, 5, 3, 2, 2); Assert.Equal(expected.Outputs["Afp"], result.ToArray()); }
            Assert.Equal(2, calls); Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void OtherAverageKindsRetainTheirConfiguredMoments()
    {
        var bars = Bars(Enumerable.Range(0, 41).Select(i => 3 + Math.Sin(i * .27))); var expected = BuiltInFormulaReferences.AtrFilterValues(bars, 5, 3, 2, 2, kind: 4).Outputs["Afp"]; var batch = Data(bars).CalculateAtrFilteredExponentialMovingAverage(MovingAvgType.DoubleExponentialMovingAverage, 5, 3, 2, 2); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeAtrFilteredExponentialMovingAverageFast(Data(bars), context, 5, 3, 2, 2, maType: MovingAvgType.DoubleExponentialMovingAverage); using var state = new AtrFilteredExponentialMovingAverageState(MovingAvgType.DoubleExponentialMovingAverage, 5, 3, 2, 2); var budget = new IndicatorErrorBudget(1e-11, 1e-11);
        for (var i = 0; i < bars.Length; i++) { Assert.True(budget.Accepts(expected[i], batch.CustomValuesList[i])); Assert.True(budget.Accepts(expected[i], fast.Span[i])); Assert.True(budget.Accepts(expected[i], state.Update(Native(bars[i]), true, false).Value)); }
    }
    [Fact]
    public void InvalidCapPreservesCallerDataAndCoreOutput()
    {
        foreach (var cap in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateAtrFilteredExponentialMovingAverage(min: cap)); Assert.Equal(selected, data.CustomValuesList);
            Assert.Throws<ArgumentOutOfRangeException>(() => new AtrFilteredExponentialMovingAverageState(min: cap)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeAtrFilteredExponentialMovingAverageFast(data, context, min: cap)); Assert.Equal(selected, data.CustomValuesList);
            var output = new[] { 9d }; Assert.Throws<ArgumentOutOfRangeException>(() => MovingAverageCore.AtrFilteredExponentialMovingAverage(new[] { 1d }, new[] { 1d }, new[] { 1d }, output, min: cap)); Assert.Equal(9d, output[0]);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRangesMomentsOrFloor()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new AtrFilteredExponentialMovingAverageState(length: 5, atrLength: 2, stdDevLength: 2, lbLength: 2); using var control = new AtrFilteredExponentialMovingAverageState(length: 5, atrLength: 2, stdDevLength: 2, lbLength: 2); foreach (var bar in Bars(new[] { 1d, 3, 8, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
