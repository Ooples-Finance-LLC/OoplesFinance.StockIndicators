using AiDotNet.Tensors.LinearAlgebra;

namespace OoplesFinance.StockIndicators.Builder.AI;

/// <summary>
/// Risk protection wrapper for trading agents that enforces loss limits,
/// circuit breakers, and position limits before allowing trades.
/// </summary>
/// <remarks>
/// <para>
/// <b>For Beginners:</b> This is like a safety system for your AI trader.
/// It prevents the AI from making trades that could lose too much money
/// or take on too much risk.
/// </para>
/// <para>
/// <b>Example Usage:</b>
/// <code>
/// var guard = AgentRiskGuard.Create()
///     .WithDailyLossLimit(1000)      // Max $1000 loss per day
///     .WithPositionLimit(0.1m)        // Max 10% in any single position
///     .WithVolatilityBreaker(30)      // Pause when VIX > 30
///     .ForAgent(tradingAgentResult)
///     .Build();
///
/// // Get protected action
/// var result = guard.GetProtectedAction(currentState, currentPrice);
/// if (result.IsAllowed)
/// {
///     ExecuteTrade(result.Action);
/// }
/// else
/// {
///     Console.WriteLine($"Trade blocked: {result.BlockReason}");
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class AgentRiskGuard
{
    private readonly RiskGuardOptions _options;
    private readonly TradingAgentResult? _tradingAgent;
    private readonly PortfolioAgentResult? _portfolioAgent;
    private readonly HierarchicalAgentOrchestrator? _hierarchicalAgent;

    private readonly object _stateLock = new();
    private double _dailyPnL;
    private double _weeklyPnL;
    private double _monthlyPnL;
    private DateTime _lastResetDate;
    private DateTime _lastWeekResetDate;
    private DateTime _lastMonthResetDate;
    private int _consecutiveLosses;
    private bool _circuitBreakerTripped;
    private DateTime? _circuitBreakerResetTime;
    private DateTime? _coolingOffUntil;

    internal AgentRiskGuard(
        RiskGuardOptions options,
        TradingAgentResult? tradingAgent,
        PortfolioAgentResult? portfolioAgent,
        HierarchicalAgentOrchestrator? hierarchicalAgent)
    {
        _options = options;
        _tradingAgent = tradingAgent;
        _portfolioAgent = portfolioAgent;
        _hierarchicalAgent = hierarchicalAgent;
        _lastResetDate = DateTime.UtcNow.Date;
        _lastWeekResetDate = GetStartOfWeek(DateTime.UtcNow);
        _lastMonthResetDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    }

    /// <summary>
    /// Creates a new risk guard builder.
    /// </summary>
    public static RiskGuardBuilder Create() => new();

    /// <summary>
    /// Gets whether trading is currently allowed.
    /// </summary>
    public bool IsTradingAllowed
    {
        get
        {
            lock (_stateLock)
            {
                if (_circuitBreakerTripped && _circuitBreakerResetTime > DateTime.UtcNow)
                    return false;

                if (_coolingOffUntil > DateTime.UtcNow)
                    return false;

                return true;
            }
        }
    }

    /// <summary>
    /// Gets the current daily P&L.
    /// </summary>
    public double DailyPnL
    {
        get { lock (_stateLock) { return _dailyPnL; } }
    }

    /// <summary>
    /// Gets a protected trading action from the underlying agent.
    /// </summary>
    /// <param name="state">Current market state.</param>
    /// <param name="currentPrice">Current asset price.</param>
    /// <param name="currentVolatility">Current volatility (VIX or equivalent). Optional.</param>
    /// <returns>Protected action result with approval status.</returns>
    public ProtectedActionResult GetProtectedAction(
        Vector<double> state,
        double currentPrice,
        double? currentVolatility = null)
    {
        ResetPeriodsIfNeeded();

        // Check pre-conditions
        var preCheck = CheckPreConditions(currentVolatility);
        if (!preCheck.IsAllowed)
        {
            return preCheck;
        }

        // Get raw action from agent
        Vector<double> rawAction;
        try
        {
            if (_tradingAgent?.Model is not null)
            {
                rawAction = _tradingAgent.Model.Predict(state);
            }
            else if (_portfolioAgent?.Model is not null)
            {
                rawAction = _portfolioAgent.Model.Predict(state);
            }
            else
            {
                return new ProtectedActionResult
                {
                    IsAllowed = false,
                    BlockReason = "No trained agent available.",
                    RiskLevel = RiskLevel.High
                };
            }
        }
        catch (Exception ex)
        {
            return new ProtectedActionResult
            {
                IsAllowed = false,
                BlockReason = $"Agent error: {ex.Message}",
                RiskLevel = RiskLevel.High
            };
        }

        // Apply position size limits
        var adjustedAction = ApplyPositionLimits(rawAction, currentPrice);

        // Check action risk
        var riskCheck = EvaluateActionRisk(adjustedAction, currentPrice);
        if (!riskCheck.IsAllowed)
        {
            return riskCheck;
        }

        return new ProtectedActionResult
        {
            IsAllowed = true,
            Action = adjustedAction,
            OriginalAction = rawAction,
            WasModified = !VectorsEqual(rawAction, adjustedAction),
            RiskLevel = riskCheck.RiskLevel,
            RiskWarnings = riskCheck.RiskWarnings
        };
    }

    /// <summary>
    /// Records a trade result for P&L tracking and loss limit enforcement.
    /// </summary>
    /// <param name="pnl">The P&L from the trade.</param>
    public void RecordTradeResult(double pnl)
    {
        lock (_stateLock)
        {
            _dailyPnL += pnl;
            _weeklyPnL += pnl;
            _monthlyPnL += pnl;

            if (pnl < 0)
            {
                _consecutiveLosses++;

                // Check for consecutive loss breaker
                if (_options.MaxConsecutiveLosses > 0 &&
                    _consecutiveLosses >= _options.MaxConsecutiveLosses)
                {
                    TripCircuitBreaker("Too many consecutive losses");
                }
            }
            else
            {
                _consecutiveLosses = 0;
            }

            // Check loss limits
            if (_options.DailyLossLimit > 0 && _dailyPnL <= -_options.DailyLossLimit)
            {
                TripCircuitBreaker("Daily loss limit reached");
            }

            if (_options.WeeklyLossLimit > 0 && _weeklyPnL <= -_options.WeeklyLossLimit)
            {
                TripCircuitBreaker("Weekly loss limit reached");
            }

            if (_options.MonthlyLossLimit > 0 && _monthlyPnL <= -_options.MonthlyLossLimit)
            {
                TripCircuitBreaker("Monthly loss limit reached");
            }
        }
    }

    /// <summary>
    /// Manually trips the circuit breaker.
    /// </summary>
    public void TripCircuitBreaker(string reason)
    {
        lock (_stateLock)
        {
            _circuitBreakerTripped = true;
            _circuitBreakerResetTime = DateTime.UtcNow + _options.CircuitBreakerCooldown;

            OnCircuitBreakerTripped?.Invoke(new CircuitBreakerEvent
            {
                Reason = reason,
                TrippedAt = DateTime.UtcNow,
                ResetsAt = _circuitBreakerResetTime.Value,
                DailyPnL = _dailyPnL
            });
        }
    }

    /// <summary>
    /// Manually resets the circuit breaker.
    /// </summary>
    public void ResetCircuitBreaker()
    {
        lock (_stateLock)
        {
            _circuitBreakerTripped = false;
            _circuitBreakerResetTime = null;
        }
    }

    /// <summary>
    /// Starts a cooling off period during which trading is paused.
    /// </summary>
    public void StartCoolingOff(TimeSpan duration)
    {
        lock (_stateLock)
        {
            _coolingOffUntil = DateTime.UtcNow + duration;
        }
    }

    /// <summary>
    /// Gets the current risk guard status.
    /// </summary>
    public RiskGuardStatus GetStatus()
    {
        lock (_stateLock)
        {
            return new RiskGuardStatus
            {
                IsTradingAllowed = IsTradingAllowed,
                IsCircuitBreakerTripped = _circuitBreakerTripped,
                CircuitBreakerResetsAt = _circuitBreakerResetTime,
                CoolingOffUntil = _coolingOffUntil,
                DailyPnL = _dailyPnL,
                WeeklyPnL = _weeklyPnL,
                MonthlyPnL = _monthlyPnL,
                ConsecutiveLosses = _consecutiveLosses,
                DailyLossLimitRemaining = _options.DailyLossLimit > 0
                    ? Math.Max(0, _options.DailyLossLimit + _dailyPnL)
                    : double.MaxValue
            };
        }
    }

    /// <summary>
    /// Event raised when circuit breaker is tripped.
    /// </summary>
    public event Action<CircuitBreakerEvent>? OnCircuitBreakerTripped;

    /// <summary>
    /// Event raised when an action is blocked.
    /// </summary>
    public event Action<ActionBlockedEvent>? OnActionBlocked;

    private ProtectedActionResult CheckPreConditions(double? currentVolatility)
    {
        lock (_stateLock)
        {
            // Check circuit breaker
            if (_circuitBreakerTripped)
            {
                if (_circuitBreakerResetTime <= DateTime.UtcNow)
                {
                    _circuitBreakerTripped = false;
                    _circuitBreakerResetTime = null;
                }
                else
                {
                    var result = new ProtectedActionResult
                    {
                        IsAllowed = false,
                        BlockReason = $"Circuit breaker active. Resets at {_circuitBreakerResetTime:HH:mm:ss}",
                        RiskLevel = RiskLevel.VeryHigh
                    };
                    OnActionBlocked?.Invoke(new ActionBlockedEvent { Reason = result.BlockReason });
                    return result;
                }
            }

            // Check cooling off
            if (_coolingOffUntil > DateTime.UtcNow)
            {
                var result = new ProtectedActionResult
                {
                    IsAllowed = false,
                    BlockReason = $"Cooling off period until {_coolingOffUntil:HH:mm:ss}",
                    RiskLevel = RiskLevel.Medium
                };
                OnActionBlocked?.Invoke(new ActionBlockedEvent { Reason = result.BlockReason });
                return result;
            }

            // Check volatility circuit breaker
            if (currentVolatility.HasValue && _options.VolatilityBreaker > 0 &&
                currentVolatility.Value > _options.VolatilityBreaker)
            {
                var result = new ProtectedActionResult
                {
                    IsAllowed = false,
                    BlockReason = $"Volatility ({currentVolatility:F1}) exceeds threshold ({_options.VolatilityBreaker:F1})",
                    RiskLevel = RiskLevel.High
                };
                OnActionBlocked?.Invoke(new ActionBlockedEvent { Reason = result.BlockReason });
                return result;
            }

            // Check remaining daily loss capacity
            if (_options.DailyLossLimit > 0)
            {
                var remaining = _options.DailyLossLimit + _dailyPnL;
                if (remaining <= 0)
                {
                    var result = new ProtectedActionResult
                    {
                        IsAllowed = false,
                        BlockReason = "Daily loss limit reached",
                        RiskLevel = RiskLevel.VeryHigh
                    };
                    OnActionBlocked?.Invoke(new ActionBlockedEvent { Reason = result.BlockReason });
                    return result;
                }
            }
        }

        return new ProtectedActionResult { IsAllowed = true };
    }

    private Vector<double> ApplyPositionLimits(Vector<double> action, double currentPrice)
    {
        if (_options.MaxPositionPercent <= 0)
            return action;

        // Assume action[0] represents position size/direction
        if (action.Length == 0)
            return action;

        var maxPositionValue = _options.TotalCapital * (double)_options.MaxPositionPercent;
        var maxShares = maxPositionValue / currentPrice;

        var adjustedData = new double[action.Length];
        for (int i = 0; i < action.Length; i++)
        {
            adjustedData[i] = action[i];
        }

        // Clamp position size
        adjustedData[0] = Math.Clamp(adjustedData[0], -maxShares, maxShares);

        return new Vector<double>(adjustedData);
    }

    private ProtectedActionResult EvaluateActionRisk(Vector<double> action, double currentPrice)
    {
        var warnings = new List<string>();
        var riskLevel = RiskLevel.Low;

        if (action.Length == 0)
        {
            return new ProtectedActionResult
            {
                IsAllowed = true,
                Action = action,
                RiskLevel = RiskLevel.Low
            };
        }

        // Check action magnitude
        var actionMagnitude = Math.Abs(action[0]);

        // Large position warning
        if (_options.TotalCapital > 0)
        {
            var positionValue = actionMagnitude * currentPrice;
            var positionPercent = positionValue / _options.TotalCapital;

            if (positionPercent > 0.2)
            {
                warnings.Add($"Large position: {positionPercent:P0} of portfolio");
                riskLevel = RiskLevel.High;
            }
            else if (positionPercent > 0.1)
            {
                warnings.Add($"Significant position: {positionPercent:P0} of portfolio");
                riskLevel = RiskLevel.Medium;
            }
        }

        // Check if we're near loss limits
        lock (_stateLock)
        {
            if (_options.DailyLossLimit > 0)
            {
                var remainingPercent = (_options.DailyLossLimit + _dailyPnL) / _options.DailyLossLimit;
                if (remainingPercent < 0.2)
                {
                    warnings.Add($"Near daily loss limit: {remainingPercent:P0} remaining");
                    riskLevel = riskLevel == RiskLevel.Low ? RiskLevel.Medium : RiskLevel.High;
                }
            }

            if (_consecutiveLosses >= 2)
            {
                warnings.Add($"{_consecutiveLosses} consecutive losses");
                riskLevel = riskLevel == RiskLevel.Low ? RiskLevel.Medium : riskLevel;
            }
        }

        return new ProtectedActionResult
        {
            IsAllowed = true,
            Action = action,
            RiskLevel = riskLevel,
            RiskWarnings = warnings
        };
    }

    private void ResetPeriodsIfNeeded()
    {
        lock (_stateLock)
        {
            var now = DateTime.UtcNow;

            // Daily reset
            if (now.Date > _lastResetDate)
            {
                _dailyPnL = 0;
                _consecutiveLosses = 0;
                _lastResetDate = now.Date;
            }

            // Weekly reset
            var startOfWeek = GetStartOfWeek(now);
            if (startOfWeek > _lastWeekResetDate)
            {
                _weeklyPnL = 0;
                _lastWeekResetDate = startOfWeek;
            }

            // Monthly reset
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            if (startOfMonth > _lastMonthResetDate)
            {
                _monthlyPnL = 0;
                _lastMonthResetDate = startOfMonth;
            }
        }
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static bool VectorsEqual(Vector<double> a, Vector<double> b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > 1e-10) return false;
        }
        return true;
    }
}

