using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;

// These checks run without BenchmarkDotNet and do not produce timings:
//   --coverage  what each library ships, so a missing row is never mistaken for a slow one
//   --verify    what each library computes, so the timings are known to be over the same arithmetic
if (args.Length > 0 && args[0].Equals("--coverage", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(CompetitorCoverage.ToMarkdown());
    return;
}

if (args.Length > 0 && args[0].Equals("--verify", StringComparison.OrdinalIgnoreCase))
{
    AgreementCheck.Run(Console.Out);
    return;
}

if (args.Length > 0 && args[0].Equals("--verify-workloads", StringComparison.OrdinalIgnoreCase))
{
    WorkloadCheck.Run(Console.Out);
    return;
}

if (args.Length > 0 && args[0].Equals("--alloc", StringComparison.OrdinalIgnoreCase))
{
    AllocProbe.Run(Console.Out);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(BatchBenchmarks).Assembly).Run(args);
