using OoplesFinance.StockIndicators.Builder.MarketData;

namespace OoplesFinance.StockIndicators.Builder.Trading.Algorithms;

/// <summary>
/// Interface for execution algorithms that split large orders into smaller pieces.
/// </summary>
public interface IExecutionAlgorithm
{
    /// <summary>Gets the algorithm name.</summary>
    string Name { get; }

    /// <summary>Gets the algorithm description.</summary>
    string Description { get; }

    /// <summary>
    /// Executes the algorithm to fill an order.
    /// </summary>
    /// <param name="order">The order to execute.</param>
    /// <param name="marketData">Market data provider.</param>
    /// <param name="broker">Broker for order submission.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<AlgorithmExecutionResult> ExecuteAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        IBroker broker,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the execution schedule (child orders to submit).
    /// </summary>
    /// <param name="order">The parent order.</param>
    /// <param name="marketData">Market data provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scheduled child orders.</returns>
    Task<IReadOnlyList<ScheduledOrder>> GetScheduleAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Order to be executed by an algorithm.
/// </summary>
public sealed class AlgorithmOrder
{
    /// <summary>Gets or sets the order ID.</summary>
    public string OrderId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the order side.</summary>
    public Orders.OrderSide Side { get; set; }

    /// <summary>Gets or sets the total quantity to fill.</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>Gets or sets the limit price (optional).</summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>Gets or sets the start time.</summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the end time (for TWAP/VWAP).</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets the execution duration.</summary>
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// A scheduled child order to be submitted.
/// </summary>
public sealed class ScheduledOrder
{
    /// <summary>Gets or sets the scheduled execution time.</summary>
    public DateTime ScheduledTime { get; set; }

    /// <summary>Gets or sets the quantity for this slice.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the limit price (optional).</summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>Gets or sets whether this order has been submitted.</summary>
    public bool IsSubmitted { get; set; }

    /// <summary>Gets or sets the broker order ID after submission.</summary>
    public string? BrokerOrderId { get; set; }

    /// <summary>Gets or sets the fill quantity.</summary>
    public decimal FilledQuantity { get; set; }

    /// <summary>Gets or sets the average fill price.</summary>
    public decimal? AverageFillPrice { get; set; }
}

/// <summary>
/// Result of algorithm execution.
/// </summary>
public sealed class AlgorithmExecutionResult
{
    /// <summary>Gets or sets whether execution completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the total filled quantity.</summary>
    public decimal TotalFilledQuantity { get; set; }

    /// <summary>Gets or sets the average execution price (VWAP of fills).</summary>
    public decimal AveragePrice { get; set; }

    /// <summary>Gets or sets the total value executed.</summary>
    public decimal TotalValue { get; set; }

    /// <summary>Gets or sets the number of child orders.</summary>
    public int ChildOrderCount { get; set; }

    /// <summary>Gets or sets the child order results.</summary>
    public IReadOnlyList<ChildOrderResult> ChildOrders { get; set; } = Array.Empty<ChildOrderResult>();

    /// <summary>Gets or sets any error message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the execution start time.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the execution end time.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets the execution duration.</summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>Gets the fill rate (filled / requested).</summary>
    public decimal FillRate { get; set; }

    /// <summary>Gets the slippage from limit price (if any).</summary>
    public decimal? Slippage { get; set; }
}

/// <summary>
/// Result of a child order execution.
/// </summary>
public sealed class ChildOrderResult
{
    /// <summary>Gets or sets the order ID.</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested quantity.</summary>
    public decimal RequestedQuantity { get; set; }

    /// <summary>Gets or sets the filled quantity.</summary>
    public decimal FilledQuantity { get; set; }

    /// <summary>Gets or sets the average fill price.</summary>
    public decimal AverageFillPrice { get; set; }

    /// <summary>Gets or sets the submission time.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Gets or sets the fill time.</summary>
    public DateTime? FilledAt { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public BrokerOrderStatus Status { get; set; }
}

/// <summary>
/// TWAP (Time-Weighted Average Price) execution algorithm.
/// Splits order into equal-sized slices spread evenly over time.
/// </summary>
public sealed class TwapAlgorithm : IExecutionAlgorithm
{
    private readonly TwapOptions _options;
    private readonly Random _random;

