using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (double[] Values, Signal[] Signals) SquelchValues(IReadOnlyList<Bar> bars, int length, int threshold, int horizon)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var chunk = new ReferenceFraction(BigInteger.One << 512); var upper = new ReferenceFraction(BigInteger.One << 256); var lower = new ReferenceFraction(1) / upper;
        ReferenceFraction Round(ReferenceFraction value)
        {
            if (value.Sign == 0) return value; var scale = new ReferenceFraction(1);
            while (value.Abs().CompareTo(lower) < 0) { value *= chunk; scale /= chunk; }
            while (value.Abs().CompareTo(upper) >= 0) { value /= chunk; scale *= chunk; }
            return R(value.ToDouble()) * scale;
        }
        length = Math.Max(1, length); threshold = Math.Max(1, threshold); horizon = Math.Max(1, horizon); var zero = new ReferenceFraction(0);
        var differences = new ReferenceFraction[bars.Count]; var real = new ReferenceFraction[bars.Count]; var imaginary = new ReferenceFraction[bars.Count]; var phases = new double[bars.Count]; var advances = new double[bars.Count]; var cycles = new double[bars.Count]; var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        ReferenceFraction Prior(ReferenceFraction[] source, int i) => i < 0 ? zero : source[i];
        for (var i = 0; i < bars.Count; i++)
        {
            differences[i] = i < length ? zero : Round(R(bars[i].Close) - R(bars[i - length].Close));
            var forcing = Round(R(.75) * (differences[i] - Prior(differences, i - length)) + R(.25) * (Prior(differences, i - 2) - Prior(differences, i - 4)));
            real[i] = Round(R(.33) * Prior(differences, i - 3) + R(.67) * Prior(real, i - 1)); imaginary[i] = Round(R(.2) * forcing + R(.8) * Prior(imaginary, i - 1));
            var numerator = imaginary[i] + Prior(imaginary, i - 1); var denominator = real[i] + Prior(real, i - 1); var phase = denominator.Sign == 0 ? 0 : (180 / Math.PI) * Math.Atan(Math.Abs((numerator / denominator).ToDouble()));
            if (real[i].Sign < 0 && imaginary[i].Sign > 0) phase = 180 - phase; if (real[i].Sign < 0 && imaginary[i].Sign < 0) phase = 180 + phase; if (real[i].Sign > 0 && imaginary[i].Sign < 0) phase = 360 - phase;
            phases[i] = phase; var previous = i == 0 ? 0 : phases[i - 1]; advances[i] = Math.Max(1, Math.Min(60, previous < 90 && phase > 270 ? 360 + previous - phase : previous - phase));
            double total = 0; var period = 0; for (var lag = 0; lag <= Math.Min(horizon, i); lag++) { total += advances[i - lag]; if (total > 360) { period = lag; break; } }
            cycles[i] = .25 * period + .75 * (i == 0 ? 0 : cycles[i - 1]); values[i] = cycles[i] < threshold ? 0 : 1;
            var current = real[i] + imaginary[i]; var older = Prior(real, i - 1) + Prior(imaginary, i - 1); signals[i] = current.Sign > 0 ? current.CompareTo(older) > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? current.CompareTo(older) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        return (values, signals);
    }
}
