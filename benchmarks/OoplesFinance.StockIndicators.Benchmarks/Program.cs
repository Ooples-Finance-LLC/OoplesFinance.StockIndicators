using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.Benchmarks;

if (StreamingPerformanceRunner.TryRun(args))
{
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
