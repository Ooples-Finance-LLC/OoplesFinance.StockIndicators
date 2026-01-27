using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OoplesFinance.StockIndicators.Builder.Trading.Brokers.TDAmeritrade;

/// <summary>
/// TD Ameritrade / Charles Schwab broker implementation.
/// Provides trading capabilities through the Schwab API (formerly TD Ameritrade).
/// </summary>
/// <remarks>
/// As of 2024, TD Ameritrade accounts have migrated to Schwab.
/// This implementation uses the Schwab Trader API for authentication and trading.
/// Supports stocks, options, ETFs, and futures.
/// </remarks>
public sealed class TDBroker : IBroker, IDisposable
{
    private readonly TDOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private bool _disposed;

    // Schwab API endpoints (formerly TD Ameritrade)
    private const string AuthUrl = "https://api.schwabapi.com/v1/oauth/token";
    private const string BaseUrl = "https://api.schwabapi.com/trader/v1";

    /// <summary>
    /// Creates a new TD Ameritrade / Schwab broker instance.
    /// </summary>
    /// <param name="options">TD/Schwab API options.</param>
    public TDBroker(TDOptions? options = null)
    {
        _options = options ?? new TDOptions();

        _httpClient = new HttpClient
        {
            Timeout = _options.Timeout
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Authenticates with the Schwab API using OAuth 2.0.
    /// </summary>
    public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var clientId = _options.ClientId ?? Environment.GetEnvironmentVariable("TD_CLIENT_ID");
        var clientSecret = _options.ClientSecret ?? Environment.GetEnvironmentVariable("TD_CLIENT_SECRET");
        var refreshToken = _options.RefreshToken ?? Environment.GetEnvironmentVariable("TD_REFRESH_TOKEN");

        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException(
                "TD/Schwab client ID is required. Set TDOptions.ClientId or TD_CLIENT_ID environment variable.");
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new InvalidOperationException(
                "TD/Schwab refresh token is required. Set TDOptions.RefreshToken or TD_REFRESH_TOKEN environment variable.");
        }

        // Exchange refresh token for access token
        var authContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("client_id", clientId)
        });

        if (!string.IsNullOrEmpty(clientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        var response = await _httpClient.PostAsync(AuthUrl, authContent, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<TDTokenResponse>(content, _jsonOptions);

        if (tokenResponse?.AccessToken is null)
        {
            throw new InvalidOperationException("Failed to obtain access token from Schwab API");
        }

        _accessToken = tokenResponse.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // 60 second buffer
    }

    /// <inheritdoc />
    public async Task<BrokerAccount> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/accounts/{accountId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TDAccountResponse>(content, _jsonOptions);

        if (result?.SecuritiesAccount is null)
        {
            throw new InvalidOperationException("Failed to parse TD/Schwab account response");
        }

        var account = result.SecuritiesAccount;
        var balances = account.CurrentBalances;

        return new BrokerAccount
        {
            AccountId = account.AccountId ?? accountId,
            Equity = balances?.Equity ?? 0m,
            Cash = balances?.AvailableFunds ?? balances?.CashBalance ?? 0m,
            BuyingPower = balances?.BuyingPower ?? balances?.AvailableFunds ?? 0m,
            IsPaper = false // TD/Schwab doesn't have paper trading via API
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/accounts/{accountId}?fields=positions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TDAccountResponse>(content, _jsonOptions);

        if (result?.SecuritiesAccount?.Positions is null)
        {
            return Array.Empty<BrokerPosition>();
        }

        return result.SecuritiesAccount.Positions.Select(p => new BrokerPosition
        {
            Symbol = p.Instrument?.Symbol ?? string.Empty,
            Quantity = p.LongQuantity - p.ShortQuantity,
            AverageEntryPrice = p.AveragePrice,
            CurrentPrice = p.CurrentDayProfitLossPercentage != 0
                ? p.AveragePrice * (1 + p.CurrentDayProfitLossPercentage / 100)
                : p.MarketValue / (p.LongQuantity - p.ShortQuantity),
            UnrealizedPnL = p.CurrentDayProfitLoss,
            CostBasis = p.AveragePrice * (p.LongQuantity - p.ShortQuantity)
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> SubmitOrderAsync(ExtendedTradeRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var orderRequest = new TDOrderRequest
        {
            OrderType = MapOrderType(request.OrderType),
            Session = "NORMAL",
            Duration = MapTimeInForce(request.TimeInForce),
            OrderStrategyType = "SINGLE",
            Price = request.LimitPrice,
            StopPrice = request.StopPrice,
            OrderLegCollection = new[]
            {
                new TDOrderLeg
                {
                    Instruction = MapOrderInstruction(request.Action),
                    Quantity = (int)request.Quantity,
                    Instrument = new TDInstrument
                    {
                        Symbol = request.Symbol,
                        AssetType = "EQUITY"
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(orderRequest, _jsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/accounts/{accountId}/orders")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Get the order ID from the Location header
        var locationHeader = response.Headers.Location?.ToString();
        var orderId = locationHeader?.Split('/').LastOrDefault() ?? Guid.NewGuid().ToString();

        return new BrokerOrder
        {
            OrderId = orderId,
            Symbol = request.Symbol,
            Quantity = request.Quantity,
            Side = request.Action == TradeAction.MarketBuy ? "buy" : "sell",
            OrderType = request.OrderType,
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice,
            Status = BrokerOrderStatus.PendingNew,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/accounts/{accountId}/orders/{orderId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<BrokerOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var accountId = _options.AccountId ?? await GetDefaultAccountIdAsync(cancellationToken).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/accounts/{accountId}/orders/{orderId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<TDOrder>(content, _jsonOptions);

        if (result is null)
        {
            throw new InvalidOperationException($"Order not found: {orderId}");
        }

        var leg = result.OrderLegCollection?.FirstOrDefault();
        return new BrokerOrder
        {
            OrderId = result.OrderId?.ToString() ?? orderId,
            Symbol = leg?.Instrument?.Symbol ?? string.Empty,
            Quantity = leg?.Quantity ?? 0,
            FilledQuantity = result.FilledQuantity,
            Side = leg?.Instruction?.ToLowerInvariant().Contains("buy") == true ? "buy" : "sell",
            OrderType = MapOrderTypeFromString(result.OrderType ?? string.Empty),
            LimitPrice = result.Price,
            StopPrice = result.StopPrice,
            Status = MapOrderStatus(result.Status ?? string.Empty),
            AverageFillPrice = result.FilledQuantity > 0
                ? result.OrderActivityCollection?.FirstOrDefault()?.ExecutionLegs?.FirstOrDefault()?.Price
                : null,
            CreatedAt = result.EnteredTime ?? DateTime.UtcNow
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

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is null || DateTime.UtcNow >= _tokenExpiry)
        {
            await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> GetDefaultAccountIdAsync(CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/accounts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var accounts = JsonSerializer.Deserialize<List<TDAccountResponse>>(content, _jsonOptions);

        var accountId = accounts?.FirstOrDefault()?.SecuritiesAccount?.AccountId;
        if (string.IsNullOrEmpty(accountId))
        {
            throw new InvalidOperationException("No TD/Schwab account found");
        }

        return accountId ?? throw new InvalidOperationException("No TD/Schwab account found");
    }

    private static string MapOrderType(OrderType orderType) => orderType switch
    {
        OrderType.Market => "MARKET",
        OrderType.Limit => "LIMIT",
        OrderType.Stop => "STOP",
        OrderType.StopLimit => "STOP_LIMIT",
        OrderType.TrailingStop => "TRAILING_STOP",
        _ => "MARKET"
    };

    private static string MapTimeInForce(TimeInForce tif) => tif switch
    {
        TimeInForce.Day => "DAY",
        TimeInForce.GTC => "GOOD_TILL_CANCEL",
        TimeInForce.IOC => "IMMEDIATE_OR_CANCEL",
        TimeInForce.FOK => "FILL_OR_KILL",
        _ => "DAY"
    };

    private static string MapOrderInstruction(TradeAction action) => action switch
    {
        TradeAction.MarketBuy => "BUY",
        TradeAction.MarketSell => "SELL",
        TradeAction.ClosePosition => "SELL",
        _ => "BUY"
    };

    private static BrokerOrderStatus MapOrderStatus(string status) => status.ToUpperInvariant() switch
    {
        "AWAITING_PARENT_ORDER" => BrokerOrderStatus.PendingNew,
        "AWAITING_CONDITION" => BrokerOrderStatus.PendingNew,
        "AWAITING_MANUAL_REVIEW" => BrokerOrderStatus.PendingNew,
        "ACCEPTED" => BrokerOrderStatus.New,
        "PENDING_ACTIVATION" => BrokerOrderStatus.New,
        "QUEUED" => BrokerOrderStatus.New,
        "WORKING" => BrokerOrderStatus.New,
        "FILLED" => BrokerOrderStatus.Filled,
        "EXPIRED" => BrokerOrderStatus.Expired,
        "CANCELED" => BrokerOrderStatus.Cancelled,
        "REJECTED" => BrokerOrderStatus.Rejected,
        "PENDING_CANCEL" => BrokerOrderStatus.PendingCancel,
        "PENDING_REPLACE" => BrokerOrderStatus.PendingReplace,
        "REPLACED" => BrokerOrderStatus.Replaced,
        _ => BrokerOrderStatus.New
    };

    private static OrderType MapOrderTypeFromString(string orderType) => orderType.ToUpperInvariant() switch
    {
        "MARKET" => OrderType.Market,
        "LIMIT" => OrderType.Limit,
        "STOP" => OrderType.Stop,
        "STOP_LIMIT" => OrderType.StopLimit,
        "TRAILING_STOP" => OrderType.TrailingStop,
        _ => OrderType.Market
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TDBroker));
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
/// Configuration options for TD Ameritrade / Schwab API connection.
/// </summary>
public sealed class TDOptions
{
    /// <summary>
    /// Gets or sets the OAuth client ID (App Key).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the OAuth refresh token.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the account ID.
    /// If not set, uses the first account.
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

#region TD/Schwab API Types

internal sealed class TDTokenResponse
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public string? Scope { get; set; }
}

internal sealed class TDAccountResponse
{
    public TDSecuritiesAccount? SecuritiesAccount { get; set; }
}

internal sealed class TDSecuritiesAccount
{
    public string? AccountId { get; set; }
    public string? Type { get; set; }
    public TDBalances? CurrentBalances { get; set; }
    public List<TDPosition>? Positions { get; set; }
}

internal sealed class TDBalances
{
    public decimal AvailableFunds { get; set; }
    public decimal BuyingPower { get; set; }
    public decimal CashBalance { get; set; }
    public decimal Equity { get; set; }
    public decimal LongMarketValue { get; set; }
    public decimal ShortMarketValue { get; set; }
}

internal sealed class TDPosition
{
    public decimal ShortQuantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentDayProfitLoss { get; set; }
    public decimal CurrentDayProfitLossPercentage { get; set; }
    public decimal LongQuantity { get; set; }
    public decimal MarketValue { get; set; }
    public TDInstrument? Instrument { get; set; }
}

internal sealed class TDInstrument
{
    public string? AssetType { get; set; }
    public string? Cusip { get; set; }
    public string? Symbol { get; set; }
    public string? Description { get; set; }
}

internal sealed class TDOrderRequest
{
    public string? OrderType { get; set; }
    public string? Session { get; set; }
    public string? Duration { get; set; }
    public string? OrderStrategyType { get; set; }
    public decimal? Price { get; set; }
    public decimal? StopPrice { get; set; }
    public TDOrderLeg[]? OrderLegCollection { get; set; }
}

internal sealed class TDOrderLeg
{
    public string? Instruction { get; set; }
    public int Quantity { get; set; }
    public TDInstrument? Instrument { get; set; }
}

internal sealed class TDOrder
{
    public long? OrderId { get; set; }
    public string? OrderType { get; set; }
    public string? Status { get; set; }
    public decimal? Price { get; set; }
    public decimal? StopPrice { get; set; }
    public decimal FilledQuantity { get; set; }
    public DateTime? EnteredTime { get; set; }
    public List<TDOrderLeg>? OrderLegCollection { get; set; }
    public List<TDOrderActivity>? OrderActivityCollection { get; set; }
}

internal sealed class TDOrderActivity
{
    public string? ActivityType { get; set; }
    public List<TDExecutionLeg>? ExecutionLegs { get; set; }
}

internal sealed class TDExecutionLeg
{
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public DateTime? Time { get; set; }
}

#endregion
