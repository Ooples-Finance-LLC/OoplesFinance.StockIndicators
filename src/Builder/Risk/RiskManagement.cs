namespace OoplesFinance.StockIndicators.Builder.Risk;

/// <summary>
/// Portfolio risk calculator providing VaR, CVaR, and other risk metrics.
/// </summary>
public sealed class PortfolioRiskCalculator
{
    private readonly IRiskDataProvider? _dataProvider;

    /// <summary>
    /// Creates a new portfolio risk calculator.
    /// </summary>
    public PortfolioRiskCalculator(IRiskDataProvider? dataProvider = null)
    {
        _dataProvider = dataProvider;
    }

    /// <summary>
    /// Calculates Value at Risk (VaR) using historical simulation.
    /// </summary>
    /// <param name="returns">Historical returns.</param>
    /// <param name="confidenceLevel">Confidence level (e.g., 0.95 for 95%).</param>
    /// <param name="portfolioValue">Current portfolio value.</param>
    /// <returns>The VaR as a positive dollar amount.</returns>
    public decimal CalculateHistoricalVaR(
        IReadOnlyList<decimal> returns,
        decimal confidenceLevel,
        decimal portfolioValue)
    {
        if (returns.Count == 0) return 0m;

        var sortedReturns = returns.OrderBy(r => r).ToList();
        var index = (int)Math.Floor((1 - confidenceLevel) * sortedReturns.Count);
        var varReturn = sortedReturns[Math.Max(0, index)];

        return Math.Abs(varReturn) * portfolioValue;
    }

    /// <summary>
    /// Calculates Value at Risk (VaR) using parametric (variance-covariance) method.
    /// Assumes returns are normally distributed.
    /// </summary>
    /// <param name="returns">Historical returns.</param>
    /// <param name="confidenceLevel">Confidence level (e.g., 0.95 for 95%).</param>
    /// <param name="portfolioValue">Current portfolio value.</param>
    /// <param name="holdingPeriod">Holding period in days (default 1).</param>
    /// <returns>The VaR as a positive dollar amount.</returns>
    public decimal CalculateParametricVaR(
        IReadOnlyList<decimal> returns,
        decimal confidenceLevel,
        decimal portfolioValue,
        int holdingPeriod = 1)
    {
        if (returns.Count < 2) return 0m;

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1);
        var stdDev = (decimal)Math.Sqrt((double)variance);

        // Z-score for confidence level
        var zScore = GetZScore(confidenceLevel);

        // Scale by square root of time for multi-day VaR
        var scaledStdDev = stdDev * (decimal)Math.Sqrt(holdingPeriod);

