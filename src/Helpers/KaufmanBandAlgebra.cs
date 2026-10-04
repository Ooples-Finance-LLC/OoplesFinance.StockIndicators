using System.Numerics;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
using Bounds = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Bounds;

namespace OoplesFinance.StockIndicators.Helpers;

// Expressions share one nonnegative rational exponent. Ordinary evaluations use
// outward intervals; exact power classes resolve cancellation at rounding ties.
internal sealed class KaufmanBandAlgebra
{
    private readonly F _exponent;
    internal KaufmanBandAlgebra(double exponent) => _exponent = F.Of(exponent);
    internal Node Constant(F value) => new(this, 'c', value, null, null);
    internal Node Power(F value)
    {
        if (_exponent.Sign == 0 || value.CompareTo(1) == 0) return Constant(1);
        if (value.Sign == 0) return Constant(0);
        if (_exponent.Denominator.IsOne && _exponent.Numerator <= 32)
            return Constant(value.Pow((int)_exponent.Numerator));
        return new(this, 'p', value, null, null);
    }

    internal sealed class Node
    {
        private readonly KaufmanBandAlgebra _owner;
        private readonly char _kind;
        private readonly F _value;
        private readonly Node? _left, _right;
        private Dictionary<F, F>? _terms;
        private readonly Dictionary<int, Bounds> _bounds = new();
        internal Node(KaufmanBandAlgebra owner, char kind, F value, Node? left, Node? right)
        { _owner = owner; _kind = kind; _value = value; _left = left; _right = right; }
        private bool Is(F value) => _kind == 'c' && _value.CompareTo(value) == 0;
        private static bool SmallConstants(Node a, Node b)
            => a._kind == 'c' && b._kind == 'c'
                && Bits(BigInteger.Abs(a._value.Numerator)) + Bits(a._value.Denominator)
                + Bits(BigInteger.Abs(b._value.Numerator)) + Bits(b._value.Denominator) <= 384;
        public static Node operator +(Node a, Node b)
        {
            if (a.Is(0)) return b; if (b.Is(0)) return a;
            if (SmallConstants(a, b)) return a._owner.Constant(a._value + b._value);
            return new(a._owner, '+', default, a, b);
        }
        public static Node operator -(Node a, Node b)
            => ReferenceEquals(a, b) ? a._owner.Constant(0) : a + b * a._owner.Constant(-1);
        public static Node operator *(Node a, Node b)
        {
            if (a.Is(0) || b.Is(0)) return a._owner.Constant(0);
            if (a.Is(1)) return b; if (b.Is(1)) return a;
            if (SmallConstants(a, b)) return a._owner.Constant(a._value * b._value);
            return new(a._owner, '*', default, a, b);
        }
        internal Bounds Evaluate(int bits)
        {
            if (_bounds.TryGetValue(bits, out var saved)) return saved;
            Bounds result;
            if (_kind == 'c') result = new(_value, _value);
            else if (_kind == 'p') result = UltimatePowerWeights.Power(_value, _owner._exponent, bits);
            else
            {
                var a = _left!.Evaluate(bits); var b = _right!.Evaluate(bits);
                if (_kind == '+') result = new(a.Lower + b.Lower, a.Upper + b.Upper);
                else
                {
                    var products = new[] { a.Lower * b.Lower, a.Lower * b.Upper, a.Upper * b.Lower, a.Upper * b.Upper };
                    var lower = products[0]; var upper = products[0];
                    foreach (var product in products)
                    { if (product < lower) lower = product; if (product > upper) upper = product; }
                    result = new(lower, upper);
                }
                result = result.Round(bits);
            }
            _bounds.Add(bits, result); return result;
        }

