using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ChandelierComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static string[] Names(bool trady) => trady ? ["Long", "Short"] : ["Value"];

    internal static ChandelierExitSelection Selection(bool trady, bool shortSide) =>
        trady ? ChandelierExitSelection.Both
        : shortSide ? ChandelierExitSelection.Short
        : ChandelierExitSelection.Long;

    internal static ComparisonPair Create(
        bool trady,
        bool shortSide = false,
        double multiplier = 3
    ) =>
        new(
            trady ? "Trady.Indicator.ChandelierExit" : "Skender.GetChandelier",
            nameof(WindowChandelierExit),
            (d, p) => Native(d, p, trady, shortSide, multiplier),
            (d, p) => Owned(d.IndicatorBars, p, trady, shortSide, multiplier),
            (d, p) => Pack(Reference(d.IndicatorBars, p, multiplier, !trady, 0), trady, shortSide),
            Names(trady),
            CompetitorReference: (d, p) =>
                trady
                    ? DecimalSeries(
                        TradyReference(
                            d.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray(),
                            p,
                            (decimal)multiplier
                        )
                    )
                    : Pack(
                        Reference(
                            d.Quotes.Select(q => new Bar(
                                    q.Date,
                                    (double)q.Open,
                                    (double)q.High,
                                    (double)q.Low,
                                    (double)q.Close,
                                    (double)q.Volume
                                ))
                                .ToArray(),
                            p,
                            multiplier,
                            true,
                            1
                        ),
                        false,
                        shortSide
                    ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Pack(double?[][] values, bool trady, bool shortSide) =>
        RetrospectivePriceComparison.Series(
            Names(trady),
            trady ? values : [values[shortSide ? 1 : 0]]
        );

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        bool trady,
        bool shortSide,
        double multiplier
    )
    {
        if (!trady)
            return VolumePriceComparison.Mask(
                data.Quotes.GetChandelier(
                        period,
                        multiplier,
                        shortSide ? ChandelierType.Short : ChandelierType.Long
                    )
                    .Select(r => r.ChandelierExit)
                    .ToArray()
            );
        var values = new T.ChandelierExit(data.Candles, period, (decimal)multiplier)
            .Compute()
            .Select(r => r.Tick)
            .ToArray();
        return DecimalSeries(values);
    }

    private static ComparisonSeries DecimalSeries((decimal? Long, decimal? Short)[] values) =>
        Pack(
            [
                values.Select(v => (double?)v.Long).ToArray(),
                values.Select(v => (double?)v.Short).ToArray(),
            ],
            true,
            false
        );

    internal static (decimal? Long, decimal? Short)[] TradyReference(
        (decimal High, decimal Low, decimal Close)[] bars,
        int period,
        decimal multiplier
    )
    {
        var result = new (decimal? Long, decimal? Short)[bars.Length];
        decimal seed = 0,
            average = 0;
        for (var i = 1; i < bars.Length; i++)
        {
            var range = Math.Max(
                bars[i].High - bars[i].Low,
                Math.Max(
                    Math.Abs(bars[i].High - bars[i - 1].Close),
                    Math.Abs(bars[i].Low - bars[i - 1].Close)
                )
            );
            if (i <= period)
                seed += range;
            if (i < period)
                continue;
            average = i == period ? seed / period : average + (1.0m / period) * (range - average);
            var window = bars.Skip(i - period + 1).Take(period).ToArray();
            result[i] = (
                window.Max(b => b.High) - average * multiplier,
                window.Min(b => b.Low) + average * multiplier
            );
        }
        return result;
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int period,
        bool trady,
        bool shortSide = false,
        double multiplier = 3
    )
    {
        var indicator = new WindowChandelierExit(
            period,
            multiplier,
            Selection(trady, shortSide),
            !trady
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = new double?[2][];
        for (var slot = 0; slot < 2; slot++)
        {
            var response = run[indicator.Outputs[slot]].ToArray();
            var mask = run[indicator.Outputs[slot + 2]].ToArray();
            values[slot] = response.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray();
        }
        return Pack(values, trady, shortSide);
    }

    private static BigInteger Extended(BigInteger numerator, BigInteger denominator)
    {
        var shift = 0;
        double rounded;
        while (double.IsInfinity(rounded = Round(numerator, denominator << shift)))
            shift += 512;
        return Units(rounded) << shift;
    }

    // mode 0: exact owned arithmetic; 1: Skender binary64 stages.
    internal static double?[][] Reference(
        Bar[] bars,
        int period,
        double multiplier,
        bool zeroFloor,
        int mode
    )
    {
        var result = Enumerable.Range(0, 2).Select(_ => new double?[bars.Length]).ToArray();
        BigInteger seed = 0,
            average = 0;
        double seedBinary = 0,
            averageBinary = 0;
        for (var i = 1; i < bars.Length; i++)
        {
            var h = bars[i].High;
            var l = bars[i].Low;
            var c = bars[i - 1].Close;
            if (mode == 0)
            {
                var range = Extended(
                    BigInteger.Max(
                        Units(h) - Units(l),
                        BigInteger.Max(
                            BigInteger.Abs(Units(h) - Units(c)),
                            BigInteger.Abs(Units(l) - Units(c))
                        )
                    ),
                    Grid
                );
                if (i <= period)
                    seed += range;
                if (i < period)
                    continue;
                average = Extended(
                    i == period ? seed : average * (period - 1) + range,
                    Grid * period
                );
            }
            else
            {
                var range = Math.Max(
                    Subtract(h, l),
                    Math.Max(Math.Abs(Subtract(h, c)), Math.Abs(Subtract(l, c)))
                );
                if (i <= period)
                    seedBinary = Add(seedBinary, range);
                if (i < period)
                    continue;
                averageBinary =
                    i == period
                        ? Divide(seedBinary, period)
                        : Divide(Add(Multiply(averageBinary, period - 1), range), period);
            }
            var window = bars.Skip(i - period + 1).Take(period).ToArray();
            var high = window.Max(b => b.High);
            if (zeroFloor)
                high = Math.Max(0, high);
            var low = window.Min(b => b.Low);
            if (mode == 0)
            {
                var product = average * Units(multiplier);
                result[0][i] = Round(Units(high) * Grid - product, Grid * Grid);
                result[1][i] = Round(Units(low) * Grid + product, Grid * Grid);
            }
            else
            {
                result[0][i] = Subtract(high, Multiply(averageBinary, multiplier));
                result[1][i] = Add(low, Multiply(averageBinary, multiplier));
            }
        }
        return result;
    }
}
