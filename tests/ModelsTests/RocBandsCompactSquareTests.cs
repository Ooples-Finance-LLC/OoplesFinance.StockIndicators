using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RocBandsCompactSquareTests(ITestOutputHelper output)
{
    [Fact]
    public void CompactSquaresRetainOriginalBitsThroughExpiryMissingValuesAndReset()
    {
        var random = new Random(255320);
        double[][] prices =
        [
            Enumerable.Range(0, 193).Select(i => 100 + (i % 31) / 8d + (i % 7) / 10d).ToArray(),
            Enumerable.Range(0, 193).Select(i => new[] { 1d, -1d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 0d, -0d }[i % 8]).ToArray(),
            Enumerable.Range(0, 193).Select(_ =>
            {
                var value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
                return double.IsFinite(value) ? value : double.Epsilon;
            }).ToArray(),
            Enumerable.Repeat(1d, 193).ToArray()
        ];
        foreach (var data in prices)
        foreach (var (lag, ema, deviation) in new[] { (1,1,1), (3,5,2), (20,3,3), (63,1,63), (int.MaxValue,int.MaxValue,int.MaxValue), (1,int.MaxValue,1) })
        {
            var actual = new RateOfChangeRmsBands.State(lag,ema,deviation);
            var original = new FrozenState(lag,ema,deviation);
            var a = new double[7]; var b = new double[7];
            for (int replay = 0; replay < 2; replay++)
            {
                actual.Reset(); original.Reset();
                foreach (double value in data)
                {
                    var bar = new Bar(default,value,value,value,value,1);
                    actual.Update(bar,a); original.Update(bar,b);
                    for (int slot = 0; slot < 7; slot++)
                        Assert.Equal(BitConverter.DoubleToInt64Bits(b[slot]),BitConverter.DoubleToInt64Bits(a[slot]));
                }
            }
        }
    }

    [Fact]
    public void CompactRmsWindowReducesOrdinaryAllocations()
    {
        var bars = Enumerable.Range(0,1000).Select(i =>
        {
            double value = 100 + (i % 31) / 8d + (i % 7) / 10d;
            return new Bar(default,value,value,value,value,1);
        }).ToArray();
        var actual = new RateOfChangeRmsBands.State(20,3,3);
        var original = new FrozenState(20,3,3);
        var a = new double[7]; var b = new double[7];
        foreach (var bar in bars) { actual.Update(bar,a); original.Update(bar,b); }
        actual.Reset(); original.Reset();
        long start = GC.GetAllocatedBytesForCurrentThread();
        foreach (var bar in bars) original.Update(bar,b);
        long oldBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        foreach (var bar in bars) actual.Update(bar,a);
        long newBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(b.Select(BitConverter.DoubleToInt64Bits),a.Select(BitConverter.DoubleToInt64Bits));
        output.WriteLine($"1000 ROC band updates: original={oldBytes}, compact={newBytes} bytes");
        Assert.True(newBytes < oldBytes, $"{newBytes} >= {oldBytes}");
    }

    // Frozen pre-change state retains the independent accumulator/root path.
    private sealed class FrozenState(int period, int emaPeriod, int deviationPeriod) : IMultiOutputState
    {
        private readonly Queue<double> _prices = new();
        private readonly Queue<(double Value, bool Present)> _returns = new();
        private ExactMeanAccumulator _seed,
            _squares;
        private double _ema;
        private int _seedCount,
            _missing;
        private bool _emaPossible = true;

        public void Reset()
        {
            _prices.Clear();
            _returns.Clear();
            _seed = _squares = default;
            _ema = 0;
            _seedCount = _missing = 0;
            _emaPossible = true;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            if (_prices.Count < period)
            {
                _prices.Enqueue(bar.Close);
                return;
            }
            var previous = _prices.Dequeue();
            _prices.Enqueue(bar.Close);
            var present = previous != 0;
            var roc = present ? RoundedPercentageChange.Of(bar.Close, previous) : 0;
            outputs[0] = roc;
            outputs[1] = present ? 1 : 0;
            if (double.IsInfinity(roc))
                return;
            UpdateEma(roc, present, outputs);
            if (_returns.Count == deviationPeriod)
            {
                var old = _returns.Dequeue();
                if (old.Present)
                    _squares.AddProduct(old.Value, old.Value, -1);
                else
                    _missing--;
            }
            _returns.Enqueue((roc, present));
            if (present)
                _squares.AddProduct(roc, roc);
            else
                _missing++;
            if (_returns.Count != deviationPeriod || _missing != 0)
                return;
            outputs[4] = _squares.SqrtMean(deviationPeriod);
            outputs[5] = -outputs[4];
            outputs[6] = 1;
        }

        private void UpdateEma(double roc, bool present, Span<double> outputs)
        {
            _emaPossible &= present;
            if (!_emaPossible)
                return;
            if (_seedCount < emaPeriod)
            {
                _seed.Add(roc);
                if (++_seedCount < emaPeriod)
                    return;
                _ema = _seed.Mean(emaPeriod);
            }
            else
            {
                var sum = new ExactMeanAccumulator();
                sum.Add(roc, 2);
                sum.Add(_ema, emaPeriod - 1);
                _ema = sum.Mean((long)emaPeriod + 1);
            }
            outputs[2] = _ema;
            outputs[3] = 1;
        }
    }
}
