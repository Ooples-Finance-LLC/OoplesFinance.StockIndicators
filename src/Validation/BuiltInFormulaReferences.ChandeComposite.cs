using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ChandeCompositeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => ChandeCompositeValues(bars, 5, 10, 20, 3, 4).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[][] Ratios) ChandeCompositeValues(IReadOnlyList<Bar> bars, int length1, int length2, int length3, int smoothLength, int kind, double[][]? external = null)
    {
        length1 = Math.Max(1, length1); smoothLength = Math.Max(1, smoothLength); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var changes = prices.Select((p, i) => i == 0 ? R(0) : p - prices[i - 1]).ToArray();
        var periods = new[] { length1, Math.Max(1, length2), Math.Max(1, length3) }; var weights = new double[3][]; var ratios = new double[3][]; var smoothed = new ReferenceFraction[3][];
        for (var leg = 0; leg < 3; leg++)
        {
            var period = periods[leg]; weights[leg] = new double[bars.Count]; ratios[leg] = new double[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                var steps = Window(changes, i, period).ToArray(); var distance = steps.Aggregate(R(0), (a, b) => a + b.Abs());
                ratios[leg][i] = distance.Sign == 0 ? 0 : (R(100) * steps.Aggregate(R(0), (a, b) => a + b) / distance).ToDouble();
                if (i < period - 1) continue;
                var values = Window(prices, i, period).ToArray(); var mean = values.Aggregate(R(0), (a, b) => a + b) / new ReferenceFraction(period);
                weights[leg][i] = (values.Aggregate(R(0), (a, b) => a + (b - mean) * (b - mean)) / new ReferenceFraction(period)).SqrtToDouble();
            }
            if (external is not null) smoothed[leg] = external[leg].Select(R).ToArray();
            else if (kind == 4)
            {
                var first = SmoothRocBankStage(ratios[leg].Select(R).ToArray(), smoothLength, 3); var second = SmoothRocBankStage(first, smoothLength, 3);
                smoothed[leg] = first.Select((v, i) => R((R(2) * v - second[i]).ToDouble())).ToArray();
            }
            else smoothed[leg] = SmoothRocBankStage(ratios[leg].Select(R).ToArray(), smoothLength, kind);
        }
        var composite = new ReferenceFraction[bars.Count]; var line = new double[bars.Count]; var signalLine = new double[bars.Count]; var signals = new Signal[bars.Count]; var previous = R(0); var previousSpread = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var total = Enumerable.Range(0, 3).Aggregate(R(0), (a, j) => a + R(weights[j][i]));
            var raw = total.Sign == 0 ? R(0) : Enumerable.Range(0, 3).Aggregate(R(0), (a, j) => a + R(weights[j][i]) * smoothed[j][i]) / total;
            composite[i] = R(Clamp(raw.ToDouble(), -100, 100)); var window = Window(composite, i, length1).ToArray();
            signalLine[i] = (window.Aggregate(R(0), (a, b) => a + b) / new ReferenceFraction(window.Length)).ToDouble();
            line[i] = ((previous * new ReferenceFraction(smoothLength - 1L) + R(2) * composite[i]) / new ReferenceFraction(smoothLength + 1L)).ToDouble();
            var current = R(line[i]); var spread = current - R(signalLine[i]);
            signals[i] = spread.Sign > 0 && spread.CompareTo(previousSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(previousSpread) < 0 ? Signal.StrongSell
                : spread.Sign > 0 || previous.CompareTo(R(-70)) < 0 && current.CompareTo(R(-70)) > 0 ? Signal.Buy : spread.Sign < 0 || previous.CompareTo(R(70)) > 0 && current.CompareTo(R(70)) < 0 ? Signal.Sell : Signal.None;
            previous = current; previousSpread = spread;
        }
        return (new Dictionary<string, double[]> { ["Ccmi"] = line, ["Signal"] = signalLine }, signals, ratios);
    }
}
