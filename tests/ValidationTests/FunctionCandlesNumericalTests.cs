using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FunctionCandlesNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FunctionToCandles)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCandleRsi(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FunctionCandlesOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static readonly string[] Keys = { "Close", "Open", "High", "Low" };
    private static Bar[] Bars(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v / 2, Math.Max(v, 0), Math.Min(v, 0), v, 1)).ToArray();
    private static IReadOnlyDictionary<string, double[]> Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod, int reference = 6)
    {
        var expected = BuiltInFormulaReferences.FunctionCandlesValues(bars, length, reference); var batch = Data(bars).CalculateFunctionToCandles(kind, length);
        Assert.Empty(batch.CustomValuesList);
        foreach (var key in Keys)
        { Assert.Equal(expected[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFunctionToCandlesFast(Data(bars), context, length, kind, Enum.Parse<IndicatorCompute.CandleSeries>(key)); Assert.Equal(expected[key], output.ToArray()); }
        using var state = new FunctionToCandlesState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(5, -3, 2)) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(-999)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Close"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected[key][i], point.Outputs![key]); }
            }
        }
        return expected;
    }
    [Fact]
    public void HandCandleFieldsRemainIndependentAndSignalsUseTheirMean()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 2, 5, -2, 1, 1), new Bar(DateTime.UnixEpoch, 1, 8, -4, 4, 1), new Bar(DateTime.UnixEpoch, 3, 7, -3, 3, 1) };
        var result = Check(bars, 2); Assert.Equal(new[] { 100d, 100, 60 }, result["Close"]); Assert.Equal(new[] { 100d, 0, 80 }, result["Open"]);
        Assert.Equal(new[] { 100d, 100, 60 }, result["High"]); Assert.Equal(new[] { 100d, 0, 50 }, result["Low"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongSell, Signal.StrongBuy }, Data(bars).CalculateFunctionToCandles(length: 2).SignalsList);
        var flat = Check(Bars(4, 4, 4, 4), 2); foreach (var key in Keys) Assert.All(flat[key], v => Assert.Equal(100, v));
    }
    [Fact]
    public void WideSubnormalExpiryAndFlatRunsMatchExactReference()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { 1, 2, 3, 7 })
        {
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
                Check(Bars(Enumerable.Range(0, 21).Select(i => (i % 7 - 3) * scale).ToArray()), length, kind.Kind, kind.Reference);
            Check(Bars(-double.MaxValue, double.MaxValue, 0, -double.MaxValue, double.Epsilon, 2 * double.Epsilon, 0, 1, 1, 1, 1, 1, 2, 0), length, kind.Kind, kind.Reference);
        }
    }
    [Fact]
    public void ExtremePeriodsAllocateObservedHistoryOnly()
    { foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Bars(4, -2, 7), period, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), period, kind.Kind, kind.Reference); } }
    [Fact]
    public void CallbackSlotsUseEachSelectedCandleAndCompleteRatio()
    {
        var bars = Bars(4, -2, 7, 1); var selected = new[] { 20d, 3, -10, 2 };
        var projected = bars.Select((b, i) => { var previous = selected[Math.Max(0, i - 1)]; var outside = selected[i] > b.High || selected[i] < b.Low; return new Bar(b.Time, b.Open, outside ? Math.Max(previous, selected[i]) : b.High, outside ? Math.Min(previous, selected[i]) : b.Low, selected[i], b.Volume); }).ToArray();
        foreach (var key in Keys)
        {
            var prices = projected.Select(b => key == "Close" ? b.Close : key == "Open" ? b.Open : key == "High" ? b.High : b.Low).ToArray();
            var calls = 0; var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (v, n) => { Assert.Equal(3, n); Assert.Equal(prices.Select((p, i) => i == 0 ? 0 : Math.Max(0, p - prices[i - 1])), v); calls++; return Enumerable.Repeat(double.MaxValue, bars.Length).ToArray(); },
                (v, n) => { Assert.Equal(3, n); Assert.Equal(prices.Select((p, i) => i == 0 ? 0 : Math.Max(0, prices[i - 1] - p)), v); calls++; return Enumerable.Repeat(double.MaxValue, bars.Length).ToArray(); } };
            using (ComponentAverage.Arm(callbacks))
            { var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFunctionToCandlesFast(data, context, 3, series: Enum.Parse<IndicatorCompute.CandleSeries>(key)); Assert.All(output.ToArray(), v => Assert.Equal(50, v)); Assert.Equal(2, calls); Assert.Equal(2, ComponentAverage.Requests); Assert.Equal(selected, data.ChainedValues); }
            using (ComponentAverage.Arm((v, n) => v))
            { Assert.NotNull(ComponentAverage.Take(new[] { 1d }, 1)); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFunctionToCandlesFast(Data(bars), context, 3, series: Enum.Parse<IndicatorCompute.CandleSeries>(key)); Assert.Equal(BuiltInFormulaReferences.FunctionCandlesValues(bars, 3, 6)[key], output.ToArray()); Assert.Equal(3, ComponentAverage.Requests); }
        }
        using (ComponentAverage.Arm((v, n) => throw new InvalidOperationException("Batch consumes no callback")))
        { var data = Data(bars); data.SetCustomValues(selected.ToList()); data.CalculateFunctionToCandles(length: 3); var expected = BuiltInFormulaReferences.FunctionCandlesValues(projected, 3, 6); foreach (var key in Keys) Assert.Equal(expected[key], data.OutputValues[key]); Assert.Equal(0, ComponentAverage.Requests); }
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (v, n) => new[] { double.Epsilon, double.Epsilon }, (v, n) => new[] { double.Epsilon, double.Epsilon } }))
        { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFunctionToCandlesFast(Data(Bars(1, 2)), context, 2); Assert.Equal(new[] { 50d, 50 }, output.ToArray()); }
    }
    [Fact]
    public void ActualOverridesDetermineFlatBarRatios()
    {
        using var binding = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (v, n) => new[] { 1d, 3, 1 }, (v, n) => new[] { 1d, 1, 3 } });
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFunctionToCandlesFast(Data(Bars(1, 1, 1)), context, 3);
        Assert.Equal(new[] { 50d, 75, 25 }, output.ToArray());
    }
    [Fact]
    public void LegacyMeanRetainsRouteAgreement()
    {
        var bars = Bars(4, -2, 7, 1, 0, 3); var kind = MovingAvgType.TripleExponentialMovingAverage;
        var batch = Data(bars).CalculateFunctionToCandles(kind, 3); using var state = new FunctionToCandlesState(kind, 3);
        foreach (var key in Keys) { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFunctionToCandlesFast(Data(bars), context, 3, kind, Enum.Parse<IndicatorCompute.CandleSeries>(key)); Assert.Equal(batch.OutputValues[key], output.ToArray()); }
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); foreach (var key in Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key], 12); }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FunctionToCandlesState(length: 3); using var control = new FunctionToCandlesState(length: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Low"], actual.Outputs!["Low"]); }
        }
    }
}
