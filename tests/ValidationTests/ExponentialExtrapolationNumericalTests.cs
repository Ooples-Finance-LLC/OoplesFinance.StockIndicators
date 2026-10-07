using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ExponentialExtrapolationNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Dema) || c.IndicatorType == typeof(Tema) || c.IndicatorType == typeof(Zlema))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void ConstantComponentsSurviveOverflowingProductsAndTinyDifferences()
    {
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon })
        {
            Assert.Equal(value, ExponentialExtrapolation.Double(value, value));
            Assert.Equal(value, ExponentialExtrapolation.Triple(value, value, value));
        }
        Assert.Equal(double.PositiveInfinity, ExponentialExtrapolation.Double(double.MaxValue, -double.MaxValue));
        Assert.Equal(double.NegativeInfinity, ExponentialExtrapolation.Triple(-double.MaxValue, double.MaxValue, 0));
        Assert.Equal(3 * double.Epsilon, ExponentialExtrapolation.Triple(double.Epsilon, 0, 0));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 3)]
    [InlineData(0, 14)]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(1, 14)]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    [InlineData(2, 14)]
    public async Task EveryRouteMatchesIndependentRoundedStages(int variant, int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars.ToArray();
            var input = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedExponentialExtrapolation(bars, length, variant == 1);
            var actual = new double[bars.Length];
            if (variant == 0) MovingAverageCore.DoubleExponentialMovingAverage(input, actual, length);
            else if (variant == 1) MovingAverageCore.TripleExponentialMovingAverage(input, actual, length);
            else MovingAverageCore.ZeroLagEma(input, actual, length);
            Assert.Equal(expected, actual);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                input, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (variant == 0) data.CalculateDoubleExponentialMovingAverage(length: length);
            else if (variant == 1) data.CalculateTripleExponentialMovingAverage(length: length);
            else data.CalculateZeroLagExponentialMovingAverage(length: length);
            Assert.Equal(expected, data.CustomValuesList);
            IIndicator indicator = variant == 0 ? new Dema(length) : variant == 1 ? new Tema(length) : new Zlema(length);
            if (expected.Any(double.IsInfinity))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(new[] { indicator }).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new[] { indicator }).BuildAsync();
                Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
            }
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
            var key = variant == 0 ? "Dema" : variant == 1 ? "Tema" : "Zema";
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
                        var native = new OhlcvBar("EXP", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs![key]);
                    }
                }
            }
            var kind = variant == 0 ? MovingAvgType.DoubleExponentialMovingAverage
                : variant == 1 ? MovingAvgType.TripleExponentialMovingAverage : MovingAvgType.ZeroLagExponentialMovingAverage;
            using var smoother = MovingAverageSmootherFactory.Create(kind, length);
            for (var replay = 0; replay < 2; replay++)
            {
                smoother.Reset();
                for (var i = 0; i < input.Length; i++)
                {
                    Assert.Equal(expected[i], smoother.Next(input[i], false));
                    Assert.Equal(expected[i], smoother.Next(input[i], true));
                }
            }
        }
    }
}