/// <summary>
/// Builder for creating risk guards.
/// </summary>
public sealed class RiskGuardBuilder
{
    private readonly RiskGuardOptions _options = new();
    private TradingAgentResult? _tradingAgent;
    private PortfolioAgentResult? _portfolioAgent;
    private HierarchicalAgentOrchestrator? _hierarchicalAgent;

    internal RiskGuardBuilder() { }

    /// <summary>
    /// Sets the total capital for position sizing calculations.
    /// </summary>
    public RiskGuardBuilder WithTotalCapital(double capital)
    {
        _options.TotalCapital = capital;
        return this;
    }

    /// <summary>
    /// Sets the maximum daily loss limit.
    /// </summary>
    public RiskGuardBuilder WithDailyLossLimit(double limit)
    {
        _options.DailyLossLimit = limit;
        return this;
    }

    /// <summary>
    /// Sets the maximum weekly loss limit.
    /// </summary>
    public RiskGuardBuilder WithWeeklyLossLimit(double limit)
    {
        _options.WeeklyLossLimit = limit;
        return this;
    }

    /// <summary>
    /// Sets the maximum monthly loss limit.
    /// </summary>
    public RiskGuardBuilder WithMonthlyLossLimit(double limit)
    {
        _options.MonthlyLossLimit = limit;
        return this;
    }

