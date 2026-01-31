using System.Collections.Concurrent;
using OoplesFinance.StockIndicators.Builder.DataProviders;
using OoplesFinance.StockIndicators.Builder.DataProviders.Pipeline;
using OoplesFinance.StockIndicators.Builder.ML.Sentiment;

namespace OoplesFinance.StockIndicators.Builder.AI;

/// <summary>
/// AI-powered trading assistant that provides intelligent trade recommendations,
/// natural language interaction, and real-time market insights.
/// This is the main integration point connecting all AI/ML components to the trading UI.
/// </summary>
public sealed class AITradingAssistant : IDisposable
{
    private readonly AIAssistantOptions _options;
    private readonly INewsProvider? _newsProvider;
    private readonly ISocialSentimentProvider? _socialProvider;
    private readonly ISECFilingProvider? _secFilingProvider;
    private readonly NewsSentimentAnalyzer _newsAnalyzer;
    private readonly SocialSentimentAnalyzer _socialAnalyzer;
    private readonly SECFilingAnalyzer _secAnalyzer;
    private readonly ConcurrentDictionary<string, SymbolIntelligence> _symbolIntelligence = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastAnalysisTime = new();
    private readonly SemaphoreSlim _analysisLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Creates a new AI trading assistant.
    /// </summary>
    public AITradingAssistant(
        AIAssistantOptions? options = null,
        INewsProvider? newsProvider = null,
        ISocialSentimentProvider? socialProvider = null,
        ISECFilingProvider? secFilingProvider = null)
    {
        _options = options ?? new AIAssistantOptions();
        _newsProvider = newsProvider;
        _socialProvider = socialProvider;
        _secFilingProvider = secFilingProvider;

        _newsAnalyzer = new NewsSentimentAnalyzer();
        _socialAnalyzer = new SocialSentimentAnalyzer();
        _secAnalyzer = new SECFilingAnalyzer();
    }

    /// <summary>
    /// Gets an AI-powered trade recommendation for a symbol.
    /// Combines sentiment analysis, technical signals, and risk assessment.
    /// </summary>
    public async Task<TradeRecommendation> GetRecommendationAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var intelligence = await GetSymbolIntelligenceAsync(symbol, cancellationToken)
            .ConfigureAwait(false);

        var recommendation = new TradeRecommendation
        {
            Symbol = symbol,
            GeneratedAt = DateTime.UtcNow,
            Action = DetermineAction(intelligence),
            Confidence = CalculateConfidence(intelligence),
            Reasoning = GenerateReasoning(intelligence),
            SentimentScore = intelligence.OverallSentiment,
            RiskLevel = AssessRiskLevel(intelligence),
            KeyFactors = ExtractKeyFactors(intelligence),
            SuggestedEntryPrice = intelligence.CurrentPrice,
            SuggestedStopLoss = CalculateStopLoss(intelligence),
            SuggestedTakeProfit = CalculateTakeProfit(intelligence),
            TimeHorizon = DetermineTimeHorizon(intelligence)
        };

