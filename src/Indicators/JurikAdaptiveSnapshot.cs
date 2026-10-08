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
    private readonly bool _rejectOverflow;
    private int _historyPosition, _historyCount;
    private long _seen;
    private JurikDyadic _upper, _lower, _ma, _det0, _det1, _jma, _vsum, _avolty;
    internal JurikCpuKernel(int period, double phase, int volatilityPeriod, int capacity, bool rejectOverflow = true)
    {
        _period = period; _volatilityPeriod = volatilityPeriod; _rejectOverflow = rejectOverflow;
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
        return JurikDyadic.FromDouble(value);
    }
    private static JurikDyadic Stage(JurikDyadic value) => value.Rounded();
    private static JurikDyadic Blend(JurikDyadic old, JurikDyadic next, double weight)
    {
        if (JurikDyadic.TryBlend(old, next, weight, out var fused)) return fused;
        next.Subtract(old);
        return JurikDyadic.SumProduct(old, next, weight);
    }

#if !NETFRAMEWORK
    private readonly record struct FastState(double Upper, double Lower, double Ma, double Det0,
        double Det1, double Jma, double Vsum, double Avolty, double Volty);

    private bool TryNextDouble(double input, out FastState state)
    {
        state = default;
        if (!_upper.TryDouble(out var upper) || !_lower.TryDouble(out var lower)
            || !_ma.TryDouble(out var ma) || !_det0.TryDouble(out var det0)
            || !_det1.TryDouble(out var det1) || !_jma.TryDouble(out var jma)
            || !_vsum.TryDouble(out var vsum) || !_avolty.TryDouble(out var avolty)) return false;
        var high = _highs.NextValue(input, _seen - _period + 1);
        var low = _lows.NextValue(input, _seen - _period + 1);
        if (!ExactDifference(high, upper, out var dh) || !ExactDifference(low, lower, out var dl)) return false;
        var volty = Math.Max(Math.Abs(dh), Math.Abs(dl));
        var oldest = volty;
        if (_historyCount != 0 && _volatilityPeriod != 1
            && !_history[_historyCount == _volatilityPeriod ? (_historyPosition + 1) % _history.Length : 0]
                .TryDouble(out oldest)) return false;
        vsum = DifferenceProductSum(volty, oldest, .1, vsum);
        if (!double.IsFinite(vsum)) return false;
        avolty = DifferenceProductSum(vsum, avolty, _volatilityWeight, avolty);
        if (!double.IsFinite(avolty)) return false;
        var relative = avolty <= 0 ? 0 : volty / avolty;
        relative = Math.Min(Math.Max(relative, 1), _maximum);
        var pow = relative == 1 ? 1 : Math.Pow(relative, _power); // NOSONAR: exact identity.
        var kv = pow == 1 ? _bandBase : Math.Pow(_bandBase, Math.Sqrt(pow)); // NOSONAR: exact identity.
        upper = dh > 0 ? high : Math.FusedMultiplyAdd(dh, -kv, high);
        lower = dl < 0 ? low : Math.FusedMultiplyAdd(dl, -kv, low);
        var alpha = pow == 1 ? _beta : Math.Pow(_beta, pow); // NOSONAR: exact identity.
        ma = DifferenceProductSum(ma, input, alpha, input);
        if (!ExactDifference(input, ma, out var difference)) return false;
        det0 = DifferenceProductSum(det0, difference, _beta, difference);
        if (!double.IsFinite(det0)) return false;
        var ma2 = Math.FusedMultiplyAdd(det0, _phaseGain, ma);
        if (!ExactDifference(ma2, jma, out difference)) return false;
        det1 = RoundedProducts(difference, (1 - alpha) * (1 - alpha), det1, alpha * alpha);
        jma += det1;
        if (!double.IsFinite(upper) || !double.IsFinite(lower) || !double.IsFinite(ma)
            || !double.IsFinite(det0) || !double.IsFinite(det1) || !double.IsFinite(jma)
            || !double.IsFinite(vsum) || !double.IsFinite(avolty)) return false;
        state = new(upper, lower, ma, det0, det1, jma, vsum, avolty, volty);
        return true;
    }
    private void CommitFast(FastState state, double input)
    {
        _upper = U(state.Upper); _lower = U(state.Lower); _ma = U(state.Ma); _det0 = U(state.Det0);
        _det1 = U(state.Det1); _jma = U(state.Jma); _vsum = U(state.Vsum); _avolty = U(state.Avolty);
        _history[_historyPosition] = U(state.Volty);
        _historyPosition = (_historyPosition + 1) % _history.Length;
        if (_historyCount < _volatilityPeriod) _historyCount++;
        _highs.Add(_seen, input, _seen - _period + 1); _lows.Add(_seen, input, _seen - _period + 1);
        _seen++;
    }
    private static bool ExactDifference(double a, double b, out double difference)
    {
        difference = a - b;
        var virtualB = difference - a;
        var error = (a - (difference - virtualB)) + (-b - virtualB);
        return double.IsFinite(difference) && error == 0; // NOSONAR: error-free TwoSum certificate.
    }
    private static double DifferenceProductSum(double a, double b, double weight, double sum)
    {
        var difference = TwoSum(a, -b, out var error);
        if (error == 0) return Math.FusedMultiplyAdd(difference, weight, sum); // NOSONAR: exact residual.
        if (SafeProduct(difference, weight) && SafeProduct(error, weight))
        {
            var p = difference * weight; var q = error * weight;
            var e = Math.FusedMultiplyAdd(difference, weight, -p);
            var f = Math.FusedMultiplyAdd(error, weight, -q);
            var high = TwoSum(p, sum, out var low);
            if (TryRoundExpansion(high, low, e, q, f, out var value)) return value;
        }
        var exact = U(a); exact.Subtract(U(b));
        return JurikDyadic.SumProduct(U(sum), exact, weight).Mean(1);
    }
    private static double RoundedProducts(double a, double b, double c, double d)
    {
        // FMA product residuals are exact when neither product loses low bits to
        // underflow. Bound the remaining TwoSum residuals before accepting a round.
        if (SafeProduct(a, b) && SafeProduct(c, d))
        {
            var p = a * b; var q = c * d;
            var e = Math.FusedMultiplyAdd(a, b, -p); var f = Math.FusedMultiplyAdd(c, d, -q);
            var s = TwoSum(p, q, out var t);
            var r = TwoSum(t, e, out var e1);
            r = TwoSum(r, f, out var e2);
            var value = TwoSum(s, r, out var tail);
            var bound = Math.BitIncrement(Math.BitIncrement(Math.Abs(e1) + Math.Abs(e2)) + Math.Abs(tail));
            var gap = Math.Min(Math.BitIncrement(value) - value, value - Math.BitDecrement(value));
            if (double.IsFinite(value) && bound < gap * .5) return value;
        }
        var left = U(a); left.Multiply(b);
        var right = U(c); right.Multiply(d); left.AddExact(right);
        return left.Mean(1);
    }
    // TwoSum expresses the exact input sum as value + tail + e1 + e2 + e3.
    // Each BitIncrement rounds the absolute-error bound outward. A strict bound
    // below half of BOTH neighbor gaps proves the same ties-to-even result;
    // ambiguous midpoints, subnormal boundaries and overflow use exact fallback.
    private static bool TryRoundExpansion(double high, double low, double a, double b, double c, out double value)
    {
        var residual = TwoSum(low, a, out var e1);
        residual = TwoSum(residual, b, out var e2);
        residual = TwoSum(residual, c, out var e3);
        value = TwoSum(high, residual, out var tail);
        var bound = Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(
            Math.Abs(e1) + Math.Abs(e2)) + Math.Abs(e3)) + Math.Abs(tail));
        var gap = Math.Min(Math.BitIncrement(value) - value, value - Math.BitDecrement(value));
        return double.IsFinite(value) && bound < gap * .5;
    }
    private static bool SafeProduct(double a, double b)
    {
        if (a == 0 || b == 0) return true; // NOSONAR: an exact zero product has no residual.
        var left = (int)((BitConverter.DoubleToInt64Bits(a) >> 52) & 2047);
        var right = (int)((BitConverter.DoubleToInt64Bits(b) >> 52) & 2047);
        // Unbiased exponent sum in [-968,1021] keeps the lowest possible
        // product bit above 2^-1074 and the highest product below overflow.
        var sum = left + right;
        return left is > 0 and < 2047 && right is > 0 and < 2047 && sum is >= 1078 and <= 3067;
    }
    private static double TwoSum(double a, double b, out double error)
    {
        var sum = a + b; var virtualB = sum - a;
        error = (a - (sum - virtualB)) + (b - virtualB);
        return sum;
    }
