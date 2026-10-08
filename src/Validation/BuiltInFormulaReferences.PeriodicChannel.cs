using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> PeriodicOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return PeriodicValues(bars, Integer(options, "Length1", 500), Integer(options, "Length2", 2)).Outputs;
    }

    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, int[] Directions) PeriodicValues(
        IReadOnlyList<Bar> bars, int period, int lookback, double[]? selected = null)
    {
        period = Math.Max(1, period); lookback = Math.Max(1, lookback);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var zero = R(0); var prices = (selected ?? Closes(bars)).Select(R).ToArray();
        var groups = new List<(ReferenceFraction Square, ReferenceFraction Weight)>();
        var directions = new int[prices.Length]; var signals = new Signal[prices.Length];
        var outputs = new[] { "K", "Os", "Ap", "Bp", "Cp", "Al", "Bl", "Cl" }.ToDictionary(k => k, _ => new double[prices.Length]);
        var totalPrice = zero; var totalSine = zero; var priceDeviation = zero; var sineDeviation = zero; var previous = zero;
        BigInteger errorNumerator = 0, errorDenominator = 1;
        for (var i = 0; i < prices.Length; i++)
        {
            var start = Math.Max(0, i - lookback + 1); var n = i - start + 1;
            var mean = zero;
            for (var j = start; j <= i; j++) mean += prices[j];
            mean /= R(n);
            var xx = zero; var yy = zero; var xy = zero;
            for (var j = start; j <= i; j++)
            {
                var x = R(j - start) - R(n - 1) / R(2); var y = prices[j] - mean;
                xx += x * x; yy += y * y; xy += x * y;
            }
            if (xy.Sign != 0)
            {
                var square = xy * xy / (xx * yy); var found = false;
                for (var g = 0; g < groups.Count; g++)
                {
                    var ratio = (square / groups[g].Square).Components;
                    var a = PeriodicRoot(ratio.Numerator); var b = PeriodicRoot(ratio.Denominator);
                    if (a * a != ratio.Numerator || b * b != ratio.Denominator) continue;
                    var weight = groups[g].Weight + new ReferenceFraction(xy.Sign * a) / new ReferenceFraction(b);
                    if (weight.Sign == 0) groups.RemoveAt(g); else groups[g] = (groups[g].Square, weight);
                    found = true; break;
                }
                if (!found) groups.Add((square, R(xy.Sign)));
            }
            directions[i] = PeriodicRadicalDirection(groups);
            var sine = R(Math.Sin((double)i * directions[i] / period));
            totalPrice += prices[i]; totalSine += sine;
            var priceMean = i == 0 ? zero : totalPrice / R(i);
            var sineResidual = i == 0 ? zero : sine - totalSine / R(i);
            sineDeviation += sineResidual.Abs(); priceDeviation += (prices[i] - priceMean).Abs();
            var sineCoordinate = sineDeviation.Sign == 0 ? zero : sineResidual * R(i) / sineDeviation;
            var line = i == 0 ? zero : priceMean + (R(i > 1 ? 2 : 0) + sineCoordinate) * priceDeviation / R(i);
            var margin = prices[i] - line;
            var addition = margin.Abs().Components;
            var errorCommon = errorDenominator / BigInteger.GreatestCommonDivisor(errorDenominator, addition.Denominator) * addition.Denominator;
            errorNumerator = errorNumerator * (errorCommon / errorDenominator) + addition.Numerator * (errorCommon / addition.Denominator);
            // Both operands were reduced: any factor left in the LCM numerator
            // must divide the incoming denominator. The accumulated denominator
            // can be much larger, but does not need a second full-size gcd.
            var errorReduction = BigInteger.GreatestCommonDivisor(errorNumerator, addition.Denominator);
            errorNumerator /= errorReduction; errorDenominator = errorCommon / errorReduction;
            var center = line.Components;
            var widthNumerator = i == 0 ? BigInteger.Zero : errorNumerator;
            var widthDenominator = i == 0 ? BigInteger.One : errorDenominator * i;
            foreach (var (key, multiple) in new[] { ("K", 0), ("Ap", 1), ("Bp", 2), ("Cp", 3), ("Al", -1), ("Bl", -2), ("Cl", -3) })
                outputs[key][i] = PeriodicPublish(center.Numerator * widthDenominator + multiple * widthNumerator * center.Denominator,
                    center.Denominator * widthDenominator);
            outputs["Os"][i] = PeriodicPublish(widthNumerator, widthDenominator);
            signals[i] = margin.Sign > 0 && margin.CompareTo(previous) > 0 ? Signal.StrongBuy
                : margin.Sign < 0 && margin.CompareTo(previous) < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            previous = margin;
        }
        return (outputs, signals, directions);
    }

    // Publication does not require reducing these large band fractions. Quantize
    // their exact quotient on the binary64 grid, independently of production's accumulator.
    private static double PeriodicPublish(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.IsZero) return 0;
        var sign = numerator.Sign; numerator = BigInteger.Abs(numerator);
        int Bits(BigInteger value)
        {
            var bytes = value.ToByteArray(); var last = bytes.Length - 1;
            while (last > 0 && bytes[last] == 0) last--;
            var count = last * 8; for (var head = bytes[last]; head != 0; head >>= 1) count++;
            return count;
        }
        var exponent = Bits(numerator) - Bits(denominator);
        if (exponent >= 0 ? numerator < (denominator << exponent) : (numerator << -exponent) < denominator) exponent--;
        if (exponent > 1023) return sign * double.PositiveInfinity;
        var grid = Math.Max(-1074, exponent - 52);
        if (grid < 0) numerator <<= -grid; else denominator <<= grid;
        var integer = BigInteger.DivRem(numerator, denominator, out var remainder);
        var comparison = (2 * remainder).CompareTo(denominator);
        if (comparison > 0 || comparison == 0 && !integer.IsEven) integer++;
        var unit = grid >= -1022 ? BitConverter.Int64BitsToDouble((long)(grid + 1023) << 52)
            : BitConverter.Int64BitsToDouble(1L << (grid + 1074));
        return sign * (double)integer * unit;
    }

    private static int PeriodicRadicalDirection(List<(ReferenceFraction Square, ReferenceFraction Weight)> groups)
    {
        if (groups.Count == 0) return 0;
        // The oracle uses square roots of rational squares, not production kernels.
        for (var digits = 32; ; digits = checked(digits * 2))
        {
            BigInteger lower = 0, upper = 0;
            foreach (var (square, weight) in groups)
            {
                var value = (square * weight * weight).Components;
                var scaled = value.Numerator << checked(2 * digits);
                var floor = PeriodicRoot(scaled / value.Denominator);
                var ceiling = floor * floor * value.Denominator == scaled ? floor : floor + 1;
                lower += weight.Sign > 0 ? floor : -ceiling;
                upper += weight.Sign > 0 ? ceiling : -floor;
            }
            if (lower.Sign > 0) return 1;
            if (upper.Sign < 0) return -1;
        }
    }

    private static BigInteger PeriodicRoot(BigInteger value)
    {
        if (value <= 1) return value;
        var estimate = BigInteger.One << (value.ToByteArray().Length * 4 + 1);
        var next = (estimate + value / estimate) / 2;
        while (next < estimate) { estimate = next; next = (estimate + value / estimate) / 2; }
        return estimate;
    }
}
