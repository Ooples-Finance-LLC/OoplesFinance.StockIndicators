namespace OoplesFinance.StockIndicators.Builder.Compliance;

/// <summary>
/// Pattern Day Trader (PDT) rule checker.
/// PDT rule: 4+ day trades in 5 business days with less than $25,000 equity.
/// </summary>
public sealed class PDTRuleChecker
{
    private readonly decimal _pdtThreshold;
    private readonly int _dayTradeLimit;
    private readonly int _lookbackDays;

    /// <summary>
    /// Creates a new PDT rule checker with default SEC rules.
    /// </summary>
    public PDTRuleChecker()
        : this(pdtThreshold: 25000m, dayTradeLimit: 3, lookbackDays: 5)
    {
    }

    /// <summary>
    /// Creates a new PDT rule checker with custom parameters.
    /// </summary>
    /// <param name="pdtThreshold">Equity threshold for PDT (default $25,000).</param>
    /// <param name="dayTradeLimit">Max day trades before PDT (default 3).</param>
    /// <param name="lookbackDays">Business days to look back (default 5).</param>
    public PDTRuleChecker(decimal pdtThreshold, int dayTradeLimit, int lookbackDays)
    {
        _pdtThreshold = pdtThreshold;
        _dayTradeLimit = dayTradeLimit;
        _lookbackDays = lookbackDays;
    }

    /// <summary>
    /// Checks if an account is subject to PDT restrictions.
    /// </summary>
    /// <param name="accountEquity">Current account equity.</param>
    /// <returns>True if equity is below PDT threshold.</returns>
    public bool IsSubjectToPDT(decimal accountEquity) => accountEquity < _pdtThreshold;

    /// <summary>
    /// Counts day trades in the lookback period.
    /// A day trade is opening and closing the same position on the same day.
    /// </summary>
    /// <param name="trades">List of recent trades.</param>
    /// <returns>Number of day trades in the lookback period.</returns>
    public int CountDayTrades(IReadOnlyList<TradeRecord> trades)
    {
        var cutoffDate = GetLookbackStartDate();
        var recentTrades = trades.Where(t => t.ExecutedAt.Date >= cutoffDate).ToList();

        // Group by date and symbol
        var dayTrades = new HashSet<(DateTime Date, string Symbol)>();

        foreach (var trade in recentTrades)
        {
            var date = trade.ExecutedAt.Date;
            var symbol = trade.Symbol;

            // Check if there's an opposite trade on the same day
            var hasOpposite = recentTrades.Any(t =>
                t.ExecutedAt.Date == date &&
                t.Symbol == symbol &&
                t.Side != trade.Side &&
                t != trade);

            if (hasOpposite)
            {
                dayTrades.Add((date, symbol));
            }
        }

        return dayTrades.Count;
    }

    /// <summary>
    /// Checks if executing a trade would violate PDT rules.
    /// </summary>
    /// <param name="trades">Recent trades.</param>
    /// <param name="accountEquity">Current account equity.</param>
    /// <param name="proposedTrade">The trade being considered.</param>
    /// <returns>PDT check result.</returns>
    public PDTCheckResult CheckPDTViolation(
        IReadOnlyList<TradeRecord> trades,
        decimal accountEquity,
        TradeRecord proposedTrade)
    {
        var result = new PDTCheckResult
        {
            AccountEquity = accountEquity,
            PDTThreshold = _pdtThreshold,
            IsSubjectToPDT = IsSubjectToPDT(accountEquity)
        };

        if (!result.IsSubjectToPDT)
        {
            result.IsAllowed = true;
            return result;
        }

        var currentDayTrades = CountDayTrades(trades);
        result.CurrentDayTradeCount = currentDayTrades;
        result.DayTradesRemaining = _dayTradeLimit - currentDayTrades;

        // Check if proposed trade would be a day trade
        var wouldBeDayTrade = trades.Any(t =>
            t.ExecutedAt.Date == proposedTrade.ExecutedAt.Date &&
            t.Symbol == proposedTrade.Symbol &&
            t.Side != proposedTrade.Side);

        result.WouldBeDayTrade = wouldBeDayTrade;

        if (wouldBeDayTrade)
        {
            result.IsAllowed = currentDayTrades < _dayTradeLimit;
            if (!result.IsAllowed)
            {
                result.ViolationMessage = $"This trade would exceed the {_dayTradeLimit} day trade limit. " +
                    $"You have {currentDayTrades} day trades in the last {_lookbackDays} business days.";
            }
        }
        else
        {
            result.IsAllowed = true;
        }

        return result;
    }

