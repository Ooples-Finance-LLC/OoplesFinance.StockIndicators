using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedVervoortCandlesTests
{
    private static IIndicator Create(int operation, int length) => operation switch { 0 => new VervoortHeikenAshiCandlestickOscillator(length), 1 => new VervoortHeikenAshiLongTermCandlestickOscillator(length), _ => new VervoortSmoothedOscillator(length, length1: length, length2: 5, length3: 2, smoothLength: 3, stdDevMult: 2) };
    public static IEnumerable<object[]> Cases => from operation in new[] { 0, 1, 2 } from length in new[] { int.MinValue, 0, 1, 2, 14, 32, int.MaxValue } select new object[] { operation, length };
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
    [Theory, InlineData(0), InlineData(1), InlineData(2)]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts(int operation)
    {
        var root = Create(operation, 14); IIndicator selected = Create(operation, 14) switch { IndicatorBase single => single.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() };
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
    [Theory, InlineData(false), InlineData(true)]
    public void CandleHandLatchKeepsReset(bool longTerm)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedVervoortCandleState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [longTerm, 1, 1.1, false], null)!;
        var state = (IIndicatorState)disposable;
        double[] prices = [4, 1, 8, 0, -1, 9], expected = longTerm ? [0, 0, 0, 0, 0, 0] : [0, 0, 1, 1, -1, 1];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                var p = prices[i]; var bar = new Bar(default, p, p, p, p, 1);
                Bits([expected[i]], [state.Update(in bar)]);
            }
            state.Reset();
        }
    }
    [Fact]
    public void SmoothedHandKeepsDistinctStochasticLegAndReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedVervoortSmoothedState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [1, 1, 1, 1, 2d, true], null)!;
        var state = (IMultiOutputState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < 3; i++)
            {
                var bar = new Bar(default, 6 * i, 6 * i + 6, 6 * i, 6 * i, 1);
                var outputs = new double[2]; state.Update(in bar, outputs); Bits([0, 20], outputs);
            }
            state.Reset();
        }
    }
    [Theory, InlineData(0), InlineData(1), InlineData(2)]
    public async Task NamedPricesKeepWholeSeriesRangePolicyAndMixedRoots(int operation)
    {
        IIndicator[] roots = [Create(operation, 3), new VolumeFlowIndicator(), new VervoortModifiedBollingerBandIndicator()];
        // First bar's named price equals its close despite inverted bounds.
        // A later distinct named price enables range replacement for the whole batch.
        Bar[] bars = [new(default, 2, 1, 3, 2, 1), new(default, 20, 2, 0, 4, 2),
            new(default, -10, -5, 5, 8, 3), new(default, 3, 5, 1, 3, 4)];
        await Compare(roots, bars); await Compare(roots, [bars[0]]);
    }
    [Fact]
    public async Task NonfiniteFactorsRetainLegacyValidation()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            IIndicator[] roots = [new VervoortSmoothedOscillator(3, length1: 3, length2: 5, length3: 2, smoothLength: 3, stdDevMult: value)];
            Assert.False(ValuesBarExecution.SupportsOwned(roots)); await Compare(roots, BarsOf([1, 2, 3]));
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
