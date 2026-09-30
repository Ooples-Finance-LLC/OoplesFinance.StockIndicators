using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DrunkardWalkOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => DrunkardWalkValues(bars, Integer(indicator.CreateOptions(), "Length1", 80)).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) DrunkardWalkValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction Abs(ReferenceFraction v) => v.Sign < 0 ? R(0) - v : v;
        var up = new double[bars.Count]; var down = new double[bars.Count]; var signals = new Signal[bars.Count];
        var previousUp = R(0); var previousDown = R(0); var oldSpread = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var h = R(b.High); var l = R(b.Low); var previous = R(i == 0 ? b.Close : bars[i - 1].Close);
            var ranges = new[] { h - l, Abs(h - previous), Abs(l - previous) }; var range = ranges.Aggregate((a, v) => a.CompareTo(v) > 0 ? a : v).RoundExtendedBinary64();
            var start = (int)Math.Max(0L, i - (long)length + 1); var indexes = Enumerable.Range(start, i - start + 1).ToArray();
            var low = indexes.Min(j => bars[j].Low); var high = indexes.Max(j => bars[j].High);
            var upAge = i - indexes.Last(j => bars[j].Low.Equals(low)); var downAge = i - indexes.Last(j => bars[j].High.Equals(high)); // NOSONAR: S1244 - Exact equality locates the newest actual extremum observation; a tolerance would change its age.
            ReferenceFraction Mean(ReferenceFraction old, int age)
            {
                var gain = age == 0 ? 0 : 1d / age;
                return ((range * R(gain)).RoundExtendedBinary64() + (old * R(1 - gain)).RoundExtendedBinary64()).RoundExtendedBinary64();
            }
            previousUp = Mean(previousUp, upAge); previousDown = Mean(previousDown, downAge);
            ReferenceFraction Walk(ReferenceFraction distance, ReferenceFraction mean, int age)
            {
                if (age == 0) return R(0);
                var root = R(new ReferenceFraction(age).SqrtToDouble()); var denominator = (root * (mean.Sign > 0 ? mean : R(1))).RoundExtendedBinary64();
                return (distance.RoundExtendedBinary64() / denominator).RoundExtendedBinary64();
            }
            var u = Walk(h - R(low), previousUp, upAge); var d = Walk(R(high) - l, previousDown, downAge); up[i] = u.ToDouble(); down[i] = d.ToDouble(); var spread = u - d;
            signals[i] = spread.Sign > 0 && spread.CompareTo(oldSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(oldSpread) < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
            oldSpread = spread;
        }
        return (new Dictionary<string, double[]> { ["UpWalk"] = up, ["DnWalk"] = down }, signals);
    }
}
