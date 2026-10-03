using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? WindowFormula(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        string key;
        Func<int, double> weight;
        switch (indicator.BatchName)
        {
            case IndicatorName.DampedSineWaveWeightedFilter:
                length = Math.Max(3, length);
                key = "Dswwf";
                // Truncated sinc kernel: the common angular scale cancels in normalization.
                weight = j =>
                {
                    var angle = 2 * Math.PI * (j + 1) / length;
                    return Math.Sin(angle) / angle;
                };
                break;
            case IndicatorName.PolynomialLeastSquaresMovingAverage:
                key = "Plsma";
                // Integrate the kernel 2x + pi*sum(cos(k*pi*x), k=1..3) over each lag cell.
                // The sine-difference identity avoids copying the endpoint subtraction loop.
                weight = j => (2d * j + 1) / (length * (double)length)
                    + Enumerable.Range(1, 3).Sum(k => 2 * Math.Sin(k * Math.PI / (2 * length))
                        * Math.Cos(k * Math.PI * (2d * j + 1) / (2 * length)) / k);
                break;
            case IndicatorName.RightSidedRickerMovingAverage:
                var rickerWidth = Number(options, 60, "PctWidth") * length / 100;
                key = "Rsrma";
                // Negative second derivative of a Gaussian, up to a common normalization.
                weight = j => (rickerWidth * rickerWidth - j * (double)j) * Math.Exp(-.5 * Math.Pow(j / rickerWidth, 2));
                break;
            case IndicatorName.HendersonWeightedMovingAverage:
                var halfWidth = Math.Max(2, Math.Min(530, (length - 1) / 2));
                key = "Hwma";
                weight = j =>
                {
                    var x = j - halfWidth;
                    // Henderson's cubic-preserving graduation polynomial; factors common
                    // to every tap cancel when this kernel is normalized below.
                    var product = 1d;
                    for (var k = 1; k <= 3; k++) product *= (halfWidth + k - x) * (double)(halfWidth + k + x);
                    return product * (3 * Math.Pow(halfWidth + 2, 2) - 11 * x * (double)x - 16);
                };
                break;
            case IndicatorName.QuickMovingAverage:
                var peak = Math.Max(2, Math.Min(530, (length + 2) / 3));
                var quickPeriod = length;
                length++;
                key = "Qma";
                // Asymmetric triangular FIR, peaking about one third into the lag window.
                // The final tap is normally zero; period one retains the minimum peak of two.
                weight = j => j + 1 <= peak ? (j + 1d) / peak : (quickPeriod - j) / (quickPeriod + 1d - peak);
                break;
            case IndicatorName.Spencer15PointMovingAverage:
            case IndicatorName.Spencer21PointMovingAverage:
                var fifteen = indicator.BatchName == IndicatorName.Spencer15PointMovingAverage;
                key = fifteen ? "S15ma" : "S21ma";
                // Spencer's graduation filters factor into three boxcar polynomials
                // and a short symmetric correction polynomial. Derive the kernel instead
                // of repeating the production coefficient table.
                var kernel = fifteen ? new double[] { -3, 3, 4, 3, -3 } : new double[] { -1, 0, 1, 2, 1, 0, -1 };
                foreach (var width in fifteen ? new[] { 4, 4, 5 } : new[] { 5, 5, 7 })
                {
                    var convolution = new double[kernel.Length + width - 1];
                    for (var j = 0; j < kernel.Length; j++)
                        for (var k = 0; k < width; k++) convolution[j + k] += kernel[j];
                    kernel = convolution;
                }
                length = kernel.Length;
                weight = j => kernel[j];
                break;
            case IndicatorName.SimplifiedWeightedMovingAverage:
                key = "Swma"; weight = j => length - j; break;
            case IndicatorName.SimplifiedLeastSquaresMovingAverage:
                // OLS prediction at the newest point of the zero-padded window.
                key = "Slsma";
                weight = j => 6d * (length - j) / (length + 1d) - 2;
                break;
            case IndicatorName.ArnaudLegouxMovingAverage:
                var offset = Number(options, .85, "Offset");
                var sigma = Number(options, 6, "Sigma");
                key = "Alma";
                weight = j => Math.Exp(-.5 * Math.Pow((length - 1 - j - offset * (length - 1)) * sigma / length, 2));
                break;
            case IndicatorName.LeastSquaresMovingAverage:
                return new("Lsma", new[] { "Lsma" }, bars => Outputs(("Lsma", bars.Select((_, i) =>
                {
                    // Full-window OLS endpoint weights, with the published partial-window
                    // convention of 3*zero-padded WMA until the SMA window is available.
                    return Enumerable.Range(0, Math.Min(length, i + 1)).Sum(j => bars[i - j].Close
                        * (6d * (length - j) / (length * (length + 1d)) - (i + 1 >= length ? 2d / length : 0)));
                }).ToArray())));
            case IndicatorName.LinearWeightedMovingAverage:
                key = "Lwma"; weight = j => length - j; break;
            case IndicatorName.CubedWeightedMovingAverage:
                key = "Cwma"; weight = j => Math.Pow(length - j, 3); break;
            case IndicatorName.ParabolicWeightedMovingAverage:
                key = "Pwma"; weight = j => Math.Pow(length - j, 2); break;
            case IndicatorName.SquareRootWeightedMovingAverage:
                key = "Srwma"; weight = j => Math.Sqrt(length - j); break;
            case IndicatorName.SineWeightedMovingAverage:
                key = "Swma"; weight = j => Math.Sin(Math.PI * (j + 1) / (length + 1)); break;
            case IndicatorName.SymmetricallyWeightedMovingAverage:
                key = "Swma"; weight = j => Math.Min(j + 1, length - j); break;
            case IndicatorName.EhlersTriangleMovingAverage:
                key = "Etma"; weight = j => Math.Min(j + 1, length - j); break;
            case IndicatorName.EhlersHannMovingAverage:
                key = "Ehma"; weight = j => Math.Pow(Math.Sin(Math.PI * (j + 1) / (length + 1)), 2); break;
            case IndicatorName.EhlersHammingMovingAverage:
                var pedestal = Number(options, 3, "Pedestal");
                key = "Ehma"; weight = j => length == 1 ? 1 : Math.Cos(pedestal - Math.PI / 2
                    + (Math.PI - 2 * pedestal) * j / (length - 1)); break;
            case IndicatorName.FibonacciWeightedMovingAverage:
                key = "Fwma";
                var phi = (1 + Math.Sqrt(5)) / 2;
                var psi = (1 - Math.Sqrt(5)) / 2;
                weight = j => Math.Round((Math.Pow(phi, length - j) - Math.Pow(psi, length - j)) / Math.Sqrt(5));
                break;
            case IndicatorName.GeometricMovingAverage:
                return new("Gma", new[] { "Gma" }, bars => Outputs(("Gma", RoundedGeometricMean(bars, length))));
            default: return null;
        }
        // The convolution is normalized once; samples before the first bar are zero.
        var weights = Enumerable.Range(0, length).Select(weight).ToArray();
        var denominator = weights.Sum();
        return new(key, new[] { key }, bars => Outputs((key, bars.Select((_, i) => denominator == 0 ? 0
            : Enumerable.Range(0, Math.Min(length, i + 1)).Sum(j => bars[i - j].Close * weights[j]) / denominator).ToArray())));
    }
}
