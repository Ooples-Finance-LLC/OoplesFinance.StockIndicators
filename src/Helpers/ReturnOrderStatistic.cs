using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Rank exact one-bar returns without materializing their possibly overflowing quotient.
// (a/b - 1) < (c/d - 1) iff a*d < c*b once denominators are positive.
internal sealed class ReturnOrderStatistic : IDisposable
{
    private readonly int _length;
    private readonly Queue<ReturnKey> _window = new();
    private readonly Random _random = new(244);
    private Node? _root;

    internal ReturnOrderStatistic(int length) => _length = Math.Max(1, length);

    internal int CountLessThan(double current, double previous)
    {
        var key = new ReturnKey(current, previous);
        var node = _root; var count = 0;
        while (node is not null)
        {
            if (key.CompareTo(node.Key) <= 0) node = node.Left;
            else { count += Size(node.Left) + node.Count; node = node.Right; }
        }
        return count;
    }

    internal void Add(double current, double previous)
    {
        var key = new ReturnKey(current, previous);
        if (_window.Count == _length) _root = Remove(_root!, _window.Dequeue());
        _window.Enqueue(key);
        _root = Insert(_root, key);
    }

    public void Dispose() { _window.Clear(); _root = null; }

    internal readonly struct ReturnKey : IComparable<ReturnKey>
    {
#if NETFRAMEWORK
        private readonly double _current, _previous;
#else
        private readonly ExactMeanAccumulator.ProductFactor _current, _previous;
#endif
        internal ReturnKey(double current, double previous)
        {
            if (double.IsNaN(current) || double.IsInfinity(current) || double.IsNaN(previous) || double.IsInfinity(previous))
                throw new ArgumentOutOfRangeException(nameof(current), "Return ranking requires finite prices.");
            // A zero previous price explicitly represents zero return, as in the public formula.
            double numerator = previous == 0 ? 1 : previous < 0 ? -current : current;
            double denominator = previous == 0 ? 1 : Math.Abs(previous);
#if NETFRAMEWORK
            _current = numerator; _previous = denominator;
#else
            _current = new(numerator); _previous = new(denominator);
#endif
        }
        public int CompareTo(ReturnKey other)
        {
#if !NETFRAMEWORK
            int sign = Math.Sign(_current.Mantissa), otherSign = Math.Sign(other._current.Mantissa);
            if (sign != otherSign) return sign.CompareTo(otherSign);
            if (sign == 0) return 0;
            UInt128 left = (UInt128)(ulong)Math.Abs(_current.Mantissa) * (ulong)other._previous.Mantissa;
            UInt128 right = (UInt128)(ulong)Math.Abs(other._current.Mantissa) * (ulong)_previous.Mantissa;
            int leftScale = _current.Scale + other._previous.Scale;
            int rightScale = other._current.Scale + _previous.Scale;
            int topOrder = (Bits(left) + leftScale).CompareTo(Bits(right) + rightScale);
            if (topOrder != 0) return sign * topOrder;
            // Each exact product has at most 106 bits. Equal top exponents
            // imply that aligning the smaller product still needs at most
            // 106 bits, even when the original price exponents are extreme.
            if (leftScale > rightScale) left <<= leftScale - rightScale;
            else right <<= rightScale - leftScale;
            return sign * left.CompareTo(right);
#else
            var difference = JurikDyadic.FromDouble(_current);
            difference.Multiply(other._previous);
            var right = JurikDyadic.FromDouble(other._current);
            right.Multiply(_previous);
            difference.Subtract(right);
            return difference.Sign;
#endif
        }
#if !NETFRAMEWORK
        private static int Bits(UInt128 value)
        {
            ulong upper = (ulong)(value >> 64);
            return upper == 0 ? 64 - BitOperations.LeadingZeroCount((ulong)value)
                : 128 - BitOperations.LeadingZeroCount(upper);
        }
#endif
    }

    private Node Insert(Node? node, ReturnKey key)
    {
        if (node is null) return new Node(key, _random.Next());
        var order = key.CompareTo(node.Key);
        if (order == 0) node.Count++;
        else if (order < 0)
        {
            node.Left = Insert(node.Left, key);
            if (node.Left.Priority > node.Priority) node = RotateRight(node);
        }
        else
        {
            node.Right = Insert(node.Right, key);
            if (node.Right.Priority > node.Priority) node = RotateLeft(node);
        }
        Refresh(node); return node;
    }
    private static Node? Remove(Node node, ReturnKey key)
    {
        var order = key.CompareTo(node.Key);
        if (order < 0) node.Left = Remove(node.Left!, key);
        else if (order > 0) node.Right = Remove(node.Right!, key);
        else if (node.Count > 1) node.Count--;
        else if (node.Left is null) return node.Right;
        else if (node.Right is null) return node.Left;
        else if (node.Left.Priority > node.Right.Priority)
        {
            node = RotateRight(node); node.Right = Remove(node.Right!, key);
        }
        else
        {
            node = RotateLeft(node); node.Left = Remove(node.Left!, key);
        }
        Refresh(node); return node;
    }
    private static int Size(Node? node) => node?.Size ?? 0;
    private static void Refresh(Node node) => node.Size = node.Count + Size(node.Left) + Size(node.Right);
    private static Node RotateRight(Node node)
    {
        var root = node.Left!; node.Left = root.Right; root.Right = node;
        Refresh(node); Refresh(root); return root;
    }
    private static Node RotateLeft(Node node)
    {
        var root = node.Right!; node.Right = root.Left; root.Left = node;
        Refresh(node); Refresh(root); return root;
    }
    private sealed class Node
    {
        internal readonly ReturnKey Key;
        internal readonly int Priority;
        internal int Count = 1, Size = 1;
        internal Node? Left, Right;
        internal Node(ReturnKey key, int priority) { Key = key; Priority = priority; }
    }
}
