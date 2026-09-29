namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Wilder stop, seeded long at the first low; only completed bars change the state.</summary>
internal sealed class ParabolicSarKernel
{
    private readonly double _start, _increment, _maximum;
    private bool _initialized, _long = true;
    private long _count;
    private RocBankValue _sar;
    private double _extreme, _acceleration, _high1, _high2, _low1, _low2;
    private ExactMeanAccumulator _previousSpread;
    internal Signal Signal { get; private set; }
    private static int Compare(RocBankValue left, double right)
    { var difference = new ExactMeanAccumulator(); left.AddTo(ref difference); difference.Add(right, -1); return difference.Sign; }
    private static RocBankValue Step(RocBankValue stop, double acceleration, double extreme)
    {
        var sum = new ExactMeanAccumulator(); sum.AddProduct(stop.Mantissa, acceleration, -1); sum.ScaleByPowerOfTwo(stop.UpperShift);
        stop.AddTo(ref sum); sum.AddProduct(acceleration, extreme); return RocBankValue.Round(sum);
    }

    internal ParabolicSarKernel(double start, double increment, double maximum)
    {
        if (double.IsNaN(start) || double.IsInfinity(start) || start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (double.IsNaN(increment) || double.IsInfinity(increment) || increment < 0) throw new ArgumentOutOfRangeException(nameof(increment));
        if (double.IsNaN(maximum) || double.IsInfinity(maximum) || maximum < start) throw new ArgumentOutOfRangeException(nameof(maximum));
        _start = start; _increment = increment; _maximum = maximum;
    }

    internal double Next(double high, double low, bool final)
    {
        var rising = _long;
        var extreme = _initialized ? _extreme : high;
        var acceleration = _initialized ? _acceleration : _start;
        var stop = _initialized ? Step(_sar, acceleration, extreme) : new RocBankValue(low);
        if (_initialized)
        {
            var bound = rising ? (_count > 1 ? Math.Min(_low1, _low2) : _low1)
                : (_count > 1 ? Math.Max(_high1, _high2) : _high1);
            if (rising ? Compare(stop, bound) > 0 : Compare(stop, bound) < 0) stop = new(bound);
            if (rising ? Compare(stop, low) > 0 : Compare(stop, high) < 0)
            {
                stop = new(rising ? Math.Max(extreme, high) : Math.Min(extreme, low));
                rising = !rising;
                extreme = rising ? high : low;
                acceleration = _start;
            }
            else if (rising ? high > extreme : low < extreme)
            {
                extreme = rising ? high : low;
                var increase = new ExactMeanAccumulator(); increase.Add(acceleration); increase.Add(_increment); acceleration = Math.Min(_maximum, increase.Mean(1));
            }
        }
        var spread = new ExactMeanAccumulator(); spread.Add(high); stop.AddTo(ref spread, -1); var change = spread; change.Subtract(_previousSpread);
        if (final)
        {
            Signal = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
            _previousSpread = spread;
            _initialized = true; _count++; _long = rising;
            _sar = stop; _extreme = extreme; _acceleration = acceleration;
            _high2 = _high1; _high1 = high; _low2 = _low1; _low1 = low;
        }
        return stop.Publish();
    }

    internal void Reset()
    {
        _initialized = false; _long = true; _count = 0;
        _sar = default; _previousSpread = default; Signal = Signal.None; _extreme = _acceleration = _high1 = _high2 = _low1 = _low2 = 0;
    }
}