    /// <summary>
    /// Gets the number of day trades remaining.
    /// </summary>
    public int GetDayTradesRemaining(IReadOnlyList<TradeRecord> trades, decimal accountEquity)
    {
        if (!IsSubjectToPDT(accountEquity)) return int.MaxValue;
        return _dayTradeLimit - CountDayTrades(trades);
    }

    private DateTime GetLookbackStartDate()
    {
        var businessDays = 0;
        var date = DateTime.UtcNow.Date;

        while (businessDays < _lookbackDays)
        {
            date = date.AddDays(-1);
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                businessDays++;
            }
        }

        return date;
    }
}

/// <summary>
/// Result of PDT rule check.
/// </summary>
public sealed class PDTCheckResult
{
    /// <summary>Gets or sets whether the trade is allowed.</summary>
    public bool IsAllowed { get; set; }

    /// <summary>Gets or sets whether the account is subject to PDT.</summary>
    public bool IsSubjectToPDT { get; set; }

    /// <summary>Gets or sets the account equity.</summary>
    public decimal AccountEquity { get; set; }

    /// <summary>Gets or sets the PDT threshold.</summary>
    public decimal PDTThreshold { get; set; }

    /// <summary>Gets or sets the current day trade count.</summary>
    public int CurrentDayTradeCount { get; set; }

    /// <summary>Gets or sets day trades remaining.</summary>
    public int DayTradesRemaining { get; set; }

    /// <summary>Gets or sets whether the proposed trade would be a day trade.</summary>
    public bool WouldBeDayTrade { get; set; }

    /// <summary>Gets or sets the violation message.</summary>
    public string? ViolationMessage { get; set; }
}

/// <summary>
/// Wash sale rule tracker.
/// IRS wash sale rule: Loss disallowed if same or substantially identical security
/// purchased within 30 days before or after the sale.
/// </summary>
public sealed class WashSaleTracker
{
    private readonly int _washSaleWindow;

    /// <summary>
    /// Creates a new wash sale tracker with default 30-day window.
    /// </summary>
    public WashSaleTracker() : this(30) { }

    /// <summary>
    /// Creates a new wash sale tracker with custom window.
    /// </summary>
    /// <param name="washSaleWindow">Days before and after sale to check.</param>
    public WashSaleTracker(int washSaleWindow)
    {
        _washSaleWindow = washSaleWindow;
    }

