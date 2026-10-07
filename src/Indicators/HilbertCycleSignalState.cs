using System.Numerics;
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
    private static readonly BigInteger Grid = BigInteger.One << 1074;
    private readonly HilbertPhaseState _filter = new(37, true, false);
    private readonly HilbertTrendMeanState? _trend =
        kind == HilbertCycleSignal.Trend ? new() : null;
    private readonly BigInteger[] _smooth = new BigInteger[50];
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
        var price = ExactVarianceWindow.Units(bar.Close);
        _filter.Update(price);
        if (_index < 37)
        {
            _trend?.Update(price, null);
            return;
        }
        _period = .33 * _filter.Period + .67 * _period;
        var count = (int)(_period + .5);
        _smooth[_cursor] = _filter.SmoothPrice;
        BigInteger real = 0,
            imaginary = 0;
        for (var lag = 0; lag < count; lag++)
        {
            var angle = lag * 2d * Math.PI / count;
            var value = _smooth[(_cursor - lag + 50) % 50];
            real += ExactVarianceWindow.Units(Math.Sin(angle)) * value;
            imaginary += ExactVarianceWindow.Units(Math.Cos(angle)) * value;
        }
        real = RocBankValue.RoundUnits(real, Grid);
        imaginary = RocBankValue.RoundUnits(imaginary, Grid);
        var oldPhase = _phase;
        var phase = !imaginary.IsZero
            ? Math.Atan(
                ExactMeanAccumulator.UnitRatio(
                    (real * imaginary.Sign) << 1074,
                    BigInteger.Abs(imaginary)
                )
            )
                * 180
                / Math.PI
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
            var baseline = _trend.RoundedUnits;
            if (
                !baseline.IsZero
                && BigInteger.Abs(_filter.SmoothPrice - baseline) * Grid
                    >= ExactVarianceWindow.Units(.015) * BigInteger.Abs(baseline)
            )
                trend = 1;
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
