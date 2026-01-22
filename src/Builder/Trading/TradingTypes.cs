using OoplesFinance.StockIndicators.Builder.Catalogs;

namespace OoplesFinance.StockIndicators.Builder.Trading;

/// <summary>
/// Interface for auto-trade adapters.
/// </summary>
public interface IAutoTradeAdapter
{
    /// <summary>
    /// Executes a trade request.
    /// </summary>
    void Execute(TradeRequest request);
}

/// <summary>
/// Trade request data.
/// </summary>
public sealed class TradeRequest
{
    /// <summary>
    /// Creates a new trade request.
    /// </summary>
    public TradeRequest(SignalHandle signal, TradeAction action, DateTime timestamp)
    {
        Signal = signal;
        Action = action;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the signal handle.
    /// </summary>
    public SignalHandle Signal { get; }

    /// <summary>
    /// Gets the trade action.
    /// </summary>
    public TradeAction Action { get; }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Auto-trade rule.
/// </summary>
public sealed class AutoTradeRule
{
    /// <summary>
    /// Creates a new auto-trade rule.
    /// </summary>
    public AutoTradeRule(IAutoTradeAdapter adapter, SignalHandle signal, TradeAction action)
    {
        Adapter = adapter;
        Signal = signal;
        Action = action;
    }

    /// <summary>
    /// Gets the adapter.
    /// </summary>
    public IAutoTradeAdapter Adapter { get; }

    /// <summary>
    /// Gets the signal handle.
    /// </summary>
    public SignalHandle Signal { get; }

    /// <summary>
    /// Gets the trade action.
    /// </summary>
    public TradeAction Action { get; }
}

/// <summary>
/// Auto-trading configuration.
/// </summary>
public sealed class AutoTradingConfiguration
{
    /// <summary>
    /// Creates a new auto-trading configuration.
    /// </summary>
    public AutoTradingConfiguration(IReadOnlyList<AutoTradeRule> rules, IReadOnlyList<IAutoTradeAdapter> adapters)
    {
        Rules = rules;
        Adapters = adapters;
    }

    /// <summary>
    /// Gets the rules.
    /// </summary>
    public IReadOnlyList<AutoTradeRule> Rules { get; }

    /// <summary>
    /// Gets the adapters.
    /// </summary>
    public IReadOnlyList<IAutoTradeAdapter> Adapters { get; }
}

/// <summary>
/// Builder for auto-trade adapters.
/// </summary>
public readonly struct AutoTradeAdapterBuilder
{
    private readonly AutoTradingCatalog _catalog;
    private readonly IAutoTradeAdapter _adapter;

    internal AutoTradeAdapterBuilder(AutoTradingCatalog catalog, IAutoTradeAdapter adapter)
    {
        _catalog = catalog;
        _adapter = adapter;
    }

    /// <summary>
    /// Creates a rule for a signal.
    /// </summary>
    public AutoTradeRuleBuilder OnSignal(SignalHandle signal)
    {
        return new AutoTradeRuleBuilder(_catalog, _adapter, signal);
    }
}

/// <summary>
/// Builder for auto-trade rules.
/// </summary>
public readonly struct AutoTradeRuleBuilder
{
    private readonly AutoTradingCatalog _catalog;
    private readonly IAutoTradeAdapter _adapter;
    private readonly SignalHandle _signal;

    internal AutoTradeRuleBuilder(AutoTradingCatalog catalog, IAutoTradeAdapter adapter, SignalHandle signal)
    {
        _catalog = catalog;
        _adapter = adapter;
        _signal = signal;
    }

    /// <summary>
    /// Creates a market buy rule.
    /// </summary>
    public AutoTradeAdapterBuilder MarketBuy()
    {
        _catalog.AddRule(new AutoTradeRule(_adapter, _signal, TradeAction.MarketBuy));
        return new AutoTradeAdapterBuilder(_catalog, _adapter);
    }

    /// <summary>
    /// Creates a market sell rule.
    /// </summary>
    public AutoTradeAdapterBuilder MarketSell()
    {
        _catalog.AddRule(new AutoTradeRule(_adapter, _signal, TradeAction.MarketSell));
        return new AutoTradeAdapterBuilder(_catalog, _adapter);
    }

    /// <summary>
    /// Creates a close position rule.
    /// </summary>
    public AutoTradeAdapterBuilder ClosePosition()
    {
        _catalog.AddRule(new AutoTradeRule(_adapter, _signal, TradeAction.ClosePosition));
        return new AutoTradeAdapterBuilder(_catalog, _adapter);
    }
}

/// <summary>
/// Alpaca trading options.
/// </summary>
public sealed class AlpacaOptions
{
    /// <summary>
    /// Gets or sets the API key. Defaults from ALPACA_KEY environment variable.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API secret. Defaults from ALPACA_SECRET environment variable.
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// Gets or sets whether to use paper trading. Defaults to true.
    /// </summary>
    public bool? UsePaper { get; set; }

    /// <summary>
    /// Gets or sets the base URL.
    /// </summary>
    public string? BaseUrl { get; set; }
}

/// <summary>
/// Trade execution options.
/// </summary>
public sealed class TradeExecutionOptions
{
    /// <summary>
    /// Gets or sets the default order type.
    /// </summary>
    public string? DefaultOrderType { get; set; }

    /// <summary>
    /// Gets or sets the default time in force.
    /// </summary>
    public string? DefaultTimeInForce { get; set; }

    /// <summary>
    /// Gets or sets the default quantity.
    /// </summary>
    public decimal? DefaultQuantity { get; set; }

    /// <summary>
    /// Gets or sets whether to enable live trading. Defaults to false (safety).
    /// </summary>
    public bool? EnableLiveTrading { get; set; }
}

/// <summary>
/// Console trade adapter (for testing).
/// </summary>
public sealed class ConsoleTradeAdapter : IAutoTradeAdapter
{
    /// <inheritdoc />
    public void Execute(TradeRequest request)
    {
        Console.WriteLine($"[Trade] Signal: {request.Signal}, Action: {request.Action} at {request.Timestamp:yyyy-MM-dd HH:mm:ss}");
    }
}

/// <summary>
/// Alpaca trade adapter using the Alpaca Trading API.
/// </summary>
public sealed class AlpacaTradeAdapter : IAutoTradeAdapter
{
    private readonly AlpacaOptions _options;
    private readonly TradeExecutionOptions? _executionOptions;
    private static readonly System.Net.Http.HttpClient HttpClient = new();

    /// <summary>
    /// Creates a new Alpaca trade adapter.
    /// </summary>
    public AlpacaTradeAdapter(AlpacaOptions options, TradeExecutionOptions? executionOptions = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _executionOptions = executionOptions;
    }

    /// <inheritdoc />
    public void Execute(TradeRequest request)
    {
        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("ALPACA_KEY");
        var apiSecret = _options.ApiSecret ?? Environment.GetEnvironmentVariable("ALPACA_SECRET");
        var usePaper = _options.UsePaper ?? true;

        // Safety check - EnableLiveTrading must be explicitly true for live trading
        var enableLiveTrading = _executionOptions?.EnableLiveTrading ?? false;
        if (!usePaper && !enableLiveTrading)
        {
            Console.WriteLine("[Alpaca LIVE] BLOCKED - EnableLiveTrading is false. Set EnableLiveTrading = true to allow live trading.");
            return;
        }

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            Console.WriteLine($"[Alpaca] Configuration incomplete - ApiKey: {(string.IsNullOrEmpty(apiKey) ? "missing" : "set")}, ApiSecret: {(string.IsNullOrEmpty(apiSecret) ? "missing" : "set")}");
            return;
        }

        var baseUrl = _options.BaseUrl ?? (usePaper
            ? "https://paper-api.alpaca.markets"
            : "https://api.alpaca.markets");

        var mode = usePaper ? "PAPER" : "LIVE";

        try
        {
            // Determine order parameters
            var symbol = "SPY"; // Default symbol - would be configured per-signal in real use
            var qty = _executionOptions?.DefaultQuantity ?? 1m;
            var side = request.Action switch
            {
                TradeAction.MarketBuy => "buy",
                TradeAction.MarketSell => "sell",
                TradeAction.ClosePosition => "sell", // Simplified - would check position direction
                _ => "buy"
            };
            var orderType = _executionOptions?.DefaultOrderType ?? "market";
            var timeInForce = _executionOptions?.DefaultTimeInForce ?? "day";

            var orderPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                symbol,
                qty = qty.ToString("F0"),
                side,
                type = orderType,
                time_in_force = timeInForce
            });

            var url = $"{baseUrl}/v2/orders";
            var requestMessage = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);
            requestMessage.Headers.Add("APCA-API-KEY-ID", apiKey);
            requestMessage.Headers.Add("APCA-API-SECRET-KEY", apiSecret);
            requestMessage.Content = new System.Net.Http.StringContent(orderPayload, System.Text.Encoding.UTF8, "application/json");

            var response = HttpClient.SendAsync(requestMessage).GetAwaiter().GetResult();
            var responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Alpaca {mode}] Order submitted: {side} {qty} {symbol} - Signal: {request.Signal}");
            }
            else
            {
                Console.WriteLine($"[Alpaca {mode}] Order failed: {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Alpaca {mode}] Error executing trade: {ex.Message}");
        }
    }
}
