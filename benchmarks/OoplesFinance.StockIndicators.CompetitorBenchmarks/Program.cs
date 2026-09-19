using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Two commands run without BenchmarkDotNet, because both belong next to the timings and neither is a timing:
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

BenchmarkSwitcher.FromAssembly(typeof(BatchBenchmarks).Assembly).Run(args);
