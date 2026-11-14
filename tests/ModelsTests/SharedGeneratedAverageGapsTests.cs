using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedAverageGapsTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(PpoMa) || c.IndicatorType == typeof(QmaSmaDifference) || c.IndicatorType == typeof(QuadraticMovingAverage)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IndicatorBase Create(int kind, int length) => kind switch {0=>new PpoMa(length),1=>new QmaSmaDifference(length),_=>new QuadraticMovingAverage(length)};
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, 1024 } select new object[] { kind, length };

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

    [Theory] [InlineData(int.MinValue)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(14)] [InlineData(65)] [InlineData(int.MaxValue)]
    public async Task PpoSlowPeriodRemainsIndependent(int length)
    {await Compare([new PpoMa(3,length)],ToBars([1,3,2,0,4,8,-1]));}
    [Fact]
    public async Task PpoUsesPartialSeedAndPercentageGap()
    {
        var root=new PpoMa(1,2);var bars=ToBars([1,3,2]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits([0,50,0],run[root.Value].ToArray());
    }
    [Fact]
    public async Task PpoZeroSlowAveragePublishesZero()
    {
        var root=new PpoMa(1,2);var bars=ToBars([1,-1]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits([0,0],run[root.Value].ToArray());
    }
    [Theory] [InlineData(1)] [InlineData(2)]
    public async Task RmsPartialWindowAndSmaStartupKeepDistinctContracts(int kind)
    {
        var root=Create(kind,2);var bars=ToBars([3,4,0]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits(kind==1 ? [3,Math.Sqrt(12.5)-3.5,Math.Sqrt(8)-2] : [3,Math.Sqrt(12.5),Math.Sqrt(8)],run[root.Outputs[0]].ToArray());
    }
    [Theory] [InlineData(1)] [InlineData(2)]
    public async Task MaximumPeriodUsesOnlyObservedRmsHistory(int kind)
    {
        // Original standalone smoothers allocate by period; the shared route is bounded by this run.
        var root=Create(kind,int.MaxValue);var bars=ToBars([3,4,0]);Assert.True(ValuesBarExecution.SupportsOwned([root]));
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([3,Math.Sqrt(12.5),2.8867513459481287],run[root.Outputs[0]].ToArray());
    }
    [Fact]
    public void OutputsRemainScalar() {Assert.Single(new PpoMa().Outputs);Assert.Single(new QmaSmaDifference().Outputs);Assert.Single(new QuadraticMovingAverage().Outputs);}
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void BothKernelsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType(kind==0 ? "GeneratedPpoAverageState" : "GeneratedQuadraticMeanState", System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, kind==0 ? new object[]{3,5,bars.Length} : new object[]{3,bars.Length,kind==1}, null)!;
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
