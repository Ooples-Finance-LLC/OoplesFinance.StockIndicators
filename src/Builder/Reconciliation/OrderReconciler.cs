namespace OoplesFinance.StockIndicators.Builder.Reconciliation;

using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Builder.Trading.Orders;

/// <summary>
/// Reconciles order states between local tracking and broker.
/// Handles order state recovery after disconnections.
/// </summary>
public sealed class OrderReconciler
{
    private readonly IBroker _broker;
    private readonly Dictionary<string, TrackedOrder> _trackedOrders = new();
    private readonly object _lockObj = new();
    private readonly Action<OrderReconciliationEvent>? _onReconciliationEvent;

    /// <summary>Gets the number of tracked orders.</summary>
    public int TrackedOrderCount
    {
        get { lock (_lockObj) { return _trackedOrders.Count; } }
    }

    /// <summary>
    /// Creates a new order reconciler.
    /// </summary>
    public OrderReconciler(
        IBroker broker,
        Action<OrderReconciliationEvent>? onReconciliationEvent = null)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _onReconciliationEvent = onReconciliationEvent;
    }

    /// <summary>
    /// Tracks a new order for reconciliation.
    /// </summary>
    public void TrackOrder(string orderId, TradeRequest request)
    {
        lock (_lockObj)
        {
            _trackedOrders[orderId] = new TrackedOrder
            {
                OrderId = orderId,
                Request = request,
                LastKnownStatus = BrokerOrderStatus.New,
                SubmittedAt = DateTime.UtcNow,
                LastCheckedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Updates tracked order status.
    /// </summary>
    public void UpdateOrderStatus(string orderId, BrokerOrderStatus status, decimal? filledQuantity = null)
    {
        lock (_lockObj)
        {
            if (_trackedOrders.TryGetValue(orderId, out var tracked))
            {
                tracked.LastKnownStatus = status;
                tracked.LastCheckedAt = DateTime.UtcNow;

                if (filledQuantity.HasValue)
                {
                    tracked.FilledQuantity = filledQuantity.Value;
                }

                if (IsTerminalStatus(status))
                {
                    tracked.CompletedAt = DateTime.UtcNow;
                }
            }
        }
    }

    /// <summary>
    /// Reconciles all tracked orders with the broker.
    /// </summary>
    public async Task<ReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var result = new ReconciliationResult { StartedAt = DateTime.UtcNow };

        IReadOnlyList<TrackedOrder> ordersToCheck;
        lock (_lockObj)
        {
            ordersToCheck = _trackedOrders.Values
                .Where(o => !IsTerminalStatus(o.LastKnownStatus))
                .ToList();
        }

        foreach (var tracked in ordersToCheck)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ReconcileSingleOrderAsync(tracked, result, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ReconciliationError
                {
                    OrderId = tracked.OrderId,
                    Error = ex.Message,
                    Exception = ex
                });
            }
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Gets orders that need attention (stuck, orphaned, etc.).
    /// </summary>
    public IReadOnlyList<TrackedOrder> GetOrdersNeedingAttention(TimeSpan stuckThreshold)
    {
        lock (_lockObj)
        {
            var cutoff = DateTime.UtcNow - stuckThreshold;

            return _trackedOrders.Values
                .Where(o => !IsTerminalStatus(o.LastKnownStatus) &&
                           o.SubmittedAt < cutoff)
                .ToList();
        }
    }

    /// <summary>
    /// Cleans up completed orders older than the specified age.
    /// </summary>
    public int CleanupCompletedOrders(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var removed = 0;

        lock (_lockObj)
        {
            var toRemove = _trackedOrders.Values
                .Where(o => IsTerminalStatus(o.LastKnownStatus) &&
                           o.CompletedAt.HasValue &&
                           o.CompletedAt.Value < cutoff)
                .Select(o => o.OrderId)
                .ToList();

            foreach (var orderId in toRemove)
            {
                _trackedOrders.Remove(orderId);
                removed++;
            }
        }

        return removed;
    }

    private async Task ReconcileSingleOrderAsync(
        TrackedOrder tracked,
        ReconciliationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var brokerOrder = await _broker.GetOrderAsync(tracked.OrderId, cancellationToken)
                .ConfigureAwait(false);

            if (brokerOrder.Status != tracked.LastKnownStatus)
            {
                result.StatusMismatches.Add(new StatusMismatch
                {
                    OrderId = tracked.OrderId,
                    LocalStatus = tracked.LastKnownStatus,
                    BrokerStatus = brokerOrder.Status
                });

                UpdateOrderStatus(tracked.OrderId, brokerOrder.Status, brokerOrder.FilledQuantity);

                RaiseEvent(new OrderReconciliationEvent
                {
                    Type = ReconciliationEventType.StatusCorrected,
                    OrderId = tracked.OrderId,
                    Details = $"Status corrected from {tracked.LastKnownStatus} to {brokerOrder.Status}"
                });
            }

            result.ReconcileSuccessCount++;
        }
        catch (Exception)
        {
            result.OrphanedOrders.Add(tracked.OrderId);

            RaiseEvent(new OrderReconciliationEvent
            {
                Type = ReconciliationEventType.OrphanedOrder,
                OrderId = tracked.OrderId,
                Details = "Order not found in broker"
            });
        }
    }

    private void RaiseEvent(OrderReconciliationEvent evt)
    {
        evt.Timestamp = DateTime.UtcNow;
        _onReconciliationEvent?.Invoke(evt);
    }

    private static bool IsTerminalStatus(BrokerOrderStatus status)
    {
        return status == BrokerOrderStatus.Filled ||
               status == BrokerOrderStatus.Cancelled ||
               status == BrokerOrderStatus.Rejected ||
               status == BrokerOrderStatus.Expired;
    }
}

