using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CpuBuilderBenchmarkTests
{
    public static IEnumerable<object[]> Cases => CpuBuilderWorkload.PairIds.Select(id => new object[] { id });
    [Theory, MemberData(nameof(Cases))]
    public void BuilderOutputsMatchExistingVerifiedPublicContracts(string id)
    {
        var work = new CpuNativeWorkload(id, 160, commonGrid: true);
        CpuBuilderWorkload.Verify(work);
        CpuBuilderWorkload.Verify(work); // No state retained between fresh builder runs.
        var pair = ComparisonPairs.Get(CpuNativeWorkload.CanonicalPair(id));
        ComparisonVerifier.Check(pair, work.Data, 20, verifyIsolation: false);
        var expected = pair.Competitor(work.Data, 20);
        ComparisonVerifier.Compare(expected, work.Normalize(work.NativeOwned()), id + " direct native", pair.ErrorBudget);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(40)]
    [InlineData(41)]
    public async Task RetrospectiveFractalsPreservePresenceAndCenterPlacement(int count)
    {
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 0,
            i == 20 ? 10 : 0, i == 20 ? -10 : 0, 0, 0)).ToArray();
        var indicator = new RetrospectiveFractals(20, 20);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        for (var i = 0; i < count; i++)
        {
            var present = count == 41 && i == 20;
            Assert.Equal(present ? 10 : 0, run[indicator.Bear][i]);
            Assert.Equal(present ? -10 : 0, run[indicator.Bull][i]);
            Assert.Equal(present ? 1 : 0, run[indicator.BearIsDefined][i]);
            Assert.Equal(present ? 1 : 0, run[indicator.BullIsDefined][i]);
        }
    }

    [Fact]
    public async Task RetrospectiveFractalsRejectLiveSources()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.Live()).ConfigureIndicators(new RetrospectiveFractals()).BuildAsync());
    }

    [Fact]
    public async Task FractalsCanReadAChainedSourceWithoutChangingCenterPlacement()
    {
        var bars = Enumerable.Range(0, 20).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 1, 100, -100, i % 5, 0)).ToArray();
        var source = new Sma(1);
        var fractal = new RetrospectiveFractals(2, 2, true);
        fractal.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(fractal).BuildAsync();
        var expected = FractalSnapshot.Calculate(bars, 2, 2, true);
        for (var i = 0; i < bars.Length; i++)
        {
            Assert.Equal(expected[i].Bear ?? 0, run[fractal.Bear][i]);
            Assert.Equal(expected[i].Bull ?? 0, run[fractal.Bull][i]);
        }
    }

    [Theory]
    [InlineData(PivotLevelStyle.Standard)]
    [InlineData(PivotLevelStyle.Camarilla)]
    [InlineData(PivotLevelStyle.Demark)]
    [InlineData(PivotLevelStyle.Fibonacci)]
    [InlineData(PivotLevelStyle.Woodie)]
    public async Task PivotStylesAndOffsetsPreserveAllPresenceFlags(PivotLevelStyle style)
    {
        var work = new CpuNativeWorkload("Skender.GetRollingPivots", 80);
        var indicator = new RollingPivotLevels(7, 3, style);
        using var run = await CpuBuilderWorkload.Build(work, indicator);
        var expected = PivotLevelSnapshots.Rolling(work.Data.IndicatorBars, 7, 3, style);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            double?[] levels = [e.PP, e.S1, e.S2, e.S3, e.S4, e.R1, e.R2, e.R3, e.R4];
            for (var slot = 0; slot < 9; slot++)
            {
                Assert.Equal(levels[slot] ?? 0, run[indicator.Outputs[slot]][i]);
                Assert.Equal(levels[slot].HasValue ? 1 : 0, run[indicator.IsDefined(indicator.Outputs[slot])][i]);
            }
        }
        Assert.Throws<ArgumentException>(() => indicator.IsDefined(new RollingPivotLevels().PP));
        Assert.Throws<ArgumentException>(() => indicator.IsDefined(indicator.IsDefined(indicator.PP)));
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(7, -100, 3)]
    [InlineData(20, 100, 10)]
    public async Task JurikBuilderParametersAndStateLifecycleMatchSnapshot(int period, double phase, int volatility)
    {
        var work = new CpuNativeWorkload("QuanTAlib.Jma", 90);
        var indicator = new JurikAdaptive(period, phase, volatility);
        var expected = JurikAdaptiveSnapshot.Calculate(work.Data.IndicatorBars, period, phase, volatility);
        using var first = await CpuBuilderWorkload.Build(work, indicator);
        using var second = await CpuBuilderWorkload.Build(work, indicator);
        Assert.Equal(expected, first[indicator].ToArray());
        Assert.Equal(expected, second[indicator].ToArray());
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var feed = Bars.Live();
            using var live = await new StockIndicatorBuilder().ConfigureSource(feed).ConfigureIndicators(indicator).BuildAsync();
            foreach (var bar in work.Data.IndicatorBars) Assert.True(feed.Publish(bar));
            feed.Complete();
            var actual = new List<double>();
            await foreach (var snapshot in live) actual.Add(snapshot[indicator]);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void InvalidNewIndicatorConfigurationsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JurikAdaptive(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JurikAdaptive(20, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JurikAdaptive(20, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingPivotLevels(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingPivotLevels(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingPivotLevels(1, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingPivotLevels(1, 0, (PivotLevelStyle)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetrospectiveFractals(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetrospectiveFractals(2, 1));
    }

    [Theory]
    [InlineData("QuanTAlib.Jma")]
    [InlineData("Skender.GetRollingPivots")]
    [InlineData("Skender.GetFractal")]
    public async Task BuilderRejectsNonfiniteBarsForNewIntegrations(string id)
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 0, 1, -1, double.NaN, 0) };
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(CpuBuilderWorkload.Create(id)).BuildAsync());
    }
}
