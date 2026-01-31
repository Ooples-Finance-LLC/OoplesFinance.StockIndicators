namespace OoplesFinance.StockIndicators.Builder.AI;

/// <summary>
/// Unified AI analysis service interface for financial analysis.
/// Provides sentiment analysis, time series forecasting, and risk analysis
/// using advanced ML models like FinBERT, PatchTST, and NeuralVaR.
/// </summary>
public interface IAIAnalysisService
{
    /// <summary>
    /// Analyzes sentiment of financial text using FinBERT.
    /// </summary>
    /// <param name="text">The text to analyze (news headline, social post, etc.)</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sentiment analysis result with score and classification.</returns>
    Task<FinBertSentimentResult> AnalyzeSentimentAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes sentiment for a batch of texts efficiently.
    /// </summary>
    /// <param name="texts">Collection of texts to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sentiment results matching input order.</returns>
    Task<IReadOnlyList<FinBertSentimentResult>> AnalyzeBatchSentimentAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forecasts future price movements using PatchTST time series model.
    /// </summary>
    /// <param name="symbol">The symbol being forecast.</param>
    /// <param name="historicalPrices">Historical OHLCV data.</param>
    /// <param name="forecastHorizon">Number of periods to forecast (default: 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Price forecast with confidence intervals.</returns>
    Task<PriceForecastResult> ForecastPriceAsync(
        string symbol,
        IReadOnlyList<OhlcvData> historicalPrices,
        int forecastHorizon = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates Value at Risk using neural network model (NeuralVaR).
    /// </summary>
    /// <param name="portfolio">Portfolio positions with values.</param>
    /// <param name="confidenceLevel">Confidence level (default: 0.95 = 95%).</param>
    /// <param name="holdingPeriod">Holding period in days (default: 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>VaR and CVaR calculations.</returns>
    Task<NeuralVaRResult> CalculateNeuralVaRAsync(
        IReadOnlyList<PortfolioPosition> portfolio,
        decimal confidenceLevel = 0.95m,
        int holdingPeriod = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a trading signal using multi-agent AI system.
    /// </summary>
    /// <param name="symbol">The symbol to analyze.</param>
    /// <param name="context">Current market context (prices, sentiment, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AI-generated trading signal with reasoning.</returns>
    Task<AITradingSignal> GetTradingSignalAsync(
        string symbol,
        MarketContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets embeddings for financial text for similarity comparisons.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Vector embeddings for the text.</returns>
    Task<float[]> GetEmbeddingsAsync(
        string text,
        CancellationToken cancellationToken = default);
}

#region Result Models

/// <summary>
/// Result from FinBERT sentiment analysis.
/// </summary>
public sealed class FinBertSentimentResult
{
    /// <summary>The original text analyzed.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Sentiment score from -1 (very bearish) to +1 (very bullish).</summary>
    public decimal SentimentScore { get; set; }

    /// <summary>Classified sentiment category.</summary>
    public FinBertSentiment Sentiment { get; set; }

    /// <summary>Confidence in the classification (0-1).</summary>
    public decimal Confidence { get; set; }

    /// <summary>Raw probability scores from the model.</summary>
    public FinBertProbabilities Probabilities { get; set; } = new();

    /// <summary>When the analysis was performed.</summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// FinBERT sentiment classification.
/// </summary>
public enum FinBertSentiment
{
    VeryBearish,
    Bearish,
    Neutral,
    Bullish,
    VeryBullish
}

/// <summary>
/// Raw probability outputs from FinBERT.
/// </summary>
public sealed class FinBertProbabilities
{
    public decimal Positive { get; set; }
    public decimal Neutral { get; set; }
    public decimal Negative { get; set; }
}

/// <summary>
/// OHLCV data for time series forecasting.
/// </summary>
public sealed class OhlcvData
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

/// <summary>
/// Result from PatchTST price forecasting.
/// </summary>
public sealed class PriceForecastResult
{
    /// <summary>The symbol being forecast.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Point forecasts for each period.</summary>
    public IReadOnlyList<ForecastPoint> Forecasts { get; set; } = Array.Empty<ForecastPoint>();

    /// <summary>Direction prediction (bullish, bearish, neutral).</summary>
    public ForecastDirection Direction { get; set; }

    /// <summary>Confidence in the forecast (0-1).</summary>
    public decimal Confidence { get; set; }

    /// <summary>Model metrics used for this forecast.</summary>
    public ForecastMetrics Metrics { get; set; } = new();

    /// <summary>When the forecast was generated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Single forecast point with confidence intervals.
/// </summary>
public sealed class ForecastPoint
{
    /// <summary>The forecast timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Point estimate (predicted price).</summary>
    public decimal PredictedPrice { get; set; }

    /// <summary>Lower bound of 95% confidence interval.</summary>
    public decimal LowerBound95 { get; set; }

    /// <summary>Upper bound of 95% confidence interval.</summary>
    public decimal UpperBound95 { get; set; }

    /// <summary>Lower bound of 80% confidence interval.</summary>
    public decimal LowerBound80 { get; set; }

    /// <summary>Upper bound of 80% confidence interval.</summary>
    public decimal UpperBound80 { get; set; }
}

/// <summary>
/// Forecast direction.
/// </summary>
public enum ForecastDirection
{
    StrongBearish,
    Bearish,
    Neutral,
    Bullish,
    StrongBullish
}

/// <summary>
/// Model performance metrics.
/// </summary>
public sealed class ForecastMetrics
{
    public decimal MAE { get; set; }    // Mean Absolute Error
    public decimal RMSE { get; set; }   // Root Mean Square Error
    public decimal MAPE { get; set; }   // Mean Absolute Percentage Error
    public decimal R2 { get; set; }     // R-squared
}

/// <summary>
/// Portfolio position for VaR calculation.
/// </summary>
public sealed class PortfolioPosition
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue => Quantity * CurrentPrice;
    public string AssetClass { get; set; } = "Equity";
    public string? Sector { get; set; }
}

/// <summary>
/// Result from NeuralVaR calculation.
/// </summary>
public sealed class NeuralVaRResult
{
    /// <summary>Total portfolio value.</summary>
    public decimal PortfolioValue { get; set; }

    /// <summary>Value at Risk (potential loss at confidence level).</summary>
    public decimal VaR { get; set; }

    /// <summary>VaR as percentage of portfolio.</summary>
    public decimal VaRPercent => PortfolioValue > 0 ? VaR / PortfolioValue : 0;

    /// <summary>Conditional VaR (Expected Shortfall) - average loss beyond VaR.</summary>
    public decimal CVaR { get; set; }

    /// <summary>CVaR as percentage of portfolio.</summary>
    public decimal CVaRPercent => PortfolioValue > 0 ? CVaR / PortfolioValue : 0;

    /// <summary>Confidence level used (e.g., 0.95 for 95%).</summary>
    public decimal ConfidenceLevel { get; set; }

    /// <summary>Holding period in days.</summary>
    public int HoldingPeriod { get; set; }

    /// <summary>Risk contribution by position.</summary>
    public IReadOnlyList<RiskContribution> RiskContributions { get; set; } = Array.Empty<RiskContribution>();

    /// <summary>When the calculation was performed.</summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Risk contribution from a single position.
/// </summary>
public sealed class RiskContribution
{
    public string Symbol { get; set; } = string.Empty;
    public decimal MarketValue { get; set; }
    public decimal PortfolioWeight { get; set; }
    public decimal RiskContributionPercent { get; set; }
    public decimal MarginalVaR { get; set; }
}

/// <summary>
/// Market context for trading signal generation.
/// </summary>
public sealed class MarketContext
{
    public string Symbol { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal PriceChange24h { get; set; }
    public decimal PriceChangePercent24h { get; set; }
    public long Volume24h { get; set; }
    public decimal AverageVolume { get; set; }
    public decimal SentimentScore { get; set; }
    public int SocialMentions { get; set; }
    public decimal RSI { get; set; }
    public decimal MACD { get; set; }
    public decimal MACDSignal { get; set; }
    public decimal BollingerUpper { get; set; }
    public decimal BollingerLower { get; set; }
    public bool HasRecentNews { get; set; }
    public bool HasUpcomingEarnings { get; set; }
    public IReadOnlyList<OhlcvData> RecentPrices { get; set; } = Array.Empty<OhlcvData>();
}

/// <summary>
/// AI-generated trading signal.
/// </summary>
public sealed class AITradingSignal
{
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Signal type (buy, sell, hold).</summary>
    public SignalType Signal { get; set; }

    /// <summary>Signal strength from 0 (weak) to 1 (strong).</summary>
    public decimal Strength { get; set; }

    /// <summary>Confidence in the signal (0-1).</summary>
    public decimal Confidence { get; set; }

    /// <summary>Suggested entry price.</summary>
    public decimal? SuggestedEntry { get; set; }

    /// <summary>Suggested stop loss price.</summary>
    public decimal? SuggestedStopLoss { get; set; }

    /// <summary>Suggested take profit price.</summary>
    public decimal? SuggestedTakeProfit { get; set; }

    /// <summary>AI reasoning for the signal.</summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>Key factors that influenced the decision.</summary>
    public IReadOnlyList<string> KeyFactors { get; set; } = Array.Empty<string>();

    /// <summary>Time horizon for the trade.</summary>
    public SignalTimeHorizon TimeHorizon { get; set; }

    /// <summary>When the signal was generated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Signal expiration (after which it should be re-evaluated).</summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Signal type.
/// </summary>
public enum SignalType
{
    StrongBuy,
    Buy,
    Hold,
    Sell,
    StrongSell
}

/// <summary>
/// Signal time horizon.
/// </summary>
public enum SignalTimeHorizon
{
    Scalp,       // Minutes
    Intraday,    // Same day
    Swing,       // Days to weeks
    Position,    // Weeks to months
    LongTerm     // Months+
}

#endregion
