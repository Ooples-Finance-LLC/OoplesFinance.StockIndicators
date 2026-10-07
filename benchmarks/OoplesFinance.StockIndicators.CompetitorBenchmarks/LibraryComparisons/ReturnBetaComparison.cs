using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ReturnBetaComparison
{
    internal static readonly string[] FullNames =
    [
        "Beta",
        "BetaUp",
        "BetaDown",
        "Ratio",
        "Convexity",
        "ReturnsEval",
        "ReturnsMrkt",
    ];
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static string[] Names(bool full) => full ? FullNames : ["Beta"];

    internal static ComparisonPair Create(
        bool full,
        ReturnBetaSelection selection = ReturnBetaSelection.All
    ) =>
        new(
            full ? "Skender.GetBeta" : "TaLib.Functions.Beta",
            full ? nameof(WindowBetaStatistics) : nameof(WindowReturnBeta),
            (d, p) => Native(d, p, full, selection),
            (d, p) => Owned(d.IndicatorBars, p, full, selection),
            (d, p) =>
                RetrospectivePriceComparison.Series(
                    Names(full),
                    Reference(
                        full ? d.Opens : d.Closes,
                        full ? d.Closes : d.Opens,
                        p,
                        full,
                        selection
                    )
                ),
            Names(full),
            MinimumInputCount: full ? 1 : 2,
            CompetitorReference: (d, p) =>
                RetrospectivePriceComparison.Series(
                    Names(full),
                    NativeReference(
                        full ? d.Opens : d.Closes,
                        full ? d.Closes : d.Opens,
                        p,
                        full,
                        selection
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries FromSkender(IEnumerable<BetaResult> source)
    {
        var r = source.ToArray();
        return RetrospectivePriceComparison.Series(
            FullNames,
            [
                r.Select(v => v.Beta).ToArray(),
                r.Select(v => v.BetaUp).ToArray(),
                r.Select(v => v.BetaDown).ToArray(),
                r.Select(v => v.Ratio).ToArray(),
                r.Select(v => v.Convexity).ToArray(),
                r.Select(v => v.ReturnsEval).ToArray(),
                r.Select(v => v.ReturnsMrkt).ToArray(),
            ]
        );
    }

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        bool full,
        ReturnBetaSelection selection
    )
    {
        if (full)
            return FromSkender(
                data.Dates.Zip(data.Closes, (d, v) => (d, v))
                    .GetBeta(
                        data.Dates.Zip(data.Opens, (d, v) => (d, v)),
                        period,
                        (BetaType)selection
                    )
            );
        var values = new double?[data.Count];
        if (data.Count == 0)
            return RetrospectivePriceComparison.Series(Names(false), [values]);
        var packed = new double[data.Count];
        var code = Functions.Beta<double>(
            data.Closes,
            data.Opens,
            System.Range.All,
            packed,
            out var range,
            period
        );
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib beta returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != Math.Max(0, data.Count - period) || count > 0 && start != period)
            throw new InvalidOperationException("Unexpected beta alignment");
        for (var i = 0; i < count; i++)
            values[start + i] = packed[i];
        return RetrospectivePriceComparison.Series(Names(false), [values]);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int period,
        bool full,
        ReturnBetaSelection selection = ReturnBetaSelection.All
    )
    {
        IIndicator indicator = full
            ? new WindowBetaStatistics(period, selection)
            : new WindowReturnBeta(period);
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

    private static BigInteger Extended(BigInteger numerator, BigInteger denominator)
    {
        var shift = 0;
        double value;
        while (double.IsInfinity(value = Round(numerator, denominator << shift)))
            shift += 512;
        return Units(value) << shift;
    }

    internal static double?[][] Reference(
        double[] market,
        double[] evaluation,
        int period,
        bool full,
        ReturnBetaSelection selection
    )
    {
        var result = Enumerable
            .Range(0, full ? 7 : 1)
            .Select(_ => new double?[market.Length])
            .ToArray();
        var x = new BigInteger[market.Length];
        var y = new BigInteger[market.Length];
        BigInteger Change(double current, double previous) =>
            previous == 0 ? 0 : Extended(Units(current) - Units(previous), Units(previous));
        for (var i = 0; i < market.Length; i++)
        {
            x[i] = i == 0 ? 0 : Change(market[i], market[i - 1]);
            y[i] = i == 0 ? 0 : Change(evaluation[i], evaluation[i - 1]);
            if (full)
            {
                result[5][i] = Round(y[i], Grid);
                result[6][i] = Round(x[i], Grid);
            }
            if (i < period)
                continue;
            for (var slot = 0; slot < (full ? 3 : 1); slot++)
            {
                if (full && selection != ReturnBetaSelection.All && (int)selection != slot)
                    continue;
                var indices = Enumerable
                    .Range(i - period + 1, period)
                    .Where(j =>
                        slot == 0 || slot == 1 && x[j].Sign > 0 || slot == 2 && x[j].Sign < 0
                    )
                    .ToArray();
                var n = new BigInteger(indices.Length);
                var sx = indices.Aggregate(BigInteger.Zero, (s, j) => s + x[j]);
                var sy = indices.Aggregate(BigInteger.Zero, (s, j) => s + y[j]);
                var a = BigInteger.Zero;
                var c = a;
                foreach (var j in indices)
                {
                    var dx = n * x[j] - sx;
                    a += dx * dx;
                    c += dx * (n * y[j] - sy);
                }
                result[slot][i] = a.IsZero
                    ? full
                        ? null
                        : 0
                    : Round(c, a);
            }
            if (
                !full
                || selection != ReturnBetaSelection.All
                || !result[1][i].HasValue
                || !result[2][i].HasValue
                || !double.IsFinite(result[1][i]!.Value)
                || !double.IsFinite(result[2][i]!.Value)
            )
                continue;
            var up = Units(result[1][i]!.Value);
            var down = Units(result[2][i]!.Value);
            if (!down.IsZero)
                result[3][i] = Round(up, down);
            result[4][i] = Round((up - down) * (up - down), Grid * Grid);
        }
        return result;
    }

    internal static double?[][] NativeReference(
        double[] market,
        double[] evaluation,
        int period,
        bool full,
        ReturnBetaSelection selection
    )
    {
        var result = Enumerable
            .Range(0, full ? 7 : 1)
            .Select(_ => new double?[market.Length])
            .ToArray();
        var x = new double[market.Length];
        var y = new double[market.Length];
        double sx = 0,
            sy = 0,
            xx = 0,
            xy = 0;
        static double? Clean(double value) => double.IsNaN(value) ? null : value;
        for (var i = 0; i < market.Length; i++)
        {
            double Change(double[] prices) =>
                i == 0 || prices[i - 1] == 0 ? 0
                : full ? prices[i] / prices[i - 1] - 1
                : (prices[i] - prices[i - 1]) / prices[i - 1];
            x[i] = Change(market);
            y[i] = Change(evaluation);
            if (full)
            {
                result[5][i] = y[i];
                result[6][i] = x[i];
            }
            else if (i > 0)
            {
                xx += x[i] * x[i];
                xy += x[i] * y[i];
                sx += x[i];
                sy += y[i];
            }
            if (i < period)
                continue;
            if (!full)
            {
                var denominator = period * xx - sx * sx;
                result[0][i] = denominator != 0 ? (period * xy - sx * sy) / denominator : 0;
                var old = i - period + 1;
                xx -= x[old] * x[old];
                xy -= x[old] * y[old];
                sx -= x[old];
                sy -= y[old];
                continue;
            }
            for (var slot = 0; slot < 3; slot++)
            {
                if (selection != ReturnBetaSelection.All && (int)selection != slot)
                    continue;
                var indices = Enumerable
                    .Range(i - period + 1, period)
                    .Where(j => slot == 0 || slot == 1 && x[j] > 0 || slot == 2 && x[j] < 0)
                    .ToArray();
                if (indices.Length == 0)
                    continue;
                sx = sy = xx = xy = 0;
                foreach (var j in indices)
                {
                    sx += x[j];
                    sy += y[j];
                    xx += x[j] * x[j];
                    xy += x[j] * y[j];
                }
                var mx = sx / indices.Length;
                var my = sy / indices.Length;
                var variance = Clean(xx / indices.Length - mx * mx);
                var covariance = Clean(xy / indices.Length - mx * my);
                if (variance.HasValue && variance.Value != 0 && covariance.HasValue)
                    result[slot][i] = Clean(covariance.Value / variance.Value);
            }
            if (
                selection == ReturnBetaSelection.All
                && result[1][i].HasValue
                && result[2][i].HasValue
            )
            {
                var up = result[1][i]!.Value;
                var down = result[2][i]!.Value;
                result[3][i] = down != 0 ? up / down : null;
                result[4][i] = (up - down) * (up - down);
            }
        }
        return result;
    }

    internal static CompetitorData Fixture(int count = 37) =>
        CompetitorData.FromBodies(
            Enumerable
                .Range(0, count)
                .Select(i => new[] { 100d, 104, 101, 105, 97, 96, 101 }[i % 7] + i / 16d)
                .ToArray(),
            Enumerable
                .Range(0, count)
                .Select(i => new[] { 50d, 53, 51, 58, 55, 59 }[i % 6] + i / 32d)
                .ToArray()
        );
}
