using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal enum HilbertCycleSignal
{
    Phase,
    Sine,
    Trend,
}

internal sealed class HilbertCycleSignalState(HilbertCycleSignal kind, int suppression)
    : IMultiOutputState
{
    private readonly record struct ProjectionCoefficient(JurikDyadic Sine, JurikDyadic Cosine,
        bool SmallSine, bool SmallCosine);
    private static readonly ProjectionCoefficient[][] ProjectionCoefficients = CreateProjectionCoefficients();

    private static ProjectionCoefficient[][] CreateProjectionCoefficients()
    {
        var result = new ProjectionCoefficient[51][];
        for (int count = 0; count <= 50; count++)
        {
            result[count] = new ProjectionCoefficient[count];
            for (int lag = 0; lag < count; lag++)
            {
                // Preserve the original binary64 angle operation order.
                var angle = lag * 2d * Math.PI / count;
                double sine = Math.Sin(angle), cosine = Math.Cos(angle);
                var sin = JurikDyadic.FromDouble(sine); sin.ScaleByPowerOfTwo(0);
                var cos = JurikDyadic.FromDouble(cosine); cos.ScaleByPowerOfTwo(0);
                result[count][lag] = new(sin, cos, Math.Abs(sine) < 1e-12, Math.Abs(cosine) < 1e-12);
            }
        }
        return result;
    }

    private readonly HilbertPhaseState _filter = new(37, true, false);
    private readonly HilbertTrendMeanState? _trend =
        kind == HilbertCycleSignal.Trend ? new() : null;
    private readonly JurikDyadic[] _smooth = new JurikDyadic[50];
    private long _index = -1;
    private int _cursor,
        _days;
    private double _period,
        _phase,
        _sine,
        _lead;

    public void Reset()
    {
        _filter.Reset();
        _trend?.Reset();
        Array.Clear(_smooth, 0, 50);
        _index = -1;
        _cursor = _days = 0;
        _period = _phase = _sine = _lead = 0;
    }

    internal static IEnumerable<IndicatorValidationRule> Rules(
        HilbertCycleSignal kind,
        int suppression
    )
    {
        var count = kind == HilbertCycleSignal.Sine ? 2 : 1;
        return Enumerable
            .Range(0, 2 * count)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        HilbertCycleSignalReference
                            .Values(bars, kind, suppression)[slot % count]
                            .Select(v =>
                                slot < count ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );
    }

    public void Update(in Bar bar, Span<double> output)
    {
        output.Clear();
        _index++;
        var price = JurikDyadic.FromDouble(bar.Close);
        _filter.Update(price);
        if (_index < 37)
        {
            _trend?.Update(price, null);
            return;
        }
        _period = .33 * _filter.Period + .67 * _period;
        var count = (int)(_period + .5);
        _smooth[_cursor] = _filter.SmoothPrice;
        JurikDyadic real = default,
            imaginary = default,
            smallReal = default,
            smallImaginary = default;
        var coefficients = ProjectionCoefficients[count];
        for (var lag = 0; lag < count; lag++)
        {
            var value = _smooth[(_cursor - lag + 50) % 50];
            var coefficient = coefficients[lag];
            var term = value; term.Multiply(coefficient.Sine);
            if (coefficient.SmallSine) smallReal.AddExact(term); else real.AddExact(term);
            term = value; term.Multiply(coefficient.Cosine);
            if (coefficient.SmallCosine) smallImaginary.AddExact(term); else imaginary.AddExact(term);
        }
        // Near-axis Math.Sin/Cos values are small but nonzero. Keep every bit,
        // combining their exact sums last so they do not force all subsequent
        // ordinary products onto the wide path. This partition never rounds or
        // discards a term; only the complete projections round below.
        real.AddExact(smallReal);
        imaginary.AddExact(smallImaginary);
        real = real.Rounded();
        imaginary = imaginary.Rounded();
        var oldPhase = _phase;
        var phase = imaginary.Sign != 0
            ? Math.Atan(real.Ratio(imaginary)) * 180 / Math.PI
            : _phase
                + (
                    real.Sign < 0 ? -90
                    : real.Sign > 0 ? 90
                    : 0
                );
        phase += 90;
        phase += 360 / _period;
        if (imaginary.Sign < 0)
            phase += 180;
        if (phase > 315)
            phase -= 360;
        var sine = Math.Sin(phase * (Math.PI / 180));
        var lead = Math.Sin((phase + 45) * (Math.PI / 180));
        var trend = 1;
        if (_trend is not null)
        {
            _trend.Update(price, count);
            if (sine > lead && _sine <= _lead || sine < lead && _sine >= _lead)
            {
                _days = 0;
                trend = 0;
            }
            _days = Math.Min(_days + 1, 50);
            if (_days < .5 * _period)
                trend = 0;
            var delta = phase - oldPhase;
            if (delta > .67 * 90 * 4 / _period && delta < 1.5 * 90 * 4 / _period)
                trend = 0;
            var baseline = _trend.Average;
            if (baseline.Sign != 0)
            {
                var deviation = _filter.SmoothPrice; deviation.Subtract(baseline);
                if (deviation.Sign < 0) deviation.Multiply(-1);
                if (baseline.Sign < 0) baseline.Multiply(-1);
                baseline.Multiply(.015);
                deviation.Subtract(baseline);
                if (deviation.Sign >= 0) trend = 1;
            }
        }
        _phase = phase;
        _sine = sine;
        _lead = lead;
        _cursor = (_cursor + 1) % 50;
        if (_index < 63L + suppression)
            return;
        if (kind == HilbertCycleSignal.Sine)
        {
            output[0] = sine;
            output[1] = lead;
            output[2] = output[3] = 1;
        }
        else
        {
            output[0] = kind == HilbertCycleSignal.Phase ? phase : trend;
            output[1] = 1;
        }
    }
}
