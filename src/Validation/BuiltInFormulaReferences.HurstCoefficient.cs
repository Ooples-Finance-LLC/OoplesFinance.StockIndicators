using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HurstCoefficientOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => HurstCoefficientValues(bars, Integer(indicator.CreateOptions(), "Length1", 30), Integer(indicator.CreateOptions(), "Length2", 20)).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Dimensions, double[] Hurst) HurstCoefficientValues(IReadOnlyList<Bar> bars, int length, int smooth)
    {
        length = Math.Max(1, length); smooth = Math.Max(1, smooth); var half = (int)Math.Ceiling(length / 2d);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var prices = Closes(bars); var angle = Math.Sqrt(2) * Math.PI / smooth;
        var radius = R(Math.Exp(-angle)); var cosine = R(Math.Cos(Math.Min(angle, .99)));
        var gain = (R(1) - radius) * (R(1) - radius) + R(2) * radius * (R(1) - cosine);
        var feedback = R(2) * radius * cosine; var decay = radius * radius;
        ReferenceFraction Range(IEnumerable<double> values) { var a = values.ToArray(); return R(a.Max()) - R(a.Min()); }
        double Log(ReferenceFraction ratio)
        {
            var exponent = 0;
            while (ratio.CompareTo(R(2)) >= 0) { ratio /= R(2); exponent++; }
            while (ratio.CompareTo(R(1)) < 0) { ratio *= R(2); exponent--; }
            // The normalized argument is rounded once before the platform log.
            return exponent + Math.Log(ratio.ToDouble()) / Math.Log(2);
        }
        var dimension = new double[bars.Count]; var hurst = new double[bars.Count]; var output = new double[bars.Count]; var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var start = (int)Math.Max(0L, i - (long)length + 1); var recentStart = (int)Math.Max(0L, i - (long)half + 1);
            var full = Range(prices.Skip(start).Take(i - start + 1)); var recent = Range(prices.Skip(recentStart).Take(i - recentStart + 1));
            var older = prices.Skip(start).Take(Math.Max(0, i - half - start + 1)).Append(i < half ? prices[i] : prices[i - half]);
            if (length > half && i < length - 1L) older = older.Append(0);
            var halves = recent + Range(older); var previous = i == 0 ? R(0) : R(dimension[i - 1]);
            dimension[i] = full.Sign > 0 && halves.Sign > 0 ? ((R(Log(halves / R(half) / (full / R(length)))) + previous) / R(2)).ToDouble() : previous.ToDouble();
            hurst[i] = (R(2) - R(dimension[i])).ToDouble();
            var driven = (R(hurst[i]) + (i == 0 ? R(0) : R(hurst[i - 1]))) / R(2);
            output[i] = (gain * driven + (i == 0 ? R(0) : feedback * R(output[i - 1])) - (i < 2 ? R(0) : decay * R(output[i - 2]))).ToDouble();
            var slope = R(output[i]) - (i == 0 ? R(0) : R(output[i - 1])); var oldSlope = (i == 0 ? R(0) : R(output[i - 1])) - (i < 2 ? R(0) : R(output[i - 2]));
            trades[i] = slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Ehc"] = output }, trades, dimension, hurst);
    }
}