        return recommendation;
    }

    /// <summary>
    /// Answers natural language questions about markets and trading.
    /// Examples:
    /// - "Why is AAPL down today?"
    /// - "Should I buy TSLA?"
    /// - "Show me bullish tech stocks"
    /// - "What's the sentiment on crypto?"
    /// </summary>
    public async Task<AIResponse> AskAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuestion = question.ToLowerInvariant().Trim();

        // Parse intent
        var intent = ParseIntent(normalizedQuestion);

        return intent.Type switch
        {
            QueryIntent.ExplainMovement => await HandleExplainMovementAsync(intent, cancellationToken),
            QueryIntent.ShouldBuy => await HandleShouldBuyAsync(intent, cancellationToken),
            QueryIntent.ShouldSell => await HandleShouldSellAsync(intent, cancellationToken),
            QueryIntent.FindStocks => await HandleFindStocksAsync(intent, cancellationToken),
            QueryIntent.GetSentiment => await HandleGetSentimentAsync(intent, cancellationToken),
            QueryIntent.WhatNews => await HandleWhatNewsAsync(intent, cancellationToken),
            QueryIntent.RiskAssessment => await HandleRiskAssessmentAsync(intent, cancellationToken),
            _ => new AIResponse
            {
                Query = question,
                Answer = "I can help you with trading questions like:\n" +
                         "- \"Why is [symbol] up/down?\"\n" +
                         "- \"Should I buy [symbol]?\"\n" +
                         "- \"Show me bullish stocks\"\n" +
                         "- \"What's the sentiment on [symbol]?\"\n" +
                         "- \"Any news on [symbol]?\"",
                Confidence = 1.0m,
                GeneratedAt = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// Explains why a stock is moving (up or down).
    /// </summary>
    public async Task<string> ExplainMovementAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var intelligence = await GetSymbolIntelligenceAsync(symbol, cancellationToken)
            .ConfigureAwait(false);

        var reasons = new List<string>();

        // News-driven reasons
        if (intelligence.RecentNews.Count > 0)
        {
            var topNews = intelligence.RecentNews
                .OrderByDescending(n => Math.Abs(n.SentimentScore))
                .FirstOrDefault();

            if (topNews is not null)
            {
                var direction = topNews.SentimentScore > 0 ? "positive" : "negative";
                reasons.Add($"Recent {direction} news: \"{topNews.Title}\"");
            }
        }

        // Social sentiment
        if (Math.Abs(intelligence.SocialSentiment) > 0.3m)
        {
            var sentiment = intelligence.SocialSentiment > 0 ? "bullish" : "bearish";
            reasons.Add($"Social media is {sentiment} ({intelligence.SocialMentions:N0} mentions)");
        }

        // SEC filings
        if (intelligence.RecentFilings.Count > 0)
        {
            var recentFiling = intelligence.RecentFilings.First();
            reasons.Add($"Recent SEC filing: {recentFiling.FormType} filed on {recentFiling.FiledAt:MMM dd}");
        }

        // Insider activity
        if (intelligence.InsiderBuyValue > 0 || intelligence.InsiderSellValue > 0)
        {
            if (intelligence.InsiderBuyValue > intelligence.InsiderSellValue)
            {
                reasons.Add($"Insider buying: ${intelligence.InsiderBuyValue:N0} in purchases");
            }
            else
            {
                reasons.Add($"Insider selling: ${intelligence.InsiderSellValue:N0} in sales");
            }
        }

        if (reasons.Count == 0)
        {
            return $"No significant news or events found for {symbol}. The movement may be driven by overall market conditions or technical factors.";
        }

        return $"Possible reasons for {symbol}'s movement:\n" +
               string.Join("\n", reasons.Select((r, i) => $"{i + 1}. {r}"));
    }

    /// <summary>
    /// Assesses risk before placing a trade.
    /// </summary>
    public async Task<RiskAssessment> AssessTradeRiskAsync(
        string symbol,
        TradeDirection direction,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        var intelligence = await GetSymbolIntelligenceAsync(symbol, cancellationToken)
            .ConfigureAwait(false);

        var warnings = new List<string>();
        var riskScore = 0.5m; // Base risk

        // Check if sentiment aligns with trade direction
        if (direction == TradeDirection.Buy && intelligence.OverallSentiment < -0.3m)
        {
            warnings.Add("Warning: Sentiment is bearish. Consider waiting for better entry.");
            riskScore += 0.15m;
        }
        else if (direction == TradeDirection.Sell && intelligence.OverallSentiment > 0.3m)
        {
            warnings.Add("Warning: Sentiment is bullish. You may be selling too early.");
            riskScore += 0.1m;
        }

        // Check for upcoming events
        if (intelligence.HasUpcomingEarnings)
        {
            warnings.Add("Caution: Earnings announcement is upcoming. Expect high volatility.");
            riskScore += 0.2m;
        }

        // Check insider activity
        if (direction == TradeDirection.Buy && intelligence.InsiderSellValue > intelligence.InsiderBuyValue * 2)
        {
            warnings.Add("Note: Significant insider selling detected.");
            riskScore += 0.1m;
        }

        // Check social momentum
        if (intelligence.SocialMentionVelocity > 10 && Math.Abs(intelligence.SocialSentiment) > 0.5m)
        {
            warnings.Add("Caution: High social media activity. May indicate FOMO/panic trading.");
            riskScore += 0.1m;
        }

        // Clamp risk score
        riskScore = Math.Clamp(riskScore, 0m, 1m);

        return new RiskAssessment
        {
            Symbol = symbol,
            Direction = direction,
            Quantity = quantity,
            RiskScore = riskScore,
            RiskLevel = riskScore switch
            {
                < 0.3m => RiskLevel.Low,
                < 0.6m => RiskLevel.Medium,
                < 0.8m => RiskLevel.High,
                _ => RiskLevel.VeryHigh
            },
            Warnings = warnings,
            ShouldProceed = riskScore < _options.MaxAcceptableRiskScore,
            Reasoning = GenerateRiskReasoning(riskScore, warnings),
            AssessedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets AI-suggested watchlist items based on current market conditions.
    /// </summary>
    public async Task<IReadOnlyList<WatchlistSuggestion>> GetWatchlistSuggestionsAsync(
        WatchlistCriteria? criteria = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new WatchlistCriteria();
        var suggestions = new List<WatchlistSuggestion>();

        // Get trending from social
        if (_socialProvider is not null)
        {
            try
            {
                var trending = await _socialProvider.GetTrendingTickersAsync(limit * 2, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var ticker in trending)
                {
                    if (criteria.MinSentiment.HasValue && ticker.SentimentScore < criteria.MinSentiment.Value)
                        continue;

                    if (criteria.MaxSentiment.HasValue && ticker.SentimentScore > criteria.MaxSentiment.Value)
                        continue;

                    suggestions.Add(new WatchlistSuggestion
                    {
                        Symbol = ticker.Symbol,
                        Reason = $"Trending on social media (#{ticker.Rank})",
                        SentimentScore = ticker.SentimentScore,
                        MentionCount = ticker.MentionCount,
                        TrendingRank = ticker.Rank,
                        SuggestedAt = DateTime.UtcNow
                    });
                }
            }
            catch
            {
                // Continue without social data
            }
        }

        // Filter and sort
        return suggestions
            .OrderByDescending(s => (double)Math.Abs(s.SentimentScore) * 0.5 + (1.0 / (s.TrendingRank + 1)) * 0.5)
            .Take(limit)
            .ToList();
    }

    private async Task<SymbolIntelligence> GetSymbolIntelligenceAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();

        // Check cache
        if (_symbolIntelligence.TryGetValue(normalizedSymbol, out var cached) &&
            _lastAnalysisTime.TryGetValue(normalizedSymbol, out var lastTime) &&
            DateTime.UtcNow - lastTime < _options.CacheExpiration)
        {
            return cached;
        }

        await _analysisLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_symbolIntelligence.TryGetValue(normalizedSymbol, out cached) &&
                _lastAnalysisTime.TryGetValue(normalizedSymbol, out lastTime) &&
                DateTime.UtcNow - lastTime < _options.CacheExpiration)
            {
                return cached;
            }

            var intelligence = new SymbolIntelligence { Symbol = normalizedSymbol };

            // Gather news sentiment
            if (_newsProvider is not null)
            {
                try
                {
                    var news = await _newsProvider.GetLatestNewsAsync(normalizedSymbol, 20, cancellationToken)
                        .ConfigureAwait(false);

                    var mlNews = news.Select(n => n.ToSentimentAnalyzerModel()).ToList();
                    var newsResult = _newsAnalyzer.Analyze(mlNews);

                    intelligence.NewsSentiment = (decimal)newsResult.AggregateScore;
                    intelligence.RecentNews = news.Select(n => new NewsInsight
                    {
                        Title = n.Title,
                        Source = n.Source,
                        PublishedAt = n.PublishedAt,
                        SentimentScore = n.SourceSentiment ?? 0
                    }).ToList();
                }
                catch
                {
                    // Continue without news data
                }
            }

            // Gather social sentiment
            if (_socialProvider is not null)
            {
                try
                {
                    var metrics = await _socialProvider.GetAggregatedMetricsAsync(
                        normalizedSymbol,
                        TimeSpan.FromHours(24),
                        cancellationToken).ConfigureAwait(false);

                    intelligence.SocialSentiment = metrics.AverageSentiment;
                    intelligence.SocialMentions = metrics.TotalMentions;
                    intelligence.SocialMentionVelocity = metrics.MentionVelocity;
                    intelligence.BullishCount = metrics.BullishCount;
                    intelligence.BearishCount = metrics.BearishCount;
                }
                catch
                {
                    // Continue without social data
                }
            }

            // Gather SEC filing data
            if (_secFilingProvider is not null)
            {
                try
                {
                    var filings = await _secFilingProvider.GetFilingsAsync(
                        normalizedSymbol,
                        null,
                        DateTime.UtcNow.AddDays(-30),
                        DateTime.UtcNow,
                        10,
                        cancellationToken).ConfigureAwait(false);

                    intelligence.RecentFilings = filings.Select(f => new FilingInsight
                    {
                        FormType = f.FormType,
                        FiledAt = f.FiledAt,
                        Description = f.Description ?? string.Empty
                    }).ToList();

                    // Get insider transactions
                    var insiderTxns = await _secFilingProvider.GetInsiderTransactionsAsync(
                        normalizedSymbol,
                        DateTime.UtcNow.AddDays(-90),
                        DateTime.UtcNow,
                        cancellationToken).ConfigureAwait(false);

                    intelligence.InsiderBuyValue = insiderTxns
                        .Where(t => t.TransactionType == InsiderTransactionType.Purchase)
                        .Sum(t => t.Shares * (t.PricePerShare ?? 0));

                    intelligence.InsiderSellValue = insiderTxns
                        .Where(t => t.TransactionType == InsiderTransactionType.Sale)
                        .Sum(t => t.Shares * (t.PricePerShare ?? 0));
                }
                catch
                {
                    // Continue without SEC data
                }
            }

            // Calculate overall sentiment
            intelligence.OverallSentiment = CalculateOverallSentiment(intelligence);
            intelligence.AnalyzedAt = DateTime.UtcNow;

            // Cache result
            _symbolIntelligence[normalizedSymbol] = intelligence;
            _lastAnalysisTime[normalizedSymbol] = DateTime.UtcNow;

            return intelligence;
        }
        finally
        {
            _analysisLock.Release();
        }
    }

    private decimal CalculateOverallSentiment(SymbolIntelligence intelligence)
    {
        var weights = _options.SentimentWeights;
        var total = 0m;
        var weightSum = 0m;

        if (intelligence.NewsSentiment != 0)
        {
            total += intelligence.NewsSentiment * weights.NewsWeight;
            weightSum += weights.NewsWeight;
        }

        if (intelligence.SocialSentiment != 0)
        {
            total += intelligence.SocialSentiment * weights.SocialWeight;
            weightSum += weights.SocialWeight;
        }

        // Insider activity sentiment
        if (intelligence.InsiderBuyValue > 0 || intelligence.InsiderSellValue > 0)
        {
            var insiderSentiment = intelligence.InsiderBuyValue > intelligence.InsiderSellValue
                ? 0.5m : -0.5m;
            total += insiderSentiment * weights.InsiderWeight;
            weightSum += weights.InsiderWeight;
        }

        return weightSum > 0 ? total / weightSum : 0;
    }

    private RecommendedAction DetermineAction(SymbolIntelligence intelligence)
    {
        var sentiment = intelligence.OverallSentiment;

        if (sentiment > _options.BullishThreshold)
            return RecommendedAction.StrongBuy;
        if (sentiment > _options.BullishThreshold / 2)
            return RecommendedAction.Buy;
        if (sentiment < _options.BearishThreshold)
            return RecommendedAction.StrongSell;
        if (sentiment < _options.BearishThreshold / 2)
            return RecommendedAction.Sell;

        return RecommendedAction.Hold;
    }

    private decimal CalculateConfidence(SymbolIntelligence intelligence)
    {
        var dataPoints = 0;
        if (intelligence.NewsSentiment != 0) dataPoints++;
        if (intelligence.SocialSentiment != 0) dataPoints++;
        if (intelligence.InsiderBuyValue > 0 || intelligence.InsiderSellValue > 0) dataPoints++;

        // More data = higher confidence
        var dataConfidence = dataPoints / 3.0m;

        // Strong sentiment = higher confidence
        var sentimentStrength = Math.Abs(intelligence.OverallSentiment);

        // Agreement between sources = higher confidence
        var agreementScore = 1.0m;
        if (intelligence.NewsSentiment != 0 && intelligence.SocialSentiment != 0)
        {
            var sameDirection = Math.Sign(intelligence.NewsSentiment) == Math.Sign(intelligence.SocialSentiment);
            agreementScore = sameDirection ? 1.0m : 0.6m;
        }

        return Math.Clamp(
            dataConfidence * 0.3m + sentimentStrength * 0.4m + agreementScore * 0.3m,
            0.1m, 0.95m);
    }

    private string GenerateReasoning(SymbolIntelligence intelligence)
    {
        var reasons = new List<string>();

        if (intelligence.NewsSentiment > 0.2m)
            reasons.Add("positive news coverage");
        else if (intelligence.NewsSentiment < -0.2m)
            reasons.Add("negative news coverage");

        if (intelligence.SocialSentiment > 0.3m)
            reasons.Add($"bullish social sentiment ({intelligence.BullishCount} bullish vs {intelligence.BearishCount} bearish)");
        else if (intelligence.SocialSentiment < -0.3m)
            reasons.Add($"bearish social sentiment ({intelligence.BearishCount} bearish vs {intelligence.BullishCount} bullish)");

        if (intelligence.InsiderBuyValue > intelligence.InsiderSellValue * 1.5m)
            reasons.Add("significant insider buying");
        else if (intelligence.InsiderSellValue > intelligence.InsiderBuyValue * 1.5m)
            reasons.Add("significant insider selling");

        if (reasons.Count == 0)
            return "Neutral sentiment across all data sources.";

        return "Based on " + string.Join(", ", reasons) + ".";
    }

    private RiskLevel AssessRiskLevel(SymbolIntelligence intelligence)
    {
        var riskScore = 0.5m;

        // High volatility in sentiment
        if (Math.Abs(intelligence.NewsSentiment - intelligence.SocialSentiment) > 0.5m)
            riskScore += 0.2m;

        // High social activity
        if (intelligence.SocialMentionVelocity > 20)
            riskScore += 0.15m;

        // Recent SEC filings
        if (intelligence.RecentFilings.Any(f => f.FormType == "8-K"))
            riskScore += 0.1m;

        return riskScore switch
        {
            < 0.3m => RiskLevel.Low,
            < 0.5m => RiskLevel.Medium,
            < 0.7m => RiskLevel.High,
            _ => RiskLevel.VeryHigh
        };
    }

    private IReadOnlyList<string> ExtractKeyFactors(SymbolIntelligence intelligence)
    {
        var factors = new List<string>();

        if (Math.Abs(intelligence.NewsSentiment) > 0.2m)
            factors.Add($"News sentiment: {(intelligence.NewsSentiment > 0 ? "Positive" : "Negative")}");

        if (intelligence.SocialMentions > 100)
            factors.Add($"Social mentions: {intelligence.SocialMentions:N0} in 24h");

        if (intelligence.RecentFilings.Count > 0)
            factors.Add($"Recent SEC filing: {intelligence.RecentFilings.First().FormType}");

        if (intelligence.InsiderBuyValue > 10000 || intelligence.InsiderSellValue > 10000)
        {
            var netActivity = intelligence.InsiderBuyValue - intelligence.InsiderSellValue;
            factors.Add($"Insider activity: Net {(netActivity > 0 ? "buying" : "selling")} ${Math.Abs(netActivity):N0}");
        }

        return factors;
    }

    private decimal? CalculateStopLoss(SymbolIntelligence intelligence)
    {
        if (intelligence.CurrentPrice <= 0) return null;

        // Use ATR-based or percentage-based stop loss
        var stopPercent = intelligence.OverallSentiment > 0 ? 0.05m : 0.03m; // Tighter stops in bearish conditions
        return intelligence.CurrentPrice * (1 - stopPercent);
    }

    private decimal? CalculateTakeProfit(SymbolIntelligence intelligence)
    {
        if (intelligence.CurrentPrice <= 0) return null;

        // Risk:Reward of at least 2:1
        var profitPercent = intelligence.OverallSentiment > 0.3m ? 0.15m : 0.10m;
        return intelligence.CurrentPrice * (1 + profitPercent);
    }

    private TimeHorizon DetermineTimeHorizon(SymbolIntelligence intelligence)
    {
        // High social activity = short term
        if (intelligence.SocialMentionVelocity > 20)
            return TimeHorizon.ShortTerm;

        // Recent 10-K/10-Q = medium term
        if (intelligence.RecentFilings.Any(f => f.FormType is "10-K" or "10-Q"))
            return TimeHorizon.MediumTerm;

        return TimeHorizon.MediumTerm;
    }

    private ParsedIntent ParseIntent(string question)
    {
        var intent = new ParsedIntent { OriginalQuery = question };

        // Extract symbol (look for $SYMBOL or just SYMBOL in caps)
        var symbolMatch = System.Text.RegularExpressions.Regex.Match(
            question, @"\$?([A-Z]{1,5})\b",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));
        if (symbolMatch.Success)
        {
            intent.Symbol = symbolMatch.Groups[1].Value;
        }

        // Determine intent type
        if (question.Contains("why") && (question.Contains("up") || question.Contains("down") || question.Contains("moving")))
        {
            intent.Type = QueryIntent.ExplainMovement;
        }
        else if (question.Contains("should") && question.Contains("buy"))
        {
            intent.Type = QueryIntent.ShouldBuy;
        }
        else if (question.Contains("should") && question.Contains("sell"))
        {
            intent.Type = QueryIntent.ShouldSell;
        }
        else if (question.Contains("show") || question.Contains("find") || question.Contains("list"))
        {
            intent.Type = QueryIntent.FindStocks;
            intent.IsBullish = question.Contains("bullish") || question.Contains("buy");
            intent.IsBearish = question.Contains("bearish") || question.Contains("sell");
        }
        else if (question.Contains("sentiment") || question.Contains("feeling"))
        {
            intent.Type = QueryIntent.GetSentiment;
        }
        else if (question.Contains("news") || question.Contains("headline"))
        {
            intent.Type = QueryIntent.WhatNews;
        }
        else if (question.Contains("risk") || question.Contains("safe"))
        {
            intent.Type = QueryIntent.RiskAssessment;
        }
        else
        {
            intent.Type = QueryIntent.Unknown;
        }

        return intent;
    }

    private async Task<AIResponse> HandleExplainMovementAsync(ParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(intent.Symbol))
        {
            return new AIResponse
            {
                Query = intent.OriginalQuery,
                Answer = "Please specify a stock symbol. Example: \"Why is AAPL down?\"",
                Confidence = 1.0m,
                GeneratedAt = DateTime.UtcNow
            };
        }

        var explanation = await ExplainMovementAsync(intent.Symbol, ct).ConfigureAwait(false);
        var intelligence = await GetSymbolIntelligenceAsync(intent.Symbol, ct).ConfigureAwait(false);

        return new AIResponse
        {
            Query = intent.OriginalQuery,
            Answer = explanation,
            Symbol = intent.Symbol,
            SentimentScore = intelligence.OverallSentiment,
            Confidence = 0.75m,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<AIResponse> HandleShouldBuyAsync(ParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(intent.Symbol))
        {
            return new AIResponse
            {
                Query = intent.OriginalQuery,
                Answer = "Please specify a stock symbol. Example: \"Should I buy TSLA?\"",
                Confidence = 1.0m,
                GeneratedAt = DateTime.UtcNow
            };
        }

        var recommendation = await GetRecommendationAsync(intent.Symbol, ct).ConfigureAwait(false);

        var answer = recommendation.Action switch
        {
            RecommendedAction.StrongBuy => $"Yes, {intent.Symbol} shows strong bullish signals. {recommendation.Reasoning}",
            RecommendedAction.Buy => $"Leaning towards yes for {intent.Symbol}. {recommendation.Reasoning}",
            RecommendedAction.Hold => $"Neutral on {intent.Symbol} right now. {recommendation.Reasoning}",
            RecommendedAction.Sell => $"I would wait on {intent.Symbol}. {recommendation.Reasoning}",
            RecommendedAction.StrongSell => $"No, {intent.Symbol} shows bearish signals. {recommendation.Reasoning}",
            _ => $"Insufficient data to recommend {intent.Symbol}."
        };

        return new AIResponse
        {
            Query = intent.OriginalQuery,
            Answer = answer,
            Symbol = intent.Symbol,
            Recommendation = recommendation,
            SentimentScore = recommendation.SentimentScore,
            Confidence = recommendation.Confidence,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<AIResponse> HandleShouldSellAsync(ParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(intent.Symbol))
        {
            return new AIResponse
            {
                Query = intent.OriginalQuery,
                Answer = "Please specify a stock symbol. Example: \"Should I sell AAPL?\"",
                Confidence = 1.0m,
                GeneratedAt = DateTime.UtcNow
            };
        }

        var recommendation = await GetRecommendationAsync(intent.Symbol, ct).ConfigureAwait(false);

        var answer = recommendation.Action switch
        {
            RecommendedAction.StrongSell => $"Yes, consider selling {intent.Symbol}. {recommendation.Reasoning}",
            RecommendedAction.Sell => $"You might want to reduce your {intent.Symbol} position. {recommendation.Reasoning}",
            RecommendedAction.Hold => $"No strong reason to sell {intent.Symbol} now. {recommendation.Reasoning}",
            RecommendedAction.Buy => $"I wouldn't sell {intent.Symbol} yet. {recommendation.Reasoning}",
            RecommendedAction.StrongBuy => $"No, {intent.Symbol} shows bullish signals. Consider holding. {recommendation.Reasoning}",
            _ => $"Insufficient data to recommend selling {intent.Symbol}."
        };

        return new AIResponse
        {
            Query = intent.OriginalQuery,
            Answer = answer,
            Symbol = intent.Symbol,
            Recommendation = recommendation,
            SentimentScore = recommendation.SentimentScore,
            Confidence = recommendation.Confidence,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<AIResponse> HandleFindStocksAsync(ParsedIntent intent, CancellationToken ct)
    {
        var criteria = new WatchlistCriteria();

        if (intent.IsBullish)
        {
            criteria.MinSentiment = 0.3m;
        }
        else if (intent.IsBearish)
        {
            criteria.MaxSentiment = -0.3m;
        }

        var suggestions = await GetWatchlistSuggestionsAsync(criteria, 5, ct).ConfigureAwait(false);

        if (suggestions.Count == 0)
        {
            return new AIResponse
            {
                Query = intent.OriginalQuery,
                Answer = "No stocks matching your criteria found right now. Try again later.",
                Confidence = 0.8m,
                GeneratedAt = DateTime.UtcNow
            };
        }

        var stockList = string.Join("\n", suggestions.Select(s =>
            $"- {s.Symbol}: {s.Reason} (sentiment: {s.SentimentScore:+0.00;-0.00})"));

        return new AIResponse
        {
            Query = intent.OriginalQuery,
            Answer = $"Here are some {(intent.IsBullish ? "bullish" : intent.IsBearish ? "bearish" : "trending")} stocks:\n{stockList}",
            WatchlistSuggestions = suggestions,
            Confidence = 0.7m,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<AIResponse> HandleGetSentimentAsync(ParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(intent.Symbol))
        {
            return new AIResponse
            {
                Query = intent.OriginalQuery,
                Answer = "Please specify a stock symbol. Example: \"What's the sentiment on MSFT?\"",
                Confidence = 1.0m,
                GeneratedAt = DateTime.UtcNow
            };
        }

        var intelligence = await GetSymbolIntelligenceAsync(intent.Symbol, ct).ConfigureAwait(false);

        var sentimentDesc = intelligence.OverallSentiment switch
        {
            > 0.5m => "very bullish",
            > 0.2m => "moderately bullish",
            > -0.2m => "neutral",
            > -0.5m => "moderately bearish",
            _ => "very bearish"
        };

        var answer = $"{intent.Symbol} sentiment is {sentimentDesc} ({intelligence.OverallSentiment:+0.00;-0.00}).\n\n" +
                     $"News sentiment: {intelligence.NewsSentiment:+0.00;-0.00}\n" +
                     $"Social sentiment: {intelligence.SocialSentiment:+0.00;-0.00}\n" +
                     $"Social mentions (24h): {intelligence.SocialMentions:N0}\n" +
                     $"Bullish/Bearish ratio: {intelligence.BullishCount}/{intelligence.BearishCount}";

        return new AIResponse
        {
            Query = intent.OriginalQuery,
            Answer = answer,
            Symbol = intent.Symbol,
            SentimentScore = intelligence.OverallSentiment,
            Confidence = 0.8m,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<AIResponse> HandleWhatNewsAsync(ParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(intent.Symbol))
        {
            return new AIResponse
            {
                Query = intent.OriginalQuery,
                Answer = "Please specify a stock symbol. Example: \"Any news on NVDA?\"",
                Confidence = 1.0m,
                GeneratedAt = DateTime.UtcNow
            };
        }

        var intelligence = await GetSymbolIntelligenceAsync(intent.Symbol, ct).ConfigureAwait(false);

        if (intelligence.RecentNews.Count == 0)
        {
            return new AIResponse
            {
                Query = intent.OriginalQuery,
                Answer = $"No recent news found for {intent.Symbol}.",
                Symbol = intent.Symbol,
                Confidence = 0.9m,
                GeneratedAt = DateTime.UtcNow
            };
        }

        var newsItems = intelligence.RecentNews.Take(5).Select(n =>
            $"- {n.Title} ({n.Source}, {n.PublishedAt:MMM dd})");

        return new AIResponse
        {
            Query = intent.OriginalQuery,
            Answer = $"Recent news for {intent.Symbol}:\n{string.Join("\n", newsItems)}",
            Symbol = intent.Symbol,
            SentimentScore = intelligence.NewsSentiment,
            Confidence = 0.85m,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<AIResponse> HandleRiskAssessmentAsync(ParsedIntent intent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(intent.Symbol))
        {
            return new AIResponse
            {
                Query = intent.OriginalQuery,
                Answer = "Please specify a stock symbol. Example: \"Is TSLA risky?\"",
                Confidence = 1.0m,
                GeneratedAt = DateTime.UtcNow
            };
        }

        var risk = await AssessTradeRiskAsync(intent.Symbol, TradeDirection.Buy, 1, ct).ConfigureAwait(false);

        var answer = $"Risk assessment for {intent.Symbol}:\n\n" +
                     $"Risk Level: {risk.RiskLevel}\n" +
                     $"Risk Score: {risk.RiskScore:P0}\n";

        if (risk.Warnings.Count > 0)
        {
            answer += "\nWarnings:\n" + string.Join("\n", risk.Warnings.Select(w => $"- {w}"));
        }

        answer += $"\n\nRecommendation: {(risk.ShouldProceed ? "Acceptable risk for trading" : "Consider waiting for better conditions")}";

        return new AIResponse
        {
            Query = intent.OriginalQuery,
            Answer = answer,
            Symbol = intent.Symbol,
            Confidence = 0.75m,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static string GenerateRiskReasoning(decimal riskScore, IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
            return "No significant risk factors detected.";

        return $"Found {warnings.Count} risk factor(s) contributing to a {riskScore:P0} overall risk score.";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _analysisLock.Dispose();
            _disposed = true;
        }
    }
}

#region Models

/// <summary>
/// AI assistant configuration options.
/// </summary>
public sealed class AIAssistantOptions
{
    /// <summary>Gets or sets the cache expiration for symbol intelligence.</summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the bullish sentiment threshold.</summary>
    public decimal BullishThreshold { get; set; } = 0.3m;

    /// <summary>Gets or sets the bearish sentiment threshold.</summary>
    public decimal BearishThreshold { get; set; } = -0.3m;

    /// <summary>Gets or sets the maximum acceptable risk score (0-1).</summary>
    public decimal MaxAcceptableRiskScore { get; set; } = 0.7m;

    /// <summary>Gets or sets sentiment weights for different data sources.</summary>
    public SentimentWeights SentimentWeights { get; set; } = new();
}

/// <summary>
/// Weights for combining sentiment from different sources.
/// </summary>
public sealed class SentimentWeights
{
    public decimal NewsWeight { get; set; } = 0.4m;
    public decimal SocialWeight { get; set; } = 0.35m;
    public decimal InsiderWeight { get; set; } = 0.25m;
}

/// <summary>
/// Aggregated intelligence about a symbol from all data sources.
/// </summary>
public sealed class SymbolIntelligence
{
    public string Symbol { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal NewsSentiment { get; set; }
    public decimal SocialSentiment { get; set; }
    public decimal OverallSentiment { get; set; }
    public int SocialMentions { get; set; }
    public decimal SocialMentionVelocity { get; set; }
    public int BullishCount { get; set; }
    public int BearishCount { get; set; }
    public decimal InsiderBuyValue { get; set; }
    public decimal InsiderSellValue { get; set; }
    public bool HasUpcomingEarnings { get; set; }
    public IReadOnlyList<NewsInsight> RecentNews { get; set; } = Array.Empty<NewsInsight>();
    public IReadOnlyList<FilingInsight> RecentFilings { get; set; } = Array.Empty<FilingInsight>();
    public DateTime AnalyzedAt { get; set; }
}

/// <summary>
/// Insight extracted from a news article.
/// </summary>
public sealed class NewsInsight
{
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public decimal SentimentScore { get; set; }
}

/// <summary>
/// Insight extracted from an SEC filing.
/// </summary>
public sealed class FilingInsight
{
    public string FormType { get; set; } = string.Empty;
    public DateTime FiledAt { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// AI-generated trade recommendation.
/// </summary>
public sealed class TradeRecommendation
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public RecommendedAction Action { get; set; }
    public decimal Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public decimal SentimentScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public IReadOnlyList<string> KeyFactors { get; set; } = Array.Empty<string>();
    public decimal? SuggestedEntryPrice { get; set; }
    public decimal? SuggestedStopLoss { get; set; }
    public decimal? SuggestedTakeProfit { get; set; }
    public TimeHorizon TimeHorizon { get; set; }
}

/// <summary>
/// Recommended action from AI analysis.
/// </summary>
public enum RecommendedAction
{
    StrongBuy,
    Buy,
    Hold,
    Sell,
    StrongSell
}

/// <summary>
/// Time horizon for a trade recommendation.
/// </summary>
public enum TimeHorizon
{
    Intraday,
    ShortTerm,    // Days
    MediumTerm,   // Weeks
    LongTerm      // Months
}

/// <summary>
/// Response from the AI assistant.
/// </summary>
public sealed class AIResponse
{
    public string Query { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public decimal? SentimentScore { get; set; }
    public decimal Confidence { get; set; }
    public DateTime GeneratedAt { get; set; }
    public TradeRecommendation? Recommendation { get; set; }
    public IReadOnlyList<WatchlistSuggestion>? WatchlistSuggestions { get; set; }
}

/// <summary>
/// Trade direction for risk assessment.
/// </summary>
public enum TradeDirection
{
    Buy,
    Sell
}

/// <summary>
/// Risk level classification.
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    VeryHigh
}

/// <summary>
/// Risk assessment result.
/// </summary>
public sealed class RiskAssessment
{
    public string Symbol { get; set; } = string.Empty;
    public TradeDirection Direction { get; set; }
    public decimal Quantity { get; set; }
    public decimal RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public bool ShouldProceed { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; }
}

/// <summary>
/// Criteria for filtering watchlist suggestions.
/// </summary>
public sealed class WatchlistCriteria
{
    public decimal? MinSentiment { get; set; }
    public decimal? MaxSentiment { get; set; }
    public int? MinMentions { get; set; }
    public IReadOnlyList<string>? Sectors { get; set; }
}

/// <summary>
/// AI-suggested watchlist item.
/// </summary>
public sealed class WatchlistSuggestion
{
    public string Symbol { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal SentimentScore { get; set; }
    public int MentionCount { get; set; }
    public int TrendingRank { get; set; }
    public DateTime SuggestedAt { get; set; }
}

/// <summary>
/// Parsed intent from natural language query.
/// </summary>
internal sealed class ParsedIntent
{
    public string OriginalQuery { get; set; } = string.Empty;
    public QueryIntent Type { get; set; }
    public string? Symbol { get; set; }
    public bool IsBullish { get; set; }
    public bool IsBearish { get; set; }
}

/// <summary>
/// Types of user queries.
/// </summary>
internal enum QueryIntent
{
    Unknown,
    ExplainMovement,
    ShouldBuy,
    ShouldSell,
    FindStocks,
    GetSentiment,
    WhatNews,
    RiskAssessment
}

#endregion
