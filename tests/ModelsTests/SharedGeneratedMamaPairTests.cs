using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedMamaPairTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(FollowingAdaptiveMovingAverage) || c.IndicatorType == typeof(EhlersMesaAdaptiveMovingAverage))
        .Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from following in new[] { false, true }
        from length in new[] { int.MinValue, 1, 2, 5, 20, int.MaxValue } select new object[] { following, length };
    private static IEnumerable<double[]> Prices() => new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
        [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
        [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
        Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
        Enumerable.Range(0, 129).Select(i => Math.Sin(i * .7) + .25 * Math.Sin(i * .11)).ToArray() };

    [Theory, MemberData(nameof(Cases))]
    public async Task LengthMetadataAndEveryOutputRetainBits(bool following, int length)
    {
        IIndicator root = following ? new FollowingAdaptiveMovingAverage(length) : new EhlersMesaAdaptiveMovingAverage(length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(-.5, -.05)] [InlineData(.05, .5)]
    [InlineData(double.Epsilon, -double.Epsilon)] [InlineData(double.MaxValue, .05)]
    [InlineData(.5, -double.MaxValue)] [InlineData(-double.MaxValue, double.MaxValue)]
    public async Task AllFiniteCoefficientPairsRetainValidationAndArithmetic(double fast, double slow)
    {
        var root = new EhlersMesaAdaptiveMovingAverage(20, fast, slow); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task NonfiniteCoefficientsKeepFallbackAndValidationPriority(double invalid)
    {
        foreach (var root in new[] { new EhlersMesaAdaptiveMovingAverage(20, invalid, .05), new EhlersMesaAdaptiveMovingAverage(20, .5, invalid) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []); await Compare([root], ToBars([1, 3, 2]));
            await Compare([new FollowingAdaptiveMovingAverage(20), root], [new(default, 1, 2, 0, 1, double.NaN)]);
        }
    }

    [Fact]
    public async Task FamaSelectionAndMamaPrimarySlotKeepLengthIndependentValues()
    {
        var following = new FollowingAdaptiveMovingAverage(2); var mama = new EhlersMesaAdaptiveMovingAverage(17);
        Assert.Same(mama.Outputs[0], mama.Fama); Assert.Same(mama.Outputs[1], mama.Value); Assert.Same(mama.Value, mama.PrimaryOutput);
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        await Compare([following, mama], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(following, mama).BuildAsync();
        Bits(run[following.Outputs[0]].ToArray(), run[mama.Fama].ToArray());
        Assert.Equal(2, following.WarmupBars); Assert.Equal(17, mama.WarmupBars);
    }

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new FollowingAdaptiveMovingAverage(20).Of(new Sma(2)),
            new EhlersMesaAdaptiveMovingAverage(20).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = new EhlersMesaAdaptiveMovingAverage(20);
        IIndicator[] roots = [repeated, new FollowingAdaptiveMovingAverage(20), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
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
            (Name: "GeneratedFollowingMamaState", Arguments: Array.Empty<object>()),
            (Name: "GeneratedMamaOutputsState", Arguments: new object[] { .5, .05 }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() {
                var result = new List<double>();
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { var outputs = new double[8]; ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
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
