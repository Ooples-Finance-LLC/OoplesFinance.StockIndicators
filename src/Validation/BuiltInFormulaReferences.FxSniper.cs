using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FxSniperOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var prices = indicator is IIndicator { Source: not null } ? Closes(bars) : bars.Select(b => ExactPriceMean(b.High, b.Low, b.Close)).ToArray();
        return FxSniperValues(prices, Integer(options, "CciLength", 14), Integer(options, "T3Length", 5), AverageKind(options, 1), Number(options, .618, "B")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FxSniperValues(double[] prices, int cciLength, int t3Length, int kind, double factor = .618, double[][]? external = null)
    {
        cciLength = Math.Max(1, cciLength); t3Length = Math.Max(1, t3Length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var cci = external is null || kind == 1 ? CommodityValues(prices, cciLength, kind).Select(R).ToArray() : prices.Select((v, i) => external[1][i] == 0 ? R(0)
            : ((R(v) - R(external[0][i])).RoundExtendedBinary64() / (R(.015) * R(external[1][i]))).RoundExtendedBinary64()).ToArray();
        var stages = new ReferenceFraction[6][]; var gain = R(4) / new ReferenceFraction(t3Length + 3L); var carry = R(1) - gain;
        for (var depth = 0; depth < stages.Length; depth++)
        {
            var input = depth == 0 ? cci : stages[depth - 1]; var output = new ReferenceFraction[prices.Length]; var previous = R(0);
            for (var i = 0; i < output.Length; i++) { output[i] = (gain * input[i] + carry * previous).RoundExtendedBinary64(); previous = output[i]; }
            stages[depth] = output;
        }
        var b = R(factor); var b2 = b * b; var b3 = b2 * b; var coefficients = new[] { R(1) + R(3) * b + b3 + R(3) * b2, R(-3) * (R(2) * b2 + b + b3), R(3) * (b2 + b3), R(0) - b3 };
        var line = Enumerable.Range(0, prices.Length).Select(i => Enumerable.Range(0, 4).Aggregate(R(0), (sum, j) => sum + coefficients[j] * stages[j + 2][i]).RoundExtendedBinary64()).ToArray();
        var signals = new Signal[prices.Length]; var prior = R(0);
        for (var i = 0; i < line.Length; i++) { var v = line[i]; signals[i] = v.Sign > 0 && v.CompareTo(prior) > 0 ? Signal.StrongBuy : v.Sign < 0 && v.CompareTo(prior) < 0 ? Signal.StrongSell : v.Sign > 0 ? Signal.Buy : v.Sign < 0 ? Signal.Sell : Signal.None; prior = v; }
        return (new Dictionary<string, double[]> { ["FXSniper"] = line.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