    /// <summary>
    /// Creates a new TWAP algorithm.
    /// </summary>
    public TwapAlgorithm(TwapOptions? options = null)
    {
        _options = options ?? new TwapOptions();
        _random = new Random();
    }

    /// <inheritdoc />
    public string Name => "TWAP";

    /// <inheritdoc />
    public string Description => "Time-Weighted Average Price - splits order into equal slices over time";

    /// <inheritdoc />
    public async Task<AlgorithmExecutionResult> ExecuteAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        IBroker broker,
        CancellationToken cancellationToken = default)
    {
        var result = new AlgorithmExecutionResult
        {
            StartTime = DateTime.UtcNow
        };

        var schedule = await GetScheduleAsync(order, marketData, cancellationToken).ConfigureAwait(false);
        var childResults = new List<ChildOrderResult>();
        var totalFilled = 0m;
        var totalValue = 0m;

        foreach (var scheduledOrder in schedule)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Wait until scheduled time
            var waitTime = scheduledOrder.ScheduledTime - DateTime.UtcNow;
            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
            }

            // Add randomization to avoid detection
            if (_options.RandomizationPercent > 0)
            {
                var randomDelay = _random.Next(0, (int)(_options.RandomizationPercent * 1000));
                await Task.Delay(randomDelay, cancellationToken).ConfigureAwait(false);
            }

            // Submit the child order
            var request = new ExtendedTradeRequest
            {
                Symbol = order.Symbol,
                Action = order.Side == Orders.OrderSide.Buy ? TradeAction.MarketBuy : TradeAction.MarketSell,
                Quantity = scheduledOrder.Quantity,
                OrderType = scheduledOrder.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market,
                LimitPrice = scheduledOrder.LimitPrice,
                TimeInForce = TimeInForce.IOC,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var brokerOrder = await broker.SubmitOrderAsync(request, cancellationToken).ConfigureAwait(false);

                var childResult = new ChildOrderResult
                {
                    OrderId = brokerOrder.OrderId,
                    RequestedQuantity = scheduledOrder.Quantity,
                    FilledQuantity = brokerOrder.FilledQuantity,
                    AverageFillPrice = brokerOrder.AverageFillPrice ?? 0m,
                    SubmittedAt = DateTime.UtcNow,
                    FilledAt = brokerOrder.FilledAt,
                    Status = brokerOrder.Status
                };

                childResults.Add(childResult);

                if (brokerOrder.FilledQuantity > 0 && brokerOrder.AverageFillPrice.HasValue)
                {
                    totalFilled += brokerOrder.FilledQuantity;
                    totalValue += brokerOrder.FilledQuantity * brokerOrder.AverageFillPrice.Value;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }
        }

        result.EndTime = DateTime.UtcNow;
        result.TotalFilledQuantity = totalFilled;
        result.AveragePrice = totalFilled > 0 ? totalValue / totalFilled : 0m;
        result.TotalValue = totalValue;
        result.ChildOrderCount = childResults.Count;
        result.ChildOrders = childResults;
        result.FillRate = order.TotalQuantity > 0 ? totalFilled / order.TotalQuantity : 0m;
        result.Success = totalFilled > 0;

        if (order.LimitPrice.HasValue && result.AveragePrice > 0)
        {
            result.Slippage = Math.Abs(result.AveragePrice - order.LimitPrice.Value);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduledOrder>> GetScheduleAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        CancellationToken cancellationToken = default)
    {
        var schedule = new List<ScheduledOrder>();
        var sliceCount = _options.NumSlices;
        var sliceQuantity = Math.Floor(order.TotalQuantity / sliceCount);
        var remainder = order.TotalQuantity - (sliceQuantity * sliceCount);

        var interval = order.Duration.TotalMilliseconds / sliceCount;

        for (var i = 0; i < sliceCount; i++)
        {
            var qty = sliceQuantity;
            if (i == sliceCount - 1)
            {
                qty += remainder; // Add remainder to last slice
            }

            schedule.Add(new ScheduledOrder
            {
                ScheduledTime = order.StartTime.AddMilliseconds(interval * i),
                Quantity = qty,
                LimitPrice = order.LimitPrice
            });
        }

        return Task.FromResult<IReadOnlyList<ScheduledOrder>>(schedule);
    }
}

/// <summary>
/// Options for TWAP algorithm.
/// </summary>
public sealed class TwapOptions
{
    /// <summary>Gets or sets the number of slices.</summary>
    public int NumSlices { get; set; } = 10;

