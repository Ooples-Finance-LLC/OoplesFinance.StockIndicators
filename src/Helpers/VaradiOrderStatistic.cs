using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// A rank tree over exact smoothed ratios. The queue and nodes grow only as bars
// arrive; requesting an extreme period does not allocate period-sized storage.
internal sealed class VaradiOrderStatistic : IDisposable
{
    private readonly int _length;
    private readonly Queue<Number> _window = new();
    private readonly Random _random = new(719);
    private Node? _root;
    internal VaradiOrderStatistic(int length) => _length = Math.Max(1, length);
    internal int CountLessThanOrEqual(Number key)
    {
        var node = _root; var count = 0;
        while (node is not null)
        {
            if ((key - node.Key).Sign < 0) node = node.Left;
            else { count += Size(node.Left) + node.Count; node = node.Right; }
        }
        return count;
    }
    internal void Add(Number key)
    {
        if (_window.Count == _length) _root = Remove(_root!, _window.Dequeue());
        _window.Enqueue(key);
        _root = Insert(_root, key);
    }
    public void Dispose() { _window.Clear(); _root = null; }

    private Node Insert(Node? node, Number key)
    {
        if (node is null) return new Node(key, _random.Next());
        var order = (key - node.Key).Sign;
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
    private static Node? Remove(Node node, Number key)
    {
        var order = (key - node.Key).Sign;
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
        internal readonly Number Key;
        internal readonly int Priority;
        internal int Count = 1, Size = 1;
        internal Node? Left, Right;
        internal Node(Number key, int priority) { Key = key; Priority = priority; }
    }
}
