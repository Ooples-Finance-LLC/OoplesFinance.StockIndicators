using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DtOscillatorNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DTOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRsiRangeAndMeans(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.DtOscillatorOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, int range = 4, int first = 2, int second = 3, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod, int referenceKind = 6)
    {
        var expected = BuiltInFormulaReferences.DtOscillatorValues(bars, length, range, first, second, referenceKind); var batch = Data(bars).CalculateDTOscillator(kind, length, range, first, second);
        Assert.Equal(expected.Outputs["Dto"], batch.CustomValuesList); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { using var fast = IndicatorCompute.ComputeDTOscillatorFast(Data(bars), context, length, kind, range, first, second, key); Assert.Equal(expected.Outputs[key], fast.ToArray()); }
        if (kind == MovingAvgType.WildersSmoothingMethod)
        {
            var core = new double[bars.Length + 1]; core[^1] = 97; OscillatorCore.DTOscillator(Array.Empty<double>(), Array.Empty<double>(), bars.Select(b => b.Close).ToArray(), core, length, range, first); Assert.Equal(expected.Outputs["Dto"], core.Take(bars.Length)); Assert.Equal(97, core[^1]);
            var inplace = bars.Select(b => b.Close).ToArray(); OscillatorCore.DTOscillator(Array.Empty<double>(), Array.Empty<double>(), inplace, inplace, length, range, first); Assert.Equal(expected.Outputs["Dto"], inplace);
        }
        using var state = new DTOscillatorState(kind, length, range, first, second); using var window = new DtOscillatorWindow(kind, length, range, first, second);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 1d, 4, -2, 7, 3, -8, 2 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -97d })[0]), false, false); window.Next(-97, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Dto"][i], point.Value); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Outputs["Signal"][i], direct.SignalLine); Assert.Equal(expected.Signals[i], direct.Signal); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideDifferencesRangesAndBothPartialMeansMatchIndependentFractions()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 8 }) foreach (var kind in Kinds)
            Check(Bars(Enumerable.Range(0, 24).Select(i => (i % 9 - 4) * scale)), kind: kind.Kind, referenceKind: kind.Reference);
        foreach (var kind in Kinds) Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 1, -2, 3, 4, 5 }), kind: kind.Kind, referenceKind: kind.Reference);
        Check(Bars(new[] { 1d, Math.BitIncrement(1), 1, 1, Math.BitIncrement(Math.BitIncrement(1)), 1, 1, 1 }), 2);
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) External(double[] rsi, int range, int first, int second)
    {
        var bars = Bars(Enumerable.Repeat(0d, rsi.Length)); var expected = BuiltInFormulaReferences.DtOscillatorValues(bars, 2, range, first, second, 6, rsi);
        using var window = new DtOscillatorWindow(MovingAvgType.WildersSmoothingMethod, 2, range, first, second, true);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var v in new[] { 0d, 30, 70, 20, 90, 10 }) window.Next(0, true, v); window.Reset();
            for (var i = 0; i < rsi.Length; i++)
            {
                window.Next(0, false, 100); window.Next(0, false, 0);
                foreach (var final in new[] { false, false, true }) { var value = window.Next(0, final, rsi[i]); Assert.Equal(expected.Outputs["Dto"][i], value.Line); Assert.Equal(expected.Outputs["Signal"][i], value.SignalLine); Assert.Equal(expected.Signals[i], value.Signal); }
            }
        }
        return expected;
    }
    [Fact]
    public void HandFlatRangeAndAvailableObservationDivisorsStayExact()
    {
        var result = External(new[] { 0d, 100, 0, 100 }, 2, 2, 2); Assert.Equal(new[] { 0d, 50, 50, 50 }, result.Outputs["Dto"]); Assert.Equal(new[] { 0d, 25, 50, 50 }, result.Outputs["Signal"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.None }, result.Signals);
        Assert.All(Check(Bars(new[] { 1d, 2, 3, 4, 5 })).Outputs.Values.SelectMany(v => v), v => Assert.Equal(0, v));
        Assert.All(Check(Bars(new[] { 1d, -2, 3, 0, 4 }), range: 1).Outputs.Values.SelectMany(v => v), v => Assert.Equal(0, v));
    }
    [Fact]
    public void TinyRangesExtremaExpiryAndPreviewAreIndependent()
    {
        foreach (var values in new[] { new[] { 0d, double.Epsilon, 3 * double.Epsilon, double.Epsilon, 0, 4 * double.Epsilon, 0, 0 }, new[] { 50d, Math.BitIncrement(50), 50, Math.BitDecrement(50), 50, 50, Math.BitIncrement(50) }, new[] { 0d, 100, 25, 25, 30, 25, 35, 35, 35 } }) External(values, 3, 2, 3);
        using var rsi = new DtOscillatorWindow(MovingAvgType.ExponentialMovingAverage, 3, 4, 2, 3);
        rsi.Next(1, true); rsi.Next(3, true); var before = rsi.Next(2, true).Rsi; Assert.Equal(before, rsi.Next(2, false).Rsi); Assert.Equal(before, rsi.Next(2, true).Rsi);
    }
    [Fact]
    public void EachExtremePeriodGrowsOnlyWithObservations()
    {
        foreach (var kind in Kinds) foreach (var slot in Enumerable.Range(0, 4))
        {
            var periods = new[] { 3, 4, 2, 3 }; periods[slot] = int.MaxValue;
            Check(Array.Empty<Bar>(), periods[0], periods[1], periods[2], periods[3], kind.Kind, kind.Reference);
            Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, 2, -3, 7 }), periods[0], periods[1], periods[2], periods[3], kind.Kind, kind.Reference);
        }
        Check(Bars(new[] { 1d, 3, -2, 7 }), 0, -1, int.MinValue, 0);
    }
    [Fact]
    public void CustomRsiCallbacksKeepBatchAndFastRequestTopology()
    {
        var prices = new[] { 1d, 3, 2, 4, 1, 3 }; var bars = Bars(prices); var up = new[] { 0d, 1, 3, 1, 2, 5 }; var down = new[] { 0d, 2, 1, 4, 1, 2 };
        foreach (var kind in Kinds) foreach (var fast in new[] { false, true }) foreach (var key in new[] { "Dto", "Signal" })
        {
            var supplied = up.Select((v, i) => down[i] == 0 ? 100 : v == 0 ? 0 : 100 - 100 / (1 + v / down[i])).ToArray();
            var count = fast ? 2 : 0;
            if (count == 0)
            {
                ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
                double[] Mean(double[] input)
                {
                    var output = new double[input.Length];
                    for (var i = 0; i < input.Length; i++)
                    {
                        var previous = i == 0 ? R(0) : R(output[i - 1]); var priorInput = i == 0 ? R(0) : R(input[i - 1]);
                        output[i] = kind.Reference == 1 ? i == 0 ? 0 : ((priorInput + R(input[i])) / R(2)).ToDouble()
                            : kind.Reference == 2 ? ((priorInput + R(2) * R(input[i])) / R(3)).ToDouble()
                            : kind.Reference == 6 ? ((previous + R(input[i])) / R(2)).ToDouble()
                            : i < 2 ? ((priorInput + R(input[i])) / R(i + 1)).ToDouble() : ((previous + R(2) * R(input[i])) / R(3)).ToDouble();
                    }
                    return output;
                }
                var gains = Mean(new[] { 0d, 2, 0, 2, 0, 2 }); var losses = Mean(new[] { 0d, 0, 1, 0, 3, 0 });
                supplied = gains.Select((gain, i) => losses[i] == 0 ? 100 : gain == 0 ? 0 : 100 - 100 / (1 + gain / losses[i])).ToArray();
            }
            var expected = BuiltInFormulaReferences.DtOscillatorValues(bars, 2, 3, 2, 3, kind.Reference, supplied); var calls = 0;
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(2, period); var slot = calls++; Assert.Equal(slot == 0 ? new[] { 0d, 2, 0, 2, 0, 2 } : slot == 1 ? new[] { 0d, 0, 1, 0, 3, 0 } : supplied, values); return slot == 0 ? up : slot == 1 ? down : new double[prices.Length]; };
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, Math.Max(1, count)).ToArray()); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeDTOscillatorFast(Data(bars), context, 2, kind.Kind, 3, 2, 3, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
            else { var batch = Data(bars).CalculateDTOscillator(kind.Kind, 2, 3, 2, 3); foreach (var name in expected.Outputs.Keys) Assert.Equal(expected.Outputs[name], batch.OutputValues[name]); Assert.Equal(expected.Signals, batch.SignalsList); }
            Assert.Equal(count, calls); Assert.Equal(count, ComponentAverage.Requests); Assert.Equal(count, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void SelectedPricesAndLegacyRsiPreserveBothOutputs()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8, -3, 2 }; var bars = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i + 1)).ToArray(); var effective = Bars(selected);
        var expected = BuiltInFormulaReferences.DtOscillatorValues(effective, 3, 4, 2, 3, 2); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateDTOscillator(MovingAvgType.WeightedMovingAverage, 3, 4, 2, 3); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeDTOscillatorFast(source, context, 3, MovingAvgType.WeightedMovingAverage, 4, 2, 3, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var legacy = Data(effective).CalculateDTOscillator(kind, 3, 4, 2, 3); using var state = new DTOscillatorState(kind, 3, 4, 2, 3);
        foreach (var key in expected.Outputs.Keys) { using var fast = IndicatorCompute.ComputeDTOscillatorFast(Data(effective), context, 3, kind, 4, 2, 3, key); Assert.Equal(legacy.OutputValues[key], fast.ToArray()); }
        for (var i = 0; i < effective.Length; i++) { var point = state.Update(Native(effective[i]), true, true); foreach (var key in expected.Outputs.Keys) Assert.Equal(legacy.OutputValues[key][i], point.Outputs![key]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRsiRangesOrMeans()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new DTOscillatorState(length1: 3); using var control = new DTOscillatorState(length1: 3);
            foreach (var b in Bars(new[] { 1d, 4, -2, 7 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Bars(new[] { -3d, 2, 7, 0 })) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
        Assert.Throws<ArgumentException>(() => OscillatorCore.DTOscillator(Array.Empty<double>(), Array.Empty<double>(), new[] { 1d }, Array.Empty<double>()));
    }
}
