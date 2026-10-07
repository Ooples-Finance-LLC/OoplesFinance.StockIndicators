using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PolarizedEfficiencyOutputs(IReadOnlyList<Bar> bars, int length, int smooth, int kind = 3)
    {
        length = Math.Max(1, length); smooth = Math.Max(1, smooth);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction Norm(ReferenceFraction delta, int horizontal)
        {
            var square = delta * delta + new ReferenceFraction((long)horizontal * horizontal);
            var root = square.SqrtToDouble();
            return double.IsInfinity(root) ? R((square / R(4)).SqrtToDouble()) * R(2) : R(root);
        }
        var prices = Closes(bars).Select(R).ToArray(); var legs = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++) legs[i] = Norm(i == 0 ? R(0) : prices[i] - prices[i - 1], 1);
        var raw = new double[bars.Count];
        for (var i = length; i < bars.Count; i++)
        {
            var path = R(0); for (var j = i - length + 1; j <= i; j++) path += legs[j];
            var displacement = prices[i] - prices[i - length];
            var ratio = (R(100) * Norm(displacement, length) / path).ToDouble();
            raw[i] = displacement.Sign * Math.Min(100, ratio);
        }
        if (kind is not (1 or 2 or 3 or 6)) return Outputs(("Pfe", Average(raw, smooth, kind)));
        var output = new double[bars.Count];
        for (var i = 0; i < output.Length; i++)
        {
            var previous = i == 0 ? R(0) : R(output[i - 1]);
            if (kind == 6) output[i] = ((previous * R(smooth - 1) + R(raw[i])) / R(smooth)).ToDouble();
            else if (kind == 3 && i >= smooth) output[i] = ((previous * R(smooth - 1) + R(2) * R(raw[i])) / R(smooth + 1L)).ToDouble();
            else if (kind == 1 && i + 1 < smooth) output[i] = 0;
            else
            {
                var sum = R(0);
                for (var j = Math.Max(0, i - smooth + 1); j <= i; j++) sum += R(raw[j]) * R(kind == 2 ? smooth - i + j : 1);
                output[i] = (sum / new ReferenceFraction(kind == 2 ? (long)smooth * (smooth + 1L) / 2 : Math.Min(i + 1, smooth))).ToDouble();
            }
        }
        return Outputs(("Pfe", output));
    }
}
