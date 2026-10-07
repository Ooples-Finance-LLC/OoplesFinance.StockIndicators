using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Quan-style Jurik adaptive smoothing with bounded histories and wide recurrence stages.</summary>
public static class JurikAdaptiveSnapshot
{
    /// <summary>Calculates the published volatility-adaptive recurrence from fresh state.</summary>
    /// <remarks>Each recurrence stage rounds once with extended upper exponent; only unrepresentable published values are rejected.
    /// Period one preserves the initial price, as in the native recurrence. Histories grow only with observed bars.</remarks>
    public static IReadOnlyList<double> Calculate(
        IReadOnlyList<Bar> bars,
        int period = 20,
        double phase = 0,
        int volatilityPeriod = 10
    )
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (period < 1 || volatilityPeriod < 1 || !FrameworkCompatibility.IsFinite(phase))
            throw new ArgumentOutOfRangeException(nameof(period));
        foreach (var b in bars)
            if (!FrameworkCompatibility.IsFinite(b.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var result = new double[bars.Count];
        if (bars.Count == 0)
            return result;
        var grid = BigInteger.One << 1074;
        BigInteger U(double x) => ExactVarianceWindow.Units(x);
        BigInteger Stage(BigInteger n, BigInteger d) => RocBankValue.RoundUnits(n, d);
        BigInteger Blend(BigInteger old, BigInteger next, double weight) =>
            Stage(old * (grid - U(weight)) + next * U(weight), grid);
        var beta = .45 * (period - 1) / (.45 * (period - 1) + 2);
        var length = Math.Max(Math.Log(Math.Sqrt(period - 1)) / Math.Log(2) + 2, 0);
        var power = Math.Max(length - 2, .5);
        var maximum = Math.Pow(length, 1 / power);
        var phaseGain = FrameworkCompatibility.Clamp(phase * .01 + 1.5, .5, 2.5) + 1;
        var shortHistory = new Queue<BigInteger>();
        var prices = new Queue<BigInteger>();
        BigInteger upper = 0,
            lower = 0,
            ma = U(bars[0].Close),
            det0 = 0,
            det1 = 0,
            jma = ma,
            vsum = 0,
            avolty = 0;
        for (var i = 0; i < bars.Count; i++)
        {
            var input = U(bars[i].Close);
            prices.Enqueue(input);
            if (prices.Count > period)
                prices.Dequeue();
            if (i == 0)
            {
                result[i] = bars[i].Close;
                continue;
            }
            var high = prices.Max();
            var low = prices.Min();
            var dh = high - upper;
            var dl = low - lower;
            var volty = BigInteger.Max(BigInteger.Abs(dh), BigInteger.Abs(dl));
            shortHistory.Enqueue(volty);
            if (shortHistory.Count > volatilityPeriod)
                shortHistory.Dequeue();
            vsum = Stage(vsum * grid + U(.1) * (volty - shortHistory.Peek()), grid);
            avolty = Blend(avolty, vsum, 2 / (Math.Max(4d * period, 30) + 1));
            var relative =
                avolty.Sign <= 0 ? 0 : ExactMeanAccumulator.UnitRatio(volty * grid, avolty);
            relative = Math.Min(Math.Max(relative, 1), maximum);
            var pow = Math.Pow(relative, power);
            var len2 = Math.Sqrt(.5 * (period - 1)) * length;
            var kv = Math.Pow(len2 / (len2 + 1), Math.Sqrt(pow));
            upper = dh.Sign > 0 ? high : Stage(high * grid - U(kv) * dh, grid);
            lower = dl.Sign < 0 ? low : Stage(low * grid - U(kv) * dl, grid);
            var alpha = Math.Pow(beta, pow);
            ma = Blend(input, ma, alpha);
            det0 = Blend(input - ma, det0, beta);
            var ma2 = Stage(ma * grid + U(phaseGain) * det0, grid);
            det1 = Stage(
                U((1 - alpha) * (1 - alpha)) * (ma2 - jma) + U(alpha * alpha) * det1,
                grid
            );
            jma = Stage(jma + det1, 1);
            var value = ExactMeanAccumulator.UnitRatio(jma, 1);
            if (!FrameworkCompatibility.IsFinite(value))
                throw new OverflowException("Jurik output is not representable.");
            result[i] = value;
        }
        return result;
    }
}
