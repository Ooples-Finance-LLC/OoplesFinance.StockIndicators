using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AtrEnvelopeComparison
{
    internal static readonly string[] Ids =
    [
        "Skender.GetKeltner",
        "Skender.GetStarcBands",
        "Trady.Indicator.KeltnerChannels",
    ];
    internal static readonly ComparisonPair[] Pairs = Enumerable
        .Range(0, 3)
        .Select(v => Create(v))
        .ToArray();

    internal static string[] Names(int variant) =>
        variant == 0 ? ["Center", "Upper", "Lower", "Width"] : ["Center", "Upper", "Lower"];

    internal static AtrBandCenterMode Mode(int variant) =>
        variant == 1 ? AtrBandCenterMode.Simple
        : variant == 2 ? AtrBandCenterMode.FirstSeededExponential
        : AtrBandCenterMode.MeanSeededExponential;

    internal static ComparisonPair Create(
        int variant,
        int? atrPeriod = null,
        double multiplier = 2
    ) =>
        new(
            Ids[variant],
            nameof(WindowAtrBands),
            (d, p) => Native(d, p, atrPeriod ?? p, multiplier, variant),
            (d, p) => Owned(d.IndicatorBars, p, atrPeriod ?? p, multiplier, variant),
            (d, p) =>
                Series(Reference(d.IndicatorBars, p, atrPeriod ?? p, multiplier, variant), variant),
            Names(variant),
            CompetitorReference: (d, p) =>
                variant == 2
                    ? DecimalSeries(
                        TradyReference(
                            d.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray(),
                            p,
                            atrPeriod ?? p,
                            (decimal)multiplier
                        )
                    )
                    : Series(
                        NativeReference(
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
                            atrPeriod ?? p,
                            multiplier,
                            variant
                        ),
                        variant
                    ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] values, int variant) =>
        RetrospectivePriceComparison.Series(
            Names(variant),
            values.Take(Names(variant).Length).ToArray()
        );

    private static ComparisonSeries DecimalSeries(
        (decimal? LowerChannel, decimal? Middle, decimal? UpperChannel)[] values
    ) =>
        Series(
            [
                values.Select(v => (double?)v.Middle).ToArray(),
                values.Select(v => (double?)v.UpperChannel).ToArray(),
                values.Select(v => (double?)v.LowerChannel).ToArray(),
            ],
            2
        );

    private static ComparisonSeries Native(
        CompetitorData data,
        int centerPeriod,
        int atrPeriod,
        double multiplier,
        int variant
    )
    {
        if (variant == 2)
            return DecimalSeries(
                new T.KeltnerChannels(data.Candles, centerPeriod, (decimal)multiplier, atrPeriod)
                    .Compute()
                    .Select(r => r.Tick)
                    .ToArray()
            );
        if (variant == 1)
        {
            var r = data.Quotes.GetStarcBands(centerPeriod, multiplier, atrPeriod).ToArray();
            return Series(
                [
                    r.Select(v => v.Centerline).ToArray(),
                    r.Select(v => v.UpperBand).ToArray(),
                    r.Select(v => v.LowerBand).ToArray(),
                ],
                1
            );
        }
        var k = data.Quotes.GetKeltner(centerPeriod, multiplier, atrPeriod).ToArray();
        return Series(
            [
                k.Select(v => v.Centerline).ToArray(),
                k.Select(v => v.UpperBand).ToArray(),
                k.Select(v => v.LowerBand).ToArray(),
                k.Select(v => v.Width).ToArray(),
            ],
            0
        );
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int centerPeriod,
        int atrPeriod,
        double multiplier,
        int variant
    )
    {
        var indicator = new WindowAtrBands(
            centerPeriod,
            atrPeriod,
            multiplier,
            Mode(variant),
            variant == 0
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = new double?[Names(variant).Length][];
        for (var slot = 0; slot < values.Length; slot++)
        {
            var response = run[indicator.Outputs[slot]].ToArray();
            var mask = run[indicator.Outputs[slot + 4]].ToArray();
            values[slot] = response.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray();
        }
        return Series(values, variant);
    }

    private static BigInteger Extended(BigInteger numerator, BigInteger denominator)
    {
        var shift = 0;
        double value;
        while (double.IsInfinity(value = Round(numerator, denominator << shift)))
            shift += 512;
        return Units(value) << shift;
    }

    internal static double?[][] Reference(
        Bar[] bars,
        int cp,
        int ap,
        double multiplier,
        int variant
    )
    {
        var result = Enumerable.Range(0, 4).Select(_ => new double?[bars.Length]).ToArray();
        BigInteger seed = 0,
            atr = 0;
        double center = 0;
        for (var i = 0; i < bars.Length; i++)
        {
            if (i > 0)
            {
                var h = Units(bars[i].High);
                var l = Units(bars[i].Low);
                var c = Units(bars[i - 1].Close);
                var range = Extended(
                    BigInteger.Max(
                        h - l,
                        BigInteger.Max(BigInteger.Abs(h - c), BigInteger.Abs(l - c))
                    ),
                    Grid
                );
                if (i <= ap)
                    seed += range;
                if (i >= ap)
                    atr = Extended(i == ap ? seed : atr * (ap - 1) + range, Grid * ap);
            }
            if (variant == 2)
                center =
                    i == 0
                        ? bars[0].Close
                        : Round(
                            2 * Units(bars[i].Close) + (cp - 1) * Units(center),
                            Grid * ((long)cp + 1)
                        );
            else if (i >= cp - 1)
                center =
                    variant == 1 || i == cp - 1
                        ? Round(
                            bars.Skip(i - cp + 1)
                                .Take(cp)
                                .Aggregate(BigInteger.Zero, (s, b) => s + Units(b.Close)),
                            Grid * cp
                        )
                        : Round(
                            2 * Units(bars[i].Close) + (cp - 1) * Units(center),
                            Grid * ((long)cp + 1)
                        );
            if (i < (variant == 1 ? cp : Math.Max(cp, ap)) - 1)
                continue;
            result[0][i] = center;
            if (i < ap)
                continue;
            var offset = atr * Units(multiplier);
            result[1][i] = Round(Units(center) * Grid + offset, Grid * Grid);
            result[2][i] = Round(Units(center) * Grid - offset, Grid * Grid);
            if (
                variant == 0
                && center != 0 // NOSONAR: Exact zero center has undefined width.
                && double.IsFinite(result[1][i]!.Value)
                && double.IsFinite(result[2][i]!.Value)
            )
                result[3][i] = Round(
                    Units(result[1][i]!.Value) - Units(result[2][i]!.Value),
                    Units(center)
                );
        }
        return result;
    }

    internal static double?[][] NativeReference(
        Bar[] bars,
        int cp,
        int ap,
        double multiplier,
        int variant
    )
    {
        var result = Enumerable.Range(0, 4).Select(_ => new double?[bars.Length]).ToArray();
        double atrSeed = 0,
            atr = 0,
            center = 0;
        for (var i = 0; i < bars.Length; i++)
        {
            if (i > 0)
            {
                var range = Math.Max(
                    Subtract(bars[i].High, bars[i].Low),
                    Math.Max(
                        Math.Abs(Subtract(bars[i].High, bars[i - 1].Close)),
                        Math.Abs(Subtract(bars[i].Low, bars[i - 1].Close))
                    )
                );
                if (i <= ap)
                    atrSeed = Add(atrSeed, range);
                if (i >= ap)
                    atr =
                        i == ap
                            ? Divide(atrSeed, ap)
                            : Divide(Add(Multiply(atr, ap - 1), range), ap);
            }
            if (i >= cp - 1)
                center =
                    variant == 1 || i == cp - 1
                        ? Divide(
                            bars.Skip(i - cp + 1).Take(cp).Select(b => b.Close).Aggregate(0d, Add),
                            cp
                        )
                        : Add(
                            center,
                            Multiply(Divide(2, (long)cp + 1), Subtract(bars[i].Close, center))
                        );
            if (i < (variant == 1 ? cp : Math.Max(cp, ap)) - 1)
                continue;
            result[0][i] = center;
            if (i < ap)
                continue;
            var offset = Multiply(atr, multiplier);
            result[1][i] = Add(center, offset);
            result[2][i] = Subtract(center, offset);
            if (variant == 0 && center != 0) // NOSONAR: Native width tests exact zero.
                result[3][i] = Divide(Subtract(result[1][i]!.Value, result[2][i]!.Value), center);
        }
        return result;
    }

    internal static (
        decimal? LowerChannel,
        decimal? Middle,
        decimal? UpperChannel
    )[] TradyReference(
        (decimal High, decimal Low, decimal Close)[] bars,
        int cp,
        int ap,
        decimal multiplier
    )
    {
        var result = new (decimal? LowerChannel, decimal? Middle, decimal? UpperChannel)[
            bars.Length
        ];
        decimal seed = 0,
            atr = 0,
            center = 0;
        for (var i = 0; i < bars.Length; i++)
        {
            center =
                i == 0
                    ? bars[0].Close
                    : center + (2.0m / ((long)cp + 1)) * (bars[i].Close - center);
            if (i > 0)
            {
                var range = Math.Max(
                    bars[i].High - bars[i].Low,
                    Math.Max(
                        Math.Abs(bars[i].High - bars[i - 1].Close),
                        Math.Abs(bars[i].Low - bars[i - 1].Close)
                    )
                );
                if (i <= ap)
                    seed += range;
                if (i >= ap)
                    atr = i == ap ? seed / ap : atr + (1.0m / ap) * (range - atr);
            }
            if (i < Math.Max(cp, ap) - 1)
                continue;
            result[i] =
                i < ap
                    ? (null, center, null)
                    : (center - multiplier * atr, center, center + multiplier * atr);
        }
        return result;
    }
}
