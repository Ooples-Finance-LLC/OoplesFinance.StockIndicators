using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedLbrLindaTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(LBRPaintBars) || c.IndicatorType == typeof(LindaRaschke310Oscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new LBRPaintBars(length) : new LindaRaschke310Oscillator(length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { kind, length };

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

    public static IEnumerable<object[]> OtherPeriods => from slot in new[] { 0, 1, 2 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { slot, length };
    [Theory, MemberData(nameof(OtherPeriods))]
    public async Task LookbackSlowAndSignalPeriodsRemainIndependent(int slot, int length)
    { IIndicator root = slot == 0 ? new LBRPaintBars(3, length) : slot == 1 ? new LindaRaschke310Oscillator(3, length, 2) : new LindaRaschke310Oscillator(3, 2, length); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, -1])); }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task EveryCustomAverageSlotKeepsFallback(int slot)
    {
        IIndicator root = slot == 0 ? new LBRPaintBars(3, maType: new Sma(2))
            : new LindaRaschke310Oscillator(3, 4, 2, maType: new Sma(2),
                secondAverage: slot >= 2 ? new Sma(2) : null, thirdAverage: slot >= 3 ? new Sma(2) : null, fourthAverage: slot >= 4 ? new Sma(2) : null);
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, -1]));
    }
    [Theory] [InlineData(-2)] [InlineData(-0d)] [InlineData(0)] [InlineData(2.5)] [InlineData(double.MaxValue)]
    public async Task PaintMultiplierPreservesSignAndOverflow(double multiplier)
    { await Compare([new LBRPaintBars(2, 3, multiplier)], ToBars([1, 3, -2, 4, 0, 2, -1])); }
    [Fact]
    public async Task PaintRangeAndCrossedBandsKeepHandValues()
    {
        Bar[] bars = [new(default, 2, 3, 1, 2, 1), new(default, 4, 5, 3, 4, 1), new(default, 1, 2, 0, 1, 1)];
        var root = new LBRPaintBars(1, 2, 1); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([1, 2, 1], run[root.UpperBand].ToArray()); Bits([3, 4, 4], run[root.LowerBand].ToArray()); Bits([2, 3, 4], run[root.Value].ToArray());
    }
    [Fact]
    public async Task LindaPublishesAllSixRoundedStages()
    {
        var root = new LindaRaschke310Oscillator(1, 2, 2); var bars = ToBars([1, 3, 5]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        double[][] expected = [[1, 1, 1], [0, 1, 1], [1, 0, 0], [0, 50, 25], [0, 25, 37.5], [0, 25, -12.5]];
        for (var i = 0; i < expected.Length; i++) Bits(expected[i], run[root.Outputs[i]].ToArray());
    }
    [Fact]
    public async Task OverflowKeepsSignalInvalidAfterPriceRecovers()
    {
        var root = new LindaRaschke310Oscillator(1, 3, 2); var bars = ToBars([double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0]); await Compare([root], bars);
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync());
        Assert.Equal(0, error.OutputSlot); Assert.Equal(2, error.BarIndex);
        Assert.True(double.IsNegativeInfinity(error.Value)); Assert.Equal(IndicatorStartupPolicy.Finite, error.Policy);
        // The public builder rejects the nonfinite output. Inspect the raw kernel
        // separately to retain the propagation assertions after the rejected bar.
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedLindaState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 1, 3, 2, bars.Length }, null)!;
        try
        {
            var values = new List<double[]>();
            foreach (var bar in bars) { var output = new double[6]; state.Update(in bar, output); values.Add(output); }
            Assert.True(double.IsNegativeInfinity(values[2][0]));
            Assert.All(values.Skip(2), v => Assert.True(double.IsNaN(v[1])));
        }
        finally { ((IDisposable)state).Dispose(); }
    }
    [Fact]
    public void OutputOrderAndPrimaryRemainStable()
    {
        var paint = new LBRPaintBars(); Assert.Equal(3, paint.Outputs.Count); Assert.Same(paint.UpperBand, paint.Outputs[0]); Assert.Same(paint.LowerBand, paint.Outputs[1]); Assert.Same(paint.Value, paint.Outputs[2]); Assert.Same(paint.Value, paint.PrimaryOutput);
        var linda = new LindaRaschke310Oscillator(); Assert.Equal(new[] { linda.Value, linda.LindaMacdSignal, linda.LindaMacdHistogram, linda.LindaPpo, linda.LindaPpoSignal, linda.LindaPpoHistogram }, linda.Outputs); Assert.Same(linda.Value, linda.PrimaryOutput);
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3) switch { IndicatorBase scalar => (IIndicator)scalar.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() }; Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void BothKernelsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType(kind == 0 ? "GeneratedLbrPaintState" : "GeneratedLindaState", System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, kind == 0 ? new object[] { 3, 2, 2.5 } : new object[] { 3, 5, 2, bars.Length }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { if (state is IIndicatorState scalar) values.Add(scalar.Update(in bar)); else { var output = new double[kind == 0 ? 3 : 6]; ((IMultiOutputState)state).Update(in bar, output); values.AddRange(output); } } return values.ToArray(); }
        try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
    }
    [Theory] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void MissingPrecedingAverageIsRejected(int slot)
    {
        var error = Assert.Throws<ArgumentException>(() => new LindaRaschke310Oscillator(3, 4, 2,
            secondAverage: slot == 2 ? new Sma(2) : null, thirdAverage: slot == 3 ? new Sma(2) : null,
            fourthAverage: slot == 4 ? new Sma(2) : null));
        Assert.Equal(slot == 2 ? "secondAverage" : slot == 3 ? "thirdAverage" : "fourthAverage", error.ParamName);
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
