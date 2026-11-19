using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedPriceOscillatorsTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(PremierStochasticOscillator) || c.IndicatorType == typeof(PrettyGoodOscillator) || c.IndicatorType == typeof(PriceZoneOscillator) || c.IndicatorType == typeof(PriceVolumeRank) || c.IndicatorType == typeof(PsychologicalLine)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch { 0 => new PremierStochasticOscillator(length), 1 => new PrettyGoodOscillator(length), 2 => new PriceZoneOscillator(length), 3 => new PriceVolumeRank(length), _ => new PsychologicalLine(length) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3, 4 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { kind, length };

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

    public static IEnumerable<object[]> SmoothCases => from kind in new[]{0,3} from length in new[]{int.MinValue,1,2,3,65,int.MaxValue} select new object[]{kind,length};
    [Theory,MemberData(nameof(SmoothCases))]
    public async Task IndependentSmoothingPeriods(int kind,int length)
    {await Compare([kind==0 ? new PremierStochasticOscillator(3,length) : new PriceVolumeRank(3,length)],ToBars([1,3,2,0,4,8]));}
    [Theory] [InlineData(0,0)] [InlineData(1,0)] [InlineData(1,1)] [InlineData(2,0)] [InlineData(2,1)] [InlineData(3,0)] [InlineData(3,1)]
    public async Task EveryCustomSlotKeepsFallback(int kind,int slot)
    {
        IMovingAverage first = new Sma(2);
        IMovingAverage? second = slot == 1 ? new Sma(3) : null;
        IIndicator root=kind switch {0=>new PremierStochasticOscillator(3,4,first),1=>new PrettyGoodOscillator(3,first,second),2=>new PriceZoneOscillator(3,first,second),_=>new PriceVolumeRank(3,4,first,second)};
        Assert.False(ValuesBarExecution.SupportsOwned([root]));await Compare([root],ToBars([1,3,2,0,4,8]));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(-1)]
    public async Task PremierMidpointEdgesAndFlatRangeKeepHandValues(int position)
    {
        var root=new PremierStochasticOscillator(3,4);double close=position==0 ? 1 : position==1 ? 2 : 0;
        Bar[] bars=Enumerable.Repeat(new Bar(default,close,position==-1 ? 0 : 2,0,close,1),8).ToArray();await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits(Enumerable.Repeat(position==0 ? 0 : Math.Tanh(position*2.5),bars.Length).ToArray(),run[root.Value].ToArray());
    }
    [Fact]
    public async Task PrettyGoodUsesOriginalTrueRangeAndFullWindowSma()
    {
        var root=new PrettyGoodOscillator(2);Bar[] bars=[new(default,1,2,0,1,1),new(default,3,4,2,3,1),new(default,2,3,1,2,1)];await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits([0,.4,-.2],run[root.Value].ToArray());
    }
    [Fact]
    public async Task PriceZoneUsesDirectionAndZeroDenominator()
    {
        var root=new PriceZoneOscillator(1);var bars=ToBars([1,3,2,2,0,-2,-1]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits([0,100,-100,0,0,-100,100],run[root.Value].ToArray());
    }
    [Fact]
    public async Task RankKeepsOriginalVolumeEqualityAndSlowBeforeFast()
    {
        var root=new PriceVolumeRank(2,3);double[] prices=[1,2,3,3,2],volumes=[1,2,1,1,2];var bars=prices.Select((p,i)=>new Bar(default,p,p,p,p,volumes[i])).ToArray();await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([1,1,2,3,4],run[root.Value].ToArray());Bits([0,0,4d/3,2,3],run[root.SlowSignal].ToArray());Bits([0,1,1.5,2.5,3.5],run[root.FastSignal].ToArray());
        Assert.Equal(new[]{root.Value,root.SlowSignal,root.FastSignal},root.Outputs);
    }
    [Fact]
    public async Task PsychologicalLineUsesFullPeriodDenominatorAndNoFirstBarVote()
    {
        var root=new PsychologicalLine(3);var bars=ToBars([1,3,2,2,4,5]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();Bits([0,100d/3,100d/3,100d/3,100d/3,200d/3],run[root.Value].ToArray());
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3) switch { IndicatorBase scalar => (IIndicator)scalar.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() }; Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void BothKernelsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType(kind switch {0=>"GeneratedPremierState",1=>"GeneratedPrettyGoodState",2=>"GeneratedPriceZoneState",3=>"GeneratedPriceVolumeRankState",_=>"GeneratedPsychologicalState"}, System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, kind switch {0=>new object[]{3,4},1=>new object[]{3,bars.Length},2=>new object[]{3},3=>new object[]{3,4,bars.Length},_=>new object[]{3,bars.Length}}, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { if (state is IIndicatorState scalar) values.Add(scalar.Update(in bar)); else { var output = new double[3]; ((IMultiOutputState)state).Update(in bar, output); values.AddRange(output); } } return values.ToArray(); }
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