        return zScore * scaledStdDev * portfolioValue;
    }

    /// <summary>
    /// Calculates Conditional Value at Risk (CVaR / Expected Shortfall).
    /// Average of losses beyond VaR.
    /// </summary>
    /// <param name="returns">Historical returns.</param>
    /// <param name="confidenceLevel">Confidence level.</param>
    /// <param name="portfolioValue">Current portfolio value.</param>
    /// <returns>The CVaR as a positive dollar amount.</returns>
    public decimal CalculateCVaR(
        IReadOnlyList<decimal> returns,
        decimal confidenceLevel,
        decimal portfolioValue)
    {
        if (returns.Count == 0) return 0m;

        var sortedReturns = returns.OrderBy(r => r).ToList();
        var cutoffIndex = (int)Math.Floor((1 - confidenceLevel) * sortedReturns.Count);

        if (cutoffIndex == 0) return Math.Abs(sortedReturns[0]) * portfolioValue;

        var tailReturns = sortedReturns.Take(cutoffIndex).ToList();
        var averageTailLoss = tailReturns.Average();

        return Math.Abs(averageTailLoss) * portfolioValue;
    }

    /// <summary>
    /// Calculates portfolio beta relative to a benchmark.
    /// </summary>
    /// <param name="portfolioReturns">Portfolio returns.</param>
    /// <param name="benchmarkReturns">Benchmark returns.</param>
    /// <returns>The portfolio beta.</returns>
    public decimal CalculateBeta(
        IReadOnlyList<decimal> portfolioReturns,
        IReadOnlyList<decimal> benchmarkReturns)
    {
        if (portfolioReturns.Count != benchmarkReturns.Count || portfolioReturns.Count < 2)
            return 1m;

        var portfolioMean = portfolioReturns.Average();
        var benchmarkMean = benchmarkReturns.Average();

        var covariance = 0m;
        var benchmarkVariance = 0m;

        for (var i = 0; i < portfolioReturns.Count; i++)
        {
            var portDev = portfolioReturns[i] - portfolioMean;
            var benchDev = benchmarkReturns[i] - benchmarkMean;
            covariance += portDev * benchDev;
            benchmarkVariance += benchDev * benchDev;
        }

        if (benchmarkVariance == 0) return 1m;

        return covariance / benchmarkVariance;
    }

    /// <summary>
    /// Calculates the correlation matrix for a set of assets.
    /// </summary>
    /// <param name="assetReturns">Dictionary of asset name to returns.</param>
    /// <returns>Correlation matrix.</returns>
    public CorrelationMatrix CalculateCorrelationMatrix(
        IReadOnlyDictionary<string, IReadOnlyList<decimal>> assetReturns)
    {
        var assets = assetReturns.Keys.ToList();
        var n = assets.Count;
        var matrix = new decimal[n, n];

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (i == j)
                {
                    matrix[i, j] = 1m;
                }
                else if (j > i)
                {
                    var corr = CalculateCorrelation(
                        assetReturns[assets[i]],
                        assetReturns[assets[j]]);
                    matrix[i, j] = corr;
                    matrix[j, i] = corr;
                }
            }
        }

        return new CorrelationMatrix(assets, matrix);
    }

    /// <summary>
    /// Calculates correlation between two return series.
    /// </summary>
    public decimal CalculateCorrelation(
        IReadOnlyList<decimal> returns1,
        IReadOnlyList<decimal> returns2)
    {
        if (returns1.Count != returns2.Count || returns1.Count < 2)
            return 0m;

        var mean1 = returns1.Average();
        var mean2 = returns2.Average();

        var covariance = 0m;
        var var1 = 0m;
        var var2 = 0m;

        for (var i = 0; i < returns1.Count; i++)
        {
            var dev1 = returns1[i] - mean1;
            var dev2 = returns2[i] - mean2;
            covariance += dev1 * dev2;
            var1 += dev1 * dev1;
            var2 += dev2 * dev2;
        }

        var denom = (decimal)Math.Sqrt((double)(var1 * var2));
        if (denom == 0) return 0m;

        return covariance / denom;
    }

    /// <summary>
    /// Calculates maximum drawdown.
    /// </summary>
    /// <param name="equityCurve">List of portfolio values over time.</param>
    /// <returns>Maximum drawdown as a decimal (e.g., 0.20 = 20%).</returns>
    public decimal CalculateMaxDrawdown(IReadOnlyList<decimal> equityCurve)
    {
        if (equityCurve.Count == 0) return 0m;

        var maxDrawdown = 0m;
        var peak = equityCurve[0];

        foreach (var value in equityCurve)
        {
            if (value > peak)
            {
                peak = value;
            }
            else if (peak > 0)
            {
                var drawdown = (peak - value) / peak;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
        }

        return maxDrawdown;
    }

    /// <summary>
    /// Calculates Sharpe ratio.
    /// </summary>
    /// <param name="returns">Portfolio returns.</param>
    /// <param name="riskFreeRate">Risk-free rate (annualized).</param>
    /// <param name="periodsPerYear">Number of periods per year (252 for daily).</param>
    public decimal CalculateSharpeRatio(
        IReadOnlyList<decimal> returns,
        decimal riskFreeRate = 0.02m,
        int periodsPerYear = 252)
    {
        if (returns.Count < 2) return 0m;

        var meanReturn = returns.Average();
        var variance = returns.Sum(r => (r - meanReturn) * (r - meanReturn)) / (returns.Count - 1);
        var stdDev = (decimal)Math.Sqrt((double)variance);

        if (stdDev == 0) return 0m;

        var annualizedReturn = meanReturn * periodsPerYear;
        var annualizedStdDev = stdDev * (decimal)Math.Sqrt(periodsPerYear);

        return (annualizedReturn - riskFreeRate) / annualizedStdDev;
    }

    /// <summary>
    /// Calculates Sortino ratio (uses downside deviation instead of total).
    /// </summary>
    public decimal CalculateSortinoRatio(
        IReadOnlyList<decimal> returns,
        decimal riskFreeRate = 0.02m,
        int periodsPerYear = 252)
    {
        if (returns.Count < 2) return 0m;

        var meanReturn = returns.Average();
        var downsideReturns = returns.Where(r => r < 0).ToList();

        if (downsideReturns.Count == 0) return decimal.MaxValue;

        var downsideVariance = downsideReturns.Sum(r => r * r) / downsideReturns.Count;
        var downsideDeviation = (decimal)Math.Sqrt((double)downsideVariance);

        if (downsideDeviation == 0) return decimal.MaxValue;

        var annualizedReturn = meanReturn * periodsPerYear;
        var annualizedDownsideDev = downsideDeviation * (decimal)Math.Sqrt(periodsPerYear);

        return (annualizedReturn - riskFreeRate) / annualizedDownsideDev;
    }

    private static decimal GetZScore(decimal confidenceLevel)
    {
        // Common Z-scores
        return confidenceLevel switch
        {
            >= 0.99m => 2.326m,
            >= 0.975m => 1.96m,
            >= 0.95m => 1.645m,
            >= 0.90m => 1.28m,
            _ => 1.645m
        };
    }
}

