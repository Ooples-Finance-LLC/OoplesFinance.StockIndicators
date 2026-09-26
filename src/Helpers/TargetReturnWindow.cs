using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TargetReturnWindow : IDisposable
{
    private readonly struct Fraction
    {
        internal readonly BigInteger N;
        private readonly BigInteger _d;
        internal BigInteger D => _d.IsZero ? BigInteger.One : _d;
        internal Fraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) throw new DivideByZeroException();
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            N = numerator / gcd; _d = denominator / gcd;
        }
        public static Fraction operator +(Fraction a, Fraction b)
        {
            var common = BigInteger.GreatestCommonDivisor(a.D, b.D);
            return new Fraction(a.N * (b.D / common) + b.N * (a.D / common), a.D * (b.D / common));
        }
        public static Fraction operator -(Fraction a, Fraction b) => a + new Fraction(-b.N, b.D);
        internal Fraction Square() => new(N * N, D * D);
        internal Fraction Times(int n) => new(N * n, D);
    }
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<(Fraction Up, Fraction Down)> _returns;
    private readonly Fraction _benchmark;
    private readonly Fraction? _beta;
    private readonly bool _potential;
    private readonly (Fraction Up, Fraction Down) _padding;
    private Fraction _up, _down;

    internal TargetReturnWindow(int length, double benchmark, bool potential, double? beta = null)
    {
        length = Math.Max(1, length);
        if (!double.IsFinite(benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark));
        var target = Math.Pow(1 + benchmark, length / 360d) - 1;
        if (!double.IsFinite(target)) throw new ArgumentOutOfRangeException(nameof(benchmark), "The period benchmark must be finite and real.");
        _benchmark = new Fraction(ExactVarianceWindow.Units(target), BigInteger.One << 1074);
        if (beta.HasValue)
        {
            if (!double.IsFinite(beta.Value)) throw new ArgumentOutOfRangeException(nameof(beta));
            _beta = new Fraction(ExactVarianceWindow.Units(beta.Value), BigInteger.One << 1074);
        }
        _potential = potential; _prices = new(length); _returns = new(length);
        _padding = Contributions(default); Reset();
    }
    private (Fraction Up, Fraction Down) Contributions(Fraction value)
    {
        if (_beta.HasValue) return (value, default);
        var difference = value - _benchmark;
        return difference.N.Sign > 0 ? (difference, default) :
            (default, _potential ? difference.Square() : new Fraction(-difference.N, difference.D));
    }
    internal double Next(double price, bool commit)
    {
        var previous = _prices.Count == _prices.Capacity ? _prices[0] : 0;
        var relative = previous == 0 ? default : new Fraction(ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(previous), ExactVarianceWindow.Units(previous));
        var current = Contributions(relative);
        var expired = _returns.Count == _returns.Capacity ? _returns[0] : _padding;
        var up = _up + current.Up - expired.Up; var down = _down + current.Down - expired.Down;
        var result = down.N.IsZero ? 0 : _potential
            ? ExactPopulationDeviation.RootRatio((up.N * up.N * down.D) << 2148, up.D * up.D * _prices.Capacity * down.N)
            : ExactMeanAccumulator.UnitRatio((up.N * down.D) << 1074, up.D * down.N);
        if (_beta is { } beta)
        {
            var count = Math.Min(_returns.Count + 1, _returns.Capacity);
            var excess = new Fraction(up.N, up.D * count) - _benchmark;
            result = beta.N.IsZero ? 0 : ExactMeanAccumulator.UnitRatio((excess.N * beta.D * beta.N.Sign) << 1074, excess.D * BigInteger.Abs(beta.N));
        }
        if (commit) { _prices.TryAdd(price, out _); _returns.TryAdd(current, out _); _up = up; _down = down; }
        return result;
    }
    internal void Reset()
    { _prices.Clear(); _returns.Clear(); _up = _padding.Up.Times(_prices.Capacity); _down = _padding.Down.Times(_prices.Capacity); }
    public void Dispose() { _prices.Dispose(); _returns.Dispose(); }
}
