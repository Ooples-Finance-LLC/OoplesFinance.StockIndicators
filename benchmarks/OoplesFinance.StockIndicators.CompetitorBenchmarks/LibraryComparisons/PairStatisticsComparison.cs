using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PairStatisticsComparison
{
    internal static readonly string[] FullNames =
    [
        "Correlation",
        "RSquared",
        "Covariance",
        "VarianceA",
        "VarianceB",
    ];
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static string[] Names(bool full) => full ? FullNames : ["Correlation"];

    internal static ComparisonPair Create(bool full) =>
        new(
            full ? "Skender.GetCorrelation" : "TaLib.Functions.Correl",
            full ? nameof(WindowPairStatistics) : nameof(WindowCorrelation),
            (d, p) => Native(d, p, full),
            (d, p) => Owned(d.IndicatorBars, p, full),
            (d, p) =>
                RetrospectivePriceComparison.Series(
                    Names(full),
                    Reference(d.Closes, d.Opens, p, full)
                ),
            Names(full),
            MinimumInputCount: full ? 1 : 2,
            CompetitorReference: (d, p) =>
                RetrospectivePriceComparison.Series(
                    Names(full),
                    NativeReference(d.Closes, d.Opens, p, full)
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries FromSkender(IEnumerable<CorrResult> source)
    {
        var r = source.ToArray();
        return RetrospectivePriceComparison.Series(
            FullNames,
            [
                r.Select(v => v.Correlation).ToArray(),
                r.Select(v => v.RSquared).ToArray(),
                r.Select(v => v.Covariance).ToArray(),
                r.Select(v => v.VarianceA).ToArray(),
                r.Select(v => v.VarianceB).ToArray(),
            ]
        );
    }

    private static ComparisonSeries Native(CompetitorData data, int period, bool full)
    {
        if (full)
            return FromSkender(
                data.Dates.Zip(data.Closes, (d, v) => (d, v))
                    .GetCorrelation(data.Dates.Zip(data.Opens, (d, v) => (d, v)), period)
            );
        var packed = new double[data.Count];
        var values = new double?[data.Count];
        if (data.Count == 0)
            return RetrospectivePriceComparison.Series(Names(false), [values]);
        var code = Functions.Correl<double>(
            data.Closes,
            data.Opens,
            System.Range.All,
            packed,
            out var range,
            period
        );
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib correlation returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != Math.Max(0, data.Count - period + 1) || count > 0 && start != period - 1)
            throw new InvalidOperationException("Unexpected correlation alignment");
        for (var i = 0; i < count; i++)
            values[start + i] = packed[i];
        return RetrospectivePriceComparison.Series(Names(false), [values]);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, bool full)
    {
        IIndicator indicator = full
            ? new WindowPairStatistics(period)
            : new WindowCorrelation(period, flatZero: true);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var names = Names(full);
        var result = new double?[names.Length][];
        for (var slot = 0; slot < names.Length; slot++)
        {
            var values = run[indicator.Outputs[slot]].ToArray();
            var mask = run[indicator.Outputs[slot + names.Length]].ToArray();
            result[slot] = values.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray();
        }
        return RetrospectivePriceComparison.Series(names, result);
    }

    internal static double?[][] Reference(double[] x, double[] y, int period, bool full)
    {
        var output = Enumerable.Range(0, full ? 5 : 1).Select(_ => new double?[x.Length]).ToArray();
        for (var end = period - 1; end < x.Length; end++)
        {
            var xs = x.Skip(end - period + 1).Take(period).Select(Units).ToArray();
            var ys = y.Skip(end - period + 1).Take(period).Select(Units).ToArray();
            var sx = xs.Aggregate(BigInteger.Zero, (s, v) => s + v);
            var sy = ys.Aggregate(BigInteger.Zero, (s, v) => s + v);
            var a = BigInteger.Zero;
            var b = a;
            var c = a;
            for (var j = 0; j < period; j++)
            {
                var dx = period * xs[j] - sx;
                var dy = period * ys[j] - sy;
                a += dx * dx;
                b += dy * dy;
                c += dx * dy;
            }
            if (full)
            {
                var denom = BigInteger.Pow(new BigInteger(period), 3) * Grid * Grid;
                output[2][end] = Round(c, denom);
                output[3][end] = Round(a, denom);
                output[4][end] = Round(b, denom);
            }
            if (a.IsZero || b.IsZero)
            {
                output[0][end] = full ? null : 0;
                continue;
            }
            output[0][end] = c.Sign * DispersionReferenceArithmetic.Sqrt(c * c, a * b);
            if (full)
                output[1][end] = Round(c * c, a * b);
        }
        return output;
    }

    internal static double?[][] NativeReference(double[] x, double[] y, int period, bool full)
    {
        var result = Enumerable.Range(0, full ? 5 : 1).Select(_ => new double?[x.Length]).ToArray();
        double sx = 0,
            sy = 0,
            xx = 0,
            yy = 0,
            xy = 0;
        void AddPoint(int i, bool remove)
        {
            if (remove)
            {
                sx -= x[i];
                sy -= y[i];
                xx -= x[i] * x[i];
                yy -= y[i] * y[i];
                xy -= x[i] * y[i];
            }
            else
            {
                sx += x[i];
                sy += y[i];
                xx += x[i] * x[i];
                yy += y[i] * y[i];
                xy += x[i] * y[i];
            }
        }
        static double? Clean(double v) => double.IsNaN(v) ? null : v;
        for (var i = 0; i < x.Length; i++)
        {
            if (full)
            {
                if (i < period - 1)
                    continue;
                sx = sy = xx = yy = xy = 0;
                for (var j = i - period + 1; j <= i; j++)
                    AddPoint(j, false);
                var mx = sx / period;
                var my = sy / period;
                var a = xx / period - mx * mx;
                var b = yy / period - my * my;
                var c = xy / period - mx * my;
                var divisor = Math.Sqrt(a * b);
                result[0][i] = divisor == 0 ? null : Clean(c / divisor);
                result[1][i] = result[0][i] * result[0][i];
                result[2][i] = Clean(c);
                result[3][i] = Clean(a);
                result[4][i] = Clean(b);
            }
            else
            {
                if (i >= period)
                    AddPoint(i - period, true);
                AddPoint(i, false);
                if (i < period - 1)
                    continue;
                var product = (xx - sx * sx / period) * (yy - sy * sy / period);
                result[0][i] = product > 0 ? (xy - sx * sy / period) / Math.Sqrt(product) : 0;
            }
        }
        return result;
    }

    internal static CompetitorData Fixture(int count = 31) =>
        CompetitorData.FromBodies(
            Enumerable
                .Range(0, count)
                .Select(i => new[] { 3d, -4, 8, 2, -1, 7, 5 }[i % 7] + i / 16d)
                .ToArray(),
            Enumerable
                .Range(0, count)
                .Select(i => new[] { -2d, 5, 1, 8, -3, 4 }[i % 6] - i / 32d)
                .ToArray()
        );
}
