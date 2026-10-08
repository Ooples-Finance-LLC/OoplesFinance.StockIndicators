using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KasePeakV1NumericalTests
{
    private static Bar B(double high, double low, double close, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), close, high, low, close, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KASE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(KasePeakOscillatorV1)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentPeakAndCenteredVariance(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.KasePeakV1Outputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsRetainFormulaRanges(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void SignTransitionsAndThresholdsHaveIndependentHands()
    {
        var bars = new[] { B(2, 0, 1), B(0, -2, -1, 1), B(-1, -3, -2, 2), B(2, 0, 1, 3), B(3, 1, 2, 4), B(4, 2, 3, 5) };
        var actual = Data(bars).CalculateKasePeakOscillatorV1(1, 1);
        Assert.Equal(new[] { 1d, -4d / 3, -1d, 1.5, 1, 1 }, actual.OutputValues["Pk"]);
        Assert.Equal(new[] { 2.08, 0, -1.92, 0, 2.08, 2.08 }, actual.OutputValues["Kpo"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.None, Signal.StrongSell, Signal.None, Signal.StrongBuy, Signal.Buy }, actual.SignalsList);
    }
    [Theory]
    [InlineData(double.Epsilon)] [InlineData(1d)] [InlineData(double.MaxValue / 8)]
    public void ExtremeRangesPreserveBandsPreviewAndReset(double scale)
    {
        var bars = new[] { B(8 * scale, -8 * scale, 0), B(6 * scale, -2 * scale, 5 * scale, 1),
            B(-scale, -7 * scale, -6 * scale, 2), B(7 * scale, 3 * scale, 4 * scale, 3),
            B(2 * scale, -5 * scale, -scale, 4), B(8 * scale, -2 * scale, 6 * scale, 5) };
        var expected = BuiltInFormulaReferences.KasePeakV1Outputs(bars, 2, 2);
        var actual = Data(bars).CalculateKasePeakOscillatorV1(2, 2);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], actual.OutputValues[key]);
        using var state = new KasePeakOscillatorV1State(2, 2);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(B(7, 3, 4)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(-1, -9, -3)), false, false);
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(B(double.NaN, 0, 0)), true, false));
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void NarrowCandlesKeepLargeStartupPeaksAndWindowExpiry()
    {
        var bars = Enumerable.Range(0, 12).Select(i => B(1, Math.BitDecrement(1d), 1, i)).ToArray();
        var expected = BuiltInFormulaReferences.KasePeakV1Outputs(bars, 3, 2);
        var actual = Data(bars).CalculateKasePeakOscillatorV1(3, 2);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], actual.OutputValues[key]);
        Assert.True(actual.OutputValues["Pk"][1] > 1e15);
        Assert.Equal(0, actual.OutputValues["Pk"][^1]);
    }
    [Fact]
    public void MaximumPeriodsAllocateOnlyObservedHistory()
    {
        var bars = new[] { B(2, 0, 1), B(4, 1, 3, 1), B(1, -2, -1, 2) };
        var expected = BuiltInFormulaReferences.KasePeakV1Outputs(bars, int.MaxValue, int.MaxValue);
        using var state = new KasePeakV1Window(int.MaxValue, int.MaxValue);
        var points = bars.Select(b => state.Next(b.High, b.Low, b.Close, true)).ToArray();
        Assert.Equal(expected["Kpo"], points.Select(p => p.Level)); Assert.Equal(expected["Pk"], points.Select(p => p.Peak));
    }
    [Fact]
    public void CorePreservesPublicLevelsAndOverlappingCandleSpans()
    {
        var bars = new[] { B(2, 0, 1), B(4, 1, 3, 1), B(1, -2, -1, 2), B(3, -1, 2, 3) };
        var expected = BuiltInFormulaReferences.KasePeakV1Outputs(bars, 2, 3)["Kpo"];
        foreach (var alias in new[] { 0, 1, 2 })
        {
            var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray(); var close = bars.Select(b => b.Close).ToArray();
            var output = alias == 0 ? high : alias == 1 ? low : close;
            OscillatorCore.KasePeakOscillatorV1(high, low, close, output, 2); Assert.Equal(expected, output);
        }
        var sentinel = new[] { 17d, 19d };
        Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.KasePeakOscillatorV1(new[] { 1d, double.NaN }, new[] { 0d, 0d }, new[] { 1d, 1d }, sentinel, 2));
        Assert.Equal(new[] { 17d, 19d }, sentinel);
    }
    [Fact]
    public void FastCallbacksKeepPeakAndMeanRequests()
    {
        var bars = new[] { B(2, 0, 1), B(4, 1, 3, 1), B(1, -2, -1, 2) };
        var periods = new List<int>();
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
        { periods.Add(period); return values.Select(_ => 3d).ToArray(); };
        using var scope = ComponentAverage.Arm(Enumerable.Repeat(callback, 8).ToArray());
        using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeKasePeakOscillatorV1Fast(Data(bars), context, 2, 1);
        Assert.Contains(1, periods); Assert.Contains(2, periods); Assert.True(ComponentAverage.Substitutions >= 2);
        Assert.Equal(bars.Length, result.Length);
    }
}
