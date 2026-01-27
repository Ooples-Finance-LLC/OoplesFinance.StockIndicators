namespace OoplesFinance.StockIndicators.Builder.Futures;

/// <summary>
/// Represents a futures contract specification.
/// </summary>
public sealed class FuturesContract
{
    /// <summary>Gets the contract symbol (e.g., "ES", "NQ", "CL").</summary>
    public string Symbol { get; }

    /// <summary>Gets the full contract symbol with expiration (e.g., "ESH24").</summary>
    public string FullSymbol { get; }

    /// <summary>Gets the underlying asset or index.</summary>
    public string Underlying { get; }

    /// <summary>Gets the exchange where the contract trades.</summary>
    public string Exchange { get; }

    /// <summary>Gets the contract expiration date.</summary>
    public DateTime ExpirationDate { get; }

    /// <summary>Gets the last trading date.</summary>
    public DateTime LastTradingDate { get; }

    /// <summary>Gets the first notice date (for physical delivery contracts).</summary>
    public DateTime? FirstNoticeDate { get; }

    /// <summary>Gets the contract multiplier (point value).</summary>
    public decimal Multiplier { get; }

    /// <summary>Gets the tick size (minimum price movement).</summary>
    public decimal TickSize { get; }

    /// <summary>Gets the tick value (dollar value of one tick).</summary>
    public decimal TickValue { get; }

    /// <summary>Gets the settlement type.</summary>
    public SettlementType Settlement { get; }

    /// <summary>Gets the asset class.</summary>
    public FuturesAssetClass AssetClass { get; }

    /// <summary>Gets the contract month code.</summary>
    public char MonthCode { get; }

    /// <summary>Gets the contract year (2-digit).</summary>
    public int Year { get; }

    /// <summary>Gets the initial margin requirement.</summary>
    public decimal InitialMargin { get; set; }

    /// <summary>Gets the maintenance margin requirement.</summary>
    public decimal MaintenanceMargin { get; set; }

    /// <summary>Gets the trading hours.</summary>
    public TradingHours TradingHours { get; set; } = TradingHours.Default;

    /// <summary>
    /// Creates a new futures contract.
    /// </summary>
    public FuturesContract(
        string symbol,
        string underlying,
        string exchange,
        DateTime expirationDate,
        decimal multiplier,
        decimal tickSize,
        SettlementType settlement = SettlementType.Cash,
        FuturesAssetClass assetClass = FuturesAssetClass.Index)
    {
        Symbol = symbol;
        Underlying = underlying;
        Exchange = exchange;
        ExpirationDate = expirationDate;
        Multiplier = multiplier;
        TickSize = tickSize;
        TickValue = tickSize * multiplier;
        Settlement = settlement;
        AssetClass = assetClass;

        // Calculate month code and year
        MonthCode = GetMonthCode(expirationDate.Month);
        Year = expirationDate.Year % 100;
        FullSymbol = $"{symbol}{MonthCode}{Year}";

        // Set last trading date (typically 3rd Friday for index futures)
        LastTradingDate = CalculateLastTradingDate(expirationDate, assetClass);
    }

    /// <summary>
    /// Calculates the notional value of the contract.
    /// </summary>
    public decimal CalculateNotionalValue(decimal price)
    {
        return price * Multiplier;
    }

    /// <summary>
    /// Calculates P&L for a position.
    /// </summary>
    public decimal CalculatePnL(decimal entryPrice, decimal currentPrice, int contracts, bool isLong)
    {
        var priceDiff = currentPrice - entryPrice;
        var pnl = priceDiff * Multiplier * contracts;
        return isLong ? pnl : -pnl;
    }

    /// <summary>
    /// Calculates P&L in ticks.
    /// </summary>
    public decimal CalculatePnLInTicks(decimal entryPrice, decimal currentPrice, int contracts, bool isLong)
    {
        var ticks = (currentPrice - entryPrice) / TickSize;
        return isLong ? ticks * contracts : -ticks * contracts;
    }

    /// <summary>
    /// Gets the number of days until expiration.
    /// </summary>
    public int DaysToExpiration => Math.Max(0, (ExpirationDate.Date - DateTime.UtcNow.Date).Days);

    /// <summary>
    /// Gets whether the contract is expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow.Date > ExpirationDate.Date;

    /// <summary>
    /// Gets whether the contract is the front month.
    /// </summary>
    public bool IsFrontMonth(IEnumerable<FuturesContract> allContracts)
    {
        var activeContracts = allContracts
            .Where(c => c.Symbol == Symbol && !c.IsExpired)
            .OrderBy(c => c.ExpirationDate)
            .ToList();

        return activeContracts.FirstOrDefault()?.FullSymbol == FullSymbol;
    }

