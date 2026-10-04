using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ConfluenceRootRelationTests
{
    private static readonly Dictionary<int, BigInteger[]> Cyclotomics = new();
    private static BigInteger[] Divide(BigInteger[] numerator, BigInteger[] denominator, out BigInteger[] remainder)
    {
        remainder = (BigInteger[])numerator.Clone(); var quotient = new BigInteger[Math.Max(1, numerator.Length - denominator.Length + 1)];
        for (var i = numerator.Length - denominator.Length; i >= 0; i--)
        {
            quotient[i] = remainder[i + denominator.Length - 1] / denominator[^1];
            for (var j = 0; j < denominator.Length; j++) remainder[i + j] -= quotient[i] * denominator[j];
        }
        return quotient;
    }
    private static BigInteger[] Cyclotomic(int n)
    {
        if (Cyclotomics.TryGetValue(n, out var known)) return known;
        var polynomial = new BigInteger[n + 1]; polynomial[0] = -1; polynomial[n] = 1;
        for (var d = 1; d < n; d++)
            if (n % d == 0)
            {
                polynomial = Divide(polynomial, Cyclotomic(d), out var remainder);
                Assert.All(remainder, v => Assert.True(v.IsZero));
            }
        Cyclotomics[n] = polynomial; return polynomial;
    }
    private static bool DenseZero(int n, int[] sine, int[] cosine)
    {
        var order = 4 * n; var polynomial = new BigInteger[order];
        int Mod(int i) => (i % order + order) % order;
        for (var k = 0; k < n; k++)
        {
            polynomial[Mod(4 * k)] += cosine[k]; polynomial[Mod(-4 * k)] += cosine[k];
            polynomial[Mod(4 * k + 3 * n)] += sine[k]; polynomial[Mod(-4 * k + 3 * n)] -= sine[k];
        }
        _ = Divide(polynomial, Cyclotomic(order), out var remainder);
        return remainder.All(v => v.IsZero);
    }
    [Fact]
    public void SparseTowerMatchesIndependentDensePolynomialDivision()
    {
        var random = new Random(1001);
        for (var n = 3; n <= 30; n++)
        for (var trial = 0; trial < 12; trial++)
        {
            var sin = new int[n]; var cos = new int[n];
            if (trial < 2)
            {
                var relation = Cyclotomic(n);
                for (var k = 0; k < relation.Length; k++) (trial == 0 ? sin : cos)[k] = (int)relation[k];
            }
            else for (var k = 0; k < n; k++) { sin[k] = random.Next(-2, 3); cos[k] = random.Next(-2, 3); }
            var terms = Enumerable.Range(0, n).Select(k => (new F(360 * k, n), (F)sin[k], (F)cos[k]));
            Assert.Equal(DenseZero(n, sin, cos), ConfluenceRootRelations.IsZero(0, terms, ConfluenceRootRelations.PrimeFactors(new long[] { n })));
        }
    }
    [Fact]
    public void AstronomicDyadicOrdersRetainExactCancellationAndNonzeroPerturbations()
    {
        var factors = ConfluenceRootRelations.PrimeFactors(new long[] { 1 });
        foreach (var angle in new[] { F.Of(double.Epsilon), F.Of(double.MaxValue), new F(1, BigInteger.One << 4096) })
        {
            Assert.True(ConfluenceRootRelations.IsZero(0, new[] { (angle, (F)1, (F)0), (angle + 180, (F)1, (F)0) }, factors));
            Assert.True(ConfluenceRootRelations.IsZero(0, new[] { (angle, (F)0, (F)1), (angle + 180, (F)0, (F)1) }, factors));
            Assert.False(ConfluenceRootRelations.IsZero(F.Of(double.Epsilon), new[] { (angle, (F)1, (F)0), (angle + 180, (F)1, (F)0) }, factors));
        }
        Assert.False(ConfluenceRootRelations.IsZero(0, new[] { (F.Of(double.Epsilon), (F)1, (F)0) }, factors));
    }
    [Fact]
    public void FifthAndSeventhRootRelationsDoNotBecomeVotes()
    {
        foreach (var period in new[] { 5, 7, 11, 13 })
        {
            var terms = Enumerable.Range(1, period - 1).Select(k => (new F(360 * k, period), (F)0, (F)1)).ToArray();
            var factors = ConfluenceRootRelations.PrimeFactors(new long[] { period });
            Assert.True(ConfluenceRootRelations.IsZero(1, terms, factors));
            Assert.False(ConfluenceRootRelations.IsZero(2, terms, factors));
        }
    }
}
