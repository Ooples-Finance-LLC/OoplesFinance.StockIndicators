namespace OoplesFinance.StockIndicators.Builder.Analytics;

/// <summary>
/// Performs comprehensive performance attribution analysis.
/// Decomposes portfolio returns into factor contributions.
/// </summary>
public sealed class PerformanceAttributionEngine
{
    private readonly PerformanceAttributionOptions _options;

    /// <summary>
    /// Initializes a new instance of the PerformanceAttributionEngine.
    /// </summary>
    public PerformanceAttributionEngine(PerformanceAttributionOptions? options = null)
    {
        _options = options ?? new PerformanceAttributionOptions();
    }

    /// <summary>
    /// Performs Brinson attribution analysis.
    /// </summary>
    public BrinsonAttributionResult BrinsonAttribution(
        PortfolioSnapshot portfolio,
        BenchmarkSnapshot benchmark,
        DateTime startDate,
        DateTime endDate)
    {
        var result = new BrinsonAttributionResult
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalPortfolioReturn = CalculateTotalReturn(portfolio),
            TotalBenchmarkReturn = CalculateTotalReturn(benchmark)
        };

        result.TotalActiveReturn = result.TotalPortfolioReturn - result.TotalBenchmarkReturn;

        // Get all sectors
        var allSectors = portfolio.Holdings
            .Select(h => h.Sector)
            .Union(benchmark.Holdings.Select(h => h.Sector))
            .Distinct()
            .ToList();

        foreach (var sector in allSectors)
        {
            var portfolioHoldings = portfolio.Holdings.Where(h => h.Sector == sector).ToList();
            var benchmarkHoldings = benchmark.Holdings.Where(h => h.Sector == sector).ToList();

            var portfolioWeight = portfolioHoldings.Sum(h => h.Weight);
            var benchmarkWeight = benchmarkHoldings.Sum(h => h.Weight);

            var portfolioReturn = portfolioHoldings.Sum(h => h.Weight * h.Return) /
                (portfolioWeight > 0 ? portfolioWeight : 1);
            var benchmarkReturn = benchmarkHoldings.Sum(h => h.Weight * h.Return) /
                (benchmarkWeight > 0 ? benchmarkWeight : 1);

            // Allocation effect: Over/underweight in outperforming sectors
            var allocationEffect = (portfolioWeight - benchmarkWeight) *
                (benchmarkReturn - result.TotalBenchmarkReturn);

            // Selection effect: Stock picking within sectors
            var selectionEffect = benchmarkWeight * (portfolioReturn - benchmarkReturn);

            // Interaction effect: Combined effect
            var interactionEffect = (portfolioWeight - benchmarkWeight) *
                (portfolioReturn - benchmarkReturn);

            result.SectorAttribution.Add(new SectorAttribution
            {
                Sector = sector,
                PortfolioWeight = portfolioWeight,
                BenchmarkWeight = benchmarkWeight,
                PortfolioReturn = portfolioReturn,
                BenchmarkReturn = benchmarkReturn,
                AllocationEffect = allocationEffect,
                SelectionEffect = selectionEffect,
                InteractionEffect = interactionEffect,
                TotalEffect = allocationEffect + selectionEffect + interactionEffect
            });
        }

        result.TotalAllocationEffect = result.SectorAttribution.Sum(s => s.AllocationEffect);
        result.TotalSelectionEffect = result.SectorAttribution.Sum(s => s.SelectionEffect);
        result.TotalInteractionEffect = result.SectorAttribution.Sum(s => s.InteractionEffect);

