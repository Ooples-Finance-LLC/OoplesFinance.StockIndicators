using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ClosedDistanceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return ClosedDistanceValues(bars, Integer(options, "Length", 14), AverageKind(options, 3)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ClosedDistanceValues(IReadOnlyList<Bar> bars, int length, int kind, double[]? external = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        var prices = bars.Select(b => R(b.Close)).ToArray(); var means = external?.Select(R).ToArray() ?? SmoothRocBankStage(prices, length, kind); var previous = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var window = Window(bars, i, length).ToArray();
            if (window.Any(b => b.High < 0 || b.Low < 0)) throw new ArgumentOutOfRangeException(nameof(bars));
            var high = window.Aggregate(R(0), (s, b) => s + R(b.High)); var low = window.Aggregate(R(0), (s, b) => s + R(b.Low));
            double Root()
            {
                if (high.CompareTo(low) == 0) return 0;
                if (high.Sign == 0 || low.Sign == 0) return 1;
                var q = R(4) * high * low / ((high + low) * (high + low));
                int Compare(ReferenceFraction x) { var residual = R(1) - x * x; var squared = residual * residual; return (squared * squared).CompareTo(q); }
                // Search binary64's ordered nonnegative values, independently of
                // the production approximation and exact-unit midpoint repair.
                long left = 0, right = 0x3ff0000000000000;
                while (right - left > 1)
                {
                    var middle = left + (right - left) / 2; var value = BitConverter.Int64BitsToDouble(middle); var comparison = Compare(R(value));
                    if (comparison == 0) return value;
                    if (comparison > 0) left = middle; else right = middle;
                }
                var midpoint = (R(BitConverter.Int64BitsToDouble(left)) + R(BitConverter.Int64BitsToDouble(right))) / R(2);
                var side = Compare(midpoint); return BitConverter.Int64BitsToDouble(side > 0 || side == 0 && (left & 1) != 0 ? right : left);
            }
            values[i] = Root(); var residual = prices[i] - means[i];
            signals[i] = i > 0 && values[i] < values[i - 1] ? Signal.None : residual.Sign > 0 && residual.CompareTo(previous) > 0 ? Signal.StrongBuy
                : residual.Sign < 0 && residual.CompareTo(previous) < 0 ? Signal.StrongSell : residual.Sign > 0 ? Signal.Buy : residual.Sign < 0 ? Signal.Sell : Signal.None;
            previous = residual;
        }
        return (new Dictionary<string, double[]> { ["Cfdv"] = values }, signals);
    }
}
