using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed class HilbertTrendMeanState
{
    private readonly JurikDyadic[] _prices = new JurikDyadic[50],
        _means = new JurikDyadic[3];
    private int _cursor;
    internal JurikDyadic Average { get; private set; }

    internal void Reset()
    {
        Array.Clear(_prices, 0, 50);
        Array.Clear(_means, 0, 3);
        _cursor = 0;
        Average = default;
    }

    internal void Update(JurikDyadic price, int? count)
    {
        _prices[_cursor] = price;
        if (count.HasValue)
        {
            JurikDyadic sum = default;
            for (var lag = 0; lag < count.Value; lag++)
                sum.AddExact(_prices[(_cursor - lag + 50) % 50]);
            var mean = count.Value == 0 ? default : sum.Rounded(count.Value);
            var weighted = mean; weighted.Multiply(4);
            var term = _means[0]; term.Multiply(3); weighted.AddExact(term);
            term = _means[1]; term.Multiply(2); weighted.AddExact(term);
            weighted.AddExact(_means[2]);
            Average = weighted.Rounded(10);
            _means[2] = _means[1];
            _means[1] = _means[0];
            _means[0] = mean;
        }
        _cursor = (_cursor + 1) % 50;
    }
}
