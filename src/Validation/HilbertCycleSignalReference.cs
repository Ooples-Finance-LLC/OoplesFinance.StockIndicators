using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class HilbertCycleSignalReference
{
    private static ReferenceFraction F(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction R(ReferenceFraction v) => v.RoundExtendedBinary64();

    internal static double?[][] Values(
        IReadOnlyList<Bar> bars,
        HilbertCycleSignal kind,
        int suppression
    )
    {
        var outputs = kind == HilbertCycleSignal.Sine ? 2 : 1;
        var result = Enumerable.Range(0, outputs).Select(_ => new double?[bars.Count]).ToArray();
        if (63L + suppression >= bars.Count)
            return result;
        var periods = SeededPhaseReference.Cycle(bars, 0, 37, 37)[2];
        var z = F(0);
        var smooth = Enumerable.Repeat(z, bars.Count).ToArray();
        var means = Enumerable.Repeat(z, bars.Count).ToArray();
        double phase = 0,
            sine = 0,
            lead = 0;
        long days = 0;
        for (var i = 37; i < bars.Count; i++)
        {
            smooth[i] = R(
                (
                    F(4) * F(bars[i].Close)
                    + F(3) * F(bars[i - 1].Close)
                    + F(2) * F(bars[i - 2].Close)
                    + F(bars[i - 3].Close)
                ) / F(10)
            );
            var period = periods[i]!.Value;
            var count = (int)(period + .5);
            var real = z;
            var imaginary = z;
            for (var lag = 0; lag < count; lag++)
            {
                var angle = lag * 2d * Math.PI / count;
                real += F(Math.Sin(angle)) * smooth[i - lag];
                imaginary += F(Math.Cos(angle)) * smooth[i - lag];
            }
            real = R(real);
            imaginary = R(imaginary);
            var next =
                imaginary.Sign != 0
                    ? Math.Atan((real / imaginary).ToDouble()) * 180 / Math.PI
                    : phase
                        + (
                            real.Sign < 0 ? -90
                            : real.Sign > 0 ? 90
                            : 0
                        );
            next += 90;
            next += 360 / period;
            if (imaginary.Sign < 0)
                next += 180;
            if (next > 315)
                next -= 360;
            var sn = Math.Sin(next * (Math.PI / 180));
            var ld = Math.Sin((next + 45) * (Math.PI / 180));
            var trend = 1;
            if (kind == HilbertCycleSignal.Trend)
            {
                if (count > 0)
                    means[i] = R(
                        bars.Skip(i - count + 1).Take(count).Aggregate(z, (s, b) => s + F(b.Close))
                            / F(count)
                    );
                var baseline = R(
                    (F(4) * means[i] + F(3) * means[i - 1] + F(2) * means[i - 2] + means[i - 3])
                        / F(10)
                );
                if (sn > ld && sine <= lead || sn < ld && sine >= lead)
                {
                    days = 0;
                    trend = 0;
                }
                days++;
                if (days < .5 * period)
                    trend = 0;
                if (next - phase > .67 * 90 * 4 / period && next - phase < 1.5 * 90 * 4 / period)
                    trend = 0;
                if (
                    baseline.Sign != 0
                    && ((smooth[i] - baseline) / baseline).Abs().CompareTo(F(.015)) >= 0
                )
                    trend = 1;
            }
            phase = next;
            sine = sn;
            lead = ld;
            if (i < 63L + suppression)
                continue;
            result[0][i] =
                kind == HilbertCycleSignal.Phase ? phase
                : kind == HilbertCycleSignal.Sine ? sine
                : trend;
            if (kind == HilbertCycleSignal.Sine)
                result[1][i] = lead;
        }
        return result;
    }
}
