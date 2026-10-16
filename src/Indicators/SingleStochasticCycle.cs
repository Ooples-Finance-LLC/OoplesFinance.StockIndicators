using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Three-value SMA of one stochastic stage over independently seeded MACD averages.</summary>
/// <remarks>The cycle starts at the first available MACD. Flat windows contribute zero;
/// three complete stochastic values are required. Exact comparisons and extended
/// unpublished values preserve bounded outputs. All history grows lazily.</remarks>
public sealed class SingleStochasticCycle : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive cycle/fast periods and a larger slow period.</summary>
    public SingleStochasticCycle(int cyclePeriod = 10, int fastPeriod = 23, int slowPeriod = 50) : base(2)
    {
        if (cyclePeriod < 1) throw new ArgumentOutOfRangeException(nameof(cyclePeriod));
        if (fastPeriod < 1) throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod <= fastPeriod) throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        CyclePeriod = cyclePeriod; FastPeriod = fastPeriod; SlowPeriod = slowPeriod;
    }
    /// <summary>Complete stochastic window length.</summary>
    public int CyclePeriod { get; }
    /// <summary>Fast SMA-seeded EMA period.</summary>
    public int FastPeriod { get; }
    /// <summary>Slow SMA-seeded EMA period.</summary>
    public int SlowPeriod { get; }
    /// <summary>Cycle value or zero while absent.</summary>
    public IIndicatorOutput Value => Outputs[0];
    /// <summary>One when the cycle value is available.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];
    /// <inheritdoc/>
    protected internal override object CreateState() => new State(CyclePeriod, FastPeriod, SlowPeriod);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules => Enumerable.Range(0, 2).Select(slot =>
        IndicatorValidationRule.Reference(slot, bars => SchaffTrendCycleSnapshot.Calculate(bars, CyclePeriod, FastPeriod, SlowPeriod)
            .Select(value => slot == 0 ? value ?? 0 : value.HasValue ? 1d : 0).ToArray(), IndicatorErrorBudget.Exact));

    private sealed class Extrema(int period, bool maximum)
    {
        private (long Index, JurikDyadic Value)[] _items = [];
        private int _head, _count;
        internal JurikDyadic Value => _items[_head].Value;
        private int Slot(int offset) => offset >= _items.Length - _head ? offset - (_items.Length - _head) : _head + offset;
        internal void Reset() { Array.Clear(_items, 0, _items.Length); _head = _count = 0; }
        internal void Add(long index, JurikDyadic value)
        {
            while (_count > 0 && _items[_head].Index <= index - period)
            {
                _items[_head] = default; _head = Slot(1); _count--;
            }
            while (_count > 0)
            {
                int tail = Slot(_count - 1);
                var difference = value; difference.Subtract(_items[tail].Value);
                if (maximum ? difference.Sign < 0 : difference.Sign > 0) break;
                _items[tail] = default; _count--;
            }
            if (_count == _items.Length)
            {
                int capacity = (int)Math.Min(period, Math.Max(4L, 2L * _items.Length));
                var grown = new (long Index, JurikDyadic Value)[capacity];
                for (int i = 0; i < _count; i++) grown[i] = _items[Slot(i)];
                _items = grown; _head = 0;
            }
            _items[Slot(_count++)] = (index, value);
        }
    }

    internal sealed class State(int cycle, int fast, int slow) : IMultiOutputState
    {
        private readonly SeededDyadicEma.State _fast = new(fast), _slow = new(slow);
        private readonly Extrema _low = new(cycle, false), _high = new(cycle, true);
        private readonly double[] _smoothing = new double[3];
        private JurikDyadic _sum;
        private long _index;
        private int _count, _position;
        public void Reset()
        {
            _fast.Reset(); _slow.Reset(); _low.Reset(); _high.Reset();
            Array.Clear(_smoothing, 0, 3); _sum = default; _index = 0; _count = _position = 0;
        }
        public void Update(in Bar bar, Span<double> output)
        {
            if (!FrameworkCompatibility.IsFinite(bar.Close)) throw new ArgumentOutOfRangeException(nameof(bar));
            output.Clear();
            var input = JurikDyadic.FromDouble(bar.Close);
            bool fastReady = _fast.Add(input, out var f), slowReady = _slow.Add(input, out var s);
            if (!fastReady || !slowReady) return;
            f.Subtract(s); var macd = f.Rounded();
            _low.Add(_index, macd); _high.Add(_index, macd);
            if (++_index < cycle) return;
            var range = _high.Value; range.Subtract(_low.Value);
            double value = 0;
            if (range.Sign != 0)
            {
                macd.Subtract(_low.Value); macd.Multiply(100); value = macd.Ratio(range);
            }
            if (_count == 3) _sum.Subtract(JurikDyadic.FromDouble(_smoothing[_position])); else _count++;
            _smoothing[_position] = value; if (++_position == 3) _position = 0;
            _sum.Add(value);
            if (_count == 3) { output[0] = _sum.Mean(3); output[1] = 1; }
        }
    }
}
