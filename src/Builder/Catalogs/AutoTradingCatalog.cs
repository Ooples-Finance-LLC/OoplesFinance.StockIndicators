using OoplesFinance.StockIndicators.Builder.Trading;

namespace OoplesFinance.StockIndicators.Builder.Catalogs;

/// <summary>
/// Catalog of auto-trading adapters with fluent configuration.
/// </summary>
public sealed class AutoTradingCatalog
{
    private readonly List<AutoTradeRule> _rules = new();
    private readonly List<IAutoTradeAdapter> _adapters = new();
    private readonly List<ExtendedAutoTradeRule> _extendedRules = new();
    private IBroker? _broker;

    /// <summary>
    /// Gets or sets the maximum number of concurrent positions. Defaults to 10.
    /// </summary>
    public int MaxPositions { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum position size as percent of portfolio. Defaults to 10%.
    /// </summary>
    public decimal MaxPositionSize { get; set; } = 10m;

    /// <summary>
    /// Gets or sets the daily loss limit as decimal (0.02 = 2%). Defaults to 2%.
    /// </summary>
    public decimal DailyLossLimit { get; set; } = 0.02m;

    /// <summary>
    /// Gets or sets whether confirmation is required before executing trades. Defaults to false.
    /// </summary>
    public bool RequireConfirmation { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the emergency stop is activated. Defaults to false.
    /// </summary>
    public bool EmergencyStop { get; set; } = false;

    /// <summary>
    /// Gets or sets the default symbol to trade. Defaults to "SPY".
    /// </summary>
    public string DefaultSymbol { get; set; } = "SPY";

    /// <summary>
    /// Sets the broker to use for trading operations.
    /// </summary>
    /// <param name="broker">The broker implementation.</param>
    /// <returns>This catalog for fluent chaining.</returns>
    public AutoTradingCatalog UseBroker(IBroker broker)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        return this;
    }

    /// <summary>
    /// Creates a trade rule for a signal by name or pattern.
    /// </summary>
    /// <param name="signalNameOrPattern">The signal name or wildcard pattern (e.g., "RSI*").</param>
    /// <returns>A builder for configuring the trade rule.</returns>
    /// <example>
    /// <code>
    /// trading.OnSignal("Strong Buy Signal")
    ///        .Buy()
    ///        .WithSize(PositionSize.Fixed(100))
    ///        .WithStopLoss(StopLoss.Percent(2))
    ///        .Execute();
    /// </code>
    /// </example>
    public SignalTradeRuleBuilder OnSignal(string signalNameOrPattern)
    {
        return new SignalTradeRuleBuilder(signalNameOrPattern, AddExtendedRule);
    }

    /// <summary>
    /// Creates a trade rule for a signal by handle.
    /// </summary>
    /// <param name="signalHandle">The signal handle from ConfigureSignals.</param>
    /// <returns>A builder for configuring the trade rule.</returns>
    public SignalTradeRuleBuilder OnSignal(SignalHandle signalHandle)
    {
        return new SignalTradeRuleBuilder(signalHandle, AddExtendedRule);
    }

    private void AddExtendedRule(ExtendedAutoTradeRule rule)
    {
        _extendedRules.Add(rule);
    }

    /// <summary>
    /// Creates a console trade adapter builder.
    /// </summary>
    public AutoTradeAdapterBuilder ConsoleAdapter()
    {
        var adapter = new ConsoleTradeAdapter();
        _adapters.Add(adapter);
        return new AutoTradeAdapterBuilder(this, adapter);
    }

    /// <summary>
    /// Creates an Alpaca trade adapter builder.
    /// </summary>
    /// <param name="options">Alpaca API configuration options.</param>
    /// <param name="executionOptions">Trade execution options including safety settings.</param>
    public AutoTradeAdapterBuilder Alpaca(AlpacaOptions? options = null, TradeExecutionOptions? executionOptions = null)
    {
        var adapter = new AlpacaTradeAdapter(options ?? new AlpacaOptions(), executionOptions);
        _adapters.Add(adapter);
        return new AutoTradeAdapterBuilder(this, adapter);
    }

    /// <summary>
    /// Adds a custom trade adapter.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when adapter is null.</exception>
    public AutoTradeAdapterBuilder AddAdapter(IAutoTradeAdapter adapter)
    {
        if (adapter is null) throw new ArgumentNullException(nameof(adapter));
        _adapters.Add(adapter);
        return new AutoTradeAdapterBuilder(this, adapter);
    }

    internal void AddRule(AutoTradeRule rule)
    {
        _rules.Add(rule);
    }

    internal AutoTradingConfiguration Build()
    {
        // Return singleton empty configuration when no trading configured to avoid allocation
        if (_rules.Count == 0 && _adapters.Count == 0 && _extendedRules.Count == 0 && _broker is null)
        {
            return AutoTradingConfiguration.Empty;
        }

        return new AutoTradingConfiguration(
            new List<AutoTradeRule>(_rules),
            new List<IAutoTradeAdapter>(_adapters),
            new List<ExtendedAutoTradeRule>(_extendedRules),
            _broker,
            new TradingSettings
            {
                MaxPositions = MaxPositions,
                MaxPositionSize = MaxPositionSize,
                DailyLossLimit = DailyLossLimit,
                RequireConfirmation = RequireConfirmation,
                EmergencyStop = EmergencyStop,
                DefaultSymbol = DefaultSymbol
            });
    }
}
