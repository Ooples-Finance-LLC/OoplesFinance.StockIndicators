using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HirashimaNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(HirashimaSugitaRS)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentResidualRegressions(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.HirashimaOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly string[] Keys = { "UpperBand1", "UpperBand2", "MiddleBand", "LowerBand1", "LowerBand2" };
    private static readonly int[] Offsets = { 1, 2, 0, -1, -2 };
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static Bar[] Prices(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.WeightedMovingAverage, int reference = 2)
    {
        var expected = BuiltInFormulaReferences.HirashimaValues(bars, length, reference); var batch = Data(bars).CalculateHirashimaSugitaRS(kind, length);
        Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        for (var k = 0; k < Keys.Length; k++) { Assert.Equal(expected.Outputs[Keys[k]], batch.OutputValues[Keys[k]]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeHirashimaSugitaRSFast(Data(bars), context, length, kind, Offsets[k]); Assert.Equal(expected.Outputs[Keys[k]], output.ToArray()); }
        using var state = new HirashimaSugitaRSState(kind, length); var window = new HirashimaWindow(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Prices(4, -2, 7, 0)) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Prices(999)[0]), false, false); window.Next(-999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final);
                    Assert.Equal(expected.Outputs["MiddleBand"][i], point.Value); Assert.Equal(expected.Signals[i], direct.Trade);
                    for (var k = 0; k < Keys.Length; k++) { Assert.Equal(expected.Outputs[Keys[k]][i], point.Outputs![Keys[k]]); Assert.Equal(point.Outputs[Keys[k]], direct.Bands[k]); }
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void HandPartialRegressionsDetermineCenterAndBothBandWidths()
    {
        var values = Check(Prices(0, 0, 9)); var center = 53d / 6;
        Assert.Equal(new[] { 0d, 0, center }, values["MiddleBand"]);
        for (var k = 0; k < Keys.Length; k++) Assert.Equal((ReferenceFraction.FromDouble(center) + new ReferenceFraction(3 * Offsets[k])).ToDouble(), values[Keys[k]][2]);
        var two = Check(Prices(2, 4, 8), 2); Assert.Equal(new[] { 2d, 4, 8 }, two["MiddleBand"]);
    }
    [Fact]
    public void WideResidualsAndBandsRecoverAfterExtremePrices()
    {
        foreach (var pair in Kinds) foreach (var length in new[] { 2, 3, 5, 8 })
            Check(Prices(double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.Epsilon, -double.Epsilon, 1, 4, -2, 7, 0), length, pair.Kind, pair.Reference);
        foreach (var pair in Kinds)
        {
            Check(Prices(Enumerable.Range(0, 18).Select(i => (i % 7 - 3) * double.Epsilon).ToArray()), 3, pair.Kind, pair.Reference);
            Check(Prices(Enumerable.Range(0, 18).Select(i => i % 3 == 0 ? Math.BitIncrement(double.MaxValue / 2) : double.MaxValue / 2).ToArray()), 5, pair.Kind, pair.Reference);
        }
    }
    [Fact]
    public void ExtremePeriodsUsePartialFitsAndLazyHistory()
    {
        foreach (var pair in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue })
        {
            Check(Array.Empty<Bar>(), length, pair.Kind, pair.Reference); var prices = new[] { double.MaxValue, -double.MaxValue, 0d, 2, -1 };
            var result = Check(Prices(prices), length, pair.Kind, pair.Reference); if (length <= 1) foreach (var key in Keys) Assert.Equal(prices, result[key]);
        }
    }
    [Fact]
    public void CallbackSlotsUseEmaThenAbsoluteResidualAndAllBands()
    {
        var bars = Prices(2, 5, -1, 4, 0); var suppliedEma = new[] { 1d, -2, 3, 0, 2 }; var suppliedWidth = new[] { 2d, -3, 1, 4, -2 };
        var expected = BuiltInFormulaReferences.HirashimaValues(bars, 3, 2, suppliedEma, suppliedWidth);
        for (var k = 0; k < Keys.Length; k++)
        {
            var seen = new List<double[]>(); Func<IReadOnlyList<double>, int, IReadOnlyList<double>> supplied = (values, length) => { Assert.Equal(3, length); seen.Add(values.ToArray()); return seen.Count == 1 ? suppliedEma : suppliedWidth; }; using var armed = ComponentAverage.Arm(new[] { supplied, supplied });
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeHirashimaSugitaRSFast(Data(bars), context, 3, MovingAvgType.WeightedMovingAverage, Offsets[k]);
            Assert.Equal(expected.Outputs[Keys[k]], output.ToArray()); Assert.Equal(2, seen.Count); Assert.Equal(bars.Select(b => b.Close), seen[0]); Assert.Equal(expected.Residual.Select(Math.Abs), seen[1]);
        }
        var calls = 0; using (ComponentAverage.Arm((values, _) => { calls++; return values; })) Data(bars).CalculateHirashimaSugitaRS(length: 3); Assert.Equal(0, calls);
    }
    [Fact]
    public void ExhaustedCallbacksRetainDefaultExtendedStages()
    {
        var bars = Prices(double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0); var expected = BuiltInFormulaReferences.HirashimaValues(bars, 3, 2).Outputs;
        for (var k = 0; k < Keys.Length; k++)
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (v, n) => new[] { 1d } }); ComponentAverage.Take(new[] { 0d }, 1);
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeHirashimaSugitaRSFast(Data(bars), context, 3, MovingAvgType.WeightedMovingAverage, Offsets[k]); Assert.Equal(expected[Keys[k]], output.ToArray());
        }
    }
    [Fact]
    public void SelectedSeriesPreservesAllFiveFastBands()
    {
        var bars = Prices(4, 7, -2, 0, 9); var selected = new[] { -2d, 5, 1, -4, 3 }; var expected = BuiltInFormulaReferences.HirashimaValues(Prices(selected), 3, 2).Outputs;
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateHirashimaSugitaRS(length: 3);
        for (var k = 0; k < Keys.Length; k++)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeHirashimaSugitaRSFast(data, context, 3, bandOffset: Offsets[k]);
            Assert.Equal(expected[Keys[k]], output.ToArray()); Assert.Equal(expected[Keys[k]], batch.OutputValues[Keys[k]]); Assert.Equal(selected, data.ChainedValues);
        }
    }
    [Fact]
    public void LegacyMeansRetainOrdinaryRouteAgreement()
    {
        var expectedBand = (new ReferenceFraction(2) - new ReferenceFraction(2) * ReferenceFraction.FromDouble(1d / 3)).ToDouble();
        Assert.Equal(expectedBand, HirashimaWindow.Band(2, 1d / 3, -2));
        var bars = Prices(4, -2, 7, 0, 3, 1); var kind = MovingAvgType.TripleExponentialMovingAverage; var batch = Data(bars).CalculateHirashimaSugitaRS(kind, 3);
        using var state = new HirashimaSugitaRSState(kind, 3);
        for (var k = 0; k < Keys.Length; k++) { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeHirashimaSugitaRSFast(Data(bars), context, 3, kind, Offsets[k]); Assert.Equal(batch.OutputValues[Keys[k]], output.ToArray()); }
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); foreach (var key in Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceResidualOrRegressionHistory()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new HirashimaSugitaRSState(length: 3); using var control = new HirashimaSugitaRSState(length: 3);
            foreach (var b in Prices(2, -1, 4)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Prices(7, -2, 3)) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); foreach (var key in Keys) Assert.Equal(expected.Outputs![key], actual.Outputs![key]); }
        }
    }
}
