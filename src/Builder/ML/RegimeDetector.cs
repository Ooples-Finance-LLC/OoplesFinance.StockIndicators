namespace OoplesFinance.StockIndicators.Builder.ML;

/// <summary>
/// Detects market regimes using statistical and machine learning methods.
/// Identifies bull markets, bear markets, high/low volatility periods, and sideways markets.
/// </summary>
public sealed class RegimeDetector
{
    /// <summary>Gets or sets the lookback period for regime detection.</summary>
    public int LookbackPeriod { get; set; } = 252; // 1 year of trading days

    /// <summary>Gets or sets the short-term lookback for trend detection.</summary>
    public int ShortTermLookback { get; set; } = 20;

    /// <summary>Gets or sets the volatility percentile threshold for high volatility.</summary>
    public double HighVolatilityThreshold { get; set; } = 75;

    /// <summary>Gets or sets the volatility percentile threshold for low volatility.</summary>
    public double LowVolatilityThreshold { get; set; } = 25;

    /// <summary>Gets or sets the trend strength threshold.</summary>
    public double TrendStrengthThreshold { get; set; } = 0.02;

    /// <summary>
    /// Detects the current market regime from price data.
    /// </summary>
    /// <param name="prices">Historical closing prices (oldest to newest).</param>
    /// <returns>The detected market regime.</returns>
    public MarketRegime DetectRegime(IReadOnlyList<decimal> prices)
    {
        if (prices.Count < LookbackPeriod)
        {
            return MarketRegime.Unknown;
        }

        // Calculate returns
        var returns = CalculateReturns(prices);

        // Detect trend
        var trend = DetectTrend(prices, returns);

        // Detect volatility regime
        var volatilityRegime = DetectVolatilityRegime(returns);

        // Combine into market regime
        return CombineRegimes(trend, volatilityRegime);
    }

    /// <summary>
    /// Detects market regime with full analysis.
    /// </summary>
    public RegimeAnalysis AnalyzeRegime(IReadOnlyList<decimal> prices)
    {
        if (prices.Count < LookbackPeriod)
        {
            return new RegimeAnalysis
            {
                CurrentRegime = MarketRegime.Unknown,
                Confidence = 0
            };
        }

        var returns = CalculateReturns(prices);
        var trend = DetectTrend(prices, returns);
        var volatilityRegime = DetectVolatilityRegime(returns);
        var regime = CombineRegimes(trend, volatilityRegime);

        // Calculate various metrics
        var volatility = CalculateVolatility(returns, ShortTermLookback);
        var longTermVolatility = CalculateVolatility(returns, LookbackPeriod);
        var momentum = CalculateMomentum(prices, ShortTermLookback);
        var trendStrength = CalculateTrendStrength(prices);
        var autocorrelation = CalculateAutocorrelation(returns, 1);

        // Calculate regime probability
        var confidence = CalculateRegimeConfidence(trend, volatilityRegime, trendStrength);

        return new RegimeAnalysis
        {
            CurrentRegime = regime,
            Confidence = confidence,
            TrendDirection = trend,
            VolatilityRegime = volatilityRegime,
            ShortTermVolatility = volatility,
            LongTermVolatility = longTermVolatility,
            Momentum = momentum,
            TrendStrength = trendStrength,
            Autocorrelation = autocorrelation,
            RegimeProbabilities = CalculateRegimeProbabilities(returns, prices)
        };
    }

    /// <summary>
    /// Detects regime transitions over time.
    /// </summary>
    public List<RegimeTransition> DetectRegimeTransitions(IReadOnlyList<decimal> prices, int windowSize = 20)
    {
        var transitions = new List<RegimeTransition>();
        var previousRegime = MarketRegime.Unknown;

        for (var i = LookbackPeriod; i <= prices.Count; i++)
        {
            var windowPrices = prices.Take(i).ToList();
            var currentRegime = DetectRegime(windowPrices);

            if (currentRegime != previousRegime && previousRegime != MarketRegime.Unknown)
            {
                transitions.Add(new RegimeTransition
                {
                    Index = i - 1,
                    FromRegime = previousRegime,
                    ToRegime = currentRegime,
                    Price = prices[i - 1]
                });
            }

            previousRegime = currentRegime;
        }

        return transitions;
    }

