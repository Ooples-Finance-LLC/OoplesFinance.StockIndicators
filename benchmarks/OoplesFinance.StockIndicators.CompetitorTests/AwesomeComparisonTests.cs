using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AwesomeComparisonTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 5)]
    [InlineData(5, 34)]
    [InlineData(1, int.MaxValue)]
    public async Task IndependentContractsCheckWindowsLifecycleAndBoundaries(int fast, int slow)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(AwesomeWithDetails),
                $"{fast}/{slow}",
                () => new AwesomeWithDetails(fast, slow)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void WarmupBothOutputsAndZeroMidpointAreExplicit()
    {
        var data = AwesomeComparison.Fixture();
        var pair = AwesomeComparison.Create(1, 2);
        ComparisonVerifier.Check(pair, data, 20);
        var outputs = pair.Ooples(data, 20).Outputs;
        Assert.Equal(
            new[] { false, true, true, true, true, true, true, true },
            outputs["Oscillator"].Present
        );
        Assert.Equal(
            new[] { false, true, true, false, true, true, true, false },
            outputs["Normalized"].Present
        );
        Assert.Equal(.5, outputs["Oscillator"].Values[1]);
        Assert.Equal(100d / 6, outputs["Normalized"].Values[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AwesomeWithDetails(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AwesomeWithDetails(2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetAwesome(0, 2).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetAwesome(2, 2).ToArray());
    }

    [Fact]
    public async Task WideMidpointsSumsAndNormalizedProductsDoNotOverflowPrematurely()
    {
        foreach (var value in new[] { double.MaxValue, double.Epsilon })
        {
            var actual = await VolumePriceComparisonTests.Run(
                new AwesomeWithDetails(1, 2),
                (value, value, value, 1),
                (value, value, value, 1)
            );
            Assert.Equal(new[] { 0d, 0 }, actual[0]);
            Assert.Equal(new[] { 0d, 0 }, actual[1]);
            Assert.Equal(new[] { 0d, 1 }, actual[2]);
            Assert.Equal(new[] { 0d, 1 }, actual[3]);
        }
        var finite = await VolumePriceComparisonTests.Run(
            new AwesomeWithDetails(1, 2),
            (-double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
            (double.MaxValue, double.MaxValue, double.MaxValue, 1)
        );
        Assert.Equal(double.MaxValue, finite[0][1]);
        Assert.Equal(100, finite[1][1]);
        var tiny = await VolumePriceComparisonTests.Run(
            new AwesomeWithDetails(1, 2),
            (-double.Epsilon, -double.Epsilon, -double.Epsilon, 1),
            (double.Epsilon, double.Epsilon, double.Epsilon, 1)
        );
        Assert.Equal(double.Epsilon, tiny[0][1]);
        Assert.Equal(100, tiny[1][1]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new AwesomeWithDetails(1, 3),
                (-double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
                (-double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
                (double.MaxValue, double.MaxValue, double.MaxValue, 1)
            )
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new AwesomeWithDetails(1, 2),
                (1, 1, 1, 1),
                (double.Epsilon, double.Epsilon, double.Epsilon, 1)
            )
        );
    }

    [Fact]
    public async Task ChainedSourceUsesItsPublishedPrices()
    {
        var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
        var indicator = new AwesomeWithDetails(1, 2);
        indicator.Of(source);
        double[] prices = [0, .5, 1, .25];
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    prices.Select(
                        (v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v + 2, v + 4, v - 2, v, 1)
                    )
                )
            )
            .ConfigureIndicators(source, indicator)
            .BuildAsync();
        var selected = run[source.Value].ToArray();
        var data = CompetitorData.FromOhlcv(
            selected,
            selected,
            selected,
            selected,
            new double[selected.Length]
        );
        var expected = AwesomeComparison.Create(1, 2).Ooples(data, 20).Outputs;
        foreach (var slot in new[] { 0, 1 })
        {
            var actual = run[indicator.Outputs[slot]].ToArray();
            for (var i = 1; i < prices.Length; i++)
                Assert.Equal(expected[AwesomeComparison.Names[slot]].Values[i], actual[i]);
        }
    }

    [Fact]
    public void NativeQuoteTupleReusableAndSortingRoutesAgree()
    {
        var data = AwesomeComparison.Fixture();
        var tuples = data.Quotes.Select(q => (q.Date, (double)(q.High + q.Low) / 2)).ToArray();
        var expected = data.Quotes.GetAwesome(1, 2).ToArray();
        var reusable = tuples
            .Select(q => new AwesomeResult(q.Date) { Oscillator = q.Item2 })
            .Cast<IReusableResult>();
        foreach (
            var rows in new[]
            {
                tuples.Reverse().GetAwesome(1, 2).ToArray(),
                reusable.GetAwesome(1, 2).ToArray(),
                data.Quotes.AsEnumerable().Reverse().GetAwesome(1, 2).ToArray(),
            }
        )
        {
            Assert.Equal(expected.Select(r => r.Date), rows.Select(r => r.Date));
            Assert.Equal(expected.Select(r => r.Oscillator), rows.Select(r => r.Oscillator));
            Assert.Equal(expected.Select(r => r.Normalized), rows.Select(r => r.Normalized));
        }
        Assert.Equal(expected[1].Oscillator, ((IReusableResult)expected[1]).Value);
        var invalid = new[]
        {
            new Quote
            {
                Date = DateTime.UnixEpoch,
                High = decimal.MaxValue,
                Low = decimal.MaxValue,
            },
        };
        Assert.Throws<OverflowException>(() => invalid.GetAwesome().ToArray());
    }

    [Fact]
    public void EveryOutputAndPresenceDetectsCorruption()
    {
        var data = AwesomeComparison.Fixture();
        var pair = AwesomeComparison.Create(1, 2);
        foreach (var name in AwesomeComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs[name];
                if (presence)
                    output.Present![1] = false;
                else
                    output.Values[1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    20
                )
            );
        }
    }
}
