using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using TaCore = TALib.Core;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedHilbertBenchmarkTests
{
    [Theory]
    [InlineData("TaLib.Functions.HtDcPeriod", 33, 1)]
    [InlineData("TaLib.Functions.HtDcPeriod", 65, 1)]
    [InlineData("TaLib.Functions.HtPhasor", 33, 2)]
    [InlineData("TaLib.Functions.HtPhasor", 65, 2)]
    [InlineData("TaLib.Functions.Mama", 33, 2)]
    [InlineData("TaLib.Functions.Mama", 65, 2)]
    public void SetupChecksBothHistoriesPackedOutputsAndUnevenPrefixReplay(string operation, int count, int outputs)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedHilbertBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Equal((count - 32) * outputs, fixture.NativeSingle().Length);
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }

    [Theory]
    [InlineData("TaLib.Functions.HtDcPeriod", TaCore.UnstableFunc.HtDcPeriod)]
    [InlineData("TaLib.Functions.HtPhasor", TaCore.UnstableFunc.HtPhasor)]
    [InlineData("TaLib.Functions.Mama", TaCore.UnstableFunc.Mama)]
    public void NondefaultSuppressionIsRejected(string operation, TaCore.UnstableFunc kind)
    {
        int prior = TaCore.UnstablePeriodSettings.Get(kind);
        var fixture = new SharedHilbertBenchmarks { Operation = operation, Count = 65 };
        try
        {
            TaCore.UnstablePeriodSettings.Set(kind, 1);
            Assert.Throws<InvalidOperationException>(fixture.Setup);
        }
        finally { fixture.Cleanup(); TaCore.UnstablePeriodSettings.Set(kind, prior); }
    }
}
