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
    protected internal override object CreateState()
    {
#if !NETFRAMEWORK
        // The grid state reserves its windows. Bound only fast-path eligibility,
        // not the public period contract: huge periods retain lazy general storage.
        if (DojiPeriod <= 4096 && NearPeriod <= 4096)
            return new RickshawGridState(DojiPeriod, NearPeriod);
#endif
        return new State(DojiPeriod, NearPeriod);
    }

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

    internal sealed class State(int dojiPeriod, int nearPeriod, bool reserve = false) : IPreviewIndicatorState
    {
        private readonly Queue<ExactMeanAccumulator> _dojiHistory = new(reserve ? dojiPeriod : 0),
            _nearHistory = new(reserve ? nearPeriod : 0);
        private readonly long _dojiDen = (long)dojiPeriod * 10,
            _nearDen = (long)nearPeriod * 5;
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

        public double Update(in Bar b) => Update(b, true);

        public double Update(in Bar b, bool commit)
        {
            var value = _seen >= _warmup && Matches(b) ? 100d : 0;
            if (!commit) return value;
            var range = new ExactMeanAccumulator(); range.Add(b.High); range.Add(b.Low, -1);
            if (_dojiHistory.Count == dojiPeriod) _doji.Subtract(_dojiHistory.Dequeue());
            _dojiHistory.Enqueue(range); _doji.AddExact(range);
            if (_nearHistory.Count == nearPeriod) _near.Subtract(_nearHistory.Dequeue());
            _nearHistory.Enqueue(range); _near.AddExact(range);
            if (_seen < _warmup)
                _seen++;
            return value;
        }

#if !NETFRAMEWORK
        internal void Seed(long[] doji, int dojiCount, int dojiPosition,
            long[] near, int nearCount, int nearPosition, int exponent, int seen)
        {
            Reset();
            SeedWindow(doji, dojiCount, dojiPosition, exponent, _dojiHistory, ref _doji);
            SeedWindow(near, nearCount, nearPosition, exponent, _nearHistory, ref _near);
            _seen = seen;
        }
        private static void SeedWindow(long[] values, int count, int position, int exponent,
            Queue<ExactMeanAccumulator> history, ref ExactMeanAccumulator sum)
        {
            for (var i = 0; i < count; i++)
            {
                var value = values[(count == values.Length ? position + i : i) % values.Length];
                var range = new ExactMeanAccumulator();
                range.Add(value >> 32); range.ScaleByPowerOfTwo(32);
                range.Add((uint)(value & uint.MaxValue)); range.ScaleByPowerOfTwo(exponent);
                history.Enqueue(range); sum.AddExact(range);
            }
        }
#endif

        private static void AddWeighted(ref ExactMeanAccumulator sum, double value, long weight)
        {
            if (weight >= int.MinValue && weight <= int.MaxValue) sum.Add(value, (int)weight);
            else sum.Add(value, new BigInteger(weight));
        }

        private bool Matches(in Bar b)
        {
            var top = Math.Max(b.Open, b.Close);
            var bottom = Math.Min(b.Open, b.Close);
            var doji = _doji;
            AddWeighted(ref doji, top, -_dojiDen);
            AddWeighted(ref doji, bottom, _dojiDen);
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
            AddWeighted(ref above, b.High, _nearDen);
            AddWeighted(ref above, b.Low, _nearDen);
            AddWeighted(ref above, bottom, -2 * _nearDen);
            var below = _near;
            below.ScaleByPowerOfTwo(1);
            AddWeighted(ref below, top, 2 * _nearDen);
            AddWeighted(ref below, b.High, -_nearDen);
            AddWeighted(ref below, b.Low, -_nearDen);
            return above.Sign >= 0 && below.Sign >= 0;
        }
    }
}
