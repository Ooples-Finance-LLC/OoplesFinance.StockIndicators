using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LeaderDependentOverflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResidualOverflowProvesExactPublicRejection(bool percentage)
    {
        IIndicator Create() => percentage ? new PercentagePriceOscillatorLeader(2)
            : new MovingAverageConvergenceDivergenceLeader(1, 5, 2);
        var index = percentage ? 12 : 3;
        var bars = Bars(Enumerable.Repeat(double.MaxValue, index).Concat([-double.MaxValue, 1d, 2d]));
        var reference = BuiltInFormulaReferences.LeaderOscillatorReference(bars,
            ((IBuiltInIndicator)Create()).CreateOptions(), percentage);
        var key = percentage ? "Ppo" : "Macd";
        Assert.True(double.IsNaN(reference.Values[key][index]));
        Assert.True(double.IsNegativeInfinity(reference.OverflowSources[key][index]));
        Assert.All(reference.OverflowSources[key].Take(index), v => Assert.False(double.IsInfinity(v)));
        Assert.All(reference.OverflowSources[key].Skip(index + 1), v => Assert.False(double.IsInfinity(v)));
        if (!percentage)
        {
            Assert.Equal(bars.Select(b => b.Close), reference.Values["I1"]);
            Assert.All(reference.OverflowSources["I1"], v => Assert.False(double.IsInfinity(v)));
        }
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(Create().GetType(), "leader-residual", Create),
            new IndicatorValidationOptions { AdditionalFixtures = [new IndicatorValidationFixture("leader-residual", bars)] });
        report.ThrowIfInvalid();
        var evidence = Assert.Single(report.FixtureEvidence, f => f.Name == "leader-residual");
        Assert.Equal(index, evidence.OutputOverflowBarIndex);
        Assert.Equal(0, evidence.OutputOverflowSlot);
        Assert.Equal(2, evidence.OutputOverflowRejectionsChecked);
        Assert.Null(evidence.OutputOverflowSign);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeaderSumOverflowIsActualUpstreamEvidence(bool percentage)
    {
        var reference = BuiltInFormulaReferences.LeaderOscillatorReference(Bars([0, double.MaxValue, double.MaxValue]),
            new MovingAverageConvergenceDivergenceLeaderSpecOptions(1, 2), percentage);
        var key = percentage ? "Ppo" : "Macd";
        Assert.True(double.IsPositiveInfinity(reference.OverflowSources[key][2]));
        Assert.All(reference.OverflowSources[key].Take(2), v => Assert.False(double.IsInfinity(v)));
        if (percentage) Assert.True(double.IsNaN(reference.Values[key][2]));
        else Assert.True(double.IsNegativeInfinity(reference.Values[key][2]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiniteLeaderStagesHaveNoOverflowProof(bool percentage)
    {
        var reference = BuiltInFormulaReferences.LeaderOscillatorReference(Bars([1, 3, -2, 4, 0, 1]),
            new MovingAverageConvergenceDivergenceLeaderSpecOptions(1, 2), percentage);
        foreach (var trace in reference.OverflowSources.Values)
            Assert.All(trace, v => Assert.False(double.IsInfinity(v)));
    }

    private static Bar[] Bars(IEnumerable<double> values) => values.Select((v, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
}
