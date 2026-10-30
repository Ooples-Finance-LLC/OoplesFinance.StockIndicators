using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedPredictionFamilyTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersMesaPredictIndicatorV1) || c.IndicatorType == typeof(EhlersMesaPredictIndicatorV2) || c.IndicatorType == typeof(EhlersAnticipateIndicator))
        .Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new EhlersMesaPredictIndicatorV1(length, length, length, length),
        1 => new EhlersMesaPredictIndicatorV2(length, length, length, length),
        _ => new EhlersAnticipateIndicator(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 5, 12 } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsAndPublishedSlotsRetainBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Repeat(0d, 15).Concat(Enumerable.Repeat(1d, 15)).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task IndependentBoundsKeepForecastWorkBounded(int configuration)
    {
        // Horizon and Burg order remain small while independent window limits are exercised.
        IIndicator root = configuration switch {
            0 => new EhlersMesaPredictIndicatorV1(2, 3, 5, int.MaxValue),
            1 => new EhlersMesaPredictIndicatorV1(2, int.MaxValue, 5, 3),
            2 => new EhlersMesaPredictIndicatorV2(int.MaxValue, int.MaxValue, int.MaxValue, 2),
            _ => new EhlersMesaPredictIndicatorV2(2, 135, 12, int.MaxValue) };
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 33).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.Epsilon)] [InlineData(double.MaxValue)] [InlineData(-double.MaxValue)]
    public async Task FiniteAnticipateBandwidthRetainsClampedCoefficients(double bandwidth)
    {
        var root = new EhlersAnticipateIndicator(12, bandwidth);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 33).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task NonfiniteBandwidthRetainsFallbackAndErrorPriority(double invalid)
    {
        var root = new EhlersAnticipateIndicator(12, invalid);
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], ToBars([1, 2, 3]));
        await Compare([Create(0, 5), root], [new(default, 1, 2, 0, 1, double.NaN)]);
    }

    [Theory]
    [InlineData(4097)] [InlineData(int.MaxValue)]
    public async Task AnticipateLengthLimitKeepsOriginalException(int length)
    {
        var root = new EhlersAnticipateIndicator(length);
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], ToBars([1, 2, 3]));
        await Compare([root], [new(default, 1, 2, 0, 1, double.NaN)]);
    }

    [Fact]
    public async Task MaximumAnticipateLengthKeepsShortImpulseResponse()
    {
        var root = new EhlersAnticipateIndicator(4096);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], ToBars([0, 0, 0, 1]));
    }

    [Fact]
    public void PredictionPrimaryRemainsSecondSlot()
    {
        var first = new EhlersMesaPredictIndicatorV1(); var second = new EhlersMesaPredictIndicatorV2();
        Assert.Same(first.Outputs[1], first.Value); Assert.Same(second.Outputs[1], second.Value);
    }

    [Fact]
    public async Task ExplicitAveragesRetainComponentPaths()
    {
        var bars = ToBars(Enumerable.Range(0, 33).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var average in new IMovingAverage[] { new Sma(3), new Wma(4) })
        foreach (var root in new IIndicator[] { new EhlersMesaPredictIndicatorV2(5, 135, 12, 4, average), new EhlersAnticipateIndicator(12, 1, average) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new EhlersMesaPredictIndicatorV1().Of(new Sma(2)),
            new EhlersMesaPredictIndicatorV2().Of(new Sma(2)), new EhlersAnticipateIndicator().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 5);
        IIndicator[] roots = [repeated, Create(1, 5), Create(2, 12), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 33).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedMesaPredictV1State", Arguments: new object[] { 2, 3, 5, 12 }),
            (Name: "GeneratedMesaPredictV2State", Arguments: new object[] { 5, 135, 12, 4 }),
            (Name: "GeneratedAnticipateState", Arguments: new object[] { 12, 1d }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() {
                var result = new List<double>(); var outputs = new double[3];
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
                }
                return result.ToArray();
            }
            try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); }
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
