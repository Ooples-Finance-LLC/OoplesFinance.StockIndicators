using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes an inside candle followed by a directional high/low breakout. Returns 100 for a bullish formation, -100 for bearish, and twice that value for confirmation within the next three bars.</summary>
/// <remarks>A new formation supersedes pending confirmation. Formations are tracked during warmup; outputs begin at index five. Confirmation requires a strict close beyond the inside candle's opposite extreme.</remarks>
public sealed class HikkakeCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <inheritdoc/>
    public override int WarmupBars => 5;

    /// <inheritdoc/>
    protected internal override object CreateState() => new HikkakeState(0);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -200, 200),
            IndicatorValidationRule.Reference(0, bars => HikkakeReference.Evaluate(bars, 0), 0, 0),
        ];
}

/// <summary>Recognizes two successive inside candles followed by a directional high/low breakout, with the first inside close near its low for bullish formations or high for bearish. Returns signed 100 for formation and signed 200 for confirmation within three bars.</summary>
/// <remarks>The near tolerance is one fifth of the mean range preceding the first inside candle. Its boundary is inclusive. New formations supersede pending confirmation, including during warmup.</remarks>
public sealed class ModifiedHikkakeCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive near period at most Int32.MaxValue minus five.</summary>
    public ModifiedHikkakeCandle(int period = 5)
    {
        if (period < 1 || period > int.MaxValue - 5)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior observations used for the near threshold.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 5;

    /// <inheritdoc/>
    protected internal override object CreateState() => new HikkakeState(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -200, 200),
            IndicatorValidationRule.Reference(
                0,
                bars => HikkakeReference.Evaluate(bars, Period),
                0,
                0
            ),
        ];
}

internal sealed class HikkakeState(int period) : IIndicatorState
{
    private readonly Queue<Bar> _history = new();
    private readonly BigInteger _den = new BigInteger(period) * 5;
    private ExactMeanAccumulator _sum,
        _lag1,
        _lag2;
    private Bar _before,
        _first,
        _second;
    private int _seen,
        _direction,
        _age = 4;
    private double _confirmation;

    public void Reset()
    {
        _history.Clear();
        _sum = _lag1 = _lag2 = default;
        _before = _first = _second = default;
        _seen = _direction = 0;
        _age = 4;
        _confirmation = 0;
    }

    public double Update(in Bar bar)
    {
        if (_age < 4)
            _age++;
        var signal = 0d;
        if (_seen >= period + 2)
        {
            var direction = Formation(bar);
            if (direction != 0)
            {
                _direction = direction;
                _age = 0;
                _confirmation = direction > 0 ? _second.High : _second.Low;
                signal = direction * 100;
            }
            else if (
                _direction != 0
                && _age <= 3
                && (_direction > 0 ? bar.Close > _confirmation : bar.Close < _confirmation)
            )
            {
                signal = _direction * 200;
                _direction = 0;
                _age = 4;
            }
        }
        var value = _seen >= period + 5 ? signal : 0;
        _lag2 = _lag1;
        _lag1 = _sum;
        if (period > 0)
        {
            if (_history.Count == period)
                Add(_history.Dequeue(), -1);
            _history.Enqueue(bar);
            Add(bar, 1);
        }
        _before = _first;
        _first = _second;
        _second = bar;
        if (_seen < period + 5)
            _seen++;
        return value;
    }

    private void Add(in Bar b, int sign)
    {
        _sum.Add(b.High, sign);
        _sum.Add(b.Low, -sign);
    }

    private int Formation(in Bar b)
    {
        if (_second.High >= _first.High || _second.Low <= _first.Low)
            return 0;
        var direction =
            b.High < _second.High && b.Low < _second.Low ? 1
            : b.High > _second.High && b.Low > _second.Low ? -1
            : 0;
        if (period == 0 || direction == 0)
            return direction;
        if (_first.High >= _before.High || _first.Low <= _before.Low)
            return 0;
        var margin = _lag2;
        margin.Add(direction > 0 ? _first.Close : _first.High, -_den);
        margin.Add(direction > 0 ? _first.Low : _first.Close, _den);
        return margin.Sign >= 0 ? direction : 0;
    }
}

internal static class HikkakeReference
{
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, int period)
    {
        var values = new double[bars.Count];
        var pending = -4;
        var direction = 0;
        for (var i = period + 2; i < bars.Count; i++)
        {
            var a = bars[i - 2];
            var b = bars[i - 1];
            var c = bars[i];
            var formed = 0;
            if (b.High < a.High && b.Low > a.Low)
                formed =
                    c.High < b.High && c.Low < b.Low ? 1
                    : c.High > b.High && c.Low > b.Low ? -1
                    : 0;
            if (formed != 0 && period > 0)
            {
                var before = bars[i - 3];
                var sum = new ReferenceFraction(0);
                for (var j = i - 2 - period; j < i - 2; j++)
                    sum += R(bars[j].High) - R(bars[j].Low);
                var distance = formed > 0 ? R(a.Close) - R(a.Low) : R(a.High) - R(a.Close);
                if (
                    a.High >= before.High
                    || a.Low <= before.Low
                    || distance.CompareTo(sum / new ReferenceFraction(new BigInteger(period) * 5))
                        > 0
                )
                    formed = 0;
            }
            var result = 0;
            if (formed != 0)
            {
                direction = formed;
                pending = i;
                result = formed * 100;
            }
            else if (
                direction != 0
                && i - pending <= 3
                && (
                    direction > 0
                        ? c.Close > bars[pending - 1].High
                        : c.Close < bars[pending - 1].Low
                )
            )
            {
                result = direction * 200;
                direction = 0;
            }
            if (i >= period + 5)
                values[i] = result;
        }
        return values;
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
}
