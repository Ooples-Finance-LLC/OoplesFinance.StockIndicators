using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedVervoortModifiedTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(18, 200, 8)]
    [InlineData(3, 7, 2)]
    [InlineData(0, -1, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData(1, 200, 2)]
    public async Task ExactTypicalPriceAndDeviationStagesMatchLegacy(int band, int outer, int smooth)
    {
        var root = new VervoortModifiedBollingerBandIndicator(band, outer, smooth);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var values in new double[][]
        {
            [], [1], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0],
            [double.Epsilon, double.MaxValue, 1, 2, 3, 4, 5, 6, 7],
            [1, -1, 0, 0, 2, -2, 0, 3, -3],
            Enumerable.Range(0, 33).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 17).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        }) await Compare(root, values);
    }

    [Fact]
    public async Task UnfilledDeviationWindowsPublishFiftyCenterAndZeroPercent()
    {
        var bars = new[] { double.MaxValue, -double.MaxValue, double.Epsilon }.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        var root = new VervoortModifiedBollingerBandIndicator(int.MaxValue, int.MaxValue, int.MaxValue);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureHistory(history).BuildAsync();
            Bits([50, 50, 50], run[root.UpperBand].ToArray());
            Bits([50, 50, 50], run[root.MiddleBand].ToArray());
            Bits([50, 50, 50], run[root.LowerBand].ToArray());
            Bits(new double[3], run[root.PercentB].ToArray());
        }
    }

    [Fact]
    public async Task ExtremeFactorsAndExplicitAverageStagesRetainArithmeticAndFallback()
    {
        foreach (var factor in new[] { -2d, 0, double.Epsilon, 2, double.MaxValue })
            await Compare(new VervoortModifiedBollingerBandIndicator(2, 3, 1, factor), [1, -1, 2, 0, 3, -2]);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var root = new VervoortModifiedBollingerBandIndicator(stdDevMult: invalid);
            Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare(root, [1, 2, 3]);
        }
        foreach (IIndicator root in new IIndicator[] {
            new VervoortModifiedBollingerBandIndicator(3, 2, 2, maType: new Wma(2)),
            new VervoortModifiedBollingerBandIndicator(3, 2, 2, maType: new Wma(2), secondAverage: new Sma(2), thirdAverage: new Ema(2)),
            new VervoortModifiedBollingerBandIndicator(3, 2, 2).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare(root, [double.Epsilon, 2, -1, 0, 4, 2]);
        }
    }

    [Fact]
    public async Task EntireSeriesEqualityControlsRangeReplacementBeforeTheChangedTail()
    {
        foreach (var changedTail in new[] { false, true })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            // Each initial derived OHLC price equals its close despite reversed bounds.
            // A differing tail enables legacy per-bar range replacement for the whole series.
            var bars = Enumerable.Repeat(new Bar(default, -3, 1, 2, 0, 1), 5).ToArray();
            if (changedTail) bars[^1] = new(default, 1, 1, 2, 0, 1);
            var root = new VervoortModifiedBollingerBandIndicator(2, 2, 1);
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureHistory(history);
            using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Builder().BuildAsync();
            foreach (var output in root.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
        }
    }

    [Fact]
    public async Task OriginalCandleFieldsRepeatedBuildsAndInvalidTailPreserveStateAndValidationPriority()
    {
        IIndicator[] roots = [new VervoortModifiedBollingerBandIndicator(3, 2, 2), new Cci(3), new GarmanKlassVolatility(3), new ErgodicCandlestickOscillator(3)];
        var bars = Enumerable.Range(0, 17).Select(i => new Bar(default, i % 13 - 6d, i % 7 + 1, -i % 11 - 1, i % 5 - 2d, 1)).ToArray();
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators([.. roots, roots[0]]);
        using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder();
        for (var repeat = 0; repeat < 2; repeat++)
        {
            using var actual = await builder.BuildAsync();
            foreach (var root in roots) foreach (var output in root.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
        }
        bars[0] = new(default, double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1);
        bars[1] = bars[0]; bars[^1] = new(default, 0, 1, -1, 0, double.NaN);
        var expectedError = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
        var actualError = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.IsType<ArgumentOutOfRangeException>(expectedError);
        Assert.Equal(expectedError!.GetType(), actualError!.GetType()); Assert.Equal(expectedError.Message, actualError.Message);
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
    }

    private static async Task Compare(IIndicator root, double[] values)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select(v => new Bar(default, v / 4, v, v / 2, v, 1)).ToArray();
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                Assert.Equal(expectedError?.GetType(), actualError?.GetType()); Assert.Equal(expectedError?.Message, actualError?.Message);
                if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                Assert.NotNull(expected); Assert.NotNull(actual);
                if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned([root])) Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                foreach (var output in root.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
                if (bars.Length > 0) { Assert.Equal(expected.Latest.Bar, actual.Latest.Bar); Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp); }
                actual.Dispose(); Array.Clear(bars);
                foreach (var output in root.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
