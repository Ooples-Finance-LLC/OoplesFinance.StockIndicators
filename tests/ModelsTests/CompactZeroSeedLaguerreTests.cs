using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactZeroSeedLaguerreTests
{
    [Fact]
    public void RoundedMeanCertificateAgreesWithExactAccumulatorAcrossCancellationAndExponentRanges()
    {
        var random = new Random(77431);
        var stages = new double[4];
        int accepted = 0;
        for (int trial = 0; trial < 4000; trial++)
        {
            int exponent = random.Next(-500, 501);
            for (int i = 0; i < stages.Length; i++)
                stages[i] = Math.ScaleB((random.Next(2) == 0 ? -1 : 1) * (1 + random.NextDouble()),
                    trial % 2 == 0 ? exponent : random.Next(-500, 501));
            if (trial % 3 == 0) stages[3] = -stages[0];
            if (trial % 5 == 0) stages[2] = -Math.BitIncrement(stages[1]);
            if (!LaguerreRoundedKernel.TryMean(stages, out double actual)) continue;
            var exact = new ExactMeanAccumulator();
            for (int i = 0; i < stages.Length; i++) exact.AddProduct(stages[i], i is 1 or 2 ? 2 : 1);
            Assert.Equal(BitConverter.DoubleToInt64Bits(exact.Mean(6)), BitConverter.DoubleToInt64Bits(actual));
            accepted++;
        }
        Assert.True(accepted > 0);
    }

    [Theory]
    [InlineData(0)] [InlineData(.1)] [InlineData(.5)] [InlineData(.9)]
    [InlineData(1)] [InlineData(double.Epsilon)] [InlineData(.9999999999999999)]
    public void CompactStagesMatchPriorExactAlgorithmAcrossResets(double gamma)
    {
        var actual = new ZeroSeedLaguerreFilter.State(gamma);
        var expected = new Legacy(gamma);
        double[] extremes = [0, -0d, double.MaxValue, -double.MaxValue, double.Epsilon,
            -double.Epsilon, Math.BitIncrement(1d), Math.BitDecrement(1d), 1e-200, 1e200];
        for (int replay = 0; replay < 2; replay++)
        {
            actual.Reset(); expected.Reset();
            for (int i = 0; i < 256; i++)
            {
                double price = i < extremes.Length ? extremes[replay == 0 ? i : extremes.Length - i - 1]
                    : (i * 13 % 101 - 50) / 8d;
                var bar = new Bar(default, price, price, price, price, 0);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Update(bar)),
                    BitConverter.DoubleToInt64Bits(actual.Update(bar)));
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(.1)] [InlineData(.5)] [InlineData(1)]
    public void OrdinaryUpdatesIncludingResetAllocateNothing(double gamma)
    {
        var state = new ZeroSeedLaguerreFilter.State(gamma);
        double Run()
        {
            state.Reset();
            double last = 0;
            for (int i = 0; i < 1000; i++)
            {
                double price = 100 + (i * 13 % 101) / 8d;
                last = state.Update(new Bar(default, price, price, price, price, 0));
            }
            return last;
        }
        for (int i = 0; i < 3; i++) Run();
        double expected = Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        double result = Run();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected, result);
        Assert.Equal(0, allocated);
    }

    // Frozen pre-optimization implementation: independent accumulator stages
    // catch altered intermediate rounding and extended-overflow cancellation.
    private sealed class Legacy(double gamma) : IIndicatorState
    {
        private readonly RocBankValue[] _stages = new RocBankValue[4];

        public void Reset() => Array.Clear(_stages, 0, _stages.Length);

        private static void Product(
            ref ExactMeanAccumulator sum,
            RocBankValue value,
            double coefficient
        )
        {
            var product = new ExactMeanAccumulator();
            product.AddProduct(value.Mantissa, -coefficient);
            product.ScaleByPowerOfTwo(value.UpperShift);
            sum.Subtract(product);
        }

        public double Update(in Bar bar)
        {
            var sum = new ExactMeanAccumulator();
            sum.Add(bar.Close);
            sum.AddProduct(bar.Close, -gamma);
            Product(ref sum, _stages[0], gamma);
            var current = RocBankValue.Round(sum);
            var prior = _stages[0];
            _stages[0] = current;
            for (var stage = 1; stage < 4; stage++)
            {
                var next = new ExactMeanAccumulator();
                prior.AddTo(ref next);
                Product(ref next, _stages[stage], gamma);
                Product(ref next, current, -gamma);
                prior = _stages[stage];
                current = RocBankValue.Round(next);
                _stages[stage] = current;
            }
            var output = new ExactMeanAccumulator();
            for (var stage = 0; stage < 4; stage++)
                _stages[stage].AddTo(ref output, stage is 1 or 2 ? 2 : 1);
            return output.Mean(6);
        }
    }
}