    /// <summary>
    /// Checks if a sale would trigger a wash sale.
    /// </summary>
    /// <param name="sellTrade">The sale being analyzed.</param>
    /// <param name="allTrades">All trades to check against.</param>
    /// <returns>Wash sale analysis result.</returns>
    public WashSaleResult CheckWashSale(TradeRecord sellTrade, IReadOnlyList<TradeRecord> allTrades)
    {
        if (sellTrade.Side != TradeSide.Sell)
        {
            return new WashSaleResult { IsWashSale = false };
        }

        var result = new WashSaleResult
        {
            Symbol = sellTrade.Symbol,
            SaleDate = sellTrade.ExecutedAt.Date,
            SalePrice = sellTrade.Price,
            SaleQuantity = sellTrade.Quantity,
            OriginalCostBasis = sellTrade.CostBasis ?? 0m
        };

        // Calculate loss on the sale
        var saleProceeds = sellTrade.Price * sellTrade.Quantity;
        var costBasis = sellTrade.CostBasis ?? 0m;
        var loss = costBasis - saleProceeds;

        if (loss <= 0)
        {
            // No loss, no wash sale
            result.IsWashSale = false;
            return result;
        }

        result.DisallowedLoss = loss;

        // Check for purchases within the wash sale window
        var windowStart = sellTrade.ExecutedAt.Date.AddDays(-_washSaleWindow);
        var windowEnd = sellTrade.ExecutedAt.Date.AddDays(_washSaleWindow);

        var washSalePurchases = allTrades
            .Where(t =>
                t.Symbol == sellTrade.Symbol &&
                t.Side == TradeSide.Buy &&
                t.ExecutedAt.Date >= windowStart &&
                t.ExecutedAt.Date <= windowEnd &&
                t != sellTrade)
            .ToList();

        if (washSalePurchases.Count > 0)
        {
            result.IsWashSale = true;
            result.TriggeringPurchases = washSalePurchases;

            // Calculate adjusted cost basis for replacement shares
            var totalReplacementShares = washSalePurchases.Sum(p => p.Quantity);
            var disallowedLossPerShare = loss / sellTrade.Quantity;

            // Adjust basis on replacement shares (capped at actual replacement quantity)
            var sharesToAdjust = Math.Min(sellTrade.Quantity, totalReplacementShares);
            result.AdjustedCostBasis = washSalePurchases.Sum(p => p.Price * p.Quantity)
                + disallowedLossPerShare * sharesToAdjust;
        }

        return result;
    }

    /// <summary>
    /// Gets all wash sales in a list of trades.
    /// </summary>
    public IReadOnlyList<WashSaleResult> IdentifyWashSales(IReadOnlyList<TradeRecord> trades)
    {
        var results = new List<WashSaleResult>();

        var sellTrades = trades.Where(t => t.Side == TradeSide.Sell).ToList();
        foreach (var sell in sellTrades)
        {
            var result = CheckWashSale(sell, trades);
            if (result.IsWashSale)
            {
                results.Add(result);
            }
        }

        return results;
    }
}

/// <summary>
/// Result of wash sale check.
/// </summary>
public sealed class WashSaleResult
{
    /// <summary>Gets or sets whether this is a wash sale.</summary>
    public bool IsWashSale { get; set; }

    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the sale date.</summary>
    public DateTime SaleDate { get; set; }

    /// <summary>Gets or sets the sale price.</summary>
    public decimal SalePrice { get; set; }

    /// <summary>Gets or sets the sale quantity.</summary>
    public decimal SaleQuantity { get; set; }

    /// <summary>Gets or sets the original cost basis.</summary>
    public decimal OriginalCostBasis { get; set; }

    /// <summary>Gets or sets the disallowed loss amount.</summary>
    public decimal DisallowedLoss { get; set; }

    /// <summary>Gets or sets the adjusted cost basis for replacement shares.</summary>
    public decimal AdjustedCostBasis { get; set; }

    /// <summary>Gets or sets the purchases that triggered the wash sale.</summary>
    public IReadOnlyList<TradeRecord> TriggeringPurchases { get; set; } = Array.Empty<TradeRecord>();
}

/// <summary>
/// Tax lot accounting methods.
/// </summary>
public enum TaxLotMethod
{
    /// <summary>First In, First Out.</summary>
    FIFO,

    /// <summary>Last In, First Out.</summary>
    LIFO,

    /// <summary>Specific identification (user selects lots).</summary>
    SpecificIdentification,

    /// <summary>Highest cost first (minimizes short-term gains).</summary>
    HighestCost,

    /// <summary>Lowest cost first (maximizes deferred gains).</summary>
    LowestCost,

    /// <summary>Average cost (mutual funds only).</summary>
    AverageCost
}

