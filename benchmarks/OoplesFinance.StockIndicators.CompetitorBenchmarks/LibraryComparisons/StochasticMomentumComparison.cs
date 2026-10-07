using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Trady.Analysis.Infrastructure;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class StochasticMomentumComparison
{
    internal static readonly string[] Variants = ["Raw", "Trady", "Skender"];
    internal static readonly ComparisonPair[] Pairs = Variants.Select(v => Create(v)).ToArray();

    internal static string Id(string variant) =>
        variant == "Skender"
            ? "Skender.GetSmi"
            : "Trady.Indicator.StochasticsMomentum" + (variant == "Raw" ? "" : "Index");

    internal static string[] Names(string variant) =>
        variant == "Skender" ? ["Value", "Signal"] : ["Value"];

    internal static ComparisonPair Create(
        string variant,
        int first = 25,
        int second = 2,
        int signal = 3
    ) =>
        new(
            Id(variant),
            variant == "Raw"
                ? nameof(WindowStochasticMomentum)
                : nameof(DoubleSmoothedStochasticMomentum),
            (d, p) => Native(d, p, variant, first, second, signal),
            (d, p) => Owned(d.IndicatorBars, p, variant, first, second, signal),
            (d, p) =>
                RetrospectivePriceComparison.Series(
                    Names(variant),
                    OwnedReference(d.IndicatorBars, p, variant, first, second, signal)
                ),
            Names(variant),
            CompetitorReference: (d, p) =>
                RetrospectivePriceComparison.Series(
                    Names(variant),
                    NativeReference(d, p, variant, first, second, signal)
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static IIndicator Indicator(
        int p,
        string variant,
        int first,
        int second,
        int signal
    ) =>
        variant == "Raw"
            ? new WindowStochasticMomentum(p)
            : new DoubleSmoothedStochasticMomentum(p, first, second, signal);

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int p,
        string variant,
        int first,
        int second,
        int signal
    )
    {
        var indicator = Indicator(p, variant, first, second, signal);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var flags = variant == "Raw" ? 1 : 2;
        return RetrospectivePriceComparison.Series(
            Names(variant),
            Enumerable
                .Range(0, Names(variant).Length)
                .Select(slot =>
                {
                    var present = run[indicator.Outputs[slot + flags]].ToArray();
                    return run[indicator.Outputs[slot]]
                        .ToArray()
                        .Select((v, i) => present[i] > 0 ? (double?)v : null)
                        .ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        string variant,
        int first,
        int second,
        int signal
    )
    {
        if (variant == "Skender")
        {
            var rows = data.Quotes.GetSmi(period, first, second, signal).ToArray();
            return RetrospectivePriceComparison.Series(
                Names(variant),
                [rows.Select(r => r.Smi).ToArray(), rows.Select(r => r.Signal).ToArray()]
            );
        }
        var values =
            variant == "Raw"
                ? new T.StochasticsMomentum(data.Candles, period)
                    .Compute()
                    .Select(r => (double?)r.Tick)
                : new T.StochasticsMomentumIndex(data.Candles, period, first, second)
                    .Compute()
                    .Select(r => (double?)r.Tick);
        return VolumePriceComparison.Mask(values.ToArray());
    }

    internal static AnalyzableBase<
        (decimal High, decimal Low, decimal Close),
        (decimal High, decimal Low, decimal Close),
        decimal?,
        decimal?
    > Tuple(
        (decimal High, decimal Low, decimal Close)[] prices,
        int p,
        bool index,
        int first,
        int second
    ) =>
        index
            ? new T.StochasticsMomentumIndexByTuple(prices, p, first, second)
            : new T.StochasticsMomentumByTuple(prices, p);

    internal static double?[][] OwnedReference(
        Bar[] bars,
        int period,
        string variant,
        int first,
        int second,
        int signal
    )
    {
        BigInteger Stage(BigInteger n, BigInteger d)
        {
            var shift = 0;
            double rounded;
            while (!double.IsFinite(rounded = Round(n, d << shift)))
                shift += 512;
            return Units(rounded) << shift;
        }
        BigInteger Smooth(BigInteger value, BigInteger previous, int length) =>
            Stage(2 * value + (length - 1) * previous, ((BigInteger)length + 1) * Grid);
        var output = Enumerable
            .Range(0, Names(variant).Length)
            .Select(_ => new double?[bars.Length])
            .ToArray();
        BigInteger a = 0,
            b = 0,
            c = 0,
            d = 0;
        double? previousSignal = null;
        for (var i = period - 1; i < bars.Length; i++)
        {
            var high = Units(bars.Skip(i - period + 1).Take(period).Max(v => v.High));
            var low = Units(bars.Skip(i - period + 1).Take(period).Min(v => v.Low));
            var delta = Stage(2 * Units(bars[i].Close) - high - low, 2 * Grid);
            var range = Stage(high - low, Grid);
            if (variant == "Raw")
            {
                output[0][i] = Round(delta, Grid);
                continue;
            }
            if (i == period - 1)
            {
                a = b = delta;
                c = d = range;
            }
            else
            {
                a = Smooth(delta, a, first);
                b = Smooth(a, b, second);
                c = Smooth(range, c, first);
                d = Smooth(c, d, second);
            }
            if (d.IsZero)
            {
                previousSignal = null;
                continue;
            }
            var index = Round(200 * b, d);
            output[0][i] = index;
            if (variant != "Skender" || !double.IsFinite(index))
                continue;
            previousSignal = previousSignal.HasValue
                ? Round(
                    2 * Units(index) + (signal - 1) * Units(previousSignal.Value),
                    ((BigInteger)signal + 1) * Grid
                )
                : index;
            output[1][i] = previousSignal;
        }
        return output;
    }

    internal static decimal?[] TradyReference(
        (decimal High, decimal Low, decimal Close)[] prices,
        int period,
        bool index,
        int first,
        int second
    )
    {
        var result = new decimal?[prices.Length];
        decimal a = 0,
            b = 0,
            c = 0,
            d = 0;
        for (var i = period - 1; i < prices.Length; i++)
        {
            var high = prices.Skip(i - period + 1).Take(period).Max(v => v.High);
            var low = prices.Skip(i - period + 1).Take(period).Min(v => v.Low);
            var delta = prices[i].Close - .5m * (high + low);
            if (!index)
            {
                result[i] = delta;
                continue;
            }
            var range = high - low;
            if (i == period - 1)
            {
                a = b = delta;
                c = d = range;
            }
            else
            {
                a += 2m / (first + 1) * (delta - a);
                b += 2m / (second + 1) * (a - b);
                c += 2m / (first + 1) * (range - c);
                d += 2m / (second + 1) * (c - d);
            }
            var half = d / 2;
            if (half != 0)
                result[i] = 100 * b / half;
        }
        return result;
    }

    internal static double?[][] NativeReference(
        CompetitorData data,
        int period,
        string variant,
        int first,
        int second,
        int signal
    )
    {
        if (variant != "Skender")
            return
            [
                TradyReference(
                        data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray(),
                        period,
                        variant != "Raw",
                        first,
                        second
                    )
                    .Select(v => (double?)v)
                    .ToArray(),
            ];
        // Exact staged binary64 operations for finite operands, with IEEE
        // propagation retained when a native stage has already become invalid.
        double A(double x, double y) =>
            double.IsFinite(x) && double.IsFinite(y) ? Add(x, y) : x + y;
        double S(double x, double y) =>
            double.IsFinite(x) && double.IsFinite(y) ? Subtract(x, y) : x - y;
        double M(double x, double y) =>
            double.IsFinite(x) && double.IsFinite(y) ? Multiply(x, y) : x * y;
        double D(double x, double y) =>
            double.IsFinite(x) && double.IsFinite(y) && y != 0 ? Divide(x, y) : x / y; // NOSONAR: Exact zero selects native IEEE division behavior.
        double Smooth(double value, double previous, double alpha) =>
            A(previous, M(alpha, S(value, previous)));
        var k1 = D(2, checked(first + 1));
        var k2 = D(2, checked(second + 1));
        var ks = D(2, checked(signal + 1));
        var output = new[] { new double?[data.Count], new double?[data.Count] };
        double a = 0,
            b = 0,
            c = 0,
            d = 0,
            previousSignal = 0;
        for (var i = period - 1; i < data.Count; i++)
        {
            var high = data.Quotes.Skip(i - period + 1).Take(period).Max(v => (double)v.High);
            var low = data.Quotes.Skip(i - period + 1).Take(period).Min(v => (double)v.Low);
            var delta = S((double)data.Quotes[i].Close, M(.5, A(high, low)));
            var range = S(high, low);
            if (i == period - 1)
            {
                a = b = delta;
                c = d = range;
            }
            a = Smooth(delta, a, k1);
            b = Smooth(a, b, k2);
            c = Smooth(range, c, k1);
            d = Smooth(c, d, k2);
            var index = M(100, D(b, M(.5, d)));
            if (i == period - 1)
                previousSignal = index;
            previousSignal = Smooth(index, previousSignal, ks);
            output[0][i] = index;
            output[1][i] = previousSignal;
        }
        return output;
    }
}
