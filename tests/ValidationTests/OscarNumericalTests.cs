using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class OscarNumericalTests
{
    private static Bar[] Bars(double[] prices, double[]? high = null, double[]? low = null) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, high?[i] ?? p, low?[i] ?? p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("OSCAR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(OscarIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWeightedSum(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.OscarOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.OscarBudget);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Equal(double expected, double actual)
    {
        Assert.True(double.IsFinite(actual)); Assert.InRange(actual, 0, 40);
        if (expected == 0) Assert.Equal(expected, actual);
        else { Assert.Equal(Math.Sign(expected), Math.Sign(actual)); Assert.True(Math.Abs((actual - expected) / expected) <= 4e-15, $"Expected {expected:R}, actual {actual:R}"); }
    }
    private static double[] Check(Bar[] bars, int length = 3)
    {
        var expected = BuiltInFormulaReferences.OscarValues(bars, length);
        var batch = Data(bars).CalculateOscarIndicator(length);
        var output = Enumerable.Repeat(-123d, bars.Length + 2).ToArray();
        OscillatorCore.OscarIndicator(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), output.AsSpan(1, bars.Length), length);
        Assert.Equal(-123, output[0]); Assert.Equal(-123, output[output.Length - 1]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeOscarIndicatorFast(Data(bars), context, length); var fastValues = fast.ToArray();
        using var state = new OscarIndicatorState(length); using var window = new OscarWindow(length);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { -7d, 9, 2 })) { state.Update(Native(b), true, false); window.Next(b.Close, b.High, b.Low, true); }
            state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                Equal(expected[i], batch.CustomValuesList[i]); Equal(expected[i], batch.OutputValues["Oscar"][i]); Equal(expected[i], output[i + 1]); Equal(expected[i], fastValues[i]);
                var decoy = Bars(new[] { 100d })[0]; state.Update(Native(decoy), false, false); window.Next(100, 100, 100, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var kernel = window.Next(bars[i].Close, bars[i].High, bars[i].Low, final);
                    Equal(expected[i], point.Value); Equal(expected[i], point.Outputs!["Oscar"]); Equal(expected[i], kernel.Value); Assert.Equal(batch.SignalsList[i], kernel.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentHandValuesAndSignals()
    {
        var bars = Bars(new[] { 2d, 4, 2 }, new[] { 3d, 5, 3 }, new[] { 1d, 3, 1 });
        var values = Check(bars, 1); var hands = new[] { 50d / 3, 175d / 9, 1075d / 54 };
        for (var i = 0; i < hands.Length; i++) Equal(hands[i], values[i]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.Buy }, Data(bars).CalculateOscarIndicator(1).SignalsList);
        var turns = Bars(new[] { 1d, 0, 1, 0 }, Enumerable.Repeat(1d, 4).ToArray(), new double[4]);
        Check(turns, 1); Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongSell, Signal.StrongBuy, Signal.StrongSell }, Data(turns).CalculateOscarIndicator(1).SignalsList);
    }
    [Fact]
    public void OppositeExtremeBoundsAndSubnormalRanges()
    {
        foreach (var scale in new[] { double.MaxValue, 1d, double.Epsilon })
        {
            var values = Check(Bars(new[] { 0d, scale, -scale, 0d }, Enumerable.Repeat(scale, 4).ToArray(), Enumerable.Repeat(-scale, 4).ToArray()), 2);
            Equal(50d / 3, values[0]);
        }
        Check(Bars(new[] { double.Epsilon, 1d, 0d }, new[] { double.MaxValue, 1d, 0d }, new double[3]), 1);
    }
    [Fact]
    public void ClampAndExactZeroRangePreserveFormula()
    {
        Assert.All(Check(Bars(new[] { 0d, 0, 0 }), 1), v => Assert.Equal(0, v));
        var values = Check(Bars(new[] { -2d, 2, 0d }, new[] { 1d, 1, 1 }, new double[3]), 1);
        Assert.Equal(0, values[0]); Equal(100d / 3, values[1]);
        var perturbation = Check(Bars(new[] { 1d }, new[] { Math.BitIncrement(1d) }, new[] { 1d }), 1); Assert.Equal(0, perturbation[0]);
        Equal(100d / 3, Check(Bars(new[] { Math.BitIncrement(1d) }, new[] { Math.BitIncrement(1d) }, new[] { 1d }), 1)[0]);
        Check(Bars(new[] { 0d, 1 }, new[] { -1d, -2 }, new[] { 1d, 2 }), 1);
    }
    [Fact]
    public void ExpiryPreviewResetAndLazyExtremePeriods()
    {
        var bars = Bars(new[] { -9d, 7, 2, 3, 1, 5, -1, 4, 0 });
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue }) { Check(Array.Empty<Bar>(), length); Check(bars, length); }
        var prefix = Data(bars.Take(5).ToArray()).CalculateOscarIndicator(3).CustomValuesList;
        Assert.Equal(prefix, Data(bars).CalculateOscarIndicator(3).CustomValuesList.Take(5));
    }
    [Fact]
    public void CoreSpanAndInvalidInputsAreAtomic()
    {
        var output = new[] { 123d, 456d };
        Assert.Throws<ArgumentException>(() => OscillatorCore.OscarIndicator(new[] { 1d, 2 }, new[] { 1d }, new[] { 1d, 2 }, output));
        Assert.Throws<ArgumentException>(() => OscillatorCore.OscarIndicator(new[] { 1d, 2 }, new[] { 1d, 2 }, new[] { 1d }, output));
        Assert.Throws<ArgumentException>(() => OscillatorCore.OscarIndicator(new[] { 1d, 2 }, new[] { 1d, 2 }, new[] { 1d, 2 }, new double[1]));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 3))
        {
            var values = new[] { new[] { 1d, 2 }, new[] { 1d, 2 }, new[] { 1d, 2 } }; values[field][1] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.OscarIndicator(values[0], values[1], values[2], output));
            Assert.Equal(new[] { 123d, 456d }, output);
            var bars = Bars(values[0], values[1], values[2]); Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateOscarIndicator());
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeOscarIndicatorFast(Data(bars), context));
        }
    }
    [Fact]
    public void RejectedCandlesDoNotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new OscarIndicatorState(2); using var control = new OscarIndicatorState(2);
            foreach (var b in Bars(new[] { 1d, 4, 2 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, true));
            foreach (var b in Bars(new[] { 7d, -1, 0, 3 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