/// <summary>
/// Tax lot accounting for tracking cost basis.
/// </summary>
public sealed class TaxLotAccounting
{
    private readonly List<TaxLot> _lots = new();

    /// <summary>Gets or sets the accounting method.</summary>
    public TaxLotMethod Method { get; set; } = TaxLotMethod.FIFO;

    /// <summary>Gets all open tax lots.</summary>
    public IReadOnlyList<TaxLot> OpenLots => _lots.Where(l => l.RemainingQuantity > 0).ToList();

    /// <summary>
    /// Records a purchase, creating new tax lots.
    /// </summary>
    public TaxLot AddPurchase(string symbol, decimal quantity, decimal price, DateTime date, decimal? commission = null)
    {
        var lot = new TaxLot
        {
            LotId = Guid.NewGuid().ToString(),
            Symbol = symbol,
            OriginalQuantity = quantity,
            RemainingQuantity = quantity,
            CostPerShare = price,
            TotalCost = quantity * price + (commission ?? 0),
            AcquiredDate = date,
            HoldingPeriod = HoldingPeriod.ShortTerm
        };

        _lots.Add(lot);
        return lot;
    }

    /// <summary>
    /// Processes a sale and returns the lots used.
    /// </summary>
    /// <param name="symbol">Symbol being sold.</param>
    /// <param name="quantity">Quantity to sell.</param>
    /// <param name="salePrice">Sale price per share.</param>
    /// <param name="saleDate">Sale date.</param>
    /// <param name="specificLotIds">Specific lot IDs (for SpecificIdentification method).</param>
    /// <returns>Tax impact of the sale.</returns>
    public TaxImpact ProcessSale(
        string symbol,
        decimal quantity,
        decimal salePrice,
        DateTime saleDate,
        IReadOnlyList<string>? specificLotIds = null)
    {
        var lotsToUse = GetLotsForSale(symbol, quantity, specificLotIds);
        var impact = new TaxImpact { SaleDate = saleDate, SalePrice = salePrice };

        var remainingToSell = quantity;

        foreach (var lot in lotsToUse)
        {
            if (remainingToSell <= 0) break;

            var quantityFromLot = Math.Min(remainingToSell, lot.RemainingQuantity);
            var costBasisUsed = quantityFromLot * lot.CostPerShare;
            var proceeds = quantityFromLot * salePrice;
            var gain = proceeds - costBasisUsed;

            // Update holding period based on actual sale date
            var daysHeld = (saleDate - lot.AcquiredDate).Days;
            var holdingPeriod = daysHeld > 365 ? HoldingPeriod.LongTerm : HoldingPeriod.ShortTerm;

            impact.LotsSold.Add(new SoldLot
            {
                LotId = lot.LotId,
                Quantity = quantityFromLot,
                CostBasis = costBasisUsed,
                Proceeds = proceeds,
                Gain = gain,
                HoldingPeriod = holdingPeriod,
                AcquiredDate = lot.AcquiredDate
            });

            if (holdingPeriod == HoldingPeriod.ShortTerm)
            {
                impact.ShortTermGain += gain;
            }
            else
            {
                impact.LongTermGain += gain;
            }

            impact.TotalCostBasis += costBasisUsed;
            impact.TotalProceeds += proceeds;

            lot.RemainingQuantity -= quantityFromLot;
            remainingToSell -= quantityFromLot;
        }

        impact.TotalGain = impact.TotalProceeds - impact.TotalCostBasis;
        return impact;
    }

