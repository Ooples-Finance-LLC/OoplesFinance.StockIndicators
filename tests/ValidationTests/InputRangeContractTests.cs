using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using SeriesKey = OoplesFinance.StockIndicators.Streaming.SeriesKey;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class InputRangeContractTests
{
    [Fact]
    public void RangeIntersectionsAreImmutableAndRejectImpossibleDomains()
    {
        var domain = IndicatorInputDomain.Finite.WithRange(IndicatorInputFields.Prices, -1, 1);
        var narrowed = domain.WithRange(IndicatorInputFields.Close, -.01, .01);
        Assert.Empty(IndicatorInputDomain.Finite.Ranges);
        Assert.Equal(1, domain.Ranges.Single(r => r.Field == IndicatorInputFields.Close).Maximum);
        Assert.Equal(.01, narrowed.Ranges.Single(r => r.Field == IndicatorInputFields.Close).Maximum);
        Assert.Throws<ArgumentException>(() => narrowed.WithRange(IndicatorInputFields.Close, 2, 3));
        Assert.Throws<ArgumentException>(() => IndicatorInputDomain.PositiveClose.WithRange(IndicatorInputFields.Close, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => domain.WithRange(IndicatorInputFields.None, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => domain.WithRange(IndicatorInputFields.Close, double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => domain.WithRange(IndicatorInputFields.Close, 0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => domain.WithRange(IndicatorInputFields.Close, 2, 1));
    }

    [Fact]
    public void EveryInvalidProbeIsolatesOneFieldAndBoundaryProbesStayInsideTheDomain()
    {
        var domain = IndicatorInputDomain.Finite.WithRange(IndicatorInputFields.All, -double.Epsilon, double.Epsilon);
        var invalid = domain.InvalidExamples().ToArray();
        Assert.Equal(25, invalid.Length);
        foreach (var bar in invalid)
        {
            var fields = new[] { bar.Open, bar.High, bar.Low, bar.Close, bar.Volume };
            Assert.Single(fields.Where(v => double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > double.Epsilon));
            Assert.Throws<ArgumentOutOfRangeException>(() => domain.Validate(bar));
        }
        var fixture = Assert.Single(domain.BoundaryFixtures(8));
        Assert.All(fixture.Bars, bar => Assert.Null(domain.Violation(bar)));
        Assert.Contains(fixture.Bars, bar => bar.Close == -double.Epsilon);
        Assert.Contains(fixture.Bars, bar => bar.Close == double.Epsilon);
    }

    [Fact]
    public async Task CustomerRangesReceiveValidRecoveryAndBoundaryTrajectoriesAutomatically()
    {
        var report = await IndicatorValidation.ValidateAsync(new(typeof(BoundedIdentity), "bounded", () => new BoundedIdentity()));
        report.ThrowIfInvalid();
        Assert.Contains(report.FixtureEvidence, f => f.Name == "declared-domain/alternating-boundaries" && f.Passed);
        var bar = new Bar(DateTime.UnixEpoch, 0, 0, 0, 1, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { bar })).ConfigureIndicators(new BoundedIdentity()).BuildAsync());
    }

    [Fact]
    public void PairedRecoveryRespectsDifferentMagnitudeDomains()
    {
        var primary = IndicatorInputDomain.Finite.WithRange(IndicatorInputFields.All, -.01, .01);
        var market = IndicatorInputDomain.Finite.WithRange(IndicatorInputFields.All, 2, 3);
        var report = MultiSeriesIndicatorValidation.Validate(new(typeof(BoundedPair), "bounded-pair",
            (p, m) => new BoundedPair(p, m, primary, market), new[] { "Spread" },
            SharedIndicatorValidationTests.CustomerSpread.Reference, primaryDomain: primary, benchmarkDomain: market));
        report.ThrowIfInvalid();
        Assert.Contains(report.FixtureEvidence, f => f.Name.StartsWith("primary-domain/") && f.Passed);
        Assert.Contains(report.FixtureEvidence, f => f.Name.StartsWith("benchmark-domain/") && f.Passed);
    }

    public sealed class BoundedIdentity : SharedIndicatorValidationTests.Identity, IIndicatorInputDomainContract
    {
        private static readonly IndicatorInputDomain Domain = IndicatorInputDomain.Finite
            .WithRange(IndicatorInputFields.Prices, -.01, .01).WithRange(IndicatorInputFields.Volume, 0, 2);
        public IndicatorInputDomain InputDomain => Domain;
    }

    private sealed class BoundedPair(SeriesKey primary, SeriesKey market,
        IndicatorInputDomain primaryDomain, IndicatorInputDomain marketDomain) : IMultiSeriesIndicatorState
    {
        private double _benchmark;
        public IndicatorName Name => IndicatorName.None;
        public void Reset() => _benchmark = 0;
        public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series,
            OhlcvBar bar, bool isFinal, bool includeOutputs)
        {
            var domain = series.Equals(market) ? marketDomain : primaryDomain;
            domain.Validate(new Bar(bar.EndTime, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume));
            if (series.Equals(market)) { if (isFinal) _benchmark = bar.Close; return new(false, 0, null); }
            if (!series.Equals(primary)) return new(false, 0, null);
            var value = bar.Close - _benchmark;
            return new(true, value, includeOutputs ? new Dictionary<string, double> { ["Spread"] = value } : null);
        }
    }
}