    /// <summary>
    /// Sets the maximum position size as a percentage of capital.
    /// </summary>
    public RiskGuardBuilder WithPositionLimit(decimal maxPercent)
    {
        _options.MaxPositionPercent = maxPercent;
        return this;
    }

    /// <summary>
    /// Sets the volatility level that triggers a circuit breaker.
    /// </summary>
    public RiskGuardBuilder WithVolatilityBreaker(double vixThreshold)
    {
        _options.VolatilityBreaker = vixThreshold;
        return this;
    }

    /// <summary>
    /// Sets the maximum consecutive losses before circuit breaker.
    /// </summary>
    public RiskGuardBuilder WithMaxConsecutiveLosses(int count)
    {
        _options.MaxConsecutiveLosses = count;
        return this;
    }

    /// <summary>
    /// Sets the circuit breaker cooldown period.
    /// </summary>
    public RiskGuardBuilder WithCircuitBreakerCooldown(TimeSpan duration)
    {
        _options.CircuitBreakerCooldown = duration;
        return this;
    }

    /// <summary>
    /// Attaches a trading agent to protect.
    /// </summary>
    public RiskGuardBuilder ForAgent(TradingAgentResult agent)
    {
        _tradingAgent = agent;
        return this;
    }

    /// <summary>
    /// Attaches a portfolio agent to protect.
    /// </summary>
    public RiskGuardBuilder ForPortfolioAgent(PortfolioAgentResult agent)
    {
        _portfolioAgent = agent;
        return this;
    }

