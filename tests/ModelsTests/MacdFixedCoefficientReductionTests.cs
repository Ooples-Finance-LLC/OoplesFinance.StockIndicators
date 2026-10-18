using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class MacdFixedCoefficientReductionTests(ITestOutputHelper output)
{
    private static Bar[] Bars(double[] prices) => prices.Select(v => new Bar(default,v,v,v,v,1)).ToArray();

    [Fact]
    public void ReducedCoefficientsRetainOriginalBitsForEverySelectionAndReset()
    {
        var random = new Random(255075);
        double[][] cases =
        [
            Enumerable.Range(0, 97).Select(i => 100 + i / 8d + (i % 7) / 10d).ToArray(),
            Enumerable.Range(0, 97).Select(i => new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0d, -0d, 1d, -1d }[i % 8]).ToArray(),
            Enumerable.Range(0, 97).Select(_ =>
            {
                var value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
                return double.IsFinite(value) ? value : double.Epsilon;
            }).ToArray()
        ];
        foreach (var prices in cases)
        foreach (bool first in new[] { false, true })
        foreach (int signal in new[] { 1, 9, int.MaxValue })
        foreach (int selection in Enumerable.Range(1, 7))
        {
            var owner = new AlignedEmaMacd(26,12,signal,first,2,true,(EmaDifferenceSelection)selection);
            var actual = new AlignedEmaMacd.State(owner);
            var original = new FrozenState(owner);
            var a = new double[6]; var b = new double[6];
            for (int replay = 0; replay < 2; replay++)
            {
                actual.Reset(); original.Reset();
                foreach (var bar in Bars(prices))
                {
                    actual.Update(bar,a); original.Update(bar,b);
                    for (int slot = 0; slot < 6; slot++)
                        Assert.Equal(BitConverter.DoubleToInt64Bits(b[slot]), BitConverter.DoubleToInt64Bits(a[slot]));
                }
            }
        }
    }

    [Fact]
    public void FixedCoefficientRecurrenceReducesAllocations()
    {
        var bars = Bars(Enumerable.Range(0, 1000).Select(i => 100 + i / 8d + (i % 7) / 10d).ToArray());
        var owner = new AlignedEmaMacd(fixedCoefficients:true);
        var actual = new AlignedEmaMacd.State(owner);
        var original = new FrozenState(owner);
        var a = new double[6]; var b = new double[6];
        foreach (var bar in bars) { actual.Update(bar,a); original.Update(bar,b); }
        actual.Reset(); original.Reset();
        long start = GC.GetAllocatedBytesForCurrentThread();
        foreach (var bar in bars) original.Update(bar,b);
        long oldBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        foreach (var bar in bars) actual.Update(bar,a);
        long newBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(b.Select(BitConverter.DoubleToInt64Bits),a.Select(BitConverter.DoubleToInt64Bits));
        output.WriteLine($"1000 fixed MACD updates: original={oldBytes}, reduced={newBytes} bytes");
        Assert.True(newBytes < oldBytes, $"{newBytes} >= {oldBytes}");
    }

    // Frozen pre-change average and state preserve the original rounding stages.
    private sealed class Average(int period, bool first, double? coefficient)
    {
        private long _count;
        private BigInteger _sum;
        internal BigInteger Value { get; private set; }
        private readonly BigInteger _alpha = coefficient.HasValue
            ? ExactVarianceWindow.Units(coefficient.Value)
            : 0;

        public void Reset()
        {
            _count = 0;
            _sum = Value = 0;
        }

        internal void Add(BigInteger input)
        {
            if (_count < (first ? 1 : period))
            {
                _count++;
                _sum += input;
                if (_count == (first ? 1 : period))
                {
                    Value = RocBankValue.RoundUnits(_sum, first ? 1 : period);
                    _sum = 0;
                }
                return;
            }
            Value = coefficient.HasValue
                ? RocBankValue.RoundUnits(
                    Value * ((BigInteger.One << 1074) - _alpha) + input * _alpha,
                    BigInteger.One << 1074
                )
                : RocBankValue.RoundUnits(Value * (period - 1) + 2 * input, (long)period + 1);
        }
    }

    private sealed class FrozenState(AlignedEmaMacd owner) : IMultiOutputState
    {
        private long _index = -1;
        private readonly Average _fast =
                new(owner.FastPeriod, owner.FirstPrice, owner.FixedCoefficients ? .15 : null),
            _slow = new(owner.SlowPeriod, owner.FirstPrice, owner.FixedCoefficients ? .075 : null),
            _signal = new(owner.SignalPeriod, owner.FirstPrice, null);

        public void Reset()
        {
            _index = -1;
            _fast.Reset();
            _slow.Reset();
            _signal.Reset();
        }

        private void Publish(BigInteger value, int slot, Span<double> output)
        {
            if (((int)owner.Selection & (1 << slot)) != 0)
            {
                output[slot] = ExactMeanAccumulator.UnitRatio(value, 1);
                output[slot + 3] = 1;
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            var input = ExactVarianceWindow.Units(bar.Close);
            _slow.Add(input);
            if (owner.FirstPrice || _index >= owner.SlowPeriod - owner.FastPeriod)
                _fast.Add(input);
            var start = (long)owner.SlowPeriod - 1 + owner.Suppression;
            if (_index < start)
                return;
            var difference = RocBankValue.RoundUnits(_fast.Value - _slow.Value, 1);
            _signal.Add(difference);
            if (_index < start + owner.SignalPeriod - 1L + owner.Suppression)
                return;
            Publish(difference, 0, output);
            Publish(_signal.Value, 1, output);
            Publish(RocBankValue.RoundUnits(difference - _signal.Value, 1), 2, output);
        }
    }
}
