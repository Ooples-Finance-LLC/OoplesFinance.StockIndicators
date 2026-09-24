using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SingleInputContractTests
{
    private static Bar At(int index, double close) => new(new DateTime(2024, 1, 1).AddMinutes(index), close, close, close, close, 1000);

    [Theory]
    [InlineData(0d)]
    [InlineData(-100d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task AbsoluteStrengthRejectsUndefinedPriceRatiosBeforeAdvancingState(double invalid)
    {
        Assert.Same(IndicatorInputDomain.PositiveClose, IndicatorInputDomain.For(new AbsoluteStrengthIndex()));
        var bars = new[] { At(0, 100), At(1, invalid) };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(new AbsoluteStrengthIndex()).BuildAsync());
        var stock = new OoplesFinance.StockIndicators.Models.StockData(bars.Select(b => b.Open).ToList(),
            bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
        Assert.Throws<ArgumentOutOfRangeException>(() => stock.CalculateAbsoluteStrengthIndex());
        var actual = new OoplesFinance.StockIndicators.Streaming.AbsoluteStrengthIndexState();
        var expected = new OoplesFinance.StockIndicators.Streaming.AbsoluteStrengthIndexState();
        for (var i = 0; i < 8; i++)
        {
            var b = At(i, i == 4 ? invalid : 100 + i);
            var input = new OoplesFinance.StockIndicators.Streaming.OhlcvBar("TEST",
                OoplesFinance.StockIndicators.Streaming.BarTimeframe.Minutes(1), b.Time, b.Time.AddMinutes(1),
                b.Open, b.High, b.Low, b.Close, b.Volume, true);
            if (i == 4)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(input, false, true));
                Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(input, true, true));
            }
            else Assert.Equal(expected.Update(input, true, true).Value, actual.Update(input, true, true).Value);
        }
    }

    [Fact]
    public async Task DiscoveryChecksCustomerDomainsWithGeneratedInvalidFields()
    {
        var testCase = Assert.Single(IndicatorValidationDiscovery.Discover(new[] { typeof(PositiveIdentity).Assembly })
            .Where(c => c.IndicatorType == typeof(PositiveIdentity)));
        var report = await IndicatorValidation.ValidateAsync(testCase, new()
        {
            AdditionalFixtures = new[] { new IndicatorValidationFixture("signed-input", new[] { At(0, -2), At(1, 0), At(2, 4) }) }
        });
        report.ThrowIfInvalid();
        // 15 nonfinite fields, zero and negative domain probes, two supplied
        // invalid bars, and the built-in zero/negative fixtures. Count adversarial
        // rejections from their inputs so every newly generated class is included.
        var generatedRejections = IndicatorAdversarialCases.Generate(256, 244)
            .Sum(f => f.Bars.Count(b => b.Close <= 0));
        Assert.Equal(19 + 2 * 256 + generatedRejections, report.InputRejectionsChecked);
        var zero = Assert.Single(report.FixtureEvidence.Where(f => f.Name == "zero-price"));
        Assert.Equal(256, zero.InputRejectionsChecked);
        Assert.Equal(256, zero.ValuesChecked);
        Assert.True(zero.Passed);
    }

    [Fact]
    public async Task InvalidRawInputIsRejectedBeforeAnyCustomerStateAdvancesAndCanRecover()
    {
        var feed = Bars.Live();
        var first = new CountingIdentity();
        var positive = new PositiveIdentity();
        using var run = await new StockIndicatorBuilder().ConfigureSource(feed).ConfigureIndicators(first, positive).BuildAsync();
        feed.Publish(At(0, -1));
        await using (var read = run.GetAsyncEnumerator())
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await read.MoveNextAsync());
        Assert.Equal(0, first.Updates);
        Assert.Equal(0, run.BarCount);
        feed.Publish(At(1, 3));
        feed.Complete();
        await foreach (var snapshot in run) Assert.Equal(3, snapshot[positive]);
        Assert.Equal(1, first.Updates);
        Assert.Equal(1, run.BarCount);
    }

    [Fact]
    public async Task DerivedDomainFailurePublishesNoPartialHistoryAndRequiresFreshReplay()
    {
        var feed = Bars.Live();
        var first = new CountingIdentity(negate: true);
        var positive = new PositiveIdentity();
        positive.Of(first);
        using var run = await new StockIndicatorBuilder().ConfigureSource(feed).ConfigureIndicators(first, positive).BuildAsync();
        feed.Publish(At(0, 2));
        await using (var read = run.GetAsyncEnumerator())
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await read.MoveNextAsync());
        Assert.Equal(1, first.Updates);
        Assert.Equal(0, run.BarCount);
        Assert.Empty(run[first].ToArray());
        Assert.Empty(run[positive].ToArray());
        feed.Publish(At(1, 3));
        feed.Complete();
        await using var retry = run.GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await retry.MoveNextAsync());
        Assert.Contains("fresh run", error.Message);
        Assert.Equal(1, first.Updates);
    }

    [Fact]
    public async Task FiniteSourceRejectsNonfiniteBarsAndValidatesDerivedCloses()
    {
        var state = new CountingIdentity();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { At(0, 1), At(1, double.NaN) })).ConfigureIndicators(state).BuildAsync());
        Assert.Equal(0, state.Updates);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { At(0, -1) })).ConfigureIndicators(state, new PositiveIdentity()).BuildAsync());
        Assert.Equal(0, state.Updates);
        var negative = new CountingIdentity(negate: true);
        var positive = new PositiveIdentity();
        positive.Of(negative);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { At(0, 1) })).ConfigureIndicators(positive).BuildAsync());
    }

    public sealed class PositiveIdentity : IndicatorBase, IIndicatorInputDomainContract, IIndicatorValidationContract
    {
        public IndicatorInputDomain InputDomain => IndicatorInputDomain.PositiveClose;
        public IEnumerable<IndicatorValidationRule> ValidationRules => new[]
        { IndicatorValidationRule.Reference(0, bars => bars.Select(b => b.Close).ToArray(), IndicatorErrorBudget.Exact) };
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public double Update(in Bar bar) => bar.Close;
            public void Reset() { }
        }
    }

    private sealed class CountingIdentity(bool negate = false) : IndicatorBase
    {
        public int Updates { get; private set; }
        protected internal override object CreateState() => new State(this, negate);
        private sealed class State(CountingIdentity owner, bool negate) : IIndicatorState
        {
            public double Update(in Bar bar) { owner.Updates++; return negate ? -bar.Close : bar.Close; }
            public void Reset() => owner.Updates = 0;
        }
    }
}
