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

internal static class MovingAverageSmootherFactory
{
    public static IMovingAverageSmoother Create(MovingAvgType maType, int length)
    {
        return maType switch
        {
            MovingAvgType.SimpleMovingAverage => new SimpleMovingAverageSmoother(length),
            MovingAvgType.ExponentialMovingAverage => new ExponentialMovingAverageSmoother(length),
            MovingAvgType.WeightedMovingAverage => new WeightedMovingAverageSmoother(length),
            MovingAvgType.WildersSmoothingMethod => new WilderMovingAverageSmoother(length),
            _ => throw new NotSupportedException($"MovingAvgType {maType} is not supported in streaming stateful indicators.")
        };
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