/// <summary>
/// Correlation matrix for portfolio assets.
/// </summary>
public sealed class CorrelationMatrix
{
    private readonly decimal[,] _matrix;

    /// <summary>
    /// Creates a new correlation matrix.
    /// </summary>
    public CorrelationMatrix(IReadOnlyList<string> assets, decimal[,] matrix)
    {
        Assets = assets;
        _matrix = matrix;
    }

    /// <summary>Gets the asset names.</summary>
    public IReadOnlyList<string> Assets { get; }

    /// <summary>
    /// Gets the correlation between two assets.
    /// </summary>
    public decimal GetCorrelation(string asset1, string asset2)
    {
        var i = Assets.ToList().IndexOf(asset1);
        var j = Assets.ToList().IndexOf(asset2);
        if (i < 0 || j < 0) return 0m;
        return _matrix[i, j];
    }

    /// <summary>
    /// Gets all correlations for an asset.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> GetCorrelationsFor(string asset)
    {
        var i = Assets.ToList().IndexOf(asset);
        if (i < 0) return new Dictionary<string, decimal>();

        var result = new Dictionary<string, decimal>();
        for (var j = 0; j < Assets.Count; j++)
        {
            result[Assets[j]] = _matrix[i, j];
        }
        return result;
    }

    /// <summary>
    /// Finds assets highly correlated with the given asset.
    /// </summary>
    public IReadOnlyList<(string Asset, decimal Correlation)> FindHighlyCorrelated(
        string asset,
        decimal threshold = 0.7m)
    {
        var correlations = GetCorrelationsFor(asset);
        return correlations
            .Where(kvp => kvp.Key != asset && Math.Abs(kvp.Value) >= threshold)
            .OrderByDescending(kvp => Math.Abs(kvp.Value))
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }
}

/// <summary>
/// Kelly Criterion for optimal position sizing.
/// </summary>
public static class KellyCriterion
{
    /// <summary>
    /// Calculates the optimal Kelly fraction.
    /// f* = (bp - q) / b
    /// where b = odds, p = win probability, q = loss probability
    /// </summary>
    /// <param name="winRate">Win rate (probability of winning).</param>
    /// <param name="avgWin">Average winning trade return.</param>
    /// <param name="avgLoss">Average losing trade return (absolute value).</param>
    /// <returns>Optimal fraction of capital to risk.</returns>
    public static decimal CalculateOptimalFraction(decimal winRate, decimal avgWin, decimal avgLoss)
    {
        if (winRate <= 0 || winRate >= 1 || avgWin <= 0 || avgLoss <= 0)
            return 0m;

        var lossRate = 1 - winRate;
        var odds = avgWin / avgLoss; // Win/loss ratio

        var kelly = (odds * winRate - lossRate) / odds;

        return Math.Max(0, kelly);
    }

    /// <summary>
    /// Calculates Kelly fraction from historical trades.
    /// </summary>
    /// <param name="tradeReturns">List of trade returns (positive = win, negative = loss).</param>
    /// <returns>Optimal fraction of capital to risk.</returns>
    public static decimal CalculateFromTrades(IReadOnlyList<decimal> tradeReturns)
    {
        if (tradeReturns.Count == 0) return 0m;

        var wins = tradeReturns.Where(r => r > 0).ToList();
        var losses = tradeReturns.Where(r => r < 0).ToList();

        if (wins.Count == 0 || losses.Count == 0) return 0m;

        var winRate = (decimal)wins.Count / tradeReturns.Count;
        var avgWin = wins.Average();
        var avgLoss = Math.Abs(losses.Average());

        return CalculateOptimalFraction(winRate, avgWin, avgLoss);
    }