    /// <summary>
    /// Gets lots to use for a sale based on the accounting method.
    /// </summary>
    public IReadOnlyList<TaxLot> GetLotsForSale(
        string symbol,
        decimal quantity,
        IReadOnlyList<string>? specificLotIds = null)
    {
        var availableLots = _lots
            .Where(l => l.Symbol == symbol && l.RemainingQuantity > 0)
            .ToList();

        if (specificLotIds?.Count > 0)
        {
            return availableLots
                .Where(l => specificLotIds.Contains(l.LotId))
                .ToList();
        }

        return Method switch
        {
            TaxLotMethod.FIFO => availableLots.OrderBy(l => l.AcquiredDate).ToList(),
            TaxLotMethod.LIFO => availableLots.OrderByDescending(l => l.AcquiredDate).ToList(),
            TaxLotMethod.HighestCost => availableLots.OrderByDescending(l => l.CostPerShare).ToList(),
            TaxLotMethod.LowestCost => availableLots.OrderBy(l => l.CostPerShare).ToList(),
            _ => availableLots.OrderBy(l => l.AcquiredDate).ToList()
        };
    }

    /// <summary>
    /// Calculates what-if tax impact for a proposed sale.
    /// </summary>
    public TaxImpact CalculateTaxImpact(string symbol, decimal quantity, decimal salePrice, DateTime saleDate)
    {
        // Clone lots for simulation
        var simulatedLots = _lots
            .Where(l => l.Symbol == symbol && l.RemainingQuantity > 0)
            .Select(l => new TaxLot
            {
                LotId = l.LotId,
                Symbol = l.Symbol,
                OriginalQuantity = l.OriginalQuantity,
                RemainingQuantity = l.RemainingQuantity,
                CostPerShare = l.CostPerShare,
                TotalCost = l.TotalCost,
                AcquiredDate = l.AcquiredDate
            })
            .ToList();

        // Calculate impact without modifying actual lots
        var tempAccounting = new TaxLotAccounting { Method = this.Method };
        foreach (var lot in simulatedLots)
        {
            tempAccounting._lots.Add(lot);
        }

        return tempAccounting.ProcessSale(symbol, quantity, salePrice, saleDate);
    }

    /// <summary>
    /// Gets tax lot summary for a symbol.
    /// </summary>
    public TaxLotSummary GetSummary(string symbol)
    {
        var symbolLots = _lots.Where(l => l.Symbol == symbol && l.RemainingQuantity > 0).ToList();

        return new TaxLotSummary
        {
            Symbol = symbol,
            TotalQuantity = symbolLots.Sum(l => l.RemainingQuantity),
            TotalCostBasis = symbolLots.Sum(l => l.RemainingQuantity * l.CostPerShare),
            AverageCost = symbolLots.Count > 0
                ? symbolLots.Sum(l => l.RemainingQuantity * l.CostPerShare) / symbolLots.Sum(l => l.RemainingQuantity)
                : 0m,
            ShortTermQuantity = symbolLots
                .Where(l => (DateTime.UtcNow - l.AcquiredDate).Days <= 365)
                .Sum(l => l.RemainingQuantity),
            LongTermQuantity = symbolLots
                .Where(l => (DateTime.UtcNow - l.AcquiredDate).Days > 365)
                .Sum(l => l.RemainingQuantity),
            LotCount = symbolLots.Count
        };
    }
}

/// <summary>
/// A single tax lot.
/// </summary>
public sealed class TaxLot
{
    /// <summary>Gets or sets the lot ID.</summary>
    public string LotId { get; set; } = string.Empty;

    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the original quantity purchased.</summary>
    public decimal OriginalQuantity { get; set; }

    /// <summary>Gets or sets the remaining quantity.</summary>
    public decimal RemainingQuantity { get; set; }

    /// <summary>Gets or sets the cost per share.</summary>
    public decimal CostPerShare { get; set; }

    /// <summary>Gets or sets the total cost including commissions.</summary>
    public decimal TotalCost { get; set; }

    /// <summary>Gets or sets the acquisition date.</summary>
    public DateTime AcquiredDate { get; set; }

    /// <summary>Gets or sets the holding period.</summary>
    public HoldingPeriod HoldingPeriod { get; set; }

    /// <summary>Gets days held.</summary>
    public int DaysHeld => (DateTime.UtcNow - AcquiredDate).Days;

