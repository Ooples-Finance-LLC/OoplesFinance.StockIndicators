using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? SwissArmy(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.EhlersSwissArmyKnifeIndicator) return null;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 20);
        var angle = Clamp(2 * Math.PI / length, .01, .99);
        var bandwidth = Clamp(4 * Math.PI * Number(options, .1, "Delta") / length, .01, .99);
        var alpha = (Math.Cos(angle) + Math.Sin(angle) - 1) / Math.Cos(angle);
        var beta = 2.415 * (1 - Math.Cos(angle));
        var gaussian = Math.Sqrt(beta * (beta + 2)) - beta;
        var pole = 1 - gaussian;
        var band = 1 / Math.Cos(bandwidth) - Math.Sqrt(1 / Math.Pow(Math.Cos(bandwidth), 2) - 1);
        var bandA = Math.Cos(angle) * (1 + band);
        var keys = new[] { "EmaFilter", "SmaFilter", "GaussFilter", "ButterFilter", "SmoothFilter", "HpFilter", "PhpFilter", "BpFilter", "BsFilter" };
        return new("SmaFilter", keys, bars =>
        {
            var values = Closes(bars);
            double Price(int index) => index < 0 ? 0 : values[index];
            double[] Filter(double a1, double a2, Func<int, double> forcing, bool zeroSeed = false)
            {
                // Invert 1-a1*z^-1-a2*z^-2 analytically, then convolve its impulse response.
                var gap = Complex.Sqrt(a1 * a1 + 4 * a2);
                var first = (a1 + gap) / 2;
                var second = (a1 - gap) / 2;
                var impulse = Enumerable.Range(0, values.Length).Select(j => gap.Magnitude < 1e-12
                    ? ((j + 1) * Complex.Pow(first, j)).Real
                    : ((Complex.Pow(first, j + 1) - Complex.Pow(second, j + 1)) / gap).Real).ToArray();
                var drive = values.Select((v, i) => i <= length
                    ? zeroSeed ? 0 : v - a1 * Price(i - 1) - a2 * Price(i - 2)
                    : forcing(i)).ToArray();
                return values.Select((_, i) => i <= length ? zeroSeed ? 0 : values[i]
                    : Enumerable.Range(0, i + 1).Sum(j => impulse[i - j] * drive[j])).ToArray();
            }
            var smooth = values.Select((v, i) => (v + 2 * Price(i - 1) + Price(i - 2)) / 4).ToArray();
            return Outputs(
                (keys[0], Filter(1 - alpha, 0, i => alpha * values[i])),
                (keys[1], values.Select((v, i) => i <= length ? v : values[length]
                    + Enumerable.Range(length + 1, i - length).Sum(j => (values[j] - values[j - length]) / length)).ToArray()),
                (keys[2], Filter(2 * pole, -pole * pole, i => gaussian * gaussian * values[i])),
                (keys[3], Filter(2 * pole, -pole * pole, i => gaussian * gaussian * smooth[i])),
                (keys[4], smooth),
                (keys[5], Filter(1 - alpha, 0, i => (1 - alpha / 2) * (values[i] - Price(i - 1)), true)),
                (keys[6], Filter(2 * pole, -pole * pole, i => Math.Pow(1 - gaussian / 2, 2) * (values[i] - 2 * Price(i - 1) + Price(i - 2)), true)),
                (keys[7], Filter(bandA, -band, i => (1 - band) / 2 * (values[i] - Price(i - 2)))),
                (keys[8], Filter(bandA, -band, i => (1 + band) / 2 * (values[i] - 2 * Math.Cos(angle) * Price(i - 1) + Price(i - 2)))));
        });
    }
}
