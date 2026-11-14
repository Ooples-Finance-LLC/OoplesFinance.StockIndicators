using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedEhlersPhaseGravityTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersAllPassPhaseShifter) || c.IndicatorType == typeof(EhlersCenterOfGravityOscillator) || c.IndicatorType == typeof(EhlersCenterofGravityOscillator) || c.IndicatorType == typeof(EhlersChebyshevLowPassFilter)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, 4096, int.MaxValue } select new object[] { kind, length };
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new EhlersAllPassPhaseShifter(length),
        1 => new EhlersCenterOfGravityOscillator(length),
        2 => new EhlersCenterofGravityOscillator(length),
        _ => new EhlersChebyshevLowPassFilter(length)
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task ExactKernelsRetainLegacyBits(int kind, int length)
    {
        var root = Create(kind, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
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
        foreach (var root in new IIndicator[] { new EhlersAllPassPhaseShifter(3).Of(new Sma(2)), new EhlersCenterOfGravityOscillator(3).Of(new Sma(2)), new EhlersCenterofGravityOscillator(3).Of(new Sma(2)), new EhlersChebyshevLowPassFilter(3).Of(new Sma(2)) })
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
        IIndicator[] roots = [Create(0, 3), Create(2, 3), Create(3, 3), new VervoortModifiedBollingerBandIndicator(2, 2, 1), repeated, repeated];
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
        var window0 = new AllPassPhaseWindow(3, .5);
        var expected0 = prices.Select(value => window0.Next(value, true)).ToArray();
        window0.Reset();
        Bits(expected0, prices.Select(value => window0.Next(value, true)).ToArray());
        var window1 = new CenterGravityWindow(3, prices.Length);
        var expected1 = prices.Select(value => window1.Next(value, true)).ToArray();
        window1.Reset();
        Bits(expected1, prices.Select(value => window1.Next(value, true)).ToArray());
        var window2 = new ChebyshevWaveWindow(0);
        var expected2 = prices.Select(value => window2.Next(value, true)).ToArray();
        window2.Reset();
        Bits(expected2, prices.Select(value => window2.Next(value, true)).ToArray());
        var window3 = new ChebyshevWaveWindow(8);
        var expected3 = prices.Select(value => window3.Next(value, true)).ToArray();
        window3.Reset();
        Bits(expected3, prices.Select(value => window3.Next(value, true)).ToArray());
        window1.Dispose();
    }

    [Fact]
    public async Task IgnoredRippleAndAllNineOutputsRetainLegacyBehavior()
    {
        var bars = ToBars([1, -2, 0, double.Epsilon, 3, 4, -1]);
        foreach (var ripple in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity, double.MaxValue, -1, 0, .5 })
        {
            var root = new EhlersChebyshevLowPassFilter(3, ripple);
            Assert.True(ValuesBarExecution.SupportsOwned([root]));
            Assert.Equal(9, root.Outputs.Count);
            await Compare([root], bars);
        }
        var selected = new EhlersChebyshevLowPassFilter(3);
        var consumer = new Sma(2).Of(selected);
        Assert.False(ValuesBarExecution.SupportsOwned([consumer]));
        await Compare([consumer], bars);
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

