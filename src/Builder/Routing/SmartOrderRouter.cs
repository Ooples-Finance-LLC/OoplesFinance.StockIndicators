using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Builder.Trading.Orders;

namespace OoplesFinance.StockIndicators.Builder.Routing;

/// <summary>
/// Smart Order Router that optimizes order execution across multiple venues.
/// </summary>
public sealed class SmartOrderRouter
{
    private readonly List<IExecutionVenue> _venues = new();
    private readonly RoutingStrategy _strategy;
    private readonly RoutingOptions _options;
    private readonly object _lockObj = new();

    /// <summary>
    /// Creates a new Smart Order Router.
    /// </summary>
    public SmartOrderRouter(RoutingStrategy strategy = RoutingStrategy.BestPrice, RoutingOptions? options = null)
    {
        _strategy = strategy;
        _options = options ?? new RoutingOptions();
    }

    /// <summary>
    /// Registers an execution venue.
    /// </summary>
    public void RegisterVenue(IExecutionVenue venue)
    {
        lock (_lockObj)
        {
            if (!_venues.Any(v => v.VenueId == venue.VenueId))
            {
                _venues.Add(venue);
            }
        }
    }

    /// <summary>
    /// Removes an execution venue.
    /// </summary>
    public void RemoveVenue(string venueId)
    {
        lock (_lockObj)
        {
            _venues.RemoveAll(v => v.VenueId == venueId);
        }
    }