    /// <summary>Gets or sets the randomization percent (0-1) to avoid detection.</summary>
    public decimal RandomizationPercent { get; set; } = 0.1m;

    /// <summary>Gets or sets whether to use limit orders instead of market.</summary>
    public bool UseLimitOrders { get; set; } = false;

    /// <summary>Gets or sets the limit order offset from mid price (in ticks).</summary>
    public int LimitOrderOffset { get; set; } = 1;
}

/// <summary>
/// VWAP (Volume-Weighted Average Price) execution algorithm.
/// Sizes slices based on historical volume profile to minimize market impact.
/// </summary>
public sealed class VwapAlgorithm : IExecutionAlgorithm
{
    private readonly VwapOptions _options;
    private readonly Random _random;

    /// <summary>
    /// Creates a new VWAP algorithm.
    /// </summary>
    public VwapAlgorithm(VwapOptions? options = null)
    {
        _options = options ?? new VwapOptions();
        _random = new Random();
    }

    /// <inheritdoc />
    public string Name => "VWAP";

    /// <inheritdoc />
    public string Description => "Volume-Weighted Average Price - sizes slices based on historical volume";

    /// <inheritdoc />
    public async Task<AlgorithmExecutionResult> ExecuteAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        IBroker broker,
        CancellationToken cancellationToken = default)
    {
        var result = new AlgorithmExecutionResult
        {
            StartTime = DateTime.UtcNow
        };

        var schedule = await GetScheduleAsync(order, marketData, cancellationToken).ConfigureAwait(false);
        var childResults = new List<ChildOrderResult>();
        var totalFilled = 0m;
        var totalValue = 0m;

        foreach (var scheduledOrder in schedule)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Wait until scheduled time
            var waitTime = scheduledOrder.ScheduledTime - DateTime.UtcNow;
            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
            }

            // Check participation rate
            if (_options.MaxParticipationRate > 0)
            {
                try
                {
                    var currentQuote = await marketData.GetQuoteAsync(order.Symbol, cancellationToken).ConfigureAwait(false);
                    var volumeSinceStart = await EstimateVolumeSinceStartAsync(order, marketData, cancellationToken).ConfigureAwait(false);
                    var maxQuantity = volumeSinceStart * _options.MaxParticipationRate;

                    if (totalFilled >= maxQuantity)
                    {
                        continue; // Skip this slice to stay under participation rate
                    }
                }
                catch
                {
                    // Continue even if volume check fails
                }
            }

            var request = new ExtendedTradeRequest
            {
                Symbol = order.Symbol,
                Action = order.Side == Orders.OrderSide.Buy ? TradeAction.MarketBuy : TradeAction.MarketSell,
                Quantity = scheduledOrder.Quantity,
                OrderType = scheduledOrder.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market,
                LimitPrice = scheduledOrder.LimitPrice,
                TimeInForce = TimeInForce.IOC,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var brokerOrder = await broker.SubmitOrderAsync(request, cancellationToken).ConfigureAwait(false);

                var childResult = new ChildOrderResult
                {
                    OrderId = brokerOrder.OrderId,
                    RequestedQuantity = scheduledOrder.Quantity,
                    FilledQuantity = brokerOrder.FilledQuantity,
                    AverageFillPrice = brokerOrder.AverageFillPrice ?? 0m,
                    SubmittedAt = DateTime.UtcNow,
                    FilledAt = brokerOrder.FilledAt,
                    Status = brokerOrder.Status
                };

                childResults.Add(childResult);

                if (brokerOrder.FilledQuantity > 0 && brokerOrder.AverageFillPrice.HasValue)
                {
                    totalFilled += brokerOrder.FilledQuantity;
                    totalValue += brokerOrder.FilledQuantity * brokerOrder.AverageFillPrice.Value;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }
        }