        return result;
    }

    /// <summary>
    /// Performs factor-based attribution analysis.
    /// </summary>
    public FactorAttributionResult FactorAttribution(
        IReadOnlyList<decimal> portfolioReturns,
        Dictionary<string, IReadOnlyList<decimal>> factorReturns,
        decimal riskFreeRate = 0.02m)
    {
        if (portfolioReturns.Count == 0 || factorReturns.Count == 0)
        {
            throw new ArgumentException("Returns data cannot be empty");
        }

        var result = new FactorAttributionResult
        {
            TotalReturn = portfolioReturns.Average(),
            RiskFreeRate = riskFreeRate / 252 // Daily
        };

        // Calculate factor exposures using OLS regression
        var factorNames = factorReturns.Keys.ToList();
        var n = portfolioReturns.Count;
        var k = factorNames.Count;

        // Build design matrix
        var X = new decimal[n, k + 1];
        for (var i = 0; i < n; i++)
        {
            X[i, 0] = 1; // Intercept
            for (var j = 0; j < k; j++)
            {
                X[i, j + 1] = factorReturns[factorNames[j]][i];
            }
        }

        // Solve OLS: beta = (X'X)^-1 X'y
        var y = portfolioReturns.ToArray();
        var betas = SolveOLS(X, y);

        result.Alpha = betas[0] * 252; // Annualized

        var totalFactorContribution = 0m;

        for (var i = 0; i < k; i++)
        {
            var factorName = factorNames[i];
            var beta = betas[i + 1];
            var avgFactorReturn = factorReturns[factorName].Average();
            var contribution = beta * avgFactorReturn * 252; // Annualized

            result.FactorExposures[factorName] = new FactorExposure
            {
                FactorName = factorName,
                Beta = beta,
                AverageReturn = avgFactorReturn * 252,
                Contribution = contribution,
                TStatistic = CalculateTStatistic(X, y, betas, i + 1)
            };

            totalFactorContribution += contribution;
        }

        result.TotalFactorContribution = totalFactorContribution;
        result.IdiosyncraticReturn = result.TotalReturn * 252 - result.TotalFactorContribution - result.Alpha;

        // Calculate R-squared
        var yHat = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            yHat[i] = betas[0];
            for (var j = 0; j < k; j++)
            {
                yHat[i] += betas[j + 1] * factorReturns[factorNames[j]][i];
            }
        }

        var ssTotal = portfolioReturns.Sum(r => (r - portfolioReturns.Average()) * (r - portfolioReturns.Average()));
        var ssResidual = portfolioReturns.Select((r, i) => (r - yHat[i]) * (r - yHat[i])).Sum();
        result.RSquared = ssTotal > 0 ? 1 - ssResidual / ssTotal : 0;

        return result;
    }

    /// <summary>
    /// Performs risk attribution analysis.
    /// </summary>
    public RiskAttributionResult RiskAttribution(
        PortfolioSnapshot portfolio,
        decimal[,] covarianceMatrix,
        string[] assetNames)
    {
        var n = assetNames.Length;
        var weights = new decimal[n];

        for (var i = 0; i < n; i++)
        {
            var holding = portfolio.Holdings.FirstOrDefault(h => h.Symbol == assetNames[i]);
            weights[i] = holding?.Weight ?? 0;
        }

        // Calculate portfolio variance
        var portfolioVariance = 0m;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                portfolioVariance += weights[i] * weights[j] * covarianceMatrix[i, j];
            }
        }

        var portfolioStdDev = (decimal)Math.Sqrt((double)portfolioVariance);

        var result = new RiskAttributionResult
        {
            TotalPortfolioRisk = portfolioStdDev * (decimal)Math.Sqrt(252) // Annualized
        };

        // Calculate marginal contribution to risk (MCR) for each asset
        for (var i = 0; i < n; i++)
        {
            // MCR = (Sigma * w)_i / sigma_p
            var covarianceContribution = 0m;
            for (var j = 0; j < n; j++)
            {
                covarianceContribution += weights[j] * covarianceMatrix[i, j];
            }

            var mcr = portfolioStdDev > 0 ? covarianceContribution / portfolioStdDev : 0;
            var componentRisk = weights[i] * mcr;

            result.AssetRiskContribution.Add(new AssetRiskContribution
            {
                AssetName = assetNames[i],
                Weight = weights[i],
                MarginalContribution = mcr * (decimal)Math.Sqrt(252),
                ComponentContribution = componentRisk * (decimal)Math.Sqrt(252),
                PercentContribution = portfolioVariance > 0
                    ? componentRisk / portfolioStdDev * 100
                    : 0
            });
        }

        // Decompose by sector
        var sectors = portfolio.Holdings
            .Select(h => h.Sector)
            .Distinct()
            .ToList();

        foreach (var sector in sectors)
        {
            var sectorContribution = result.AssetRiskContribution
                .Where(a => portfolio.Holdings.Any(h => h.Symbol == a.AssetName && h.Sector == sector))
                .Sum(a => a.ComponentContribution);

            result.SectorRiskContribution[sector] = sectorContribution;
        }

        return result;
    }

    /// <summary>
    /// Calculates time-weighted return decomposition.
    /// </summary>
    public TimeWeightedReturnDecomposition DecomposeTimeWeightedReturn(
        IReadOnlyList<PeriodReturn> periodReturns)
    {
        var result = new TimeWeightedReturnDecomposition
        {
            PeriodReturns = periodReturns.ToList()
        };

        // Compound returns
        var compoundReturn = 1m;
        foreach (var period in periodReturns)
        {
            compoundReturn *= (1 + period.Return);
        }
        result.TotalReturn = compoundReturn - 1;

        // Geometric vs arithmetic decomposition
        result.ArithmeticReturn = periodReturns.Average(p => p.Return);
        result.GeometricReturn = (decimal)Math.Pow((double)compoundReturn, 1.0 / periodReturns.Count) - 1;

        // Contribution from each period
        foreach (var period in periodReturns)
        {
            period.Contribution = period.Return / periodReturns.Count;
        }

        // Calculate volatility drag
        var variance = periodReturns.Select(p => p.Return)
            .Select(r => (r - result.ArithmeticReturn) * (r - result.ArithmeticReturn))
            .Average();
        result.VolatilityDrag = variance / 2;

        return result;
    }

    /// <summary>
    /// Performs transaction cost analysis.
    /// </summary>
    public TransactionCostAnalysis AnalyzeTransactionCosts(
        IReadOnlyList<Trade> trades,
        decimal grossReturn)
    {
        var result = new TransactionCostAnalysis
        {
            GrossReturn = grossReturn,
            TotalTrades = trades.Count
        };

        foreach (var trade in trades)
        {
            result.TotalCommissions += trade.Commission;
            result.TotalSpreadCost += trade.Quantity * trade.SpreadCost;
            result.TotalSlippage += trade.Quantity * trade.SlippageCost;
            result.TotalImpactCost += trade.Quantity * trade.MarketImpact;
        }

        result.TotalTransactionCosts = result.TotalCommissions + result.TotalSpreadCost +
                                       result.TotalSlippage + result.TotalImpactCost;
        result.NetReturn = grossReturn - result.TotalTransactionCosts;
        result.TransactionCostRatio = grossReturn != 0
            ? result.TotalTransactionCosts / Math.Abs(grossReturn) * 100
            : 0;

        // Cost breakdown by type
        result.CostBreakdown["Commissions"] = result.TotalCommissions;
        result.CostBreakdown["SpreadCost"] = result.TotalSpreadCost;
        result.CostBreakdown["Slippage"] = result.TotalSlippage;
        result.CostBreakdown["MarketImpact"] = result.TotalImpactCost;

        // Average cost per trade
        if (trades.Count > 0)
        {
            result.AverageCostPerTrade = result.TotalTransactionCosts / trades.Count;
            result.AverageSlippagePerTrade = result.TotalSlippage / trades.Count;
        }

        return result;
    }

    /// <summary>
    /// Calculates holding period attribution.
    /// </summary>
    public HoldingPeriodAttribution CalculateHoldingPeriodAttribution(
        IReadOnlyList<PositionHistory> positionHistories)
    {
        var result = new HoldingPeriodAttribution();

        foreach (var history in positionHistories)
        {
            var holdingDays = (int)(history.CloseDate - history.OpenDate).TotalDays;
            var dailyReturn = holdingDays > 0
                ? (decimal)Math.Pow((double)(1 + history.TotalReturn), 1.0 / holdingDays) - 1
                : history.TotalReturn;

            var attribution = new PositionAttribution
            {
                Symbol = history.Symbol,
                OpenDate = history.OpenDate,
                CloseDate = history.CloseDate,
                HoldingPeriodDays = holdingDays,
                TotalReturn = history.TotalReturn,
                AnnualizedReturn = dailyReturn * 252,
                PriceReturn = history.PriceReturn,
                DividendReturn = history.DividendReturn,
                CurrencyReturn = history.CurrencyReturn
            };

            result.PositionAttributions.Add(attribution);
            result.TotalPriceReturn += history.PriceReturn * history.AverageWeight;
            result.TotalDividendReturn += history.DividendReturn * history.AverageWeight;
            result.TotalCurrencyReturn += history.CurrencyReturn * history.AverageWeight;
        }

        result.TotalReturn = result.TotalPriceReturn + result.TotalDividendReturn + result.TotalCurrencyReturn;

        // Calculate by holding period buckets
        result.ReturnByHoldingPeriod["<1 week"] = result.PositionAttributions
            .Where(p => p.HoldingPeriodDays < 7)
            .Sum(p => p.TotalReturn);

        result.ReturnByHoldingPeriod["1-4 weeks"] = result.PositionAttributions
            .Where(p => p.HoldingPeriodDays >= 7 && p.HoldingPeriodDays < 28)
            .Sum(p => p.TotalReturn);

        result.ReturnByHoldingPeriod["1-3 months"] = result.PositionAttributions
            .Where(p => p.HoldingPeriodDays >= 28 && p.HoldingPeriodDays < 90)
            .Sum(p => p.TotalReturn);

        result.ReturnByHoldingPeriod["3-12 months"] = result.PositionAttributions
            .Where(p => p.HoldingPeriodDays >= 90 && p.HoldingPeriodDays < 365)
            .Sum(p => p.TotalReturn);

        result.ReturnByHoldingPeriod[">12 months"] = result.PositionAttributions
            .Where(p => p.HoldingPeriodDays >= 365)
            .Sum(p => p.TotalReturn);

        return result;
    }

    private static decimal CalculateTotalReturn(PortfolioSnapshot snapshot)
    {
        return snapshot.Holdings.Sum(h => h.Weight * h.Return);
    }

    private static decimal CalculateTotalReturn(BenchmarkSnapshot snapshot)
    {
        return snapshot.Holdings.Sum(h => h.Weight * h.Return);
    }

    private static decimal[] SolveOLS(decimal[,] X, decimal[] y)
    {
        var n = y.Length;
        var k = X.GetLength(1);

        // X'X
        var XtX = new decimal[k, k];
        for (var i = 0; i < k; i++)
        {
            for (var j = 0; j < k; j++)
            {
                for (var l = 0; l < n; l++)
                {
                    XtX[i, j] += X[l, i] * X[l, j];
                }
            }
        }

        // X'y
        var Xty = new decimal[k];
        for (var i = 0; i < k; i++)
        {
            for (var l = 0; l < n; l++)
            {
                Xty[i] += X[l, i] * y[l];
            }
        }

        // Solve using Gaussian elimination (simplified)
        return GaussianElimination(XtX, Xty);
    }

    private static decimal[] GaussianElimination(decimal[,] A, decimal[] b)
    {
        var n = b.Length;
        var augmented = new decimal[n, n + 1];

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                augmented[i, j] = A[i, j];
            }
            augmented[i, n] = b[i];
        }

        // Forward elimination
        for (var col = 0; col < n; col++)
        {
            // Find pivot
            var maxRow = col;
            for (var row = col + 1; row < n; row++)
            {
                if (Math.Abs(augmented[row, col]) > Math.Abs(augmented[maxRow, col]))
                {
                    maxRow = row;
                }
            }

            // Swap rows
            for (var j = col; j <= n; j++)
            {
                (augmented[maxRow, j], augmented[col, j]) = (augmented[col, j], augmented[maxRow, j]);
            }

            // Eliminate
            for (var row = col + 1; row < n; row++)
            {
                if (augmented[col, col] == 0) continue;
                var factor = augmented[row, col] / augmented[col, col];
                for (var j = col; j <= n; j++)
                {
                    augmented[row, j] -= factor * augmented[col, j];
                }
            }
        }

        // Back substitution
        var x = new decimal[n];
        for (var i = n - 1; i >= 0; i--)
        {
            x[i] = augmented[i, n];
            for (var j = i + 1; j < n; j++)
            {
                x[i] -= augmented[i, j] * x[j];
            }
            if (augmented[i, i] != 0)
            {
                x[i] /= augmented[i, i];
            }
        }

        return x;
    }

    private static decimal CalculateTStatistic(decimal[,] X, decimal[] y, decimal[] betas, int betaIndex)
    {
        var n = y.Length;
        var k = betas.Length;

        // Calculate residuals
        var residuals = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            residuals[i] = y[i];
            for (var j = 0; j < k; j++)
            {
                residuals[i] -= betas[j] * X[i, j];
            }
        }

        // Calculate residual variance
        var sse = residuals.Sum(r => r * r);
        var mse = sse / (n - k);

        // Calculate X'X inverse diagonal element (simplified)
        var xTx = 0m;
        for (var i = 0; i < n; i++)
        {
            xTx += X[i, betaIndex] * X[i, betaIndex];
        }

        var se = xTx > 0 ? (decimal)Math.Sqrt((double)(mse / xTx)) : 0;

        return se > 0 ? betas[betaIndex] / se : 0;
    }
}

