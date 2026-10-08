using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class StochasticMomentumComparisonTests
{
    [Theory]
    [InlineData("Raw", 1, 1, 1, 1)]
    [InlineData("Raw", 3, 1, 1, 1)]
    [InlineData("Raw", int.MaxValue, 1, 1, 1)]
    [InlineData("Trady", 1, 1, 1, 1)]
    [InlineData("Skender", 3, 2, 5, 3)]
    [InlineData("Skender", 13, 25, 2, 3)]
    [InlineData("Skender", 1, int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData("Skender", int.MaxValue, 2, 3, 4)]
    public async Task IndependentContractsVerifySeedRecurrencesAndLifecycle(
        string variant,
        int period,
        int first,
        int second,
        int signal
    )
    {
        var sample = StochasticMomentumComparison.Indicator(period, variant, first, second, signal);
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                sample.GetType(),
                $"{variant}/{period}/{first}/{second}/{signal}",
                () => StochasticMomentumComparison.Indicator(period, variant, first, second, signal)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void SeedStartsAtFullWindowWithIndependentPresenceAndNoHiddenPadding()
    {
        var data = TradyExtremaComparison.Fixture();
        foreach (var variant in StochasticMomentumComparison.Variants)
        {
            var pair = StochasticMomentumComparison.Create(variant, 2, 3, 4);
            ComparisonVerifier.Check(pair, data, 3);
            foreach (var output in pair.Ooples(data, 3).Outputs.Values)
            {
                Assert.All(output.Present!.Take(2), Assert.False);
                Assert.All(output.Present!.Skip(2), Assert.True);
            }
        }
        Assert.Equal(
            2.5,
            StochasticMomentumComparison.Create("Raw").Ooples(data, 3).Outputs["Value"].Values[2]
        );
    }

    private sealed class MappedSmi(
        int[] indexes,
        (decimal High, decimal Low, decimal Close)[] prices,
        int period,
        int first,
        int second
    ) : T.StochasticsMomentumIndex<int, decimal?>(indexes, i => prices[i], period, first, second);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TradyTupleGenericIndexedRangesAndRepeatRoutesAgree(bool index)
    {
        var data = TradyExtremaComparison.Fixture();
        var prices = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        var expected = StochasticMomentumComparison.TradyReference(prices, 3, index, 2, 5);
        var tuple = StochasticMomentumComparison.Tuple(prices, 3, index, 2, 5);
        Assert.Equal(expected, tuple.Compute());
        Assert.Equal(expected, tuple.Compute());
        int[] positions = [7, 3, 3, 0, 6];
        Assert.Equal(
            positions.Select(i => expected[i]),
            tuple.Compute((IEnumerable<int>)positions)
        );
        Assert.Equal(expected.Skip(2).Take(4), tuple.Compute(startIndex: 2, endIndex: 5));
        foreach (var i in positions)
            Assert.Equal(expected[i], tuple[i]);
        var all = Enumerable.Range(0, prices.Length).ToArray();
        var mapped = index
            ? new MappedSmi(all, prices, 3, 2, 5).Compute()
            : new T.StochasticsMomentum<int, decimal?>(all, i => prices[i], 3).Compute();
        Assert.Equal(expected, mapped);
        ComparisonVerifier.Check(
            StochasticMomentumComparison.Create(index ? "Trady" : "Raw", 2, 5),
            data,
            3
        );
    }

    private sealed class CustomQuote : IQuote
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    [Fact]
    public void SkenderGenericSortingDefaultAndReusableValueAgree()
    {
        var data = TradyExtremaComparison.Fixture();
        var custom = data
            .Quotes.Select(q => new CustomQuote
            {
                Date = q.Date,
                Open = q.Open,
                High = q.High,
                Low = q.Low,
                Close = q.Close,
                Volume = q.Volume,
            })
            .Reverse()
            .ToArray();
        var expected = data.Quotes.GetSmi(3, 2, 5, 4).ToArray();
        Assert.Equal(
            expected.Select(r => (r.Date, r.Smi, r.Signal)),
            custom.GetSmi(3, 2, 5, 4).Select(r => (r.Date, r.Smi, r.Signal))
        );
        Assert.Equal(
            data.Quotes.GetSmi().Select(r => (r.Smi, r.Signal)),
            data.Quotes.GetSmi(13, 25, 2, 3).Select(r => (r.Smi, r.Signal))
        );
        Assert.Equal(expected[2].Smi, ((IReusableResult)expected[2]).Value);
    }

    [Fact]
    public void FlatRangeRecoveryAndNativeSkenderSignalPoisoningAreExplicit()
    {
        var data = CompetitorData.FromOhlcv(
            [1, 1, 1, 1],
            [1, 2, 1, 2],
            [1, 0, 1, 0],
            [1, 2, 1, 0],
            [1, 1, 1, 1]
        );
        var rows = data.Quotes.GetSmi(1, 1, 1, 3).ToArray();
        Assert.True(double.IsNaN(rows[0].Smi!.Value));
        Assert.Equal(100, rows[1].Smi);
        Assert.Equal(-100, rows[3].Smi);
        Assert.All(rows, r => Assert.True(double.IsNaN(r.Signal!.Value)));
        var pair = StochasticMomentumComparison.Create("Skender", 1, 1, 3);
        Assert.True(pair.Competitor(data, 1).Outputs["Value"].Present![0]);
        Assert.Throws<InvalidOperationException>(() => ComparisonVerifier.Check(pair, data, 1));
        var owned = pair.Ooples(data, 1).Outputs;
        Assert.Equal(new[] { false, true, false, true }, owned["Value"].Present);
        Assert.Equal(100, owned["Signal"].Values[1]);
        Assert.Equal(-100, owned["Signal"].Values[3]);
        ComparisonVerifier.Compare(
            RetrospectivePriceComparison.Series(
                StochasticMomentumComparison.Names("Skender"),
                StochasticMomentumComparison.OwnedReference(
                    data.IndicatorBars,
                    1,
                    "Skender",
                    1,
                    1,
                    3
                )
            ),
            pair.Ooples(data, 1),
            "flat index recovery",
            IndicatorErrorBudget.Exact
        );
        ComparisonVerifier.Check(StochasticMomentumComparison.Create("Trady", 1, 1), data, 1);
        var nativeReference = StochasticMomentumComparison.NativeReference(
            data,
            1,
            "Skender",
            1,
            1,
            3
        );
        Assert.All(nativeReference[1], v => Assert.True(double.IsNaN(v!.Value)));
    }

    [Fact]
    public async Task ExtendedMidpointDisplacementAndRangePreserveFiniteIndices()
    {
        foreach (var magnitude in new[] { double.Epsilon, double.MaxValue })
        {
            Bar[] bars =
            [
                new(DateTime.UnixEpoch, 0, magnitude, -magnitude, magnitude, 1),
                new(DateTime.UnixEpoch.AddDays(1), 0, magnitude, -magnitude, -magnitude, 1),
            ];
            foreach (var variant in StochasticMomentumComparison.Variants)
                ComparisonVerifier.Compare(
                    RetrospectivePriceComparison.Series(
                        StochasticMomentumComparison.Names(variant),
                        StochasticMomentumComparison.OwnedReference(bars, 1, variant, 3, 2, 3)
                    ),
                    StochasticMomentumComparison.Owned(bars, 1, variant, 3, 2, 3),
                    "wide smoothed momentum",
                    IndicatorErrorBudget.Exact
                );
        }
        var oversized = await VolumePriceComparisonTests.Run(
            new DoubleSmoothedStochasticMomentum(1, 1, 1, 1),
            (double.MaxValue, double.MaxValue, double.MaxValue, 1)
        );
        Assert.Equal(0, oversized[2][0]); // Exactly flat even at the upper bound.
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new WindowStochasticMomentum(1),
                (-double.MaxValue, -double.MaxValue, double.MaxValue, 1)
            )
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new DoubleSmoothedStochasticMomentum(1, 1, 1, 1),
                (double.Epsilon, 0, double.MaxValue, 1)
            )
        );
        (decimal High, decimal Low, decimal Close)[] wide =
        [
            (decimal.MaxValue, -decimal.MaxValue, 0),
        ];
        Assert.Equal(
            0m,
            StochasticMomentumComparison.Tuple(wide, 1, false, 1, 1).Compute().Single()
        );
        Assert.Throws<OverflowException>(() =>
            StochasticMomentumComparison.Tuple(wide, 1, true, 1, 1).Compute()
        );
        (decimal High, decimal Low, decimal Close)[] highMidpoint =
        [
            (decimal.MaxValue, decimal.MaxValue, decimal.MaxValue),
        ];
        Assert.Throws<OverflowException>(() =>
            StochasticMomentumComparison.Tuple(highMidpoint, 1, false, 1, 1).Compute()
        );
        Assert.Equal(
            new decimal?[] { 0 },
            StochasticMomentumComparison.TradyReference(wide, 1, false, 1, 1)
        );
        Bar[] oversizedDelta =
        [
            new(DateTime.UnixEpoch, 0, -double.MaxValue / 2, -double.MaxValue, double.MaxValue, 1),
        ];
        ComparisonVerifier.Compare(
            RetrospectivePriceComparison.Series(
                StochasticMomentumComparison.Names("Skender"),
                StochasticMomentumComparison.OwnedReference(oversizedDelta, 1, "Skender", 1, 1, 1)
            ),
            StochasticMomentumComparison.Owned(oversizedDelta, 1, "Skender", 1, 1, 1),
            "unpublished oversized displacement",
            IndicatorErrorBudget.Exact
        );
        Assert.True(
            StochasticMomentumComparison
                .Owned(oversizedDelta, 1, "Skender", 1, 1, 1)
                .Outputs["Value"]
                .Values[0] > 600
        );
    }

    [Fact]
    public void DecimalHalfRangeUnderflowAndLongDecayHaveIndependentReferences()
    {
        var tiny = CompetitorData.FromOhlcv([0], [1e-28], [0], [1e-28], [1]);
        var pair = StochasticMomentumComparison.Create("Trady", 1, 1);
        ComparisonVerifier.Check(pair, tiny, 1);
        Assert.False(pair.Competitor(tiny, 1).Outputs["Value"].Present![0]);
        Assert.True(pair.Ooples(tiny, 1).Outputs["Value"].Present![0]);
        var bars = new[] { new Bar(DateTime.UnixEpoch, 0, 1, 0, 1, 1) }
            .Concat(
                Enumerable
                    .Range(1, 1500)
                    .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 0, 0, 0, 0, 1))
            )
            .ToArray();
        ComparisonVerifier.Compare(
            RetrospectivePriceComparison.Series(
                StochasticMomentumComparison.Names("Skender"),
                StochasticMomentumComparison.OwnedReference(bars, 1, "Skender", 3, 3, 3)
            ),
            StochasticMomentumComparison.Owned(bars, 1, "Skender", 3, 3, 3),
            "long-decay binary rounding",
            IndicatorErrorBudget.Exact
        );
    }

    [Fact]
    public void ParameterBoundariesRejectOwnedInvalidPeriodsAndPinNativeDifferences()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowStochasticMomentum(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleSmoothedStochasticMomentum(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DoubleSmoothedStochasticMomentum(1, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DoubleSmoothedStochasticMomentum(1, 1, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DoubleSmoothedStochasticMomentum(1, 1, 1, 0)
        );
        var data = TradyExtremaComparison.Fixture();
        foreach (var config in new[] { (0, 1, 1, 1), (1, 0, 1, 1), (1, 1, 0, 1), (1, 1, 1, 0) })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                data.Quotes.GetSmi(config.Item1, config.Item2, config.Item3, config.Item4).ToArray()
            );
        Assert.Throws<OverflowException>(() => data.Quotes.GetSmi(1, int.MaxValue, 1, 1).ToArray());
        var prices = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        Assert.All(
            StochasticMomentumComparison.Tuple(prices, 0, false, 1, 1).Compute(),
            v => Assert.Null(v)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StochasticMomentumComparison.Tuple(prices, 0, true, 1, 1).Compute()
        );
        Assert.Throws<DivideByZeroException>(() =>
            StochasticMomentumComparison.Tuple(prices, 2, true, -1, 1).Compute()
        );
        Assert.Equal(
            StochasticMomentumComparison.TradyReference(prices, 2, true, 0, 1),
            StochasticMomentumComparison.Tuple(prices, 2, true, 0, 1).Compute()
        );
    }

    [Fact]
    public async Task IncomingAndOutgoingChainingRetainCandleRanges()
    {
        var data = TradyExtremaComparison.Fixture();
        foreach (var variant in new[] { "Raw", "Skender" })
        {
            var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
            var indicator = StochasticMomentumComparison.Indicator(3, variant, 2, 3, 4);
            ((MultiOutputIndicatorBase)indicator).Of(source);
            var next = new FirstValueEma(1);
            next.Of(indicator);
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(source, indicator, next)
                .BuildAsync();
            var prices = run[source.Value].ToArray();
            var bars = data
                .IndicatorBars.Select(
                    (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)
                )
                .ToArray();
            var expected = StochasticMomentumComparison
                .OwnedReference(bars, 3, variant, 2, 3, 4)[0]
                .Select(v => v ?? 0)
                .ToArray();
            Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
            Assert.Equal(expected, run[next.Value].ToArray());
        }
    }

    [Fact]
    public void EveryIndexSignalAndPresenceDetectsCorruption()
    {
        var data = TradyExtremaComparison.Fixture();
        foreach (var pair in StochasticMomentumComparison.Pairs)
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int period)
            {
                var result = native ? pair.Competitor(d, period) : pair.Ooples(d, period);
                var output = result.Outputs[name];
                if (presence)
                    output.Present![4] = false;
                else
                    output.Values[4] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    3
                )
            );
        }
    }
}
