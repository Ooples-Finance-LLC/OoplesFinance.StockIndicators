using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
using Radical = OoplesFinance.StockIndicators.Helpers.VolatilityAverageWindow.Radical;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolatilityAverageNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static Bar[] Hands => new[] { 0d, 2, 4, 2, 0, 0 }.Select((p, i) => B(p, i)).ToArray();
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("VOLMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolatilityMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void OutputsMatchIndependentRadicalReference(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.VolatilityAverageOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedPriceFeedsAdaptivePeriods(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals, int[] Periods) Check(Bar[] bars,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int length = 10, int lookback = 2, int smooth = 2)
    {
        var expected = BuiltInFormulaReferences.VolatilityAverageValues(bars, kind, length, lookback, smooth);
        var data = Data(bars).CalculateVolatilityMovingAverage(kind, length, lookback, smooth);
        Assert.Equal(expected.Outputs["Vma"], data.OutputValues["Vma"]); Assert.Equal(expected.Outputs["Vma"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeVolatilityMovingAverageFast(Data(bars), context, length, kind, lookback, smooth);
        Assert.Equal(expected.Outputs["Vma"], output.ToArray());
        using var state = new VolatilityMovingAverageState(kind, length, lookback, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(B(-3)), true, false); state.Update(Native(B(7)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(99)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected.Outputs["Vma"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Vma"]);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandCancellationSelectsLongWindowAndExactSignalTies()
    {
        // Scores=[0,100,100,-100,-100,0]; SMA2 gives [0,50,100,0,-100,-50].
        var result = Check(Hands);
        Assert.Equal(new[] { 10, 1, 1, 10, 1, 1 }, result.Periods);
        Assert.Equal(new[] { 0d, 1, 3, 146d / 55, 36d / 55, 0 }, result.Outputs["Vma"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.StrongSell, Signal.Sell, Signal.None }, result.Signals);
    }
    [Fact]
    public void ScoreScaleChangesSelectedPeriod()
    {
        // EMA startup mean([0,2])=1 and population deviation=1, giving score=100.
        // With lookback=20, level=5 and period=5; zero-filled WMA gives 2*5/15=2/3.
        // Halving the score gives level=roundEven(2.5)=2, period=8 and output=4/9.
        var result = Check(new[] { B(0), B(2, 1) }, MovingAvgType.ExponentialMovingAverage, 10, 20, 1);
        Assert.Equal(new[] { 10, 5 }, result.Periods);
        Assert.Equal(new[] { 0d, 2d / 3 }, result.Outputs["Vma"]);
    }
    [Fact]
    public void SquareClassCancellationAndSubnormalPerturbationsResolveHalfEvenTies()
    {
        // sqrt(2) - 2/sqrt(2) is exactly zero; the smallest binary64 displacement
        // from a period boundary must survive cancellation and adaptive refinement.
        var zero = Radical.QuotientRoot(Number.Of(1), Number.Of(.5)) + Radical.QuotientRoot(Number.Of(-2), Number.Of(2));
        Assert.Equal(0, zero.Compare(default));
        var tie = zero + Radical.Of(Number.Of(50)); var epsilon = Radical.Of(Number.Of(double.Epsilon));
        Assert.Equal(8, VolatilityAverageWindow.Period(tie, 10, 20));
        Assert.Equal(7, VolatilityAverageWindow.Period(tie + epsilon, 10, 20));
        Assert.Equal(8, VolatilityAverageWindow.Period(tie - epsilon, 10, 20));
        Assert.Equal(7, VolatilityAverageWindow.Period((tie + epsilon).Times(Number.Of(-1)), 10, 20));
        Assert.Equal(4, VolatilityAverageWindow.Period(Radical.Of(Number.Of(20)), 5, 20));
        Assert.Equal(4, VolatilityAverageWindow.Period(Radical.Of(Number.Of(60)), 5, 20));
        var beyondPrecision = Number.Of(2) + Number.Of(double.Epsilon);
        var nearRoot = Radical.QuotientRoot(beyondPrecision, beyondPrecision) - Radical.QuotientRoot(Number.Of(2), Number.Of(2));
        Assert.Equal(1, nearRoot.Compare(default)); Assert.Equal(-1, nearRoot.Times(Number.Of(-1)).Compare(default));
        Assert.Equal(7, VolatilityAverageWindow.Period(tie + nearRoot, 10, 20));
        Assert.Equal(8, VolatilityAverageWindow.Period(tie - nearRoot, 10, 20));
        var equivalent = Radical.QuotientRoot(Number.Of(3362), Number.Of(3362)) - Radical.QuotientRoot(Number.Of(2), Number.Of(2)).Times(Number.Of(41));
        Assert.Equal(0, equivalent.Compare(default));
        var referenceVariance = new ReferenceFraction(2) + ReferenceFraction.FromDouble(double.Epsilon);
        var referenceNear = VolatilityScoreOracle.DivideRoot(referenceVariance, referenceVariance).Plus(VolatilityScoreOracle.DivideRoot(new(-2), new(2)));
        Assert.Equal(1, referenceNear.Compare(new(0)));
        var reference = VolatilityScoreOracle.DivideRoot(new(1), ReferenceFraction.FromDouble(.5)).Plus(VolatilityScoreOracle.DivideRoot(new(-2), new(2)));
        Assert.Equal(0, reference.Compare(new(0)));
        Assert.Equal(1, reference.Plus(VolatilityScoreOracle.Of(ReferenceFraction.FromDouble(double.Epsilon))).Compare(new(0)));
        foreach (var value in new[] { double.Epsilon, double.MaxValue })
        { var number = Number.Of(value); Assert.Equal(0, Radical.QuotientRoot(number, number * number).Compare(Number.Of(1))); }
        var tinySum = Radical.QuotientRoot(Number.Of(double.Epsilon), Number.Of(.5)) - Radical.QuotientRoot(Number.Of(double.Epsilon), Number.Of(1d / 3));
        Assert.Equal(-1, tinySum.Compare(default)); Assert.Equal(1, tinySum.Times(Number.Of(-1)).Compare(default));
        Assert.Equal(Math.Sqrt(2), Radical.QuotientRoot(Number.Of(1), Number.Of(.5)).Publish());
        var irrational = Radical.QuotientRoot(Number.Of(1), Number.Of(.5)) - Radical.QuotientRoot(Number.Of(1), Number.Of(1d / 3));
        Assert.Equal(-1, irrational.Compare(default));
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void ExtremeSignedAndSubnormalPricesRetainPeriods(MovingAvgType kind)
    {
        var max = double.MaxValue;
        Check(new[] { -max, max, 0, -max, max, max }.Select((p, i) => B(p, i)).ToArray(), kind, 5, 3, 2);
        Check(Hands.Select(b => B(b.Close * double.Epsilon)).ToArray(), kind);
        Check(Enumerable.Range(0, 18).Select(i => B(i * 7 % 11 - 5, i)).ToArray(), kind, 7, 3, 4);
        Check(new[] { B(1), B(1), B(1), B(1) }, kind);
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsUseObservedHistory(int length)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), kind, length, length, length); Check(Hands, kind, length, length, length); Check(Hands, kind, length, 2, 1); }
    }
    [Fact]
    public void CoreRegistryAndOverlappingSpansUsePublicCausalFormula()
    {
        var prices = Enumerable.Range(0, 20).Select(i => (double)(i * 7 % 13 - 6)).ToArray(); var bars = prices.Select((p, i) => B(p, i)).ToArray();
        var expected = BuiltInFormulaReferences.VolatilityAverageValues(bars, length: 7).Outputs["Vma"]; var output = new double[prices.Length + 1]; output[^1] = 777;
        MovingAverageCore.VolatilityMovingAverage(prices, output, 7); Assert.Equal(expected, output.Take(prices.Length)); Assert.Equal(777, output[^1]);
        var alias = new double[prices.Length + 1]; prices.CopyTo(alias, 0); MovingAverageCore.VolatilityMovingAverage(alias.AsSpan(0, prices.Length), alias.AsSpan(1), 7); Assert.Equal(expected, alias.Skip(1));
        new VolMaCore().Compute(prices, output, 7); Assert.Equal(expected, output.Take(prices.Length));
        var full = new double[prices.Length]; MovingAverageCore.VolatilityMovingAverage(prices, full, 7, 3, 2);
        Assert.Equal(BuiltInFormulaReferences.VolatilityAverageValues(bars, length: 7, lookback: 3, smooth: 2).Outputs["Vma"], full);
        for (var count = 1; count <= prices.Length; count++)
        { var prefix = new double[count]; MovingAverageCore.VolatilityMovingAverage(prices.AsSpan(0, count), prefix, 7); Assert.Equal(expected.Take(count), prefix); }
        Assert.Throws<ArgumentException>(() => MovingAverageCore.VolatilityMovingAverage(prices, new double[1]));
        var guard = new[] { 123d, 456 }; Assert.ThrowsAny<ArgumentException>(() => MovingAverageCore.VolatilityMovingAverage(new[] { 1d, double.NaN }, guard)); Assert.Equal(new[] { 123d, 456 }, guard);
        MovingAverageCore.VolatilityMovingAverage(Array.Empty<double>(), Array.Empty<double>());
    }
    [Fact]
    public void ThreeFastCallbacksRetainOrderingAndBatchBypassesThem()
    {
        var calls = 0;
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { calls++; Assert.Equal(2, period); Assert.Equal(Hands.Select(b => b.Close), values); return Enumerable.Repeat(0d, Hands.Length).ToArray(); },
            (values, period) => { calls++; Assert.Equal(3, period); return Enumerable.Repeat(0d, Hands.Length).ToArray(); },
            (values, period) => { calls++; Assert.Equal(3, period); Assert.Equal(2d / 5, values[1]); return new[] { .25 }; }
        }))
        {
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeVolatilityMovingAverageFast(Data(Hands), context, 9, MovingAvgType.SimpleMovingAverage, 2, 3);
            Assert.Equal(new[] { .25, 0, 0, 0, 0, 0 }, output.ToArray()); Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Requests);
        }
        using (ComponentAverage.Arm((v, p) => throw new InvalidOperationException("Batch hooks bypassed")))
        { Data(Hands).CalculateVolatilityMovingAverage(); Assert.Equal(0, ComponentAverage.Requests); }
    }
    [Fact]
    public void DirectSelectedPricesAndInvalidFieldsPreserveContracts()
    {
        var prices = new[] { 9d, -2, 7, 0, 3, -5 }; var projected = prices.Select((p, i) => B(p, i)).ToArray(); var expected = BuiltInFormulaReferences.VolatilityAverageValues(projected, length: 7, lookback: 3, smooth: 2);
        var data = Data(Hands); data.SetCustomValues(prices.ToList()); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeVolatilityMovingAverageFast(data, context, 7, MovingAvgType.SimpleMovingAverage, 3, 2); Assert.Equal(expected.Outputs["Vma"], output.ToArray());
        data.CalculateVolatilityMovingAverage(length: 7, lbLength: 3, smoothLength: 2); Assert.Equal(expected.Outputs["Vma"], data.OutputValues["Vma"]); Assert.Equal(Hands.Select(b => b.Close), data.ClosePrices);
        using var state = new VolatilityMovingAverageState(); using var control = new VolatilityMovingAverageState(); state.Update(Native(B(2)), true, false); control.Update(Native(B(2)), true, false);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5))
        {
            var values = new[] { 1d, 1, 1, 1, 1 }; values[field] = bad; var bar = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), true, true));
            var invalid = Data(new[] { bar }); invalid.SetCustomValues(new List<double> { 1 });
            Assert.ThrowsAny<ArgumentException>(() => invalid.CalculateVolatilityMovingAverage());
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeVolatilityMovingAverageFast(invalid, context));
        }
        foreach (var bar in Hands) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
    }
}
