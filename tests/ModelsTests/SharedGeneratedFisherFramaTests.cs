using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedFisherFramaTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(Frama) || c.IndicatorType == typeof(EhlersFrama) || c.IndicatorType == typeof(FisherTransform) || c.IndicatorType == typeof(EhlersFisherTransform) || c.IndicatorType == typeof(EhlersInverseFisherTransform) || c.IndicatorType == typeof(EhlersRelativeStrengthIndexInverseFisherTransform)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3, 4, 5 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, 4096, int.MaxValue - 1, int.MaxValue } select new object[] { kind, length };
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new Frama(length),
        1 => new EhlersFrama(length),
        2 => new FisherTransform(length),
        3 => new EhlersFisherTransform(length),
        4 => new EhlersInverseFisherTransform(length),
        _ => new EhlersRelativeStrengthIndexInverseFisherTransform(length)
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task ExactKernelsRetainLegacyBits(int kind, int length)
    {
        var root = Create(kind, length);
        Assert.Equal(kind >= 2 || length < int.MaxValue, ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            [-.2, .1, -.05, .3, .15, 8, -9, .05],
            Enumerable.Range(0, 67).Select(i => (i * 13 % 101 - 50) / 80d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task SelectedSourcesKeepTheirFallbackContracts()
    {
        var bars = ToBars([.01, .02, -.03, .05, -.02, 0, .04]);
        foreach (var root in new IIndicator[] {
            new EhlersInverseFisherTransform(3, new Ema(2)),
            new EhlersRelativeStrengthIndexInverseFisherTransform(3, 2, new Ema(2), new Wma(2), new Sma(2)),
            new Frama(3).Of(new Sma(2)),
            new EhlersFrama(3).Of(new Sma(2)),
            new FisherTransform(3).Of(new Sma(2)),
            new EhlersFisherTransform(3).Of(new Sma(2)),
            new EhlersInverseFisherTransform(3).Of(new Sma(2)),
            new EhlersRelativeStrengthIndexInverseFisherTransform(3).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], bars);
        }
    }

    [Fact]
    public async Task MixedRootsErrorsCancellationAndOwnership()
    {
        var bars = ToBars([.01, .02, -.03, .05, -.02, 0, .04]);
        var repeated = Create(1, 3);
        IIndicator[] roots = [Create(0, 3), Create(2, 3), Create(3, 3), Create(4, 3), Create(5, 3), new EhlersModifiedRelativeStrengthIndex(7, 3, 5), repeated, repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactWindowsReplayAfterReset()
    {
        double[] prices = [.01, .02, -.03, .05, -.02, 0, .04];
        var bars = ToBars(prices);
        foreach (var configuration in new[] {
            (Name: "GeneratedFramaState", Arguments: new object[] { 3 }),
            (Name: "GeneratedFisherState", Arguments: new object[] { 3 }),
            (Name: "GeneratedInverseRsiFisherState", Arguments: new object[] { 3, 2, bars.Length }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(configuration.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = (IIndicatorState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, configuration.Arguments, null)!;
            try
            {
                var expected = bars.Select(bar => state.Update(in bar)).ToArray(); state.Reset();
                Bits(expected, bars.Select(bar => state.Update(in bar)).ToArray());
            }
            finally { (state as IDisposable)?.Dispose(); }
        }
    }

    [Theory]
    [InlineData(int.MinValue, 0)] [InlineData(1, 1)] [InlineData(3, 2)]
    [InlineData(2, 9)] [InlineData(int.MaxValue, 3)] [InlineData(3, int.MaxValue)]
    public async Task InverseFisherIndependentStagesRetainPublishedRsiArithmetic(int length, int signal)
    {
        await Compare([new EhlersRelativeStrengthIndexInverseFisherTransform(length, signal)],
            ToBars([0, 0, 1, -1, 0, double.MaxValue, -double.MaxValue, 2, 2, 3, 0, 4]));
    }

    [Fact]
    public async Task FramaOriginalRangesAndOddPeriodOverflowPreserveContracts()
    {
        var bars = new[] { new Bar(default, 1, double.MaxValue, -double.MaxValue, 1, 1),
            new Bar(default, 1, -double.MaxValue, double.MaxValue, 2, 1),
            new Bar(default, 1, 1, 1, 3, 1), new Bar(default, 1, 2, 1, 4, 1),
            new Bar(default, 1, 3, 0, 2, 1), new Bar(default, 1, 2, 1, 3, 1), new Bar(default, 1, 1, 0, 2, 1) };
        await Compare([new Frama(3), new EhlersFrama(3)], bars);
        foreach (var root in new IIndicator[] { new Frama(int.MaxValue), new EhlersFrama(int.MaxValue) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []); await Compare([root], bars);
            var invalid = bars.ToArray(); invalid[^1] = new(default, 1, 2, 0, 1, double.NaN);
            await Compare([new EhlersFisherTransform(3), root], invalid);
        }
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

