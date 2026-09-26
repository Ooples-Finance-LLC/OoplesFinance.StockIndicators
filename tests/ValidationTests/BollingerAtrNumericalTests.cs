using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class BollingerAtrNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BBATR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void DeviationWidthPreservesSmallSpreadsAndExtendedRangeRatios()
    {
        foreach (var length in new[] { 1, 2, 7 }) foreach (var atrLength in new[] { 1, 3 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var multiplier in new[] { -2d, 0, .25, 2, double.MaxValue }) foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 10).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray(), atrLength, length, kind, multiplier);
        Check(Array.Empty<Bar>(), 2, 2, MovingAvgType.SimpleMovingAverage, 2);
        var prices = new[] { 4503599627370496d, 4503599627370497d };
        var tinySpread = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        Check(tinySpread, 2, 2, MovingAvgType.SimpleMovingAverage, .25);
        Assert.Equal(2, Data(tinySpread).CalculateBollingerBandsAvgTrueRange(atrLength: 2, length: 2, stdDevMult: .25).CustomValuesList[1]);
        var extreme = new[] { new Bar(DateTime.UnixEpoch, double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), -double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1) };
        Check(extreme, 2, 2, MovingAvgType.SimpleMovingAverage, 2);
        Assert.Equal(.5, Data(extreme).CalculateBollingerBandsAvgTrueRange(atrLength: 2, length: 2).CustomValuesList[1]);
    }
    private static void Check(Bar[] bars, int atrLength, int length, MovingAvgType kind, double multiplier)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.BollingerAtrOutputs(bars, atrLength, length, code, multiplier)["AtrDev"];
        Assert.Equal(expected, Data(bars).CalculateBollingerBandsAvgTrueRange(kind, atrLength, length, multiplier).CustomValuesList);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeBollingerBandsAvgTrueRangeFast(Data(bars), context, atrLength, length, kind, multiplier); Assert.Equal(expected, raw.Span.ToArray());
        using var alias = IndicatorCompute.ComputeBollingerBandsAtrFast(Data(bars), context, length, multiplier, atrLength, kind); Assert.Equal(expected, alias.Span.ToArray());
        using var state = new BollingerBandsAverageTrueRangeState(kind, atrLength, length, multiplier);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true })
            { var output = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], output.Value); Assert.Equal(expected[i], output.Outputs!["AtrDev"]); }
        }
    }
    [Fact]
    public void SelectedClosesAndCustomerAtrRetainTheirStages()
    {
        var highs = new[] { 10d, 5, 7, 4 }; var lows = new[] { -2d, -2, 0, 0 }; var prices = new[] { 9d, -1, 6, 2 };
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 4, highs[i], lows[i], 4, 1)).ToArray();
        var selectedBars = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray();
        var data = Data(bars); data.SetCustomValues(prices.ToList());
        var expected = BuiltInFormulaReferences.BollingerAtrOutputs(selectedBars, 2, 3, 1, 2)["AtrDev"];
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeBollingerBandsAvgTrueRangeFast(data, context, 2, 3); Assert.Equal(expected, raw.Span.ToArray());
        var deviation = BuiltInFormulaReferences.PopulationDeviation(prices, 3);
        foreach (var batch in new[] { false, true }) foreach (var irrelevantCenter in new[] { 1d, double.MaxValue })
        {
            var selected = Data(bars); selected.SetCustomValues(prices.ToList());
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(3, period); Assert.Equal(prices, values); return Enumerable.Repeat(irrelevantCenter, prices.Length).ToArray(); },
                (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 12d, 11, 8, 6 }, values); return new[] { 2d, 2, 2, 2 }; } });
            var target = deviation.Select(d => d == 0 ? 0 : (new ReferenceFraction(2) / (new ReferenceFraction(4) * ReferenceFraction.FromDouble(d))).ToDouble()).ToArray();
            if (batch) Assert.Equal(target, selected.CalculateBollingerBandsAvgTrueRange(atrLength: 2, length: 3).CustomValuesList);
            else { using var result = IndicatorCompute.ComputeBollingerBandsAvgTrueRangeFast(selected, context, 2, 3); Assert.Equal(target, result.Span.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void LegacyBatchOnlyAtrAveragesRemainAvailable()
    {
        var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10 + i, 13 + i, 8 + i, 11 + i % 5, 1)).ToArray();
        var ranges = bars.Select((b, i) => Math.Max(b.High - b.Low, Math.Max(Math.Abs(b.High - bars[i == 0 ? 0 : i - 1].Close), Math.Abs(b.Low - bars[i == 0 ? 0 : i - 1].Close)))).ToList();
        var atr = CalculationsHelper.GetMovingAverageList(Data(bars), MovingAvgType.HullMovingAverage, 3, ranges);
        var deviation = BuiltInFormulaReferences.PopulationDeviation(bars.Select(b => b.Close).ToArray(), 4);
        var expected = atr.Select((v, i) => deviation[i] == 0 ? 0 : (ReferenceFraction.FromDouble(v) / (new ReferenceFraction(4) * ReferenceFraction.FromDouble(deviation[i]))).ToDouble()).ToArray();
        Assert.Equal(expected, Data(bars).CalculateBollingerBandsAvgTrueRange(MovingAvgType.HullMovingAverage, 3, 4).CustomValuesList);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeBollingerBandsAvgTrueRangeFast(Data(bars), context, 3, 4, MovingAvgType.HullMovingAverage); Assert.Equal(expected, raw.Span.ToArray());
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceState()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new BollingerBandsAverageTrueRangeState(atrLength: 2, length: 2); using var control = new BollingerBandsAverageTrueRangeState(atrLength: 2, length: 2);
            var first = new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1); state.Update(Native(first), true, true); control.Update(Native(first), true, true);
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BBATR", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var next = Native(new Bar(DateTime.UnixEpoch.AddMinutes(1), 4, 5, 3, 4, 1)); Assert.Equal(control.Update(next, true, true).Value, state.Update(next, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(BollingerBandsAtr) || c.IndicatorType == typeof(BollingerBandsAvgTrueRange)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.BollingerAtrOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
