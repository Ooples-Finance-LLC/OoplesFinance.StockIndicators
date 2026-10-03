using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DemandIndexNumericalTests
{
    private static Bar B(double h, double l, double c, double v = 1) => new(DateTime.UnixEpoch, c, h, l, c, v);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DemandIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRoutePreservesRoundedVolumeStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.DemandIndexValues(bars).Outputs, IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(params Bar[] bars)
    {
        var expected = BuiltInFormulaReferences.DemandIndexValues(bars); var batch = Data(bars).CalculateDemandIndex();
        Assert.Equal(expected.Outputs["Di"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Di"], batch.OutputValues["Di"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeDemandIndexFast(Data(bars), context); Assert.Equal(expected.Outputs["Di"], fast.ToArray());
        var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray(); var volume = bars.Select(b => b.Volume).ToArray();
        var inplace = bars.Select(b => b.Close).ToArray(); VolumeCore.DemandIndex(high, low, inplace, volume, inplace); Assert.Equal(expected.Outputs["Di"], inplace);
        var output = new double[bars.Length + 1]; output[^1] = 97; VolumeCore.DemandIndex(high, low, bars.Select(b => b.Close).ToArray(), volume, output); Assert.Equal(expected.Outputs["Di"], output.Take(bars.Length)); Assert.Equal(97, output[^1]);
        var state = new DemandIndexState(); var window = new DemandIndexWindow();
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(B(4, 0, 3)), true, false); window.Next(4, 0, 3, 1, true); state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(9, -7, -3, 0)), false, false); window.Next(9, -7, -3, 0, false);
                foreach (var final in new[] { false, false, true }) { var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, b.Volume, final); Assert.Equal(expected.Outputs["Di"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Di"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Signal); }
            }
        }
        return expected.Outputs["Di"];
    }
    [Fact]
    public void HandSeedsSlopePriorityAndDegenerateVolumeRemainExact()
    {
        Assert.Empty(Check());
        Assert.Equal(new[] { 0d, 2, -0.6666666666666667, 0, 0, -1, 0 }, Check(B(4, 0, 3), B(4, 0, 3), B(4, 0, 1), B(4, 0, 2), B(4, 0, 4), B(4, 0, 0), B(4, 0, 3, 0)));
        Assert.Equal(new[] { 0d, 0, 0, 2 }, Check(B(4, 0, 2), B(2, 2, 2), B(2, 2, 3), B(4, 0, 3, -1)));
        Assert.Equal(new[] { 0d, 1, 2 }, Check(B(4, 0, 2), B(3, 0, 2), B(4, 0, 3)));
        Check(new[] { 2d, 3, 3, 1, 1, 2, 3.5, 3 }.Select(c => B(4, 0, c)).ToArray());
    }
    [Fact]
    public void SubnormalVolumesCannotBeCancelledFromTheFormula()
    {
        Assert.Equal(new[] { 0d, 0, -1, 0, 1 }, Check(B(4, 0, 2), B(4, 0, 3, double.Epsilon), B(4, 0, 1, double.Epsilon), B(4, 0, 2, double.Epsilon), B(4, 0, 3, 3 * double.Epsilon)));
        foreach (var volume in new[] { double.Epsilon, 3 * double.Epsilon, Math.Pow(2, -1022), 1d, double.MaxValue, -double.MaxValue })
            Check(Enumerable.Range(0, 17).Select(i => B(7, -3, i % 11 - 3, volume)).ToArray());
    }
    [Fact]
    public void WideRangesAndTrueOverflowRecoverWithoutNaN()
    {
        Assert.Equal(new[] { 0d, 2, -0.6666666666666667, 0 }, Check(B(double.MaxValue, -double.MaxValue, 0), B(double.MaxValue, -double.MaxValue, double.MaxValue / 2), B(double.MaxValue, -double.MaxValue, -double.MaxValue / 2), B(double.MaxValue, -double.MaxValue, 0)));
        var values = Check(B(4, 0, 2), B(double.Epsilon, -1, 0), B(4, 0, 3), B(4, 0, 1), B(4, 0, 2)); Assert.Equal(double.PositiveInfinity, values[1]); Assert.Equal(2, values[2]); Assert.DoesNotContain(values, double.IsNaN);
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 19).Select(i => B(7 * scale, -3 * scale, (i % 11 - 3) * scale, double.MaxValue)).ToArray());
        Check(B(1, 0, .5), B(1, 0, Math.BitDecrement(1)), B(1, 0, Math.BitIncrement(0)), B(1, 0, Math.BitDecrement(.5)));
    }
    [Fact]
    public void SelectedInputAndObsoletePeriodPreserveTheCandleRule()
    {
        var selected = new[] { 1d, 3, 2, -7, 8, 1 }; var bars = selected.Select(_ => B(4, 0, 2)).ToArray(); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateDemandIndex();
        using var context = new ComputeContext(); foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { var data = Data(bars); data.SetCustomValues(selected.ToList()); using var fast = IndicatorCompute.ComputeDemandIndexFast(data, context, length); Assert.Equal(batch.CustomValuesList, fast.ToArray()); Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); }
    }
    [Fact]
    public void InvalidCandlesCannotSeedOrAdvanceNativeState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var seeded in new[] { false, true }) foreach (var final in new[] { false, true })
        {
            var state = new DemandIndexState(); var control = new DemandIndexState(); if (seeded) { state.Update(Native(B(4, 0, 3)), true, false); control.Update(Native(B(4, 0, 3)), true, false); }
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in new[] { B(4, 0, 3), B(4, 0, 1) }) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
    [Fact]
    public void ShortSpansFailBeforeWritingAnyOutput()
    {
        var two = new[] { 1d, 2 }; var one = new[] { 1d }; var output = new[] { 97d, 97 };
        Assert.Throws<ArgumentException>(() => VolumeCore.DemandIndex(two, two, two, two, one)); Assert.Equal(1, one[0]);
        Assert.Throws<ArgumentException>(() => VolumeCore.DemandIndex(one, two, two, two, output));
        Assert.Throws<ArgumentException>(() => VolumeCore.DemandIndex(two, one, two, two, output));
        Assert.Throws<ArgumentException>(() => VolumeCore.DemandIndex(two, two, two, one, output)); Assert.Equal(new[] { 97d, 97 }, output);
    }
}
