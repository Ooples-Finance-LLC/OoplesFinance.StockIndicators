using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeWeightedNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void RelativeVolumeWeightsPreservePreviewResetAndExtendedRange()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 1063 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, double.Epsilon, -double.Epsilon, 3 * double.Epsilon, 0, 0 }, Enumerable.Repeat(double.MaxValue, 90).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new VolumeWeightedMovingAverage(length);
            var expected = BuiltInFormulaReferences.VolumeWeightedOutputs(bars, indicator)["Vwma"];
            Assert.Equal(expected, Data(bars).CalculateVolumeWeightedMovingAverage(length: length).CustomValuesList);
            var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.MovingAverageCore.VolumeWeightedMovingAverage(prices, bars.Select(b => b.Volume).ToArray(), core, length); Assert.Equal(expected, core);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeVolumeWeightedMovingAverageFast(Data(bars), context, length); Assert.Equal(expected, raw.Span.ToArray());
            var state = new VolumeWeightedMovingAverageState(length: length);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var seed in BarsOf(new[] { 99d, -31, 7 })) state.Update(Native(seed), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }

    [Fact]
    public void RawAndBatchRespectSelectedInputBeforeCloses()
    {
        var original = BarsOf(new[] { 20d, 40, 30, 80 }); var selected = new[] { 1d, 3, -2, 4 };
        var expected = BuiltInFormulaReferences.VolumeWeightedOutputs(BarsOf(selected), new VolumeWeightedMovingAverage(3))["Vwma"];
        var data = Data(original); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeVolumeWeightedMovingAverageFast(data, context, 3);
        Assert.Equal(expected, raw.Span.ToArray());
        Assert.Equal(expected, data.CalculateVolumeWeightedMovingAverage(length: 3).CustomValuesList);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new VolumeWeightedMovingAverageState(length: 3);
            IStreamingIndicatorState control = new VolumeWeightedMovingAverageState(length: 3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    [Fact]
    public void CustomerAverageReceivesVolumesOnce()
    {
        var bars = new[] { 1d, 3, 2, 4 }.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (values, period) => {
            Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Volume), values); return values.Select(_ => 20d).ToArray(); } };
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch) Assert.Equal(new[] { .05, .1, .125, .15 }, Data(bars).CalculateVolumeWeightedMovingAverage(length: 2).CustomValuesList);
            else { using var actual = IndicatorCompute.ComputeVolumeWeightedMovingAverageFast(Data(bars), context, 2); Assert.Equal(new[] { .05, .1, .125, .15 }, actual.Span.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }

    [Fact]
    public void NativeMaximumPeriodDoesNotAllocateUnobservedHistory()
    {
        using var state = new VolumeWeightedMovingAverageState(length: int.MaxValue);
        foreach (var bar in BarsOf(new[] { 1d, 2, 3 })) Assert.Equal(0, state.Update(Native(bar), true, true).Value);
        state.Reset();
    }


    [Fact]
    public void EverySupportedVolumeAveragePreservesResetAndPreview()
    {
        var bars = new[] { 2d, 7, -1, 5, 3, 8, 4, -2 }.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 4 + 1)).ToArray();
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            IMovingAverage average = kind switch { MovingAvgType.SimpleMovingAverage => new Sma(), MovingAvgType.WeightedMovingAverage => new Wma(), MovingAvgType.WildersSmoothingMethod => new Smma(), _ => new Ema() };
            var expected = BuiltInFormulaReferences.VolumeWeightedOutputs(bars, new VolumeWeightedMovingAverage(3, maType: average))["Vwma"];
            Assert.Equal(expected, Data(bars).CalculateVolumeWeightedMovingAverage(kind, 3).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeVolumeWeightedMovingAverageFast(Data(bars), context, 3, maType: kind);
            Assert.Equal(expected, raw.Span.ToArray());
            using var state = new VolumeWeightedMovingAverageState(kind, 3);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var bar in bars) state.Update(Native(bar), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(bars[(i + 3) % bars.Length]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolumeWeightedMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.VolumeWeightedOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
