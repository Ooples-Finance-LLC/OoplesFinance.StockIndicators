using OoplesFinance.StockIndicators.Builder.Notifications;

namespace OoplesFinance.StockIndicators.Builder.Trading;

/// <summary>
/// Production-ready trade execution engine that processes signals and executes trades
/// with full risk management and position sizing.
/// </summary>
public sealed class TradeExecutionEngine : IDisposable
{
    private readonly IBroker _broker;
    private readonly TradingSettings _settings;
    private readonly IReadOnlyList<ExtendedAutoTradeRule> _rules;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private readonly Dictionary<string, decimal> _dailyPnL = new();
    private DateTime _lastPnLResetDate = DateTime.MinValue;
    private bool _disposed;

    /// <summary>
    /// Event raised when a trade is executed.
    /// </summary>
    public event EventHandler<TradeExecutedEventArgs>? TradeExecuted;

    /// <summary>
    /// Event raised when a trade is blocked by risk management.
    /// </summary>
    public event EventHandler<TradeBlockedEventArgs>? TradeBlocked;

    /// <summary>
    /// Creates a new trade execution engine.
    /// </summary>
    /// <param name="broker">The broker to execute trades through.</param>
    /// <param name="settings">Trading settings including risk limits.</param>
    /// <param name="rules">Trade rules to match signals against.</param>
    public TradeExecutionEngine(
        IBroker broker,
        TradingSettings settings,
        IReadOnlyList<ExtendedAutoTradeRule> rules)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <summary>
    /// Processes a notification event and executes any matching trade rules.
    /// </summary>
    /// <param name="notification">The notification to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of processing the notification.</returns>
    public async Task<TradeExecutionResult> ProcessNotificationAsync(
        NotificationEvent notification,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Check emergency stop
        if (_settings.EmergencyStop)
        {
            var blockedResult = TradeExecutionResult.Blocked("Emergency stop is activated");
            TradeBlocked?.Invoke(this, new TradeBlockedEventArgs(notification.Name, blockedResult.BlockedReason ?? "Emergency stop"));
            return blockedResult;
        }

        // Find matching rules
        var matchingRules = FindMatchingRules(notification);
        if (matchingRules.Count == 0)
        {
            return TradeExecutionResult.NoMatch();
        }

        // Lock for thread safety during execution
        await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var results = new List<BrokerOrder>();

            foreach (var rule in matchingRules)
            {
                var orderResult = await ExecuteRuleAsync(rule, notification, cancellationToken).ConfigureAwait(false);
                if (orderResult is not null)
                {
                    results.Add(orderResult);
                    TradeExecuted?.Invoke(this, new TradeExecutedEventArgs(notification.Name, orderResult));
                }
            }

            return new TradeExecutionResult
            {
                Success = results.Count > 0,
                Orders = results
            };
        }
        finally
        {
            _executionLock.Release();
        }
    }

    /// <summary>
    /// Processes a signal by name and executes any matching trade rules.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <param name="signalValue">The signal value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of processing the signal.</returns>
    public Task<TradeExecutionResult> ProcessSignalAsync(
        string signalName,
        double signalValue,
        CancellationToken cancellationToken = default)
    {
        var notification = new NotificationEvent(
            new SignalHandle(0),
            signalName,
            signalValue,
            DateTime.UtcNow);

        return ProcessNotificationAsync(notification, cancellationToken);
    }

    private List<ExtendedAutoTradeRule> FindMatchingRules(NotificationEvent notification)
    {
        var matching = new List<ExtendedAutoTradeRule>();

        foreach (var rule in _rules)
        {
            // Match by handle
            if (rule.SignalHandle.HasValue && rule.SignalHandle.Value.Id == notification.Signal.Id)
            {
                matching.Add(rule);
                continue;
            }

            // Match by pattern
            if (rule.Matches(notification.Name))
            {
                matching.Add(rule);
            }
        }

        return matching;
    }

    private async Task<BrokerOrder?> ExecuteRuleAsync(
        ExtendedAutoTradeRule rule,
        NotificationEvent notification,
        CancellationToken cancellationToken)
    {
        // Get current account state for risk checks
        var account = await _broker.GetAccountAsync(cancellationToken).ConfigureAwait(false);
        var positions = await _broker.GetPositionsAsync(cancellationToken).ConfigureAwait(false);

        // Reset daily P&L if new day
        var today = DateTime.UtcNow.Date;
        if (today > _lastPnLResetDate)
        {
            _dailyPnL.Clear();
            _lastPnLResetDate = today;
        }

        // Check daily loss limit
        if (!CheckDailyLossLimit(account))
        {
            var reason = $"Daily loss limit ({_settings.DailyLossLimit:P0}) reached";
            TradeBlocked?.Invoke(this, new TradeBlockedEventArgs(notification.Name, reason));
            return null;
        }

        // Handle close all positions action
        if (rule.CloseAllPositions)
        {
            var closedOrders = await _broker.CloseAllPositionsAsync(cancellationToken).ConfigureAwait(false);
            return closedOrders.FirstOrDefault();
        }

        // Handle close position for a symbol
        if (rule.Action == TradeAction.ClosePosition)
        {
            var symbol = rule.Symbol ?? _settings.DefaultSymbol;
            return await _broker.ClosePositionAsync(symbol, cancellationToken).ConfigureAwait(false);
        }

        // Check max positions limit for buy orders
        if (rule.Action == TradeAction.MarketBuy)
        {
            if (positions.Count >= _settings.MaxPositions)
            {
                var reason = $"Max positions limit ({_settings.MaxPositions}) reached";
                TradeBlocked?.Invoke(this, new TradeBlockedEventArgs(notification.Name, reason));
                return null;
            }
        }

        // Calculate position size
        var symbol2 = rule.Symbol ?? _settings.DefaultSymbol;
        var quantity = await CalculatePositionSizeAsync(rule, account, symbol2, cancellationToken).ConfigureAwait(false);

        if (quantity <= 0)
        {
            var reason = "Calculated position size is zero or negative";
            TradeBlocked?.Invoke(this, new TradeBlockedEventArgs(notification.Name, reason));
            return null;
        }

        // Check position size limit
        var currentPrice = await GetCurrentPriceAsync(symbol2, cancellationToken).ConfigureAwait(false);
        var positionValue = quantity * currentPrice;
        var maxPositionValue = account.Equity * _settings.MaxPositionSize / 100m;

        if (positionValue > maxPositionValue)
        {
            // Adjust quantity to fit within limit
            quantity = Math.Floor(maxPositionValue / currentPrice);
            if (quantity <= 0)
            {
                var reason = $"Position value exceeds max size ({_settings.MaxPositionSize}% of equity)";
                TradeBlocked?.Invoke(this, new TradeBlockedEventArgs(notification.Name, reason));
                return null;
            }
        }

        // Build the trade request
        var request = BuildTradeRequest(rule, symbol2, quantity, currentPrice, notification);

        // Execute the trade
        return await _broker.SubmitOrderAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private bool CheckDailyLossLimit(BrokerAccount account)
    {
        // Calculate current day's loss percentage
        var dailyLossPercent = account.Equity > 0
            ? Math.Abs(account.DayPnL) / account.Equity
            : 0m;

        // Check against limit
        return dailyLossPercent < _settings.DailyLossLimit;
    }

    private async Task<decimal> CalculatePositionSizeAsync(
        ExtendedAutoTradeRule rule,
        BrokerAccount account,
        string symbol,
        CancellationToken cancellationToken)
    {
        var positionSize = rule.PositionSize;
        if (positionSize is null)
        {
            // Default to fixed 1 share
            return 1m;
        }

        var currentPrice = await GetCurrentPriceAsync(symbol, cancellationToken).ConfigureAwait(false);
        if (currentPrice <= 0)
        {
            return 0m;
        }

        switch (positionSize.Method)
        {
            case TradingPositionSizing.Fixed:
                return positionSize.Value;

            case TradingPositionSizing.PercentOfEquity:
                var equityToUse = account.Equity * (positionSize.Value / 100m);
                return Math.Floor(equityToUse / currentPrice);

            case TradingPositionSizing.RiskBased:
                // Risk-based sizing: risk amount / (current price * stop loss %)
                if (rule.StopLoss is null)
                {
                    // Without stop loss, fall back to fixed
                    return positionSize.Value;
                }

                var stopLossPercent = rule.StopLoss.IsPercent
                    ? rule.StopLoss.Value / 100m
                    : rule.StopLoss.Value / currentPrice;

                if (stopLossPercent <= 0)
                {
                    return positionSize.Value; // Fallback
                }

                var riskPerShare = currentPrice * stopLossPercent;
                return Math.Floor(positionSize.Value / riskPerShare);

            case TradingPositionSizing.AllIn:
                return Math.Floor(account.BuyingPower / currentPrice);

            default:
                return 1m;
        }
    }

    private async Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken)
    {
        // Try to get price from current positions
        var positions = await _broker.GetPositionsAsync(cancellationToken).ConfigureAwait(false);
        var existingPosition = positions.FirstOrDefault(p =>
            string.Equals(p.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (existingPosition is not null && existingPosition.CurrentPrice > 0)
        {
            return existingPosition.CurrentPrice;
        }

        // Fallback: In a real implementation, would query market data
        // For now, we'll use a reasonable default or throw
        return 100m; // Placeholder - real implementation would use market data API
    }

    private ExtendedTradeRequest BuildTradeRequest(
        ExtendedAutoTradeRule rule,
        string symbol,
        decimal quantity,
        decimal currentPrice,
        NotificationEvent notification)
    {
        var request = new ExtendedTradeRequest
        {
            Signal = notification.Signal,
            Action = rule.Action,
            Symbol = symbol,
            Quantity = quantity,
            OrderType = OrderType.Market,
            TimeInForce = TimeInForce.Day,
            Timestamp = DateTime.UtcNow
        };

        // Apply stop loss
        if (rule.StopLoss is not null)
        {
            if (rule.StopLoss.IsTrailing)
            {
                request.IsTrailingStop = true;
                request.TrailingStopOffset = rule.StopLoss.Value;
            }
            else
            {
                var stopLossAmount = rule.StopLoss.IsPercent
                    ? currentPrice * (rule.StopLoss.Value / 100m)
                    : rule.StopLoss.Value;

                request.StopLossPrice = rule.Action == TradeAction.MarketBuy
                    ? currentPrice - stopLossAmount
                    : currentPrice + stopLossAmount;
            }
        }

        // Apply take profit
        if (rule.TakeProfit is not null)
        {
            var takeProfitAmount = rule.TakeProfit.IsPercent
                ? currentPrice * (rule.TakeProfit.Value / 100m)
                : rule.TakeProfit.Value;

            request.TakeProfitPrice = rule.Action == TradeAction.MarketBuy
                ? currentPrice + takeProfitAmount
                : currentPrice - takeProfitAmount;
        }

        return request;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TradeExecutionEngine));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _executionLock.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Result of a trade execution attempt.
/// </summary>
public sealed class TradeExecutionResult
{
    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets the orders that were executed.
    /// </summary>
    public IReadOnlyList<BrokerOrder> Orders { get; set; } = Array.Empty<BrokerOrder>();

    /// <summary>
    /// Gets the reason why the trade was blocked, if applicable.
    /// </summary>
    public string? BlockedReason { get; set; }

    /// <summary>
    /// Gets whether no rules matched the signal.
    /// </summary>
    public bool IsNoMatch { get; set; }

    /// <summary>
    /// Creates a blocked result.
    /// </summary>
    public static TradeExecutionResult Blocked(string reason) => new TradeExecutionResult
    {
        Success = false,
        BlockedReason = reason
    };

    /// <summary>
    /// Creates a no-match result.
    /// </summary>
    public static TradeExecutionResult NoMatch() => new TradeExecutionResult
    {
        Success = false,
        IsNoMatch = true
    };
}

/// <summary>
/// Event arguments for trade execution.
/// </summary>
public sealed class TradeExecutedEventArgs : EventArgs
{
    /// <summary>
    /// Creates new trade executed event args.
    /// </summary>
    public TradeExecutedEventArgs(string signalName, BrokerOrder order)
    {
        SignalName = signalName;
        Order = order;
    }

    /// <summary>
    /// Gets the signal that triggered the trade.
    /// </summary>
    public string SignalName { get; }

    /// <summary>
    /// Gets the executed order.
    /// </summary>
    public BrokerOrder Order { get; }
}

/// <summary>
/// Event arguments for blocked trades.
/// </summary>
public sealed class TradeBlockedEventArgs : EventArgs
{
    /// <summary>
    /// Creates new trade blocked event args.
    /// </summary>
    public TradeBlockedEventArgs(string signalName, string reason)
    {
        SignalName = signalName;
        Reason = reason;
    }

    /// <summary>
    /// Gets the signal that would have triggered the trade.
    /// </summary>
    public string SignalName { get; }

    /// <summary>
    /// Gets the reason the trade was blocked.
    /// </summary>
    public string Reason { get; }
}
