using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class StochasticRsiComparisonTests
{
    [Theory]
    [InlineData(false, 1, 1, 1, 1)]
    [InlineData(false, 3, 3, 1, 1)]
    [InlineData(false, int.MaxValue, 1, 1, 1)]
    [InlineData(true, 1, 1, 1, 1)]
    [InlineData(true, 3, 2, 4, 3)]
    [InlineData(true, 14, 14, 3, 1)]
    [InlineData(true, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData(true, 2, int.MaxValue, 1, 1)]
    [InlineData(true, 2, 2, int.MaxValue, int.MaxValue)]
    public async Task IndependentContractsVerifyCompositionLifecycleAndLazyPeriods(
        bool skender,
        int period,
        int stochastic,
        int signal,
        int smooth
    )
    {
        var sample = StochasticRsiComparison.Indicator(period, skender, stochastic, signal, smooth);
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                sample.GetType(),
                $"{skender}/{period}/{stochastic}/{signal}/{smooth}",
                () => StochasticRsiComparison.Indicator(period, skender, stochastic, signal, smooth)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void CalendarStartupFlatScaleAndSignalSeedAreExplicit()
    {
        var data = CompetitorData.FromCloses(Enumerable.Repeat(1d, 12).ToArray());
        var trady = StochasticRsiComparison.Create(false).Ooples(data, 3).Outputs["Value"];
        Assert.All(trady.Present!.Take(2), Assert.False);
        Assert.All(trady.Values.Skip(2), v => Assert.Equal(.5, v));
        var pair = StochasticRsiComparison.Create(true, 2, 4, 3);
        ComparisonVerifier.Check(pair, data, 3);
        var skender = pair.Ooples(data, 3).Outputs;
        Assert.Equal(6, Array.FindIndex(skender["Value"].Present!, v => v));
        Assert.Equal(9, Array.FindIndex(skender["Signal"].Present!, v => v));
        Assert.All(
            skender.Values.SelectMany(v => v.Values.Where(double.IsFinite)),
            v => Assert.Equal(0, v)
        );
        var rising = CompetitorData.FromCloses(
            Enumerable.Range(0, 12).Select(i => (double)i).ToArray()
        );
        ComparisonVerifier.Check(StochasticRsiComparison.Create(false), rising, 3);
        ComparisonVerifier.Check(StochasticRsiComparison.Create(true), rising, 3);
    }

    [Fact]
    public void NullableGapsRecoverToHalfWhenRemainingRsiRangeBecomesFlatOrEmpty()
    {
        double?[] prices = [5, 3, 7, 2, 4, 1, null, 9, 4, 3, 2, 1];
        var expected = StochasticRsiComparison.NullableReference(prices, 3);
        var enumerable = NullableStochasticRsi.FromValues(prices, 3);
        Assert.Equal(expected, enumerable.ToArray());
        Assert.Equal(expected, enumerable.ToArray());
        Assert.Equal(.5, expected[2]);
        Assert.Null(expected[6]);
        Assert.All(expected.Skip(7), v => Assert.Equal(.5, v));
        foreach (
            double?[] values in new[]
            {
                new double?[] { null, null, null, null, 2, 1, 3, 0 },
                new double?[] { 1, null, 3, 2, 4, 1, 5, 0 },
            }
        )
            Assert.Equal(
                StochasticRsiComparison.NullableReference(values, 3),
                NullableStochasticRsi.FromValues(values, 3)
            );
        decimal?[] native = prices.Select(v => (decimal?)v).ToArray();
        Assert.Equal(
            StochasticRsiComparison.TradyReference(native, 3),
            new T.StochasticsRsiOscillatorByTuple(native, 3).Compute()
        );
    }

    [Fact]
    public void TradyTupleGenericIndexedRangeAndRepeatedRoutesAgree()
    {
        decimal?[] values = [5, 3, 7, 2, 4, 1, null, 9, 4, 3, 2, 1];
        var expected = StochasticRsiComparison.TradyReference(values, 3);
        var tuple = new T.StochasticsRsiOscillatorByTuple(values, 3);
        Assert.Equal(expected, tuple.Compute());
        Assert.Equal(expected, tuple.Compute());
        int[] indexes = [11, 2, 2, 0, 6];
        Assert.Equal(indexes.Select(i => expected[i]), tuple.Compute((IEnumerable<int>)indexes));
        Assert.Equal(expected.Skip(2).Take(6), tuple.Compute(startIndex: 2, endIndex: 7));
        foreach (var i in indexes)
            Assert.Equal(expected[i], tuple[i]);
        Assert.Equal(
            expected,
            new T.StochasticsRsiOscillator<int, decimal?>(
                Enumerable.Range(0, values.Length),
                i => values[i],
                3
            ).Compute()
        );
    }

    [Fact]
    public void SkenderQuoteTupleReusableAndSortingRoutesAgree()
    {
        var data = NullableStrengthComparison.Fixture();
        var expected = data.Quotes.GetStochRsi(2, 3, 2, 2).ToArray();
        var tuples = data.Quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        var reusable = tuples
            .Select(q => (IReusableResult)new SmaResult(q.Date) { Sma = q.Item2 })
            .ToArray();
        foreach (
            var rows in new[]
            {
                tuples.Reverse().GetStochRsi(2, 3, 2, 2).ToArray(),
                reusable.GetStochRsi(2, 3, 2, 2).ToArray(),
                data.Quotes.AsEnumerable().Reverse().GetStochRsi(2, 3, 2, 2).ToArray(),
            }
        )
            Assert.Equal(
                expected.Select(r => (r.Date, r.StochRsi, r.Signal)),
                rows.Select(r => (r.Date, r.StochRsi, r.Signal))
            );
        Assert.Equal(
            data.Quotes.GetStochRsi(2, 3, 2).Select(r => (r.StochRsi, r.Signal)),
            data.Quotes.GetStochRsi(2, 3, 2, 1).Select(r => (r.StochRsi, r.Signal))
        );
        Assert.Equal(expected.Last().StochRsi, ((IReusableResult)expected.Last()).Value);
    }

    [Fact]
    public void InvalidInputAndNativePeriodBoundaryDifferencesArePinned()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NullableStochasticRsi(0));
        Assert.Throws<ArgumentNullException>(() => NullableStochasticRsi.FromValues(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => NullableStochasticRsi.FromValues([], 0));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NullableStochasticRsi.FromValues([1, bad], 1).ToArray()
            );
        foreach (var config in new[] { (0, 1, 1, 1), (1, 0, 1, 1), (1, 1, 0, 1), (1, 1, 1, 0) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SmoothedStochasticRsi(config.Item1, config.Item2, config.Item3, config.Item4)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NullableStrengthComparison
                    .Fixture()
                    .Quotes.GetStochRsi(config.Item1, config.Item2, config.Item3, config.Item4)
                    .ToArray()
            );
        }
        Assert.Throws<OverflowException>(() =>
            NullableStrengthComparison.Fixture().Quotes.GetStochRsi(int.MaxValue, 2, 1).ToArray()
        );
        Assert.All(
            new T.StochasticsRsiOscillatorByTuple([1, 2, 3], 0).Compute(),
            v => Assert.Equal(.5m, v)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new T.StochasticsRsiOscillatorByTuple([1, 2, 3], -1).Compute()
        );
    }

    [Fact]
    public void TinyAndWideInputsUseTheVerifiedRsiEngineWithoutDecimalClamping()
    {
        foreach (var magnitude in new[] { double.Epsilon, double.MaxValue })
        {
            double[] prices = [0, magnitude, 0, -magnitude, 0, magnitude, -magnitude, 0, magnitude];
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1))
                .ToArray();
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(
                    StochasticRsiComparison.NullableReference(
                        prices.Select(v => (double?)v).ToArray(),
                        2
                    )
                ),
                StochasticRsiComparison.Owned(bars, 2, false, 2, 1, 1),
                "nullable RSI extremes",
                IndicatorErrorBudget.Exact
            );
            ComparisonVerifier.Compare(
                RetrospectivePriceComparison.Series(
                    StochasticRsiComparison.Names(true),
                    StochasticRsiComparison.SmoothedReference(bars, 2, 2, 2, 2, false)
                ),
                StochasticRsiComparison.Owned(bars, 2, true, 2, 2, 2),
                "smoothed RSI extremes",
                IndicatorErrorBudget.Exact
            );
        }
        var tiny = CompetitorData.FromCloses([
            0,
            double.Epsilon,
            0,
            -double.Epsilon,
            0,
            double.Epsilon,
            0,
            -double.Epsilon,
        ]);
        foreach (var pair in StochasticRsiComparison.Pairs)
            ComparisonVerifier.Check(pair, tiny, 2);
    }

    [Fact]
    public async Task SourceAndDownstreamChainingPreserveBothOutputs()
    {
        var data = NullableStrengthComparison.Fixture();
        foreach (var skender in new[] { false, true })
        {
            var source = new PriceCircularTransform(PriceCircularOperation.Cosine);
            var indicator = StochasticRsiComparison.Indicator(2, skender, 2, 2, 1);
            ((MultiOutputIndicatorBase)indicator).Of(source);
            var downstream = new FirstValueEma(1);
            downstream.Of(indicator);
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(source, indicator, downstream)
                .BuildAsync();
            var prices = run[source.Value].ToArray();
            var bars = data
                .IndicatorBars.Select(
                    (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)
                )
                .ToArray();
            var expected = skender
                ? StochasticRsiComparison.SmoothedReference(bars, 2, 2, 2, 1, false)
                : new[]
                {
                    StochasticRsiComparison.NullableReference(
                        prices.Select(v => (double?)v).ToArray(),
                        2
                    ),
                };
            for (var slot = 0; slot < expected.Length; slot++)
                Assert.Equal(
                    expected[slot].Select(v => v ?? 0),
                    run[indicator.Outputs[slot]].ToArray()
                );
            Assert.Equal(expected[0].Select(v => v ?? 0), run[downstream.Value].ToArray());
        }
    }

    [Fact]
    public void EveryStochasticAndSignalFieldDetectsValueAndPresenceCorruption()
    {
        var data = NullableStrengthComparison.Fixture();
        foreach (var pair in StochasticRsiComparison.Pairs)
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs[name];
                var index = Array.FindLastIndex(output.Present!, v => v);
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
