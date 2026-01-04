using System;

namespace OoplesFinance.StockIndicators.Benchmarks;

internal static class BenchmarkProfile
{
    internal static bool IsFull
    {
        get
        {
            var profile = Environment.GetEnvironmentVariable("OOPLES_BENCHMARK_PROFILE") ??
                Environment.GetEnvironmentVariable("BENCHMARK_PROFILE");
            if (!string.IsNullOrWhiteSpace(profile))
            {
                return profile.Equals("full", StringComparison.OrdinalIgnoreCase);
            }

            var ci = Environment.GetEnvironmentVariable("CI");
            if (!string.IsNullOrWhiteSpace(ci))
            {
                return ci.Equals("true", StringComparison.OrdinalIgnoreCase) || ci == "1";
            }

            return false;
        }
    }
}
