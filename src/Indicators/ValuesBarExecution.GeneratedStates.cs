#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsGeneratedState(IIndicator indicator) =>
        indicator is HighestHigh { Length: > 0 } or LowestLow { Length: > 0 } or TrueRange or BalanceOfPower or Wma or WilliamsR or Ema or Adl
            or Wwma or Smma or ModifiedMa or Dema or Tema or Zlema or Tma or Hma or Rsi or Atr or Roc or Momentum
            or Cci or Cmo or Trix or Apo or Ppo or Macd or Aroon or Dpo or Cmf or Mfi or ForceIndex or Obv
            or Nvi or Pvi or Vroc or Pvt or Adx or MassIndex or ChaikinOscillator or Emv or Vwap or AroonUp or AroonDown
            or Demarker or DeMarker or UlcerIndex or ChoppinessIndex;

    private static object CreateGeneratedState(IIndicator indicator, int count) => indicator switch
    {
        HighestHigh high when high.Length >= count => new PrefixPriceExtremeState(true),
        LowestLow low when low.Length >= count => new PrefixPriceExtremeState(false),
        HighestHigh high => new PriceExtremeState(high.Length, count, true),
        LowestLow low => new PriceExtremeState(low.Length, count, false),
        TrueRange => new TrueRangeValueState(),
        BalanceOfPower power => new BalanceOfPowerValueState(Math.Max(1, power.Length)),
        Wma average => new WmaValueState(Math.Max(1, average.Length), count),
        WilliamsR range => new WilliamsValueState(Math.Max(1, range.Length), count),
        Ema average => new EmaValueState(average.Length),
        Wwma average => new WilderValueState(average.Length),
        Smma average => new WilderValueState(average.Length),
        ModifiedMa average => new WilderValueState(average.Length),
        Dema average => new ExtrapolatedEmaValueState(average.Length, triple: false),
        Tema average => new ExtrapolatedEmaValueState(average.Length, triple: true),
        Zlema average => new ExtrapolatedEmaValueState(average.Length, triple: false),
        Tma average => new GeneratedTriangularState(average.Length),
        Hma average => new HullValueState(average.Length),
        Rsi strength => new GeneratedRsiState(strength.Length, count),
        Atr range => new GeneratedAtrState(range.Length, count),
        Roc change => new GeneratedRatioState(Math.Max(1, change.Length), count),
        Momentum momentum => new GeneratedMomentumState(Math.Max(1, momentum.Length), count),
        Cci commodity => new GeneratedCommodityState(commodity.Length, count),
        Cmo momentum => new GeneratedChandeState(momentum.Length, count),
        Trix trix => new GeneratedTrixState(trix.Length),
        Apo oscillator => new GeneratedEmaGapState(oscillator.FastLength, oscillator.SlowLength),
        Ppo oscillator => new GeneratedEmaGapSignalState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength, percentage: true),
        Macd oscillator => new GeneratedEmaGapSignalState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength),
        Aroon oscillator => new GeneratedAroonState(Math.Max(1, oscillator.Length)),
        Dpo oscillator => new GeneratedDetrendedState(Math.Max(1, oscillator.Length), count),
        Cmf flow => new GeneratedChaikinFlowState(flow.Length),
        Mfi flow => new GeneratedMoneyFlowState(flow.Length, count),
        ForceIndex force => new GeneratedForceState(force.Length),
        Obv volume => new GeneratedObvState(volume.Length),
        Nvi volume => new GeneratedVolumeIndexState(volume.Length, positive: false),
        Pvi volume => new GeneratedVolumeIndexState(volume.Length, positive: true),
        Vroc volume => new GeneratedVolumeRatioState(Math.Max(1, volume.Length), count),
        Pvt volume => new GeneratedVolumeTrendState(volume.Length),
        Adx direction => new GeneratedDirectionalState(direction.Length, count),
        MassIndex mass => new GeneratedMassState(mass.EmaLength, mass.SumLength, count),
        ChaikinOscillator flow => new GeneratedChaikinOscillatorState(flow.FastLength, flow.SlowLength),
        Emv => new GeneratedEaseState(),
        Vwap => new GeneratedVwapState(),
        AroonUp up => new GeneratedAroonLegState(Math.Max(1, up.Length), maximum: true),
        AroonDown down => new GeneratedAroonLegState(Math.Max(1, down.Length), maximum: false),
        Demarker marker => new GeneratedDemarkerState(marker.Length, count),
        DeMarker marker => new GeneratedDemarkerState(marker.Length, count),
        UlcerIndex drawdown => new GeneratedDrawdownState(drawdown.Length, count),
        ChoppinessIndex choppiness => new GeneratedChoppinessState(choppiness.Length),
        Adl line => new AdlValueState(line.Length, count),
        _ => throw new InvalidOperationException("Unqualified generated state.")
    };

    private sealed class EmaValueState(int period) : IIndicatorState
    {
        private readonly Streaming.EmaState _state = new(period);
        public void Reset() => _state.Reset();
        public double Update(in Bar bar) => _state.GetNext(bar.Close, commit: true);
    }

    private sealed class GeneratedRatioState(int period, int count, bool momentum = false) : IIndicatorState
    {
        private readonly double[] _lag = new double[Math.Min(period, Math.Max(1, count))];
        private int _position, _seen;
        public void Reset() { _position = _seen = 0; }
        public double Update(in Bar bar)
        {
            double prior = _seen >= period ? _lag[_position] : 0;
            double value = momentum ? RoundedMomentumRatio.Of(bar.Close, prior)
                : RoundedPercentageChange.Of(bar.Close, prior);
            _lag[_position] = bar.Close;
            if (++_position == _lag.Length) _position = 0;
            if (_seen < period) _seen++;
            return value;
        }
    }

    private sealed class GeneratedCommodityState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly CommodityIndexWindow _window = new(MovingAvgType.SimpleMovingAverage, period, .015, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close), true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAroonState(int period) : IMultiOutputState
    {
        private readonly WindowExtremeDeque _high = new(Math.Max(2, period), true);
        private readonly WindowExtremeDeque _low = new(Math.Max(2, period), false);
        private long _index;
        public void Reset() { _high.Reset(); _low.Reset(); _index = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            _high.Add(bar.Close); _low.Add(bar.Close);
            double up = (double)(period - (_index - _high.Index)) / period * 100;
            double down = (double)(period - (_index - _low.Index)) / period * 100;
            _index++;
            outputs[0] = up - down;
            outputs[1] = up;
            outputs[2] = down;
        }
    }

    private sealed class GeneratedChaikinFlowState(int period) : IIndicatorState, IDisposable
    {
        private readonly ChaikinFlowWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolumeIndexState(int period, bool positive) : IMultiOutputState, IDisposable
    {
        private readonly VolumeIndexWindow _window = new(MovingAvgType.ExponentialMovingAverage, period, positive, 1000);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDirectionalState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly DirectionalIndexWindow _window = new(MovingAvgType.WildersSmoothingMethod, Math.Max(1, period), Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Plus;
            outputs[1] = value.Minus;
            outputs[2] = value.Adx;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEaseState : IIndicatorState
    {
        private readonly EaseWindow _window = new(1000000);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Volume, true).Publish();
    }

    private sealed class GeneratedDemarkerState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly DemarkerWindow _window = new(MovingAvgType.SimpleMovingAverage, period, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDrawdownState(int period, int count) : IIndicatorState, IDisposable
    {
        // No window can evict an observation beyond this owned finite input.
        private readonly DrawdownWindow _window = new(Math.Min(Math.Max(1, period), Math.Max(1, count)));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChoppinessState(int period) : IIndicatorState
    {
        private readonly ChoppinessWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
    }

    private sealed class GeneratedVwapState : IIndicatorState
    {
        private ExactVolumeMean _mean;
        public void Reset() => _mean = default;
        public double Update(in Bar bar)
        {
            _mean.AddTypical(bar.High, bar.Low, bar.Close, bar.Volume);
            return _mean.Value();
        }
    }

    private sealed class GeneratedAroonLegState(int period, bool maximum) : IIndicatorState
    {
        // These facades include both endpoints and blank the first period bars.
        private readonly WindowExtremeDeque _window = new((long)period + 1, maximum);
        private long _index;
        public void Reset() { _window.Reset(); _index = 0; }
        public double Update(in Bar bar)
        {
            _window.Add(maximum ? bar.High : bar.Low);
            double value = _index < period ? 0 : 100.0 * (period - (_index - _window.Index)) / period;
            _index++;
            return value;
        }
    }

    private sealed class GeneratedMassState(int averagePeriod, int sumPeriod, int count) : IMultiOutputState, IDisposable
    {
        private readonly MassIndexWindow _window = new(MovingAvgType.ExponentialMovingAverage, averagePeriod, averagePeriod, sumPeriod, 9, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, true);
            outputs[0] = value.Value;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChaikinOscillatorState(int fastPeriod, int slowPeriod) : IIndicatorState, IDisposable
    {
        private readonly MoneyFlowAverageWindow _window = new(MovingAvgType.ExponentialMovingAverage, fastPeriod, slowPeriod);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true).Signal;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolumeRatioState(int period, int count) : IIndicatorState
    {
        private readonly GeneratedRatioState _ratio = new(period, count);
        public void Reset() => _ratio.Reset();
        public double Update(in Bar bar) => _ratio.Update(new Bar(default, 0, 0, 0, bar.Volume, 0));
    }

    private sealed class GeneratedVolumeTrendState(int period) : IMultiOutputState, IDisposable
    {
        private readonly PriceVolumeTrendWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMoneyFlowState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingMoneyFlowIndex _window = new(Math.Min(Math.Max(1, period), Math.Max(1, count)));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(
            Streaming.RollingMoneyFlowIndex.TypicalPrice(bar.High, bar.Low, bar.Close), bar.Volume, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedForceState(int period) : IIndicatorState, IDisposable
    {
        private readonly ForceWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedObvState(int period) : IMultiOutputState
    {
        private readonly Streaming.EmaState _signal = new(period);
        private ExactMeanAccumulator _sum;
        private double _previous;
        private bool _invalidSignal;
        public void Reset() { _signal.Reset(); _sum = default; _previous = 0; _invalidSignal = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (bar.Close > _previous) _sum.Add(bar.Volume);
            else if (bar.Close < _previous) _sum.Add(bar.Volume, -1);
            _previous = bar.Close;
            double value = _sum.Mean(1);
            _invalidSignal |= !double.IsFinite(value);
            outputs[0] = value;
            outputs[1] = _invalidSignal ? double.NaN : _signal.GetNext(value, commit: true);
        }
    }

    private sealed class GeneratedDetrendedState : IIndicatorState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother _mean;
        private readonly double[] _lag;
        private readonly int _lookback;
        private int _position, _seen;
        internal GeneratedDetrendedState(int period, int count)
        {
            _lookback = MathHelper.MinOrMax((int)Math.Ceiling(period / 2.0 + 1));
            _lag = new double[Math.Min(_lookback, Math.Max(1, count))];
            _mean = new(period);
        }
        public void Reset() { _mean.Reset(); _position = _seen = 0; }
        public double Update(in Bar bar)
        {
            double previous = _seen >= _lookback ? _lag[_position] : 0;
            _lag[_position] = bar.Close;
            if (++_position == _lag.Length) _position = 0;
            if (_seen < _lookback) _seen++;
            return previous - _mean.Next(bar.Close, true);
        }
        public void Dispose() => _mean.Dispose();
    }

    private sealed class GeneratedEmaGapState(int fastPeriod, int slowPeriod, bool percentage = false) : IIndicatorState
    {
        private readonly Streaming.EmaState _fast = new(fastPeriod);
        private readonly Streaming.EmaState _slow = new(slowPeriod);
        public void Reset() { _fast.Reset(); _slow.Reset(); }
        public double Update(in Bar bar)
        {
            double fast = _fast.GetNext(bar.Close, commit: true);
            double slow = _slow.GetNext(bar.Close, commit: true);
            return percentage ? RoundedPercentageChange.Of(fast, slow) : fast - slow;
        }
    }

    private sealed class GeneratedEmaGapSignalState(int fastPeriod, int slowPeriod, int signalPeriod, bool percentage = false) : IMultiOutputState
    {
        private readonly GeneratedEmaGapState _line = new(fastPeriod, slowPeriod, percentage);
        private readonly Streaming.EmaState _signal = new(signalPeriod);
        private bool _invalidSignal;
        public void Reset() { _line.Reset(); _signal.Reset(); _invalidSignal = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _line.Update(bar);
            _invalidSignal |= double.IsInfinity(value);
            double signal = _invalidSignal ? double.NaN : _signal.GetNext(value, commit: true);
            outputs[0] = value;
            outputs[1] = signal;
            outputs[2] = value - signal;
        }
    }

    private sealed class GeneratedChandeState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly ChandeMomentumWindow _window = new(Math.Min(Math.Max(1, period), Math.Max(1, count)));
        private readonly Streaming.EmaState _signal = new(3);
        public void Reset() { _window.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _window.Next(bar.Close, true);
            outputs[0] = value;
            outputs[1] = _signal.GetNext(value, commit: true);
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTrixState(int period) : IIndicatorState, IDisposable
    {
        private readonly TrixWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Publish();
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMomentumState(int period, int count) : IMultiOutputState
    {
        private readonly GeneratedRatioState _ratio = new(period, count, momentum: true);
        private readonly WmaValueState _signal = new(period, count);
        private bool _invalidSignal;
        public void Reset() { _ratio.Reset(); _signal.Reset(); _invalidSignal = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _ratio.Update(bar);
            _invalidSignal |= !double.IsFinite(value);
            outputs[0] = value;
            // FiniteSignalInput invalidates the entire dependent suffix.
            outputs[1] = _invalidSignal ? double.NaN
                : _signal.Update(new Bar(default, 0, 0, 0, value, 0));
        }
    }

    private sealed class GeneratedRsiState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly PriceRsiWindow _line = new(MovingAvgType.WildersSmoothingMethod, period, count);
        private readonly StrengthAverage _signal = new(MovingAvgType.WildersSmoothingMethod, 3, count);
        public void Reset() { _line.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _line.Next(bar.Close, true);
            double signal = _signal.Next(new StrengthValue(value), true).Mantissa;
            outputs[0] = value;
            outputs[1] = signal;
            outputs[2] = value - signal;
        }
        public void Dispose() { _line.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedAtrState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly Helpers.KeltnerWindow _window = new(MovingAvgType.WildersSmoothingMethod, 1, period,
            MovingAvgType.WildersSmoothingMethod, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Atr.Publish();
        public void Dispose() => _window.Dispose();
    }

    private sealed class WilderValueState(int period) : IIndicatorState
    {
        private readonly Streaming.WilderState _state = new(period);
        public void Reset() => _state.Reset();
        public double Update(in Bar bar) => _state.GetNext(bar.Close, commit: true);
    }

    private sealed class ExtrapolatedEmaValueState(int period, bool triple) : IIndicatorState
    {
        private readonly Streaming.EmaState _first = new(period);
        private readonly Streaming.EmaState _second = new(period);
        private readonly Streaming.EmaState? _third = triple ? new(period) : null;
        public void Reset() { _first.Reset(); _second.Reset(); _third?.Reset(); }
        public double Update(in Bar bar)
        {
            double first = _first.GetNext(bar.Close, commit: true);
            double second = _second.GetNext(first, commit: true);
            return _third is null ? ExponentialExtrapolation.Double(first, second)
                : ExponentialExtrapolation.Triple(first, second, _third.GetNext(second, commit: true));
        }
    }

    private sealed class GeneratedTriangularState : IIndicatorState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother _first;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother _second;
        internal GeneratedTriangularState(int period)
        {
            _first = new(period);
            try { _second = new(period); }
            catch { _first.Dispose(); throw; }
        }
        public void Reset() { _first.Reset(); _second.Reset(); }
        public double Update(in Bar bar) => _second.Next(_first.Next(bar.Close, true), true);
        public void Dispose() { _first.Dispose(); _second.Dispose(); }
    }

    private sealed class HullValueState(int period) : IIndicatorState, IDisposable
    {
        private readonly Helpers.HullWindow _window = new(MovingAvgType.WeightedMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class AdlValueState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly MoneyFlowAccumulationWindow _line = new();
        private readonly RocBankAverage _signal = new(MovingAvgType.ExponentialMovingAverage, period, count);
        public void Reset() { _line.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _line.Next(bar.High, bar.Low, bar.Close, bar.Volume, commit: true);
            outputs[0] = value.Publish();
            outputs[1] = _signal.Next(value, true).Publish();
        }
        public void Dispose() => _signal.Dispose();
    }

    private sealed class PrefixPriceExtremeState(bool maximum) : IIndicatorState
    {
        private bool _started;
        private double _best;
        public void Reset() { _started = false; _best = 0; }
        public double Update(in Bar bar)
        {
            double value = maximum ? bar.High : bar.Low;
            if (!_started || (maximum ? value > _best : value < _best)) _best = value;
            _started = true;
            return _best;
        }
    }

    private sealed class PriceExtremeState(int period, int count, bool maximum) : IIndicatorState
    {
        // Earliest equal candidates must survive, including opposite signed zeros,
        // matching TrendCore's strict comparison in an oldest-to-newest scan.
        private readonly ExtremeDeque _window = new(Math.Min(period, Math.Max(1, count)), maximum);
        private long _index;
        public void Reset() { _window.Reset(); _index = 0; }
        public double Update(in Bar bar)
        {
            _window.Add(_index, maximum ? bar.High : bar.Low, _index - period + 1);
            _index++;
            return _window.Value;
        }
    }

    private sealed class TrueRangeValueState : IIndicatorState
    {
        private bool _started;
        private double _previousClose;
        public void Reset() { _started = false; _previousClose = 0; }
        public double Update(in Bar bar)
        {
            // Keep VolatilityCore.TrueRange's first-bar rule and operation order.
            var value = _started
                ? Math.Max(bar.High - bar.Low, Math.Max(Math.Abs(bar.High - _previousClose), Math.Abs(bar.Low - _previousClose)))
                : bar.High - bar.Low;
            _started = true;
            _previousClose = bar.Close;
            return value;
        }
    }
}
#endif
