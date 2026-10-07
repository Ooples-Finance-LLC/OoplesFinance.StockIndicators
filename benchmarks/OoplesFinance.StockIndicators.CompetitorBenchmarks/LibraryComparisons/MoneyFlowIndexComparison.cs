using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class MoneyFlowIndexComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static ComparisonPair Create(bool ta) =>
        new(
            ta ? "TaLib.Functions.Mfi" : "Skender.GetMfi",
            nameof(WindowMoneyFlowIndex),
            (d, p) => Native(d, p, ta),
            (d, p) => Owned(d.IndicatorBars, p, ta),
            (d, p) => VolumePriceComparison.Mask(Reference(d.IndicatorBars, p, ta)),
            MinimumInputCount: ta ? 2 : 1,
            CompetitorReference: (d, p) =>
                VolumePriceComparison.Mask(
                    NativeReference(ta ? d.IndicatorBars : QuoteBars(d), p, ta)
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static Bar[] QuoteBars(CompetitorData d) =>
        d
            .Quotes.Select(q => new Bar(
                q.Date,
                (double)q.Open,
                (double)q.High,
                (double)q.Low,
                (double)q.Close,
                (double)q.Volume
            ))
            .ToArray();

    private static ComparisonSeries Native(CompetitorData data, int period, bool ta)
    {
        if (!ta)
            return VolumePriceComparison.Mask(
                data.Quotes.GetMfi(period).Select(r => r.Mfi).ToArray()
            );
        var result = new double?[data.Count];
        if (data.Count == 0)
            return VolumePriceComparison.Mask(result);
        var packed = new double[data.Count];
        var code = Functions.Mfi<double>(
            data.Highs,
            data.Lows,
            data.Closes,
            data.Volumes,
            System.Range.All,
            packed,
            out var range,
            period
        );
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA MFI returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != Math.Max(0, data.Count - period) || count > 0 && start != period)
            throw new InvalidOperationException("Unexpected MFI alignment");
        for (var i = 0; i < count; i++)
            result[start + i] = packed[i];
        return VolumePriceComparison.Mask(result);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, bool ta, int suppressed = 0)
    {
        var indicator = new WindowMoneyFlowIndex(period, ta, suppressed);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var mask = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    internal static double?[] Reference(Bar[] bars, int period, bool ta, int suppressed = 0)
    {
        var prices = bars.Select(b =>
                Units(Round(Units(b.High) + Units(b.Low) + Units(b.Close), 3 * Grid))
            )
            .ToArray();
        var result = new double?[bars.Length];
        for (long end = (long)period + suppressed; end < bars.Length; end++)
        {
            var i = (int)end;
            var pos = BigInteger.Zero;
            var neg = BigInteger.Zero;
            for (var j = i - period + 1; j <= i; j++)
            {
                var flow = prices[j] * Units(bars[j].Volume);
                if (prices[j] > prices[j - 1])
                    pos += flow;
                else if (prices[j] < prices[j - 1])
                    neg += flow;
            }
            var total = pos + neg;
            result[i] =
                ta && total < Grid * Grid ? 0
                : !ta && neg.IsZero ? 100
                : total.IsZero ? double.NegativeInfinity
                : Round(100 * pos, total);
        }
        return result;
    }

    internal static double?[] NativeReference(Bar[] bars, int period, bool ta, int suppressed = 0)
    {
        var result = new double?[bars.Length];
        if (ta && bars.Length <= (long)period + suppressed)
            return result;
        var prices = bars.Select(b => Divide(Add(Add(b.High, b.Low), b.Close), 3)).ToArray();
        var pos = new double[bars.Length];
        var neg = new double[bars.Length];
        var positive = 0d;
        var negative = 0d;
        for (var i = 1; i < bars.Length; i++)
        {
            var flow = Multiply(prices[i], bars[i].Volume);
            if (prices[i] > prices[i - 1])
                pos[i] = flow;
            else if (prices[i] < prices[i - 1])
                neg[i] = flow;
            if (ta)
            {
                if (i > period)
                {
                    positive = Subtract(positive, pos[i - period]);
                    negative = Subtract(negative, neg[i - period]);
                }
                positive = Add(positive, pos[i]);
                negative = Add(negative, neg[i]);
            }
            if ((long)i < period + (long)suppressed)
                continue;
            if (!ta)
            {
                positive = pos.Skip(i - period + 1).Take(period).Aggregate(0d, Add);
                negative = neg.Skip(i - period + 1).Take(period).Aggregate(0d, Add);
            }
            var total = Add(positive, negative);
            result[i] =
                ta ? (total >= 1 ? Multiply(100, Divide(positive, total)) : 0)
                : negative == 0 ? 100 // NOSONAR: Skender uses an exactly zero negative-flow branch.
                : Subtract(100, Divide(100, Add(1, Divide(positive, negative))));
        }
        return result;
    }
}
