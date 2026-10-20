using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedTargetReturnsTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(TreynorRatio) || c.IndicatorType == typeof(UpsidePotentialRatio)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new TreynorRatio(length) : new UpsidePotentialRatio(length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, 1024 } select new object[] { kind, length };

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
    public async Task LaggedReturnsAndBetaRetainHandValues()
    {
        var root=new TreynorRatio(2,2,0);var bars=ToBars([1,2,4,1,2]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0,0,.75,.625,-.25],run[root.Value].ToArray());
    }
    [Fact]
    public async Task DownsideSquareAndFullDenominatorRetainHandValues()
    {
        var root=new UpsidePotentialRatio(2,0);var bars=ToBars([1,2,4,1,2]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0,0,0,Math.Sqrt(18),0],run[root.Value].ToArray());
    }
    [Theory] [InlineData(-double.MaxValue)] [InlineData(-1d)] [InlineData(0d)] [InlineData(2d)] [InlineData(double.MaxValue)]
    public async Task FiniteBetaKeepsBits(double beta)
    {await Compare([new TreynorRatio(2,beta,0)],ToBars([1,2,4,1,2]));}
    [Theory] [InlineData(-2d)] [InlineData(-1d)] [InlineData(0d)] [InlineData(.02)] [InlineData(.05)] [InlineData(double.MaxValue)]
    public async Task PeriodBenchmarksKeepValuesOrOriginalErrors(double benchmark)
    {await Compare([new TreynorRatio(3,1,benchmark),new UpsidePotentialRatio(3,benchmark)],ToBars([1,2,4,1,2]));}
    [Fact]
    public void NonfiniteParametersKeepLegacyRouting()
    {foreach(var value in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var root in new IIndicator[]{new TreynorRatio(3,value,0),new TreynorRatio(3,1,value),new UpsidePotentialRatio(3,value)})Assert.False(ValuesBarExecution.SupportsOwned([root]));}
    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task MaximumZeroBenchmarkPeriodKeepsLaggedZeroStartup(int kind)
    {
        IIndicator root=kind==0 ? new TreynorRatio(int.MaxValue,2,0) : new UpsidePotentialRatio(int.MaxValue,0);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1,2,4]))).ConfigureIndicators(root).BuildAsync();
        Bits([0,0,0],run[root.Outputs[0]].ToArray());
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void OutputsRemainScalar(int kind){Assert.Single(Create(kind,3).Outputs);}
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
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedTargetReturnState", System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object?[] { 3, 0d, kind == 1, bars.Length, kind == 0 ? (double?)2 : null }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { if (state is IIndicatorState scalar) values.Add(scalar.Update(in bar)); else { var output = new double[2]; ((IMultiOutputState)state).Update(in bar, output); values.AddRange(output); } } return values.ToArray(); }
        try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
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
