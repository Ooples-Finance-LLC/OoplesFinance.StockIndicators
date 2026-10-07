using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TStepLeastSquaresNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TAI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TStepLeastSquaresMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentSteppedRegression(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TStepLeastSquaresOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var o = (TStepLeastSquaresMovingAverageSpecOptions)indicator.CreateOptions();
        var bars = Bars(Enumerable.Range(0, 64).Select(i => 20d + i % 5).ToArray()); var selected = bars.Select((_, i) => i % 7 - 3d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.TStepLeastSquaresOutputs(projected, indicator);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var pair in expected)
        { using var actual = IndicatorCompute.ComputeTStepLeastSquaresMovingAverageFast(data, context, o.Length, o.MaType); Assert.Equal(pair.Value, actual.ToArray()); }
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateTStepLeastSquaresMovingAverage(o.MaType, o.Length);
        Assert.Equal(expected["Tslsma"], batch.CustomValuesList); Assert.Equal(bars.Select(b => b.High), batch.HighPrices); Assert.Equal(bars.Select(b => b.Low), batch.LowPrices); Assert.Equal(bars.Select(b => b.Close), batch.ClosePrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (double[] Line, Signal[] Signals, double[] Steps) Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.TStepLeastSquaresValues(bars, length, kind); var data = Data(bars).CalculateTStepLeastSquaresMovingAverage(kind, length);
        Assert.Equal(expected.Line, data.CustomValuesList); Assert.Equal(expected.Line, data.OutputValues["Tslsma"]); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeTStepLeastSquaresMovingAverageFast(Data(bars), context, length, kind); Assert.Equal(expected.Line, fast.ToArray());
        using var native = new TStepLeastSquaresMovingAverageState(kind, length); using var raw = new TStepLeastSquaresWindow(kind, length);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Bars(3, -2, 7, 1, 4)) { native.Update(Native(seed), true, true); raw.Next(seed.Close, true); }
            native.Reset(); raw.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(-17)[0]), false, false); raw.Next(-17, false);
                foreach (var final in new[] { false, false, true })
                { var b = bars[i]; var next = native.Update(Native(b), final, true); var direct = raw.Next(b.Close, final); Assert.Equal(expected.Line[i], next.Value); Assert.Equal(next.Value, next.Outputs!["Tslsma"]); Assert.Equal(next.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Trade); }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentStepsCovarianceAndSignalHands()
    {
        var result = Check(Bars(1, 2, 4, 2)); Assert.Equal(new[] { 1d, 1, 4, 4 }, result.Steps); Assert.Equal(new[] { 0d, 0, 4, 3 }, result.Line);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongBuy, Signal.None, Signal.StrongSell }, result.Signals);
        Assert.Equal(new[] { 0d, 3, 3 }, Check(Bars(2, 4, 2), 2).Line);
        foreach (var kind in new[] { MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod, MovingAvgType.DoubleExponentialMovingAverage })
            Check(Bars(1, 2, 4, 2, 8, -1, 3, 0, 7, 7, 7, 7, 7, 2, -3, 4), 3, kind);
    }
    [Fact]
    public void OverflowingMomentsAndSubnormalStepsRemainFinite()
    {
        var m = double.MaxValue; var e = double.Epsilon;
        Assert.Equal(new[] { 0d, 0, m, m * .75 }, Check(Bars(m / 4, m / 2, m, m / 2)).Line);
        Assert.Equal(new[] { 0d, 0, 4 * e, 3 * e }, Check(Bars(e, 2 * e, 4 * e, 2 * e)).Line);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
            Check(Bars(m, -m, m / 2, -m / 4, 0, e, -e, 0, m, -m, 0), 3, kind);
    }
    [Fact]
    public void ExactThresholdTiesAndNeighboringPricesSelectDifferentSteps()
    {
        foreach (var last in new[] { Math.BitDecrement(2d), 2d, Math.BitIncrement(2d), 3 * double.Epsilon })
        {
            var prices = last < 1 ? new[] { 0d, double.Epsilon, last } : new[] { 0d, 1, last }; var bars = Bars(prices);
            var expectedStep = last > 2 || last < 1 ? last : 0;
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> first = (values, _) => values;
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> second = (values, _) => { Assert.Equal(new[] { 0d, 0, expectedStep }, values); return values; };
            using var scope = ComponentAverage.Arm(new[] { first, second }); Data(bars).CalculateTStepLeastSquaresMovingAverage(length: 10); Assert.Equal(2, ComponentAverage.Requests);
        }
    }
    [Fact]
    public void CallbackOrderDiscoveryShortArraysAndFirstSignalArePreserved()
    {
        var bars = Bars(1, 2, 4, 2);
        foreach (var fast in new[] { false, true })
        {
            var data = Data(Bars(9, 9, 9, 9)); data.SetCustomValues(bars.Select(b => b.Close).ToList());
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> first = (values, length) => { Assert.Equal(bars.Select(b => b.Close), values); Assert.Equal(3, length); return new[] { 0d, 0, 10, 20 }; };
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> second = (values, length) => { Assert.Equal(new[] { 1d, 1, 4, 4 }, values); Assert.Equal(3, length); return new[] { 0d, 0, 2, 3 }; };
            using var scope = ComponentAverage.Arm(new[] { first, second });
            if (fast) { using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeTStepLeastSquaresMovingAverageFast(data, context, 3); Assert.Equal(new[] { 0d, 0, 35d / 3, 61d / 3 }, actual.ToArray()); Assert.Equal(bars.Select(b => b.Close), data.ChainedValues); }
            else Assert.Equal(new[] { 0d, 0, 35d / 3, 61d / 3 }, data.CalculateTStepLeastSquaresMovingAverage(length: 3).CustomValuesList);
            Assert.Equal(2, ComponentAverage.Requests); Assert.Equal(2, ComponentAverage.Substitutions); Assert.Equal(new[] { 9d, 9, 9, 9 }, data.ClosePrices);
        }
        using (ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>()))
        { Data(bars).CalculateTStepLeastSquaresMovingAverage(length: 3); Assert.Equal(2, ComponentAverage.Requests); }
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (_, _) => new[] { 1.5 } }))
        { var result = Data(bars).CalculateTStepLeastSquaresMovingAverage(length: 3); Assert.Equal(Signal.Sell, result.SignalsList[0]); Assert.Equal(new[] { 1.5, 0, 5d / 3, 1d / 3 }, result.CustomValuesList); Assert.Equal(2, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions); }
    }
    [Fact]
    public void ExtremePeriodsExpiryAndUnusedScParameterRemainDefined()
    {
        var bars = Bars(1, 2, 4, 2, 8, -1, 3, 3, 3, 3, 3, 3, 2, 0);
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 7, int.MaxValue }) Check(bars, length);
        var expected = Check(bars).Line;
        foreach (var sc in new[] { -1d, 0, 1, double.NaN, double.PositiveInfinity })
        {
            Assert.Equal(expected, Data(bars).CalculateTStepLeastSquaresMovingAverage(length: 3, sc: sc).CustomValuesList);
            using var native = new TStepLeastSquaresMovingAverageState(length: 3, sc: sc); Assert.Equal(expected, bars.Select(b => native.Update(Native(b), true, false).Value));
        }
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceStepMomentsOrMeans()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var actual = new TStepLeastSquaresMovingAverageState(length: 3); using var expected = new TStepLeastSquaresMovingAverageState(length: 3);
            actual.Update(Native(Bars(1)[0]), true, true); expected.Update(Native(Bars(1)[0]), true, true);
            var v = new[] { 2d, 2, 2, 2, 1 }; v[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(2, 4, -3, 4)) Assert.Equal(expected.Update(Native(b), true, true).Value, actual.Update(Native(b), true, true).Value);
        }
    }
}
