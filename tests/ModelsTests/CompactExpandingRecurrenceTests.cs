using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactExpandingRecurrenceTests
{
    [Theory]
    [InlineData(1, 0)] [InlineData(1, .5)] [InlineData(3, .5)] [InlineData(20, 2)]
    [InlineData(int.MaxValue, .5)] [InlineData(3, double.Epsilon)] [InlineData(3, double.MaxValue)]
    public void RecurrencesMatchPreviousExactAlgorithmsAcrossResetAndExtremeInputs(int period, double lambda)
    {
        IIndicatorState[] states = [new ExpandingRegularizedEma.State(period, lambda), new ExpandingZeroLagEma.State(period)];
        IIndicatorState[] originals = [new LegacyExpandingRegularizedEma(period, lambda), new LegacyExpandingZeroLagEma(period)];
        double[][] sequences = [
            Enumerable.Range(0, 160).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            [0, -0d, double.Epsilon, -double.Epsilon, double.MaxValue / 4, -double.MaxValue / 4, 0, 1, -1],
            [double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue],
            [Math.BitIncrement(1d), Math.BitDecrement(1d), 1, .1, 1e-200, 1e200]];
        for (int variant = 0; variant < states.Length; variant++)
        foreach (var prices in sequences)
        {
            states[variant].Reset(); originals[variant].Reset();
            foreach (double price in prices)
            {
                var bar = new Bar(default, price, price, price, price, 0);
                double expected = originals[variant].Update(bar);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(states[variant].Update(bar)));
                if (!double.IsFinite(expected)) break;
            }
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(20)]
    public void WarmedOrdinaryStateUpdatesIncludingResetAllocateNothing(int period)
    {
        foreach (IIndicatorState state in new IIndicatorState[] {
            new ExpandingRegularizedEma.State(period, .5), new ExpandingZeroLagEma.State(period) })
        {
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
            double actual = Run();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(expected, actual);
            Assert.Equal(0, allocated);
        }
    }

    private sealed class LegacyExpandingRegularizedEma(int period, double lambda) : IIndicatorState
    {
        private int _count;
        private double _previous,
            _older;

        public void Reset()
        {
            _count = 0;
            _previous = _older = 0;
        }

        public double Update(in Bar bar)
        {
            if (_count < Math.Max(3, period))
                _count++;
            var result = bar.Close;
            if (_count > 2)
            {
                var effective = Math.Min(_count, period);
                var sum = new ExactMeanAccumulator();
                sum.Add(_previous, effective - 1);
                sum.Add(bar.Close, 2);
                // Multiply by effective+1 without narrowing a maximum-period divisor.
                for (var copy = 0; copy < 2; copy++)
                {
                    sum.AddProduct(_previous, lambda, effective);
                    sum.AddProduct(_previous, lambda);
                }
                sum.AddProduct(_older, lambda, -effective);
                sum.AddProduct(_older, lambda, -1);
                var denominator = new ExactMeanAccumulator();
                var divisor = new BigInteger(effective) + 1;
                denominator.Add(1, divisor);
                denominator.Add(lambda, divisor);
                result = sum.Ratio(denominator);
            }
            _older = _previous;
            _previous = result;
            return result;
        }
    }
    private sealed class LegacyExpandingZeroLagEma(int period) : IIndicatorState
    {
        private readonly Queue<double> _history = new();
        private int _count;
        private double _previous;

        public void Reset()
        {
            _history.Clear();
            _count = 0;
            _previous = 0;
        }

        public double Update(in Bar bar)
        {
            if (_history.Count == (period - 1) / 2 + 1)
                _history.Dequeue();
            _history.Enqueue(bar.Close);
            if (_count < period)
                _count++;
            var sum = new ExactMeanAccumulator();
            sum.Add(bar.Close, 4);
            sum.Add(_history.Peek(), -2);
            sum.Add(_previous, _count - 1);
            _previous = sum.Mean((long)_count + 1);
            return _previous;
        }
    }
}
