using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns 100 for a doji with both shadows longer than its body and a body near the range midpoint; otherwise zero.</summary>
/// <remarks>Doji body is at most one tenth of the preceding mean range. The body must intersect the midpoint tolerance band, whose width each side is one fifth of a separate preceding mean range. Both endpoints are inclusive.</remarks>
public sealed class RickshawManCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive prior-range periods.</summary>
    public RickshawManCandle(int dojiPeriod = 10, int nearPeriod = 5)
    {
        if (dojiPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(dojiPeriod));
        if (nearPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(nearPeriod));
        DojiPeriod = dojiPeriod;
        NearPeriod = nearPeriod;
    }

    /// <summary>Prior ranges defining the doji body threshold.</summary>
    public int DojiPeriod { get; }

    /// <summary>Prior ranges defining the midpoint tolerance.</summary>
    public int NearPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(DojiPeriod, NearPeriod);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(DojiPeriod, NearPeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 100),
            IndicatorValidationRule.Reference(0, Reference, 0, 0),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count];
        for (var i = WarmupBars; i < bars.Count; i++)
        {
            var b = bars[i];
            var top = R(Math.Max(b.Open, b.Close));
            var bottom = R(Math.Min(b.Open, b.Close));
            var body = top - bottom;
            var doji = new ReferenceFraction(0);
            var near = new ReferenceFraction(0);
            for (var j = i - DojiPeriod; j < i; j++)
                doji += R(bars[j].High) - R(bars[j].Low);
            for (var j = i - NearPeriod; j < i; j++)
                near += R(bars[j].High) - R(bars[j].Low);
            doji /= new ReferenceFraction(DojiPeriod) * new ReferenceFraction(10);
            near /= new ReferenceFraction(NearPeriod) * new ReferenceFraction(5);
            var mid = (R(b.High) + R(b.Low)) / new ReferenceFraction(2);
            if (
                body.CompareTo(doji) <= 0
                && (R(b.High) - top).CompareTo(body) > 0
                && (bottom - R(b.Low)).CompareTo(body) > 0
                && bottom.CompareTo(mid + near) <= 0
                && top.CompareTo(mid - near) >= 0
            )
                values[i] = 100;
        }
        return values;
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);

    private sealed class State(int dojiPeriod, int nearPeriod) : IIndicatorState
    {
        private readonly Queue<Bar> _dojiHistory = new(),
            _nearHistory = new();
        private readonly BigInteger _dojiDen = new BigInteger(dojiPeriod) * 10,
            _nearDen = new BigInteger(nearPeriod) * 5;
        private readonly int _warmup = Math.Max(dojiPeriod, nearPeriod);
        private ExactMeanAccumulator _doji,
            _near;
        private int _seen;

        public void Reset()
        {
            _dojiHistory.Clear();
            _nearHistory.Clear();
            _doji = _near = default;
            _seen = 0;
        }

        public double Update(in Bar b)
        {
            var value = _seen >= _warmup && Matches(b) ? 100d : 0;
            if (_dojiHistory.Count == dojiPeriod)
                Add(ref _doji, _dojiHistory.Dequeue(), -1);
            _dojiHistory.Enqueue(b);
            Add(ref _doji, b, 1);
            if (_nearHistory.Count == nearPeriod)
                Add(ref _near, _nearHistory.Dequeue(), -1);
            _nearHistory.Enqueue(b);
            Add(ref _near, b, 1);
            if (_seen < _warmup)
                _seen++;
            return value;
        }

        private static void Add(ref ExactMeanAccumulator sum, in Bar b, int sign)
        {
            sum.Add(b.High, sign);
            sum.Add(b.Low, -sign);
        }

        private bool Matches(in Bar b)
        {
            var top = Math.Max(b.Open, b.Close);
            var bottom = Math.Min(b.Open, b.Close);
            var doji = _doji;
            doji.Add(top, -_dojiDen);
            doji.Add(bottom, _dojiDen);
            if (doji.Sign < 0)
                return false;
            var upper = new ExactMeanAccumulator();
            upper.Add(b.High);
            upper.Add(bottom);
            upper.Add(top, -2);
            if (upper.Sign <= 0)
                return false;
            var lower = new ExactMeanAccumulator();
            lower.Add(bottom, 2);
            lower.Add(top, -1);
            lower.Add(b.Low, -1);
            if (lower.Sign <= 0)
                return false;
            var above = _near;
            above.ScaleByPowerOfTwo(1);
            above.Add(b.High, _nearDen);
            above.Add(b.Low, _nearDen);
            above.Add(bottom, -2 * _nearDen);
            var below = _near;
            below.ScaleByPowerOfTwo(1);
            below.Add(top, 2 * _nearDen);
            below.Add(b.High, -_nearDen);
            below.Add(b.Low, -_nearDen);
            return above.Sign >= 0 && below.Sign >= 0;
        }
    }
}
