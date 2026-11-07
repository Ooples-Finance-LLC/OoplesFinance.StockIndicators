using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedElderPairTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(ElderImpulseSystem) || c.IndicatorType == typeof(ElderRayIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new ElderImpulseSystem(length)
        : new ElderRayIndex(length);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2)
        from length in new[] { int.MinValue, 1, 2, 14, 65, int.MaxValue } select new object[] { kind, length };

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
    public void BothFacadesRemainScalar()
    { Assert.Single(new ElderImpulseSystem().Outputs); Assert.Single(new ElderRayIndex().Outputs); }

    [Fact]
    public async Task InitialImpulseAndBullPowerUseOriginalHigh()
    {
        var impulse = new ElderImpulseSystem(1); var ray = new ElderRayIndex(1);
        Bar[] bars = [new(default, 99, 10, 2, 5, 1), new(default, 99, 12, 3, 6, 1), new(default, 99, 14, 1, 4, 1)];
        await Compare([impulse, ray], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(impulse, ray).BuildAsync();
        Bits([0, 0, 0], run[impulse.Outputs[0]].ToArray()); Bits([5, 6, 10], run[ray.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task ExtremePricesCrossFixedMacdStartupAndRecurrence()
    {
        var prices = Enumerable.Range(0, 96).Select(i => i % 29 < 13 ? double.MaxValue : -double.MaxValue).ToArray();
        await Compare([new ElderImpulseSystem(13), new ElderRayIndex(13)], ToBars(prices));
    }

    [Fact]
    public async Task LongHistoryCrossesTrendAndSignalChanges()
    {
        await Compare([new ElderImpulseSystem(13), new ElderRayIndex(13)], ToBars(Enumerable.Range(0, 129).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task RayDerivesRangeForOutsideClosesAndReturnsToOriginalHigh()
    {
        var ray = new ElderRayIndex(1);
        Bar[] bars = [new(default, 99, 3, 1, -4, 1), new(default, 99, 10, 2, 5, 1),
            new(default, 99, 7, 1, -2, 1), new(default, 99, 9, 1, -3, 1), new(default, 99, 6, 2, 4, 1)];
        await Compare([ray], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(ray).BuildAsync();
        // Length-one EMA equals close. Outside ranges use max(previous close, close),
        // with the first close as its own predecessor; inside ranges keep candle high.
        Bits([0, 5, 7, 1, 2], run[ray.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task ExplicitRayAverageKeepsFallback()
    {
        var root = new ElderRayIndex(3, new Sma(3)); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -2, 0, 1, 3, -1, 4]));
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new ElderImpulseSystem(3).Of(new Sma(2)), new ElderRayIndex(3).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 13), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StatesReplayAfterReset(bool ray)
    {
        var bars = ToBars(Enumerable.Range(0, 39).Select(i => Math.Sin(i * .7)).ToArray());
        var name = ray ? "GeneratedElderRayState" : "GeneratedElderImpulseState";
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 3 }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) values.Add(state.Update(in bar)); return values.ToArray(); }
        var expected = Run(); state.Reset(); Bits(expected, Run());
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