    private static char GetMonthCode(int month) => month switch
    {
        1 => 'F',  // January
        2 => 'G',  // February
        3 => 'H',  // March
        4 => 'J',  // April
        5 => 'K',  // May
        6 => 'M',  // June
        7 => 'N',  // July
        8 => 'Q',  // August
        9 => 'U',  // September
        10 => 'V', // October
        11 => 'X', // November
        12 => 'Z', // December
        _ => throw new ArgumentException($"Invalid month: {month}")
    };

    private static DateTime CalculateLastTradingDate(DateTime expiration, FuturesAssetClass assetClass)
    {
        // Simplified - actual rules vary by contract
        return assetClass switch
        {
            FuturesAssetClass.Index => GetThirdFriday(expiration.Year, expiration.Month),
            FuturesAssetClass.Energy => expiration.AddDays(-3),
            FuturesAssetClass.Metals => expiration.AddDays(-2),
            FuturesAssetClass.Agriculture => expiration.AddDays(-5),
            _ => expiration.AddDays(-1)
        };
    }

    private static DateTime GetThirdFriday(int year, int month)
    {
        var firstDay = new DateTime(year, month, 1);
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)firstDay.DayOfWeek + 7) % 7;
        return firstDay.AddDays(daysUntilFriday + 14); // Third Friday
    }

    public override string ToString() => $"{FullSymbol} ({Underlying})";
}

/// <summary>
/// Futures settlement types.
/// </summary>
public enum SettlementType
{
    /// <summary>Cash settlement on expiration.</summary>
    Cash,

    /// <summary>Physical delivery of underlying.</summary>
    Physical
}

/// <summary>
/// Futures asset classes.
/// </summary>
public enum FuturesAssetClass
{
    /// <summary>Stock index futures (ES, NQ, YM, RTY).</summary>
    Index,

    /// <summary>Interest rate futures (ZN, ZB, ZF, ZT).</summary>
    InterestRate,

    /// <summary>Energy futures (CL, NG, RB, HO).</summary>
    Energy,

    /// <summary>Metals futures (GC, SI, HG, PL).</summary>
    Metals,

    /// <summary>Agricultural futures (ZC, ZS, ZW, KC).</summary>
    Agriculture,

    /// <summary>Currency futures (6E, 6B, 6J).</summary>
    Currency,

    /// <summary>Cryptocurrency futures (BTC, ETH).</summary>
    Crypto
}

/// <summary>
/// Trading hours specification.
/// </summary>
public sealed class TradingHours
{
    /// <summary>Gets the regular trading session start (UTC).</summary>
    public TimeSpan RegularOpen { get; init; }

    /// <summary>Gets the regular trading session end (UTC).</summary>
    public TimeSpan RegularClose { get; init; }

    /// <summary>Gets the extended/electronic trading start (UTC).</summary>
    public TimeSpan ExtendedOpen { get; init; }

    /// <summary>Gets the extended/electronic trading end (UTC).</summary>
    public TimeSpan ExtendedClose { get; init; }

    /// <summary>Gets whether 24-hour trading is available.</summary>
    public bool Is24Hour { get; init; }

    /// <summary>Gets days when the market is closed.</summary>
    public IReadOnlyList<DayOfWeek> ClosedDays { get; init; } = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };

    /// <summary>Default trading hours (ES/NQ style - nearly 24 hours).</summary>
    public static TradingHours Default { get; } = new()
    {
        RegularOpen = new TimeSpan(14, 30, 0),  // 9:30 AM ET
        RegularClose = new TimeSpan(21, 0, 0), // 4:00 PM ET
        ExtendedOpen = new TimeSpan(23, 0, 0), // 6:00 PM ET Sunday
        ExtendedClose = new TimeSpan(22, 0, 0), // 5:00 PM ET Friday
        Is24Hour = false
    };

    /// <summary>
    /// Checks if trading is active at the given UTC time.
    /// </summary>
    public bool IsTradingActive(DateTime utcTime)
    {
        if (ClosedDays.Contains(utcTime.DayOfWeek)) return false;
        var time = utcTime.TimeOfDay;
        return time >= ExtendedOpen || time < ExtendedClose;
    }
}

/// <summary>
/// Futures position with margin and roll tracking.
/// </summary>
public sealed class FuturesPosition
{
    /// <summary>Gets the contract.</summary>
    public FuturesContract Contract { get; }