    /// <summary>
    /// Attaches a hierarchical agent orchestrator to protect.
    /// </summary>
    public RiskGuardBuilder ForHierarchicalAgent(HierarchicalAgentOrchestrator orchestrator)
    {
        _hierarchicalAgent = orchestrator;
        return this;
    }

    /// <summary>
    /// Builds the risk guard.
    /// </summary>
    public AgentRiskGuard Build()
    {
        if (_tradingAgent is null && _portfolioAgent is null && _hierarchicalAgent is null)
        {
            throw new InvalidOperationException("Must specify an agent with ForAgent(), ForPortfolioAgent(), or ForHierarchicalAgent().");
        }

        return new AgentRiskGuard(_options, _tradingAgent, _portfolioAgent, _hierarchicalAgent);
    }
}

#region Types

/// <summary>
/// Options for the risk guard.
/// </summary>
public sealed class RiskGuardOptions
{
    public double TotalCapital { get; set; } = 100000;
    public double DailyLossLimit { get; set; }
    public double WeeklyLossLimit { get; set; }
    public double MonthlyLossLimit { get; set; }
    public decimal MaxPositionPercent { get; set; } = 0.1m;
    public double VolatilityBreaker { get; set; }
    public int MaxConsecutiveLosses { get; set; } = 5;
    public TimeSpan CircuitBreakerCooldown { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Result from getting a protected action.
/// </summary>
public sealed class ProtectedActionResult
{
    public bool IsAllowed { get; init; }
    public Vector<double>? Action { get; init; }
    public Vector<double>? OriginalAction { get; init; }
    public bool WasModified { get; init; }
    public string? BlockReason { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public IReadOnlyList<string> RiskWarnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Current status of the risk guard.
/// </summary>
public sealed class RiskGuardStatus
{
    public bool IsTradingAllowed { get; init; }
    public bool IsCircuitBreakerTripped { get; init; }
    public DateTime? CircuitBreakerResetsAt { get; init; }
    public DateTime? CoolingOffUntil { get; init; }
    public double DailyPnL { get; init; }
    public double WeeklyPnL { get; init; }
    public double MonthlyPnL { get; init; }
    public int ConsecutiveLosses { get; init; }
    public double DailyLossLimitRemaining { get; init; }
}

/// <summary>
/// Event when circuit breaker is tripped.
/// </summary>
public sealed class CircuitBreakerEvent
{
    public string Reason { get; init; } = string.Empty;
    public DateTime TrippedAt { get; init; }
    public DateTime ResetsAt { get; init; }
    public double DailyPnL { get; init; }
}

/// <summary>
/// Event when an action is blocked.
/// </summary>
public sealed class ActionBlockedEvent
{
    public string Reason { get; init; } = string.Empty;
    public DateTime BlockedAt { get; init; } = DateTime.UtcNow;
}

#endregion
