using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BuilderCpuContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task IndependentContractsRejectCorruptionOfEveryPublishedOutput(int kind)
    {
        var bars = Enumerable.Range(0, 80).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            100 + Math.Sin(i), 102 + Math.Sin(i), 98 + Math.Sin(i), 100 + Math.Cos(i), 1000)).ToArray();
        IIndicator indicator = kind switch
        {
            0 => new JurikAdaptive(), 1 => new RollingPivotLevels(), _ => new RetrospectiveFractals()
        };
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var rules = ((IIndicatorValidationContract)indicator).ValidationRules.ToArray();
        var values = indicator.Outputs.Select(output => run[output].ToArray()).ToArray();
        Assert.Equal(values.Length, rules.Length);
        for (var slot = 0; slot < values.Length; slot++)
        {
            rules[slot].Check(new IndicatorValidationContext("uncorrupted", bars, values, 0));
            var mutated = values.Select(column => (double[])column.Clone()).ToArray();
            mutated[slot][40] += 1;
            Assert.ThrowsAny<Exception>(() => rules[slot].Check(new IndicatorValidationContext(
                "corrupted-output", bars, mutated, 0)));
        }
    }
}
