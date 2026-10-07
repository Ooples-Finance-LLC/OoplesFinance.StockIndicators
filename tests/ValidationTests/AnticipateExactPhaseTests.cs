using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AnticipateExactPhaseTests
{
    private static BigInteger Units(double value)
    {
        var fraction = ReferenceFraction.FromDouble(value).Components;
        return (fraction.Numerator << 1074) / fraction.Denominator;
    }
    private static double Oracle(BigInteger[] observed, int length)
    {
        if (length <= 2) return 0;
        var history = Enumerable.Range(0, length).Select(i => i < observed.Length ? observed[i] : BigInteger.Zero).ToArray();
        if (history.All(v => v == history[0])) return 0;
        BigInteger previousCovariance = 0, previousVariance = 1; var best = 0;
        for (var phase = 0; phase < length; phase++)
        {
            var wave = Enumerable.Range(0, length).Select(i => Units(-Math.Sin(2 * Math.PI * (phase + i) / length))).ToArray();
            BigInteger covariance = 0, variance = 0;
            // Pairwise differences independently eliminate both means.
            for (var i = 0; i < length; i++)
                for (var j = i + 1; j < length; j++)
                { var difference = wave[i] - wave[j]; covariance += (history[i] - history[j]) * difference; variance += difference * difference; }
            if (variance.IsZero) { covariance = 0; variance = 1; }
            var wins = phase == 0 || covariance.Sign > previousCovariance.Sign;
            if (covariance.Sign == previousCovariance.Sign && covariance.Sign != 0)
            {
                var current = new ReferenceFraction(covariance * covariance) / new ReferenceFraction(variance);
                var previous = new ReferenceFraction(previousCovariance * previousCovariance) / new ReferenceFraction(previousVariance);
                wins = current.CompareTo(previous) * covariance.Sign > 0;
            }
            if (wins) { best = phase; previousCovariance = covariance; previousVariance = variance; }
        }
        return -Math.Sin(2 * Math.PI * best / length);
    }
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(31)]
    public void PackedCovariancesMatchIndependentPairwiseCorrelations(int length)
    {
        var phase = new AnticipateExactPhase(length); var random = new Random(1035 + length);
        foreach (var count in new[] { 1, length / 2, length })
            for (var trial = 0; trial < 4; trial++)
            {
                var values = Enumerable.Range(0, count).Select(_ => new BigInteger(random.Next(-50, 51))).ToArray();
                Assert.Equal(Oracle(values, length), phase.Predict(values));
                var direct = typeof(AnticipateExactPhase).GetMethod("Direct", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var sum = values.Aggregate(BigInteger.Zero, (a,b)=>a+b);
                Assert.Equal(Oracle(values, length), (double)direct.Invoke(phase,new object[]{values,values.Length,sum})!);
                Assert.Equal(Oracle(values.Select(v => -v).ToArray(), length), phase.Predict(values.Select(v => -v).ToArray()));
            }
    }
    private static BigInteger Root(BigInteger value)
    {
        var guess = BigInteger.One << (value.ToByteArray().Length * 4 + 1);
        while (true) { var next = (guess + value / guess) / 2; if (next >= guess) return guess; guess = next; }
    }
    [Fact]
    public void AdjacentWaveBisectorsResolveVarianceDifferencesExactly()
    {
        foreach (var length in new[] { 3, 4, 7, 14 })
        {
            var phase = new AnticipateExactPhase(length);
            for (var first = 0; first < length - 1; first++)
            {
                var a = Enumerable.Range(0,length).Select(lag=>Units(-Math.Sin(2*Math.PI*(first+lag)/length))).ToArray();
                var b = Enumerable.Range(0,length).Select(lag=>Units(-Math.Sin(2*Math.PI*(first+1+lag)/length))).ToArray();
                var sumA=a.Aggregate(BigInteger.Zero,(x,y)=>x+y); var sumB=b.Aggregate(BigInteger.Zero,(x,y)=>x+y);
                a=a.Select(v=>length*v-sumA).ToArray(); b=b.Select(v=>length*v-sumB).ToArray();
                var normA=a.Aggregate(BigInteger.Zero,(x,y)=>x+y*y); var normB=b.Aggregate(BigInteger.Zero,(x,y)=>x+y*y);
                var rootA=Root(normA<<1024); var rootB=Root(normB<<1024);
                foreach(var side in new[]{-8,8})
                {
                    var history=a.Select((v,i)=>v*(rootB+side)+b[i]*rootA).ToArray();
                    Assert.Equal(Oracle(history,length),phase.Predict(history));
                }
            }
        }
    }
    [Fact]
    public void TinyDifferencesAndExtremeCommonScalingKeepTheirPhase()
    {
        var phase = new AnticipateExactPhase(7);
        var small = new BigInteger[] { 1, -2, 3, -4, 1, 8, -5 };
        var expected = Oracle(small, 7);
        Assert.Equal(expected, phase.Predict(small));
        Assert.Equal(expected, phase.Predict(small.Select(v => v << 4096).ToArray()));
        var offset = small.Select(v => (BigInteger.One << 4096) + v).ToArray();
        Assert.Equal(expected, phase.Predict(offset));
        var direct = typeof(AnticipateExactPhase).GetMethod("Direct", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal(expected, (double)direct.Invoke(phase,new object[]{offset,offset.Length,offset.Aggregate(BigInteger.Zero,(x,y)=>x+y)})!);
        Assert.NotEqual(0, expected);
    }
    [Fact]
    public void FullFlatAndUnresolvableWindowsNeedNoBasisAllocation()
    {
        Assert.Equal(0, new AnticipateExactPhase(1).Predict(new BigInteger[] { 17 }));
        Assert.Equal(0, new AnticipateExactPhase(2).Predict(new BigInteger[] { 17, -9 }));
        Assert.Equal(0, new AnticipateExactPhase(7).Predict(Enumerable.Repeat(new BigInteger(17), 7).ToArray()));
        Assert.Equal(0, new AnticipateExactPhase(int.MaxValue).Predict(new BigInteger[] { 0, 0 }));
    }
    [Fact]
    public void CompleteBasisAndPeriodsBeyondProposedCapUseLinearStorage()
    {
        new AnticipateExactPhase(3).Predict(new BigInteger[] { 1 });
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(1, new AnticipateExactPhase(8192).Predict(new BigInteger[] { 1 }));
        const int length = 4096, selected = 17;
        var history = Enumerable.Range(0, length).Select(i => Units(-Math.Sin(2 * Math.PI * (selected + i) / length))).ToArray();
        Assert.Equal(-Math.Sin(2 * Math.PI * selected / length), new AnticipateExactPhase(length).Predict(history));
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 128_000_000);
    }
}
