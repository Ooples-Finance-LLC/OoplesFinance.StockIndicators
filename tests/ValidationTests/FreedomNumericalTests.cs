using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FreedomNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FreedomOfMovement)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentVolumeMovementAndScores(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FreedomOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static Bar[] Bars(double[] prices, double[] volumes) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, volumes[i])).ToArray();
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1)
    {
        var expected = BuiltInFormulaReferences.FreedomValues(bars, length, reference); var batch = Data(bars).CalculateFreedomOfMovement(kind, length);
        Assert.Equal(expected.Outputs["Fom"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Fom", "Dpl" })
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFreedomOfMovementFast(Data(bars), context, length, kind, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new FreedomOfMovementState(kind, length); using var window = new FreedomWindow(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 8d, -2, 6, 1 }, new[] { 1d, 9, 3, 8 })) { state.Update(Native(b), true, false); window.Next(b.Close, b.Volume, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { 999d }, new[] { 999d })[0]), false, false); window.Next(-999, -999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, bars[i].Volume, final);
                    Assert.Equal(expected.Outputs["Fom"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Fom"]); Assert.Equal(point.Value, direct.Score); Assert.Equal(expected.Signals[i], direct.Trade);
                    Assert.Equal(expected.Outputs["Dpl"][i], point.Outputs["Dpl"]); Assert.Equal(point.Outputs["Dpl"], direct.Demand);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandExactCenterAndInclusiveDemandThreshold()
    {
        var result = Check(Bars(new[] { 1d, 2, 6, 6 }, new[] { 1d, 2, 3, 6 }));
        Assert.Equal(new[] { 0d, 0, Math.Sqrt(2), 19 / Math.Sqrt(182) }, result.Outputs["Fom"]); Assert.Equal(new[] { 1d, 1, 1, 1 }, result.Outputs["Dpl"]);
        var triggered = Check(Bars(new[] { 1d, 2, 3, 4, 8 }, new[] { 1d, 1, 1, 1, 2 }), 5);
        Assert.Equal(new[] { 0d, 0, 0, 0, 2 }, triggered.Outputs["Fom"]); Assert.Equal(new[] { 1d, 1, 1, 1, 4 }, triggered.Outputs["Dpl"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongBuy, Signal.StrongBuy, Signal.StrongBuy }, triggered.Signals);
        Check(Bars(new[] { 1d, 2, 3, 4, 8, 3, 4, 5, 1 }, new[] { 1d, 1, 1, 1, 2, 1, 1, 1, 1 }), 5);
        var wideSignals = Check(Bars(new[] { -double.MaxValue, double.MaxValue, double.MaxValue / 2, -double.MaxValue, 0 }, Enumerable.Repeat(1d, 5).ToArray()));
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.None, Signal.StrongBuy }, wideSignals.Signals);
        var negativeSignals = Check(Bars(new[] { double.MaxValue, -double.MaxValue, -double.MaxValue / 2, double.MaxValue, 0 }, Enumerable.Repeat(1d, 5).ToArray()));
        Assert.Equal(new[] { Signal.None, Signal.StrongSell, Signal.Sell, Signal.None, Signal.StrongSell }, negativeSignals.Signals);
        var flat = Check(Bars(new[] { 4d, -2, 0, 7 }, new[] { 1d, 3, 1, 6 }), 1); Assert.All(flat.Outputs["Fom"], v => Assert.Equal(0, v)); Assert.All(flat.Outputs["Dpl"], v => Assert.Equal(4, v));
    }
    [Fact]
    public void WideMovesVolumeScoresAndRankRangesRecover()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Bars(Enumerable.Range(0, 19).Select(i => (i % 7 - 3) * scale).ToArray(), Enumerable.Range(0, 19).Select(i => (i % 5 - 2) * scale).ToArray()), 4, kind.Kind, kind.Reference);
        foreach (var kind in Kinds)
        {
            Check(Bars(new[] { double.Epsilon, double.MaxValue, -double.MaxValue, double.Epsilon, 0, 1, 2, 1, 1, 1, 1 }, new[] { double.MaxValue, double.Epsilon, 2 * double.Epsilon, double.Epsilon, 0, 1, 2, 1, 1, 1, 1 }), 2, kind.Kind, kind.Reference);
            Check(Bars(new[] { 1d, 2, 6, 6, 3, 1, 2 }, new[] { 1d, Math.BitIncrement(1), 1, Math.BitIncrement(1), 1, 1, 1 }), 3, kind.Kind, kind.Reference);
            Check(Bars(Enumerable.Repeat(double.MaxValue, 12).ToArray(), Enumerable.Repeat(double.MaxValue, 12).ToArray()), 3, kind.Kind, kind.Reference);
        }
    }
    [Fact]
    public void SubnormalPopulationVarianceAndExtendedVolumeScoresStayDefined()
    {
        var bars = Bars(new[] { 1d, 2, 3, 4, 8 }, new[] { 0d, 0, 0, 0, double.Epsilon });
        Assert.Equal(2, Check(bars, 5).Outputs["Fom"][^1]);
        using var mean = new FreedomWindow(MovingAvgType.SimpleMovingAverage, 2); Assert.Equal(0, mean.RelativeVolume(0, true).Publish()); Assert.Equal(1, mean.RelativeVolume(double.Epsilon, false).Publish()); Assert.Equal(1, mean.RelativeVolume(double.Epsilon, true).Publish());
        using var wide = new FreedomWindow(MovingAvgType.SimpleMovingAverage, 2);
        wide.RelativeVolume(0, true, double.MaxValue); var extended = wide.RelativeVolume(double.Epsilon, true, double.MaxValue); Assert.True(extended.UpperShift > 0); Assert.True(double.IsNegativeInfinity(extended.Publish()));
        Assert.Equal(1, wide.RelativeVolume(1, true).Publish());
    }
    [Fact]
    public void ExpiryFlatRanksAndDemandMemoryUseIndependentWindows()
    {
        foreach (var length in new[] { 2, 3, 5, 7 })
            Check(Bars(new[] { 1d, 2, 3, 4, 8, 8, 4, 4, 2, 1, 1, 1, 1 }, new[] { 1d, 1, 1, 1, 8, 8, 1, 1, 2, 1, 1, 1, 1 }), length);
        Check(Bars(new[] { 0d, 1, 0, -1, 0, 1, 1, 1 }, new[] { 0d, 0, 1, 1, 1, 0, 0, 0 }), 3);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Bars(new[] { 4d, -2, 5 }, new[] { 1d, 3, 1 }), period, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), period, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void VolumeCallbackAndSelectedPriceContractCoversBothOutputs()
    {
        var bars = Bars(new[] { 1d, 4, -2, 8, 0, 4 }, new[] { 1d, 2, 5, 3, 7, 9 }); var selected = new[] { 4d, -1, 3, 7, 2, 9 }; var supplied = new[] { 0d, 0, 0, 1, 1, 1 };
        var projected = Bars(selected, bars.Select(b => b.Volume).ToArray()); var expected = BuiltInFormulaReferences.FreedomValues(projected, 3, 1, supplied);
        foreach (var key in new[] { "Fom", "Dpl" })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); var calls = 0;
            using var binding = ComponentAverage.Arm((values, n) => { Assert.Equal(3, n); Assert.Equal(bars.Select(b => b.Volume), values); calls++; return supplied; });
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFreedomOfMovementFast(data, context, 3, outputKey: key); Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(1, calls); Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        }
        using (ComponentAverage.Arm((v, _) => throw new InvalidOperationException("Batch must consume no callback")))
        { var data = Data(bars); data.SetCustomValues(selected.ToList()); data.CalculateFreedomOfMovement(length: 3); Assert.Equal(BuiltInFormulaReferences.FreedomValues(projected, 3, 1).Outputs["Fom"], data.CustomValuesList); Assert.Equal(0, ComponentAverage.Requests); }
        foreach (var key in new[] { "Fom", "Dpl" })
        {
            var calls = 0; using var binding = ComponentAverage.Arm((v, _) => { calls++; return v; }); Assert.NotNull(ComponentAverage.Take(new[] { 1d }, 1));
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFreedomOfMovementFast(Data(bars), context, 3, outputKey: key); Assert.Equal(BuiltInFormulaReferences.FreedomValues(bars, 3, 1).Outputs[key], output.ToArray()); Assert.Equal(1, calls); Assert.Equal(2, ComponentAverage.Requests);
        }
    }
    [Fact]
    public void LegacyMeansKeepPublicRouteAgreement()
    {
        var bars = Bars(new[] { 4d, -2, 7, 0, 3, 1 }, new[] { 1d, 2, 4, 3, 2, 1 }); var kind = MovingAvgType.TripleExponentialMovingAverage;
        var batch = Data(bars).CalculateFreedomOfMovement(kind, 3); using var state = new FreedomOfMovementState(kind, 3);
        foreach (var key in new[] { "Fom", "Dpl" }) { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFreedomOfMovementFast(Data(bars), context, 3, kind, key); Assert.Equal(batch.OutputValues[key], output.ToArray()); }
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); foreach (var key in batch.OutputValues.Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]); }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FreedomOfMovementState(length: 3); using var control = new FreedomOfMovementState(length: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Dpl"], actual.Outputs!["Dpl"]); }
        }
    }
}
