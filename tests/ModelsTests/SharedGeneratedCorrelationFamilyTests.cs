using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedCorrelationFamilyTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersCorrelationTrendIndicator) || c.IndicatorType == typeof(EhlersCorrelationCycleIndicator) || c.IndicatorType == typeof(EhlersCorrelationAngleIndicator) || c.IndicatorType == typeof(EhlersMarketStateIndicator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new EhlersCorrelationTrendIndicator(length), 1 => new EhlersCorrelationCycleIndicator(length),
        2 => new EhlersCorrelationAngleIndicator(length), _ => new EhlersMarketStateIndicator(length) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 }
        from length in new[] { int.MinValue, 0, 1, 2, 7, 20, 64 } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task CorrelationAxesAnglesAndMarketStateRetainBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Range(0, 47).Select(i => (i * 13 % 101 - 50) / 8d).ToArray(),
            Enumerable.Range(0, 53).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray(),
            Enumerable.Repeat(1d, 25).Concat(Enumerable.Repeat(0d, 25)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersCorrelationTrendIndicator(7).Of(new Sma(2)),
            new EhlersCorrelationCycleIndicator(7).Of(new Sma(2)), new EhlersCorrelationAngleIndicator(7).Of(new Sma(2)),
            new EhlersMarketStateIndicator(7).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(1, 7);
        IIndicator[] roots = [repeated, Create(0, 7), Create(2, 7), Create(3, 7), new EhlersReverseEmaIndicatorV2(), repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData("Trend", 1)] [InlineData("Cycle", 2)] [InlineData("Angle", 1)] [InlineData("MarketState", 1)]
    public void ExactStateReplaysAfterReset(string kind, int slots)
    {
        var bars = ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray());
        var flags = System.Reflection.BindingFlags.NonPublic;
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedCorrelationState", flags)!;
        var mode = Enum.Parse(typeof(ValuesBarExecution).GetNestedType("CorrelationKind", flags)!, kind);
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | flags,
            null, new object[] { 7, mode }, null)!;
        double[] Run() { var all = new List<double>(); foreach (var bar in bars) { var outputs = new double[slots]; state.Update(in bar, outputs); all.AddRange(outputs); } return all.ToArray(); }
        try { var expected = Run(); state.Reset(); Bits(expected, Run()); }
        finally { (state as IDisposable)?.Dispose(); }
    }

    private static Bar[] ToBars(double[] prices) => prices.Select((v, i) => new Bar(default,
        i % 5 == 0 ? 0 : v / 4, Math.Abs(v), i % 7 == 0 ? 0 : Math.Abs(v) / 2, v, 1)).ToArray();

    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(history);
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
                        if (bars.Length > 0) { Assert.Equal(expected.Latest.Bar, actual.Latest.Bar); Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp); }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
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