    private double[] CalculateReturns(IReadOnlyList<decimal> prices)
    {
        var returns = new double[prices.Count - 1];
        for (var i = 1; i < prices.Count; i++)
        {
            returns[i - 1] = (double)((prices[i] - prices[i - 1]) / prices[i - 1]);
        }
        return returns;
    }

    private TrendDirection DetectTrend(IReadOnlyList<decimal> prices, double[] returns)
    {
        // Calculate short-term and long-term moving averages
        var shortMA = prices.TakeLast(ShortTermLookback).Average();
        var longMA = prices.TakeLast(LookbackPeriod / 2).Average();

        // Calculate momentum
        var recentReturn = returns.TakeLast(ShortTermLookback).Sum();

        // Determine trend
        if (shortMA > longMA && recentReturn > TrendStrengthThreshold)
        {
            return TrendDirection.Bullish;
        }
        else if (shortMA < longMA && recentReturn < -TrendStrengthThreshold)
        {
            return TrendDirection.Bearish;
        }
        else
        {
            return TrendDirection.Sideways;
        }
    }

    private VolatilityRegime DetectVolatilityRegime(double[] returns)
    {
        var currentVolatility = CalculateVolatility(returns, ShortTermLookback);
        var historicalVolatility = CalculateVolatility(returns, LookbackPeriod);

        var ratio = currentVolatility / historicalVolatility;
        var percentile = CalculateVolatilityPercentile(returns, currentVolatility);

        if (percentile >= HighVolatilityThreshold || ratio > 1.5)
        {
            return VolatilityRegime.High;
        }
        else if (percentile <= LowVolatilityThreshold || ratio < 0.7)
        {
            return VolatilityRegime.Low;
        }
        else
        {
            return VolatilityRegime.Normal;
        }
    }

    private double CalculateVolatility(double[] returns, int period)
    {
        var relevantReturns = returns.TakeLast(period).ToArray();
        if (relevantReturns.Length < 2) return 0;

        var mean = relevantReturns.Average();
        var variance = relevantReturns.Select(r => (r - mean) * (r - mean)).Average();
        return Math.Sqrt(variance * 252); // Annualized
    }

    private double CalculateVolatilityPercentile(double[] returns, double currentVolatility)
    {
        var historicalVolatilities = new List<double>();
        var window = Math.Min(20, returns.Length);

        for (var i = window; i <= returns.Length; i++)
        {
            var windowReturns = returns.Skip(i - window).Take(window).ToArray();
            var vol = CalculateVolatility(windowReturns, window);
            historicalVolatilities.Add(vol);
        }

        if (historicalVolatilities.Count == 0) return 50;

        var countBelow = historicalVolatilities.Count(v => v <= currentVolatility);
        return (double)countBelow / historicalVolatilities.Count * 100;
    }

    private double CalculateMomentum(IReadOnlyList<decimal> prices, int period)
    {
        if (prices.Count < period + 1) return 0;

        var currentPrice = prices[prices.Count - 1];
        var pastPrice = prices[prices.Count - 1 - period];

        return (double)((currentPrice - pastPrice) / pastPrice);
    }

