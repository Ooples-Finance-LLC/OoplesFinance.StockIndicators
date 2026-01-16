//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright (c) Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using System.Buffers;

namespace OoplesFinance.StockIndicators.Helpers;

internal static class RollingWindowSettings
{
    internal const int SmallWindowThreshold = 32;
}

internal sealed class RollingSum
{
    private readonly List<double> _cumulative = new();

    public int Count => _cumulative.Count;

    public void Add(double value)
    {
        var sum = value + (_cumulative.Count > 0 ? _cumulative[_cumulative.Count - 1] : 0);
        _cumulative.Add(sum);
    }

    public double Sum(int length)
    {
        if (_cumulative.Count == 0 || length <= 0)
        {
            return 0;
        }

        var end = _cumulative[_cumulative.Count - 1];
        var startIndex = _cumulative.Count - length - 1;
        var start = startIndex >= 0 ? _cumulative[startIndex] : 0;
        return end - start;
    }

    public double SumAt(int length, int endIndex)
    {
        if (_cumulative.Count == 0 || length <= 0 || endIndex < 0)
        {
            return 0;
        }

        var end = _cumulative[endIndex];
        var startIndex = endIndex - length;
        var start = startIndex >= 0 ? _cumulative[startIndex] : 0;
        return end - start;
    }

    public double Average(int length)
    {
        if (_cumulative.Count == 0)
        {
            return 0;
        }

        var count = Math.Min(length, _cumulative.Count);
        return count > 0 ? Sum(length) / count : 0;
    }

    public double AverageAt(int length, int endIndex)
    {
        if (_cumulative.Count == 0 || endIndex < 0)
        {
            return 0;
        }

        var count = Math.Min(length, endIndex + 1);
        return count > 0 ? SumAt(length, endIndex) / count : 0;
    }
}

internal sealed class RollingMinMax
{
    private readonly int _length;
    private readonly bool _useLinear;
    private readonly double[]? _linearBuffer;
    private int _linearCount;
    private int _linearIndex;
    private double _linearMin;
    private double _linearMax;
    private readonly LinkedList<(double value, int index)> _minDeque = new();   
    private readonly LinkedList<(double value, int index)> _maxDeque = new();   
    private int _index;

    public RollingMinMax(int length)
    {
        _length = Math.Max(1, length);
        _useLinear = _length <= RollingWindowSettings.SmallWindowThreshold;
        if (_useLinear)
        {
            _linearBuffer = new double[_length];
        }
    }

    public void Add(double value)
    {
        if (_useLinear)
        {
            _linearBuffer![_linearIndex] = value;
            _linearIndex++;
            if (_linearIndex == _length)
            {
                _linearIndex = 0;
            }

            if (_linearCount < _length)
            {
                _linearCount++;
            }

            RecalculateLinearMinMax();
            return;
        }

        while (_minDeque.Last != null && _minDeque.Last.Value.value >= value)   
        {
            _minDeque.RemoveLast();
        }

        _minDeque.AddLast((value, _index));

        while (_maxDeque.Last != null && _maxDeque.Last.Value.value <= value)
        {
            _maxDeque.RemoveLast();
        }

        _maxDeque.AddLast((value, _index));

        var expireIndex = _index - _length;
        while (_minDeque.First != null && _minDeque.First.Value.index <= expireIndex)
        {
            _minDeque.RemoveFirst();
        }

        while (_maxDeque.First != null && _maxDeque.First.Value.index <= expireIndex)
        {
            _maxDeque.RemoveFirst();
        }

        _index++;
    }

    public double Min
    {
        get
        {
            if (_useLinear)
            {
                return _linearMin;
            }

            var node = _minDeque.First;
            return node != null ? node.Value.value : 0;
        }
    }

    public double Max
    {
        get
        {
            if (_useLinear)
            {
                return _linearMax;
            }

            var node = _maxDeque.First;
            return node != null ? node.Value.value : 0;
        }
    }

    private void RecalculateLinearMinMax()
    {
        if (_linearCount == 0)
        {
            _linearMin = 0;
            _linearMax = 0;
            return;
        }

        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        for (var i = 0; i < _linearCount; i++)
        {
            var value = _linearBuffer![i];
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        _linearMin = min == double.PositiveInfinity ? 0 : min;
        _linearMax = max == double.NegativeInfinity ? 0 : max;
    }
}

internal sealed class RollingCorrelation
{
    private readonly RollingSum _xSum = new();
    private readonly RollingSum _ySum = new();
    private readonly RollingSum _x2Sum = new();
    private readonly RollingSum _y2Sum = new();
    private readonly RollingSum _xySum = new();

