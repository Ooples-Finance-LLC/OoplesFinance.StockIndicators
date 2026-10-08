using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Quan-style Jurik adaptive smoothing with bounded histories and wide recurrence stages.</summary>
public static class JurikAdaptiveSnapshot
{
    /// <summary>Calculates the published volatility-adaptive recurrence from fresh state.</summary>
    /// <remarks>Each recurrence stage rounds once with extended upper exponent; only unrepresentable published values are rejected.
    /// Period one preserves the initial price, as in the native recurrence. Histories grow only with observed bars.</remarks>
    public static IReadOnlyList<double> Calculate(
        IReadOnlyList<Bar> bars,
        int period = 20,
        double phase = 0,
        int volatilityPeriod = 10
    )
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (period < 1 || volatilityPeriod < 1 || !FrameworkCompatibility.IsFinite(phase))
            throw new ArgumentOutOfRangeException(nameof(period));
        foreach (var b in bars)
            if (!FrameworkCompatibility.IsFinite(b.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var result = new double[bars.Count];
        if (bars.Count == 0)
            return result;
        var kernel = new JurikCpuKernel(period, phase, volatilityPeriod, bars.Count);
        Span<double> output = stackalloc double[1];
        for (var i = 0; i < bars.Count; i++)
        {
            kernel.Next(bars[i], output, true);
            result[i] = output[0];
        }
        return result;
    }
}

internal sealed class JurikCpuKernel : IndicatorKernel
{
    private readonly int _period, _volatilityPeriod;
    private readonly double _beta, _power, _maximum, _phaseGain, _bandBase, _volatilityWeight;
    private readonly ExtremeDeque _highs, _lows;
    private readonly JurikDyadic[] _history;
    private int _historyPosition, _historyCount;
    private long _seen;
    private JurikDyadic _upper, _lower, _ma, _det0, _det1, _jma, _vsum, _avolty;
    internal JurikCpuKernel(int period, double phase, int volatilityPeriod, int capacity)
    {
        _period = period; _volatilityPeriod = volatilityPeriod;
        _beta = .45 * (period - 1) / (.45 * (period - 1) + 2);
        var length = Math.Max(Math.Log(Math.Sqrt(period - 1)) / Math.Log(2) + 2, 0);
        _power = Math.Max(length - 2, .5);
        _maximum = Math.Pow(length, 1 / _power);
        var len2 = Math.Sqrt(.5 * (period - 1)) * length;
        _bandBase = len2 / (len2 + 1);
        _volatilityWeight = 2 / (Math.Max(4d * period, 30) + 1);
        _phaseGain = FrameworkCompatibility.Clamp(phase * .01 + 1.5, .5, 2.5) + 1;
        _highs = new ExtremeDeque(Math.Min(period, capacity), true);
        _lows = new ExtremeDeque(Math.Min(period, capacity), false);
        _history = new JurikDyadic[Math.Min(volatilityPeriod, capacity)];
    }
    public override int OutputCount => 1;
    public override void Reset()
    {
        _seen = 0; _historyPosition = _historyCount = 0;
        _upper = _lower = _ma = _det0 = _det1 = _jma = _vsum = _avolty = default;
        _highs.Reset(); _lows.Reset();
        Array.Clear(_history, 0, _history.Length);
    }
    private static JurikDyadic U(double value)
    {
        var sum = new JurikDyadic(); sum.Add(value); return sum;
    }
    private static JurikDyadic Stage(JurikDyadic value) => value.Rounded();
    private static JurikDyadic Blend(JurikDyadic old, JurikDyadic next, double weight)
    {
        if (JurikDyadic.TryBlend(old, next, weight, out var fused)) return fused;
        next.Subtract(old);
        return JurikDyadic.SumProduct(old, next, weight);
    }

    private protected override void Evaluate(in Bar bar, Span<double> output, bool commit) => Next(bar, output, commit);
    internal void Next(in Bar bar, Span<double> output, bool commit)
    {
        var input = U(bar.Close);
        if (_seen == 0)
        {
            if (commit)
            {
                _ma = _jma = input;
                _highs.Add(0, bar.Close, 0); _lows.Add(0, bar.Close, 0); _seen = 1;
            }
            output[0] = bar.Close;
            return;
        }
        var upper = _upper; var lower = _lower; var ma = _ma;
        var det0 = _det0; var det1 = _det1; var jma = _jma;
        var vsum = _vsum; var avolty = _avolty;
        var high = U(_highs.NextValue(bar.Close, _seen - _period + 1));
        var low = U(_lows.NextValue(bar.Close, _seen - _period + 1));
        var dh = high; dh.Subtract(upper);
        var dl = low; dl.Subtract(lower);
        var volty = dh; if (volty.Sign < 0) volty.Multiply(-1);
        var absLow = dl; if (absLow.Sign < 0) absLow.Multiply(-1);
        var comparison = absLow; comparison.Subtract(volty);
        if (comparison.Sign > 0) volty = absLow;
        var oldest = _historyCount == 0 || _volatilityPeriod == 1 ? volty
            : _history[_historyCount == _volatilityPeriod ? (_historyPosition + 1) % _history.Length : 0];
        var delta = volty; delta.Subtract(oldest);
        vsum = JurikDyadic.SumProduct(vsum, delta, .1);
        avolty = Blend(avolty, vsum, _volatilityWeight);
        var relative = avolty.Sign <= 0 ? 0 : volty.Ratio(avolty);
        relative = Math.Min(Math.Max(relative, 1), _maximum);
        var pow = Math.Pow(relative, _power);
        var kv = Math.Pow(_bandBase, Math.Sqrt(pow));
        if (dh.Sign > 0) upper = high;
        else upper = JurikDyadic.SumProduct(high, dh, -kv);
        if (dl.Sign < 0) lower = low;
        else lower = JurikDyadic.SumProduct(low, dl, -kv);
        var alpha = Math.Pow(_beta, pow);
        ma = Blend(input, ma, alpha);
        var difference = input; difference.Subtract(ma);
        det0 = Blend(difference, det0, _beta);
        var ma2 = JurikDyadic.SumProduct(ma, det0, _phaseGain);
        ma2.Subtract(jma); ma2.Multiply((1 - alpha) * (1 - alpha));
        det1.Multiply(alpha * alpha); det1.AddExact(ma2); det1 = Stage(det1);
        jma = JurikDyadic.SumProduct(jma, det1, 1);
        var value = jma.Mean(1);
        if (!FrameworkCompatibility.IsFinite(value))
            throw new OverflowException("Jurik output is not representable.");

        if (commit)
        {
            _upper = upper; _lower = lower; _ma = ma; _det0 = det0;
            _det1 = det1; _jma = jma; _vsum = vsum; _avolty = avolty;
            _history[_historyPosition] = volty;
            _historyPosition = (_historyPosition + 1) % _history.Length;
            if (_historyCount < _volatilityPeriod) _historyCount++;
            _highs.Add(_seen, bar.Close, _seen - _period + 1);
            _lows.Add(_seen, bar.Close, _seen - _period + 1);
            _seen++;
        }
        output[0] = value;
    }
}
