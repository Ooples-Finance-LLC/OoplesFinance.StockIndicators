using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VortexBandsNumericalTests
{
    private static readonly string[] Keys = { "UpperBand", "MiddleBand", "LowerBand" };
    private static Bar B(double close, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), close, close, close, close, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VORTEX", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VortexBands)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void ThreeOutputsMatchIndependentBandConstruction(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VortexBandsOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedPricesFeedEveryFastBand(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions(); var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var kind = (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!;
        var bars = Enumerable.Range(0, 36).Select(i => B(100 + i, i)).ToArray(); var selected = bars.Select((_, i) => (double)(i * 7 % 13 - 6)).ToArray();
        var expected = BuiltInFormulaReferences.VortexBandsValues(selected.Select((v, i) => B(v, i)).ToArray(), length, kind);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var key in Keys)
        {
            using var output = IndicatorCompute.ComputeVortexBandsFast(data, context, length, kind, key);
            Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(selected, data.ChainedValues);
        }
        data.CalculateVortexBands(kind, length);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Empty(data.CustomValuesList);
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 2,
        MovingAvgType kind = MovingAvgType.McNichollMovingAverage)
    {
        var expected = BuiltInFormulaReferences.VortexBandsValues(bars, length, kind);
        var batch = Data(bars).CalculateVortexBands(kind, length);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]);
        Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext();
        foreach (var key in Keys.Concat(new string[] { null! }))
        {
            using var output = IndicatorCompute.ComputeVortexBandsFast(Data(bars), context, length, kind, key);
            Assert.Equal(expected.Outputs[key ?? "UpperBand"], output.ToArray());
        }
        using var state = new VortexBandsState(kind, length); using var kernel = new VortexBandWindow(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); kernel.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                _ = state.Update(Native(B(-17)), false, false); _ = kernel.Next(-17, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var raw = kernel.Next(bars[i].Close, final);
                    Assert.Equal(expected.Outputs["UpperBand"][i], point.Value); Assert.Equal(point.Value, raw.Upper);
                    Assert.Equal(expected.Outputs["MiddleBand"][i], raw.Middle); Assert.Equal(expected.Outputs["LowerBand"][i], raw.Lower);
                    Assert.Equal(expected.Signals[i], raw.Trade);
                    foreach (var key in Keys) { Assert.False(double.IsNaN(point.Outputs![key])); Assert.Equal(expected.Outputs[key][i], point.Outputs[key]); }
                    Assert.True(raw.Upper >= raw.Middle && raw.Middle >= raw.Lower);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentMcNichollAndAbsoluteDeviationHands()
    {
        var result = Check(new[] { B(1), B(3), B(0) });
        Assert.Equal(new[] { 1d, 19d / 4, 13d / 36 }, result.Outputs["UpperBand"]);
        Assert.Equal(new[] { 1d, 7d / 2, -1d / 6 }, result.Outputs["MiddleBand"]);
        Assert.Equal(new[] { 1d, 9d / 4, -25d / 36 }, result.Outputs["LowerBand"]);
        Assert.Equal(new[] { Signal.None, Signal.Buy, Signal.Buy }, result.Signals);
    }
    [Fact]
    public void NegativeSmoothedWidthClampsToZero()
    {
        var result = Check(new[] { B(0), B(1), B(-.25), B(.25) });
        // The fourth raw McNicholl width is -1/48, so every band is 1/4.
        foreach (var key in Keys) Assert.Equal(.25, result.Outputs[key][3]);
        Assert.Equal(new[] { Signal.None, Signal.Buy, Signal.Buy, Signal.None }, result.Signals);
    }
    [Fact]
    public void SubnormalResidualSurvivesBothMeans()
    {
        var result = Check(new[] { B(0), B(double.Epsilon) });
        // Basis=5/4 epsilon, width=5/16 epsilon, bands=15/8 and 5/8 epsilon.
        Assert.Equal(new[] { 0d, 2 * double.Epsilon }, result.Outputs["UpperBand"]);
        Assert.Equal(new[] { 0d, double.Epsilon }, result.Outputs["MiddleBand"]);
        Assert.Equal(new[] { 0d, double.Epsilon }, result.Outputs["LowerBand"]);
        Assert.Equal(Signal.Buy, result.Signals[1]);
    }
    [Fact]
    public void OverflowedMiddleCanStillHaveFiniteLowerBand()
    {
        var result = Check(new[] { B(-double.MaxValue), B(double.MaxValue) });
        // Middle=3/2 MaxValue, width=5/8 MaxValue, lower=1/4 MaxValue.
        Assert.Equal(double.PositiveInfinity, result.Outputs["UpperBand"][1]);
        Assert.Equal(double.PositiveInfinity, result.Outputs["MiddleBand"][1]);
        Assert.Equal(double.MaxValue / 4, result.Outputs["LowerBand"][1]);
        Assert.Equal(Signal.Buy, result.Signals[1]);
        Check(new[] { B(-double.MaxValue), B(double.MaxValue), B(0), B(1), B(-1), B(2) });
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    [InlineData(MovingAvgType.McNichollMovingAverage)]
    public void SupportedMeansPreserveExtremeAndSubnormalBands(MovingAvgType kind)
    {
        Check(Enumerable.Range(0, 28).Select(i => B((i * 7 % 13 - 6) * (double.MaxValue / 8), i)).ToArray(), 3, kind);
        Check(Enumerable.Range(0, 16).Select(i => B((i * 5 % 7 - 3) * double.Epsilon, i)).ToArray(), 3, kind);
        Check(Enumerable.Range(0, 8).Select(i => B(double.MaxValue, i)).ToArray(), 3, kind);
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsUseObservedHistory(int period)
    {
        foreach (var kind in new[] { MovingAvgType.McNichollMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            Check(Array.Empty<Bar>(), period, kind);
            Check(new[] { B(1), B(3), B(0), B(2) }, period, kind);
        }
    }
    [Fact]
    public void BatchAndFastKeepTheirOriginalCallbackBypass()
    {
        using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Vortex direct mean dispatch bypasses these hooks."));
        Check(new[] { B(1), B(3), B(0) });
        Check(new[] { B(1), B(3), B(0) }, kind: MovingAvgType.WeightedMovingAverage);
        Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void InvalidNativeAndOriginalSelectedFieldsCannotAdvanceMeans()
    {
        using var state = new VortexBandsState(length: 2);
        Assert.Equal(1, state.Update(Native(B(1)), true, false).Value);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, 0, bad, 0, 0, 1)), true, false));
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(B(bad)), true, false));
            var data = Data(new[] { B(bad) }); data.SetCustomValues(new List<double> { 20 });
            Assert.ThrowsAny<ArgumentException>(() => data.CalculateVortexBands());
            using var context = new ComputeContext();
            foreach (var key in Keys) Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVortexBandsFast(data, context, outputKey: key));
        }
        var result = state.Update(Native(B(3)), true, true);
        Assert.Equal(19d / 4, result.Value); Assert.Equal(9d / 4, result.Outputs!["LowerBand"]);
    }
}
