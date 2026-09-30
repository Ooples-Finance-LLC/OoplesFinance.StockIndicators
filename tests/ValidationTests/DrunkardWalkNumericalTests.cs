using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DrunkardWalkNumericalTests
{
    private static Bar B(double high, double low, double close) => new(DateTime.UnixEpoch, close, high, low, close, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DrunkardWalk)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAgeAndRangeStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.DrunkardWalkOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, int ignored = 14)
    {
        var expected = BuiltInFormulaReferences.DrunkardWalkValues(bars, length); var batch = Data(bars).CalculateDrunkardWalk(length, ignored);
        Assert.Empty(batch.CustomValuesList); foreach (var key in new[] { "UpWalk", "DnWalk" }) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var down in new[] { false, true }) { using var output = IndicatorCompute.ComputeDrunkardWalkFast(Data(bars), context, length, ignored, down); Assert.Equal(expected.Outputs[down ? "DnWalk" : "UpWalk"], output.ToArray()); }
        using var state = new DrunkardWalkState(length, ignored); var window = new DrunkardWalkWindow(length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in new[] { B(9, -7, 3), B(6, -4, -2), B(4, -3, 1) }) { state.Update(Native(b), true, false); window.Next(b.High, b.Low, b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(99, -99, -31)), false, false); window.Next(99, -99, -31, false);
                foreach (var final in new[] { false, false, true }) { var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final); Assert.Equal(expected.Outputs["UpWalk"][i], point.Value); Assert.Equal(point.Value, direct.Up); Assert.Equal(expected.Outputs["DnWalk"][i], point.Outputs!["DnWalk"]); Assert.Equal(point.Value, point.Outputs["UpWalk"]); Assert.Equal(expected.Signals[i], direct.Signal); }
            }
        }
        return expected;
    }
    [Fact]
    public void HandAgeRootsTiesAndFlatWindowsStayDefined()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => B(v, v, v)).ToArray();
        Assert.Equal(new[] { 0d, 1, 1.414213562373095 }, Check(bars).Outputs["UpWalk"]);
        Assert.Equal(new[] { 0d, 1, 1.414213562373095 }, Check(bars.Select(b => B(-b.Low, -b.High, -b.Close)).ToArray()).Outputs["DnWalk"]);
        Assert.All(Check(Enumerable.Repeat(B(2, 2, 2), 6).ToArray()).Outputs["UpWalk"], v => Assert.Equal(0, v));
        var ties = new[] { B(7, -3, 2), B(6, -2, 1), B(7, -3, 2), B(5, -1, 0), B(4, 0, 1), B(3, 1, 2), B(3, 1, 2) };
        var result = Check(ties, 4); Assert.Equal(0, result.Outputs["UpWalk"][2]); Assert.Equal(0, result.Outputs["DnWalk"][2]);
        Check(new[] { B(10, 10, 10), B(9, 9, 9), B(9, 9, 9), B(8, 8, 8), B(8, 8, 8) }, 4);
    }
    [Fact]
    public void WidePricesRangesAndRoundedAveragesRecover()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 23).Select(i => B((i % 7 + 1) * scale, -(i % 5 + 1) * scale, (i % 5 - 2) * scale)).ToArray(), 5);
        Check(new[] { B(double.MaxValue, -double.MaxValue, 0), B(double.MaxValue / 2, -double.MaxValue / 2, 0), B(4, -2, 1), B(3, -1, 0), B(2, 0, 1), B(3, -2, 1), B(2, -1, 0) });
        Check(Enumerable.Range(0, 40).Select(i => B(1 + (i % 5) * Math.Pow(2, -52), 1 - (i % 7) * Math.Pow(2, -52), 1)).ToArray(), 8);
    }
    [Fact]
    public void NewExtremeCarriesItsAverageAndRecentTiesResetTheAge()
    {
        var bars = new[] { B(1, 0, .5), B(2, 1, 1.5), B(3, 2, 2.5), B(4, -1, 1), B(3, 0, 2), B(5, -1, 1), B(4, 0, 2), B(3, 1, 2) };
        var result = Check(bars, 5); Assert.Equal(0, result.Outputs["UpWalk"][3]); Assert.Equal(0, result.Outputs["UpWalk"][5]);
        Assert.True(result.Outputs["UpWalk"][6] > 0); Check(bars.Select(b => B(-b.Low, -b.High, -b.Close)).ToArray(), 5);
        Check(Enumerable.Range(0, 20).Select(i => B(30 - i, i, i)).ToArray(), 7);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistoryAndIgnoreLengthTwo()
    {
        var bars = new[] { B(7, -3, 2), B(6, -2, 1), B(7, -3, 2), B(5, -1, 0) };
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Array.Empty<Bar>(), length); Check(bars, length); }
        foreach (var ignored in new[] { int.MinValue, 0, 1, int.MaxValue }) Check(bars, 3, ignored);
    }
    [Fact]
    public void SelectedCandleRangesAndNoCallbackContractArePreserved()
    {
        var bars = Enumerable.Range(0, 14).Select(i => B(7 - i % 3, -3 + i % 2, 2)).ToArray(); var selected = bars.Select((_, i) => i % 2 == 0 ? -11d : 12d).ToArray();
        var calls = 0; using var armed = ComponentAverage.Arm((v, _) => { calls++; return v; }); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateDrunkardWalk(4);
        using var context = new ComputeContext(); foreach (var down in new[] { false, true }) { var data = Data(bars); data.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeDrunkardWalkFast(data, context, 4, down: down); Assert.Equal(batch.OutputValues[down ? "DnWalk" : "UpWalk"], output.ToArray()); Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); }
        Assert.Equal(0, calls);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceEitherRangeAverage()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new DrunkardWalkState(3); using var control = new DrunkardWalkState(3); foreach (var b in new[] { B(7, -3, 2), B(6, -2, 1) }) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in new[] { B(5, -1, 0), B(7, -3, 2) }) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["DnWalk"], actual.Outputs!["DnWalk"]); }
        }
    }
}
