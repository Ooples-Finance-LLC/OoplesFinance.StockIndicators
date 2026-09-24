using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed partial class SharedIndicatorValidationTests
{
    [Fact]
    public void CustomerMultiSeriesStatesAreDiscoveredAndReceivePairedFixtures()
    {
        var cases = IndicatorValidationDiscovery.DiscoverMultiSeries(new[] { typeof(CustomerSpread).Assembly });
        var testCase = Assert.Single(cases.Where(c => c.IndicatorType == typeof(CustomerSpread)));
        var report = MultiSeriesIndicatorValidation.Validate(testCase, new() { RequireFormulaReference = true });
        report.ThrowIfInvalid();
        Assert.True(report.FixturesCompleted >= 50);
        Assert.True(report.FormulaCoverage!.IsComplete);
    }

    [Fact]
    public void CustomerMultiSeriesFormulaErrorsThrowAndMissingContractsCannotPassRequiredCoverage()
    {
        var testCase = new MultiSeriesIndicatorValidationCase(typeof(CustomerSpread), "broken",
            (primary, market) => new CustomerSpread(primary, market, true), new[] { "Spread" }, CustomerSpread.Reference);
        var report = MultiSeriesIndicatorValidation.Validate(testCase, new() { RequireFormulaReference = true });
        Assert.Contains(report.Failures, failure => failure.Message.Contains("expected"));
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
        var missing = new MultiSeriesIndicatorValidationCase(typeof(CustomerSpread), "no-reference",
            (primary, market) => new CustomerSpread(primary, market), new[] { "Spread" });
        Assert.Contains(MultiSeriesIndicatorValidation.Validate(missing, new() { RequireFormulaReference = true }).Failures,
            failure => failure.Rule == "FormulaReference");
    }

    [Fact]
    public void PairedReplayFixturesCheckCustomerDomainRejectionAndRecovery()
    {
        var cases = IndicatorValidationDiscovery.DiscoverMultiSeries(new[] { typeof(CustomerSpread).Assembly });
        var discovered = Assert.Single(cases.Where(c => c.IndicatorType == typeof(CustomerSpread)));
        Assert.Same(IndicatorInputDomain.PositiveClose, discovered.PrimaryDomain);
        var fixture = new IndicatorValidationFixture("invalid-close-replay", new[]
        {
            new Bar(new DateTime(2024, 1, 1), 1, 1, 1, -1, 1),
            new Bar(new DateTime(2024, 1, 2), 1, 1, 1, double.NaN, 1),
            new Bar(new DateTime(2024, 1, 3), 1, 1, 1, 4, 1)
        });
        var baseline = MultiSeriesIndicatorValidation.Validate(discovered);
        var options = new IndicatorValidationOptions { AdditionalFixtures = new[] { fixture } };
        var report = MultiSeriesIndicatorValidation.Validate(discovered, options);
        report.ThrowIfInvalid();
        Assert.Equal(baseline.FixturesCompleted + 5, report.FixturesCompleted);
        Assert.Equal(report.FixturesCompleted, report.FixtureEvidence.Count);
        Assert.Equal(report.ValuesChecked, report.FixtureEvidence.Sum(f => f.ValuesChecked));
        Assert.Equal(report.InputRejectionsChecked, report.FixtureEvidence.Sum(f => f.InputRejectionsChecked));
        Assert.All(report.FixtureEvidence, f => { Assert.True(f.Completed); Assert.True(f.Passed); });
        Assert.Contains(report.FixtureEvidence, f => f.Name == "negative-price/identical" && f.InputRejectionsChecked > 0);
        // Five benchmark shapes, seventeen probes per positive-close domain on each
        // side, plus seventeen invalid observations in the supplied fixture shapes.
        Assert.Equal(baseline.InputRejectionsChecked + 5 * 34 + 17, report.InputRejectionsChecked);
        var corrupt = new MultiSeriesIndicatorValidationCase(typeof(CustomerSpread), "mutates-before-rejection",
            (p, b) => new CustomerSpread(p, b, corruptRejection: true), new[] { "Spread" }, CustomerSpread.Reference,
            primaryDomain: IndicatorInputDomain.PositiveClose, benchmarkDomain: IndicatorInputDomain.PositiveClose);
        var corruptReport = MultiSeriesIndicatorValidation.Validate(corrupt, options);
        Assert.False(corruptReport.IsValid);
        Assert.Contains(corruptReport.FixtureEvidence, f => !f.Passed);
        options.AdditionalFixtures = new[] { fixture, fixture };
        Assert.Throws<ArgumentException>(() => MultiSeriesIndicatorValidation.Validate(discovered, options));
        options.AdditionalFixtures = new[] { new IndicatorValidationFixture("too-large", Enumerable.Repeat(fixture.Bars[0], 8193)) };
        Assert.Throws<ArgumentException>(() => MultiSeriesIndicatorValidation.Validate(discovered, options));
    }

    public sealed class CustomerSpread : IMultiSeriesIndicatorState, IMultiSeriesIndicatorValidationContract, IMultiSeriesInputDomainContract
    {
        private readonly SeriesKey _primary, _market;
        private readonly bool _broken;
        private readonly bool _corruptRejection;
        private double _damage;
        private double _benchmark;
        public CustomerSpread(SeriesKey primary, SeriesKey market, bool broken = false, bool corruptRejection = false)
        { _primary = primary; _market = market; _broken = broken; _corruptRejection = corruptRejection; }
        public IndicatorInputDomain PrimaryInputDomain => IndicatorInputDomain.PositiveClose;
        public IndicatorInputDomain BenchmarkInputDomain => IndicatorInputDomain.PositiveClose;
        public IndicatorName Name => IndicatorName.None;
        public IReadOnlyList<string> ValidationOutputKeys => new[] { "Spread" };
        public MultiSeriesFormulaReference FormulaReference => Reference;
        public static IReadOnlyDictionary<string, IReadOnlyList<double>> Reference(IReadOnlyList<Bar> primary, IReadOnlyList<Bar> benchmark)
            => new Dictionary<string, IReadOnlyList<double>> { ["Spread"] = primary.Select((bar, i) => bar.Close-benchmark[i].Close).ToArray() };
        public void Reset() { _benchmark = 0; _damage = 0; }
        public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar, bool isFinal, bool includeOutputs)
        {
            var input = new Bar(bar.EndTime, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume);
            if (PrimaryInputDomain.Violation(input) is not null)
            {
                if (_corruptRejection) _damage++;
                PrimaryInputDomain.Validate(input);
            }
            if (series.Equals(_market)) { if (isFinal) _benchmark = bar.Close; return new(false, 0, null); }
            if (!series.Equals(_primary)) return new(false, 0, null);
            var value = bar.Close-_benchmark+(_broken ? 1 : 0)+_damage;
            return new(true, value, includeOutputs ? new Dictionary<string, double> { ["Spread"] = value } : null);
        }
    }
}
