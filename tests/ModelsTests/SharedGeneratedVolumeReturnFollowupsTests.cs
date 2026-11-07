using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedVolumeReturnFollowupsTests
{
    private static IIndicator Create(int kind, int length) => kind switch
    { 0 => new NormalizedVolume(length), 1 => new OCHistogram(length), 2 => new ModifiedPriceVolumeTrend(length), _ => new OmegaRatio(length) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 }
        from length in new[] { int.MinValue, 0, 1, 2, 14, 65, 1024 } select new object[] { kind, length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task SourcesDuplicateRootsInvalidBarsAndCancellationKeepContracts(int kind)
    {
        var root = Create(kind, 2);
        IIndicator selected = Create(kind, 2) switch { IndicatorBase scalar => scalar.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() };
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] repeated = [root, Create((kind + 1) % 4, 2), root];
        Assert.True(ValuesBarExecution.SupportsOwned(repeated));
        await Compare(repeated, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ResetReplaysEveryOutput(int kind)
    {
        var name = kind switch { 0 => "GeneratedNormalizedVolumeState", 1 => "GeneratedOcHistogramState", 2 => "GeneratedModifiedPriceVolumeTrendState", _ => "GeneratedTargetReturnState" };
        object?[] args = kind switch { 0 => [2, 5], 3 => [2, .05, false, 5, null], _ => [2] };
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
        double[] Run()
        {
            var result = new List<double>();
            foreach (var bar in BarsOf([1, 3, 3, 1, 2]))
                if (disposable is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                else { var outputs = new double[2]; ((IMultiOutputState)disposable).Update(in bar, outputs); result.AddRange(outputs); }
            return result.ToArray();
        }
        var expected = Run();
        if (disposable is IIndicatorState state) state.Reset(); else ((IMultiOutputState)disposable).Reset();
        Bits(expected, Run());
    }
    [Fact]
    public async Task NormalizedVolumeRetainsHandRatiosAndBoundedMaximumWarmup()
    {
        Bar[] bars = [new(default, 1, 1, 1, 1, 1), new(default, 1, 1, 1, 1, 3), new(default, 1, 1, 1, 1, 2)];
        foreach (var length in new[] { 2, int.MaxValue })
        {
            var root = new NormalizedVolume(length);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
            Bits(length == 2 ? [0, 1.5, .8] : [0, 0, 0], run[root.Value].ToArray());
        }
    }
    [Fact]
    public void NonfiniteBenchmarksRetainLegacyRouting()
    { foreach (var benchmark in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) Assert.False(ValuesBarExecution.SupportsOwned([new OmegaRatio(2, benchmark)])); }
    [Fact]
    public async Task CustomerAveragesKeepOriginalResults()
    {
        IIndicator[] roots = [new OCHistogram(2, new Sma(2), new Ema(2)), new ModifiedPriceVolumeTrend(2, new Ema(2))];
        foreach (var root in roots) { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], BarsOf([1, 2, 3, 1, 2])); }
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select((v, i) => new Bar(default, v / 2, Math.Abs(v), -Math.Abs(v), v, i % 5 == 0 ? 0 : v)).ToArray();
    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots).ConfigureHistory(history);
            IIndicatorRun? expected = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    IIndicatorRun? actual = null;
                    try
                    {
                        var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                        Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                        Assert.Equal(expectedError?.Message, actualError?.Message);
                        if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                        Assert.NotNull(expected); Assert.NotNull(actual);
                        if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned(roots))
                            Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
                        if (bars.Length > 0)
                        {
                            Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                            Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                        }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Assert.Equal(bars.Length, expected[output].ToArray().Length);
                    }
                    finally { actual?.Dispose(); }
                }
            }
            finally { expected?.Dispose(); }
        }
    }
    private static void Bits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
