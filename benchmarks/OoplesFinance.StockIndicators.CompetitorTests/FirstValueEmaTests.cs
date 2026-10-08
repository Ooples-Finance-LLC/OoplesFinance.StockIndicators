using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class FirstValueEmaTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task ExactReferenceAndLifecycle(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(FirstValueEma),
                "period",
                () => new FirstValueEma(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task ExtremeCancellationAndSubnormalRounding()
    {
        Assert.Equal(
            new[] { double.MaxValue, 0d },
            await Run(3, [double.MaxValue, -double.MaxValue])
        );
        Assert.Equal(new[] { double.Epsilon, 0d }, await Run(3, [double.Epsilon, -double.Epsilon]));
        Assert.Equal(
            new[] { double.Epsilon, double.Epsilon },
            await Run(3, [double.Epsilon, double.Epsilon])
        );
        Assert.Equal(
            new[] { double.MaxValue, -double.MaxValue },
            await Run(1, [double.MaxValue, -double.MaxValue])
        );
    }

    [Fact]
    public void PeriodValidationAndWarmup()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FirstValueEma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FirstValueEma(-1));
        Assert.Equal(0, new FirstValueEma(int.MaxValue).WarmupBars);
    }

    private static async Task<double[]> Run(int period, double[] values)
    {
        var bars = values
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1))
            .ToArray();
        var indicator = new FirstValueEma(period);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}
