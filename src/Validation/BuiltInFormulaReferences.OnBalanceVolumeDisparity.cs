using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget OnBalanceVolumeDisparityBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> OnBalanceVolumeDisparityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return OnBalanceVolumeDisparityValues(bars, Integer(options, "Length", 33), Integer(options, "SignalLength", 4),
            AverageKind(options, 1), Number(options, 1.1, "Top"), Number(options, .9, "Bottom")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Index) OnBalanceVolumeDisparityValues(
        IReadOnlyList<Bar> bars, int length, int signalLength, int kind = 1, double top = 1.1, double bottom = .9,
        double[]? selected = null)
    {
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0);
        var prices = (selected ?? Closes(bars)).Select(R).ToArray(); var indices = new ReferenceFraction[bars.Count]; var index = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? zero : prices[i - 1];
            var direction = prices[i].CompareTo(previous);
            if (direction > 0) index += R(bars[i].Volume);
            else if (direction < 0) index -= R(bars[i].Volume);
            indices[i] = index;
        }
        var priceMeans = DisparityReference.Mean(prices, length, kind);
        var indexMeans = DisparityReference.Mean(indices, length, kind);
        var pricePositions = DisparityReference.Coordinate(prices, priceMeans, length); var indexPositions = DisparityReference.Coordinate(indices, indexMeans, length);
        var line = pricePositions.Select((value, i) => indexPositions[i].Sign == 0 ? zero : value / indexPositions[i]).ToArray();
        var signal = DisparityReference.Mean(line, signalLength, kind);
        var trades = new Signal[bars.Count]; var previousLine = zero; var previousState = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var state = previousLine.CompareTo(R(bottom)) < 0 && line[i].CompareTo(R(bottom)) > 0 || line[i].CompareTo(signal[i]) > 0 ? 1
                : previousLine.CompareTo(R(top)) > 0 && line[i].CompareTo(R(top)) < 0 || line[i].CompareTo(R(bottom)) < 0 ? -1 : previousState;
            trades[i] = state > 0 && state > previousState ? Signal.StrongBuy : state < 0 && state < previousState ? Signal.StrongSell
                : state > 0 ? Signal.Buy : state < 0 ? Signal.Sell : Signal.None;
            previousLine = line[i]; previousState = state;
        }
        return (new Dictionary<string, double[]> { ["Obvdi"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades, indices.Select(v => v.ToDouble()).ToArray());
    }
}
