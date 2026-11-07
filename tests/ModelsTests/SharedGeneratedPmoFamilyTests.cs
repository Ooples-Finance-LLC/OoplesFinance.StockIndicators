using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedPmoFamilyTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(Pmo) || c.IndicatorType == typeof(PriceMomentumOscillator) || c.IndicatorType == typeof(DecisionPointPriceMomentumOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch
    { 0 => new Pmo(length, signalLength: length), 1 => new PriceMomentumOscillator(length, length, length), _ => new DecisionPointPriceMomentumOscillator(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
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

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task FullFacadeKeepsIndependentPeriods(int axis)
    {
        int[] lengths = [35, 20, 10]; lengths[axis] = 2;
        await Compare([new PriceMomentumOscillator(lengths[0], lengths[1], lengths[2])], ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(1)] [InlineData(14)] [InlineData(1073741820)] [InlineData(1073741821)]
    [InlineData(int.MaxValue - 6)] [InlineData(int.MaxValue - 5)] [InlineData(int.MaxValue)]
    public async Task DecisionPointPreservesDerivedPeriodOverflow(int length)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var decision = new DecisionPointPriceMomentumOscillator(length);
        await Compare([decision], bars);
        if (length > (int.MaxValue - 7) / 2)
        {
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(decision).ConfigureHistory(history);
                await Assert.ThrowsAsync<OverflowException>(() => builder.BuildAsync());
                Assert.Null(builder.LastExecution);
            }
            return;
        }
        var full = new PriceMomentumOscillator(checked(Math.Max(1, length) * 2 + 7), checked(Math.Max(1, length) + 6), 10);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(decision, full).BuildAsync();
        for (var i = 0; i < 2; i++) Bits(run[decision.Outputs[i]].ToArray(), run[full.Outputs[i]].ToArray());
    }

    [Fact]
    public void AliasesKeepTwoAndThreeOutputShapes()
    {
        Assert.Equal(2, new Pmo().Outputs.Count); Assert.Equal(2, new PriceMomentumOscillator().Outputs.Count);
        var root = new DecisionPointPriceMomentumOscillator(14); Assert.Equal(3, root.Outputs.Count);
        Assert.Same(root.Outputs[0], root.Value); Assert.Same(root.Outputs[1], root.Signal); Assert.Same(root.Outputs[2], root.Histogram);
    }

    [Fact]
    public async Task PmoAliasKeepsFixedSecondStage()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var alias = new Pmo(14, signalLength: 3); var full = new PriceMomentumOscillator(14, 20, 3);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(alias, full).BuildAsync();
        for (var i = 0; i < 2; i++) Bits(run[alias.Outputs[i]].ToArray(), run[full.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesEveryWindow()
    {
        await Compare([new Pmo(), new PriceMomentumOscillator(), new DecisionPointPriceMomentumOscillator(14)], ToBars(Enumerable.Range(0, 257).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task ExplicitAveragesKeepFallback()
    {
        foreach (var root in new IIndicator[] { new Pmo(14, new Sma(3)), new PriceMomentumOscillator(maType: new Sma(3)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0])); }
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new Pmo().Of(new Sma(2)), new PriceMomentumOscillator().Of(new Sma(2)), new DecisionPointPriceMomentumOscillator(14).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 14); IIndicator[] roots = [repeated, Create(1, 14), Create(2, 14), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStateReplaysAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        foreach (var histogram in new[] { false, true })
        {
            var type = typeof(ValuesBarExecution).GetNestedType("GeneratedPriceMomentumState", System.Reflection.BindingFlags.NonPublic)!;
            var state = (IMultiOutputState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { 35, 20, 10, bars.Length, histogram }, null)!;
            double[] Run() { var result = new List<double>(); var outputs = new double[histogram ? 3 : 2]; foreach (var bar in bars) { state.Update(in bar, outputs); result.AddRange(outputs); } return result.ToArray(); }
            try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
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
