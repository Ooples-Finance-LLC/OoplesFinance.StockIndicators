namespace OoplesFinance.StockIndicators.Streaming;

public enum OutOfOrderPolicy
{
    Drop,
    BufferWithinWindow
}

public sealed class BarAggregatorOptions
{
    public BarAggregatorOptions(string symbol, BarTimeframe timeframe)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        Symbol = symbol;
        Timeframe = timeframe ?? throw new ArgumentNullException(nameof(timeframe));
    }

    public string Symbol { get; }
    public BarTimeframe Timeframe { get; }
    public bool EmitUpdates { get; set; } = true;
    public QuotePriceMode QuotePriceMode { get; set; } = QuotePriceMode.Mid;
    public OutOfOrderPolicy OutOfOrderPolicy { get; set; } = OutOfOrderPolicy.Drop;
    public TimeSpan ReorderWindow { get; set; } = TimeSpan.Zero;
    public int MaxBufferSize { get; set; } = 1024;
}

public sealed class BarAggregator
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly BarAggregatorOptions _options;
    private readonly bool _emitUpdates;
    private readonly bool _isTick;
    private readonly TimeSpan _timeSpan;
    private readonly SampleBuffer? _buffer;
    private BarBuilder? _current;
    private bool _hasCurrent;
    private DateTime _currentStart;
    private DateTime _currentEnd;

    public BarAggregator(BarAggregatorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _emitUpdates = options.EmitUpdates;
        _isTick = options.Timeframe.IsTick;
        _timeSpan = options.Timeframe.ToTimeSpan();

        if (options.OutOfOrderPolicy == OutOfOrderPolicy.BufferWithinWindow)
        {
            var window = options.ReorderWindow;
            if (window <= TimeSpan.Zero)
            {
                window = _timeSpan == TimeSpan.Zero ? TimeSpan.FromSeconds(1) : _timeSpan;
            }

            var maxBuffer = Math.Max(1, options.MaxBufferSize);
            _buffer = new SampleBuffer(window, maxBuffer);
        }
    }

    public event Action<OhlcvBar>? BarUpdated;
    public event Action<OhlcvBar>? BarClosed;

    public void AddTrade(StreamTrade trade)
    {
        if (trade == null || !IsSymbolMatch(trade.Symbol))
        {
            return;
        }

        AddSample(new BarSample(trade.Symbol, trade.Timestamp, trade.Price, trade.Size));
    }

    public void AddQuote(StreamQuote quote)
    {
        if (quote == null || !IsSymbolMatch(quote.Symbol))
        {
            return;
        }

        var price = GetQuotePrice(quote);
        var size = (quote.BidSize + quote.AskSize) / 2d;
        AddSample(new BarSample(quote.Symbol, quote.Timestamp, price, size));
    }

    public void AddSample(string symbol, DateTime timestamp, double price, double volume)
    {
        if (!IsSymbolMatch(symbol))
        {
            return;
        }

        AddSample(new BarSample(symbol, timestamp, price, volume));
    }

    public void Complete()
    {
        if (_buffer != null)
        {
            _buffer.Drain(ProcessSample);
        }

        CloseCurrentBar();
    }

    private void AddSample(BarSample sample)
    {
        if (_isTick)
        {
            EmitTick(sample);
            return;
        }

        if (_buffer != null)
        {
            _buffer.Add(sample);
            var flushBefore = _buffer.MaxTimestamp - _buffer.Window;
            while (_buffer.TryDequeue(flushBefore, out var ready))
            {
                ProcessSample(ready);
            }

            return;
        }

        ProcessSample(sample);
    }

    private void ProcessSample(BarSample sample)
    {
        var start = GetBarStart(sample.Timestamp);

        if (!_hasCurrent)
        {
            StartNewBar(sample, start);
            EmitUpdate();
            return;
        }

        if (sample.Timestamp < _currentStart)
        {
            return;
        }

        if (sample.Timestamp >= _currentEnd)
        {
            CloseCurrentBar();
            StartNewBar(sample, start);
            EmitUpdate();
            return;
        }

        _current!.Update(sample);
        EmitUpdate();
    }

    private void StartNewBar(BarSample sample, DateTime start)
    {
        _current = new BarBuilder();
        _currentStart = start;
        _currentEnd = start + _timeSpan;
        _current!.Update(sample);
        _hasCurrent = true;
    }

    private void EmitTick(BarSample sample)
    {
        var bar = new OhlcvBar(sample.Symbol, _options.Timeframe, sample.Timestamp, sample.Timestamp,
            sample.Price, sample.Price, sample.Price, sample.Price, sample.Volume, true);

        if (_emitUpdates)
        {
            BarUpdated?.Invoke(bar);
        }

        BarClosed?.Invoke(bar);
    }

    private void EmitUpdate()
    {
        if (!_emitUpdates || !_hasCurrent)
        {
            return;
        }

        BarUpdated?.Invoke(BuildBar(isFinal: false));
    }

    private void CloseCurrentBar()
    {
        if (!_hasCurrent)
        {
            return;
        }

        BarClosed?.Invoke(BuildBar(isFinal: true));
        _hasCurrent = false;
    }

    private OhlcvBar BuildBar(bool isFinal)
    {
        return new OhlcvBar(_options.Symbol, _options.Timeframe, _currentStart, _currentEnd,
            _current!.Open, _current.High, _current.Low, _current.Close, _current.Volume, isFinal);
    }

    private bool IsSymbolMatch(string symbol)
    {
        return string.Equals(_options.Symbol, symbol, StringComparison.OrdinalIgnoreCase);
    }

    private double GetQuotePrice(StreamQuote quote)
    {
        return _options.QuotePriceMode switch
        {
            QuotePriceMode.Bid => quote.BidPrice,
            QuotePriceMode.Ask => quote.AskPrice,
            _ => (quote.BidPrice + quote.AskPrice) / 2d
        };
    }

    private DateTime GetBarStart(DateTime timestamp)
    {
        if (_isTick)
        {
            return timestamp;
        }

        var utc = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
        if (_timeSpan == TimeSpan.Zero)
        {
            return utc;
        }

        var unitSeconds = _timeSpan.TotalSeconds;
        if (unitSeconds <= 0)
        {
            return utc;
        }

        var totalSeconds = (long)(utc - UnixEpoch).TotalSeconds;
        var bucket = (long)(totalSeconds / unitSeconds) * (long)unitSeconds;
        return UnixEpoch.AddSeconds(bucket);
    }

    private readonly struct BarSample
    {
        public BarSample(string symbol, DateTime timestamp, double price, double volume)
        {
            Symbol = symbol;
            Timestamp = timestamp;
            Price = price;
            Volume = volume;
        }

        public string Symbol { get; }
        public DateTime Timestamp { get; }
        public double Price { get; }
        public double Volume { get; }
    }

    private sealed class BarBuilder
    {
        public bool HasValue { get; private set; }
        public double Open { get; private set; }
        public double High { get; private set; }
        public double Low { get; private set; }
        public double Close { get; private set; }
        public double Volume { get; private set; }

        public void Update(BarSample sample)
        {
            if (!HasValue)
            {
                Open = sample.Price;
                High = sample.Price;
                Low = sample.Price;
                Close = sample.Price;
                Volume = sample.Volume;
                HasValue = true;
                return;
            }

            if (sample.Price > High)
            {
                High = sample.Price;
            }

            if (sample.Price < Low)
            {
                Low = sample.Price;
            }

            Close = sample.Price;
            Volume += sample.Volume;
        }
    }

    private sealed class SampleBuffer
    {
        private readonly List<BarSample> _samples;
        private readonly int _maxSize;

        public SampleBuffer(TimeSpan window, int maxSize)
        {
            Window = window;
            _maxSize = Math.Max(1, maxSize);
            _samples = new List<BarSample>(_maxSize);
        }

        public TimeSpan Window { get; }
        public DateTime MaxTimestamp { get; private set; }

        public void Add(BarSample sample)
        {
            if (_samples.Count == 0 || sample.Timestamp >= MaxTimestamp)
            {
                MaxTimestamp = sample.Timestamp;
            }

            var index = FindInsertIndex(sample.Timestamp);
            _samples.Insert(index, sample);
            if (_samples.Count > _maxSize)
            {
                _samples.RemoveAt(0);
            }
        }

        public bool TryDequeue(DateTime flushBefore, out BarSample sample)
        {
            if (_samples.Count == 0 || _samples[0].Timestamp > flushBefore)
            {
                sample = default;
                return false;
            }

            sample = _samples[0];
            _samples.RemoveAt(0);
            return true;
        }

        public void Drain(Action<BarSample> sink)
        {
            for (var i = 0; i < _samples.Count; i++)
            {
                sink(_samples[i]);
            }

            _samples.Clear();
        }

        private int FindInsertIndex(DateTime timestamp)
        {
            var lo = 0;
            var hi = _samples.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (timestamp < _samples[mid].Timestamp)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return lo;
        }
    }
}