    public int Count => _xSum.Count;

    public void Add(double x, double y)
    {
        _xSum.Add(x);
        _ySum.Add(y);
        _x2Sum.Add(x * x);
        _y2Sum.Add(y * y);
        _xySum.Add(x * y);
    }

    public double R(int length)
    {
        if (length <= 1 || _xSum.Count == 0)
        {
            return 0;
        }

        var n = Math.Min(length, _xSum.Count);
        if (n <= 1)
        {
            return 0;
        }

        var sumX = _xSum.Sum(length);
        var sumY = _ySum.Sum(length);
        var sumX2 = _x2Sum.Sum(length);
        var sumY2 = _y2Sum.Sum(length);
        var sumXY = _xySum.Sum(length);

        var numerator = (n * sumXY) - (sumX * sumY);
        var denomLeft = (n * sumX2) - (sumX * sumX);
        var denomRight = (n * sumY2) - (sumY * sumY);
        var denom = Math.Sqrt(denomLeft * denomRight);
        return denom != 0 ? numerator / denom : 0;
    }

    public double RSquared(int length)
    {
        var r = R(length);
        return r * r;
    }
}

internal sealed class RollingMedian : IDisposable
{
    private readonly int _length;
    private readonly bool _useLinear;
    private readonly PooledRingBuffer<double> _window;
    private readonly double[]? _scratch;
    private readonly BinaryHeap? _lower;
    private readonly BinaryHeap? _upper;
    private readonly Dictionary<double, int>? _delayedLower;
    private readonly Dictionary<double, int>? _delayedUpper;
    private int _lowerSize;
    private int _upperSize;
    private bool _disposed;

    public RollingMedian(int length)
    {
        _length = Math.Max(1, length);
        _useLinear = _length <= RollingWindowSettings.SmallWindowThreshold;
        _window = new PooledRingBuffer<double>(_length);

        if (_useLinear)
        {
            _scratch = ArrayPool<double>.Shared.Rent(_length);
        }
        else
        {
            _lower = new BinaryHeap(isMinHeap: false);
            _upper = new BinaryHeap(isMinHeap: true);
            _delayedLower = new Dictionary<double, int>();
            _delayedUpper = new Dictionary<double, int>();
        }
    }

    public int Count => _window.Count;

    public void Add(double value)
    {
        if (_useLinear)
        {
            _window.TryAdd(value, out _);
            return;
        }

        if (_lower!.Count == 0 || value <= _lower.Peek())
        {
            _lower.Add(value);
            _lowerSize++;
        }
        else
        {
            _upper!.Add(value);
            _upperSize++;
        }

        if (_window.TryAdd(value, out var removed))
        {
            Remove(removed);
        }

        Balance();
    }

    public double Median
    {
        get
        {
            if (_window.Count == 0)
            {
                return 0;
            }

            if (_useLinear)
            {
                return LinearMedian();
            }

            Prune(_lower!, _delayedLower!);
            Prune(_upper!, _delayedUpper!);

            if ((_window.Count & 1) == 1)
            {
                return _lower!.Peek();
            }

            return (_lower!.Peek() + _upper!.Peek()) / 2;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Dispose();
        if (_scratch != null)
        {
            ArrayPool<double>.Shared.Return(_scratch, clearArray: true);
        }

        _disposed = true;
    }

    private double LinearMedian()
    {
        var count = _window.Count;
        if (count == 0)
        {
            return 0;
        }

        var scratch = _scratch!;
        _window.CopyTo(scratch);
        Array.Sort(scratch, 0, count);
        if ((count & 1) == 1)
        {
            return scratch[count / 2];
        }

        return (scratch[(count / 2) - 1] + scratch[count / 2]) / 2;
    }

    private void Remove(double value)
    {
        if (_lower!.Count == 0)
        {
            AddDelayed(_delayedUpper!, value);
            _upperSize--;
            Prune(_upper!, _delayedUpper!);
            return;
        }

        if (value <= _lower.Peek())
        {
            AddDelayed(_delayedLower!, value);
            _lowerSize--;
            if (value == _lower.Peek())
            {
                Prune(_lower, _delayedLower!);
            }
        }
        else
        {
            AddDelayed(_delayedUpper!, value);
            _upperSize--;
            if (_upper!.Count > 0 && value == _upper.Peek())
            {
                Prune(_upper, _delayedUpper!);
            }
        }
    }

    private void Balance()
    {
        if (_lowerSize > _upperSize + 1)
        {
            _upper!.Add(_lower!.Pop());
            _lowerSize--;
            _upperSize++;
            Prune(_lower, _delayedLower!);
        }
        else if (_lowerSize < _upperSize)
        {
            _lower!.Add(_upper!.Pop());
            _upperSize--;
            _lowerSize++;
            Prune(_upper, _delayedUpper!);
        }

        Prune(_lower!, _delayedLower!);
        Prune(_upper!, _delayedUpper!);
    }

    private static void AddDelayed(Dictionary<double, int> delayed, double value)
    {
        delayed.TryGetValue(value, out var count);
        delayed[value] = count + 1;
    }

    private static void Prune(BinaryHeap heap, Dictionary<double, int> delayed)
    {
        while (heap.Count > 0)
        {
            var value = heap.Peek();
            if (delayed.TryGetValue(value, out var count) && count > 0)
            {
                heap.Pop();
                if (count == 1)
                {
                    delayed.Remove(value);
                }
                else
                {
                    delayed[value] = count - 1;
                }
            }
            else
            {
                break;
            }
        }
    }
}

internal sealed class BinaryHeap
{
    private readonly bool _isMinHeap;
    private readonly List<double> _items = new();

