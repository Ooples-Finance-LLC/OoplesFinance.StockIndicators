using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

internal readonly struct StreamingInputResolver
{
    private readonly InputName _inputName;
    private readonly Func<OhlcvBar, double>? _selector;

    public StreamingInputResolver(InputName inputName, Func<OhlcvBar, double>? selector)
    {
        _inputName = inputName;
        _selector = selector;
    }

    public double GetValue(OhlcvBar bar)
    {
        return _selector != null ? _selector(bar) : StreamingInputSelector.GetValue(bar, _inputName);
    }
}

public sealed class SimpleMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _window;
    private readonly StreamingInputResolver _input;

    public SimpleMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _window = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SimpleMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _window = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SimpleMovingAverage;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int countAfter;
        var sum = isFinal ? _window.Add(value, out countAfter) : _window.Preview(value, out countAfter);
        var sma = countAfter >= _length ? sum / _length : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sma", sma }
            };
        }

        return new StreamingIndicatorStateResult(sma, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

public sealed class ExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly EmaState _ema;
    private readonly StreamingInputResolver _input;

    public ExponentialMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _ema = new EmaState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ExponentialMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema = new EmaState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ExponentialMovingAverage;

    public void Reset()
    {
        _ema.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var ema = _ema.GetNext(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ema", ema }
            };
        }

        return new StreamingIndicatorStateResult(ema, outputs);
    }
}

public sealed class WeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly WmaState _wma;
    private readonly StreamingInputResolver _input;

    public WeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _wma = new WmaState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public WeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _wma = new WmaState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.WeightedMovingAverage;

    public void Reset()
    {
        _wma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var wma = _wma.GetNext(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Wma", wma }
            };
        }

        return new StreamingIndicatorStateResult(wma, outputs);
    }

    public void Dispose()
    {
        _wma.Dispose();
    }
}

public sealed class WellesWilderMovingAverageState : IStreamingIndicatorState
{
    private readonly WilderState _wilder;
    private readonly StreamingInputResolver _input;

    public WellesWilderMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _wilder = new WilderState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public WellesWilderMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _wilder = new WilderState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.WellesWilderMovingAverage;

    public void Reset()
    {
        _wilder.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var wwma = _wilder.GetNext(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Wwma", wwma }
            };
        }

        return new StreamingIndicatorStateResult(wwma, outputs);
    }
}

public sealed class TriangularMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _primary;
    private readonly IMovingAverageSmoother _secondary;
    private readonly StreamingInputResolver _input;

    public TriangularMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _primary = MovingAverageSmootherFactory.Create(maType, resolved);
        _secondary = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TriangularMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _primary = MovingAverageSmootherFactory.Create(maType, resolved);
        _secondary = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TriangularMovingAverage;

    public void Reset()
    {
        _primary.Reset();
        _secondary.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var first = _primary.Next(value, isFinal);
        var tma = _secondary.Next(first, isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tma", tma }
            };
        }

        return new StreamingIndicatorStateResult(tma, outputs);
    }

    public void Dispose()
    {
        _primary.Dispose();
        _secondary.Dispose();
    }
}

public sealed class HullMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _longSmoother;
    private readonly IMovingAverageSmoother _shortSmoother;
    private readonly IMovingAverageSmoother _finalSmoother;
    private readonly StreamingInputResolver _input;

    public HullMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var length2 = MathHelper.MinOrMax((int)Math.Round((double)resolved / 2));
        var sqrtLength = MathHelper.MinOrMax((int)Math.Round(MathHelper.Sqrt(resolved)));
        _longSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _shortSmoother = MovingAverageSmootherFactory.Create(maType, length2);
        _finalSmoother = MovingAverageSmootherFactory.Create(maType, sqrtLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HullMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var length2 = MathHelper.MinOrMax((int)Math.Round((double)resolved / 2));
        var sqrtLength = MathHelper.MinOrMax((int)Math.Round(MathHelper.Sqrt(resolved)));
        _longSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _shortSmoother = MovingAverageSmootherFactory.Create(maType, length2);
        _finalSmoother = MovingAverageSmootherFactory.Create(maType, sqrtLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HullMovingAverage;

    public void Reset()
    {
        _longSmoother.Reset();
        _shortSmoother.Reset();
        _finalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wmaLong = _longSmoother.Next(value, isFinal);
        var wmaShort = _shortSmoother.Next(value, isFinal);
        var total = (2 * wmaShort) - wmaLong;
        var hma = _finalSmoother.Next(total, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Hma", hma }
            };
        }

        return new StreamingIndicatorStateResult(hma, outputs);
    }

    public void Dispose()
    {
        _longSmoother.Dispose();
        _shortSmoother.Dispose();
        _finalSmoother.Dispose();
    }
}


public sealed class AveragePriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public AveragePriceState()
    {
        _input = new StreamingInputResolver(InputName.AveragePrice, null);
    }

    public AveragePriceState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.AveragePrice, selector);
    }

    public IndicatorName Name => IndicatorName.AveragePrice;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var averagePrice = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "AveragePrice", averagePrice }
            };
        }

        return new StreamingIndicatorStateResult(averagePrice, outputs);
    }
}

public sealed class FullTypicalPriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public FullTypicalPriceState()
    {
        _input = new StreamingInputResolver(InputName.FullTypicalPrice, null);
    }

    public FullTypicalPriceState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.FullTypicalPrice, selector);
    }

    public IndicatorName Name => IndicatorName.FullTypicalPrice;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var fullTypicalPrice = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "FullTp", fullTypicalPrice }
            };
        }

        return new StreamingIndicatorStateResult(fullTypicalPrice, outputs);
    }
}

public sealed class MedianPriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public MedianPriceState()
    {
        _input = new StreamingInputResolver(InputName.MedianPrice, null);
    }

    public MedianPriceState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.MedianPrice, selector);
    }

    public IndicatorName Name => IndicatorName.MedianPrice;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var medianPrice = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "MedianPrice", medianPrice }
            };
        }

        return new StreamingIndicatorStateResult(medianPrice, outputs);
    }
}

public sealed class TypicalPriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public TypicalPriceState()
    {
        _input = new StreamingInputResolver(InputName.TypicalPrice, null);
    }

    public TypicalPriceState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.TypicalPrice, selector);
    }

    public IndicatorName Name => IndicatorName.TypicalPrice;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var typicalPrice = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tp", typicalPrice }
            };
        }

        return new StreamingIndicatorStateResult(typicalPrice, outputs);
    }
}

public sealed class WeightedCloseState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public WeightedCloseState()
    {
        _input = new StreamingInputResolver(InputName.WeightedClose, null);
    }

    public WeightedCloseState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.WeightedClose, selector);
    }

    public IndicatorName Name => IndicatorName.WeightedClose;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var weightedClose = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "WeightedClose", weightedClose }
            };
        }

        return new StreamingIndicatorStateResult(weightedClose, outputs);
    }
}

public sealed class MidpointState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;

    public MidpointState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MidpointState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Midpoint;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int maxCount;
        int minCount;
        var highest = isFinal ? _maxWindow.Add(value, out maxCount) : _maxWindow.Preview(value, out maxCount);
        var lowest = isFinal ? _minWindow.Add(value, out minCount) : _minWindow.Preview(value, out minCount);
        var midpoint = (highest + lowest) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "HCLC2", midpoint }
            };
        }

        return new StreamingIndicatorStateResult(midpoint, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

public sealed class MidpriceState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;

    public MidpriceState(int length = 14)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
    }

    public IndicatorName Name => IndicatorName.Midprice;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        int highCount;
        int lowCount;
        var highest = isFinal ? _highWindow.Add(bar.High, out highCount) : _highWindow.Preview(bar.High, out highCount);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out lowCount) : _lowWindow.Preview(bar.Low, out lowCount);
        var midprice = (highest + lowest) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "HHLL2", midprice }
            };
        }

        return new StreamingIndicatorStateResult(midprice, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class AverageTrueRangeChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _middleSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;
    private double _prevValue;
    private bool _hasPrev;

    public AverageTrueRangeChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double mult = 2.5, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _mult = mult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public AverageTrueRangeChannelState(MovingAvgType maType, int length, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AverageTrueRangeChannel;

    public void Reset()
    {
        _atrSmoother.Reset();
        _middleSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);
        var middle = _middleSmoother.Next(value, isFinal);
        var upper = Math.Round(value + (atr * _mult));
        var lower = Math.Round(value - (atr * _mult));

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _middleSmoother.Dispose();
    }
}

