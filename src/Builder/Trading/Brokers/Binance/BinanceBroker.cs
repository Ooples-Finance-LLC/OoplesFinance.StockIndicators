using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OoplesFinance.StockIndicators.Builder.Trading.Brokers.Binance;

/// <summary>
/// Binance cryptocurrency exchange broker implementation.
/// Provides trading capabilities through the Binance REST API.
/// </summary>
/// <remarks>
/// Supports spot and futures trading of cryptocurrency pairs.
/// Uses HMAC SHA256 authentication for secure API requests.
/// </remarks>
public sealed class BinanceBroker : IBroker, IDisposable
{
    private readonly BinanceOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private bool _disposed;

    private const string SpotBaseUrl = "https://api.binance.com";
    private const string TestnetUrl = "https://testnet.binance.vision";
    private const string FuturesBaseUrl = "https://fapi.binance.com";
    private const string FuturesTestnetUrl = "https://testnet.binancefuture.com";

    /// <summary>
    /// Creates a new Binance broker instance.
    /// </summary>
    /// <param name="options">Binance API options.</param>
    public BinanceBroker(BinanceOptions? options = null)
    {
        _options = options ?? new BinanceOptions();

        _apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? string.Empty;
        _apiSecret = _options.ApiSecret ?? Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? string.Empty;

        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException(
                "Binance API key is required. Set BinanceOptions.ApiKey or BINANCE_API_KEY environment variable.");
        }

        if (string.IsNullOrEmpty(_apiSecret))
        {
            throw new InvalidOperationException(
                "Binance API secret is required. Set BinanceOptions.ApiSecret or BINANCE_API_SECRET environment variable.");
        }

        var baseUrl = _options.UseFutures
            ? (_options.UseTestnet ? FuturesTestnetUrl : FuturesBaseUrl)
            : (_options.UseTestnet ? TestnetUrl : SpotBaseUrl);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = _options.Timeout
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", _apiKey);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<BrokerAccount> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"timestamp={timestamp}";
        var signature = ComputeSignature(queryString);

