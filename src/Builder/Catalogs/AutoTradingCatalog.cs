using OoplesFinance.StockIndicators.Builder.Trading;

namespace OoplesFinance.StockIndicators.Builder.Catalogs;

/// <summary>
/// Catalog of auto-trading adapters.
/// </summary>
public sealed class AutoTradingCatalog
{
    private readonly List<AutoTradeRule> _rules = new();
    private readonly List<IAutoTradeAdapter> _adapters = new();

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
    public AutoTradeAdapterBuilder AddAdapter(IAutoTradeAdapter adapter)
    {
        _adapters.Add(adapter);
        return new AutoTradeAdapterBuilder(this, adapter);
    }

    internal void AddRule(AutoTradeRule rule)
    {
        _rules.Add(rule);
    }

    internal AutoTradingConfiguration Build()
    {
        return new AutoTradingConfiguration(new List<AutoTradeRule>(_rules), new List<IAutoTradeAdapter>(_adapters));
    }
}
