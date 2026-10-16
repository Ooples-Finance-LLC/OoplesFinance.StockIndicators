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

    internal sealed class State(int period, double a, double b, double c) : IIndicatorState
    {
        private JurikDyadic _level, _velocity, _acceleration;
        private bool _started;

        public void Reset()
        {
            _level = _velocity = _acceleration = default;
            _started = false;
        }

        private static JurikDyadic Forecast(JurikDyadic level, JurikDyadic velocity, JurikDyadic acceleration)
        {
            acceleration.ScaleByPowerOfTwo(-1);
            level.AddExact(velocity);
            level.AddExact(acceleration);
            return level;
        }

        public double Update(in Bar bar)
        {
            if (!_started || period == 1)
            {
                _started = true;
                _level = JurikDyadic.FromDouble(bar.Close);
                return bar.Close;
            }
#if !NETFRAMEWORK
            if (_level.TryDouble(out double oldLevel) && _velocity.TryDouble(out double oldVelocity)
                && _acceleration.TryDouble(out double oldAcceleration)
                && HoltWinterRoundedKernel.TryUpdate(oldLevel, oldVelocity, oldAcceleration,
                    bar.Close, a, b, c, out double fastLevel, out double fastVelocity,
                    out double fastAcceleration, out double fastOutput))
            {
                _level = JurikDyadic.FromDouble(fastLevel);
                _velocity = JurikDyadic.FromDouble(fastVelocity);
                _acceleration = JurikDyadic.FromDouble(fastAcceleration);
                return fastOutput;
            }
#endif
            var forecast = Forecast(_level, _velocity, _acceleration);
            var difference = JurikDyadic.FromDouble(bar.Close);
            difference.Subtract(forecast);
            var level = JurikDyadic.SumProduct(forecast, difference, a);

            var trend = _velocity;
            trend.AddExact(_acceleration);
            difference = level;
            difference.Subtract(_level);
            difference.Subtract(trend);
            var velocity = JurikDyadic.SumProduct(trend, difference, b);

            difference = velocity;
            difference.Subtract(_velocity);
            difference.Subtract(_acceleration);
            var acceleration = JurikDyadic.SumProduct(_acceleration, difference, c);
            var result = Forecast(level, velocity, acceleration);
            _level = level;
            _velocity = velocity;
            _acceleration = acceleration;
            return result.Mean(1);
        }
    }
}
