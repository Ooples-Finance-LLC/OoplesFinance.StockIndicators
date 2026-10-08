using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WilsonOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return WilsonValues(bars, (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!,
            Integer(options, "Length", 34), Integer(options, "SmoothLength", 1),
            Number(options, 70, "Overbought"), Number(options, 30, "Oversold"),
            Number(options, 55, "UpperNeutralZone"), Number(options, 45, "LowerNeutralZone")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) WilsonValues(IReadOnlyList<Bar> bars,
        MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int length = 34, int smooth = 1,
        double overbought = 70, double oversold = 30, double upper = 55, double lower = 45)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var code = AverageKind(new { MaType = kind }, 3);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => RationalAverage(values, length, code);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        // Net/absolute change identity provides an independent RSI construction.
        var changes = prices.Select((p, i) => i == 0 ? R(0) : p - prices[i - 1]).ToArray();
        var net = Mean(changes, length); var total = Mean(changes.Select(v => v.Abs()).ToArray(), length);
        var rsi = net.Select((v, i) => total[i].Sign == 0 ? R(100) : R(50) * (R(1) + v / total[i])).ToArray();
        var keys = new[] { "S1", "S2", "U1", "U2" }; var thresholds = new[] { oversold, lower, overbought, upper };
        var bands = thresholds.Select(t => Mean(rsi.Select(v => v - R(t)).ToArray(), smooth)
            .Select((d, i) => prices[i] * (R(1) - d / R(100))).ToArray()).ToArray();
        var trades = new Signal[prices.Length]; var previousBull = R(0); var previousBear = R(0);
        for (var i = 0; i < prices.Length; i++)
        {
            var bull = prices[i] - (bands[2][i].CompareTo(bands[3][i]) < 0 ? bands[2][i] : bands[3][i]);
            var bear = prices[i] - (bands[0][i].CompareTo(bands[1][i]) > 0 ? bands[0][i] : bands[1][i]);
            trades[i] = bull.Sign > 0 && bull.CompareTo(previousBull) > 0 ? Signal.StrongBuy
                : bear.Sign < 0 && bear.CompareTo(previousBear) < 0 ? Signal.StrongSell
                : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
            previousBull = bull; previousBear = bear;
        }
        return (keys.Select((key, i) => (key, values: bands[i].Select(v => v.ToDouble()).ToArray())).ToDictionary(x => x.key, x => x.values), trades);
    }
}
