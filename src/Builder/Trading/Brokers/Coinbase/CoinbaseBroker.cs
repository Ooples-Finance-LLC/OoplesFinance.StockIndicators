using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OoplesFinance.StockIndicators.Builder.Trading.Brokers.Coinbase;

/// <summary>
/// Coinbase Advanced Trade broker implementation.
/// Provides cryptocurrency trading capabilities through the Coinbase API.
/// </summary>
/// <remarks>
/// Supports spot trading of cryptocurrency pairs.
/// Uses HMAC SHA256 authentication.
/// </remarks>
public sealed class CoinbaseBroker : IBroker, IDisposable
{
    private readonly CoinbaseOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private bool _disposed;

    private const string BaseUrl = "https://api.coinbase.com/api/v3/brokerage";
    private const string SandboxUrl = "https://api-public.sandbox.exchange.coinbase.com";

    /// <summary>
    /// Creates a new Coinbase broker instance.
    /// </summary>
    /// <param name="options">Coinbase API options.</param>
    public CoinbaseBroker(CoinbaseOptions? options = null)
    {
        _options = options ?? new CoinbaseOptions();

        _apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("COINBASE_API_KEY") ?? string.Empty;
        _apiSecret = _options.ApiSecret ?? Environment.GetEnvironmentVariable("COINBASE_API_SECRET") ?? string.Empty;

        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException(
                "Coinbase API key is required. Set CoinbaseOptions.ApiKey or COINBASE_API_KEY environment variable.");
        }