        private Dictionary<F, F> Terms()
        {
            if (_terms is not null) return _terms;
            var terms = new Dictionary<F, F>();
            if (_kind == 'c') _owner.Add(terms, 1, _value);
            else if (_kind == 'p') _owner.Add(terms, _value, 1);
            else if (_kind == '+')
            {
                foreach (var term in _left!.Terms()) _owner.Add(terms, term.Key, term.Value);
                foreach (var term in _right!.Terms()) _owner.Add(terms, term.Key, term.Value);
            }
            else
                foreach (var a in _left!.Terms())
                foreach (var b in _right!.Terms()) _owner.Add(terms, a.Key * b.Key, a.Value * b.Value);
            return _terms = terms;
        }
        internal int Sign()
        {
            if (_kind == 'c') return _value.Sign;
            for (var bits = 96; ; bits = checked(bits * 2))
            {
                var bounds = Evaluate(bits);
                if (bounds.Lower.Sign > 0) return 1;
                if (bounds.Upper.Sign < 0) return -1;
                if (bounds.Lower.Sign == 0 && bounds.Upper.Sign == 0) return 0;
                // Very large exponents usually certify underflow before exact
                // normalization would need their enormous integer powers.
                if (bits >= (_owner._exponent.Numerator <= 4096 ? 192 : 6144))
                {
                    var terms = Terms();
                    if (terms.Count == 0) return 0;
                    if (terms.Count == 1) return terms.First().Value.Sign;
                }
            }
        }
        internal double Publish() => Round(Evaluate, target => (this - _owner.Constant(target)).Sign());
        internal double Band(Node variance, int direction)
        {
            Bounds EvaluateBand(int bits)
            {
                var center = Evaluate(bits); var square = variance.Evaluate(bits);
                var lowerRoot = UltimatePowerWeights.Root(square.Lower.Sign < 0 ? 0 : square.Lower, bits).Lower;
                var upperRoot = UltimatePowerWeights.Root(square.Upper.Sign < 0 ? 0 : square.Upper, bits).Upper;
                return direction > 0 ? new(center.Lower + lowerRoot, center.Upper + upperRoot)
                    : new(center.Lower - upperRoot, center.Upper - lowerRoot);
            }
            int Compare(F target)
            {
                var distance = direction > 0 ? _owner.Constant(target) - this : this - _owner.Constant(target);
                if (distance.Sign() < 0) return direction;
                return direction * (variance - distance * distance).Sign();
            }
            return Round(EvaluateBand, Compare);
        }
    }

    private static int Bits(BigInteger value)
    {
        var bytes = value.ToByteArray(); var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        var result = last * 8;
        for (var top = bytes[last]; top != 0; top >>= 1) result++;
        return result;
    }
    private static bool TryRoot(BigInteger value, BigInteger degree, out BigInteger root)
    {
        root = 1; if (value.IsOne) return true;
        if (degree > Bits(value)) return false;
        var n = (int)degree;
        if (n == 1) { root = value; return true; }
        if (n == 2) { root = ExactPopulationDeviation.IntegerRoot(value); return root * root == value; }
        BigInteger lower = 1, upper = BigInteger.One << ((Bits(value) + n - 1) / n);
        while (upper - lower > 1)
        { var middle = (lower + upper) / 2; if (BigInteger.Pow(middle, n) <= value) lower = middle; else upper = middle; }
        root = BigInteger.Pow(upper, n) == value ? upper : lower;
        return BigInteger.Pow(root, n) == value;
    }
    private static F PowerInteger(F value, BigInteger power)
    {
        F result = 1;
        while (power.Sign > 0)
        { if (!power.IsEven) result *= value; power >>= 1; if (power.Sign > 0) value *= value; }
        return result;
    }
    private bool RationalRatio(F ratio, out F value)
    {
        value = default;
        if (!TryRoot(ratio.Numerator, _exponent.Denominator, out var numerator)
            || !TryRoot(ratio.Denominator, _exponent.Denominator, out var denominator)) return false;
        value = PowerInteger(new(numerator, denominator), _exponent.Numerator); return true;
    }
    private void Add(Dictionary<F, F> terms, F basis, F coefficient)
    {
        if (coefficient.Sign == 0) return;
        if (RationalRatio(basis, out var rational)) { basis = 1; coefficient *= rational; }
        foreach (var term in terms)
        {
            if (!RationalRatio(basis / term.Key, out var ratio)) continue;
            var sum = term.Value + coefficient * ratio;
            if (sum.Sign == 0) terms.Remove(term.Key); else terms[term.Key] = sum;
            return;
        }
        terms.Add(basis, coefficient);
    }
    private static double Round(Func<int, Bounds> evaluate, Func<F, int> compare)
    {
        for (var bits = 96; ; bits = checked(bits * 2))
        {
            var bounds = evaluate(bits); var lower = bounds.Lower.Publish(); var upper = bounds.Upper.Publish();
#pragma warning disable S1244 // Exact equality of rounded bounds certifies the binary64 result.
            if (lower == upper) return lower;
            var next = lower == 0 ? double.Epsilon : BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(lower) + (lower < 0 ? -1 : 1));
            if (next != upper) continue;
#pragma warning restore S1244
            var a = double.IsNegativeInfinity(lower) ? new F(-(BigInteger.One << 1024), 1) : F.Of(lower);
            var b = double.IsPositiveInfinity(upper) ? new F(BigInteger.One << 1024, 1) : F.Of(upper);
            var sign = compare((a + b) / 2);
            return sign < 0 || sign == 0 && (BitConverter.DoubleToInt64Bits(lower) & 1) == 0 ? lower : upper;
        }
    }
}