        result.EndTime = DateTime.UtcNow;
        result.TotalFilledQuantity = totalFilled;
        result.AveragePrice = totalFilled > 0 ? totalValue / totalFilled : 0m;
        result.TotalValue = totalValue;
        result.ChildOrderCount = childResults.Count;
        result.ChildOrders = childResults;
        result.FillRate = order.TotalQuantity > 0 ? totalFilled / order.TotalQuantity : 0m;
        result.Success = totalFilled > 0;

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduledOrder>> GetScheduleAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        CancellationToken cancellationToken = default)
    {
        // Get historical volume profile
        var volumeProfile = await GetVolumeProfileAsync(order, marketData, cancellationToken).ConfigureAwait(false);

        var schedule = new List<ScheduledOrder>();
        var sliceCount = _options.NumSlices;
        var interval = order.Duration.TotalMilliseconds / sliceCount;

        var totalVolume = volumeProfile.Sum();
        if (totalVolume <= 0)
        {
            // Fall back to equal distribution
            var equalQty = Math.Floor(order.TotalQuantity / sliceCount);
            for (var i = 0; i < sliceCount; i++)
            {
                schedule.Add(new ScheduledOrder
                {
                    ScheduledTime = order.StartTime.AddMilliseconds(interval * i),
                    Quantity = i == sliceCount - 1 ? order.TotalQuantity - equalQty * (sliceCount - 1) : equalQty,
                    LimitPrice = order.LimitPrice
                });
            }
            return schedule;
        }

        // Distribute quantity based on volume profile
        var allocated = 0m;
        for (var i = 0; i < sliceCount; i++)
        {
            var volumePercent = volumeProfile[i] / totalVolume;
            var qty = Math.Floor(order.TotalQuantity * (decimal)volumePercent);

            if (i == sliceCount - 1)
            {
                qty = order.TotalQuantity - allocated;
            }

            schedule.Add(new ScheduledOrder
            {
                ScheduledTime = order.StartTime.AddMilliseconds(interval * i),
                Quantity = qty,
                LimitPrice = order.LimitPrice
            });

            allocated += qty;
        }

        return schedule;
    }

    private async Task<double[]> GetVolumeProfileAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        CancellationToken cancellationToken)
    {
        // Get historical bars for volume profile
        var lookbackEnd = order.StartTime.Date;
        var lookbackStart = lookbackEnd.AddDays(-_options.LookbackDays);

        try
        {
            var bars = await marketData.GetHistoricalBarsAsync(
                order.Symbol,
                lookbackStart,
                lookbackEnd,
                BarTimeframe.Hour1,
                cancellationToken).ConfigureAwait(false);

            // Aggregate volume by hour of day
            var volumeByHour = new double[24];
            foreach (var bar in bars)
            {
                var hour = bar.Timestamp.Hour;
                volumeByHour[hour] += bar.Volume;
            }

            // Map to our slice count
            var profile = new double[_options.NumSlices];
            var hoursPerSlice = 24.0 / _options.NumSlices;

            for (var i = 0; i < _options.NumSlices; i++)
            {
                var startHour = (int)(i * hoursPerSlice);
                var endHour = (int)((i + 1) * hoursPerSlice);

                for (var h = startHour; h < endHour && h < 24; h++)
                {
                    profile[i] += volumeByHour[h];
                }
            }

            return profile;
        }
        catch
        {
            // Return uniform profile on error
            return Enumerable.Repeat(1.0, _options.NumSlices).ToArray();
        }
    }

    private async Task<decimal> EstimateVolumeSinceStartAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        CancellationToken cancellationToken)
    {
        // Estimate volume traded since order started
        try
        {
            var bars = await marketData.GetHistoricalBarsAsync(
                order.Symbol,
                order.StartTime,
                DateTime.UtcNow,
                BarTimeframe.Minute1,
                cancellationToken).ConfigureAwait(false);

            return bars.Sum(b => b.Volume);
        }
        catch
        {
            return 0m;
        }
    }
}