public sealed class UniChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly double _ubFac;
    private readonly double _lbFac;
    private readonly bool _type1;

    public UniChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10, double ubFac = 0.02,
        double lbFac = 0.02, bool type1 = false, InputName inputName = InputName.Close)
    {
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _ubFac = ubFac;
        _lbFac = lbFac;
        _type1 = type1;
        _input = new StreamingInputResolver(inputName, null);
    }

    public UniChannelState(MovingAvgType maType, int length, double ubFac, double lbFac, bool type1,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _ubFac = ubFac;
        _lbFac = lbFac;
        _type1 = type1;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.UniChannel;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _smoother.Next(value, isFinal);
        var upper = _type1 ? middle + _ubFac : middle + (middle * _ubFac);
        var lower = _type1 ? middle - _lbFac : middle - (middle * _lbFac);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class PriceHeadleyAccelerationBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _upperSmoother;
    private readonly IMovingAverageSmoother _lowerSmoother;
    private readonly IMovingAverageSmoother _middleSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _factor;

    public PriceHeadleyAccelerationBandsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        double factor = 0.001, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _upperSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowerSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _factor = factor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceHeadleyAccelerationBandsState(MovingAvgType maType, int length, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _upperSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowerSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _factor = factor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceHeadleyAccelerationBands;

    public void Reset()
    {
        _upperSmoother.Reset();
        _lowerSmoother.Reset();
        _middleSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _middleSmoother.Next(value, isFinal);
        var mult = bar.High + bar.Low != 0
            ? 4 * _factor * 1000 * (bar.High - bar.Low) / (bar.High + bar.Low)
            : 0;
        var outerUpper = bar.High * (1 + mult);
        var outerLower = bar.Low * (1 - mult);
        var upper = _upperSmoother.Next(outerUpper, isFinal);
        var lower = _lowerSmoother.Next(outerLower, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _upperSmoother.Dispose();
        _lowerSmoother.Dispose();
        _middleSmoother.Dispose();
    }
}

public sealed class PseudoPolynomialChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _morph;
    private readonly IMovingAverageSmoother _kSmoother;
    private readonly PooledRingBuffer<double> _kWindow;
    private readonly StreamingInputResolver _input;
    private double _yk1Sum;
    private int _count;

    public PseudoPolynomialChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double morph = 0.9, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _morph = morph;
        _kSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _kWindow = new PooledRingBuffer<double>(_length * 2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PseudoPolynomialChannelState(MovingAvgType maType, int length, double morph,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _morph = morph;
        _kSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _kWindow = new PooledRingBuffer<double>(_length * 2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PseudoPolynomialChannel;

    public void Reset()
    {
        _kSmoother.Reset();
        _kWindow.Clear();
        _yk1Sum = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var index = (double)_count;
        var prevK = _kWindow.Count >= _length ? _kWindow[_kWindow.Count - _length] : value;
        var prevK2 = _kWindow.Count >= _length * 2 ? _kWindow[_kWindow.Count - (_length * 2)] : value;
        var prevIndex = index >= _length ? index - _length : 0;
        var prevIndex2 = index >= _length * 2 ? index - (_length * 2) : 0;
        var ky = (_morph * prevK) + ((1 - _morph) * value);
        var ky2 = (_morph * prevK2) + ((1 - _morph) * value);
        var denom = prevIndex2 - prevIndex;
        var k = denom != 0 ? ky + ((index - prevIndex) / denom * (ky2 - ky)) : 0;
        var k1 = _kSmoother.Next(k, isFinal);
        var yk1 = Math.Abs(value - k1);
        var yk1Sum = _yk1Sum + yk1;
        var er = index != 0 ? yk1Sum / index : 0;
        var upper = k1 + er;
        var lower = k1 - er;
        var middle = (upper + lower) / 2;

        if (isFinal)
        {
            _kWindow.TryAdd(k, out _);
            _yk1Sum = yk1Sum;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _kSmoother.Dispose();
        _kWindow.Dispose();
    }
}

public sealed class ProjectedSupportAndResistanceState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public ProjectedSupportAndResistanceState(int length = 25, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ProjectedSupportAndResistanceState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ProjectedSupportAndResistance;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var range = highest - lowest;
        var support1 = lowest - (0.25 * range);
        var support2 = lowest - (0.5 * range);
        var resistance1 = highest + (0.25 * range);
        var resistance2 = highest + (0.5 * range);
        var middle = (support1 + support2 + resistance1 + resistance2) / 4;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(5)
            {
                { "Support1", support1 },
                { "Support2", support2 },
                { "Resistance1", resistance1 },
                { "Resistance2", resistance2 },
                { "MiddleBand", middle }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class ProjectionBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly ProjectionBandsCalculator _calculator;
    private readonly StreamingInputResolver _input;

    public ProjectionBandsState(int length = 14, InputName inputName = InputName.Close)
    {
        _calculator = new ProjectionBandsCalculator(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ProjectionBandsState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _calculator = new ProjectionBandsCalculator(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ProjectionBands;

    public void Reset()
    {
        _calculator.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var bands = _calculator.Update(bar.High, bar.Low, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", bands.Upper },
                { "MiddleBand", bands.Middle },
                { "LowerBand", bands.Lower }
            };
        }

        return new StreamingIndicatorStateResult(bands.Middle, outputs);
    }

    public void Dispose()
    {
        _calculator.Dispose();
    }
}

public sealed class ProjectionOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly ProjectionBandsCalculator _bands;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public ProjectionOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14,
        int smoothLength = 4, InputName inputName = InputName.Close)
    {
        _bands = new ProjectionBandsCalculator(length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ProjectionOscillatorState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _bands = new ProjectionBandsCalculator(length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ProjectionOscillator;

    public void Reset()
    {
        _bands.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var bands = _bands.Update(bar.High, bar.Low, isFinal);
        var range = bands.Upper - bands.Lower;
        var pbo = range != 0 ? 100 * (value - bands.Lower) / range : 0;
        var signal = _signalSmoother.Next(pbo, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Pbo", pbo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(pbo, outputs);
    }

    public void Dispose()
    {
        _bands.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ProjectionBandwidthState : IStreamingIndicatorState, IDisposable
{
    private readonly ProjectionBandsCalculator _bands;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public ProjectionBandwidthState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        _bands = new ProjectionBandsCalculator(resolvedLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ProjectionBandwidthState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        _bands = new ProjectionBandsCalculator(resolvedLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ProjectionBandwidth;

    public void Reset()
    {
        _bands.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var bands = _bands.Update(bar.High, bar.Low, isFinal);
        var sum = bands.Upper + bands.Lower;
        var pbw = sum != 0 ? 200 * (bands.Upper - bands.Lower) / sum : 0;
        var signal = _signalSmoother.Next(pbw, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Pbw", pbw },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(pbw, outputs);
    }

    public void Dispose()
    {
        _bands.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class RootMovingAverageSquaredErrorBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _meanSmoother;
    private readonly IMovingAverageSmoother _powSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDevFactor;

    public RootMovingAverageSquaredErrorBandsState(double stdDevFactor = 1,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _powSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public RootMovingAverageSquaredErrorBandsState(double stdDevFactor, MovingAvgType maType, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _powSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RootMovingAverageSquaredErrorBands;

    public void Reset()
    {
        _meanSmoother.Reset();
        _powSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _meanSmoother.Next(value, isFinal);
        var pow = (value - middle) * (value - middle);
        var powAvg = _powSmoother.Next(pow, isFinal);
        var rmaseDev = MathHelper.Sqrt(powAvg);
        var upper = middle + (rmaseDev * _stdDevFactor);
        var lower = middle - (rmaseDev * _stdDevFactor);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _meanSmoother.Dispose();
        _powSmoother.Dispose();
    }
}

public sealed class MovingAverageBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly RollingWindowSum _sqSum;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;

    public MovingAverageBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 10,
        int slowLength = 50, double mult = 1, InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _sqSum = new RollingWindowSum(resolvedFast);
        _mult = mult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public MovingAverageBandsState(MovingAvgType maType, int fastLength, int slowLength, double mult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _sqSum = new RollingWindowSum(resolvedFast);
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MovingAverageBands;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _sqSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var diff = slow - fast;
        var sq = diff * diff;

        int countAfter;
        var sum = isFinal ? _sqSum.Add(sq, out countAfter) : _sqSum.Preview(sq, out countAfter);
        var dev = MathHelper.Sqrt(countAfter > 0 ? sum / countAfter : 0) * _mult;
        var upper = slow + dev;
        var lower = slow - dev;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", fast },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(fast, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _sqSum.Dispose();
    }
}

public sealed class MovingAverageSupportResistanceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly double _supportLevel;

    public MovingAverageSupportResistanceState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10,
        double factor = 2, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _supportLevel = 1 + (factor / 100);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MovingAverageSupportResistanceState(MovingAvgType maType, int length, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _supportLevel = 1 + (factor / 100);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MovingAverageSupportResistance;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _smoother.Next(value, isFinal);
        var upper = middle * _supportLevel;
        var lower = _supportLevel != 0 ? middle / _supportLevel : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class MotionToAttractionChannelsState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private double _prevB;
    private double _prevC;
    private double _prevD;
    private double _prevAMa;
    private double _prevBMa;
    private int _count;

    public MotionToAttractionChannelsState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _alpha = (double)1 / resolved;
        _input = new StreamingInputResolver(inputName, null);
    }

    public MotionToAttractionChannelsState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _alpha = (double)1 / resolved;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MotionToAttractionChannels;

    public void Reset()
    {
        _prevA = 0;
        _prevB = 0;
        _prevC = 0;
        _prevD = 0;
        _prevAMa = 0;
        _prevBMa = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevA = _count >= 1 ? _prevA : value;
        var prevB = _count >= 1 ? _prevB : value;
        var prevAMa = _count >= 1 ? _prevAMa : value;
        var prevBMa = _count >= 1 ? _prevBMa : value;
        var prevC = _prevC;
        var prevD = _prevD;

        var a = value > prevAMa ? value : prevA;
        var b = value < prevBMa ? value : prevB;
        var c = b - prevB != 0 ? prevC + _alpha : a - prevA != 0 ? 0 : prevC;
        var d = a - prevA != 0 ? prevD + _alpha : b - prevB != 0 ? 0 : prevD;

        var avg = (a + b) / 2;
        var aMa = (c * avg) + ((1 - c) * a);
        var bMa = (d * avg) + ((1 - d) * b);
        var avgMa = (aMa + bMa) / 2;

        if (isFinal)
        {
            _prevA = a;
            _prevB = b;
            _prevC = c;
            _prevD = d;
            _prevAMa = aMa;
            _prevBMa = bMa;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", aMa },
                { "MiddleBand", avgMa },
                { "LowerBand", bMa }
            };
        }

        return new StreamingIndicatorStateResult(avgMa, outputs);
    }
}

public sealed class MeanAbsoluteErrorBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _meanSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDevFactor;
    private double _devSum;
    private int _count;

    public MeanAbsoluteErrorBandsState(double stdDevFactor = 1,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public MeanAbsoluteErrorBandsState(double stdDevFactor, MovingAvgType maType, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MeanAbsoluteErrorBands;

    public void Reset()
    {
        _meanSmoother.Reset();
        _devSum = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _meanSmoother.Next(value, isFinal);
        var dev = Math.Abs(value - middle);
        var sum = _devSum + dev;
        var maeDev = _count != 0 ? sum / _count : 0;
        var upper = middle + (maeDev * _stdDevFactor);
        var lower = middle - (maeDev * _stdDevFactor);

        if (isFinal)
        {
            _devSum = sum;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _meanSmoother.Dispose();
    }
}

public sealed class MeanAbsoluteDeviationBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _meanSmoother;
    private readonly IMovingAverageSmoother _varianceSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDevFactor;

    public MeanAbsoluteDeviationBandsState(double stdDevFactor = 2,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public MeanAbsoluteDeviationBandsState(double stdDevFactor, MovingAvgType maType, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MeanAbsoluteDeviationBands;

    public void Reset()
    {
        _meanSmoother.Reset();
        _varianceSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _meanSmoother.Next(value, isFinal);
        var deviation = value - middle;
        var variance = _varianceSmoother.Next(deviation * deviation, isFinal);
        var stdDev = MathHelper.Sqrt(variance);
        var upper = middle + (stdDev * _stdDevFactor);
        var lower = middle - (stdDev * _stdDevFactor);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _meanSmoother.Dispose();
        _varianceSmoother.Dispose();
    }
}

public sealed class MovingAverageDisplacedEnvelopeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly IMovingAverageSmoother _emaSmoother;
    private readonly PooledRingBuffer<double> _emaWindow;
    private readonly StreamingInputResolver _input;
    private readonly double _pct;

    public MovingAverageDisplacedEnvelopeState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 9, int length2 = 13, double pct = 0.5, InputName inputName = InputName.Close)
    {
        _length2 = Math.Max(1, length2);
        _emaSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _emaWindow = new PooledRingBuffer<double>(_length2);
        _pct = pct;
        _input = new StreamingInputResolver(inputName, null);
    }

    public MovingAverageDisplacedEnvelopeState(MovingAvgType maType, int length1, int length2, double pct,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length2 = Math.Max(1, length2);
        _emaSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _emaWindow = new PooledRingBuffer<double>(_length2);
        _pct = pct;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MovingAverageDisplacedEnvelope;

    public void Reset()
    {
        _emaSmoother.Reset();
        _emaWindow.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _emaSmoother.Next(value, isFinal);
        var prevEma = _emaWindow.Count >= _length2 ? _emaWindow[0] : 0;
        var upper = prevEma * ((100 + _pct) / 100);
        var lower = prevEma * ((100 - _pct) / 100);
        var middle = (upper + lower) / 2;

        if (isFinal)
        {
            _emaWindow.TryAdd(ema, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _emaSmoother.Dispose();
        _emaWindow.Dispose();
    }
}

public sealed class VerticalHorizontalFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly RollingWindowSum _changeSum;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public VerticalHorizontalFilterState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 18,
        int signalLength = 6, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _changeSum = new RollingWindowSum(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public VerticalHorizontalFilterState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _changeSum = new RollingWindowSum(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VerticalHorizontalFilter;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _changeSum.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);
        var lowest = isFinal ? _minWindow.Add(value, out _) : _minWindow.Preview(value, out _);
        var numerator = Math.Abs(highest - lowest);
        var priceChange = _hasPrev ? Math.Abs(value - _prevValue) : 0;

        int countAfter;
        var sum = isFinal ? _changeSum.Add(priceChange, out countAfter) : _changeSum.Preview(priceChange, out countAfter);
        var vhf = sum != 0 ? numerator / sum : 0;
        var signal = _signalSmoother.Next(vhf, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Vhf", vhf },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(vhf, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _changeSum.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class SigmaSpikesState : IStreamingIndicatorState, IDisposable    
{
    private readonly IMovingAverageSmoother _meanSmoother;
    private readonly IMovingAverageSmoother _varianceSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;
    private double _prevStd;
    private bool _hasStd;

    public SigmaSpikesState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SigmaSpikesState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SigmaSpikes;

    public void Reset()
    {
        _meanSmoother.Reset();
        _varianceSmoother.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
        _prevStd = 0;
        _hasStd = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var ret = prevValue != 0 ? (value / prevValue) - 1 : 0;

        var mean = _meanSmoother.Next(ret, isFinal);
        var deviation = ret - mean;
        var variance = _varianceSmoother.Next(deviation * deviation, isFinal);
        var stdDev = MathHelper.Sqrt(variance);

        var sigma = _hasStd && _prevStd != 0 ? ret / _prevStd : 0;
        var signal = _signalSmoother.Next(sigma, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
            _prevStd = stdDev;
            _hasStd = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ss", sigma },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(sigma, outputs);
    }

    public void Dispose()
    {
        _meanSmoother.Dispose();
        _varianceSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class StatisticalVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _closeMax;
    private readonly RollingWindowMin _closeMin;
    private readonly RollingWindowMax _highMax;
    private readonly RollingWindowMin _lowMin;
    private readonly IMovingAverageSmoother _volSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _annualSqrt;

    public StatisticalVolatilityState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 30,
        int length2 = 253, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length1);
        _closeMax = new RollingWindowMax(resolved);
        _closeMin = new RollingWindowMin(resolved);
        _highMax = new RollingWindowMax(resolved);
        _lowMin = new RollingWindowMin(resolved);
        _volSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _annualSqrt = Math.Sqrt((double)length2 / resolved);
    }

    public StatisticalVolatilityState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length1);
        _closeMax = new RollingWindowMax(resolved);
        _closeMin = new RollingWindowMin(resolved);
        _highMax = new RollingWindowMax(resolved);
        _lowMin = new RollingWindowMin(resolved);
        _volSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _annualSqrt = Math.Sqrt((double)length2 / resolved);
    }

    public IndicatorName Name => IndicatorName.StatisticalVolatility;

    public void Reset()
    {
        _closeMax.Reset();
        _closeMin.Reset();
        _highMax.Reset();
        _lowMin.Reset();
        _volSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var maxC = isFinal ? _closeMax.Add(value, out _) : _closeMax.Preview(value, out _);
        var minC = isFinal ? _closeMin.Add(value, out _) : _closeMin.Preview(value, out _);
        var maxH = isFinal ? _highMax.Add(bar.High, out _) : _highMax.Preview(bar.High, out _);
        var minL = isFinal ? _lowMin.Add(bar.Low, out _) : _lowMin.Preview(bar.Low, out _);

        var cLog = minC != 0 ? Math.Log(maxC / minC) : 0;
        var hlLog = minL != 0 ? Math.Log(maxH / minL) : 0;
        var vol = MathHelper.MinOrMax(((0.6 * cLog * _annualSqrt) + (0.6 * hlLog * _annualSqrt)) * 0.5, 2.99, 0);
        var signal = _volSmoother.Next(vol, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Sv", vol },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(vol, outputs);
    }

    public void Dispose()
    {
        _closeMax.Dispose();
        _closeMin.Dispose();
        _highMax.Dispose();
        _lowMin.Dispose();
        _volSmoother.Dispose();
    }
}

public sealed class VolatilitySwitchIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _meanSmoother;
    private readonly IMovingAverageSmoother _varianceSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public VolatilitySwitchIndicatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolatilitySwitchIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VolatilitySwitchIndicator;

    public void Reset()
    {
        _meanSmoother.Reset();
        _varianceSmoother.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var rocSma = (value + prevValue) / 2;
        var dr = _hasPrev && rocSma != 0 ? (value - prevValue) / rocSma : 0;

        var mean = _meanSmoother.Next(dr, isFinal);
        var deviation = dr - mean;
        var variance = _varianceSmoother.Next(deviation * deviation, isFinal);
        var stdDev = MathHelper.Sqrt(variance);
        var vswitch = _signalSmoother.Next(stdDev, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vsi", vswitch }
            };
        }

        return new StreamingIndicatorStateResult(vswitch, outputs);
    }

    public void Dispose()
    {
        _meanSmoother.Dispose();
        _varianceSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ExtendedRecursiveBandsState : IStreamingIndicatorState
{
    private readonly double _sc;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private double _prevB;
    private bool _hasPrev;

    public ExtendedRecursiveBandsState(int length = 100, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _sc = (double)2 / (resolved + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ExtendedRecursiveBandsState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _sc = (double)2 / (resolved + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ExtendedRecursiveBands;

    public void Reset()
    {
        _prevA = 0;
        _prevB = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevA = _hasPrev ? _prevA : value;
        var prevB = _hasPrev ? _prevB : value;

        var a = Math.Max(prevA, value) - (_sc * Math.Abs(value - prevA));
        var b = Math.Min(prevB, value) + (_sc * Math.Abs(value - prevB));
        var middle = (a + b) / 2;

        if (isFinal)
        {
            _prevA = a;
            _prevB = b;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", a },
                { "MiddleBand", middle },
                { "LowerBand", b }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }
}

public sealed class StollerAverageRangeChannelsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _middleSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _atrMult;
    private double _prevValue;
    private bool _hasPrev;

    public StollerAverageRangeChannelsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double atrMult = 2, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrMult = atrMult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public StollerAverageRangeChannelsState(MovingAvgType maType, int length, double atrMult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrMult = atrMult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StollerAverageRangeChannels;

    public void Reset()
    {
        _atrSmoother.Reset();
        _middleSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);
        var middle = _middleSmoother.Next(value, isFinal);
        var upper = middle + (atr * _atrMult);
        var lower = middle - (atr * _atrMult);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _middleSmoother.Dispose();
    }
}

public sealed class Dema2LinesState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _fastDema;
    private readonly IMovingAverageSmoother _slowDema;
    private readonly StreamingInputResolver _input;

    public Dema2LinesState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 10,
        int slowLength = 40, InputName inputName = InputName.Close)
    {
        var fast = Math.Max(1, fastLength);
        var slow = Math.Max(1, slowLength);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, fast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, slow);
        _fastDema = MovingAverageSmootherFactory.Create(maType, fast);
        _slowDema = MovingAverageSmootherFactory.Create(maType, slow);
        _input = new StreamingInputResolver(inputName, null);
    }

    public Dema2LinesState(MovingAvgType maType, int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var fast = Math.Max(1, fastLength);
        var slow = Math.Max(1, slowLength);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, fast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, slow);
        _fastDema = MovingAverageSmootherFactory.Create(maType, fast);
        _slowDema = MovingAverageSmootherFactory.Create(maType, slow);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Dema2Lines;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _fastDema.Reset();
        _slowDema.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var dema1 = _fastDema.Next(fast, isFinal);
        var dema2 = _slowDema.Next(slow, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dema1", dema1 },
                { "Dema2", dema2 }
            };
        }

        return new StreamingIndicatorStateResult(dema1, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _fastDema.Dispose();
        _slowDema.Dispose();
    }
}

public sealed class DynamicSupportAndResistanceState : IStreamingIndicatorState, IDisposable
{
    private readonly double _mult;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public DynamicSupportAndResistanceState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 25,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _mult = MathHelper.Sqrt(resolved);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DynamicSupportAndResistanceState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _mult = MathHelper.Sqrt(resolved);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DynamicSupportAndResistance;

    public void Reset()
    {
        _atrSmoother.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);

        int highCount;
        int lowCount;
        var highest = isFinal ? _highWindow.Add(bar.High, out highCount) : _highWindow.Preview(bar.High, out highCount);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out lowCount) : _lowWindow.Preview(bar.Low, out lowCount);

        var support = highest - (atr * _mult);
        var resistance = lowest + (atr * _mult);
        var middle = (support + resistance) / 2;

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Support", support },
                { "Resistance", resistance },
                { "MiddleBand", middle }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class DailyAveragePriceDeltaState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;

    public DailyAveragePriceDeltaState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 21)
    {
        var resolved = Math.Max(1, length);
        _highSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.DailyAveragePriceDelta;

    public void Reset()
    {
        _highSmoother.Reset();
        _lowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var highSma = _highSmoother.Next(bar.High, isFinal);
        var lowSma = _lowSmoother.Next(bar.Low, isFinal);
        var dapd = highSma - lowSma;
        var upper = bar.High + dapd;
        var lower = bar.Low - dapd;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "UpperBand", upper },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(upper, outputs);
    }

    public void Dispose()
    {
        _highSmoother.Dispose();
        _lowSmoother.Dispose();
    }
}
public sealed class PeriodicChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly RollingWindowCorrelation _corrWindow;
    private readonly StreamingInputResolver _input;
    private double _tempSum;
    private double _indexSum;
    private double _absIndexCumDiffSum;
    private double _corrSum;
    private double _sinSum;
    private double _inSinSum;
    private double _absSinCumDiffSum;
    private double _absInSinCumDiffSum;
    private double _absDiffSum;
    private double _absKDiffSum;
    private int _count;

    public PeriodicChannelState(int length1 = 500, int length2 = 2, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _corrWindow = new RollingWindowCorrelation(Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public PeriodicChannelState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _corrWindow = new RollingWindowCorrelation(Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PeriodicChannel;

    public void Reset()
    {
        _corrWindow.Reset();
        _tempSum = 0;
        _indexSum = 0;
        _absIndexCumDiffSum = 0;
        _corrSum = 0;
        _sinSum = 0;
        _inSinSum = 0;
        _absSinCumDiffSum = 0;
        _absInSinCumDiffSum = 0;
        _absDiffSum = 0;
        _absKDiffSum = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var index = (double)_count;
        var tempSum = _tempSum + value;
        var indexSum = _indexSum + index;
        var indexCum = index != 0 ? indexSum / index : 0;
        var indexCumDiff = index - indexCum;
        var absIndexCumDiff = Math.Abs(index - indexCum);
        var absIndexCumDiffSum = _absIndexCumDiffSum + absIndexCumDiff;
        var absIndexCum = index != 0 ? absIndexCumDiffSum / index : 0;
        var z = absIndexCum != 0 ? indexCumDiff / absIndexCum : 0;

        var corr = isFinal
            ? _corrWindow.Add(index, value, out _)
            : _corrWindow.Preview(index, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;
        var corrSum = _corrSum + corr;

        var s = index * Math.Sign(corrSum);
        var sin = Math.Sin(s / _length1);
        var sinSum = _sinSum + sin;
        var inSin = -sin;
        var inSinSum = _inSinSum + inSin;

        var sinCum = index != 0 ? sinSum / index : 0;
        var inSinCum = index != 0 ? inSinSum / index : 0;
        var sinCumDiff = sin - sinCum;
        var inSinCumDiff = inSin - inSinCum;

        var absSinCumDiff = Math.Abs(sin - sinCum);
        var absSinCumDiffSum = _absSinCumDiffSum + absSinCumDiff;
        var absSinCum = index != 0 ? absSinCumDiffSum / index : 0;
        var absInSinCumDiff = Math.Abs(inSin - inSinCum);
        var absInSinCumDiffSum = _absInSinCumDiffSum + absInSinCumDiff;
        var absInSinCum = index != 0 ? absInSinCumDiffSum / index : 0;
        var zs = absSinCum != 0 ? sinCumDiff / absSinCum : 0;
        var inZs = absInSinCum != 0 ? inSinCumDiff / absInSinCum : 0;
        var cum = index != 0 ? tempSum / index : 0;

        var absDiff = Math.Abs(value - cum);
        var absDiffSum = _absDiffSum + absDiff;
        var absDiffCum = index != 0 ? absDiffSum / index : 0;
        var k = cum + ((z + zs) * absDiffCum);

        var absKDiff = Math.Abs(value - k);
        var absKDiffSum = _absKDiffSum + absKDiff;
        var os = index != 0 ? absKDiffSum / index : 0;

        var ap = k + os;
        var bp = ap + os;
        var cp = bp + os;
        var al = k - os;
        var bl = al - os;
        var cl = bl - os;

        if (isFinal)
        {
            _tempSum = tempSum;
            _indexSum = indexSum;
            _absIndexCumDiffSum = absIndexCumDiffSum;
            _corrSum = corrSum;
            _sinSum = sinSum;
            _inSinSum = inSinSum;
            _absSinCumDiffSum = absSinCumDiffSum;
            _absInSinCumDiffSum = absInSinCumDiffSum;
            _absDiffSum = absDiffSum;
            _absKDiffSum = absKDiffSum;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(8)
            {
                { "K", k },
                { "Os", os },
                { "Ap", ap },
                { "Bp", bp },
                { "Cp", cp },
                { "Al", al },
                { "Bl", bl },
                { "Cl", cl }
            };
        }

        return new StreamingIndicatorStateResult(k, outputs);
    }

    public void Dispose()
    {
        _corrWindow.Dispose();
    }
}

public sealed class PriceLineChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevA1;
    private double _prevA2;
    private double _prevB1;
    private double _prevB2;
    private double _prevSizeA;
    private double _prevSizeB;
    private double _prevSizeC;
    private double _prevValue;
    private int _count;
    private bool _hasPrev;

    public PriceLineChannelState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 100,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceLineChannelState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceLineChannel;

    public void Reset()
    {
        _atrSmoother.Reset();
        _prevA1 = 0;
        _prevA2 = 0;
        _prevB1 = 0;
        _prevB2 = 0;
        _prevSizeA = 0;
        _prevSizeB = 0;
        _prevSizeC = 0;
        _prevValue = 0;
        _count = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);

        var prevA1 = _count >= 1 ? _prevA1 : value;
        var prevB1 = _count >= 1 ? _prevB1 : value;
        var prevA2 = _count >= 2 ? _prevA2 : 0;
        var prevB2 = _count >= 2 ? _prevB2 : 0;
        var prevSizeA = _count >= 1 ? _prevSizeA : atr / _length;
        var prevSizeB = _count >= 1 ? _prevSizeB : atr / _length;
        var prevSizeC = _count >= 1 ? _prevSizeC : atr / _length;

        var sizeA = prevA1 - prevA2 > 0 ? atr : prevSizeA;
        var sizeB = prevB1 - prevB2 < 0 ? atr : prevSizeB;
        var sizeC = prevA1 - prevA2 > 0 || prevB1 - prevB2 < 0 ? atr : prevSizeC;
        var a = Math.Max(value, prevA1) - (sizeA / _length);
        var b = Math.Min(value, prevB1) + (sizeB / _length);
        var middle = (a + b) / 2;

        if (isFinal)
        {
            _prevA2 = _count >= 1 ? _prevA1 : 0;
            _prevB2 = _count >= 1 ? _prevB1 : 0;
            _prevA1 = a;
            _prevB1 = b;
            _prevSizeA = sizeA;
            _prevSizeB = sizeB;
            _prevSizeC = sizeC;
            _prevValue = value;
            _count++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", a },
                { "MiddleBand", middle },
                { "LowerBand", b }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
    }
}

public sealed class PriceCurveChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevA1;
    private double _prevA2;
    private double _prevB1;
    private double _prevB2;
    private double _prevSize;
    private double _prevValue;
    private int _count;
    private int _lastAChangeIndex;
    private int _lastBChangeIndex;
    private bool _hasPrev;

    public PriceCurveChannelState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 100,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
        _lastAChangeIndex = -1;
        _lastBChangeIndex = -1;
    }

    public PriceCurveChannelState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _lastAChangeIndex = -1;
        _lastBChangeIndex = -1;
    }

    public IndicatorName Name => IndicatorName.PriceCurveChannel;

    public void Reset()
    {
        _atrSmoother.Reset();
        _prevA1 = 0;
        _prevA2 = 0;
        _prevB1 = 0;
        _prevB2 = 0;
        _prevSize = 0;
        _prevValue = 0;
        _count = 0;
        _lastAChangeIndex = -1;
        _lastBChangeIndex = -1;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);

        var prevA1 = _count >= 1 ? _prevA1 : value;
        var prevB1 = _count >= 1 ? _prevB1 : value;
        var prevA2 = _count >= 2 ? _prevA2 : 0;
        var prevB2 = _count >= 2 ? _prevB2 : 0;
        var prevSize = _count >= 1 ? _prevSize : atr / _length;

        var size = prevA1 - prevA2 > 0 || prevB1 - prevB2 < 0 ? atr : prevSize;
        var aChg = prevA1 > prevA2 ? 1 : 0;
        var bChg = prevB1 < prevB2 ? 1 : 0;
        var lastAIndex = aChg == 1 ? _count : _lastAChangeIndex;
        var lastBIndex = bChg == 1 ? _count : _lastBChangeIndex;
        var barsSinceA = _count - lastAIndex;
        var barsSinceB = _count - lastBIndex;
        var lengthSquared = (double)_length * _length;
        var factor = lengthSquared != 0 ? size / lengthSquared : 0;
        var a = Math.Max(value, prevA1) - (factor * (barsSinceA + 1));
        var b = Math.Min(value, prevB1) + (factor * (barsSinceB + 1));
        var middle = (a + b) / 2;

        if (isFinal)
        {
            _prevA2 = _count >= 1 ? _prevA1 : 0;
            _prevB2 = _count >= 1 ? _prevB1 : 0;
            _prevA1 = a;
            _prevB1 = b;
            _prevSize = size;
            _prevValue = value;
            if (aChg == 1)
            {
                _lastAChangeIndex = _count;
            }

            if (bChg == 1)
            {
                _lastBChangeIndex = _count;
            }

            _count++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", a },
                { "MiddleBand", middle },
                { "LowerBand", b }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
    }
}

public sealed class RangeBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _stdDevFactor;
    private readonly IMovingAverageSmoother _middleSmoother;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;

    public RangeBandsState(double stdDevFactor = 1, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stdDevFactor = stdDevFactor;
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        var windowLength = Math.Max(2, _length);
        _maxWindow = new RollingWindowMax(windowLength);
        _minWindow = new RollingWindowMin(windowLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RangeBandsState(double stdDevFactor, MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stdDevFactor = stdDevFactor;
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        var windowLength = Math.Max(2, _length);
        _maxWindow = new RollingWindowMax(windowLength);
        _minWindow = new RollingWindowMin(windowLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RangeBands;

    public void Reset()
    {
        _middleSmoother.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _middleSmoother.Next(value, isFinal);
        var highest = isFinal ? _maxWindow.Add(middle, out _) : _maxWindow.Preview(middle, out _);
        var lowest = isFinal ? _minWindow.Add(middle, out _) : _minWindow.Preview(middle, out _);
        var rangeDev = highest - lowest;
        var upper = middle + (rangeDev * _stdDevFactor);
        var lower = middle - (rangeDev * _stdDevFactor);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _middleSmoother.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

public sealed class RangeIdentifierState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevUp;
    private double _prevDown;
    private bool _hasPrev;

    public RangeIdentifierState(int length = 34, InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public RangeIdentifierState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RangeIdentifier;

    public void Reset()
    {
        _prevUp = 0;
        _prevDown = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevUp = _hasPrev ? _prevUp : 0;
        var prevDown = _hasPrev ? _prevDown : 0;
        var up = value < prevUp && value > prevDown ? prevUp : bar.High;
        var down = value < prevUp && value > prevDown ? prevDown : bar.Low;
        var middle = (up + down) / 2;

        if (isFinal)
        {
            _prevUp = up;
            _prevDown = down;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", up },
                { "MiddleBand", middle },
                { "LowerBand", down }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }
}

public sealed class LinearRegressionState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _xSum;
    private readonly RollingWindowSum _ySum;
    private readonly RollingWindowSum _xySum;
    private readonly RollingWindowSum _x2Sum;
    private readonly StreamingInputResolver _input;
    private int _index;

    public LinearRegressionState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _xSum = new RollingWindowSum(_length);
        _ySum = new RollingWindowSum(_length);
        _xySum = new RollingWindowSum(_length);
        _x2Sum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public LinearRegressionState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _xSum = new RollingWindowSum(_length);
        _ySum = new RollingWindowSum(_length);
        _xySum = new RollingWindowSum(_length);
        _x2Sum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LinearRegression;

    public void Reset()
    {
        _xSum.Reset();
        _ySum.Reset();
        _xySum.Reset();
        _x2Sum.Reset();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var x = (double)_index;

        var sumX = isFinal ? _xSum.Add(x, out _) : _xSum.Preview(x, out _);
        var sumY = isFinal ? _ySum.Add(value, out _) : _ySum.Preview(value, out _);
        var sumXY = isFinal ? _xySum.Add(x * value, out _) : _xySum.Preview(x * value, out _);
        var sumX2 = isFinal ? _x2Sum.Add(x * x, out _) : _x2Sum.Preview(x * x, out _);

        var top = (_length * sumXY) - (sumX * sumY);
        var bottom = (_length * sumX2) - (sumX * sumX);
        var slope = bottom != 0 ? top / bottom : 0;
        var intercept = _length != 0 ? (sumY - (slope * sumX)) / _length : 0;
        var predictedToday = intercept + (slope * x);
        var predictedTomorrow = intercept + (slope * (x + 1));

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "LinearRegression", predictedToday },
                { "PredictedTomorrow", predictedTomorrow },
                { "Slope", slope },
                { "Intercept", intercept }
            };
        }

        return new StreamingIndicatorStateResult(predictedToday, outputs);
    }

    public void Dispose()
    {
        _xSum.Dispose();
        _ySum.Dispose();
        _xySum.Dispose();
        _x2Sum.Dispose();
    }
}

public sealed class StandardDeviationChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDevState;
    private readonly LinearRegressionState _regressionState;
    private readonly double _stdDevMult;
    private double _regressionInput;

    public StandardDeviationChannelState(int length = 40, double stdDevMult = 2, InputName inputName = InputName.Close)
    {
        _stdDevState = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, length, inputName);
        _regressionState = new LinearRegressionState(length, _ => _regressionInput);
        _stdDevMult = stdDevMult;
    }

    public StandardDeviationChannelState(int length, double stdDevMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _stdDevState = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, length, selector);
        _regressionState = new LinearRegressionState(length, _ => _regressionInput);
        _stdDevMult = stdDevMult;
    }

    public IndicatorName Name => IndicatorName.StandardDeviationChannel;

    public void Reset()
    {
        _stdDevState.Reset();
        _regressionState.Reset();
        _regressionInput = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var stdDev = _stdDevState.Update(bar, isFinal, includeOutputs: false).Value;
        _regressionInput = stdDev;
        var middle = _regressionState.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = middle + (stdDev * _stdDevMult);
        var lower = middle - (stdDev * _stdDevMult);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _stdDevState.Dispose();
        _regressionState.Dispose();
    }
}

public sealed class TimeSeriesForecastState : IStreamingIndicatorState, IDisposable
{
    private readonly LinearRegressionState _regressionState;
    private readonly StreamingInputResolver _input;
    private double _absDiffSum;
    private int _index;

    public TimeSeriesForecastState(int length = 500, InputName inputName = InputName.Close)
    {
        _regressionState = new LinearRegressionState(length, inputName);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TimeSeriesForecastState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _regressionState = new LinearRegressionState(length, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TimeSeriesForecast;

    public void Reset()
    {
        _regressionState.Reset();
        _absDiffSum = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _regressionState.Update(bar, isFinal, includeOutputs: false).Value;
        var absDiff = Math.Abs(value - middle);
        var absDiffSum = _absDiffSum + absDiff;
        var e = _index != 0 ? absDiffSum / _index : 0;
        var upper = middle + e;
        var lower = middle - e;

        if (isFinal)
        {
            _absDiffSum = absDiffSum;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _regressionState.Dispose();
    }
}

public sealed class SmartEnvelopeState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly double _factor;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevA;
    private double _prevB;
    private double _prevASignal;
    private double _prevBSignal;
    private bool _hasPrev;

    public SmartEnvelopeState(int length = 14, double factor = 1, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _factor = factor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public SmartEnvelopeState(int length, double factor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _factor = factor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SmartEnvelope;

    public void Reset()
    {
        _prevValue = 0;
        _prevA = 0;
        _prevB = 0;
        _prevASignal = 0;
        _prevBSignal = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevA = _hasPrev ? _prevA : value;
        var prevB = _hasPrev ? _prevB : value;
        var prevASignal = _hasPrev ? _prevASignal : 0;
        var prevBSignal = _hasPrev ? _prevBSignal : 0;
        var diff = _hasPrev ? Math.Abs(value - prevValue) : 0;
        var a = Math.Max(value, prevA) - (Math.Min(Math.Abs(value - prevA), diff) / _length * prevASignal);
        var b = Math.Min(value, prevB) + (Math.Min(Math.Abs(value - prevB), diff) / _length * prevBSignal);
        var aSignal = b < prevB ? -_factor : _factor;
        var bSignal = a > prevA ? -_factor : _factor;
        var avg = (a + b) / 2;

        if (isFinal)
        {
            _prevValue = value;
            _prevA = a;
            _prevB = b;
            _prevASignal = aSignal;
            _prevBSignal = bSignal;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", a },
                { "MiddleBand", avg },
                { "LowerBand", b }
            };
        }

        return new StreamingIndicatorStateResult(avg, outputs);
    }
}

public sealed class SupportResistanceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevSma;
    private double _prevRes;
    private double _prevSupp;
    private bool _hasPrev;

    public SupportResistanceState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SupportResistanceState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SupportResistance;

    public void Reset()
    {
        _smoother.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevValue = 0;
        _prevSma = 0;
        _prevRes = 0;
        _prevSupp = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var sma = _smoother.Next(value, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevSma = _hasPrev ? _prevSma : 0;
        var crossAbove = prevValue < prevSma && value >= prevSma;
        var crossBelow = prevValue > prevSma && value <= prevSma;
        var res = crossBelow ? highest : _hasPrev ? _prevRes : highest;
        var supp = crossAbove ? lowest : _hasPrev ? _prevSupp : lowest;

        if (isFinal)
        {
            _prevValue = value;
            _prevSma = sma;
            _prevRes = res;
            _prevSupp = supp;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Support", supp },
                { "Resistance", res }
            };
        }

        return new StreamingIndicatorStateResult(supp, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class StationaryExtrapolatedLevelsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _smoother;
    private readonly RollingWindowMax _extMax1;
    private readonly RollingWindowMin _extMin1;
    private readonly RollingWindowMax _extMax2;
    private readonly RollingWindowMin _extMin2;
    private readonly PooledRingBuffer<double> _yBuffer;
    private readonly StreamingInputResolver _input;
    private int _index;

    public StationaryExtrapolatedLevelsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _extMax1 = new RollingWindowMax(_length);
        _extMin1 = new RollingWindowMin(_length);
        _extMax2 = new RollingWindowMax(_length);
        _extMin2 = new RollingWindowMin(_length);
        _yBuffer = new PooledRingBuffer<double>(_length * 2 + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public StationaryExtrapolatedLevelsState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _extMax1 = new RollingWindowMax(_length);
        _extMin1 = new RollingWindowMin(_length);
        _extMax2 = new RollingWindowMax(_length);
        _extMin2 = new RollingWindowMin(_length);
        _yBuffer = new PooledRingBuffer<double>(_length * 2 + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StationaryExtrapolatedLevels;

    public void Reset()
    {
        _smoother.Reset();
        _extMax1.Reset();
        _extMin1.Reset();
        _extMax2.Reset();
        _extMin2.Reset();
        _yBuffer.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smoother.Next(value, isFinal);
        var y = value - sma;
        var i = _index;
        var priorY = i >= _length ? _yBuffer[_yBuffer.Count - _length] : 0;
        var priorY2 = i >= _length * 2 ? _yBuffer[_yBuffer.Count - (_length * 2)] : 0;
        var priorX = i >= _length ? i - _length : 0;
        var priorX2 = i >= _length * 2 ? i - (_length * 2) : 0;
        var x = (double)i;
        var ext = priorX2 - priorX != 0 && priorY2 - priorY != 0
            ? (priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY))) / 2
            : 0;

        var highest1 = isFinal ? _extMax1.Add(ext, out _) : _extMax1.Preview(ext, out _);
        var lowest1 = isFinal ? _extMin1.Add(ext, out _) : _extMin1.Preview(ext, out _);
        var upper = isFinal ? _extMax2.Add(highest1, out _) : _extMax2.Preview(highest1, out _);
        var lower = isFinal ? _extMin2.Add(lowest1, out _) : _extMin2.Preview(lowest1, out _);

        if (isFinal)
        {
            _yBuffer.TryAdd(y, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", y },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(y, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _extMax1.Dispose();
        _extMin1.Dispose();
        _extMax2.Dispose();
        _extMin2.Dispose();
        _yBuffer.Dispose();
    }
}

public sealed class ScalpersChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smaSmoother;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevSma;
    private bool _hasPrev;

    public ScalpersChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 15,
        int length2 = 20, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ScalpersChannelState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ScalpersChannel;

    public void Reset()
    {
        _smaSmoother.Reset();
        _atrSmoother.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevSma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smaSmoother.Next(value, isFinal);
        // Match batch behavior: ATR uses the prior SMA values as the close series.
        var prevSma = _hasPrev ? _prevSma : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevSma);
        var atr = _atrSmoother.Next(tr, isFinal);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var scalper = Math.PI * atr > 0 ? sma - Math.Log(Math.PI * atr) : sma;

        if (isFinal)
        {
            _prevSma = sma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", highest },
                { "MiddleBand", scalper },
                { "LowerBand", lowest }
            };
        }

        return new StreamingIndicatorStateResult(scalper, outputs);
    }

    public void Dispose()
    {
        _smaSmoother.Dispose();
        _atrSmoother.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class SmoothedVolatilityBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _maSmoother;
    private readonly IMovingAverageSmoother _middleSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _deviation;
    private readonly double _bandAdjust;
    private double _prevClose;
    private bool _hasPrev;

    public SmoothedVolatilityBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 20,
        int length2 = 21, double deviation = 2.4, double bandAdjust = 0.9, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var atrPeriod = Math.Max(1, (resolved1 * 2) - 1);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, atrPeriod);
        _maSmoother = MovingAverageSmootherFactory.Create(maType, resolved1);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _deviation = deviation;
        _bandAdjust = bandAdjust;
        _input = new StreamingInputResolver(inputName, null);
    }

    public SmoothedVolatilityBandsState(MovingAvgType maType, int length1, int length2, double deviation, double bandAdjust,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var atrPeriod = Math.Max(1, (resolved1 * 2) - 1);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, atrPeriod);
        _maSmoother = MovingAverageSmootherFactory.Create(maType, resolved1);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _deviation = deviation;
        _bandAdjust = bandAdjust;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SmoothedVolatilityBands;

    public void Reset()
    {
        _atrSmoother.Reset();
        _maSmoother.Reset();
        _middleSmoother.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atrSmoother.Next(tr, isFinal);
        var ma = _maSmoother.Next(value, isFinal);
        var middle = _middleSmoother.Next(value, isFinal);
        var atrBuf = atr * _deviation;
        var upper = value != 0 ? ma + (ma * atrBuf / value) : ma;
        var lower = value != 0 ? ma - (ma * atrBuf * _bandAdjust / value) : ma;

        if (isFinal)
        {
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _maSmoother.Dispose();
        _middleSmoother.Dispose();
    }
}

public sealed class MovingAverageEnvelopeState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;

    public MovingAverageEnvelopeState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        double mult = 0.025, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _mult = mult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public MovingAverageEnvelopeState(MovingAvgType maType, int length, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MovingAverageEnvelope;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _smoother.Next(value, isFinal);
        var factor = middle * _mult;
        var upper = middle + factor;
        var lower = middle - factor;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class LinearChannelsState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private double _prevA2;
    private double _prevUpper;
    private double _prevLower;
    private int _count;

    public LinearChannelsState(int length = 14, double mult = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _mult = mult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public LinearChannelsState(int length, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LinearChannels;

    public void Reset()
    {
        _prevA = 0;
        _prevA2 = 0;
        _prevUpper = 0;
        _prevLower = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var s = (double)1 / _length;
        var prevA = _count >= 1 ? _prevA : value;
        var prevA2 = _count >= 2 ? _prevA2 : value;
        var x = value + ((prevA - prevA2) * _mult);
        var a = x > prevA + s ? prevA + s : x < prevA - s ? prevA - s : prevA;

        var up = a + (Math.Abs(a - prevA) * _mult);
        var dn = a - (Math.Abs(a - prevA) * _mult);
        var prevUpper = _count >= 1 ? _prevUpper : 0;
        var prevLower = _count >= 1 ? _prevLower : 0;
        var upper = up == a ? prevUpper : up;
        var lower = dn == a ? prevLower : dn;

        if (isFinal)
        {
            _prevA2 = _prevA;
            _prevA = a;
            _prevUpper = upper;
            _prevLower = lower;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "UpperBand", upper },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(upper, outputs);
    }
}

public sealed class NarrowSidewaysChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _meanSmoother;
    private readonly IMovingAverageSmoother _varianceSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDevMult;

    public NarrowSidewaysChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double stdDevMult = 3, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDevMult = stdDevMult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public NarrowSidewaysChannelState(MovingAvgType maType, int length, double stdDevMult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _meanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDevMult = stdDevMult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.NarrowSidewaysChannel;

    public void Reset()
    {
        _meanSmoother.Reset();
        _varianceSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _meanSmoother.Next(value, isFinal);
        var deviation = value - middle;
        var variance = _varianceSmoother.Next(deviation * deviation, isFinal);
        var stdDev = MathHelper.Sqrt(variance);
        var upper = middle + (stdDev * _stdDevMult);
        var lower = middle - (stdDev * _stdDevMult);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _meanSmoother.Dispose();
        _varianceSmoother.Dispose();
    }
}

public sealed class RateOfChangeBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RateOfChangeState _roc;
    private readonly IMovingAverageSmoother _middleSmoother;
    private readonly RollingWindowSum _rocSquaredSum;

    public RateOfChangeBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 12,
        int smoothLength = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _roc = new RateOfChangeState(_length, inputName);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _rocSquaredSum = new RollingWindowSum(_length);
    }

    public RateOfChangeBandsState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _roc = new RateOfChangeState(_length, selector);
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _rocSquaredSum = new RollingWindowSum(_length);
    }

    public IndicatorName Name => IndicatorName.RateOfChangeBands;

    public void Reset()
    {
        _roc.Reset();
        _middleSmoother.Reset();
        _rocSquaredSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roc = _roc.Update(bar, isFinal, includeOutputs: false).Value;
        var middle = _middleSmoother.Next(roc, isFinal);
        var rocSquared = roc * roc;

        int countAfter;
        var sum = isFinal ? _rocSquaredSum.Add(rocSquared, out countAfter) : _rocSquaredSum.Preview(rocSquared, out countAfter);
        var squaredAvg = countAfter > 0 ? sum / countAfter : 0;
        var upper = MathHelper.Sqrt(squaredAvg);
        var lower = -upper;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _roc.Dispose();
        _middleSmoother.Dispose();
        _rocSquaredSum.Dispose();
    }
}

public sealed class GChannelsState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private double _prevB;
    private int _count;

    public GChannelsState(int length = 100, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public GChannelsState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GChannels;

    public void Reset()
    {
        _prevA = 0;
        _prevB = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevA = _count >= 1 ? _prevA : 0;
        var prevB = _count >= 1 ? _prevB : 0;
        var factor = _length != 0 ? (prevA - prevB) / _length : 0;

        var a = Math.Max(value, prevA) - factor;
        var b = Math.Min(value, prevB) + factor;
        var middle = (a + b) / 2;

        if (isFinal)
        {
            _prevA = a;
            _prevB = b;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", a },
                { "MiddleBand", middle },
                { "LowerBand", b }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }
}

public sealed class HighLowMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _upperSmoother;
    private readonly IMovingAverageSmoother _lowerSmoother;
    private readonly StreamingInputResolver _input;

    public HighLowMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _upperSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowerSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HighLowMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _upperSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowerSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HighLowMovingAverage;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _upperSmoother.Reset();
        _lowerSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var upper = _upperSmoother.Next(highest, isFinal);
        var lower = _lowerSmoother.Next(lowest, isFinal);
        var middle = (upper + lower) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _upperSmoother.Dispose();
        _lowerSmoother.Dispose();
    }
}

public sealed class HighLowBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _firstSmoother;
    private readonly IMovingAverageSmoother _secondSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _pctShift;

    public HighLowBandsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double pctShift = 1, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _firstSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _secondSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _pctShift = pctShift;
        _input = new StreamingInputResolver(inputName, null);
    }

    public HighLowBandsState(MovingAvgType maType, int length, double pctShift, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _firstSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _secondSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _pctShift = pctShift;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HighLowBands;

    public void Reset()
    {
        _firstSmoother.Reset();
        _secondSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var tma1 = _firstSmoother.Next(value, isFinal);
        var tma = _secondSmoother.Next(tma1, isFinal);
        var upper = tma + (tma * _pctShift / 100);
        var lower = tma - (tma * _pctShift / 100);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", tma },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(tma, outputs);
    }

    public void Dispose()
    {
        _firstSmoother.Dispose();
        _secondSmoother.Dispose();
    }
}

public sealed class KeltnerChannelsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _middleSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;
    private double _prevValue;
    private bool _hasPrev;

    public KeltnerChannelsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 20,
        int length2 = 10, double multFactor = 2, InputName inputName = InputName.Close)
    {
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _mult = multFactor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public KeltnerChannelsState(MovingAvgType maType, int length1, int length2, double multFactor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _middleSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _mult = multFactor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KeltnerChannels;

    public void Reset()
    {
        _atrSmoother.Reset();
        _middleSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);
        var middle = _middleSmoother.Next(value, isFinal);
        var upper = middle + (_mult * atr);
        var lower = middle - (_mult * atr);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _middleSmoother.Dispose();
    }
}

public sealed class DEnvelopeState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly double _devFactor;
    private readonly StreamingInputResolver _input;
    private double _mt;
    private double _ut;
    private double _dt;
    private double _mt2;
    private double _ut2;

    public DEnvelopeState(int length = 20, double devFactor = 2, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _alpha = (double)2 / (resolved + 1);
        _devFactor = devFactor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public DEnvelopeState(int length, double devFactor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _alpha = (double)2 / (resolved + 1);
        _devFactor = devFactor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DEnvelope;

    public void Reset()
    {
        _mt = 0;
        _ut = 0;
        _dt = 0;
        _mt2 = 0;
        _ut2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var oneMinus = 1 - _alpha;
        var mt = (_alpha * value) + (oneMinus * _mt);
        var ut = (_alpha * mt) + (oneMinus * _ut);
        var dt = (2 - _alpha) * (mt - ut) / oneMinus;
        var mt2 = (_alpha * Math.Abs(value - dt)) + (oneMinus * _mt2);
        var ut2 = (_alpha * mt2) + (oneMinus * _ut2);
        var dt2 = (2 - _alpha) * (mt2 - ut2) / oneMinus;
        var upper = dt + (_devFactor * dt2);
        var lower = dt - (_devFactor * dt2);

        if (isFinal)
        {
            _mt = mt;
            _ut = ut;
            _dt = dt;
            _mt2 = mt2;
            _ut2 = ut2;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", dt },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(dt, outputs);
    }
}

public sealed class PriceChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly double _pct;

    public PriceChannelState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21,
        double pct = 0.06, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _pct = pct;
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceChannelState(MovingAvgType maType, int length, double pct, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _pct = pct;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceChannel;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _smoother.Next(value, isFinal);
        var upper = ema * (1 + _pct);
        var lower = ema * (1 - _pct);
        var middle = (upper + lower) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperChannel", upper },
                { "LowerChannel", lower },
                { "MiddleChannel", middle }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class MovingAverageChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;
    private readonly StreamingInputResolver _input;

    public MovingAverageChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MovingAverageChannelState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MovingAverageChannel;

    public void Reset()
    {
        _highSmoother.Reset();
        _lowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var upper = _highSmoother.Next(bar.High, isFinal);
        var lower = _lowSmoother.Next(bar.Low, isFinal);
        var middle = (upper + lower) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _highSmoother.Dispose();
        _lowSmoother.Dispose();
    }
}
public sealed class RelativeStrengthIndexState : IStreamingIndicatorState       
{
    private readonly WilderState _avgGain;
    private readonly WilderState _avgLoss;
    private readonly WilderState _signal;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public RelativeStrengthIndexState(int length = 14, int signalLength = 3, InputName inputName = InputName.Close)
    {
        _avgGain = new WilderState(length);
        _avgLoss = new WilderState(length);
        _signal = new WilderState(signalLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RelativeStrengthIndexState(int length, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _avgGain = new WilderState(length);
        _avgLoss = new WilderState(length);
        _signal = new WilderState(signalLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RelativeStrengthIndex;

    public void Reset()
    {
        _avgGain.Reset();
        _avgLoss.Reset();
        _signal.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var priceChg = _hasPrev ? currentValue - prevClose : 0;
        var gain = priceChg > 0 ? priceChg : 0;
        var loss = priceChg < 0 ? Math.Abs(priceChg) : 0;

        var avgGain = _avgGain.GetNext(gain, isFinal);
        var avgLoss = _avgLoss.GetNext(loss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);
        var signal = _signal.GetNext(rsi, isFinal);
        var histogram = rsi - signal;

        if (isFinal)
        {
            _prevClose = currentValue;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Rsi", rsi },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }
}

public sealed class StochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticOscillatorState(MovingAvgType maType, int length, int smoothLength1, int smoothLength2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StochasticOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = _input.GetValue(bar);

        var highestHigh = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lowestLow = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var range = highestHigh - lowestLow;
        var fastK = range != 0 ? MathHelper.MinOrMax((close - lowestLow) / range * 100, 100, 0) : 0;

        var fastD = _fastSmoother.Next(fastK, isFinal);
        var slowD = _slowSmoother.Next(fastD, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "FastK", fastK },
                { "FastD", fastD },
                { "SlowD", slowD }
            };
        }

        return new StreamingIndicatorStateResult(fastK, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class WilliamsRState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public WilliamsRState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public WilliamsRState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.WilliamsR;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = _input.GetValue(bar);

        var highestHigh = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lowestLow = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var range = highestHigh - lowestLow;
        var williamsR = range != 0 ? -100 * (highestHigh - close) / range : -100;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Williams%R", williamsR }
            };
        }

        return new StreamingIndicatorStateResult(williamsR, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class CommodityChannelIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _priceSmoother;
    private readonly IMovingAverageSmoother _meanDevSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _constant;

    public CommodityChannelIndexState(InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20, double constant = 0.015)
    {
        _priceSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _meanDevSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
        _constant = constant;
    }

    public CommodityChannelIndexState(InputName inputName, MovingAvgType maType, int length, double constant,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _priceSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _meanDevSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, selector);
        _constant = constant;
    }

    public IndicatorName Name => IndicatorName.CommodityChannelIndex;

    public void Reset()
    {
        _priceSmoother.Reset();
        _meanDevSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _priceSmoother.Next(value, isFinal);
        var meanDev = _meanDevSmoother.Next(Math.Abs(value - sma), isFinal);
        var cci = meanDev != 0 ? (value - sma) / (_constant * meanDev) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cci", cci }
            };
        }

        return new StreamingIndicatorStateResult(cci, outputs);
    }

    public void Dispose()
    {
        _priceSmoother.Dispose();
        _meanDevSmoother.Dispose();
    }
}

public sealed class StochasticRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RsiState _rsi;
    private readonly RollingWindowMax _inputHighWindow;
    private readonly RollingWindowMin _inputLowWindow;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _rsi = new RsiState(maType, _length);
        _inputHighWindow = new RollingWindowMax(2);
        _inputLowWindow = new RollingWindowMin(2);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticRelativeStrengthIndexState(MovingAvgType maType, int length, int smoothLength1, int smoothLength2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _rsi = new RsiState(maType, _length);
        _inputHighWindow = new RollingWindowMax(2);
        _inputLowWindow = new RollingWindowMin(2);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StochasticRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _inputHighWindow.Reset();
        _inputLowWindow.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var inputHigh = isFinal ? _inputHighWindow.Add(rsi, out _) : _inputHighWindow.Preview(rsi, out _);
        var inputLow = isFinal ? _inputLowWindow.Add(rsi, out _) : _inputLowWindow.Preview(rsi, out _);
        var highest = isFinal ? _maxWindow.Add(inputHigh, out _) : _maxWindow.Preview(inputHigh, out _);
        var lowest = isFinal ? _minWindow.Add(inputLow, out _) : _minWindow.Preview(inputLow, out _);
        var range = highest - lowest;
        var fastK = range != 0 ? MathHelper.MinOrMax((rsi - lowest) / range * 100, 100, 0) : 0;

        var fastD = _fastSmoother.Next(fastK, isFinal);
        var slowD = _slowSmoother.Next(fastD, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "StochRsi", fastD },
                { "Signal", slowD }
            };
        }

        return new StreamingIndicatorStateResult(fastD, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _inputHighWindow.Dispose();
        _inputLowWindow.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class ConnorsRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _rocLength;
    private readonly RsiState _rsi;
    private readonly RsiState _streakRsi;
    private readonly RollingPercentRank _rocRank;
    private readonly PooledRingBuffer<double> _rocWindow;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _streak;
    private bool _hasPrev;

    public ConnorsRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 2, int length2 = 3, int length3 = 100, InputName inputName = InputName.Close)
    {
        _rocLength = Math.Max(1, length3);
        _rsi = new RsiState(maType, Math.Max(1, length2));
        _streakRsi = new RsiState(maType, Math.Max(1, length1));
        _rocRank = new RollingPercentRank(_rocLength);
        _rocWindow = new PooledRingBuffer<double>(_rocLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ConnorsRelativeStrengthIndexState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _rocLength = Math.Max(1, length3);
        _rsi = new RsiState(maType, Math.Max(1, length2));
        _streakRsi = new RsiState(maType, Math.Max(1, length1));
        _rocRank = new RollingPercentRank(_rocLength);
        _rocWindow = new PooledRingBuffer<double>(_rocLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ConnorsRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _streakRsi.Reset();
        _rocRank.Reset();
        _rocWindow.Clear();
        _prevValue = 0;
        _streak = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var rsi = _rsi.Next(currentValue, isFinal);
        var rocPrev = _rocWindow.Count >= _rocLength ? _rocWindow[0] : 0;
        var roc = rocPrev != 0 ? (rsi - rocPrev) / rocPrev * 100 : 0;
        var pctRank = isFinal ? _rocRank.Add(roc) : _rocRank.Preview(roc);

        var prevValue = _hasPrev ? _prevValue : 0;
        var prevStreak = _streak;
        var streak = currentValue > prevValue
            ? prevStreak >= 0 ? prevStreak + 1 : 1
            : currentValue < prevValue
                ? prevStreak <= 0 ? prevStreak - 1 : -1
                : 0;
        var streakRsi = _streakRsi.Next(streak, isFinal);

        var connors = MathHelper.MinOrMax((rsi + pctRank + streakRsi) / 3, 100, 0);

        if (isFinal)
        {
            _prevValue = currentValue;
            _streak = streak;
            _hasPrev = true;
            _rocWindow.TryAdd(rsi, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Rsi", rsi },
                { "PctRank", pctRank },
                { "StreakRsi", streakRsi },
                { "ConnorsRsi", connors }
            };
        }

        return new StreamingIndicatorStateResult(connors, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _streakRsi.Dispose();
        _rocRank.Dispose();
        _rocWindow.Dispose();
    }
}

public sealed class StochasticConnorsRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly ConnorsRelativeStrengthIndexState _connors;
    private readonly RollingWindowMax _inputHighWindow;
    private readonly RollingWindowMin _inputLowWindow;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;

    public StochasticConnorsRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 2, int length2 = 3, int length3 = 100, int smoothLength1 = 3, int smoothLength2 = 3,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length2);
        _connors = new ConnorsRelativeStrengthIndexState(maType, length1, length2, length3, inputName);
        _inputHighWindow = new RollingWindowMax(2);
        _inputLowWindow = new RollingWindowMin(2);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
    }

    public StochasticConnorsRelativeStrengthIndexState(MovingAvgType maType, int length1, int length2, int length3,
        int smoothLength1, int smoothLength2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length2);
        _connors = new ConnorsRelativeStrengthIndexState(maType, length1, length2, length3, selector);
        _inputHighWindow = new RollingWindowMax(2);
        _inputLowWindow = new RollingWindowMin(2);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
    }

    public IndicatorName Name => IndicatorName.StochasticConnorsRelativeStrengthIndex;

    public void Reset()
    {
        _connors.Reset();
        _inputHighWindow.Reset();
        _inputLowWindow.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var connors = _connors.Update(bar, isFinal, includeOutputs: false).Value;
        var inputHigh = isFinal ? _inputHighWindow.Add(connors, out _) : _inputHighWindow.Preview(connors, out _);
        var inputLow = isFinal ? _inputLowWindow.Add(connors, out _) : _inputLowWindow.Preview(connors, out _);
        var highest = isFinal ? _maxWindow.Add(inputHigh, out _) : _maxWindow.Preview(inputHigh, out _);
        var lowest = isFinal ? _minWindow.Add(inputLow, out _) : _minWindow.Preview(inputLow, out _);
        var range = highest - lowest;
        var fastK = range != 0 ? MathHelper.MinOrMax((connors - lowest) / range * 100, 100, 0) : 0;
        var fastD = _fastSmoother.Next(fastK, isFinal);
        var slowD = _slowSmoother.Next(fastD, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "SaRsi", fastD },
                { "Signal", slowD }
            };
        }

        return new StreamingIndicatorStateResult(fastD, outputs);
    }

    public void Dispose()
    {
        _connors.Dispose();
        _inputHighWindow.Dispose();
        _inputLowWindow.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class StochasticMomentumIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _diffSmoother1;
    private readonly IMovingAverageSmoother _diffSmoother2;
    private readonly IMovingAverageSmoother _rangeSmoother1;
    private readonly IMovingAverageSmoother _rangeSmoother2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticMomentumIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 2, int length2 = 8, int smoothLength1 = 5, int smoothLength2 = 5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _diffSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _rangeSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _rangeSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticMomentumIndexState(MovingAvgType maType, int length1, int length2, int smoothLength1,
        int smoothLength2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _diffSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _rangeSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _rangeSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StochasticMomentumIndex;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _diffSmoother1.Reset();
        _diffSmoother2.Reset();
        _rangeSmoother1.Reset();
        _rangeSmoother2.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var median = (highest + lowest) / 2;
        var diff = value - median;
        var range = highest - lowest;

        var diffEma = _diffSmoother1.Next(diff, isFinal);
        var rangeEma = _rangeSmoother1.Next(range, isFinal);
        var diffSmooth = _diffSmoother2.Next(diffEma, isFinal);
        var rangeSmooth = _rangeSmoother2.Next(rangeEma, isFinal);
        var halfRange = rangeSmooth / 2;
        var smi = halfRange != 0 ? MathHelper.MinOrMax(100 * diffSmooth / halfRange, 100, -100) : 0;
        var signal = _signalSmoother.Next(smi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Smi", smi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(smi, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _diffSmoother1.Dispose();
        _diffSmoother2.Dispose();
        _rangeSmoother1.Dispose();
        _rangeSmoother2.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MovingAverageConvergenceDivergenceState : IStreamingIndicatorState
{
    private readonly EmaState _fast;
    private readonly EmaState _slow;
    private readonly EmaState _signal;
    private readonly StreamingInputResolver _input;

    public MovingAverageConvergenceDivergenceState(int fastLength = 12, int slowLength = 26, int signalLength = 9,
        InputName inputName = InputName.Close)
    {
        _fast = new EmaState(fastLength);
        _slow = new EmaState(slowLength);
        _signal = new EmaState(signalLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MovingAverageConvergenceDivergenceState(int fastLength, int slowLength, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fast = new EmaState(fastLength);
        _slow = new EmaState(slowLength);
        _signal = new EmaState(signalLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MovingAverageConvergenceDivergence;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.GetNext(value, isFinal);
        var slow = _slow.GetNext(value, isFinal);
        var macd = fast - slow;
        var signal = _signal.GetNext(macd, isFinal);
        var histogram = macd - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Macd", macd },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }
}

public sealed class AbsolutePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly StreamingInputResolver _input;

    public AbsolutePriceOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 10, int slowLength = 20, InputName inputName = InputName.Close)
    {
        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public AbsolutePriceOscillatorState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AbsolutePriceOscillator;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.Next(value, isFinal);
        var slow = _slow.Next(value, isFinal);
        var apo = fast - slow;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Apo", apo }
            };
        }

        return new StreamingIndicatorStateResult(apo, outputs);
    }

    public void Dispose()
    {
        _fast.Dispose();
        _slow.Dispose();
    }
}

public sealed class PercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public PercentagePriceOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9,
        InputName inputName = InputName.Close)
    {
        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public PercentagePriceOscillatorState(MovingAvgType maType, int fastLength, int slowLength, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PercentagePriceOscillator;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.Next(value, isFinal);
        var slow = _slow.Next(value, isFinal);
        var ppo = slow != 0 ? 100 * (fast - slow) / slow : 0;
        var signal = _signal.Next(ppo, isFinal);
        var histogram = ppo - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Ppo", ppo },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(ppo, outputs);
    }

    public void Dispose()
    {
        _fast.Dispose();
        _slow.Dispose();
        _signal.Dispose();
    }
}

public sealed class PercentageVolumeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public PercentageVolumeOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Volume, null);
    }

    public PercentageVolumeOscillatorState(MovingAvgType maType, int fastLength, int slowLength, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Volume, selector);
    }

    public IndicatorName Name => IndicatorName.PercentageVolumeOscillator;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.Next(value, isFinal);
        var slow = _slow.Next(value, isFinal);
        var pvo = slow != 0 ? 100 * (fast - slow) / slow : 0;
        var signal = _signal.Next(pvo, isFinal);
        var histogram = pvo - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Pvo", pvo },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(pvo, outputs);
    }

    public void Dispose()
    {
        _fast.Dispose();
        _slow.Dispose();
        _signal.Dispose();
    }
}

public sealed class DonchianChannelsState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public DonchianChannelsState(int length = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DonchianChannelsState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DonchianChannels;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var upper = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lower = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var middle = (upper + lower) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperChannel", upper },
                { "LowerChannel", lower },
                { "MiddleChannel", middle }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class ClosedFormDistanceVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _ema;
    private readonly RollingWindowSum _highSum;
    private readonly RollingWindowSum _lowSum;
    private readonly StreamingInputResolver _input;

    public ClosedFormDistanceVolatilityState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(maType, _length);
        _highSum = new RollingWindowSum(_length);
        _lowSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ClosedFormDistanceVolatilityState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(maType, _length);
        _highSum = new RollingWindowSum(_length);
        _lowSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ClosedFormDistanceVolatility;

    public void Reset()
    {
        _ema.Reset();
        _highSum.Reset();
        _lowSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _ = _ema.Next(value, isFinal);

        var sumHigh = isFinal ? _highSum.Add(bar.High, out _) : _highSum.Preview(bar.High, out _);
        var sumLow = isFinal ? _lowSum.Add(bar.Low, out _) : _lowSum.Preview(bar.Low, out _);
        var abAvg = (sumHigh + sumLow) / 2;
        var hv = abAvg != 0 && sumHigh != sumLow
            ? MathHelper.Sqrt(1 - (MathHelper.Pow(sumHigh, 0.25) * MathHelper.Pow(sumLow, 0.25)
                / MathHelper.Pow(abAvg, 0.5)))
            : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cfdv", hv }
            };
        }

        return new StreamingIndicatorStateResult(hv, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _highSum.Dispose();
        _lowSum.Dispose();
    }
}

public sealed class DonchianChannelWidthState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _priceSmoother;
    private readonly IMovingAverageSmoother _widthSmoother;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public DonchianChannelWidthState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        int smoothLength = 22, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _priceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _widthSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DonchianChannelWidthState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _priceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _widthSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DonchianChannelWidth;

    public void Reset()
    {
        _priceSmoother.Reset();
        _widthSmoother.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _ = _priceSmoother.Next(value, isFinal);

        var upper = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lower = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var width = upper - lower;
        var signal = _widthSmoother.Next(width, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dcw", width },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(width, outputs);
    }

    public void Dispose()
    {
        _priceSmoother.Dispose();
        _widthSmoother.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class HistoricalVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private readonly double _annualSqrt;
    private double _prevValue;
    private double _logReturn;
    private bool _hasPrev;

    public HistoricalVolatilityState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        _stdDev = new StandardDeviationVolatilityState(maType, length, _ => _logReturn);
        _input = new StreamingInputResolver(inputName, null);
        _annualSqrt = MathHelper.Sqrt(365);
    }

    public HistoricalVolatilityState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _stdDev = new StandardDeviationVolatilityState(maType, length, _ => _logReturn);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _annualSqrt = MathHelper.Sqrt(365);
    }

    public IndicatorName Name => IndicatorName.HistoricalVolatility;

    public void Reset()
    {
        _stdDev.Reset();
        _prevValue = 0;
        _logReturn = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var temp = prevValue != 0 ? value / prevValue : 0;
        _logReturn = temp > 0 ? Math.Log(temp) : 0;
        var stdDevLog = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var hv = 100 * stdDevLog * _annualSqrt;

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Hv", hv }
            };
        }

        return new StreamingIndicatorStateResult(hv, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
    }
}

public sealed class GarmanKlassVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _logSum;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private readonly double _logCoeff;
    private int _index;

    public GarmanKlassVolatilityState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14,
        int signalLength = 7, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _logSum = new RollingWindowSum(_length);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
        _logCoeff = (2 * Math.Log(2)) - 1;
    }

    public GarmanKlassVolatilityState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _logSum = new RollingWindowSum(_length);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _logCoeff = (2 * Math.Log(2)) - 1;
    }

    public IndicatorName Name => IndicatorName.GarmanKlassVolatility;

    public void Reset()
    {
        _logSum.Reset();
        _signal.Reset();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentClose = _input.GetValue(bar);
        var logHl = bar.Low != 0 ? Math.Log(bar.High / bar.Low) : 0;
        var logCo = bar.Open != 0 ? Math.Log(currentClose / bar.Open) : 0;
        var log = (0.5 * MathHelper.Pow(logHl, 2)) - (_logCoeff * MathHelper.Pow(logCo, 2));

        var currentIndex = _index;
        var logSum = isFinal ? _logSum.Add(log, out _) : _logSum.Preview(log, out _);
        var gcv = logSum != 0 ? MathHelper.Sqrt((double)currentIndex / _length * logSum) : 0;
        var signal = _signal.Next(gcv, isFinal);

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Gcv", gcv },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(gcv, outputs);
    }

    public void Dispose()
    {
        _logSum.Dispose();
        _signal.Dispose();
    }
}

public sealed class GopalakrishnanRangeIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly double _logLength;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public GopalakrishnanRangeIndexState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 5,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _logLength = Math.Log(resolved);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _signal = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public GopalakrishnanRangeIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _logLength = Math.Log(resolved);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _signal = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GopalakrishnanRangeIndex;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var upper = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lower = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var range = upper - lower;
        var rangeLog = range > 0 ? Math.Log(range) : 0;
        var gapo = rangeLog / _logLength;
        var signal = _signal.Next(gapo, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Gapo", gapo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(gapo, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _signal.Dispose();
    }
}

public sealed class HistoricalVolatilityPercentileState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _annualLength;
    private readonly double _annualSqrt;
    private readonly RollingWindowSum _tempLogSum;
    private readonly RollingWindowSum _devLogSqSum;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private RollingOrderStatistic _hvOrder;
    private double _prevValue;
    private bool _hasPrev;

    public HistoricalVolatilityPercentileState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 21, int annualLength = 252, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _annualLength = Math.Max(1, annualLength);
        _annualSqrt = MathHelper.Sqrt(_annualLength);
        _tempLogSum = new RollingWindowSum(_length);
        _devLogSqSum = new RollingWindowSum(_length);
        _signal = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
        _hvOrder = new RollingOrderStatistic(_annualLength);
    }

    public HistoricalVolatilityPercentileState(MovingAvgType maType, int length, int annualLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _annualLength = Math.Max(1, annualLength);
        _annualSqrt = MathHelper.Sqrt(_annualLength);
        _tempLogSum = new RollingWindowSum(_length);
        _devLogSqSum = new RollingWindowSum(_length);
        _signal = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _hvOrder = new RollingOrderStatistic(_annualLength);
    }

    public IndicatorName Name => IndicatorName.HistoricalVolatilityPercentile;

    public void Reset()
    {
        _tempLogSum.Reset();
        _devLogSqSum.Reset();
        _signal.Reset();
        _hvOrder.Dispose();
        _hvOrder = new RollingOrderStatistic(_annualLength);
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var temp = prevValue != 0 ? value / prevValue : 0;
        var tempLog = temp > 0 ? Math.Log(temp) : 0;

        int tempCount;
        var tempSum = isFinal ? _tempLogSum.Add(tempLog, out tempCount) : _tempLogSum.Preview(tempLog, out tempCount);
        var avgLog = tempCount > 0 ? tempSum / tempCount : 0;

        var devLogSq = MathHelper.Pow(tempLog - avgLog, 2);
        var devSum = isFinal ? _devLogSqSum.Add(devLogSq, out _) : _devLogSqSum.Preview(devLogSq, out _);
        var devLogSqAvg = devSum / (_length - 1);
        var stdDevLog = devLogSqAvg >= 0 ? MathHelper.Sqrt(devLogSqAvg) : 0;
        var hv = stdDevLog * _annualSqrt;

        if (isFinal)
        {
            _hvOrder.Add(hv);
        }

        var count = (double)_hvOrder.CountLessThan(hv);
        var hvp = count / _annualLength * 100;
        var signal = _signal.Next(hvp, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Hvp", hvp },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(hvp, outputs);
    }

    public void Dispose()
    {
        _tempLogSum.Dispose();
        _devLogSqSum.Dispose();
        _signal.Dispose();
        _hvOrder.Dispose();
    }
}

public sealed class FastZScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly LinearRegressionState _linregLong;
    private readonly LinearRegressionState _linregShort;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private double _maValue;

    public FastZScoreState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var length2 = MathHelper.MinOrMax((int)Math.Ceiling((double)resolved / 2));
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _linregLong = new LinearRegressionState(resolved, _ => _maValue);
        _linregShort = new LinearRegressionState(length2, _ => _maValue);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _maValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FastZScoreState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var length2 = MathHelper.MinOrMax((int)Math.Ceiling((double)resolved / 2));
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _linregLong = new LinearRegressionState(resolved, _ => _maValue);
        _linregShort = new LinearRegressionState(length2, _ => _maValue);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _maValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FastZScore;

    public void Reset()
    {
        _smoother.Reset();
        _linregLong.Reset();
        _linregShort.Reset();
        _stdDev.Reset();
        _maValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma = _smoother.Next(value, isFinal);
        _maValue = ma;

        var linreg = _linregLong.Update(bar, isFinal, includeOutputs: false).Value;
        var linreg2 = _linregShort.Update(bar, isFinal, includeOutputs: false).Value;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var gs = stdDev != 0 ? (linreg2 - linreg) / stdDev / 2 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Fzs", gs }
            };
        }

        return new StreamingIndicatorStateResult(gs, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _linregLong.Dispose();
        _linregShort.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class ChoppinessIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _trSum;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ChoppinessIndexState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _trSum = new RollingWindowSum(_length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChoppinessIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _trSum = new RollingWindowSum(_length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChoppinessIndex;

    public void Reset()
    {
        _trSum.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);

        int trCount;
        int highCount;
        int lowCount;
        var trSum = isFinal ? _trSum.Add(tr, out trCount) : _trSum.Preview(tr, out trCount);
        var highest = isFinal ? _highWindow.Add(bar.High, out highCount) : _highWindow.Preview(bar.High, out highCount);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out lowCount) : _lowWindow.Preview(bar.Low, out lowCount);
        var range = highest - lowest;

        var ci = range > 0 ? 100 * Math.Log10(trSum / range) / Math.Log10(_length) : 0;

        if (isFinal)
        {
            _prevValue = currentValue;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ci", ci }
            };
        }

        return new StreamingIndicatorStateResult(ci, outputs);
    }

    public void Dispose()
    {
        _trSum.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class ChandeMomentumOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _posSum;
    private readonly RollingWindowSum _negSum;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ChandeMomentumOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, int signalLength = 3, InputName inputName = InputName.Close)
    {
        _posSum = new RollingWindowSum(Math.Max(1, length));
        _negSum = new RollingWindowSum(Math.Max(1, length));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeMomentumOscillatorState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _posSum = new RollingWindowSum(Math.Max(1, length));
        _negSum = new RollingWindowSum(Math.Max(1, length));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillator;

    public void Reset()
    {
        _posSum.Reset();
        _negSum.Reset();
        _signal.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = _hasPrev ? currentValue - prevValue : 0;
        var negChg = diff < 0 ? Math.Abs(diff) : 0;
        var posChg = diff > 0 ? diff : 0;

        int posCount;
        int negCount;
        var posSum = isFinal ? _posSum.Add(posChg, out posCount) : _posSum.Preview(posChg, out posCount);
        var negSum = isFinal ? _negSum.Add(negChg, out negCount) : _negSum.Preview(negChg, out negCount);

        var denom = posSum + negSum;
        var cmo = denom != 0 ? MathHelper.MinOrMax((posSum - negSum) / denom * 100, 100, -100) : 0;
        var signal = _signal.Next(cmo, isFinal);

        if (isFinal)
        {
            _prevValue = currentValue;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Cmo", cmo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(cmo, outputs);
    }

    public void Dispose()
    {
        _posSum.Dispose();
        _negSum.Dispose();
        _signal.Dispose();
    }
}

public sealed class AverageTrueRangeState : IStreamingIndicatorState
{
    private readonly WilderState _atr;
    private double _prevClose;
    private bool _hasPrev;

    public AverageTrueRangeState(int length = 14)
    {
        _atr = new WilderState(length);
    }

    public IndicatorName Name => IndicatorName.AverageTrueRange;

    public void Reset()
    {
        _atr.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atr.GetNext(tr, isFinal);

        if (isFinal)
        {
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Atr", atr }
            };
        }

        return new StreamingIndicatorStateResult(atr, outputs);
    }
}

public sealed class AverageDirectionalIndexState : IStreamingIndicatorState
{
    private readonly WilderState _dmPlus;
    private readonly WilderState _dmMinus;
    private readonly WilderState _tr;
    private readonly WilderState _adx;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    public AverageDirectionalIndexState(int length = 14)
    {
        _dmPlus = new WilderState(length);
        _dmMinus = new WilderState(length);
        _tr = new WilderState(length);
        _adx = new WilderState(length);
    }

    public IndicatorName Name => IndicatorName.AverageDirectionalIndex;

    public void Reset()
    {
        _dmPlus.Reset();
        _dmMinus.Reset();
        _tr.Reset();
        _adx.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;

        var highDiff = bar.High - prevHigh;
        var lowDiff = prevLow - bar.Low;

        var dmPlus = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
        var dmMinus = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);

        var dmPlus14 = _dmPlus.GetNext(dmPlus, isFinal);
        var dmMinus14 = _dmMinus.GetNext(dmMinus, isFinal);
        var tr14 = _tr.GetNext(tr, isFinal);

        var diPlus = tr14 != 0 ? MathHelper.MinOrMax(100 * dmPlus14 / tr14, 100, 0) : 0;
        var diMinus = tr14 != 0 ? MathHelper.MinOrMax(100 * dmMinus14 / tr14, 100, 0) : 0;
        var diDiff = Math.Abs(diPlus - diMinus);
        var diSum = diPlus + diMinus;
        var dx = diSum != 0 ? MathHelper.MinOrMax(100 * diDiff / diSum, 100, 0) : 0;
        var adx = _adx.GetNext(dx, isFinal);

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "DiPlus", diPlus },
                { "DiMinus", diMinus },
                { "Adx", adx }
            };
        }

        return new StreamingIndicatorStateResult(adx, outputs);
    }
}

