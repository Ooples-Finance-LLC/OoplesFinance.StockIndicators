using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BryantNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(BryantAdaptiveMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAdaptiveGain(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.BryantOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Efficiency, double[] Alpha) Check(Bar[] bars, int length = 3, int maximum = 100, double trend = -1)
    {
        var expected = BuiltInFormulaReferences.BryantValues(bars, length, maximum, trend); var batch = Data(bars).CalculateBryantAdaptiveMovingAverage(length, maximum, trend);
        Assert.Equal(expected.Outputs["Bama"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Bama"], batch.OutputValues["Bama"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeBryantAdaptiveMovingAverageFast(Data(bars), context, length, maximum, trend); Assert.Equal(expected.Outputs["Bama"], output.ToArray());
        using var state = new BryantAdaptiveMovingAverageState(length, maximum, trend); var window = new BryantWindow(length, maximum, trend);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 3d, -2, 5, -1, 8, 1, -4 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i]; state.Update(Native(Bars(new[] { -91d })[0]), false, false); window.Next(-91, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(b), final, true); Assert.Equal(expected.Outputs["Bama"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Bama"]); var direct = window.Next(b.Close, final); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Signal); Assert.Equal(expected.Efficiency[i], direct.Efficiency); Assert.Equal(expected.Alpha[i], direct.Alpha); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideEfficiencyGainAndConvexRecursionMatchIndependentRationalStages()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var length in new[] { 2, 7 }) foreach (var maximum in new[] { 3, 100 }) foreach (var trend in new[] { -3d, -1, 0, 2, double.MaxValue, -double.MaxValue })
        {
            var bars = Bars(Enumerable.Range(0, 17).Select(i => (i % 7 - 3) * scale)); var result = Check(bars, length, maximum, trend);
            for (var i = 0; i < bars.Length; i++) { Assert.InRange(result.Efficiency[i], 0, 1); Assert.InRange(result.Alpha[i], 0, 1); Assert.InRange(result.Outputs["Bama"][i], Math.Min(0, bars.Take(i + 1).Min(b => b.Close)), Math.Max(0, bars.Take(i + 1).Max(b => b.Close))); }
        }
    }
    [Fact]
    public void HandZeroSeedEfficiencyWarmupAndGainClampsArePreserved()
    {
        Assert.Equal(new[] { 1d, 2.5, 1.25 }, Check(Bars(new[] { 2d, 4, 0 }), 3, 100, 0).Outputs["Bama"]);
        var rising = Check(Bars(new[] { 1d, 2, 4 }), 2); Assert.Equal(new[] { 1d, 2, 7d / 3 }, rising.Outputs["Bama"]); Assert.Equal(new[] { 0d, 0, 1 }, rising.Efficiency);
        var zeroShape = Check(Bars(new[] { 1d, 2, 4 }), 3, 100, 2); Assert.Equal(new[] { 1d, 2, 4 }, zeroShape.Outputs["Bama"]); Assert.Equal(new[] { 1d, 1, 1 }, zeroShape.Alpha);
        var floor = Check(Bars(new[] { 3d }), 20, 2, 0); Assert.Equal(2d / 3, floor.Alpha[0]); Assert.Equal(2, floor.Outputs["Bama"][0]);
        Assert.Equal(new[] { 2d, -3, 4 }, Check(Bars(new[] { 2d, -3, 4 }), 7, 1).Outputs["Bama"]);
    }
    [Fact]
    public void HalfEfficiencyCancelsHugeSignedTrendBeforeGainClamping()
    {
        foreach (var trend in new[] { double.MaxValue, -double.MaxValue })
        {
            var result = Check(Bars(new[] { 0d, 3, 2, 4 }), 2, 100, trend); Assert.Equal(.5, result.Efficiency[2]); Assert.Equal(2d / 3, result.Alpha[2]); Assert.Equal(7d / 3, result.Outputs["Bama"][2]);
        }
        Check(Bars(new[] { 0d, 1, 0, 1, 2, 1, 0 }), 2, 100, -2);
    }
    [Fact]
    public void BothExtremePeriodsNormalizeWithoutPreallocatingHistory()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var position in new[] { 0, 1 })
        { var length = position == 0 ? period : 3; var maximum = position == 1 ? period : 100; Check(Array.Empty<Bar>(), length, maximum); Check(Bars(new[] { 1d, -3, 4, 0, 2, 7, -1 }), length, maximum); }
    }
    [Fact]
    public void SelectedCallerPricesNeedNoAverageComponents()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var bars = Enumerable.Range(0, 6).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i + 1)).ToArray(); var expected = BuiltInFormulaReferences.BryantValues(Bars(selected), 3, 100, -1);
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> forbidden = (_, _) => throw new InvalidOperationException("Bryant has no average components."); using var armed = ComponentAverage.Arm(new[] { forbidden }); var data = Data(bars); data.SetCustomValues(selected.ToList()); data.CalculateBryantAdaptiveMovingAverage(3); Assert.Equal(expected.Outputs["Bama"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); var source = Data(bars); source.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeBryantAdaptiveMovingAverageFast(source, context, 3); Assert.Equal(expected.Outputs["Bama"], output.ToArray()); Assert.Equal(0, ComponentAverage.Requests);
    }
    [Fact]
    public void BothCoreRoutesRegistryAndInPlaceSpansUseTheDefaultAdaptiveGain()
    {
        var registry = OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.GetRequired(MovingAvgType.BryantAdaptiveMovingAverage);
        foreach (var length in new[] { 1, 2, 7, int.MaxValue }) foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d, 2, 10, -4, 3, 8, 1, 20, -9 }), Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0 }) })
        {
            var prices = bars.Select(b => b.Close).ToArray(); var expected = BuiltInFormulaReferences.BryantValues(bars, length, 100, -1).Outputs["Bama"]; var output = new double[prices.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.BryantAdaptiveMovingAverage(prices, output, length); Assert.Equal(expected, output);
            OoplesFinance.StockIndicators.Core.TrendCore.BryantAdaptiveMovingAverage(prices, output, length); Assert.Equal(expected, output);
            registry.Compute(prices, output, length); Assert.Equal(expected, output);
            var inPlace = prices.ToArray(); OoplesFinance.StockIndicators.Core.MovingAverageCore.BryantAdaptiveMovingAverage(inPlace, inPlace, length); Assert.Equal(expected, inPlace);
            inPlace = prices.ToArray(); OoplesFinance.StockIndicators.Core.TrendCore.BryantAdaptiveMovingAverage(inPlace, inPlace, length); Assert.Equal(expected, inPlace);
            inPlace = prices.ToArray(); registry.Compute(inPlace, inPlace, length); Assert.Equal(expected, inPlace);
            Assert.Equal(expected, CalculationsHelper.GetMovingAverageList(Data(bars), MovingAvgType.BryantAdaptiveMovingAverage, length, prices.ToList()));
        }
        Assert.Throws<ArgumentException>(() => OoplesFinance.StockIndicators.Core.MovingAverageCore.BryantAdaptiveMovingAverage(new[] { 1d }, Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => OoplesFinance.StockIndicators.Core.TrendCore.BryantAdaptiveMovingAverage(new[] { 1d }, Array.Empty<double>()));
    }
    [Fact]
    public void WideResidualSignalsAndExpiredTravelStayFinite()
    {
        var prices = new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 0, 2, -3, 4, 0, 1, 7, -1, 0 };
        foreach (var trend in new[] { -1d, 0, 2 }) Check(Bars(prices), 4, 100, trend);
        var flat = Check(Bars(Enumerable.Repeat(3d, 12)), 4, 100, -1); Assert.All(flat.Efficiency, value => Assert.Equal(0, value)); Assert.All(flat.Outputs["Bama"], value => Assert.InRange(value, 0, 3));
        var signals = Check(Bars(new[] { 2d, 4, 0 }), 3, 100, 0).Signals; Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongBuy, Signal.StrongSell }, signals);
    }
    [Fact]
    public void InvalidTrendAndCandlesCannotAdvanceEfficiencyOrRecursion()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BryantAdaptiveMovingAverageState(trend: invalid)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateBryantAdaptiveMovingAverage(trend: invalid)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeBryantAdaptiveMovingAverageFast(Data(Array.Empty<Bar>()), context, trend: invalid));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new BryantAdaptiveMovingAverageState(3); using var control = new BryantAdaptiveMovingAverageState(3);
                foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
                var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
                foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
            }
        }
    }
}
