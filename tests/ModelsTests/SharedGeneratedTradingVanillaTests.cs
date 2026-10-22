using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedTradingVanillaTests
{
    private static IndicatorBase Create(int operation, int length) => operation == 0 ? new TradingMadeMoreSimplerOscillator(length) : new VanillaABCDPattern();
    public static IEnumerable<object[]> Cases => (from length in new[] { int.MinValue, 0, 1, 2, 14, 32, int.MaxValue } select new object[] { 0, length }).Concat(new[] { new object[] { 1, 1 } });
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int operation, int length)
    {
        var root = Create(operation, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Theory, InlineData(0), InlineData(1)]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts(int operation)
    {
        var root = Create(operation, 14); var selected = Create(operation, 14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] roots = [root, Create(operation, 3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Fact]
    public void TradingHandKeepsIndependentLegsAndReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedTradingSimplerState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [1, 1, 1, 50d, 0d, 4], null)!;
        var state = (IIndicatorState)disposable;
        // RSI:100,100,0,100. Stochastic:80,90,10,20. The negative arm is subtracted.
        double[] prices = [8, 9, 1, 2], expected = [30, 40, 40, 0];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                var bar = new Bar(default, 5, 10, 0, prices[i], 1);
                Bits([expected[i]], [state.Update(in bar)]);
            }
            state.Reset();
        }
    }
    [Fact]
    public void VanillaHandKeepsBothLatchTransitionsAndReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedVanillaAbcdState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type, true)!;
        double[] prices = [4, 1, 3, 0, 2, 1, 3], expected = [0, 0, 0, 1, 0, 0, -1];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                var bar = new Bar(default, prices[i] + 10, 100, -100, prices[i], 1);
                Bits([expected[i]], [state.Update(in bar)]);
            }
            state.Reset();
        }
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
