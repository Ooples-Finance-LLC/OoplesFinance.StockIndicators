using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Fraction = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolatilityWaveNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VOLWAVE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolatilityWaveMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void OutputsMatchIndependentPowerReference(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.VolatilityWaveOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.VolatilityWaveBudget);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedPricesFeedBothMeans(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static void Near(double expected, double actual)
    {
        Assert.True(double.IsFinite(actual));
        Assert.True(Math.Abs(expected - actual) <= Math.Max(double.Epsilon, Math.Abs(expected) * 5e-16), $"Expected {expected:R}, actual {actual:R}");
        if (expected != 0) Assert.Equal(Math.Sign(expected), Math.Sign(actual));
    }
    private static double[] Check(double[] prices, MovingAvgType kind = MovingAvgType.WeightedMovingAverage, int length = 2, double factor = 2.5)
    {
        var bars = prices.Select((p, i) => B(p, i)).ToArray();
        var expected = BuiltInFormulaReferences.VolatilityWaveValues(bars, kind, length, factor)["Vwma"];
        var data = Data(bars).CalculateVolatilityWaveMovingAverage(kind, length, factor);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVolatilityWaveMovingAverageFast(Data(bars), context, length, factor, kind);
        using var state = new VolatilityWaveMovingAverageState(kind, length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                Near(expected[i], data.OutputValues["Vwma"][i]); Near(expected[i], fast.Span[i]);
                state.Update(Native(B(91)), false, false);
                Near(expected[i], state.Update(Native(bars[i]), false, true).Value);
                Near(expected[i], state.Update(Native(bars[i]), true, true).Value);
            }
        }
        return data.OutputValues["Vwma"].ToArray();
    }
    [Fact]
    public void IndependentImpulseHandsRetainZeroFilledDenominators()
    {
        // Two-bar WMA A=(2+z)/3; two smoothing passes give 2*A^2-A^3=(16+12z-z^3)/27.
        Assert.Equal(new[] { 16d, 12, 0, -1, 0 }, Check(new[] { 27d, 0, 0, 0, 0 }, factor: 0));
        var hand = Data(new[] { 27d, 0, 0, 0, 0 }.Select((p, i) => B(p, i)).ToArray()).CalculateVolatilityWaveMovingAverage(length: 2, kf: 0);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongSell, Signal.None, Signal.StrongBuy, Signal.None }, hand.SignalsList);
        Assert.Equal(new[] { 16d, 12, 0, -1, 0 }.Select(v => v * double.Epsilon), Check(new[] { 27 * double.Epsilon, 0, 0, 0, 0 }, factor: 0));
        using var wave = new VolatilityWaveWindow(MovingAvgType.WeightedMovingAverage, 2, double.MaxValue);
        Assert.Equal(0, wave.Weighted(0, true).Publish()); Assert.Equal(16, wave.Weighted(17, true).Publish());
        Assert.Equal(17d / 3, wave.Weighted(0, true).Publish());
        Assert.Equal(Check(new[] { -2d, 5, 0, 9 }, factor: 0), Check(new[] { -2d, 5, 0, 9 }, factor: -double.MaxValue));
    }
    [Fact]
    public void FractionalWeightsAndBillionBarSumsHaveIndependentBounds()
    {
        var point = new VolatilityWaveWindow.PowerMean(new Fraction[] { 1, 0 }, 2, 4);
        var actual = VolatilityWaveWindow.Expression.Of(point).Publish(); Near(1 / (1 + Math.Pow(.5, Math.Sqrt(2))), actual);
        // Integral bounds: n/(p+1) <= sum((k/n)^p) <= n/(p+1)+1.
        var large = new VolatilityWaveWindow.PowerMean(new Fraction[] { 1 }, int.MaxValue, 4).Evaluate(96);
        var exponent = Math.Sqrt(2); var lower = 1 / (int.MaxValue / (exponent + 1) + 1); var upper = (exponent + 1) / int.MaxValue;
        Assert.True(large.Lower.Publish() >= lower * (1 - 5e-16)); Assert.True(large.Upper.Publish() <= upper * (1 + 5e-16));
        foreach (var p in new[] { 1, 2, 3, 4 })
        {
            var bounds = UltimatePowerWeights.Sum(7, p, 96); var direct = (Fraction)0;
            for (var j = 1; j <= 7; j++) direct += ((Fraction)j / 7).Pow(p);
            Assert.Equal(0, direct.CompareTo(bounds.Lower)); Assert.Equal(0, direct.CompareTo(bounds.Upper));
        }
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void SignedTinyAndExtremeInputsRetainFractionalMeans(MovingAvgType kind)
    {
        Check(new[] { 97d, 103, 99, 101, 96, 107 }, kind, 3, .15);
        Check(new[] { -2d, 1, 0, 2, -1 }.Select(v => v * double.Epsilon).ToArray(), kind, 5);
        Check(new[] { -1d, 1, 0, -1, 1 }.Select(v => v * (double.MaxValue / 4)).ToArray(), kind, 5);
    }
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ExtremePeriodsAllocateOnlyObservedHistory(int length)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<double>(), kind, length); Check(new[] { 97d, 103, 99 }, kind, length, .15); }
    }
    [Fact]
    public void CoreRegistryAndOverlappingSpansAreCausal()
    {
        var prices = new[] { 97d, 103, 99, 101, 96, 107 }; var expected = Check(prices, length: 5); var output = new double[prices.Length + 1]; output[^1] = 777;
        MovingAverageCore.VolatilityWaveMovingAverage(prices, output, 5); Assert.Equal(expected, output.Take(prices.Length)); Assert.Equal(777, output[^1]);
        var alias = new double[prices.Length + 1]; prices.CopyTo(alias, 0); MovingAverageCore.VolatilityWaveMovingAverage(alias.AsSpan(0, prices.Length), alias.AsSpan(1), 5); Assert.Equal(expected, alias.Skip(1));
        new VwmCore().Compute(prices, output, 5); Assert.Equal(expected, output.Take(prices.Length));
        for (var count = 1; count <= prices.Length; count++)
        { var prefix = new double[count]; MovingAverageCore.VolatilityWaveMovingAverage(prices.AsSpan(0, count), prefix, 5); Assert.Equal(expected.Take(count), prefix); }
        var extended = prices.Concat(new[] { 1e100 }).ToArray(); var longer = new double[extended.Length]; MovingAverageCore.VolatilityWaveMovingAverage(extended, longer, 5); Assert.Equal(expected, longer.Take(prices.Length));
        Assert.Throws<ArgumentException>(() => MovingAverageCore.VolatilityWaveMovingAverage(prices, new double[1]));
        var guard = new[] { 123d, 456 }; Assert.ThrowsAny<ArgumentException>(() => MovingAverageCore.VolatilityWaveMovingAverage(new[] { 1d, double.NaN }, guard)); Assert.Equal(new[] { 123d, 456 }, guard);
        MovingAverageCore.VolatilityWaveMovingAverage(Array.Empty<double>(), Array.Empty<double>());
    }
    [Fact]
    public void TwoFastCallbacksKeepOrderingAndShortReplacements()
    {
        var bars = new[] { B(27), B(0), B(0) }; var calls = 0;
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { calls++; Assert.Equal(2, period); Assert.Equal(new[] { 18d, 9, 0 }, values); return new[] { 3d, 5, 7 }; },
            (values, period) => { calls++; Assert.Equal(2, period); Assert.Equal(new[] { 3d, 5, 7 }, values); return new[] { 1d }; }
        }))
        {
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeVolatilityWaveMovingAverageFast(Data(bars), context, 2, 0);
            Assert.Equal(new[] { 5d, 10, 14 }, output.ToArray()); Assert.Equal(2, calls); Assert.Equal(2, ComponentAverage.Requests);
        }
        using (ComponentAverage.Arm((v, p) => throw new InvalidOperationException("Batch hooks bypassed")))
        { Data(bars).CalculateVolatilityWaveMovingAverage(); Assert.Equal(0, ComponentAverage.Requests); }
    }
    [Fact]
    public void OverflowedLineRetainsSignalsAndRecoversOnLaterBars()
    {
        foreach (var sign in new[] { 1, -1 })
        {
            var m = sign * double.MaxValue; var bars = new[] { m, m, -m, 0, 0 }.Select((v, i) => B(v, i)).ToArray();
            var expected = BuiltInFormulaReferences.VolatilityWaveValues(bars, length: 1)["Vwma"];
            Assert.Equal(sign * double.PositiveInfinity, expected[1]);
            var data = Data(bars).CalculateVolatilityWaveMovingAverage(length: 1);
            using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVolatilityWaveMovingAverageFast(Data(bars), context, 1);
            using var state = new VolatilityWaveMovingAverageState(length: 1);
            for (var i = 0; i < bars.Length; i++)
            {
                var native = state.Update(Native(bars[i]), true, true).Value;
                if (double.IsInfinity(expected[i])) { Assert.Equal(expected[i], data.OutputValues["Vwma"][i]); Assert.Equal(expected[i], fast.Span[i]); Assert.Equal(expected[i], native); }
                else { Near(expected[i], data.OutputValues["Vwma"][i]); Near(expected[i], fast.Span[i]); Near(expected[i], native); }
            }
            Assert.Equal(sign > 0 ? new[] { Signal.StrongBuy, Signal.StrongSell, Signal.StrongSell, Signal.StrongBuy, Signal.StrongSell }
                : new[] { Signal.StrongSell, Signal.StrongBuy, Signal.StrongBuy, Signal.StrongSell, Signal.StrongBuy }, data.SignalsList);
        }
    }
    [Fact]
    public void DirectSelectedBatchAndFastPathsUseTheSuppliedSeries()
    {
        var bars = new[] { 100d, 101, 99, 103 }.Select((p, i) => B(p, i)).ToArray(); var selected = new[] { 1d, -2, 3, -4 };
        var expected = BuiltInFormulaReferences.VolatilityWaveValues(selected.Select((p, i) => B(p, i)).ToArray(), length: 5)["Vwma"];
        var original = BuiltInFormulaReferences.VolatilityWaveValues(bars, length: 5)["Vwma"]; Assert.False(expected.SequenceEqual(original));
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeVolatilityWaveMovingAverageFast(data, context, 5);
        for (var i = 0; i < selected.Length; i++) Near(expected[i], fast.Span[i]); Assert.Equal(selected, data.ChainedValues);
        data.CalculateVolatilityWaveMovingAverage(length: 5);
        for (var i = 0; i < selected.Length; i++) Near(expected[i], data.OutputValues["Vwma"][i]); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    [Fact]
    public void InvalidParametersAndCandlesDoNotAdvanceState()
    {
        using var state = new VolatilityWaveMovingAverageState(); using var control = new VolatilityWaveMovingAverageState();
        state.Update(Native(B(2)), true, false); control.Update(Native(B(2)), true, false);
        using var context = new ComputeContext();
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => new VolatilityWaveMovingAverageState(kf: bad));
            Assert.ThrowsAny<ArgumentException>(() => Data(Array.Empty<Bar>()).CalculateVolatilityWaveMovingAverage(kf: bad));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                var values = new[] { 1d, 1, 1, 1, 1 }; values[field] = bad; var bar = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
                Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), final, true));
                var invalid = Data(new[] { bar }); invalid.SetCustomValues(new List<double> { 1 });
                Assert.ThrowsAny<ArgumentException>(() => invalid.CalculateVolatilityWaveMovingAverage());
                Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVolatilityWaveMovingAverageFast(invalid, context));
            }
        }
        foreach (var price in new[] { 9d, -3, 5 }) Assert.Equal(control.Update(Native(B(price)), true, true).Value, state.Update(Native(B(price)), true, true).Value);
    }
}
