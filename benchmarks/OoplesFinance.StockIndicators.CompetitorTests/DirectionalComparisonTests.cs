using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class DirectionalComparisonTests
{
    [Fact]
    public async Task ChainingPreservesCandleRangesAndReplacesClose()
    {
        var data = CompetitorData.Create(30);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        foreach (var measure in Enum.GetValues<DirectionalWindowMeasure>())
        {
            var indicator = new WindowDirectionalMeasure(measure, 3, 2);
            indicator.Of(new FixedPeriodWma(3));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = DirectionalComparison.Reference(bars, 3, 2)[(int)measure];
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
            Assert.Same(indicator.Value, indicator.PrimaryOutput);
        }
    }

    [Fact]
    public async Task SelectedRawOverflowRejectsWhileNativeDecimalLimitsRemainVisible()
    {
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run(
                DirectionalWindowMeasure.PositiveMovement,
                1,
                0,
                (-double.MaxValue, -double.MaxValue, -double.MaxValue),
                (double.MaxValue, 0, 0)
            )
        );
        Assert.Throws<OverflowException>(() =>
            new T.PlusDirectionalMovementByTuple(new[] { -decimal.MaxValue, decimal.MaxValue })
                .Compute()
                .ToArray()
        );
        var tiny = CompetitorData.FromOhlc(
            [0, 0],
            [0, 2 * double.Epsilon],
            [0, -double.Epsilon],
            [0, 0]
        );
        Assert.Null(
            DirectionalComparison.Native(tiny, 1, 0, DirectionalWindowMeasure.PositiveIndicator)[1]
        );
        Assert.Equal(
            100d * 2 / 3,
            DirectionalComparison
                .Owned(tiny.IndicatorBars, 1, 0, DirectionalWindowMeasure.PositiveIndicator)
                .Outputs["Value"]
                .Values[1]
        );
    }

    [Fact]
    public void NativeTupleGenericIndexRangeAndRepeatedRoutesMatchDecimalOracle()
    {
        var data = CompetitorData.Create(30);
        var inputs = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        foreach (var p in new[] { 1, 3, 14 })
        foreach (var lag in new[] { 0, 1, 14, int.MaxValue })
        {
            var expected = DirectionalComparison.DecimalReference(data, p, lag);
            var tuples = new Trady.Analysis.Infrastructure.NumericAnalyzableBase<
                (decimal High, decimal Low, decimal Close),
                (decimal High, decimal Low, decimal Close),
                decimal?
            >[]
            {
                new T.PlusDirectionalIndicatorByTuple(inputs, p),
                new T.MinusDirectionalIndicatorByTuple(inputs, p),
                new T.DirectionalMovementIndexByTuple(inputs, p),
                new T.AverageDirectionalIndexByTuple(inputs, p),
                new T.AverageDirectionalIndexRatingByTuple(inputs, p, lag),
            };
            var generics = new Trady.Analysis.Infrastructure.NumericAnalyzableBase<
                int,
                (decimal High, decimal Low, decimal Close),
                decimal?
            >[]
            {
                new T.PlusDirectionalIndicator<int, decimal?>(
                    Enumerable.Range(0, inputs.Length),
                    i => inputs[i],
                    p
                ),
                new T.MinusDirectionalIndicator<int, decimal?>(
                    Enumerable.Range(0, inputs.Length),
                    i => inputs[i],
                    p
                ),
                new T.DirectionalMovementIndex<int, decimal?>(
                    Enumerable.Range(0, inputs.Length),
                    i => inputs[i],
                    p
                ),
                new T.AverageDirectionalIndex<int, decimal?>(
                    Enumerable.Range(0, inputs.Length),
                    i => inputs[i],
                    p
                ),
                new T.AverageDirectionalIndexRating<int, decimal?>(
                    Enumerable.Range(0, inputs.Length),
                    i => inputs[i],
                    p,
                    lag
                ),
            };
            for (var j = 0; j < tuples.Length; j++)
            {
                var oracle = expected[j + 2];
                var tuple = tuples[j];
                Assert.Equal(oracle, tuple.Compute());
                Assert.Equal(oracle, tuple.Compute());
                Assert.Equal(
                    oracle.Select(v => (double?)v),
                    tuple.Compute().Select(v => (double?)v)
                );
                Assert.Equal(oracle, generics[j].Compute());
                var indexes = new[] { 20, 3, 3, 0, 8 };
                Assert.Equal(
                    indexes.Select(i => oracle[i]),
                    tuple.Compute((IEnumerable<int>)indexes)
                );
                Assert.Equal(oracle.Skip(2).Take(8), tuple.Compute(startIndex: 2, endIndex: 9));
                foreach (var i in indexes)
                    Assert.Equal(oracle[i], tuple[i]);
            }
            Assert.Equal(
                expected[0],
                new T.PlusDirectionalMovementByTuple(inputs.Select(v => v.High)).Compute()
            );
            Assert.Equal(
                expected[1],
                new T.MinusDirectionalMovementByTuple(inputs.Select(v => v.Low)).Compute()
            );
            Assert.Equal(
                expected[0],
                new T.PlusDirectionalMovement<int, decimal?>(
                    Enumerable.Range(0, inputs.Length),
                    i => inputs[i].High
                ).Compute()
            );
            Assert.Equal(
                expected[1],
                new T.MinusDirectionalMovement<int, decimal?>(
                    Enumerable.Range(0, inputs.Length),
                    i => inputs[i].Low
                ).Compute()
            );
        }
    }

    [Fact]
    public void ValuePresenceAndFormulaMutationsAreDetected()
    {
        foreach (var pair in DirectionalComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CompetitorData.Create(30),
                    3
                )
            );
        }
        var positive = DirectionalComparison.Pair(DirectionalWindowMeasure.PositiveIndicator);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                positive with
                {
                    Library = DirectionalComparison
                        .Pair(DirectionalWindowMeasure.NegativeIndicator)
                        .Ooples,
                },
                ComparisonVerifier.Fixture("ramp", 20),
                3
            )
        );
        var rating = DirectionalComparison.Pair(DirectionalWindowMeasure.EarlyRating, 0);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                rating with
                {
                    Library = DirectionalComparison
                        .Pair(DirectionalWindowMeasure.EarlyRating, 1)
                        .Ooples,
                },
                CompetitorData.Create(30),
                3
            )
        );
    }

    [Fact]
    public void IndependentDecimalAndIntegerGridOraclesCoverAllOutputs()
    {
        foreach (var measure in Enum.GetValues<DirectionalWindowMeasure>())
        foreach (var period in new[] { 1, 3, 14 })
        foreach (var lag in new[] { 0, 1, 20, int.MaxValue })
        {
            var pair = DirectionalComparison.Pair(measure, lag);
            ComparisonVerifier.Check(pair, CompetitorData.Create(45), period);
            foreach (var shape in ComparisonVerifier.Shapes)
                ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture(shape, 25), period);
        }
    }

    private static async Task<double[][]> Run(
        DirectionalWindowMeasure measure,
        int period,
        int lag,
        params (double High, double Low, double Close)[] values
    )
    {
        var indicator = new WindowDirectionalMeasure(measure, period, lag);
        var bars = values
            .Select(
                (v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v.Close, v.High, v.Low, v.Close, 0)
            )
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
    }

    [Theory]
    [InlineData(DirectionalWindowMeasure.PositiveMovement)]
    [InlineData(DirectionalWindowMeasure.NegativeMovement)]
    [InlineData(DirectionalWindowMeasure.PositiveIndicator)]
    [InlineData(DirectionalWindowMeasure.NegativeIndicator)]
    [InlineData(DirectionalWindowMeasure.Index)]
    [InlineData(DirectionalWindowMeasure.EarlyAverage)]
    [InlineData(DirectionalWindowMeasure.EarlyRating)]
    public async Task IndependentReferenceCoversLifecycle(DirectionalWindowMeasure measure)
    {
        foreach (var period in new[] { 1, 3, 14 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(WindowDirectionalMeasure),
                    $"directional {measure} {period}",
                    () => new WindowDirectionalMeasure(measure, period, 2)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public async Task EarlySeedAndMissingDirectionRemainDistinct()
    {
        var rising = Enumerable
            .Range(0, 6)
            .Select(i => ((double)i, (double)i, (double)i))
            .ToArray();
        var dx = await Run(DirectionalWindowMeasure.Index, 3, 0, rising);
        Assert.Equal(new double[] { 0, 0, 0, 100, 100, 100 }, dx[0]);
        Assert.All(dx[1], v => Assert.Equal(1, v));
        var adx = await Run(DirectionalWindowMeasure.EarlyAverage, 3, 0, rising);
        Assert.Equal(100d / 3, adx[0][3]);
        Assert.Equal(new double[] { 0, 0, 0, 1, 1, 1 }, adx[1]);
        var flat = Enumerable.Repeat((2d, 0d, 1d), 6).ToArray();
        dx = await Run(DirectionalWindowMeasure.Index, 3, 0, flat);
        Assert.Equal(new double[] { 1, 1, 1, 0, 0, 0 }, dx[1]);
        adx = await Run(DirectionalWindowMeasure.EarlyAverage, 3, 0, flat);
        Assert.Equal(new double[] { 0, 0, 0, 1, 0, 0 }, adx[1]);
        Assert.All(adx[0], v => Assert.Equal(0, v));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(int.MaxValue)]
    public async Task RatingRequiresBothAlignedEndpointsWithoutEagerAllocation(int lag)
    {
        var values = Enumerable
            .Range(0, 9)
            .Select(i => ((double)i, (double)i, (double)i))
            .ToArray();
        var mean = await Run(DirectionalWindowMeasure.EarlyAverage, 3, lag, values);
        var rating = await Run(DirectionalWindowMeasure.EarlyRating, 3, lag, values);
        for (var i = 0; i < values.Length; i++)
        {
            var present = i >= lag && mean[1][i] == 1 && mean[1][i - lag] == 1;
            Assert.Equal(present ? 1 : 0, rating[1][i]);
            if (present)
                Assert.Equal((mean[0][i] + mean[0][i - lag]) / 2, rating[0][i], 12);
        }
    }

    [Fact]
    public async Task SignedRawChangesAndExactDominanceArePreserved()
    {
        var values = new[] { (2d, 0d, 1d), (1d, -1d, 0d), (3d, -3d, 0d) };
        Assert.Equal(
            new double[] { 0, -1, 2 },
            (await Run(DirectionalWindowMeasure.PositiveMovement, 1, 0, values))[0]
        );
        Assert.Equal(
            new double[] { 0, 1, 2 },
            (await Run(DirectionalWindowMeasure.NegativeMovement, 1, 0, values))[0]
        );
        Assert.Equal(
            0,
            (await Run(DirectionalWindowMeasure.PositiveIndicator, 1, 0, values))[0][2]
        );
        Assert.Equal(
            0,
            (await Run(DirectionalWindowMeasure.NegativeIndicator, 1, 0, values))[0][2]
        );
        var tiny = new[] { (0d, 0d, 0d), (2 * double.Epsilon, -double.Epsilon, 0d) };
        Assert.Equal(
            100d * 2 / 3,
            (await Run(DirectionalWindowMeasure.PositiveIndicator, 1, 0, tiny))[0][1]
        );
        Assert.Equal(0, (await Run(DirectionalWindowMeasure.NegativeIndicator, 1, 0, tiny))[0][1]);
        var huge = new[]
        {
            (-double.MaxValue, -double.MaxValue, -double.MaxValue),
            (double.MaxValue, 0d, 0d),
        };
        Assert.Equal(
            100,
            (await Run(DirectionalWindowMeasure.PositiveIndicator, 1, 0, huge))[0][1]
        );
        Assert.Equal(100, (await Run(DirectionalWindowMeasure.Index, 1, 0, huge))[0][1]);
    }

    [Fact]
    public void InvalidConfigurationIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowDirectionalMeasure((DirectionalWindowMeasure)7)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowDirectionalMeasure(DirectionalWindowMeasure.Index, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowDirectionalMeasure(DirectionalWindowMeasure.Index, 1, -1)
        );
    }

    [Fact]
    public void TradyNullableDxStartupAndAdxSeedAreExplicit()
    {
        var rising = CompetitorData.FromOhlc(
            [0, 1, 2, 3, 4, 5],
            [0, 1, 2, 3, 4, 5],
            [0, 1, 2, 3, 4, 5],
            [0, 1, 2, 3, 4, 5]
        );
        Assert.Equal(
            new decimal?[] { 0, 0, 0, 100, 100, 100 },
            new T.DirectionalMovementIndex(rising.Candles, 3).Compute().Select(r => r.Tick)
        );
        var adx = new T.AverageDirectionalIndex(rising.Candles, 3)
            .Compute()
            .Select(r => r.Tick)
            .ToArray();
        Assert.Equal(new decimal?[] { null, null, null }, adx.Take(3));
        Assert.Equal(100m / 3, adx[3]);
        var flat = CompetitorData.FromOhlc(
            new double[6],
            new double[6],
            new double[6],
            new double[6]
        );
        Assert.All(
            new T.DirectionalMovementIndex(flat.Candles, 3).Compute(),
            r => Assert.Equal(0m, r.Tick)
        );
        var noMove = CompetitorData.FromOhlc(
            Enumerable.Repeat(1d, 6).ToArray(),
            Enumerable.Repeat(2d, 6).ToArray(),
            new double[6],
            Enumerable.Repeat(1d, 6).ToArray()
        );
        Assert.Equal(
            new decimal?[] { 0, 0, 0, null, null, null },
            new T.DirectionalMovementIndex(noMove.Candles, 3).Compute().Select(r => r.Tick)
        );
        Assert.Equal(
            new decimal?[] { null, null, null, 0, null, null },
            new T.AverageDirectionalIndex(noMove.Candles, 3).Compute().Select(r => r.Tick)
        );
    }
}
