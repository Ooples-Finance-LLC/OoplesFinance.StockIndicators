using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed class HilbertTrendMeanState
{
    private readonly JurikDyadic[] _prices = new JurikDyadic[50],
        _means = new JurikDyadic[3];
    private int _cursor, _count;
    private JurikDyadic _sum;
    internal JurikDyadic Average { get; private set; }

    internal void Reset()
    {
        Array.Clear(_prices, 0, 50);
        Array.Clear(_means, 0, 3);
        _cursor = 0;
        _count = 0;
        _sum = default;
        Average = default;
    }

    internal void Update(JurikDyadic price, int? count)
    {
        JurikDyadic mean = default;
        if (count is > 0)
        {
            // Remove the previous window's oldest price before overwriting the ring.
            if (_count > 0)
                _sum.Subtract(_prices[(_cursor - _count + 50) % 50]);
            _prices[_cursor] = price;
            _sum.AddExact(price);
            int retained = Math.Max(1, _count);
            while (retained < count.Value)
            {
                _sum.AddExact(_prices[(_cursor - retained + 50) % 50]);
                retained++;
            }
            while (retained > count.Value)
            {
                retained--;
                _sum.Subtract(_prices[(_cursor - retained + 50) % 50]);
            }
            _count = count.Value;
            mean = _sum.Rounded(_count);
        }
        else
        {
            _prices[_cursor] = price;
            _sum = default;
            _count = 0;
        }
        if (count.HasValue)
        {
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
