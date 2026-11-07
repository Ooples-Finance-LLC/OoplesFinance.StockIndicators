using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSignalNoiseTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersSignalToNoiseRatioV1) || c.IndicatorType == typeof(EhlersSignalToNoiseRatioV2)
            || c.IndicatorType == typeof(EhlersAlternateSignalToNoiseRatio) || c.IndicatorType == typeof(EhlersEnhancedSignalToNoiseRatio))
        .Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new EhlersSignalToNoiseRatioV1(length),
        1 => new EhlersSignalToNoiseRatioV2(length, MovingAvgType.ExponentialMovingAverage),
        2 => new EhlersAlternateSignalToNoiseRatio(length), _ => new EhlersEnhancedSignalToNoiseRatio(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 4)
        from length in new[] { int.MinValue, 1, 2, 7, 48, int.MaxValue } select new object[] { kind, length };
    private static IEnumerable<double[]> Prices() => new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
        [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
        [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
        Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
        Enumerable.Range(0, 129).Select(i => Math.Sin(i * .7) + .25 * Math.Sin(i * .11)).ToArray() };

    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsAndEveryNoiseOutputRetainBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)] [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)] [InlineData((MovingAvgType)int.MaxValue)]
    public async Task V2RetainsIgnoredAverageMetadata(MovingAvgType average)
    {
        var root = new EhlersSignalToNoiseRatioV2(6, average); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task ExplicitV1AveragesKeepComponentFallback()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var root in new IIndicator[] { new EhlersSignalToNoiseRatioV1(7, new Sma(3)),
            new EhlersSignalToNoiseRatioV1(7, new Wma(3)), new EhlersSignalToNoiseRatioV1(7, new Ema(3)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }

    [Fact]
    public async Task OriginalRangesAndEnhancedSlotsRemainDistinct()
    {
        var bars = Enumerable.Range(0, 67).Select(i => new Bar(default, i % 2 == 0 ? 100 : -100,
            i % 3 == 0 ? -20 : 30, i % 5 == 0 ? 40 : -10, Math.Sin(i / 3d), i + 1)).ToArray();
        await Compare([Create(0, 7), Create(1, 6), Create(2, 6), Create(3, 6)], bars);
        foreach (var kind in Enumerable.Range(0, 4))
            await Compare([Create(kind, 6)], Enumerable.Range(0, 33).Select(i => new Bar(default, 0, 0, 0, Math.Sin(i), 1)).ToArray());
    }

    [Fact]
    public async Task SourcesMixedRootsAndErrorsKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersSignalToNoiseRatioV1().Of(new Sma(2)),
            new EhlersSignalToNoiseRatioV2(6, MovingAvgType.ExponentialMovingAverage).Of(new Sma(2)),
            new EhlersAlternateSignalToNoiseRatio().Of(new Sma(2)), new EhlersEnhancedSignalToNoiseRatio().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(3, 6);
        IIndicator[] roots = [repeated, Create(0, 7), Create(1, 6), Create(2, 6), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedHilbertNoiseState", Arguments: new object[] { 7 }),
            (Name: "GeneratedMamaNoiseState", Arguments: new object[] { 6, 0 }),
            (Name: "GeneratedMamaNoiseState", Arguments: new object[] { 6, 1 }),
            (Name: "GeneratedEnhancedMamaNoiseState", Arguments: new object[] { 6 }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() {
                var result = new List<double>();
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { var outputs = new double[4]; ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
                }
                return result.ToArray();
            }
            try {
                var expected = Run();
                if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset();
                Bits(expected, Run());
            }
            finally { (state as IDisposable)?.Dispose(); }
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