#endif

    private protected override void Evaluate(in Bar bar, Span<double> output, bool commit) => Next(bar, output, commit);
    internal void Next(in Bar bar, Span<double> output, bool commit)
    {
        if (_seen == 0)
        {
            if (commit)
            {
                _ma = _jma = U(bar.Close);
                _highs.Add(0, bar.Close, 0); _lows.Add(0, bar.Close, 0); _seen = 1;
            }
            output[0] = bar.Close;
            return;
        }
#if !NETFRAMEWORK
        if (TryNextDouble(bar.Close, out var fast))
        {
            if (commit) CommitFast(fast, bar.Close);
            output[0] = fast.Jma;
            return;
        }
#endif
        NextWide(bar, output, commit);
    }
    private void NextWide(in Bar bar, Span<double> output, bool commit)
    {
        var input = U(bar.Close);
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
        var pow = relative == 1 ? 1 : Math.Pow(relative, _power); // NOSONAR: exact identity.
        var kv = pow == 1 ? _bandBase : Math.Pow(_bandBase, Math.Sqrt(pow)); // NOSONAR: exact identity.
        if (dh.Sign > 0) upper = high;
        else upper = JurikDyadic.SumProduct(high, dh, -kv);
        if (dl.Sign < 0) lower = low;
        else lower = JurikDyadic.SumProduct(low, dl, -kv);
        var alpha = pow == 1 ? _beta : Math.Pow(_beta, pow); // NOSONAR: exact identity.
        ma = Blend(input, ma, alpha);
        var difference = input; difference.Subtract(ma);
        det0 = Blend(difference, det0, _beta);
        var ma2 = JurikDyadic.SumProduct(ma, det0, _phaseGain);
        ma2.Subtract(jma); ma2.Multiply((1 - alpha) * (1 - alpha));
        det1.Multiply(alpha * alpha); det1.AddExact(ma2); det1 = Stage(det1);
        jma = JurikDyadic.SumProduct(jma, det1, 1);
        var value = jma.Mean(1);
        if (_rejectOverflow && !FrameworkCompatibility.IsFinite(value))
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
