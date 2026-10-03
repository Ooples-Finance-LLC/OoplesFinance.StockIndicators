namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ChandeMomentumAverageWindow : IDisposable
{
    private readonly ChandeMomentumWindow _first, _second, _third;

    internal ChandeMomentumAverageWindow(int length1, int length2, int length3)
    {
        _first = new(length1, zeroOrigin: true, unitRatio: true);
        _second = new(length2, zeroOrigin: true, unitRatio: true);
        _third = new(length3, zeroOrigin: true, unitRatio: true);
    }

    internal double Next(double value, bool commit)
    {
        var mean = new ExactMeanAccumulator();
        mean.Add(_first.Next(value, commit), 100);
        mean.Add(_second.Next(value, commit), 100);
        mean.Add(_third.Next(value, commit), 100);
        return mean.Mean(3);
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length1, int length2, int length3, bool absolute = false)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        using var window = new ChandeMomentumAverageWindow(Math.Min(Math.Max(1, length1), input.Length),
            Math.Min(Math.Max(1, length2), input.Length), Math.Min(Math.Max(1, length3), input.Length));
        for (var i = 0; i < input.Length; i++)
        {
            var value = window.Next(input[i], true);
            output[i] = absolute ? Math.Abs(value) : value;
        }
    }

    internal void Reset() { _first.Reset(); _second.Reset(); _third.Reset(); }
    public void Dispose() { _first.Dispose(); _second.Dispose(); _third.Dispose(); }
}
