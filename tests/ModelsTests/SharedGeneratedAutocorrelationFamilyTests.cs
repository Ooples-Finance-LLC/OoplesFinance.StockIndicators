using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedAutocorrelationFamilyTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersAutoCorrelationIndicator) || c.IndicatorType == typeof(EhlersAutoCorrelationPeriodogram) || c.IndicatorType == typeof(EhlersAdaptiveBandPassFilter) || c.IndicatorType == typeof(EhlersZeroCrossingsDominantCycle)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    private static IIndicator Create(int kind, int upper, int lower = 3, int lag = 3, double bandwidth = .3) => kind switch {
        0 => new EhlersAutoCorrelationIndicator(upper, lower), 1 => new EhlersAutoCorrelationPeriodogram(upper, lower, lag),
        2 => new EhlersAdaptiveBandPassFilter(upper, lower, lag, bandwidth), _ => new EhlersZeroCrossingsDominantCycle(upper, bandwidth) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 }
        from length in new[] { int.MinValue, 0, 1, 2, 7, 20 } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task ExactKernelsRetainBitsAcrossStartupAndLag(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Range(0, 65).Select(i => (i * 13 % 101 - 50) / 8d).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(7, 2, 0)] [InlineData(7, 2, -1)] [InlineData(7, 2, 3)]
    [InlineData(2, 7, 3)] [InlineData(48, 10, 3)] [InlineData(1, 1, 1)]
    public async Task IndependentRoofingAndSpectrumPeriodsKeepOutputSlots(int upper, int lower, int lag)
    {
        var bars = ToBars(Enumerable.Range(0, 129).Select(i => Math.Sin(2 * Math.PI * i / 11) + (i % 7) / 8d).ToArray());
        await Compare([Create(0, upper, lower, lag), Create(1, upper, lower, lag), Create(2, upper, lower, lag)], bars);
    }

    [Fact]
    public async Task LargePeriodsWithUnavailableLagsRetainContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1]);
        await Compare([Create(0, int.MaxValue, 3), Create(1, int.MaxValue, 3, int.MaxValue),
            Create(2, int.MaxValue, 3, int.MaxValue), Create(3, int.MaxValue)], bars);
    }

    [Theory]
    [InlineData(0)] [InlineData(double.Epsilon)] [InlineData(.3)] [InlineData(double.MaxValue)]
    public async Task FiniteBandwidthKeepsBothBandAndCyclePaths(double bandwidth)
    {
        IIndicator[] roots = [Create(2, 7, 2, 3, bandwidth), Create(3, 7, 2, 3, bandwidth)];
        Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray()));
    }

    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task InvalidBandwidthKeepsOptionsValidationPriority(double bandwidth)
    {
        foreach (var kind in new[] { 2, 3 })
        {
            var root = Create(kind, 7, 2, 3, bandwidth);
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []); await Compare([root], ToBars([1, 3, 2]));
            await Compare([new EhlersAutoCorrelationIndicator(3, 2), root], [new(default, 1, 2, 0, 1, double.NaN)]);
        }
    }

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersAutoCorrelationIndicator(7, 2).Of(new Sma(2)),
            new EhlersAutoCorrelationPeriodogram(7, 2, 3).Of(new Sma(2)), new EhlersAdaptiveBandPassFilter(7, 2, 3).Of(new Sma(2)),
            new EhlersZeroCrossingsDominantCycle(7).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(2, 7, 2);
        IIndicator[] roots = [repeated, Create(0, 7, 2), Create(1, 7, 2), Create(3, 7), new EhlersHurstCoefficient(7, 3), repeated];
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
        foreach (var config in new[] { (Name: "GeneratedRoofAutocorrelationState", Arguments: new object[] { 7, 2 }),
            (Name: "GeneratedAutocorrelationSpectrumState", Arguments: new object[] { 7, 2, 3 }),
            (Name: "GeneratedAdaptiveBandPassState", Arguments: new object[] { 7, 2, 3, .3 }),
            (Name: "GeneratedZeroCrossingCycleState", Arguments: new object[] { 7, .3 }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() {
                var result = new List<double>();
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { var outputs = new double[2]; ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
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
