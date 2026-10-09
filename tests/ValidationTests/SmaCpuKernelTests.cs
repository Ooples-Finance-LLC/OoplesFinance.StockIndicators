using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SmaCpuKernelTests
{
    private struct Deviation : SmaCpuKernel.IConsumer
    {
        internal int Calls;
        public double Consume(double input, double mean, int index)
        {
            Calls++;
            return input - mean;
        }
    }

    private readonly struct DoubleReader : SmaCpuKernel.IReader<double>
    {
        public double Read(in double value) => value;
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 7)]
    [InlineData(20, 1)]
    [InlineData(20, 31)]
    [InlineData(int.MaxValue, 64)]
    public void FusedConsumerMatchesIndependentWindowsAcrossBlocks(int period, int blockSize)
    {
        var input = Enumerable.Range(0, 129).Select(i => (i % 17 - 8) / 4d).ToArray();
        input[0] = -0d;
        var owned = new double[input.Length];
        var output = new double[input.Length];
        var state = new SmaCpuKernel.State(period, input.Length);
        var reader = new DoubleReader();
        var consumer = new Deviation();
        for (var offset = 0; offset < input.Length; offset += blockSize)
        {
            var size = Math.Min(blockSize, input.Length - offset);
            SmaCpuKernel.Process<double, DoubleReader, Deviation>(input.AsSpan(offset, size),
                owned.AsSpan(offset, size), output.AsSpan(offset, size), ref state, ref reader, ref consumer);
        }
        Assert.True(state.Certified);
        Assert.Equal(input.Length, consumer.Calls);
        Assert.Equal(input, owned);
        for (var i = 0; i < input.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            if (i >= period - 1)
                for (var j = i - period + 1; j <= i; j++) sum += ReferenceFraction.FromDouble(input[j]);
            var mean = period == 1 ? input[i] : (sum / new ReferenceFraction(period)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(input[i] - mean), BitConverter.DoubleToInt64Bits(output[i]));
        }
        if (period > 1)
        {
            var contiguous = new double[input.Length];
            consumer = new Deviation();
            Assert.True(SmaCpuKernel.TryProcess(input, contiguous, period, ref consumer));
            Assert.Equal(output, contiguous);
        }
    }

    [Fact]
    public void RejectedBatchCannotInvokeConsumerOrModifyOutput()
    {
        var input = Enumerable.Repeat(1d, 128).Append(double.Epsilon).ToArray();
        var output = Enumerable.Repeat(123d, input.Length).ToArray();
        var consumer = new Deviation();
        Assert.False(SmaCpuKernel.TryProcess(input, output, 20, ref consumer));
        Assert.Equal(0, consumer.Calls);
        Assert.All(output, value => Assert.Equal(123d, value));
        Assert.False(SmaCpuKernel.TryProcess(input, input, 20, ref consumer));
        Assert.Equal(0, consumer.Calls);
    }
}