    private double CalculateTrendStrength(IReadOnlyList<decimal> prices)
    {
        // Use ADX-like calculation for trend strength
        var period = Math.Min(14, prices.Count - 1);
        if (prices.Count < period + 1) return 0;

        var plusDM = new List<double>();
        var minusDM = new List<double>();
        var tr = new List<double>();

        for (var i = prices.Count - period; i < prices.Count; i++)
        {
            var high = prices[i];
            var low = prices[i];
            var prevHigh = prices[i - 1];
            var prevLow = prices[i - 1];
            var prevClose = prices[i - 1];

            var upMove = (double)(high - prevHigh);
            var downMove = (double)(prevLow - low);

            plusDM.Add(upMove > downMove && upMove > 0 ? upMove : 0);
            minusDM.Add(downMove > upMove && downMove > 0 ? downMove : 0);

            var trueRange = Math.Max(Math.Max(
                (double)(high - low),
                Math.Abs((double)(high - prevClose))),
                Math.Abs((double)(low - prevClose)));
            tr.Add(trueRange);
        }

        var avgTR = tr.Average();
        if (avgTR < 0.0001) return 0;

        var plusDI = plusDM.Average() / avgTR * 100;
        var minusDI = minusDM.Average() / avgTR * 100;

        var dx = Math.Abs(plusDI - minusDI) / (plusDI + minusDI + 0.0001) * 100;
        return dx / 100; // Normalize to 0-1
    }

    private double CalculateAutocorrelation(double[] returns, int lag)
    {
        if (returns.Length < lag + 2) return 0;

        var mean = returns.Average();
        var variance = returns.Select(r => (r - mean) * (r - mean)).Average();

        if (variance < 0.0001) return 0;

        var covariance = 0.0;
        for (var i = lag; i < returns.Length; i++)
        {
            covariance += (returns[i] - mean) * (returns[i - lag] - mean);
        }
        covariance /= returns.Length - lag;

        return covariance / variance;
    }

    private double CalculateRegimeConfidence(TrendDirection trend, VolatilityRegime volatility, double trendStrength)
    {
        var baseConfidence = 0.5 + trendStrength * 0.3;

        // Strong trends have higher confidence
        if (trend != TrendDirection.Sideways)
        {
            baseConfidence += 0.1;
        }

        // Extreme volatility regimes are more certain
        if (volatility != VolatilityRegime.Normal)
        {
            baseConfidence += 0.1;
        }

        return Math.Min(1.0, baseConfidence);
    }

    private Dictionary<MarketRegime, double> CalculateRegimeProbabilities(double[] returns, IReadOnlyList<decimal> prices)
    {
        var probabilities = new Dictionary<MarketRegime, double>();

        var trend = DetectTrend(prices, returns);
        var volatility = DetectVolatilityRegime(returns);
        var trendStrength = CalculateTrendStrength(prices);

        // Calculate base probabilities
        var bullProb = trend == TrendDirection.Bullish ? 0.6 : (trend == TrendDirection.Sideways ? 0.2 : 0.1);
        var bearProb = trend == TrendDirection.Bearish ? 0.6 : (trend == TrendDirection.Sideways ? 0.2 : 0.1);
        var sideProb = trend == TrendDirection.Sideways ? 0.6 : 0.2;

        // Adjust for volatility
        if (volatility == VolatilityRegime.High)
        {
            bearProb *= 1.3;
            bullProb *= 0.9;
        }
        else if (volatility == VolatilityRegime.Low)
        {
            bullProb *= 1.2;
            sideProb *= 1.1;
        }

        // Normalize
        var total = bullProb + bearProb + sideProb;
        probabilities[MarketRegime.Bull] = bullProb / total;
        probabilities[MarketRegime.Bear] = bearProb / total;
        probabilities[MarketRegime.Sideways] = sideProb / total;
        probabilities[MarketRegime.HighVolatility] = volatility == VolatilityRegime.High ? 0.8 : 0.2;
        probabilities[MarketRegime.LowVolatility] = volatility == VolatilityRegime.Low ? 0.8 : 0.2;

        return probabilities;
    }