/// <summary>
/// Performance attribution options.
/// </summary>
public sealed class PerformanceAttributionOptions
{
    /// <summary>Annualization factor.</summary>
    public int AnnualizationFactor { get; set; } = 252;

    /// <summary>Risk-free rate.</summary>
    public decimal RiskFreeRate { get; set; } = 0.02m;
}

/// <summary>
/// Portfolio snapshot.
/// </summary>
public sealed class PortfolioSnapshot
{
    public DateTime Date { get; set; }
    public List<HoldingSnapshot> Holdings { get; set; } = [];
    public decimal TotalValue { get; set; }
}

/// <summary>
/// Holding snapshot.
/// </summary>
public sealed class HoldingSnapshot
{
    public string Symbol { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Return { get; set; }
    public decimal Value { get; set; }
}

/// <summary>
/// Benchmark snapshot.
/// </summary>
public sealed class BenchmarkSnapshot
{
    public DateTime Date { get; set; }
    public List<HoldingSnapshot> Holdings { get; set; } = [];
}

/// <summary>
/// Brinson attribution result.
/// </summary>
public sealed class BrinsonAttributionResult
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalPortfolioReturn { get; set; }
    public decimal TotalBenchmarkReturn { get; set; }
    public decimal TotalActiveReturn { get; set; }
    public decimal TotalAllocationEffect { get; set; }
    public decimal TotalSelectionEffect { get; set; }
    public decimal TotalInteractionEffect { get; set; }
    public List<SectorAttribution> SectorAttribution { get; set; } = [];
}

