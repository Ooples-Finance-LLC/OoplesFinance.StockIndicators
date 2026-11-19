using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedCycleDecompositionTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersCycleAmplitude) || c.IndicatorType == typeof(EhlersTrendExtraction)
            || c.IndicatorType == typeof(EhlersEmpiricalModeDecomposition)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length, double delta = .1) => kind switch {
        0 => new EhlersCycleAmplitude(length, delta), 1 => new EhlersTrendExtraction(length, delta),
        _ => new EhlersEmpiricalModeDecomposition(length, length, delta) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 20, 65 } select new object[] { kind, length };
    private static IEnumerable<double[]> Prices() => new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
        [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
        [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
        Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
        Enumerable.Range(0, 129).Select(i => Math.Sin(i * .7) + .25 * Math.Sin(i * .11)).ToArray() };

    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsAndOutputSlotsRetainBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.Epsilon)] [InlineData(.1)]
    [InlineData(1)] [InlineData(double.MaxValue)] [InlineData(-double.MaxValue)]
    public async Task FiniteDeltasKeepClampedPhaseArithmetic(double delta)
    {
        foreach (var kind in Enumerable.Range(0, 3))
        {
            var root = Create(kind, 20, delta); Assert.True(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(-.1)] [InlineData(double.Epsilon)] [InlineData(.1)]
    [InlineData(double.MaxValue)] [InlineData(-double.MaxValue)]
    public async Task FiniteFractionsKeepExactPeakAndValleyPublication(double fraction)
    {
        var root = new EhlersEmpiricalModeDecomposition(2, 3, .5, fraction);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task NonfiniteParametersKeepFallbackAndValidationPriority(double invalid)
    {
        foreach (var root in new IIndicator[] { Create(0, 20, invalid), Create(1, 20, invalid), Create(2, 20, invalid),
            new EhlersEmpiricalModeDecomposition(20, 50, .5, invalid) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []); await Compare([root], ToBars([1, 3, 2]));
            await Compare([Create(1, 20), root], [new(default, 1, 2, 0, 1, double.NaN)]);
        }
    }

    [Fact]
    public async Task HugeAmplitudePeriodUsesAvailableBatchCapacity()
    {
        // The finite batch core bounds pooled capacity by observed input; the streaming state has no such hint.
        foreach (var count in new[] { 0, 1, 65 })
        {
            var prices = Enumerable.Range(0, count).Select(i => Math.Sin(i * .7)).ToArray();
            var expected = new double[count]; OscillatorCore.EhlersCycleAmplitude(prices, expected, int.MaxValue, .1);
            var root = new EhlersCycleAmplitude(int.MaxValue, .1);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars(prices)))
                .ConfigureIndicators(root).ConfigureHistory(IndicatorHistoryMode.Full).BuildAsync();
            Assert.False(((IndicatorRun)run).HasLegacyRuntime); Bits(expected, run[root.Outputs[0]].ToArray());
        }
    }

    [Fact]
    public async Task IndependentAndHugeSmoothingPeriodsRetainBits()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var pair in new[] { (1, 7), (7, 1), (20, 50), (int.MaxValue, 1), (1, int.MaxValue) })
            await Compare([new EhlersEmpiricalModeDecomposition(pair.Item1, pair.Item2)], bars);
        await Compare([new EhlersTrendExtraction(int.MaxValue)], bars);
    }

    [Fact]
    public async Task ExplicitAveragesKeepTheirComponentPaths()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var root in new IIndicator[] { new EhlersTrendExtraction(20, .1, new Wma(3)),
            new EhlersEmpiricalModeDecomposition(20, 50, .5, .1, new Sma(3)),
            new EhlersEmpiricalModeDecomposition(20, 50, .5, .1, new Sma(3), new Wma(4), new Ema(5)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }

    [Fact]
    public async Task SourcesMixedRootsAndErrorsKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersCycleAmplitude().Of(new Sma(2)),
            new EhlersTrendExtraction().Of(new Sma(2)), new EhlersEmpiricalModeDecomposition().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(2, 20);
        IIndicator[] roots = [repeated, Create(0, 20), Create(1, 20), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedCycleAmplitudeState", Arguments: new object[] { 20, .1, bars.Length }, Slots: 1),
            (Name: "GeneratedTrendExtractionState", Arguments: new object[] { 20, .1 }, Slots: 2),
            (Name: "GeneratedEmpiricalDecompositionState", Arguments: new object[] { 20, 50, .5, .1 }, Slots: 3) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() {
                var result = new List<double>();
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { var outputs = new double[config.Slots]; ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
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
