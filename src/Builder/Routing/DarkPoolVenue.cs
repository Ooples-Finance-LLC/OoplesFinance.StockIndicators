using OoplesFinance.StockIndicators.Builder.Trading.Orders;

namespace OoplesFinance.StockIndicators.Builder.Routing;

/// <summary>
/// Dark pool execution venue implementation.
/// Provides anonymous liquidity matching without displaying orders publicly.
/// </summary>
public abstract class DarkPoolVenue : IExecutionVenue
{
    /// <inheritdoc />
    public abstract string VenueId { get; }

    /// <inheritdoc />
    public abstract string VenueName { get; }

    /// <inheritdoc />
    public bool IsConnected { get; protected set; }

    /// <inheritdoc />
    public bool IsDarkPool => true;

    /// <summary>Gets the minimum order size for this dark pool.</summary>
    public virtual decimal MinimumOrderSize { get; } = 100;

    /// <summary>Gets the crossing rule for this dark pool.</summary>
    public virtual CrossingRule CrossingRule { get; } = CrossingRule.MidpointPeg;

    /// <summary>Gets the supported order types.</summary>
    public virtual IReadOnlyList<DarkPoolOrderType> SupportedOrderTypes { get; } = new[]
    {
        DarkPoolOrderType.MidpointPeg,
        DarkPoolOrderType.PrimaryPeg,
        DarkPoolOrderType.MarketPeg
    };

    /// <inheritdoc />
    public abstract bool SupportsSymbol(string symbol);

    /// <inheritdoc />
    public abstract Task<VenueQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract Task<VenueOrderResult> ExecuteOrderAsync(SmartOrderRequest request, CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract decimal GetFeeEstimate(decimal quantity, decimal price);

    /// <summary>
    /// Gets the indication of interest (IOI) for a symbol.
    /// </summary>
    public virtual Task<IndicationOfInterest?> GetIOIAsync(string symbol, CancellationToken cancellationToken)
    {
        return Task.FromResult<IndicationOfInterest?>(null);
    }

    /// <summary>
    /// Submits a conditional order (only executes if contra-side interest exists).
    /// </summary>
    public virtual Task<ConditionalOrderResult> SubmitConditionalOrderAsync(
        SmartOrderRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ConditionalOrderResult
        {
            Status = ConditionalOrderStatus.NotSupported,
            Message = "Conditional orders not supported by this venue"
        });
    }
}

/// <summary>
/// Simulated dark pool for testing and paper trading.
/// </summary>
public sealed class SimulatedDarkPool : DarkPoolVenue
{
    private readonly Dictionary<string, DarkPoolLiquidity> _liquidity = new();
    private readonly Random _random;
    private readonly object _lockObj = new();

    /// <inheritdoc />
    public override string VenueId { get; }

    /// <inheritdoc />
    public override string VenueName { get; }

    /// <summary>Gets or sets the fill probability (0-1).</summary>
    public decimal FillProbability { get; set; } = 0.7m;

    /// <summary>Gets or sets the average fill rate (0-1).</summary>
    public decimal AverageFillRate { get; set; } = 0.85m;

    /// <summary>Gets or sets the fee per share.</summary>
    public decimal FeePerShare { get; set; } = 0.001m;

    /// <summary>Gets or sets the minimum fee.</summary>
    public decimal MinimumFee { get; set; } = 0.25m;

    /// <summary>
    /// Creates a new simulated dark pool.
    /// </summary>
    public SimulatedDarkPool(string venueId, string venueName, int? seed = null)
    {
        VenueId = venueId;
        VenueName = venueName;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        IsConnected = true;
    }