/// <summary>
/// Tracked order for reconciliation.
/// </summary>
public sealed class TrackedOrder
{
    public string OrderId { get; set; } = string.Empty;
    public TradeRequest Request { get; set; } = null!;
    public BrokerOrderStatus LastKnownStatus { get; set; }
    public decimal FilledQuantity { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime LastCheckedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Result of reconciliation.
/// </summary>
public sealed class ReconciliationResult
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int ReconcileSuccessCount { get; set; }
    public List<StatusMismatch> StatusMismatches { get; set; } = new();
    public List<RecoveredOrder> RecoveredOrders { get; set; } = new();
    public List<string> OrphanedOrders { get; set; } = new();
    public List<BrokerOrder> UnknownOrders { get; set; } = new();
    public List<ReconciliationError> Errors { get; set; } = new();

    public bool HasIssues =>
        StatusMismatches.Count > 0 ||
        OrphanedOrders.Count > 0 ||
        UnknownOrders.Count > 0 ||
        Errors.Count > 0;
}

/// <summary>
/// Status mismatch between local and broker.
/// </summary>
public sealed class StatusMismatch
{
    public string OrderId { get; set; } = string.Empty;
    public BrokerOrderStatus LocalStatus { get; set; }
    public BrokerOrderStatus BrokerStatus { get; set; }
}

/// <summary>
/// Recovered order information.
/// </summary>
public sealed class RecoveredOrder
{
    public string OrderId { get; set; } = string.Empty;
    public BrokerOrderStatus FinalStatus { get; set; }
    public decimal FilledQuantity { get; set; }
}

/// <summary>
/// Reconciliation error.
/// </summary>
public sealed class ReconciliationError
{
    public string OrderId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}

/// <summary>
/// Order reconciliation event.
/// </summary>
public sealed class OrderReconciliationEvent
{
    public ReconciliationEventType Type { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Reconciliation event types.
/// </summary>
public enum ReconciliationEventType
{
    StatusCorrected,
    OrderRecovered,
    OrphanedOrder,
    UnknownOrderFound
}

/// <summary>
/// Reconciles positions between local tracking and broker.
/// </summary>
public sealed class PositionReconciler
{
    private readonly IBroker _broker;
    private readonly Dictionary<string, LocalPosition> _localPositions = new();
    private readonly object _lockObj = new();
    private readonly Action<PositionReconciliationEvent>? _onEvent;

    /// <summary>
    /// Creates a new position reconciler.
    /// </summary>
    public PositionReconciler(
        IBroker broker,
        Action<PositionReconciliationEvent>? onEvent = null)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _onEvent = onEvent;
    }

    /// <summary>
    /// Updates local position tracking.
    /// </summary>
    public void UpdateLocalPosition(string symbol, decimal quantity, decimal avgCost)
    {
        lock (_lockObj)
        {
            if (quantity == 0)
            {
                _localPositions.Remove(symbol);
            }
            else
            {
                _localPositions[symbol] = new LocalPosition
                {
                    Symbol = symbol,
                    Quantity = quantity,
                    AverageCost = avgCost,
                    LastUpdated = DateTime.UtcNow
                };
            }
        }
    }

    /// <summary>
    /// Reconciles positions with broker.
    /// </summary>
    public async Task<PositionReconciliationResult> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new PositionReconciliationResult { StartedAt = DateTime.UtcNow };

        // Get broker positions
        var brokerPositions = await _broker.GetPositionsAsync(cancellationToken).ConfigureAwait(false);
        var brokerPositionDict = brokerPositions.ToDictionary(p => p.Symbol);

        IReadOnlyDictionary<string, LocalPosition> localSnapshot;
        lock (_lockObj)
        {
            localSnapshot = _localPositions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        // Check each local position against broker
        foreach (var kvp in localSnapshot)
        {
            var symbol = kvp.Key;
            var local = kvp.Value;

            if (brokerPositionDict.TryGetValue(symbol, out var brokerPos))
            {
                // Position exists - check for quantity mismatch
                if (Math.Abs(local.Quantity - brokerPos.Quantity) > 0.0001m)
                {
                    result.QuantityMismatches.Add(new PositionMismatch
                    {
                        Symbol = symbol,
                        LocalQuantity = local.Quantity,
                        BrokerQuantity = brokerPos.Quantity,
                        Difference = brokerPos.Quantity - local.Quantity
                    });

                    RaiseEvent(new PositionReconciliationEvent
                    {
                        Type = PositionEventType.QuantityMismatch,
                        Symbol = symbol,
                        Details = $"Local: {local.Quantity}, Broker: {brokerPos.Quantity}"
                    });
                }
            }
            else
            {
                // Position in local but not in broker
                result.MissingInBroker.Add(symbol);

                RaiseEvent(new PositionReconciliationEvent
                {
                    Type = PositionEventType.MissingInBroker,
                    Symbol = symbol,
                    Details = $"Local position {local.Quantity} not found in broker"
                });
            }
        }

        // Check for broker positions not in local
        foreach (var brokerPos in brokerPositions)
        {
            if (!localSnapshot.ContainsKey(brokerPos.Symbol))
            {
                result.MissingInLocal.Add(brokerPos);

                RaiseEvent(new PositionReconciliationEvent
                {
                    Type = PositionEventType.MissingInLocal,
                    Symbol = brokerPos.Symbol,
                    Details = $"Broker position {brokerPos.Quantity} not tracked locally"
                });
            }
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Syncs local positions from broker (overwrites local with broker data).
    /// </summary>
    public async Task SyncFromBrokerAsync(CancellationToken cancellationToken = default)
    {
        var brokerPositions = await _broker.GetPositionsAsync(cancellationToken).ConfigureAwait(false);

        lock (_lockObj)
        {
            _localPositions.Clear();
            foreach (var pos in brokerPositions)
            {
                _localPositions[pos.Symbol] = new LocalPosition
                {
                    Symbol = pos.Symbol,
                    Quantity = pos.Quantity,
                    AverageCost = pos.AverageEntryPrice,
                    LastUpdated = DateTime.UtcNow
                };
            }
        }
    }

    private void RaiseEvent(PositionReconciliationEvent evt)
    {
        evt.Timestamp = DateTime.UtcNow;
        _onEvent?.Invoke(evt);
    }
}

/// <summary>
/// Local position tracking.
/// </summary>
public sealed class LocalPosition
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Position reconciliation result.
/// </summary>
public sealed class PositionReconciliationResult
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<PositionMismatch> QuantityMismatches { get; set; } = new();
    public List<string> MissingInBroker { get; set; } = new();
    public List<BrokerPosition> MissingInLocal { get; set; } = new();

    public bool HasDiscrepancies =>
        QuantityMismatches.Count > 0 ||
        MissingInBroker.Count > 0 ||
        MissingInLocal.Count > 0;
}

/// <summary>
/// Position quantity mismatch.
/// </summary>
public sealed class PositionMismatch
{
    public string Symbol { get; set; } = string.Empty;
    public decimal LocalQuantity { get; set; }
    public decimal BrokerQuantity { get; set; }
    public decimal Difference { get; set; }
}

/// <summary>
/// Position reconciliation event.
/// </summary>
public sealed class PositionReconciliationEvent
{
    public PositionEventType Type { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Position event types.
/// </summary>
public enum PositionEventType
{
    QuantityMismatch,
    MissingInBroker,
    MissingInLocal
}
