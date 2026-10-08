using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CandleComparisonExecutionTests
{
    [Fact]
    public void PackedSignalsRetainAlignmentAndReturnIndependentArrays()
    {
        var packed = new[] { -100, 0, 200, 777, 777 };
        var result = CandleComparisonExecution
            .Unpack(TALib.Core.RetCode.Success, packed, 2..5, 5, 2, "test")
            .Outputs["Value"];
        Assert.Equal(2, result.FirstValid);
        Assert.Equal(new double[] { 0, 0, -100, 0, 200 }, result.Values);
        result.Values[2] = 99;
        Assert.Equal(-100, packed[0]);
        Assert.Equal(
            new double[] { 0, 0, -100, 0, 200 },
            CandleComparisonExecution
                .Unpack(TALib.Core.RetCode.Success, packed, 2..5, 5, 2, "test")
                .Outputs["Value"]
                .Values
        );
    }

    [Fact]
    public void OnlyThePinnedSingletonRejectionIsAccepted()
    {
        var result = CandleComparisonExecution
            .Unpack(TALib.Core.RetCode.OutOfRangeParam, [0], 0..0, 1, 1, "test")
            .Outputs["Value"];
        Assert.Equal(1, result.FirstValid);
        Assert.Single(result.Values);
        Assert.True(double.IsNaN(result.Values[0]));
        Assert.Throws<InvalidOperationException>(() =>
            CandleComparisonExecution.Unpack(
                TALib.Core.RetCode.OutOfRangeParam,
                [0, 0],
                0..0,
                2,
                2,
                "test"
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            CandleComparisonExecution.Unpack(TALib.Core.RetCode.BadParam, [0], 0..0, 1, 1, "test")
        );
    }

    [Fact]
    public void MisalignedOrTruncatedOutputIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CandleComparisonExecution.Unpack(
                TALib.Core.RetCode.Success,
                new int[5],
                0..3,
                5,
                2,
                "test"
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            CandleComparisonExecution.Unpack(
                TALib.Core.RetCode.Success,
                new int[5],
                2..4,
                5,
                2,
                "test"
            )
        );
    }

    [Fact]
    public void EmptyWarmupOutputKeepsItsFirstValidIndex()
    {
        var result = CandleComparisonExecution
            .Unpack(TALib.Core.RetCode.Success, [0, 0], 0..0, 2, 2, "test")
            .Outputs["Value"];
        Assert.Equal(2, result.FirstValid);
        Assert.Equal(new double[2], result.Values);
    }
}
