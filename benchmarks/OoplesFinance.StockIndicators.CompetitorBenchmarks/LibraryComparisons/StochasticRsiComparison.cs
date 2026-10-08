using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class StochasticRsiComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static string[] Names(bool skender) => skender ? ["Value", "Signal"] : ["Value"];

    internal static ComparisonPair Create(
        bool skender,
        int? stochastic = null,
        int signal = 3,
        int smooth = 1
    ) =>
        new(
            skender ? "Skender.GetStochRsi" : "Trady.Indicator.StochasticsRsiOscillator",
            skender ? nameof(SmoothedStochasticRsi) : nameof(NullableStochasticRsi),
            (d, p) => Native(d, p, skender, stochastic ?? p, signal, smooth),
            (d, p) => Owned(d.IndicatorBars, p, skender, stochastic ?? p, signal, smooth),
            (d, p) =>
                skender
                    ? RetrospectivePriceComparison.Series(
                        Names(true),
                        SmoothedReference(
                            d.IndicatorBars,
                            p,
                            stochastic ?? p,
                            signal,
                            smooth,
                            false
                        )
                    )
                    : VolumePriceComparison.Mask(
                        NullableReference(d.Closes.Select(v => (double?)v).ToArray(), p)
                    ),
            Names(skender),
            CompetitorReference: (d, p) =>
                skender
                    ? RetrospectivePriceComparison.Series(
                        Names(true),
                        SmoothedReference(
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
                            stochastic ?? p,
                            signal,
                            smooth,
                            true
                        )
                    )
                    : VolumePriceComparison.Mask(
                        TradyReference(d.Candles.Select(c => (decimal?)c.Close).ToArray(), p)
                            .Select(v => (double?)v)
                            .ToArray()
                    ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        bool skender,
        int stochastic,
        int signal,
        int smooth
    )
    {
        if (!skender)
            return VolumePriceComparison.Mask(
                new T.StochasticsRsiOscillator(data.Candles, period)
                    .Compute()
                    .Select(r => (double?)r.Tick)
                    .ToArray()
            );
        var rows = data.Quotes.GetStochRsi(period, stochastic, signal, smooth).ToArray();
        return RetrospectivePriceComparison.Series(
            Names(true),
            [rows.Select(r => r.StochRsi).ToArray(), rows.Select(r => r.Signal).ToArray()]
        );
    }

    internal static IIndicator Indicator(
        int period,
        bool skender,
        int stochastic,
        int signal,
        int smooth
    ) =>
        skender
            ? new SmoothedStochasticRsi(period, stochastic, signal, smooth)
            : new NullableStochasticRsi(period);

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int period,
        bool skender,
        int stochastic,
        int signal,
        int smooth
    )
    {
        var indicator = Indicator(period, skender, stochastic, signal, smooth);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var count = Names(skender).Length;
        return RetrospectivePriceComparison.Series(
            Names(skender),
            Enumerable
                .Range(0, count)
                .Select(slot =>
                {
                    var mask = run[indicator.Outputs[slot + count]].ToArray();
                    return run[indicator.Outputs[slot]]
                        .ToArray()
                        .Select((v, i) => mask[i] > 0 ? (double?)v : null)
                        .ToArray();
                })
                .ToArray()
        );
    }

    internal static double?[] NullableReference(double?[] prices, int period)
    {
        var rsi = NullableStrengthComparison.OwnedReference(
            prices,
            period,
            NullableStrengthConvention.RelativeStrengthIndex,
            1
        );
        var output = new double?[prices.Length];
        for (var i = period - 1; i < prices.Length; i++)
        {
            var known = rsi.Skip(i - period + 1)
                .Take(period)
                .Where(v => v.HasValue)
                .Select(v => Units(v!.Value))
                .ToArray();
            if (known.Length == 0 || known.Min() == known.Max())
            {
                output[i] = .5;
                continue;
            }
            if (rsi[i].HasValue)
                output[i] = Round(Units(rsi[i]!.Value) - known.Min(), known.Max() - known.Min());
        }
        return output;
    }

    internal static decimal?[] TradyReference(decimal?[] prices, int period)
    {
        var rsi = NullableStrengthComparison.NativeReference(
            prices,
            period,
            NullableStrengthConvention.RelativeStrengthIndex,
            1
        );
        var output = new decimal?[prices.Length];
        for (var i = period - 1; i < prices.Length; i++)
        {
            var known = rsi.Skip(i - period + 1)
                .Take(period)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToArray();
            if (known.Length == 0 || known.Min() == known.Max())
            {
                output[i] = .5m;
                continue;
            }
            if (rsi[i].HasValue)
                output[i] = (rsi[i]!.Value - known.Min()) / (known.Max() - known.Min());
        }
        return output;
    }

    internal static double?[][] SmoothedReference(
        Bar[] bars,
        int rsiPeriod,
        int stochPeriod,
        int signalPeriod,
        int smoothPeriod,
        bool native
    )
    {
        double?[] rsi;
        if (native)
        {
            var (values, range) = WilderStrengthComparison.NativeReference(
                bars.Select(b => b.Close).ToArray(),
                rsiPeriod,
                WilderStrengthConvention.RsiHundredFlat
            );
            rsi = new double?[bars.Length];
            for (var i = 0; i < values.Length; i++)
                rsi[i + range.Start.Value] = values[i];
        }
        else
        {
            // Reuse the independent RSI quantizer while retaining raw binary64
            // inputs, including prices outside the native decimal quote domain.
            rsi = OwnedRsi(bars, rsiPeriod);
        }
        var raw = new double?[bars.Length];
        for (long end = (long)rsiPeriod + stochPeriod - 1; end < bars.Length; end++)
        {
            var i = (int)end;
            var window = rsi.Skip(i - stochPeriod + 1)
                .Take(stochPeriod)
                .Select(v => v!.Value)
                .ToArray();
            var high = window.Max();
            var low = window.Min();
            if (high == low) // NOSONAR: Exactly flat stochastic windows use zero.
            {
                raw[i] = 0;
                continue;
            }
            raw[i] = native
                ? Divide(Multiply(100, Subtract(rsi[i]!.Value, low)), Subtract(high, low))
                : Round(100 * (Units(rsi[i]!.Value) - Units(low)), Units(high) - Units(low));
        }
        double?[] Mean(double?[] values, int period)
        {
            var output = new double?[values.Length];
            for (var i = period - 1; i < values.Length; i++)
            {
                var window = values.Skip(i - period + 1).Take(period).ToArray();
                if (window.Any(v => !v.HasValue))
                    continue;
                output[i] = native
                    ? Divide(window.Aggregate(0d, (sum, v) => Add(sum, v!.Value)), period)
                    : Round(
                        window.Aggregate(BigInteger.Zero, (sum, v) => sum + Units(v!.Value)),
                        period * Grid
                    );
            }
            return output;
        }
        var value = Mean(raw, smoothPeriod);
        return [value, Mean(value, signalPeriod)];
    }

    private static double?[] OwnedRsi(Bar[] bars, int period)
    {
        var rsi = WilderStrengthComparison
            .OwnedReference(
                bars.Select(b => b.Close).ToArray(),
                period,
                WilderStrengthConvention.RsiHundredFlat
            )
            .Outputs["Value"];
        return rsi.Values.Select((v, i) => rsi.Present![i] ? (double?)v : null).ToArray();
    }
}
