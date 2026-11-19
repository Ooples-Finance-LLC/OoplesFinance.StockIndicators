using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedTfsPairTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(TFSMboIndicator) || c.IndicatorType == typeof(TFSMboPercentagePriceOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new TFSMboIndicator(length,3,2) : new TFSMboPercentagePriceOscillator(length);
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

    public static IEnumerable<object[]> CustomCases => from kind in new[]{0,1} from slot in new[]{0,1,2} select new object[]{kind,slot};
    [Theory,MemberData(nameof(CustomCases))]
    public async Task AllCustomAverageSlotsKeepFallback(int kind,int slot)
    {
        IMovingAverage? first=slot>=0?new Ema(2):null,second=slot>=1?new Ema(2):null,third=slot>=2?new Ema(2):null;
        IIndicator root=kind==0?new TFSMboIndicator(2,3,2,first,second,third):new TFSMboPercentagePriceOscillator(3,first,second,third);
        Assert.False(ValuesBarExecution.SupportsOwned([root]));await Compare([root],ToBars([1,3,2,0,4,1]));
    }
    [Fact]
    public async Task DifferenceSignalAndHistogramRetainHandValues()
    {
        var root=new TFSMboIndicator(1,2,2);var bars=ToBars([1,3,2,4]);await Compare([root],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([1,1,-.5,1],run[root.Value].ToArray());Bits([0,1,.25,.25],run[root.Signal].ToArray());Bits([1,0,-.75,.75],run[root.Histogram].ToArray());
    }
    [Fact]
    public async Task MaximumPeriodsRetainReadinessWithObservedStorage()
    {
        var root=new TFSMboIndicator(int.MaxValue,int.MaxValue,int.MaxValue);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1,3]))).ConfigureIndicators(root).BuildAsync();
        foreach(var output in root.Outputs)Bits([0,0],run[output].ToArray());
    }
    [Theory] [InlineData(1)] [InlineData(3)] [InlineData(65)]
    public async Task SlowAndSignalPeriodsAreIndependent(int length)
    {await Compare([new TFSMboIndicator(2,length,3),new TFSMboIndicator(2,3,length)],ToBars([1,3,2,0,4,1]));}
    [Fact]
    public async Task NonfiniteDifferencePoisonsLaterSignal()
    {
        var root=new TFSMboIndicator(1,3,2);var bars=ToBars([-double.MaxValue,-double.MaxValue,double.MaxValue,0]);await Compare([root],bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
        Assert.Equal(typeof(TFSMboIndicator), error.IndicatorType);
        Assert.Equal(0, error.OutputSlot); Assert.Equal(2, error.BarIndex);
        Assert.True(double.IsPositiveInfinity(error.Value)); Assert.Null(builder.LastExecution);
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedTfsPairState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object[] { 1, 3, 2, bars.Length, false }, null)!;
        var values = new double[3];
        try
        {
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], values);
                if (i == 2) Assert.True(double.IsPositiveInfinity(values[0]));
                if (i >= 2)
                {
                    Assert.True(double.IsNaN(values[1]));
                    Assert.True(double.IsNaN(values[2]));
                }
            }
        }
        finally { ((IDisposable)state).Dispose(); }
    }
    [Fact]
    public async Task PercentageFacadeLengthKeepsFixedPeriods()
    {
        var first=new TFSMboPercentagePriceOscillator(1);var second=new TFSMboPercentagePriceOscillator(int.MaxValue);
        var bars=ToBars(Enumerable.Range(1,220).Select(i=>(double)i).ToArray());await Compare([first,second],bars);
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(first,second).BuildAsync();
        for(var slot=0;slot<3;slot++)Bits(run[first.Outputs[slot]].ToArray(),run[second.Outputs[slot]].ToArray());
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void OutputOrderAndPrimaryStayStable(int kind)
    {
        var root=(MultiOutputIndicatorBase)Create(kind,3);Assert.Equal(3,root.Outputs.Count);Assert.Same(root.Outputs[0],root.PrimaryOutput);
        if(root is TFSMboIndicator m){Assert.Same(m.Value,root.Outputs[0]);Assert.Same(m.Signal,root.Outputs[1]);Assert.Same(m.Histogram,root.Outputs[2]);}
        else {var p=(TFSMboPercentagePriceOscillator)root;Assert.Same(p.Value,root.Outputs[0]);Assert.Same(p.Signal,root.Outputs[1]);Assert.Same(p.Histogram,root.Outputs[2]);}
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
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedTfsPairState", System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object[] { 3, 5, 2, bars.Length, kind == 1 }, null)!;
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
