using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FisherLeastSquaresNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FisherLeastSquaresMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentResidualRegression(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FisherLsOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static Bar[] Prices(params double[] values) => Candles(values.Select(v => (v, v, v)).ToArray());
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1)
    {
        var expected = BuiltInFormulaReferences.FisherLsValues(bars, length, reference); var batch = Data(bars).CalculateFisherLeastSquaresMovingAverage(kind, length);
        Assert.Equal(expected.Outputs["Flsma"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFisherLeastSquaresMovingAverageFast(Data(bars), context, length, kind); Assert.Equal(expected.Outputs["Flsma"], output.ToArray());
        using var state = new FisherLeastSquaresMovingAverageState(kind, length); using var window = new FisherLeastSquaresWindow(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Prices(8, -2, 6, 1)) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Prices(999)[0]), false, false); window.Next(-999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final);
                    Assert.Equal(expected.Outputs["Flsma"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Flsma"]); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandInverseFisherAndFullWindowStartup()
    {
        var result = Check(Prices(0, 2), 2); Assert.Equal(new[] { 0d, 1 + Math.Tanh(1) }, result.Outputs["Flsma"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy }, result.Signals);
        foreach (var kind in Kinds) Assert.Equal(new[] { 4d, -2, 7 }, Check(Prices(4, -2, 7), 1, kind.Kind, kind.Reference).Outputs["Flsma"]);
        foreach (var kind in Kinds) Check(Prices(4, -2, 7, 3, -1, 8), 3, kind.Kind, kind.Reference);
        foreach (var period in new[] { -1, 0, 1, 2, 5, int.MaxValue }) Check(Prices(4, -2, 7, 3), period);
        Assert.Equal(new[] { 0d, 0, 4 }, Check(Prices(4, 4, 4), 3).Outputs["Flsma"]);
    }
    [Fact]
    public void WideResidualMomentsAndFourMeansMatchFractions()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Prices(Enumerable.Range(0, 17).Select(i => (i % 7 - 3) * scale).ToArray()), 3, kind.Kind, kind.Reference);
        foreach (var kind in Kinds)
        {
            Check(Prices(double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue, -double.MaxValue, 1, 1, 1, 1, 1, 1), 3, kind.Kind, kind.Reference);
            Check(Prices(Enumerable.Repeat(double.MaxValue, 9).ToArray()), 3, kind.Kind, kind.Reference);
            Check(Prices(0, double.Epsilon, 0, double.Epsilon, 0), 2, kind.Kind, kind.Reference);
            Check(Prices(1, Math.BitIncrement(1), 1, Math.BitIncrement(1), 1), 2, kind.Kind, kind.Reference);
            Check(Prices(3, -2, 5), int.MaxValue, kind.Kind, kind.Reference);
        }
    }
    [Fact]
    public void TinySignedResidualRatioSurvivesInverseTransform()
    {
        var bars = Prices(0, 1, -Math.BitDecrement(1)); var prices = new[] { 0d, 0, 0 }; var indices = new[] { 0d, 1, 1 };
        var expected = BuiltInFormulaReferences.FisherLsValues(bars, 2, 1, prices, indices).Outputs["Flsma"];
        Assert.Equal(Math.Pow(2, -53), expected[2]);
        using var window = new FisherLeastSquaresWindow(MovingAvgType.SimpleMovingAverage, 2, false);
        for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], window.Finish(bars[i].Close, new(prices[i]), new(indices[i]), true).Line);
        var slot = 0; using var binding = ComponentAverage.Arm(Enumerable.Range(0, 2).Select(_ => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((_, n) => { Assert.Equal(2, n); return slot++ == 0 ? prices : indices; })).ToArray());
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFisherLeastSquaresMovingAverageFast(Data(bars), context, 2);
        Assert.Equal(expected, output.ToArray()); Assert.Equal(2, slot);
    }
    [Fact]
    public void ComponentSlotsAndSelectedPricesPreserveCallerInputs()
    {
        var bars = Prices(1, 4, -2, 8, 0); var selected = new[] { 4d, -1, 3, 7, 2 }; var data = Data(bars); data.SetCustomValues(selected.ToList());
        var externalPrices = new[] { 0d, 2, 1, -1, 4 }; var externalIndices = new[] { 0d, 0, 1, 1, 2 };
        var expected = BuiltInFormulaReferences.FisherLsValues(Prices(selected), 3, 1, externalPrices, externalIndices); var calls = 0;
        using (ComponentAverage.Arm(Enumerable.Range(0, 2).Select(_ => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, n) => { Assert.Equal(3, n); Assert.Equal(calls == 0 ? selected : new[] { 0d, 1, 2, 3, 4 }, values); return calls++ == 0 ? externalPrices : externalIndices; })).ToArray()))
        { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFisherLeastSquaresMovingAverageFast(data, context, 3); Assert.Equal(expected.Outputs["Flsma"], output.ToArray()); Assert.Equal(2, calls); }
        Assert.Equal(selected, data.CustomValuesList); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        using (ComponentAverage.Arm((v, _) => { calls++; return v; }))
        { var batch = Data(Prices(selected)).CalculateFisherLeastSquaresMovingAverage(length: 3); Assert.Equal(BuiltInFormulaReferences.FisherLsValues(Prices(selected), 3, 1).Outputs["Flsma"], batch.CustomValuesList); Assert.Equal(0, ComponentAverage.Requests); }
        var wide = Prices(double.MaxValue, -double.MaxValue, double.MaxValue, 0, 0, 0, 0); calls = 0;
        using (ComponentAverage.Arm((v, _) => { calls++; return v; }))
        {
            Assert.NotNull(ComponentAverage.Take(new[] { 1d }, 1)); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFisherLeastSquaresMovingAverageFast(Data(wide), context, 3, MovingAvgType.WeightedMovingAverage);
            Assert.Equal(BuiltInFormulaReferences.FisherLsValues(wide, 3, 2).Outputs["Flsma"], output.ToArray()); Assert.Equal(1, calls); Assert.Equal(3, ComponentAverage.Requests);
        }
    }
    [Fact]
    public void CoreUsesFullWindowsAndProtectsSpansAndInPlaceHistory()
    {
        var bars = Prices(4, -2, double.MaxValue, -double.MaxValue, 5, 1, 1, 1, 1);
        foreach (var period in new[] { -1, 0, 1, 2, 5, int.MaxValue })
        {
            var input = bars.Select(b => b.Close).ToArray(); var expected = BuiltInFormulaReferences.FisherLsValues(bars, period, 1).Outputs["Flsma"]; var output = Enumerable.Repeat(97d, bars.Length + 1).ToArray();
            MovingAverageCore.FisherLeastSquaresMovingAverage(input, output, period); Assert.Equal(expected, output.Take(bars.Length)); Assert.Equal(97, output[^1]);
            MovingAverageCore.FisherLeastSquaresMovingAverage(input, input, period); Assert.Equal(expected, input);
        }
        MovingAverageCore.FisherLeastSquaresMovingAverage(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
        var unchanged = Enumerable.Repeat(97d, bars.Length - 1).ToArray(); Assert.Throws<ArgumentException>(() => MovingAverageCore.FisherLeastSquaresMovingAverage(bars.Select(b => b.Close).ToArray(), unchanged)); Assert.All(unchanged, v => Assert.Equal(97, v));
    }
    [Fact]
    public void LegacyMeansKeepPublicRouteAgreement()
    {
        var bars = Prices(4, -2, 7, 0, 3, 1); var kind = MovingAvgType.TripleExponentialMovingAverage;
        var batch = Data(bars).CalculateFisherLeastSquaresMovingAverage(kind, 3); using var state = new FisherLeastSquaresMovingAverageState(kind, 3); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeFisherLeastSquaresMovingAverageFast(Data(bars), context, 3, kind); Assert.Equal(batch.CustomValuesList, output.ToArray());
        for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FisherLeastSquaresMovingAverageState(length: 3); using var control = new FisherLeastSquaresMovingAverageState(length: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Flsma"], actual.Outputs!["Flsma"]); }
        }
    }
}
