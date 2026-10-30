using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedStochasticTests
{
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new Stochastic(length, 3), 1 => new StochasticK(length),
        2 => new StochasticD(length), 3 => new PricePosition(length),
        4 => new StochasticOscillator(length), 5 => new StochasticFastOscillator(length),
        _ => new StochasticRegular(length)
    };
    private static IIndicator Selected(IIndicator indicator) => indicator is MultiOutputIndicatorBase multi
        ? multi.Of(new Sma(2)) : ((IndicatorBase)indicator).Of(new Sma(2));
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 7) from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { kind, length };
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
    [Theory, InlineData(0), InlineData(1), InlineData(2), InlineData(3), InlineData(4), InlineData(5), InlineData(6)]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts(int kind)
    {
        var root = Create(kind, 14); var selected = Selected(Create(kind, 14));
        Assert.False(ValuesBarExecution.SupportsOwned([selected])); var bars = BarsOf([1, 3, 3, 1, 2]);
        await Compare([selected], bars);
        IIndicator[] roots = [root, Create(kind, 3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory, InlineData(0), InlineData(1), InlineData(2)]
    public void OutputLayoutsResetAcrossWindowWraps(int layout)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedStochasticState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [14, 3, 2, layout == 2 ? OoplesFinance.StockIndicators.Enums.MovingAvgType.ExponentialMovingAverage : OoplesFinance.StockIndicators.Enums.MovingAvgType.SimpleMovingAverage, layout, 65], null)!;
        var state = (IMultiOutputState)disposable;
        double[] Run()
        {
            var values = new List<double>();
            foreach (var bar in BarsOf(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()))
            { var outputs = new double[layout == 0 ? 3 : 2]; state.Update(in bar, outputs); values.AddRange(outputs); }
            return values.ToArray();
        }
        var expected = Run(); state.Reset(); Bits(expected, Run());
    }
    [Fact]
    public void ScalarDResetsAcrossWindowWraps()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedStochasticDState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [3, 65], null)!;
        var state = (IIndicatorState)disposable;
        double[] Run() => BarsOf(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()).Select(bar => state.Update(in bar)).ToArray();
        var expected = Run(); state.Reset(); Bits(expected, Run());
    }
    [Fact]
    public async Task CustomAveragesKeepLegacyRouting()
    {
        IIndicator[] roots = [new Stochastic(3, 4, new Wma()), new StochasticK(3, 4, 5, new Wma()),
            new PricePosition(3, 4, 5, new Wma()), new StochasticOscillator(3, 4, 5, new Wma(), new Sma()),
            new StochasticFastOscillator(3, 4, 5, new Wma()), new StochasticRegular(3, 4, new Wma())];
        foreach (var root in roots) { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], BarsOf([1, 3, 3, 1, 2])); }
    }
    [Fact]
    public async Task DistinctSmoothingHorizonsKeepEveryOutput()
    {
        IIndicator[] roots = [new Stochastic(5, 7), new StochasticK(5, 7, 2), new PricePosition(5, 7, 2),
            new StochasticOscillator(5, 7, 2), new StochasticFastOscillator(5, 7, 2), new StochasticRegular(5, 7)];
        await Compare(roots, BarsOf(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }
    [Fact]
    public void MaximumPeriodsKeepRawPrefixAndZeroSmoothingWarmup()
    {
        var bars = BarsOf([1, 3, 3, 1, 2]);
        var expected = OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.RoundedStochasticK(bars, int.MaxValue);
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedStochasticState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [int.MaxValue, int.MaxValue, int.MaxValue, OoplesFinance.StockIndicators.Enums.MovingAvgType.SimpleMovingAverage, 0, bars.Length], null)!;
        var state = (IMultiOutputState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < bars.Length; i++)
            { var outputs = new double[3]; state.Update(in bars[i], outputs); Bits([expected[i], 0, 0], outputs); }
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
