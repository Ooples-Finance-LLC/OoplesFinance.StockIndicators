using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Helpers;
using static OoplesFinance.StockIndicators.Helpers.CalculationsHelper;
using static OoplesFinance.StockIndicators.Helpers.MathHelper;
using static OoplesFinance.StockIndicators.Helpers.SignalHelper;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Preserved from 71c31b97: only method/class names and access are changed.
// The original RollingWindowSettings.SmallWindowThreshold constant is 32.
internal static class PreservedKaufmanStressBatch
{
    internal static StockData Run(StockData stockData, StockData marketData, int length = 60)
    {
        List<double> svList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList1, highList1, lowList1, _, _) = GetInputValuesList(stockData);
        var (inputList2, highList2, lowList2, _, _) = GetInputValuesList(marketData);
        var (highestList1, lowestList1) = GetMaxAndMinValuesList(highList1, lowList1, length);
        var (highestList2, lowestList2) = GetMaxAndMinValuesList(highList2, lowList2, length);
        var dWindow = new PreservedKaufmanRollingMinMax(length);

        if (stockData.Count == marketData.Count)
        {
            for (var i = 0; i < stockData.Count; i++)
            {
                var highestHigh1 = highestList1[i];
                var lowestLow1 = lowestList1[i];
                var highestHigh2 = highestList2[i];
                var lowestLow2 = lowestList2[i];
                var currentValue1 = inputList1[i];
                var currentValue2 = inputList2[i];
                var prevSv1 = i >= 1 ? svList[i - 1] : 0;
                var prevSv2 = i >= 2 ? svList[i - 2] : 0;
                var r1 = highestHigh1 - lowestLow1;
                var r2 = highestHigh2 - lowestLow2;
                var s1 = r1 != 0 ? (currentValue1 - lowestLow1) / r1 : 0.5;
                var s2 = r2 != 0 ? (currentValue2 - lowestLow2) / r2 : 0.5;

                var d = Math.Abs(s1-s2) <= 1.4210854715202004e-14 ? 0 : s1-s2;
                dWindow.Add(d);
                var highestD = dWindow.Max;
                var lowestD = dWindow.Min;
                var r11 = highestD - lowestD;

                var sv = r11 != 0 ? MinOrMax(100 * (d - lowestD) / r11, 100, 0) : 50;
                svList.Add(sv);

                var signal = GetRsiSignal(sv - prevSv1, prevSv1 - prevSv2, sv, prevSv1, 90, 10);
                signalsList?.Add(signal);
            }
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ksi", svList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(svList);
        stockData.IndicatorName = IndicatorName.KaufmanStressIndicator;

        return stockData;
    }
}

internal sealed class PreservedKaufmanRollingMinMax
{
    private readonly int _length;
    private readonly bool _useLinear;
    private readonly double[]? _linearBuffer;
    private int _linearCount;
    private int _linearIndex;
    private double _linearMin;
    private double _linearMax;
    private readonly LinkedList<(double value, int index)> _minDeque = new();
    private readonly LinkedList<(double value, int index)> _maxDeque = new();
    private int _index;

    public PreservedKaufmanRollingMinMax(int length)
    {
        _length = Math.Max(1, length);
        _useLinear = _length <= 32;
        if (_useLinear)
        {
            _linearBuffer = new double[_length];
        }
    }

    public void Add(double value)
    {
        if (_useLinear)
        {
            _linearBuffer![_linearIndex] = value;
            _linearIndex++;
            if (_linearIndex == _length)
            {
                _linearIndex = 0;
            }

            if (_linearCount < _length)
            {
                _linearCount++;
            }

            RecalculateLinearMinMax();
            return;
        }

        while (_minDeque.Last != null && _minDeque.Last.Value.value >= value)
        {
            _minDeque.RemoveLast();
        }

        _minDeque.AddLast((value, _index));

        while (_maxDeque.Last != null && _maxDeque.Last.Value.value <= value)
        {
            _maxDeque.RemoveLast();
        }

        _maxDeque.AddLast((value, _index));

        var expireIndex = _index - _length;
        while (_minDeque.First != null && _minDeque.First.Value.index <= expireIndex)
        {
            _minDeque.RemoveFirst();
        }

        while (_maxDeque.First != null && _maxDeque.First.Value.index <= expireIndex)
        {
            _maxDeque.RemoveFirst();
        }

        _index++;
    }

    public double Min
    {
        get
        {
            if (_useLinear)
            {
                return _linearMin;
            }

            var node = _minDeque.First;
            return node != null ? node.Value.value : 0;
        }
    }

    public double Max
    {
        get
        {
            if (_useLinear)
            {
                return _linearMax;
            }

            var node = _maxDeque.First;
            return node != null ? node.Value.value : 0;
        }
    }

    private void RecalculateLinearMinMax()
    {
        if (_linearCount == 0)
        {
            _linearMin = 0;
            _linearMax = 0;
            return;
        }

        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        for (var i = 0; i < _linearCount; i++)
        {
            var value = _linearBuffer![i];
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        _linearMin = min == double.PositiveInfinity ? 0 : min;
        _linearMax = max == double.NegativeInfinity ? 0 : max;
    }
}
