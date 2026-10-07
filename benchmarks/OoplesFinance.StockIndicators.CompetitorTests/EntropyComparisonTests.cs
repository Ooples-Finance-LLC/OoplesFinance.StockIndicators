using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class EntropyComparisonTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(20)]
    public void FrequenciesExpirationAndConstantConventionMatch(int period)
    {
        ComparisonVerifier.Check(EntropyComparison.Pair, EntropyComparison.Fixture(), period);
        var data = CompetitorData.FromCloses([1, 1, 1, 2]);
        var result = EntropyComparison.Pair.Ooples(data, 4).Outputs["Value"].Values;
        Assert.Equal(new[] { 1d, 1, 1 }, result.Take(3));
        Assert.InRange(result[3], .8112781244591327, .811278124459133);
        ComparisonVerifier.Check(
            EntropyComparison.Pair,
            CompetitorData.FromCloses(Enumerable.Repeat(1d, 499).Append(2).ToArray()),
            500
        );
    }

    [Fact]
    public void ExactGroupsHandleAdjacentTinyWideAndSignedZeroValues()
    {
        var values = new[]
        {
            0d,
            -0d,
            double.Epsilon,
            2 * double.Epsilon,
            double.MaxValue,
            double.BitDecrement(double.MaxValue),
            double.MaxValue,
        };
        var bars = values
            .Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1))
            .ToArray();
        foreach (var period in new[] { 2, 4, int.MaxValue })
            ComparisonVerifier.Compare(
                EntropyComparison.Reference(values, period),
                EntropyComparison.Owned(bars, period),
                "exact frequency groups",
                EntropyComparison.Pair.ErrorBudget!
            );
        Assert.Throws<ArgumentOutOfRangeException>(() => new NormalizedWindowEntropy(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Entropy(1));
    }

    [Fact]
    public void NativeSourceResetRevisionAndHotFlag()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Entropy(source, 4);
        var direct = new QuanTAlib.Entropy(4);
        Assert.Equal(2, direct.WarmupPeriod);
        foreach (var x in new[] { 1d, 1, 2 })
        {
            var input = new QuanTAlib.TValue(x, true, false);
            source.Add(input);
            Assert.Equal(direct.Calc(input).Value, subscribed.Value);
        }
        Assert.False(direct.IsHot);
        Assert.Equal(1, direct.Calc(new QuanTAlib.TValue(1, false, false)).Value);
        Assert.True(direct.Calc(new QuanTAlib.TValue(2, true, false)).IsHot);
        direct.Init();
        Assert.Equal(1, direct.Calc(new QuanTAlib.TValue(9, true, false)).Value);
        Assert.False(direct.IsHot);
    }

    [Fact]
    public void ValueAndPresenceCorruptionAreDetectedOnBothArms()
    {
        var pair = EntropyComparison.Pair;
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                if (presence)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] = 0;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    EntropyComparison.Fixture(),
                    4
                )
            );
        }
    }
}