/// <summary>
/// Sector attribution.
/// </summary>
public sealed class SectorAttribution
{
    public string Sector { get; set; } = string.Empty;
    public decimal PortfolioWeight { get; set; }
    public decimal BenchmarkWeight { get; set; }
    public decimal PortfolioReturn { get; set; }
    public decimal BenchmarkReturn { get; set; }
    public decimal AllocationEffect { get; set; }
    public decimal SelectionEffect { get; set; }
    public decimal InteractionEffect { get; set; }
    public decimal TotalEffect { get; set; }
}

/// <summary>
/// Factor attribution result.
/// </summary>
public sealed class FactorAttributionResult
{
    public decimal TotalReturn { get; set; }
    public decimal Alpha { get; set; }
    public decimal TotalFactorContribution { get; set; }
    public decimal IdiosyncraticReturn { get; set; }
    public decimal RSquared { get; set; }
    public decimal RiskFreeRate { get; set; }
    public Dictionary<string, FactorExposure> FactorExposures { get; set; } = [];
}

/// <summary>
/// Factor exposure.
/// </summary>
public sealed class FactorExposure
{
    public string FactorName { get; set; } = string.Empty;
    public decimal Beta { get; set; }
    public decimal AverageReturn { get; set; }
    public decimal Contribution { get; set; }
    public decimal TStatistic { get; set; }
}