    public BinaryHeap(bool isMinHeap)
    {
        _isMinHeap = isMinHeap;
    }

    public int Count => _items.Count;

    public double Peek()
    {
        return _items.Count > 0 ? _items[0] : 0;
    }

    public void Add(double value)
    {
        _items.Add(value);
        SiftUp(_items.Count - 1);
    }

    public double Pop()
    {
        if (_items.Count == 0)
        {
            return 0;
        }

        var root = _items[0];
        var last = _items[_items.Count - 1];
        _items.RemoveAt(_items.Count - 1);
        if (_items.Count > 0)
        {
            _items[0] = last;
            SiftDown(0);
        }

        return root;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (Compare(_items[index], _items[parent]))
            {
                (_items[index], _items[parent]) = (_items[parent], _items[index]);
                index = parent;
            }
            else
            {
                break;
            }
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            var left = (index * 2) + 1;
            var right = left + 1;
            var best = index;

            if (left < _items.Count && Compare(_items[left], _items[best]))
            {
                best = left;
            }

            if (right < _items.Count && Compare(_items[right], _items[best]))
            {
                best = right;
            }

            if (best == index)
            {
                break;
            }

            (_items[index], _items[best]) = (_items[best], _items[index]);
            index = best;
        }
    }

    private bool Compare(double left, double right)
    {
        return _isMinHeap ? left < right : left > right;
    }
}

internal sealed class RollingOrderStatistic : IDisposable
{
    private readonly int _length;
    private readonly bool _useLinear;
    private readonly PooledRingBuffer<double> _window;
    private readonly OrderStatisticTree? _tree;
    private readonly double[]? _scratch;
    private bool _disposed;

    public RollingOrderStatistic(int length)
    {
        _length = Math.Max(1, length);
        _useLinear = _length <= RollingWindowSettings.SmallWindowThreshold;
        _window = new PooledRingBuffer<double>(_length);

        if (_useLinear)
        {
            _scratch = ArrayPool<double>.Shared.Rent(_length);
        }
        else
        {
            _tree = new OrderStatisticTree();
        }
    }

    public int Count => _window.Count;

    public void Add(double value)
    {
        if (_useLinear)
        {
            _window.TryAdd(value, out _);
            return;
        }

        if (_window.TryAdd(value, out var removed))
        {
            _tree!.Remove(removed);
        }

        _tree!.Insert(value);
    }

    public int CountLessThan(double value)
    {
        if (_useLinear)
        {
            var count = 0;
            for (var i = 0; i < _window.Count; i++)
            {
                if (_window[i] < value)
                {
                    count++;
                }
            }

            return count;
        }

        return _tree!.CountLessThan(value);
    }

    public int CountLessThanOrEqual(double value)
    {
        if (_useLinear)
        {
            var count = 0;
            for (var i = 0; i < _window.Count; i++)
            {
                if (_window[i] <= value)
                {
                    count++;
                }
            }

            return count;
        }

        return _tree!.CountLessThanOrEqual(value);
    }

