#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsGeneratedState(IIndicator indicator) =>
        indicator is HighestHigh { Length: > 0 } or LowestLow { Length: > 0 } or TrueRange or BalanceOfPower or Wma or WilliamsR or Ema or Adl
            or Wwma or Smma or ModifiedMa or Dema or Tema;

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
        Adl line => new AdlValueState(line.Length, count),
        _ => throw new InvalidOperationException("Unqualified generated state.")
    };

    private sealed class EmaValueState(int period) : IIndicatorState
    {
        private readonly Streaming.EmaState _state = new(period);
        public void Reset() => _state.Reset();
        public double Update(in Bar bar) => _state.GetNext(bar.Close, commit: true);
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

    private sealed class AdlValueState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly MoneyFlowAccumulationWindow _line = new();
        private readonly RocBankAverage _signal = new(MovingAvgType.ExponentialMovingAverage, period, count);
        public void Reset() { _line.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> output)
        {
            var value = _line.Next(bar.High, bar.Low, bar.Close, bar.Volume, commit: true);
            output[0] = value.Publish();
            output[1] = _signal.Next(value, true).Publish();
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