public sealed class BollingerBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _stdDevMult;
    private readonly RollingWindowStats _window;
    private readonly StreamingInputResolver _input;

    public BollingerBandsState(int length = 20, double stdDevMult = 2, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _window = new RollingWindowStats(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BollingerBandsState(int length, double stdDevMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _window = new RollingWindowStats(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BollingerBands;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var snapshot = isFinal ? _window.Add(value) : _window.Preview(value);
        var middle = snapshot.Count >= _length ? snapshot.Sum / _length : 0;
        var variance = snapshot.Count >= _length ? (snapshot.SumSquares / _length) - (middle * middle) : 0;
        var stdDev = MathHelper.Sqrt(variance);
        var upper = middle + (stdDev * _stdDevMult);
        var lower = middle - (stdDev * _stdDevMult);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

public sealed class StandardDeviationVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _inputMa;
    private readonly IMovingAverageSmoother _varianceMa;
    private readonly IMovingAverageSmoother _signalMa;
    private readonly StreamingInputResolver _input;

    public StandardDeviationVolatilityState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _inputMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public StandardDeviationVolatilityState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _inputMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StandardDeviationVolatility;

    public void Reset()
    {
        _inputMa.Reset();
        _varianceMa.Reset();
        _signalMa.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mean = _inputMa.Next(value, isFinal);
        var deviation = value - mean;
        var variance = _varianceMa.Next(deviation * deviation, isFinal);
        var stdDev = MathHelper.Sqrt(variance);
        var signal = _signalMa.Next(stdDev, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "StdDev", stdDev },
                { "Variance", variance },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(stdDev, outputs);
    }

    public void Dispose()
    {
        _inputMa.Dispose();
        _varianceMa.Dispose();
        _signalMa.Dispose();
    }
}

public sealed class StandardDeviationState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _sumWindow;
    private readonly IMovingAverageSmoother _powMa;
    private readonly IMovingAverageSmoother _signalMa;
    private readonly StreamingInputResolver _input;

    public StandardDeviationState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sumWindow = new RollingWindowSum(_length);
        _powMa = MovingAverageSmootherFactory.Create(maType, _length);
        _signalMa = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public StandardDeviationState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sumWindow = new RollingWindowSum(_length);
        _powMa = MovingAverageSmootherFactory.Create(maType, _length);
        _signalMa = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StandardDeviation;

    public void Reset()
    {
        _sumWindow.Reset();
        _powMa.Reset();
        _signalMa.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int sumCount;
        var sum = isFinal ? _sumWindow.Add(value, out sumCount) : _sumWindow.Preview(value, out sumCount);
        var powMean = _powMa.Next(value * value, isFinal);
        var denom = (double)_length * _length;
        var b = denom != 0 ? (sum * sum) / denom : 0;
        var diff = powMean - b;
        var std = diff >= 0 ? MathHelper.Sqrt(diff) : 0;
        var signal = _signalMa.Next(std, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Std", std },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(std, outputs);
    }

    public void Dispose()
    {
        _sumWindow.Dispose();
        _powMa.Dispose();
        _signalMa.Dispose();
    }
}


public sealed class UltimateVolatilityIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _absSum;
    private readonly StreamingInputResolver _input;

    public UltimateVolatilityIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _absSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public UltimateVolatilityIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _absSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.UltimateVolatilityIndicator;

    public void Reset()
    {
        _absSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var abs = Math.Abs(value - bar.Open);
        int countAfter;
        var sum = isFinal ? _absSum.Add(abs, out countAfter) : _absSum.Preview(abs, out countAfter);
        var uvi = sum / _length;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Uvi", uvi }
            };
        }

        return new StreamingIndicatorStateResult(uvi, outputs);
    }

    public void Dispose()
    {
        _absSum.Dispose();
    }
}

public sealed class VolatilityBasedMomentumState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public VolatilityBasedMomentumState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length1 = 22,
        int length2 = 65, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _window = new PooledRingBuffer<double>(_length1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolatilityBasedMomentumState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _window = new PooledRingBuffer<double>(_length1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VolatilityBasedMomentum;

    public void Reset()
    {
        _atrSmoother.Reset();
        _signalSmoother.Reset();
        _window.Clear();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);
        var prevLengthValue = _window.Count >= _length1 ? _window[0] : 0;
        var rateOfChange = _window.Count >= _length1 ? value - prevLengthValue : 0;
        var vbm = atr != 0 ? rateOfChange / atr : 0;
        var signal = _signalSmoother.Next(vbm, isFinal);

        if (isFinal)
        {
            _window.TryAdd(value, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Vbm", vbm },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(vbm, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _signalSmoother.Dispose();
        _window.Dispose();
    }
}

public sealed class VolatilityQualityIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;
    private double _vqiSum;
    private double _prevVqiT;
    private double _prevClose;
    private bool _hasPrev;

    public VolatilityQualityIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 9,
        int slowLength = 200, InputName inputName = InputName.Close)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolatilityQualityIndexState(MovingAvgType maType, int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VolatilityQualityIndex;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _vqiSum = 0;
        _prevVqiT = 0;
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var trueRange = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var range = bar.High - bar.Low;
        var vqiT = trueRange != 0 && range != 0
            ? (((value - prevClose) / trueRange) + ((value - bar.Open) / range)) * 0.5
            : _prevVqiT;
        var vqi = Math.Abs(vqiT) * ((value - prevClose + (value - bar.Open)) * 0.5);
        var vqiSum = _vqiSum + vqi;
        var fast = _fastSmoother.Next(vqiSum, isFinal);
        var slow = _slowSmoother.Next(vqiSum, isFinal);

        if (isFinal)
        {
            _vqiSum = vqiSum;
            _prevVqiT = vqiT;
            _prevClose = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Vqi", vqiSum },
                { "FastSignal", fast },
                { "SlowSignal", slow }
            };
        }

        return new StreamingIndicatorStateResult(vqiSum, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}
public sealed class OnBalanceVolumeState : IStreamingIndicatorState
{
    private readonly EmaState _signal;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private double _obv;
    private bool _hasPrev;

    public OnBalanceVolumeState(int length = 20, InputName inputName = InputName.Close)
    {
        _signal = new EmaState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public OnBalanceVolumeState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signal = new EmaState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.OnBalanceVolume;

    public void Reset()
    {
        _signal.Reset();
        _prevClose = 0;
        _obv = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevObv = _obv;
        var obv = currentValue > prevClose ? prevObv + bar.Volume
            : currentValue < prevClose ? prevObv - bar.Volume
            : prevObv;

        var signal = _signal.GetNext(obv, isFinal);

        if (isFinal)
        {
            _prevClose = currentValue;
            _obv = obv;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Obv", obv },
                { "ObvSignal", signal }
            };
        }

        return new StreamingIndicatorStateResult(obv, outputs);
    }
}

public sealed class ChaikinMoneyFlowState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _volumeSum;
    private readonly RollingWindowSum _mfVolumeSum;

    public ChaikinMoneyFlowState(int length = 20)
    {
        var resolved = Math.Max(1, length);
        _volumeSum = new RollingWindowSum(resolved);
        _mfVolumeSum = new RollingWindowSum(resolved);
    }

    public IndicatorName Name => IndicatorName.ChaikinMoneyFlow;

    public void Reset()
    {
        _volumeSum.Reset();
        _mfVolumeSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = bar.Close;
        var volume = bar.Volume;
        var multiplier = high - low != 0
            ? (close - low - (high - close)) / (high - low)
            : 0;
        var mfVolume = multiplier * volume;

        var volumeSum = isFinal ? _volumeSum.Add(volume, out _) : _volumeSum.Preview(volume, out _);
        var mfVolumeSum = isFinal ? _mfVolumeSum.Add(mfVolume, out _) : _mfVolumeSum.Preview(mfVolume, out _);
        var cmf = volumeSum != 0 ? mfVolumeSum / volumeSum : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cmf", cmf }
            };
        }

        return new StreamingIndicatorStateResult(cmf, outputs);
    }

    public void Dispose()
    {
        _volumeSum.Dispose();
        _mfVolumeSum.Dispose();
    }
}

public sealed class MoneyFlowIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _posSum;
    private readonly RollingWindowSum _negSum;
    private readonly StreamingInputResolver _input;
    private double _prevTypical;
    private bool _hasPrev;

    public MoneyFlowIndexState(int length = 14, InputName inputName = InputName.TypicalPrice)
    {
        var resolved = Math.Max(1, length);
        _posSum = new RollingWindowSum(resolved);
        _negSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MoneyFlowIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _posSum = new RollingWindowSum(resolved);
        _negSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(InputName.TypicalPrice, selector);
    }

    public IndicatorName Name => IndicatorName.MoneyFlowIndex;

    public void Reset()
    {
        _posSum.Reset();
        _negSum.Reset();
        _prevTypical = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var typical = _input.GetValue(bar);
        var rawFlow = typical * bar.Volume;
        var posFlow = _hasPrev && typical > _prevTypical ? rawFlow : 0;
        var negFlow = _hasPrev && typical < _prevTypical ? rawFlow : 0;

        int _;
        var posSum = isFinal ? _posSum.Add(posFlow, out _) : _posSum.Preview(posFlow, out _);
        var negSum = isFinal ? _negSum.Add(negFlow, out _) : _negSum.Preview(negFlow, out _);

        var ratio = negSum != 0 ? posSum / negSum : 0;
        var mfi = negSum == 0 ? 100 : posSum == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + ratio)), 100, 0);

        if (isFinal)
        {
            _prevTypical = typical;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mfi", mfi }
            };
        }

        return new StreamingIndicatorStateResult(mfi, outputs);
    }

    public void Dispose()
    {
        _posSum.Dispose();
        _negSum.Dispose();
    }
}

public sealed class AccumulationDistributionLineState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private double _adl;

    public AccumulationDistributionLineState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.AccumulationDistributionLine;

    public void Reset()
    {
        _signalSmoother.Reset();
        _adl = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = bar.Close;
        var volume = bar.Volume;
        var multiplier = high - low != 0
            ? (close - low - (high - close)) / (high - low)
            : 0;
        var moneyFlowVolume = multiplier * volume;

        var adl = _adl + moneyFlowVolume;
        var signal = _signalSmoother.Next(adl, isFinal);

        if (isFinal)
        {
            _adl = adl;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Adl", adl },
                { "AdlSignal", signal }
            };
        }

        return new StreamingIndicatorStateResult(adl, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class ChaikinOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private double _adl;

    public ChaikinOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 3, int slowLength = 10)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
    }

    public IndicatorName Name => IndicatorName.ChaikinOscillator;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _adl = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = bar.Close;
        var volume = bar.Volume;
        var multiplier = high - low != 0
            ? (close - low - (high - close)) / (high - low)
            : 0;
        var moneyFlowVolume = multiplier * volume;
        var adl = _adl + moneyFlowVolume;

        var fast = _fastSmoother.Next(adl, isFinal);
        var slow = _slowSmoother.Next(adl, isFinal);
        var chaikinOsc = fast - slow;

        if (isFinal)
        {
            _adl = adl;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "ChaikinOsc", chaikinOsc }
            };
        }

        return new StreamingIndicatorStateResult(chaikinOsc, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class TrueStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _pcSmoother1;
    private readonly IMovingAverageSmoother _pcSmoother2;
    private readonly IMovingAverageSmoother _absSmoother1;
    private readonly IMovingAverageSmoother _absSmoother2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public TrueStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 25,
        int length2 = 13, int signalLength = 7, InputName inputName = InputName.Close)
    {
        _pcSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _pcSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrueStrengthIndexState(MovingAvgType maType, int length1, int length2, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _pcSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _pcSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TrueStrengthIndex;

    public void Reset()
    {
        _pcSmoother1.Reset();
        _pcSmoother2.Reset();
        _absSmoother1.Reset();
        _absSmoother2.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var pc = _hasPrev ? value - prevValue : 0;
        var absPc = Math.Abs(pc);

        var pcSmooth1 = _pcSmoother1.Next(pc, isFinal);
        var pcSmooth2 = _pcSmoother2.Next(pcSmooth1, isFinal);
        var absSmooth1 = _absSmoother1.Next(absPc, isFinal);
        var absSmooth2 = _absSmoother2.Next(absSmooth1, isFinal);
        var tsi = absSmooth2 != 0 ? MathHelper.MinOrMax(100 * pcSmooth2 / absSmooth2, 100, -100) : 0;
        var signal = _signalSmoother.Next(tsi, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Tsi", tsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(tsi, outputs);
    }

    public void Dispose()
    {
        _pcSmoother1.Dispose();
        _pcSmoother2.Dispose();
        _absSmoother1.Dispose();
        _absSmoother2.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ElderRayIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly StreamingInputResolver _input;

    public ElderRayIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 13, InputName inputName = InputName.Close)
    {
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ElderRayIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ElderRayIndex;

    public void Reset()
    {
        _ema.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.Next(value, isFinal);
        var bullPower = bar.High - ema;
        var bearPower = bar.Low - ema;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "BullPower", bullPower },
                { "BearPower", bearPower }
            };
        }

        return new StreamingIndicatorStateResult(bullPower, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
    }
}

public sealed class AbsoluteStrengthIndexState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly int _maLength;
    private readonly double _alpha;
    private double _a;
    private double _m;
    private double _d;
    private double _abssiEma;
    private double _mt;
    private double _ut;
    private double _prevValue;
    private bool _hasPrev;

    public AbsoluteStrengthIndexState(int length = 10, int maLength = 21, int signalLength = 34)
    {
        _length = Math.Max(1, length);
        _maLength = Math.Max(1, maLength);
        var resolvedSignalLength = Math.Max(1, signalLength);
        _alpha = (double)2 / (resolvedSignalLength + 1);
    }

    public IndicatorName Name => IndicatorName.AbsoluteStrengthIndex;

    public void Reset()
    {
        _a = 0;
        _m = 0;
        _d = 0;
        _abssiEma = 0;
        _mt = 0;
        _ut = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = bar.Close;
        var prevValue = _hasPrev ? _prevValue : 0;

        var a = value > prevValue && prevValue != 0 ? _a + ((value / prevValue) - 1) : _a;
        var m = value == prevValue ? _m + ((double)1 / _length) : _m;
        var d = value < prevValue && value != 0 ? _d + ((prevValue / value) - 1) : _d;

        var dm = (d + m) / 2;
        var am = (a + m) / 2;
        var abssi = dm != 0 ? 1 - (1 / (1 + (am / dm))) : 1;
        var abssiEma = CalculationsHelper.CalculateEMA(abssi, _abssiEma, _maLength);
        var abssio = abssi - abssiEma;
        var mt = (_alpha * abssio) + ((1 - _alpha) * _mt);
        var ut = (_alpha * mt) + ((1 - _alpha) * _ut);
        var s = (2 - _alpha) * (mt - ut) / (1 - _alpha);
        var asi = abssio - s;

        if (isFinal)
        {
            _a = a;
            _m = m;
            _d = d;
            _abssiEma = abssiEma;
            _mt = mt;
            _ut = ut;
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Asi", asi }
            };
        }

        return new StreamingIndicatorStateResult(asi, outputs);
    }
}

public sealed class AccumulativeSwingIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signal;
    private double _asi;
    private double _prevClose;
    private double _prevOpen;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public AccumulativeSwingIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.AccumulativeSwingIndex;

    public void Reset()
    {
        _signal.Reset();
        _asi = 0;
        _prevClose = 0;
        _prevOpen = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentClose = bar.Close;
        var currentHigh = bar.High;
        var currentLow = bar.Low;
        var currentOpen = bar.Open;
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevOpen = _hasPrev ? _prevOpen : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevHighCurrentClose = prevHigh - currentClose;
        var prevLowCurrentClose = prevLow - currentClose;
        var prevClosePrevOpen = prevClose - prevOpen;
        var currentHighPrevClose = currentHigh - prevClose;
        var currentLowPrevClose = currentLow - prevClose;
        var t = currentHigh - currentLow;
        var k = Math.Max(Math.Abs(prevHighCurrentClose), Math.Abs(prevLowCurrentClose));
        var r = currentHighPrevClose > Math.Max(currentLowPrevClose, t)
            ? currentHighPrevClose - (0.5 * currentLowPrevClose) + (0.25 * prevClosePrevOpen)
            : currentLowPrevClose > Math.Max(currentHighPrevClose, t)
                ? currentLowPrevClose - (0.5 * currentHighPrevClose) + (0.25 * prevClosePrevOpen)
                : t > Math.Max(currentHighPrevClose, currentLowPrevClose)
                    ? t + (0.25 * prevClosePrevOpen)
                    : 0;
        var swingIndex = r != 0 && t != 0
            ? 50 * ((prevClose - currentClose + (0.5 * prevClosePrevOpen) + (0.25 * (currentClose - currentOpen))) / r) * (k / t)
            : 0;
        var asi = _asi + swingIndex;
        var signal = _signal.Next(asi, isFinal);

        if (isFinal)
        {
            _asi = asi;
            _prevClose = currentClose;
            _prevOpen = currentOpen;
            _prevHigh = currentHigh;
            _prevLow = currentLow;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Asi", asi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(asi, outputs);
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}

public sealed class BalanceOfPowerState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signal;

    public BalanceOfPowerState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.BalanceOfPower;

    public void Reset()
    {
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var range = bar.High - bar.Low;
        var bop = range != 0 ? (bar.Close - bar.Open) / range : 0;
        var bopSignal = _signal.Next(bop, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Bop", bop },
                { "BopSignal", bopSignal }
            };
        }

        return new StreamingIndicatorStateResult(bop, outputs);
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}

public sealed class BelkhayateTimingState : IStreamingIndicatorState
{
    private double _prevHigh1;
    private double _prevHigh2;
    private double _prevHigh3;
    private double _prevHigh4;
    private double _prevLow1;
    private double _prevLow2;
    private double _prevLow3;
    private double _prevLow4;

    public IndicatorName Name => IndicatorName.BelkhayateTiming;

    public void Reset()
    {
        _prevHigh1 = 0;
        _prevHigh2 = 0;
        _prevHigh3 = 0;
        _prevHigh4 = 0;
        _prevLow1 = 0;
        _prevLow2 = 0;
        _prevLow3 = 0;
        _prevLow4 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = bar.Close;
        var currentHigh = bar.High;
        var currentLow = bar.Low;
        var middle = (((currentHigh + currentLow) / 2) + ((_prevHigh1 + _prevLow1) / 2) +
                      ((_prevHigh2 + _prevLow2) / 2) + ((_prevHigh3 + _prevLow3) / 2) +
                      ((_prevHigh4 + _prevLow4) / 2)) / 5;
        var scale = ((currentHigh - currentLow + (_prevHigh1 - _prevLow1) + (_prevHigh2 - _prevLow2) +
                      (_prevHigh3 - _prevLow3) + (_prevHigh4 - _prevLow4)) / 5) * 0.2;
        var b = scale != 0 ? (currentValue - middle) / scale : 0;

        if (isFinal)
        {
            _prevHigh4 = _prevHigh3;
            _prevHigh3 = _prevHigh2;
            _prevHigh2 = _prevHigh1;
            _prevHigh1 = currentHigh;
            _prevLow4 = _prevLow3;
            _prevLow3 = _prevLow2;
            _prevLow2 = _prevLow1;
            _prevLow1 = currentLow;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Belkhayate", b }
            };
        }

        return new StreamingIndicatorStateResult(b, outputs);
    }
}

