using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedWalkRainbowTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(RandomWalkIndex) || c.IndicatorType == typeof(RSINGIndicator) || c.IndicatorType == typeof(SellGravitationIndex) || c.IndicatorType == typeof(RainbowOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {0=>new RandomWalkIndex(length),1=>new RSINGIndicator(length),2=>new SellGravitationIndex(length),_=>new RainbowOscillator(length)};
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { kind, length };

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

    [Theory] [InlineData(0,0)] [InlineData(1,0)] [InlineData(1,1)] [InlineData(2,0)] [InlineData(2,1)] [InlineData(3,0)]
    public async Task EveryCustomAverageSlotKeepsFallback(int kind,int slot)
    {
        IMovingAverage? first=slot>=0 ? new Ema(2) : null,second=slot>=1 ? new Sma(2) : null;
        IIndicator root=kind switch {0=>new RandomWalkIndex(3,first),1=>new RSINGIndicator(3,first,second),2=>new SellGravitationIndex(3,first,second),_=>new RainbowOscillator(3,first)};
        Assert.False(ValuesBarExecution.SupportsOwned([root]));await Compare([root],ToBars([1,3,2,0,4,8]));
    }
    [Fact]
    public async Task RandomWalkUsesPriorHighLowAndTrueRange()
    {
        var root=new RandomWalkIndex(1);Bar[] bars=[new(default,2,3,1,2,1),new(default,4,5,2,4,1),new(default,1,4,0,1,1)];await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits([1.5,4d/3,.5],run[root.Value].ToArray());Bits([-.5,1d/3,1.25],run[root.RwiLow].ToArray());
    }
    [Fact]
    public async Task RsingUsesPriceLagRangeVarianceAndOriginalVolume()
    {
        var root=new RSINGIndicator(2);Bar[] bars=[new(default,0,1,0,1,1),new(default,0,2,0,2,1),new(default,0,4,0,3,1)];await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits([0,0,8],run[root.Rsing].ToArray());Bits([0,0,16d/3],run[root.Signal].ToArray());
    }
    [Fact]
    public async Task RsingZeroVarianceProducesZero()
    {
        var root=new RSINGIndicator(1);var bars=ToBars([1,3,2,0,4]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits(new double[5],run[root.Rsing].ToArray());Bits(new double[5],run[root.Signal].ToArray());
    }
    [Fact]
    public async Task SellUsesSignedBodyRatioAndZeroRange()
    {
        var root=new SellGravitationIndex(1);Bar[] bars=[new(default,1,3,1,2,1),new(default,3,5,1,2,1),new(default,2,2,2,2,1)];await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits([.5,-.25,0],run[root.Sgi].ToArray());Bits([.5,-.25,0],run[root.Signal].ToArray());
    }
    [Fact]
    public async Task RainbowUnitStagesPreserveNegativeZeroLowerBand()
    {
        var root=new RainbowOscillator(1);var bars=ToBars([1,3,2,0]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits(new double[4],run[root.Ro].ToArray());Bits(new double[4],run[root.UpperBand].ToArray());Bits([-0d,-0d,-0d,-0d],run[root.LowerBand].ToArray());
    }
    [Fact]
    public void OutputOrderRemainsStable()
    {
        var walk=new RandomWalkIndex();var rsing=new RSINGIndicator();var sell=new SellGravitationIndex();var rainbow=new RainbowOscillator();
        Assert.Equal(new[]{walk.Value,walk.RwiLow},walk.Outputs);Assert.Equal(new[]{rsing.Rsing,rsing.Signal},rsing.Outputs);Assert.Equal(new[]{sell.Sgi,sell.Signal},sell.Outputs);Assert.Equal(new[]{rainbow.Ro,rainbow.UpperBand,rainbow.LowerBand},rainbow.Outputs);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3) switch { IndicatorBase scalar => (IIndicator)scalar.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() }; Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void BothKernelsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType(kind switch {0=>"GeneratedRandomWalkState",1=>"GeneratedRsingState",2=>"GeneratedSellGravitationState",_=>"GeneratedRainbowState"}, System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object[] { 3 }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { if (state is IIndicatorState scalar) values.Add(scalar.Update(in bar)); else { var output = new double[kind == 3 ? 3 : 2]; ((IMultiOutputState)state).Update(in bar, output); values.AddRange(output); } } return values.ToArray(); }
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
