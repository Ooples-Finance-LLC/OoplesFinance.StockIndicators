using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>Shared partitioning and eager-result checks for direct Skender fixtures.</summary>
public abstract class SharedSkenderBenchmarkBase : SharedMappedBenchmarkBase
{
    protected List<Quote> _quotes = null!;
    protected int[] _ends = null!;
    protected int[] _seeds = null!;

    private protected (ComparisonPair Pair, CompetitorData Data) PrepareNativeFixture(string operation, int? precedingBars = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var fixture = PrepareFixture(operation);
        _quotes = fixture.Data.Quotes;
        int chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, chunks).Select(i => (int)((long)Count * i / chunks)).ToArray();
        _seeds = Enumerable.Range(0, chunks).Select(i => precedingBars.HasValue
            ? Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - precedingBars.Value) : 0).ToArray();
        return fixture;
    }

    protected T[] Parallel<TInput, T>(IEnumerable<TInput>[] inputs, Func<IEnumerable<TInput>, IEnumerable<T>> calculate)
    {
        var result = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, chunk =>
        {
            int start = chunk == 0 ? 0 : _ends[chunk - 1], end = _ends[chunk];
            // Skender 2.7.3 eagerly returns List<T>. Preserve native ownership
            // without an intermediate array copy. Calculation and joining are timed.
            var rows = (List<T>)calculate(inputs[chunk]);
            if (rows.Count != end - _seeds[chunk]) throw new InvalidOperationException("Native input range length differs.");
            rows.CopyTo(start - _seeds[chunk], result, start, end - start);
        });
        return result;
    }

    private protected void VerifyNative(ComparisonSeries expected, object serial, object parallel,
        Func<object, ComparisonSeries> normalize, string operation, IndicatorErrorBudget? budget)
    {
        ComparisonVerifier.Compare(expected, normalize(serial), operation + " direct native serial", budget);
        ComparisonVerifier.Compare(expected, normalize(parallel), operation + " direct native parallel", budget);
        foreach (var result in new[] { serial, parallel })
            if (!((IEnumerable<ResultBase>)result).Select(row => row.Date).SequenceEqual(_quotes.Select(quote => quote.Date)))
                throw new InvalidOperationException("Native result dates differ from the fixture.");
    }
}
