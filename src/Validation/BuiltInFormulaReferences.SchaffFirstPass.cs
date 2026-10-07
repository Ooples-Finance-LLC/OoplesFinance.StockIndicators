using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SchaffFirstPassOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return new Dictionary<string, double[]> { ["Stc"] = SchaffFirstPassValues(bars, Integer(options, "FastLength", 23), Integer(options, "SlowLength", 50), Integer(options, "CycleLength", Integer(options, "Length", 10)), AverageKind(options, 3)).Values }; }
    internal static (double[] Values, Signal[] Signals) SchaffFirstPassValues(IReadOnlyList<Bar> bars, int fastLength, int slowLength, int cycleLength, int kind, double[][]? external = null)
    {
        fastLength = Math.Max(1, fastLength); slowLength = Math.Max(1, slowLength); cycleLength = Math.Max(1, cycleLength); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var fast = external is null ? SmoothRocBankStage(prices, fastLength, kind) : external[0].Select(R).ToArray(); var slow = external is null ? SmoothRocBankStage(prices, slowLength, kind) : external[1].Select(R).ToArray();
        var macd = fast.Select((value, i) => (value - slow[i]).RoundExtendedBinary64()).ToArray(); var scales = fast.Select((value, i) => (value.Abs() + slow[i].Abs()).RoundExtendedBinary64()).ToArray();
        var result = new double[bars.Count]; var signals = new Signal[bars.Count]; var before = R(0); var previousSlope = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var values = Window(macd, i, cycleLength).ToArray(); var lower = values.Min(); var upper = values.Max(); var range = (upper - lower).RoundExtendedBinary64();
            var scale = Window(scales, i, cycleLength).Max(); var threshold = (R(Math.Pow(2, -46)) * scale).RoundExtendedBinary64();
            result[i] = range.CompareTo(threshold) <= 0 ? 0 : Math.Max(0, Math.Min(100, (R(100) * (macd[i] - lower) / range).ToDouble()));
            var slope = R(result[i]) - before; signals[i] = slope.Sign > 0 && slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None; before = R(result[i]); previousSlope = slope;
        }
        return (result, signals);
    }
}
