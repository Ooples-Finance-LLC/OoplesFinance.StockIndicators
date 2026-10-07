using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class ClassicAverageReference
{
    internal static double?[] Values(IReadOnlyList<Bar> bars, ClassicMovingAverage owner) =>
        ExtendedValues(bars, owner).Select(v => v?.ToDouble()).ToArray();

    internal static ReferenceFraction?[] ExtendedValues(
        IReadOnlyList<Bar> bars,
        ClassicMovingAverage owner
    ) => ExtendedValues(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), owner);

    internal static ReferenceFraction?[] ExtendedValues(
        IReadOnlyList<ReferenceFraction> prices,
        ClassicMovingAverage owner
    )
    {
        if (owner.Period == 1)
            return prices.Select(v => (ReferenceFraction?)v).ToArray();
        if (owner.Method == ClassicAverageMethod.Kama)
            return SeededAdaptiveReference.ExtendedValues(prices, owner.Adaptive())[0];
        if (owner.Method == ClassicAverageMethod.Mama)
            return SeededPhaseReference.DelayedExtended(prices, .5, .05, owner.Suppression)[0];
        if (owner.Method == ClassicAverageMethod.T3)
            return TillsonReference.CalculateExtended(
                prices,
                owner.Period,
                .7,
                TillsonSeed.CascadedMeans,
                owner.Suppression
            );
        if (
            owner.Method
            is ClassicAverageMethod.Ema
                or ClassicAverageMethod.Dema
                or ClassicAverageMethod.Tema
        )
            return Exponential(prices, owner);
        var result = new ReferenceFraction?[prices.Count];
        for (var i = owner.Period - 1; i < prices.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            long mass = 0;
            for (var j = 0; j < owner.Period; j++)
            {
                var weight =
                    owner.Method == ClassicAverageMethod.Wma ? j + 1
                    : owner.Method == ClassicAverageMethod.Trima ? Math.Min(j + 1, owner.Period - j)
                    : 1;
                sum += prices[i - owner.Period + 1 + j] * new ReferenceFraction(weight);
                mass += weight;
            }
            result[i] = (sum / new ReferenceFraction(mass)).RoundExtendedBinary64();
        }
        return result;
    }

    internal static ReferenceFraction?[] AlignedExtendedValues(
        IReadOnlyList<Bar> bars,
        ClassicMovingAverage owner,
        long first
    ) =>
        AlignedExtendedValues(
            bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(),
            owner,
            first
        );

    internal static ReferenceFraction?[] AlignedExtendedValues(
        IReadOnlyList<ReferenceFraction> prices,
        ClassicMovingAverage owner,
        long first
    )
    {
        var offset = first - owner.First;
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(first));
        if (first >= prices.Count)
            return new ReferenceFraction?[prices.Count];
        if (
            owner.Period != 1
            && owner.Method
                is ClassicAverageMethod.Ema
                    or ClassicAverageMethod.Dema
                    or ClassicAverageMethod.Tema
        )
            return Exponential(prices, owner, offset);
        return Enumerable
            .Repeat<ReferenceFraction?>(null, (int)offset)
            .Concat(ExtendedValues(prices.Skip((int)offset).ToArray(), owner))
            .ToArray();
    }

    private static ReferenceFraction?[] Exponential(
        IReadOnlyList<ReferenceFraction> prices,
        ClassicMovingAverage owner,
        long offset = 0
    )
    {
        var result = new ReferenceFraction?[prices.Count];
        var layers = Enumerable
            .Range(0, owner.Order)
            .Select(_ => new ReferenceFraction?[prices.Count])
            .ToArray();
        var delay = owner.Period - 1L + owner.Suppression;
        for (var j = 0; j < owner.Order; j++)
        {
            var start = owner.FirstPriceSeed && j == 0 ? 0 : offset + j * delay;
            if (start >= prices.Count)
                continue;
            ReferenceFraction Input(int i) => j == 0 ? prices[i] : layers[j - 1][i]!.Value;
            var value = new ReferenceFraction(0);
            for (var i = (int)start; i < prices.Count; i++)
            {
                if (owner.FirstPriceSeed && i == start)
                    value = Input(i);
                else if (!owner.FirstPriceSeed && i < start + owner.Period)
                {
                    value += Input(i);
                    if (i == start + owner.Period - 1)
                        value = (
                            value / new ReferenceFraction(owner.Period)
                        ).RoundExtendedBinary64();
                }
                else
                    value = (
                        (
                            new ReferenceFraction(2) * Input(i)
                            + new ReferenceFraction(owner.Period - 1) * value
                        ) / new ReferenceFraction(owner.Period + 1L)
                    ).RoundExtendedBinary64();
                if (i >= start + delay)
                    layers[j][i] = value;
            }
        }
        for (var i = (int)Math.Min(prices.Count, owner.First + offset); i < prices.Count; i++)
        {
            var first = layers[0][i]!.Value;
            result[i] = (
                owner.Order == 1 ? first
                : owner.Order == 2 ? new ReferenceFraction(2) * first - layers[1][i]!.Value
                : new ReferenceFraction(3) * (first - layers[1][i]!.Value) + layers[2][i]!.Value
            ).RoundExtendedBinary64();
        }
        return result;
    }
}
