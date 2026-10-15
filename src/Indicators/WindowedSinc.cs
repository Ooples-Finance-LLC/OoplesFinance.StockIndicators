using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Oldest-first windowed sinc filter, extending startup with the newest close.</summary>
/// <remarks>Coefficients use binary64 trigonometric functions. The complete weighted
/// ratio rounds once. Decoded coefficients are shared across executions and workers;
/// each state retains its own rolling history. Kernel storage grows with taps.</remarks>
public sealed class WindowedSinc : IndicatorBase, IIndicatorValidationContract
{
#if NETFRAMEWORK
    private readonly ExactMeanAccumulator.ProductCoefficient[] _coefficients;
    private readonly ExactMeanAccumulator _mass;
#else
    private readonly ExactMeanAccumulator.ProductFactor[] _compactCoefficients;
    private readonly JurikDyadic _compactMass;
#endif

    /// <summary>Creates a defined, nonzero sinc kernel with positive period and taps.</summary>
    public WindowedSinc(int period = 14, int taps = 21, SincWindow window = SincWindow.Hann)
    {
        WindowedSincSnapshot.ValidateParameters(period, taps, window);
        var weights = WindowedSincSnapshot.CreateWeights(period, taps, window, out var mass);
#if NETFRAMEWORK
        _mass = mass;
        _coefficients = weights.Select(w => new ExactMeanAccumulator.ProductCoefficient(w)).ToArray();
#else
        _compactCoefficients = new ExactMeanAccumulator.ProductFactor[weights.Length];
        var compactMass = new JurikDyadic();
        for (int i = 0; i < weights.Length; i++)
        {
            var factor = new ExactMeanAccumulator.ProductFactor(weights[i]);
            _compactCoefficients[i] = factor;
            compactMass.AddExact(JurikDyadic.FromFactor(in factor));
        }
        _compactMass = compactMass;
#endif
        Period = period;
        Taps = taps;
        Window = window;
    }

    /// <summary>Sinc wavelength parameter.</summary>
    public int Period { get; }
    /// <summary>Number of coefficients and retained closes.</summary>
    public int Taps { get; }
    /// <summary>Window function applied to the sinc kernel.</summary>
    public SincWindow Window { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Taps - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() =>
#if !NETFRAMEWORK
        new CompactState(_compactCoefficients, _compactMass);
#else
        new State(_coefficients, _mass);
#endif
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.ReferenceWithOverflowRejection(0,
            bars => WindowedSincSnapshot.Calculate(bars, Period, Taps, Window), IndicatorErrorBudget.Exact)];

#if NETFRAMEWORK
    private sealed class State(ExactMeanAccumulator.ProductCoefficient[] coefficients, ExactMeanAccumulator mass) : IIndicatorState
    {
        private ExactMeanAccumulator.ProductFactor[] _history = [];
        private int _count, _position;

        public void Reset() => _count = _position = 0;

        public double Update(in Bar bar)
        {
            if (_count < coefficients.Length)
            {
                if (_count == _history.Length)
                {
                    int capacity = _count == 0 ? Math.Min(coefficients.Length, 4)
                        : _count <= coefficients.Length / 2 ? _count * 2 : coefficients.Length;
                    Array.Resize(ref _history, capacity);
                    _position = _count;
                }
                _count++;
            }
            var newest = new ExactMeanAccumulator.ProductFactor(bar.Close);
            _history[_position] = newest;
            if (++_position == _history.Length) _position = 0;
            int oldest = _count == coefficients.Length ? _position : 0, coefficient = 0;
            var sum = new ExactMeanAccumulator();
            for (int i = oldest; i < _count; i++)
                sum.AddProduct(in _history[i], in coefficients[coefficient++]);
            for (int i = 0; i < oldest; i++)
                sum.AddProduct(in _history[i], in coefficients[coefficient++]);
            // Startup keeps the entire kernel and repeats the newest price.
            for (; coefficient < coefficients.Length; coefficient++)
                sum.AddProduct(in newest, in coefficients[coefficient]);
            return sum.Ratio(mass);
        }
    }

#else
    private sealed class CompactState(ExactMeanAccumulator.ProductFactor[] coefficients, JurikDyadic mass) : IIndicatorState
    {
        private ExactMeanAccumulator.ProductFactor[] _history = [];
        private int _count, _position;

        public void Reset() => _count = _position = 0;

        public double Update(in Bar bar)
        {
            if (_count < coefficients.Length)
            {
                if (_count == _history.Length)
                {
                    int capacity = _count == 0 ? Math.Min(coefficients.Length, 4)
                        : _count <= coefficients.Length / 2 ? _count * 2 : coefficients.Length;
                    Array.Resize(ref _history, capacity);
                    _position = _count;
                }
                _count++;
            }
            var newest = new ExactMeanAccumulator.ProductFactor(bar.Close);
            _history[_position] = newest;
            if (++_position == _history.Length) _position = 0;
            int oldest = _count == coefficients.Length ? _position : 0, coefficient = 0;
            var sum = new JurikDyadic();
            for (int i = oldest; i < _count; i++) Add(in _history[i], in coefficients[coefficient++], ref sum);
            for (int i = 0; i < oldest; i++) Add(in _history[i], in coefficients[coefficient++], ref sum);
            for (; coefficient < coefficients.Length; coefficient++) Add(in newest, in coefficients[coefficient], ref sum);
            return sum.Ratio(mass);
        }

        private static void Add(in ExactMeanAccumulator.ProductFactor value,
            in ExactMeanAccumulator.ProductFactor coefficient, ref JurikDyadic sum)
        {
            // Each normalized finite factor has at most 53 mantissa bits.
            // Their exact product fits Int128, including negative sinc weights.
            var product = JurikDyadic.FromBinaryProduct(
                (Int128)value.Mantissa * coefficient.Mantissa,
                value.Scale + coefficient.Scale - 2148);
            sum.AddExact(product);
        }
    }
#endif
}
