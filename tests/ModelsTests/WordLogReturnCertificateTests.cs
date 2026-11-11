using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class WordLogReturnCertificateTests
{
    [Fact]
    public void OrdinaryAndAdjacentWordCellsMatchIndependentRationalLogarithms()
    {
        foreach (double current in new[] { .9, 1.1, .75, Math.BitIncrement(1), Math.BitDecrement(1) })
        foreach (int exponent in new[] { -900, 0, 900 })
        {
            double next = Math.ScaleB(current, exponent), prior = Math.ScaleB(1, exponent);
            Assert.True(CertifiedLogReturn.TryWord(new(next), new(prior), out var value));
            Bits(Expected(next, prior), value);
            Check(next, prior);
        }
    }

    [Fact]
    public void NormalizationAndFullExponentEdgesRetainIndependentBits()
    {
        double normal = Math.ScaleB(1, -1022);
        double[] values = [double.Epsilon, 3 * double.Epsilon, Math.BitDecrement(normal), normal,
            Math.BitDecrement(.75), .75, Math.BitIncrement(.75), Math.BitDecrement(1), 1,
            Math.BitIncrement(1), Math.BitDecrement(1.5), 1.5, Math.BitIncrement(1.5),
            Math.BitDecrement(double.MaxValue), double.MaxValue];
        foreach (double current in values)
        foreach (double previous in values) Check(current, previous);
        var random = new Random(101110);
        for (int i = 0; i < 128; i++)
        {
            double previous = Math.ScaleB(1 + random.NextDouble(), random.Next(-900, 901));
            Check(previous * (.76 + .73 * random.NextDouble()), previous);
        }
    }

    [Fact]
    public void UnresolvedWordAndNonzeroExponentKeepTheExistingCertificates()
    {
        Assert.False(CertifiedLogReturn.TryWord(new(131.25), new(132.25), out _));
        Assert.True(CertifiedLogReturn.TryShort(new(131.25), new(132.25), out _));
        Assert.False(CertifiedLogReturn.TryWord(new(2d), new(1d), out _));
        Assert.True(CertifiedLogReturn.TryShort(new(2d), new(1d), out _));
        Check(131.25, 132.25);
        Check(2, 1);
    }

    [Fact]
    public void WordIterationsAllocateNoManagedScratch()
    {
        ExactMeanAccumulator.ProductFactor[] current = [new(.9), new(1.1), new(Math.BitIncrement(1))];
        var previous = new ExactMeanAccumulator.ProductFactor(1);
        for (int i = 0; i < 1000; i++) CertifiedLogReturn.TryWord(current[i % current.Length], previous, out _);
        bool success = true;
        double result = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++)
            success &= CertifiedLogReturn.TryWord(current[i % current.Length], previous, out result);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(success);
        Assert.NotEqual(0, BitConverter.DoubleToInt64Bits(result));
    }

    private static double Expected(double current, double previous) =>
        (ReferenceFraction.FromDouble(current) / ReferenceFraction.FromDouble(previous)).LogToDouble();

    private static void Check(double current, double previous)
    {
        double expected = Expected(current, previous);
        if (CertifiedLogReturn.TryWord(new(current), new(previous), out double word)) Bits(expected, word);
        Bits(expected, CertifiedLogReturn.Of(current, previous));
        Bits(expected, CertifiedLogReturn.Of(-current, -previous));
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
