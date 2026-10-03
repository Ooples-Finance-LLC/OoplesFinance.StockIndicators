using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget UltimateBudget = new(0, 5e-16, requireSameSign: true);
    internal static IReadOnlyDictionary<string, double[]> UltimateOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var bands = indicator.BatchName == IndicatorName.UltimateMovingAverageBands;
        return UltimateValues(bars, bands ? Integer(options, "MinLength", 5) : 5,
            bands ? Integer(options, "MaxLength", 50) : 50,
            (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!,
            bands ? Number(options, 2, "StdDevMult") : 0, bands);
    }

    internal static IReadOnlyDictionary<string, double[]> UltimateValues(IReadOnlyList<Bar> bars, int minimum, int maximum,
        MovingAvgType kind, double multiplier, bool bands, double[]? customMean = null)
    {
        minimum = Math.Max(1, minimum); maximum = Math.Max(minimum, maximum);
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        var zero = R(0); var one = R(1); var two = R(2);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var typical = bars.Select(b => (R(b.High) + R(b.Low) + R(b.Close)) / R(3)).ToArray();
        var positive = bars.Select((b, i) => i > 0 && typical[i].CompareTo(typical[i - 1]) > 0 ? typical[i] * R(b.Volume) : zero).ToArray();
        var negative = bars.Select((b, i) => i > 0 && typical[i].CompareTo(typical[i - 1]) < 0 ? typical[i] * R(b.Volume) : zero).ToArray();
        var lengths = VariableLengthValues(bars, minimum, maximum, kind, customMean).Outputs["Length"];
        var middle = new double[bars.Count]; var upper = new double[bars.Count]; var lower = new double[bars.Count];
        var relative = one / new ReferenceFraction(BigInteger.One << 80);

        bool Publish(ReferenceFraction low, ReferenceFraction high, out double value)
        {
#pragma warning disable S1244 // Exact equality certifies that both interval endpoints round to the same binary64 value; an epsilon cannot certify this.
            value = low.ToDouble(); if (value == high.ToDouble()) return true;
#pragma warning restore S1244
            var center = (low + high) / two;
            if (low.Sign != high.Sign || (high - low).CompareTo(center.Abs() * relative) > 0) return false;
            value = center.ToDouble(); return true;
        }

        for (var i = 0; i < bars.Count; i++)
        {
            var period = (int)lengths[i]; var start = Math.Max(0, i - period + 1);
            var up = zero; var down = zero;
            for (var j = start; j <= i; j++) { up += positive[j]; down += negative[j]; }
            var balance = down.Sign == 0 ? one : up.Sign == 0 || (up + down).Sign == 0 ? R(-1)
                : (up - down) / (up + down);
            if (balance.CompareTo(one) > 0) balance = one; else if (balance.CompareTo(R(-1)) < 0) balance = R(-1);
            var power = one + R(4) * balance.Abs();
            var variance = zero;
            if (bands && i + 1 >= minimum)
            {
                var average = zero; for (var j = i - minimum + 1; j <= i; j++) average += prices[j]; average /= R(minimum);
                for (var j = i - minimum + 1; j <= i; j++) { var residual = prices[j] - average; variance += residual * residual; }
                variance /= R(minimum);
            }
            var square = variance * R(multiplier) * R(multiplier);
            var constant = i + 1 >= period && Enumerable.Range(start, period).All(j => prices[j].CompareTo(prices[i]) == 0);
            // A full constant price window has the exact weighted mean price,
            // independently of its positive weights, and zero band variance.
            if (constant) { middle[i] = upper[i] = lower[i] = prices[i].ToDouble(); continue; }
            for (var bits = 192; ; bits = checked(bits * 2))
            {
                (ReferenceFraction Low, ReferenceFraction High)[]? weights = null;
                (ReferenceFraction Low, ReferenceFraction High) total;
                if (period <= 256 && !power.Components.Denominator.IsOne)
                {
                    weights = new (ReferenceFraction Low, ReferenceFraction High)[period + 1]; total = (zero, zero);
                    for (var k = 1; k <= period; k++)
                    {
                        weights[k] = UltimateReferenceArithmetic.Weight(k, period, power, checked(bits + 64));
                        total = (total.Low + weights[k].Low, total.High + weights[k].High);
                    }
                }
                else total = UltimateReferenceArithmetic.PublicWeightSum(period, power, bits);
                var divisor = total.Low.CompareTo(one) < 0 ? one : total.Low;
                var lowSum = zero; var highSum = zero;
                for (var j = start; j <= i; j++)
                {
                    if (prices[j].Sign == 0) continue;
                    var weight = weights is null ? UltimateReferenceArithmetic.Weight(period - i + j, period, power, bits + 32) : weights[period - i + j];
                    lowSum += prices[j] * (prices[j].Sign > 0 ? weight.Low : weight.High);
                    highSum += prices[j] * (prices[j].Sign > 0 ? weight.High : weight.Low);
                }
                if (lowSum.Sign < 0 && highSum.Sign > 0 && UltimateZeroNumerator(prices, i, period, power)) lowSum = highSum = zero;
                var lowMean = lowSum / (lowSum.Sign >= 0 ? total.High : divisor);
                var highMean = highSum / (highSum.Sign >= 0 ? divisor : total.High);
                if (!Publish(lowMean, highMean, out var mid)) continue;
                if (!bands || square.Sign == 0) { middle[i] = upper[i] = lower[i] = mid; break; }
                var width = UltimateReferenceArithmetic.Root(square, bits);
                var lowWidth = multiplier >= 0 ? width.Low : zero - width.High;
                var highWidth = multiplier >= 0 ? width.High : zero - width.Low;
                if (!Publish(lowMean + lowWidth, highMean + highWidth, out var hi)
                    || !Publish(lowMean - highWidth, highMean - lowWidth, out var lo)) continue;
                middle[i] = mid; upper[i] = hi; lower[i] = lo; break;
            }
        }
        return bands ? Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower)) : Outputs(("Uma", middle));
    }

    private static bool UltimateZeroNumerator(ReferenceFraction[] prices, int last, int period, ReferenceFraction power)
    {
        // Canonical prime exponents identify rationally proportional radicals.
        // This zero check uses factorization, not production's pairwise roots.
        var p = power.Components; var groups = new Dictionary<string, ReferenceFraction>();
        for (var j = Math.Max(0, last - period + 1); j <= last; j++)
        {
            if (prices[j].Sign == 0) continue;
            long index = period - last + j; BigInteger coefficient = 1; var key = new System.Text.StringBuilder();
            void Factor(long prime, int multiplicity)
            {
                var quotient = BigInteger.DivRem(p.Numerator * multiplicity, p.Denominator, out var remainder);
                coefficient *= BigInteger.Pow(prime, (int)quotient);
                if (!remainder.IsZero) key.Append(prime).Append(':').Append(remainder).Append(';');
            }
            for (long prime = 2; prime * prime <= index; prime++)
            {
                var count = 0; while (index % prime == 0) { index /= prime; count++; }
                if (count > 0) Factor(prime, count);
            }
            if (index > 1) Factor(index, 1);
            var name = key.ToString(); if (!groups.TryGetValue(name, out var sum)) sum = new ReferenceFraction(0);
            groups[name] = sum + prices[j] * new ReferenceFraction(coefficient);
        }
        return groups.Values.All(value => value.Sign == 0);
    }
}
