using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Three-stage Holt-Winter level, velocity, and acceleration forecast.</summary>
/// <remarks>The first close seeds level; velocity and acceleration start at zero.
/// Output is level+velocity+acceleration/2. Each complete state update rounds once
/// at binary64 precision with an extended upper exponent, then the final forecast
/// rounds once. Finite coefficients outside [0,1] are permitted and may diverge.
/// Period one explicitly overrides coefficients with (1,0,0), yielding identity.
/// Storage is constant; unrepresentable final forecasts are rejected by the runtime.</remarks>
public sealed class HoltWinterForecast : IndicatorBase, IMovingAverage, IIndicatorValidationContract
{
    /// <summary>Creates factors 2/(period+1), 1/period, and 1/period for a positive period.</summary>
    public HoltWinterForecast(int period = 14)
        : this(period, 2d / ((long)period + 1), 1d / period, 1d / period) { }

    /// <summary>Derives period by truncating (2-levelFactor)/levelFactor, then uses the supplied factors.</summary>
    /// <remarks>The derived period must be finite and in the positive signed 32-bit range.</remarks>
    public HoltWinterForecast(double levelFactor, double trendFactor, double accelerationFactor)
        : this(DerivePeriod(levelFactor), levelFactor, trendFactor, accelerationFactor) { }

    /// <summary>Creates a positive-period forecast with three finite, explicitly supplied factors.</summary>
    public HoltWinterForecast(
        int period,
        double levelFactor,
        double trendFactor,
        double accelerationFactor
    )
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(levelFactor))
            throw new ArgumentOutOfRangeException(nameof(levelFactor));
        if (!FrameworkCompatibility.IsFinite(trendFactor))
            throw new ArgumentOutOfRangeException(nameof(trendFactor));
        if (!FrameworkCompatibility.IsFinite(accelerationFactor))
            throw new ArgumentOutOfRangeException(nameof(accelerationFactor));
        Period = period;
        LevelFactor = levelFactor;
        TrendFactor = trendFactor;
        AccelerationFactor = accelerationFactor;
    }

    /// <summary>Configured period; one activates the explicit identity convention.</summary>
    public int Period { get; }

    /// <summary>Configured level factor, overridden when Period is one.</summary>
    public double LevelFactor { get; }

    /// <summary>Configured velocity factor, overridden when Period is one.</summary>
    public double TrendFactor { get; }

    /// <summary>Configured acceleration factor, overridden when Period is one.</summary>
    public double AccelerationFactor { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, LevelFactor, TrendFactor, AccelerationFactor);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    private static int DerivePeriod(double levelFactor)
    {
        var derived = (2 - levelFactor) / levelFactor;
        if (
            !FrameworkCompatibility.IsFinite(levelFactor)
            || !FrameworkCompatibility.IsFinite(derived)
            || derived < 1
            || derived >= (double)int.MaxValue + 1
        )
            throw new ArgumentOutOfRangeException(nameof(levelFactor));
        return (int)derived;
    }

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count];
        var level = new ReferenceFraction(0);
        var velocity = level;
        var acceleration = level;
        var a = ReferenceFraction.FromDouble(LevelFactor);
        var b = ReferenceFraction.FromDouble(TrendFactor);
        var c = ReferenceFraction.FromDouble(AccelerationFactor);
        var one = new ReferenceFraction(1);
        var half = one / new ReferenceFraction(2);
        for (var i = 0; i < bars.Count; i++)
        {
            var input = ReferenceFraction.FromDouble(bars[i].Close);
            if (i == 0 || Period == 1)
            {
                level = input;
                values[i] = bars[i].Close;
                continue;
            }
            var f = (
                (one - a) * (level + velocity + half * acceleration) + a * input
            ).RoundExtendedBinary64();
            var v = (
                (one - b) * (velocity + acceleration) + b * (f - level)
            ).RoundExtendedBinary64();
            var acc = ((one - c) * acceleration + c * (v - velocity)).RoundExtendedBinary64();
            values[i] = (f + v + half * acc).ToDouble();
            if (double.IsInfinity(values[i]))
                break;
            level = f;
            velocity = v;
            acceleration = acc;
        }
        return values;
    }

    private sealed class State(int period, double a, double b, double c) : IIndicatorState
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
