using System.Collections.Concurrent;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

// Every enclosure contains the original exact recurrence. Powers of the signed
// transition matrix bound block errors; iterating |A| would lose pole stability.
internal sealed class CertifiedMultiDepth
{
    private const int Precision = 160;
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78)).Round(1);
    private static readonly Number One = Number.Integer(1);
    private static readonly ConcurrentDictionary<int, Lazy<Table[]?>> Tables = new();
    private readonly State[] _states;
    private Ball _previousMargin;

    internal readonly struct Seed
    {
        internal readonly Number Gain;
        internal readonly Number[] Feedback, Values;
        internal Seed(Number gain, Number[] feedback, Number[] values)
        { Gain = gain; Feedback = feedback; Values = values; }
    }
    internal static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
    private static Number Max(Number a, Number b) => (a - b).Sign >= 0 ? a : b;
    // Unbounded-exponent rounding never underflows. Its relative error is at
    // most 2^-80; padding by 2^-78 therefore rounds a nonnegative bound upward.
    private static Number Upper(Number value)
    { var rounded = value.Round(80); return rounded + rounded * Padding; }

    private readonly struct Ball
    {
        internal readonly Number Center, Radius;
        internal Ball(Number center, Number radius) { Center = center; Radius = radius; }
        internal static Ball Exact(Number value)
        { var center = value.Round(Precision); return new(center, Upper(Abs(value - center))); }
        internal Number Magnitude => Upper(Abs(Center) + Radius);
        public static Ball operator +(Ball a, Ball b)
        {
            var exact = a.Center + b.Center; var center = exact.Round(Precision);
            return new(center, Upper(a.Radius + b.Radius + Abs(exact - center)));
        }
        public static Ball operator *(Ball a, Ball b)
        {
            if (a.Center.Sign == 0 && a.Radius.Sign == 0 || b.Center.Sign == 0 && b.Radius.Sign == 0) return default;
            var exact = a.Center * b.Center; var center = exact.Round(Precision);
            return new(center, Upper(Abs(a.Center) * b.Radius + Abs(b.Center) * a.Radius
                + a.Radius * b.Radius + Abs(exact - center)));
        }
        internal bool TryPublish(out double value)
        {
            value = (Center - Radius).Publish(); var upper = (Center + Radius).Publish();
            return BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(upper);
        }
        internal bool TrySign(out int sign)
        {
            sign = Center.Sign;
            return Radius.Sign == 0 || (Abs(Center) - Radius).Sign > 0;
        }
    }

    private sealed class Table
    {
        internal readonly Number Gain;
        internal readonly Number[] Feedback;
        internal readonly Number[][] Powers, Prefix;
        internal readonly int Depth, Block;
        internal bool Contracts { get; }
        internal Table(Seed seed, int block)
        {
            Gain = seed.Gain; Feedback = (Number[])seed.Feedback.Clone(); Depth = Feedback.Length; Block = block;
            var n = 2 * Depth;
            var matrix = new Ball[n * n]; var powers = new Ball[block + 1][];
            powers[0] = new Ball[n * n];
            for (var i = 0; i < n; i++) powers[0][i * n + i] = Ball.Exact(One);
            for (var i = 0; i < Depth; i++)
            {
                matrix[i] = Ball.Exact(Feedback[i]);
                matrix[Depth * n + i] = Ball.Exact(default(Number) - Gain * Feedback[i]);
                matrix[Depth * n + Depth + i] = Ball.Exact(Feedback[i]);
            }
            for (var i = 1; i < Depth; i++)
            { matrix[i * n + i - 1] = Ball.Exact(One); matrix[(Depth + i) * n + Depth + i - 1] = Ball.Exact(One); }
            powers[1] = matrix;
            // Balanced products accumulate interval error over log(k) levels,
            // instead of applying the unstable absolute matrix k times.
            for (var k = 2; k <= block; k++)
            {
                var left = powers[k / 2]; var right = powers[k - k / 2]; var product = new Ball[n * n];
                for (var row = 0; row < n; row++) for (var col = 0; col < n; col++)
                    for (var j = 0; j < n; j++) product[row * n + col] += left[row * n + j] * right[j * n + col];
                powers[k] = product;
            }
            Contracts = true;
            for (var row = 0; row < n; row++)
            {
                Number sum = default;
                for (var col = 0; col < n; col++) sum += powers[block][row * n + col].Magnitude;
                if ((sum - One.Divide(2)).Sign >= 0) Contracts = false;
            }
            Powers = new Number[block + 1][]; Prefix = new Number[block + 1][];
            Prefix[0] = new Number[4];
            for (var k = 0; k <= block; k++)
            {
                Powers[k] = new Number[2 * n];
                for (var row = 0; row < 2; row++) for (var col = 0; col < n; col++)
                    Powers[k][row * n + col] = powers[k][row * Depth * n + col].Magnitude;
                if (k == 0) continue;
                Prefix[k] = new Number[4];
                for (var row = 0; row < 2; row++) for (var col = 0; col < 2; col++)
                    Prefix[k][row * 2 + col] = Upper(Prefix[k - 1][row * 2 + col]
                        + powers[k - 1][row * Depth * n + col * Depth].Magnitude);
            }
        }
    }

    private readonly struct Proposal
    {
        internal readonly Number Alpha, Beta, AlphaRadius, BetaRadius, AlphaError, BetaError;
        internal readonly Ball Output;
        internal Proposal(Number alpha, Number beta, Number alphaRadius, Number betaRadius,
            Number alphaError, Number betaError, int depth)
        {
            Alpha = alpha; Beta = beta; AlphaRadius = alphaRadius; BetaRadius = betaRadius;
            AlphaError = alphaError; BetaError = betaError;
            Output = new(alpha + beta.Divide(depth), Upper(alphaRadius + betaRadius.Divide(depth)));
        }
    }
    private sealed class State
    {
        private readonly Table _table;
        private readonly Number[] _values, _radii, _initialRadii;
        private Number _alphaError, _betaError;
        private int _step;
        internal State(Table table, Number[] values)
        {
            _table = table; _values = new Number[values.Length]; _radii = new Number[values.Length];
            _initialRadii = new Number[values.Length];
            for (var i = 0; i < values.Length; i++)
            { var ball = Ball.Exact(values[i]); _values[i] = ball.Center; _radii[i] = _initialRadii[i] = ball.Radius; }
        }
        internal Proposal Evaluate(Number price)
        {
            var d = _table.Depth; var alpha = _table.Gain * price;
            for (var i = 0; i < d; i++) alpha += _table.Feedback[i] * _values[i];
            var beta = _table.Gain * (price - alpha);
            for (var i = 0; i < d; i++) beta += _table.Feedback[i] * _values[d + i];
            var roundedAlpha = alpha.Round(Precision); var roundedBeta = beta.Round(Precision);
            var alphaError = Max(_alphaError, Abs(alpha - roundedAlpha));
            var betaError = Max(_betaError, Abs(beta - roundedBeta));
            var bounds = _table.Powers[_step + 1]; var prefix = _table.Prefix[_step + 1];
            Number alphaRadius = default, betaRadius = default;
            for (var i = 0; i < _values.Length; i++)
            { alphaRadius += bounds[i] * _initialRadii[i]; betaRadius += bounds[_values.Length + i] * _initialRadii[i]; }
            alphaRadius = Upper(alphaRadius + prefix[0] * alphaError + prefix[1] * betaError);
            betaRadius = Upper(betaRadius + prefix[2] * alphaError + prefix[3] * betaError);
            return new(roundedAlpha, roundedBeta, alphaRadius, betaRadius, alphaError, betaError, d);
        }
        internal void Commit(Proposal proposal)
        {
            var d = _table.Depth;
            for (var i = d - 1; i > 0; i--)
            {
                _values[i] = _values[i - 1]; _values[d + i] = _values[d + i - 1];
                _radii[i] = _radii[i - 1]; _radii[d + i] = _radii[d + i - 1];
            }
            _values[0] = proposal.Alpha; _values[d] = proposal.Beta;
            _radii[0] = proposal.AlphaRadius; _radii[d] = proposal.BetaRadius;
            _alphaError = proposal.AlphaError; _betaError = proposal.BetaError;
            if (++_step == _table.Block)
            { Array.Copy(_radii, _initialRadii, _radii.Length); _step = 0; _alphaError = _betaError = default; }
        }
    }

    private CertifiedMultiDepth(Table[] tables, Seed[] seeds, Number previousMargin)
    {
        _states = new State[3];
        for (var i = 0; i < 3; i++) _states[i] = new(tables[i], seeds[i].Values);
        _previousMargin = Ball.Exact(previousMargin);
    }
    internal static CertifiedMultiDepth? Create(int length, Seed[] seeds, Number previousMargin)
    {
        // A bounded cache and modest, well-conditioned periods. Other periods
        // retain the existing exact implementation without allocating tables.
        if (length < 2 || length > 64) return null;
        var tables = Tables.GetOrAdd(length, _ => new Lazy<Table[]?>(() =>
        {
            var result = new Table[3];
            for (var i = 0; i < 3; i++)
            {
                var unity = seeds[i].Gain;
                foreach (var feedback in seeds[i].Feedback) unity += feedback;
                if ((unity - One).Sign != 0) return null;
                result[i] = new(seeds[i], 8 * length);
                if (!result[i].Contracts) return null;
            }
            return result;
        })).Value;
        return tables is null ? null : new(tables, seeds, previousMargin);
    }
    internal bool TryNext(double price, bool final, out (double One, double Two, double Three, Signal Trade) result)
    {
        var current = Number.Of(price).Round(53);
        var first = _states[0].Evaluate(current); var second = _states[1].Evaluate(current); var third = _states[2].Evaluate(current);
        var margin = new Ball(current - second.Output.Center, second.Output.Radius);
        var change = new Ball(margin.Center - _previousMargin.Center, Upper(margin.Radius + _previousMargin.Radius));
        result = default;
        if (!first.Output.TryPublish(out var one) || !second.Output.TryPublish(out var two) || !third.Output.TryPublish(out var three)
            || !margin.TrySign(out var direction) || !change.TrySign(out var movement)) return false;
        var trade = direction > 0 && movement > 0 ? Signal.StrongBuy : direction < 0 && movement < 0 ? Signal.StrongSell
            : direction > 0 ? Signal.Buy : direction < 0 ? Signal.Sell : Signal.None;
        if (final)
        { _states[0].Commit(first); _states[1].Commit(second); _states[2].Commit(third); _previousMargin = margin; }
        result = (one, two, three, trade); return true;
    }
}
