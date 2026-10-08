using OoplesFinance.StockIndicators.Streaming;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
using T = OoplesFinance.StockIndicators.Helpers.ConfluenceTrigonometry;
namespace OoplesFinance.StockIndicators.Helpers;

// Rational projections and algebraic trigonometric comparisons. Periods are
// widened before arithmetic; storage grows only as observations arrive.
internal sealed class ConfluenceWindow
{
    private sealed class Mean(MovingAvgType kind, long period)
    {
        private readonly Queue<F> _values = new();
        private F _sum, _weighted, _previous;
        private long _count;
        internal F Next(F value, bool final)
        {
            var sum = _sum; var weighted = _weighted; F result;
            var window = kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted += period * value - sum;
                if (_values.Count == period) sum -= _values.Peek();
                sum += value;
                result = kind == MovingAvgType.WeightedMovingAverage ? weighted / ((F)period * (period + 1) / 2)
                    : _count + 1 < period ? (F)0 : sum / period;
            }
            else if (kind == MovingAvgType.ExponentialMovingAverage && _count < period)
            { sum += value; result = sum / (_count + 1); }
            else
            {
                var ema = kind == MovingAvgType.ExponentialMovingAverage;
                result = (_previous * (period - 1) + value * (ema ? 2 : 1)) / (ema ? period + 1 : period);
            }
            if (final)
            {
                if (window) { if (_values.Count == period) _values.Dequeue(); _values.Enqueue(value); }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _values.Clear(); _sum = _weighted = _previous = default; _count = 0; }
    }
    private sealed class History<V>(long width, V zero)
    {
        private readonly Queue<V> _values = new();
        internal V Delayed(V current) => width == 0 ? current : _values.Count == width ? _values.Peek() : zero;
        internal void Add(V value) { if (width == 0) return; if (_values.Count == width) _values.Dequeue(); _values.Enqueue(value); }
        internal void Clear() => _values.Clear();
    }
    private readonly long[] _periods, _delays;
    private readonly long _short;
    private readonly MovingAvgType _kind;
    private readonly Mean[] _means;
    private readonly History<F>[] _delayedMeans, _delayedProjections;
    private readonly History<F> _phaseMean;
    private readonly History<T> _phaseWave;
    private readonly Queue<T> _errors = new();
    private readonly Queue<F> _spreads = new();
    private readonly HashSet<long> _factors;
    private F[] _previousMeans = new F[4];
    private F _previousMomentum, _previousSpread, _spreadSum;
    private T _previousError = T.Zero, _errorSum = T.Zero;
    private long _count;
    private double _previousOutput, _previousPreviousOutput;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.SimpleMovingAverage
        or MovingAvgType.WeightedMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
    internal ConfluenceWindow(MovingAvgType kind, int length)
    {
        _kind = kind; var n = Math.Max(1, length);
        _periods = new long[] { n, 2L * n - 1, 4L * n - 3, 8L * n - 7 };
        _delays = _periods.Take(3).Select(p => p / 2).ToArray(); _short = Math.Max(2, Math.Min(530, n - 1));
        var lengths = _periods.Concat(new[] { _short, Math.Max(1, _periods[3] - 1), Math.Max(1, _periods[3] - 1) }).ToArray();
        _means = lengths.Select(p => new Mean(kind, p)).ToArray();
        _factors = new(ConfluenceRootRelations.PrimeFactors(lengths.Concat(lengths.Select(p => p + 1))));
        _delayedMeans = _delays.Select(p => new History<F>(p, default)).ToArray();
        _delayedProjections = _delays.Select(p => new History<F>(p, default)).ToArray();
        _phaseMean = new(_delays[1], default); _phaseWave = new(_delays[1], T.Zero);
    }
    private static int Vote(int sign, int motion, int position) => sign == 0 || motion == 0 || position == 0
        ? 0 : sign * (1 + (motion == sign ? 1 : 0) + (position == sign ? 1 : 0));
    internal (double Value, Signal Trade) Next(F close, F fullPrice, bool final)
    {
        // EMA startup introduces observed-count denominators, independently of periods.
        var factors = _factors;
        if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _periods[3])
        { factors = new(_factors); factors.UnionWith(ConfluenceRootRelations.PrimeFactors(new[] { _count + 1 })); }
        var primes = factors.OrderByDescending(p => p).ToArray();
        var means = _means.Select((m, i) => m.Next(i == 6 ? fullPrice : close, false)).ToArray();
        var projections = Enumerable.Range(0, 4).Select(i => 2 * means[i] - _previousMeans[i]).ToArray();
        projections[0] += ((F)(_periods[0] - 1 - _short) / _periods[0]) * means[4];
        projections[3] += ((F)(_periods[3] - 1) / _periods[3]) * (means[6] - means[5]);
        F momentum = 0, benchmark = 0; var wave = T.Zero; var baseline = T.Zero;
        for (var i = 0; i < 3; i++)
        {
            momentum += projections[i + 1] - _delayedProjections[i].Delayed(projections[i]);
            benchmark += means[i + 1] - _delayedMeans[i].Delayed(means[i]);
            wave += T.Wave(projections[i], 1, 1); baseline += T.Wave(means[i], 1, 1);
        }
        var phase = (wave - _phaseWave.Delayed(wave)).Sign(primes) * (means[0] - _phaseMean.Delayed(means[0])).Sign < 0 ? -1 : 1;
        var error = (wave - baseline).Times(phase);
        var errorSum = _errorSum + error;
        if (_errors.Count == _delays[1] && _errors.Count > 0) errorSum -= _errors.Peek();
        var errorSignal = _delays[1] == 0 ? T.Zero : errorSum.Times((F)1 / Math.Min(_count + 1, _delays[1]));
        var spread = projections[0] - projections[3];
        var spreadSum = _spreadSum + spread;
        if (_spreads.Count == _periods[0]) spreadSum -= _spreads.Peek();
        var spreadSignal = spreadSum / Math.Min(_count + 1, _periods[0]);
        var total = Vote(error.Sign(primes), (error - _previousError).Sign(primes), (error - errorSignal).Sign(primes))
            + Vote(momentum.Sign, (momentum - _previousMomentum).Sign, (momentum - benchmark).Sign)
            + Vote(spread.Sign, (spread - _previousSpread).Sign, (spread - spreadSignal).Sign);
        var output = spread.Sign == 0 ? 0 : Math.Sign(total) == spread.Sign ? total : total / 10d;
        var change = F.Of(output) - F.Of(_previousOutput);
        var acceleration = change - (F.Of(_previousOutput) - F.Of(_previousPreviousOutput));
        var trade = change.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : change.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : change.Sign > 0 ? Signal.Buy : change.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            for (var i = 0; i < _means.Length; i++) _means[i].Next(i == 6 ? fullPrice : close, true);
            for (var i = 0; i < 3; i++) { _delayedMeans[i].Add(means[i]); _delayedProjections[i].Add(projections[i]); }
            _phaseMean.Add(means[0]); _phaseWave.Add(wave);
            if (_delays[1] > 0) { if (_errors.Count == _delays[1]) _errors.Dequeue(); _errors.Enqueue(error); _errorSum = errorSum; }
            if (_spreads.Count == _periods[0]) _spreads.Dequeue(); _spreads.Enqueue(spread); _spreadSum = spreadSum;
            _previousMeans = means.Take(4).ToArray(); _previousMomentum = momentum; _previousSpread = spread; _previousError = error;
            _previousPreviousOutput = _previousOutput; _previousOutput = output; _count++; _factors.UnionWith(factors);
        }
        return (output, trade);
    }
    internal void Reset()
    {
        foreach (var mean in _means) mean.Reset();
        foreach (var history in _delayedMeans.Concat(_delayedProjections)) history.Clear();
        _phaseMean.Clear(); _phaseWave.Clear(); _errors.Clear(); _spreads.Clear(); _previousMeans = new F[4];
        _previousMomentum = _previousSpread = _spreadSum = default; _previousError = _errorSum = T.Zero;
        _count = 0; _previousOutput = _previousPreviousOutput = 0;
    }
    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int length)
    {
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var selected = data.ChainedValues.Count > 0;
        var state = new ConfluenceWindow(kind, length); var values = new double[data.Count]; var signals = new Signal[data.Count];
        for (var i = 0; i < values.Length; i++)
        {
            var close = F.Of(selected ? data.ChainedValues[i] : data.ClosePrices[i]);
            var full = selected ? close : (F.Of(data.OpenPrices[i]) + F.Of(data.HighPrices[i]) + F.Of(data.LowPrices[i]) + close) / 4;
            var next = state.Next(close, full, true); values[i] = next.Value; signals[i] = next.Trade;
        }
        return (values, signals);
    }
}
