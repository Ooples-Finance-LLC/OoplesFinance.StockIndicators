using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedQuasiNoiseTests
{
    private static QuasiWhiteNoise Create(int length) => new QuasiWhiteNoise(length, 5, 8);
    public static IEnumerable<object[]> Cases => from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int length)
    {
        var root = Create(length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Fact]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts()
    {
        var root = Create(14); var selected = Create(14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] roots = [root, Create(3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory, InlineData(8d), InlineData(-8d), InlineData(double.MaxValue)]
    public void IndependentReferenceAndReset(double divisor)
    {
        var bars = BarsOf([1, 3, 2, 4, 0, -1, 2]);
        var indicator = new QuasiWhiteNoise(3, 5, divisor);
        var expected = BuiltInFormulaReferences.QuasiWhiteNoiseOutputs(bars, indicator);
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedQuasiNoiseState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [3, 5, divisor, bars.Length], null)!;
        var state = (IMultiOutputState)disposable;
        string[] keys = ["WhiteNoise", "WhiteNoiseMa", "WhiteNoiseStdDev", "WhiteNoiseVariance"];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < bars.Length; i++)
            {
                var outputs = new double[4]; state.Update(in bars[i], outputs);
                Bits(keys.Select(k => expected[k][i]).ToArray(), outputs);
            }
            state.Reset();
        }
    }
    [Fact]
    public void UnreachablePopulationWarmupRemainsZero()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedQuasiNoiseState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [3, int.MaxValue, 8d, 4], null)!;
        var state = (IMultiOutputState)disposable;
        foreach (var price in new double[] { 1, 3, 2, 4 })
        {
            var bar = new Bar(default, price, price, price, price, 1);
            var outputs = new double[4]; state.Update(in bar, outputs);
            Assert.All(outputs, value => Assert.True(double.IsFinite(value)));
            Bits([0, 0], outputs[2..]);
        }
    }
    [Fact]
    public async Task UnsupportedDivisorsRetainFallback()
    {
        foreach (var divisor in new double[] { 0, double.Epsilon, double.NaN, double.PositiveInfinity })
        {
            var root = new QuasiWhiteNoise(3, 5, divisor);
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], BarsOf([1, 3, 2]));
        }
    }
    [Fact]
    public async Task CustomAverageRetainsFallback()
    {
        var root = new QuasiWhiteNoise(3, 5, 8, new Sma());
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], BarsOf([1, 3, 3, 1, 2]));
    }
    [Fact]
    public async Task DistinctPeriodsPreserveOriginalCandlesAndVolumes()
    {
        await Compare([new QuasiWhiteNoise(3, 7, 8), new QuasiWhiteNoise(7, 3, -8)],
            BarsOf(Enumerable.Range(0, 65).Select(i => 5 + Math.Sin(i * .7)).ToArray()));
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select(v => new Bar(default, v / 2, Math.Abs(v) + 1, -Math.Abs(v) - 2, v, 1)).ToArray();
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