    /// <summary>
    /// Seeds liquidity for a symbol.
    /// </summary>
    public void SeedLiquidity(string symbol, decimal midPrice, decimal availableBuyQuantity, decimal availableSellQuantity)
    {
        lock (_lockObj)
        {
            _liquidity[symbol] = new DarkPoolLiquidity
            {
                Symbol = symbol,
                MidPrice = midPrice,
                AvailableBuyQuantity = availableBuyQuantity,
                AvailableSellQuantity = availableSellQuantity,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    /// <inheritdoc />
    public override bool SupportsSymbol(string symbol)
    {
        lock (_lockObj)
        {
            return _liquidity.ContainsKey(symbol);
        }
    }

    /// <inheritdoc />
    public override Task<VenueQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        lock (_lockObj)
        {
            if (!_liquidity.TryGetValue(symbol, out var liquidity))
            {
                return Task.FromResult<VenueQuote?>(null);
            }

            // Dark pools typically don't show exact prices, but we simulate mid-point
            return Task.FromResult<VenueQuote?>(new VenueQuote
            {
                VenueId = VenueId,
                Symbol = symbol,
                BidPrice = liquidity.MidPrice,
                BidSize = liquidity.AvailableSellQuantity,
                AskPrice = liquidity.MidPrice,
                AskSize = liquidity.AvailableBuyQuantity,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <inheritdoc />
    public override Task<VenueOrderResult> ExecuteOrderAsync(SmartOrderRequest request, CancellationToken cancellationToken)
    {
        lock (_lockObj)
        {
            if (!_liquidity.TryGetValue(request.Symbol, out var liquidity))
            {
                return Task.FromResult(new VenueOrderResult
                {
                    OrderId = Guid.NewGuid().ToString(),
                    Status = FillStatus.Rejected
                });
            }

            // Simulate fill probability
            if ((decimal)_random.NextDouble() > FillProbability)
            {
                return Task.FromResult(new VenueOrderResult
                {
                    OrderId = Guid.NewGuid().ToString(),
                    FilledQuantity = 0,
                    Status = FillStatus.Rejected
                });
            }

            // Calculate fill based on available liquidity
            var availableQuantity = request.Side == OrderSide.Buy
                ? liquidity.AvailableBuyQuantity
                : liquidity.AvailableSellQuantity;

            var fillRate = (decimal)_random.NextDouble() * 0.3m + AverageFillRate - 0.15m;
            fillRate = Math.Max(0.5m, Math.Min(fillRate, 1.0m));

            var filledQuantity = Math.Min(request.Quantity * fillRate, availableQuantity);

            // Update liquidity
            if (request.Side == OrderSide.Buy)
            {
                liquidity.AvailableBuyQuantity -= filledQuantity;
            }
            else
            {
                liquidity.AvailableSellQuantity -= filledQuantity;
            }

            var fees = Math.Max(MinimumFee, filledQuantity * FeePerShare);

            return Task.FromResult(new VenueOrderResult
            {
                OrderId = Guid.NewGuid().ToString(),
                FilledQuantity = filledQuantity,
                AveragePrice = liquidity.MidPrice,
                Fees = fees,
                Status = filledQuantity >= request.Quantity ? FillStatus.Filled : FillStatus.PartialFill
            });
        }
    }

    /// <inheritdoc />
    public override decimal GetFeeEstimate(decimal quantity, decimal price)
    {
        return Math.Max(MinimumFee, quantity * FeePerShare);
    }

    /// <inheritdoc />
    public override Task<IndicationOfInterest?> GetIOIAsync(string symbol, CancellationToken cancellationToken)
    {
        lock (_lockObj)
        {
            if (!_liquidity.TryGetValue(symbol, out var liquidity))
            {
                return Task.FromResult<IndicationOfInterest?>(null);
            }

            return Task.FromResult<IndicationOfInterest?>(new IndicationOfInterest
            {
                Symbol = symbol,
                VenueId = VenueId,
                Side = liquidity.AvailableBuyQuantity > liquidity.AvailableSellQuantity
                    ? OrderSide.Buy
                    : OrderSide.Sell,
                SizeCategory = GetSizeCategory(Math.Max(liquidity.AvailableBuyQuantity, liquidity.AvailableSellQuantity)),
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private static IOISizeCategory GetSizeCategory(decimal quantity) => quantity switch
    {
        < 1000 => IOISizeCategory.Small,
        < 10000 => IOISizeCategory.Medium,
        < 100000 => IOISizeCategory.Large,
        _ => IOISizeCategory.VeryLarge
    };
}

/// <summary>
/// Dark pool liquidity snapshot.
/// </summary>
internal sealed class DarkPoolLiquidity
{
    public string Symbol { get; init; } = string.Empty;
    public decimal MidPrice { get; set; }
    public decimal AvailableBuyQuantity { get; set; }
    public decimal AvailableSellQuantity { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Crossing rules for dark pools.
/// </summary>
public enum CrossingRule
{
    /// <summary>Cross at NBBO midpoint.</summary>
    MidpointPeg,

    /// <summary>Cross at NBBO bid for buys, ask for sells.</summary>
    PrimaryPeg,

    /// <summary>Cross at VWAP.</summary>
    VWAP,

    /// <summary>Cross at specified limit price or better.</summary>
    Limit,

    /// <summary>Cross at market close price.</summary>
    ClosingCross
}

/// <summary>
/// Dark pool order types.
/// </summary>
public enum DarkPoolOrderType
{
    /// <summary>Pegged to NBBO midpoint.</summary>
    MidpointPeg,

    /// <summary>Pegged to primary market (bid for buys, ask for sells).</summary>
    PrimaryPeg,

    /// <summary>Pegged to current market price.</summary>
    MarketPeg,

    /// <summary>Limit order with minimum execution size.</summary>
    LimitWithMinSize,

    /// <summary>Conditional order requiring contra-side interest.</summary>
    Conditional
}

/// <summary>
/// Indication of Interest (IOI) from a dark pool.
/// </summary>
public sealed class IndicationOfInterest
{
    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Gets the venue identifier.</summary>
    public string VenueId { get; init; } = string.Empty;

    /// <summary>Gets the indicated side.</summary>
    public OrderSide Side { get; init; }

    /// <summary>Gets the size category (not exact size for anonymity).</summary>
    public IOISizeCategory SizeCategory { get; init; }

    /// <summary>Gets when the IOI was generated.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Gets the IOI validity duration.</summary>
    public TimeSpan ValidFor { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets whether the IOI is still valid.</summary>
    public bool IsValid => DateTime.UtcNow - Timestamp < ValidFor;
}

/// <summary>
/// IOI size categories.
/// </summary>
public enum IOISizeCategory
{
    /// <summary>Less than 1,000 shares.</summary>
    Small,

    /// <summary>1,000 - 9,999 shares.</summary>
    Medium,

    /// <summary>10,000 - 99,999 shares.</summary>
    Large,

    /// <summary>100,000+ shares.</summary>
    VeryLarge
}

/// <summary>
/// Conditional order result.
/// </summary>
public sealed class ConditionalOrderResult
{
    /// <summary>Gets the order ID.</summary>
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Gets the conditional order status.</summary>
    public ConditionalOrderStatus Status { get; init; }

    /// <summary>Gets the message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets whether contra-side interest was found.</summary>
    public bool ContraInterestFound { get; init; }

    /// <summary>Gets the invitation ID if interest was found.</summary>
    public string? InvitationId { get; init; }
}

/// <summary>
/// Conditional order status.
/// </summary>
public enum ConditionalOrderStatus
{
    /// <summary>Conditional order is active, waiting for contra-interest.</summary>
    Active,

    /// <summary>Contra-interest found, invitation sent.</summary>
    InvitationSent,

    /// <summary>Match found, order executed.</summary>
    Executed,

    /// <summary>No contra-interest found.</summary>
    NoMatch,

    /// <summary>Order expired.</summary>
    Expired,

    /// <summary>Order cancelled.</summary>
    Cancelled,

    /// <summary>Feature not supported.</summary>
    NotSupported
}

/// <summary>
/// Common dark pool venue implementations.
/// </summary>
public static class DarkPoolVenues
{
    /// <summary>
    /// Creates a simulated Crossfinder-style dark pool (Credit Suisse).
    /// </summary>
    public static SimulatedDarkPool CreateCrossfinder(int? seed = null)
    {
        return new SimulatedDarkPool("CSFB", "Credit Suisse Crossfinder", seed)
        {
            FillProbability = 0.65m,
            AverageFillRate = 0.80m,
            FeePerShare = 0.0008m
        };
    }

    /// <summary>
    /// Creates a simulated Sigma X dark pool (Goldman Sachs).
    /// </summary>
    public static SimulatedDarkPool CreateSigmaX(int? seed = null)
    {
        return new SimulatedDarkPool("SGMT", "Goldman Sachs Sigma X", seed)
        {
            FillProbability = 0.70m,
            AverageFillRate = 0.85m,
            FeePerShare = 0.0009m
        };
    }

    /// <summary>
    /// Creates a simulated MS Pool dark pool (Morgan Stanley).
    /// </summary>
    public static SimulatedDarkPool CreateMSPool(int? seed = null)
    {
        return new SimulatedDarkPool("MSPL", "Morgan Stanley MS Pool", seed)
        {
            FillProbability = 0.68m,
            AverageFillRate = 0.82m,
            FeePerShare = 0.0010m
        };
    }

    /// <summary>
    /// Creates a simulated UBS ATS dark pool.
    /// </summary>
    public static SimulatedDarkPool CreateUBSAts(int? seed = null)
    {
        return new SimulatedDarkPool("UBSA", "UBS ATS", seed)
        {
            FillProbability = 0.72m,
            AverageFillRate = 0.88m,
            FeePerShare = 0.0007m
        };
    }

    /// <summary>
    /// Creates a simulated IEX dark pool.
    /// </summary>
    public static SimulatedDarkPool CreateIEX(int? seed = null)
    {
        return new SimulatedDarkPool("IEXG", "IEX", seed)
        {
            FillProbability = 0.75m,
            AverageFillRate = 0.90m,
            FeePerShare = 0.0009m
        };
    }
}

/// <summary>
/// Execution quality analyzer for comparing venue performance.
/// </summary>
public sealed class ExecutionQualityAnalyzer
{
    private readonly List<ExecutionRecord> _records = new();
    private readonly object _lockObj = new();

    /// <summary>
    /// Records an execution for analysis.
    /// </summary>
    public void RecordExecution(RoutingResult result, NBBO nbboAtExecution)
    {
        if (result.FilledQuantity <= 0) return;

        lock (_lockObj)
        {
            _records.Add(new ExecutionRecord
            {
                Timestamp = DateTime.UtcNow,
                Symbol = result.Symbol,
                Side = result.Side,
                RequestedQuantity = result.RequestedQuantity,
                FilledQuantity = result.FilledQuantity,
                AveragePrice = result.AveragePrice,
                TotalFees = result.TotalFees,
                VenuesUsed = result.Fills.Select(f => f.VenueId).ToList(),
                NbboBid = nbboAtExecution.BidPrice,
                NbboAsk = nbboAtExecution.AskPrice,
                NbboMid = nbboAtExecution.MidPrice,
                LatencyMs = result.RoutingLatencyMs
            });
        }

        // Keep only last 10000 records
        lock (_lockObj)
        {
            if (_records.Count > 10000)
            {
                _records.RemoveRange(0, _records.Count - 10000);
            }
        }
    }

    /// <summary>
    /// Gets execution quality statistics.
    /// </summary>
    public ExecutionStatistics GetStatistics(DateTime? since = null, string? symbol = null)
    {
        IReadOnlyList<ExecutionRecord> records;
        lock (_lockObj)
        {
            var query = _records.AsEnumerable();
            if (since.HasValue)
            {
                query = query.Where(r => r.Timestamp >= since.Value);
            }
            if (!string.IsNullOrEmpty(symbol))
            {
                query = query.Where(r => r.Symbol == symbol);
            }
            records = query.ToList();
        }

        if (records.Count == 0)
        {
            return new ExecutionStatistics();
        }

        var slippages = records.Select(r =>
        {
            var expected = r.Side == OrderSide.Buy ? r.NbboAsk : r.NbboBid;
            return r.AveragePrice - expected;
        }).ToList();

        var fillRates = records.Select(r => r.FilledQuantity / r.RequestedQuantity).ToList();

        return new ExecutionStatistics
        {
            TotalExecutions = records.Count,
            TotalVolume = records.Sum(r => r.FilledQuantity),
            AverageSlippage = slippages.Average(),
            MedianSlippage = GetMedian(slippages),
            AverageSlippageBps = slippages.Zip(records, (s, r) => r.NbboMid > 0 ? (s / r.NbboMid) * 10000 : 0).Average(),
            AverageFillRate = fillRates.Average(),
            AverageLatencyMs = records.Average(r => r.LatencyMs),
            VenueBreakdown = GetVenueBreakdown(records)
        };
    }

    /// <summary>
    /// Gets venue-specific statistics.
    /// </summary>
    public IReadOnlyDictionary<string, VenueStatistics> GetVenueStatistics()
    {
        IReadOnlyList<ExecutionRecord> records;
        lock (_lockObj)
        {
            records = _records.ToList();
        }

        var result = new Dictionary<string, VenueStatistics>();

        foreach (var venueId in records.SelectMany(r => r.VenuesUsed).Distinct())
        {
            var venueRecords = records.Where(r => r.VenuesUsed.Contains(venueId)).ToList();

            if (venueRecords.Count == 0) continue;

            var slippages = venueRecords.Select(r =>
            {
                var expected = r.Side == OrderSide.Buy ? r.NbboAsk : r.NbboBid;
                return r.AveragePrice - expected;
            }).ToList();

            result[venueId] = new VenueStatistics
            {
                VenueId = venueId,
                TotalExecutions = venueRecords.Count,
                TotalVolume = venueRecords.Sum(r => r.FilledQuantity),
                AverageSlippageBps = slippages.Zip(venueRecords, (s, r) => r.NbboMid > 0 ? (s / r.NbboMid) * 10000 : 0).Average(),
                AverageLatencyMs = venueRecords.Average(r => r.LatencyMs),
                AverageFees = venueRecords.Average(r => r.TotalFees)
            };
        }

        return result;
    }

    private static decimal GetMedian(List<decimal> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }

    private static IReadOnlyDictionary<string, int> GetVenueBreakdown(IReadOnlyList<ExecutionRecord> records)
    {
        var breakdown = new Dictionary<string, int>();
        foreach (var venueId in records.SelectMany(r => r.VenuesUsed))
        {
            if (!breakdown.ContainsKey(venueId))
            {
                breakdown[venueId] = 0;
            }
            breakdown[venueId]++;
        }
        return breakdown;
    }
}

/// <summary>
/// Execution record for analysis.
/// </summary>
internal sealed class ExecutionRecord
{
    public DateTime Timestamp { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public OrderSide Side { get; init; }
    public decimal RequestedQuantity { get; init; }
    public decimal FilledQuantity { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal TotalFees { get; init; }
    public List<string> VenuesUsed { get; init; } = new();
    public decimal NbboBid { get; init; }
    public decimal NbboAsk { get; init; }
    public decimal NbboMid { get; init; }
    public double LatencyMs { get; init; }
}

/// <summary>
/// Aggregate execution statistics.
/// </summary>
public sealed class ExecutionStatistics
{
    /// <summary>Gets the total number of executions.</summary>
    public int TotalExecutions { get; init; }

    /// <summary>Gets the total volume traded.</summary>
    public decimal TotalVolume { get; init; }

    /// <summary>Gets the average slippage.</summary>
    public decimal AverageSlippage { get; init; }

    /// <summary>Gets the median slippage.</summary>
    public decimal MedianSlippage { get; init; }

    /// <summary>Gets the average slippage in basis points.</summary>
    public decimal AverageSlippageBps { get; init; }

    /// <summary>Gets the average fill rate.</summary>
    public decimal AverageFillRate { get; init; }

    /// <summary>Gets the average latency in milliseconds.</summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>Gets the breakdown by venue.</summary>
    public IReadOnlyDictionary<string, int> VenueBreakdown { get; init; } = new Dictionary<string, int>();
}

/// <summary>
/// Per-venue statistics.
/// </summary>
public sealed class VenueStatistics
{
    /// <summary>Gets the venue identifier.</summary>
    public string VenueId { get; init; } = string.Empty;

    /// <summary>Gets the total executions at this venue.</summary>
    public int TotalExecutions { get; init; }

    /// <summary>Gets the total volume at this venue.</summary>
    public decimal TotalVolume { get; init; }

    /// <summary>Gets the average slippage in bps at this venue.</summary>
    public decimal AverageSlippageBps { get; init; }

    /// <summary>Gets the average latency in ms at this venue.</summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>Gets the average fees at this venue.</summary>
    public decimal AverageFees { get; init; }
}
