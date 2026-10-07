using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PhaseChangeNumericalTests
{
    private static Bar[] Bars(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("PCI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 1;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(PhaseChangeIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentResiduals(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.PhaseChangeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var smooth = (int)options.GetType().GetProperty("SmoothLength")!.GetValue(options)!;
        var kind = (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!;
        var bars = Bars(Enumerable.Range(1, 48).Select(i => (double)i).ToArray());
        var selected = Enumerable.Range(0, 48).Select(i => (double)(i * 7 % 13 - 6)).ToArray();
        var expected = BuiltInFormulaReferences.PhaseChangeValues(bars, length, smooth, Kind(kind), selected: selected);
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext();
        using var primary = IndicatorCompute.ComputePhaseChangeIndexFast(data, context, length);
        using var signal = IndicatorCompute.ComputePhaseChangeIndexSignalFast(data, context, length, smooth, kind);
        Assert.Equal(expected.Outputs["Pci"], primary.ToArray()); Assert.Equal(expected.Outputs["Signal"], signal.ToArray());
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(bar => bar.Close), data.ClosePrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(double[] prices, int length = 3, int smooth = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var bars = Bars(prices); var expected = BuiltInFormulaReferences.PhaseChangeValues(bars, length, smooth, Kind(kind));
        var batch = Data(bars).CalculatePhaseChangeIndex(kind, length, smooth);
        foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]);
        Assert.Equal(expected.Outputs["Pci"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var primary = IndicatorCompute.ComputePhaseChangeIndexFast(Data(bars), context, length);
        using var signal = IndicatorCompute.ComputePhaseChangeIndexSignalFast(Data(bars), context, length, smooth, kind);
        Assert.Equal(expected.Outputs["Pci"], primary.ToArray()); Assert.Equal(expected.Outputs["Signal"], signal.ToArray());
        using var state = new PhaseChangeIndexState(kind, length, smooth); using var window = new PhaseChangeWindow(kind, length, smooth);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 7d, -2, 1 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); }
            state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -100d })[0]), false, false); window.Next(-100, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(prices[i], final);
                    Assert.Equal(expected.Outputs["Pci"][i], point.Value);
                    foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                    Assert.Equal(expected.Outputs["Pci"][i], direct.Line); Assert.Equal(expected.Outputs["Signal"][i], direct.SignalLine); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void IndependentGradientRatioAndSignalHands()
    {
        var values = Check(new[] { 0d, 3, -1, 1 });
        Assert.Equal(new[] { 0d, 0, 100, 40 }, values["Pci"]); Assert.Equal(new[] { 0d, 0, 50, 70 }, values["Signal"]);
        Assert.Equal(new[] { Signal.None, Signal.None, Signal.StrongBuy, Signal.Buy }, Data(Bars(new[] { 0d, 3, -1, 1 })).CalculatePhaseChangeIndex(length: 3, smoothLength: 2).SignalsList);
        Assert.Equal(new[] { 0d, 100, 0 }, Check(new[] { 2d, 4, 6 }, 2, 1)["Pci"]);
    }
    [Fact]
    public void SignalAveragesUnpublishedRatios()
    {
        var values = Check(new[] { -6d, 7, -2, -7, -2, 6, -7, -5, 0, 4 });
        Assert.Equal(1350d / 23, values["Pci"][4]); Assert.Equal(1825d / 23, values["Signal"][4]);
        Assert.NotEqual((values["Pci"][3] + values["Pci"][4]) / 2, values["Signal"][4]);
    }
    [Fact]
    public void ExtremeAndSubnormalPricesKeepTheirDimensionlessRatios()
    {
        foreach (var pattern in new[] { new[] { 0d, 3, -1, 1, 2, -2, 0, 1 }, new[] { -1d, 1, 0, 1, -1, 0, -1, 1 } })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var baseline = Check(pattern, 3, 2, kind);
            foreach (var scale in new[] { pattern.Max(Math.Abs) == 1 ? double.MaxValue : Math.Pow(2, 1020), double.Epsilon })
            { var actual = Check(pattern.Select(v => v * scale).ToArray(), 3, 2, kind); foreach (var key in baseline.Keys) Assert.Equal(baseline[key], actual[key]); }
        }
    }
    [Fact]
    public void ExactFlatRangeAndOneUlpChangesRemainDistinct()
    {
        Assert.Equal(0, Check(new[] { 1d, 1, 1, 1 })["Pci"][3]);
        Assert.Equal(100, Check(new[] { 1d, 1, 1, Math.BitDecrement(1d) })["Pci"][3]);
        Assert.Equal(0, Check(new[] { 1d, 1, 1, Math.BitIncrement(1d) })["Pci"][3]);
        Assert.All(Check(new double[8])["Pci"], value => Assert.Equal(0, value));
    }
    [Fact]
    public void ExpiryPreviewResetAndLazyExtremePeriods()
    {
        var prices = new[] { 0d, 4, -1, 1, 7, -3, 0, 2, 1, -2 };
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue - 1, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<double>(), period, period, kind); Check(prices, period, period, kind); }
        var prefix = Check(prices.Take(6).ToArray()); var full = Check(prices);
        foreach (var key in prefix.Keys) Assert.Equal(prefix[key], full[key].Take(6));
    }
    [Fact]
    public void RejectedPricesAndCandlesCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var badBars = Bars(new[] { 1d, bad });
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(badBars).CalculatePhaseChangeIndex());
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputePhaseChangeIndexFast(Data(badBars), context));
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputePhaseChangeIndexSignalFast(Data(badBars), context, 3, 2, MovingAvgType.SimpleMovingAverage));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new PhaseChangeIndexState(length: 3, smoothLength: 2); using var control = new PhaseChangeIndexState(length: 3, smoothLength: 2);
                foreach (var b in Bars(new[] { 0d, 3, -1 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
                var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, true));
                foreach (var b in Bars(new[] { 1d, -3, 0, 4 }))
                { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void SignalCallbackReceivesRawIndexAndSmoothingPeriod()
    {
        var bars = Bars(new[] { 1d, 7, 2, 5, 4 }); var selected = new[] { 3d, -1, 5, 2, 7 }; var replacement = new[] { 0d, .5, -1, 2, 4 };
        var expected = BuiltInFormulaReferences.PhaseChangeValues(bars, 3, 2, selected: selected, externalSignal: replacement);
        foreach (var includeSignal in new[] { false, true })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            using var armed = ComponentAverage.Arm(new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>> { (values, period) => { Assert.Equal(expected.Outputs["Pci"], values); Assert.Equal(2, period); return replacement; } });
            using var context = new ComputeContext(); using var actual = includeSignal ? IndicatorCompute.ComputePhaseChangeIndexSignalFast(data, context, 3, 2, MovingAvgType.SimpleMovingAverage) : IndicatorCompute.ComputePhaseChangeIndexFast(data, context, 3);
            Assert.Equal(expected.Outputs[includeSignal ? "Signal" : "Pci"], actual.ToArray()); Assert.Equal(includeSignal ? 1 : 0, ComponentAverage.Requests); Assert.Equal(ComponentAverage.Requests, ComponentAverage.Substitutions); Assert.Equal(selected, data.ChainedValues);
        }
    }
}
