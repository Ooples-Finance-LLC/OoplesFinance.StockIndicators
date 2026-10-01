using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? KaufmanRegressionFormula(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName is not (IndicatorName.KaufmanAdaptiveCorrelationOscillator
            or IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage)) return null;
        var length = Integer(indicator.CreateOptions(), "Length");
        var regression = indicator.BatchName == IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage;
        return new(regression ? "Kalsma" : "Kaco", regression ? new[] { "Kalsma" }
            : new[] { "IndexSt", "SrcSt", "Kaco" }, bars =>
        {
            var values = KaufmanRegressionOutputs(bars, length, regression);
            return regression ? Outputs(("Kalsma", values["Kalsma"]))
                : Outputs(("IndexSt", values["IndexSt"]), ("SrcSt", values["SrcSt"]), ("Kaco", values["Kaco"]));
        });
    }

    internal static Dictionary<string, double[]> KaufmanRegressionOutputs(IReadOnlyList<Bar> bars, int length, bool regression)
    {
        // Constant prices have zero centered price variance and covariance for
        // every positive weight measure. Their fitted value is exactly the price;
        // no time moments are needed for the regression's single published output.
        var lastWarmup = Math.Min(bars.Count - 1, Math.Max(1, length) - 1);
        if (regression && (bars.Count == 0 || bars.Skip(lastWarmup).All(b => b.Close == bars[lastWarmup].Close)))
            return new() { { "Kalsma", bars.Select(b => b.Close).ToArray() } };
        return KaufmanRegressionValues(bars, length, regression);
    }

    internal static Dictionary<string, double[]> KaufmanRegressionValues(IReadOnlyList<Bar> bars, int length, bool fitOnly = false)
    {
        length = Math.Max(1, length);
        // Independent IEEE decoding, using a common grid only for observations.
        BigInteger Units(double value)
        {
            var bits = BitConverter.DoubleToInt64Bits(value); var exponent = (int)((bits >> 52) & 2047);
            if (exponent == 2047) throw new ArgumentOutOfRangeException(nameof(value));
            var significand = new BigInteger(bits & ((1L << 52) - 1));
            if (exponent != 0) significand = (significand + (BigInteger.One << 52)) << (exponent - 1);
            return bits < 0 ? -significand : significand;
        }
        ReferenceFraction Ratio(BigInteger numerator, BigInteger denominator) => new ReferenceFraction(numerator) / new ReferenceFraction(denominator);
        var prices = bars.Select(b => Units(b.Close)).ToArray(); var unit = BigInteger.One << 1074;
        var denominator = BigInteger.One; BigInteger timeSum = 0, priceSum = 0, timeSquare = 0, priceSquare = 0, cross = 0;
        var timeDeviation = new double[bars.Count]; var priceDeviation = new double[bars.Count];
        var correlation = new double[bars.Count]; var fitted = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var time = new BigInteger(i); var price = prices[i];
            if (i < length)
            {
                denominator = BigInteger.One; timeSum = time; priceSum = price; timeSquare = time * time;
                priceSquare = fitOnly ? BigInteger.Zero : price * price; cross = time * price; fitted[i] = bars[i].Close; continue;
            }
            var travel = BigInteger.Zero;
            for (var j = i - length + 1; j <= i; j++) travel += BigInteger.Abs(prices[j] - prices[j - 1]);
            var efficiency = travel.IsZero ? 0 : Ratio(BigInteger.Abs(price - prices[i - length]), travel).ToDouble();
            var gain = Math.Pow(2d / 31 + efficiency * (2d / 3 - 2d / 31), 2);
            var bits = BitConverter.DoubleToInt64Bits(gain); var power = 1075 - (int)((bits >> 52) & 2047);
            var weight = new BigInteger((bits & ((1L << 52) - 1)) | (1L << 52));
            while (weight.IsEven) { weight >>= 1; power--; }
            var retention = (BigInteger.One << power) - weight; var newWeight = weight * denominator;
            // Raw sums of absolute time/price powers share one dyadic denominator.
            // No rounding or rational reduction occurs in these updates.
            timeSum = retention * timeSum + newWeight * time;
            priceSum = retention * priceSum + newWeight * price;
            timeSquare = retention * timeSquare + newWeight * time * time;
            if (!fitOnly) priceSquare = retention * priceSquare + newWeight * price * price;
            cross = retention * cross + newWeight * time * price;
            denominator <<= power;
            var timeVariance = timeSquare * denominator - timeSum * timeSum;
            var covariance = cross * denominator - timeSum * priceSum;
            fitted[i] = timeVariance.IsZero ? Ratio(priceSum, denominator * unit).ToDouble()
                : Ratio(priceSum * timeVariance + (time * denominator - timeSum) * covariance, denominator * timeVariance * unit).ToDouble();
            if (fitOnly) continue;
            var priceVariance = priceSquare * denominator - priceSum * priceSum;
            var totalSquare = denominator * denominator;
            timeDeviation[i] = KaufmanReferenceRoot(Ratio(timeVariance, totalSquare));
            priceDeviation[i] = KaufmanReferenceRoot(Ratio(priceVariance, totalSquare * unit * unit));
            correlation[i] = timeVariance.IsZero || priceVariance.IsZero ? 0
                : covariance.Sign * KaufmanReferenceRoot(Ratio(covariance * covariance, timeVariance * priceVariance));
        }
        return fitOnly ? new() { { "Kalsma", fitted } }
            : new() { { "IndexSt", timeDeviation }, { "SrcSt", priceDeviation }, { "Kaco", correlation }, { "Kalsma", fitted } };
    }

    // A platform root is only a search seed. Exact rational comparisons prove
    // both adjacent endpoints and their rounding midpoint, including subnormals
    // and overflow. Fall back to the independent full search if the seed is poor.
    internal static double KaufmanReferenceRoot(ReferenceFraction value)
    {
        if (value.Sign == 0) return 0;
        if (value.Sign < 0) throw new ArgumentOutOfRangeException(nameof(value));
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var scale = new ReferenceFraction(BigInteger.One << 512); var small = one / scale;
        var normalized = value; var exponent = 0;
        while (normalized.CompareTo(scale) >= 0) { normalized /= scale; exponent += 256; }
        while (normalized.CompareTo(small) < 0) { normalized *= scale; exponent -= 256; }
        var estimate = ReferenceFraction.FromDouble(Math.Sqrt(normalized.ToDouble()));
        estimate = exponent >= 0 ? estimate * new ReferenceFraction(BigInteger.One << exponent)
            : estimate / new ReferenceFraction(BigInteger.One << -exponent);
        const long maximum = 0x7fefffffffffffff;
        var bits = Math.Min(maximum, BitConverter.DoubleToInt64Bits(estimate.ToDouble())); var moves = 0;
        ReferenceFraction At(long position) => ReferenceFraction.FromDouble(BitConverter.Int64BitsToDouble(position));
        var lower = At(bits);
        while ((lower * lower).CompareTo(value) > 0)
        { if (++moves > 8) return value.SqrtToDouble(); lower = At(--bits); }
        while (bits < maximum)
        {
            var next = At(bits + 1); if ((next * next).CompareTo(value) > 0) break;
            if (++moves > 8) return value.SqrtToDouble(); bits++; lower = next;
        }
        var upper = bits == maximum ? new ReferenceFraction(BigInteger.One << 1024) : At(bits + 1);
        var midpoint = (lower + upper) / two; var comparison = value.CompareTo(midpoint * midpoint);
        return BitConverter.Int64BitsToDouble(bits + (comparison > 0 || comparison == 0 && (bits & 1) != 0 ? 1 : 0));
    }
}
