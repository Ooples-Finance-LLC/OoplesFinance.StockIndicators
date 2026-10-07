using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class RelativeVolatilityComparison
{
    internal static ComparisonPair Pair() =>
        new(
            "QuanTAlib.Rvi",
            nameof(RelativeVolatilitySnapshot),
            (d, p) =>
            {
                var indicator = new QuanTAlib.Rvi(p);
                return new(
                    0,
                    d.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                        .ToArray()
                );
            },
            (d, p) => new(0, RelativeVolatilitySnapshot.Calculate(d.IndicatorBars, p).ToArray()),
            (d, p) => new(0, Reference(d.Closes, p)),
            MinimumInputCount: 0,
            CompetitorReference: (d, p) => new(0, NativeReference(d.Closes, p)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static double[] Reference(double[] prices, int period)
    {
        var output = new double[prices.Length];
        var directions = new[] { new BigInteger[prices.Length], new BigInteger[prices.Length] };
        var deviations = new[] { new BigInteger[prices.Length], new BigInteger[prices.Length] };
        for (var i = 0; i < prices.Length; i++)
        {
            var change = Units(prices[i]) - (i == 0 ? BigInteger.Zero : Units(prices[i - 1]));
            directions[0][i] = BigInteger.Max(change, 0);
            directions[1][i] = BigInteger.Max(-change, 0);
            var count = Math.Min(period, i + 1);
            var averages = new BigInteger[2];
            for (var side = 0; side < 2; side++)
            {
                var window = directions[side].Skip(i + 1 - count).Take(count).ToArray();
                if (count > 1)
                {
                    // Pairwise squared distances avoid production's rolling moment identity.
                    var squared = BigInteger.Zero;
                    for (var a = 0; a < count; a++)
                    for (var b = a + 1; b < count; b++)
                        squared += BigInteger.Pow(window[a] - window[b], 2);
                    var denominator = (long)count * (count - 1) * Grid * Grid;
                    for (var shift = 0; ; shift += 32)
                    {
                        var root = DispersionReferenceArithmetic.Sqrt(
                            squared,
                            denominator << (2 * shift)
                        );
                        if (!double.IsFinite(root))
                            continue;
                        deviations[side][i] = Units(root) << shift;
                        break;
                    }
                }
                averages[side] = DirectionalComparison.RoundedUnits(
                    deviations[side]
                        .Skip(i + 1 - count)
                        .Take(count)
                        .Aggregate(BigInteger.Zero, (a, b) => a + b),
                    count
                );
            }
            var total = averages[0] + averages[1];
            output[i] = total.IsZero ? 0 : Round(100 * averages[0], total);
        }
        return output;
    }

    internal static double[] NativeReference(double[] prices, int period)
    {
        var up = new double[prices.Length];
        var down = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var change = Subtract(prices[i], i == 0 ? 0 : prices[i - 1]);
            up[i] = Math.Max(change, 0);
            down[i] = Math.Max(-change, 0);
        }
        double[] Deviations(double[] values)
        {
            var output = new double[values.Length];
            for (var i = 1; i < values.Length; i++)
            {
                var window = values
                    .Skip(Math.Max(0, i + 1 - period))
                    .Take(Math.Min(i + 1, period))
                    .ToArray();
                var mean = Divide(window.Aggregate(0d, Add), window.Length);
                var squares = window.Select(v => Math.Pow(Subtract(v, mean), 2)).Aggregate(0d, Add);
                output[i] = Math.Sqrt(Divide(squares, window.Length - 1));
            }
            return output;
        }
        double[] Means(double[] values)
        {
            var output = new double[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var window = values
                    .Skip(Math.Max(0, i + 1 - period))
                    .Take(Math.Min(i + 1, period))
                    .ToArray();
                var lanes = new double[Vector<double>.Count];
                var j = 0;
                for (; j + lanes.Length <= window.Length; j += lanes.Length)
                for (var k = 0; k < lanes.Length; k++)
                    lanes[k] = Add(lanes[k], window[j + k]);
                var sum = lanes.Aggregate(0d, Add);
                for (; j < window.Length; j++)
                    sum = Add(sum, window[j]);
                output[i] = Divide(sum, window.Length);
            }
            return output;
        }
        var u = Means(Deviations(up));
        var d = Means(Deviations(down));
        return Enumerable
            .Range(0, prices.Length)
            .Select(i => Add(u[i], d[i]) == 0 ? 0 : Divide(Multiply(100, u[i]), Add(u[i], d[i])))
            .ToArray();
    }
}
