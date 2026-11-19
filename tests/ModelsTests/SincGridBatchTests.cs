using System.Reflection;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SincGridBatchTests
{
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static Bar[] Fixture(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(default, 1000 + i, 2000 + i, -2000 - i, (i * 13 % 101 - 50) / 8d, i + 1)).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(20)] [InlineData(21)] [InlineData(22)]
    [InlineData(148)] [InlineData(149)] [InlineData(150)]
    [InlineData(276)] [InlineData(277)] [InlineData(278)]
    [InlineData(513)] [InlineData(1000)] [InlineData(1192)] [InlineData(8193)] [InlineData(10000)]
    public async Task OriginalStartupBlockEndsWorkerHalosAndHistoryMatchIndependentRationals(int count)
    {
        await Check(Fixture(count), new WindowedSinc(20, 21));
    }

    [Theory]
    [InlineData(21, 125, false)] [InlineData(21, 145, false)]
    [InlineData(21, 148, false)] [InlineData(21, 149, true)]
    [InlineData(64, 191, false)] [InlineData(64, 192, true)]
    [InlineData(65, 500, false)]
    public void FundedCountGuardSelectsBeforeAllocatingWrapperStorage(int taps, int count, bool selected)
    {
        var state = CreateKernelState(new WindowedSinc(100, taps), count, new double[count], 0, 0);
        Assert.Equal(selected ? "SincGridBatchState" : "CompactState", state.GetType().Name);
    }

    [Theory]
    [InlineData(3)] [InlineData(20)]
    public void LagGetterPreservesOriginalCoefficientValuesAndExplicitlyReversesTheirOrder(int period)
    {
        var indicator = new WindowedSinc(period, 21);
        var original = WindowedSincSnapshot.CreateWeights(period, 21, SincWindow.Hann, out _);
        for (int lag = 0; lag < original.Length; lag++)
            // ProductFactor already canonicalizes signed zero in the original state.
            Assert.Equal(BitConverter.DoubleToInt64Bits(original[original.Length - 1 - lag] is 0 ? 0d : original[original.Length - 1 - lag]),
                BitConverter.DoubleToInt64Bits(indicator.BatchCoefficient(lag)));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(41, 61)]
    public void ShiftedOriginCountAndWriteStartPublishEachOwnedRowAndResetExactly(int origin, int writeStart)
    {
        const int count = 405;
        var bars = Fixture(origin + count + 17);
        var output = Enumerable.Repeat(double.NaN, bars.Length).ToArray();
        var indicator = new WindowedSinc(20, 21);
        var state = CreateKernelState(indicator, count, output, origin, writeStart);
        Assert.Equal("SincGridBatchState", state.GetType().Name);
        var expected = Reference(bars, indicator);
        for (int pass = 0; pass < 2; pass++)
        {
            state.Reset();
            Array.Fill(output, double.NaN);
            for (int row = origin; row < origin + count; row++)
            {
                double value = state.Update(in bars[row]);
                if (row >= writeStart) output[row] = value;
            }
            Assert.All(output.Take(writeStart), value => Assert.True(double.IsNaN(value)));
            Assert.All(output.Skip(origin + count), value => Assert.True(double.IsNaN(value)));
            AssertBits(expected.Skip(writeStart).Take(origin + count - writeStart),
                output.Skip(writeStart).Take(origin + count - writeStart));
        }
    }

    [Theory]
    [InlineData("negative")] [InlineData("wide")] [InlineData("zero")]
    public void RejectedCertificateLatchesOriginalAtSavedRowBeforeAnySeedUpdate(string rejection)
    {
        var indicator = new WindowedSinc(20, 21);
        var coefficients = (ExactMeanAccumulator.ProductFactor[])typeof(WindowedSinc)
            .GetField("_compactCoefficients", Instance)!.GetValue(indicator)!;
        switch (rejection)
        {
            case "negative": coefficients[0] = new(-1); break;
            case "wide": coefficients[0] = new(double.Epsilon); coefficients[1] = new(1); break;
            case "zero": Array.Clear(coefficients); break;
        }
        var original = new CountingState();
        var bars = Fixture(200);
        var state = CreateWrapper(original, indicator, bars.Length, new double[bars.Length]);
        for (int row = 0; row < bars.Length; row++)
        {
            Assert.Equal(bars[row].Close, state.Update(in bars[row]));
            Assert.Equal(row + 1, original.Updates);
        }
        Assert.Null(state.GetType().GetField("_batch", Instance)!.GetValue(state));
        Assert.True((bool)state.GetType().GetField("_fallback", Instance)!.GetValue(state)!);
        state.Reset();
        Assert.Equal(0, original.Updates);
        Assert.Equal(bars[0].Close, state.Update(in bars[0]));
        Assert.Equal(1, original.Updates);
    }

    [Fact]
    public void CertifiedSeedHandoffNeverAppendsCapturedRowsToOriginalState()
    {
        var indicator = new WindowedSinc(20, 21);
        var original = new CountingState();
        var bars = Fixture(405);
        var state = CreateWrapper(original, indicator, bars.Length, new double[bars.Length]);
        for (int pass = 0; pass < 2; pass++)
        {
            state.Reset();
            for (int row = 0; row < bars.Length; row++)
            {
                state.Update(in bars[row]);
                Assert.Equal(Math.Min(row + 1, indicator.Taps), original.Updates);
            }
        }
    }

    [Fact]
    public async Task UnsafePriceBlocksReplayOriginalWithFullMassAndCapturedCarry()
    {
        var bars = Fixture(3001);
        foreach (var (row, close) in new[]
        {
            (20, Math.BitIncrement(.1)), (147, double.Epsilon), (148, -double.Epsilon),
            (277, double.MaxValue), (510, -double.MaxValue), (999, Math.PI), (1999, -Math.PI)
        })
        {
            var bar = bars[row];
            bars[row] = new(bar.Time, bar.Open, bar.High, bar.Low, close, bar.Volume);
        }
        await Check(bars, new WindowedSinc(20, 21));
    }

    [Theory]
    [InlineData(20, 1, SincWindow.Rectangular)]
    [InlineData(20, 3, SincWindow.Hann)]
    [InlineData(3, 21, SincWindow.Hann)]
    [InlineData(3, 8, SincWindow.Rectangular)]
    [InlineData(100, 64, SincWindow.Hann)]
    [InlineData(100, 65, SincWindow.Hann)]
    public async Task KernelBoundaryAndFallbackCasesMatchIndependentRationals(int period, int taps, SincWindow window)
    {
        await Check(Fixture(1601), new WindowedSinc(period, taps, window));
    }

    [Fact]
    public async Task PreCancellationAndAllFieldValidationKeepOriginalFailureAndRecovery()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 1;
            var bars = Fixture(601);
            var indicator = new WindowedSinc(20, 21);
            var builder = Build(bars, indicator, IndicatorHistoryMode.Full);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            foreach (int row in new[] { 20, 21, 147, 148, 149, 500 })
            {
                var valid = bars[row];
                bars[row] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
                var ordinary = await Record.ExceptionAsync(() => Build(bars, indicator, IndicatorHistoryMode.Full)
                    .ConfigureBehavior(_ => { }).BuildAsync());
                var shared = await Record.ExceptionAsync(() => builder.BuildAsync());
                Assert.NotNull(ordinary); Assert.NotNull(shared);
                Assert.Equal(ordinary.GetType(), shared.GetType());
                Assert.Equal(ordinary.Message, shared.Message);
                bars[row] = valid;
            }
            using var recovered = await builder.BuildAsync();
            AssertBits(Reference(bars, indicator), recovered[indicator].ToArray());
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static IIndicatorState CreateKernelState(WindowedSinc indicator, int count, double[] output, int origin, int writeStart)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("StateWindowKernel", BindingFlags.NonPublic)!;
        var kernel = type.GetMethod("Create", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [indicator.Taps, count, indicator, Target(output, origin, writeStart)])!;
        return (IIndicatorState)type.GetFields(Instance).Single(f => f.FieldType == typeof(IIndicatorState)).GetValue(kernel)!;
    }

    private static IIndicatorState CreateWrapper(IIndicatorState original, WindowedSinc indicator, int count, double[] output)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("SincGridBatchState", BindingFlags.NonPublic)!;
        return (IIndicatorState)Activator.CreateInstance(type, Instance, null,
            [original, indicator, count, Target(output, 0, 0)], null)!;
    }

    private static object Target(double[] output, int origin, int writeStart)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("BoundedOutput", BindingFlags.NonPublic)!;
        return Activator.CreateInstance(type, Instance, null, [new[] { output }, origin, writeStart], null)!;
    }

    private sealed class CountingState : IIndicatorState
    {
        internal int Updates;
        public void Reset() => Updates = 0;
        public double Update(in Bar bar) { Updates++; return bar.Close; }
    }

    private static double[] Reference(Bar[] bars, WindowedSinc indicator)
    {
        // Original binary64 coefficients define the kernel. BigInteger rational
        // products and a separate nearest-even publisher establish every bit.
        var weights = WindowedSincSnapshot.CreateWeights(indicator.Period, indicator.Taps, indicator.Window, out _)
            .Select(ReferenceFraction.FromDouble).ToArray();
        var mass = weights.Aggregate(new ReferenceFraction(0), (sum, weight) => sum + weight);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var result = new double[bars.Length];
        for (int row = 0; row < result.Length; row++)
        {
            var sum = new ReferenceFraction(0);
            int oldest = Math.Max(0, row - indicator.Taps + 1);
            for (int tap = 0; tap < weights.Length; tap++)
                sum += prices[Math.Min(row, oldest + tap)] * weights[tap];
            result[row] = (sum / mass).ToDouble();
        }
        return result;
    }

    private static void AssertBits(IEnumerable<double> expected, IEnumerable<double> actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));

    private static StockIndicatorBuilder Build(Bar[] bars, WindowedSinc indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);

    private static async Task Check(Bar[] bars, WindowedSinc indicator)
    {
        var expected = Reference(bars, indicator);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var run = await Build(input, indicator, history).BuildAsync();
                Array.Clear(input);
                AssertBits(expected, run[indicator].ToArray());
                if (bars.Length != 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (history != IndicatorHistoryMode.Full) continue;
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
