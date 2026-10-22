using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSupportResistanceLevelsTests
{
    private static SupportResistance Create(int length) => new SupportResistance(length);
    public static IEnumerable<object[]> Cases => from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int length)
    {
        var root = Create(length); Assert.Equal(length > 0, ValuesBarExecution.SupportsOwned([root]));
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
    [Theory, InlineData(1), InlineData(3), InlineData(int.MaxValue)]
    public void IndependentCrossoversAndReset(int length)
    {
        var prices = new double[] { 3, 7, -2, 2, 0, -4, 6, 1, -3, 4 };
        var bars = prices.Select(p => new Bar(default, p, p + 1, p - 1, p, 1)).ToArray();
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedSupportResistanceLevelsState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [length, bars.Length], null)!;
        var state = (IMultiOutputState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            double support = bars[0].Low, resistance = bars[0].High;
            for (var i = 0; i < bars.Length; i++)
            {
                var previous = i == 0 ? 0 : prices[i - 1];
                var priorMean = i < length ? 0 : prices.Take(i).TakeLast(length).Average();
                var window = bars.Take(i + 1).TakeLast(length).ToArray();
                if (previous < priorMean && prices[i] >= priorMean) support = window.Min(b => b.Low);
                if (previous > priorMean && prices[i] <= priorMean) resistance = window.Max(b => b.High);
                var outputs = new double[2]; state.Update(in bars[i], outputs);
                Bits([support, resistance], outputs);
            }
            state.Reset();
        }
    }
    [Fact]
    public async Task CustomAverageRetainsFallback()
    {
        var root = new SupportResistance(3, new Sma());
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], BarsOf([1, 3, 3, 1, 2]));
    }
    [Fact]
    public async Task DistinctPeriodsPreserveOriginalCandlesAndVolumes()
    {
        await Compare([new SupportResistance(3), new SupportResistance(7)],
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
