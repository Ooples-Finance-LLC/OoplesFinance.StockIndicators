using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VaradiNumericalTests
{
    private static Bar B(double close, double high = 2, double low = 0, int i = 0)
        => new(DateTime.UnixEpoch.AddMinutes(i), close, high, low, close, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VARADI", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VaradiOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentExactRanks(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VaradiOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedInputUsesProjectedRanges(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var kind = (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!;
        var bars = Enumerable.Range(0, 32).Select(i => B(100 + i, 104 + i, 96 + i, i)).ToArray();
        var selected = bars.Select((_, i) => (double)(i * 11 % 17 - 8)).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.VaradiValues(projected, length, kind, selected: true);
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVaradiOscillatorFast(data, context, length, kind);
        Assert.Equal(expected.Outputs["Vo"], fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
        data.CalculateVaradiOscillator(kind, length);
        Assert.Equal(expected.Outputs["Vo"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    private static double[] Check(Bar[] bars, int length = 1, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.VaradiValues(bars, length, kind);
        var batch = Data(bars).CalculateVaradiOscillator(kind, length);
        Assert.Equal(expected.Outputs["Vo"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVaradiOscillatorFast(Data(bars), context, length, kind);
        Assert.Equal(expected.Outputs["Vo"], fast.ToArray());
        var core = Enumerable.Repeat(-123d, bars.Length + 2).ToArray();
        OscillatorCore.VaradiOscillator(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(), core.AsSpan(1, bars.Length), length, kind);
        Assert.Equal(-123, core[0]); Assert.Equal(-123, core[core.Length - 1]);
        Assert.Equal(expected.Outputs["Vo"], core.Skip(1).Take(bars.Length));
        using var state = new VaradiOscillatorState(kind, length); using var kernel = new VaradiWindow(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(B(-5, 7, -9)), true, false); kernel.Next(-5, 7, -9, true);
            state.Reset(); kernel.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(-100, 100, -101)), false, false); kernel.Next(-100, 100, -101, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    var value = kernel.Next(bars[i].Close, bars[i].High, bars[i].Low, final);
                    Assert.Equal(expected.Outputs["Vo"][i], point.Value);
                    Assert.Equal(point.Value, point.Outputs!["Vo"]); Assert.Equal(point.Value, value.Value);
                    Assert.Equal(expected.Signals[i], value.Trade);
                }
            }
        }
        return expected.Outputs["Vo"];
    }
    [Fact]
    public void AdjacentValuesAreDistinctAndTrueTiesRemainInclusive()
    {
        Assert.Equal(new[] { 100d, 0 }, Check(new[] { B(1), B(Math.BitDecrement(1d)) }));
        Assert.Equal(new[] { 100d, 100 }, Check(new[] { B(1), B(1) }));
        Assert.Equal(new[] { 100d, 0, 100, 0 }, Check(new[] { B(1), B(0), B(1), B(0) }));
    }
    [Fact]
    public void MidpointOverflowAndUnderflowCannotChangeTheRank()
    {
        Assert.Equal(new[] { 100d, 100 }, Check(new[] { B(1), B(double.MaxValue, double.MaxValue, double.MaxValue) }));
        Assert.Equal(new[] { 100d, 100 }, Check(new[] { B(1), B(double.Epsilon, double.Epsilon, 0) }));
        Assert.Equal(new[] { 100d, 0 }, Check(new[] { B(double.Epsilon), B(double.Epsilon, double.MaxValue, 0) }));
        Assert.Equal(new[] { 100d, 0 }, Check(new[] { B(1), B(0, 1, -1) }));
        // The changing low makes the midpoint ratio fall despite a rising close.
        Assert.Equal(new[] { 100d, 0 }, Check(new[] { B(1), B(3, 4, 3) }));
        // Finite signed candles can produce ratios outside binary64 before ranking.
        Check(new[] { B(double.MaxValue, double.Epsilon, 0), B(double.MaxValue / 2, double.Epsilon, 0) });
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void StandardMeansRankExactFractions(MovingAvgType kind)
    {
        var bars = Enumerable.Range(0, 80).Select(i => B(i % 5 == 0 ? 1 : Math.BitDecrement(1d), 2 + i % 3, 0, i)).ToArray();
        Check(bars, 3, kind);
        // A tiny positive term next to MaxValue distinguishes descending means
        // from exact ties. Even extended fixed precision can erase this term.
        var extreme = new[] { B(double.Epsilon), B(double.MaxValue),
            B(kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage ? 0 : double.MaxValue / 2),
            B(kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage ? double.MaxValue : double.MaxValue / 2) };
        var expected = kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage
            ? new[] { 50d, 100, 50, 50 } : new[] { 50d, 100, 50, 0 };
        Assert.Equal(expected, Check(extreme, 2, kind));
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsUseOnlyObservedHistory(int length)
    {
        Check(Array.Empty<Bar>(), length);
        var values = Check(new[] { B(1), B(0), B(-1), B(1) }, length);
        if (length == int.MaxValue)
            Assert.Equal(new[] { 100d / int.MaxValue, 200d / int.MaxValue, 300d / int.MaxValue, 400d / int.MaxValue }, values);
    }
    [Fact]
    public void FastCallbackAndBatchBypassKeepTheirExistingSlots()
    {
        var bars = new[] { B(1, 4), B(2, 4), B(3, 4), B(4, 4) };
        using var armed = ComponentAverage.Arm((source, period) =>
        { Assert.Equal(2, period); Assert.Equal(new[] { .5, 1, 1.5, 2 }, source); return new[] { 2d, 1 }; });
        var batch = Data(bars).CalculateVaradiOscillator(length: 2);
        Assert.Equal(0, ComponentAverage.Requests);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVaradiOscillatorFast(Data(bars), context, 2);
        Assert.Equal(new[] { 50d, 50, 0, 50 }, fast.ToArray());
        Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions);
        Assert.Equal(new[] { 50d, 100, 100, 100 }, batch.CustomValuesList);
    }
    [Fact]
    public void CorePriceOnlyUsesFlatCandlesAndRejectsInvalidSpansAtomically()
    {
        var prices = new[] { 1d, 0, -1, 2 }; var output = new double[prices.Length];
        OscillatorCore.VaradiOscillator(prices, output, 2);
        Assert.Equal(Check(prices.Select(p => B(p, p, p)).ToArray(), 2), output);
        OscillatorCore.VaradiOscillator(Array.Empty<double>(), Array.Empty<double>());
        var guard = new[] { 123d, 456 };
        Assert.Throws<ArgumentException>(() => OscillatorCore.VaradiOscillator(new[] { 1d, 2 }, new[] { 1d }, new[] { 0d, 0 }, guard));
        Assert.Throws<ArgumentException>(() => OscillatorCore.VaradiOscillator(new[] { 1d, 2 }, new double[1]));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 3))
        {
            var fields = new[] { new[] { 1d, 2 }, new[] { 2d, 3 }, new[] { 0d, 1 } }; fields[field][1] = bad;
            Assert.ThrowsAny<ArgumentException>(() => OscillatorCore.VaradiOscillator(fields[0], fields[1], fields[2], guard));
            Assert.Equal(new[] { 123d, 456 }, guard);
        }
    }
    [Fact]
    public void InvalidNativeAndOriginalSelectedCandlesAreRejectedBeforeMutation()
    {
        using var state = new VaradiOscillatorState(length: 1);
        Assert.Equal(100, state.Update(Native(B(1)), true, false).Value);
        var bad = new Bar(DateTime.UnixEpoch, 1, 2, 0, 0, double.NaN);
        Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bad), true, false));
        Assert.Equal(0, state.Update(Native(B(Math.BitDecrement(1d))), true, false).Value);
        var data = Data(new[] { B(1, double.NaN, 0) }); data.SetCustomValues(new List<double> { 20 });
        Assert.ThrowsAny<ArgumentException>(() => data.CalculateVaradiOscillator());
        using var context = new ComputeContext();
        Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVaradiOscillatorFast(data, context));
    }
}
