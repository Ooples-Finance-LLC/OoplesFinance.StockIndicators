using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? MesaPredictionFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not EhlersMesaPredictIndicatorV1SpecOptions options) return null;
        return new("Predict", new[] { "Ssf", "Predict", "PrePredict" }, bars =>
        {
            var prices = Closes(bars); var exact = prices.Select(BinaryDecimal).ToArray();
            var angle = Math.Sqrt(2)*Math.PI/options.UpperLength;
            var pole = Complex.FromPolarCoordinates(Math.Exp(-angle), angle);
            var gain = (1+2*pole.Real+pole.Magnitude*pole.Magnitude)/4;
            // Expand the high-pass denominator into its conjugate-pole impulse response.
            var impulse = Enumerable.Range(0, bars.Count).Select(age =>
                Math.Pow(pole.Magnitude, age)*Math.Sin((age+1)*angle)/Math.Sin(angle)).ToArray();
            var drive = exact.Select((v, i) => i < 4 ? 0m : v-2*exact[i-1]+exact[i-2]).ToArray();
            var high = exact.Select((_, i) => (double)Enumerable.Range(0, i+1)
                .Sum(j => drive[j]*BinaryDecimal(gain*impulse[i-j]))).ToArray();
            var lowAngle = Math.Sqrt(2)*Math.PI/options.LowerLength;
            var ssf = HilbertLowPass(high, Math.Exp(-lowAngle), lowAngle);
            var coefficients = new List<decimal[]>(); var prediction = new double[bars.Count];
            var weights = Enumerable.Range(1, options.LowerLength).Select(j =>
                BinaryDecimal(1-Math.Cos(2*Math.PI*j/(options.LowerLength+1)))).ToArray();
            for (var i = 0; i < bars.Count; i++)
            {
                var sample = Enumerable.Range(0, options.UpperLength).Select(j =>
                    i-options.UpperLength+1+j < 0 ? 0 : ssf[i-options.UpperLength+1+j]).ToArray();
                var scale = sample.Max(Math.Abs); var polynomial = new[] { 1m };
                if (scale > 64*Math.Pow(2, -52)*Math.Max(Math.Abs(prices[i]), i == 0 ? 0 : Math.Abs(prices[i-1])))
                {
                    var forward = sample.Skip(1).Select(v => BinaryDecimal(v/scale)).ToArray();
                    var backward = sample.Take(sample.Length-1).Select(v => BinaryDecimal(v/scale)).ToArray();
                    for (var order = 1; order <= options.Length2; order++)
                    {
                        var cross = forward.Zip(backward, (f, b) => f*b).Sum();
                        var energy = forward.Sum(v => v*v)+backward.Sum(v => v*v);
                        var reflection = energy == 0 ? 0 : Math.Max(-1m, Math.Min(1m, 2*cross/energy));
                        // Prediction-error polynomial A(z) - reflection*z^order*A(1/z).
                        var extended = polynomial.Concat(new[] { 0m }).ToArray();
                        polynomial = extended.Select((v, k) => v-reflection*extended[order-k]).ToArray();
                        var nextForward = forward.Skip(1).Zip(backward.Skip(1), (f, b) => f-reflection*b).ToArray();
                        backward = backward.Take(backward.Length-1).Zip(forward, (b, f) => b-reflection*f).ToArray();
                        forward = nextForward;
                    }
                }
                var fitted = Enumerable.Range(1, options.Length2).Select(k => k < polynomial.Length ? -polynomial[k] : 0m).ToArray();
                coefficients.Add(fitted);
                var smoothed = fitted.Select((_, k) => Enumerable.Range(0, Math.Min(i+1, weights.Length))
                    .Sum(lag => weights[lag]*coefficients[i-lag][k])/weights.Sum()).ToArray();
                var path = sample.Select(BinaryDecimal).ToList();
                for (var step = 0; step < options.Length1; step++)
                    path.Add(smoothed.Select((value, lag) => value*path[path.Count-1-lag]).Sum());
                prediction[i] = (double)path[path.Count-1];
            }
            return Outputs(("Ssf", ssf), ("PrePredict", prediction),
                ("Predict", prediction.Select((v, i) => (v+(i == 0 ? 0 : prediction[i-1]))/2).ToArray()));
        });
    }
}
