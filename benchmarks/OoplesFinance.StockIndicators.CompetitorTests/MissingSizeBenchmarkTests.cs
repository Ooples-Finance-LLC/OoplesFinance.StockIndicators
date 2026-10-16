using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class MissingSizeBenchmarkTests
{
    public static IEnumerable<object[]> Cases
    {
        get
        {
            foreach (string operation in new[] { "Sin", "Cos", "Tan", "Acos", "Atan", "Ln", "Log10",
                "Exp", "Sinh", "Cosh", "Tanh", "Ceil", "Floor", "Sqrt" })
                yield return [typeof(SharedPointwiseBenchmarks), operation, 1000];
            yield return [typeof(SharedZeroSeedLaguerreBenchmarks), "QuanTAlib.Ltma", 1000];
            yield return [typeof(SharedTriangularBenchmarks), "QuanTAlib.Trima", 1000];
            yield return [typeof(SharedTriangularBenchmarks), "TaLib.Functions.Trima", 1000];
            yield return [typeof(SharedPathRatioBenchmarks), "Skender.GetCmo", 1000];
            yield return [typeof(SharedPathRatioBenchmarks), "Trady.Indicator.EfficiencyRatio", 1000];
            yield return [typeof(SharedUlcerBenchmarks), "Skender.GetUlcerIndex", 1000];
            yield return [typeof(SharedWilderStrengthBenchmarks), "TaLib.Functions.Rsi", 1000];
            yield return [typeof(SharedHilbertBenchmarks), "TaLib.Functions.HtDcPeriod", 1000];
            yield return [typeof(SharedDynamicMomentumBenchmarks), "Trady.Indicator.DynamicMomentumIndex", 10000];
        }
    }

    [Theory, MemberData(nameof(Cases))]
    public void MissingSizeIsDiscoverableAndPassesAllFixtureOutputChecks(Type fixtureType, string operation, int count)
    {
        var fixture = Activator.CreateInstance(fixtureType)!;
        var countProperty = fixtureType.GetProperty("Count")!;
        var parameters = Assert.Single(countProperty.GetCustomAttributes(typeof(ParamsAttribute), true).Cast<ParamsAttribute>());
        Assert.Contains(count, parameters.Values);
        countProperty.SetValue(fixture, count);
        fixtureType.GetProperty("Operation")!.SetValue(fixture, operation);
        if (fixture is SharedTriangularBenchmarks triangular) triangular.IncludeGpuValidation = false;
        var setup = fixtureType.GetMethod("Setup")!.CreateDelegate<Action>(fixture);
        var cleanup = fixtureType.GetMethod("Cleanup")!.CreateDelegate<Action>(fixture);
        try { setup(); }
        finally { cleanup(); }
    }
}
