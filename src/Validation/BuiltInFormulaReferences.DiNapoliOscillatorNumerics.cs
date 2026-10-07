using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedDiNapoliOscillator(IReadOnlyList<Bar> bars, object options, bool percentage)
    {
        double[] Filter(double[] values, double period)
        {
            var coefficient = ReferenceFraction.FromDouble(2 / (1 + period));
            var complement = new ReferenceFraction(1) - coefficient;
            var result = new double[values.Length];
            double previous = 0;
            for (var i = 0; i < result.Length; i++)
            {
                previous = double.IsNaN(previous) || double.IsInfinity(previous) || double.IsNaN(values[i]) || double.IsInfinity(values[i])
                    ? double.NaN : (ReferenceFraction.FromDouble(values[i]) * coefficient + ReferenceFraction.FromDouble(previous) * complement).ToDouble();
                result[i] = previous;
            }
            return result;
        }
        double Difference(double left, double right) => double.IsNaN(left) || double.IsInfinity(left) || double.IsNaN(right) || double.IsInfinity(right)
            ? left - right : (ReferenceFraction.FromDouble(left) - ReferenceFraction.FromDouble(right)).ToDouble();
        var fast = Filter(Closes(bars), Number(options, 8.3896, "Sc"));
        var slow = Filter(Closes(bars), Number(options, 17.5185, "Lc"));
        var line = fast.Select((value, i) => !percentage ? Difference(value, slow[i])
            : double.IsNaN(value) || double.IsInfinity(value) || double.IsNaN(slow[i]) || double.IsInfinity(slow[i]) ? double.NaN
            : slow[i] == 0 ? 0 : (new ReferenceFraction(100) *
                (ReferenceFraction.FromDouble(value) / ReferenceFraction.FromDouble(slow[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
        var signal = Filter(line, Number(options, 9.0503, "Sp"));
        var histogram = line.Select((value, i) => Difference(value, signal[i])).ToArray();
        return percentage ? Outputs(("Ppo", line), ("Signal", signal), ("Histogram", histogram))
            : Outputs(("FastS", fast), ("SlowS", slow), ("Macd", line), ("Signal", signal), ("Histogram", histogram));
    }
}