/// <summary>
/// Options for VWAP algorithm.
/// </summary>
public sealed class VwapOptions
{
    /// <summary>Gets or sets the number of slices.</summary>
    public int NumSlices { get; set; } = 10;

    /// <summary>Gets or sets the lookback period in days for volume profile.</summary>
    public int LookbackDays { get; set; } = 5;

    /// <summary>Gets or sets the max participation rate (0-1).</summary>
    public decimal MaxParticipationRate { get; set; } = 0.1m;
}

/// <summary>
/// Iceberg order execution algorithm.
/// Shows only a small "visible" portion while hiding the full order size.
/// </summary>
public sealed class IcebergAlgorithm : IExecutionAlgorithm
{
    private readonly IcebergOptions _options;
    private readonly Random _random;

    /// <summary>
    /// Creates a new Iceberg algorithm.
    /// </summary>
    public IcebergAlgorithm(IcebergOptions? options = null)
    {
        _options = options ?? new IcebergOptions();
        _random = new Random();
    }

    /// <inheritdoc />
    public string Name => "Iceberg";

    /// <inheritdoc />
    public string Description => "Shows only visible quantity while hiding total order size";

    /// <inheritdoc />
    public async Task<AlgorithmExecutionResult> ExecuteAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        IBroker broker,
        CancellationToken cancellationToken = default)
    {
        var result = new AlgorithmExecutionResult
        {
            StartTime = DateTime.UtcNow
        };

        var childResults = new List<ChildOrderResult>();
        var totalFilled = 0m;
        var totalValue = 0m;
        var remaining = order.TotalQuantity;

        while (remaining > 0 && !cancellationToken.IsCancellationRequested)
        {
            // Calculate visible quantity with variance
            var visibleQty = _options.VisibleQuantity;
            if (_options.QuantityVariancePercent > 0)
            {
                var variance = (decimal)(_random.NextDouble() * 2 - 1) * _options.QuantityVariancePercent;
                visibleQty = visibleQty * (1 + variance);
            }

            visibleQty = Math.Min(visibleQty, remaining);

            // Submit the visible slice
            var request = new ExtendedTradeRequest
            {
                Symbol = order.Symbol,
                Action = order.Side == Orders.OrderSide.Buy ? TradeAction.MarketBuy : TradeAction.MarketSell,
                Quantity = visibleQty,
                OrderType = order.LimitPrice.HasValue ? OrderType.Limit : OrderType.Market,
                LimitPrice = order.LimitPrice,
                TimeInForce = TimeInForce.GTC,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var brokerOrder = await broker.SubmitOrderAsync(request, cancellationToken).ConfigureAwait(false);

                // Wait for fill or timeout
                var timeout = DateTime.UtcNow.Add(_options.RefreshInterval);
                while (DateTime.UtcNow < timeout && !cancellationToken.IsCancellationRequested)
                {
                    var orderStatus = await broker.GetOrderAsync(brokerOrder.OrderId, cancellationToken).ConfigureAwait(false);

                    if (orderStatus.IsFilled)
                    {
                        var childResult = new ChildOrderResult
                        {
                            OrderId = orderStatus.OrderId,
                            RequestedQuantity = visibleQty,
                            FilledQuantity = orderStatus.FilledQuantity,
                            AverageFillPrice = orderStatus.AverageFillPrice ?? 0m,
                            SubmittedAt = DateTime.UtcNow,
                            FilledAt = orderStatus.FilledAt,
                            Status = orderStatus.Status
                        };

                        childResults.Add(childResult);

                        if (orderStatus.FilledQuantity > 0 && orderStatus.AverageFillPrice.HasValue)
                        {
                            totalFilled += orderStatus.FilledQuantity;
                            totalValue += orderStatus.FilledQuantity * orderStatus.AverageFillPrice.Value;
                            remaining -= orderStatus.FilledQuantity;
                        }

                        break;
                    }

                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }

                // Cancel unfilled order and refresh
                if (!brokerOrder.IsFilled)
                {
                    await broker.CancelOrderAsync(brokerOrder.OrderId, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                break;
            }

            // Wait before next refresh
            await Task.Delay(_options.RefreshInterval, cancellationToken).ConfigureAwait(false);
        }

        result.EndTime = DateTime.UtcNow;
        result.TotalFilledQuantity = totalFilled;
        result.AveragePrice = totalFilled > 0 ? totalValue / totalFilled : 0m;
        result.TotalValue = totalValue;
        result.ChildOrderCount = childResults.Count;
        result.ChildOrders = childResults;
        result.FillRate = order.TotalQuantity > 0 ? totalFilled / order.TotalQuantity : 0m;
        result.Success = totalFilled > 0;

        return result;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduledOrder>> GetScheduleAsync(
        AlgorithmOrder order,
        IMarketDataProvider marketData,
        CancellationToken cancellationToken = default)
    {
        // Iceberg doesn't pre-schedule - it reacts to fills
        var schedule = new List<ScheduledOrder>();
        var remaining = order.TotalQuantity;

        while (remaining > 0)
        {
            var visibleQty = Math.Min(_options.VisibleQuantity, remaining);
            schedule.Add(new ScheduledOrder
            {
                Quantity = visibleQty,
                LimitPrice = order.LimitPrice
            });
            remaining -= visibleQty;
        }

        return Task.FromResult<IReadOnlyList<ScheduledOrder>>(schedule);
    }
}

/// <summary>
/// Options for Iceberg algorithm.
/// </summary>
public sealed class IcebergOptions
{
    /// <summary>Gets or sets the visible quantity per slice.</summary>
    public decimal VisibleQuantity { get; set; } = 100m;

    /// <summary>Gets or sets the refresh interval between slices.</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the variance percent for visible quantity (0-1).</summary>
    public decimal QuantityVariancePercent { get; set; } = 0.2m;
}

/// <summary>
/// Executor for running execution algorithms.
/// </summary>
public sealed class AlgorithmExecutor
{
    private readonly IMarketDataProvider _marketData;
    private readonly IBroker _broker;

    /// <summary>
    /// Creates a new algorithm executor.
    /// </summary>
    public AlgorithmExecutor(IMarketDataProvider marketData, IBroker broker)
    {
        _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    /// <summary>
    /// Executes a TWAP order.
    /// </summary>
    public Task<AlgorithmExecutionResult> ExecuteTwapAsync(
        string symbol,
        Orders.OrderSide side,
        decimal quantity,
        TimeSpan duration,
        TwapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var algo = new TwapAlgorithm(options);
        var order = new AlgorithmOrder
        {
            Symbol = symbol,
            Side = side,
            TotalQuantity = quantity,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.Add(duration)
        };

        return algo.ExecuteAsync(order, _marketData, _broker, cancellationToken);
    }

    /// <summary>
    /// Executes a VWAP order.
    /// </summary>
    public Task<AlgorithmExecutionResult> ExecuteVwapAsync(
        string symbol,
        Orders.OrderSide side,
        decimal quantity,
        TimeSpan duration,
        VwapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var algo = new VwapAlgorithm(options);
        var order = new AlgorithmOrder
        {
            Symbol = symbol,
            Side = side,
            TotalQuantity = quantity,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.Add(duration)
        };

        return algo.ExecuteAsync(order, _marketData, _broker, cancellationToken);
    }

    /// <summary>
    /// Executes an Iceberg order.
    /// </summary>
    public Task<AlgorithmExecutionResult> ExecuteIcebergAsync(
        string symbol,
        Orders.OrderSide side,
        decimal totalQuantity,
        decimal visibleQuantity,
        decimal? limitPrice = null,
        IcebergOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new IcebergOptions();
        options.VisibleQuantity = visibleQuantity;

        var algo = new IcebergAlgorithm(options);
        var order = new AlgorithmOrder
        {
            Symbol = symbol,
            Side = side,
            TotalQuantity = totalQuantity,
            LimitPrice = limitPrice,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(4) // Default max duration
        };

        return algo.ExecuteAsync(order, _marketData, _broker, cancellationToken);
    }
}
