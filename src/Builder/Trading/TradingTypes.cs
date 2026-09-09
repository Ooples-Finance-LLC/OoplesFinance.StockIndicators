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
/// Trading settings for risk management and safety controls.
/// </summary>
public sealed class TradingSettings
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent positions.
    /// </summary>
    public int MaxPositions { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum position size as percent of portfolio.
    /// </summary>
    public decimal MaxPositionSize { get; set; } = 10m;

    /// <summary>
    /// Gets or sets the daily loss limit as decimal (0.02 = 2%).
    /// </summary>
    public decimal DailyLossLimit { get; set; } = 0.02m;

    /// <summary>
    /// Gets or sets whether confirmation is required before executing trades.
    /// </summary>
    public bool RequireConfirmation { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the emergency stop is activated.
    /// </summary>
    public bool EmergencyStop { get; set; } = false;

    /// <summary>
    /// Gets or sets the default symbol to trade.
    /// </summary>
    public string DefaultSymbol { get; set; } = "SPY";
}

/// <summary>
/// Auto-trading configuration.
/// </summary>
public sealed class AutoTradingConfiguration
{
    /// <summary>
    /// Singleton empty configuration to avoid allocation when no trading is configured.
    /// </summary>
    public static readonly AutoTradingConfiguration Empty = new(
        Array.Empty<AutoTradeRule>(),
        Array.Empty<IAutoTradeAdapter>(),
        Array.Empty<ExtendedAutoTradeRule>(),
        null,
        new TradingSettings());

    /// <summary>
    /// Creates a new auto-trading configuration (legacy).
    /// </summary>
    public AutoTradingConfiguration(IReadOnlyList<AutoTradeRule> rules, IReadOnlyList<IAutoTradeAdapter> adapters)
        : this(rules, adapters, Array.Empty<ExtendedAutoTradeRule>(), null, new TradingSettings())
    {
    }

    /// <summary>
    /// Creates a new auto-trading configuration with extended rules.
    /// </summary>
    public AutoTradingConfiguration(
        IReadOnlyList<AutoTradeRule> rules,
        IReadOnlyList<IAutoTradeAdapter> adapters,
        IReadOnlyList<ExtendedAutoTradeRule> extendedRules,
        IBroker? broker,
        TradingSettings settings)
    {
        Rules = rules;
        Adapters = adapters;
        ExtendedRules = extendedRules;
        Broker = broker;
        Settings = settings;
    }

    /// <summary>
    /// Gets the rules.
    /// </summary>
    public IReadOnlyList<AutoTradeRule> Rules { get; }

    /// <summary>
    /// Gets the adapters.
    /// </summary>
    public IReadOnlyList<IAutoTradeAdapter> Adapters { get; }

    /// <summary>
    /// Gets the extended rules with position sizing and risk controls.
    /// </summary>
    public IReadOnlyList<ExtendedAutoTradeRule> ExtendedRules { get; }

    /// <summary>
    /// Gets the broker for trading operations.
    /// </summary>
    public IBroker? Broker { get; }

    /// <summary>
    /// Gets the trading settings.
    /// </summary>
    public TradingSettings Settings { get; }
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

    /// <summary>
    /// Gets or sets the maximum number of concurrent positions. Defaults to 10.
    /// </summary>
    public int MaxPositions { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum position size as percent of portfolio (0-100). Defaults to 10%.
    /// </summary>
    public double MaxPositionSizePercent { get; set; } = 10.0;

    /// <summary>
    /// Gets or sets the daily loss limit as percent of portfolio (0-100). Trading stops when reached. Defaults to 2%.
    /// </summary>
    public double DailyLossLimitPercent { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets whether confirmation is required before executing trades. Defaults to false.
    /// </summary>
    public bool RequireConfirmation { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the emergency stop (kill switch) is activated. When true, no trades will execute.
    /// </summary>
    public bool EmergencyStop { get; set; } = false;

    /// <summary>
    /// Gets or sets the default stop loss percent. Null means no stop loss. Defaults to 2%.
    /// </summary>
    public double? DefaultStopLossPercent { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the default take profit percent. Null means no take profit. Defaults to null.
    /// </summary>
    public double? DefaultTakeProfitPercent { get; set; }

    /// <summary>
    /// Gets or sets whether to use trailing stop instead of fixed stop. Defaults to false.
    /// </summary>
    public bool UseTrailingStop { get; set; } = false;

    /// <summary>
    /// Gets or sets the position sizing method. Defaults to Fixed.
    /// </summary>
    public TradingPositionSizing PositionSizing { get; set; } = TradingPositionSizing.Fixed;
}

/// <summary>
/// Position sizing methods for auto-trading.
/// </summary>
public enum TradingPositionSizing
{
    /// <summary>
    /// Use fixed quantity from DefaultQuantity.
    /// </summary>
    Fixed,

    /// <summary>
    /// Size position as percent of available equity.
    /// </summary>
    PercentOfEquity,

    /// <summary>
    /// Size position based on risk (stop loss distance).
    /// </summary>
    RiskBased,

    /// <summary>
    /// Use all available buying power.
    /// </summary>
    AllIn
}

/// <summary>
/// Extended trade request with additional order parameters.
/// </summary>
public sealed class ExtendedTradeRequest
{
    /// <summary>
    /// Gets or sets the signal that triggered the trade.
    /// </summary>
    public SignalHandle Signal { get; set; }

    /// <summary>
    /// Gets or sets the trade action.
    /// </summary>
    public TradeAction Action { get; set; }

    /// <summary>
    /// Gets or sets the symbol to trade.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity to trade.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets the order type (market, limit, stop, stop_limit).
    /// </summary>
    public OrderType OrderType { get; set; } = OrderType.Market;

    /// <summary>
    /// Gets or sets the limit price (for limit and stop_limit orders).
    /// </summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>
    /// Gets or sets the stop price (for stop and stop_limit orders).
    /// </summary>
    public decimal? StopPrice { get; set; }

    /// <summary>
    /// Gets or sets the stop loss price.
    /// </summary>
    public decimal? StopLossPrice { get; set; }

    /// <summary>
    /// Gets or sets the take profit price.
    /// </summary>
    public decimal? TakeProfitPrice { get; set; }

    /// <summary>
    /// Gets or sets the time in force.
    /// </summary>
    public TimeInForce TimeInForce { get; set; } = TimeInForce.Day;

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets whether this is a trailing stop order.
    /// </summary>
    public bool IsTrailingStop { get; set; }

    /// <summary>
    /// Gets or sets the trailing stop offset (percent or dollar amount).
    /// </summary>
    public decimal? TrailingStopOffset { get; set; }
}

/// <summary>
/// Order types for trading.
/// </summary>
public enum OrderType
{
    /// <summary>
    /// Market order - executes immediately at current market price.
    /// </summary>
    Market,

    /// <summary>
    /// Limit order - executes at specified price or better.
    /// </summary>
    Limit,

    /// <summary>
    /// Stop order - triggers when price reaches stop price, then executes as market order.
    /// </summary>
    Stop,

    /// <summary>
    /// Stop limit order - triggers when price reaches stop price, then executes as limit order.
    /// </summary>
    StopLimit,

    /// <summary>
    /// Trailing stop order - stop price trails the market price.
    /// </summary>
    TrailingStop
}

/// <summary>
/// Time in force options for orders.
/// </summary>
public enum TimeInForce
{
    /// <summary>
    /// Day order - expires at end of trading day.
    /// </summary>
    Day,

    /// <summary>
    /// Good til cancelled - remains active until filled or cancelled.
    /// </summary>
    GTC,

    /// <summary>
    /// Immediate or cancel - fills what it can immediately, cancels the rest.
    /// </summary>
    IOC,

    /// <summary>
    /// Fill or kill - must fill entire order immediately or cancel.
    /// </summary>
    FOK,

    /// <summary>
    /// On market open - executes at market open.
    /// </summary>
    OPG,

    /// <summary>
    /// On market close - executes at market close.
    /// </summary>
    CLS
}

/// <summary>
/// Interface for broker operations (async version).
/// </summary>
public interface IBroker
{
    /// <summary>
    /// Gets the account information.
    /// </summary>
    Task<BrokerAccount> GetAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all current positions.
    /// </summary>
    Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an order.
    /// </summary>
    Task<BrokerOrder> SubmitOrderAsync(ExtendedTradeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an order.
    /// </summary>
    Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets order status.
    /// </summary>
    Task<BrokerOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes a position.
    /// </summary>
    Task<BrokerOrder> ClosePositionAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes all positions (emergency).
    /// </summary>
    Task<IReadOnlyList<BrokerOrder>> CloseAllPositionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Broker account information.
/// </summary>
public sealed class BrokerAccount
{
    /// <summary>
    /// Gets or sets the account ID.
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account equity (total account value).
    /// </summary>
    public decimal Equity { get; set; }

    /// <summary>
    /// Gets or sets the cash available for trading.
    /// </summary>
    public decimal Cash { get; set; }

    /// <summary>
    /// Gets or sets the buying power (margin accounts may have more).
    /// </summary>
    public decimal BuyingPower { get; set; }

    /// <summary>
    /// Gets or sets the portfolio value (securities only).
    /// </summary>
    public decimal PortfolioValue { get; set; }

    /// <summary>
    /// Gets or sets the day's P&L.
    /// </summary>
    public decimal DayPnL { get; set; }

    /// <summary>
    /// Gets or sets the day's P&L percentage.
    /// </summary>
    public double DayPnLPercent { get; set; }

    /// <summary>
    /// Gets or sets whether the account can trade.
    /// </summary>
    public bool TradingEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether this is a paper trading account.
    /// </summary>
    public bool IsPaper { get; set; }
}

/// <summary>
/// Broker position information.
/// </summary>
public sealed class BrokerPosition
{
    /// <summary>
    /// Gets or sets the symbol.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity (positive for long, negative for short).
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets the average entry price.
    /// </summary>
    public decimal AverageEntryPrice { get; set; }

    /// <summary>
    /// Gets or sets the current market value.
    /// </summary>
    public decimal MarketValue { get; set; }

    /// <summary>
    /// Gets or sets the current market price.
    /// </summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Gets or sets the unrealized P&L.
    /// </summary>
    public decimal UnrealizedPnL { get; set; }

    /// <summary>
    /// Gets or sets the unrealized P&L percentage.
    /// </summary>
    public double UnrealizedPnLPercent { get; set; }

    /// <summary>
    /// Gets or sets the cost basis.
    /// </summary>
    public decimal CostBasis { get; set; }

    /// <summary>
    /// Gets whether this is a long position.
    /// </summary>
    public bool IsLong => Quantity > 0;

    /// <summary>
    /// Gets whether this is a short position.
    /// </summary>
    public bool IsShort => Quantity < 0;
}

/// <summary>
/// Broker order information.
/// </summary>
public sealed class BrokerOrder
{
    /// <summary>
    /// Gets or sets the order ID.
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client order ID.
    /// </summary>
    public string? ClientOrderId { get; set; }

    /// <summary>
    /// Gets or sets the symbol.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the order side (buy/sell).
    /// </summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the order type.
    /// </summary>
    public OrderType OrderType { get; set; }

    /// <summary>
    /// Gets or sets the requested quantity.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets the filled quantity.
    /// </summary>
    public decimal FilledQuantity { get; set; }

    /// <summary>
    /// Gets or sets the average fill price.
    /// </summary>
    public decimal? AverageFillPrice { get; set; }

    /// <summary>
    /// Gets or sets the limit price.
    /// </summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>
    /// Gets or sets the stop price.
    /// </summary>
    public decimal? StopPrice { get; set; }

    /// <summary>
    /// Gets or sets the order status.
    /// </summary>
    public BrokerOrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the time in force.
    /// </summary>
    public TimeInForce TimeInForce { get; set; }

    /// <summary>
    /// Gets or sets the created timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the filled timestamp.
    /// </summary>
    public DateTime? FilledAt { get; set; }

    /// <summary>
    /// Gets or sets the cancelled timestamp.
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Gets whether the order is filled.
    /// </summary>
    public bool IsFilled => Status == BrokerOrderStatus.Filled;

    /// <summary>
    /// Gets whether the order is still pending.
    /// </summary>
    public bool IsPending => Status == BrokerOrderStatus.New || Status == BrokerOrderStatus.PartiallyFilled;
}

/// <summary>
/// Broker order status.
/// </summary>
public enum BrokerOrderStatus
{
    /// <summary>
    /// Order is new and pending.
    /// </summary>
    New,

    /// <summary>
    /// Order is partially filled.
    /// </summary>
    PartiallyFilled,

    /// <summary>
    /// Order is completely filled.
    /// </summary>
    Filled,

    /// <summary>
    /// Order was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Order expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Order was rejected.
    /// </summary>
    Rejected,

    /// <summary>
    /// Order pending cancel.
    /// </summary>
    PendingCancel,

    /// <summary>
    /// Order pending new.
    /// </summary>
    PendingNew,

    /// <summary>
    /// Order was replaced.
    /// </summary>
    Replaced,

    /// <summary>
    /// Order pending replace.
    /// </summary>
    PendingReplace
}

/// <summary>
/// Position sizing configuration.
/// </summary>
public sealed class PositionSize
{
    private PositionSize(TradingPositionSizing method, decimal value)
    {
        Method = method;
        Value = value;
    }

    /// <summary>
    /// Gets the position sizing method.
    /// </summary>
    public TradingPositionSizing Method { get; }

    /// <summary>
    /// Gets the value (quantity, percent, or risk amount depending on method).
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// Creates a fixed quantity position size.
    /// </summary>
    public static PositionSize Fixed(decimal quantity) => new(TradingPositionSizing.Fixed, quantity);

    /// <summary>
    /// Creates a percent of equity position size.
    /// </summary>
    public static PositionSize PercentOfEquity(decimal percent) => new(TradingPositionSizing.PercentOfEquity, percent);

    /// <summary>
    /// Creates a percent of portfolio position size.
    /// </summary>
    public static PositionSize PercentOfPortfolio(decimal percent) => new(TradingPositionSizing.PercentOfEquity, percent);

    /// <summary>
    /// Creates a risk-based position size (based on stop loss distance).
    /// </summary>
    public static PositionSize RiskBased(decimal riskAmount) => new(TradingPositionSizing.RiskBased, riskAmount);

    /// <summary>
    /// Creates an all-in position size (uses all buying power).
    /// </summary>
    public static PositionSize AllIn() => new(TradingPositionSizing.AllIn, 100m);
}

/// <summary>
/// Stop loss configuration.
/// </summary>
public sealed class StopLoss
{
    private StopLoss(bool isPercent, decimal value, bool isTrailing)
    {
        IsPercent = isPercent;
        Value = value;
        IsTrailing = isTrailing;
    }

    /// <summary>
    /// Gets whether this is a percent-based stop loss.
    /// </summary>
    public bool IsPercent { get; }

    /// <summary>
    /// Gets the stop loss value (percent or dollar amount).
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// Gets whether this is a trailing stop loss.
    /// </summary>
    public bool IsTrailing { get; }

    /// <summary>
    /// Creates a percent-based stop loss.
    /// </summary>
    public static StopLoss Percent(decimal percent) => new(true, percent, false);

    /// <summary>
    /// Creates a dollar-based stop loss.
    /// </summary>
    public static StopLoss Dollars(decimal amount) => new(false, amount, false);

    /// <summary>
    /// Creates a trailing percent stop loss.
    /// </summary>
    public static StopLoss TrailingPercent(decimal percent) => new(true, percent, true);

    /// <summary>
    /// Creates a trailing dollar stop loss.
    /// </summary>
    public static StopLoss TrailingDollars(decimal amount) => new(false, amount, true);
}

/// <summary>
/// Take profit configuration.
/// </summary>
public sealed class TakeProfit
{
    private TakeProfit(bool isPercent, decimal value)
    {
        IsPercent = isPercent;
        Value = value;
    }

    /// <summary>
    /// Gets whether this is a percent-based take profit.
    /// </summary>
    public bool IsPercent { get; }

    /// <summary>
    /// Gets the take profit value (percent or dollar amount).
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// Creates a percent-based take profit.
    /// </summary>
    public static TakeProfit Percent(decimal percent) => new(true, percent);

    /// <summary>
    /// Creates a dollar-based take profit.
    /// </summary>
    public static TakeProfit Dollars(decimal amount) => new(false, amount);
}

/// <summary>
/// Extended auto-trade rule with position sizing and risk controls.
/// </summary>
public sealed class ExtendedAutoTradeRule
{
    /// <summary>
    /// Gets or sets the signal name pattern to match.
    /// </summary>
    public string SignalPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signal handle (if matching by handle).
    /// </summary>
    public SignalHandle? SignalHandle { get; set; }

    /// <summary>
    /// Gets or sets the trade action.
    /// </summary>
    public TradeAction Action { get; set; }

    /// <summary>
    /// Gets or sets the position size configuration.
    /// </summary>
    public PositionSize? PositionSize { get; set; }

    /// <summary>
    /// Gets or sets the stop loss configuration.
    /// </summary>
    public StopLoss? StopLoss { get; set; }

    /// <summary>
    /// Gets or sets the take profit configuration.
    /// </summary>
    public TakeProfit? TakeProfit { get; set; }

    /// <summary>
    /// Gets or sets whether to close all positions with this action.
    /// </summary>
    public bool CloseAllPositions { get; set; }

    /// <summary>
    /// Gets or sets the symbol to trade. If null, uses default symbol.
    /// </summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// Checks if this rule matches a notification by name.
    /// Supports wildcard patterns: * (match all), prefix*, *suffix, *contains*
    /// </summary>
    public bool Matches(string signalName) =>
        SignalPatternMatcher.IsMatch(SignalPattern, signalName);
}

/// <summary>
/// Builder for extended auto-trade rules with fluent API.
/// </summary>
public sealed class SignalTradeRuleBuilder
{
    private readonly Action<ExtendedAutoTradeRule> _addRule;
    private readonly ExtendedAutoTradeRule _rule;

    internal SignalTradeRuleBuilder(string signalPattern, Action<ExtendedAutoTradeRule> addRule)
    {
        _addRule = addRule;
        _rule = new ExtendedAutoTradeRule { SignalPattern = signalPattern };
    }

    internal SignalTradeRuleBuilder(SignalHandle handle, Action<ExtendedAutoTradeRule> addRule)
    {
        _addRule = addRule;
        _rule = new ExtendedAutoTradeRule { SignalHandle = handle };
    }

    /// <summary>
    /// Sets the action to buy.
    /// </summary>
    public SignalTradeActionBuilder Buy()
    {
        _rule.Action = TradeAction.MarketBuy;
        return new SignalTradeActionBuilder(_rule, _addRule);
    }

    /// <summary>
    /// Sets the action to sell.
    /// </summary>
    public SignalTradeActionBuilder Sell()
    {
        _rule.Action = TradeAction.MarketSell;
        return new SignalTradeActionBuilder(_rule, _addRule);
    }

    /// <summary>
    /// Sets the action to close position.
    /// </summary>
    public SignalTradeActionBuilder Close()
    {
        _rule.Action = TradeAction.ClosePosition;
        return new SignalTradeActionBuilder(_rule, _addRule);
    }
}

/// <summary>
/// Builder for trade actions with position sizing and risk controls.
/// </summary>
public sealed class SignalTradeActionBuilder
{
    private readonly ExtendedAutoTradeRule _rule;
    private readonly Action<ExtendedAutoTradeRule> _addRule;

    internal SignalTradeActionBuilder(ExtendedAutoTradeRule rule, Action<ExtendedAutoTradeRule> addRule)
    {
        _rule = rule;
        _addRule = addRule;
    }

    /// <summary>
    /// Sets the position size.
    /// </summary>
    public SignalTradeActionBuilder WithSize(PositionSize size)
    {
        _rule.PositionSize = size;
        return this;
    }

    /// <summary>
    /// Sets the stop loss.
    /// </summary>
    public SignalTradeActionBuilder WithStopLoss(StopLoss stopLoss)
    {
        _rule.StopLoss = stopLoss;
        return this;
    }

    /// <summary>
    /// Sets the take profit.
    /// </summary>
    public SignalTradeActionBuilder WithTakeProfit(TakeProfit takeProfit)
    {
        _rule.TakeProfit = takeProfit;
        return this;
    }

    /// <summary>
    /// Sets the symbol to trade.
    /// </summary>
    public SignalTradeActionBuilder ForSymbol(string symbol)
    {
        _rule.Symbol = symbol;
        return this;
    }

    /// <summary>
    /// Specifies that all positions should be closed.
    /// </summary>
    public void AllPositions()
    {
        _rule.CloseAllPositions = true;
        _addRule(_rule);
    }

    /// <summary>
    /// Completes the rule configuration.
    /// </summary>
    public void Execute()
    {
        _addRule(_rule);
    }
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
            using var requestMessage = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);
            requestMessage.Headers.Add("APCA-API-KEY-ID", apiKey);
            requestMessage.Headers.Add("APCA-API-SECRET-KEY", apiSecret);
            requestMessage.Content = new System.Net.Http.StringContent(orderPayload, System.Text.Encoding.UTF8, "application/json");

            using var response = HttpClient.SendAsync(requestMessage).GetAwaiter().GetResult();
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
