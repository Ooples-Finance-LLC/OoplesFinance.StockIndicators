using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class SeededPhaseReference
{
    private static ReferenceFraction F(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction R(ReferenceFraction v) => v.RoundExtendedBinary64();

    internal static double?[][] Values(IReadOnlyList<Bar> bars, SeededPhaseAdaptiveAverage owner) =>
        Values(
            bars,
            owner.FastLimit,
            owner.SlowLimit,
            owner.PublishStartup,
            owner.RetainOlderZeroPhase,
            owner.UseMidpoint,
            false,
            0
        );

    internal static double?[][] Delayed(
        IReadOnlyList<Bar> bars,
        double fast,
        double slow,
        int suppression
    ) => Values(bars, fast, slow, false, false, false, true, suppression);

    private static double?[][] Values(
        IReadOnlyList<Bar> bars,
        double fast,
        double slow,
        bool publishStartup,
        bool retainOlder,
        bool useMidpoint,
        bool delayed,
        int suppression
    ) =>
        ExtendedValues(
                bars,
                fast,
                slow,
                publishStartup,
                retainOlder,
                useMidpoint,
                delayed,
                suppression
            )
            .Select(row => row.Select(v => v?.ToDouble()).ToArray())
            .ToArray();

    internal static ReferenceFraction?[][] DelayedExtended(
        IReadOnlyList<ReferenceFraction> prices,
        double fast,
        double slow,
        int suppression
    ) =>
        ExtendedValues(
            Array.Empty<Bar>(),
            fast,
            slow,
            false,
            false,
            false,
            true,
            suppression,
            prices.ToArray()
        );

    internal static double?[][] Cycle(
        IReadOnlyList<Bar> bars,
        int suppression,
        int filterStart = 12,
        int publication = 32,
        bool delayed = true
    ) =>
        ExtendedValues(
                bars,
                .5,
                .05,
                false,
                false,
                false,
                delayed,
                suppression,
                cycleReadings: true,
                filterStart: filterStart,
                publication: publication
            )
            .Select(row => row.Select(v => v?.ToDouble()).ToArray())
            .ToArray();

    private static ReferenceFraction?[][] ExtendedValues(
        IReadOnlyList<Bar> bars,
        double fast,
        double slow,
        bool publishStartup,
        bool retainOlder,
        bool useMidpoint,
        bool delayed,
        int suppression,
        ReferenceFraction[]? wide = null,
        bool cycleReadings = false,
        int filterStart = 12,
        int publication = 32
    )
    {
        var n = wide?.Length ?? bars.Count;
        var z = new ReferenceFraction(0);
        var price =
            wide
            ?? bars.Select(b =>
                    useMidpoint ? R((F(b.High) + F(b.Low)) / new ReferenceFraction(2)) : F(b.Close)
                )
                .ToArray();
        var stages = Enumerable
            .Range(0, 10)
            .Select(_ => Enumerable.Repeat(z, n).ToArray())
            .ToArray();
        var sm = stages[0];
        var dt = stages[1];
        var q1 = stages[2];
        var i1 = stages[3];
        var i2 = stages[4];
        var q2 = stages[5];
        var re = stages[6];
        var im = stages[7];
        var mama = stages[8];
        var fama = stages[9];
        var periods = new double[n];
        var phases = new double[n];
        var result = Enumerable
            .Range(0, cycleReadings ? 3 : 2)
            .Select(_ => new ReferenceFraction?[n])
            .ToArray();
        double smoothedPeriod = 0;
        ReferenceFraction Fir(ReferenceFraction[] a, int i, double correction) =>
            R(
                R(F(.0962) * a[i] + F(.5769) * a[i - 2] - F(.5769) * a[i - 4] - F(.0962) * a[i - 6])
                    * F(correction)
            );
        ReferenceFraction Blend(ReferenceFraction value, ReferenceFraction previous) =>
            R(F(.2) * value + F(.8) * previous);
        ReferenceFraction Adaptive(
            ReferenceFraction value,
            ReferenceFraction previous,
            double alpha
        ) => R(previous + F(alpha) * (value - previous));
        var seed = z;
        for (var i = 0; i < n; i++)
        {
            if (i < (delayed ? filterStart : 6))
            {
                if (!delayed)
                {
                    seed += price[i];
                    mama[i] = fama[i] = R(seed / new ReferenceFraction(i + 1));
                }
            }
            else
            {
                var correction = .075 * periods[i - 1] + .54;
                sm[i] = R(
                    (
                        new ReferenceFraction(4) * price[i]
                        + new ReferenceFraction(3) * price[i - 1]
                        + new ReferenceFraction(2) * price[i - 2]
                        + price[i - 3]
                    ) / new ReferenceFraction(10)
                );
                dt[i] = Fir(sm, i, correction);
                q1[i] = Fir(dt, i, correction);
                i1[i] = dt[i - 3];
                var ji = Fir(i1, i, correction);
                var jq = Fir(q1, i, correction);
                i2[i] = Blend(R(i1[i] - jq), i2[i - 1]);
                q2[i] = Blend(R(q1[i] + ji), q2[i - 1]);
                re[i] = Blend(R(i2[i] * i2[i - 1] + q2[i] * q2[i - 1]), re[i - 1]);
                im[i] = Blend(R(i2[i] * q2[i - 1] - q2[i] * i2[i - 1]), im[i - 1]);
                var measured =
                    im[i].Sign != 0 && re[i].Sign != 0
                        ? 2 * Math.PI / Math.Atan((im[i] / re[i]).ToDouble())
                    : delayed ? periods[i - 1]
                    : retainOlder ? periods[i - 2]
                    : 0;
                measured = Math.Min(measured, 1.5 * periods[i - 1]);
                measured = Math.Max(measured, .67 * periods[i - 1]);
                measured = Math.Min(50, Math.Max(6, measured));
                periods[i] = .2 * measured + .8 * periods[i - 1];
                smoothedPeriod = .33 * periods[i] + .67 * smoothedPeriod;
                phases[i] =
                    i1[i].Sign != 0 ? Math.Atan((q1[i] / i1[i]).ToDouble()) * 180 / Math.PI
                    : retainOlder ? phases[i - 2]
                    : 0;
                var delta = Math.Max(phases[i - 1] - phases[i], 1);
                var alpha = delayed && delta <= 1 ? fast : Math.Max(fast / delta, slow);
                mama[i] = Adaptive(price[i], mama[i - 1], alpha);
                fama[i] = Adaptive(mama[i], fama[i - 1], .5 * alpha);
            }
            if (
                i
                >= (
                    delayed ? (long)publication + suppression
                    : publishStartup ? 0
                    : 5
                )
            )
            {
                result[0][i] = cycleReadings ? i1[i] : mama[i];
                result[1][i] = cycleReadings ? q1[i] : fama[i];
                if (cycleReadings)
                    result[2][i] = F(smoothedPeriod);
            }
        }
        return result;
    }
}
