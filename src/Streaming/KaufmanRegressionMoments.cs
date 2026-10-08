using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Price-adaptive, consistently weighted centered regression moments.</summary>
internal sealed class KaufmanRegressionMoments : IDisposable
{
    private readonly int _length;
    private readonly Queue<double> _prices = new();
    private readonly Queue<BigInteger> _changes = new();
    private BigInteger _travel;
    private double _previous;
    private bool _hasPrevious;
    private bool _affine = true, _hasAffineStep;
    private BigInteger _affineStep;
    private Moments _moments;
    private Number _lastFit;

    private struct Moments
    {
        internal Number Age, Price, TimeVariance, PriceVariance, Covariance;
    }

    // Normalize two-component mantissas before arithmetic. Both exponent ends
    // remain extended: a squared subnormal must survive until its square root.
    private readonly struct Number
    {
        private readonly double _high, _low;
        private readonly int _shift;
        private Number(double high, double low, int shift) { _high = high; _low = low; _shift = shift; }
        internal bool IsZero => _high == 0 && _low == 0;
        internal static Number Of(double value) { var sum = new ExactMeanAccumulator(); sum.Add(value); return Round(sum); }
        internal void AddTo(ref ExactMeanAccumulator sum, int sign = 1)
        {
            var opposite = new ExactMeanAccumulator(); opposite.Add(_high, -sign); opposite.Add(_low, -sign);
            opposite.ScaleByPowerOfTwo(_shift); sum.Subtract(opposite);
        }
        private static Number Round(ExactMeanAccumulator sum, int shift = 0)
        {
            if (sum.IsExactlyZero) return default;
            var high = sum.Mean(1); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(high) < lower) { sum.ScaleByPowerOfTwo(512); shift = checked(shift - 512); high = sum.Mean(1); }
            while (double.IsInfinity(high) || Math.Abs(high) >= upper) { sum.ScaleByPowerOfTwo(-512); shift = checked(shift + 512); high = sum.Mean(1); }
            sum.Add(high, -1); return new(high, sum.Mean(1), shift);
        }
        internal double Publish() { var sum = new ExactMeanAccumulator(); AddTo(ref sum); return sum.Mean(1); }
        public static Number operator +(Number a, Number b) { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum); return Round(sum); }
        public static Number operator -(Number a, Number b) { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, -1); return Round(sum); }
        public static Number operator *(Number a, Number b)
        {
            var sum = new ExactMeanAccumulator(); sum.AddProduct(a._high, b._high); sum.AddProduct(a._high, b._low);
            sum.AddProduct(a._low, b._high); sum.AddProduct(a._low, b._low); return Round(sum, checked(a._shift + b._shift));
        }
        public static Number operator /(Number a, Number b)
        {
            if (b.IsZero) throw new DivideByZeroException();
            var numerator = new Number(a._high, a._low, 0); var divisor = new Number(b._high, b._low, 0);
            var quotient = Of(a._high / b._high);
            for (var pass = 0; pass < 2; pass++)
            {
                var remainder = numerator - divisor * quotient;
                quotient += Of(remainder.Publish() / b._high);
            }
            return new(quotient._high, quotient._low, checked(quotient._shift + a._shift - b._shift));
        }
        internal Number Sqrt()
        {
            if (IsZero) return default;
            var odd = _shift & 1; var scale = odd == 0 ? 1 : 2;
            var value = new Number(_high * scale, _low * scale, 0);
            var root = Of(Math.Sqrt(_high * scale));
            root += (value - root * root) / (Of(2) * root);
            return new(root._high, root._low, checked(root._shift + (_shift - odd) / 2));
        }
    }

    internal KaufmanRegressionMoments(int length) => _length = Math.Max(1, length);

    internal double Next(double price, bool isFinal, out double indexDeviation,
        out double sourceDeviation, out double correlation)
    {
        if (MathHelper.IsValueNullOrInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var units = ExactVarianceWindow.Units(price);
        var signedChange = _hasPrevious ? units - ExactVarianceWindow.Units(_previous) : BigInteger.Zero;
        var change = BigInteger.Abs(signedChange);
        var travel = _travel + change;
        if (_changes.Count == _length) travel -= _changes.Peek();
        var ready = _prices.Count == _length;
        var affine = !ready || _affine; var hasStep = ready && _hasAffineStep; var step = _affineStep;
        if (ready)
        {
            if (hasStep) affine &= signedChange == step;
            else { step = signedChange; hasStep = true; }
        }
        var displacement = ready ? BigInteger.Abs(units - ExactVarianceWindow.Units(_prices.Peek())) : BigInteger.Zero;
        var efficiency = travel.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(displacement << 1074, travel);
        var gain = ready ? Math.Pow(2d / 31 + efficiency * (2d / 3 - 2d / 31), 2) : 1;
        var one = Number.Of(1); var input = Number.Of(price); var next = _moments;
        var fit = input;
        if (gain == 1) // NOSONAR: S1244 - Unit gain discards all prior mass exactly.
            next = new Moments { Price = input };
        else
        {
            var weight = Number.Of(gain); var retained = one - weight;
            var dx = one - next.Age; var dy = input - next.Price;
            // Evaluate the fitted error against the new observation. The gain's
            // common dx*dy term cancels algebraically before rounding. In
            // particular, two observations fit the current price exactly even
            // when their magnitudes differ by hundreds of decimal places.
            var denominator = next.TimeVariance + weight * dx * dx;
            if (!denominator.IsZero)
                fit = input + retained * (dx * next.Covariance - dy * next.TimeVariance) / denominator;
            next.TimeVariance = retained * (next.TimeVariance + weight * dx * dx);
            next.PriceVariance = retained * (next.PriceVariance + weight * dy * dy);
            next.Covariance = retained * (next.Covariance + weight * dx * dy);
            next.Age = retained * (next.Age - one);
            next.Price += weight * dy;
        }
        var timeRoot = next.TimeVariance.Sqrt(); var priceRoot = next.PriceVariance.Sqrt();
        indexDeviation = timeRoot.Publish(); sourceDeviation = priceRoot.Publish();
        correlation = timeRoot.IsZero || priceRoot.IsZero ? 0
            : Math.Max(-1, Math.Min(1, (next.Covariance / (timeRoot * priceRoot)).Publish()));
        // A common positive weight measure fits an exact affine price sequence
        // without residual. Prove that invariant from integer price differences;
        // do not turn a small numerical residual into a tie using a tolerance.
        if (affine) { fit = input; correlation = ready ? step.Sign : 0; }
        _lastFit = fit;
        if (isFinal)
        {
            _moments = next; _travel = travel; _previous = price; _hasPrevious = true;
            _affine = affine; _hasAffineStep = hasStep; _affineStep = step;
            if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price);
            if (_changes.Count == _length) _changes.Dequeue(); _changes.Enqueue(change);
        }
        return _lastFit.Publish();
    }

    internal ExactMeanAccumulator FitDifference(double price)
    { var difference = new ExactMeanAccumulator(); difference.Add(price); _lastFit.AddTo(ref difference, -1); return difference; }
    internal void Reset()
    { _prices.Clear(); _changes.Clear(); _travel = default; _previous = 0; _hasPrevious = false; _moments = default; _lastFit = default; _affine = true; _hasAffineStep = false; _affineStep = default; }
    public void Dispose() => Reset();
}
