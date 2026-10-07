using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class EmaDifferenceSignalComparison
{
    internal static readonly string[] Ids =
    [
        "Skender.GetMacd",
        "Skender.GetPvo",
        "Trady.Indicator.MovingAverageConvergenceDivergence",
        "Trady.Indicator.MovingAverageConvergenceDivergenceHistogram",
    ];
    internal static readonly ComparisonPair[] Pairs = Enumerable
        .Range(0, 4)
        .Select(v => Pair(v))
        .ToArray();

    internal static string[] Names(int variant) =>
        variant == 0 ? ["Macd", "Signal", "Histogram", "FastEma", "SlowEma"]
        : variant == 3 ? ["Histogram"]
        : [variant == 1 ? "Pvo" : "Macd", "Signal", "Histogram"];

    internal static EmaDifferenceSelection Selection(int variant) =>
        variant == 0 ? EmaDifferenceSelection.All
        : variant == 3 ? EmaDifferenceSelection.Histogram
        : EmaDifferenceSelection.Oscillator
            | EmaDifferenceSelection.Signal
            | EmaDifferenceSelection.Histogram;

    internal static ComparisonPair Pair(
        int variant,
        int fast = 12,
        int slow = 26,
        int signal = 9
    ) =>
        new(
            Ids[variant],
            nameof(EmaDifferenceSignal),
            (d, p) => Native(d, variant, fast, slow, signal),
            (d, p) => Owned(d.IndicatorBars, variant, fast, slow, signal),
            (d, p) =>
                Series(
                    GridReference(
                        variant == 1 ? d.Volumes : d.Closes,
                        fast,
                        slow,
                        signal,
                        variant >= 2,
                        variant == 1
                    ),
                    variant
                ),
            Names(variant),
            CompetitorReference: (d, p) =>
                variant >= 2
                    ? Series(
                        DecimalReference(
                                d.Candles.Select(c => c.Close).ToArray(),
                                fast,
                                slow,
                                signal
                            )
                            .Select(row => row.Select(v => (double?)v).ToArray())
                            .ToArray(),
                        variant
                    )
                    : Series(
                        NativeReference(
                            d.Quotes.Select(q => (double)(variant == 1 ? q.Volume : q.Close))
                                .ToArray(),
                            fast,
                            slow,
                            signal,
                            variant == 1
                        ),
                        variant
                    ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static CompetitorData Fixture(int variant, string shape, int count)
    {
        var d = ComparisonVerifier.Fixture(shape, count);
        return variant == 1
            ? CompetitorData.FromOhlcv(d.Opens, d.Highs, d.Lows, d.Closes, d.Closes)
            : d;
    }

    internal static ComparisonSeries Series(double?[][] rows, int variant) =>
        RetrospectivePriceComparison.Series(
            Names(variant),
            variant == 0 ? rows
                : variant == 3 ? [rows[2]]
                : rows.Take(3).ToArray()
        );

    private static ComparisonSeries Native(CompetitorData data, int variant, int f, int s, int g)
    {
        if (variant == 0)
        {
            var r = data.Quotes.GetMacd(f, s, g).ToArray();
            return Series(
                [
                    r.Select(v => v.Macd).ToArray(),
                    r.Select(v => v.Signal).ToArray(),
                    r.Select(v => v.Histogram).ToArray(),
                    r.Select(v => v.FastEma).ToArray(),
                    r.Select(v => v.SlowEma).ToArray(),
                ],
                variant
            );
        }
        if (variant == 1)
        {
            var r = data.Quotes.GetPvo(f, s, g).ToArray();
            return Series(
                [
                    r.Select(v => v.Pvo).ToArray(),
                    r.Select(v => v.Signal).ToArray(),
                    r.Select(v => v.Histogram).ToArray(),
                ],
                variant
            );
        }
        if (variant == 3)
            return RetrospectivePriceComparison.Series(
                Names(variant),
                [
                    new T.MovingAverageConvergenceDivergenceHistogram(data.Candles, f, s, g)
                        .Compute()
                        .Select(v => (double?)v.Tick)
                        .ToArray(),
                ]
            );
        var values = new T.MovingAverageConvergenceDivergence(data.Candles, f, s, g)
            .Compute()
            .ToArray();
        return Series(
            [
                values.Select(v => (double?)v.Tick.MacdLine).ToArray(),
                values.Select(v => (double?)v.Tick.SignalLine).ToArray(),
                values.Select(v => (double?)v.Tick.MacdHistogram).ToArray(),
            ],
            variant
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int variant, int fast, int slow, int signal)
    {
        var indicator = new EmaDifferenceSignal(
            fast,
            slow,
            signal,
            variant >= 2,
            variant == 1,
            variant == 1,
            Selection(variant)
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var rows = new double?[5][];
        for (var j = 0; j < 5; j++)
        {
            var v = run[indicator.Outputs[j]].ToArray();
            var present = run[indicator.Outputs[j + 5]].ToArray();
            rows[j] = v.Select((x, i) => present[i] > 0 ? (double?)x : null).ToArray();
        }
        return Series(rows, variant);
    }

    private static BigInteger?[] GridAverage(
        BigInteger?[] source,
        long start,
        int period,
        bool first
    )
    {
        var r = new BigInteger?[source.Length];
        var begin = start + (first ? 0 : period - 1L);
        if (begin >= source.Length)
            return r;
        r[(int)begin] = DirectionalComparison.RoundedUnits(
            source
                .Skip((int)start)
                .Take(first ? 1 : period)
                .Aggregate(BigInteger.Zero, (s, v) => s + v!.Value),
            first ? 1 : period
        );
        for (var i = (int)begin + 1; i < source.Length; i++)
            r[i] = DirectionalComparison.RoundedUnits(
                r[i - 1]!.Value * (period - 1) + 2 * source[i]!.Value,
                (long)period + 1
            );
        return r;
    }

    internal static double?[][] GridReference(
        double[] prices,
        int fast,
        int slow,
        int signal,
        bool first,
        bool percentage,
        EmaDifferenceSelection selection = EmaDifferenceSelection.All
    )
    {
        var source = prices.Select(v => (BigInteger?)Units(v)).ToArray();
        var f = GridAverage(source, 0, fast, first);
        var s = GridAverage(source, 0, slow, first);
        var oscillator = new BigInteger?[prices.Length];
        var signalInput = new BigInteger?[prices.Length];
        for (var i = 0; i < prices.Length; i++)
            if (f[i].HasValue && s[i].HasValue)
            {
                if (!percentage)
                    oscillator[i] = DirectionalComparison.RoundedUnits(
                        f[i]!.Value - s[i]!.Value,
                        1
                    );
                else if (!s[i]!.Value.IsZero)
                    oscillator[i] = DirectionalComparison.RoundedUnits(
                        100 * (f[i]!.Value - s[i]!.Value) * Grid * s[i]!.Value.Sign,
                        BigInteger.Abs(s[i]!.Value)
                    );
                signalInput[i] = oscillator[i] ?? BigInteger.Zero;
            }
        var signaled = GridAverage(
            signalInput,
            first ? 0 : Math.Max(fast, slow) - 1L,
            signal,
            first
        );
        var histogram = new BigInteger?[prices.Length];
        for (var i = 0; i < prices.Length; i++)
            if (oscillator[i].HasValue && signaled[i].HasValue)
                histogram[i] = DirectionalComparison.RoundedUnits(
                    oscillator[i]!.Value - signaled[i]!.Value,
                    1
                );
        return new[] { oscillator, signaled, histogram, f, s }
            .Select(
                (row, j) =>
                    row.Select(v =>
                            v.HasValue && ((int)selection & (1 << j)) != 0
                                ? (double?)Round(v.Value, Grid)
                                : null
                        )
                        .ToArray()
            )
            .ToArray();
    }

    private static double?[] NativeAverage(double[] input, int p)
    {
        var output = new double?[input.Length];
        var alpha = 2d / checked(p + 1);
        var previous = 0d;
        foreach (var x in input.Take(p))
            previous += x;
        previous /= p;
        for (var i = p - 1; i < input.Length; i++)
        {
            if (i >= p)
                previous = (input[i] - previous) * alpha + previous;
            output[i] = double.IsNaN(previous) ? null : previous;
        }
        return output;
    }

    internal static double?[][] NativeReference(
        double[] prices,
        int fast,
        int slow,
        int signal,
        bool percentage
    )
    {
        var f = NativeAverage(prices, fast);
        var s = NativeAverage(prices, slow);
        var oscillator = new double?[prices.Length];
        for (var i = slow - 1; i < prices.Length; i++)
        {
            if (percentage)
                oscillator[i] = s[i] != 0 ? 100 * ((f[i] - s[i]) / s[i]) : null; // NOSONAR: Native exact zero slow denominator.
            else
            {
                var v = f[i] - s[i];
                oscillator[i] = v.HasValue && double.IsNaN(v.Value) ? null : v;
            }
        }
        var inputs = oscillator
            .Skip(slow - 1)
            .Select(v => v ?? (percentage ? 0 : double.NaN))
            .ToArray();
        var values = NativeAverage(inputs, signal);
        var signaled = new double?[prices.Length];
        var histogram = new double?[prices.Length];
        for (var i = slow - 1; i < prices.Length; i++)
        {
            signaled[i] = values[i - slow + 1];
            var h = oscillator[i] - signaled[i];
            histogram[i] = !percentage && h.HasValue && double.IsNaN(h.Value) ? null : h;
        }
        return [oscillator, signaled, histogram, f, s];
    }

    internal static decimal?[][] DecimalReference(decimal[] prices, int fast, int slow, int signal)
    {
        decimal?[] Average(decimal?[] input, int p)
        {
            var r = new decimal?[input.Length];
            if (input.Length == 0)
                return r;
            r[0] = input[0];
            for (var i = 1; i < r.Length; i++)
                r[i] = r[i - 1] + (2.0m / unchecked(p + 1)) * (input[i] - r[i - 1]);
            return r;
        }
        var f = Average(prices.Select(v => (decimal?)v).ToArray(), fast);
        var s = Average(prices.Select(v => (decimal?)v).ToArray(), slow);
        var oscillator = f.Select((v, i) => v - s[i]).ToArray();
        var signaled = Average(oscillator, signal);
        return [oscillator, signaled, oscillator.Select((v, i) => v - signaled[i]).ToArray()];
    }
}
