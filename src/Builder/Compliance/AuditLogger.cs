using System.Collections.Concurrent;
using OoplesFinance.StockIndicators.Builder.Trading;

namespace OoplesFinance.StockIndicators.Builder.Compliance;

/// <summary>
/// Audit logger for trading compliance.
/// Records all trading activities for regulatory and compliance purposes.
/// </summary>
public sealed class AuditLogger : IDisposable
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private readonly IAuditStore? _persistentStore;
    private readonly int _maxInMemoryEntries;
    private readonly Timer? _flushTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new audit logger with optional persistent storage.
    /// </summary>
    /// <param name="persistentStore">Optional persistent store for audit entries.</param>
    /// <param name="maxInMemoryEntries">Max entries to keep in memory.</param>
    /// <param name="flushIntervalSeconds">Interval for flushing to persistent store.</param>
    public AuditLogger(
        IAuditStore? persistentStore = null,
        int maxInMemoryEntries = 10000,
        int flushIntervalSeconds = 60)
    {
        _persistentStore = persistentStore;
        _maxInMemoryEntries = maxInMemoryEntries;

        if (persistentStore != null && flushIntervalSeconds > 0)
        {
            _flushTimer = new Timer(
                FlushCallback,
                null,
                TimeSpan.FromSeconds(flushIntervalSeconds),
                TimeSpan.FromSeconds(flushIntervalSeconds));
        }
    }

    /// <summary>
    /// Logs an order submission.
    /// </summary>
    public void LogOrderSubmission(ExtendedTradeRequest request, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.OrderSubmitted,
            Symbol = request.Symbol,
            Action = request.Action.ToString(),
            Quantity = request.Quantity,
            OrderType = request.OrderType.ToString(),
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice,
            UserId = userId,
            Details = $"TIF: {request.TimeInForce}, StopLoss: {request.StopLossPrice}, TakeProfit: {request.TakeProfitPrice}"
        });
    }

    /// <summary>
    /// Logs an order fill.
    /// </summary>
    public void LogOrderFill(BrokerOrder order, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.OrderFilled,
            OrderId = order.OrderId,
            Symbol = order.Symbol,
            Action = order.Side,
            Quantity = order.FilledQuantity,
            Price = order.AverageFillPrice,
            UserId = userId,
            Details = $"RequestedQty: {order.Quantity}, FilledQty: {order.FilledQuantity}"
        });
    }

    /// <summary>
    /// Logs an order cancellation.
    /// </summary>
    public void LogOrderCancellation(string orderId, string reason, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.OrderCancelled,
            OrderId = orderId,
            UserId = userId,
            Details = reason
        });
    }

    /// <summary>
    /// Logs an order rejection.
    /// </summary>
    public void LogOrderRejection(string orderId, string reason, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.OrderRejected,
            OrderId = orderId,
            UserId = userId,
            Details = reason
        });
    }

    /// <summary>
    /// Logs a position close.
    /// </summary>
    public void LogPositionClose(string symbol, decimal quantity, decimal? pnl, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.PositionClosed,
            Symbol = symbol,
            Quantity = quantity,
            Price = pnl,
            UserId = userId,
            Details = $"P&L: {pnl:C2}"
        });
    }

    /// <summary>
    /// Logs a risk limit being triggered.
    /// </summary>
    public void LogRiskLimitTriggered(string limitType, decimal currentValue, decimal limitValue, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.RiskLimitTriggered,
            UserId = userId,
            Details = $"{limitType}: Current={currentValue:F4}, Limit={limitValue:F4}"
        });
    }

    /// <summary>
    /// Logs emergency stop activation.
    /// </summary>
    public void LogEmergencyStop(string reason, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.EmergencyStop,
            UserId = userId,
            Details = reason
        });
    }

    /// <summary>
    /// Logs a compliance violation.
    /// </summary>
    public void LogComplianceViolation(string violationType, string details, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.ComplianceViolation,
            UserId = userId,
            Details = $"{violationType}: {details}"
        });
    }

    /// <summary>
    /// Logs a configuration change.
    /// </summary>
    public void LogConfigChange(string setting, string oldValue, string newValue, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.ConfigurationChanged,
            UserId = userId,
            Details = $"Setting '{setting}' changed from '{oldValue}' to '{newValue}'"
        });
    }

    /// <summary>
    /// Logs a login event.
    /// </summary>
    public void LogLogin(string userId, bool success, string? failureReason = null)
    {
        Log(new AuditEntry
        {
            EventType = success ? AuditEventType.Login : AuditEventType.LoginFailed,
            UserId = userId,
            Details = failureReason
        });
    }

    /// <summary>
    /// Logs a custom event.
    /// </summary>
    public void LogCustomEvent(string eventName, string details, string? userId = null)
    {
        Log(new AuditEntry
        {
            EventType = AuditEventType.Custom,
            UserId = userId,
            Details = $"{eventName}: {details}"
        });
    }

    /// <summary>
    /// Gets audit trail for a date range.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetAuditTrail(DateTime start, DateTime end)
    {
        return _entries
            .Where(e => e.Timestamp >= start && e.Timestamp <= end)
            .OrderBy(e => e.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Gets audit trail for a specific order.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetOrderAuditTrail(string orderId)
    {
        return _entries
            .Where(e => e.OrderId == orderId)
            .OrderBy(e => e.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Gets audit trail for a specific symbol.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetSymbolAuditTrail(string symbol, DateTime? start = null, DateTime? end = null)
    {
        var query = _entries.Where(e => e.Symbol == symbol);

        if (start.HasValue)
            query = query.Where(e => e.Timestamp >= start.Value);
        if (end.HasValue)
            query = query.Where(e => e.Timestamp <= end.Value);

        return query.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// Gets all entries of a specific type.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetEntriesByType(AuditEventType eventType, DateTime? start = null, DateTime? end = null)
    {
        var query = _entries.Where(e => e.EventType == eventType);

        if (start.HasValue)
            query = query.Where(e => e.Timestamp >= start.Value);
        if (end.HasValue)
            query = query.Where(e => e.Timestamp <= end.Value);

        return query.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// Gets entries for a specific user.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetUserAuditTrail(string userId, DateTime? start = null, DateTime? end = null)
    {
        var query = _entries.Where(e => e.UserId == userId);

        if (start.HasValue)
            query = query.Where(e => e.Timestamp >= start.Value);
        if (end.HasValue)
            query = query.Where(e => e.Timestamp <= end.Value);

        return query.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// Generates a compliance report for a date range.
    /// </summary>
    public ComplianceReport GenerateReport(DateTime start, DateTime end)
    {
        var entries = GetAuditTrail(start, end);

        return new ComplianceReport
        {
            StartDate = start,
            EndDate = end,
            TotalOrders = entries.Count(e => e.EventType == AuditEventType.OrderSubmitted),
            TotalFills = entries.Count(e => e.EventType == AuditEventType.OrderFilled),
            TotalCancellations = entries.Count(e => e.EventType == AuditEventType.OrderCancelled),
            TotalRejections = entries.Count(e => e.EventType == AuditEventType.OrderRejected),
            RiskLimitTriggers = entries.Count(e => e.EventType == AuditEventType.RiskLimitTriggered),
            ComplianceViolations = entries.Count(e => e.EventType == AuditEventType.ComplianceViolation),
            EmergencyStops = entries.Count(e => e.EventType == AuditEventType.EmergencyStop),
            UniqueSymbols = entries.Where(e => !string.IsNullOrEmpty(e.Symbol)).Select(e => e.Symbol!).Distinct().ToList(),
            Entries = entries
        };
    }

    private void Log(AuditEntry entry)
    {
        entry.EntryId = Guid.NewGuid().ToString();
        entry.Timestamp = DateTime.UtcNow;

        _entries.Enqueue(entry);

        // Trim if exceeding max entries
        while (_entries.Count > _maxInMemoryEntries && _entries.TryDequeue(out _)) { }
    }

    private void FlushCallback(object? state)
    {
        if (_persistentStore == null) return;

        var entriesToFlush = new List<AuditEntry>();
        while (_entries.TryDequeue(out var entry))
        {
            entriesToFlush.Add(entry);
        }

        if (entriesToFlush.Count > 0)
        {
            _persistentStore.StoreEntriesAsync(entriesToFlush).Wait();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _flushTimer?.Dispose();
            FlushCallback(null);
            _disposed = true;
        }
    }
}

/// <summary>
/// Audit entry types.
/// </summary>
public enum AuditEventType
{
    /// <summary>Order submitted.</summary>
    OrderSubmitted,

    /// <summary>Order filled.</summary>
    OrderFilled,

    /// <summary>Order partially filled.</summary>
    OrderPartiallyFilled,

    /// <summary>Order cancelled.</summary>
    OrderCancelled,

    /// <summary>Order rejected.</summary>
    OrderRejected,

    /// <summary>Order modified.</summary>
    OrderModified,

    /// <summary>Position opened.</summary>
    PositionOpened,

    /// <summary>Position closed.</summary>
    PositionClosed,

    /// <summary>Risk limit triggered.</summary>
    RiskLimitTriggered,

    /// <summary>Emergency stop activated.</summary>
    EmergencyStop,

    /// <summary>Compliance violation.</summary>
    ComplianceViolation,

    /// <summary>Configuration changed.</summary>
    ConfigurationChanged,

    /// <summary>Login success.</summary>
    Login,

    /// <summary>Login failed.</summary>
    LoginFailed,

    /// <summary>System started.</summary>
    SystemStart,

    /// <summary>System stopped.</summary>
    SystemStop,

    /// <summary>Custom event.</summary>
    Custom
}

/// <summary>
/// A single audit entry.
/// </summary>
public sealed class AuditEntry
{
    /// <summary>Gets or sets the entry ID.</summary>
    public string EntryId { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the event type.</summary>
    public AuditEventType EventType { get; set; }

    /// <summary>Gets or sets the order ID (if applicable).</summary>
    public string? OrderId { get; set; }

    /// <summary>Gets or sets the symbol (if applicable).</summary>
    public string? Symbol { get; set; }

    /// <summary>Gets or sets the action/side.</summary>
    public string? Action { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal? Price { get; set; }

    /// <summary>Gets or sets the order type.</summary>
    public string? OrderType { get; set; }

    /// <summary>Gets or sets the limit price.</summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>Gets or sets the stop price.</summary>
    public decimal? StopPrice { get; set; }

    /// <summary>Gets or sets the user ID.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets additional details.</summary>
    public string? Details { get; set; }

    /// <summary>Gets or sets the IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets metadata.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Compliance report for a date range.
/// </summary>
public sealed class ComplianceReport
{
    /// <summary>Gets or sets the report start date.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Gets or sets the report end date.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Gets or sets the total orders submitted.</summary>
    public int TotalOrders { get; set; }

    /// <summary>Gets or sets the total fills.</summary>
    public int TotalFills { get; set; }

    /// <summary>Gets or sets the total cancellations.</summary>
    public int TotalCancellations { get; set; }

    /// <summary>Gets or sets the total rejections.</summary>
    public int TotalRejections { get; set; }

    /// <summary>Gets or sets risk limit triggers.</summary>
    public int RiskLimitTriggers { get; set; }

    /// <summary>Gets or sets compliance violations.</summary>
    public int ComplianceViolations { get; set; }

    /// <summary>Gets or sets emergency stop count.</summary>
    public int EmergencyStops { get; set; }

    /// <summary>Gets or sets unique symbols traded.</summary>
    public IReadOnlyList<string> UniqueSymbols { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets all entries in the report period.</summary>
    public IReadOnlyList<AuditEntry> Entries { get; set; } = Array.Empty<AuditEntry>();

    /// <summary>Gets the fill rate.</summary>
    public decimal FillRate => TotalOrders > 0 ? (decimal)TotalFills / TotalOrders : 0m;
}

/// <summary>
/// Interface for persistent audit storage.
/// </summary>
public interface IAuditStore
{
    /// <summary>Stores audit entries.</summary>
    Task StoreEntriesAsync(IReadOnlyList<AuditEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>Retrieves entries for a date range.</summary>
    Task<IReadOnlyList<AuditEntry>> GetEntriesAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
}