public sealed class ChartmillValueIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _inputSmoother;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _lengthSqrt;
    private double _prevClose;
    private bool _hasPrev;

    public ChartmillValueIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        InputName inputName = InputName.MedianPrice, int length = 5)
    {
        var resolved = Math.Max(1, length);
        _inputSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _lengthSqrt = MathHelper.Pow(resolved, 0.5);
    }

    public ChartmillValueIndicatorState(MovingAvgType maType, InputName inputName, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _inputSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, selector);
        _lengthSqrt = MathHelper.Pow(resolved, 0.5);
    }

    public IndicatorName Name => IndicatorName.ChartmillValueIndicator;

    public void Reset()
    {
        _inputSmoother.Reset();
        _atrSmoother.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var input = _input.GetValue(bar);
        var f = _inputSmoother.Next(input, isFinal);
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atrSmoother.Next(tr, isFinal);

        var denom = atr * _lengthSqrt;
        var cmvC = denom != 0 ? MathHelper.MinOrMax((bar.Close - f) / denom, 1, -1) : 0;
        var cmvO = denom != 0 ? MathHelper.MinOrMax((bar.Open - f) / denom, 1, -1) : 0;
        var cmvH = denom != 0 ? MathHelper.MinOrMax((bar.High - f) / denom, 1, -1) : 0;
        var cmvL = denom != 0 ? MathHelper.MinOrMax((bar.Low - f) / denom, 1, -1) : 0;

        if (isFinal)
        {
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Cmvc", cmvC },
                { "Cmvo", cmvO },
                { "Cmvh", cmvH },
                { "Cmvl", cmvL }
            };
        }

        return new StreamingIndicatorStateResult(cmvC, outputs);
    }

    public void Dispose()
    {
        _inputSmoother.Dispose();
        _atrSmoother.Dispose();
    }
}

public sealed class ConditionalAccumulatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signal;
    private readonly double _increment;
    private double _value;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public ConditionalAccumulatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, double increment = 1)
    {
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _increment = increment;
    }

    public IndicatorName Name => IndicatorName.ConditionalAccumulator;

    public void Reset()
    {
        _signal.Reset();
        _value = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var value = bar.Low >= prevHigh ? _value + _increment
            : bar.High <= prevLow ? _value - _increment
            : _value;
        var signal = _signal.Next(value, isFinal);

        if (isFinal)
        {
            _value = value;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ca", value },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}

public sealed class DetrendedPriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _window;
    private readonly int _prevPeriods;

    public DetrendedPriceOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _prevPeriods = MathHelper.MinOrMax((int)Math.Ceiling(((double)resolved / 2) + 1));
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _window = new PooledRingBuffer<double>(_prevPeriods);
    }

    public DetrendedPriceOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _prevPeriods = MathHelper.MinOrMax((int)Math.Ceiling(((double)resolved / 2) + 1));
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _window = new PooledRingBuffer<double>(_prevPeriods);
    }

    public IndicatorName Name => IndicatorName.DetrendedPriceOscillator;

    public void Reset()
    {
        _smoother.Reset();
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smoother.Next(value, isFinal);
        var prevValue = _window.Count >= _prevPeriods ? _window[0] : 0;
        var dpo = prevValue - sma;

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dpo", dpo }
            };
        }

        return new StreamingIndicatorStateResult(dpo, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _window.Dispose();
    }
}

public sealed class AdaptiveErgodicCandlestickOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _mep;
    private readonly double _ce;
    private readonly IMovingAverageSmoother _signal;
    private readonly StochasticOscillatorState _stoch;
    private double _came1;
    private double _came2;
    private double _came11;
    private double _came22;
    private int _index;

    public AdaptiveErgodicCandlestickOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int smoothLength = 5, int stochLength = 14, int signalLength = 9)
    {
        _mep = (double)2 / (Math.Max(1, smoothLength) + 1);
        _ce = (stochLength + smoothLength) * 2d;
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _stoch = new StochasticOscillatorState(maType, stochLength, 3, 3);
    }

    public IndicatorName Name => IndicatorName.AdaptiveErgodicCandlestickOscillator;

    public void Reset()
    {
        _signal.Reset();
        _stoch.Reset();
        _came1 = 0;
        _came2 = 0;
        _came11 = 0;
        _came22 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var stoch = _stoch.Update(bar, isFinal, includeOutputs: false).Value;
        var vrb = Math.Abs(stoch - 50) / 50;
        var currentHigh = bar.High;
        var currentLow = bar.Low;
        var currentOpen = bar.Open;
        var currentClose = bar.Close;

        var useSeed = _index < _ce;
        var came1 = useSeed ? currentClose - currentOpen : _came1 + (_mep * vrb * (currentClose - currentOpen - _came1));
        var came2 = useSeed ? currentHigh - currentLow : _came2 + (_mep * vrb * (currentHigh - currentLow - _came2));
        var came11 = useSeed ? came1 : _came11 + (_mep * vrb * (came1 - _came11));
        var came22 = useSeed ? came2 : _came22 + (_mep * vrb * (came2 - _came22));
        var eco = came22 != 0 ? came11 / came22 * 100 : 0;
        var signal = _signal.Next(eco, isFinal);

        if (isFinal)
        {
            _came1 = came1;
            _came2 = came2;
            _came11 = came11;
            _came22 = came22;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eco", eco },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(eco, outputs);
    }

    public void Dispose()
    {
        _signal.Dispose();
        _stoch.Dispose();
    }
}

public sealed class AbsoluteStrengthMTFIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _price1;
    private readonly IMovingAverageSmoother _price2;
    private readonly IMovingAverageSmoother _bulls;
    private readonly IMovingAverageSmoother _bears;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public AbsoluteStrengthMTFIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50,
        int smoothLength = 25, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        _price1 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _price2 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _bulls = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _bears = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public AbsoluteStrengthMTFIndicatorState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        _price1 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _price2 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _bulls = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _bears = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AbsoluteStrengthMTFIndicator;

    public void Reset()
    {
        _price1.Reset();
        _price2.Reset();
        _bulls.Reset();
        _bears.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var price1 = _price1.Next(value, isFinal);
        var price2 = _price2.Next(prevValue, isFinal);
        var diff = price1 - price2;
        var bulls0 = 0.5 * (Math.Abs(diff) + diff);
        var bears0 = 0.5 * (Math.Abs(diff) - diff);
        var bulls = _bulls.Next(bulls0, isFinal);
        var bears = _bears.Next(bears0, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Bulls", bulls },
                { "Bears", bears }
            };
        }

        return new StreamingIndicatorStateResult(bulls, outputs);
    }

    public void Dispose()
    {
        _price1.Dispose();
        _price2.Dispose();
        _bulls.Dispose();
        _bears.Dispose();
    }
}

public sealed class AroonOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public AroonOscillatorState(int length = 25, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AroonOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AroonOscillator;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var count = _window.Count;
        var willEvict = count == _window.Capacity;
        var startIndex = willEvict ? 1 : 0;
        var max = double.MinValue;
        var min = double.MaxValue;
        var maxIndex = 0;
        var minIndex = 0;
        var virtualIndex = 0;

        for (var i = startIndex; i < count; i++)
        {
            var windowValue = _window[i];
            if (windowValue >= max)
            {
                max = windowValue;
                maxIndex = virtualIndex;
            }

            if (windowValue <= min)
            {
                min = windowValue;
                minIndex = virtualIndex;
            }

            virtualIndex++;
        }

        if (value >= max)
        {
            max = value;
            maxIndex = virtualIndex;
        }

        if (value <= min)
        {
            min = value;
            minIndex = virtualIndex;
        }

        var countAfter = virtualIndex + 1;
        var daysSinceMax = countAfter - 1 - maxIndex;
        var daysSinceMin = countAfter - 1 - minIndex;
        var aroonUp = (double)(_length - daysSinceMax) / _length * 100;
        var aroonDown = (double)(_length - daysSinceMin) / _length * 100;
        var aroon = aroonUp - aroonDown;

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Aroon", aroon }
            };
        }

        return new StreamingIndicatorStateResult(aroon, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

public sealed class BearPowerIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signal;
    private double _prevClose;
    private bool _hasPrev;

    public BearPowerIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.BearPowerIndicator;

    public void Reset()
    {
        _signal.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = bar.Close;
        var prevClose = _hasPrev ? _prevClose : 0;
        var open = bar.Open;
        var high = bar.High;
        var low = bar.Low;

        var bpi = close < open ? high - low : prevClose > open ? Math.Max(close - open, high - low) :
            close > open ? Math.Max(open - low, high - close) : prevClose > open ? Math.Max(prevClose - low, high - close) :
            high - close > close - low ? high - low : prevClose > open ? Math.Max(prevClose - open, high - low) :
            high - close < close - low ? open - low : close > open ? Math.Max(close - low, high - close) :
            close > open ? Math.Max(prevClose - open, high - close) : prevClose < open ? Math.Max(open - low, high - close) : high - low;
        var signal = _signal.Next(bpi, isFinal);

        if (isFinal)
        {
            _prevClose = close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "BearPower", bpi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(bpi, outputs);
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}

public sealed class BullPowerIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signal;
    private double _prevClose;
    private bool _hasPrev;

    public BullPowerIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.BullPowerIndicator;

    public void Reset()
    {
        _signal.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = bar.Close;
        var prevClose = _hasPrev ? _prevClose : 0;
        var open = bar.Open;
        var high = bar.High;
        var low = bar.Low;

        var bpi = close < open ? Math.Max(high - open, close - low) : prevClose < open ? Math.Max(high - prevClose, close - low) :
            close > open ? Math.Max(open - prevClose, high - low) : prevClose > open ? high - low :
            high - close > close - low ? high - open : prevClose < open ? Math.Max(high - prevClose, close - low) :
            high - close < close - low ? Math.Max(open - close, high - low) : prevClose > open ? high - low :
            prevClose > open ? Math.Max(high - open, close - low) : prevClose < open ? Math.Max(open - close, high - low) : high - low;
        var signal = _signal.Next(bpi, isFinal);

        if (isFinal)
        {
            _prevClose = close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "BullPower", bpi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(bpi, outputs);
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}

public sealed class ContractHighLowState : IStreamingIndicatorState
{
    private double _conHi;
    private double _conLow;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.ContractHighLow;

    public void Reset()
    {
        _conHi = 0;
        _conLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var conHi = _hasPrev ? Math.Max(_conHi, bar.High) : bar.High;
        var conLow = _hasPrev ? Math.Min(_conLow, bar.Low) : bar.Low;

        if (isFinal)
        {
            _conHi = conHi;
            _conLow = conLow;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ch", conHi },
                { "Cl", conLow }
            };
        }

        return new StreamingIndicatorStateResult(conHi, outputs);
    }
}

public sealed class ChopZoneState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _ema;
    private readonly StreamingInputResolver _input;
    private double _prevEma;
    private bool _hasPrevEma;

    public ChopZoneState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        InputName inputName = InputName.TypicalPrice, int length1 = 30, int length2 = 34)
    {
        var resolved1 = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChopZoneState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChopZone;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _ema.Reset();
        _prevEma = 0;
        _hasPrevEma = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var ema = _ema.Next(bar.Close, isFinal);
        var prevEma = _hasPrevEma ? _prevEma : 0;
        var range = highest - lowest != 0 ? 25 / (highest - lowest) * lowest : 0;
        var avg = _input.GetValue(bar);
        var y = avg != 0 && range != 0 ? (prevEma - ema) / avg * range : 0;
        var c = Math.Sqrt(1 + (y * y));
        var emaAngle1 = c != 0 ? Math.Round(Math.Acos(1 / c).ToDegrees()) : 0;
        var emaAngle = y > 0 ? -emaAngle1 : emaAngle1;

        if (isFinal)
        {
            _prevEma = ema;
            _hasPrevEma = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cz", emaAngle }
            };
        }

        return new StreamingIndicatorStateResult(emaAngle, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _ema.Dispose();
    }
}

public sealed class CenterOfLinearityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _sumWindow;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;
    private int _index;

    public CenterOfLinearityState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sumWindow = new RollingWindowSum(_length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public CenterOfLinearityState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sumWindow = new RollingWindowSum(_length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.CenterOfLinearity;

    public void Reset()
    {
        _sumWindow.Reset();
        _window.Clear();
        _prevValue = 0;
        _hasPrev = false;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priorValue = _window.Count >= _length ? _window[0] : 0;
        var a = (_index + 1) * (priorValue - prevValue);
        var sum = isFinal ? _sumWindow.Add(a, out _) : _sumWindow.Preview(a, out _);
        var col = sum;

        if (isFinal)
        {
            _window.TryAdd(value, out _);
            _prevValue = value;
            _hasPrev = true;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Col", col }
            };
        }

        return new StreamingIndicatorStateResult(col, outputs);
    }

    public void Dispose()
    {
        _sumWindow.Dispose();
        _window.Dispose();
    }
}

public sealed class ChaikinVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly IMovingAverageSmoother _ema;
    private readonly PooledRingBuffer<double> _window;

    public ChaikinVolatilityState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 10, int length2 = 12)
    {
        _length2 = Math.Max(1, length2);
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _window = new PooledRingBuffer<double>(_length2);
    }

    public IndicatorName Name => IndicatorName.ChaikinVolatility;

    public void Reset()
    {
        _ema.Reset();
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var highLow = bar.High - bar.Low;
        var highLowEma = _ema.Next(highLow, isFinal);
        var prevHighLowEma = _window.Count >= _length2 ? _window[0] : 0;
        var chaikinVolatility = prevHighLowEma != 0 ? (highLowEma - prevHighLowEma) / prevHighLowEma * 100 : 0;

        if (isFinal)
        {
            _window.TryAdd(highLowEma, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cv", chaikinVolatility }
            };
        }

        return new StreamingIndicatorStateResult(chaikinVolatility, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _window.Dispose();
    }
}

public sealed class CoppockCurveState : IStreamingIndicatorState, IDisposable
{
    private readonly RateOfChangeState _rocFast;
    private readonly int _slowLength;
    private readonly PooledRingBuffer<double> _rocFastWindow;
    private readonly IMovingAverageSmoother _smoother;

    public CoppockCurveState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10, int fastLength = 11,
        int slowLength = 14, InputName inputName = InputName.Close)
    {
        _rocFast = new RateOfChangeState(Math.Max(1, fastLength), inputName);
        _slowLength = Math.Max(1, slowLength);
        _rocFastWindow = new PooledRingBuffer<double>(_slowLength);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public CoppockCurveState(MovingAvgType maType, int length, int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _rocFast = new RateOfChangeState(Math.Max(1, fastLength), selector);
        _slowLength = Math.Max(1, slowLength);
        _rocFastWindow = new PooledRingBuffer<double>(_slowLength);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.CoppockCurve;

    public void Reset()
    {
        _rocFast.Reset();
        _rocFastWindow.Clear();
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var rocFast = _rocFast.Update(bar, isFinal, includeOutputs: false).Value;
        var prevRocFast = _rocFastWindow.Count >= _slowLength ? _rocFastWindow[0] : 0;
        var rocSlow = prevRocFast != 0 ? (rocFast - prevRocFast) / prevRocFast * 100 : 0;
        if (isFinal)
        {
            _rocFastWindow.TryAdd(rocFast, out _);
        }
        var rocTotal = rocFast + rocSlow;
        var coppock = _smoother.Next(rocTotal, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cc", coppock }
            };
        }

        return new StreamingIndicatorStateResult(coppock, outputs);
    }

    public void Dispose()
    {
        _rocFast.Dispose();
        _rocFastWindow.Dispose();
        _smoother.Dispose();
    }
}

public sealed class CommoditySelectionIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _k;
    private readonly IMovingAverageSmoother _atr;
    private readonly IMovingAverageSmoother _dmPlus;
    private readonly IMovingAverageSmoother _dmMinus;
    private readonly IMovingAverageSmoother _tr;
    private readonly IMovingAverageSmoother _adx;
    private readonly RollingWindowMax _atrHighWindow;
    private readonly RollingWindowMin _atrLowWindow;
    private readonly RollingWindowSum _sumWindow;
    private double _prevClose;
    private double _prevAtr;
    private bool _hasPrev;
    private bool _hasPrevAtr;
    private double _prevAtrHigh;
    private double _prevAtrLow;
    private bool _hasPrevAtrRange;

    public CommoditySelectionIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14,
        double pointValue = 50, double margin = 3000, double commission = 10)
    {
        _length = Math.Max(1, length);
        _k = 100 * (pointValue / Math.Sqrt(margin) / (150 + commission));
        _atr = MovingAverageSmootherFactory.Create(maType, _length);
        _dmPlus = MovingAverageSmootherFactory.Create(maType, _length);
        _dmMinus = MovingAverageSmootherFactory.Create(maType, _length);
        _tr = MovingAverageSmootherFactory.Create(maType, _length);
        _adx = MovingAverageSmootherFactory.Create(maType, _length);
        _atrHighWindow = new RollingWindowMax(2);
        _atrLowWindow = new RollingWindowMin(2);
        _sumWindow = new RollingWindowSum(_length);
    }

    public IndicatorName Name => IndicatorName.CommoditySelectionIndex;

    public void Reset()
    {
        _atr.Reset();
        _dmPlus.Reset();
        _dmMinus.Reset();
        _tr.Reset();
        _adx.Reset();
        _atrHighWindow.Reset();
        _atrLowWindow.Reset();
        _sumWindow.Reset();
        _prevClose = 0;
        _prevAtr = 0;
        _hasPrev = false;
        _hasPrevAtr = false;
        _prevAtrHigh = 0;
        _prevAtrLow = 0;
        _hasPrevAtrRange = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentHigh = bar.High;
        var currentLow = bar.Low;
        var currentClose = bar.Close;
        var prevClose = _hasPrev ? _prevClose : 0;
        var trRaw = CalculationsHelper.CalculateTrueRange(currentHigh, currentLow, prevClose);
        var atr = _atr.Next(trRaw, isFinal);
        var atrHigh = isFinal ? _atrHighWindow.Add(atr, out _) : _atrHighWindow.Preview(atr, out _);
        var atrLow = isFinal ? _atrLowWindow.Add(atr, out _) : _atrLowWindow.Preview(atr, out _);
        var prevAtrHigh = _hasPrevAtrRange ? _prevAtrHigh : 0;
        var prevAtrLow = _hasPrevAtrRange ? _prevAtrLow : 0;
        var highDiff = atrHigh - prevAtrHigh;
        var lowDiff = prevAtrLow - atrLow;
        var dmPlusRaw = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
        var dmMinusRaw = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
        var prevAtr = _hasPrevAtr ? _prevAtr : 0;
        var trRawAdx = CalculationsHelper.CalculateTrueRange(atrHigh, atrLow, prevAtr);

        var dmPlus = _dmPlus.Next(dmPlusRaw, isFinal);
        var dmMinus = _dmMinus.Next(dmMinusRaw, isFinal);
        var tr = _tr.Next(trRawAdx, isFinal);
        var diPlus = tr != 0 ? MathHelper.MinOrMax(100 * dmPlus / tr, 100, 0) : 0;
        var diMinus = tr != 0 ? MathHelper.MinOrMax(100 * dmMinus / tr, 100, 0) : 0;
        var diDiff = Math.Abs(diPlus - diMinus);
        var diSum = diPlus + diMinus;
        var di = diSum != 0 ? MathHelper.MinOrMax(100 * diDiff / diSum, 100, 0) : 0;
        var adx = _adx.Next(di, isFinal);
        var csi = _k * atr * adx;
        var sum = isFinal ? _sumWindow.Add(csi, out var countAfter) : _sumWindow.Preview(csi, out countAfter);
        var csiSma = countAfter > 0 ? sum / countAfter : 0;

        if (isFinal)
        {
            _prevClose = currentClose;
            _prevAtr = atr;
            _hasPrev = true;
            _hasPrevAtr = true;
            _prevAtrHigh = atrHigh;
            _prevAtrLow = atrLow;
            _hasPrevAtrRange = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Csi", csi },
                { "Signal", csiSma }
            };
        }

        return new StreamingIndicatorStateResult(csi, outputs);
    }

    public void Dispose()
    {
        _atr.Dispose();
        _dmPlus.Dispose();
        _dmMinus.Dispose();
        _tr.Dispose();
        _adx.Dispose();
        _atrHighWindow.Dispose();
        _atrLowWindow.Dispose();
        _sumWindow.Dispose();
    }
}

public sealed class DeltaMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _openWindow;
    private readonly StreamingInputResolver _input;

    public DeltaMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10, int length2 = 5,
        InputName inputName = InputName.Close)
    {
        _length2 = Math.Max(1, length2);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _openWindow = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DeltaMovingAverageState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length2 = Math.Max(1, length2);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _openWindow = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DeltaMovingAverage;

    public void Reset()
    {
        _smoother.Reset();
        _openWindow.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentClose = _input.GetValue(bar);
        var prevOpen = _openWindow.Count >= _length2 ? _openWindow[0] : 0;
        var delta = currentClose - prevOpen;
        var deltaSma = _smoother.Next(delta, isFinal);
        var histogram = delta - deltaSma;

        if (isFinal)
        {
            _openWindow.TryAdd(bar.Open, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Delta", delta },
                { "Signal", deltaSma },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(delta, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _openWindow.Dispose();
    }
}

public sealed class DetrendedSyntheticPriceState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private double _ema1;
    private double _ema2;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;
    private bool _hasEma;

    public DetrendedSyntheticPriceState(int length = 14)
    {
        _alpha = length > 2 ? (double)2 / (Math.Max(1, length) + 1) : 0.67;
    }

    public IndicatorName Name => IndicatorName.DetrendedSyntheticPrice;

    public void Reset()
    {
        _ema1 = 0;
        _ema2 = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
        _hasEma = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var high = Math.Max(bar.High, prevHigh);
        var low = Math.Min(bar.Low, prevLow);
        var price = (high + low) / 2;
        var prevEma1 = _hasEma ? _ema1 : price;
        var prevEma2 = _hasEma ? _ema2 : price;
        var ema1 = (_alpha * price) + ((1 - _alpha) * prevEma1);
        var ema2 = ((_alpha / 2) * price) + ((1 - (_alpha / 2)) * prevEma2);
        var dsp = ema1 - ema2;

        if (isFinal)
        {
            _ema1 = ema1;
            _ema2 = ema2;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
            _hasEma = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dsp", dsp }
            };
        }

        return new StreamingIndicatorStateResult(dsp, outputs);
    }
}

public sealed class DerivativeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly RollingWindowSum _sumWindow;
    private readonly StreamingInputResolver _input;

    public DerivativeOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14, int length2 = 9,
        int length3 = 5, int length4 = 3, InputName inputName = InputName.Close)
    {
        _rsi = new RsiState(maType, Math.Max(1, length1));
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _sumWindow = new RollingWindowSum(Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DerivativeOscillatorState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _rsi = new RsiState(maType, Math.Max(1, length1));
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _sumWindow = new RollingWindowSum(Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DerivativeOscillator;

    public void Reset()
    {
        _rsi.Reset();
        _ema1.Reset();
        _ema2.Reset();
        _sumWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var ema1 = _ema1.Next(rsi, isFinal);
        var ema2 = _ema2.Next(ema1, isFinal);
        var sum = isFinal ? _sumWindow.Add(ema2, out var countAfter) : _sumWindow.Preview(ema2, out countAfter);
        var s1Sma = countAfter > 0 ? sum / countAfter : 0;
        var s2 = ema2 - s1Sma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Do", s2 }
            };
        }

        return new StreamingIndicatorStateResult(s2, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _ema1.Dispose();
        _ema2.Dispose();
        _sumWindow.Dispose();
    }
}

public sealed class DemandOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _rangeSmoother;
    private readonly IMovingAverageSmoother _doSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public DemandOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 10, int length2 = 2,
        int length3 = 20, InputName inputName = InputName.Close)
    {
        var resolved2 = Math.Max(1, length2);
        _highWindow = new RollingWindowMax(resolved2);
        _lowWindow = new RollingWindowMin(resolved2);
        _rangeSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _doSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DemandOscillatorState(MovingAvgType maType, int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved2 = Math.Max(1, length2);
        _highWindow = new RollingWindowMax(resolved2);
        _lowWindow = new RollingWindowMin(resolved2);
        _rangeSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _doSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DemandOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _rangeSmoother.Reset();
        _doSmoother.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var range = highest - lowest;
        var va = _rangeSmoother.Next(range, isFinal);
        var currentValue = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var pctChg = prevValue != 0 ? (currentValue - prevValue) / Math.Abs(prevValue) * 100 : 0;
        var k = va != 0 ? (3 * currentValue) / va : 0;
        var pctK = pctChg * k;
        var volPctK = pctK != 0 ? bar.Volume / pctK : 0;
        var bp = currentValue > prevValue ? bar.Volume : volPctK;
        var sp = currentValue > prevValue ? volPctK : bar.Volume;
        var dosc = bp - sp;
        var doEma = _doSmoother.Next(dosc, isFinal);
        var doSignal = _signalSmoother.Next(doEma, isFinal);

        if (isFinal)
        {
            _prevValue = currentValue;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Do", doEma },
                { "Signal", doSignal }
            };
        }

        return new StreamingIndicatorStateResult(doEma, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _rangeSmoother.Dispose();
        _doSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class DoubleSmoothedMomentaState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _topSmoother1;
    private readonly IMovingAverageSmoother _topSmoother2;
    private readonly IMovingAverageSmoother _botSmoother1;
    private readonly IMovingAverageSmoother _botSmoother2;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public DoubleSmoothedMomentaState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2, int length2 = 5,
        int length3 = 25, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        _maxWindow = new RollingWindowMax(resolved1);
        _minWindow = new RollingWindowMin(resolved1);
        _topSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _topSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _botSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _botSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DoubleSmoothedMomentaState(MovingAvgType maType, int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        _maxWindow = new RollingWindowMax(resolved1);
        _minWindow = new RollingWindowMin(resolved1);
        _topSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _topSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _botSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _botSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DoubleSmoothedMomenta;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _topSmoother1.Reset();
        _topSmoother2.Reset();
        _botSmoother1.Reset();
        _botSmoother2.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var high = isFinal ? _maxWindow.Add(currentValue, out _) : _maxWindow.Preview(currentValue, out _);
        var low = isFinal ? _minWindow.Add(currentValue, out _) : _minWindow.Preview(currentValue, out _);
        var srcLc = currentValue - low;
        var hcLc = high - low;
        var top1 = _topSmoother1.Next(srcLc, isFinal);
        var top2 = _topSmoother2.Next(top1, isFinal);
        var bot1 = _botSmoother1.Next(hcLc, isFinal);
        var bot2 = _botSmoother2.Next(bot1, isFinal);
        var mom = bot2 != 0 ? MathHelper.MinOrMax(100 * top2 / bot2, 100, 0) : 0;
        var signal = _signal.Next(mom, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dsm", mom },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mom, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _topSmoother1.Dispose();
        _topSmoother2.Dispose();
        _botSmoother1.Dispose();
        _botSmoother2.Dispose();
        _signal.Dispose();
    }
}

public sealed class DidiIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _shortSma;
    private readonly IMovingAverageSmoother _mediumSma;
    private readonly IMovingAverageSmoother _longSma;
    private readonly StreamingInputResolver _input;

    public DidiIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 3, int length2 = 8, int length3 = 20,
        InputName inputName = InputName.Close)
    {
        _shortSma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _mediumSma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _longSma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DidiIndexState(MovingAvgType maType, int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _shortSma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _mediumSma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _longSma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DidiIndex;

    public void Reset()
    {
        _shortSma.Reset();
        _mediumSma.Reset();
        _longSma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mediumSma = _mediumSma.Next(value, isFinal);
        var shortSma = _shortSma.Next(value, isFinal);
        var longSma = _longSma.Next(value, isFinal);
        var curta = mediumSma != 0 ? shortSma / mediumSma : 0;
        var longa = mediumSma != 0 ? longSma / mediumSma : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Curta", curta },
                { "Media", mediumSma },
                { "Longa", longSma }
            };
        }

        return new StreamingIndicatorStateResult(curta, outputs);
    }

    public void Dispose()
    {
        _shortSma.Dispose();
        _mediumSma.Dispose();
        _longSma.Dispose();
    }
}

public sealed class DisparityIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;

    public DisparityIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, InputName inputName = InputName.Close)
    {
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DisparityIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DisparityIndex;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smoother.Next(value, isFinal);
        var disparity = sma != 0 ? (value - sma) / sma * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Di", disparity }
            };
        }

        return new StreamingIndicatorStateResult(disparity, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class DampingIndexState : IStreamingIndicatorState, IDisposable
{
    private const int RangeLookback = 6;
    private readonly IMovingAverageSmoother _rangeSmoother;
    private readonly PooledRingBuffer<double> _rangeWindow;

    public DampingIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 5, double threshold = 1.5)
    {
        _rangeSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _rangeWindow = new PooledRingBuffer<double>(RangeLookback);
    }

    public IndicatorName Name => IndicatorName.DampingIndex;

    public void Reset()
    {
        _rangeSmoother.Reset();
        _rangeWindow.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var range = bar.High - bar.Low;
        var rangeSma = _rangeSmoother.Next(range, isFinal);
        var prevSma1 = _rangeWindow.Count >= 1 ? _rangeWindow[_rangeWindow.Count - 1] : 0;
        var prevSma6 = _rangeWindow.Count >= RangeLookback ? _rangeWindow[_rangeWindow.Count - RangeLookback] : 0;
        var di = prevSma6 != 0 ? prevSma1 / prevSma6 : 0;

        if (isFinal)
        {
            _rangeWindow.TryAdd(rangeSma, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Di", di }
            };
        }

        return new StreamingIndicatorStateResult(di, outputs);
    }

    public void Dispose()
    {
        _rangeSmoother.Dispose();
        _rangeWindow.Dispose();
    }
}

public sealed class DirectionalTrendIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _diffEma1;
    private readonly IMovingAverageSmoother _absDiffEma1;
    private readonly IMovingAverageSmoother _diffEma2;
    private readonly IMovingAverageSmoother _absDiffEma2;
    private readonly IMovingAverageSmoother _diffEma3;
    private readonly IMovingAverageSmoother _absDiffEma3;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public DirectionalTrendIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14, int length2 = 10, int length3 = 5)
    {
        _diffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absDiffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _diffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absDiffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _absDiffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
    }

    public IndicatorName Name => IndicatorName.DirectionalTrendIndex;

    public void Reset()
    {
        _diffEma1.Reset();
        _absDiffEma1.Reset();
        _diffEma2.Reset();
        _absDiffEma2.Reset();
        _diffEma3.Reset();
        _absDiffEma3.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var hmu = bar.High - prevHigh > 0 ? bar.High - prevHigh : 0;
        var lmd = bar.Low - prevLow < 0 ? (bar.Low - prevLow) * -1 : 0;
        var diff = hmu - lmd;
        var absDiff = Math.Abs(diff);

        var diffEma1 = _diffEma1.Next(diff, isFinal);
        var absDiffEma1 = _absDiffEma1.Next(absDiff, isFinal);
        var diffEma2 = _diffEma2.Next(diffEma1, isFinal);
        var absDiffEma2 = _absDiffEma2.Next(absDiffEma1, isFinal);
        var diffEma3 = _diffEma3.Next(diffEma2, isFinal);
        var absDiffEma3 = _absDiffEma3.Next(absDiffEma2, isFinal);
        var dti = absDiffEma3 != 0 ? MathHelper.MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dti", dti }
            };
        }

        return new StreamingIndicatorStateResult(dti, outputs);
    }

    public void Dispose()
    {
        _diffEma1.Dispose();
        _absDiffEma1.Dispose();
        _diffEma2.Dispose();
        _absDiffEma2.Dispose();
        _diffEma3.Dispose();
        _absDiffEma3.Dispose();
    }
}

public sealed class DTOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _wima;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly RollingWindowSum _stoSum;
    private readonly RollingWindowSum _skSum;
    private readonly StreamingInputResolver _input;

    public DTOscillatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length1 = 13, int length2 = 8,
        int length3 = 5, int length4 = 3, InputName inputName = InputName.Close)
    {
        _wima = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _maxWindow = new RollingWindowMax(Math.Max(1, length2));
        _minWindow = new RollingWindowMin(Math.Max(1, length2));
        _stoSum = new RollingWindowSum(Math.Max(1, length3));
        _skSum = new RollingWindowSum(Math.Max(1, length4));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DTOscillatorState(MovingAvgType maType, int length1, int length2, int length3, int length4, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _wima = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _maxWindow = new RollingWindowMax(Math.Max(1, length2));
        _minWindow = new RollingWindowMin(Math.Max(1, length2));
        _stoSum = new RollingWindowSum(Math.Max(1, length3));
        _skSum = new RollingWindowSum(Math.Max(1, length4));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DTOscillator;

    public void Reset()
    {
        _wima.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _stoSum.Reset();
        _skSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wima = _wima.Next(value, isFinal);
        var highest = isFinal ? _maxWindow.Add(wima, out _) : _maxWindow.Preview(wima, out _);
        var lowest = isFinal ? _minWindow.Add(wima, out _) : _minWindow.Preview(wima, out _);
        var stoRsi = highest - lowest != 0 ? MathHelper.MinOrMax(100 * (wima - lowest) / (highest - lowest), 100, 0) : 0;
        var stoSum = isFinal ? _stoSum.Add(stoRsi, out var stoCount) : _stoSum.Preview(stoRsi, out stoCount);
        var sk = stoCount > 0 ? stoSum / stoCount : 0;
        var skSum = isFinal ? _skSum.Add(sk, out var skCount) : _skSum.Preview(sk, out skCount);
        var sd = skCount > 0 ? skSum / skCount : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dto", sk },
                { "Signal", sd }
            };
        }

        return new StreamingIndicatorStateResult(sk, outputs);
    }

    public void Dispose()
    {
        _wima.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _stoSum.Dispose();
        _skSum.Dispose();
    }
}

public sealed class RateOfChangeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public RateOfChangeState(int length = 12, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RateOfChangeState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RateOfChange;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var current = _input.GetValue(bar);
        double prevValue = 0;
        if (_window.Count >= _length)
        {
            prevValue = _window[0];
        }

        var roc = prevValue != 0 ? (current - prevValue) / prevValue * 100 : 0;
        if (isFinal)
        {
            _window.TryAdd(current, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Roc", roc }
            };
        }

        return new StreamingIndicatorStateResult(roc, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

public sealed class UlcerIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowSum _drawdownSum;
    private readonly StreamingInputResolver _input;

    public UlcerIndexState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(_length);
        _drawdownSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public UlcerIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(_length);
        _drawdownSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.UlcerIndex;

    public void Reset()
    {
        _maxWindow.Reset();
        _drawdownSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var maxValue = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);

        var pctDrawdownSquared = maxValue != 0 ? MathHelper.Pow((value - maxValue) / maxValue * 100, 2) : 0;

        int sumCount;
        var sum = isFinal ? _drawdownSum.Add(pctDrawdownSquared, out sumCount)
            : _drawdownSum.Preview(pctDrawdownSquared, out sumCount);
        var denom = Math.Min(sumCount, _length);
        var average = denom > 0 ? sum / denom : 0;
        var ulcer = MathHelper.Sqrt(average);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ui", ulcer }
            };
        }

        return new StreamingIndicatorStateResult(ulcer, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _drawdownSum.Dispose();
    }
}

public sealed class VortexIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _vmPlusSum;
    private readonly RollingWindowSum _vmMinusSum;
    private readonly RollingWindowSum _trSum;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    public VortexIndicatorState(int length = 14)
    {
        var resolved = Math.Max(1, length);
        _vmPlusSum = new RollingWindowSum(resolved);
        _vmMinusSum = new RollingWindowSum(resolved);
        _trSum = new RollingWindowSum(resolved);
    }

    public IndicatorName Name => IndicatorName.VortexIndicator;

    public void Reset()
    {
        _vmPlusSum.Reset();
        _vmMinusSum.Reset();
        _trSum.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;

        var vmPlus = Math.Abs(bar.High - prevLow);
        var vmMinus = Math.Abs(bar.Low - prevHigh);
        var trueRange = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);

        int _;
        var vmPlusTotal = isFinal ? _vmPlusSum.Add(vmPlus, out _) : _vmPlusSum.Preview(vmPlus, out _);
        var vmMinusTotal = isFinal ? _vmMinusSum.Add(vmMinus, out _) : _vmMinusSum.Preview(vmMinus, out _);
        var trueRangeTotal = isFinal ? _trSum.Add(trueRange, out _) : _trSum.Preview(trueRange, out _);

        var viPlus = trueRangeTotal != 0 ? vmPlusTotal / trueRangeTotal : 0;
        var viMinus = trueRangeTotal != 0 ? vmMinusTotal / trueRangeTotal : 0;

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "ViPlus", viPlus },
                { "ViMinus", viMinus }
            };
        }

        return new StreamingIndicatorStateResult(viPlus, outputs);
    }

    public void Dispose()
    {
        _vmPlusSum.Dispose();
        _vmMinusSum.Dispose();
        _trSum.Dispose();
    }
}

