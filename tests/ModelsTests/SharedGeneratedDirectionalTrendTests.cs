using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDirectionalTrendTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(DirectionalTrendIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => new DirectionalTrendIndex(length);
    public static IEnumerable<object[]> Cases => new[] { int.MinValue, 1, 2, 14, 30, int.MaxValue }
        .Select(length => new object[] { 0, length });

    [Theory, MemberData(nameof(Cases))]
    public async Task AliasPeriodsRetainAllOutputBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Fact]
    public void ScalarShapeIsRetained() => Assert.Single(new DirectionalTrendIndex(14).Outputs);

    [Fact]
    public async Task InitialRangeUsesZeroPreviousHighAndLow()
    {
        var bars = Enumerable.Range(0, 8).Select(i => new Bar(default, 10, 11 + i, 9, 10, 1)).ToArray();
        var root = new DirectionalTrendIndex(2); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits(Enumerable.Repeat(100d, bars.Length).ToArray(), run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task ExtendedOriginalRangesPreserveThreeSignedAndAbsoluteStages()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0,
                i % 3 == 0 ? scale : 0, i % 3 == 1 ? -scale : 0, 0, i + 1)).ToArray();
            await Compare([new DirectionalTrendIndex(1), new DirectionalTrendIndex(3), new DirectionalTrendIndex(int.MaxValue)], bars);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task EveryExplicitAveragePrefixRetainsFallback(int stages)
    {
        IIndicator root = stages switch {
            1 => new DirectionalTrendIndex(3, new Sma(3)),
            2 => new DirectionalTrendIndex(3, new Sma(3), new Sma(3)),
            3 => new DirectionalTrendIndex(3, new Sma(3), new Sma(3), new Sma(3)),
            4 => new DirectionalTrendIndex(3, new Sma(3), new Sma(3), new Sma(3), new Sma(3)),
            5 => new DirectionalTrendIndex(3, new Sma(3), new Sma(3), new Sma(3), new Sma(3), new Sma(3)),
            _ => new DirectionalTrendIndex(3, new Sma(3), new Sma(3), new Sma(3), new Sma(3), new Sma(3), new Sma(3)) };
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]));
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = new DirectionalTrendIndex(3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        await Compare([selected], bars);
        var repeated = new DirectionalTrendIndex(3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStateReplaysAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 25).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedDirectionalTrendState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 3, bars.Length }, null)!;
        double[] Run() { var result = new List<double>(); foreach (var bar in bars) result.Add(state.Update(in bar)); return result.ToArray(); }
        try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(14)] [InlineData(int.MaxValue)]
    public void FiniteAndWideDifferencesMatchOriginalStateAcrossReset(int length)
    {
        var ranges = new List<(double High, double Low)>
        {
            (0d, -0d), (double.Epsilon, -double.Epsilon),
            (-double.MaxValue, -double.MaxValue), (double.MaxValue, double.MaxValue),
            (double.MaxValue, -double.MaxValue), (1, -1), (0, 0),
            (Math.BitIncrement(1), Math.BitDecrement(1))
        };
        var random = new Random(71831);
        double Finite()
        {
            double value;
            do value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            while (!double.IsFinite(value));
            return value;
        }
        for (var i = 0; i < 512; i++)
        {
            var a = Finite(); var b = Finite();
            ranges.Add((Math.Max(a, b), Math.Min(a, b)));
        }
        var bars = ranges.Select(p => new Bar(default, 0, p.High, p.Low, 0, 1)).ToArray();
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedDirectionalTrendState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { length, bars.Length }, null)!;
        using var reference = new OoplesFinance.StockIndicators.Helpers.DirectionalStrengthWindow(
            OoplesFinance.StockIndicators.Enums.MovingAvgType.ExponentialMovingAverage, length, 10, 5, bars.Length);
        try
        {
            for (var repeat = 0; repeat < 2; repeat++)
            {
                state.Reset(); reference.Reset();
                foreach (var bar in bars)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(reference.Next(bar.High, bar.Low, true)),
                        BitConverter.DoubleToInt64Bits(state.Update(in bar)));
            }
        }
        finally { ((IDisposable)state).Dispose(); }
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
