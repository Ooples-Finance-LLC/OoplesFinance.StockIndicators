namespace OoplesFinance.StockIndicators.Builder.ML.Helpers;

/// <summary>
/// Extension methods for .NET Framework 4.6.1 compatibility.
/// </summary>
internal static class CompatibilityExtensions
{
    /// <summary>
    /// Returns the last N elements from a sequence.
    /// </summary>
    public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (count <= 0) return [];

        return TakeLastIterator(source, count);
    }

    private static IEnumerable<T> TakeLastIterator<T>(IEnumerable<T> source, int count)
    {
        if (source is IList<T> list)
        {
            var start = Math.Max(0, list.Count - count);
            for (var i = start; i < list.Count; i++)
            {
                yield return list[i];
            }
        }
        else if (source is IReadOnlyList<T> readOnlyList)
        {
            var start = Math.Max(0, readOnlyList.Count - count);
            for (var i = start; i < readOnlyList.Count; i++)
            {
                yield return readOnlyList[i];
            }
        }
        else
        {
            // Buffer for non-list sources
            var buffer = new Queue<T>();
            foreach (var item in source)
            {
                buffer.Enqueue(item);
                if (buffer.Count > count)
                {
                    buffer.Dequeue();
                }
            }

            foreach (var item in buffer)
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Computes the base-2 logarithm of a number.
    /// </summary>
    public static double Log2(double value)
    {
        return Math.Log(value) / Math.Log(2);
    }

    /// <summary>
    /// Gets the element at the specified index from the end of the list.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="indexFromEnd">Index from end (1 = last element, 2 = second to last, etc.).</param>
    public static T FromEnd<T>(this IReadOnlyList<T> list, int indexFromEnd)
    {
        return list[list.Count - indexFromEnd];
    }
}