    /// <summary>Gets whether this is a long-term holding.</summary>
    public bool IsLongTerm => DaysHeld > 365;
}

/// <summary>
/// Holding period for tax purposes.
/// </summary>
public enum HoldingPeriod
{
    /// <summary>Held one year or less.</summary>
    ShortTerm,

    /// <summary>Held more than one year.</summary>
    LongTerm
}

/// <summary>
/// Tax impact of a sale.
/// </summary>
public sealed class TaxImpact
{
    /// <summary>Gets or sets the sale date.</summary>
    public DateTime SaleDate { get; set; }

    /// <summary>Gets or sets the sale price.</summary>
    public decimal SalePrice { get; set; }

    /// <summary>Gets or sets the total proceeds.</summary>
    public decimal TotalProceeds { get; set; }

    /// <summary>Gets or sets the total cost basis.</summary>
    public decimal TotalCostBasis { get; set; }

    /// <summary>Gets or sets the total gain/loss.</summary>
    public decimal TotalGain { get; set; }

    /// <summary>Gets or sets the short-term gain/loss.</summary>
    public decimal ShortTermGain { get; set; }

    /// <summary>Gets or sets the long-term gain/loss.</summary>
    public decimal LongTermGain { get; set; }

    /// <summary>Gets or sets the lots that were sold.</summary>
    public List<SoldLot> LotsSold { get; set; } = new();

    /// <summary>Gets whether this resulted in a gain.</summary>
    public bool IsGain => TotalGain > 0;

    /// <summary>Gets whether this resulted in a loss.</summary>
    public bool IsLoss => TotalGain < 0;
}

/// <summary>
/// Details of a lot sold.
/// </summary>
public sealed class SoldLot
{
    /// <summary>Gets or sets the lot ID.</summary>
    public string LotId { get; set; } = string.Empty;

    /// <summary>Gets or sets the quantity sold from this lot.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the cost basis.</summary>
    public decimal CostBasis { get; set; }

    /// <summary>Gets or sets the proceeds.</summary>
    public decimal Proceeds { get; set; }

    /// <summary>Gets or sets the gain/loss.</summary>
    public decimal Gain { get; set; }

    /// <summary>Gets or sets the holding period.</summary>
    public HoldingPeriod HoldingPeriod { get; set; }

    /// <summary>Gets or sets the original acquisition date.</summary>
    public DateTime AcquiredDate { get; set; }
}

/// <summary>
/// Summary of tax lots for a symbol.
/// </summary>
public sealed class TaxLotSummary
{
    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the total quantity.</summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>Gets or sets the total cost basis.</summary>
    public decimal TotalCostBasis { get; set; }

    /// <summary>Gets or sets the average cost per share.</summary>
    public decimal AverageCost { get; set; }

    /// <summary>Gets or sets the short-term quantity.</summary>
    public decimal ShortTermQuantity { get; set; }

    /// <summary>Gets or sets the long-term quantity.</summary>
    public decimal LongTermQuantity { get; set; }

    /// <summary>Gets or sets the number of lots.</summary>
    public int LotCount { get; set; }
}

/// <summary>
/// Trade record for compliance tracking.
/// </summary>
public sealed class TradeRecord
{
    /// <summary>Gets or sets the trade ID.</summary>
    public string TradeId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the side (buy/sell).</summary>
    public TradeSide Side { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the cost basis (for sells).</summary>
    public decimal? CostBasis { get; set; }

    /// <summary>Gets or sets when the trade was executed.</summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>Gets or sets the order ID.</summary>
    public string? OrderId { get; set; }

    /// <summary>Gets or sets the commission.</summary>
    public decimal Commission { get; set; }

    /// <summary>Gets the trade value.</summary>
    public decimal Value => Price * Quantity;
}

/// <summary>
/// Trade side.
/// </summary>
public enum TradeSide
{
    /// <summary>Buy trade.</summary>
    Buy,

    /// <summary>Sell trade.</summary>
    Sell
}
