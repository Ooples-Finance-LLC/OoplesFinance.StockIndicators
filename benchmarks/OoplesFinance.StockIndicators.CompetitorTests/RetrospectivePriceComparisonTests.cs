using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RetrospectivePriceComparisonTests
{
    private static Bar[] Bars(double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(int.MaxValue)]
    public void DpoAlignmentUsesFutureWindowAndLeavesTrailingRowsAbsent(int period)
    {
        double[] prices = [1, 3, 2, 7, 5, -1, 0, 4, 9, 2, 1, 3];
        var bars = Bars(prices);
        ComparisonVerifier.Compare(
            RetrospectivePriceComparison.DpoReference(prices, period, false),
            RetrospectivePriceComparison.OwnedDpo(bars, period),
            "DPO independent",
            IndicatorErrorBudget.Exact
        );
        var first = DetrendedPriceSnapshot.Calculate(bars.Take(5).ToArray(), period);
        var all = DetrendedPriceSnapshot.Calculate(bars, period);
        Assert.All(
            all.Skip(Math.Max(0, all.Count - (period / 2 + 1))),
            row =>
            {
                Assert.Null(row.Dpo);
                Assert.Null(row.Sma);
            }
        );
        for (var i = 0; i < first.Count; i++)
            if (first[i].Dpo.HasValue)
                Assert.Equal(first[i], all[i]);
        Assert.Equal(all, DetrendedPriceSnapshot.Calculate(bars, period));
    }

    [Fact]
    public void DpoSmallExampleAndFuturePerturbationExposeRetrospectiveMeaning()
    {
        var first = DetrendedPriceSnapshot.Calculate(Bars([1, 2, 3]), 3);
        Assert.Equal(new DetrendedPriceValue(-1, 2), first[0]);
        Assert.Null(first[1].Dpo);
        var changed = DetrendedPriceSnapshot.Calculate(Bars([1, 2, 9]), 3);
        Assert.Equal(new DetrendedPriceValue(-3, 4), changed[0]);
        var extended = DetrendedPriceSnapshot.Calculate(Bars([1, 2, 3, 4]), 3);
        Assert.Equal(first[0], extended[0]);
        Assert.Equal(new DetrendedPriceValue(-1, 3), extended[1]);
    }

    [Fact]
    public void DpoNativeQuoteTupleReusableAndSortedRoutesAgree()
    {
        var data = RetrospectivePriceComparison.Fixture();
        var expected = data.Quotes.GetDpo(3).ToArray();
        var tuples = data.Quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        var reusable = tuples
            .Select(q => (IReusableResult)new SmaResult(q.Date) { Sma = q.Item2 })
            .ToArray();
        foreach (
            var rows in new[]
            {
                tuples.Reverse().GetDpo(3).ToArray(),
                reusable.GetDpo(3).ToArray(),
                data.Quotes.AsEnumerable().Reverse().GetDpo(3).ToArray(),
            }
        )
        {
            Assert.Equal(expected.Select(r => r.Date), rows.Select(r => r.Date));
            Assert.Equal(expected.Select(r => r.Dpo), rows.Select(r => r.Dpo));
            Assert.Equal(expected.Select(r => r.Sma), rows.Select(r => r.Sma));
        }
        Assert.Equal(expected[0].Dpo, ((IReusableResult)expected[0]).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetDpo(0).ToArray());
    }

    [Fact]
    public void DpoFiniteExtremeMeansAndTrueFinalOverflowAreDistinguished()
    {
        foreach (
            var prices in new[]
            {
                new[] { double.MaxValue, double.MaxValue, double.MaxValue },
                new[] { double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon },
            }
        )
            ComparisonVerifier.Compare(
                RetrospectivePriceComparison.DpoReference(prices, 3, false),
                RetrospectivePriceComparison.OwnedDpo(Bars(prices), 3),
                "extreme DPO",
                IndicatorErrorBudget.Exact
            );
        Assert.Equal(
            0,
            DetrendedPriceSnapshot
                .Calculate(Bars([double.MaxValue, double.MaxValue, double.MaxValue]), 3)[0]
                .Dpo
        );
        Assert.Throws<OverflowException>(() =>
            DetrendedPriceSnapshot.Calculate(Bars([double.MaxValue, -double.MaxValue]), 1)
        );
        var native = new[]
        {
            (DateTime.UnixEpoch, double.MaxValue),
            (DateTime.UnixEpoch.AddDays(1), double.MaxValue),
            (DateTime.UnixEpoch.AddDays(2), double.MaxValue),
        }
            .GetDpo(3)
            .ToArray();
        Assert.Equal(double.PositiveInfinity, native[0].Sma);
        Assert.Equal(double.NegativeInfinity, native[0].Dpo);
    }

    [Theory]
    [InlineData(2, 2, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, true)]
    [InlineData(int.MaxValue, int.MaxValue, false)]
    public void FractalWindowsMatchIndependentExhaustiveDecisions(int left, int right, bool close)
    {
        var data = RetrospectivePriceComparison.Fixture();
        ComparisonVerifier.Check(
            RetrospectivePriceComparison.FractalPair(left, right, close),
            data,
            2
        );
        Assert.Equal(
            FractalSnapshot.Calculate(data.IndicatorBars, left, right, close),
            FractalSnapshot.Calculate(data.IndicatorBars, left, right, close)
        );
    }

    [Fact]
    public void FractalTiesAndBothExtremesAreExactAndConfirmationIsDelayed()
    {
        double[] high = [1, 2, 5, 2, 1, 2, 4];
        double[] low = [0, -1, -5, -1, 0, -1, -2];
        var data = CompetitorData.FromOhlcv(new double[7], high, low, new double[7], new double[7]);
        var rows = FractalSnapshot.Calculate(data.IndicatorBars);
        Assert.Equal(new FractalValue(5, -5), rows[2]);
        Assert.All(
            FractalSnapshot.Calculate(data.IndicatorBars.Take(4).ToArray()),
            r =>
            {
                Assert.Null(r.Bear);
                Assert.Null(r.Bull);
            }
        );
        var bands = FractalSnapshot.ChaosBands(data.IndicatorBars);
        Assert.All(bands.Take(4), r => Assert.Equal(new FractalChaosValue(null, null), r));
        Assert.All(bands.Skip(4), r => Assert.Equal(new FractalChaosValue(5, -5), r));
        var tied = data.IndicatorBars.ToArray();
        tied[3] = new Bar(DateTime.UnixEpoch.AddDays(3), 0, 5, -5, 0, 1);
        Assert.Equal(new FractalValue(null, null), FractalSnapshot.Calculate(tied)[2]);
        foreach (var value in new[] { double.Epsilon, double.MaxValue })
        {
            var extreme = Bars([0, 0, value, 0, 0]);
            Assert.Equal(value, FractalSnapshot.Calculate(extreme)[2].Bear);
        }
        var adjacent = Bars([1, 1, Math.BitIncrement(1), 1, 1]);
        Assert.Equal(Math.BitIncrement(1), FractalSnapshot.Calculate(adjacent)[2].Bear);
    }

    [Fact]
    public void NativeDecimalConversionCanEraseAnOtherwiseStrictFractal()
    {
        foreach (
            var prices in new[]
            {
                new[] { 0d, 0, double.Epsilon, 0, 0 },
                new[] { 1d, 1, Math.BitIncrement(1), 1, 1 },
            }
        )
        {
            var data = CompetitorData.FromOhlcv(
                prices,
                prices,
                prices,
                prices,
                new double[prices.Length]
            );
            var pair = RetrospectivePriceComparison.FractalPair();
            ComparisonVerifier.Check(pair, data, 2);
            Assert.True(pair.Ooples(data, 2).Outputs["Bear"].Present![2]);
            Assert.False(pair.Competitor(data, 2).Outputs["Bear"].Present![2]);
            ComparisonVerifier.Check(RetrospectivePriceComparison.ChaosPair, data, 2);
        }
    }

    [Fact]
    public void ChaosBandsPreserveConfirmedPrefixesAndHoldIndependentLines()
    {
        var data = RetrospectivePriceComparison.Fixture();
        foreach (var period in new[] { 2, 3, 9 })
        {
            ComparisonVerifier.Check(RetrospectivePriceComparison.ChaosPair, data, period);
            var all = FractalSnapshot.ChaosBands(data.IndicatorBars, period);
            for (var count = 0; count <= data.Count; count++)
                Assert.Equal(
                    all.Take(count),
                    FractalSnapshot.ChaosBands(data.IndicatorBars.Take(count).ToArray(), period)
                );
        }
        Assert.All(
            FractalSnapshot.ChaosBands(data.IndicatorBars, int.MaxValue),
            row => Assert.Equal(new FractalChaosValue(null, null), row)
        );
        Assert.Throws<OverflowException>(() => data.Quotes.GetFcb(int.MaxValue).ToArray());
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
    public void NativeFractalGenericSortingSymmetricAndAsymmetricRoutesAgree()
    {
        var data = RetrospectivePriceComparison.Fixture();
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
        foreach (var end in new[] { EndType.HighLow, EndType.Close })
        foreach (var (left, right) in new[] { (2, 2), (2, 3), (3, 2) })
        {
            var expected = data.Quotes.GetFractal(left, right, end).ToArray();
            var actual = custom.GetFractal(left, right, end).ToArray();
            Assert.Equal(
                expected.Select(r => (r.Date, r.FractalBear, r.FractalBull)),
                actual.Select(r => (r.Date, r.FractalBear, r.FractalBull))
            );
            if (left == right)
                Assert.Equal(
                    expected.Select(r => (r.FractalBear, r.FractalBull)),
                    custom.GetFractal(left, end).Select(r => (r.FractalBear, r.FractalBull))
                );
        }
        Assert.Equal(
            data.Quotes.GetFcb().Select(r => (r.Date, r.UpperBand, r.LowerBand)),
            custom.GetFcb().Select(r => (r.Date, r.UpperBand, r.LowerBand))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => custom.GetFractal(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => custom.GetFractal(2, 1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => custom.GetFcb(1).ToArray());
    }

    [Fact]
    public void SnapshotsRejectInvalidSelectedPricesAndPeriodsBeforeCalculation()
    {
        Assert.Throws<ArgumentNullException>(() => DetrendedPriceSnapshot.Calculate(null!));
        Assert.Throws<ArgumentNullException>(() => FractalSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => DetrendedPriceSnapshot.Calculate([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FractalSnapshot.Calculate([], 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => FractalSnapshot.Calculate([], 2, 1));
        foreach (
            var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }
        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DetrendedPriceSnapshot.Calculate(Bars([invalid]))
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FractalSnapshot.Calculate(Bars([invalid]))
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FractalSnapshot.Calculate(Bars([invalid]), useClose: true)
            );
        }
        Assert.Empty(DetrendedPriceSnapshot.Calculate([]));
        Assert.Empty(FractalSnapshot.Calculate([]));
        Assert.Empty(FractalSnapshot.ChaosBands([]));
    }

    [Fact]
    public void EverySnapshotFieldAndPresenceDetectsCorruption()
    {
        var data = RetrospectivePriceComparison.Fixture();
        foreach (var pair in RetrospectivePriceComparison.Pairs)
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs[name];
                var index = Array.FindIndex(output.Present!, v => v);
                Assert.True(index >= 0);
                if (presence)
                    output.Present![index] = false;
                else
                    output.Values[index] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    2
                )
            );
        }
    }
}
