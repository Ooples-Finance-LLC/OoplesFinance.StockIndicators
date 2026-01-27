using Alpaca.Markets;

namespace OoplesFinance.StockIndicators.Builder.Trading;

/// <summary>
/// Production-ready Alpaca broker implementation using the official Alpaca.Markets SDK.
/// Implements IBroker for full trading operations.
/// </summary>
public sealed class AlpacaBroker : IBroker, IDisposable
{
    private readonly IAlpacaTradingClient _tradingClient;
    private readonly AlpacaOptions _options;
    private readonly bool _isPaper;
    private bool _disposed;

    /// <summary>
    /// Creates a new Alpaca broker with the specified options.
    /// </summary>
    /// <param name="options">Alpaca API configuration options. If null, reads from environment variables.</param>
    /// <exception cref="InvalidOperationException">Thrown when API credentials are missing.</exception>
    public AlpacaBroker(AlpacaOptions? options = null)
    {
        _options = options ?? new AlpacaOptions();

        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("ALPACA_KEY");
        var apiSecret = _options.ApiSecret ?? Environment.GetEnvironmentVariable("ALPACA_SECRET");

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "Alpaca API key is required. Set AlpacaOptions.ApiKey or ALPACA_KEY environment variable.");
        }

        if (string.IsNullOrEmpty(apiSecret))
        {
            throw new InvalidOperationException(
                "Alpaca API secret is required. Set AlpacaOptions.ApiSecret or ALPACA_SECRET environment variable.");
        }

        _isPaper = _options.UsePaper ?? true;

        // Create the Alpaca trading client using the official SDK
        var environment = _isPaper
            ? Environments.Paper
            : Environments.Live;

        var secretKey = new SecretKey(apiKey, apiSecret);

        _tradingClient = environment.GetAlpacaTradingClient(secretKey);
    }

    /// <inheritdoc />
    public async Task<BrokerAccount> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var account = await _tradingClient.GetAccountAsync(cancellationToken).ConfigureAwait(false);

        var equity = account.Equity ?? 0m;
        var lastEquity = account.LastEquity;
        var dayPnL = equity - lastEquity;
        var dayPnLPercent = lastEquity != 0 ? (double)((dayPnL / lastEquity) * 100) : 0.0;

        return new BrokerAccount
        {
            AccountId = account.AccountId.ToString(),
            Equity = equity,
            Cash = account.TradableCash,
            BuyingPower = account.BuyingPower ?? 0m,
            PortfolioValue = (account.LongMarketValue ?? 0m) + (account.ShortMarketValue ?? 0m),
            DayPnL = dayPnL,
            DayPnLPercent = dayPnLPercent,
            TradingEnabled = account.IsTradingBlocked == false,
            IsPaper = _isPaper
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var positions = await _tradingClient.ListPositionsAsync(cancellationToken).ConfigureAwait(false);

        var result = new List<BrokerPosition>();
        foreach (var position in positions)
        {
            result.Add(new BrokerPosition
            {
                Symbol = position.Symbol,
                Quantity = position.Quantity,
                AverageEntryPrice = position.AverageEntryPrice,
                MarketValue = position.MarketValue ?? 0m,
                CurrentPrice = position.AssetCurrentPrice ?? 0m,
                UnrealizedPnL = position.UnrealizedProfitLoss ?? 0m,
                UnrealizedPnLPercent = (double)(position.UnrealizedProfitLossPercent ?? 0m),
                CostBasis = position.CostBasis
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> SubmitOrderAsync(ExtendedTradeRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(request.Symbol))
        {
            throw new ArgumentException("Symbol is required for order submission.", nameof(request));
        }

        // Determine order side
        var side = request.Action switch
        {
            TradeAction.MarketBuy => OrderSide.Buy,
            TradeAction.MarketSell => OrderSide.Sell,
            TradeAction.ClosePosition => OrderSide.Sell, // Assumes long position - would need position check for short
            _ => OrderSide.Buy
        };

        // Create the order based on order type
        IOrder order;

        switch (request.OrderType)
        {
            case Trading.OrderType.Market:
                order = await SubmitMarketOrderAsync(request.Symbol, request.Quantity, side, request.TimeInForce, cancellationToken).ConfigureAwait(false);
                break;

            case Trading.OrderType.Limit:
                if (!request.LimitPrice.HasValue)
                {
                    throw new ArgumentException("Limit price is required for limit orders.", nameof(request));
                }
                order = await SubmitLimitOrderAsync(request.Symbol, request.Quantity, side, request.LimitPrice.Value, request.TimeInForce, cancellationToken).ConfigureAwait(false);
                break;

            case Trading.OrderType.Stop:
                if (!request.StopPrice.HasValue)
                {
                    throw new ArgumentException("Stop price is required for stop orders.", nameof(request));
                }
                order = await SubmitStopOrderAsync(request.Symbol, request.Quantity, side, request.StopPrice.Value, request.TimeInForce, cancellationToken).ConfigureAwait(false);
                break;

            case Trading.OrderType.StopLimit:
                if (!request.StopPrice.HasValue || !request.LimitPrice.HasValue)
                {
                    throw new ArgumentException("Both stop price and limit price are required for stop-limit orders.", nameof(request));
                }
                order = await SubmitStopLimitOrderAsync(request.Symbol, request.Quantity, side, request.StopPrice.Value, request.LimitPrice.Value, request.TimeInForce, cancellationToken).ConfigureAwait(false);
                break;

            case Trading.OrderType.TrailingStop:
                if (!request.TrailingStopOffset.HasValue)
                {
                    throw new ArgumentException("Trailing stop offset is required for trailing stop orders.", nameof(request));
                }
                order = await SubmitTrailingStopOrderAsync(request.Symbol, request.Quantity, side, request.TrailingStopOffset.Value, request.TimeInForce, cancellationToken).ConfigureAwait(false);
                break;

            default:
                order = await SubmitMarketOrderAsync(request.Symbol, request.Quantity, side, request.TimeInForce, cancellationToken).ConfigureAwait(false);
                break;
        }

        return MapToBrokerOrder(order);
    }

    private async Task<IOrder> SubmitMarketOrderAsync(
        string symbol,
        decimal quantity,
        OrderSide side,
        Trading.TimeInForce timeInForce,
        CancellationToken cancellationToken)
    {
        var orderQuantity = OrderQuantity.Fractional(quantity);
        var alpacaTimeInForce = MapTimeInForce(timeInForce);

        return await _tradingClient.PostOrderAsync(
            side.Market(symbol, orderQuantity).WithDuration(alpacaTimeInForce),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IOrder> SubmitLimitOrderAsync(
        string symbol,
        decimal quantity,
        OrderSide side,
        decimal limitPrice,
        Trading.TimeInForce timeInForce,
        CancellationToken cancellationToken)
    {
        var orderQuantity = OrderQuantity.Fractional(quantity);
        var alpacaTimeInForce = MapTimeInForce(timeInForce);

        return await _tradingClient.PostOrderAsync(
            side.Limit(symbol, orderQuantity, limitPrice).WithDuration(alpacaTimeInForce),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IOrder> SubmitStopOrderAsync(
        string symbol,
        decimal quantity,
        OrderSide side,
        decimal stopPrice,
        Trading.TimeInForce timeInForce,
        CancellationToken cancellationToken)
    {
        var orderQuantity = OrderQuantity.Fractional(quantity);
        var alpacaTimeInForce = MapTimeInForce(timeInForce);

        return await _tradingClient.PostOrderAsync(
            side.Stop(symbol, orderQuantity, stopPrice).WithDuration(alpacaTimeInForce),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IOrder> SubmitStopLimitOrderAsync(
        string symbol,
        decimal quantity,
        OrderSide side,
        decimal stopPrice,
        decimal limitPrice,
        Trading.TimeInForce timeInForce,
        CancellationToken cancellationToken)
    {
        var orderQuantity = OrderQuantity.Fractional(quantity);
        var alpacaTimeInForce = MapTimeInForce(timeInForce);

        return await _tradingClient.PostOrderAsync(
            side.StopLimit(symbol, orderQuantity, stopPrice, limitPrice).WithDuration(alpacaTimeInForce),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IOrder> SubmitTrailingStopOrderAsync(
        string symbol,
        decimal quantity,
        OrderSide side,
        decimal trailOffsetPercent,
        Trading.TimeInForce timeInForce,
        CancellationToken cancellationToken)
    {
        var orderQuantity = OrderQuantity.Fractional(quantity);
        var alpacaTimeInForce = MapTimeInForce(timeInForce);

        // Use TrailOffset.InPercent for percentage-based trailing stop
        var trailOffset = TrailOffset.InPercent(trailOffsetPercent);

        return await _tradingClient.PostOrderAsync(
            side.TrailingStop(symbol, orderQuantity, trailOffset).WithDuration(alpacaTimeInForce),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!Guid.TryParse(orderId, out var orderGuid))
        {
            throw new ArgumentException("Invalid order ID format. Expected a GUID.", nameof(orderId));
        }

        return await _tradingClient.CancelOrderAsync(orderGuid, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!Guid.TryParse(orderId, out var orderGuid))
        {
            throw new ArgumentException("Invalid order ID format. Expected a GUID.", nameof(orderId));
        }

        var order = await _tradingClient.GetOrderAsync(orderGuid, cancellationToken).ConfigureAwait(false);
        return MapToBrokerOrder(order);
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> ClosePositionAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(symbol))
        {
            throw new ArgumentException("Symbol is required to close a position.", nameof(symbol));
        }

        var order = await _tradingClient.DeletePositionAsync(
            new DeletePositionRequest(symbol),
            cancellationToken).ConfigureAwait(false);

        return MapToBrokerOrder(order);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerOrder>> CloseAllPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var statuses = await _tradingClient.DeleteAllPositionsAsync(
            new DeleteAllPositionsRequest(),
            cancellationToken).ConfigureAwait(false);

        var result = new List<BrokerOrder>();
        foreach (var status in statuses)
        {
            // Check if the close was successful by examining the status
            if (status.IsSuccess)
            {
                // Get the order details for this position
                var positions = await _tradingClient.ListPositionsAsync(cancellationToken).ConfigureAwait(false);
                // The position should be closed, so we create a placeholder order result
                result.Add(new BrokerOrder
                {
                    OrderId = Guid.NewGuid().ToString(),
                    Symbol = status.Symbol,
                    Side = "sell",
                    OrderType = Trading.OrderType.Market,
                    Status = BrokerOrderStatus.Filled,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return result;
    }

    private static Alpaca.Markets.TimeInForce MapTimeInForce(Trading.TimeInForce timeInForce)
    {
        return timeInForce switch
        {
            Trading.TimeInForce.Day => Alpaca.Markets.TimeInForce.Day,
            Trading.TimeInForce.GTC => Alpaca.Markets.TimeInForce.Gtc,
            Trading.TimeInForce.IOC => Alpaca.Markets.TimeInForce.Ioc,
            Trading.TimeInForce.FOK => Alpaca.Markets.TimeInForce.Fok,
            Trading.TimeInForce.OPG => Alpaca.Markets.TimeInForce.Opg,
            Trading.TimeInForce.CLS => Alpaca.Markets.TimeInForce.Cls,
            _ => Alpaca.Markets.TimeInForce.Day
        };
    }

    private static BrokerOrder MapToBrokerOrder(IOrder order)
    {
        return new BrokerOrder
        {
            OrderId = order.OrderId.ToString(),
            ClientOrderId = order.ClientOrderId,
            Symbol = order.Symbol,
            Side = order.OrderSide == OrderSide.Buy ? "buy" : "sell",
            OrderType = MapOrderType(order.OrderType),
            Quantity = order.Quantity ?? 0m,
            FilledQuantity = order.FilledQuantity,
            AverageFillPrice = order.AverageFillPrice,
            LimitPrice = order.LimitPrice,
            StopPrice = order.StopPrice,
            Status = MapOrderStatus(order.OrderStatus),
            TimeInForce = MapTimeInForceFromAlpaca(order.TimeInForce),
            CreatedAt = order.CreatedAtUtc ?? DateTime.UtcNow,
            FilledAt = order.FilledAtUtc,
            CancelledAt = order.CancelledAtUtc
        };
    }

    private static Trading.OrderType MapOrderType(Alpaca.Markets.OrderType orderType)
    {
        return orderType switch
        {
            Alpaca.Markets.OrderType.Market => Trading.OrderType.Market,
            Alpaca.Markets.OrderType.Limit => Trading.OrderType.Limit,
            Alpaca.Markets.OrderType.Stop => Trading.OrderType.Stop,
            Alpaca.Markets.OrderType.StopLimit => Trading.OrderType.StopLimit,
            Alpaca.Markets.OrderType.TrailingStop => Trading.OrderType.TrailingStop,
            _ => Trading.OrderType.Market
        };
    }

    private static BrokerOrderStatus MapOrderStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.New => BrokerOrderStatus.New,
            OrderStatus.PartiallyFilled => BrokerOrderStatus.PartiallyFilled,
            OrderStatus.Filled => BrokerOrderStatus.Filled,
            OrderStatus.Canceled => BrokerOrderStatus.Cancelled,
            OrderStatus.Expired => BrokerOrderStatus.Expired,
            OrderStatus.Rejected => BrokerOrderStatus.Rejected,
            OrderStatus.PendingCancel => BrokerOrderStatus.PendingCancel,
            OrderStatus.PendingNew => BrokerOrderStatus.PendingNew,
            OrderStatus.Replaced => BrokerOrderStatus.Replaced,
            OrderStatus.PendingReplace => BrokerOrderStatus.PendingReplace,
            _ => BrokerOrderStatus.New
        };
    }

    private static Trading.TimeInForce MapTimeInForceFromAlpaca(Alpaca.Markets.TimeInForce timeInForce)
    {
        return timeInForce switch
        {
            Alpaca.Markets.TimeInForce.Day => Trading.TimeInForce.Day,
            Alpaca.Markets.TimeInForce.Gtc => Trading.TimeInForce.GTC,
            Alpaca.Markets.TimeInForce.Ioc => Trading.TimeInForce.IOC,
            Alpaca.Markets.TimeInForce.Fok => Trading.TimeInForce.FOK,
            Alpaca.Markets.TimeInForce.Opg => Trading.TimeInForce.OPG,
            Alpaca.Markets.TimeInForce.Cls => Trading.TimeInForce.CLS,
            _ => Trading.TimeInForce.Day
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AlpacaBroker));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _tradingClient.Dispose();
            _disposed = true;
        }
    }
}
