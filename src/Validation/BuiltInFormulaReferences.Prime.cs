using System.Numerics;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PrimeOutputs(IReadOnlyList<OoplesFinance.StockIndicators.Indicators.Bar> bars, int length, bool bands)
    {
        if (!bands) return Outputs(("Pno", PrimeOffsetsReference(bars.Select(b => b.Close).ToArray(), length)));
        var high = PrimeOffsetsReference(bars.Select(b => b.High).ToArray(), length);
        var low = PrimeOffsetsReference(bars.Select(b => b.Low).ToArray(), length);
        return Outputs(("UpperBand", high.Select((_, i) => Window(high, i, Math.Max(2, length)).Max()).ToArray()),
            ("LowerBand", low.Select((_, i) => Window(low, i, Math.Max(2, length)).Min()).ToArray()));
    }
    internal static bool PrimeDefinition(long number)
    {
        if (number < 2) return false;
        if (number < 1000000)
        {
            for (long divisor = 2; divisor * divisor <= number; divisor++) if (number % divisor == 0) return false;
            return true;
        }
        // The first twelve prime witnesses are deterministic below 318665857834031151167461,
        // independently of production's seven nonconsecutive 64-bit witnesses.
        var exponent = number - 1; var twoPower = 0;
        while (exponent % 2 == 0) { exponent /= 2; twoPower++; }
        foreach (var basis in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
        {
            var power = BigInteger.ModPow(basis, exponent, number);
            var accepted = power == 1;
            for (var step = 0; step < twoPower; step++)
            {
                accepted |= power == number - 1;
                power = BigInteger.Remainder(BigInteger.Pow(power, 2), number);
            }
            if (!accepted) return false;
        }
        return true;
    }
    internal static double[] PrimeOffsetsReference(double[] prices, int tolerance)
    {
        var result = new double[prices.Length]; long top = 0, bottom = 0;
        for (var i = 0; i < prices.Length; i++)
        {
            var value = prices[i];
            if (double.IsNaN(value) || value < -9223372036854775808d || value > 9223372036854774784d) throw new ArgumentOutOfRangeException(nameof(prices));
            var center = new BigInteger(Math.Round(value)); var delta = value * Math.Max(1, tolerance) / 100;
            var upper = BigInteger.Min(long.MaxValue, new BigInteger(Math.Round(value + delta)));
            var lower = BigInteger.Max(2, new BigInteger(Math.Round(value - delta)));
            for (var candidate = BigInteger.Max(2, center); candidate <= upper; candidate++)
                if (PrimeDefinition((long)candidate)) { top = (long)candidate; break; }
            for (var candidate = center; candidate >= lower; candidate--)
                if (PrimeDefinition((long)candidate)) { bottom = (long)candidate; break; }
            var price = ReferenceFraction.FromDouble(value);
            var above = new ReferenceFraction(top) - price; var below = new ReferenceFraction(bottom) - price;
            var offset = (above + below).Sign < 0 ? above : below;
            result[i] = offset.Sign == 0 ? i == 0 ? 0 : result[i - 1] : offset.ToDouble();
        }
        return result;
    }
}
