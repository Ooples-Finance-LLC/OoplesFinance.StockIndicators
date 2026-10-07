using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed class HilbertTrendMeanState
{
    private readonly BigInteger[] _prices = new BigInteger[50],
        _means = new BigInteger[3];
    private int _cursor;
    internal BigInteger RoundedUnits { get; private set; }

    internal void Reset()
    {
        Array.Clear(_prices, 0, 50);
        Array.Clear(_means, 0, 3);
        _cursor = 0;
        RoundedUnits = 0;
    }

    internal void Update(BigInteger price, int? count)
    {
        _prices[_cursor] = price;
        if (count.HasValue)
        {
            BigInteger sum = 0;
            for (var lag = 0; lag < count.Value; lag++)
                sum += _prices[(_cursor - lag + 50) % 50];
            var mean =
                count.Value == 0 ? BigInteger.Zero : RocBankValue.RoundUnits(sum, count.Value);
            RoundedUnits = RocBankValue.RoundUnits(
                4 * mean + 3 * _means[0] + 2 * _means[1] + _means[2],
                10
            );
            _means[2] = _means[1];
            _means[1] = _means[0];
            _means[0] = mean;
        }
        _cursor = (_cursor + 1) % 50;
    }
}
