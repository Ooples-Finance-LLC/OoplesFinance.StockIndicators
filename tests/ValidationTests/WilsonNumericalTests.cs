using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class WilsonNumericalTests
{
    private static readonly string[] Keys = { "S1", "S2", "U1", "U2" };
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("WILSON", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(WilsonRelativePriceChannel)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void FourChannelsMatchIndependentRatio(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.WilsonOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputFeedsAllChannels(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars,
        MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int length = 2, int smooth = 1,
        double overbought = 70, double oversold = 30, double upper = 55, double lower = 45)
    {
        var expected = BuiltInFormulaReferences.WilsonValues(bars, kind, length, smooth, overbought, oversold, upper, lower);
        var data = Data(bars).CalculateWilsonRelativePriceChannel(kind, length, smooth, overbought, oversold, upper, lower);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Empty(data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); var thresholds = new[] { oversold, lower, overbought, upper };
        for (var k = 0; k < Keys.Length; k++)
        {
            using var output = IndicatorCompute.ComputeWilsonRelativePriceChannelFast(Data(bars), context, length, smooth, kind, thresholds[k]);
            Assert.Equal(expected.Outputs[Keys[k]], output.ToArray());
        }
        using var state = new WilsonRelativePriceChannelState(kind, length, smooth, overbought, oversold, upper, lower);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(B(17)), true, false); state.Update(Native(B(-3)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(23)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected.Outputs["S1"][i], point.Value);
                    foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandValuesDetermineThresholdOrderAndSignals()
    {
        // EMA(positive changes)=[0,1,1/3], EMA(losses)=[0,0,2]; final RSI=100/7.
        var result = Check(new[] { B(10), B(12), B(9) });
        Assert.Equal(new[] { 3d, 3.6, 729d / 70 }, result.Outputs["S1"]);
        Assert.Equal(new[] { 4.5, 5.4, 1647d / 140 }, result.Outputs["S2"]);
        Assert.Equal(new[] { 7d, 8.4, 981d / 70 }, result.Outputs["U1"]);
        Assert.Equal(new[] { 5.5, 6.6, 1773d / 140 }, result.Outputs["U2"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongBuy, Signal.StrongSell }, result.Signals);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void StandardMeansRetainExtremeAndSignedPrices(MovingAvgType kind)
    {
        Check(Enumerable.Range(0, 6).Select(i => B(double.MaxValue, i)).ToArray(), kind, 3, 2);
        Check(Enumerable.Range(0, 12).Select(i => B((i * 7 % 13 - 6) * (double.MaxValue / 16), i)).ToArray(), kind, 3, 4);
        Check(new[] { B(0), B(double.Epsilon), B(-double.Epsilon), B(0), B(0) }, kind, 2, 2);
        Check(new[] { B(0), B(1), B(1), B(0), B(0), B(2) }, kind, 3, 2);
    }
    [Fact]
    public void ExtremeThresholdsRetainFiniteTinyPriceProducts()
    {
        Check(new[] { B(double.Epsilon), B(2 * double.Epsilon), B(0), B(-double.Epsilon) },
            overbought: double.MaxValue, oversold: -double.MaxValue, upper: double.MaxValue, lower: -double.MaxValue);
        var result = Check(new[] { B(0), B(0), B(0) }, overbought: double.MaxValue, oversold: -double.MaxValue);
        Assert.All(result.Outputs.Values.SelectMany(v => v), value => Assert.Equal(0, value));
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsAllocateObservedHistory(int length)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            Check(Array.Empty<Bar>(), kind, length, length);
            Check(new[] { B(0), B(1), B(0), B(2) }, kind, length, length);
        }
    }
    [Fact]
    public void DirectSelectedPricesLeaveOriginalCandlesIntact()
    {
        var bars = new[] { B(100), B(110), B(90) }; var selected = new[] { 10d, 12, 9 };
        var expected = BuiltInFormulaReferences.WilsonValues(selected.Select((v, i) => B(v, i)).ToArray(), length: 2);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var (key, threshold) in Keys.Zip(new[] { 30d, 45, 70, 55 }))
        {
            using var output = IndicatorCompute.ComputeWilsonRelativePriceChannelFast(data, context, length: 2, threshold: threshold);
            Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(selected, data.ChainedValues);
        }
        data.CalculateWilsonRelativePriceChannel(length: 2);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    [Fact]
    public void CallbackSlotsAreDynamicAndShortValuesAreZeroPadded()
    {
        foreach (var fast in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (v, p) => { Assert.Equal(2, p); Assert.Equal(new[] { 0d, 2, 0 }, v); return new[] { 0d, 3 }; },
                (v, p) => { Assert.Equal(2, p); Assert.Equal(new[] { 0d, 0, 3 }, v); return new[] { 0d, 1, 2 }; },
                (v, p) => { Assert.Equal(3, p); Assert.Equal(new[] { 70d, 45, -30 }, v); return new[] { 10d }; }
            });
            var data = Data(new[] { B(10), B(12), B(9) }); using var context = new ComputeContext();
            if (fast)
            {
                using var output = IndicatorCompute.ComputeWilsonRelativePriceChannelFast(data, context, length: 2, smoothLength: 3);
                Assert.Equal(new[] { 9d, 12, 9 }, output.ToArray());
            }
            else
            {
                data.CalculateWilsonRelativePriceChannel(length: 2, smoothLength: 3);
                Assert.Equal(BuiltInFormulaReferences.WilsonValues(new[] { B(10), B(12), B(9) }, length: 2, smooth: 3).Outputs["S1"], data.OutputValues["S1"]);
            }
            Assert.Equal(fast ? 3 : 0, ComponentAverage.Requests); Assert.Equal(ComponentAverage.Requests, ComponentAverage.Substitutions);
        }
        using (ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>()))
        {
            using var context = new ComputeContext();
            using var output = IndicatorCompute.ComputeWilsonRelativePriceChannelFast(Data(new[] { B(10), B(12) }), context);
            Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidInputsAndThresholdsAreRejectedBeforeStateChanges()
    {
        using var state = new WilsonRelativePriceChannelState(length: 2); using var control = new WilsonRelativePriceChannelState(length: 2);
        state.Update(Native(B(10)), true, false); control.Update(Native(B(10)), true, false);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => new WilsonRelativePriceChannelState(oversold: bad));
            Assert.ThrowsAny<ArgumentException>(() => Data(new[] { B(1) }).CalculateWilsonRelativePriceChannel(upperNeutralZone: bad));
            using var context = new ComputeContext();
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeWilsonRelativePriceChannelFast(Data(new[] { B(1) }), context, threshold: bad));
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, 1, 1, 1, 1, bad)), true, true));
            var data = Data(new[] { B(bad) }); data.SetCustomValues(new List<double> { 10 });
            Assert.ThrowsAny<ArgumentException>(() => data.CalculateWilsonRelativePriceChannel());
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeWilsonRelativePriceChannelFast(data, context));
        }
        Assert.Equal(control.Update(Native(B(7)), true, true).Value, state.Update(Native(B(7)), true, true).Value);
    }
}
