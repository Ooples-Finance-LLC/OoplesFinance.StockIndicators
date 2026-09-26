using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EhlersCorrelationOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => EhlersCorrelations(indicator)!.Compute(bars);

    private static FormulaDefinition? EhlersCorrelations(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name is not (IndicatorName.EhlersCorrelationTrendIndicator or IndicatorName.EhlersCorrelationCycleIndicator
            or IndicatorName.EhlersCorrelationAngleIndicator or IndicatorName.EhlersMarketStateIndicator)) return null;
        var length = Integer(indicator.CreateOptions(), "Length", 20);
        var trend = name == IndicatorName.EhlersCorrelationTrendIndicator;
        var cycle = name == IndicatorName.EhlersCorrelationCycleIndicator;
        var marketState = name == IndicatorName.EhlersMarketStateIndicator;
        var key = marketState ? "Emsi" : trend ? "Ecti" : cycle ? "Real" : "Cai";
        return new(key, cycle ? new[] { "Real", "Imag" } : new[] { key }, bars =>
        {
            double Correlation(double[] x, double[] y)
            {
                var count = new ReferenceFraction(x.Length);
                var xx = x.Select(ReferenceFraction.FromDouble).ToArray();
                var yy = y.Select(ReferenceFraction.FromDouble).ToArray();
                var mx = xx.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / count;
                var my = yy.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / count;
                var covariance = new ReferenceFraction(0); var vx = new ReferenceFraction(0); var vy = new ReferenceFraction(0);
                for (var j = 0; j < x.Length; j++)
                {
                    var dx = xx[j] - mx; var dy = yy[j] - my;
                    covariance += dx * dy; vx += dx * dx; vy += dy * dy;
                }
                return vx.Sign == 0 || vy.Sign == 0 ? 0 : covariance.Sign * (covariance * covariance / (vx * vy)).SqrtToDouble();
            }
            var cosine = Enumerable.Range(0, length).Select(j => Math.Cos(2 * Math.PI * ((double)j / length))).ToArray();
            var sine = Enumerable.Range(0, length).Select(j => length <= 2 ? 0 : -Math.Sin(2 * Math.PI * ((double)j / length))).ToArray();
            var ramp = Enumerable.Range(0, length).Select(j => -(double)j).ToArray();
            var real = new double[bars.Count];
            var imaginary = new double[bars.Count];
            var angles = new double[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                var observations = Enumerable.Range(0, length).Select(j => i < j ? 0 : bars[i - j].Close).ToArray();
                real[i] = Correlation(observations, trend ? ramp : cosine);
                if (trend) continue;
                imaginary[i] = Correlation(observations, sine);
                var r = Math.Abs(real[i]) < 1e-12 ? 0 : real[i];
                var im = Math.Abs(imaginary[i]) < 1e-12 ? 0 : imaginary[i];
                var angle = r == 0 && im == 0 ? 90 : im == 0 ? (r < 0 ? 180 : 0)
                    : new Complex(r, -im).Phase * 180 / Math.PI;
                var previous = i == 0 ? 0 : angles[i - 1];
                angles[i] = angle < previous && previous - angle < 270 ? previous : angle;
            }
            if (marketState) return Outputs((key, angles.Select((angle, i) =>
                Math.Abs(angle - (i == 0 ? 0 : angles[i - 1])) >= 9 - 1e-10 ? 0d : angle < 0 ? -1d : 1d).ToArray()));
            return trend ? Outputs((key, real)) : cycle ? Outputs(("Real", real), ("Imag", imaginary)) : Outputs((key, angles));
        });
    }
}
