using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

// Rolling integer moments accept both finite closes and wide unpublished stages.
// Triangular weights are the convolution of two unrounded box sums.
internal sealed class WideClassicalWindowState : IWideAverageState
{
    private readonly int _period,
        _firstLength,
        _secondLength;
    private readonly ClassicAverageMethod _method;
    private readonly Queue<BigInteger> _prices = new(),
        _sums = new();
    private BigInteger _sum,
        _weighted,
        _second,
        _numerator;
    private long _denominator = 1;
    public BigInteger RoundedUnits => RocBankValue.RoundUnits(_numerator, _denominator);

    internal WideClassicalWindowState(int period, ClassicAverageMethod method)
    {
        _period = period;
        _method = method;
        _firstLength = method == ClassicAverageMethod.Trima ? (int)((period + 1L) / 2) : period;
        _secondLength = period - _firstLength + 1;
    }

    public void Reset()
    {
        _prices.Clear();
        _sums.Clear();
        _sum = _weighted = _second = _numerator = 0;
        _denominator = 1;
    }

    public void UpdateUnits(BigInteger input, Span<double> outputs)
    {
        outputs.Clear();
        if (_method == ClassicAverageMethod.Wma)
            _weighted -= _sum;
        if (_prices.Count == _firstLength)
            _sum -= _prices.Dequeue();
        _prices.Enqueue(input);
        _sum += input;
        if (_method == ClassicAverageMethod.Wma)
            _weighted += _period * input;
        var count = _prices.Count;
        if (_method == ClassicAverageMethod.Trima)
        {
            if (count < _firstLength)
                return;
            if (_sums.Count == _secondLength)
                _second -= _sums.Dequeue();
            _sums.Enqueue(_sum);
            _second += _sum;
            if (_sums.Count < _secondLength)
                return;
            _numerator = _second;
            _denominator = (long)_firstLength * _secondLength;
        }
        else
        {
            _numerator = _method == ClassicAverageMethod.Wma ? _weighted : _sum;
            _denominator =
                _method == ClassicAverageMethod.Wma
                    ? (long)count * (2L * _period - count + 1) / 2
                    : count;
        }
        outputs[0] = ExactMeanAccumulator.UnitRatio(_numerator, _denominator);
        outputs[1] = 1;
    }
}
