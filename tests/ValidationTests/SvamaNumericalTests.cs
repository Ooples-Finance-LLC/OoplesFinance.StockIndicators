using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SvamaNumericalTests
{
    private static Bar[] Bars(double[] prices, double[] volumes) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, volumes[i])).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SVAMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(Svama)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentVolumeRecurrence(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, BuiltInFormulaReferences.SvamaOutputs, IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var bars = Enumerable.Range(0, Math.Max(64, c.Factory().WarmupBars + 8)).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 20, 30, 10, 21 + i % 5, 1 + i % 7)).ToArray();
        var selected = bars.Select((_, i) => (double)(i % 11 - 5)).ToArray(); var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.SvamaValues(projected).Line;
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeSvamaFast(data, context);
        Assert.Equal(expected, actual.ToArray()); Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices); Assert.Equal(bars.Select(b => b.Volume), data.Volumes);
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateSvama().CustomValuesList);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (double[] Line, Signal[] Signals) Check(Bar[] bars, int length = 14)
    {
        var expected = BuiltInFormulaReferences.SvamaValues(bars); var batch = Data(bars).CalculateSvama(length);
        Assert.Equal(expected.Line, batch.OutputValues["Svama"]); Assert.Equal(expected.Line, batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeSvamaFast(Data(bars), context); Assert.Equal(expected.Line, fast.ToArray());
        var native = new SvamaState(length); var direct = new SvamaWindow();
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Bars(new[] { 3d, 9, 4 }, new[] { 4d, 1, 2 })) { native.Update(Native(seed), true, true); direct.Next(seed.Close, seed.Volume, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var alternate = Bars(new[] { -double.MaxValue }, new[] { double.MaxValue })[0]; native.Update(Native(alternate), false, false); direct.Next(alternate.Close, alternate.Volume, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var actual = native.Update(Native(b), final, true); var raw = direct.Next(b.Close, b.Volume, final);
                    Assert.Equal(expected.Line[i], actual.Value); Assert.Equal(expected.Line[i], actual.Outputs!["Svama"]); Assert.Equal(expected.Line[i], raw.Line); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected;
    }
    private static Bar[] Hand() => Bars(new[] { 2d, 4, 2, 8, -2, 6, 1 }, new[] { 2d, 1, 2, 0, 4, 1, 8 });
    [Fact]
    public void IndependentHandUsesRunningMaximumAndZeroVolumeCarry()
    {
        var result = Check(Hand()); Assert.Equal(new[] { 2d, 3, 2, 2, -2, 0, 1 }, result.Line);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.None, Signal.StrongBuy, Signal.None, Signal.StrongBuy, Signal.None }, result.Signals);
        var signed = Check(Bars(new[] { 2d, 4, 2 }, new[] { 1d, -1, 2 })); Assert.Equal(new[] { 2d, 0, 2 }, signed.Line);
    }
    [Fact]
    public void TinyVolumeGainProducesRepresentablePriceContribution()
    {
        var result = Check(Bars(new[] { 0d, double.MaxValue, double.MaxValue }, new[] { double.MaxValue, double.Epsilon, double.Epsilon }));
        Assert.Equal(new[] { 0d, double.Epsilon, 2 * double.Epsilon }, result.Line);
    }
    [Fact]
    public void UnpublishedSubnormalRecurrencePreservesSignalDirection()
    {
        var result = Check(Bars(new[] { 0d, double.Epsilon, 0 }, new[] { 1d, .5, 0 }));
        Assert.Equal(new[] { 0d, 0, 0 }, result.Line); Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell }, result.Signals);
        var ordinary = Check(Bars(new[] { 1d, Math.BitIncrement(1), 1 }, new[] { 1d, .25, 0 })); Assert.Equal(new[] { 1d, 1, 1 }, ordinary.Line); Assert.Equal(result.Signals, ordinary.Signals);
    }
    [Fact]
    public void OverflowingSignedWeightRecoversWithoutReset()
    {
        var result = Check(Bars(new[] { 0d, double.MaxValue, 1, 2 }, new[] { -double.Epsilon, -double.MaxValue, 1, 1 }));
        Assert.Equal(0, result.Line[0]); Assert.True(double.IsPositiveInfinity(result.Line[1])); Assert.Equal(new[] { 1d, 2 }, result.Line.Skip(2));
        var negative = Check(Bars(new[] { 0d, -double.MaxValue, -1, -2 }, new[] { -double.Epsilon, -double.MaxValue, 1, 1 })); Assert.True(double.IsNegativeInfinity(negative.Line[1])); Assert.Equal(new[] { -1d, -2 }, negative.Line.Skip(2));
    }
    [Fact]
    public void ZeroAndNegativeVolumeMaximaKeepDefinedCarry()
    {
        Assert.Equal(new[] { 3d, 3, 3, 3 }, Check(Bars(new[] { 3d, 8, 4, 1 }, new double[4])).Line);
        Assert.Equal(new[] { 3d, 3, 3, 3 }, Check(Bars(new[] { 3d, 8, 4, 1 }, new[] { -1d, 0, -2, 0 })).Line);
        Assert.Equal(new[] { 2d, 6, 6 }, Check(Bars(new[] { 2d, 4, 3 }, new[] { -1d, -2, 0 })).Line);
    }
    [Fact]
    public void SignalAccelerationDistinguishesBuySellAndTies()
    {
        var result = Check(Bars(new[] { 2d, 4, 4, 3, 0, 0, 1 }, new[] { 1d, 0, 0, 0, 0, 0, 0 }));
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.Buy, Signal.StrongSell, Signal.Sell, Signal.Sell }, result.Signals);
    }
    [Fact]
    public void ExtremePricesAndVolumeHistoriesRetainExactRecurrence()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 4 })
        {
            var prices = Enumerable.Range(0, 39).Select(i => (i % 7 - 3) * scale).ToArray();
            foreach (var volumes in new[] { Enumerable.Range(0, 39).Select(i => (double)(1 + i % 5)).ToArray(), Enumerable.Range(0, 39).Select(i => (double)(i % 7 - 3)).ToArray() }) Check(Bars(prices, volumes));
        }
        Check(Bars(Enumerable.Repeat(double.MaxValue, 16).ToArray(), Enumerable.Range(0, 16).Select(i => i == 0 ? -double.Epsilon : -double.MaxValue).ToArray()));
    }
    [Fact]
    public void IgnoredLengthAndEmptyInputsNeedNoHistoryAllocationOrAverageSlots()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => throw new InvalidOperationException("Unexpected average"); using var armed = ComponentAverage.Arm(new[] { callback });
        foreach (var length in new[] { 0, 1, 2, int.MaxValue }) { Check(Hand(), length); Check(Array.Empty<Bar>(), length); } Assert.Equal(0, ComponentAverage.Substitutions);
        Assert.Null(new SvamaState().Update(Native(Hand()[0]), true, false).Outputs);
    }
    [Fact]
    public void NonfiniteInputsCannotAdvanceRunningMaximumOrRecurrence()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            foreach (var invalid in new[] { Bars(new[] { 1d, bad }, new[] { 2d, 3 }), Bars(new[] { 1d, 2 }, new[] { 2d, bad }) })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => Data(invalid).CalculateSvama()); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeSvamaFast(Data(invalid), context));
            }
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                var state = new SvamaState(); var control = new SvamaState(); foreach (var b in Hand()) { state.Update(Native(b), true, true); control.Update(Native(b), true, true); }
                var values = new[] { 1d, 4, 0, 2, 1 }; values[field] = bad; var invalid = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(invalid), final, true));
                foreach (var b in Hand()) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
            }
        }
    }
}