public sealed class AwesomeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly RollingWindowSum _fastSum;
    private readonly RollingWindowSum _slowSum;
    private readonly StreamingInputResolver _input;

    public AwesomeOscillatorState(int fastLength = 5, int slowLength = 34, InputName inputName = InputName.MedianPrice)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AwesomeOscillatorState(int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _input = new StreamingInputResolver(InputName.MedianPrice, selector);
    }

    public IndicatorName Name => IndicatorName.AwesomeOscillator;

    public void Reset()
    {
        _fastSum.Reset();
        _slowSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int fastCount;
        int slowCount;
        var fastSum = isFinal ? _fastSum.Add(value, out fastCount) : _fastSum.Preview(value, out fastCount);
        var slowSum = isFinal ? _slowSum.Add(value, out slowCount) : _slowSum.Preview(value, out slowCount);
        var fastSma = fastCount >= _fastLength ? fastSum / _fastLength : 0;
        var slowSma = slowCount >= _slowLength ? slowSum / _slowLength : 0;
        var ao = fastSma - slowSma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ao", ao }
            };
        }

        return new StreamingIndicatorStateResult(ao, outputs);
    }

    public void Dispose()
    {
        _fastSum.Dispose();
        _slowSum.Dispose();
    }
}

public sealed class AcceleratorOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly int _smoothLength;
    private readonly RollingWindowSum _fastSum;
    private readonly RollingWindowSum _slowSum;
    private readonly RollingWindowSum _smoothSum;
    private readonly StreamingInputResolver _input;

    public AcceleratorOscillatorState(int fastLength = 5, int slowLength = 34, int smoothLength = 5,
        InputName inputName = InputName.MedianPrice)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _smoothLength = Math.Max(1, smoothLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _smoothSum = new RollingWindowSum(_smoothLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AcceleratorOscillatorState(int fastLength, int slowLength, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _smoothLength = Math.Max(1, smoothLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _smoothSum = new RollingWindowSum(_smoothLength);
        _input = new StreamingInputResolver(InputName.MedianPrice, selector);
    }

    public IndicatorName Name => IndicatorName.AcceleratorOscillator;

    public void Reset()
    {
        _fastSum.Reset();
        _slowSum.Reset();
        _smoothSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int fastCount;
        int slowCount;
        var fastSum = isFinal ? _fastSum.Add(value, out fastCount) : _fastSum.Preview(value, out fastCount);
        var slowSum = isFinal ? _slowSum.Add(value, out slowCount) : _slowSum.Preview(value, out slowCount);
        var fastSma = fastCount >= _fastLength ? fastSum / _fastLength : 0;
        var slowSma = slowCount >= _slowLength ? slowSum / _slowLength : 0;
        var ao = fastSma - slowSma;

        int smoothCount;
        var smoothSum = isFinal ? _smoothSum.Add(ao, out smoothCount) : _smoothSum.Preview(ao, out smoothCount);
        var aoSma = smoothCount >= _smoothLength ? smoothSum / _smoothLength : 0;
        var ac = ao - aoSma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ac", ac }
            };
        }

        return new StreamingIndicatorStateResult(ac, outputs);
    }

    public void Dispose()
    {
        _fastSum.Dispose();
        _slowSum.Dispose();
        _smoothSum.Dispose();
    }
}

public sealed class TrixState : IStreamingIndicatorState
{
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;
    private readonly EmaState _ema3;
    private readonly StreamingInputResolver _input;
    private double _prevEma3;
    private bool _hasPrev;

    public TrixState(int length = 15, InputName inputName = InputName.Close)
    {
        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
        _ema3 = new EmaState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrixState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
        _ema3 = new EmaState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Trix;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _ema3.Reset();
        _prevEma3 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.GetNext(value, isFinal);
        var ema2 = _ema2.GetNext(ema1, isFinal);
        var ema3 = _ema3.GetNext(ema2, isFinal);
        var prevEma3 = _hasPrev ? _prevEma3 : 0;
        var trix = CalculationsHelper.CalculatePercentChange(ema3, prevEma3);

        if (isFinal)
        {
            _prevEma3 = ema3;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Trix", trix }
            };
        }

        return new StreamingIndicatorStateResult(trix, outputs);
    }
}

public sealed class _1LCLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _sma;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly RollingWindowCorrelation _correlation;
    private readonly StreamingInputResolver _input;
    private double _smaValue;
    private int _index;

    public _1LCLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, _ => _smaValue);
        _correlation = new RollingWindowCorrelation(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public _1LCLeastSquaresMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, _ => _smaValue);
        _correlation = new RollingWindowCorrelation(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName._1LCLeastSquaresMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _stdDev.Reset();
        _correlation.Reset();
        _smaValue = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var x = (double)_index;
        var corr = isFinal ? _correlation.Add(x, value, out _) : _correlation.Preview(x, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;

        var sma = _sma.Next(value, isFinal);
        _smaValue = sma;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var lsma = sma + (corr * stdDev * 1.7);

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "1lsma", lsma }
            };
        }

        return new StreamingIndicatorStateResult(lsma, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _stdDev.Dispose();
        _correlation.Dispose();
    }
}

public sealed class _3HMAState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _wma1;
    private readonly IMovingAverageSmoother _wma2;
    private readonly IMovingAverageSmoother _wma3;
    private readonly IMovingAverageSmoother _final;
    private readonly StreamingInputResolver _input;

    public _3HMAState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 50,
        InputName inputName = InputName.Close)
    {
        var p = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        var p1 = MathHelper.MinOrMax((int)Math.Ceiling((double)p / 3));
        var p2 = MathHelper.MinOrMax((int)Math.Ceiling((double)p / 2));

        _wma1 = MovingAverageSmootherFactory.Create(maType, p1);
        _wma2 = MovingAverageSmootherFactory.Create(maType, p2);
        _wma3 = MovingAverageSmootherFactory.Create(maType, p);
        _final = MovingAverageSmootherFactory.Create(maType, p);
        _input = new StreamingInputResolver(inputName, null);
    }

    public _3HMAState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var p = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        var p1 = MathHelper.MinOrMax((int)Math.Ceiling((double)p / 3));
        var p2 = MathHelper.MinOrMax((int)Math.Ceiling((double)p / 2));

        _wma1 = MovingAverageSmootherFactory.Create(maType, p1);
        _wma2 = MovingAverageSmootherFactory.Create(maType, p2);
        _wma3 = MovingAverageSmootherFactory.Create(maType, p);
        _final = MovingAverageSmootherFactory.Create(maType, p);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName._3HMA;

    public void Reset()
    {
        _wma1.Reset();
        _wma2.Reset();
        _wma3.Reset();
        _final.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wma1 = _wma1.Next(value, isFinal);
        var wma2 = _wma2.Next(value, isFinal);
        var wma3 = _wma3.Next(value, isFinal);
        var mid = (wma1 * 3) - wma2 - wma3;
        var hma = _final.Next(mid, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "3hma", hma }
            };
        }

        return new StreamingIndicatorStateResult(hma, outputs);
    }

    public void Dispose()
    {
        _wma1.Dispose();
        _wma2.Dispose();
        _wma3.Dispose();
        _final.Dispose();
    }
}

public sealed class _4MovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema5;
    private readonly IMovingAverageSmoother _ema8;
    private readonly IMovingAverageSmoother _ema10;
    private readonly IMovingAverageSmoother _ema17;
    private readonly IMovingAverageSmoother _ema14;
    private readonly IMovingAverageSmoother _ema16;
    private readonly IMovingAverageSmoother _signal1;
    private readonly IMovingAverageSmoother _signal2;
    private readonly IMovingAverageSmoother _signal3;
    private readonly IMovingAverageSmoother _signal4;
    private readonly double _blueMult;
    private readonly double _yellowMult;
    private readonly StreamingInputResolver _input;

    public _4MovingAverageConvergenceDivergenceState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 5,
        int length2 = 8, int length3 = 10, int length4 = 17, int length5 = 14, int length6 = 16,
        double blueMult = 4.3, double yellowMult = 1.4, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        _ema5 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _ema8 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema10 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema17 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _ema14 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema16 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signal1 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal2 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal3 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal4 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _blueMult = blueMult;
        _yellowMult = yellowMult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public _4MovingAverageConvergenceDivergenceState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        int length5, int length6, double blueMult, double yellowMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        _ema5 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _ema8 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema10 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema17 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _ema14 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema16 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signal1 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal2 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal3 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal4 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _blueMult = blueMult;
        _yellowMult = yellowMult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName._4MovingAverageConvergenceDivergence;

    public void Reset()
    {
        _ema5.Reset();
        _ema8.Reset();
        _ema10.Reset();
        _ema17.Reset();
        _ema14.Reset();
        _ema16.Reset();
        _signal1.Reset();
        _signal2.Reset();
        _signal3.Reset();
        _signal4.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema5 = _ema5.Next(value, isFinal);
        var ema8 = _ema8.Next(value, isFinal);
        var ema10 = _ema10.Next(value, isFinal);
        var ema17 = _ema17.Next(value, isFinal);
        var ema14 = _ema14.Next(value, isFinal);
        var ema16 = _ema16.Next(value, isFinal);

        var macd1 = ema17 - ema14;
        var macd2 = ema17 - ema8;
        var macd3 = ema10 - ema16;
        var macd4 = ema5 - ema10;

        var macd1Signal = _signal1.Next(macd1, isFinal);
        var macd2Signal = _signal2.Next(macd2, isFinal);
        var macd3Signal = _signal3.Next(macd3, isFinal);
        var macd4Signal = _signal4.Next(macd4, isFinal);

        var macd1Histogram = macd1 - macd1Signal;
        var macd2Histogram = macd2 - macd2Signal;
        var macd3Histogram = macd3 - macd3Signal;
        var macd4Histogram = macd4 - macd4Signal;
        _ = _blueMult * macd1Histogram;
        _ = _yellowMult * macd3Histogram;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Macd1", macd4 },
                { "Signal1", macd4Signal },
                { "Histogram1", macd4Histogram },
                { "Macd2", macd2 },
                { "Signal2", macd2Signal },
                { "Histogram2", macd2Histogram }
            };
        }

        return new StreamingIndicatorStateResult(macd4, outputs);
    }

    public void Dispose()
    {
        _ema5.Dispose();
        _ema8.Dispose();
        _ema10.Dispose();
        _ema17.Dispose();
        _ema14.Dispose();
        _ema16.Dispose();
        _signal1.Dispose();
        _signal2.Dispose();
        _signal3.Dispose();
        _signal4.Dispose();
    }
}

public sealed class _4PercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema5;
    private readonly IMovingAverageSmoother _ema8;
    private readonly IMovingAverageSmoother _ema10;
    private readonly IMovingAverageSmoother _ema17;
    private readonly IMovingAverageSmoother _ema14;
    private readonly IMovingAverageSmoother _ema16;
    private readonly IMovingAverageSmoother _signal1;
    private readonly IMovingAverageSmoother _signal2;
    private readonly IMovingAverageSmoother _signal3;
    private readonly IMovingAverageSmoother _signal4;
    private readonly double _blueMult;
    private readonly double _yellowMult;
    private readonly StreamingInputResolver _input;

    public _4PercentagePriceOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 5,
        int length2 = 8, int length3 = 10, int length4 = 17, int length5 = 14, int length6 = 16,
        double blueMult = 4.3, double yellowMult = 1.4, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        _ema5 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _ema8 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema10 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema17 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _ema14 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema16 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signal1 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal2 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal3 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal4 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _blueMult = blueMult;
        _yellowMult = yellowMult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public _4PercentagePriceOscillatorState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        int length5, int length6, double blueMult, double yellowMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        _ema5 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _ema8 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema10 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema17 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _ema14 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema16 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signal1 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal2 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal3 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal4 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _blueMult = blueMult;
        _yellowMult = yellowMult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName._4PercentagePriceOscillator;

    public void Reset()
    {
        _ema5.Reset();
        _ema8.Reset();
        _ema10.Reset();
        _ema17.Reset();
        _ema14.Reset();
        _ema16.Reset();
        _signal1.Reset();
        _signal2.Reset();
        _signal3.Reset();
        _signal4.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema5 = _ema5.Next(value, isFinal);
        var ema8 = _ema8.Next(value, isFinal);
        var ema10 = _ema10.Next(value, isFinal);
        var ema17 = _ema17.Next(value, isFinal);
        var ema14 = _ema14.Next(value, isFinal);
        var ema16 = _ema16.Next(value, isFinal);

        var macd1 = ema17 - ema14;
        var macd2 = ema17 - ema8;
        var macd3 = ema10 - ema16;
        var macd4 = ema5 - ema10;

        var ppo1 = ema14 != 0 ? macd1 / ema14 * 100 : 0;
        var ppo2 = ema8 != 0 ? macd2 / ema8 * 100 : 0;
        var ppo3 = ema16 != 0 ? macd3 / ema16 * 100 : 0;
        var ppo4 = ema10 != 0 ? macd4 / ema10 * 100 : 0;

        var ppo1Signal = _signal1.Next(ppo1, isFinal);
        var ppo2Signal = _signal2.Next(ppo2, isFinal);
        var ppo3Signal = _signal3.Next(ppo3, isFinal);
        var ppo4Signal = _signal4.Next(ppo4, isFinal);

        var ppo1Histogram = ppo1 - ppo1Signal;
        var ppo2Histogram = ppo2 - ppo2Signal;
        var ppo3Histogram = ppo3 - ppo3Signal;
        var ppo4Histogram = ppo4 - ppo4Signal;
        _ = _blueMult * ppo1Histogram;
        _ = _yellowMult * ppo3Histogram;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Ppo1", ppo4 },
                { "Signal1", ppo4Signal },
                { "Histogram1", ppo4Histogram },
                { "Ppo2", ppo2 },
                { "Signal2", ppo2Signal },
                { "Histogram2", ppo2Histogram }
            };
        }

        return new StreamingIndicatorStateResult(ppo4, outputs);
    }

    public void Dispose()
    {
        _ema5.Dispose();
        _ema8.Dispose();
        _ema10.Dispose();
        _ema17.Dispose();
        _ema14.Dispose();
        _ema16.Dispose();
        _signal1.Dispose();
        _signal2.Dispose();
        _signal3.Dispose();
        _signal4.Dispose();
    }
}

public sealed class AdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevAma;
    private bool _hasPrev;

    public AdaptiveMovingAverageState(int fastLength = 2, int slowLength = 14, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _fastAlpha = (double)2 / (Math.Max(1, fastLength) + 1);
        _slowAlpha = (double)2 / (Math.Max(1, slowLength) + 1);
        var windowLength = Math.Max(1, _length + 1);
        _highWindow = new RollingWindowMax(windowLength);
        _lowWindow = new RollingWindowMin(windowLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptiveMovingAverageState(int fastLength, int slowLength, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _fastAlpha = (double)2 / (Math.Max(1, fastLength) + 1);
        _slowAlpha = (double)2 / (Math.Max(1, slowLength) + 1);
        var windowLength = Math.Max(1, _length + 1);
        _highWindow = new RollingWindowMax(windowLength);
        _lowWindow = new RollingWindowMin(windowLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptiveMovingAverage;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevAma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var mltp = highest - lowest != 0
            ? MathHelper.MinOrMax(Math.Abs((2 * value) - lowest - highest) / (highest - lowest), 1, 0)
            : 0;
        var ssc = (mltp * (_fastAlpha - _slowAlpha)) + _slowAlpha;
        var prevAma = _hasPrev ? _prevAma : 0;
        var ama = prevAma + (MathHelper.Pow(ssc, 2) * (value - prevAma));

        if (isFinal)
        {
            _prevAma = ama;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ama", ama }
            };
        }

        return new StreamingIndicatorStateResult(ama, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class AdaptiveExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _mltp1;
    private readonly IMovingAverageSmoother _sma;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevAema;
    private int _index;
    private bool _hasPrev;

    public AdaptiveExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _mltp1 = (double)2 / (_length + 1);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptiveExponentialMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _mltp1 = (double)2 / (_length + 1);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptiveExponentialMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevAema = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var sma = _sma.Next(value, isFinal);
        var mltp2 = highest - lowest != 0
            ? MathHelper.MinOrMax(Math.Abs((2 * value) - lowest - highest) / (highest - lowest), 1, 0)
            : 0;
        var rate = _mltp1 * (1 + mltp2);
        var prevAema = _hasPrev ? _prevAema : value;
        var aema = _index <= _length ? sma : prevAema + (rate * (value - prevAema));

        if (isFinal)
        {
            _prevAema = aema;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Aema", aema }
            };
        }

        return new StreamingIndicatorStateResult(aema, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class AdaptiveAutonomousRecursiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveAutonomousRecursiveMovingAverageEngine _engine;
    private readonly StreamingInputResolver _input;

    public AdaptiveAutonomousRecursiveMovingAverageState(int length = 14, double gamma = 3,
        InputName inputName = InputName.Close)
    {
        _engine = new AdaptiveAutonomousRecursiveMovingAverageEngine(length, gamma);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptiveAutonomousRecursiveMovingAverageState(int length, double gamma, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _engine = new AdaptiveAutonomousRecursiveMovingAverageEngine(length, gamma);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptiveAutonomousRecursiveMovingAverage;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var aarma = _engine.Next(value, isFinal, out var d);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "D", d },
                { "Aarma", aarma }
            };
        }

        return new StreamingIndicatorStateResult(aarma, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

public sealed class AdaptiveLeastSquaresState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _smooth;
    private readonly RollingWindowMax _trWindow;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevX;
    private double _prevY;
    private double _prevMx;
    private double _prevMy;
    private double _prevMxx;
    private double _prevMyy;
    private double _prevMxy;
    private int _index;
    private bool _hasPrev;

    public AdaptiveLeastSquaresState(int length = 500, double smooth = 1.5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _smooth = smooth;
        _trWindow = new RollingWindowMax(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptiveLeastSquaresState(int length, double smooth, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _smooth = smooth;
        _trWindow = new RollingWindowMax(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptiveLeastSquares;

    public void Reset()
    {
        _trWindow.Reset();
        _prevValue = 0;
        _prevX = 0;
        _prevY = 0;
        _prevMx = 0;
        _prevMy = 0;
        _prevMxx = 0;
        _prevMyy = 0;
        _prevMxy = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var index = (double)_index;
        var prevValue = _hasPrev ? _prevValue : 0;

        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var highest = isFinal ? _trWindow.Add(tr, out _) : _trWindow.Preview(tr, out _);
        var alpha = highest != 0 ? MathHelper.MinOrMax(MathHelper.Pow(tr / highest, _smooth), 0.99, 0.01) : 0.01;
        var xx = index * index;
        var yy = value * value;
        var xy = index * value;

        var prevX = _hasPrev ? _prevX : index;
        var x = (alpha * index) + ((1 - alpha) * prevX);

        var prevY = _hasPrev ? _prevY : value;
        var y = (alpha * value) + ((1 - alpha) * prevY);

        var dx = Math.Abs(index - x);
        var dy = Math.Abs(value - y);

        var prevMx = _hasPrev ? _prevMx : dx;
        var mx = (alpha * dx) + ((1 - alpha) * prevMx);

        var prevMy = _hasPrev ? _prevMy : dy;
        var my = (alpha * dy) + ((1 - alpha) * prevMy);

        var prevMxx = _hasPrev ? _prevMxx : xx;
        var mxx = (alpha * xx) + ((1 - alpha) * prevMxx);

        var prevMyy = _hasPrev ? _prevMyy : yy;
        var myy = (alpha * yy) + ((1 - alpha) * prevMyy);

        var prevMxy = _hasPrev ? _prevMxy : xy;
        var mxy = (alpha * xy) + ((1 - alpha) * prevMxy);

        var alphaVal = (2 / alpha) + 1;
        var a1 = alpha != 0 ? (MathHelper.Pow(alphaVal, 2) * mxy) - (alphaVal * mx * alphaVal * my) : 0;
        var tempVal = ((MathHelper.Pow(alphaVal, 2) * mxx) - MathHelper.Pow(alphaVal * mx, 2)) *
            ((MathHelper.Pow(alphaVal, 2) * myy) - MathHelper.Pow(alphaVal * my, 2));
        var b1 = tempVal >= 0 ? MathHelper.Sqrt(tempVal) : 0;
        var r = b1 != 0 ? a1 / b1 : 0;
        var a = mx != 0 ? r * (my / mx) : 0;
        var b = y - (a * x);
        var reg = (x * a) + b;

        if (isFinal)
        {
            _prevValue = value;
            _prevX = x;
            _prevY = y;
            _prevMx = mx;
            _prevMy = my;
            _prevMxx = mxx;
            _prevMyy = myy;
            _prevMxy = mxy;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Als", reg }
            };
        }

        return new StreamingIndicatorStateResult(reg, outputs);
    }

    public void Dispose()
    {
        _trWindow.Dispose();
    }
}

public sealed class AlphaDecreasingExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevEma;
    private int _index;

    public AlphaDecreasingExponentialMovingAverageState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public AlphaDecreasingExponentialMovingAverageState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AlphaDecreasingExponentialMovingAverage;

    public void Reset()
    {
        _prevEma = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var alpha = (double)2 / (_index + 1);
        var ema = (alpha * value) + ((1 - alpha) * _prevEma);

        if (isFinal)
        {
            _prevEma = ema;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ema", ema }
            };
        }

        return new StreamingIndicatorStateResult(ema, outputs);
    }
}

public sealed class AdaptivePriceZoneIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _pct;
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _xhlEma1;
    private readonly IMovingAverageSmoother _xhlEma2;
    private readonly StreamingInputResolver _input;

    public AdaptivePriceZoneIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20,
        double pct = 2, InputName inputName = InputName.Close)
    {
        var nP = MathHelper.MinOrMax((int)Math.Ceiling(MathHelper.Sqrt(length)));
        _pct = pct;
        _ema1 = MovingAverageSmootherFactory.Create(maType, nP);
        _ema2 = MovingAverageSmootherFactory.Create(maType, nP);
        _xhlEma1 = MovingAverageSmootherFactory.Create(maType, nP);
        _xhlEma2 = MovingAverageSmootherFactory.Create(maType, nP);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptivePriceZoneIndicatorState(MovingAvgType maType, int length, double pct, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var nP = MathHelper.MinOrMax((int)Math.Ceiling(MathHelper.Sqrt(length)));
        _pct = pct;
        _ema1 = MovingAverageSmootherFactory.Create(maType, nP);
        _ema2 = MovingAverageSmootherFactory.Create(maType, nP);
        _xhlEma1 = MovingAverageSmootherFactory.Create(maType, nP);
        _xhlEma2 = MovingAverageSmootherFactory.Create(maType, nP);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptivePriceZoneIndicator;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _xhlEma1.Reset();
        _xhlEma2.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var xVal1 = _ema2.Next(_ema1.Next(value, isFinal), isFinal);
        var xhl = bar.High - bar.Low;
        var xVal2 = _xhlEma2.Next(_xhlEma1.Next(xhl, isFinal), isFinal);
        var upper = (xVal2 * _pct) + xVal1;
        var lower = xVal1 - (xVal2 * _pct);
        var middle = (upper + lower) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
        _xhlEma1.Dispose();
        _xhlEma2.Dispose();
    }
}

public sealed class AdaptiveRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi;
    private readonly StreamingInputResolver _input;
    private double _prevArsi;
    private bool _hasPrev;

    public AdaptiveRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14,
        InputName inputName = InputName.Close)
    {
        _rsi = new RsiState(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptiveRelativeStrengthIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _rsi = new RsiState(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptiveRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _prevArsi = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var alpha = 2 * Math.Abs((rsi / 100) - 0.5);
        var prevArsi = _hasPrev ? _prevArsi : 0;
        var arsi = (alpha * value) + ((1 - alpha) * prevArsi);

        if (isFinal)
        {
            _prevArsi = arsi;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Arsi", arsi }
            };
        }

        return new StreamingIndicatorStateResult(arsi, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
    }
}

public sealed class AdaptiveStochasticState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly LinearRegressionState _regression;
    private readonly RollingWindowMax _fastMax;
    private readonly RollingWindowMin _fastMin;
    private readonly RollingWindowMax _slowMax;
    private readonly RollingWindowMin _slowMin;
    private readonly StreamingInputResolver _input;
    private double _regressionInput;

    public AdaptiveStochasticState(int length = 50, int fastLength = 50, int slowLength = 200,
        InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var regressionLength = Math.Max(1, Math.Abs(resolvedSlow - resolvedFast));
        _er = new EfficiencyRatioState(resolvedLength);
        _regression = new LinearRegressionState(regressionLength, _ => _regressionInput);
        _fastMax = new RollingWindowMax(resolvedFast);
        _fastMin = new RollingWindowMin(resolvedFast);
        _slowMax = new RollingWindowMax(resolvedSlow);
        _slowMin = new RollingWindowMin(resolvedSlow);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptiveStochasticState(int length, int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var regressionLength = Math.Max(1, Math.Abs(resolvedSlow - resolvedFast));
        _er = new EfficiencyRatioState(resolvedLength);
        _regression = new LinearRegressionState(regressionLength, _ => _regressionInput);
        _fastMax = new RollingWindowMax(resolvedFast);
        _fastMin = new RollingWindowMin(resolvedFast);
        _slowMax = new RollingWindowMax(resolvedSlow);
        _slowMin = new RollingWindowMin(resolvedSlow);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptiveStochastic;

    public void Reset()
    {
        _er.Reset();
        _regression.Reset();
        _fastMax.Reset();
        _fastMin.Reset();
        _slowMax.Reset();
        _slowMin.Reset();
        _regressionInput = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _regressionInput = value;
        var src = _regression.Update(bar, isFinal, includeOutputs: false).Value;
        var er = _er.Next(src, isFinal);
        var highest1 = isFinal ? _fastMax.Add(src, out _) : _fastMax.Preview(src, out _);
        var lowest1 = isFinal ? _fastMin.Add(src, out _) : _fastMin.Preview(src, out _);
        var highest2 = isFinal ? _slowMax.Add(src, out _) : _slowMax.Preview(src, out _);
        var lowest2 = isFinal ? _slowMin.Add(src, out _) : _slowMin.Preview(src, out _);
        var a = (er * highest1) + ((1 - er) * highest2);
        var b = (er * lowest1) + ((1 - er) * lowest2);
        var stc = a - b != 0 ? MathHelper.MinOrMax((src - b) / (a - b), 1, 0) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ast", stc }
            };
        }

        return new StreamingIndicatorStateResult(stc, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
        _regression.Dispose();
        _fastMax.Dispose();
        _fastMin.Dispose();
        _slowMax.Dispose();
        _slowMin.Dispose();
    }
}

public sealed class AdaptiveTrailingStopState : IStreamingIndicatorState, IDisposable
{
    private readonly double _factor;
    private readonly EfficiencyRatioState _er;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private double _prevB;
    private double _prevUp;
    private double _prevDn;
    private double _prevOs;
    private bool _hasPrev;

    public AdaptiveTrailingStopState(int length = 100, double factor = 3, InputName inputName = InputName.Close)
    {
        _factor = factor;
        _er = new EfficiencyRatioState(Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptiveTrailingStopState(int length, double factor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _factor = factor;
        _er = new EfficiencyRatioState(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptiveTrailingStop;

    public void Reset()
    {
        _er.Reset();
        _prevA = 0;
        _prevB = 0;
        _prevUp = 0;
        _prevDn = 0;
        _prevOs = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = _er.Next(value, isFinal);
        var per = MathHelper.Pow(er, _factor);

        var prevA = _hasPrev ? _prevA : value;
        var a = Math.Max(value, prevA) - (Math.Abs(value - prevA) * per);
        var prevB = _hasPrev ? _prevB : value;
        var b = Math.Min(value, prevB) + (Math.Abs(value - prevB) * per);
        var prevUp = _hasPrev ? _prevUp : 0;
        var up = a > prevA ? a : a < prevA && b < prevB ? a : prevUp;
        var prevDn = _hasPrev ? _prevDn : 0;
        var dn = b < prevB ? b : b > prevB && a > prevA ? b : prevDn;
        var prevOs = _hasPrev ? _prevOs : 0;
        var os = up > value ? 1 : dn > value ? 0 : prevOs;
        var ts = (os * dn) + ((1 - os) * up);

        if (isFinal)
        {
            _prevA = a;
            _prevB = b;
            _prevUp = up;
            _prevDn = dn;
            _prevOs = os;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ts", ts }
            };
        }

        return new StreamingIndicatorStateResult(ts, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}

public sealed class AdaptiveAutonomousRecursiveTrailingStopState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveAutonomousRecursiveMovingAverageEngine _engine;
    private readonly StreamingInputResolver _input;
    private double _prevUpper;
    private double _prevLower;
    private double _prevOs;
    private bool _hasPrev;

    public AdaptiveAutonomousRecursiveTrailingStopState(int length = 14, double gamma = 3,
        InputName inputName = InputName.Close)
    {
        _engine = new AdaptiveAutonomousRecursiveMovingAverageEngine(length, gamma);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AdaptiveAutonomousRecursiveTrailingStopState(int length, double gamma, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _engine = new AdaptiveAutonomousRecursiveMovingAverageEngine(length, gamma);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AdaptiveAutonomousRecursiveTrailingStop;

    public void Reset()
    {
        _engine.Reset();
        _prevUpper = 0;
        _prevLower = 0;
        _prevOs = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma2 = _engine.Next(value, isFinal, out var d);
        var upper = ma2 + d;
        var lower = ma2 - d;
        var prevUpper = _hasPrev ? _prevUpper : 0;
        var prevLower = _hasPrev ? _prevLower : 0;
        var prevOs = _hasPrev ? _prevOs : 0;
        var os = value > prevUpper ? 1 : value < prevLower ? 0 : prevOs;
        var ts = (os * lower) + ((1 - os) * upper);

        if (isFinal)
        {
            _prevUpper = upper;
            _prevLower = lower;
            _prevOs = os;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ts", ts }
            };
        }

        return new StreamingIndicatorStateResult(ts, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

public sealed class AhrensMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;
    private double _prevAhma;
    private bool _hasPrev;

    public AhrensMovingAverageState(int length = 9, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AhrensMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AhrensMovingAverage;

    public void Reset()
    {
        _window.Clear();
        _prevAhma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorAhma = _window.Count >= _length ? _window[0] : value;
        var prevAhma = _hasPrev ? _prevAhma : 0;
        var ahma = prevAhma + ((value - ((prevAhma + priorAhma) / 2)) / _length);

        if (isFinal)
        {
            _window.TryAdd(ahma, out _);
            _prevAhma = ahma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ahma", ahma }
            };
        }

        return new StreamingIndicatorStateResult(ahma, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

public sealed class ArnaudLegouxMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public ArnaudLegouxMovingAverageState(int length = 9, double offset = 0.85, int sigma = 6,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var m = offset * (_length - 1);
        var s = (double)_length / sigma;
        _weights = new double[_length];
        double weightSum = 0;
        for (var j = 0; j < _length; j++)
        {
            var weight = s != 0
                ? MathHelper.Exp(-1 * MathHelper.Pow(j - m, 2) / (2 * MathHelper.Pow(s, 2)))
                : 0;
            _weights[j] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ArnaudLegouxMovingAverageState(int length, double offset, int sigma, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var m = offset * (_length - 1);
        var s = (double)_length / sigma;
        _weights = new double[_length];
        double weightSum = 0;
        for (var j = 0; j < _length; j++)
        {
            var weight = s != 0
                ? MathHelper.Exp(-1 * MathHelper.Pow(j - m, 2) / (2 * MathHelper.Pow(s, 2)))
                : 0;
            _weights[j] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ArnaudLegouxMovingAverage;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        var count = _window.Count;
        var missing = _length - count;
        double sum = 0;
        for (var j = 0; j < _length; j++)
        {
            var val = j < missing ? 0 : _window[j - missing];
            sum += val * _weights[j];
        }

        var alma = _weightSum != 0 ? sum / _weightSum : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Alma", alma }
            };
        }

        return new StreamingIndicatorStateResult(alma, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

public sealed class AlligatorIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _jawOffset;
    private readonly int _teethOffset;
    private readonly int _lipsOffset;
    private readonly IMovingAverageSmoother _jawSmoother;
    private readonly IMovingAverageSmoother _teethSmoother;
    private readonly IMovingAverageSmoother _lipsSmoother;
    private readonly PooledRingBuffer<double> _jawWindow;
    private readonly PooledRingBuffer<double> _teethWindow;
    private readonly PooledRingBuffer<double> _lipsWindow;
    private readonly StreamingInputResolver _input;

    public AlligatorIndexState(InputName inputName = InputName.Close, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int jawLength = 13,
        int jawOffset = 8, int teethLength = 8, int teethOffset = 5, int lipsLength = 5, int lipsOffset = 3)
    {
        _jawOffset = Math.Max(0, jawOffset);
        _teethOffset = Math.Max(0, teethOffset);
        _lipsOffset = Math.Max(0, lipsOffset);
        _jawSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, jawLength));
        _teethSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, teethLength));
        _lipsSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, lipsLength));
        _jawWindow = new PooledRingBuffer<double>(_jawOffset + 1);
        _teethWindow = new PooledRingBuffer<double>(_teethOffset + 1);
        _lipsWindow = new PooledRingBuffer<double>(_lipsOffset + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AlligatorIndexState(InputName inputName, MovingAvgType maType, int jawLength, int jawOffset, int teethLength, int teethOffset,
        int lipsLength, int lipsOffset, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _jawOffset = Math.Max(0, jawOffset);
        _teethOffset = Math.Max(0, teethOffset);
        _lipsOffset = Math.Max(0, lipsOffset);
        _jawSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, jawLength));
        _teethSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, teethLength));
        _lipsSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, lipsLength));
        _jawWindow = new PooledRingBuffer<double>(_jawOffset + 1);
        _teethWindow = new PooledRingBuffer<double>(_teethOffset + 1);
        _lipsWindow = new PooledRingBuffer<double>(_lipsOffset + 1);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.AlligatorIndex;

    public void Reset()
    {
        _jawSmoother.Reset();
        _teethSmoother.Reset();
        _lipsSmoother.Reset();
        _jawWindow.Clear();
        _teethWindow.Clear();
        _lipsWindow.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var jaw = _jawSmoother.Next(value, isFinal);
        var teeth = _teethSmoother.Next(value, isFinal);
        var lips = _lipsSmoother.Next(value, isFinal);

        if (isFinal)
        {
            _jawWindow.TryAdd(jaw, out _);
            _teethWindow.TryAdd(teeth, out _);
            _lipsWindow.TryAdd(lips, out _);
        }

        var displacedJaw = _jawOffset == 0 ? jaw : _jawWindow.Count > _jawOffset ? _jawWindow[0] : 0;
        var displacedTeeth = _teethOffset == 0 ? teeth : _teethWindow.Count > _teethOffset ? _teethWindow[0] : 0;
        var displacedLips = _lipsOffset == 0 ? lips : _lipsWindow.Count > _lipsOffset ? _lipsWindow[0] : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Lips", displacedLips },
                { "Teeth", displacedTeeth },
                { "Jaws", displacedJaw }
            };
        }

        return new StreamingIndicatorStateResult(displacedLips, outputs);
    }

    public void Dispose()
    {
        _jawSmoother.Dispose();
        _teethSmoother.Dispose();
        _lipsSmoother.Dispose();
        _jawWindow.Dispose();
        _teethWindow.Dispose();
        _lipsWindow.Dispose();
    }
}

public sealed class AnchoredMomentumState : IStreamingIndicatorState, IDisposable
{
    private readonly int _signalLength;
    private readonly IMovingAverageSmoother _ema;
    private readonly RollingWindowSum _priceSum;
    private readonly RollingWindowSum _signalSum;
    private readonly StreamingInputResolver _input;

    public AnchoredMomentumState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int smoothLength = 7,
        int signalLength = 8, int momentumLength = 10, InputName inputName = InputName.Close)
    {
        _signalLength = Math.Max(1, signalLength);
        var p = MathHelper.MinOrMax((2 * momentumLength) + 1);
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _priceSum = new RollingWindowSum(p);
        _signalSum = new RollingWindowSum(_signalLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AnchoredMomentumState(MovingAvgType maType, int smoothLength, int signalLength, int momentumLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signalLength = Math.Max(1, signalLength);
        var p = MathHelper.MinOrMax((2 * momentumLength) + 1);
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _priceSum = new RollingWindowSum(p);
        _signalSum = new RollingWindowSum(_signalLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AnchoredMomentum;

    public void Reset()
    {
        _ema.Reset();
        _priceSum.Reset();
        _signalSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.Next(value, isFinal);
        int priceCount;
        var priceSum = isFinal ? _priceSum.Add(value, out priceCount) : _priceSum.Preview(value, out priceCount);
        var sma = priceCount > 0 ? priceSum / priceCount : 0;
        var amom = sma != 0 ? 100 * ((ema / sma) - 1) : 0;
        int signalCount;
        var signalSum = isFinal ? _signalSum.Add(amom, out signalCount) : _signalSum.Preview(amom, out signalCount);
        var signal = signalCount > 0 ? signalSum / signalCount : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Amom", amom },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(amom, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _priceSum.Dispose();
        _signalSum.Dispose();
    }
}

public sealed class ApirineSlowRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smooth;
    private readonly IMovingAverageSmoother _gain;
    private readonly IMovingAverageSmoother _loss;
    private readonly StreamingInputResolver _input;

    public ApirineSlowRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14,
        int smoothLength = 6, InputName inputName = InputName.Close)
    {
        _smooth = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _gain = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _loss = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ApirineSlowRelativeStrengthIndexState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _smooth = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _gain = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _loss = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ApirineSlowRelativeStrengthIndex;

    public void Reset()
    {
        _smooth.Reset();
        _gain.Reset();
        _loss.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var r1 = _smooth.Next(value, isFinal);
        var r2 = value > r1 ? value - r1 : 0;
        var r3 = value < r1 ? r1 - value : 0;
        var r4 = _gain.Next(r2, isFinal);
        var r5 = _loss.Next(r3, isFinal);
        var rs = r5 != 0 ? r4 / r5 : 0;
        var rr = r5 == 0 ? 100 : r4 == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Asrsi", rr }
            };
        }

        return new StreamingIndicatorStateResult(rr, outputs);
    }

    public void Dispose()
    {
        _smooth.Dispose();
        _gain.Dispose();
        _loss.Dispose();
    }
}

public sealed class AsymmetricalRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _upCountSum;
    private readonly StreamingInputResolver _input;
    private double _prevUpSum;
    private double _prevDownSum;
    private double _prevValue;
    private bool _hasPrev;

    public AsymmetricalRelativeStrengthIndexState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _upCountSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AsymmetricalRelativeStrengthIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _upCountSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AsymmetricalRelativeStrengthIndex;

    public void Reset()
    {
        _upCountSum.Reset();
        _prevUpSum = 0;
        _prevDownSum = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var roc = prevValue != 0 ? (value - prevValue) / prevValue * 100 : 0;
        var upFlag = roc >= 0 ? 1 : 0;
        int countAfter;
        var upCount = isFinal ? _upCountSum.Add(upFlag, out countAfter) : _upCountSum.Preview(upFlag, out countAfter);
        var upAlpha = upCount != 0 ? 1 / upCount : 0;
        var posRoc = roc > 0 ? roc : 0;
        var negRoc = roc < 0 ? Math.Abs(roc) : 0;
        var prevUpSum = _hasPrev ? _prevUpSum : 0;
        var upSum = (upAlpha * posRoc) + ((1 - upAlpha) * prevUpSum);
        var downCount = _length - upCount;
        var downAlpha = downCount != 0 ? 1 / downCount : 0;
        var prevDownSum = _hasPrev ? _prevDownSum : 0;
        var downSum = (downAlpha * negRoc) + ((1 - downAlpha) * prevDownSum);
        var ars = downSum != 0 ? upSum / downSum : 0;
        var arsi = downSum == 0 ? 100 : upSum == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + ars)), 100, 0);

        if (isFinal)
        {
            _prevUpSum = upSum;
            _prevDownSum = downSum;
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Arsi", arsi }
            };
        }

        return new StreamingIndicatorStateResult(arsi, outputs);
    }

    public void Dispose()
    {
        _upCountSum.Dispose();
    }
}