    /// <summary>Gets the number of contracts (positive = long, negative = short).</summary>
    public int Contracts { get; private set; }

    /// <summary>Gets the average entry price.</summary>
    public decimal AverageEntryPrice { get; private set; }

    /// <summary>Gets or sets the current market price.</summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>Gets the entry time.</summary>
    public DateTime EntryTime { get; }

    /// <summary>Gets or sets the stop loss price.</summary>
    public decimal? StopLoss { get; set; }

    /// <summary>Gets or sets the take profit price.</summary>
    public decimal? TakeProfit { get; set; }

    /// <summary>Gets the realized P&L from closed portions.</summary>
    public decimal RealizedPnL { get; private set; }

    /// <summary>Gets whether this is a long position.</summary>
    public bool IsLong => Contracts > 0;

    /// <summary>
    /// Creates a new futures position.
    /// </summary>
    public FuturesPosition(FuturesContract contract, int contracts, decimal entryPrice, DateTime? entryTime = null)
    {
        Contract = contract;
        Contracts = contracts;
        AverageEntryPrice = entryPrice;
        CurrentPrice = entryPrice;
        EntryTime = entryTime ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the unrealized P&L.
    /// </summary>
    public decimal UnrealizedPnL => Contract.CalculatePnL(AverageEntryPrice, CurrentPrice, Math.Abs(Contracts), IsLong);

    /// <summary>
    /// Gets the total P&L (realized + unrealized).
    /// </summary>
    public decimal TotalPnL => RealizedPnL + UnrealizedPnL;

    /// <summary>
    /// Gets the notional value of the position.
    /// </summary>
    public decimal NotionalValue => Contract.CalculateNotionalValue(CurrentPrice) * Math.Abs(Contracts);

    /// <summary>
    /// Gets the margin required for this position.
    /// </summary>
    public decimal MarginRequired => Contract.InitialMargin * Math.Abs(Contracts);

    /// <summary>
    /// Adds to the position.
    /// </summary>
    public void Add(int additionalContracts, decimal price)
    {
        if ((Contracts > 0 && additionalContracts < 0) || (Contracts < 0 && additionalContracts > 0))
        {
            // Reducing position - calculate realized P&L
            var closingContracts = Math.Min(Math.Abs(Contracts), Math.Abs(additionalContracts));
            RealizedPnL += Contract.CalculatePnL(AverageEntryPrice, price, closingContracts, IsLong);

            if (Math.Abs(additionalContracts) > Math.Abs(Contracts))
            {
                // Flipping position
                AverageEntryPrice = price;
            }
        }
        else if (Contracts != 0)
        {
            // Adding to position - update average price
            var totalContracts = Math.Abs(Contracts) + Math.Abs(additionalContracts);
            AverageEntryPrice = (AverageEntryPrice * Math.Abs(Contracts) + price * Math.Abs(additionalContracts)) / totalContracts;
        }
        else
        {
            AverageEntryPrice = price;
        }

        Contracts += additionalContracts;
    }

    /// <summary>
    /// Calculates the P&L at a specific price.
    /// </summary>
    public decimal CalculatePnLAtPrice(decimal price)
    {
        return Contract.CalculatePnL(AverageEntryPrice, price, Math.Abs(Contracts), IsLong);
    }
}

/// <summary>
/// Manages futures contract rolling.
/// </summary>
public sealed class RollManager
{
    /// <summary>Gets or sets the default days before expiration to roll.</summary>
    public int DefaultRollDays { get; set; } = 5;

    /// <summary>Gets or sets the volume ratio threshold for roll detection.</summary>
    public decimal VolumeRatioThreshold { get; set; } = 1.5m;

    /// <summary>
    /// Determines if a position should be rolled.
    /// </summary>
    /// <param name="position">Current position.</param>
    /// <param name="frontMonthVolume">Current contract volume.</param>
    /// <param name="nextMonthVolume">Next contract volume.</param>
    /// <returns>True if position should be rolled.</returns>
    public RollDecision ShouldRoll(FuturesPosition position, long frontMonthVolume, long nextMonthVolume)
    {
        var daysToExpiry = position.Contract.DaysToExpiration;
        var volumeRatio = nextMonthVolume > 0 ? (decimal)frontMonthVolume / nextMonthVolume : decimal.MaxValue;

        // Roll conditions
        var isNearExpiry = daysToExpiry <= DefaultRollDays;
        var isVolumeShifted = volumeRatio < VolumeRatioThreshold;
        var isFirstNoticeApproaching = position.Contract.FirstNoticeDate.HasValue &&
            (position.Contract.FirstNoticeDate.Value - DateTime.UtcNow).TotalDays <= DefaultRollDays;

        var shouldRoll = isNearExpiry || isVolumeShifted || isFirstNoticeApproaching;

        return new RollDecision
        {
            ShouldRoll = shouldRoll,
            Reason = GetRollReason(isNearExpiry, isVolumeShifted, isFirstNoticeApproaching),
            DaysToExpiry = daysToExpiry,
            VolumeRatio = volumeRatio,
            Urgency = CalculateUrgency(daysToExpiry, isFirstNoticeApproaching)
        };
    }

