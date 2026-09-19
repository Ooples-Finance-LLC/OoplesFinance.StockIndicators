using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Trady.Core;
using QuanTAlib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// One deterministic OHLCV series, materialised once into the shape each library insists on.
///
/// <para>Every library here takes its input differently — Skender wants <c>IEnumerable&lt;Quote&gt;</c> of
/// decimals, TA-Lib wants bare <c>double[]</c>, Trady wants <c>IOhlcv</c> candles of decimals, QuanTAlib wants
/// its own <c>TBar</c>, and this library wants <see cref="StockData"/>. Building that shape is not part of what
/// an indicator costs, so it happens once in a <c>GlobalSetup</c> and never inside a measured method. The
/// alternative — converting inside the benchmark — measures the adapter and reports it as the indicator, which
/// is the most common way a competitor comparison ends up dishonest.</para>
///
/// <para>The series itself is a seeded random walk, so every library sees identical bars and a rerun on another
/// machine compares against the same numbers.</para>
/// </summary>
internal sealed class CompetitorData
{
    private CompetitorData(
        double[] opens,
        double[] highs,
        double[] lows,
        double[] closes,
        double[] volumes,
        DateTime[] dates)
    {
        Opens = opens;
        Highs = highs;
        Lows = lows;
        Closes = closes;
        Volumes = volumes;
        Dates = dates;

        StockData = new StockData(
            [.. opens], [.. highs], [.. lows], [.. closes], [.. volumes], [.. dates]);

        Quotes = new List<Quote>(closes.Length);
        Candles = new List<Candle>(closes.Length);
        Bars = new TBar[closes.Length];

        for (var i = 0; i < closes.Length; i++)
        {
            Quotes.Add(new Quote
            {
                Date = dates[i],
                Open = (decimal)opens[i],
                High = (decimal)highs[i],
                Low = (decimal)lows[i],
                Close = (decimal)closes[i],
                Volume = (decimal)volumes[i]
            });

            Candles.Add(new Candle(
                dates[i],
                (decimal)opens[i],
                (decimal)highs[i],
                (decimal)lows[i],
                (decimal)closes[i],
                (decimal)volumes[i]));

            Bars[i] = new TBar(dates[i], opens[i], highs[i], lows[i], closes[i], volumes[i], true);
        }
    }

    public double[] Opens { get; }

    public double[] Highs { get; }

    public double[] Lows { get; }

    public double[] Closes { get; }

    public double[] Volumes { get; }

    public DateTime[] Dates { get; }

    public int Count => Closes.Length;

    /// <summary>This library's batch input.</summary>
    public StockData StockData { get; }

    /// <summary>Skender.Stock.Indicators' input.</summary>
    public List<Quote> Quotes { get; }

    /// <summary>Trady's input.</summary>
    public List<Candle> Candles { get; }

    /// <summary>QuanTAlib's input.</summary>
    public TBar[] Bars { get; }

    /// <summary>
    /// A fresh <see cref="StockData"/> over the same bars. The batch API writes its results back into the
    /// instance it is given, so a benchmark that reuses one is measuring a partly-populated object after the
    /// first iteration.
    /// </summary>
    public StockData NewStockData() => new(
        [.. Opens], [.. Highs], [.. Lows], [.. Closes], [.. Volumes], [.. Dates]);

    /// <summary>The same series truncated to <paramref name="bars"/> bars, for warm-then-append measurements.</summary>
    public CompetitorData Take(int bars) => new(
        Opens[..bars], Highs[..bars], Lows[..bars], Closes[..bars], Volumes[..bars], Dates[..bars]);

    public static CompetitorData Create(int count, int seed = 42)
    {
        // Seeded deliberately: a benchmark that cannot be reproduced bar for bar cannot be argued with, and
        // every competitor must see the identical series for the comparison to mean anything.
        var random = new Random(seed);
        var opens = new double[count];
        var highs = new double[count];
        var lows = new double[count];
        var closes = new double[count];
        var volumes = new double[count];
        var dates = new DateTime[count];

        var lastClose = 100d;
        var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < count; i++)
        {
            var open = lastClose + ((random.NextDouble() - 0.5) * 0.5);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 2));

            opens[i] = open;
            highs[i] = Math.Max(open, close) + random.NextDouble();
            lows[i] = Math.Max(0.01, Math.Min(open, close) - random.NextDouble());
            closes[i] = close;
            volumes[i] = random.Next(1000, 500000);
            dates[i] = start.AddMinutes(i);

            lastClose = close;
        }

        return new CompetitorData(opens, highs, lows, closes, volumes, dates);
    }
}
