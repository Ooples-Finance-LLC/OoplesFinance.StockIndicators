using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using TaCore = TALib.Core;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SeededAtrBenchmarkTests
{
    [Theory]
    [InlineData("Skender.GetAtr")]
    [InlineData("TaLib.Functions.Atr")]
    [InlineData("TaLib.Functions.Natr")]
    [InlineData("Trady.Indicator.AverageTrueRange")]
    public void BothHistoryModesAndNativePrefixesMatchTheirIndependentReferences(string operation)
    {
        foreach (int count in new[] { 21, 42, 64, 167, 168, 513, 1000, 10000 })
        {
            var benchmark = new SharedSeededAtrBenchmarks { Operation = operation, Count = count };
            try { benchmark.Setup(); }
            finally { benchmark.Cleanup(); }
        }
    }

    [Theory]
    [InlineData("Skender.GetAtr", 21)]
    [InlineData("TaLib.Functions.Atr", 21)]
    [InlineData("TaLib.Functions.Natr", 21)]
    [InlineData("Trady.Indicator.AverageTrueRange", 21)]
    [InlineData("Skender.GetAtr", 20)]
    [InlineData("Skender.GetAtr", -1)]
    [InlineData("unknown", 21)]
    public void SetupAndRepeatedCleanupPreserveCallerSettings(string operation, int count)
    {
        int priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        int priorAtr = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Atr);
        int priorNatr = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Natr);
        var benchmark = new SharedSeededAtrBenchmarks { Operation = operation, Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Atr, 3);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Natr, 5);
            benchmark.Cleanup(); // No setup has saved settings yet.
            AssertSettings(2, 3, 5);
            if (count <= 20)
                Assert.Throws<ArgumentOutOfRangeException>(() => benchmark.Setup());
            else if (operation == "unknown")
                Assert.Throws<ArgumentException>(() => benchmark.Setup());
            else
            {
                benchmark.Setup();
                AssertSettings(8, 0, 0);
                benchmark.Cleanup();
            }
            AssertSettings(2, 3, 5);
            // Cleanup must not restore a stale snapshot a second time.
            CpuParallelSettings.MaxDegreeOfParallelism = 4;
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Atr, 7);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Natr, 9);
            benchmark.Cleanup();
            AssertSettings(4, 7, 9);
        }
        finally
        {
            benchmark.Cleanup();
            CpuParallelSettings.MaxDegreeOfParallelism = priorDop;
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Atr, priorAtr);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Natr, priorNatr);
        }
    }

    private static void AssertSettings(int dop, int atr, int natr)
    {
        Assert.Equal(dop, CpuParallelSettings.MaxDegreeOfParallelism);
        Assert.Equal(atr, TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Atr));
        Assert.Equal(natr, TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Natr));
    }

    [Theory]
    [InlineData("Skender.GetAtr")]
    [InlineData("TaLib.Functions.Atr")]
    [InlineData("TaLib.Functions.Natr")]
    [InlineData("Trady.Indicator.AverageTrueRange")]
    public void AllocationProbeMeasuresAllFourEagerArms(string operation)
    {
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedSeededAtrBenchmarks))));
        var report = SharedAllocationProbe.Run(operation, 64, 1);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
        Assert.Throws<ArgumentException>(() => SharedAllocationProbe.Run(operation, 64, 1, includeGpu: true));
    }
}
