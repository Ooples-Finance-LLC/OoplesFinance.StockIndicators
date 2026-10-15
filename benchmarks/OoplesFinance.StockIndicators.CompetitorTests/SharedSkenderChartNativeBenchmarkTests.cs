using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedSkenderChartNativeBenchmarkTests
{
    public static IEnumerable<object[]> RenkoCases =>
        from operation in new[] { "Skender.GetRenko", "Skender.GetRenkoAtr" }
        from count in new[] { 0, 1, 2, 19, 20, 21, 22, 159, 160, 161, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(RenkoCases))]
    public void RenkoRetainsAllBricksAndNativeDecimalFields(string operation, int count)
    {
        var fixture = new SharedSkenderRenkoChartNativeBenchmarks { Count = count, Operation = operation };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var later = fixture.NativeParallel();
            EqualBricks(serial, parallel);
            EqualBricks(parallel, later);
            Array.Clear(later);
            EqualBricks(serial, parallel);
        }
        finally { fixture.Cleanup(); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RenkoHandlesZeroAtrMultipleBricksAndFinalAtrChanges(int shape)
    {
        var quotes = Enumerable.Range(0, 81).Select(i =>
        {
            decimal close = shape == 0 ? 100 : i % 2 == 0 ? 100 : 130;
            decimal width = shape == 2 && i == 80 ? 100 : 0;
            return new Quote { Date = DateTime.UnixEpoch.AddMinutes(i), Open = close, High = close + width,
                Low = close - width, Close = close, Volume = 13 };
        }).ToList();
        foreach (bool atr in new[] { false, true })
        {
            var serial = (atr ? quotes.GetRenkoAtr(20) : quotes.GetRenko(1m)).ToList();
            var parallel = SharedSkenderRenkoChartNativeBenchmarks.CalculateParallel(quotes, [20, 40, 60, 81], atr);
            EqualBricks(serial, parallel);
            if (shape == 0) Assert.Empty(parallel);
            if (shape == 1 && !atr) Assert.True(parallel.Count(r => r.Date == quotes[1].Date) > 1);
            if (shape == 2 && atr)
            {
                var before = quotes.Take(80).GetRenkoAtr(20).ToList();
                Assert.NotEmpty(before);
                Assert.Empty(serial);
            }
        }
    }

    private static void EqualBricks(IReadOnlyList<RenkoResult> expected, IReadOnlyList<RenkoResult> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal((expected[i].Date, expected[i].Open, expected[i].High, expected[i].Low, expected[i].Close, expected[i].Volume, expected[i].IsUp),
                (actual[i].Date, actual[i].Open, actual[i].High, actual[i].Low, actual[i].Close, actual[i].Volume, actual[i].IsUp));
            Assert.NotSame(expected[i], actual[i]);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(19)] [InlineData(20)] [InlineData(21)]
    [InlineData(22)] [InlineData(159)] [InlineData(160)] [InlineData(161)] [InlineData(1000)] [InlineData(10000)]
    public void ZigZagRetainsNativeFieldsAndIndependentRows(int count)
    {
        var fixture = new SharedSkenderZigZagChartNativeBenchmarks { Count = count };
        fixture.Setup();
        var rows = fixture.NativeSingle();
        var later = fixture.NativeSingle();
        Assert.Equal(count, rows.Count);
        Assert.NotSame(rows, later);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal((rows[i].Date, rows[i].ZigZag, rows[i].PointType, rows[i].RetraceHigh, rows[i].RetraceLow),
                (later[i].Date, later[i].ZigZag, later[i].PointType, later[i].RetraceHigh, later[i].RetraceLow));
            Assert.NotSame(rows[i], later[i]);
        }
        later.Clear();
        Assert.Equal(count, rows.Count);
    }

    [Theory]
    [InlineData(typeof(SharedSkenderRenkoChartNativeBenchmarks), 12)]
    [InlineData(typeof(SharedSkenderZigZagChartNativeBenchmarks), 4)]
    public void DiscoveryRetainsSupportedEagerArms(Type type, int count)
    {
        var run = BenchmarkConverter.TypeToBenchmarks(type);
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(count, run.BenchmarksCases.Length);
    }
}
