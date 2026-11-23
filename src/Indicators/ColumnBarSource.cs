using System.Collections;
using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Borrowed price columns of equal length, one element per bar.</summary>
internal sealed class BarColumns : IReadOnlyList<Bar>
{
    internal BarColumns(DateTime[]? times, double[] open, double[] high, double[] low, double[] close, double[] volume)
    {
        Times = times;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    internal DateTime[]? Times { get; }
    internal double[] Open { get; }
    internal double[] High { get; }
    internal double[] Low { get; }
    internal double[] Close { get; }
    internal double[] Volume { get; }

    public int Count => Close.Length;

    /// <summary>The bar at an index, assembled from the columns on request.</summary>
    public Bar this[int index] => new(Times is null ? default : Times[index], Open[index], High[index], Low[index],
        Close[index], Volume[index]);

    public IEnumerator<Bar> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A source that can hand its columns to an execution route without materialising bars.</summary>
internal interface IColumnBarSource
{
    BarColumns Columns { get; }
}

/// <summary>A finite source over caller-owned columns.</summary>
internal sealed class ColumnBarSource : IBarSource, ISynchronousBarSource, IColumnBarSource
{
    internal ColumnBarSource(BarColumns columns) => Columns = columns;

    public BarColumns Columns { get; }

    public bool IsFinite => true;

    // Routes that need an array of bars read the columns through AppendValidated instead.
    public Bar[]? DirectBars => null;

    public void AppendValidated(OwnedBarHistory destination, CancellationToken cancellationToken)
    {
        var columns = Columns;
        destination.ExpectAdditional(columns.Count);
        for (var i = 0; i < columns.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bar = columns[i];
            Validation.IndicatorInputDomain.Finite.Validate(bar);
            destination.Add(bar);
        }
    }

    public async IAsyncEnumerable<Bar> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var columns = Columns;
        for (var i = 0; i < columns.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return columns[i];
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async IAsyncEnumerable<Bar> ReadWarmupAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}
