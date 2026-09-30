using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DtOscillatorOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return DtOscillatorValues(bars, Integer(o, "Length", 13), 8, 5, 3, AverageKind(o, 6)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) DtOscillatorValues(IReadOnlyList<Bar> bars, int length, int range, int first, int second, int kind, double[]? suppliedRsi = null)
    {
        length = Math.Max(1, length); range = Math.Max(1, range); first = Math.Max(1, first); second = Math.Max(1, second);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var rsi = suppliedRsi ?? (kind is 1 or 2 or 3 or 6 ? RoundedPriceRsi(bars, length, kind) : Legacy());
        double[] Legacy()
        {
            var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
            var up = Average(changes.Select(v => Math.Max(0, v)).ToArray(), length, kind); var down = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), length, kind);
            return up.Select((v, i) => down[i] == 0 ? 100 : v == 0 ? 0 : Math.Max(0, Math.Min(100, 100 - 100 / (1 + v / down[i])))).ToArray();
        }
        var stochastic = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var history = rsi.Skip(Math.Max(0, i + 1 - range)).Take(Math.Min(i + 1, range)).ToArray(); var high = R(history.Max()); var low = R(history.Min());
            stochastic[i] = (high - low).Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (R(100) * (R(rsi[i]) - low) / (high - low)).ToDouble()));
        }
        double[] Mean(double[] input, int period)
        {
            var prefix = new ReferenceFraction[input.Length + 1]; prefix[0] = R(0); for (var i = 0; i < input.Length; i++) prefix[i + 1] = prefix[i] + R(input[i]);
            return input.Select((_, i) => ((prefix[i + 1] - prefix[Math.Max(0, i + 1 - period)]) / R(Math.Min(i + 1, period))).ToDouble()).ToArray();
        }
        var line = Mean(stochastic, first); var signalLine = Mean(line, second); var events = new Signal[bars.Count]; var oldSlope = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? 0 : signalLine[i - 1]; var slope = R(signalLine[i]) - R(previous);
            events[i] = slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 || previous < 30 && signalLine[i] > 30 ? Signal.Buy : slope.Sign < 0 || previous > 70 && signalLine[i] < 70 ? Signal.Sell : Signal.None;
            oldSlope = slope;
        }
        return (new Dictionary<string, double[]> { ["Dto"] = line, ["Signal"] = signalLine }, events);
    }
}
