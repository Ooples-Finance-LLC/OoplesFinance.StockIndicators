using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class McGinleyComparison
{
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonPair Pair(bool reset, double factor = .6) =>
        new(
            reset ? "Skender.GetDynamic" : "QuanTAlib.Mgdi",
            nameof(QuarticDynamicAverage),
            (d, p) => Native(d, p, factor, reset),
            (d, p) => Owned(d.IndicatorBars, p, factor, reset),
            (d, p) => Series(GridReference(d.Closes, p, factor, reset)),
            CompetitorReference: (d, p) =>
                Series(
                    NativeReference(
                        reset ? d.Quotes.Select(q => (double)q.Close).ToArray() : d.Closes,
                        p,
                        factor,
                        reset
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[] values) =>
        RetrospectivePriceComparison.Series(["Value"], [values]);

    private static ComparisonSeries Native(CompetitorData d, int p, double factor, bool reset)
    {
        if (reset)
            return Series(d.Quotes.GetDynamic(p, factor).Select(v => v.Dynamic).ToArray());
        var indicator = new QuanTAlib.Mgdi(p, factor);
        return Series(
            d.Closes.Select(v =>
                    (double?)indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value
                )
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int p, double factor, bool reset)
    {
        var indicator = new QuarticDynamicAverage(
            p,
            factor,
            reset ? McGinleyStartup.ResetDelay : McGinleyStartup.Immediate
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var v = run[indicator.Value].ToArray();
        var mask = run[indicator.IsDefined].ToArray();
        return Series(v.Select((x, i) => mask[i] > 0 ? (double?)x : null).ToArray());
    }

    internal static double?[] GridReference(double[] prices, int p, double factor, bool reset)
    {
        var result = new double?[prices.Length];
        if (prices.Length == 0)
            return result;
        var previous = prices[0];
        long start = 1;
        var k = Units(factor) * p;
        for (var i = 0; i < prices.Length; i++)
        {
            if (i == 0 && !reset)
            {
                result[i] = previous;
                continue;
            }
            if (reset && previous == 0) // NOSONAR: Exact restart boundary.
            {
                previous = prices[i];
                start = (long)i + p;
                continue;
            }
            var old = Units(previous);
            var value = Units(prices[i]);
            if (old.IsZero)
                previous = Round(value, k);
            else if (value.IsZero)
                previous = old.Sign > 0 ? double.NegativeInfinity : double.PositiveInfinity;
            else
            {
                var fourth = value * value * value * value;
                var divisor = k * fourth;
                previous = Round(
                    old * divisor + Grid * (value - old) * old * old * old * old,
                    divisor * Grid
                );
            }
            if (!double.IsFinite(previous))
            {
                result[i] = previous;
                return result;
            }
            if (!reset || i >= start)
                result[i] = previous;
        }
        return result;
    }

    internal static double?[] NativeReference(double[] prices, int p, double factor, bool reset)
    {
        var result = new double?[prices.Length];
        if (prices.Length == 0)
            return result;
        var previous = prices[0];
        var start = 1;
        for (var i = 0; i < prices.Length; i++)
        {
            if (i == 0 && !reset)
            {
                result[i] = previous;
                continue;
            }
            if (reset && (double.IsNaN(prices[i]) || previous == 0)) // NOSONAR: Exact native restart boundary.
            {
                previous = prices[i];
                start = checked(i + p);
                continue;
            }
            var ratio = previous == 0 ? 1 : prices[i] / previous; // NOSONAR: Native zero-state substitution.
            var next = previous + (prices[i] - previous) / (factor * p * Math.Pow(ratio, 4));
            previous = next;
            if (!reset || i >= start)
                result[i] = reset && double.IsNaN(next) ? null : next;
        }
        return result;
    }

    internal static int Check(ComparisonPair pair, CompetitorData data, int period)
    {
        var expected = pair.Reference!(data, period).Outputs["Value"];
        var native = pair.CompetitorReference!(data, period).Outputs["Value"];
        var failed = Array.FindIndex(expected.Values, v => double.IsInfinity(v));
        var badNative = native
            .Values.Where((_, i) => native.Present![i])
            .Any(v => !double.IsFinite(v));
        if (failed < 0 && !badNative)
            return ComparisonVerifier.Check(pair, data, period);
        if (failed < 0)
            ComparisonVerifier.Compare(
                pair.Reference(data, period),
                pair.Ooples(data, period),
                pair.Id + " finite boundary",
                IndicatorErrorBudget.Exact
            );
        else
        {
            var prefix = data.Take(failed);
            ComparisonVerifier.Compare(pair.Reference(prefix, period), pair.Ooples(prefix, period), pair.Id + " finite prefix", IndicatorErrorBudget.Exact);
            var rejected = false;
            try
            {
                pair.Ooples(data, period);
            }
            catch (IndicatorOutputException ex)
            {
                if (
                    ex.OutputSlot != 0
                    || ex.BarIndex != failed
                    || !ex.Value.Equals(expected.Values[failed]) // NOSONAR: The signed overflow endpoint must match exactly.
                )
                    throw new InvalidOperationException("Wrong McGinley overflow boundary", ex);
                rejected = true;
            }
            if (!rejected)
                throw new InvalidOperationException(
                    "McGinley accepted an undefined/overflowing recurrence"
                );
        }
        for (var replay = 0; replay < 2; replay++)
        {
            var actual = pair.Competitor(data, period);
            if (
                actual.Outputs.Count != 1
                || !actual.Outputs.TryGetValue("Value", out var row)
                || row.FirstValid != native.FirstValid
                || row.Values.Length != native.Values.Length
                || row.Present is null
                || !row.Present.SequenceEqual(native.Present!)
            )
                throw new InvalidOperationException("McGinley native boundary shape differs");
            for (var i = 0; i < native.Values.Length; i++)
                if (!native.Values[i].Equals(row.Values[i])) // NOSONAR: Native operation reference includes exact NaN/infinity semantics.
                    throw new InvalidOperationException(
                        $"McGinley native boundary value {i} differs"
                    );
        }
        return native.Present!.Count(v => v)
            + (
                failed < 0
                    ? expected.Present!.Count(v => v)
                    : expected.Present!.Take(failed).Count(v => v)
            );
    }
}
