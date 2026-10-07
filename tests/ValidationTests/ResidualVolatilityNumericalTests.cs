using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ResidualVolatilityNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RWI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(StandardDeviationVolatility)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentProduct(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ResidualVolatilityOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.ResidualVolatilityValues(bars, length, kind);
        var batch = Data(bars).CalculateStandardDeviationVolatility(kind, length);
        foreach (var entry in expected.Outputs)
        {
            Assert.Equal(entry.Value, batch.OutputValues[entry.Key]);
            using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeResidualVolatilityFast(Data(bars), context, length, kind, entry.Key);
            Assert.Equal(entry.Value, fast.ToArray());
        }
        Assert.Equal(expected.Outputs["StdDev"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var state = new StandardDeviationVolatilityState(kind, length); using var direct = new ResidualVolatilityWindow(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var preview = StrengthWindow.Supports(kind) ? -double.MaxValue : -9d;
                state.Update(Native(Bars(preview)[0]), false, false); direct.Next(preview, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var raw = direct.Next(bars[i].Close, final);
                    foreach (var entry in expected.Outputs) Assert.Equal(entry.Value[i], point.Outputs![entry.Key]);
                    Assert.Equal(point.Value, raw.Deviation); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandResidualSquaresAndThreeAverages()
    {
        var result = Check(Bars(2, 4, 2, 8)); var a = Math.Sqrt(2.5); var b = Math.Sqrt(5);
        Assert.Equal(new[] { 0d, 2.5, 1, 5 }, result.Outputs["Variance"]);
        Assert.Equal(new[] { 0d, a, 1, b }, result.Outputs["StdDev"]);
        Assert.Equal(new[] { 0d, a / 2, (a + 1) / 2, (1 + b) / 2 }, result.Outputs["Signal"]);
    }
    [Fact]
    public void ExactSquaredMidpointsDetermineRootRounding()
    {
        var perturbation = MacZWindow.Number.Integer(1).Divide(MacZWindow.Number.Integer(System.Numerics.BigInteger.One << 5000));
        foreach (var lower in new[] { 0d, 1, Math.BitIncrement(1), double.MaxValue })
        {
            var a = MacZWindow.Number.Of(lower);
            var b = lower == double.MaxValue ? MacZWindow.Number.Integer(System.Numerics.BigInteger.One << 1024) : MacZWindow.Number.Of(Math.BitIncrement(lower));
            var midpoint = (a + b).Divide(2); var square = midpoint * midpoint;
            Assert.Equal(0, (ResidualVolatilityWindow.Root(square - perturbation) - a).Sign);
            Assert.Equal(0, (ResidualVolatilityWindow.Root(square + perturbation) - b).Sign);
            var even = (BitConverter.DoubleToInt64Bits(lower) & 1) == 0 ? a : b;
            Assert.Equal(0, (ResidualVolatilityWindow.Root(square) - even).Sign);
        }
    }
    [Fact]
    public void SquaredOverflowDoesNotOverflowFiniteDeviationOrSignal()
    {
        var result = Check(Bars(double.MaxValue, double.MaxValue, 0, -double.MaxValue));
        Assert.True(double.IsPositiveInfinity(result.Outputs["Variance"][1]));
        Assert.True(double.IsFinite(result.Outputs["StdDev"][1])); Assert.True(result.Outputs["StdDev"][1] > 0);
        Assert.True(double.IsFinite(result.Outputs["Signal"][1]));
    }
    [Fact]
    public void SubnormalAndExtendedInputsPreserveCompleteSquares()
    {
        foreach (var kind in Kinds)
        {
            Check(Bars(double.Epsilon, 4 * double.Epsilon, -double.Epsilon, 8 * double.Epsilon, 0, 2 * double.Epsilon), 3, kind);
            Check(Bars(double.MaxValue, -double.MaxValue, double.MaxValue, 0, -double.MaxValue), 3, kind);
            Check(Bars(1, Math.BitIncrement(1), Math.BitDecrement(1), 1, Math.BitIncrement(1)), 3, kind);
        }
    }
    [Fact]
    public void ExpiryAndRecursiveStartupPreservePreviewReset()
    { foreach (var kind in Kinds) Check(Bars(Enumerable.Range(0, 31).Select(i => (double)(i % 3 == 0 ? i % 7 : -i % 11)).ToArray()), 4, kind); }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistory()
    { foreach (var kind in Kinds) foreach (var length in new[] { 1, int.MaxValue }) Check(Bars(2, 4, 2, 8), length, kind); }
    [Fact]
    public void LegacyCompositeAverageFallbackRetainsRootClamp()
    { Check(Bars(2, 4, 2, 8, 1, 0, 0, 0, 3), 3, MovingAvgType.DoubleExponentialMovingAverage); }
    [Fact]
    public void ExplicitFastSelectedInputIsPreserved()
    {
        var bars = Bars(Enumerable.Range(0, 45).Select(i => 20d + i % 7).ToArray()); var selected = bars.Select((_, i) => 1d + i % 11).ToArray();
        var expected = BuiltInFormulaReferences.ResidualVolatilityValues(Bars(selected), 3, MovingAvgType.SimpleMovingAverage);
        foreach (var entry in expected.Outputs)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeResidualVolatilityFast(data, context, 3, outputKey: entry.Key);
            Assert.Equal(entry.Value, actual.ToArray()); Assert.Equal(selected, data.ChainedValues);
        }
    }
    [Fact]
    public void CallbackSlotsRespectRequestedOutputs()
    {
        foreach (var key in new[] { "StdDev", "Variance", "Signal" })
        {
            var calls = new List<double[]>();
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
            { Assert.Equal(2, period); calls.Add(values.ToArray()); return new double[values.Count]; };
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeResidualVolatilityFast(Data(Bars(2, 4, 2, 8)), context, 2, outputKey: key);
            Assert.All(actual.ToArray(), value => Assert.Equal(0, value)); Assert.Equal(key == "Signal" ? 3 : 2, calls.Count);
            Assert.Equal(new[] { 2d, 4, 2, 8 }, calls[0]); Assert.Equal(new[] { 4d, 16, 4, 64 }, calls[1]);
            if (key == "Signal") Assert.Equal(new double[4], calls[2]);
        }
    }
    [Fact]
    public async Task GeneratedApiPreservesThreeIndependentAverageSlots()
    {
        var indicator = new StandardDeviationVolatility(2, 252, new Sma(2), new Wma(2), new Ema(1));
        using var run = await new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(Bars(2, 4, 2, 8))).ConfigureIndicators(indicator).BuildAsync();
        var variance = new[] { 8d / 3, 2, 1, 19d / 3 }; var roots = variance.Select(Math.Sqrt).ToArray();
        Assert.Equal(variance, run[indicator.Outputs[1]].ToArray()); Assert.Equal(roots, run[indicator.Outputs[0]].ToArray());
        Assert.Equal(roots, run[indicator.Outputs[2]].ToArray());
    }
    [Fact]
    public void BatchNeverConsumesFastOverrides()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Batch callback");
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray());
        Assert.Equal(new[] { 0d, 2.5, 1, 5 }, Data(Bars(2, 4, 2, 8)).CalculateStandardDeviationVolatility(length: 2).OutputValues["Variance"]); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void NamedInputAndSelectorUseRequestedSeries()
    {
        var bars = Enumerable.Range(0, 7).Select(i => new Bar(DateTime.UnixEpoch, 2, 5 + i % 3, -1, 3, 1)).ToArray();
        var expected = BuiltInFormulaReferences.ResidualVolatilityValues(Bars(bars.Select(b => b.High).ToArray()), 2, MovingAvgType.SimpleMovingAverage);
        using var named = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, 2, InputName.High);
        using var selected = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, 2, b => b.High);
        for (var i = 0; i < bars.Length; i++)
        { Assert.Equal(expected.Outputs["StdDev"][i], named.Update(Native(bars[i]), true, false).Value); Assert.Equal(expected.Outputs["StdDev"][i], selected.Update(Native(bars[i]), true, false).Value); }
        Assert.Throws<ArgumentNullException>(() => new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, 2, (Func<OhlcvBar, double>)null!));
    }
    [Fact]
    public void NonfiniteCandlesCannotAdvanceAnyStage()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new StandardDeviationVolatilityState(length: 2); using var control = new StandardDeviationVolatilityState(length: 2);
            foreach (var b in Bars(2, 4, 2, 8)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 4, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(5, 1, 6, 0))
            { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); foreach (var entry in expected.Outputs!) Assert.Equal(entry.Value, actual.Outputs![entry.Key]); }
        }
    }
}
