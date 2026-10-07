using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AlmaComparisonTests
{
    [Theory]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 9)]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    [InlineData(false, 20)]
    public async Task FullTrajectoriesAndLifecycleRespectStartup(bool full, int period)
    {
        ComparisonVerifier.Check(AlmaComparison.Create(full), AlmaComparison.Fixture(), period);
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ArnaudLegouxWindow),
                "ALMA startup",
                () => new ArnaudLegouxWindow(period, fullWindowOnly: full)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        var values = AlmaComparison
            .Owned(AlmaComparison.Fixture().IndicatorBars, period, full)
            .Outputs["Value"];
        Assert.All(
            values.Present!.Take(Math.Min(period - 1, values.Values.Length)),
            present => Assert.Equal(!full, present)
        );
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(.5, 2)]
    [InlineData(1, 20)]
    [InlineData(-1, 0)]
    [InlineData(2, -6)]
    public async Task OffsetAndWidthConfigurations(double offset, double sigma)
    {
        foreach (var period in new[] { 1, 2, 3, 9 })
        {
            ComparisonVerifier.Check(
                AlmaComparison.Create(false, offset, sigma),
                AlmaComparison.Fixture(),
                period
            );
            if (period > 1 && offset >= 0 && offset <= 1 && sigma > 0)
                ComparisonVerifier.Check(
                    AlmaComparison.Create(true, offset, sigma),
                    AlmaComparison.Fixture(),
                    period
                );
        }
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ArnaudLegouxWindow),
                "ALMA parameters",
                () => new ArnaudLegouxWindow(3, offset, sigma, false)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void ExtremeWidthsPreserveExactTiesAndFiniteMeans()
    {
        var bars = CompetitorData.FromCloses([1, 3]).IndicatorBars;
        foreach (var offset in new[] { .5, Math.BitIncrement(.5), Math.BitDecrement(.5) })
        {
            var output = AlmaComparison
                .Owned(bars, 2, false, offset, double.MaxValue)
                .Outputs["Value"]
                .Values;
            Assert.Equal(
                offset == .5 ? 2
                    : offset > .5 ? 3
                    : 1,
                output[1]
            );
            ComparisonVerifier.Compare(
                AlmaComparison.Reference([1, 3], 2, false, offset, double.MaxValue, false),
                AlmaComparison.Owned(bars, 2, false, offset, double.MaxValue),
                "exact center tie",
                IndicatorErrorBudget.Exact
            );
        }
        Assert.Equal(
            new[] { 1d, 2 },
            AlmaComparison.Owned(bars, 2, false, double.MaxValue, 0).Outputs["Value"].Values
        );
        Assert.Equal(
            new[] { 1d, 3 },
            AlmaComparison.Owned(bars, 2, false, double.MaxValue, 1).Outputs["Value"].Values
        );
        Assert.Equal(
            new[] { 1d, 1 },
            AlmaComparison.Owned(bars, 2, false, -double.MaxValue, 1).Outputs["Value"].Values
        );
        Assert.Equal(
            new[] { 1d, 2 },
            AlmaComparison.Owned(bars, 2, false, .85, double.Epsilon).Outputs["Value"].Values
        );
        ComparisonVerifier.Check(
            AlmaComparison.Create(true, .5, double.MaxValue),
            CompetitorData.FromCloses([1, 3, 5, 7]),
            2
        );
        // Present native NaN remains a correctness failure, even when its cause is known.
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                AlmaComparison.Create(false, .5, double.MaxValue),
                CompetitorData.FromCloses([1, 3]),
                2
            )
        );
        var native = AlmaComparison
            .Create(false, .5, double.MaxValue)
            .Competitor(CompetitorData.FromCloses([1, 3]), 2)
            .Outputs["Value"];
        Assert.True(native.Present![1]);
        Assert.True(double.IsNaN(native.Values[1]));
        Assert.False(
            AlmaComparison
                .Create(true, .5, double.MaxValue)
                .Competitor(CompetitorData.FromCloses([1, 3]), 2)
                .Outputs["Value"]
                .Present![1]
        );
    }

    [Fact]
    public void WideAndSubnormalPricesAndMaximumPeriods()
    {
        foreach (
            var prices in new[]
            {
                new[] { double.MaxValue, double.MaxValue, double.MaxValue },
                new[] { double.MaxValue, -double.MaxValue, double.MaxValue },
                new[] { double.Epsilon, 0, -double.Epsilon, 2 * double.Epsilon },
            }
        )
        foreach (var period in new[] { 2, 3, int.MaxValue })
        foreach (var full in new[] { true, false })
        {
            var bars = prices
                .Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1))
                .ToArray();
            ComparisonVerifier.Compare(
                AlmaComparison.Reference(prices, period, full, .85, 6, false),
                AlmaComparison.Owned(bars, period, full),
                "ALMA wide/lazy",
                IndicatorErrorBudget.Exact
            );
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArnaudLegouxWindow(0));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArnaudLegouxWindow(3, bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArnaudLegouxWindow(3, sigma: bad));
        }
    }

    [Fact]
    public void NativeSourceRevisionAndReset()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Alma(source, 3);
        var direct = new QuanTAlib.Alma(3);
        foreach (var x in new[] { 1d, 2, 4 })
        {
            var input = new QuanTAlib.TValue(x, true, false);
            source.Add(input);
            Assert.Equal(direct.Calc(input).Value, subscribed.Value);
        }
        Assert.True(direct.IsHot);
        direct.Calc(new QuanTAlib.TValue(7, false, false));
        var fresh = new QuanTAlib.Alma(3);
        foreach (var x in new[] { 1d, 2, 7 })
            fresh.Calc(new QuanTAlib.TValue(x, true, false));
        Assert.Equal(fresh.Value, direct.Value);
        direct.Init();
        Assert.Equal(10d, direct.Calc(new QuanTAlib.TValue(10, true, false)).Value);
        Assert.False(direct.IsHot);
        Assert.Throws<ArgumentException>(() => new QuanTAlib.Alma(0));
        Assert.True(
            double.IsNaN(
                new QuanTAlib.Alma(1, double.NaN).Calc(new QuanTAlib.TValue(1, true, false)).Value
            )
        );
    }

    [Fact]
    public void SkenderQuoteTupleReusableRoutesAndValidation()
    {
        var data = CompetitorData.FromCloses([1, Math.BitIncrement(1d), 2, 4, 8]);
        var tuples = data.Closes.Select((x, i) => (DateTime.UnixEpoch.AddDays(i), x)).ToArray();
        var direct = tuples.GetAlma(3).ToArray();
        var expected = AlmaComparison.Reference(data.Closes, 3, true, .85, 6, true);
        ComparisonVerifier.Compare(
            expected,
            VolumePriceComparison.Mask(direct.Select(r => r.Alma).ToArray()),
            "tuple route",
            IndicatorErrorBudget.Exact
        );
        var reused = tuples.GetSma(1).GetAlma(3).ToArray();
        Assert.Equal(direct.Select(r => r.Alma), reused.Select(r => r.Alma));
        Assert.Equal(tuples.Select(t => t.Item1), direct.Select(r => r.Date));
        ComparisonVerifier.Check(AlmaComparison.Create(true), data, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetAlma(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetAlma(3, -1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetAlma(3, sigma: 0).ToArray()
        );
        Assert.All(data.Quotes.GetAlma(3, double.NaN), row => Assert.Null(row.Alma));
    }

    [Fact]
    public void ValuePresenceAndStartupCorruptionAreDetected()
    {
        foreach (var pair in AlmaComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                if (presence)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    AlmaComparison.Fixture(),
                    3
                )
            );
        }
        var wrongStartup = AlmaComparison.Create(true) with
        {
            Library = (data, period) => AlmaComparison.Owned(data.IndicatorBars, period, false),
        };
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(wrongStartup, AlmaComparison.Fixture(), 3)
        );
    }
}
