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
/// Alpaca trade adapter (placeholder implementation).
/// </summary>
public sealed class AlpacaTradeAdapter : IAutoTradeAdapter
{
    private readonly AlpacaOptions _options;

    /// <summary>
    /// Creates a new Alpaca trade adapter.
    /// </summary>
    public AlpacaTradeAdapter(AlpacaOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public void Execute(TradeRequest request)
    {
        var isPaper = _options.UsePaper ?? true;
        var mode = isPaper ? "PAPER" : "LIVE";
        Console.WriteLine($"[Alpaca {mode}] Signal: {request.Signal}, Action: {request.Action} at {request.Timestamp:yyyy-MM-dd HH:mm:ss}");
        // TODO: Implement actual Alpaca API integration
    }
}
