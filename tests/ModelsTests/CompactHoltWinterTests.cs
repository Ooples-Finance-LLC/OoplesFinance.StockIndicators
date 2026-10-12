using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactHoltWinterTests
{
    [Theory]
    [InlineData(1, .2, -.1, 2)]
    [InlineData(20, 2d / 21, .05, .05)]
    [InlineData(20, .13, .37, .21)]
    [InlineData(3, .5, .2, .1)]
    [InlineData(3, 0, 0, 0)]
    [InlineData(3, 1, 1, 1)]
    [InlineData(3, -.1, 0, 0)]
    [InlineData(3, 2, 0, 0)]
    public void CompactStateMatchesPreviousExactStagesAcrossScalesAndResets(int period, double a, double b, double c)
    {
        var actual = new HoltWinterForecast.State(period, a, b, c);
        var expected = new Legacy(period, a, b, c);
        foreach (double scale in new[] { 1d, double.MaxValue / 128, double.Epsilon })
        for (int replay = 0; replay < 2; replay++)
        {
            actual.Reset(); expected.Reset();
            for (int i = 0; i < 128; i++)
            {
                double price = (i * 13 % 101 - 50) * scale;
                if (replay == 1) price = -price;
                var bar = new Bar(default, price, price, price, price, 0);
                double value = expected.Update(bar);
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(actual.Update(bar)));
                // The builder rejects a nonfinite publication at this row.
                if (!double.IsFinite(value)) break;
            }
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(20)]
    public void OrdinaryStateUpdatesIncludingResetAllocateNothing(int period)
    {
        var state = new HoltWinterForecast.State(period, 2d / (period + 1), 1d / period, 1d / period);
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

    // Frozen pre-optimization stages preserve an independent rounding oracle.
    private sealed class Legacy(int period, double a, double b, double c) : IIndicatorState
    {
        private RocBankValue _level,
            _velocity,
            _acceleration;
        private bool _started;

        public void Reset()
        {
            _level = _velocity = _acceleration = default;
            _started = false;
        }

        private static void Product(
            ref ExactMeanAccumulator sum,
            RocBankValue value,
            double factor,
            int shift = 0
        )
        {
            var term = new ExactMeanAccumulator();
            term.AddProduct(value.Mantissa, factor);
            term.ScaleByPowerOfTwo(value.UpperShift + shift);
            sum.AddExact(term);
        }

        public double Update(in Bar bar)
        {
            if (!_started || period == 1)
            {
                _started = true;
                _level = new RocBankValue(bar.Close);
                return bar.Close;
            }
            var f = new ExactMeanAccumulator();
            _level.AddTo(ref f);
            _velocity.AddTo(ref f);
            Product(ref f, _acceleration, 1, -1);
            Product(ref f, _level, -a);
            Product(ref f, _velocity, -a);
            Product(ref f, _acceleration, -a, -1);
            f.AddProduct(bar.Close, a);
            var level = RocBankValue.Round(f);
            var v = new ExactMeanAccumulator();
            _velocity.AddTo(ref v);
            _acceleration.AddTo(ref v);
            Product(ref v, _velocity, -b);
            Product(ref v, _acceleration, -b);
            Product(ref v, level, b);
            Product(ref v, _level, -b);
            var velocity = RocBankValue.Round(v);
            var acc = new ExactMeanAccumulator();
            _acceleration.AddTo(ref acc);
            Product(ref acc, _acceleration, -c);
            Product(ref acc, velocity, c);
            Product(ref acc, _velocity, -c);
            var acceleration = RocBankValue.Round(acc);
            var result = new ExactMeanAccumulator();
            level.AddTo(ref result);
            velocity.AddTo(ref result);
            Product(ref result, acceleration, 1, -1);
            _level = level;
            _velocity = velocity;
            _acceleration = acceleration;
            return result.Mean(1);
        }
    }
}
