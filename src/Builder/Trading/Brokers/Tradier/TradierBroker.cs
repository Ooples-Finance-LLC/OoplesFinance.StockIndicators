using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OoplesFinance.StockIndicators.Builder.Trading.Brokers.Tradier;

/// <summary>
/// Tradier broker implementation.
/// Provides trading capabilities through the Tradier REST API.
/// </summary>
/// <remarks>
/// Tradier offers commission-free trading with a robust API.
/// Supports stocks, options, and ETFs.
/// </remarks>
public sealed class TradierBroker : IBroker, IDisposable
{
    private readonly TradierOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    private const string SandboxBaseUrl = "https://sandbox.tradier.com/v1";
    private const string ProductionBaseUrl = "https://api.tradier.com/v1";

    /// <summary>
    /// Creates a new Tradier broker instance.
    /// </summary>
    /// <param name="options">Tradier API options.</param>
    public TradierBroker(TradierOptions? options = null)
    {
        _options = options ?? new TradierOptions();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.UseSandbox ? SandboxBaseUrl : ProductionBaseUrl)
        };

        var accessToken = _options.AccessToken ?? Environment.GetEnvironmentVariable("TRADIER_ACCESS_TOKEN");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException(
                "Tradier access token is required. Set TradierOptions.AccessToken or TRADIER_ACCESS_TOKEN environment variable.");
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _jsonOptions = new JsonSerializerOptions
        {
#if NET8_0_OR_GREATER
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
#endif
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<BrokerAccount> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var response = await _httpClient.GetAsync(
            $"/accounts/{accountId}/balances",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TradierBalancesResponse>(content, _jsonOptions);

        var balances = result?.Balances;
        if (balances is null)
        {
            throw new InvalidOperationException("Failed to parse Tradier balances response");
        }

        return new BrokerAccount
        {
            AccountId = accountId,
            Equity = balances.TotalEquity,
            Cash = balances.TotalCash,
            BuyingPower = balances.StockBuyingPower ?? balances.TotalCash,
            IsPaper = _options.UseSandbox
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var response = await _httpClient.GetAsync(
            $"/accounts/{accountId}/positions",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TradierPositionsResponse>(content, _jsonOptions);

        if (result?.Positions?.Position is null)
        {
            return Array.Empty<BrokerPosition>();
        }

        return result.Positions.Position.Select(p => new BrokerPosition
        {
            Symbol = p.Symbol,
            Quantity = p.Quantity,
            AverageEntryPrice = p.CostBasis / p.Quantity,
            CurrentPrice = p.CostBasis / p.Quantity, // Would need separate market data call
            UnrealizedPnL = 0m, // Would need current price
            CostBasis = p.CostBasis
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> SubmitOrderAsync(ExtendedTradeRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var orderData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("class", "equity"),
            new KeyValuePair<string, string>("symbol", request.Symbol),
            new KeyValuePair<string, string>("side", MapOrderSide(request.Action)),
            new KeyValuePair<string, string>("quantity", request.Quantity.ToString()),
            new KeyValuePair<string, string>("type", MapOrderType(request.OrderType)),
            new KeyValuePair<string, string>("duration", MapTimeInForce(request.TimeInForce)),
            new KeyValuePair<string, string>("price", (request.LimitPrice ?? 0).ToString()),
            new KeyValuePair<string, string>("stop", (request.StopPrice ?? 0).ToString())
        });

        var response = await _httpClient.PostAsync(
            $"/accounts/{accountId}/orders",
            orderData,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TradierOrderResponse>(content, _jsonOptions);

        if (result?.Order is null)
        {
            throw new InvalidOperationException("Failed to parse Tradier order response");
        }

        return new BrokerOrder
        {
            OrderId = result.Order.Id.ToString(),
            Symbol = request.Symbol,
            Quantity = request.Quantity,
            Side = request.Action == TradeAction.MarketBuy ? "buy" : "sell",
            OrderType = request.OrderType,
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice,
            Status = MapOrderStatus(result.Order.Status),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var response = await _httpClient.DeleteAsync(
            $"/accounts/{accountId}/orders/{orderId}",
            cancellationToken).ConfigureAwait(false);

        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var response = await _httpClient.GetAsync(
            $"/accounts/{accountId}/orders/{orderId}",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TradierOrderDetailResponse>(content, _jsonOptions);

        if (result?.Order is null)
        {
            throw new InvalidOperationException($"Order not found: {orderId}");
        }

        var order = result.Order;
        return new BrokerOrder
        {
            OrderId = order.Id.ToString(),
            Symbol = order.Symbol,
            Quantity = order.Quantity,
            FilledQuantity = order.ExecQuantity,
            Side = order.Side,
            OrderType = MapOrderTypeFromString(order.Type),
            LimitPrice = order.Price,
            StopPrice = order.StopPrice,
            Status = MapOrderStatus(order.Status),
            AverageFillPrice = order.AvgFillPrice,
            CreatedAt = order.CreateDate
        };
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> ClosePositionAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var positions = await GetPositionsAsync(cancellationToken).ConfigureAwait(false);
        var position = positions.FirstOrDefault(p => p.Symbol == symbol);

        if (position is null || position.Quantity == 0)
        {
            throw new InvalidOperationException($"No position found for symbol: {symbol}");
        }

        var closeRequest = new ExtendedTradeRequest
        {
            Symbol = symbol,
            Action = position.Quantity > 0 ? TradeAction.MarketSell : TradeAction.MarketBuy,
            Quantity = Math.Abs(position.Quantity),
            OrderType = OrderType.Market,
            TimeInForce = TimeInForce.Day
        };

        return await SubmitOrderAsync(closeRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerOrder>> CloseAllPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var positions = await GetPositionsAsync(cancellationToken).ConfigureAwait(false);
        var orders = new List<BrokerOrder>();

        foreach (var position in positions)
        {
            if (position.Quantity != 0)
            {
                var order = await ClosePositionAsync(position.Symbol, cancellationToken).ConfigureAwait(false);
                orders.Add(order);
            }
        }

        return orders;
    }

    private async Task<string> GetDefaultAccountIdAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync("/user/profile", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TradierProfileResponse>(content, _jsonOptions);

        var accountId = result?.Profile?.Account?.FirstOrDefault()?.AccountNumber;
        if (string.IsNullOrEmpty(accountId))
        {
            throw new InvalidOperationException("No Tradier account found");
        }

        return accountId!;
    }

    private static string MapOrderSide(TradeAction action) => action switch
    {
        TradeAction.MarketBuy => "buy",
        TradeAction.MarketSell => "sell",
        TradeAction.ClosePosition => "sell", // Close defaults to sell, actual direction determined by position
        _ => "buy"
    };

    private static string MapOrderType(OrderType orderType) => orderType switch
    {
        OrderType.Market => "market",
        OrderType.Limit => "limit",
        OrderType.Stop => "stop",
        OrderType.StopLimit => "stop_limit",
        _ => "market"
    };

    private static string MapTimeInForce(TimeInForce tif) => tif switch
    {
        TimeInForce.Day => "day",
        TimeInForce.GTC => "gtc",
        TimeInForce.IOC => "immediate",
        TimeInForce.FOK => "fill_or_kill",
        _ => "day"
    };

    private static BrokerOrderStatus MapOrderStatus(string status) => status.ToLowerInvariant() switch
    {
        "pending" => BrokerOrderStatus.PendingNew,
        "open" => BrokerOrderStatus.New,
        "filled" => BrokerOrderStatus.Filled,
        "partially_filled" => BrokerOrderStatus.PartiallyFilled,
        "expired" => BrokerOrderStatus.Expired,
        "canceled" => BrokerOrderStatus.Cancelled,
        "rejected" => BrokerOrderStatus.Rejected,
        _ => BrokerOrderStatus.New
    };

    private static OrderType MapOrderTypeFromString(string orderType) => orderType.ToLowerInvariant() switch
    {
        "market" => OrderType.Market,
        "limit" => OrderType.Limit,
        "stop" => OrderType.Stop,
        "stop_limit" => OrderType.StopLimit,
        _ => OrderType.Market
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TradierBroker));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for Tradier API connection.
/// </summary>
public sealed class TradierOptions
{
    /// <summary>
    /// Gets or sets the OAuth access token.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the account ID.
    /// If not set, uses the default account.
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// Gets or sets whether to use the sandbox environment.
    /// </summary>
    public bool UseSandbox { get; set; } = true;
}

#region Tradier API Response Types

internal sealed class TradierBalancesResponse
{
    public TradierBalances? Balances { get; set; }
}

internal sealed class TradierBalances
{
    public decimal TotalEquity { get; set; }
    public decimal TotalCash { get; set; }
    public decimal? StockBuyingPower { get; set; }
    public decimal? OptionBuyingPower { get; set; }
}

internal sealed class TradierPositionsResponse
{
    public TradierPositionsList? Positions { get; set; }
}

internal sealed class TradierPositionsList
{
    public List<TradierPosition>? Position { get; set; }
}

internal sealed class TradierPosition
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CostBasis { get; set; }
}

internal sealed class TradierOrderResponse
{
    public TradierOrderResult? Order { get; set; }
}

internal sealed class TradierOrderResult
{
    public long Id { get; set; }
    public string Status { get; set; } = string.Empty;
}

internal sealed class TradierOrderDetailResponse
{
    public TradierOrderDetail? Order { get; set; }
}

internal sealed class TradierOrderDetail
{
    public long Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ExecQuantity { get; set; }
    public string Side { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public decimal? StopPrice { get; set; }
    public decimal? AvgFillPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; }
}

internal sealed class TradierProfileResponse
{
    public TradierProfile? Profile { get; set; }
}

internal sealed class TradierProfile
{
    public List<TradierAccount>? Account { get; set; }
}

internal sealed class TradierAccount
{
    public string AccountNumber { get; set; } = string.Empty;
}

#endregion
