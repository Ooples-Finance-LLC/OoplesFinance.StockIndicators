using System.Numerics;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    // Validation-only coordinate arithmetic shared by the NVI and OBV references.
    private static class DisparityReference
    {
        private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        private static readonly ReferenceFraction zero = R(0), one = R(1), factor = new(BigInteger.One << 512);
        internal static ReferenceFraction Compact(ReferenceFraction value) => CompactReferenceFraction(value);
        internal static ReferenceFraction Root(ReferenceFraction square)
        {
            var scale = one; var rootFactor = new ReferenceFraction(BigInteger.One << 256); var magnitude = square.ToDouble();
            while (double.IsInfinity(magnitude) || magnitude >= Math.Pow(2, 512)) { square /= factor; scale *= rootFactor; magnitude = square.ToDouble(); }
            while (magnitude < Math.Pow(2, -256)) { square *= factor; scale /= rootFactor; magnitude = square.ToDouble(); }
            var root = R(square.SqrtToDouble());
            // Two exact Newton corrections from the binary64 seed provide
            // over 200 bits, independently of production's 106-bit root ratio.
            for (var step = 0; step < 2; step++) root = (root + square / root) / R(2);
            // Store four binary64 residuals after refinement; window moments and
            // the cancellation discriminant remain exact.
            return Compact(root * scale);
        }
        internal static ReferenceFraction[] Mean(ReferenceFraction[] values, int period, int kind)
        {
            if (kind is not (1 or 2 or 3 or 6)) return Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
            var result = new ReferenceFraction[values.Length]; var previous = zero;
            for (var i = 0; i < values.Length; i++)
            {
                if (period == 1) result[i] = values[i];
                else if (kind == 6) result[i] = Compact((previous * R(period - 1) + values[i]) / R(period));
                else if (kind == 3 && i >= period) result[i] = Compact((previous * R(period - 1) + R(2) * values[i]) / new ReferenceFraction(period + 1L));
                else if (kind == 1 && i + 1 < period) result[i] = zero;
                else
                {
                    var total = zero;
                    for (var j = Math.Max(0, i - period + 1); j <= i; j++) total += values[j] * R(kind == 2 ? period - i + j : 1);
                    result[i] = total / new ReferenceFraction(kind == 2 ? (long)period * (period + 1L) / 2 : Math.Min(i + 1, period));
                }
                previous = result[i];
            }
            return result;
        }
        internal static ReferenceFraction[] Coordinate(ReferenceFraction[] values, ReferenceFraction[] means, int length)
        {
            var result = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                if (i + 1 < length) { result[i] = one; continue; }
                var sample = values.Skip(i - length + 1).Take(length).ToArray(); var mean = sample.Aggregate(zero, (sum, value) => sum + value) / R(length);
                var variance = sample.Aggregate(zero, (sum, value) => sum + (value - mean) * (value - mean)) / R(length);
                if (variance.Sign == 0) { result[i] = one; continue; }
                var delta = values[i] - means[i];
                if (delta.Sign == 0) { result[i] = R(1.5); continue; }
                var squaredZ = delta * delta / variance; var absoluteZ = Root(squaredZ);
                result[i] = delta.Sign > 0 ? R(1.5) + absoluteZ / R(4)
                    : (R(36) - squaredZ) / (R(4) * (R(6) + absoluteZ));
            }
            return result;
        }
    }
}
