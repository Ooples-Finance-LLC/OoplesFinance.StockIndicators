using System.Reflection;
using System.Runtime.Loader;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PilotCoreReplay
{
    internal static void Verify(string baselinePath)
    {
        var context = new AssemblyLoadContext("sma-baseline", isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(baselinePath));
            var original = assembly.GetType("OoplesFinance.StockIndicators.Core.MovingAverageCore", true)!
                .GetMethod("SimpleMovingAverage", BindingFlags.NonPublic | BindingFlags.Static)!
                .CreateDelegate<CpuFeasibilityPrototypes.SmaCore>();
            var cases = new List<double[]>
            {
                Enumerable.Range(0, 2051).Select(i => (i % 127 - 63) / 64d).ToArray(),
                Enumerable.Range(0, 2051).Select(i => 100 + i % 19 / 100d).ToArray(),
                Enumerable.Range(0, 2051).Select(i => .1 + i % 19 / 100d).ToArray(),
                Enumerable.Repeat(.25, 2050).Append(double.Epsilon).ToArray(),
                new[] { -0d, 0d, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1d }
            };
#pragma warning disable S2245 // Replay the same numerical fixture against both binaries; no secrets are generated.
            var random = new Random(541);
#pragma warning restore S2245
            cases.Add(Enumerable.Range(0, 2051).Select(_ => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-500, 501))).ToArray());
            long checkedValues = 0;
            foreach (var input in cases)
            foreach (var period in new[] { 1, 3, 20, 64, 5000 })
            {
                var expected = new double[input.Length];
                var actual = new double[input.Length];
                original(input, expected, period);
                CpuFeasibilityPrototypes.CurrentSma(input, actual, period);
                AsinFeasibilityBenchmarks.RequireSame(expected, actual);
                checkedValues += input.Length;
            }
            Console.WriteLine($"Baseline SMA replay passed: {checkedValues} bitwise values.");
        }
        finally { context.Unload(); }
    }
}