    /// <summary>
    /// Calculates the roll cost (calendar spread).
    /// </summary>
    public decimal CalculateRollCost(
        FuturesContract frontContract,
        FuturesContract backContract,
        decimal frontPrice,
        decimal backPrice,
        int contracts)
    {
        var spread = backPrice - frontPrice;
        return spread * frontContract.Multiplier * contracts;
    }

    /// <summary>
    /// Gets the next contract in the roll cycle.
    /// </summary>
    public FuturesContract? GetNextContract(FuturesContract current, IEnumerable<FuturesContract> availableContracts)
    {
        return availableContracts
            .Where(c => c.Symbol == current.Symbol && c.ExpirationDate > current.ExpirationDate)
            .OrderBy(c => c.ExpirationDate)
            .FirstOrDefault();
    }

    private static string GetRollReason(bool nearExpiry, bool volumeShifted, bool firstNotice)
    {
        var reasons = new List<string>();
        if (nearExpiry) reasons.Add("Near expiration");
        if (volumeShifted) reasons.Add("Volume shifted to next contract");
        if (firstNotice) reasons.Add("Approaching first notice date");
        return string.Join("; ", reasons);
    }

    private static RollUrgency CalculateUrgency(int daysToExpiry, bool firstNotice)
    {
        if (firstNotice || daysToExpiry <= 1) return RollUrgency.Critical;
        if (daysToExpiry <= 3) return RollUrgency.High;
        if (daysToExpiry <= 5) return RollUrgency.Medium;
        return RollUrgency.Low;
    }
}

/// <summary>
/// Roll decision details.
/// </summary>
public sealed class RollDecision
{
    /// <summary>Gets whether the position should be rolled.</summary>
    public bool ShouldRoll { get; init; }

    /// <summary>Gets the reason for the roll decision.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets the days until expiration.</summary>
    public int DaysToExpiry { get; init; }

    /// <summary>Gets the front/back volume ratio.</summary>
    public decimal VolumeRatio { get; init; }

    /// <summary>Gets the roll urgency.</summary>
    public RollUrgency Urgency { get; init; }
}

/// <summary>
/// Roll urgency levels.
/// </summary>
public enum RollUrgency
{
    /// <summary>No immediate action needed.</summary>
    Low,

    /// <summary>Should roll soon.</summary>
    Medium,

    /// <summary>Should roll today.</summary>
    High,

    /// <summary>Must roll immediately.</summary>
    Critical
}

/// <summary>
/// Margin calculator for futures positions.
/// </summary>
public sealed class FuturesMarginCalculator
{
    /// <summary>
    /// Calculates SPAN-style margin for a portfolio of futures.
    /// </summary>
    /// <param name="positions">List of futures positions.</param>
    /// <param name="marginCredits">Optional margin credits for spreads.</param>
    /// <returns>Total margin requirement.</returns>
    public MarginRequirement CalculatePortfolioMargin(
        IReadOnlyList<FuturesPosition> positions,
        IReadOnlyDictionary<string, decimal>? marginCredits = null)
    {
        var initialMargin = 0m;
        var maintenanceMargin = 0m;

        foreach (var position in positions)
        {
            var contracts = Math.Abs(position.Contracts);
            initialMargin += position.Contract.InitialMargin * contracts;
            maintenanceMargin += position.Contract.MaintenanceMargin * contracts;
        }

        // Apply spread credits if provided
        if (marginCredits is not null)
        {
            foreach (var credit in marginCredits.Values)
            {
                initialMargin -= credit;
                maintenanceMargin -= credit;
            }
        }

        return new MarginRequirement
        {
            InitialMargin = Math.Max(0, initialMargin),
            MaintenanceMargin = Math.Max(0, maintenanceMargin),
            ExcessMargin = 0, // Would be calculated with account equity
            MarginCallLevel = Math.Max(0, maintenanceMargin)
        };
    }

