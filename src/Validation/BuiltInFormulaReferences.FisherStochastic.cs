using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FisherStochOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return FisherStochValues(bars, Integer(options, "Length", 2), 30, 5, 2).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FisherStochValues(IReadOnlyList<Bar> bars, int length, int range, int smooth, int kind,
        double[][]? supplied = null)
    {
        length = Math.Max(1, length); range = Math.Max(1, range); smooth = Math.Max(1, smooth);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var stages = new ReferenceFraction[10][]; var previous = bars.Select(b => R(b.Close)).ToArray();
        for (var depth = 0; depth < 10; depth++) { stages[depth] = supplied is null ? SmoothRocBankStage(previous, length, kind) : supplied[depth].Select(R).ToArray(); previous = stages[depth]; }
        var rainbow = bars.Select((_, i) => (Enumerable.Range(0, 10).Aggregate(R(0), (a, j) => a + stages[j][i] * R(Math.Max(1, 5 - j))) / R(20)).RoundExtendedBinary64()).ToArray();
        var numerators = new ReferenceFraction[bars.Count]; var denominators = new ReferenceFraction[bars.Count]; var output = new double[bars.Count]; var trades = new Signal[bars.Count];
        var oldLine = R(0); var oldSlope = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var sample = Window(rainbow, i, range).ToArray(); var low = sample.Aggregate((a, b) => a.CompareTo(b) < 0 ? a : b); var high = sample.Aggregate((a, b) => a.CompareTo(b) > 0 ? a : b);
            numerators[i] = (rainbow[i] - low).RoundExtendedBinary64(); denominators[i] = (high - low).RoundExtendedBinary64();
            var numerator = Window(numerators, i, smooth).Aggregate(R(0), (a, b) => a + b); var denominator = Window(denominators, i, smooth).Aggregate(R(.0001), (a, b) => a + b);
            var percent = R(Math.Max(0, Math.Min(100, (R(100) * numerator / denominator).ToDouble())));
            var exponent = ((R(50) - percent) / R(5)).ToDouble(); output[i] = (R(100) / (R(1) + R(Math.Exp(exponent)))).ToDouble();
            var line = R(output[i]); var slope = line - oldLine;
            trades[i] = slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 || oldLine.CompareTo(R(30)) < 0 && line.CompareTo(R(30)) > 0 ? Signal.Buy
                : slope.Sign < 0 || oldLine.CompareTo(R(70)) > 0 && line.CompareTo(R(70)) < 0 ? Signal.Sell : Signal.None;
            oldLine = line; oldSlope = slope;
        }
        return (new Dictionary<string, double[]> { ["Ftso"] = output }, trades);
    }
}
