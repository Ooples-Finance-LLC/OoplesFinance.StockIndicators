using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedSkenderPivotNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetPivotPoints", "Skender.GetPivots" }
        from count in new[] { 0, 1, 4, 5, 6, 19, 20, 21, 22, 23, 59, 60, 61, 159, 160, 161, 175, 176, 177, 479, 480, 481, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePartitionsPreserveEveryDecimalFieldAndOwnership(string operation, int count)
    {
        var fixture = new SharedSkenderPivotNativeBenchmarks { Count = count, Operation = operation };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var later = fixture.NativeParallel();
            Assert.NotSame(parallel, later);
            Assert.Equal(count, ((Array)parallel).Length);
            if (parallel is PivotsResult[] trends)
            {
                var expected = (List<PivotsResult>)serial;
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal((expected[i].Date, expected[i].HighPoint, expected[i].LowPoint, expected[i].HighLine, expected[i].LowLine, expected[i].HighTrend, expected[i].LowTrend),
                        (trends[i].Date, trends[i].HighPoint, trends[i].LowPoint, trends[i].HighLine, trends[i].LowLine, trends[i].HighTrend, trends[i].LowTrend));
                    Assert.NotSame(trends[i], ((PivotsResult[])later)[i]);
                }
            }
            else
            {
                var levels = (PivotPointsResult[])parallel;
                var expected = (List<PivotPointsResult>)serial;
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal((expected[i].Date, expected[i].PP, expected[i].S1, expected[i].S2, expected[i].S3, expected[i].S4, expected[i].R1, expected[i].R2, expected[i].R3, expected[i].R4),
                        (levels[i].Date, levels[i].PP, levels[i].S1, levels[i].S2, levels[i].S3, levels[i].S4, levels[i].R1, levels[i].R2, levels[i].R3, levels[i].R4));
                    Assert.NotSame(levels[i], ((PivotPointsResult[])later)[i]);
                }
            }
            var retained = fixture.Normalize(parallel);
            Array.Clear((Array)later);
            ComparisonVerifier.Compare(retained, fixture.Normalize(parallel), "retained pivots", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryRetainsTwoMappingsAndThreeEagerArms()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderPivotNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(12, run.BenchmarksCases.Length);
    }
}
