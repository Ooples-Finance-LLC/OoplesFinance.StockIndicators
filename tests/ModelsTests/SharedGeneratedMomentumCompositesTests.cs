using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedMomentumCompositesTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(CoppockCurve) || c.IndicatorType == typeof(DerivativeOscillator) || c.IndicatorType == typeof(DidiIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch
    { 0 => new CoppockCurve(length), 1 => new DerivativeOscillator(length), _ => new DidiIndex(length, length, length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 14, 100, 1024 } select new object[] { kind, length };

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
    public async Task DidiKeepsIndependentPeriodAxes(int axis)
    {
        int[] lengths = [3, 8, 20]; lengths[axis] = 2;
        await Compare([new DidiIndex(lengths[0], lengths[1], lengths[2])], ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public void DidiKeepsShortMediumLongOrder()
    {
        var root = new DidiIndex(); Assert.Equal(3, root.Outputs.Count);
        Assert.Same(root.Outputs[0], root.Value); Assert.Same(root.Outputs[1], root.Media); Assert.Same(root.Outputs[2], root.Longa);
        Assert.Single(new CoppockCurve().Outputs); Assert.Single(new DerivativeOscillator().Outputs);
    }

    [Fact]
    public async Task LongHistoryCrossesEveryWindow()
    {
        await Compare([new CoppockCurve(), new DerivativeOscillator(), new DidiIndex()], ToBars(Enumerable.Range(0, 257).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task ExplicitAveragePrefixesKeepFallback()
    {
        var roots = new IIndicator[] { new CoppockCurve(10, new Sma(3)), new DerivativeOscillator(14, new Sma(3)),
            new DerivativeOscillator(14, new Sma(3), new Sma(4)), new DidiIndex(maType: new Sma(3)),
            new DidiIndex(maType: new Sma(3), secondAverage: new Sma(4)), new DidiIndex(maType: new Sma(3), secondAverage: new Sma(4), thirdAverage: new Sma(5)) };
        foreach (var root in roots)
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0])); }
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new CoppockCurve().Of(new Sma(2)), new DerivativeOscillator().Of(new Sma(2)), new DidiIndex().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 10); IIndicator[] roots = [repeated, Create(1, 14), Create(2, 3), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedCoppockState", Arguments: new object[] { 10, bars.Length }),
            (Name: "GeneratedDerivativeState", Arguments: new object[] { 14 }),
            (Name: "GeneratedDidiState", Arguments: new object[] { 3, 8, 20, bars.Length }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() { var result = new List<double>(); var outputs = new double[3]; foreach (var bar in bars) {
                if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar)); else { ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
            } return result.ToArray(); }
            try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); }
            finally { ((IDisposable)state).Dispose(); }
        }
    }

    [Fact]
    public async Task HugeOwnedPeriodsKeepBoundedStorageAndDidiStartup()
    {
        var bars = ToBars(Enumerable.Range(0, 25).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var roots = Enumerable.Range(0, 3).Select(k => Create(k, int.MaxValue)).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        foreach (var output in roots[2].Outputs) Bits(new double[bars.Length], run[output].ToArray());
        foreach (var root in roots.Take(2)) Assert.All(run[root.Outputs[0]].ToArray(), value => Assert.True(double.IsFinite(value)));
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
