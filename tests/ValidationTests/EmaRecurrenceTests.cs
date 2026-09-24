using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EmaRecurrenceTests
{
    [Fact]
    public async Task ElderImpulseDiscreteSignsUseTheDocumentedEmaRounding()
    {
        var cases = IndicatorValidationDiscovery.Discover(new[] { typeof(ElderImpulseSystem).Assembly })
            .Where(testCase => testCase.IndicatorType == typeof(ElderImpulseSystem)).ToArray();
        Assert.NotEmpty(cases);
        foreach (var testCase in cases)
            (await IndicatorValidation.ValidateAsync(testCase)).ThrowIfInvalid();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(31)]
    public async Task AutomaticValidationIncludesEveryNumericalClassForEma(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(new(typeof(Ema), "numerical-recurrence", () => new Ema(period)));
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(31)]
    public void ConstantFiniteInputsArePreservedExactlyAfterStartup(int period)
    {
        var random = new Random(244);
        var samples = new[] { double.Epsilon, -double.Epsilon, 3 * double.Epsilon,
            double.MaxValue, -double.MaxValue, 0, .1 }.Concat(Enumerable.Range(0, 64)
            .Select(_ => BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue)))
            .Where(double.IsFinite));
        foreach (var value in samples)
        {
            var output = new double[period + 64];
            MovingAverageCore.ExponentialMovingAverage(Enumerable.Repeat(value, output.Length).ToArray(), output, period);
            Assert.All(output, actual => Assert.Equal(value, actual));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task RecurrenceMatchesIndependentRationalCorrectionAndPreviewsDoNotCommit(int period)
    {
        var values = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1d, -1d };
        var prices = Enumerable.Range(0, 96).Select(i => values[i % values.Length]).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = new double[prices.Length];
        var rate = new ReferenceFraction(2) / new ReferenceFraction(period + 1);
        var previous = new ReferenceFraction(0);
        for (var i = 0; i < expected.Length; i++)
        {
            var current = ReferenceFraction.FromDouble(prices[i]);
            var next = i < period ? prices.Take(i + 1).Select(ReferenceFraction.FromDouble)
                .Aggregate(new ReferenceFraction(0), (a, b) => a + b) / new ReferenceFraction(i + 1)
                : previous + rate * (current - previous);
            expected[i] = next.ToDouble();
            previous = ReferenceFraction.FromDouble(expected[i]);
        }
        var indicator = new Ema(period);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, run[indicator].ToArray());
        var legacy = new StockData(prices.ToList(), prices.ToList(), prices.ToList(), prices.ToList(),
            prices.Select(_ => 1d).ToList(), bars.Select(b => b.Time).ToList());
        Assert.Equal(expected, legacy.CalculateExponentialMovingAverage(period).CustomValuesList);
        var state = new ExponentialMovingAverageState(period);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var input = new OhlcvBar("EMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                var other = new OhlcvBar("EMA", BarTimeframe.Minutes(1), b.Time, b.Time, 100, 100, 100, 100, 1, true);
                state.Update(other, false, false);
                Assert.Equal(expected[i], state.Update(input, false, true).Value);
                Assert.Equal(expected[i], state.Update(input, true, true).Value);
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void IntegerWeightsAndDenominatorCannotOverflow(int period)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(double.MaxValue, 2);
        sum.Add(-double.MaxValue, period - 1);
        var maximum = ReferenceFraction.FromDouble(double.MaxValue);
        var expected = (maximum * new ReferenceFraction(3L - period) / new ReferenceFraction((long)period + 1)).ToDouble();
        Assert.Equal(expected, sum.Mean((long)period + 1));
    }
}

