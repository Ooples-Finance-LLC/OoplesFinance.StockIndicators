using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedSkenderCandleBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetDoji", "Skender.GetMarubozu", "Skender.GetHeikinAshi" }
        from count in new[] { 1, 2, 7, 8, 9, 15, 16, 17, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void DirectNativeRangesRetainValuesMasksDatesAndIndependentResults(string operation, int count)
    {
        var fixture = new SharedSkenderCandleBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var first = (System.Collections.IList)fixture.NativeSingle();
            var expected = SharedSkenderCandleBenchmarks.Normalize(first);
            var second = (System.Collections.IList)fixture.NativeSingle();
            var parallel = (Array)fixture.NativeParallel();
            var repeated = (Array)fixture.NativeParallel();
            Assert.NotSame(first, second);
            Assert.NotSame(parallel, repeated);
            for (int i = 0; i < count; i++)
            {
                Assert.NotSame(first[i], second[i]);
                Assert.NotSame(parallel.GetValue(i), repeated.GetValue(i));
                RequireRawSame(first[i]!, second[i]!);
                RequireRawSame(first[i]!, parallel.GetValue(i)!);
                RequireRawSame(first[i]!, repeated.GetValue(i)!);
            }
            ComparisonVerifier.Compare(expected, SharedSkenderCandleBenchmarks.Normalize(second), "repeated serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderCandleBenchmarks.Normalize(parallel), "parallel", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderCandleBenchmarks.Normalize(repeated), "repeated parallel", IndicatorErrorBudget.Exact);
            second.Clear();
            Array.Clear(repeated);
            ComparisonVerifier.Compare(expected, SharedSkenderCandleBenchmarks.Normalize(first), "retained serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderCandleBenchmarks.Normalize(parallel), "retained parallel", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    private static void RequireRawSame(object expected, object actual)
    {
        if (expected is HeikinAshiResult a)
        {
            var b = Assert.IsType<HeikinAshiResult>(actual);
            Assert.Equal((a.Date, a.Open, a.High, a.Low, a.Close, a.Volume),
                (b.Date, b.Open, b.High, b.Low, b.Close, b.Volume));
        }
        else
        {
            var aCandle = Assert.IsType<CandleResult>(expected);
            var bCandle = Assert.IsType<CandleResult>(actual);
            Assert.NotSame(aCandle.Candle, bCandle.Candle);
            Assert.Equal((aCandle.Date, aCandle.Match, aCandle.Price), (bCandle.Date, bCandle.Match, bCandle.Price));
            Assert.Equal((aCandle.Candle.Open, aCandle.Candle.High, aCandle.Candle.Low, aCandle.Candle.Close, aCandle.Candle.Volume),
                (bCandle.Candle.Open, bCandle.Candle.High, bCandle.Candle.Low, bCandle.Candle.Close, bCandle.Candle.Volume));
            Assert.Equal((aCandle.Candle.Size, aCandle.Candle.Body, aCandle.Candle.UpperWick, aCandle.Candle.LowerWick),
                (bCandle.Candle.Size, bCandle.Candle.Body, bCandle.Candle.UpperWick, bCandle.Candle.LowerWick));
        }
    }

    [Fact]
    public void FourEagerArmsCoverThreeOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderCandleBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(24, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
