using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class NullableStrengthReference
{
    internal static double?[] Calculate(
        double?[] input,
        int period,
        NullableStrengthConvention convention,
        int lag
    )
    {
        var result = new double?[input.Length];
        var first = (long)period + lag - 1;
        if (first >= input.Length)
            return result;
        var changes = new ReferenceFraction?[input.Length];
        for (var i = lag; i < input.Length; i++)
            if (input[i].HasValue && input[i - lag].HasValue)
                changes[i] =
                    ReferenceFraction.FromDouble(input[i]!.Value)
                    - ReferenceFraction.FromDouble(input[i - lag]!.Value);
        var seed = changes
            .Skip(lag)
            .Take(period)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();
        if (seed.Length == 0)
            return result;
        var gain = StrengthReference.Quantize(
            seed.Where(v => v.Sign > 0).Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                / new ReferenceFraction(seed.Length)
        );
        var loss = StrengthReference.Quantize(
            seed.Where(v => v.Sign < 0).Aggregate(new ReferenceFraction(0), (a, b) => a - b)
                / new ReferenceFraction(seed.Length)
        );
        var momentum = NullableStrengthOscillator.IsMomentum(convention);
        for (var i = (int)first; i < input.Length; i++)
        {
            if (i > first)
            {
                if (!changes[i].HasValue)
                    break;
                var change = changes[i]!.Value;
                var up = change.Sign > 0 ? change : new ReferenceFraction(0);
                var down = change.Sign < 0 ? change.Abs() : new ReferenceFraction(0);
                var alpha =
                    new ReferenceFraction(momentum ? 2 : 1)
                    / new ReferenceFraction(momentum ? (long)period + 1 : period);
                gain = StrengthReference.Quantize(
                    gain * (new ReferenceFraction(1) - alpha) + up * alpha
                );
                loss = StrengthReference.Quantize(
                    loss * (new ReferenceFraction(1) - alpha) + down * alpha
                );
            }
            if (
                loss.Sign == 0
                || convention == NullableStrengthConvention.RelativeMomentumIndex && gain.Sign == 0
            )
                continue;
            result[i] = convention switch
            {
                NullableStrengthConvention.RelativeStrength
                or NullableStrengthConvention.RelativeMomentum => (gain / loss).ToDouble(),
                NullableStrengthConvention.NetMomentum => (
                    new ReferenceFraction(100) * (gain - loss) / (gain + loss)
                ).ToDouble(),
                _ => (new ReferenceFraction(100) * gain / (gain + loss)).ToDouble(),
            };
        }
        return result;
    }
}
