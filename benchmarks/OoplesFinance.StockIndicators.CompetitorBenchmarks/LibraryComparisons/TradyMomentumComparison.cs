using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis;
using Trady.Core.Infrastructure;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TradyMomentumComparison
{
    internal static readonly (string Name, PriceChangeKind Kind)[] Forms =
    [
        ("Momentum", PriceChangeKind.Difference),
        ("Difference", PriceChangeKind.Difference),
        ("UpMomentum", PriceChangeKind.Gain),
        ("PositiveDifference", PriceChangeKind.Gain),
        ("DownMomentum", PriceChangeKind.Loss),
        ("NegativeDifference", PriceChangeKind.Loss),
    ];
    internal static readonly ComparisonPair[] Pairs = Forms
        .Select(form => new ComparisonPair(
            "Trady.Indicator." + form.Name,
            "LaggedPriceChange(" + form.Kind + ")",
            (data, period) => Competitor(form.Name, data, period),
            (data, period) => Ooples(form.Kind, data, period),
            (data, period) => Reference(form.Kind, data, period)
        ))
        .ToArray();

    private static ComparisonSeries Ooples(PriceChangeKind kind, CompetitorData data, int period)
    {
        var indicator = new LaggedPriceChange(period, kind);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(period, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data, int period)
    {
        var rows = name switch
        {
            "Momentum" => new T.Momentum(data.Candles, period).Compute(),
            "Difference" => new T.Difference<IOhlcv, AnalyzableTick<decimal?>>(
                data.Candles,
                c => c.Close,
                period
            ).Compute(),
            "UpMomentum" => new T.UpMomentum(data.Candles, period).Compute(),
            "PositiveDifference" => new T.PositiveDifference<IOhlcv, AnalyzableTick<decimal?>>(
                data.Candles,
                c => c.Close,
                period
            ).Compute(),
            "DownMomentum" => new T.DownMomentum(data.Candles, period).Compute(),
            "NegativeDifference" => new T.NegativeDifference<IOhlcv, AnalyzableTick<decimal?>>(
                data.Candles,
                c => c.Close,
                period
            ).Compute(),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        return new(
            Math.Min(period, data.Count),
            rows.Select(r => (double?)r.Tick ?? double.NaN).ToArray()
        );
    }

    private static ComparisonSeries Reference(PriceChangeKind kind, CompetitorData data, int period)
    {
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        for (var i = period; i < data.Count; i++)
        {
            var difference = (decimal)data.Closes[i] - (decimal)data.Closes[i - period];
            values[i] = (double)(
                kind switch
                {
                    PriceChangeKind.Gain => Math.Max(0, difference),
                    PriceChangeKind.Loss => Math.Max(0, -difference),
                    _ => difference,
                }
            );
        }
        return new(Math.Min(period, data.Count), values);
    }
}
