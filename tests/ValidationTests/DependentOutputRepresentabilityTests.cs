using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DependentOutputRepresentabilityTests
{
    [Fact]
    public async Task ProvenUpstreamOverflowRequiresExactDependentRejection()
    {
        var report = await Validate(0);
        report.ThrowIfInvalid();
        var evidence = Assert.Single(report.FixtureEvidence, f => f.Name == "dependent-overflow");
        Assert.Equal(2, evidence.ValuesChecked);
        Assert.Equal(2, evidence.OutputOverflowRejectionsChecked);
        Assert.Equal(2, evidence.OutputOverflowBarIndex);
        Assert.Equal(0, evidence.OutputOverflowSlot);
        Assert.Null(evidence.OutputOverflowSign);
    }

    [Theory]
    [InlineData(1)] // accept a finite substitute
    [InlineData(2)] // reject a finite prefix
    [InlineData(3)] // reject infinity instead of the proven NaN
    [InlineData(4)] // corrupt the representable prefix
    [InlineData(5)] // throw an unrelated exception
    [InlineData(6)] // no independent upstream overflow
    [InlineData(7)] // overflow proof at a different bar
    [InlineData(8)] // malformed source reference
    public async Task DependentRejectionCannotHideImplementationOrProofFaults(int defect)
    {
        var report = await Validate(defect);
        Assert.False(report.IsValid);
        Assert.NotEmpty(report.Failures);
    }

    private static Task<IndicatorValidationReport> Validate(int defect)
    {
        var bars = new[] { 1d, 2d, double.MaxValue }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        return IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(Dependent), "dependent", () => new Dependent(defect)),
            new IndicatorValidationOptions { AdditionalFixtures = [new IndicatorValidationFixture("dependent-overflow", bars)] });
    }

    public sealed class Dependent(int defect) : IndicatorBase, IIndicatorValidationContract
    {
        private static double Source(double value) =>
            (ReferenceFraction.FromDouble(value) * new ReferenceFraction(2)).ToDouble();
        public IEnumerable<IndicatorValidationRule> ValidationRules
        {
            get
            {
                IReadOnlyList<double> Output(IReadOnlyList<Bar> bars) => bars.Select(b =>
                    double.IsInfinity(Source(b.Close)) ? double.NaN : Source(b.Close)).ToArray();
                IReadOnlyList<double> Upstream(IReadOnlyList<Bar> bars)
                {
                    var source = bars.Select(b => Source(b.Close)).ToArray();
                    if (defect == 6) return source.Select(_ => 0d).ToArray();
                    if (defect == 7) return new[] { 0d }.Concat(source).Take(source.Length).ToArray();
                    if (defect == 8) return [];
                    return source;
                }
                yield return IndicatorValidationRule.ReferenceWithDependentOverflowRejection(0, Output, Upstream, IndicatorErrorBudget.Exact);
            }
        }
        protected internal override object CreateState() => new State(defect);
        private sealed class State(int defect) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar)
            {
                var source = 2 * bar.Close;
                if (defect == 2) return double.NaN;
                if (!double.IsInfinity(source)) return defect == 4 ? 0 : source;
                return defect switch {
                    1 => 0,
                    3 => source,
                    5 => throw new InvalidOperationException("unrelated failure"),
                    _ => double.NaN
                };
            }
        }
    }
}
