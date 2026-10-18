using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedArithmeticExecutionTests
{
    public static IEnumerable<object[]> Configurations =>
        from operation in Enum.GetValues<CandleArithmeticOperation>()
        from left in Enum.GetValues<CandlePriceField>()
        from right in Enum.GetValues<CandlePriceField>()
        select new object[] { operation, left, right };

    [Theory, MemberData(nameof(Configurations))]
    public async Task EveryOperationAndFieldPairPreservesBitsAndOwnedHistory(
        CandleArithmeticOperation operation, CandlePriceField left, CandlePriceField right)
    {
        double[] values = [-0d, 0d, .25, -.25, 1, -1, 1024, -1024];
        foreach (int count in new[] { 0, 1, 8193 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                values[i % 8], values[(i + 1) % 8], values[(i + 3) % 8], values[(i + 5) % 8], 1)).ToArray();
            var original = bars.ToArray();
            var indicator = new CandleArithmetic(operation, left, right);
            var expected = Evaluate(indicator, bars);
            using var full = await Builder(bars, IndicatorHistoryMode.Full, indicator).BuildAsync();
            using var latest = await Builder(bars, IndicatorHistoryMode.LatestOnly, indicator).BuildAsync();
            Assert.False(((IndicatorRun)full).HasLegacyRuntime);
            Array.Clear(bars);
            Check(expected, full, indicator);
            Check(expected, latest, indicator);
            int index = 0;
            await foreach (var snapshot in full) Assert.Equal(original[index++], snapshot.Bar);
            Assert.Equal(count, index);
            if (count > 0) Assert.Equal(original[^1], latest.Latest.Bar);
        }
    }

    [Theory]
    [InlineData(CandleArithmeticOperation.Add)]
    [InlineData(CandleArithmeticOperation.Subtract)]
    [InlineData(CandleArithmeticOperation.Multiply)]
    [InlineData(CandleArithmeticOperation.Divide)]
    public async Task OverflowInputPrecedenceAndRecoveryMatchOrdinaryBuilder(CandleArithmeticOperation operation)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 8193).ToArray();
            var right = operation switch
            {
                CandleArithmeticOperation.Subtract => -double.MaxValue,
                CandleArithmeticOperation.Divide => double.Epsilon,
                _ => double.MaxValue
            };
            bars[1] = new Bar(default, right, 1, 0, double.MaxValue, 1);
            bars[^1] = new Bar(default, 1, 2, 0, .5, double.NaN);
            var indicator = new CandleArithmetic(operation);
            var actual = Builder(bars, history, indicator);
            var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, b => b))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            for (int step = 0; step < 2; step++)
            {
                var expectedError = await Record.ExceptionAsync(() => ordinary.BuildAsync());
                var actualError = await Record.ExceptionAsync(() => actual.BuildAsync());
                Assert.NotNull(expectedError);
                Assert.NotNull(actualError);
                Assert.Equal(expectedError.GetType(), actualError.GetType());
                Assert.Equal(expectedError.Message, actualError.Message);
                Assert.Null(actual.LastExecution);
                bars[^1] = bars[0];
            }
            bars[1] = bars[0];
            using var recovered = await actual.BuildAsync();
            Check(Evaluate(indicator, bars), recovered, indicator);
        }
    }

    [Fact]
    public async Task MixedAndDuplicateRootsKeepTheirStateAndOverflowChecks()
    {
        var bars = Enumerable.Repeat(new Bar(default, 2, 3, 0, .5, 1), 40).ToArray();
        var add = new CandleArithmetic(CandleArithmeticOperation.Add);
        var divide = new CandleArithmetic(CandleArithmeticOperation.Divide);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var mixed = await Builder(bars, history, add, divide).BuildAsync();
            Check(Evaluate(add, bars), mixed, add);
            Check(Evaluate(divide, bars), mixed, divide);
            using var duplicate = await Builder(bars, history, add, add).BuildAsync();
            Check(Evaluate(add, bars), duplicate, add);
            bars[0] = new Bar(default, double.MaxValue, 1, 0, double.MaxValue, 1);
            var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, b => b))
                .ConfigureIndicators(add, divide).ConfigureHistory(history);
            var expectedError = await Record.ExceptionAsync(() => ordinary.BuildAsync());
            var actualError = await Record.ExceptionAsync(() => Builder(bars, history, add, divide).BuildAsync());
            Assert.NotNull(expectedError);
            Assert.NotNull(actualError);
            Assert.Equal(expectedError.Message, actualError.Message);
            bars[0] = bars[1];
        }
    }

    [Fact]
    public async Task PresenceReuseCannotChangeRetainedResultsAndExceptionalFiniteValuesKeepBits()
    {
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 8193).ToArray();
        var indicator = new CandleArithmetic(CandleArithmeticOperation.Divide);
        using var first = await Builder(bars, IndicatorHistoryMode.LatestOnly, indicator).BuildAsync();
        int[] missing = [0, 63, 64, 4095, 4096, 8192];
        foreach (int index in missing) bars[index] = new Bar(default, index % 2 == 0 ? -0d : 0d, 1, 0, 1, 1);
        using var mixed = await Builder(bars, IndicatorHistoryMode.LatestOnly, indicator).BuildAsync();
        Check(Evaluate(indicator, bars), mixed, indicator);
        Assert.All(first[indicator.IsDefined].ToArray(), x => Assert.Equal(1d, x));
        foreach (double value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d })
        {
            bars[0] = new Bar(default, 1, 2, 0, value, 1);
            using var extreme = await Builder(bars, IndicatorHistoryMode.LatestOnly, indicator).BuildAsync();
            Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(extreme[indicator][0]));
        }
        Check(Evaluate(indicator, bars.Skip(1).ToArray()), mixed, indicator, 1);
    }

    [Fact]
    public async Task CancellationGpuAndComposedGraphsKeepExistingContracts()
    {
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 40).ToArray();
        var indicator = new CandleArithmetic(CandleArithmeticOperation.Add);
        var builder = Builder(bars, IndicatorHistoryMode.Full, indicator);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
        indicator.Of(new Sma(3));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.Null(builder.LastExecution);
        Assert.False(ValuesBarExecution.IsPointwise(indicator));
        using var actual = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, b => b))
            .ConfigureIndicators(indicator).BuildAsync();
        for (int slot = 0; slot < 2; slot++) Bits(expected[indicator.Outputs[slot]].ToArray(), actual[indicator.Outputs[slot]].ToArray());
    }

    private static double[][] Evaluate(CandleArithmetic indicator, Bar[] bars)
    {
        var state = (IMultiOutputState)indicator.CreateState();
        var result = new[] { new double[bars.Length], new double[bars.Length] };
        var scratch = new double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(in bars[i], scratch);
            for (int slot = 0; slot < 2; slot++) result[slot][i] = scratch[slot];
        }
        return result;
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IndicatorHistoryMode history, params IIndicator[] indicators) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureHistory(history)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);

    private static void Check(double[][] expected, IIndicatorRun actual, CandleArithmetic indicator, int offset = 0)
    {
        for (int slot = 0; slot < 2; slot++) Bits(expected[slot], actual[indicator.Outputs[slot]].Slice(offset).ToArray());
    }
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
