using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SeededPhaseComparison
{
    internal static readonly string[] Names = ["Mama", "Fama"];
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonPair Pair(bool quan, double fast = .5, double slow = .05) =>
        new(
            quan ? "QuanTAlib.Mama" : "Skender.GetMama",
            nameof(SeededPhaseAdaptiveAverage),
            (d, _) => Native(d, fast, slow, quan),
            (d, _) => Owned(d.IndicatorBars, fast, slow, quan),
            (d, _) => Series(GridReference(Input(d, quan, false), fast, slow, quan)),
            Names,
            MinimumInputCount: 0,
            CompetitorReference: (d, _) =>
                Series(NativeReference(Input(d, quan, true), fast, slow, quan)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static double[] Input(CompetitorData d, bool quan, bool native) =>
        quan ? d.Closes
        : native ? d.Quotes.Select(q => (double)(q.High + q.Low) / 2).ToArray()
        : d.Highs.Zip(d.Lows, (h, l) => Round(Units(h) + Units(l), 2 * Grid)).ToArray();

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Owned(Bar[] bars, double fast, double slow, bool quan)
    {
        var indicator = new SeededPhaseAdaptiveAverage(fast, slow, quan, quan, !quan);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var rows = new double?[2][];
        for (var j = 0; j < 2; j++)
        {
            var v = run[indicator.Outputs[j]].ToArray();
            var f = run[indicator.Outputs[j + 2]].ToArray();
            rows[j] = v.Select((x, i) => f[i] > 0 ? (double?)x : null).ToArray();
        }
        return Series(rows);
    }

    private static ComparisonSeries Native(CompetitorData d, double fast, double slow, bool quan)
    {
        if (!quan)
        {
            var r = d.Quotes.GetMama(fast, slow).ToArray();
            return Series([r.Select(v => v.Mama).ToArray(), r.Select(v => v.Fama).ToArray()]);
        }
        var indicator = new QuanTAlib.Mama(fast, slow);
        var rows = Enumerable.Range(0, 2).Select(_ => new double?[d.Count]).ToArray();
        for (var i = 0; i < d.Count; i++)
        {
            rows[0][i] = indicator.Calc(new QuanTAlib.TValue(d.Closes[i], true, false)).Value;
            rows[1][i] = indicator.Fama.Value;
        }
        return Series(rows);
    }

    // Independent complete-history grid model; production instead keeps bounded lag rings.
    internal static double?[][] GridReference(
        double[] prices,
        double fast,
        double slow,
        bool quan,
        bool delayed = false,
        int suppression = 0,
        bool cycleReadings = false,
        int filterStart = 12,
        int publication = 32
    ) =>
        GridExtendedReference(
                prices.Select(Units).ToArray(),
                fast,
                slow,
                quan,
                delayed,
                suppression,
                cycleReadings,
                filterStart,
                publication
            )
            .Select(row =>
                row.Select(v => v.HasValue ? (double?)Round(v.Value, Grid) : null).ToArray()
            )
            .ToArray();

    internal static BigInteger?[][] GridExtendedReference(
        BigInteger[] prices,
        double fast,
        double slow,
        bool quan,
        bool delayed = false,
        int suppression = 0,
        bool cycleReadings = false,
        int filterStart = 12,
        int publication = 32
    )
    {
        var n = prices.Length;
        var p = prices;
        var a = Enumerable.Range(0, 10).Select(_ => new BigInteger[n]).ToArray();
        var periods = new double[n];
        var phases = new double[n];
        var result = Enumerable
            .Range(0, cycleReadings ? 3 : 2)
            .Select(_ => new BigInteger?[n])
            .ToArray();
        double smoothedPeriod = 0;
        BigInteger Q(BigInteger value) => DirectionalComparison.RoundedUnits(value, Grid);
        BigInteger Fir(int column, int i, double correction) =>
            Q(
                DirectionalComparison.RoundedUnits(
                    Units(.0962) * a[column][i]
                        + Units(.5769) * a[column][i - 2]
                        - Units(.5769) * a[column][i - 4]
                        - Units(.0962) * a[column][i - 6],
                    Grid
                ) * Units(correction)
            );
        BigInteger Mix(BigInteger current, BigInteger previous) =>
            Q(current * Units(.2) + previous * Units(.8));
        BigInteger Adapt(BigInteger current, BigInteger previous, double alpha) =>
            Q(previous * Grid + (current - previous) * Units(alpha));
        BigInteger sum = 0;
        for (var i = 0; i < n; i++)
        {
            if (i < (delayed ? filterStart : 6))
            {
                if (!delayed)
                {
                    sum += p[i];
                    a[8][i] = a[9][i] = DirectionalComparison.RoundedUnits(sum, i + 1);
                }
            }
            else
            {
                var correction = .075 * periods[i - 1] + .54;
                a[0][i] = DirectionalComparison.RoundedUnits(
                    4 * p[i] + 3 * p[i - 1] + 2 * p[i - 2] + p[i - 3],
                    10
                );
                a[1][i] = Fir(0, i, correction);
                a[2][i] = Fir(1, i, correction);
                a[3][i] = a[1][i - 3];
                a[4][i] = Mix(
                    DirectionalComparison.RoundedUnits(a[3][i] - Fir(2, i, correction), 1),
                    a[4][i - 1]
                );
                a[5][i] = Mix(
                    DirectionalComparison.RoundedUnits(a[2][i] + Fir(3, i, correction), 1),
                    a[5][i - 1]
                );
                a[6][i] = Mix(Q(a[4][i] * a[4][i - 1] + a[5][i] * a[5][i - 1]), a[6][i - 1]);
                a[7][i] = Mix(Q(a[4][i] * a[5][i - 1] - a[5][i] * a[4][i - 1]), a[7][i - 1]);
                var measured =
                    !a[6][i].IsZero && !a[7][i].IsZero
                        ? 2 * Math.PI / Math.Atan(Round(a[7][i], a[6][i]))
                    : delayed ? periods[i - 1]
                    : quan ? periods[i - 2]
                    : 0;
                measured = Math.Min(measured, 1.5 * periods[i - 1]);
                measured = Math.Max(measured, .67 * periods[i - 1]);
                measured = Math.Clamp(measured, 6, 50);
                periods[i] = .2 * measured + .8 * periods[i - 1];
                smoothedPeriod = .33 * periods[i] + .67 * smoothedPeriod;
                phases[i] =
                    !a[3][i].IsZero ? Math.Atan(Round(a[2][i], a[3][i])) * 180 / Math.PI
                    : quan ? phases[i - 2]
                    : 0;
                var delta = Math.Max(phases[i - 1] - phases[i], 1);
                var alpha = delayed && delta <= 1 ? fast : Math.Max(fast / delta, slow);
                a[8][i] = Adapt(p[i], a[8][i - 1], alpha);
                a[9][i] = Adapt(a[8][i], a[9][i - 1], .5 * alpha);
            }
            if (
                i
                >= (
                    delayed ? (long)publication + suppression
                    : quan ? 0
                    : 5
                )
            )
            {
                result[0][i] = cycleReadings ? a[3][i] : a[8][i];
                result[1][i] = cycleReadings ? a[2][i] : a[9][i];
                if (cycleReadings)
                    result[2][i] = Units(smoothedPeriod);
            }
        }
        return result;
    }

    // Native operation order and zero fallbacks are explicit; IEEE nonfinite results remain visible.
    internal static double?[][] NativeReference(
        double[] p,
        double fast,
        double slow,
        bool quan,
        bool cycleReadings = false
    )
    {
        var n = p.Length;
        var a = Enumerable.Range(0, 10).Select(_ => new double[n]).ToArray();
        var periods = new double[n];
        var phases = new double[n];
        var r = Enumerable.Range(0, cycleReadings ? 3 : 2).Select(_ => new double?[n]).ToArray();
        double smoothedPeriod = 0;
        double sum = 0;
        double Fir(int column, int i, double correction) =>
            (
                .0962 * a[column][i]
                + .5769 * a[column][i - 2]
                - .5769 * a[column][i - 4]
                - .0962 * a[column][i - 6]
            ) * correction;
        for (var i = 0; i < n; i++)
        {
            if (i < 6)
            {
                sum += p[i];
                a[8][i] = a[9][i] = sum / (i + 1);
            }
            else
            {
                var correction = .075 * periods[i - 1] + .54;
                a[0][i] = (4 * p[i] + 3 * p[i - 1] + 2 * p[i - 2] + p[i - 3]) / 10;
                a[1][i] = Fir(0, i, correction);
                a[2][i] = Fir(1, i, correction);
                a[3][i] = a[1][i - 3];
                var inphase = a[3][i] - Fir(2, i, correction);
                var quadrature = a[2][i] + Fir(3, i, correction);
                a[4][i] = .2 * inphase + .8 * a[4][i - 1];
                a[5][i] = .2 * quadrature + .8 * a[5][i - 1];
                var real = a[4][i] * a[4][i - 1] + a[5][i] * a[5][i - 1];
                var imaginary = a[4][i] * a[5][i - 1] - a[5][i] * a[4][i - 1];
                a[6][i] = .2 * real + .8 * a[6][i - 1];
                a[7][i] = .2 * imaginary + .8 * a[7][i - 1];
                var measured =
                    a[7][i] != 0 && a[6][i] != 0 ? 2 * Math.PI / Math.Atan(a[7][i] / a[6][i])
                    : quan ? periods[i - 2]
                    : 0;
                measured = Math.Min(measured, 1.5 * periods[i - 1]);
                measured = Math.Max(measured, .67 * periods[i - 1]);
                measured = Math.Max(6, Math.Min(50, measured));
                periods[i] = .2 * measured + .8 * periods[i - 1];
                smoothedPeriod = .33 * periods[i] + .67 * smoothedPeriod;
                phases[i] =
                    a[3][i] != 0 ? Math.Atan(a[2][i] / a[3][i]) * 180 / Math.PI
                    : quan ? phases[i - 2]
                    : 0;
                var alpha = Math.Max(fast / Math.Max(phases[i - 1] - phases[i], 1), slow);
                a[8][i] = quan
                    ? alpha * (p[i] - a[8][i - 1]) + a[8][i - 1]
                    : alpha * p[i] + (1 - alpha) * a[8][i - 1];
                a[9][i] = quan
                    ? .5 * alpha * (a[8][i] - a[9][i - 1]) + a[9][i - 1]
                    : .5 * alpha * a[8][i] + (1 - .5 * alpha) * a[9][i - 1];
            }
            if (quan || i >= 5)
            {
                r[0][i] = !quan && double.IsNaN(a[8][i]) ? null : a[8][i];
                r[1][i] = !quan && double.IsNaN(a[9][i]) ? null : a[9][i];
                if (cycleReadings)
                    r[2][i] = smoothedPeriod;
            }
        }
        return r;
    }
}
