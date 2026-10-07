using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WaveTrendOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => WaveTrendValues(bars, MovingAvgType.ExponentialMovingAverage, Integer(indicator.CreateOptions(), "Length", 10), 21, 4,
            indicator is IIndicator value && value.Source is not null).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) WaveTrendValues(IReadOnlyList<Bar> bars, MovingAvgType kind,
        int channel, int average, int signal, bool selected = false, bool hlc = false)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var code = AverageKind(new { MaType = kind }, 3);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => RationalAverage(values, length, code);
        var prices = bars.Select(b => selected ? R(b.Close) : hlc ? (R(b.High) + R(b.Low) + R(b.Close)) / R(3)
            : (R(b.Open) + R(b.High) + R(b.Low) + R(b.Close)) / R(4)).ToArray();
        var basis = Mean(prices, channel); var residual = prices.Select((v, i) => v - basis[i]).ToArray();
        var deviation = Mean(residual.Select(v => v.Abs()).ToArray(), channel);
        var normalized = residual.Select((v, i) => deviation[i].Sign == 0 ? R(0) : (v / deviation[i]) / R(.015)).ToArray();
        var line = Mean(normalized, average); var trigger = Mean(line, signal); var trades = new Signal[prices.Length];
        for (var i = 0; i < trades.Length; i++)
        {
            var previous = i == 0 ? R(0) : line[i - 1]; var margin = line[i] - trigger[i];
            var before = i == 0 ? R(0) : line[i - 1] - trigger[i - 1];
            if (margin.Sign > 0 && margin.CompareTo(before) > 0) trades[i] = Signal.StrongBuy;
            else if (margin.Sign < 0 && margin.CompareTo(before) < 0) trades[i] = Signal.StrongSell;
            else if (margin.Sign > 0 || previous.CompareTo(R(-53)) < 0 && line[i].CompareTo(R(-53)) > 0) trades[i] = Signal.Buy;
            else if (margin.Sign < 0 || previous.CompareTo(R(53)) > 0 && line[i].CompareTo(R(53)) < 0) trades[i] = Signal.Sell;
        }
        return (new Dictionary<string, double[]> { ["Wto"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = trigger.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
