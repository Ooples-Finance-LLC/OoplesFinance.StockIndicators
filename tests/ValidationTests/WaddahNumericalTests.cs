using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WaddahNumericalTests
{
    private static readonly string[] Keys = { "T1", "T2", "E1", "TrendUp", "TrendDn" };
    private static Bar B(double p, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("WADDAH", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(WaddahAttarExplosion)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void FiveOutputsMatchFourIndependentLaggedMacds(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.WaddahOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedPricesRetainOriginalFields(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions(); var fast = (int)options.GetType().GetProperty("FastLength")!.GetValue(options)!;
        var slow = (int)options.GetType().GetProperty("SlowLength")!.GetValue(options)!; var sensitivity = (double)options.GetType().GetProperty("Sensitivity")!.GetValue(options)!;
        var bars = Enumerable.Range(0, 36).Select(i => B(100 + i, i)).ToArray(); var selected = bars.Select((_, i) => (double)(i * 7 % 13 - 6)).ToArray();
        var expected = BuiltInFormulaReferences.WaddahValues(selected.Select((v, i) => B(v, i)).ToArray(), fast, slow, sensitivity);
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext();
        foreach (var key in Keys)
        {
            using var output = IndicatorCompute.ComputeWaddahAttarExplosionFast(data, context, fast, slow, sensitivity, key);
            Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(selected, data.ChainedValues);
        }
        data.CalculateWaddahAttarExplosion(fast, slow, sensitivity);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Empty(data.CustomValuesList); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int fast = 2, int slow = 3, double sensitivity = 6)
    {
        var expected = BuiltInFormulaReferences.WaddahValues(bars, fast, slow, sensitivity); var data = Data(bars).CalculateWaddahAttarExplosion(fast, slow, sensitivity);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Empty(data.CustomValuesList);
        using var context = new ComputeContext();
        foreach (var key in Keys.Concat(new string[] { null! }))
        {
            using var output = IndicatorCompute.ComputeWaddahAttarExplosionFast(Data(bars), context, fast, slow, sensitivity, key);
            Assert.Equal(expected.Outputs[key ?? "T1"], output.ToArray());
        }
        using var native = new WaddahAttarExplosionState(fast, slow, sensitivity); var kernel = new WaddahWindow(fast, slow, sensitivity);
        for (var replay = 0; replay < 2; replay++)
        {
            native.Reset(); kernel.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                _ = native.Update(Native(B(-17)), false, false); _ = kernel.Next(-17, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var raw = kernel.Next(bars[i].Close, final);
                    Assert.Equal(expected.Outputs["T1"][i], point.Value);
                    foreach (var key in Keys) { Assert.False(double.IsNaN(point.Outputs![key])); Assert.Equal(expected.Outputs[key][i], point.Outputs[key]); }
                    Assert.Equal(expected.Outputs["T1"][i], raw.T1); Assert.Equal(expected.Outputs["T2"][i], raw.T2);
                    Assert.Equal(expected.Outputs["E1"][i], raw.E1); Assert.Equal(expected.Outputs["TrendUp"][i], raw.Up); Assert.Equal(expected.Outputs["TrendDn"][i], raw.Down);
                    Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentLaggedMacdAndWidthHands()
    {
        var result = Check(new[] { B(1), B(3), B(0), B(4), B(2) });
        Assert.Equal(new[] { 0d, 0, -9, 1, -11d / 3 }, result.Outputs["T1"]);
        Assert.Equal(new[] { 0d, 0, 2, 7d / 3, -61d / 18 }, result.Outputs["T2"]);
        Assert.Equal(new[] { 0d, 4, 6, 8, 4 }, result.Outputs["E1"]);
        Assert.Equal(new[] { Signal.None, Signal.Sell, Signal.Sell, Signal.Sell, Signal.Sell }, result.Signals);
        Assert.Equal(Signal.Buy, Check(new[] { B(1), B(3), B(0), B(4) }, sensitivity: 150).Signals[3]);
    }
    [Fact]
    public void ScaleBeforeRoundingSubnormalDeviation()
    {
        var result = Check(new[] { B(0), B(double.Epsilon) });
        // Population deviation is epsilon/2; the published width is 2epsilon.
        Assert.Equal(2 * double.Epsilon, result.Outputs["E1"][1]); Assert.Equal(Signal.Sell, result.Signals[1]);
    }
    [Fact]
    public void SignalThresholdsUseExactRootComparisons()
    {
        var bars = new[] { B(1), B(3), B(0), B(4) };
        Assert.Equal(Signal.None, Check(bars, sensitivity: 48).Signals[3]);
        Assert.Equal(Signal.Buy, Check(bars, sensitivity: Math.BitIncrement(48)).Signals[3]);
        Assert.Equal(Signal.Sell, Check(bars, sensitivity: Math.BitDecrement(48)).Signals[3]);
    }
    [Fact]
    public void TinySensitivityRecoversOverflowedPriceDifferences()
    {
        var result = Check(new[] { B(-double.MaxValue), B(double.MaxValue), B(0), B(1), B(-1), B(2) }, sensitivity: 1e-308);
        Assert.True(double.IsFinite(result.Outputs["T1"][2])); Assert.True(result.Outputs["T1"][2] < 0);
        Assert.Equal(double.PositiveInfinity, result.Outputs["E1"][1]);
    }
    [Theory]
    [InlineData(0d)] [InlineData(-6d)] [InlineData(double.Epsilon)] [InlineData(double.MaxValue)]
    public void FiniteSensitivitiesPreserveSignsAndCancellation(double sensitivity)
    {
        Check(Enumerable.Range(0, 20).Select(i => B((i * 7 % 11 - 5) * (double.MaxValue / 8), i)).ToArray(), sensitivity: sensitivity);
        Check(Enumerable.Range(0, 12).Select(i => B((i * 3 % 7 - 3) * double.Epsilon, i)).ToArray(), sensitivity: sensitivity);
        Check(Enumerable.Range(0, 8).Select(i => B(double.MaxValue, i)).ToArray(), sensitivity: sensitivity);
    }
    [Theory]
    [InlineData(int.MaxValue)] [InlineData(int.MinValue)]
    public void ExtremePeriodsUseObservedHistory(int length)
    {
        var bars = new[] { B(1), B(3), B(0), B(4) };
        Check(Array.Empty<Bar>(), length, length); Check(bars, length, 3); Check(bars, 2, length); Check(bars, length, length);
    }
    [Fact]
    public void BatchRetainsOriginalCallbackBypass()
    {
        using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Batch MACD uses direct means."));
        Check(new[] { B(1), B(3), B(0), B(4) });
        Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void InvalidInputsCannotAdvanceCommittedState()
    {
        using var state = new WaddahAttarExplosionState(2, 3, 6); state.Update(Native(B(1)), true, false);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => new WaddahAttarExplosionState(sensitivity: bad));
            Assert.ThrowsAny<ArgumentException>(() => Data(new[] { B(1) }).CalculateWaddahAttarExplosion(sensitivity: bad));
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, 0, bad, 0, 0, 1)), true, false));
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(B(bad)), true, false));
            var data = Data(new[] { B(bad) }); data.SetCustomValues(new List<double> { 20 });
            Assert.ThrowsAny<ArgumentException>(() => data.CalculateWaddahAttarExplosion());
        }
        Assert.Equal(4d, state.Update(Native(B(3)), true, true).Outputs!["E1"]);
    }
}
