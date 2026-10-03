using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolatilityMomentumNumericalTests
{
    private static readonly string[] Keys = { "Vbm", "Signal" };
    private static Bar B(double high, double low, double close, int i = 0)
        => new(DateTime.UnixEpoch.AddMinutes(i), close, high, low, close, 1);
    private static Bar[] Hands => new[] { 0d, 2, 4, 0, 2 }.Select((c, i) => B(c + 1, c - 1, c, i)).ToArray();
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VBM", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolatilityBasedMomentum)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void OutputsMatchIndependentFractions(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.VolatilityMomentumOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedPricesRetainOriginalRanges(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int lag = 2, int range = 2)
    {
        var expected = BuiltInFormulaReferences.VolatilityMomentumValues(bars, kind, lag, range);
        var data = Data(bars).CalculateVolatilityBasedMomentum(kind, lag, range);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Outputs["Vbm"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext();
        foreach (var key in Keys.Concat(new string[] { null! }))
        {
            using var output = IndicatorCompute.ComputeVolatilityBasedMomentumFast(Data(bars), context, lag, range, kind, key);
            Assert.Equal(expected.Outputs[key ?? "Vbm"], output.ToArray());
        }
        using var state = new VolatilityBasedMomentumState(kind, lag, range);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(B(5, -2, 3)), true, false); state.Update(Native(B(6, -4, -2)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(8, -9, 5)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Vbm"][i], point.Value);
                    foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandLagTrueRangeAndSignalUseDistinctPeriods()
    {
        var result = Check(Hands);
        Assert.Equal(new[] { 0d, 0, 4d / 3, -.5, -.5 }, result.Outputs["Vbm"]);
        Assert.Equal(new[] { 0d, 0, 2d / 3, 5d / 12, -.5 }, result.Outputs["Signal"]);
        Assert.Equal(new[] { Signal.None, Signal.None, Signal.StrongBuy, Signal.StrongSell, Signal.None }, result.Signals);
        Check(new[] { B(10, 8, 9), B(11, 9, 10), B(15, 13, 14), B(3, 1, 2) });
        Check(Hands, lag: 3, range: 2); Check(Hands, lag: 2, range: 3);
    }
    [Fact]
    public void FirstRangeUsesCurrentCloseBeforeLaggedMomentumBegins()
    {
        // The first bar has no preceding close: ranges are [2,2], so SMA2 ATR is 2.
        // With lag 1 the second price move is 1, hence momentum and signal are 1/2.
        // A fictitious previous close of zero gives first range 10 and output 1/6.
        var result = Check(new[] { B(10, 8, 9), B(11, 9, 10, 1) }, lag: 1, range: 2);
        Assert.Equal(new[] { 0d, .5 }, result.Outputs["Vbm"]);
        Assert.Equal(new[] { 0d, .5 }, result.Outputs["Signal"]);
    }
    [Fact]
    public void AtrSmaWarmupWaitsForFullRangeWindow()
    {
        // True ranges are [2,2,2]; SMA3 ATR must remain zero until bar three.
        // Lag 1 exposes premature ATR publication: the second output would be 3/4.
        var result = Check(new[] { B(10, 8, 9), B(11, 9, 10, 1), B(12, 10, 11, 2) }, lag: 1, range: 3);
        Assert.Equal(new[] { 0d, 0, .5 }, result.Outputs["Vbm"]);
        Assert.Equal(new[] { 0d, 0, .5 }, result.Outputs["Signal"]);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void ExtendedRangesAndSubnormalAtrStayNormalized(MovingAvgType kind)
    {
        var max = double.MaxValue;
        var wide = new[] { -max, -max, max, max, -max }.Select((c, i) => B(max, -max, c, i)).ToArray();
        var output = Check(wide, kind); Assert.True(output.Outputs["Vbm"][2] > 0);
        var tiny = Hands.Select(b => B(b.High * double.Epsilon, b.Low * double.Epsilon, b.Close * double.Epsilon)).ToArray();
        Check(tiny, kind); Check(tiny, kind, 1, 65);
        Check(Enumerable.Range(0, 16).Select(i => B(8, -8, i * 5 % 13 - 6, i)).ToArray(), kind, 3, 4);
    }
    [Fact]
    public void ZeroAtrAndFlatPricesPublishZero()
    {
        var output = Check(Enumerable.Range(0, 6).Select(i => B(0, 0, 0, i)).ToArray());
        foreach (var values in output.Outputs.Values) Assert.All(values, v => Assert.Equal(0, v));
        Check(new[] { B(0, 0, 0), B(5, 5, 5), B(-3, -3, -3), B(2, 2, 2) });
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsAllocateOnlyObservedHistory(int length)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), kind, length, length); Check(Hands, kind, length, length); Check(Hands, kind, 1, length); }
    }
    [Fact]
    public void CallbacksPreserveAtrThenSignalOrderingAndShortPadding()
    {
        foreach (var key in Keys)
        {
            var calls = 0;
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { calls++; Assert.Equal(3, period); Assert.Equal(new[] { 2d, 3, 3, 5, 3 }, values); return Enumerable.Repeat(2d, 5).ToArray(); },
                (values, period) => { calls++; Assert.Equal(2, period); Assert.Equal(new[] { 0d, 0, 2, -1, -1 }, values); return new[] { .25 }; }
            });
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeVolatilityBasedMomentumFast(Data(Hands), context, 2, 3, MovingAvgType.SimpleMovingAverage, key);
            Assert.Equal(key == "Vbm" ? new[] { 0d, 0, 2, -1, -1 } : new[] { .25, 0, 0, 0, 0 }, output.ToArray());
            Assert.Equal(key == "Vbm" ? 1 : 2, calls); Assert.Equal(calls, ComponentAverage.Requests); Assert.Equal(calls, ComponentAverage.Substitutions);
        }
        using (ComponentAverage.Arm((v, p) => { Assert.Equal(3, p); return Enumerable.Repeat(2d, 5).ToArray(); }))
        {
            var batch = Data(Hands).CalculateVolatilityBasedMomentum(MovingAvgType.SimpleMovingAverage, 2, 3);
            Assert.Equal(new[] { 0d, 0, 1, .5, -1 }, batch.OutputValues["Signal"]); Assert.Equal(1, ComponentAverage.Requests);
        }
        using (ComponentAverage.Arm((v, p) => new[] { 1d }))
        {
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeVolatilityBasedMomentumFast(Data(Hands), context, 2, 3);
            Assert.All(output.ToArray(), v => Assert.Equal(0, v));
        }
    }
    [Fact]
    public void DirectAndNativeSelectedInputsPreservePerBarRanges()
    {
        var bars = Hands; var prices = new[] { 20d, -10, 5, 2, 100 };
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.VolatilityMomentumValues(projected, MovingAvgType.SimpleMovingAverage, 2, 3, true);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); using var context = new ComputeContext();
        foreach (var key in Keys)
        {
            using var output = IndicatorCompute.ComputeVolatilityBasedMomentumFast(data, context, 2, 3, MovingAvgType.SimpleMovingAverage, key);
            Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(prices, data.ChainedValues);
        }
        data.CalculateVolatilityBasedMomentum(MovingAvgType.SimpleMovingAverage, 2, 3);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        using var state = new VolatilityBasedMomentumState(MovingAvgType.SimpleMovingAverage, 2, 3); ((ICustomInputConsumer)state).ReadCloseAsInput();
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < projected.Length; i++)
            {
                state.Update(Native(B(2, 0, 50)), false, false);
                foreach (var final in new[] { false, true })
                {
                    var point = state.Update(Native(projected[i]), final, true);
                    foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceRangesOrLag()
    {
        using var state = new VolatilityBasedMomentumState(length1: 2, length2: 3); using var control = new VolatilityBasedMomentumState(length1: 2, length2: 3);
        state.Update(Native(Hands[0]), true, true); control.Update(Native(Hands[0]), true, true);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5))
        {
            var fields = new[] { 1d, 1, 1, 1, 1 }; fields[field] = bad;
            var bar = new Bar(DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4]);
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), true, true));
            var data = Data(new[] { bar }); data.SetCustomValues(new List<double> { 10 }); using var context = new ComputeContext();
            Assert.ThrowsAny<ArgumentException>(() => data.CalculateVolatilityBasedMomentum());
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVolatilityBasedMomentumFast(data, context));
        }
        foreach (var bar in Hands.Skip(1)) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
    }
}
