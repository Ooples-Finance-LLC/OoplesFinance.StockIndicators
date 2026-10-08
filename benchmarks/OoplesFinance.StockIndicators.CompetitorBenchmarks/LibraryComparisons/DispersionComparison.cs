using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DispersionComparison
{
    internal static readonly string[] Ids =
    [
        "QuanTAlib.Variance",
        "QuanTAlib.Stddev",
        "QuanTAlib.Zscore",
        "TaLib.Functions.Var",
        "TaLib.Functions.StdDev",
        "Trady.Indicator.StandardDeviation",
    ];
    internal static readonly ComparisonPair[] Pairs = Ids.Select(id => Create(id)).ToArray();

    internal static ComparisonPair Create(string id, bool? sample = null, double multiplier = 1)
    {
        var useSample = sample ?? id.StartsWith("QuanTAlib.", StringComparison.Ordinal);
        var kind =
            id.EndsWith("Variance", StringComparison.Ordinal)
            || id.EndsWith(".Var", StringComparison.Ordinal)
                ? WindowDispersionOutput.Variance
            : id.EndsWith("Zscore", StringComparison.Ordinal) ? WindowDispersionOutput.ZScore
            : WindowDispersionOutput.StandardDeviation;
        return new(
            id,
            "WindowDispersion",
            (d, p) => Native(id, d, p, useSample, multiplier),
            (d, p) => Ooples(id, d, p, kind, useSample, multiplier),
            (d, p) => Reference(id, d, p, kind, useSample, multiplier),
            CompetitorReference: id.StartsWith("TaLib.", StringComparison.Ordinal)
                ? (d, p) => NativeMomentReference(d, p, kind, multiplier)
                : null
        );
    }

    private static int First(string id, int count, int period) =>
        id.StartsWith("QuanTAlib.", StringComparison.Ordinal) ? 0 : Math.Min(period - 1, count);

    private static ComparisonSeries Ooples(
        string id,
        CompetitorData data,
        int period,
        WindowDispersionOutput kind,
        bool sample,
        double multiplier
    )
    {
        var indicator = new WindowDispersion(period, kind, sample, multiplier);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(First(id, data.Count, period), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Native(
        string id,
        CompetitorData data,
        int period,
        bool sample,
        double multiplier
    )
    {
        if (id.StartsWith("QuanTAlib.", StringComparison.Ordinal))
        {
            QuanTAlib.AbstractBase indicator = id switch
            {
                "QuanTAlib.Variance" => new QuanTAlib.Variance(period, !sample),
                "QuanTAlib.Stddev" => new QuanTAlib.Stddev(period, !sample),
                _ => new QuanTAlib.Zscore(period),
            };
            return new(
                0,
                data.Closes.Select(
                        (v, i) => indicator.Calc(new QuanTAlib.TValue(data.Dates[i], v, true)).Value
                    )
                    .ToArray()
            );
        }
        if (id.StartsWith("Trady.", StringComparison.Ordinal))
            return new(
                First(id, data.Count, period),
                new Trady.Analysis.Indicator.StandardDeviation(data.Candles, period)
                    .Compute()
                    .Select(r => (double?)r.Tick ?? double.NaN)
                    .ToArray()
            );
        var packed = new double[data.Count];
        var code = TaLib(id, data, packed, period, multiplier, out var range);
        var first = First(id, data.Count, period);
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam && first == 1)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib dispersion returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected dispersion alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    internal static TALib.Core.RetCode TaLib(
        string id,
        CompetitorData data,
        double[] values,
        int period,
        double multiplier,
        out System.Range range
    ) =>
        id.EndsWith(".Var", StringComparison.Ordinal)
            ? Functions.Var<double>(data.Closes, System.Range.All, values, out range, period)
            : Functions.StdDev<double>(
                data.Closes,
                System.Range.All,
                values,
                out range,
                period,
                multiplier
            );

    private static ComparisonSeries Reference(
        string id,
        CompetitorData data,
        int period,
        WindowDispersionOutput kind,
        bool sample,
        double multiplier
    )
    {
        var values = new double[data.Count];
        var first = First(id, data.Count, period);
        for (var i = first; i < data.Count; i++)
        {
            var count = Math.Min(period, i + 1);
            if (count < 2)
                continue;
            var window = data
                .Closes.Skip(i - count + 1)
                .Take(count)
                .Select(v => (decimal)v)
                .ToArray();
            var mean = window.Sum() / count;
            var variance = window.Sum(v => (v - mean) * (v - mean)) / (sample ? count - 1 : count);
            values[i] = kind switch
            {
                WindowDispersionOutput.Variance => (double)variance * multiplier,
                WindowDispersionOutput.StandardDeviation => Math.Sqrt((double)variance)
                    * multiplier,
                _ => variance == 0
                    ? 0
                    : (double)(window[^1] - mean) / Math.Sqrt((double)variance) * multiplier,
            };
        }
        return new(first, values);
    }

    // Separately model native uncentered rolling sums and every binary64 rounding stage.
    // This intentionally preserves cancellation defects instead of tolerating them away.
    private static ComparisonSeries NativeMomentReference(
        CompetitorData data,
        int period,
        WindowDispersionOutput kind,
        double multiplier
    )
    {
        var result = new double[data.Count];
        double sum = 0,
            squares = 0;
        for (var i = 0; i < data.Count; i++)
        {
            var v = data.Closes[i];
            sum = Add(sum, v);
            squares = Add(squares, Multiply(v, v));
            if (i < period - 1)
                continue;
            var mean = Divide(sum, period);
            var variance = Subtract(Divide(squares, period), Multiply(mean, mean));
            result[i] =
                kind == WindowDispersionOutput.Variance ? variance
                : variance > 0 ? Multiply(Math.Sqrt(variance), multiplier)
                : 0;
            var expired = data.Closes[i - period + 1];
            sum = Subtract(sum, expired);
            squares = Subtract(squares, Multiply(expired, expired));
        }
        return new(Math.Min(period - 1, data.Count), result);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 3, 6, -2, 7, 7, 7, 0, -3, 1, 2, 3, 3, 3]);

    internal static CompetitorData CancellationFixture() =>
        CompetitorData.FromCloses([1e12, 1e12 + 1, 1e12 + 2, 1e12 + 3, 1e12 + 4]);
}