    /// <summary>
    /// Routes an order to the optimal venue(s).
    /// </summary>
    public async Task<RoutingResult> RouteOrderAsync(
        SmartOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new RoutingResult
        {
            OrderId = Guid.NewGuid().ToString(),
            Symbol = request.Symbol,
            RequestedQuantity = request.Quantity,
            Side = request.Side
        };

        try
        {
            // Get quotes from all venues
            var venueQuotes = await GetVenueQuotesAsync(request.Symbol, cancellationToken).ConfigureAwait(false);

            if (venueQuotes.Count == 0)
            {
                result.Status = RoutingStatus.NoVenuesAvailable;
                return result;
            }

            // Select venues based on strategy
            var selectedVenues = SelectVenues(request, venueQuotes);

            if (selectedVenues.Count == 0)
            {
                result.Status = RoutingStatus.NoLiquidityAvailable;
                return result;
            }

            // Execute across selected venues
            var fills = new List<VenueFill>();
            var remainingQuantity = request.Quantity;

            foreach (var selection in selectedVenues)
            {
                if (remainingQuantity <= 0) break;
                if (selection.Venue is null) continue;

                var venue = selection.Venue;
                var quantityForVenue = Math.Min(remainingQuantity, selection.AvailableQuantity);

                var fill = await ExecuteAtVenueAsync(
                    venue,
                    request with { Quantity = quantityForVenue },
                    cancellationToken).ConfigureAwait(false);

                fills.Add(fill);
                remainingQuantity -= fill.FilledQuantity;
            }

            result.Fills = fills;
            result.FilledQuantity = fills.Sum(f => f.FilledQuantity);
            result.AveragePrice = CalculateAveragePrice(fills);
            result.TotalFees = fills.Sum(f => f.Fees);
            result.RoutingLatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            result.Status = result.FilledQuantity >= request.Quantity
                ? RoutingStatus.FullyFilled
                : result.FilledQuantity > 0
                    ? RoutingStatus.PartiallyFilled
                    : RoutingStatus.Rejected;

            // Calculate execution quality metrics
            result.ExecutionQuality = CalculateExecutionQuality(request, result, venueQuotes);
        }
        catch (Exception ex)
        {
            result.Status = RoutingStatus.Error;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Gets the best available price across all venues.
    /// </summary>
    public async Task<BestPrice?> GetBestPriceAsync(string symbol, OrderSide side, CancellationToken cancellationToken = default)
    {
        var quotes = await GetVenueQuotesAsync(symbol, cancellationToken).ConfigureAwait(false);

        if (quotes.Count == 0) return null;

        VenueQuote? best = null;

        foreach (var quote in quotes)
        {
            if (best is null)
            {
                best = quote;
                continue;
            }

            var isBetter = side == OrderSide.Buy
                ? quote.AskPrice < best.AskPrice
                : quote.BidPrice > best.BidPrice;

            if (isBetter) best = quote;
        }

        if (best is null) return null;

        return new BestPrice
        {
            Symbol = symbol,
            Side = side,
            Price = side == OrderSide.Buy ? best.AskPrice : best.BidPrice,
            Size = side == OrderSide.Buy ? best.AskSize : best.BidSize,
            VenueId = best.VenueId,
            Timestamp = best.Timestamp
        };
    }

    /// <summary>
    /// Gets NBBO (National Best Bid and Offer).
    /// </summary>
    public async Task<NBBO?> GetNBBOAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var quotes = await GetVenueQuotesAsync(symbol, cancellationToken).ConfigureAwait(false);

        if (quotes.Count == 0) return null;

        var bestBid = quotes.OrderByDescending(q => q.BidPrice).FirstOrDefault();
        var bestAsk = quotes.OrderBy(q => q.AskPrice).FirstOrDefault();

        if (bestBid is null || bestAsk is null) return null;

        return new NBBO
        {
            Symbol = symbol,
            BidPrice = bestBid.BidPrice,
            BidSize = bestBid.BidSize,
            BidVenue = bestBid.VenueId,
            AskPrice = bestAsk.AskPrice,
            AskSize = bestAsk.AskSize,
            AskVenue = bestAsk.VenueId,
            Spread = bestAsk.AskPrice - bestBid.BidPrice,
            MidPrice = (bestBid.BidPrice + bestAsk.AskPrice) / 2,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<List<VenueQuote>> GetVenueQuotesAsync(string symbol, CancellationToken cancellationToken)
    {
        var quotes = new List<VenueQuote>();
        IReadOnlyList<IExecutionVenue> venues;

        lock (_lockObj)
        {
            venues = _venues.Where(v => v.IsConnected && v.SupportsSymbol(symbol)).ToList();
        }

        var tasks = venues.Select(v => GetQuoteFromVenueAsync(v, symbol, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var quote in results)
        {
            if (quote is not null)
            {
                quotes.Add(quote);
            }
        }

        return quotes;
    }

    private async Task<VenueQuote?> GetQuoteFromVenueAsync(
        IExecutionVenue venue,
        string symbol,
        CancellationToken cancellationToken)
    {
        try
        {
            return await venue.GetQuoteAsync(symbol, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private List<VenueSelection> SelectVenues(SmartOrderRequest request, List<VenueQuote> quotes)
    {
        var selections = new List<VenueSelection>();

        // Filter venues that meet minimum requirements
        var eligibleQuotes = quotes
            .Where(q => IsVenueEligible(q, request))
            .ToList();

        if (eligibleQuotes.Count == 0) return selections;

        switch (_strategy)
        {
            case RoutingStrategy.BestPrice:
                selections = SelectByBestPrice(request, eligibleQuotes);
                break;

            case RoutingStrategy.BestSize:
                selections = SelectByBestSize(request, eligibleQuotes);
                break;

            case RoutingStrategy.LowestFees:
                selections = SelectByLowestFees(request, eligibleQuotes);
                break;

            case RoutingStrategy.VWAP:
                selections = SelectByVWAP(request, eligibleQuotes);
                break;

            case RoutingStrategy.ProRata:
                selections = SelectProRata(request, eligibleQuotes);
                break;

            case RoutingStrategy.PriceSizeOptimal:
                selections = SelectPriceSizeOptimal(request, eligibleQuotes);
                break;
        }

        return selections;
    }

    private bool IsVenueEligible(VenueQuote quote, SmartOrderRequest request)
    {
        // Check minimum size
        var availableSize = request.Side == OrderSide.Buy ? quote.AskSize : quote.BidSize;
        if (availableSize < _options.MinimumVenueSize) return false;

        // Check if venue is in excluded list
        if (_options.ExcludedVenues.Contains(quote.VenueId)) return false;

        // Check price limits
        var price = request.Side == OrderSide.Buy ? quote.AskPrice : quote.BidPrice;
        if (request.MaxPrice.HasValue && price > request.MaxPrice) return false;
        if (request.MinPrice.HasValue && price < request.MinPrice) return false;

        return true;
    }

    private List<VenueSelection> SelectByBestPrice(SmartOrderRequest request, List<VenueQuote> quotes)
    {
        var ordered = request.Side == OrderSide.Buy
            ? quotes.OrderBy(q => q.AskPrice)
            : quotes.OrderByDescending(q => q.BidPrice);

        return ordered.Select(q => new VenueSelection
        {
            Venue = GetVenue(q.VenueId),
            Quote = q,
            AvailableQuantity = request.Side == OrderSide.Buy ? q.AskSize : q.BidSize,
            Price = request.Side == OrderSide.Buy ? q.AskPrice : q.BidPrice
        }).Where(s => s.Venue is not null).ToList()!;
    }

    private List<VenueSelection> SelectByBestSize(SmartOrderRequest request, List<VenueQuote> quotes)
    {
        var ordered = request.Side == OrderSide.Buy
            ? quotes.OrderByDescending(q => q.AskSize).ThenBy(q => q.AskPrice)
            : quotes.OrderByDescending(q => q.BidSize).ThenByDescending(q => q.BidPrice);

        return ordered.Select(q => new VenueSelection
        {
            Venue = GetVenue(q.VenueId),
            Quote = q,
            AvailableQuantity = request.Side == OrderSide.Buy ? q.AskSize : q.BidSize,
            Price = request.Side == OrderSide.Buy ? q.AskPrice : q.BidPrice
        }).Where(s => s.Venue is not null).ToList()!;
    }

    private List<VenueSelection> SelectByLowestFees(SmartOrderRequest request, List<VenueQuote> quotes)
    {
        IReadOnlyList<IExecutionVenue> venues;
        lock (_lockObj)
        {
            venues = _venues.ToList();
        }

        var withFees = quotes.Select(q =>
        {
            var venue = venues.FirstOrDefault(v => v.VenueId == q.VenueId);
            var fees = venue?.GetFeeEstimate(request.Quantity, request.Side == OrderSide.Buy ? q.AskPrice : q.BidPrice) ?? decimal.MaxValue;
            return (Quote: q, Fees: fees);
        });

        var ordered = withFees.OrderBy(x => x.Fees);

        return ordered.Select(x => new VenueSelection
        {
            Venue = GetVenue(x.Quote.VenueId),
            Quote = x.Quote,
            AvailableQuantity = request.Side == OrderSide.Buy ? x.Quote.AskSize : x.Quote.BidSize,
            Price = request.Side == OrderSide.Buy ? x.Quote.AskPrice : x.Quote.BidPrice,
            EstimatedFees = x.Fees
        }).Where(s => s.Venue is not null).ToList()!;
    }

    private List<VenueSelection> SelectByVWAP(SmartOrderRequest request, List<VenueQuote> quotes)
    {
        // Allocate proportionally to available size at each venue
        var totalSize = quotes.Sum(q => request.Side == OrderSide.Buy ? q.AskSize : q.BidSize);

        return quotes.Select(q =>
        {
            var size = request.Side == OrderSide.Buy ? q.AskSize : q.BidSize;
            var proportion = totalSize > 0 ? size / totalSize : 0;

            return new VenueSelection
            {
                Venue = GetVenue(q.VenueId),
                Quote = q,
                AvailableQuantity = request.Quantity * proportion,
                Price = request.Side == OrderSide.Buy ? q.AskPrice : q.BidPrice
            };
        }).Where(s => s.Venue is not null && s.AvailableQuantity > 0).ToList()!;
    }

    private List<VenueSelection> SelectProRata(SmartOrderRequest request, List<VenueQuote> quotes)
    {
        // Equal allocation across all venues
        var perVenue = request.Quantity / quotes.Count;

        return quotes.Select(q => new VenueSelection
        {
            Venue = GetVenue(q.VenueId),
            Quote = q,
            AvailableQuantity = Math.Min(perVenue, request.Side == OrderSide.Buy ? q.AskSize : q.BidSize),
            Price = request.Side == OrderSide.Buy ? q.AskPrice : q.BidPrice
        }).Where(s => s.Venue is not null).ToList()!;
    }

    private List<VenueSelection> SelectPriceSizeOptimal(SmartOrderRequest request, List<VenueQuote> quotes)
    {
        // Weighted score based on price improvement and size
        var scored = quotes.Select(q =>
        {
            var price = request.Side == OrderSide.Buy ? q.AskPrice : q.BidPrice;
            var size = request.Side == OrderSide.Buy ? q.AskSize : q.BidSize;

            // Normalize scores
            var avgPrice = quotes.Average(x => request.Side == OrderSide.Buy ? x.AskPrice : x.BidPrice);
            var avgSize = quotes.Average(x => request.Side == OrderSide.Buy ? x.AskSize : x.BidSize);

            var priceScore = request.Side == OrderSide.Buy
                ? (avgPrice - price) / avgPrice
                : (price - avgPrice) / avgPrice;

            var sizeScore = avgSize > 0 ? (size - avgSize) / avgSize : 0;

            var totalScore = priceScore * _options.PriceWeight + sizeScore * _options.SizeWeight;

            return (Quote: q, Score: totalScore);
        });

        var ordered = scored.OrderByDescending(x => x.Score);

        return ordered.Select(x => new VenueSelection
        {
            Venue = GetVenue(x.Quote.VenueId),
            Quote = x.Quote,
            AvailableQuantity = request.Side == OrderSide.Buy ? x.Quote.AskSize : x.Quote.BidSize,
            Price = request.Side == OrderSide.Buy ? x.Quote.AskPrice : x.Quote.BidPrice
        }).Where(s => s.Venue is not null).ToList()!;
    }

    private IExecutionVenue? GetVenue(string venueId)
    {
        lock (_lockObj)
        {
            return _venues.FirstOrDefault(v => v.VenueId == venueId);
        }
    }

    private async Task<VenueFill> ExecuteAtVenueAsync(
        IExecutionVenue venue,
        SmartOrderRequest request,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var orderResult = await venue.ExecuteOrderAsync(request, cancellationToken).ConfigureAwait(false);

            return new VenueFill
            {
                VenueId = venue.VenueId,
                VenueName = venue.VenueName,
                FilledQuantity = orderResult.FilledQuantity,
                AveragePrice = orderResult.AveragePrice,
                Fees = orderResult.Fees,
                LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                Timestamp = DateTime.UtcNow,
                OrderId = orderResult.OrderId,
                Status = orderResult.Status
            };
        }
        catch (Exception ex)
        {
            return new VenueFill
            {
                VenueId = venue.VenueId,
                VenueName = venue.VenueName,
                FilledQuantity = 0,
                AveragePrice = 0,
                Fees = 0,
                LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                Timestamp = DateTime.UtcNow,
                Status = FillStatus.Rejected,
                ErrorMessage = ex.Message
            };
        }
    }

    private static decimal CalculateAveragePrice(List<VenueFill> fills)
    {
        var totalQuantity = fills.Sum(f => f.FilledQuantity);
        if (totalQuantity == 0) return 0;

        var weightedSum = fills.Sum(f => f.FilledQuantity * f.AveragePrice);
        return weightedSum / totalQuantity;
    }

    private ExecutionQuality CalculateExecutionQuality(
        SmartOrderRequest request,
        RoutingResult result,
        List<VenueQuote> quotes)
    {
        var nbbo = GetNBBOFromQuotes(quotes, request.Side);

        var slippage = request.Side == OrderSide.Buy
            ? result.AveragePrice - nbbo.MidPrice
            : nbbo.MidPrice - result.AveragePrice;

        var priceImprovement = request.Side == OrderSide.Buy
            ? nbbo.AskPrice - result.AveragePrice
            : result.AveragePrice - nbbo.BidPrice;

        return new ExecutionQuality
        {
            Slippage = slippage,
            SlippageBps = nbbo.MidPrice > 0 ? (slippage / nbbo.MidPrice) * 10000 : 0,
            PriceImprovement = priceImprovement,
            PriceImprovementBps = nbbo.MidPrice > 0 ? (priceImprovement / nbbo.MidPrice) * 10000 : 0,
            EffectiveSpread = Math.Abs(result.AveragePrice - nbbo.MidPrice) * 2,
            FillRate = request.Quantity > 0 ? result.FilledQuantity / request.Quantity : 0,
            VenuesUsed = result.Fills.Count,
            TotalLatencyMs = result.RoutingLatencyMs
        };
    }

    private static NBBO GetNBBOFromQuotes(List<VenueQuote> quotes, OrderSide side)
    {
        var bestBid = quotes.OrderByDescending(q => q.BidPrice).FirstOrDefault();
        var bestAsk = quotes.OrderBy(q => q.AskPrice).FirstOrDefault();

        return new NBBO
        {
            BidPrice = bestBid?.BidPrice ?? 0,
            AskPrice = bestAsk?.AskPrice ?? 0,
            MidPrice = ((bestBid?.BidPrice ?? 0) + (bestAsk?.AskPrice ?? 0)) / 2
        };
    }
}

/// <summary>
/// Routing strategies.
/// </summary>
public enum RoutingStrategy
{
    /// <summary>Route to venue with best price.</summary>
    BestPrice,

    /// <summary>Route to venue with most liquidity.</summary>
    BestSize,

    /// <summary>Route to venue with lowest fees.</summary>
    LowestFees,

    /// <summary>Split order proportionally by available size (VWAP-style).</summary>
    VWAP,

    /// <summary>Split order equally across venues.</summary>
    ProRata,

    /// <summary>Optimize for both price and size.</summary>
    PriceSizeOptimal
}

/// <summary>
/// Routing options.
/// </summary>
public sealed class RoutingOptions
{
    /// <summary>Gets or sets the minimum venue size to consider.</summary>
    public decimal MinimumVenueSize { get; set; } = 1;

    /// <summary>Gets or sets venues to exclude from routing.</summary>
    public HashSet<string> ExcludedVenues { get; set; } = new();

    /// <summary>Gets or sets preferred venues (prioritized).</summary>
    public List<string> PreferredVenues { get; set; } = new();

    /// <summary>Gets or sets the price weight for optimal routing (0-1).</summary>
    public decimal PriceWeight { get; set; } = 0.6m;

    /// <summary>Gets or sets the size weight for optimal routing (0-1).</summary>
    public decimal SizeWeight { get; set; } = 0.4m;

    /// <summary>Gets or sets whether to allow dark pool routing.</summary>
    public bool AllowDarkPools { get; set; } = true;

    /// <summary>Gets or sets the maximum number of venues to route to.</summary>
    public int MaxVenues { get; set; } = 5;
}

/// <summary>
/// Interface for execution venues.
/// </summary>
public interface IExecutionVenue
{
    /// <summary>Gets the venue identifier.</summary>
    string VenueId { get; }

    /// <summary>Gets the venue display name.</summary>
    string VenueName { get; }

    /// <summary>Gets whether the venue is connected.</summary>
    bool IsConnected { get; }

    /// <summary>Gets whether this is a dark pool.</summary>
    bool IsDarkPool { get; }

    /// <summary>Checks if the venue supports a symbol.</summary>
    bool SupportsSymbol(string symbol);

    /// <summary>Gets a quote from the venue.</summary>
    Task<VenueQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken);

    /// <summary>Executes an order at the venue.</summary>
    Task<VenueOrderResult> ExecuteOrderAsync(SmartOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Gets the estimated fee for a trade.</summary>
    decimal GetFeeEstimate(decimal quantity, decimal price);
}

/// <summary>
/// Smart order request.
/// </summary>
public sealed record SmartOrderRequest
{
    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Gets the order side.</summary>
    public OrderSide Side { get; init; }

    /// <summary>Gets the quantity.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Gets the order type.</summary>
    public OrderType OrderType { get; init; } = OrderType.Market;

    /// <summary>Gets the limit price (for limit orders).</summary>
    public decimal? LimitPrice { get; init; }

    /// <summary>Gets the maximum acceptable price.</summary>
    public decimal? MaxPrice { get; init; }

    /// <summary>Gets the minimum acceptable price.</summary>
    public decimal? MinPrice { get; init; }

    /// <summary>Gets whether dark pools are allowed.</summary>
    public bool AllowDarkPools { get; init; } = true;

    /// <summary>Gets the time in force.</summary>
    public TimeInForce TimeInForce { get; init; } = TimeInForce.Day;
}

/// <summary>
/// Quote from a specific venue.
/// </summary>
public sealed class VenueQuote
{
    /// <summary>Gets the venue identifier.</summary>
    public string VenueId { get; init; } = string.Empty;

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Gets the bid price.</summary>
    public decimal BidPrice { get; init; }

    /// <summary>Gets the bid size.</summary>
    public decimal BidSize { get; init; }

    /// <summary>Gets the ask price.</summary>
    public decimal AskPrice { get; init; }

    /// <summary>Gets the ask size.</summary>
    public decimal AskSize { get; init; }

    /// <summary>Gets the timestamp.</summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Venue selection result.
/// </summary>
internal sealed class VenueSelection
{
    public IExecutionVenue? Venue { get; init; }
    public VenueQuote? Quote { get; init; }
    public decimal AvailableQuantity { get; init; }
    public decimal Price { get; init; }
    public decimal EstimatedFees { get; init; }
}

/// <summary>
/// Routing result.
/// </summary>
public sealed class RoutingResult
{
    /// <summary>Gets the order identifier.</summary>
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Gets the requested quantity.</summary>
    public decimal RequestedQuantity { get; init; }

    /// <summary>Gets the filled quantity.</summary>
    public decimal FilledQuantity { get; set; }

    /// <summary>Gets the order side.</summary>
    public OrderSide Side { get; init; }

    /// <summary>Gets the average fill price.</summary>
    public decimal AveragePrice { get; set; }

    /// <summary>Gets the total fees.</summary>
    public decimal TotalFees { get; set; }

    /// <summary>Gets the routing status.</summary>
    public RoutingStatus Status { get; set; }

    /// <summary>Gets the fills from each venue.</summary>
    public IReadOnlyList<VenueFill> Fills { get; set; } = Array.Empty<VenueFill>();

    /// <summary>Gets the routing latency in milliseconds.</summary>
    public double RoutingLatencyMs { get; set; }

    /// <summary>Gets the execution quality metrics.</summary>
    public ExecutionQuality? ExecutionQuality { get; set; }

    /// <summary>Gets the error message if any.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Routing status.
/// </summary>
public enum RoutingStatus
{
    /// <summary>Order is pending.</summary>
    Pending,

    /// <summary>Order was fully filled.</summary>
    FullyFilled,

    /// <summary>Order was partially filled.</summary>
    PartiallyFilled,

    /// <summary>Order was rejected.</summary>
    Rejected,

    /// <summary>No venues available.</summary>
    NoVenuesAvailable,

    /// <summary>No liquidity available.</summary>
    NoLiquidityAvailable,

    /// <summary>Error occurred.</summary>
    Error
}

/// <summary>
/// Fill from a specific venue.
/// </summary>
public sealed class VenueFill
{
    /// <summary>Gets the venue identifier.</summary>
    public string VenueId { get; init; } = string.Empty;

    /// <summary>Gets the venue name.</summary>
    public string VenueName { get; init; } = string.Empty;

    /// <summary>Gets the venue order ID.</summary>
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Gets the filled quantity.</summary>
    public decimal FilledQuantity { get; init; }

    /// <summary>Gets the average price.</summary>
    public decimal AveragePrice { get; init; }

    /// <summary>Gets the fees.</summary>
    public decimal Fees { get; init; }

    /// <summary>Gets the latency in milliseconds.</summary>
    public double LatencyMs { get; init; }

    /// <summary>Gets the fill timestamp.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Gets the fill status.</summary>
    public FillStatus Status { get; init; }

    /// <summary>Gets any error message.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Fill status.
/// </summary>
public enum FillStatus
{
    /// <summary>Fill succeeded.</summary>
    Filled,

    /// <summary>Partially filled.</summary>
    PartialFill,

    /// <summary>Fill was rejected.</summary>
    Rejected,

    /// <summary>Fill was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Venue order execution result.
/// </summary>
public sealed class VenueOrderResult
{
    /// <summary>Gets the order ID.</summary>
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Gets the filled quantity.</summary>
    public decimal FilledQuantity { get; init; }

    /// <summary>Gets the average price.</summary>
    public decimal AveragePrice { get; init; }

    /// <summary>Gets the fees.</summary>
    public decimal Fees { get; init; }

    /// <summary>Gets the status.</summary>
    public FillStatus Status { get; init; }
}

/// <summary>
/// Best price information.
/// </summary>
public sealed class BestPrice
{
    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Gets the order side.</summary>
    public OrderSide Side { get; init; }

    /// <summary>Gets the best price.</summary>
    public decimal Price { get; init; }

    /// <summary>Gets the size at best price.</summary>
    public decimal Size { get; init; }

    /// <summary>Gets the venue offering the best price.</summary>
    public string VenueId { get; init; } = string.Empty;

    /// <summary>Gets the timestamp.</summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// National Best Bid and Offer.
/// </summary>
public sealed class NBBO
{
    /// <summary>Gets the symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Gets the best bid price.</summary>
    public decimal BidPrice { get; init; }

    /// <summary>Gets the size at best bid.</summary>
    public decimal BidSize { get; init; }

    /// <summary>Gets the venue with best bid.</summary>
    public string BidVenue { get; init; } = string.Empty;

    /// <summary>Gets the best ask price.</summary>
    public decimal AskPrice { get; init; }

    /// <summary>Gets the size at best ask.</summary>
    public decimal AskSize { get; init; }

    /// <summary>Gets the venue with best ask.</summary>
    public string AskVenue { get; init; } = string.Empty;

    /// <summary>Gets the spread.</summary>
    public decimal Spread { get; init; }

    /// <summary>Gets the mid price.</summary>
    public decimal MidPrice { get; init; }

    /// <summary>Gets the timestamp.</summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Execution quality metrics.
/// </summary>
public sealed class ExecutionQuality
{
    /// <summary>Gets the slippage (price difference from mid).</summary>
    public decimal Slippage { get; init; }

    /// <summary>Gets the slippage in basis points.</summary>
    public decimal SlippageBps { get; init; }

    /// <summary>Gets the price improvement vs NBBO.</summary>
    public decimal PriceImprovement { get; init; }

    /// <summary>Gets the price improvement in basis points.</summary>
    public decimal PriceImprovementBps { get; init; }

    /// <summary>Gets the effective spread.</summary>
    public decimal EffectiveSpread { get; init; }

    /// <summary>Gets the fill rate (0-1).</summary>
    public decimal FillRate { get; init; }

    /// <summary>Gets the number of venues used.</summary>
    public int VenuesUsed { get; init; }

    /// <summary>Gets the total latency in milliseconds.</summary>
    public double TotalLatencyMs { get; init; }
}
