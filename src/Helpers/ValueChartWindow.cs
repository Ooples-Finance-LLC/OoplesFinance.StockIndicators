using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
using Average = OoplesFinance.StockIndicators.Helpers.MacZWindow.Average;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ValueChartWindow : IDisposable
{
    private readonly Average _basis;
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly Queue<Number> _ranges = new();
    private Number _rangeSum;
    private double _previousInput;
    private bool _hasPrevious;

    internal ValueChartWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        var rangeLength = Math.Max(2, Math.Min(530, (int)((length + 4L) / 5)));
        _basis = new(kind, length);
        _high = new(rangeLength); _low = new(rangeLength);
    }

    internal static Number Median(double high, double low) => (Number.Of(high) + Number.Of(low)).Divide(2);

    private Number[] Coordinates(double open, double high, double low, double close, Number basis, bool final)
    {
        var highest = final ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lowest = final ? _low.Add(low, out _) : _low.Preview(low, out _);
        var range = Number.Of(highest) - Number.Of(lowest);
        var total = _rangeSum + range;
        Number Coordinate(double value) => total.Sign == 0 ? default : (Number.Of(value) - basis).Times(25).Divide(total);
        var result = new[] { Coordinate(close), Coordinate(open), Coordinate(high), Coordinate(low) };
        if (final)
        {
            // Retain four preceding ranges; the current one makes the fifth.
            if (_ranges.Count == 4) _rangeSum -= _ranges.Dequeue();
            _ranges.Enqueue(range); _rangeSum += range;
        }
        return result;
    }

    internal double[] Next(OhlcvBar bar, bool custom, bool final)
    {
        StreamingInputValidation.Validate(bar);
        var input = custom ? Number.Of(bar.Close) : Median(bar.High, bar.Low);
        var high = bar.High; var low = bar.Low;
        var publishedInput = input.Publish();
        if (!custom && !CalculationsHelper.IsWithinBarRange(publishedInput, low, high))
        {
            var previous = _hasPrevious ? _previousInput : publishedInput;
            high = Math.Max(previous, publishedInput); low = Math.Min(previous, publishedInput);
        }
        var result = Coordinates(bar.Open, high, low, bar.Close, _basis.Next(input, final), final);
        if (final) { _previousInput = publishedInput; _hasPrevious = true; }
        return result.Select(v => v.Publish()).ToArray();
    }

    internal static (double[][] Outputs, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool includeTrades)
    {
        length = Math.Max(1, length);
        var count = data.Count; var custom = data.ChainedValues.Count > 0;
        var input = new Number[count]; var closes = new double[count];
        for (var i = 0; i < count; i++)
        {
            StreamingInputValidation.Finite(data.OpenPrices[i], nameof(data.OpenPrices));
            StreamingInputValidation.Finite(data.HighPrices[i], nameof(data.HighPrices));
            StreamingInputValidation.Finite(data.LowPrices[i], nameof(data.LowPrices));
            StreamingInputValidation.Finite(data.ClosePrices[i], nameof(data.ClosePrices));
            StreamingInputValidation.Finite(data.Volumes[i], nameof(data.Volumes));
            input[i] = custom ? Number.Of(data.ChainedValues[i]) : Median(data.HighPrices[i], data.LowPrices[i]);
            closes[i] = custom ? data.ChainedValues[i] : data.ClosePrices[i];
        }
        var published = input.Select(v => v.Publish()).ToArray();
        var (highs, lows) = published.SequenceEqual(data.ClosePrices)
            ? (data.HighPrices, data.LowPrices)
            : CalculationsHelper.GetCustomRangeLists(published, data.HighPrices, data.LowPrices);
        Number[] Mean(Number[] values)
        {
            var replacement = ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), length);
            if (replacement is not null) return Enumerable.Range(0, count).Select(i => i < replacement.Count ? Number.Of(replacement[i]) : default).ToArray();
            using var mean = new Average(kind, length);
            return values.Select(v => mean.Next(v, true)).ToArray();
        }
        var basis = Mean(input); var outputs = Enumerable.Range(0, 4).Select(_ => new Number[count]).ToArray();
        using var window = new ValueChartWindow(kind, length);
        for (var i = 0; i < count; i++)
        {
            var point = window.Coordinates(data.OpenPrices[i], highs[i], lows[i], closes[i], basis[i], true);
            for (var slot = 0; slot < 4; slot++) outputs[slot][i] = point[slot];
        }
        var trades = new Signal[count];
        if (includeTrades)
        {
            var mean = Mean(outputs[0]); Number previousSlope = default, previous = default;
            var upper = Number.Of(4); var lower = Number.Of(-4);
            for (var i = 0; i < count; i++)
            {
                var value = outputs[0][i]; var slope = value - mean[i]; var change = slope - previousSlope;
                trades[i] = slope.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy
                    : slope.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
                    : slope.Sign > 0 || (previous - lower).Sign < 0 && (value - lower).Sign > 0 ? Signal.Buy
                    : slope.Sign < 0 || (previous - upper).Sign > 0 && (value - upper).Sign < 0 ? Signal.Sell : Signal.None;
                previousSlope = slope; previous = value;
            }
        }
        return (outputs.Select(values => values.Select(v => v.Publish()).ToArray()).ToArray(), trades);
    }

    internal void Reset()
    {
        _basis.Reset(); _high.Reset(); _low.Reset(); _ranges.Clear(); _rangeSum = default;
        _previousInput = 0; _hasPrevious = false;
    }
    public void Dispose() { _basis.Dispose(); _high.Dispose(); _low.Dispose(); }
}
