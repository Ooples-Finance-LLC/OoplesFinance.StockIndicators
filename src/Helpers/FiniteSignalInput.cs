namespace OoplesFinance.StockIndicators.Helpers;

internal static class FiniteSignalInput
{
    // A nonrepresentable published ratio invalidates its dependent signal. Preserve
    // the finite prefix without feeding infinity into finite-only average kernels.
    internal static List<double> Create(IReadOnlyList<double> values, out int count)
    {
        count = 0;
        while (count < values.Count && !double.IsInfinity(values[count]) && !double.IsNaN(values[count])) count++;
        var result = new List<double>(values.Count);
        for (var i = 0; i < values.Count; i++) result.Add(i < count ? values[i] : 0);
        return result;
    }
}
