namespace OoplesFinance.StockIndicators.Streaming.Providers.Alpaca;

public sealed class AlpacaStreamSource : IStreamSource
{
    private readonly IAlpacaStreamClient _client;

    public AlpacaStreamSource(IAlpacaStreamClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public IStreamSubscription Subscribe(StreamSubscriptionRequest request, IStreamObserver observer)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (observer == null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        var subscription = new AlpacaStreamSubscription(_client, request, observer);
        subscription.Start();
        return subscription;
    }

    private sealed class AlpacaStreamSubscription : IStreamSubscription
    {
        private readonly IAlpacaStreamClient _client;
        private readonly StreamSubscriptionRequest _request;
        private readonly IStreamObserver _observer;
        private bool _started;

        public AlpacaStreamSubscription(IAlpacaStreamClient client, StreamSubscriptionRequest request, IStreamObserver observer)
        {
            _client = client;
            _request = request;
            _observer = observer;
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            if (_request.Trades)
            {
                _client.SubscribeTrades(_request.Symbols, _observer.OnTrade);
            }

            if (_request.Quotes)
            {
                _client.SubscribeQuotes(_request.Symbols, _observer.OnQuote);
            }

            if (_request.Bars)
            {
                var timeframes = _request.BarTimeframes.Count > 0
                    ? _request.BarTimeframes
                    : new[] { BarTimeframe.Minutes(1) };
                _client.SubscribeBars(_request.Symbols, timeframes, _observer.OnBar);
            }

            _client.Start();
            _started = true;
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _client.Stop();
            _started = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