public sealed class AtrFilteredExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _stdDevLength;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _stdDevASmoother;
    private readonly RollingCumulativeSum _atrValSum;
    private readonly RollingWindowMin _stdDevMin;
    private readonly StreamingInputResolver _input;
    private readonly double _min;
    private double _prevValue;
    private double _prevEmaAfp;
    private bool _hasPrev;

    public AtrFilteredExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 45, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, double min = 5,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stdDevLength = Math.Max(1, stdDevLength);
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            _atrSmoother = new ExactSimpleMovingAverageSmoother(Math.Max(1, atrLength));
            _stdDevASmoother = new ExactSimpleMovingAverageSmoother(_stdDevLength);
        }
        else
        {
            _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, atrLength));
            _stdDevASmoother = MovingAverageSmootherFactory.Create(maType, _stdDevLength);
        }
        _atrValSum = new RollingCumulativeSum();
        _stdDevMin = new RollingWindowMin(Math.Max(1, lbLength));
        _min = min;
        _input = new StreamingInputResolver(inputName, null);
    }

    public AtrFilteredExponentialMovingAverageState(MovingAvgType maType, int length, int atrLength, int stdDevLength,
        int lbLength, double min, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stdDevLength = Math.Max(1, stdDevLength);
        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            _atrSmoother = new ExactSimpleMovingAverageSmoother(Math.Max(1, atrLength));
            _stdDevASmoother = new ExactSimpleMovingAverageSmoother(_stdDevLength);
        }
        else
        {
            _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, atrLength));
            _stdDevASmoother = MovingAverageSmootherFactory.Create(maType, _stdDevLength);
        }
        _atrValSum = new RollingCumulativeSum();
        _stdDevMin = new RollingWindowMin(Math.Max(1, lbLength));
        _min = min;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AtrFilteredExponentialMovingAverage;

    public void Reset()
    {
        _atrSmoother.Reset();
        _stdDevASmoother.Reset();
        _atrValSum.Reset();
        _stdDevMin.Reset();
        _prevValue = 0;
        _prevEmaAfp = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var trVal = value != 0 ? tr / value : tr;
        var atrVal = _atrSmoother.Next(trVal, isFinal);
        var atrValPow = MathHelper.Pow(atrVal, 2);
        var stdDevA = _stdDevASmoother.Next(atrValPow, isFinal);
        var atrValSum = isFinal ? _atrValSum.Add(atrVal, _stdDevLength) : _atrValSum.Preview(atrVal, _stdDevLength);
        var stdDevB = _stdDevLength != 0
            ? MathHelper.Pow(atrValSum, 2) / MathHelper.Pow(_stdDevLength, 2)
            : 0;
        var diff = stdDevA - stdDevB;
        var stdDev = diff >= 0 ? MathHelper.Sqrt(diff) : 0;
        var stdDevLow = isFinal ? _stdDevMin.Add(stdDev, out _) : _stdDevMin.Preview(stdDev, out _);
        var stdDevFactorAfp = stdDev != 0 ? stdDevLow / stdDev : 0;
        var stdDevFactorAfpLow = Math.Min(stdDevFactorAfp, _min);
        var alphaAfp = (2 * stdDevFactorAfpLow) / (_length + 1);
        var emaAfp = (alphaAfp * value) + ((1 - alphaAfp) * _prevEmaAfp);

        if (isFinal)
        {
            _prevValue = value;
            _prevEmaAfp = emaAfp;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Afp", emaAfp }
            };
        }

        return new StreamingIndicatorStateResult(emaAfp, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _stdDevASmoother.Dispose();
        _stdDevMin.Dispose();
    }
}

public sealed class AutoDispersionBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _x2Sum;
    private readonly RollingWindowMax _aMax;
    private readonly RollingWindowMin _bMin;
    private readonly IMovingAverageSmoother _aSmoother;
    private readonly IMovingAverageSmoother _upperSmoother;
    private readonly IMovingAverageSmoother _bSmoother;
    private readonly IMovingAverageSmoother _lowerSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private int _index;

    public AutoDispersionBandsState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 90,
        int smoothLength = 140, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _x2Sum = new RollingWindowSum(_length);
        _aMax = new RollingWindowMax(_length);
        _bMin = new RollingWindowMin(_length);
        _aSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _upperSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _bSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _lowerSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AutoDispersionBandsState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _x2Sum = new RollingWindowSum(_length);
        _aMax = new RollingWindowMax(_length);
        _bMin = new RollingWindowMin(_length);
        _aSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _upperSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _bSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _lowerSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AutoDispersionBands;

    public void Reset()
    {
        _x2Sum.Reset();
        _aMax.Reset();
        _bMin.Reset();
        _aSmoother.Reset();
        _upperSmoother.Reset();
        _bSmoother.Reset();
        _lowerSmoother.Reset();
        _values.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _index >= _length && _values.Count >= _length ? _values[_values.Count - _length] : 0;
        var x = _index >= _length ? value - prevValue : 0;
        var x2 = x * x;
        int countAfter;
        var x2Sum = isFinal ? _x2Sum.Add(x2, out countAfter) : _x2Sum.Preview(x2, out countAfter);
        var x2Sma = countAfter > 0 ? x2Sum / countAfter : 0;
        var sq = x2Sma >= 0 ? MathHelper.Sqrt(x2Sma) : 0;
        var a = value + sq;
        var aMax = isFinal ? _aMax.Add(a, out _) : _aMax.Preview(a, out _);
        var aMa = _aSmoother.Next(aMax, isFinal);
        var upper = _upperSmoother.Next(aMa, isFinal);
        var b = value - sq;
        var bMin = isFinal ? _bMin.Add(b, out _) : _bMin.Preview(b, out _);
        var bMa = _bSmoother.Next(bMin, isFinal);
        var lower = _lowerSmoother.Next(bMa, isFinal);
        var middle = (upper + lower) / 2;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _x2Sum.Dispose();
        _aMax.Dispose();
        _bMin.Dispose();
        _aSmoother.Dispose();
        _upperSmoother.Dispose();
        _bSmoother.Dispose();
        _lowerSmoother.Dispose();
        _values.Dispose();
    }
}

public sealed class AutoFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _yMa;
    private readonly IMovingAverageSmoother _xMa;
    private readonly StandardDeviationVolatilityState _dev;
    private readonly StandardDeviationVolatilityState _xDev;
    private readonly RollingWindowCorrelation _correlation;
    private readonly StreamingInputResolver _input;
    private double _prevX;
    private double _xValue;
    private bool _hasPrev;

    public AutoFilterState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 500,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _yMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _xMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _dev = new StandardDeviationVolatilityState(maType, resolved, inputName);
        _xDev = new StandardDeviationVolatilityState(maType, resolved, _ => _xValue);
        _correlation = new RollingWindowCorrelation(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AutoFilterState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _yMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _xMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _dev = new StandardDeviationVolatilityState(maType, resolved, selector);
        _xDev = new StandardDeviationVolatilityState(maType, resolved, _ => _xValue);
        _correlation = new RollingWindowCorrelation(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AutoFilter;

    public void Reset()
    {
        _yMa.Reset();
        _xMa.Reset();
        _dev.Reset();
        _xDev.Reset();
        _correlation.Reset();
        _prevX = 0;
        _xValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var dev = _dev.Update(bar, isFinal, includeOutputs: false).Value;
        var prevX = _hasPrev ? _prevX : value;
        var x = value > prevX + dev ? value : value < prevX - dev ? value : prevX;
        _xValue = x;
        var corr = isFinal ? _correlation.Add(value, x, out _) : _correlation.Preview(value, x, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;
        var yMa = _yMa.Next(value, isFinal);
        var xMa = _xMa.Next(x, isFinal);
        var mx = _xDev.Update(bar, isFinal, includeOutputs: false).Value;
        var slope = mx != 0 ? corr * (dev / mx) : 0;
        var inter = yMa - (slope * xMa);
        var reg = (x * slope) + inter;

        if (isFinal)
        {
            _prevX = x;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Af", reg }
            };
        }

        return new StreamingIndicatorStateResult(reg, outputs);
    }

    public void Dispose()
    {
        _yMa.Dispose();
        _xMa.Dispose();
        _dev.Dispose();
        _xDev.Dispose();
        _correlation.Dispose();
    }
}

public sealed class AutoLineState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private double _prevX;
    private bool _hasPrev;

    public AutoLineState(int length = 500, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, inputName);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AutoLineState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AutoLine;

    public void Reset()
    {
        _stdDev.Reset();
        _prevX = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var dev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var prevX = _hasPrev ? _prevX : value;
        var x = value > prevX + dev ? value : value < prevX - dev ? value : prevX;

        if (isFinal)
        {
            _prevX = x;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Al", x }
            };
        }

        return new StreamingIndicatorStateResult(x, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
    }
}

public sealed class AutoLineWithDriftState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private int _index;
    private bool _hasPrev;

    public AutoLineWithDriftState(int length = 500, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, inputName);
        _values = new PooledRingBuffer<double>(_length + 2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AutoLineWithDriftState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, selector);
        _values = new PooledRingBuffer<double>(_length + 2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AutoLineWithDrift;

    public void Reset()
    {
        _stdDev.Reset();
        _values.Clear();
        _prevA = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var dev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var r = Math.Round(value);
        var prevA = _hasPrev ? _prevA : r;
        var priorA = r;
        if (_index >= _length + 1 && _values.Count >= _length + 1)
        {
            var priorIndex = _values.Count - (_length + 1);
            priorA = priorIndex >= 0 ? _values[priorIndex] : r;
        }

        var drift = (double)1 / (_length * 2) * (prevA - priorA);
        var a = value > prevA + dev ? value : value < prevA - dev ? value : prevA + drift;

        if (isFinal)
        {
            _values.TryAdd(a, out _);
            _prevA = a;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Alwd", a }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _values.Dispose();
    }
}

public sealed class AutonomousRecursiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _momLength;
    private readonly double _gamma;
    private readonly RollingWindowSum _cSum;
    private readonly RollingWindowSum _ma1Sum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _absDiffSum;
    private double _prevMad;
    private int _index;
    private bool _hasPrev;

    public AutonomousRecursiveMovingAverageState(int length = 14, int momLength = 7, double gamma = 3,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _momLength = Math.Max(1, momLength);
        _gamma = gamma;
        _cSum = new RollingWindowSum(_length);
        _ma1Sum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(Math.Max(_length, _momLength) + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AutonomousRecursiveMovingAverageState(int length, int momLength, double gamma,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _momLength = Math.Max(1, momLength);
        _gamma = gamma;
        _cSum = new RollingWindowSum(_length);
        _ma1Sum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(Math.Max(_length, _momLength) + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AutonomousRecursiveMovingAverage;

    public void Reset()
    {
        _cSum.Reset();
        _ma1Sum.Reset();
        _values.Clear();
        _absDiffSum = 0;
        _prevMad = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevMad = _hasPrev ? _prevMad : value;
        var priorValue = _index >= _length && _values.Count >= _momLength ? _values[_values.Count - _momLength] : 0;
        var absDiff = Math.Abs(priorValue - prevMad);
        var absDiffSum = _absDiffSum + absDiff;
        var d = _index != 0 ? absDiffSum / _index * _gamma : 0;
        var c = value > prevMad + d ? value + d : value < prevMad - d ? value - d : prevMad;
        int cCount;
        var cSum = isFinal ? _cSum.Add(c, out cCount) : _cSum.Preview(c, out cCount);
        var ma1 = cCount > 0 ? cSum / cCount : 0;
        int ma1Count;
        var ma1Sum = isFinal ? _ma1Sum.Add(ma1, out ma1Count) : _ma1Sum.Preview(ma1, out ma1Count);
        var mad = ma1Count > 0 ? ma1Sum / ma1Count : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _absDiffSum = absDiffSum;
            _prevMad = mad;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Arma", mad }
            };
        }

        return new StreamingIndicatorStateResult(mad, outputs);
    }

    public void Dispose()
    {
        _cSum.Dispose();
        _ma1Sum.Dispose();
        _values.Dispose();
    }
}

public sealed class AverageAbsoluteErrorNormalizationState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _eSum;
    private readonly RollingWindowSum _eAbsSum;
    private readonly StreamingInputResolver _input;
    private double _prevY;
    private bool _hasPrev;

    public AverageAbsoluteErrorNormalizationState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _eSum = new RollingWindowSum(resolved);
        _eAbsSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AverageAbsoluteErrorNormalizationState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _eSum = new RollingWindowSum(resolved);
        _eAbsSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AverageAbsoluteErrorNormalization;

    public void Reset()
    {
        _eSum.Reset();
        _eAbsSum.Reset();
        _prevY = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevY = _hasPrev ? _prevY : value;
        var e = value - prevY;
        int eCount;
        var eSum = isFinal ? _eSum.Add(e, out eCount) : _eSum.Preview(e, out eCount);
        var eAbs = Math.Abs(e);
        int eAbsCount;
        var eAbsSum = isFinal ? _eAbsSum.Add(eAbs, out eAbsCount) : _eAbsSum.Preview(eAbs, out eAbsCount);
        var eAbsSma = eAbsCount > 0 ? eAbsSum / eAbsCount : 0;
        var eSma = eCount > 0 ? eSum / eCount : 0;
        var a = eAbsSma != 0 ? MathHelper.MinOrMax(eSma / eAbsSma, 1, -1) : 0;
        var y = value + (a * eAbsSma);

        if (isFinal)
        {
            _prevY = y;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Aaen", a }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _eSum.Dispose();
        _eAbsSum.Dispose();
    }
}

public sealed class AverageMoneyFlowOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _avgVolume;
    private readonly IMovingAverageSmoother _avgChange;
    private readonly IMovingAverageSmoother _signal;
    private readonly RollingWindowMax _rMax;
    private readonly RollingWindowMin _rMin;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public AverageMoneyFlowOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 5,
        int smoothLength = 3, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        _avgVolume = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _avgChange = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _rMax = new RollingWindowMax(resolvedLength);
        _rMin = new RollingWindowMin(resolvedLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AverageMoneyFlowOscillatorState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        _avgVolume = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _avgChange = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _rMax = new RollingWindowMax(resolvedLength);
        _rMin = new RollingWindowMin(resolvedLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AverageMoneyFlowOscillator;

    public void Reset()
    {
        _avgVolume.Reset();
        _avgChange.Reset();
        _signal.Reset();
        _rMax.Reset();
        _rMin.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var chg = _hasPrev ? value - prevValue : 0;
        var avgv = _avgVolume.Next(bar.Volume, isFinal);
        var avgc = _avgChange.Next(chg, isFinal);
        var product = avgv * avgc;
        var r = Math.Abs(product) > 0 ? Math.Log(Math.Abs(product)) * Math.Sign(avgc) : 0;
        var rh = isFinal ? _rMax.Add(r, out _) : _rMax.Preview(r, out _);
        var rl = isFinal ? _rMin.Add(r, out _) : _rMin.Preview(r, out _);
        var rs = rh != rl ? (r - rl) / (rh - rl) * 100 : 0;
        var k = (rs * 2) - 100;
        var ks = _signal.Next(k, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Amfo", ks }
            };
        }

        return new StreamingIndicatorStateResult(ks, outputs);
    }

    public void Dispose()
    {
        _avgVolume.Dispose();
        _avgChange.Dispose();
        _signal.Dispose();
        _rMax.Dispose();
        _rMin.Dispose();
    }
}

public sealed class AverageTrueRangeTrailingStopsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly IMovingAverageSmoother _atr;
    private readonly StreamingInputResolver _input;
    private readonly double _factor;
    private double _prevValue;
    private double _prevAtrts;
    private bool _hasPrev;

    public AverageTrueRangeTrailingStopsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 63, int length2 = 21, double factor = 3, InputName inputName = InputName.Close)
    {
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _atr = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _factor = factor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public AverageTrueRangeTrailingStopsState(MovingAvgType maType, int length1, int length2, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _atr = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _factor = factor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.AverageTrueRangeTrailingStops;

    public void Reset()
    {
        _ema.Reset();
        _atr.Reset();
        _prevValue = 0;
        _prevAtrts = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var ema = _ema.Next(value, isFinal);
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atr.Next(tr, isFinal);
        var prevAtrts = _hasPrev ? _prevAtrts : value;
        var upTrend = value > ema;
        var dnTrend = value <= ema;
        var atrts = upTrend ? Math.Max(value - (_factor * atr), prevAtrts) : dnTrend
            ? Math.Min(value + (_factor * atr), prevAtrts)
            : prevAtrts;

        if (isFinal)
        {
            _prevValue = value;
            _prevAtrts = atrts;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Atrts", atrts }
            };
        }

        return new StreamingIndicatorStateResult(atrts, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _atr.Dispose();
    }
}

public sealed class BayesianOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _basisSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly RollingWindowSum _probUpperUp;
    private readonly RollingWindowSum _probUpperDown;
    private readonly RollingWindowSum _probBasisUp;
    private readonly RollingWindowSum _probBasisDown;
    private readonly StreamingInputResolver _input;
    private double _basisValue;

    public BayesianOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        double stdDevMult = 2.5, double lowerThreshold = 15, InputName inputName = InputName.Close)
    {
        _ = lowerThreshold;
        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _basisValue);
        _probUpperUp = new RollingWindowSum(resolved);
        _probUpperDown = new RollingWindowSum(resolved);
        _probBasisUp = new RollingWindowSum(resolved);
        _probBasisDown = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BayesianOscillatorState(MovingAvgType maType, int length, double stdDevMult, double lowerThreshold,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ = lowerThreshold;
        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _basisValue);
        _probUpperUp = new RollingWindowSum(resolved);
        _probUpperDown = new RollingWindowSum(resolved);
        _probBasisUp = new RollingWindowSum(resolved);
        _probBasisDown = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BayesianOscillator;

    public void Reset()
    {
        _basisSmoother.Reset();
        _stdDev.Reset();
        _probUpperUp.Reset();
        _probUpperDown.Reset();
        _probBasisUp.Reset();
        _probBasisDown.Reset();
        _basisValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisSmoother.Next(value, isFinal);
        _basisValue = basis;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = basis + (stdDev * _stdDevMult);

        var upperUpSeq = value > upper ? 1 : 0;
        int upperUpCount;
        var upperUpSum = isFinal ? _probUpperUp.Add(upperUpSeq, out upperUpCount)
            : _probUpperUp.Preview(upperUpSeq, out upperUpCount);
        var probUpperUp = upperUpCount > 0 ? upperUpSum / upperUpCount : 0;

        var upperDownSeq = value < upper ? 1 : 0;
        int upperDownCount;
        var upperDownSum = isFinal ? _probUpperDown.Add(upperDownSeq, out upperDownCount)
            : _probUpperDown.Preview(upperDownSeq, out upperDownCount);
        var probUpperDown = upperDownCount > 0 ? upperDownSum / upperDownCount : 0;

        var probUpBbUpper = probUpperUp + probUpperDown != 0 ? probUpperUp / (probUpperUp + probUpperDown) : 0;
        var probDownBbUpper = probUpperUp + probUpperDown != 0 ? probUpperDown / (probUpperUp + probUpperDown) : 0;

        var basisUpSeq = value > basis ? 1 : 0;
        int basisUpCount;
        var basisUpSum = isFinal ? _probBasisUp.Add(basisUpSeq, out basisUpCount)
            : _probBasisUp.Preview(basisUpSeq, out basisUpCount);
        var probBasisUp = basisUpCount > 0 ? basisUpSum / basisUpCount : 0;

        var basisDownSeq = value < basis ? 1 : 0;
        int basisDownCount;
        var basisDownSum = isFinal ? _probBasisDown.Add(basisDownSeq, out basisDownCount)
            : _probBasisDown.Preview(basisDownSeq, out basisDownCount);
        var probBasisDown = basisDownCount > 0 ? basisDownSum / basisDownCount : 0;

        var probUpBbBasis = probBasisUp + probBasisDown != 0 ? probBasisUp / (probBasisUp + probBasisDown) : 0;
        var probDownBbBasis = probBasisUp + probBasisDown != 0 ? probBasisDown / (probBasisUp + probBasisDown) : 0;

        var sigmaProbsDown = probUpBbUpper != 0 && probUpBbBasis != 0
            ? 1 + ((1 - probUpBbUpper) * (1 - probUpBbBasis))
            : 0;
        var sigmaProbsUp = probDownBbUpper != 0 && probDownBbBasis != 0
            ? 1 + ((1 - probDownBbUpper) * (1 - probDownBbBasis))
            : 0;
        var probPrime = sigmaProbsDown != 0 && sigmaProbsUp != 0
            ? 1 + ((1 - sigmaProbsDown) * (1 - sigmaProbsUp))
            : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "SigmaProbsDown", sigmaProbsDown },
                { "SigmaProbsUp", sigmaProbsUp },
                { "ProbPrime", probPrime }
            };
        }

        return new StreamingIndicatorStateResult(probPrime, outputs);
    }

    public void Dispose()
    {
        _basisSmoother.Dispose();
        _stdDev.Dispose();
        _probUpperUp.Dispose();
        _probUpperDown.Dispose();
        _probBasisUp.Dispose();
        _probBasisDown.Dispose();
    }
}

public sealed class BetterVolumeIndicatorState : IStreamingIndicatorState
{
    private double _prevClose;
    private bool _hasPrev;

    public BetterVolumeIndicatorState(int length = 8, int lbLength = 2)
    {
        _ = length;
        _ = lbLength;
    }

    public IndicatorName Name => IndicatorName.BetterVolumeIndicator;

    public void Reset()
    {
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentHigh = bar.High;
        var currentLow = bar.Low;
        var currentOpen = bar.Open;
        var currentClose = bar.Close;
        var currentVolume = bar.Volume;
        var prevClose = _hasPrev ? _prevClose : 0;
        var range = CalculationsHelper.CalculateTrueRange(currentHigh, currentLow, prevClose);
        var v1 = currentClose > currentOpen
            ? range / ((2 * range) + currentOpen - currentClose) * currentVolume
            : currentClose < currentOpen
                ? (range + currentClose - currentOpen) / ((2 * range) + currentClose - currentOpen) * currentVolume
                : 0.5 * currentVolume;

        if (isFinal)
        {
            _prevClose = currentClose;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Bvi", v1 }
            };
        }

        return new StreamingIndicatorStateResult(v1, outputs);
    }
}

public sealed class BilateralStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma;
    private readonly IMovingAverageSmoother _rangeSmoother;
    private readonly IMovingAverageSmoother _signal;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public BilateralStochasticOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        int signalLength = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _rangeSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BilateralStochasticOscillatorState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _rangeSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BilateralStochasticOscillator;

    public void Reset()
    {
        _sma.Reset();
        _rangeSmoother.Reset();
        _signal.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var highest = isFinal ? _highWindow.Add(sma, out _) : _highWindow.Preview(sma, out _);
        var lowest = isFinal ? _lowWindow.Add(sma, out _) : _lowWindow.Preview(sma, out _);
        var range = highest - lowest;
        var rangeSma = _rangeSmoother.Next(range, isFinal);
        var bull = rangeSma != 0 ? (sma / rangeSma) - (lowest / rangeSma) : 0;
        var bear = rangeSma != 0 ? Math.Abs((sma / rangeSma) - (highest / rangeSma)) : 0;
        var max = Math.Max(bull, bear);
        var signal = _signal.Next(max, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Bull", bull },
                { "Bear", bear },
                { "Bso", max },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(max, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _rangeSmoother.Dispose();
        _signal.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class BollingerBandsAverageTrueRangeState : IStreamingIndicatorState, IDisposable
{
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _basisSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _atrMaSmoother;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevAtrMa;
    private double _basisValue;
    private bool _hasPrev;

    public BollingerBandsAverageTrueRangeState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int atrLength = 22,
        int length = 55, double stdDevMult = 2, InputName inputName = InputName.Close)
    {
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _stdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, length), _ => _basisValue);
        _atrMaSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, atrLength));
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, atrLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public BollingerBandsAverageTrueRangeState(MovingAvgType maType, int atrLength, int length, double stdDevMult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _stdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, length), _ => _basisValue);
        _atrMaSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, atrLength));
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, atrLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BollingerBandsAverageTrueRange;

    public void Reset()
    {
        _basisSmoother.Reset();
        _stdDev.Reset();
        _atrMaSmoother.Reset();
        _atrSmoother.Reset();
        _prevAtrMa = 0;
        _basisValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisSmoother.Next(value, isFinal);
        _basisValue = basis;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = basis + (stdDev * _stdDevMult);
        var lower = basis - (stdDev * _stdDevMult);
        var atrMa = _atrMaSmoother.Next(value, isFinal);
        var prevAtrMa = _hasPrev ? _prevAtrMa : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevAtrMa);
        var atr = _atrSmoother.Next(tr, isFinal);
        var bbDiff = upper - lower;
        var atrDev = bbDiff != 0 ? atr / bbDiff : 0;

        if (isFinal)
        {
            _prevAtrMa = atrMa;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "AtrDev", atrDev }
            };
        }

        return new StreamingIndicatorStateResult(atrDev, outputs);
    }

    public void Dispose()
    {
        _basisSmoother.Dispose();
        _stdDev.Dispose();
        _atrMaSmoother.Dispose();
        _atrSmoother.Dispose();
    }
}

