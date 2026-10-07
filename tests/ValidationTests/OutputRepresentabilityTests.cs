using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class OutputRepresentabilityTests
{
    [Fact]
    public async Task ProvenSignedOverflowRequiresRejectionAndChecksTheFinitePrefix()
    {
        var report = await Validate(0);
        report.ThrowIfInvalid();
        Assert.True(report.ValuesChecked > 0);
        Assert.True(report.OutputOverflowRejectionsChecked > 0);
        var receipt = Assert.Single(report.FixtureEvidence, f => f.Name == "overflow-after-prefix");
        Assert.Equal(3, receipt.InputBars);
        Assert.Equal(2, receipt.ValuesChecked);
        Assert.Equal(2, receipt.OutputOverflowRejectionsChecked);
        Assert.Equal(2, receipt.OutputOverflowBarIndex);
        Assert.Equal(0, receipt.OutputOverflowSlot);
        Assert.Equal(1, receipt.OutputOverflowSign);
        Assert.Equal(new[] { 0 }, report.FormulaCoverage!.OutputOverflowReferenceSlots);
        Assert.True(receipt.Passed);
        Assert.Equal(report.OutputOverflowRejectionsChecked, report.FixtureEvidence.Sum(f => f.OutputOverflowRejectionsChecked));
    }

    [Theory]
    [InlineData(1)] // silently clamp the unrepresentable output
    [InlineData(2)] // fail before the proven bar
    [InlineData(3)] // wrong overflow sign
    [InlineData(4)] // unrelated exception
    [InlineData(5)] // incorrect representable prefix
    [InlineData(6)] // NaN instead of signed overflow
    public async Task ADeclaredOverflowCannotHideAnImplementationFault(int defect)
    {
        var report = await Validate(defect);
        Assert.False(report.IsValid);
        Assert.NotEmpty(report.Failures);
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
    }

    [Fact]
    public async Task OrdinaryReferenceDoesNotAuthorizeOverflowRejection()
    {
        var report = await Validate(0, ordinary: true);
        Assert.False(report.IsValid);
        Assert.Equal(0, report.OutputOverflowRejectionsChecked);
    }

    [Fact]
    public async Task ConflictingReferencesCannotAuthorizeOverflowRejection()
    {
        var testCase = new IndicatorValidationCase(typeof(Doubler), "conflicting", () => new Doubler(0),
            IndicatorValidationRule.Reference(0, bars => bars.Select(b => 0d).ToArray(), IndicatorErrorBudget.Exact));
        var report = await IndicatorValidation.ValidateAsync(testCase);
        Assert.False(report.IsValid);
        Assert.Contains(report.Failures, f => f.Message.Contains("unambiguous"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionMustNameTheProvenOutputSlot(bool wrongSlot)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(TwoOutputs),
            "output-slot", () => new TwoOutputs(wrongSlot)));
        Assert.Equal(!wrongSlot, report.IsValid);
        if (!wrongSlot) Assert.True(report.OutputOverflowRejectionsChecked > 0);
    }

    public sealed class TwoOutputs(bool wrongSlot) : MultiOutputIndicatorBase(2), IIndicatorValidationContract
    {
        public IEnumerable<IndicatorValidationRule> ValidationRules => new[] {
            IndicatorValidationRule.Reference(0, bars => bars.Select(b => b.Close).ToArray(), IndicatorErrorBudget.Exact),
            IndicatorValidationRule.ReferenceWithOverflowRejection(1, bars => bars.Select(b =>
                (ReferenceFraction.FromDouble(b.Close) * new ReferenceFraction(2)).ToDouble()).ToArray(), IndicatorErrorBudget.Exact)
        };
        protected internal override object CreateState() => new State(wrongSlot);
        private sealed class State(bool wrongSlot) : IMultiOutputState
        {
            public void Reset() { }
            public void Update(in Bar bar, Span<double> outputs)
            {
                var value = 2 * bar.Close;
                outputs[0] = wrongSlot && double.IsInfinity(value) ? value : bar.Close;
                outputs[1] = wrongSlot && double.IsInfinity(value) ? 0 : value;
            }
        }
    }

    private static Task<IndicatorValidationReport> Validate(int defect, bool ordinary = false)
    {
        var bars = new[] { 1d, 2d, double.MaxValue }.Select((value, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1)).ToArray();
        return IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(Doubler), "overflow", () => new Doubler(defect, ordinary)),
            new IndicatorValidationOptions { AdditionalFixtures = new[] { new IndicatorValidationFixture("overflow-after-prefix", bars) } });
    }

    public sealed class Doubler(int defect, bool ordinary = false) : IndicatorBase, IIndicatorValidationContract
    {
        public IEnumerable<IndicatorValidationRule> ValidationRules
        {
            get
            {
                IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars) => bars.Select(b =>
                    (ReferenceFraction.FromDouble(b.Close) * new ReferenceFraction(2)).ToDouble()).ToArray();
                yield return ordinary ? IndicatorValidationRule.Reference(0, Reference, IndicatorErrorBudget.Exact)
                    : IndicatorValidationRule.ReferenceWithOverflowRejection(0, Reference, IndicatorErrorBudget.Exact);
            }
        }
        protected internal override object CreateState() => new State(defect);
        private sealed class State(int defect) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar)
            {
                var value = 2 * bar.Close;
                if (defect == 2) return double.PositiveInfinity;
                if (!double.IsInfinity(value)) return defect == 5 ? 0 : value;
                return defect switch {
                    1 => 0,
                    3 => -value,
                    4 => throw new InvalidOperationException("unrelated failure"),
                    6 => double.NaN,
                    _ => value
                };
            }
        }
    }
}
