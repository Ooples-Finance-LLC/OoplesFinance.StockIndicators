using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AtrDerivedNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ATR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void ExtendedRangesScaleAndNormalizeBeforePublishing()
    {
        foreach (var length in new[] { 1, 2, 7 })
        foreach (var multiplier in new[] { 0d, .5, 2, double.MaxValue })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray(), length, multiplier);
        Check(Array.Empty<Bar>(), 2, 2);
        var extremes = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1)).ToArray();
        foreach (var multiplier in new[] { 0d, .25, 2 }) Check(extremes, 2, multiplier);
        Assert.Equal(100, Data(extremes).CalculateNormalizedAverageTrueRange(2).CustomValuesList[0]);
        Assert.Equal(double.MaxValue / 2, Data(extremes).CalculateAtrChannelWidth(2, .25).CustomValuesList[0]);
        var bar = new[] { new Bar(DateTime.UnixEpoch, 10, 11, 9, 10, 1) };
        Assert.Equal(10, Data(bar).CalculateNormalizedAverageTrueRange(2).CustomValuesList[0]);
        Assert.Equal(4, Data(bar).CalculateAtrChannelWidth(2).CustomValuesList[0]);
    }
    private static void Check(Bar[] bars, int length, double multiplier)
    {
        var expected = BuiltInFormulaReferences.AtrDerivedOutputs(bars, length, multiplier);
        foreach (var width in new[] { false, true })
        {
            var key = width ? "Acw" : "Natr"; var target = expected[key];
            Assert.Equal(target, (width ? Data(bars).CalculateAtrChannelWidth(length, multiplier) : Data(bars).CalculateNormalizedAverageTrueRange(length)).CustomValuesList);
            using var context = new ComputeContext(); using var raw = width ? IndicatorCompute.ComputeAtrChannelWidthFast(Data(bars), context, length, multiplier) : IndicatorCompute.ComputeNormalizedAtrFast(Data(bars), context, length);
            Assert.Equal(target, raw.Span.ToArray());
            var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray(); var close = bars.Select(b => b.Close).ToArray(); var core = new double[bars.Length];
            if (width) VolatilityCore.AtrChannelWidth(high, low, close, core, length, multiplier); else VolatilityCore.NormalizedAtr(high, low, close, core, length);
            Assert.Equal(target, core);
            IStreamingIndicatorState state = width ? new AtrChannelWidthState(length, multiplier) : new NormalizedAverageTrueRangeState(length);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true })
                {
                    var output = state.Update(Native(bars[i]), final, true); Assert.Equal(target[i], output.Value); Assert.Equal(target[i], output.Outputs![key]); Assert.Single(output.Outputs);
                }
            }
        }
    }
    [Fact]
    public void SelectedClosesControlBothGapsAndNormalization()
    {
        var highs = new[] { 10d, 5, 7, 4 }; var lows = new[] { -2d, -2, 0, 0 };
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 4, highs[i], lows[i], 4, 1)).ToArray();
        var prices = new[] { 9d, -1, 6, 2 }; var data = Data(bars); data.SetCustomValues(prices.ToList());
        var expected = BuiltInFormulaReferences.AtrDerivedOutputs(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray(), 2, 2);
        using var context = new ComputeContext();
        using var width = IndicatorCompute.ComputeAtrChannelWidthFast(data, context, 2); Assert.Equal(expected["Acw"], width.Span.ToArray());
        using var percent = IndicatorCompute.ComputeNormalizedAtrFast(data, context, 2); Assert.Equal(expected["Natr"], percent.Span.ToArray());
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceState()
    {
        foreach (var width in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = width ? new AtrChannelWidthState(2) : new NormalizedAverageTrueRangeState(2);
            IStreamingIndicatorState control = width ? new AtrChannelWidthState(2) : new NormalizedAverageTrueRangeState(2);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            var first = new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1); state.Update(Native(first), true, true); control.Update(Native(first), true, true);
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("ATR", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            Assert.Equal(control.Update(Native(first), true, true).Value, state.Update(Native(first), true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Natr) || c.IndicatorType == typeof(AtrPercent) || c.IndicatorType == typeof(AtrChannelWidth)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AtrDerivedOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
