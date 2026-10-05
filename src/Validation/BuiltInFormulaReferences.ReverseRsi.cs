using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> ReverseRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return ReverseRsiOutputs(bars, Integer(options, "Length", 14), Number(options, 50, "RsiLevel"));
    }
    internal static IReadOnlyDictionary<string,double[]> ReverseRsiOutputs(IReadOnlyList<Bar> bars, int length, double target,
        ICollection<Signal>? signals = null)
    {
        if (length < 2 || !(target > 0 && target < 100)) throw new ArgumentOutOfRangeException(nameof(length));
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var gain = R(1); var loss = R(1); var t = R(target) / R(100); var previousSlope = R(0);
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var change = i == 0 ? R(0) : R(bars[i].Close) - R(bars[i-1].Close);
            gain = (R(length - 1) * gain + (change.Sign > 0 ? change : R(0))) / R(length);
            loss = (R(length - 1) * loss + (change.Sign < 0 ? R(0) - change : R(0))) / R(length);
            var numerator = R(length - 1) * (t * loss - (R(1) - t) * gain);
            var displacement = numerator / (numerator.Sign >= 0 ? R(1) - t : t);
            result[i] = (R(bars[i].Close) + displacement).ToDouble();
            var slope = R(0) - displacement;
            signals?.Add(slope.Sign > 0 ? slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : Signal.Buy
                : slope.Sign < 0 ? slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None);
            previousSlope = slope;
        }
        return Outputs(("Rersi", result));
    }
}