        if (string.IsNullOrEmpty(_apiSecret))
        {
            throw new InvalidOperationException(
                "Coinbase API secret is required. Set CoinbaseOptions.ApiSecret or COINBASE_API_SECRET environment variable.");
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.UseSandbox ? SandboxUrl : BaseUrl)
        };

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

        var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            "/accounts",
            null,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<CoinbaseAccountsResponse>(content, _jsonOptions);

        if (result?.Accounts is null || result.Accounts.Count == 0)
        {
            return new BrokerAccount
            {
                AccountId = "coinbase-default",
                Equity = 0m,
                Cash = 0m,
                BuyingPower = 0m,
                IsPaper = _options.UseSandbox
            };
        }

        // Sum up all USD-denominated balances
        decimal totalEquity = 0m;
        foreach (var account in result.Accounts)
        {
            if (account.Currency == "USD" || account.Currency == "USDC")
            {
                totalEquity += account.AvailableBalance?.Value ?? 0m;
            }
        }

        return new BrokerAccount
        {
            AccountId = result.Accounts.FirstOrDefault()?.Uuid ?? "coinbase-default",
            Equity = totalEquity,
            Cash = totalEquity,
            BuyingPower = totalEquity,
            IsPaper = _options.UseSandbox
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            "/accounts",
            null,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<CoinbaseAccountsResponse>(content, _jsonOptions);

        if (result?.Accounts is null)
        {
            return Array.Empty<BrokerPosition>();
        }

        var positions = new List<BrokerPosition>();
        foreach (var account in result.Accounts)
        {
            var balance = account.AvailableBalance?.Value ?? 0m;
            if (balance > 0 && account.Currency != "USD" && account.Currency != "USDC")
            {
                positions.Add(new BrokerPosition
                {
                    Symbol = $"{account.Currency}-USD",
                    Quantity = balance,
                    AverageEntryPrice = 0m, // Coinbase doesn't provide this
                    CurrentPrice = 0m, // Would need market data call
                    UnrealizedPnL = 0m,
                    CostBasis = 0m
                });
            }
        }

        return positions;
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> SubmitOrderAsync(ExtendedTradeRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Convert symbol format (e.g., "BTC-USD" or "BTCUSD" to "BTC-USD")
        var productId = NormalizeProductId(request.Symbol);
        var clientOrderId = Guid.NewGuid().ToString();

        var orderConfig = new Dictionary<string, object>();

        switch (request.OrderType)
        {
            case OrderType.Market:
                orderConfig["market_market_ioc"] = new
                {
                    quote_size = request.Action == TradeAction.MarketBuy
                        ? (request.Quantity * (request.LimitPrice ?? 100m)).ToString("F2")
                        : null,
                    base_size = request.Action == TradeAction.MarketSell
                        ? request.Quantity.ToString("F8")
                        : null
                };
                break;

            case OrderType.Limit:
                orderConfig["limit_limit_gtc"] = new
                {
                    base_size = request.Quantity.ToString("F8"),
                    limit_price = (request.LimitPrice ?? 0).ToString("F2"),
                    post_only = false
                };
                break;

            case OrderType.Stop:
            case OrderType.StopLimit:
                orderConfig["stop_limit_stop_limit_gtc"] = new
                {
                    base_size = request.Quantity.ToString("F8"),
                    limit_price = (request.LimitPrice ?? request.StopPrice ?? 0).ToString("F2"),
                    stop_price = (request.StopPrice ?? 0).ToString("F2"),
                    stop_direction = request.Action == TradeAction.MarketBuy ? "STOP_DIRECTION_STOP_UP" : "STOP_DIRECTION_STOP_DOWN"
                };
                break;
        }

        var orderRequest = new
        {
            client_order_id = clientOrderId,
            product_id = productId,
            side = request.Action == TradeAction.MarketBuy ? "BUY" : "SELL",
            order_configuration = orderConfig
        };

        var json = JsonSerializer.Serialize(orderRequest, _jsonOptions);
        var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            "/orders",
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<CoinbaseOrderResponse>(content, _jsonOptions);

        if (result?.OrderId is null)
        {
            throw new InvalidOperationException("Failed to parse Coinbase order response");
        }

        return new BrokerOrder
        {
            OrderId = result.OrderId,
            Symbol = request.Symbol,
            Quantity = request.Quantity,
            Side = request.Action == TradeAction.MarketBuy ? "buy" : "sell",
            OrderType = request.OrderType,
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice,
            Status = BrokerOrderStatus.New,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var request = new { order_ids = new[] { orderId } };
        var json = JsonSerializer.Serialize(request, _jsonOptions);

        var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            "/orders/batch_cancel",
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken).ConfigureAwait(false);

        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            $"/orders/historical/{orderId}",
            null,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<CoinbaseOrderDetailResponse>(content, _jsonOptions);

        if (result?.Order is null)
        {
            throw new InvalidOperationException($"Order not found: {orderId}");
        }

        var order = result.Order;
        return new BrokerOrder
        {
            OrderId = order.OrderId ?? orderId,
            Symbol = order.ProductId ?? string.Empty,
            Quantity = decimal.Parse(order.FilledSize ?? "0"),
            FilledQuantity = decimal.Parse(order.FilledSize ?? "0"),
            Side = order.Side ?? string.Empty,
            OrderType = MapOrderTypeFromString(order.OrderType ?? string.Empty),
            Status = MapOrderStatus(order.Status ?? ""),
            AverageFillPrice = decimal.TryParse(order.AverageFilledPrice, out var price) ? price : null,
            CreatedAt = order.CreatedTime ?? DateTime.UtcNow
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
            TimeInForce = TimeInForce.IOC
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

    private async Task<HttpResponseMessage> SendAuthenticatedRequestAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var body = content is not null
            ? await content.ReadAsStringAsync().ConfigureAwait(false)
            : string.Empty;

        var message = $"{timestamp}{method.Method}{path}{body}";
        var signature = ComputeSignature(message);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("CB-ACCESS-KEY", _apiKey);
        request.Headers.Add("CB-ACCESS-SIGN", signature);
        request.Headers.Add("CB-ACCESS-TIMESTAMP", timestamp);

        if (content is not null)
        {
            request.Content = content;
        }

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private string ComputeSignature(string message)
    {
        var keyBytes = Convert.FromBase64String(_apiSecret);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToBase64String(hash);
    }

    private static string NormalizeProductId(string symbol)
    {
        // Convert formats like "BTCUSD", "BTC/USD" to "BTC-USD"
        symbol = symbol.ToUpperInvariant().Replace("/", "-");
        if (!symbol.Contains('-') && symbol.Length >= 6)
        {
            // Assume format like "BTCUSD" -> "BTC-USD"
            var baseLen = symbol.EndsWith("USD") || symbol.EndsWith("EUR") || symbol.EndsWith("GBP")
                ? symbol.Length - 3
                : 3;
            return $"{symbol.Substring(0, baseLen)}-{symbol.Substring(baseLen)}";
        }
        return symbol;
    }

    private static BrokerOrderStatus MapOrderStatus(string status) => status.ToUpperInvariant() switch
    {
        "PENDING" => BrokerOrderStatus.PendingNew,
        "OPEN" => BrokerOrderStatus.New,
        "FILLED" => BrokerOrderStatus.Filled,
        "CANCELLED" => BrokerOrderStatus.Cancelled,
        "EXPIRED" => BrokerOrderStatus.Expired,
        "FAILED" => BrokerOrderStatus.Rejected,
        _ => BrokerOrderStatus.New
    };

    private static OrderType MapOrderTypeFromString(string orderType) => orderType.ToUpperInvariant() switch
    {
        "MARKET" => OrderType.Market,
        "LIMIT" => OrderType.Limit,
        "STOP" => OrderType.Stop,
        "STOP_LIMIT" => OrderType.StopLimit,
        _ => OrderType.Market
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CoinbaseBroker));
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
/// Configuration options for Coinbase API connection.
/// </summary>
public sealed class CoinbaseOptions
{
    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API secret.
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// Gets or sets whether to use the sandbox environment.
    /// </summary>
    public bool UseSandbox { get; set; } = true;
}

#region Coinbase API Response Types

internal sealed class CoinbaseAccountsResponse
{
    public List<CoinbaseAccount>? Accounts { get; set; }
}

internal sealed class CoinbaseAccount
{
    public string? Uuid { get; set; }
    public string? Currency { get; set; }
    public CoinbaseBalance? AvailableBalance { get; set; }
}

internal sealed class CoinbaseBalance
{
    public decimal Value { get; set; }
    public string? Currency { get; set; }
}

internal sealed class CoinbaseOrderResponse
{
    public string? OrderId { get; set; }
    public bool Success { get; set; }
}

internal sealed class CoinbaseOrderDetailResponse
{
    public CoinbaseOrder? Order { get; set; }
}

internal sealed class CoinbaseOrder
{
    public string? OrderId { get; set; }
    public string? ProductId { get; set; }
    public string? Side { get; set; }
    public string? OrderType { get; set; }
    public string? FilledSize { get; set; }
    public string? AverageFilledPrice { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedTime { get; set; }
}

#endregion