    private MarketRegime CombineRegimes(TrendDirection trend, VolatilityRegime volatility)
    {
        return (trend, volatility) switch
        {
            (TrendDirection.Bullish, VolatilityRegime.Low) => MarketRegime.Bull,
            (TrendDirection.Bullish, VolatilityRegime.Normal) => MarketRegime.Bull,
            (TrendDirection.Bullish, VolatilityRegime.High) => MarketRegime.HighVolatility,
            (TrendDirection.Bearish, VolatilityRegime.Low) => MarketRegime.Bear,
            (TrendDirection.Bearish, VolatilityRegime.Normal) => MarketRegime.Bear,
            (TrendDirection.Bearish, VolatilityRegime.High) => MarketRegime.Crash,
            (TrendDirection.Sideways, VolatilityRegime.Low) => MarketRegime.LowVolatility,
            (TrendDirection.Sideways, VolatilityRegime.Normal) => MarketRegime.Sideways,
            (TrendDirection.Sideways, VolatilityRegime.High) => MarketRegime.HighVolatility,
            _ => MarketRegime.Unknown
        };
    }
}

/// <summary>
/// Market regime types.
/// </summary>
public enum MarketRegime
{
    /// <summary>Unknown or insufficient data.</summary>
    Unknown,

    /// <summary>Bull market - sustained upward trend.</summary>
    Bull,

    /// <summary>Bear market - sustained downward trend.</summary>
    Bear,

    /// <summary>Sideways/ranging market.</summary>
    Sideways,

    /// <summary>High volatility period.</summary>
    HighVolatility,

    /// <summary>Low volatility/calm period.</summary>
    LowVolatility,

    /// <summary>Market crash - bear market with high volatility.</summary>
    Crash,

    /// <summary>Recovery - transitioning from bear to bull.</summary>
    Recovery
}

/// <summary>
/// Trend direction.
/// </summary>
public enum TrendDirection
{
    /// <summary>Upward trend.</summary>
    Bullish,

    /// <summary>Downward trend.</summary>
    Bearish,

    /// <summary>No clear trend.</summary>
    Sideways
}

/// <summary>
/// Volatility regime.
/// </summary>
public enum VolatilityRegime
{
    /// <summary>Low volatility.</summary>
    Low,

    /// <summary>Normal volatility.</summary>
    Normal,

    /// <summary>High volatility.</summary>
    High
}

/// <summary>
/// Full regime analysis result.
/// </summary>
public sealed class RegimeAnalysis
{
    /// <summary>Gets or sets the detected regime.</summary>
    public MarketRegime CurrentRegime { get; set; }

    /// <summary>Gets or sets the confidence level (0-1).</summary>
    public double Confidence { get; set; }

    /// <summary>Gets or sets the trend direction.</summary>
    public TrendDirection TrendDirection { get; set; }

    /// <summary>Gets or sets the volatility regime.</summary>
    public VolatilityRegime VolatilityRegime { get; set; }

    /// <summary>Gets or sets the short-term volatility.</summary>
    public double ShortTermVolatility { get; set; }

    /// <summary>Gets or sets the long-term volatility.</summary>
    public double LongTermVolatility { get; set; }

    /// <summary>Gets or sets the momentum.</summary>
    public double Momentum { get; set; }

    /// <summary>Gets or sets the trend strength (0-1).</summary>
    public double TrendStrength { get; set; }

    /// <summary>Gets or sets the return autocorrelation.</summary>
    public double Autocorrelation { get; set; }

    /// <summary>Gets or sets the regime probabilities.</summary>
    public Dictionary<MarketRegime, double> RegimeProbabilities { get; set; } = new();
}

/// <summary>
/// Represents a regime transition.
/// </summary>
public sealed class RegimeTransition
{
    /// <summary>Gets or sets the index where transition occurred.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the previous regime.</summary>
    public MarketRegime FromRegime { get; set; }

    /// <summary>Gets or sets the new regime.</summary>
    public MarketRegime ToRegime { get; set; }

    /// <summary>Gets or sets the price at transition.</summary>
    public decimal Price { get; set; }
}
