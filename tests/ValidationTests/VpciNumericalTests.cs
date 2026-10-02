using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VpciNumericalTests
{
    private static Bar B(double close, double volume = 1, int i = 0)
        => new(DateTime.UnixEpoch.AddMinutes(i), close, close, close, close, volume);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VPCI", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Vpci)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void BothRoutesMatchIndependentFactors(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VpciOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedPricesRetainVolumesForBothFastOutputs(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions(); var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var bars = Enumerable.Range(0, 48).Select(i => B(100 + i, i % 5 + 1, i)).ToArray();
        var selected = bars.Select((_, i) => (double)(i * 7 % 13 - 6)).ToArray();
        var expected = BuiltInFormulaReferences.VpciValues(bars.Select((b, i) => B(selected[i], b.Volume, i)).ToArray(), 5, 20, length);
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var line = IndicatorCompute.ComputeVpciFast(data, context);
        using var signal = IndicatorCompute.ComputeVpciSignalFast(data, context, length);
        Assert.Equal(expected.Outputs["Vpci"], line.ToArray()); Assert.Equal(expected.Outputs["Signal"], signal.ToArray());
        Assert.Equal(selected, data.ChainedValues);
        data.CalculateVolumePriceConfirmationIndicator(length: length);
        foreach (var key in new[] { "Vpci", "Signal" }) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        Assert.Equal(bars.Select(b => b.Volume), data.Volumes);
    }
    private static Dictionary<string, double[]> Check(Bar[] bars, int fast = 2, int slow = 3, int signal = 2,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2
            : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.VpciValues(bars, fast, slow, signal, code);
        var batch = Data(bars).CalculateVolumePriceConfirmationIndicator(kind, fast, slow, signal);
        foreach (var key in new[] { "Vpci", "Signal" }) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]);
        Assert.Equal(expected.Outputs["Vpci"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var line = IndicatorCompute.ComputeVpciFast(Data(bars), context, fast, slow, kind);
        Assert.Equal(expected.Outputs["Vpci"], line.ToArray());
        if (kind == MovingAvgType.SimpleMovingAverage)
        {
            var core = Enumerable.Repeat(-123d, bars.Length + 2).ToArray();
            VolumeCore.VolumePriceConfirmationIndicator(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), core.AsSpan(1, bars.Length), fast, slow);
            Assert.Equal(expected.Outputs["Vpci"], core.Skip(1).Take(bars.Length)); Assert.Equal(-123, core[0]); Assert.Equal(-123, core[^1]);
        }
        using var state = new VolumePriceConfirmationIndicatorState(kind, fast, slow, signal);
        using var kernel = new VpciWindow(kind, fast, slow, signal);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); kernel.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                _ = state.Update(Native(B(-17, 9)), false, false); _ = kernel.Next(-17, 9, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var raw = kernel.Next(bars[i].Close, bars[i].Volume, final);
                    Assert.Equal(expected.Outputs["Vpci"][i], point.Value); Assert.Equal(point.Value, raw.Line);
                    Assert.Equal(expected.Outputs["Signal"][i], raw.SignalLine); Assert.Equal(expected.Signals[i], raw.Trade);
                    foreach (var key in new[] { "Vpci", "Signal" }) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void IndependentThreeFactorAndSignalHand()
    {
        var result = Check(new[] { B(1, 1), B(2, 2), B(3, 3) });
        // (7/3-2)*(13/5)/(5/2)*(5/2)/2 = 13/30; signal averages 0 and 13/30.
        Assert.Equal(new[] { 0d, 0, 13d / 30 }, result["Vpci"]);
        Assert.Equal(new[] { 0d, 0, 13d / 60 }, result["Signal"]);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void StandardMeansRetainExtremeProductsAndSignedVolumes(MovingAvgType kind)
    {
        Check(Enumerable.Range(0, 32).Select(i => B((i * 7 % 13 - 6) * (double.MaxValue / 8),
            (i % 5 - 2) * (double.MaxValue / 4), i)).ToArray(), 2, 3, 4, kind);
        Check(Enumerable.Range(0, 12).Select(i => B(double.MaxValue, double.MaxValue, i)).ToArray(), 2, 3, 4, kind);
    }
    [Fact]
    public void UnpublishedSignalRetainsSubnormalProductInformation()
    {
        var result = Check(new[] { B(3 * double.Epsilon, double.Epsilon), B(6 * double.Epsilon, 2 * double.Epsilon), B(9 * double.Epsilon, 3 * double.Epsilon) });
        // Raw is 13/10 epsilon, signal is 13/20 epsilon. Both round to epsilon;
        // smoothing a previously rounded raw value would incorrectly give zero.
        Assert.Equal(new[] { 0d, 0, double.Epsilon }, result["Vpci"]);
        Assert.Equal(new[] { 0d, 0, double.Epsilon }, result["Signal"]);
    }
    [Fact]
    public void ZeroDenominatorsAndExactCancellationRemainDefined()
    {
        Assert.All(Check(new[] { B(1, 1), B(2, -1), B(3, 0) })["Vpci"], value => Assert.Equal(0, value));
        Assert.All(Check(new[] { B(1, 1), B(2, 2), B(-2, 3) })["Vpci"], value => Assert.Equal(0, value));
        Check(new[] { B(1, 0), B(-1, 0), B(2, 0), B(3, 1), B(-2, -1), B(4, 1) });
    }
    [Fact]
    public void TrueOverflowDoesNotPoisonLaterFiniteWindows()
    {
        var result = Check(new[] { B(1, 1), B(2, -1), B(3, double.Epsilon), B(4, 1), B(5, 1), B(6, 1), B(7, 1), B(8, 1) });
        Assert.Equal(double.PositiveInfinity, result["Vpci"][2]);
        Assert.True(double.IsFinite(result["Vpci"][^1])); Assert.True(double.IsFinite(result["Signal"][^1]));
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsGrowOnlyWithObservedHistory(int period)
    {
        Check(Array.Empty<Bar>(), period, period, period);
        Check(new[] { B(1, 1), B(2, 2), B(3, 3), B(4, 4) }, period, period, period);
    }
    [Fact]
    public void CallbackOrdersAndShortResultsPreserveTheirContracts()
    {
        foreach (var route in new[] { "batch", "fast", "signal" })
        {
            var slots = route == "batch" ? new[] { "wf", "ws", "vf", "vs", "pf", "ps", "signal" }
                : new[] { "wf", "ws", "pf", "ps", "vf", "vs", "signal" };
            var calls = slots.Select(slot => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((values, period) =>
            {
                Assert.Equal(slot == "signal" ? 2 : slot.EndsWith("f") ? 5 : 20, period);
                Assert.Equal(slot == "signal" ? new[] { 3d, 3, 0 } : slot.StartsWith("v") ? new[] { 2d, 4, 6 } : new[] { 1d, 2, 3 }, values);
                return slot switch { "wf" => new[] { 3d, 3 }, "ws" => new[] { 5d, 5, 5 }, "vf" => new[] { 6d, 6, 6 },
                    "vs" => new[] { 3d, 3, 3 }, "pf" => new[] { 2d, 2, 2 }, "ps" => new[] { 4d, 4, 4 }, _ => new[] { 7d } };
            })).ToArray();
            using var armed = ComponentAverage.Arm(calls); using var context = new ComputeContext();
            var data = Data(new[] { B(1, 2), B(2, 4), B(3, 6) });
            if (route == "batch")
            {
                data.CalculateVolumePriceConfirmationIndicator(fastLength: 2, slowLength: 3, length: 2);
                Assert.Equal(new[] { 0d, 0, 13d / 30 }, data.OutputValues["Vpci"]); Assert.Equal(new[] { 0d, 0, 13d / 60 }, data.OutputValues["Signal"]);
            }
            else
            {
                using var result = route == "fast" ? IndicatorCompute.ComputeVpciFast(data, context) : IndicatorCompute.ComputeVpciSignalFast(data, context, 2);
                Assert.Equal(route == "fast" ? new[] { 3d, 3, 0 } : new[] { 7d, 0, 0 }, result.ToArray());
            }
            Assert.Equal(route == "batch" ? 0 : route == "fast" ? 6 : 7, ComponentAverage.Requests); Assert.Equal(ComponentAverage.Requests, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void CoreValidatesAndSupportsAliasedOutput()
    {
        var prices = new[] { 1d, 2, 3 }; var volumes = new[] { 1d, 2, 3 };
        VolumeCore.VolumePriceConfirmationIndicator(prices, volumes, prices, 2, 3); Assert.Equal(new[] { 0d, 0, 13d / 30 }, prices);
        VolumeCore.VolumePriceConfirmationIndicator(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        var guard = new[] { 123d, 456 };
        Assert.Throws<ArgumentException>(() => VolumeCore.VolumePriceConfirmationIndicator(new[] { 1d, 2 }, new[] { 1d }, guard));
        Assert.Throws<ArgumentException>(() => VolumeCore.VolumePriceConfirmationIndicator(new[] { 1d, 2 }, new[] { 1d, 1 }, new double[1]));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => VolumeCore.VolumePriceConfirmationIndicator(new[] { 1d, bad }, new[] { 1d, 1 }, guard));
            Assert.ThrowsAny<ArgumentException>(() => VolumeCore.VolumePriceConfirmationIndicator(new[] { 1d, 2 }, new[] { 1d, bad }, guard));
            Assert.Equal(new[] { 123d, 456 }, guard);
        }
    }
    [Fact]
    public void InvalidNativeAndOriginalSelectedFieldsAreRejectedBeforeMutation()
    {
        using var state = new VolumePriceConfirmationIndicatorState(fastLength: 2, slowLength: 3, length: 2);
        state.Update(Native(B(1, 1)), true, false); state.Update(Native(B(2, 2)), true, false);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, 0, bad, 0, 0, 1)), true, false));
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(B(0, bad)), true, false));
            var data = Data(new[] { B(bad) }); data.SetCustomValues(new List<double> { 20 });
            Assert.ThrowsAny<ArgumentException>(() => data.CalculateVolumePriceConfirmationIndicator());
            using var context = new ComputeContext(); Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVpciFast(data, context));
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVpciSignalFast(data, context, 2));
        }
        Assert.Equal(13d / 30, state.Update(Native(B(3, 3)), true, false).Value);
    }
}