    /// <summary>
    /// Checks if a margin call would occur.
    /// </summary>
    public bool IsMarginCall(decimal accountEquity, decimal maintenanceMargin)
    {
        return accountEquity < maintenanceMargin;
    }

    /// <summary>
    /// Calculates the maximum contracts tradeable given account equity.
    /// </summary>
    public int MaxContractsForEquity(FuturesContract contract, decimal equity, decimal marginBuffer = 1.2m)
    {
        if (contract.InitialMargin <= 0) return 0;
        var adjustedMargin = contract.InitialMargin * marginBuffer;
        return (int)Math.Floor(equity / adjustedMargin);
    }
}

/// <summary>
/// Margin requirement details.
/// </summary>
public sealed class MarginRequirement
{
    /// <summary>Gets the initial margin requirement.</summary>
    public decimal InitialMargin { get; init; }

    /// <summary>Gets the maintenance margin requirement.</summary>
    public decimal MaintenanceMargin { get; init; }

    /// <summary>Gets the excess margin available.</summary>
    public decimal ExcessMargin { get; init; }

    /// <summary>Gets the level at which margin call occurs.</summary>
    public decimal MarginCallLevel { get; init; }
}

/// <summary>
/// Common futures contract specifications.
/// </summary>
public static class CommonContracts
{
    /// <summary>
    /// Creates an E-mini S&P 500 contract.
    /// </summary>
    public static FuturesContract CreateES(int year, int month) => new(
        symbol: "ES",
        underlying: "S&P 500 Index",
        exchange: "CME",
        expirationDate: GetQuarterlyExpiration(year, month),
        multiplier: 50m,
        tickSize: 0.25m,
        settlement: SettlementType.Cash,
        assetClass: FuturesAssetClass.Index)
    {
        InitialMargin = 12650m,
        MaintenanceMargin = 11500m
    };

    /// <summary>
    /// Creates an E-mini NASDAQ-100 contract.
    /// </summary>
    public static FuturesContract CreateNQ(int year, int month) => new(
        symbol: "NQ",
        underlying: "NASDAQ-100 Index",
        exchange: "CME",
        expirationDate: GetQuarterlyExpiration(year, month),
        multiplier: 20m,
        tickSize: 0.25m,
        settlement: SettlementType.Cash,
        assetClass: FuturesAssetClass.Index)
    {
        InitialMargin = 17600m,
        MaintenanceMargin = 16000m
    };

    /// <summary>
    /// Creates a Crude Oil contract.
    /// </summary>
    public static FuturesContract CreateCL(int year, int month) => new(
        symbol: "CL",
        underlying: "WTI Crude Oil",
        exchange: "NYMEX",
        expirationDate: new DateTime(year, month, 20),
        multiplier: 1000m,
        tickSize: 0.01m,
        settlement: SettlementType.Physical,
        assetClass: FuturesAssetClass.Energy)
    {
        InitialMargin = 7700m,
        MaintenanceMargin = 7000m
    };

    /// <summary>
    /// Creates a Gold contract.
    /// </summary>
    public static FuturesContract CreateGC(int year, int month) => new(
        symbol: "GC",
        underlying: "Gold",
        exchange: "COMEX",
        expirationDate: new DateTime(year, month, 27),
        multiplier: 100m,
        tickSize: 0.10m,
        settlement: SettlementType.Physical,
        assetClass: FuturesAssetClass.Metals)
    {
        InitialMargin = 9900m,
        MaintenanceMargin = 9000m
    };

    /// <summary>
    /// Creates a 10-Year Treasury Note contract.
    /// </summary>
    public static FuturesContract CreateZN(int year, int month) => new(
        symbol: "ZN",
        underlying: "10-Year T-Note",
        exchange: "CBOT",
        expirationDate: GetQuarterlyExpiration(year, month),
        multiplier: 1000m,
        tickSize: 0.015625m, // 1/64 of a point
        settlement: SettlementType.Physical,
        assetClass: FuturesAssetClass.InterestRate)
    {
        InitialMargin = 2200m,
        MaintenanceMargin = 2000m
    };

    private static DateTime GetQuarterlyExpiration(int year, int month)
    {
        // Quarterly months: March (3), June (6), September (9), December (12)
        var quarterlyMonth = ((month - 1) / 3 + 1) * 3;
        if (quarterlyMonth > 12)
        {
            quarterlyMonth = 3;
            year++;
        }
        return new DateTime(year, quarterlyMonth, 1).AddMonths(1).AddDays(-1);
    }
}
