using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AutoFilterOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return AutoFilterValues(bars, Integer(options, "Length", 500), AverageKind(options, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Steps) AutoFilterValues(IReadOnlyList<Bar> bars, int length, int kind, double[][]? external = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var n = new ReferenceFraction(length); var prices = bars.Select(b => R(b.Close)).ToArray(); var steps = new double[bars.Count];
        ReferenceFraction[] Prefix(ReferenceFraction[] input) { var result = new ReferenceFraction[input.Length + 1]; result[0] = R(0); for (var i = 0; i < input.Length; i++) result[i + 1] = result[i] + input[i]; return result; }
        ReferenceFraction Sum(ReferenceFraction[] prefix, int i) => prefix[i + 1] - prefix[Math.Max(0, i + 1 - length)];
        var priceSum = Prefix(prices); var priceSquares = Prefix(prices.Select(v => v * v).ToArray());
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = Sum(priceSum, i); var deviation = i + 1 < length ? 0 : (Sum(priceSquares, i) / n - sum * sum / (n * n)).SqrtToDouble();
            var previous = i == 0 ? bars[i].Close : steps[i - 1]; var upper = previous + deviation; var lower = previous - deviation;
            steps[i] = bars[i].Close > upper || bars[i].Close < lower ? bars[i].Close : previous;
        }
        var held = steps.Select(R).ToArray(); var heldSum = Prefix(held); var heldSquares = Prefix(held.Select(v => v * v).ToArray()); var products = Prefix(held.Select((v, i) => v * prices[i]).ToArray());
        var priceMean = external is null ? SmoothRocBankStage(prices, length, kind) : external[0].Select(R).ToArray();
        var stepMean = external is null ? SmoothRocBankStage(held, length, kind) : external[1].Select(R).ToArray();
        var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var priorSpread = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var centerX = Sum(heldSum, i) / n; var centerY = Sum(priceSum, i) / n;
            var variance = Sum(heldSquares, i) / n - centerX * centerX; var covariance = Sum(products, i) / n - centerX * centerY;
            var line = (i + 1 < length || variance.Sign == 0 ? priceMean[i] : priceMean[i] + covariance / variance * (held[i] - stepMean[i])).RoundExtendedBinary64();
            values[i] = line.ToDouble(); var spread = prices[i] - line;
            signals[i] = spread.Sign > 0 && spread.CompareTo(priorSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(priorSpread) < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None; priorSpread = spread;
        }
        return (new Dictionary<string, double[]> { ["Af"] = values }, signals, steps);
    }
}
