using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// The .NET Framework x87 trigonometric intrinsics cannot reduce large arguments.
// Reduce the exact binary64 input to [-pi/4, pi/4] before calling those intrinsics.
internal static class FrameworkCircularMath
{
    private const int Precision = 2304;
    private static readonly BigInteger Scale = BigInteger.One << Precision;
    // floor((pi/2) * 2^2304), generated with Chudnovsky's series at 800 decimal
    // digits. Even the largest binary64 quotient leaves reduction error < 2^-1280.
    // The validation reference independently generates pi with Machin's identity.
    private static readonly BigInteger HalfPi = BigInteger.Parse(
        "1921fb54442d18469898cc51701b839a252049c1114cf98e804177d4c76273644a29410f31c6809bbdf2a33679a74863"
        + "6605614dbe4be286e9fc26adadaa3848bc90b6aecc4bcfd8de89885d34c6fdad617feb96de80d6fdbdc70d7f6b5133f4"
        + "b5d3e4822f8963fcc9250cca3d9c8b67b8400f97142c77e0b31b4906c38aba734d22c7f51fa499ebf06caba47b9475b2"
        + "c38c5e6ac410aa5773daa520ee12d2cdace186a9c95793009e2e8d811943042f86520bc8c5c6d9c77c73cee58301d0c0"
        + "7364f0745d80f451f6b8abbe0de98a593bc5797ed2ab02e30732a92f9d52ad5ca2ba44c3131f40a202ae51cb51555885"
        + "b5a662e1a08a0f46750aa4357be3974c9d9f70a08b1b7de1515d4e2aeba0c18fb672e1f0b4dc3c98f57eb5d19b61267a"
        + "e", NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    internal static double Value(double value, int operation)
    {
        if (!FrameworkCompatibility.IsFinite(value)) return double.NaN;
        if (Math.Abs(value) <= .5) return Kernel(value, operation);
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        var mantissa = (bits & ((1L << 52) - 1)) | (1L << 52);
        var scaled = new BigInteger(mantissa) << (Precision + exponent - 1023 - 52);
        var quadrant = (scaled + HalfPi / 2) / HalfPi;
        var remainder = scaled - quadrant * HalfPi;
        var top = new ExactMeanAccumulator();
        var bottom = new ExactMeanAccumulator();
        top.Add(1, remainder);
        bottom.Add(1, Scale);
        var angle = top.Ratio(bottom);
        var quarter = (int)(quadrant % 4);
        var result = operation switch
        {
            0 => quarter switch { 0 => Math.Sin(angle), 1 => Math.Cos(angle),
                2 => -Math.Sin(angle), _ => -Math.Cos(angle) },
            1 => quarter switch { 0 => Math.Cos(angle), 1 => -Math.Sin(angle),
                2 => -Math.Cos(angle), _ => Math.Sin(angle) },
            _ => (quarter & 1) == 0 ? Math.Tan(angle) : -1 / Math.Tan(angle)
        };
        return value < 0 && operation != 1 ? -result : result;
    }

    private static double Kernel(double value, int operation) => operation switch
    {
        0 => Math.Sin(value),
        1 => Math.Cos(value),
        _ => Math.Tan(value)
    };
}