    /// <summary>
    /// Applies fractional Kelly for more conservative sizing.
    /// </summary>
    /// <param name="fullKelly">Full Kelly fraction.</param>
    /// <param name="fraction">Fraction of Kelly to use (default 0.5 = half Kelly).</param>
    public static decimal FractionalKelly(decimal fullKelly, decimal fraction = 0.5m)
    {
        return fullKelly * fraction;
    }

    /// <summary>
    /// Calculates position size in dollars based on Kelly.
    /// </summary>
    /// <param name="accountValue">Total account value.</param>
    /// <param name="winRate">Win rate.</param>
    /// <param name="avgWin">Average win amount.</param>
    /// <param name="avgLoss">Average loss amount.</param>
    /// <param name="kellyFraction">Fraction of Kelly to use.</param>
    /// <returns>Dollar amount to risk.</returns>
    public static decimal CalculatePositionSize(
        decimal accountValue,
        decimal winRate,
        decimal avgWin,
        decimal avgLoss,
        decimal kellyFraction = 0.5m)
    {
        var fullKelly = CalculateOptimalFraction(winRate, avgWin, avgLoss);
        var fractionalKelly = FractionalKelly(fullKelly, kellyFraction);
        return accountValue * fractionalKelly;
    }
}

/// <summary>
/// Exposure limits for portfolio risk management.
/// </summary>
public sealed class ExposureLimits
{
    /// <summary>Gets or sets max single position as percent of equity (default 10%).</summary>
    public decimal MaxSinglePositionPercent { get; set; } = 0.10m;

    /// <summary>Gets or sets max sector exposure as percent of equity (default 25%).</summary>
    public decimal MaxSectorExposurePercent { get; set; } = 0.25m;

    /// <summary>Gets or sets max currency exposure as percent of equity (default 30%).</summary>
    public decimal MaxCurrencyExposurePercent { get; set; } = 0.30m;

    /// <summary>Gets or sets max leverage ratio (default 2.0).</summary>
    public decimal MaxLeverageRatio { get; set; } = 2.0m;

    /// <summary>Gets or sets max portfolio beta (default 1.5).</summary>
    public decimal MaxPortfolioBeta { get; set; } = 1.5m;

    /// <summary>Gets or sets max correlation between new position and portfolio (default 0.8).</summary>
    public decimal MaxCorrelation { get; set; } = 0.8m;

    /// <summary>Gets or sets max daily VaR as percent of equity (default 2%).</summary>
    public decimal MaxDailyVaRPercent { get; set; } = 0.02m;

    /// <summary>Gets or sets max drawdown before position reduction (default 10%).</summary>
    public decimal MaxDrawdownPercent { get; set; } = 0.10m;

    /// <summary>
    /// Checks all limits and returns any violations.
    /// </summary>
    public IReadOnlyList<ExposureViolation> CheckViolations(PortfolioExposure exposure)
    {
        var violations = new List<ExposureViolation>();

        foreach (var position in exposure.Positions)
        {
            if (position.PercentOfEquity > MaxSinglePositionPercent)
            {
                violations.Add(new ExposureViolation
                {
                    Type = ViolationType.SinglePositionExceeded,
                    Symbol = position.Symbol,
                    CurrentValue = position.PercentOfEquity,
                    Limit = MaxSinglePositionPercent,
                    Message = $"Position {position.Symbol} at {position.PercentOfEquity:P1} exceeds limit of {MaxSinglePositionPercent:P1}"
                });
            }
        }

        foreach (var sector in exposure.SectorExposures)
        {
            if (sector.Value > MaxSectorExposurePercent)
            {
                violations.Add(new ExposureViolation
                {
                    Type = ViolationType.SectorExposureExceeded,
                    Symbol = sector.Key,
                    CurrentValue = sector.Value,
                    Limit = MaxSectorExposurePercent,
                    Message = $"Sector {sector.Key} at {sector.Value:P1} exceeds limit of {MaxSectorExposurePercent:P1}"
                });
            }
        }

        if (exposure.LeverageRatio > MaxLeverageRatio)
        {
            violations.Add(new ExposureViolation
            {
                Type = ViolationType.LeverageExceeded,
                CurrentValue = exposure.LeverageRatio,
                Limit = MaxLeverageRatio,
                Message = $"Leverage {exposure.LeverageRatio:F2}x exceeds limit of {MaxLeverageRatio:F2}x"
            });
        }

        if (exposure.PortfolioBeta > MaxPortfolioBeta)
        {
            violations.Add(new ExposureViolation
            {
                Type = ViolationType.BetaExceeded,
                CurrentValue = exposure.PortfolioBeta,
                Limit = MaxPortfolioBeta,
                Message = $"Portfolio beta {exposure.PortfolioBeta:F2} exceeds limit of {MaxPortfolioBeta:F2}"
            });
        }

        if (exposure.DailyVaRPercent > MaxDailyVaRPercent)
        {
            violations.Add(new ExposureViolation
            {
                Type = ViolationType.VaRExceeded,
                CurrentValue = exposure.DailyVaRPercent,
                Limit = MaxDailyVaRPercent,
                Message = $"Daily VaR {exposure.DailyVaRPercent:P1} exceeds limit of {MaxDailyVaRPercent:P1}"
            });
        }

        return violations;
    }
}

