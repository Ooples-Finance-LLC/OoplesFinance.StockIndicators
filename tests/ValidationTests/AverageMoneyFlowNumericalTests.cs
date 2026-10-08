using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AverageMoneyFlowNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(AverageMoneyFlowOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLogFlowRanges(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AverageMoneyFlowOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static double[] Check(Bar[] bars, int length = 3, int smoothLength = 2, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        var expected = BuiltInFormulaReferences.AverageMoneyFlowValues(bars, length, smoothLength, Kind(kind)); var batch = Data(bars).CalculateAverageMoneyFlowOscillator(kind, length, smoothLength);
        Assert.Equal(expected.Outputs["Amfo"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Amfo"], batch.OutputValues["Amfo"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeAverageMoneyFlowOscillatorFast(Data(bars), context, length, kind, smoothLength); Assert.Equal(expected.Outputs["Amfo"], output.ToArray());
        using var state = new AverageMoneyFlowOscillatorState(kind, length, smoothLength);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -4, 7, 1, -2, 8 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -91d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Amfo"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Amfo"]); } }
        }
        return expected.Outputs["Amfo"];
    }
    [Fact]
    public void ProductLogRetainsTinyHugeAndNearUnityInformation()
    {
        var ulp = Math.Pow(2, -52);
        foreach (var pair in new[] { (double.Epsilon, double.Epsilon), (double.MaxValue, double.MaxValue), (double.MaxValue, double.Epsilon), (1 + ulp, 1 - ulp), (1d, 1d), (2d, 3d) }) foreach (var volumeSign in new[] { -1, 1 }) foreach (var movementSign in new[] { -1, 1 })
        {
            var volume = pair.Item1 * volumeSign; var movement = pair.Item2 * movementSign; var expected = (ReferenceFraction.FromDouble(volume) * ReferenceFraction.FromDouble(movement)).Abs().LogToDouble() * movementSign;
            Assert.Equal(expected, AverageMoneyFlowWindow.LogFlow(new(volume), new(movement)));
        }
        Assert.Equal(-Math.Pow(2, -104), AverageMoneyFlowWindow.LogFlow(new(1 - ulp), new(1 + ulp)));
        Assert.Equal(0, AverageMoneyFlowWindow.LogFlow(new(0), new(double.MaxValue))); Assert.Equal(0, AverageMoneyFlowWindow.LogFlow(new(double.MaxValue), new(0)));
        var wide = new RocBankValue(double.MaxValue, 2); var reference = (ReferenceFraction.FromDouble(double.MaxValue) * new ReferenceFraction(4) * ReferenceFraction.FromDouble(double.MaxValue)).LogToDouble();
        Assert.Equal(reference, AverageMoneyFlowWindow.LogFlow(wide, new(double.MaxValue)));
    }
    [Fact]
    public void WidePricesSignedVolumesAndMeansKeepLogRangesFinite()
    {
        foreach (var priceScale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var volumeScale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) foreach (var kind in Kinds) foreach (var length in new[] { 2, 5 })
        {
            var bars = Enumerable.Range(0, 13).Select(i => { var p = (i % 7 - 3) * priceScale; var volume = (i % 3 + 1) * volumeScale * (i % 5 == 0 ? -1 : 1); return new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, volume); }).ToArray();
            Assert.All(Check(bars, length, 3, kind), value => Assert.InRange(value, -100, 100));
        }
    }
    [Fact]
    public void HandSeedsFlatRangesAndSmoothingOrderArePreserved()
    {
        var bars = new[] { 1d, 2, 4 }.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 100)).ToArray();
        Assert.Equal(new[] { -50d, 50d / 3, 200d / 3 }, Check(bars, 2, 3));
        Assert.Equal(new[] { -50d, -250d / 3, -100 }, Check(Bars(new[] { 1d, 1, 1 }), 2, 3));
        Assert.Equal(new[] { -100d, -100, -100 }, Check(bars, 1, 1));
    }
    [Fact]
    public void BothExtremePeriodsGrowOnlyWithObservedHistory()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var position in new[] { 0, 1 }) foreach (var kind in Kinds)
        { var length = position == 0 ? period : 3; var smooth = position == 1 ? period : 2; Check(Array.Empty<Bar>(), length, smooth, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, smooth, kind); }
    }
    [Fact]
    public void CustomCallbacksKeepVolumeChangeScaledOrderAndNearUnityRange()
    {
        var selected = new[] { -2d, 0, 4, 3 }; var volumes = new[] { 1d, 2, -3, 4 }; var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, volumes[i])).ToArray(); var ulp = Math.Pow(2, -52);
        var supplied = new[] { new[] { 1d, 1 - ulp, 1, 1 }, new[] { 0d, 1 + ulp, 1, 2 }, new[] { 7d, 8, 9, 10 } }; var effective = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.AverageMoneyFlowValues(effective, 2, 3, 2, supplied);
        Assert.Equal(new[] { -100d, -100, 100, 100 }, expected.Scaled);
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(calls == 2 ? 3 : 2, period); Assert.Equal(calls == 0 ? volumes : calls == 1 ? new[] { 0d, 2, 4, -1 } : expected.Scaled, values); return supplied[calls++]; };
            using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (fast) { using var output = IndicatorCompute.ComputeAverageMoneyFlowOscillatorFast(data, context, 2); Assert.Equal(expected.Outputs["Amfo"], output.ToArray()); }
            else { data.CalculateAverageMoneyFlowOscillator(length: 2); Assert.Equal(expected.Outputs["Amfo"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
            Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Requests); Assert.Equal(3, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void SelectedPricesRetainOriginalVolumesAndLegacyMeans()
    {
        var volumes = new[] { 2d, 4, -3, 8, 1, 0 }; var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var bars = Enumerable.Range(0, 6).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, volumes[i])).ToArray();
        var effective = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.AverageMoneyFlowValues(effective, 2, 3, 2); using var context = new ComputeContext();
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateAverageMoneyFlowOscillator(length: 2); Assert.Equal(expected.Outputs["Amfo"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        var source = Data(bars); source.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeAverageMoneyFlowOscillatorFast(source, context, 2); Assert.Equal(expected.Outputs["Amfo"], output.ToArray());
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var legacyBars = effective; var legacy = Data(legacyBars).CalculateAverageMoneyFlowOscillator(kind, 3, 2); using var fast = IndicatorCompute.ComputeAverageMoneyFlowOscillatorFast(Data(legacyBars), context, 3, kind, 2); Assert.Equal(legacy.CustomValuesList, fast.ToArray()); using var state = new AverageMoneyFlowOscillatorState(kind, 3, 2);
        for (var i = 0; i < legacyBars.Length; i++) Assert.Equal(legacy.CustomValuesList[i], state.Update(Native(legacyBars[i]), true, true).Value);
    }
    [Fact]
    public void CoreAndInPlaceSpansUseLogFlowRatherThanMoneyFlowIndex()
    {
        var bars = new[] { 1d, 2, 4, 3, -1, 8 }.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 10, p - 10, p, i + 1)).ToArray(); var prices = bars.Select(b => b.Close).ToArray(); var volumes = bars.Select(b => b.Volume).ToArray(); var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray();
        foreach (var length in new[] { 1, 2, 7, int.MaxValue })
        {
            var expected = BuiltInFormulaReferences.AverageMoneyFlowValues(bars, length, 3, 2).Outputs["Amfo"]; var output = new double[prices.Length]; OoplesFinance.StockIndicators.Core.OscillatorCore.AverageMoneyFlowOscillator(high, low, prices, volumes, output, length); Assert.Equal(expected, output);
            var inPlace = prices.ToArray(); OoplesFinance.StockIndicators.Core.OscillatorCore.AverageMoneyFlowOscillator(high, low, inPlace, volumes, inPlace, length); Assert.Equal(expected, inPlace);
        }
        OoplesFinance.StockIndicators.Core.OscillatorCore.AverageMoneyFlowOscillator(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OoplesFinance.StockIndicators.Core.OscillatorCore.AverageMoneyFlowOscillator(high, low, prices, volumes, Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => OoplesFinance.StockIndicators.Core.OscillatorCore.AverageMoneyFlowOscillator(high, low, prices, Array.Empty<double>(), new double[prices.Length]));
        Assert.Throws<ArgumentException>(() => OoplesFinance.StockIndicators.Core.OscillatorCore.AverageMoneyFlowOscillator(Array.Empty<double>(), low, prices, volumes, new double[prices.Length]));
        Assert.Throws<ArgumentException>(() => OoplesFinance.StockIndicators.Core.OscillatorCore.AverageMoneyFlowOscillator(high, Array.Empty<double>(), prices, volumes, new double[prices.Length]));
    }
    [Fact]
    public void ZeroVolumesAndExpiredExtremeChangesRecover()
    {
        var prices = new[] { -double.MaxValue, double.MaxValue, -double.MaxValue }.Concat(Enumerable.Repeat(0d, 12)).ToArray();
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i < 3 ? double.MaxValue : 0)).ToArray();
        foreach (var kind in Kinds) { var output = Check(bars, 2, 3, kind); Assert.All(output, value => Assert.InRange(value, -100, 100)); if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage) Assert.Equal(-100, output[^1]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceVolumeChangeOrLogRange()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new AverageMoneyFlowOscillatorState(length: 2); using var control = new AverageMoneyFlowOscillatorState(length: 2);
            foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
