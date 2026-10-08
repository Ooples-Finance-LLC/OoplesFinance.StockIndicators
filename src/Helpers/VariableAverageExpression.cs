using System.Numerics;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

// Rational expression history supports exact fallback. Outward significant-bit
// intervals normally resolve ordering and final rounding without growing fractions.
internal sealed class VariableAverageExpression
{
    internal static readonly VariableAverageExpression Zero = new((F)0);
    internal static readonly VariableAverageExpression One = new((F)1);
    private readonly char _kind;
    private readonly F _constant;
    private readonly VariableAverageExpression? _left, _right;
    private readonly Dictionary<int, Interval?> _bounds = new();
    private F? _exact;
    private VariableAverageExpression(F value) { _kind = 'c'; _constant = value; _exact = value; }
    private VariableAverageExpression(char kind, VariableAverageExpression left, VariableAverageExpression right)
    { _kind = kind; _left = left; _right = right; }
    internal static VariableAverageExpression Constant(F value)
        => value.Sign == 0 ? Zero : value.CompareTo(1) == 0 ? One : new(value);
    private bool Is(F value) => _kind == 'c' && _constant.CompareTo(value) == 0;
    private static bool Same(VariableAverageExpression a,VariableAverageExpression b)
        => ReferenceEquals(a,b) || a._kind==b._kind && (a._kind=='c'
            ? a._constant.CompareTo(b._constant)==0 : ReferenceEquals(a._left,b._left) && ReferenceEquals(a._right,b._right));
    private static bool Small(VariableAverageExpression a, VariableAverageExpression b)
        => a._kind == 'c' && b._kind == 'c'
            && Bits(a._constant.Numerator) + Bits(a._constant.Denominator)
            + Bits(b._constant.Numerator) + Bits(b._constant.Denominator) <= 384;
    public static VariableAverageExpression operator +(VariableAverageExpression a, VariableAverageExpression b)
    {
        if (a.Is(0)) return b; if (b.Is(0)) return a;
        return Small(a,b) ? Constant(a._constant + b._constant) : new('+',a,b);
    }
    public static VariableAverageExpression operator -(VariableAverageExpression a, VariableAverageExpression b)
    {
        if (Same(a,b)) return Zero; if (b.Is(0)) return a;
        return Small(a,b) ? Constant(a._constant - b._constant) : new('-',a,b);
    }
    public static VariableAverageExpression operator *(VariableAverageExpression a, VariableAverageExpression b)
    {
        if (a.Is(0) || b.Is(0)) return Zero; if (a.Is(1)) return b; if (b.Is(1)) return a;
        return Small(a,b) ? Constant(a._constant * b._constant) : new('*',a,b);
    }
    public static VariableAverageExpression operator /(VariableAverageExpression a, VariableAverageExpression b)
    {
        if (b.Is(0)) throw new DivideByZeroException();
        if (a.Is(0)) return Zero; if (b.Is(1)) return a; if (Same(a,b)) return One;
        return Small(a,b) ? Constant(a._constant / b._constant) : new('/',a,b);
    }
    private static int Bits(BigInteger value)
    {
        var bytes=BigInteger.Abs(value).ToByteArray(); var last=bytes.Length-1;
        while (last>0 && bytes[last]==0) last--;
        var bits=last*8; for (var b=bytes[last]; b!=0; b>>=1) bits++;
        return bits;
    }
    private readonly struct Interval
    {
        internal readonly F Lower, Upper;
        internal Interval(F lower,F upper) { Lower=lower; Upper=upper; }
        internal static F Round(F value,int bits,bool upper)
        {
            if (value.Sign==0) return value;
            var exponent=Bits(value.Numerator)-Bits(value.Denominator)-bits;
            var grid=exponent>=0 ? new F(BigInteger.One<<exponent,BigInteger.One) : new F(BigInteger.One,BigInteger.One<<-exponent);
            var scaled=value/grid;
            return new F(upper ? scaled.Ceiling() : scaled.Floor(),BigInteger.One)*grid;
        }
        internal static Interval Bound(F lower,F upper,int bits) => new(Round(lower,bits,false),Round(upper,bits,true));
        internal static Interval? Apply(char kind,Interval a,Interval b,int bits)
        {
            if (kind=='+') return Bound(a.Lower+b.Lower,a.Upper+b.Upper,bits);
            if (kind=='-') return Bound(a.Lower-b.Upper,a.Upper-b.Lower,bits);
            if (kind=='/' && b.Lower.Sign<=0 && b.Upper.Sign>=0) return null;
            var candidates=kind=='*'
                ? new[] { a.Lower*b.Lower,a.Lower*b.Upper,a.Upper*b.Lower,a.Upper*b.Upper }
                : new[] { a.Lower/b.Lower,a.Lower/b.Upper,a.Upper/b.Lower,a.Upper/b.Upper };
            var low=candidates[0]; var high=candidates[0];
            foreach (var c in candidates) { if (c<low) low=c; if (c>high) high=c; }
            return Bound(low,high,bits);
        }
    }
    private bool TryBounds(int bits,out Interval? bounds)
    {
        // Shared zero/one constants remain immutable across independent engines.
        if (_kind=='c') { bounds=new Interval(_constant,_constant); return true; }
        return _bounds.TryGetValue(bits,out bounds);
    }
    private Interval? Evaluate(int bits)
    {
        // Iterative traversal avoids a call-stack limit on retained histories.
        var pending=new Stack<VariableAverageExpression>(); pending.Push(this);
        while (pending.Count>0)
        {
            var node=pending.Peek();
            if (node.TryBounds(bits,out _)) { pending.Pop(); continue; }
            if (!node._left!.TryBounds(bits,out var a)) { pending.Push(node._left); continue; }
            if (!node._right!.TryBounds(bits,out var b)) { pending.Push(node._right); continue; }
            node._bounds.Add(bits,a.HasValue && b.HasValue ? Interval.Apply(node._kind,a.Value,b.Value,bits) : null);
            pending.Pop();
        }
        TryBounds(bits,out var result); return result;
    }
    private F Exact()
    {
        var pending=new Stack<VariableAverageExpression>(); pending.Push(this);
        while (pending.Count>0)
        {
            var node=pending.Peek();
            if (node._exact.HasValue) { pending.Pop(); continue; }
            if (!node._left!._exact.HasValue) { pending.Push(node._left); continue; }
            if (!node._right!._exact.HasValue) { pending.Push(node._right); continue; }
            var a=node._left._exact.Value; var b=node._right._exact.Value;
            node._exact=node._kind switch { '+'=>a+b,'-'=>a-b,'*'=>a*b,_=>a/b };
            pending.Pop();
        }
        return _exact!.Value;
    }
    internal int CompareTo(VariableAverageExpression other)
    {
        if (Same(this,other)) return 0;
        if (_kind=='c' && other._kind=='c') return _constant.CompareTo(other._constant);
        var difference=this-other;
        for (var bits=128; bits<=4096; bits*=2)
        {
            var bounds=difference.Evaluate(bits); if (!bounds.HasValue) continue;
            if (bounds.Value.Lower.Sign>0) return 1;
            if (bounds.Value.Upper.Sign<0) return -1;
            if (bounds.Value.Lower.Sign==0 && bounds.Value.Upper.Sign==0) return 0;
        }
        return difference.Exact().Sign;
    }
    internal VariableAverageExpression Abs()
    {
        var sign=CompareTo(Zero);
        return sign==0 ? Zero : sign<0 ? Zero-this : this;
    }
    internal double Publish()
    {
        if (_kind=='c') return _constant.Publish();
        for (var bits=128; bits<=4096; bits*=2)
        {
            var bounds=Evaluate(bits); if (!bounds.HasValue) continue;
            var lower=bounds.Value.Lower.Publish(); var upper=bounds.Value.Upper.Publish();
            if (lower.Equals(upper)) return lower; // NOSONAR: S1244 - Certification requires both interval endpoints to round to exactly the same binary64 value.
        }
        return Exact().Publish();
    }
}