public sealed class BollingerBandsFibonacciRatiosState : IStreamingIndicatorState, IDisposable
{
    private readonly double _fibRatio3;
    private readonly IMovingAverageSmoother _sma;
    private readonly IMovingAverageSmoother _atr;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public BollingerBandsFibonacciRatiosState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        double fibRatio1 = MathHelper.Phi, double fibRatio2 = MathHelper.Phi + 1,
        double fibRatio3 = (2 * MathHelper.Phi) + 1, InputName inputName = InputName.Close)
    {
        _ = fibRatio1;
        _ = fibRatio2;
        _fibRatio3 = fibRatio3;
        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _atr = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BollingerBandsFibonacciRatiosState(MovingAvgType maType, int length, double fibRatio1, double fibRatio2,
        double fibRatio3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ = fibRatio1;
        _ = fibRatio2;
        _fibRatio3 = fibRatio3;
        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _atr = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BollingerBandsFibonacciRatios;

    public void Reset()
    {
        _sma.Reset();
        _atr.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atr.Next(tr, isFinal);
        var r3 = atr * _fibRatio3;
        var upper = sma + r3;
        var lower = sma - r3;
        var middle = sma;

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _atr.Dispose();
    }
}

public sealed class BollingerBandsPercentBState : IStreamingIndicatorState, IDisposable
{
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _basisSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private double _basisValue;

    public BollingerBandsPercentBState(double stdDevMult = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _basisValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BollingerBandsPercentBState(double stdDevMult, MovingAvgType maType, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _basisValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BollingerBandsPercentB;

    public void Reset()
    {
        _basisSmoother.Reset();
        _stdDev.Reset();
        _basisValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisSmoother.Next(value, isFinal);
        _basisValue = basis;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = basis + (stdDev * _stdDevMult);
        var lower = basis - (stdDev * _stdDevMult);
        var pctB = upper - lower != 0 ? (value - lower) / (upper - lower) * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "PctB", pctB }
            };
        }

        return new StreamingIndicatorStateResult(pctB, outputs);
    }

    public void Dispose()
    {
        _basisSmoother.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class BollingerBandsWidthState : IStreamingIndicatorState, IDisposable
{
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _basisSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private double _basisValue;

    public BollingerBandsWidthState(double stdDevMult = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _basisValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BollingerBandsWidthState(double stdDevMult, MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _basisValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BollingerBandsWidth;

    public void Reset()
    {
        _basisSmoother.Reset();
        _stdDev.Reset();
        _basisValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisSmoother.Next(value, isFinal);
        _basisValue = basis;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = basis + (stdDev * _stdDevMult);
        var lower = basis - (stdDev * _stdDevMult);
        var bbWidth = basis != 0 ? (upper - lower) / basis : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "BbWidth", bbWidth }
            };
        }

        return new StreamingIndicatorStateResult(bbWidth, outputs);
    }

    public void Dispose()
    {
        _basisSmoother.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class BollingerBandsWithAtrPctState : IStreamingIndicatorState, IDisposable
{
    private readonly double _ratio;
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _basisSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevAptr;
    private bool _hasPrev;

    public BollingerBandsWithAtrPctState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int bbLength = 20, double stdDevMult = 2, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        _ratio = (double)2 / (resolvedLength + 1);
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, bbLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public BollingerBandsWithAtrPctState(MovingAvgType maType, int length, int bbLength, double stdDevMult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        _ratio = (double)2 / (resolvedLength + 1);
        _stdDevMult = stdDevMult;
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, bbLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BollingerBandsWithAtrPct;

    public void Reset()
    {
        _basisSmoother.Reset();
        _prevValue = 0;
        _prevAptr = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisSmoother.Next(value, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;
        var lh = bar.High - bar.Low;
        var hc = Math.Abs(bar.High - prevValue);
        var lc = Math.Abs(bar.Low - prevValue);
        var mm = Math.Max(Math.Max(lh, hc), lc);
        var atrs = mm == hc ? hc / (prevValue + (hc / 2))
            : mm == lc ? lc / (bar.Low + (lc / 2))
            : mm == lh ? lh / (bar.Low + (lh / 2))
            : 0;
        var aptr = (100 * atrs * _ratio) + (_prevAptr * (1 - _ratio));
        var dev = _stdDevMult * aptr;
        var upper = basis + (basis * dev / 100);
        var lower = basis - (basis * dev / 100);
        var middle = basis;

        if (isFinal)
        {
            _prevValue = value;
            _prevAptr = aptr;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _basisSmoother.Dispose();
    }
}

public sealed class BreakoutRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _volumeSum;
    private readonly RollingWindowSum _posPowerSum;
    private readonly RollingWindowSum _negPowerSum;
    private readonly StreamingInputResolver _input;
    private double _prevBoPower;

    public BreakoutRelativeStrengthIndexState(InputName inputName = InputName.FullTypicalPrice, int length = 14,
        int lbLength = 2)
    {
        _length = Math.Max(1, length);
        _volumeSum = new RollingWindowSum(Math.Max(1, lbLength));
        _posPowerSum = new RollingWindowSum(_length);
        _negPowerSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BreakoutRelativeStrengthIndexState(InputName inputName, int length, int lbLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _volumeSum = new RollingWindowSum(Math.Max(1, lbLength));
        _posPowerSum = new RollingWindowSum(_length);
        _negPowerSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.BreakoutRelativeStrengthIndex;

    public void Reset()
    {
        _volumeSum.Reset();
        _posPowerSum.Reset();
        _negPowerSum.Reset();
        _prevBoPower = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volumeSum = isFinal ? _volumeSum.Add(bar.Volume, out _) : _volumeSum.Preview(bar.Volume, out _);
        var boStrength = bar.High - bar.Low != 0 ? (bar.Close - bar.Open) / (bar.High - bar.Low) : 0;
        var boPower = value * boStrength * volumeSum;
        var posPower = boPower > _prevBoPower ? Math.Abs(boPower) : 0;
        var negPower = boPower < _prevBoPower ? Math.Abs(boPower) : 0;
        var posSum = isFinal ? _posPowerSum.Add(posPower, out _) : _posPowerSum.Preview(posPower, out _);
        var negSum = isFinal ? _negPowerSum.Add(negPower, out _) : _negPowerSum.Preview(negPower, out _);
        var boRatio = negSum != 0 ? posSum / negSum : 0;
        var brsi = negSum == 0 ? 100 : posSum == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + boRatio)), 100, 0);

        if (isFinal)
        {
            _prevBoPower = boPower;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Brsi", brsi }
            };
        }

        return new StreamingIndicatorStateResult(brsi, outputs);
    }

    public void Dispose()
    {
        _volumeSum.Dispose();
        _posPowerSum.Dispose();
        _negPowerSum.Dispose();
    }
}

public sealed class BryantAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _maxLength;
    private readonly double _trend;
    private readonly EfficiencyRatioState _er;
    private readonly StreamingInputResolver _input;
    private double _prevBama;
    private bool _hasPrev;

    public BryantAdaptiveMovingAverageState(int length = 14, int maxLength = 100, double trend = -1,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _maxLength = maxLength;
        _trend = trend;
        _er = new EfficiencyRatioState(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BryantAdaptiveMovingAverageState(int length, int maxLength, double trend, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _maxLength = maxLength;
        _trend = trend;
        _er = new EfficiencyRatioState(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BryantAdaptiveMovingAverage;

    public void Reset()
    {
        _er.Reset();
        _prevBama = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = _er.Next(value, isFinal);
        var ver = MathHelper.Pow(er - (((2 * er) - 1) / 2 * (1 - _trend)) + 0.5, 2);
        var vLength = ver != 0 ? (_length - ver + 1) / ver : 0;
        vLength = Math.Min(vLength, _maxLength);
        var vAlpha = 2 / (vLength + 1);
        var prevBama = _hasPrev ? _prevBama : 0;
        var bama = (vAlpha * value) + ((1 - vAlpha) * prevBama);

        if (isFinal)
        {
            _prevBama = bama;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Bama", bama }
            };
        }

        return new StreamingIndicatorStateResult(bama, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}

public sealed class BuffAverageState : IStreamingIndicatorState, IDisposable    
{
    private readonly RollingWindowSum _fastPriceSum;
    private readonly RollingWindowSum _fastVolumeSum;
    private readonly RollingWindowSum _slowPriceSum;
    private readonly RollingWindowSum _slowVolumeSum;
    private readonly StreamingInputResolver _input;

    public BuffAverageState(int fastLength = 5, int slowLength = 20, InputName inputName = InputName.Close)
    {
        _fastPriceSum = new RollingWindowSum(Math.Max(1, fastLength));
        _fastVolumeSum = new RollingWindowSum(Math.Max(1, fastLength));
        _slowPriceSum = new RollingWindowSum(Math.Max(1, slowLength));
        _slowVolumeSum = new RollingWindowSum(Math.Max(1, slowLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public BuffAverageState(int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastPriceSum = new RollingWindowSum(Math.Max(1, fastLength));
        _fastVolumeSum = new RollingWindowSum(Math.Max(1, fastLength));
        _slowPriceSum = new RollingWindowSum(Math.Max(1, slowLength));
        _slowVolumeSum = new RollingWindowSum(Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BuffAverage;

    public void Reset()
    {
        _fastPriceSum.Reset();
        _fastVolumeSum.Reset();
        _slowPriceSum.Reset();
        _slowVolumeSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var priceVol = value * volume;
        var fastPriceSum = isFinal ? _fastPriceSum.Add(priceVol, out _) : _fastPriceSum.Preview(priceVol, out _);
        var fastVolumeSum = isFinal ? _fastVolumeSum.Add(volume, out _) : _fastVolumeSum.Preview(volume, out _);
        var slowPriceSum = isFinal ? _slowPriceSum.Add(priceVol, out _) : _slowPriceSum.Preview(priceVol, out _);
        var slowVolumeSum = isFinal ? _slowVolumeSum.Add(volume, out _) : _slowVolumeSum.Preview(volume, out _);
        var fastBuff = fastVolumeSum != 0 ? fastPriceSum / fastVolumeSum : 0;
        var slowBuff = slowVolumeSum != 0 ? slowPriceSum / slowVolumeSum : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "FastBuff", fastBuff },
                { "SlowBuff", slowBuff }
            };
        }

        return new StreamingIndicatorStateResult(fastBuff, outputs);
    }

    public void Dispose()
    {
        _fastPriceSum.Dispose();
        _fastVolumeSum.Dispose();
        _slowPriceSum.Dispose();
        _slowVolumeSum.Dispose();
    }
}

public sealed class CalmarRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _power;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _drawdownMin;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public CalmarRatioState(int length = 30, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var windowLength = Math.Max(_length, 2);
        _maxWindow = new RollingWindowMax(windowLength);
        _drawdownMin = new RollingWindowMin(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
        _power = CalculatePower(_length);
    }

    public CalmarRatioState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var windowLength = Math.Max(_length, 2);
        _maxWindow = new RollingWindowMax(windowLength);
        _drawdownMin = new RollingWindowMin(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _power = CalculatePower(_length);
    }

    public IndicatorName Name => IndicatorName.CalmarRatio;

    public void Reset()
    {
        _maxWindow.Reset();
        _drawdownMin.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _values.Count >= _length ? _values[0] : 0;

        var maxValue = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);
        var drawdown = maxValue != 0 ? (value - maxValue) / maxValue : 0;
        var maxDrawdown = isFinal
            ? _drawdownMin.Add(drawdown, out _)
            : _drawdownMin.Preview(drawdown, out _);

        var ret = prevValue != 0 ? (value / prevValue) - 1 : 0;
        var annualReturn = 1 + ret >= 0 ? MathHelper.Pow(1 + ret, _power) - 1 : 0;
        var calmar = maxDrawdown != 0 ? annualReturn / Math.Abs(maxDrawdown) : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cr", calmar }
            };
        }

        return new StreamingIndicatorStateResult(calmar, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _drawdownMin.Dispose();
        _values.Dispose();
    }

    private static double CalculatePower(int length)
    {
        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        return barsPerYr / (length * 15d);
    }
}

public sealed class CamarillaPivotPointsState : IStreamingIndicatorState
{
    private double _prevClose;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.CamarillaPivotPoints;

    public void Reset()
    {
        _prevClose = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var currentClose = _hasPrev ? prevClose : bar.Close;
        var currentHigh = _hasPrev ? prevHigh : bar.High;
        var currentLow = _hasPrev ? prevLow : bar.Low;
        var range = currentHigh - currentLow;

        var pivot = (prevHigh + prevLow + prevClose) / 3;
        var support1 = currentClose - (0.0916 * range);
        var support2 = currentClose - (0.183 * range);
        var support3 = currentClose - (0.275 * range);
        var support4 = currentClose - (0.55 * range);
        var resistance1 = currentClose + (0.0916 * range);
        var resistance2 = currentClose + (0.183 * range);
        var resistance3 = currentClose + (0.275 * range);
        var resistance4 = currentClose + (0.55 * range);
        var resistance5 = currentLow != 0 ? currentHigh / currentLow * currentClose : 0;
        var support5 = currentClose - (resistance5 - currentClose);
        var midpoint1 = (support3 + support2) / 2;
        var midpoint2 = (support2 + support1) / 2;
        var midpoint3 = (resistance2 + resistance1) / 2;
        var midpoint4 = (resistance3 + resistance2) / 2;
        var midpoint5 = (resistance3 + resistance4) / 2;
        var midpoint6 = (support4 + support3) / 2;

        if (isFinal)
        {
            _prevClose = bar.Close;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(17)
            {
                { "Pivot", pivot },
                { "S1", support1 },
                { "S2", support2 },
                { "S3", support3 },
                { "S4", support4 },
                { "S5", support5 },
                { "R1", resistance1 },
                { "R2", resistance2 },
                { "R3", resistance3 },
                { "R4", resistance4 },
                { "R5", resistance5 },
                { "M1", midpoint1 },
                { "M2", midpoint2 },
                { "M3", midpoint3 },
                { "M4", midpoint4 },
                { "M5", midpoint5 },
                { "M6", midpoint6 }
            };
        }

        return new StreamingIndicatorStateResult(pivot, outputs);
    }
}

public sealed class CCTStochRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi5;
    private readonly RsiState _rsi8;
    private readonly RsiState _rsi13;
    private readonly RsiState _rsi14;
    private readonly RsiState _rsi21;
    private readonly RollingWindowMin _rsi21Len2Min;
    private readonly RollingWindowMax _rsi21Len2Max;
    private readonly RollingWindowMin _rsi21Len3Min;
    private readonly RollingWindowMax _rsi21Len3Max;
    private readonly RollingWindowMin _rsi21Len5Min;
    private readonly RollingWindowMax _rsi21Len5Max;
    private readonly RollingWindowMin _rsi14Len4Min;
    private readonly RollingWindowMax _rsi14Len4Max;
    private readonly RollingWindowMin _rsi5Len1Min;
    private readonly RollingWindowMax _rsi5Len1Max;
    private readonly RollingWindowMin _rsi13Len3Min;
    private readonly RollingWindowMax _rsi13Len3Max;
    private readonly RollingWindowMin _rsi8Len2Min;
    private readonly RollingWindowMax _rsi8Len2Max;
    private readonly IMovingAverageSmoother _type4Smoother;
    private readonly IMovingAverageSmoother _type5Smoother;
    private readonly IMovingAverageSmoother _type6Smoother;
    private readonly IMovingAverageSmoother _customSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public CCTStochRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 5, int length2 = 8, int length3 = 13, int length4 = 14, int length5 = 21,
        int smoothLength1 = 3, int smoothLength2 = 8, int signalLength = 9, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var resolved4 = Math.Max(1, length4);
        var resolved5 = Math.Max(1, length5);
        _rsi5 = new RsiState(maType, resolved1);
        _rsi8 = new RsiState(maType, resolved2);
        _rsi13 = new RsiState(maType, resolved3);
        _rsi14 = new RsiState(maType, resolved4);
        _rsi21 = new RsiState(maType, resolved5);
        _rsi21Len2Min = new RollingWindowMin(resolved2);
        _rsi21Len2Max = new RollingWindowMax(resolved2);
        _rsi21Len3Min = new RollingWindowMin(resolved3);
        _rsi21Len3Max = new RollingWindowMax(resolved3);
        _rsi21Len5Min = new RollingWindowMin(resolved5);
        _rsi21Len5Max = new RollingWindowMax(resolved5);
        _rsi14Len4Min = new RollingWindowMin(resolved4);
        _rsi14Len4Max = new RollingWindowMax(resolved4);
        _rsi5Len1Min = new RollingWindowMin(resolved1);
        _rsi5Len1Max = new RollingWindowMax(resolved1);
        _rsi13Len3Min = new RollingWindowMin(resolved3);
        _rsi13Len3Max = new RollingWindowMax(resolved3);
        _rsi8Len2Min = new RollingWindowMin(resolved2);
        _rsi8Len2Max = new RollingWindowMax(resolved2);
        _type4Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _type5Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _type6Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _customSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public CCTStochRelativeStrengthIndexState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        int length5, int smoothLength1, int smoothLength2, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var resolved4 = Math.Max(1, length4);
        var resolved5 = Math.Max(1, length5);
        _rsi5 = new RsiState(maType, resolved1);
        _rsi8 = new RsiState(maType, resolved2);
        _rsi13 = new RsiState(maType, resolved3);
        _rsi14 = new RsiState(maType, resolved4);
        _rsi21 = new RsiState(maType, resolved5);
        _rsi21Len2Min = new RollingWindowMin(resolved2);
        _rsi21Len2Max = new RollingWindowMax(resolved2);
        _rsi21Len3Min = new RollingWindowMin(resolved3);
        _rsi21Len3Max = new RollingWindowMax(resolved3);
        _rsi21Len5Min = new RollingWindowMin(resolved5);
        _rsi21Len5Max = new RollingWindowMax(resolved5);
        _rsi14Len4Min = new RollingWindowMin(resolved4);
        _rsi14Len4Max = new RollingWindowMax(resolved4);
        _rsi5Len1Min = new RollingWindowMin(resolved1);
        _rsi5Len1Max = new RollingWindowMax(resolved1);
        _rsi13Len3Min = new RollingWindowMin(resolved3);
        _rsi13Len3Max = new RollingWindowMax(resolved3);
        _rsi8Len2Min = new RollingWindowMin(resolved2);
        _rsi8Len2Max = new RollingWindowMax(resolved2);
        _type4Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _type5Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _type6Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _customSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.CCTStochRelativeStrengthIndex;

    public void Reset()
    {
        _rsi5.Reset();
        _rsi8.Reset();
        _rsi13.Reset();
        _rsi14.Reset();
        _rsi21.Reset();
        _rsi21Len2Min.Reset();
        _rsi21Len2Max.Reset();
        _rsi21Len3Min.Reset();
        _rsi21Len3Max.Reset();
        _rsi21Len5Min.Reset();
        _rsi21Len5Max.Reset();
        _rsi14Len4Min.Reset();
        _rsi14Len4Max.Reset();
        _rsi5Len1Min.Reset();
        _rsi5Len1Max.Reset();
        _rsi13Len3Min.Reset();
        _rsi13Len3Max.Reset();
        _rsi8Len2Min.Reset();
        _rsi8Len2Max.Reset();
        _type4Smoother.Reset();
        _type5Smoother.Reset();
        _type6Smoother.Reset();
        _customSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi5 = _rsi5.Next(value, isFinal);
        var rsi8 = _rsi8.Next(rsi5, isFinal);
        var rsi13 = _rsi13.Next(rsi8, isFinal);
        var rsi14 = _rsi14.Next(rsi13, isFinal);
        var rsi21 = _rsi21.Next(rsi14, isFinal);

        var rsi21Len2Min = isFinal ? _rsi21Len2Min.Add(rsi21, out _) : _rsi21Len2Min.Preview(rsi21, out _);
        var rsi21Len2Max = isFinal ? _rsi21Len2Max.Add(rsi21, out _) : _rsi21Len2Max.Preview(rsi21, out _);
        var rsi21Len3Min = isFinal ? _rsi21Len3Min.Add(rsi21, out _) : _rsi21Len3Min.Preview(rsi21, out _);
        var rsi21Len3Max = isFinal ? _rsi21Len3Max.Add(rsi21, out _) : _rsi21Len3Max.Preview(rsi21, out _);
        var rsi21Len5Min = isFinal ? _rsi21Len5Min.Add(rsi21, out _) : _rsi21Len5Min.Preview(rsi21, out _);
        var rsi21Len5Max = isFinal ? _rsi21Len5Max.Add(rsi21, out _) : _rsi21Len5Max.Preview(rsi21, out _);
        var rsi14Len4Min = isFinal ? _rsi14Len4Min.Add(rsi14, out _) : _rsi14Len4Min.Preview(rsi14, out _);
        var rsi14Len4Max = isFinal ? _rsi14Len4Max.Add(rsi14, out _) : _rsi14Len4Max.Preview(rsi14, out _);
        var rsi5Len1Min = isFinal ? _rsi5Len1Min.Add(rsi5, out _) : _rsi5Len1Min.Preview(rsi5, out _);
        var rsi5Len1Max = isFinal ? _rsi5Len1Max.Add(rsi5, out _) : _rsi5Len1Max.Preview(rsi5, out _);
        var rsi13Len3Min = isFinal ? _rsi13Len3Min.Add(rsi13, out _) : _rsi13Len3Min.Preview(rsi13, out _);
        var rsi13Len3Max = isFinal ? _rsi13Len3Max.Add(rsi13, out _) : _rsi13Len3Max.Preview(rsi13, out _);
        var rsi8Len2Min = isFinal ? _rsi8Len2Min.Add(rsi8, out _) : _rsi8Len2Min.Preview(rsi8, out _);
        var rsi8Len2Max = isFinal ? _rsi8Len2Max.Add(rsi8, out _) : _rsi8Len2Max.Preview(rsi8, out _);

        var range1 = rsi21Len3Max - rsi21Len3Min;
        var type1 = range1 != 0 ? (rsi21 - rsi21Len2Min) / range1 * 100 : 0;
        var range2 = rsi21Len5Max - rsi21Len5Min;
        var type2 = range2 != 0 ? (rsi21 - rsi21Len5Min) / range2 * 100 : 0;
        var range3 = rsi14Len4Max - rsi14Len4Min;
        var type3 = range3 != 0 ? (rsi14 - rsi14Len4Min) / range3 * 100 : 0;
        var range4 = rsi21Len2Max - rsi21Len3Min;
        var type4Raw = range4 != 0 ? (rsi21 - rsi21Len3Min) / range4 * 100 : 0;
        var range5 = rsi5Len1Max - rsi5Len1Min;
        var type5Raw = range5 != 0 ? (rsi5 - rsi5Len1Min) / range5 * 100 : 0;
        var range6 = rsi13Len3Max - rsi13Len3Min;
        var type6Raw = range6 != 0 ? (rsi13 - rsi13Len3Min) / range6 * 100 : 0;
        var rangeCustom = rsi8Len2Max - rsi8Len2Min;
        var customRaw = rangeCustom != 0 ? (rsi8 - rsi8Len2Min) / rangeCustom * 100 : 0;

        var type4 = _type4Smoother.Next(type4Raw, isFinal);
        var type5 = _type5Smoother.Next(type5Raw, isFinal);
        var type6 = _type6Smoother.Next(type6Raw, isFinal);
        var typeCustom = _customSmoother.Next(customRaw, isFinal);
        var signal = _signalSmoother.Next(type1, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(8)
            {
                { "Type1", type1 },
                { "Type2", type2 },
                { "Type3", type3 },
                { "Type4", type4 },
                { "Type5", type5 },
                { "Type6", type6 },
                { "TypeCustom", typeCustom },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(type1, outputs);
    }

    public void Dispose()
    {
        _rsi5.Dispose();
        _rsi8.Dispose();
        _rsi13.Dispose();
        _rsi14.Dispose();
        _rsi21.Dispose();
        _rsi21Len2Min.Dispose();
        _rsi21Len2Max.Dispose();
        _rsi21Len3Min.Dispose();
        _rsi21Len3Max.Dispose();
        _rsi21Len5Min.Dispose();
        _rsi21Len5Max.Dispose();
        _rsi14Len4Min.Dispose();
        _rsi14Len4Max.Dispose();
        _rsi5Len1Min.Dispose();
        _rsi5Len1Max.Dispose();
        _rsi13Len3Min.Dispose();
        _rsi13Len3Max.Dispose();
        _rsi8Len2Min.Dispose();
        _rsi8Len2Max.Dispose();
        _type4Smoother.Dispose();
        _type5Smoother.Dispose();
        _type6Smoother.Dispose();
        _customSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ChandeCompositeMomentumIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _smoothLength;
    private readonly RollingWindowSum _diff1Sum1;
    private readonly RollingWindowSum _diff1Sum2;
    private readonly RollingWindowSum _diff1Sum3;
    private readonly RollingWindowSum _diff2Sum1;
    private readonly RollingWindowSum _diff2Sum2;
    private readonly RollingWindowSum _diff2Sum3;
    private readonly RollingWindowSum _dmiSum;
    private readonly StandardDeviationVolatilityState _stdDev1;
    private readonly StandardDeviationVolatilityState _stdDev2;
    private readonly StandardDeviationVolatilityState _stdDev3;
    private readonly IMovingAverageSmoother _cmo5Smoother;
    private readonly IMovingAverageSmoother _cmo10Smoother;
    private readonly IMovingAverageSmoother _cmo20Smoother;
    private readonly StreamingInputResolver _input;
    private double _stdDev1Value;
    private double _stdDev2Value;
    private double _prevValue;
    private double _prevE;
    private bool _hasPrev;

    public ChandeCompositeMomentumIndexState(MovingAvgType maType = MovingAvgType.DoubleExponentialMovingAverage,
        int length1 = 5, int length2 = 10, int length3 = 20, int smoothLength = 3, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _smoothLength = Math.Max(1, smoothLength);
        _diff1Sum1 = new RollingWindowSum(resolved1);
        _diff1Sum2 = new RollingWindowSum(resolved2);
        _diff1Sum3 = new RollingWindowSum(resolved3);
        _diff2Sum1 = new RollingWindowSum(resolved1);
        _diff2Sum2 = new RollingWindowSum(resolved2);
        _diff2Sum3 = new RollingWindowSum(resolved3);
        _dmiSum = new RollingWindowSum(resolved1);
        _stdDev1 = new StandardDeviationVolatilityState(maType, resolved1, inputName);
        _stdDev2 = new StandardDeviationVolatilityState(maType, resolved2, _ => _stdDev1Value);
        _stdDev3 = new StandardDeviationVolatilityState(maType, resolved3, _ => _stdDev2Value);
        _cmo5Smoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _cmo10Smoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _cmo20Smoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeCompositeMomentumIndexState(MovingAvgType maType, int length1, int length2, int length3,
        int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _smoothLength = Math.Max(1, smoothLength);
        _diff1Sum1 = new RollingWindowSum(resolved1);
        _diff1Sum2 = new RollingWindowSum(resolved2);
        _diff1Sum3 = new RollingWindowSum(resolved3);
        _diff2Sum1 = new RollingWindowSum(resolved1);
        _diff2Sum2 = new RollingWindowSum(resolved2);
        _diff2Sum3 = new RollingWindowSum(resolved3);
        _dmiSum = new RollingWindowSum(resolved1);
        _stdDev1 = new StandardDeviationVolatilityState(maType, resolved1, selector);
        _stdDev2 = new StandardDeviationVolatilityState(maType, resolved2, _ => _stdDev1Value);
        _stdDev3 = new StandardDeviationVolatilityState(maType, resolved3, _ => _stdDev2Value);
        _cmo5Smoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _cmo10Smoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _cmo20Smoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeCompositeMomentumIndex;

    public void Reset()
    {
        _diff1Sum1.Reset();
        _diff1Sum2.Reset();
        _diff1Sum3.Reset();
        _diff2Sum1.Reset();
        _diff2Sum2.Reset();
        _diff2Sum3.Reset();
        _dmiSum.Reset();
        _stdDev1.Reset();
        _stdDev2.Reset();
        _stdDev3.Reset();
        _cmo5Smoother.Reset();
        _cmo10Smoother.Reset();
        _cmo20Smoother.Reset();
        _stdDev1Value = 0;
        _stdDev2Value = 0;
        _prevValue = 0;
        _prevE = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff1 = _hasPrev && value > prevValue ? value - prevValue : 0;
        var diff2 = _hasPrev && value < prevValue ? prevValue - value : 0;

        var diff1Sum1 = isFinal ? _diff1Sum1.Add(diff1, out _) : _diff1Sum1.Preview(diff1, out _);
        var diff2Sum1 = isFinal ? _diff2Sum1.Add(diff2, out _) : _diff2Sum1.Preview(diff2, out _);
        var diff1Sum2 = isFinal ? _diff1Sum2.Add(diff1, out _) : _diff1Sum2.Preview(diff1, out _);
        var diff2Sum2 = isFinal ? _diff2Sum2.Add(diff2, out _) : _diff2Sum2.Preview(diff2, out _);
        var diff1Sum3 = isFinal ? _diff1Sum3.Add(diff1, out _) : _diff1Sum3.Preview(diff1, out _);
        var diff2Sum3 = isFinal ? _diff2Sum3.Add(diff2, out _) : _diff2Sum3.Preview(diff2, out _);

        var cmo5Ratio = diff1Sum1 + diff2Sum1 != 0
            ? MathHelper.MinOrMax(100 * (diff1Sum1 - diff2Sum1) / (diff1Sum1 + diff2Sum1), 100, -100)
            : 0;
        var cmo10Ratio = diff1Sum2 + diff2Sum2 != 0
            ? MathHelper.MinOrMax(100 * (diff1Sum2 - diff2Sum2) / (diff1Sum2 + diff2Sum2), 100, -100)
            : 0;
        var cmo20Ratio = diff1Sum3 + diff2Sum3 != 0
            ? MathHelper.MinOrMax(100 * (diff1Sum3 - diff2Sum3) / (diff1Sum3 + diff2Sum3), 100, -100)
            : 0;

        var cmo5 = _cmo5Smoother.Next(cmo5Ratio, isFinal);
        var cmo10 = _cmo10Smoother.Next(cmo10Ratio, isFinal);
        var cmo20 = _cmo20Smoother.Next(cmo20Ratio, isFinal);

        var stdDev5 = _stdDev1.Update(bar, isFinal, includeOutputs: false).Value;
        _stdDev1Value = stdDev5;
        var stdDev10 = _stdDev2.Update(bar, isFinal, includeOutputs: false).Value;
        _stdDev2Value = stdDev10;
        var stdDev20 = _stdDev3.Update(bar, isFinal, includeOutputs: false).Value;
        var stdDevSum = stdDev5 + stdDev10 + stdDev20;
        var dmi = stdDevSum != 0
            ? MathHelper.MinOrMax(((stdDev5 * cmo5) + (stdDev10 * cmo10) + (stdDev20 * cmo20)) / stdDevSum, 100, -100)
            : 0;

        int count;
        var dmiSum = isFinal ? _dmiSum.Add(dmi, out count) : _dmiSum.Preview(dmi, out count);
        var s = count > 0 ? dmiSum / count : 0;
        var e = CalculationsHelper.CalculateEMA(dmi, _prevE, _smoothLength);

        if (isFinal)
        {
            _prevValue = value;
            _prevE = e;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ccmi", e },
                { "Signal", s }
            };
        }

        return new StreamingIndicatorStateResult(e, outputs);
    }

    public void Dispose()
    {
        _diff1Sum1.Dispose();
        _diff1Sum2.Dispose();
        _diff1Sum3.Dispose();
        _diff2Sum1.Dispose();
        _diff2Sum2.Dispose();
        _diff2Sum3.Dispose();
        _dmiSum.Dispose();
        _stdDev1.Dispose();
        _stdDev2.Dispose();
        _stdDev3.Dispose();
        _cmo5Smoother.Dispose();
        _cmo10Smoother.Dispose();
        _cmo20Smoother.Dispose();
    }
}

public sealed class ChandeForecastOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly LinearRegressionState _regression;
    private readonly StreamingInputResolver _input;
    private double _regressionInput;

    public ChandeForecastOscillatorState(int length = 14, InputName inputName = InputName.Close)
    {
        _regression = new LinearRegressionState(length, _ => _regressionInput);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeForecastOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _regression = new LinearRegressionState(length, _ => _regressionInput);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeForecastOscillator;

    public void Reset()
    {
        _regression.Reset();
        _regressionInput = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _regressionInput = value;
        var linReg = _regression.Update(bar, isFinal, includeOutputs: false).Value;
        var pf = value != 0 ? (value - linReg) * 100 / value : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cfo", pf }
            };
        }

        return new StreamingIndicatorStateResult(pf, outputs);
    }

    public void Dispose()
    {
        _regression.Dispose();
    }
}

public sealed class ChandeIntradayMomentumIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _gainsSum;
    private readonly RollingWindowSum _lossesSum;
    private double _prevGains;
    private double _prevLosses;

    public ChandeIntradayMomentumIndexState(int length = 14)
    {
        var resolved = Math.Max(1, length);
        _gainsSum = new RollingWindowSum(resolved);
        _lossesSum = new RollingWindowSum(resolved);
    }

    public IndicatorName Name => IndicatorName.ChandeIntradayMomentumIndex;

    public void Reset()
    {
        _gainsSum.Reset();
        _lossesSum.Reset();
        _prevGains = 0;
        _prevLosses = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentClose = bar.Close;
        var currentOpen = bar.Open;
        var gains = currentClose > currentOpen ? _prevGains + (currentClose - currentOpen) : 0;
        var losses = currentClose < currentOpen ? _prevLosses + (currentOpen - currentClose) : 0;
        var gainsSum = isFinal ? _gainsSum.Add(gains, out _) : _gainsSum.Preview(gains, out _);
        var lossesSum = isFinal ? _lossesSum.Add(losses, out _) : _lossesSum.Preview(losses, out _);
        var total = gainsSum + lossesSum;
        var imi = total != 0 ? MathHelper.MinOrMax(100 * gainsSum / total, 100, 0) : 0;

        if (isFinal)
        {
            _prevGains = gains;
            _prevLosses = losses;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cimi", imi }
            };
        }

        return new StreamingIndicatorStateResult(imi, outputs);
    }

    public void Dispose()
    {
        _gainsSum.Dispose();
        _lossesSum.Dispose();
    }
}

public sealed class ChandeKrollRSquaredIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowCorrelation _correlation;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private int _index;

    public ChandeKrollRSquaredIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int smoothLength = 3, InputName inputName = InputName.Close)
    {
        _correlation = new RollingWindowCorrelation(Math.Max(1, length));
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeKrollRSquaredIndexState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _correlation = new RollingWindowCorrelation(Math.Max(1, length));
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeKrollRSquaredIndex;

    public void Reset()
    {
        _correlation.Reset();
        _smoother.Reset();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var x = (double)_index;
        var r = isFinal ? _correlation.Add(x, value, out _) : _correlation.Preview(x, value, out _);
        var r2 = r * r;
        r2 = double.IsNaN(r2) || double.IsInfinity(r2) ? 0 : r2;
        var smoothed = _smoother.Next(r2, isFinal);

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ckrsi", smoothed }
            };
        }

        return new StreamingIndicatorStateResult(smoothed, outputs);
    }

    public void Dispose()
    {
        _correlation.Dispose();
        _smoother.Dispose();
    }
}

public sealed class ChandelierExitState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly double _mult;
    private double _prevClose;
    private bool _hasPrev;

    public ChandelierExitState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 22,
        double mult = 3)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _mult = mult;
    }

    public IndicatorName Name => IndicatorName.ChandelierExit;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _atrSmoother.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atrSmoother.Next(tr, isFinal);
        var highestHigh = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowestLow = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var exitLong = highestHigh - (atr * _mult);
        var exitShort = lowestLow + (atr * _mult);

        if (isFinal)
        {
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "ExitLong", exitLong },
                { "ExitShort", exitShort }
            };
        }

        return new StreamingIndicatorStateResult(exitLong, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _atrSmoother.Dispose();
    }
}

public sealed class ChandeMomentumOscillatorAbsoluteState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _absDiffSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ChandeMomentumOscillatorAbsoluteState(int length = 9, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _absDiffSum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeMomentumOscillatorAbsoluteState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _absDiffSum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorAbsolute;

    public void Reset()
    {
        _absDiffSum.Reset();
        _values.Clear();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priorValue = _values.Count >= _length ? _values[0] : 0;
        var diff = _hasPrev ? value - prevValue : 0;
        var absDiff = Math.Abs(diff);
        var absSum = isFinal ? _absDiffSum.Add(absDiff, out _) : _absDiffSum.Preview(absDiff, out _);
        var num = _values.Count >= _length ? Math.Abs(100 * (value - priorValue)) : 0;
        var cmoAbs = absSum != 0 ? MathHelper.MinOrMax(num / absSum, 100, 0) : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cmoa", cmoAbs }
            };
        }

        return new StreamingIndicatorStateResult(cmoAbs, outputs);
    }

    public void Dispose()
    {
        _absDiffSum.Dispose();
        _values.Dispose();
    }
}

public sealed class ChandeMomentumOscillatorAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _diffSum1;
    private readonly RollingWindowSum _diffSum2;
    private readonly RollingWindowSum _diffSum3;
    private readonly RollingWindowSum _absDiffSum1;
    private readonly RollingWindowSum _absDiffSum2;
    private readonly RollingWindowSum _absDiffSum3;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ChandeMomentumOscillatorAverageState(int length1 = 5, int length2 = 10, int length3 = 20,
        InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _diffSum1 = new RollingWindowSum(resolved1);
        _diffSum2 = new RollingWindowSum(resolved2);
        _diffSum3 = new RollingWindowSum(resolved3);
        _absDiffSum1 = new RollingWindowSum(resolved1);
        _absDiffSum2 = new RollingWindowSum(resolved2);
        _absDiffSum3 = new RollingWindowSum(resolved3);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeMomentumOscillatorAverageState(int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _diffSum1 = new RollingWindowSum(resolved1);
        _diffSum2 = new RollingWindowSum(resolved2);
        _diffSum3 = new RollingWindowSum(resolved3);
        _absDiffSum1 = new RollingWindowSum(resolved1);
        _absDiffSum2 = new RollingWindowSum(resolved2);
        _absDiffSum3 = new RollingWindowSum(resolved3);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorAverage;

    public void Reset()
    {
        _diffSum1.Reset();
        _diffSum2.Reset();
        _diffSum3.Reset();
        _absDiffSum1.Reset();
        _absDiffSum2.Reset();
        _absDiffSum3.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = value - prevValue;
        var absDiff = Math.Abs(diff);

        var diffSum1 = isFinal ? _diffSum1.Add(diff, out _) : _diffSum1.Preview(diff, out _);
        var absSum1 = isFinal ? _absDiffSum1.Add(absDiff, out _) : _absDiffSum1.Preview(absDiff, out _);
        var diffSum2 = isFinal ? _diffSum2.Add(diff, out _) : _diffSum2.Preview(diff, out _);
        var absSum2 = isFinal ? _absDiffSum2.Add(absDiff, out _) : _absDiffSum2.Preview(absDiff, out _);
        var diffSum3 = isFinal ? _diffSum3.Add(diff, out _) : _diffSum3.Preview(diff, out _);
        var absSum3 = isFinal ? _absDiffSum3.Add(absDiff, out _) : _absDiffSum3.Preview(absDiff, out _);

        var temp1 = absSum1 != 0 ? MathHelper.MinOrMax(diffSum1 / absSum1, 1, -1) : 0;
        var temp2 = absSum2 != 0 ? MathHelper.MinOrMax(diffSum2 / absSum2, 1, -1) : 0;
        var temp3 = absSum3 != 0 ? MathHelper.MinOrMax(diffSum3 / absSum3, 1, -1) : 0;
        var cmoAvg = 100 * ((temp1 + temp2 + temp3) / 3);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cmoa", cmoAvg }
            };
        }

        return new StreamingIndicatorStateResult(cmoAvg, outputs);
    }

    public void Dispose()
    {
        _diffSum1.Dispose();
        _diffSum2.Dispose();
        _diffSum3.Dispose();
        _absDiffSum1.Dispose();
        _absDiffSum2.Dispose();
        _absDiffSum3.Dispose();
    }
}

public sealed class ChandeMomentumOscillatorAbsoluteAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _diffSum1;
    private readonly RollingWindowSum _diffSum2;
    private readonly RollingWindowSum _diffSum3;
    private readonly RollingWindowSum _absDiffSum1;
    private readonly RollingWindowSum _absDiffSum2;
    private readonly RollingWindowSum _absDiffSum3;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ChandeMomentumOscillatorAbsoluteAverageState(int length1 = 5, int length2 = 10, int length3 = 20,
        InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _diffSum1 = new RollingWindowSum(resolved1);
        _diffSum2 = new RollingWindowSum(resolved2);
        _diffSum3 = new RollingWindowSum(resolved3);
        _absDiffSum1 = new RollingWindowSum(resolved1);
        _absDiffSum2 = new RollingWindowSum(resolved2);
        _absDiffSum3 = new RollingWindowSum(resolved3);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeMomentumOscillatorAbsoluteAverageState(int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _diffSum1 = new RollingWindowSum(resolved1);
        _diffSum2 = new RollingWindowSum(resolved2);
        _diffSum3 = new RollingWindowSum(resolved3);
        _absDiffSum1 = new RollingWindowSum(resolved1);
        _absDiffSum2 = new RollingWindowSum(resolved2);
        _absDiffSum3 = new RollingWindowSum(resolved3);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorAbsoluteAverage;

    public void Reset()
    {
        _diffSum1.Reset();
        _diffSum2.Reset();
        _diffSum3.Reset();
        _absDiffSum1.Reset();
        _absDiffSum2.Reset();
        _absDiffSum3.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = value - prevValue;
        var absDiff = Math.Abs(diff);

        var diffSum1 = isFinal ? _diffSum1.Add(diff, out _) : _diffSum1.Preview(diff, out _);
        var absSum1 = isFinal ? _absDiffSum1.Add(absDiff, out _) : _absDiffSum1.Preview(absDiff, out _);
        var diffSum2 = isFinal ? _diffSum2.Add(diff, out _) : _diffSum2.Preview(diff, out _);
        var absSum2 = isFinal ? _absDiffSum2.Add(absDiff, out _) : _absDiffSum2.Preview(absDiff, out _);
        var diffSum3 = isFinal ? _diffSum3.Add(diff, out _) : _diffSum3.Preview(diff, out _);
        var absSum3 = isFinal ? _absDiffSum3.Add(absDiff, out _) : _absDiffSum3.Preview(absDiff, out _);

        var temp1 = absSum1 != 0 ? MathHelper.MinOrMax(diffSum1 / absSum1, 1, -1) : 0;
        var temp2 = absSum2 != 0 ? MathHelper.MinOrMax(diffSum2 / absSum2, 1, -1) : 0;
        var temp3 = absSum3 != 0 ? MathHelper.MinOrMax(diffSum3 / absSum3, 1, -1) : 0;
        var cmoAbsAvg = Math.Abs(100 * ((temp1 + temp2 + temp3) / 3));

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cmoaa", cmoAbsAvg }
            };
        }

        return new StreamingIndicatorStateResult(cmoAbsAvg, outputs);
    }

    public void Dispose()
    {
        _diffSum1.Dispose();
        _diffSum2.Dispose();
        _diffSum3.Dispose();
        _absDiffSum1.Dispose();
        _absDiffSum2.Dispose();
        _absDiffSum3.Dispose();
    }
}

public sealed class ChandeMomentumOscillatorAverageDisparityIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ma1;
    private readonly IMovingAverageSmoother _ma2;
    private readonly IMovingAverageSmoother _ma3;
    private readonly StreamingInputResolver _input;

    public ChandeMomentumOscillatorAverageDisparityIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 200, int length2 = 50, int length3 = 20, InputName inputName = InputName.Close)
    {
        _ma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeMomentumOscillatorAverageDisparityIndexState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorAverageDisparityIndex;

    public void Reset()
    {
        _ma1.Reset();
        _ma2.Reset();
        _ma3.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma1 = _ma1.Next(value, isFinal);
        var ma2 = _ma2.Next(value, isFinal);
        var ma3 = _ma3.Next(value, isFinal);
        var first = value != 0 ? (value - ma1) / value * 100 : 0;
        var second = value != 0 ? (value - ma2) / value * 100 : 0;
        var third = value != 0 ? (value - ma3) / value * 100 : 0;
        var avg = (first + second + third) / 3;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cmoadi", avg }
            };
        }

        return new StreamingIndicatorStateResult(avg, outputs);
    }

    public void Dispose()
    {
        _ma1.Dispose();
        _ma2.Dispose();
        _ma3.Dispose();
    }
}

public sealed class ChandeMomentumOscillatorFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _filter;
    private readonly RollingWindowSum _diffSum;
    private readonly RollingWindowSum _absDiffSum;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ChandeMomentumOscillatorFilterState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 9, double filter = 3, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _filter = filter;
        _diffSum = new RollingWindowSum(resolved);
        _absDiffSum = new RollingWindowSum(resolved);
        _signal = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeMomentumOscillatorFilterState(MovingAvgType maType, int length, double filter,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _filter = filter;
        _diffSum = new RollingWindowSum(resolved);
        _absDiffSum = new RollingWindowSum(resolved);
        _signal = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorFilter;

    public void Reset()
    {
        _diffSum.Reset();
        _absDiffSum.Reset();
        _signal.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = _hasPrev ? value - prevValue : 0;
        var absDiff = Math.Abs(diff);
        if (absDiff > _filter)
        {
            diff = 0;
            absDiff = 0;
        }

        var diffSum = isFinal ? _diffSum.Add(diff, out _) : _diffSum.Preview(diff, out _);
        var absSum = isFinal ? _absDiffSum.Add(absDiff, out _) : _absDiffSum.Preview(absDiff, out _);
        var cmo = absSum != 0 ? MathHelper.MinOrMax(100 * diffSum / absSum, 100, -100) : 0;
        var signal = _signal.Next(cmo, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Cmof", cmo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(cmo, outputs);
    }

    public void Dispose()
    {
        _diffSum.Dispose();
        _absDiffSum.Dispose();
        _signal.Dispose();
    }
}

public sealed class ChandeQuickStickState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;

    public ChandeQuickStickState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.ChandeQuickStick;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var openClose = bar.Close - bar.Open;
        var cqs = _smoother.Next(openClose, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cqs", cqs }
            };
        }

        return new StreamingIndicatorStateResult(cqs, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class ChandeTrendScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly int _startLength;
    private readonly int _endLength;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public ChandeTrendScoreState(int startLength = 11, int endLength = 20, InputName inputName = InputName.Close)
    {
        _startLength = Math.Max(1, startLength);
        _endLength = Math.Max(_startLength, endLength);
        _values = new PooledRingBuffer<double>(_endLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ChandeTrendScoreState(int startLength, int endLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _startLength = Math.Max(1, startLength);
        _endLength = Math.Max(_startLength, endLength);
        _values = new PooledRingBuffer<double>(_endLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ChandeTrendScore;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double ts = 0;
        for (var j = _startLength; j <= _endLength; j++)
        {
            var prevValue = _values.Count >= j ? _values[_values.Count - j] : 0;
            ts += value >= prevValue ? 1 : -1;
        }

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cts", ts }
            };
        }

        return new StreamingIndicatorStateResult(ts, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class ChandeVolatilityIndexDynamicAverageIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _stdDevSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _alpha1;
    private readonly double _alpha2;
    private double _prevVidya1;
    private double _prevVidya2;
    private bool _hasPrev;

    public ChandeVolatilityIndexDynamicAverageIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, double alpha1 = 0.2, double alpha2 = 0.04, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, inputName);
        _stdDevSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _alpha1 = alpha1;
        _alpha2 = alpha2;
    }

    public ChandeVolatilityIndexDynamicAverageIndicatorState(MovingAvgType maType, int length, double alpha1, double alpha2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, selector);
        _stdDevSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _alpha1 = alpha1;
        _alpha2 = alpha2;
    }

    public IndicatorName Name => IndicatorName.ChandeVolatilityIndexDynamicAverageIndicator;

    public void Reset()
    {
        _stdDev.Reset();
        _stdDevSmoother.Reset();
        _prevVidya1 = 0;
        _prevVidya2 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var stdDevEma = _stdDevSmoother.Next(stdDev, isFinal);
        var ratio = stdDevEma != 0 ? stdDev / stdDevEma : 0;
        var prevVidya1 = _hasPrev ? _prevVidya1 : value;
        var prevVidya2 = _hasPrev ? _prevVidya2 : value;
        var vidya1 = (_alpha1 * ratio * value) + ((1 - (_alpha1 * ratio)) * prevVidya1);
        var vidya2 = (_alpha2 * ratio * value) + ((1 - (_alpha2 * ratio)) * prevVidya2);

        if (isFinal)
        {
            _prevVidya1 = vidya1;
            _prevVidya2 = vidya2;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Cvida1", vidya1 },
                { "Cvida2", vidya2 }
            };
        }

        return new StreamingIndicatorStateResult(vidya1, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _stdDevSmoother.Dispose();
    }
}

public sealed class CompoundRatioMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _sumWindow;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;

    public CompoundRatioMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sumWindow = new RollingWindowSum(_length);
        var smoothLength = Math.Max((int)Math.Round(Math.Sqrt(_length)), 1);
        _smoother = MovingAverageSmootherFactory.Create(maType, smoothLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public CompoundRatioMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sumWindow = new RollingWindowSum(_length);
        var smoothLength = Math.Max((int)Math.Round(Math.Sqrt(_length)), 1);
        _smoother = MovingAverageSmootherFactory.Create(maType, smoothLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.CompoundRatioMovingAverage;

    public void Reset()
    {
        _sumWindow.Reset();
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sum = isFinal ? _sumWindow.Add(value, out _) : _sumWindow.Preview(value, out _);
        var coraRaw = _length != 0 ? sum / _length : 0;
        var coraWave = _smoother.Next(coraRaw, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Crma", coraWave }
            };
        }

        return new StreamingIndicatorStateResult(coraWave, outputs);
    }

    public void Dispose()
    {
        _sumWindow.Dispose();
        _smoother.Dispose();
    }
}

public sealed class ConstanceBrownCompositeIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly RsiState _rsi1;
    private readonly RsiState _rsi2;
    private readonly IMovingAverageSmoother _rsiSmoother;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly PooledRingBuffer<double> _rsi1Window;
    private readonly StreamingInputResolver _input;

    public ConstanceBrownCompositeIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 13, int slowLength = 33, int length1 = 14, int length2 = 9, int smoothLength = 3,
        InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _rsi1 = new RsiState(MovingAvgType.WildersSmoothingMethod, resolvedLength1);
        _rsi2 = new RsiState(MovingAvgType.WildersSmoothingMethod, resolvedSmooth);
        _rsiSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _rsi1Window = new PooledRingBuffer<double>(_length2 + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ConstanceBrownCompositeIndexState(MovingAvgType maType, int fastLength, int slowLength, int length1,
        int length2, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _rsi1 = new RsiState(MovingAvgType.WildersSmoothingMethod, resolvedLength1);
        _rsi2 = new RsiState(MovingAvgType.WildersSmoothingMethod, resolvedSmooth);
        _rsiSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _rsi1Window = new PooledRingBuffer<double>(_length2 + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ConstanceBrownCompositeIndex;

    public void Reset()
    {
        _rsi1.Reset();
        _rsi2.Reset();
        _rsiSmoother.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _rsi1Window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi1 = _rsi1.Next(value, isFinal);
        var rsi2 = _rsi2.Next(rsi1, isFinal);
        var rsiSma = _rsiSmoother.Next(rsi2, isFinal);
        var rsiDelta = _rsi1Window.Count >= _length2 ? _rsi1Window[_rsi1Window.Count - _length2] : 0;
        var s = rsiDelta + rsiSma;
        var fast = _fastSmoother.Next(s, isFinal);
        var slow = _slowSmoother.Next(s, isFinal);

        if (isFinal)
        {
            _rsi1Window.TryAdd(rsi1, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Cbci", s },
                { "FastSignal", fast },
                { "SlowSignal", slow }
            };
        }

        return new StreamingIndicatorStateResult(s, outputs);
    }

    public void Dispose()
    {
        _rsi1.Dispose();
        _rsi2.Dispose();
        _rsiSmoother.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _rsi1Window.Dispose();
    }
}

public sealed class CorrectedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smaSmoother;
    private readonly IMovingAverageSmoother _varianceMeanSmoother;
    private readonly IMovingAverageSmoother _varianceSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevCma;
    private bool _hasPrev;

    public CorrectedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 35,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceMeanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public CorrectedMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceMeanSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _varianceSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.CorrectedMovingAverage;

    public void Reset()
    {
        _smaSmoother.Reset();
        _varianceMeanSmoother.Reset();
        _varianceSmoother.Reset();
        _prevCma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smaSmoother.Next(value, isFinal);
        var smaMean = _varianceMeanSmoother.Next(sma, isFinal);
        var deviation = sma - smaMean;
        var variance = _varianceSmoother.Next(deviation * deviation, isFinal);
        var prevCma = _hasPrev ? _prevCma : sma;
        var v2 = MathHelper.Pow(prevCma - sma, 2);
        var v3 = variance == 0 || v2 == 0 ? 1 : v2 / (variance + v2);

        var tolerance = MathHelper.Pow(10, -5);
        var err = 1d;
        var kPrev = 1d;
        var k = 1d;
        for (var j = 0; j <= 5000 && err > tolerance; j++)
        {
            k = v3 * kPrev * (2 - kPrev);
            err = kPrev - k;
            kPrev = k;
        }

        var cma = prevCma + (k * (sma - prevCma));

        if (isFinal)
        {
            _prevCma = cma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cma", cma }
            };
        }

        return new StreamingIndicatorStateResult(cma, outputs);
    }

    public void Dispose()
    {
        _smaSmoother.Dispose();
        _varianceMeanSmoother.Dispose();
        _varianceSmoother.Dispose();
    }
}

public sealed class CubedWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public CubedWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _weights = new double[resolved];
        double weightSum = 0;
        for (var i = 0; i < resolved; i++)
        {
            var weight = MathHelper.Pow(resolved - i, 3);
            _weights[i] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public CubedWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _weights = new double[resolved];
        double weightSum = 0;
        for (var i = 0; i < resolved; i++)
        {
            var weight = MathHelper.Pow(resolved - i, 3);
            _weights[i] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.CubedWeightedMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sum = value * _weights[0];
        for (var j = 1; j < _weights.Length; j++)
        {
            var prevValue = _values.Count >= j ? _values[_values.Count - j] : 0;
            sum += prevValue * _weights[j];
        }

        var cwma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cwma", cwma }
            };
        }

        return new StreamingIndicatorStateResult(cwma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

internal readonly struct RollingWindowSnapshot
{
    public RollingWindowSnapshot(double sum, double sumSquares, int count)      
    {
        Sum = sum;
        SumSquares = sumSquares;
        Count = count;
    }

    public double Sum { get; }
    public double SumSquares { get; }
    public int Count { get; }
}

internal readonly struct ProjectionBandsSnapshot
{
    public ProjectionBandsSnapshot(double upper, double middle, double lower)
    {
        Upper = upper;
        Middle = middle;
        Lower = lower;
    }

    public double Upper { get; }
    public double Middle { get; }
    public double Lower { get; }
}

internal sealed class ProjectionBandsCalculator : IDisposable
{
    private readonly int _length;
    private RollingSum _xSum;
    private RollingSum _x2Sum;
    private RollingSum _highSum;
    private RollingSum _highXYSum;
    private RollingSum _lowSum;
    private RollingSum _lowXYSum;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private readonly PooledRingBuffer<double> _highSlopes;
    private readonly PooledRingBuffer<double> _lowSlopes;
    private int _index;

    public ProjectionBandsCalculator(int length)
    {
        _length = Math.Max(1, length);
        _xSum = new RollingSum();
        _x2Sum = new RollingSum();
        _highSum = new RollingSum();
        _highXYSum = new RollingSum();
        _lowSum = new RollingSum();
        _lowXYSum = new RollingSum();
        _highs = new PooledRingBuffer<double>(_length);
        _lows = new PooledRingBuffer<double>(_length);
        _highSlopes = new PooledRingBuffer<double>(_length);
        _lowSlopes = new PooledRingBuffer<double>(_length);
    }

    public ProjectionBandsSnapshot Update(double high, double low, bool isFinal)
    {
        var currentIndex = _index;
        var pu = high;
        var pl = low;

        var highCount = _highs.Count;
        var lowCount = _lows.Count;
        var slopeCount = _highSlopes.Count;
        var highStartIndex = currentIndex - highCount;
        var lowStartIndex = currentIndex - lowCount;
        var slopeStartIndex = currentIndex - slopeCount;

        for (var j = 1; j <= _length; j++)
        {
            var slopeIndex = currentIndex - j;
            var highIndex = currentIndex - (j - 1);

            double highSlope = 0;
            double lowSlope = 0;
            if (slopeIndex >= slopeStartIndex && slopeIndex >= 0)
            {
                var slopeOffset = slopeIndex - slopeStartIndex;
                if (slopeOffset >= 0 && slopeOffset < slopeCount)
                {
                    highSlope = _highSlopes[slopeOffset];
                    lowSlope = _lowSlopes[slopeOffset];
                }
            }

            double pHigh;
            if (highIndex == currentIndex)
            {
                pHigh = high;
            }
            else
            {
                var highOffset = highIndex - highStartIndex;
                pHigh = highOffset >= 0 && highOffset < highCount ? _highs[highOffset] : 0;
            }

            double pLow;
            if (highIndex == currentIndex)
            {
                pLow = low;
            }
            else
            {
                var lowOffset = highIndex - lowStartIndex;
                pLow = lowOffset >= 0 && lowOffset < lowCount ? _lows[lowOffset] : 0;
            }

            var vHigh = pHigh + (highSlope * j);
            var vLow = pLow + (lowSlope * j);
            if (vHigh > pu)
            {
                pu = vHigh;
            }

            if (vLow < pl)
            {
                pl = vLow;
            }
        }

        var middle = (pu + pl) / 2;

        if (isFinal)
        {
            var x = (double)currentIndex;
            _xSum.Add(x);
            _x2Sum.Add(x * x);
            _highSum.Add(high);
            _highXYSum.Add(x * high);
            _lowSum.Add(low);
            _lowXYSum.Add(x * low);

            var sumX = _xSum.Sum(_length);
            var sumX2 = _x2Sum.Sum(_length);
            var sumHigh = _highSum.Sum(_length);
            var sumHighXY = _highXYSum.Sum(_length);
            var sumLow = _lowSum.Sum(_length);
            var sumLowXY = _lowXYSum.Sum(_length);
            var highSlope = CalculateSlope(sumX, sumHigh, sumHighXY, sumX2);
            var lowSlope = CalculateSlope(sumX, sumLow, sumLowXY, sumX2);

            _highSlopes.TryAdd(highSlope, out _);
            _lowSlopes.TryAdd(lowSlope, out _);
            _highs.TryAdd(high, out _);
            _lows.TryAdd(low, out _);
            _index++;
        }

        return new ProjectionBandsSnapshot(pu, middle, pl);
    }

    public void Reset()
    {
        _xSum = new RollingSum();
        _x2Sum = new RollingSum();
        _highSum = new RollingSum();
        _highXYSum = new RollingSum();
        _lowSum = new RollingSum();
        _lowXYSum = new RollingSum();
        _highs.Clear();
        _lows.Clear();
        _highSlopes.Clear();
        _lowSlopes.Clear();
        _index = 0;
    }

    public void Dispose()
    {
        _highs.Dispose();
        _lows.Dispose();
        _highSlopes.Dispose();
        _lowSlopes.Dispose();
    }

    private double CalculateSlope(double sumX, double sumY, double sumXY, double sumX2)
    {
        var top = (_length * sumXY) - (sumX * sumY);
        var bottom = (_length * sumX2) - (sumX * sumX);
        return bottom != 0 ? top / bottom : 0;
    }
}

internal sealed class RollingWindowSum : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private double _sum;

    public RollingWindowSum(int length)
    {
        _window = new PooledRingBuffer<double>(length);
    }

    public double Preview(double value, out int countAfter)
    {
        if (_window.Count < _window.Capacity)
        {
            countAfter = _window.Count + 1;
            return _sum + value;
        }

        countAfter = _window.Capacity;
        return _sum + value - _window[0];
    }

    public double Add(double value, out int countAfter)
    {
        if (_window.TryAdd(value, out var removed))
        {
            _sum += value - removed;
        }
        else
        {
            _sum += value;
        }

        countAfter = _window.Count;
        return _sum;
    }

    public void Reset()
    {
        _window.Clear();
        _sum = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class RollingCumulativeSum
{
    private readonly List<double> _cumulative = new();

    public double Preview(double value, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        var end = value + (_cumulative.Count > 0 ? _cumulative[_cumulative.Count - 1] : 0);
        var startIndex = _cumulative.Count - length;
        var start = startIndex >= 0 ? _cumulative[startIndex] : 0;
        return end - start;
    }

    public double Add(double value, int length)
    {
        var end = value + (_cumulative.Count > 0 ? _cumulative[_cumulative.Count - 1] : 0);
        _cumulative.Add(end);

        if (length <= 0)
        {
            return 0;
        }

        var startIndex = _cumulative.Count - length - 1;
        var start = startIndex >= 0 ? _cumulative[startIndex] : 0;
        return end - start;
    }

    public void Reset()
    {
        _cumulative.Clear();
    }
}

internal sealed class RollingWindowCorrelation : IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _xSum;
    private readonly RollingWindowSum _ySum;
    private readonly RollingWindowSum _x2Sum;
    private readonly RollingWindowSum _y2Sum;
    private readonly RollingWindowSum _xySum;

    public RollingWindowCorrelation(int length)
    {
        _length = Math.Max(1, length);
        _xSum = new RollingWindowSum(_length);
        _ySum = new RollingWindowSum(_length);
        _x2Sum = new RollingWindowSum(_length);
        _y2Sum = new RollingWindowSum(_length);
        _xySum = new RollingWindowSum(_length);
    }

    public double Preview(double x, double y, out int countAfter)
    {
        var sumX = _xSum.Preview(x, out countAfter);
        var sumY = _ySum.Preview(y, out _);
        var sumX2 = _x2Sum.Preview(x * x, out _);
        var sumY2 = _y2Sum.Preview(y * y, out _);
        var sumXY = _xySum.Preview(x * y, out _);
        return Calculate(sumX, sumY, sumX2, sumY2, sumXY, countAfter);
    }

    public double Add(double x, double y, out int countAfter)
    {
        var sumX = _xSum.Add(x, out countAfter);
        var sumY = _ySum.Add(y, out _);
        var sumX2 = _x2Sum.Add(x * x, out _);
        var sumY2 = _y2Sum.Add(y * y, out _);
        var sumXY = _xySum.Add(x * y, out _);
        return Calculate(sumX, sumY, sumX2, sumY2, sumXY, countAfter);
    }

    public void Reset()
    {
        _xSum.Reset();
        _ySum.Reset();
        _x2Sum.Reset();
        _y2Sum.Reset();
        _xySum.Reset();
    }

    private double Calculate(double sumX, double sumY, double sumX2, double sumY2, double sumXY, int n)
    {
        if (_length <= 1 || n <= 1)
        {
            return 0;
        }

        var numerator = (n * sumXY) - (sumX * sumY);
        var denomLeft = (n * sumX2) - (sumX * sumX);
        var denomRight = (n * sumY2) - (sumY * sumY);
        var denom = Math.Sqrt(denomLeft * denomRight);
        return denom != 0 ? numerator / denom : 0;
    }

    public void Dispose()
    {
        _xSum.Dispose();
        _ySum.Dispose();
        _x2Sum.Dispose();
        _y2Sum.Dispose();
        _xySum.Dispose();
    }
}

internal sealed class RollingWindowMax : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private readonly LinkedList<(double value, int index)> _deque = new();
    private int _index;

    public RollingWindowMax(int length)
    {
        _window = new PooledRingBuffer<double>(length);
    }

    public double Preview(double value, out int countAfter)
    {
        var capacity = _window.Capacity;
        var expireIndex = _index - capacity;
        countAfter = _window.Count < capacity ? _window.Count + 1 : capacity;

        var node = _deque.First;
        while (node != null && node.Value.index <= expireIndex)
        {
            node = node.Next;
        }

        var max = node != null ? node.Value.value : value;
        if (value > max)
        {
            max = value;
        }

        return max;
    }

    public double Add(double value, out int countAfter)
    {
        _window.TryAdd(value, out _);

        while (_deque.Last != null && _deque.Last.Value.value <= value)
        {
            _deque.RemoveLast();
        }

        _deque.AddLast((value, _index));

        var expireIndex = _index - _window.Capacity;
        while (_deque.First != null && _deque.First.Value.index <= expireIndex)
        {
            _deque.RemoveFirst();
        }

        _index++;
        countAfter = _window.Count;
        return _deque.First != null ? _deque.First.Value.value : value;
    }

    public void Reset()
    {
        _window.Clear();
        _deque.Clear();
        _index = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class RollingWindowMin : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private readonly LinkedList<(double value, int index)> _deque = new();
    private int _index;

    public RollingWindowMin(int length)
    {
        _window = new PooledRingBuffer<double>(length);
    }

    public double Preview(double value, out int countAfter)
    {
        var capacity = _window.Capacity;
        var expireIndex = _index - capacity;
        countAfter = _window.Count < capacity ? _window.Count + 1 : capacity;

        var node = _deque.First;
        while (node != null && node.Value.index <= expireIndex)
        {
            node = node.Next;
        }

        var min = node != null ? node.Value.value : value;
        if (value < min)
        {
            min = value;
        }

        return min;
    }

    public double Add(double value, out int countAfter)
    {
        _window.TryAdd(value, out _);

        while (_deque.Last != null && _deque.Last.Value.value >= value)
        {
            _deque.RemoveLast();
        }

        _deque.AddLast((value, _index));

        var expireIndex = _index - _window.Capacity;
        while (_deque.First != null && _deque.First.Value.index <= expireIndex)
        {
            _deque.RemoveFirst();
        }

        _index++;
        countAfter = _window.Count;
        return _deque.First != null ? _deque.First.Value.value : value;
    }

    public void Reset()
    {
        _window.Clear();
        _deque.Clear();
        _index = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class RollingWindowStats : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private double _sum;
    private double _sumSquares;

    public RollingWindowStats(int length)
    {
        _window = new PooledRingBuffer<double>(length);
    }

    public RollingWindowSnapshot Preview(double value)
    {
        var removed = _window.Count >= _window.Capacity ? _window[0] : 0;
        var sum = _sum + value - removed;
        var sumSquares = _sumSquares + (value * value) - (removed * removed);
        var count = _window.Count < _window.Capacity ? _window.Count + 1 : _window.Capacity;
        return new RollingWindowSnapshot(sum, sumSquares, count);
    }

    public RollingWindowSnapshot Add(double value)
    {
        if (_window.TryAdd(value, out var removed))
        {
            _sum += value - removed;
            _sumSquares += (value * value) - (removed * removed);
        }
        else
        {
            _sum += value;
            _sumSquares += value * value;
        }

        return new RollingWindowSnapshot(_sum, _sumSquares, _window.Count);
    }

    public void Reset()
    {
        _window.Clear();
        _sum = 0;
        _sumSquares = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class WmaState : IDisposable
{
    private readonly int _length;
    private readonly double _denominator;
    private readonly PooledRingBuffer<double> _window;
    private double _sum;
    private double _numerator;

    public WmaState(int length)
    {
        _length = Math.Max(1, length);
        _denominator = (double)_length * (_length + 1) / 2;
        _window = new PooledRingBuffer<double>(_length);
    }

    public double GetNext(double value, bool commit)
    {
        var numerator = _numerator + (_length * value) - _sum;
        if (commit)
        {
            _numerator = numerator;
            if (_window.TryAdd(value, out var removed))
            {
                _sum += value - removed;
            }
            else
            {
                _sum += value;
            }
        }

        return numerator / _denominator;
    }

    public void Reset()
    {
        _window.Clear();
        _sum = 0;
        _numerator = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class EmaState
{
    private readonly int _length;
    private readonly double _k;
    private int _count;
    private double _sum;
    private double _prevEma;

    public EmaState(int length)
    {
        _length = Math.Max(1, length);
        _k = Math.Min(Math.Max((double)2 / (_length + 1), 0.01), 0.99);
    }

    public double GetNext(double value, bool commit)
    {
        if (_count < _length)
        {
            var sum = _sum + value;
            var ema = sum / (_count + 1);
            if (commit)
            {
                _sum = sum;
                _count++;
                _prevEma = ema;
            }

            return ema;
        }

        var updated = (value * _k) + (_prevEma * (1 - _k));
        if (commit)
        {
            _prevEma = updated;
            _count++;
        }

        return updated;
    }

    public void Reset()
    {
        _count = 0;
        _sum = 0;
        _prevEma = 0;
    }
}

internal sealed class WilderState
{
    private readonly double _k;
    private double _prev;

    public WilderState(int length)
    {
        var resolved = Math.Max(1, length);
        _k = (double)1 / resolved;
    }

    public double GetNext(double value, bool commit)
    {
        var wwma = (value * _k) + (_prev * (1 - _k));
        if (commit)
        {
            _prev = wwma;
        }

        return wwma;
    }

    public void Reset()
    {
        _prev = 0;
    }
}

internal interface IMovingAverageSmoother : IDisposable
{
    double Next(double value, bool isFinal);
    void Reset();
}

internal sealed class SimpleMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly int _length;
    private readonly RollingWindowSum _window;

    public SimpleMovingAverageSmoother(int length)
    {
        _length = Math.Max(1, length);
        _window = new RollingWindowSum(_length);
    }

    public double Next(double value, bool isFinal)
    {
        int countAfter;
        var sum = isFinal ? _window.Add(value, out countAfter) : _window.Preview(value, out countAfter);
        return countAfter >= _length ? sum / _length : 0;
    }

    public void Reset()
    {
        _window.Reset();
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class ExactSimpleMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private double _sum;

    public ExactSimpleMovingAverageSmoother(int length)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
    }

    public double Next(double value, bool isFinal)
    {
        int countAfter;
        var sum = isFinal ? Add(value, out countAfter) : Preview(value, out countAfter);
        return countAfter >= _length ? sum / _length : 0;
    }

    private double Preview(double value, out int countAfter)
    {
        if (_window.Count < _window.Capacity)
        {
            countAfter = _window.Count + 1;
            return _sum + value;
        }

        countAfter = _window.Capacity;
        return _sum + value - _window[0];
    }

    private double Add(double value, out int countAfter)
    {
        if (_window.TryAdd(value, out var removed))
        {
            _sum += value;
            _sum -= removed;
        }
        else
        {
            _sum += value;
        }

        countAfter = _window.Count;
        return _sum;
    }

    public void Reset()
    {
        _window.Clear();
        _sum = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class ExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly EmaState _ema;

    public ExponentialMovingAverageSmoother(int length)
    {
        _ema = new EmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        return _ema.GetNext(value, isFinal);
    }

    public void Reset()
    {
        _ema.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class DoubleExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;

    public DoubleExponentialMovingAverageSmoother(int length)
    {
        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        var ema1 = _ema1.GetNext(value, isFinal);
        var ema2 = _ema2.GetNext(ema1, isFinal);
        return (2 * ema1) - ema2;
    }

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class TripleExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;
    private readonly EmaState _ema3;

    public TripleExponentialMovingAverageSmoother(int length)
    {
        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
        _ema3 = new EmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        var ema1 = _ema1.GetNext(value, isFinal);
        var ema2 = _ema2.GetNext(ema1, isFinal);
        var ema3 = _ema3.GetNext(ema2, isFinal);
        return (3 * ema1) - (3 * ema2) + ema3;
    }

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _ema3.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class ZeroLagTripleExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly TripleExponentialMovingAverageSmoother _tema1;
    private readonly TripleExponentialMovingAverageSmoother _tema2;

    public ZeroLagTripleExponentialMovingAverageSmoother(int length)
    {
        _tema1 = new TripleExponentialMovingAverageSmoother(length);
        _tema2 = new TripleExponentialMovingAverageSmoother(length);
    }

    public double Next(double value, bool isFinal)
    {
        var tema1 = _tema1.Next(value, isFinal);
        var tema2 = _tema2.Next(tema1, isFinal);
        return tema1 + (tema1 - tema2);
    }

    public void Reset()
    {
        _tema1.Reset();
        _tema2.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class WeightedMovingAverageSmoother : IMovingAverageSmoother    
{
    private readonly WmaState _wma;

    public WeightedMovingAverageSmoother(int length)
    {
        _wma = new WmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        return _wma.GetNext(value, isFinal);
    }

    public void Reset()
    {
        _wma.Reset();
    }

    public void Dispose()
    {
        _wma.Dispose();
    }
}

internal sealed class WilderMovingAverageSmoother : IMovingAverageSmoother      
{
    private readonly WilderState _wilder;

    public WilderMovingAverageSmoother(int length)
    {
        _wilder = new WilderState(length);
    }

    public double Next(double value, bool isFinal)
    {
        return _wilder.GetNext(value, isFinal);
    }

    public void Reset()
    {
        _wilder.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class EhlersHannMovingAverageSmoother : IMovingAverageSmoother, IDisposable
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;

    public EhlersHannMovingAverageSmoother(int length)
    {
        _length = Math.Max(1, length);
        _weights = new double[_length];
        double sum = 0;
        for (var j = 1; j <= _length; j++)
        {
            var weight = 1 - Math.Cos(2 * Math.PI * ((double)j / (_length + 1)));
            _weights[j - 1] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
    }

    public double Next(double value, bool isFinal)
    {
        var count = _values.Count;
        double sum = _weights[0] * value;

        for (var j = 2; j <= _length; j++)
        {
            var offset = j - 1;
            var prevValue = offset <= count ? _values[count - offset] : 0;
            sum += _weights[j - 1] * prevValue;
        }

        var result = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        return result;
    }

    public void Reset()
    {
        _values.Clear();
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

internal static class MovingAverageSmootherFactory
{
    public static IMovingAverageSmoother Create(MovingAvgType maType, int length)
    {
        return maType switch
        {
            MovingAvgType.SimpleMovingAverage => new SimpleMovingAverageSmoother(length),
            MovingAvgType.ExponentialMovingAverage => new ExponentialMovingAverageSmoother(length),
            MovingAvgType.DoubleExponentialMovingAverage => new DoubleExponentialMovingAverageSmoother(length),
            MovingAvgType.TripleExponentialMovingAverage => new TripleExponentialMovingAverageSmoother(length),
            MovingAvgType.ZeroLagTripleExponentialMovingAverage => new ZeroLagTripleExponentialMovingAverageSmoother(length),
            MovingAvgType.WeightedMovingAverage => new WeightedMovingAverageSmoother(length),
            MovingAvgType.WildersSmoothingMethod => new WilderMovingAverageSmoother(length),
            MovingAvgType.EhlersHannMovingAverage => new EhlersHannMovingAverageSmoother(length),
            MovingAvgType.EhlersHammingMovingAverage => new EhlersHammingMovingAverageSmoother(length),
            MovingAvgType.Ehlers2PoleSuperSmootherFilterV1 => new Ehlers2PoleSuperSmootherFilterV1Smoother(length),
            MovingAvgType.Ehlers2PoleSuperSmootherFilterV2 => new Ehlers2PoleSuperSmootherFilterV2Smoother(length),
            MovingAvgType.EhlersTriangleMovingAverage => new EhlersTriangleMovingAverageSmoother(length),
            MovingAvgType.EhlersModifiedOptimumEllipticFilter => new EhlersModifiedOptimumEllipticFilterSmoother(length),
            _ => throw new NotSupportedException($"MovingAvgType {maType} is not supported in streaming stateful indicators.")
        };
    }
}

internal sealed class EfficiencyRatioState : IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _volatilitySum;
    private readonly PooledRingBuffer<double> _values;
    private double _prevValue;
    private bool _hasPrev;

    public EfficiencyRatioState(int length)
    {
        _length = Math.Max(1, length);
        _volatilitySum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length + 1);
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _hasPrev ? _prevValue : 0;
        var volatility = _hasPrev ? Math.Abs(value - prevValue) : 0;
        var volatilitySum = isFinal ? _volatilitySum.Add(volatility, out _) : _volatilitySum.Preview(volatility, out _);
        var priorIndex = _values.Count - _length;
        var momentum = priorIndex >= 0 ? Math.Abs(value - _values[priorIndex]) : 0;
        var er = volatilitySum != 0 ? momentum / volatilitySum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        return er;
    }

    public void Reset()
    {
        _volatilitySum.Reset();
        _values.Clear();
        _prevValue = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _volatilitySum.Dispose();
        _values.Dispose();
    }
}

internal sealed class AdaptiveAutonomousRecursiveMovingAverageEngine : IDisposable
{
    private readonly double _gamma;
    private readonly EfficiencyRatioState _er;
    private double _ma1;
    private double _ma2;
    private double _absDiffSum;
    private int _index;
    private bool _hasPrev;

    public AdaptiveAutonomousRecursiveMovingAverageEngine(int length, double gamma)
    {
        _gamma = gamma;
        _er = new EfficiencyRatioState(length);
    }

    public double Next(double value, bool isFinal, out double d)
    {
        var er = _er.Next(value, isFinal);
        var prevMa2 = _hasPrev ? _ma2 : value;
        var prevMa1 = _hasPrev ? _ma1 : value;
        var absDiff = Math.Abs(value - prevMa2);
        var absDiffSum = _absDiffSum + absDiff;
        d = _index != 0 ? (absDiffSum / _index) * _gamma : 0;
        var c = value > prevMa2 + d ? value + d : value < prevMa2 - d ? value - d : prevMa2;
        var ma1 = (er * c) + ((1 - er) * prevMa1);
        var ma2 = (er * ma1) + ((1 - er) * prevMa2);

        if (isFinal)
        {
            _ma1 = ma1;
            _ma2 = ma2;
            _absDiffSum = absDiffSum;
            _index++;
            _hasPrev = true;
        }

        return ma2;
    }

    public void Reset()
    {
        _er.Reset();
        _ma1 = 0;
        _ma2 = 0;
        _absDiffSum = 0;
        _index = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}

internal sealed class RsiState : IDisposable
{
    private readonly IMovingAverageSmoother _avgGain;
    private readonly IMovingAverageSmoother _avgLoss;
    private double _prevValue;
    private bool _hasPrev;

    public RsiState(MovingAvgType maType, int length)
    {
        var resolved = Math.Max(1, length);
        _avgGain = MovingAverageSmootherFactory.Create(maType, resolved);
        _avgLoss = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _hasPrev ? _prevValue : 0;
        var priceChg = _hasPrev ? value - prevValue : 0;
        var gain = priceChg > 0 ? priceChg : 0;
        var loss = priceChg < 0 ? Math.Abs(priceChg) : 0;

        var avgGain = _avgGain.Next(gain, isFinal);
        var avgLoss = _avgLoss.Next(loss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        return rsi;
    }

    public void Reset()
    {
        _avgGain.Reset();
        _avgLoss.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _avgGain.Dispose();
        _avgLoss.Dispose();
    }
}

internal sealed class RollingPercentRank : IDisposable
{
    private readonly int _length;
    private readonly bool _useLinear;
    private readonly PooledRingBuffer<double> _window;
    private OrderStatisticTree? _tree;
    private bool _disposed;

    public RollingPercentRank(int length)
    {
        _length = Math.Max(1, length);
        _useLinear = _length <= RollingWindowSettings.SmallWindowThreshold;
        _window = new PooledRingBuffer<double>(_length);
        if (!_useLinear)
        {
            _tree = new OrderStatisticTree();
        }
    }

    public double Add(double value)
    {
        var count = AddAndCountLessThanOrEqual(value);
        return MathHelper.MinOrMax((double)count / _length * 100, 100, 0);
    }

    public double Preview(double value)
    {
        var count = CountLessThanOrEqual(value);
        return MathHelper.MinOrMax((double)count / _length * 100, 100, 0);
    }

    public void Reset()
    {
        _window.Clear();
        if (!_useLinear)
        {
            _tree = new OrderStatisticTree();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Dispose();
        _disposed = true;
    }

    private int AddAndCountLessThanOrEqual(double value)
    {
        if (_useLinear)
        {
            _window.TryAdd(value, out _);
            var count = 0;
            for (var i = 0; i < _window.Count; i++)
            {
                if (_window[i] <= value)
                {
                    count++;
                }
            }

            return Math.Max(0, count - 1);
        }

        if (_window.TryAdd(value, out var removed))
        {
            _tree!.Remove(removed);
        }

        _tree!.Insert(value);
        return Math.Max(0, _tree.CountLessThanOrEqual(value) - 1);
    }

    private int CountLessThanOrEqual(double value)
    {
        if (_useLinear)
        {
            var count = 0;
            for (var i = 0; i < _window.Count; i++)
            {
                if (_window[i] <= value)
                {
                    count++;
                }
            }

            return count;
        }

        return _tree!.CountLessThanOrEqual(value);
    }
}