/// <summary>
/// Risk attribution result.
/// </summary>
public sealed class RiskAttributionResult
{
    public decimal TotalPortfolioRisk { get; set; }
    public List<AssetRiskContribution> AssetRiskContribution { get; set; } = [];
    public Dictionary<string, decimal> SectorRiskContribution { get; set; } = [];
}

/// <summary>
/// Asset risk contribution.
/// </summary>
public sealed class AssetRiskContribution
{
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal MarginalContribution { get; set; }
    public decimal ComponentContribution { get; set; }
    public decimal PercentContribution { get; set; }
}

/// <summary>
/// Period return.
/// </summary>
public sealed class PeriodReturn
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Return { get; set; }
    public decimal Contribution { get; set; }
}

/// <summary>
/// Time-weighted return decomposition.
/// </summary>
public sealed class TimeWeightedReturnDecomposition
{
    public decimal TotalReturn { get; set; }
    public decimal ArithmeticReturn { get; set; }
    public decimal GeometricReturn { get; set; }
    public decimal VolatilityDrag { get; set; }
    public List<PeriodReturn> PeriodReturns { get; set; } = [];
}

/// <summary>
/// Trade for transaction cost analysis.
/// </summary>
public sealed class Trade
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime TradeDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Commission { get; set; }
    public decimal SpreadCost { get; set; }
    public decimal SlippageCost { get; set; }
    public decimal MarketImpact { get; set; }
}

