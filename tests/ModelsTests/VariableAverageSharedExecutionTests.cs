using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class VariableAverageSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i, 110 + i % 17, 90 - i % 13, 100 + i % 7, i % 13)).ToArray();
    private static double Select(Bar bar) => (bar.Open % 7) switch { 0 => -1e100, 1 => 1.99, 2 => 3.99, 3 => 29.9, 4 => 1e100, _ => 7.25 };
    [Theory]
    [InlineData(1, 1)] [InlineData(1, 2)] [InlineData(2, 30)]
    [InlineData(3, 129)] [InlineData(2, 4097)] [InlineData(1, int.MaxValue)]
    public async Task ChangingPeriodsStartupAndWrapMatchOriginal(int minimum, int maximum)
    {
        foreach (int count in new[] { 0, 1, 29, 30, 31, 513, 10000 })
            await Check(Data(count), new VariablePeriodClassicAverage(Select, minimum, maximum), count <= 31);
        await Check(Data(513), new VariablePeriodClassicAverage(Select, minimum, maximum, firstPriceSeed: true, suppression: 7), false);
    }

    [Fact]
    public async Task ExactPrefixesPreserveCancellationAcrossExtremeExponentSpans()
    {
        double[] prices = [double.MaxValue, double.Epsilon, -double.MaxValue, -double.Epsilon, -0d, 0d, Math.ScaleB(1d, 900), -Math.ScaleB(1d, 900)];
        var bars = Data(1001).Select((b, i) => new Bar(b.Time, i, b.High, b.Low, prices[i % prices.Length], b.Volume)).ToArray();
        await Check(bars, new VariablePeriodClassicAverage(Select, 1, 30), false);
        await Check(bars.Take(65).ToArray(), new VariablePeriodClassicAverage(Select, 1, 7));
        var random = new Random(81372);
        await Check(Data(129).Select(b => new Bar(b.Time, b.Open, b.High, b.Low, Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024)), b.Volume)).ToArray(), new VariablePeriodClassicAverage(Select, 1, 17));
    }

    [Fact]
    public void InvalidSelectionDoesNotAdvancePrefixesAndResetReusesStorage()
    {
        bool invalid = false;
        var owner = new VariablePeriodClassicAverage(b => invalid ? double.NaN : Select(b), 1, 3);
        var original = (IMultiOutputState)owner.CreateState();
        var compact = new ValuesBarExecution.VariableSmaState(owner, 65);
        double[] expected = new double[2], actual = new double[2];
        try
        {
            for (int replay = 0; replay < 2; replay++)
            {
                original.Reset(); compact.Reset();
                var bars = Data(65);
                for (int i = 0; i < bars.Length; i++)
                {
                    if (i >= 2 && i % 7 == 2)
                    {
                        invalid = true;
                        var before = actual.ToArray();
                        var e = Assert.Throws<ArgumentOutOfRangeException>(() => original.Update(bars[i], expected));
                        var a = Assert.Throws<ArgumentOutOfRangeException>(() => compact.Update(bars[i], actual));
                        Assert.Equal(e.Message, a.Message); Assert.Equal(before, actual);
                        invalid = false;
                    }
                    original.Update(bars[i], expected); compact.Update(bars[i], actual);
                    Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
        finally { ((IDisposable)original).Dispose(); }
    }

    [Fact]
    public async Task SelectorRunsOnceAfterStartupAndLaterRawInputWins()
    {
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var seen = new List<Bar>(); var bars = Data(65);
            var owner = new VariablePeriodClassicAverage(b => { seen.Add(b); return Select(b); }, 2, 30);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
            Assert.Equal(bars.Skip(29), seen);
            var invalid = new VariablePeriodClassicAverage(_ => double.NaN, 2, 30);
            bars[^1] = new(default, 0, 0, 0, 0, double.PositiveInfinity);
            // Raw input error must name the source field rather than PeriodSelector.
            var failure = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(invalid).BuildAsync());
            Assert.NotEqual("PeriodSelector", failure.ParamName);
            var never = new VariablePeriodClassicAverage(_ => throw new InvalidOperationException("startup selector"), 1, int.MaxValue);
            using var immature = await new StockIndicatorBuilder().ConfigureSource(Bars.From(Data(65))).ConfigureIndicators(never).BuildAsync();
            Assert.All(immature[never.IsDefined].ToArray(), x => Assert.Equal(0, x));
        }
    }

    [Fact]
    public void OrdinaryVariableWindowsReduceStateAllocations()
    {
        var owner = new VariablePeriodClassicAverage(Select, 2, 30);
        var original = (IMultiOutputState)owner.CreateState();
        var compact = new ValuesBarExecution.VariableSmaState(owner, 1000);
        var bars = Data(1000); var output = new double[2];
        long Measure(IMultiOutputState state)
        {
            state.Reset(); long before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var bar in bars) state.Update(bar, output);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        try
        {
            Measure(original); Measure(compact);
            long oldBytes = Measure(original), newBytes = Measure(compact);
            log.WriteLine($"1000 updates after reset, excluding construction: original {oldBytes} bytes, compact {newBytes} bytes.");
            Assert.True(newBytes < oldBytes / 4, $"Original {oldBytes}, compact {newBytes}");
        }
        finally { ((IDisposable)original).Dispose(); }
    }

    [Fact]
    public async Task MixedChainedRootsPreserveCancellationAndBackendContracts()
    {
        var bars = Data(513); MultiOutputIndicatorBase[] roots = [new VariablePeriodClassicAverage(Select), new VariablePeriodClassicAverage(Select, 1, 7), new PriceRelativeStrength()];
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            foreach (var owner in roots)
            {
                using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
                Columns(run, owner, owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray());
            }
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        var chained = new VariablePeriodClassicAverage(b => 1 + Math.Abs(b.Close % 11)); chained.Of(new FirstValueEma(5)); await Check(bars, chained, false);
    }

    private static async Task Check(Bar[] bars, IIndicator owner, bool reference = true)
    {
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
        var expected = owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray();
        if (reference)
            foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
                rule.Check(new IndicatorValidationContext("Variable SMA shared execution", bars, expected, 0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose(); Columns(run, owner, expected);
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
    private static void Columns(IIndicatorRun run, IIndicator owner, double[][] expected)
    {
        for (int slot = 0; slot < expected.Length; slot++)
        {
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits), run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            if (expected[slot].Length > 0)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][^1]), BitConverter.DoubleToInt64Bits(run.Latest[owner.Outputs[slot]]));
        }
    }
}