        var endpoint = _options.UseFutures ? "/fapi/v2/account" : "/api/v3/account";
        var response = await _httpClient.GetAsync(
            $"{endpoint}?{queryString}&signature={signature}",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (_options.UseFutures)
        {
            var futuresAccount = JsonSerializer.Deserialize<BinanceFuturesAccountResponse>(content, _jsonOptions);
            return new BrokerAccount
            {
                AccountId = "binance-futures",
                Equity = futuresAccount?.TotalWalletBalance ?? 0m,
                Cash = futuresAccount?.AvailableBalance ?? 0m,
                BuyingPower = futuresAccount?.AvailableBalance ?? 0m,
                IsPaper = _options.UseTestnet
            };
        }
        else
        {
            var spotAccount = JsonSerializer.Deserialize<BinanceSpotAccountResponse>(content, _jsonOptions);

            // Sum up USDT balance as primary currency
            var usdtBalance = spotAccount?.Balances?
                .FirstOrDefault(b => b.Asset == "USDT" || b.Asset == "BUSD");

            var totalEquity = decimal.Parse(usdtBalance?.Free ?? "0") + decimal.Parse(usdtBalance?.Locked ?? "0");
            var availableCash = decimal.Parse(usdtBalance?.Free ?? "0");

            return new BrokerAccount
            {
                AccountId = "binance-spot",
                Equity = totalEquity,
                Cash = availableCash,
                BuyingPower = availableCash,
                IsPaper = _options.UseTestnet
            };
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var positions = new List<BrokerPosition>();

        if (_options.UseFutures)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var queryString = $"timestamp={timestamp}";
            var signature = ComputeSignature(queryString);

            var response = await _httpClient.GetAsync(
                $"/fapi/v2/positionRisk?{queryString}&signature={signature}",
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var futuresPositions = JsonSerializer.Deserialize<List<BinanceFuturesPosition>>(content, _jsonOptions);

            if (futuresPositions is not null)
            {
                foreach (var pos in futuresPositions)
                {
                    var quantity = decimal.Parse(pos.PositionAmt ?? "0");
                    if (quantity != 0)
                    {
                        var entryPrice = decimal.Parse(pos.EntryPrice ?? "0");
                        var markPrice = decimal.Parse(pos.MarkPrice ?? "0");
                        var unrealizedPnl = decimal.Parse(pos.UnRealizedProfit ?? "0");

                        positions.Add(new BrokerPosition
                        {
                            Symbol = pos.Symbol ?? string.Empty,
                            Quantity = quantity,
                            AverageEntryPrice = entryPrice,
                            CurrentPrice = markPrice,
                            UnrealizedPnL = unrealizedPnl,
                            CostBasis = entryPrice * Math.Abs(quantity)
                        });
                    }
                }
            }
        }
        else
        {
            // For spot, return non-zero balances as positions
            var account = await GetAccountAsync(cancellationToken).ConfigureAwait(false);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var queryString = $"timestamp={timestamp}";
            var signature = ComputeSignature(queryString);

            var response = await _httpClient.GetAsync(
                $"/api/v3/account?{queryString}&signature={signature}",
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var spotAccount = JsonSerializer.Deserialize<BinanceSpotAccountResponse>(content, _jsonOptions);

            if (spotAccount?.Balances is not null)
            {
                foreach (var balance in spotAccount.Balances)
                {
                    var free = decimal.Parse(balance.Free ?? "0");
                    var locked = decimal.Parse(balance.Locked ?? "0");
                    var total = free + locked;

                    // Skip stablecoins and zero balances
                    if (total > 0 && balance.Asset != "USDT" && balance.Asset != "BUSD" && balance.Asset != "USD")
                    {
                        positions.Add(new BrokerPosition
                        {
                            Symbol = $"{balance.Asset}USDT",
                            Quantity = total,
                            AverageEntryPrice = 0m, // Binance doesn't provide this
                            CurrentPrice = 0m, // Would need market data call
                            UnrealizedPnL = 0m,
                            CostBasis = 0m
                        });
                    }
                }
            }
        }

        return positions;
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> SubmitOrderAsync(ExtendedTradeRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var symbol = NormalizeSymbol(request.Symbol);
        var side = request.Action == TradeAction.MarketBuy ? "BUY" : "SELL";
        var orderType = MapOrderType(request.OrderType);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryBuilder = new StringBuilder();
        queryBuilder.Append($"symbol={symbol}");
        queryBuilder.Append($"&side={side}");
        queryBuilder.Append($"&type={orderType}");

        if (_options.UseFutures)
        {
            queryBuilder.Append($"&quantity={request.Quantity:F8}");
        }
        else
        {
            queryBuilder.Append($"&quantity={request.Quantity:F8}");
        }

        if (request.OrderType == OrderType.Limit || request.OrderType == OrderType.StopLimit)
        {
            queryBuilder.Append($"&price={request.LimitPrice:F8}");
            queryBuilder.Append("&timeInForce=GTC");
        }

        if (request.OrderType == OrderType.Stop || request.OrderType == OrderType.StopLimit)
        {
            queryBuilder.Append($"&stopPrice={request.StopPrice:F8}");
        }

        queryBuilder.Append($"&timestamp={timestamp}");

        var queryString = queryBuilder.ToString();
        var signature = ComputeSignature(queryString);

        var endpoint = _options.UseFutures ? "/fapi/v1/order" : "/api/v3/order";
        var response = await _httpClient.PostAsync(
            $"{endpoint}?{queryString}&signature={signature}",
            null,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<BinanceOrderResponse>(content, _jsonOptions);

        if (result is null)
        {
            throw new InvalidOperationException("Failed to parse Binance order response");
        }

        return new BrokerOrder
        {
            OrderId = result.OrderId?.ToString() ?? Guid.NewGuid().ToString(),
            Symbol = request.Symbol,
            Quantity = request.Quantity,
            FilledQuantity = decimal.Parse(result.ExecutedQty ?? "0"),
            Side = side.ToLowerInvariant(),
            OrderType = request.OrderType,
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice,
            Status = MapOrderStatus(result.Status ?? string.Empty),
            AverageFillPrice = decimal.TryParse(result.AvgPrice, out var avgPrice) && avgPrice > 0 ? avgPrice : null,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(result.TransactTime ?? 0).UtcDateTime
        };
    }

    /// <inheritdoc />
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Need symbol to cancel - this is a limitation
        // In practice, you'd store the symbol with the order
        throw new NotSupportedException(
            "Binance requires symbol to cancel an order. Use CancelOrderAsync(orderId, symbol) overload.");
    }

    /// <summary>
    /// Cancels an order by ID and symbol.
    /// </summary>
    public async Task<bool> CancelOrderAsync(string orderId, string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        symbol = NormalizeSymbol(symbol);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"symbol={symbol}&orderId={orderId}&timestamp={timestamp}";
        var signature = ComputeSignature(queryString);

        var endpoint = _options.UseFutures ? "/fapi/v1/order" : "/api/v3/order";
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{endpoint}?{queryString}&signature={signature}");

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Need symbol to query - this is a limitation
        throw new NotSupportedException(
            "Binance requires symbol to query an order. Use GetOrderAsync(orderId, symbol) overload.");
    }

    /// <summary>
    /// Gets an order by ID and symbol.
    /// </summary>
    public async Task<BrokerOrder> GetOrderAsync(string orderId, string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        symbol = NormalizeSymbol(symbol);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"symbol={symbol}&orderId={orderId}&timestamp={timestamp}";
        var signature = ComputeSignature(queryString);

        var endpoint = _options.UseFutures ? "/fapi/v1/order" : "/api/v3/order";
        var response = await _httpClient.GetAsync(
            $"{endpoint}?{queryString}&signature={signature}",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<BinanceOrderResponse>(content, _jsonOptions);

        if (result is null)
        {
            throw new InvalidOperationException($"Order not found: {orderId}");
        }

        return new BrokerOrder
        {
            OrderId = result.OrderId?.ToString() ?? orderId,
            Symbol = result.Symbol ?? symbol,
            Quantity = decimal.Parse(result.OrigQty ?? "0"),
            FilledQuantity = decimal.Parse(result.ExecutedQty ?? "0"),
            Side = result.Side?.ToLowerInvariant() ?? string.Empty,
            OrderType = MapOrderTypeFromString(result.Type ?? string.Empty),
            LimitPrice = decimal.TryParse(result.Price, out var price) ? price : null,
            StopPrice = decimal.TryParse(result.StopPrice, out var stopPrice) ? stopPrice : null,
            Status = MapOrderStatus(result.Status ?? string.Empty),
            AverageFillPrice = decimal.TryParse(result.AvgPrice, out var avgPrice) && avgPrice > 0 ? avgPrice : null,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(result.Time ?? 0).UtcDateTime
        };
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> ClosePositionAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var positions = await GetPositionsAsync(cancellationToken).ConfigureAwait(false);
        var position = positions.FirstOrDefault(p =>
            p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) ||
            p.Symbol.Equals(NormalizeSymbol(symbol), StringComparison.OrdinalIgnoreCase));

        if (position is null || position.Quantity == 0)
        {
            throw new InvalidOperationException($"No position found for symbol: {symbol}");
        }

        var closeRequest = new ExtendedTradeRequest
        {
            Symbol = position.Symbol,
            Action = position.Quantity > 0 ? TradeAction.MarketSell : TradeAction.MarketBuy,
            Quantity = Math.Abs(position.Quantity),
            OrderType = OrderType.Market,
            TimeInForce = TimeInForce.GTC
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

    private string ComputeSignature(string queryString)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_apiSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string NormalizeSymbol(string symbol)
    {
        // Convert formats like "BTC-USDT", "BTC/USDT" to "BTCUSDT"
        return symbol.ToUpperInvariant().Replace("-", string.Empty).Replace("/", string.Empty);
    }

    private static string MapOrderType(OrderType orderType) => orderType switch
    {
        OrderType.Market => "MARKET",
        OrderType.Limit => "LIMIT",
        OrderType.Stop => "STOP_MARKET",
        OrderType.StopLimit => "STOP",
        OrderType.TrailingStop => "TRAILING_STOP_MARKET",
        _ => "MARKET"
    };

    private static BrokerOrderStatus MapOrderStatus(string status) => status.ToUpperInvariant() switch
    {
        "NEW" => BrokerOrderStatus.New,
        "PARTIALLY_FILLED" => BrokerOrderStatus.PartiallyFilled,
        "FILLED" => BrokerOrderStatus.Filled,
        "CANCELED" => BrokerOrderStatus.Cancelled,
        "REJECTED" => BrokerOrderStatus.Rejected,
        "EXPIRED" => BrokerOrderStatus.Expired,
        "PENDING_CANCEL" => BrokerOrderStatus.PendingCancel,
        _ => BrokerOrderStatus.New
    };

    private static OrderType MapOrderTypeFromString(string orderType) => orderType.ToUpperInvariant() switch
    {
        "MARKET" => OrderType.Market,
        "LIMIT" => OrderType.Limit,
        "STOP" or "STOP_MARKET" => OrderType.Stop,
        "STOP_LIMIT" => OrderType.StopLimit,
        "TRAILING_STOP_MARKET" => OrderType.TrailingStop,
        _ => OrderType.Market
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BinanceBroker));
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
/// Configuration options for Binance API connection.
/// </summary>
public sealed class BinanceOptions
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
    /// Gets or sets whether to use the testnet environment.
    /// </summary>
    public bool UseTestnet { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use futures API instead of spot.
    /// </summary>
    public bool UseFutures { get; set; } = false;

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

#region Binance API Response Types

internal sealed class BinanceSpotAccountResponse
{
    public int MakerCommission { get; set; }
    public int TakerCommission { get; set; }
    public bool CanTrade { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanDeposit { get; set; }
    public List<BinanceBalance>? Balances { get; set; }
}

internal sealed class BinanceBalance
{
    public string? Asset { get; set; }
    public string? Free { get; set; }
    public string? Locked { get; set; }
}

internal sealed class BinanceFuturesAccountResponse
{
    public decimal TotalWalletBalance { get; set; }
    public decimal TotalUnrealizedProfit { get; set; }
    public decimal TotalMarginBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public bool CanTrade { get; set; }
}

internal sealed class BinanceFuturesPosition
{
    public string? Symbol { get; set; }
    public string? PositionAmt { get; set; }
    public string? EntryPrice { get; set; }
    public string? MarkPrice { get; set; }
    public string? UnRealizedProfit { get; set; }
    public string? LiquidationPrice { get; set; }
    public string? Leverage { get; set; }
    public string? MarginType { get; set; }
}

internal sealed class BinanceOrderResponse
{
    public long? OrderId { get; set; }
    public string? Symbol { get; set; }
    public string? Status { get; set; }
    public string? ClientOrderId { get; set; }
    public string? Price { get; set; }
    public string? AvgPrice { get; set; }
    public string? OrigQty { get; set; }
    public string? ExecutedQty { get; set; }
    public string? CummulativeQuoteQty { get; set; }
    public string? Type { get; set; }
    public string? Side { get; set; }
    public string? StopPrice { get; set; }
    public long? Time { get; set; }
    public long? TransactTime { get; set; }
}

#endregion
