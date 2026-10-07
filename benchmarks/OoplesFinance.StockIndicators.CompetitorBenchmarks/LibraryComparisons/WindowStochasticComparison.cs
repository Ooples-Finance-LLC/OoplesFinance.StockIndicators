using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class WindowStochasticComparison
{
    internal static readonly string[] Names = ["K", "D", "J"];

    internal static ComparisonSeries Series(double?[][] values) =>
        RetrospectivePriceComparison.Series(Names, values);

    internal static ComparisonPair Pair(
        int period = 14,
        int k = 3,
        int d = 3,
        bool wilder = false,
        double kFactor = 3,
        double dFactor = 2
    ) =>
        new(
            "Skender.GetStoch",
            nameof(WindowStochasticKdj),
            (data, _) => Native(data, period, k, d, wilder, kFactor, dFactor),
            (data, _) => Owned(data.IndicatorBars, period, k, d, wilder, kFactor, dFactor),
            (data, _) =>
                Series(Reference(data.IndicatorBars, period, k, d, wilder, kFactor, dFactor)),
            Names,
            MinimumInputCount: wilder && k > 1 ? period : 0,
            CompetitorReference: (data, _) =>
                Series(NativeReference(QuoteBars(data), period, k, d, wilder, kFactor, dFactor)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static Bar[] QuoteBars(CompetitorData data) =>
        data
            .Quotes.Select(q => new Bar(
                q.Date,
                (double)q.Open,
                (double)q.High,
                (double)q.Low,
                (double)q.Close,
                (double)q.Volume
            ))
            .ToArray();

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        int k,
        int d,
        bool wilder,
        double kFactor,
        double dFactor
    )
    {
        var results = data
            .Quotes.GetStoch(period, d, k, kFactor, dFactor, wilder ? MaType.SMMA : MaType.SMA)
            .ToArray();
        return Series([
            results.Select(r => r.Oscillator).ToArray(),
            results.Select(r => r.Signal).ToArray(),
            results.Select(r => r.PercentJ).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int period,
        int k,
        int d,
        bool wilder,
        double kFactor,
        double dFactor
    )
    {
        var indicator = new WindowStochasticKdj(period, k, d, wilder, kFactor, dFactor);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 3)
                .Select(j =>
                {
                    var values = run[indicator.Outputs[j]].ToArray();
                    var flags = run[indicator.Outputs[j + 3]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    internal static double?[][] Reference(
        Bar[] bars,
        int period,
        int k,
        int d,
        bool wilder,
        double kFactor,
        double dFactor
    )
    {
        var raw = new BigInteger?[bars.Length];
        for (var i = period - 1; i < bars.Length; i++)
        {
            var window = bars.Skip(i - period + 1).Take(period).ToArray();
            var low = Units(window.Min(b => b.Low));
            var denominator = Units(window.Max(b => b.High)) - low;
            var numerator = 100 * (Units(bars[i].Close) - low) * Grid;
            if (denominator.Sign < 0)
            {
                denominator = -denominator;
                numerator = -numerator;
            }
            raw[i] = denominator.IsZero
                ? BigInteger.Zero
                : DirectionalComparison.RoundedUnits(numerator, denominator);
        }
        var a = Smooth(raw, k, wilder);
        var b = Smooth(a, d, wilder);
        var output = Enumerable.Range(0, 3).Select(_ => new double?[bars.Length]).ToArray();
        for (var i = 0; i < bars.Length; i++)
        {
            output[0][i] = a[i].HasValue ? Round(a[i]!.Value, Grid) : null;
            output[1][i] = b[i].HasValue ? Round(b[i]!.Value, Grid) : null;
            if (a[i].HasValue && b[i].HasValue)
                output[2][i] = Round(
                    Units(kFactor) * a[i]!.Value - Units(dFactor) * b[i]!.Value,
                    Grid * Grid
                );
        }
        return output;
    }

    private static BigInteger?[] Smooth(BigInteger?[] input, int period, bool wilder)
    {
        var output = new BigInteger?[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            if (!input[i].HasValue)
                continue;
            if (wilder)
                output[i] =
                    i == 0 || !output[i - 1].HasValue
                        ? input[i]
                        : DirectionalComparison.RoundedUnits(
                            (period - 1) * output[i - 1]!.Value + input[i]!.Value,
                            period
                        );
            else if (i >= period - 1)
            {
                var window = input.Skip(i - period + 1).Take(period).ToArray();
                if (window.Any(v => !v.HasValue))
                    continue;
                output[i] = DirectionalComparison.RoundedUnits(
                    window.Aggregate(BigInteger.Zero, (sum, v) => sum + v!.Value),
                    period
                );
            }
        }
        return output;
    }

    internal static double?[][] NativeReference(
        Bar[] bars,
        int period,
        int k,
        int d,
        bool wilder,
        double kFactor,
        double dFactor
    )
    {
        var raw = new double?[bars.Length];
        for (var i = period - 1; i < bars.Length; i++)
        {
            var window = bars.Skip(i - period + 1).Take(period).ToArray();
            var high = window.Max(b => b.High);
            var low = window.Min(b => b.Low);
            // The competitor branches on an exactly flat range; a tolerance changes its formula.
            var value = high.Equals(low) // NOSONAR S1244: reproduce the native exact flat-range branch.
                ? 0
                : Divide(Multiply(100, Subtract(bars[i].Close, low)), Subtract(high, low));
            raw[i] = double.IsNaN(value) ? null : value;
        }
        var a = NativeSmooth(raw, k, wilder);
        var b = NativeSmooth(a, d, wilder);
        var j = new double?[bars.Length];
        for (var i = 0; i < bars.Length; i++)
            if (a[i].HasValue && b[i].HasValue)
                j[i] = NativeDifference(
                    NativeProduct(kFactor, a[i]!.Value),
                    NativeProduct(dFactor, b[i]!.Value)
                );
        return [a, b, j];
    }

    private static double NativeProduct(double a, double b) =>
        double.IsFinite(a) && double.IsFinite(b) ? Multiply(a, b) : a * b;

    private static double NativeDifference(double a, double b) =>
        double.IsFinite(a) && double.IsFinite(b) ? Subtract(a, b) : a - b;

    private static double?[] NativeSmooth(double?[] input, int period, bool wilder)
    {
        if (period == 1)
            return input.ToArray();
        var output = new double?[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            if (!input[i].HasValue)
                continue;
            if (wilder)
            {
                var seed = i == 0 ? input[i]!.Value : output[i - 1] ?? input[i]!.Value;
                output[i] = Divide(Add(Multiply(seed, period - 1), input[i]!.Value), period);
            }
            else if (i >= period - 1)
            {
                var window = input.Skip(i - period + 1).Take(period).ToArray();
                if (window.Any(v => !v.HasValue))
                    continue;
                output[i] = Divide(
                    window.Aggregate(0d, (sum, value) => Add(sum, value!.Value)),
                    period
                );
            }
        }
        return output;
    }
}
