using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CpuNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases => CpuKernelPilots.Ids.Select(id => new object[] { id });

    [Theory, MemberData(nameof(Cases))]
    public void DirectNativeArmsAgreeWithReferencesAndOwnTheirResults(string id)
    {
        var workload = new CpuNativeWorkload(id, 160);
        var before = workload.Data.Closes.ToArray();
        workload.Verify();
        var first = workload.NativeOwned();
        var second = workload.NativeOwned();
        Assert.NotSame(first, second);
        Assert.Equal(160, Assert.IsAssignableFrom<System.Collections.ICollection>(first).Count);
        Assert.Equal(160, Assert.IsAssignableFrom<System.Collections.ICollection>(second).Count);
        var pair = ComparisonPairs.Get(id);
        ComparisonVerifier.Compare(workload.Normalize(first), workload.Normalize(second), id + " repeat", pair.ErrorBudget);
        Assert.Equal(before, workload.Data.Closes);
        Assert.Equal(workload.OoplesOwned(), workload.OoplesOwned());
    }

    [Theory]
    [InlineData("QuanTAlib.Jma")]
    [InlineData("QuanTAlib.Atr")]
    public void FreshNativeStreamingStateMatchesFreshOwnedBatch(string id)
    {
        var workload = new CpuNativeWorkload(id, 80);
        var expected = (double[])workload.NativeOwned();
        for (var pass = 0; pass < 3; pass++)
        {
            var actual = new double[80]; workload.RunNativeStream(workload.NewNativeStream(), actual);
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData("Skender.GetRollingPivots")]
    [InlineData("Trady.Indicator.SimpleMovingAverage")]
    [InlineData("QuanTAlib.Jma")]
    public void UnsupportedReusableWorkloadsAreNotSilentlySubstituted(string id)
    {
        var workload = new CpuNativeWorkload(id, 80);
        Assert.False(CpuNativeWorkload.SupportsReusable(id));
        Assert.Throws<NotSupportedException>(() => workload.NativeReusable());
    }
}
