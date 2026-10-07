using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double PremierHalfTanhReference(double value)
    {
        var argument = ReferenceFraction.FromDouble(Math.Abs(value)) / new ReferenceFraction(2);
        double result;
        if (argument.CompareTo(ReferenceFraction.FromDouble(Math.Pow(2, -27))) < 0)
        {
            result = argument.ToDouble();
            // Taylor bounds put tanh(x) strictly below any positive midpoint x.
            // Detect the midpoint independently with exact rational subtraction.
            var halfUnit = ReferenceFraction.FromDouble(double.Epsilon) / new ReferenceFraction(2);
            if (result > 0 && (ReferenceFraction.FromDouble(result) - argument).CompareTo(halfUnit) == 0)
                result = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(result) - 1);
        }
        else result = argument.TanhToDouble();
        return value < 0 ? -result : result;
    }
    internal static IReadOnlyDictionary<string, double[]> PremierOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return PremierValues(bars, Integer(options, "Length", 8), Integer(options, "SmoothLength", 25), AverageKind(options, 3));
    }
    internal static IReadOnlyDictionary<string, double[]> PremierValues(IReadOnlyList<Bar> bars, int length, int smooth, int kind = 3)
    {
        length = Math.Max(1, length); var period = Math.Max(2, Math.Min(530, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, smooth)))));
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var raw = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            var lower = R(window.Min(b => b.Low)); var upper = R(window.Max(b => b.High)); var range = upper - lower;
            var position = range.Sign == 0 ? R(0) : (R(bars[i].Close) - lower) / range;
            if (position.Sign < 0) position = R(0); if (position.CompareTo(R(1)) > 0) position = R(1);
            raw[i] = (R(10) * position - R(5)).ToDouble();
        }
        double[] Mean(double[] values)
        {
            if (kind is not (1 or 2 or 3 or 6)) return Average(values, period, kind);
            var output = new double[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var previous = i == 0 ? R(0) : R(output[i - 1]);
                if (kind == 6) output[i] = ((previous * R(period - 1) + R(values[i])) / R(period)).ToDouble();
                else if (kind == 3 && i >= period) output[i] = ((previous * R(period - 1) + R(2) * R(values[i])) / R(period + 1L)).ToDouble();
                else if (kind == 1 && i + 1 < period) output[i] = 0;
                else
                {
                    var sum = R(0);
                    for (var j = Math.Max(0, i - period + 1); j <= i; j++) sum += R(values[j]) * R(kind == 2 ? period - i + j : 1);
                    output[i] = (sum / new ReferenceFraction(kind == 2 ? (long)period * (period + 1L) / 2 : Math.Min(i + 1, period))).ToDouble();
                }
            }
            return output;
        }
        return Outputs(("Pso", Mean(Mean(raw)).Select(PremierHalfTanhReference).ToArray()));
    }
}
