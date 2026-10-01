using System.Reflection;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MorphedSineNumericalTests
{
    private static Bar[] Candles(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("MORPH", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MorphedSineWave)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentPhaseAndRationalOffset(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MorphedSineOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(double[] prices, int length = 14, double power = 100)
    {
        var bars = Candles(prices); var expected = BuiltInFormulaReferences.MorphedSineValues(prices, length, power);
        var batch = Data(bars).CalculateMorphedSineWave(length, power);
        Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Values, batch.OutputValues["Msw"]); Assert.Equal(expected.Signals, batch.SignalsList);
        var core = new double[prices.Length]; OscillatorCore.MorphedSineWave(prices, core, length, power); Assert.Equal(expected.Values, core);
        if (power == 100) { using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeMorphedSineWaveFast(Data(bars), context, length); Assert.Equal(expected.Values, fast.ToArray()); }
        var native = new MorphedSineWaveState(length, power); var direct = new MorphedSineWindow(length, power);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 3; i++) { native.Update(Native(Candles(new[] { 2d })[0]), true, false); direct.Next(2, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Candles(new[] { -99d })[0]), false, false); direct.Next(-99, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var value = direct.Next(prices[i], final);
                    Assert.Equal(expected.Values[i], point.Value); Assert.Equal(point.Value, point.Outputs!["Msw"]); Assert.Equal(point.Value, value.Value); Assert.Equal(expected.Signals[i], value.Trade);
                }
            }
        }
        return expected.Values;
    }
    [Fact]
    public void CardinalPhasesAreExactAndPeriodic()
    {
        var zero = new double[12];
        Assert.Equal(new[] { 0d, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1 }, Check(zero, 4, 1));
        Assert.Equal(zero, Check(zero, 1)); Assert.Equal(zero, Check(zero, 2));
        foreach (var period in new[] { 3, 7, 14, 31 })
        {
            var output = Check(new double[period * 3], period);
            Assert.Equal(output.Take(period), output.Skip(period).Take(period)); Assert.Equal(output.Take(period), output.Skip(2 * period));
        }
    }
    [Fact]
    public void PriceScalingDoesNotOverflowOrEraseSubnormalInput()
    {
        var m = double.MaxValue; var e = double.Epsilon;
        foreach (var power in new[] { 100d, 1, -1, m, -m, e, -e })
        {
            var output = Check(new[] { m, m, -m, -m, e, -e, 0, 1, -1, 0 }, 4, power);
            Assert.Equal(m, output[0]); Assert.Equal(e, output[4]);
            Assert.Equal(e, Check(new[] { e }, 4, power)[0]);
        }
    }
    [Fact]
    public void OverflowingSineOffsetsRecoverWithoutPoisoningSignals()
    {
        var output = Check(new double[9], 4, double.Epsilon);
        Assert.Equal(new[] { 0d, double.PositiveInfinity, 0, double.NegativeInfinity, 0, double.PositiveInfinity, 0, double.NegativeInfinity, 0 }, output);
    }
    [Fact]
    public void ExtremePeriodsAndMaximumPhaseRemainBounded()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(new[] { 1d, 2, -1, 0 }, length); Check(Array.Empty<double>(), length); }
        var window = new MorphedSineWindow(int.MaxValue, 100);
        typeof(MorphedSineWindow).GetField("_phase", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, int.MaxValue - 1);
        var first = BuiltInFormulaReferences.MorphedSineValues(new double[2], int.MaxValue, 100).Values[1];
        Assert.Equal(-first, window.Next(0, false).Value); Assert.Equal(-first, window.Next(0, true).Value);
        Assert.Equal(0, window.Next(0, true).Value); Assert.Equal(first, window.Next(0, true).Value);
    }
    [Fact]
    public void CoreSupportsExactInPlaceAndValidatesBeforeWriting()
    {
        var prices = new[] { 0d, double.MaxValue, -double.MaxValue, double.Epsilon, -1, 2 };
        var expected = BuiltInFormulaReferences.MorphedSineValues(prices, 4, 100).Values;
        var inPlace = prices.ToArray(); OscillatorCore.MorphedSineWave(inPlace, inPlace, 4); Assert.Equal(expected, inPlace);
        var tail = Enumerable.Repeat(91d, prices.Length + 2).ToArray(); OscillatorCore.MorphedSineWave(prices, tail, 4);
        Assert.Equal(expected, tail.Take(prices.Length)); Assert.Equal(new[] { 91d, 91 }, tail.Skip(prices.Length));
        Assert.Throws<ArgumentException>(() => OscillatorCore.MorphedSineWave(prices, new double[2], 4));
        var unchanged = new[] { 91d, 92 }; Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.MorphedSineWave(new[] { 1d, double.NaN }, unchanged, 4)); Assert.Equal(new[] { 91d, 92 }, unchanged);
        foreach (var power in new[] { 0d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MorphedSineWaveState(power: power));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Candles(new[] { 1d })).CalculateMorphedSineWave(power: power));
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.MorphedSineWave(new[] { 1d, 2 }, unchanged, power: power)); Assert.Equal(new[] { 91d, 92 }, unchanged);
        }
    }
    [Fact]
    public void RejectedNativeBarCannotAdvancePhase()
    {
        var actual = new MorphedSineWaveState(4); var control = new MorphedSineWaveState(4);
        var seed = Candles(new[] { 0d })[0]; actual.Update(Native(seed), true, false); control.Update(Native(seed), true, false);
        var bad = new Bar(seed.Time, double.NaN, 1, -1, 0, 1);
        foreach (var final in new[] { false, true }) Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(bad), final, false));
        Assert.Equal(control.Update(Native(seed), true, true).Value, actual.Update(Native(seed), true, true).Value);
    }
}