    public double PercentileNearestRank(double percentile)
    {
        var count = _window.Count;
        if (count == 0)
        {
            return 0;
        }

        if (_useLinear)
        {
            var scratch = _scratch!;
            _window.CopyTo(scratch);
            Array.Sort(scratch, 0, count);
            var rank = (int)Math.Ceiling(percentile / 100 * count);
            rank = Math.Max(rank, 1);
            rank = Math.Min(rank, count);
            return scratch[rank - 1];
        }

        var treeCount = _tree!.Count;
        var treeRank = treeCount > 0 ? (int)Math.Ceiling(percentile / 100 * treeCount) : 0;
        return _tree.SelectByRank(Math.Max(treeRank, 1));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Dispose();
        if (_scratch != null)
        {
            ArrayPool<double>.Shared.Return(_scratch, clearArray: true);
        }

        _disposed = true;
    }
}

internal sealed class OrderStatisticTree
{
    private static readonly Random Random = new();
    private Node? _root;

    public int Count => _root?.Size ?? 0;

    public void Insert(double key)
    {
        _root = Insert(_root, key);
    }

    public void Remove(double key)
    {
        _root = Remove(_root, key);
    }

    public int CountLessThan(double key)
    {
        return CountLessThan(_root, key);
    }

    public int CountLessThanOrEqual(double key)
    {
        return CountLessThanOrEqual(_root, key);
    }

    public double SelectByRank(int rank)
    {
        if (_root == null || rank <= 0)
        {
            return 0;
        }

        rank = Math.Min(rank, _root.Size);
        return SelectByRank(_root, rank);
    }

    private static Node Insert(Node? node, double key)
    {
        if (node == null)
        {
            return new Node(key, Random.Next());
        }

        if (key == node.Key)
        {
            node.Count++;
        }
        else if (key < node.Key)
        {
            node.Left = Insert(node.Left, key);
            if (node.Left.Priority > node.Priority)
            {
                node = RotateRight(node);
            }
        }
        else
        {
            node.Right = Insert(node.Right, key);
            if (node.Right.Priority > node.Priority)
            {
                node = RotateLeft(node);
            }
        }

        node.Update();
        return node;
    }

    private static Node? Remove(Node? node, double key)
    {
        if (node == null)
        {
            return null;
        }

        if (key == node.Key)
        {
            if (node.Count > 1)
            {
                node.Count--;
            }
            else if (node.Left == null)
            {
                return node.Right;
            }
            else if (node.Right == null)
            {
                return node.Left;
            }
            else
            {
                if (node.Left.Priority > node.Right.Priority)
                {
                    node = RotateRight(node);
                    node.Right = Remove(node.Right, key);
                }
                else
                {
                    node = RotateLeft(node);
                    node.Left = Remove(node.Left, key);
                }
            }
        }
        else if (key < node.Key)
        {
            node.Left = Remove(node.Left, key);
        }
        else
        {
            node.Right = Remove(node.Right, key);
        }

        node.Update();
        return node;
    }

    private static int CountLessThan(Node? node, double key)
    {
        if (node == null)
        {
            return 0;
        }

        if (key <= node.Key)
        {
            return CountLessThan(node.Left, key);
        }

        return node.LeftSize + node.Count + CountLessThan(node.Right, key);
    }

    private static int CountLessThanOrEqual(Node? node, double key)
    {
        if (node == null)
        {
            return 0;
        }

        if (key < node.Key)
        {
            return CountLessThanOrEqual(node.Left, key);
        }

        return node.LeftSize + node.Count + CountLessThanOrEqual(node.Right, key);
    }

    private static double SelectByRank(Node node, int rank)
    {
        if (rank <= node.LeftSize)
        {
            return SelectByRank(node.Left!, rank);
        }

        var selfRank = node.LeftSize + node.Count;
        if (rank <= selfRank)
        {
            return node.Key;
        }

        return SelectByRank(node.Right!, rank - selfRank);
    }

    private static Node RotateRight(Node node)
    {
        var left = node.Left!;
        node.Left = left.Right;
        left.Right = node;
        node.Update();
        left.Update();
        return left;
    }

    private static Node RotateLeft(Node node)
    {
        var right = node.Right!;
        node.Right = right.Left;
        right.Left = node;
        node.Update();
        right.Update();
        return right;
    }

    private sealed class Node
    {
        public Node(double key, int priority)
        {
            Key = key;
            Priority = priority;
            Count = 1;
            Size = 1;
        }

        public double Key { get; }
        public int Priority { get; }
        public int Count { get; set; }
        public int Size { get; private set; }
        public Node? Left { get; set; }
        public Node? Right { get; set; }

        public int LeftSize => Left?.Size ?? 0;
        public int RightSize => Right?.Size ?? 0;

        public void Update()
        {
            Size = Count + LeftSize + RightSize;
        }
    }
}