/// <summary>
/// Type of exposure violation.
/// </summary>
public enum ViolationType
{
    /// <summary>Single position exceeds limit.</summary>
    SinglePositionExceeded,

    /// <summary>Sector exposure exceeds limit.</summary>
    SectorExposureExceeded,

    /// <summary>Currency exposure exceeds limit.</summary>
    CurrencyExposureExceeded,

    /// <summary>Leverage exceeds limit.</summary>
    LeverageExceeded,

    /// <summary>Portfolio beta exceeds limit.</summary>
    BetaExceeded,

    /// <summary>New position correlation exceeds limit.</summary>
    CorrelationExceeded,

    /// <summary>VaR exceeds limit.</summary>
    VaRExceeded,

    /// <summary>Drawdown exceeds limit.</summary>
    DrawdownExceeded
}

/// <summary>
/// Details of an exposure limit violation.
/// </summary>
public sealed class ExposureViolation
{
    /// <summary>Gets or sets the violation type.</summary>
    public ViolationType Type { get; set; }

    /// <summary>Gets or sets the symbol (if applicable).</summary>
    public string? Symbol { get; set; }

    /// <summary>Gets or sets the current value.</summary>
    public decimal CurrentValue { get; set; }

    /// <summary>Gets or sets the limit that was exceeded.</summary>
    public decimal Limit { get; set; }

    /// <summary>Gets or sets the violation message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Current portfolio exposure state.
/// </summary>
public sealed class PortfolioExposure
{
    /// <summary>Gets or sets the positions.</summary>
    public IReadOnlyList<PositionExposure> Positions { get; set; } = Array.Empty<PositionExposure>();

    /// <summary>Gets or sets the sector exposures.</summary>
    public IReadOnlyDictionary<string, decimal> SectorExposures { get; set; } = new Dictionary<string, decimal>();

    /// <summary>Gets or sets the currency exposures.</summary>
    public IReadOnlyDictionary<string, decimal> CurrencyExposures { get; set; } = new Dictionary<string, decimal>();

    /// <summary>Gets or sets the leverage ratio.</summary>
    public decimal LeverageRatio { get; set; }

    /// <summary>Gets or sets the portfolio beta.</summary>
    public decimal PortfolioBeta { get; set; }

    /// <summary>Gets or sets the daily VaR as percent.</summary>
    public decimal DailyVaRPercent { get; set; }

    /// <summary>Gets or sets the current drawdown percent.</summary>
    public decimal CurrentDrawdownPercent { get; set; }

    /// <summary>Gets or sets the total equity.</summary>
    public decimal TotalEquity { get; set; }

    /// <summary>Gets or sets the total exposure (sum of absolute position values).</summary>
    public decimal GrossExposure { get; set; }

    /// <summary>Gets or sets the net exposure (long - short).</summary>
    public decimal NetExposure { get; set; }
}

/// <summary>
/// Exposure data for a single position.
/// </summary>
public sealed class PositionExposure
{
    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the market value.</summary>
    public decimal MarketValue { get; set; }

    /// <summary>Gets or sets the percent of equity.</summary>
    public decimal PercentOfEquity { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public string? Sector { get; set; }

    /// <summary>Gets or sets the currency.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Gets or sets the position beta.</summary>
    public decimal Beta { get; set; }

    /// <summary>Gets or sets the position contribution to portfolio VaR.</summary>
    public decimal VaRContribution { get; set; }
}

/// <summary>
/// Interface for providing risk data.
/// </summary>
public interface IRiskDataProvider
{
    /// <summary>Gets historical returns for a symbol.</summary>
    Task<IReadOnlyList<decimal>> GetHistoricalReturnsAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default);

    /// <summary>Gets sector classification for a symbol.</summary>
    Task<string?> GetSectorAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>Gets beta for a symbol.</summary>
    Task<decimal> GetBetaAsync(string symbol, CancellationToken cancellationToken = default);
}
