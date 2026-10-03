using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VarianceNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Variance)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void ExactMeanPreventsTranslationBiasAndConstantOverflow()
    {
        foreach (var values in new[] { new[] { 0d, 2 }, new[] { 1e16, 1e16 + 2 } })
        {
            var actual = new double[2];
            VolatilityCore.Variance(values, actual, 2);
            Assert.Equal(new[] { 0d, 1d }, actual);
        }
        foreach (var constant in new[] { double.MaxValue, -double.MaxValue, double.Epsilon })
        {
            var actual = new double[8];
            VolatilityCore.Variance(Enumerable.Repeat(constant, 8).ToArray(), actual, 3);
            Assert.All(actual, value => Assert.Equal(0, value));
        }
        var tiny = Math.Pow(2, -537);
        var subnormal = new double[2];
        VolatilityCore.Variance(new[] { -tiny, tiny }, subnormal, 2);
        Assert.Equal(double.Epsilon, subnormal[1]);
        VolatilityCore.Variance(new[] { 0d, tiny }, subnormal, 2);
        Assert.Equal(0, subnormal[1]);
        var ties = new double[4];
        VolatilityCore.Variance(new[] { 0d, 0d, tiny, -tiny }, ties, 4);
        Assert.Equal(0, ties[3]); // Half epsilon rounds to even zero.
        VolatilityCore.Variance(new[] { tiny, -tiny, 2 * tiny, -2 * tiny }, ties, 4);
        Assert.Equal(2 * double.Epsilon, ties[3]); // 2.5 epsilon rounds to even two.
        using var window = new ExactVarianceWindow(2);
        window.Next(double.MaxValue, true);
        Assert.Equal(double.PositiveInfinity, window.Next(-double.MaxValue, true));
        window.Next(1, true);
        Assert.Equal(0, window.Next(1, true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task EveryRouteMatchesIndependentPopulationVariance(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars.ToArray();
            var expected = BuiltInFormulaReferences.RoundedPopulationVariance(bars, length);
            var actual = new double[bars.Length];
            VolatilityCore.Variance(bars.Select(b => b.Close).ToArray(), actual, length);
            Assert.Equal(expected, actual);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            using (var context = new ComputeContext())
            {
                using var result = IndicatorCompute.ComputeVarianceFast(data, context, length);
                Assert.Equal(expected, result.ToArray());
            }
            data.CalculateVariance(length);
            Assert.Equal(expected, data.OutputValues["Variance"]);
            var indicator = new Variance(length);
            if (expected.Any(double.IsInfinity))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator].ToArray());
            }
            var spec = new IndicatorSpec(IndicatorName.Variance, new VarianceSpecOptions(length));
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("LSMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Variance"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task UnrepresentableVarianceIsRejectedAtTheOutputBoundary()
    {
        var values = new[] { double.MaxValue, -double.MaxValue, double.MaxValue };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedPopulationVariance(bars, 3);
        Assert.Contains(expected, double.IsInfinity);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new Variance(3)).BuildAsync());
    }
}
