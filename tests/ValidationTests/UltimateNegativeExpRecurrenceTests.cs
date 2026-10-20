using System.Security.Cryptography;
using System.Text;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UltimateNegativeExpRecurrenceTests
{
    // Complete reduced interval endpoints captured from the verified pre-change
    // implementation at 942ddec8, rather than recomputing its Taylor recurrence.
    [Theory]
    [InlineData(-1, 3, 32, "1a05cb3c590606e52a18eebf78105fc8358da5fc37c4e9b818d0caeef353a2df")]
    [InlineData(-1, 3, 64, "bb3cd18cd5e96069c534cc552f55ec3e9c496b2528c2770c7c7e03e3d62abb6c")]
    [InlineData(-1, 3, 128, "28b428d24fbcb669a55662de70737271af496072e34d25c522d5bc81339753a8")]
    [InlineData(-1, 3, 256, "060173104ec802154b0dffff7489fed3001d361ea15db184718a70d182577ae4")]
    [InlineData(-7, 5, 96, "b5e7085ba48a4598080e3e222dcd583d435f4d8f73b6adde7f67b134ba6ef4b6")]
    [InlineData(-17, 2, 128, "7dafd742b11c97205d69849926cf145e3d4e87e7c5d272f2d3d1f2c0b3469df3")]
    [InlineData(-1, 1024, 64, "76710bd9691e1ef2d4c3cb19fd78ce985f788fb030aef162693a84b92b5b3614")]
    [InlineData(-127, 2, 64, "54107d22af037a93d467e3ddd22528a10bae257894509981e0b7277bca8251cb")]
    [InlineData(-128, 1, 128, "ff602104204228a201c104ec2a9a9954c6eeef91b165afd9527235416650fe3f")]
    [InlineData(0, 1, 96, "65e1da196959bd99e593f4e66b6dea0c3b841a2cf33637e9cdebe20706e1d355")]
    public void ReusedTaylorTermsRetainExactIntervalEndpoints(int numerator, int denominator, int bits, string expected)
    {
        var bounds = UltimatePowerWeights.NegativeExp(new(numerator, denominator), bits);
        var canonical = FormattableString.Invariant($"{bounds.Lower.Numerator}/{bounds.Lower.Denominator}:{bounds.Upper.Numerator}/{bounds.Upper.Denominator}");
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        Assert.Equal(expected, actual);
    }
}