/// <summary>
/// Transaction cost analysis.
/// </summary>
public sealed class TransactionCostAnalysis
{
    public decimal GrossReturn { get; set; }
    public decimal NetReturn { get; set; }
    public decimal TotalTransactionCosts { get; set; }
    public decimal TotalCommissions { get; set; }
    public decimal TotalSpreadCost { get; set; }
    public decimal TotalSlippage { get; set; }
    public decimal TotalImpactCost { get; set; }
    public decimal TransactionCostRatio { get; set; }
    public int TotalTrades { get; set; }
    public decimal AverageCostPerTrade { get; set; }
    public decimal AverageSlippagePerTrade { get; set; }
    public Dictionary<string, decimal> CostBreakdown { get; set; } = [];
}

/// <summary>
/// Position history.
/// </summary>
public sealed class PositionHistory
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime OpenDate { get; set; }
    public DateTime CloseDate { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal PriceReturn { get; set; }
    public decimal DividendReturn { get; set; }
    public decimal CurrencyReturn { get; set; }
    public decimal AverageWeight { get; set; }
}

/// <summary>
/// Position attribution.
/// </summary>
public sealed class PositionAttribution
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime OpenDate { get; set; }
    public DateTime CloseDate { get; set; }
    public int HoldingPeriodDays { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal AnnualizedReturn { get; set; }
    public decimal PriceReturn { get; set; }
    public decimal DividendReturn { get; set; }
    public decimal CurrencyReturn { get; set; }
}

/// <summary>
/// Holding period attribution.
/// </summary>
public sealed class HoldingPeriodAttribution
{
    public decimal TotalReturn { get; set; }
    public decimal TotalPriceReturn { get; set; }
    public decimal TotalDividendReturn { get; set; }
    public decimal TotalCurrencyReturn { get; set; }
    public List<PositionAttribution> PositionAttributions { get; set; } = [];
    public Dictionary<string, decimal> ReturnByHoldingPeriod { get; set; } = [];
}
