using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TradingAgreementNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TRADING", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Bars(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TradingMadeMoreSimplerOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStrengthAndStochasticAgreement(IndicatorValidationCase c, string route)
    {
        var options = (TradingMadeMoreSimplerOscillatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TradingAgreementOutputs(bars, options.Length), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length, MovingAvgType kind = MovingAvgType.WeightedMovingAverage, double threshold = 50, double limit = 0)
    {
        var referenceKind = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.TradingAgreementOutputs(bars, length, kind: referenceKind, threshold: threshold, limit: limit)["Tmmso"];
        Assert.Equal(expected, Data(bars).CalculateTradingMadeMoreSimplerOscillator(kind, length1: length, threshold: threshold, limit: limit).OutputValues["Tmmso"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeTradingMadeMoreSimplerOscillatorFast(Data(bars), context, length1: length, threshold: threshold, limit: limit, maType: kind);
        Assert.Equal(expected, raw.ToArray()); using var state = new TradingMadeMoreSimplerOscillatorState(kind, length1: length, threshold: threshold, limit: limit);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false);
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            }
        }
    }
    [Fact]
    public void HandAgreementRequiresAllThreeComponentsAndStrictThresholds()
    {
        var rising = Bars(new[] { 1d, 2, 3 }); var falling = Bars(new[] { 3d, 2, 1 });
        Assert.Equal(new[] { 0d, 0, 500d / 6 - 50 }, BuiltInFormulaReferences.TradingAgreementOutputs(rising, 3)["Tmmso"]);
        Assert.Equal(new[] { 0d, 50, 50 }, BuiltInFormulaReferences.TradingAgreementOutputs(falling, 3)["Tmmso"]);
        Check(rising, 3); Check(falling, 3);
        foreach (var threshold in new[] { 20d, 50, 80 }) foreach (var limit in new[] { -5d, 0, 10 }) Check(Bars(new[] { 2d, 5, 3, 8, 7, 1, 1, 9, 2 }), 3, threshold: threshold, limit: limit);
    }
    [Fact]
    public void ExtremeCandleRangesAndSmoothersPreserveFiniteAgreement()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 3, 14 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Bars(new[] { -scale, scale, 0, scale, -scale, 0, 0, scale, -scale, scale, scale, 0 }), length, kind);
            Check(Enumerable.Range(0, 24).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 0, double.MaxValue, -double.MaxValue, i % 3 == 0 ? -double.MaxValue : i % 3 == 1 ? double.MaxValue : 0, 1)).ToArray(), length, kind);
            Check(Enumerable.Range(0, 96).Select(i => { var close = 0.5 + 0.4 * Math.Sin(i * 0.37); return new Bar(DateTime.UnixEpoch.AddDays(i), close, 1, 0, close, 1); }).ToArray(), length, kind);
            Check(Array.Empty<Bar>(), length, kind);
        }
        var boundaryBars = Bars(new[] { 10d, 0, .11, .23, .31, .47, .59, .61, .73, .87, .93, .97, .13, .29 });
        var boundaries = BuiltInFormulaReferences.RoundedStochastic(boundaryBars, 8, 3, 1, 1)["FastD"];
        foreach (var threshold in boundaries.Where(v => v > 0 && v < 100).SelectMany(v => new[] { v, Math.BitIncrement(v), Math.BitDecrement(v) }).Distinct())
            Check(boundaryBars, 3, MovingAvgType.SimpleMovingAverage, threshold: threshold);
    }
    [Fact]
    public void RawSelectedClosesKeepOriginalExtrema()
    {
        var selected = new[] { 7d, -8, -7, 6, 9, 2, -9, 3, 1, -6, 8, 5 };
        var original = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddDays(i), 0, 10, -10, i / 2d, 1)).ToArray();
        var projected = original.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.TradingAgreementOutputs(projected, 3)["Tmmso"];
        var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeTradingMadeMoreSimplerOscillatorFast(data, context, length1: 3);
        Assert.Equal(expected, raw.ToArray());
        data = Data(original); data.SetCustomValues(selected.ToList());
        Assert.Equal(expected, data.CalculateTradingMadeMoreSimplerOscillator(length1: 3).OutputValues["Tmmso"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceAnyComponent()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new TradingMadeMoreSimplerOscillatorState(length1: 3); using var control = new TradingMadeMoreSimplerOscillatorState(length1: 3);
            var seed = Native(Bars(new[] { 2d })[0]); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, 4, 3, 0, 8, 2, 9, 3, 0 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
