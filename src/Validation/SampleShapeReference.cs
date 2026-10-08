using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class SampleShapeReference
{
    internal static double?[] Calculate(IReadOnlyList<Bar> bars, int period, bool kurtosis)
    {
        var output = new double?[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(i + 1, period);
            if (count < (kurtosis ? 4 : 3))
            {
                output[i] = 0;
                continue;
            }
            var values = bars.Skip(i - count + 1)
                .Take(count)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var n = new ReferenceFraction(count);
            var mean = values.Aggregate(new ReferenceFraction(0), (s, x) => s + x) / n;
            var s2 = new ReferenceFraction(0);
            var high = s2;
            foreach (var value in values)
            {
                var d = value - mean;
                s2 += d * d;
                high += kurtosis ? d * d * d * d : d * d * d;
            }
            if (s2.Sign == 0)
            {
                output[i] = kurtosis ? null : 0;
                continue;
            }
            var one = new ReferenceFraction(1);
            var two = new ReferenceFraction(2);
            var three = new ReferenceFraction(3);
            if (kurtosis)
            {
                var variance = s2 / (n - one);
                output[i] = (
                    n
                        * (n + one)
                        * high
                        / (variance * variance * (n - three) * (n - one) * (n - two))
                    - three * (n - one) * (n - one) / ((n - two) * (n - three))
                ).ToDouble();
            }
            else
                output[i] =
                    high.Sign
                    * (
                        n * n * (n - one) * high * high / ((n - two) * (n - two) * s2 * s2 * s2)
                    ).SqrtToDouble();
        }
        return output;
    }
}
