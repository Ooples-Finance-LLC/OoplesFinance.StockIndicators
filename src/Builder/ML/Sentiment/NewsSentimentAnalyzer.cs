namespace OoplesFinance.StockIndicators.Builder.ML.Sentiment;

/// <summary>
/// Analyzes sentiment from financial news articles.
/// Uses lexicon-based and machine learning approaches for sentiment scoring.
/// </summary>
public sealed class NewsSentimentAnalyzer
{
    private readonly NewsSentimentOptions _options;
    private readonly Dictionary<string, double> _positiveLexicon;
    private readonly Dictionary<string, double> _negativeLexicon;
    private readonly Dictionary<string, double> _financialLexicon;
    private readonly HashSet<string> _negationWords;
    private readonly HashSet<string> _intensifiers;

    /// <summary>
    /// Initializes a new instance of the NewsSentimentAnalyzer.
    /// </summary>
    public NewsSentimentAnalyzer(NewsSentimentOptions? options = null)
    {
        _options = options ?? new NewsSentimentOptions();
        _positiveLexicon = BuildPositiveLexicon();
        _negativeLexicon = BuildNegativeLexicon();
        _financialLexicon = BuildFinancialLexicon();
        _negationWords = BuildNegationWords();
        _intensifiers = BuildIntensifiers();
    }

    /// <summary>
    /// Analyzes sentiment from a collection of news articles.
    /// </summary>
    public NewsSentimentResult Analyze(IReadOnlyList<NewsArticle> articles)
    {
        if (articles.Count == 0)
        {
            return new NewsSentimentResult
            {
                OverallSentiment = SentimentScore.Neutral,
                AggregateScore = 0,
                Confidence = 0,
                ArticleResults = []
            };
        }

        var articleResults = new List<ArticleSentimentResult>();
        double totalWeightedScore = 0;
        double totalWeight = 0;

        foreach (var article in articles)
        {
            var result = AnalyzeArticle(article);
            articleResults.Add(result);

            // Weight by recency and source credibility
            var recencyWeight = CalculateRecencyWeight(article.PublishedAt);
            var sourceWeight = GetSourceCredibilityWeight(article.Source);
            var combinedWeight = recencyWeight * sourceWeight;

            totalWeightedScore += result.Score * combinedWeight;
            totalWeight += combinedWeight;
        }

        var aggregateScore = totalWeight > 0 ? totalWeightedScore / totalWeight : 0;
        var overallSentiment = ClassifySentiment(aggregateScore);
        var confidence = CalculateConfidence(articleResults);

        return new NewsSentimentResult
        {
            OverallSentiment = overallSentiment,
            AggregateScore = aggregateScore,
            Confidence = confidence,
            ArticleResults = articleResults,
            ArticleCount = articles.Count,
            PositiveCount = articleResults.Count(r => r.Sentiment == SentimentScore.Positive || r.Sentiment == SentimentScore.VeryPositive),
            NegativeCount = articleResults.Count(r => r.Sentiment == SentimentScore.Negative || r.Sentiment == SentimentScore.VeryNegative),
            NeutralCount = articleResults.Count(r => r.Sentiment == SentimentScore.Neutral),
            TopPositiveKeywords = ExtractTopKeywords(articleResults, true),
            TopNegativeKeywords = ExtractTopKeywords(articleResults, false)
        };
    }

    /// <summary>
    /// Analyzes a single news article.
    /// </summary>
    public ArticleSentimentResult AnalyzeArticle(NewsArticle article)
    {
        var titleScore = AnalyzeText(article.Title, isTitle: true);
        var contentScore = AnalyzeText(article.Content, isTitle: false);

        // Title has more weight (typically more carefully chosen)
        var combinedScore = titleScore * _options.TitleWeight + contentScore * (1 - _options.TitleWeight);

        var keyPhrases = ExtractKeyPhrases(article.Content);
        var entities = ExtractEntities(article.Content);
        var sentiment = ClassifySentiment(combinedScore);

        return new ArticleSentimentResult
        {
            ArticleId = article.Id,
            Title = article.Title,
            Source = article.Source,
            PublishedAt = article.PublishedAt,
            Score = combinedScore,
            TitleScore = titleScore,
            ContentScore = contentScore,
            Sentiment = sentiment,
            KeyPhrases = keyPhrases,
            Entities = entities,
            Confidence = CalculateArticleConfidence(article)
        };
    }

    /// <summary>
    /// Analyzes sentiment trend over time.
    /// </summary>
    public SentimentTrend AnalyzeTrend(IReadOnlyList<NewsArticle> articles, TimeSpan windowSize)
    {
        if (articles.Count == 0)
        {
            return new SentimentTrend { Windows = [] };
        }

        var sortedArticles = articles.OrderBy(a => a.PublishedAt).ToList();
        var windows = new List<SentimentWindow>();

        var startTime = sortedArticles[0].PublishedAt;
        var endTime = sortedArticles[sortedArticles.Count - 1].PublishedAt;

        var currentWindowStart = startTime;
        while (currentWindowStart < endTime)
        {
            var currentWindowEnd = currentWindowStart + windowSize;
            var windowArticles = sortedArticles
                .Where(a => a.PublishedAt >= currentWindowStart && a.PublishedAt < currentWindowEnd)
                .ToList();

            if (windowArticles.Count > 0)
            {
                var windowResult = Analyze(windowArticles);
                windows.Add(new SentimentWindow
                {
                    StartTime = currentWindowStart,
                    EndTime = currentWindowEnd,
                    Score = windowResult.AggregateScore,
                    ArticleCount = windowArticles.Count,
                    Sentiment = windowResult.OverallSentiment
                });
            }

            currentWindowStart = currentWindowEnd;
        }

        // Calculate trend direction
        var trendDirection = TrendDirection.Sideways;
        if (windows.Count >= 2)
        {
            var recentScore = windows[windows.Count - 1].Score;
            var previousScore = windows[windows.Count - 2].Score;
            var scoreDelta = recentScore - previousScore;

            if (scoreDelta > _options.TrendThreshold)
                trendDirection = TrendDirection.Improving;
            else if (scoreDelta < -_options.TrendThreshold)
                trendDirection = TrendDirection.Deteriorating;
        }

        // Calculate momentum
        var momentum = 0.0;
        if (windows.Count >= 3)
        {
            var recentAvg = windows.Skip(windows.Count - 3).Average(w => w.Score);
            var overallAvg = windows.Average(w => w.Score);
            momentum = recentAvg - overallAvg;
        }

        return new SentimentTrend
        {
            Windows = windows,
            TrendDirection = trendDirection,
            Momentum = momentum,
            Volatility = CalculateSentimentVolatility(windows)
        };
    }

    /// <summary>
    /// Detects significant sentiment shifts.
    /// </summary>
    public IReadOnlyList<SentimentShift> DetectShifts(IReadOnlyList<NewsArticle> articles)
    {
        var trend = AnalyzeTrend(articles, _options.ShiftDetectionWindow);
        var shifts = new List<SentimentShift>();

        for (int i = 1; i < trend.Windows.Count; i++)
        {
            var current = trend.Windows[i];
            var previous = trend.Windows[i - 1];
            var scoreDelta = current.Score - previous.Score;

            if (Math.Abs(scoreDelta) >= _options.SignificantShiftThreshold)
            {
                var direction = scoreDelta > 0 ? ShiftDirection.PositiveShift : ShiftDirection.NegativeShift;
                var magnitude = Math.Abs(scoreDelta);

                shifts.Add(new SentimentShift
                {
                    Timestamp = current.StartTime,
                    Direction = direction,
                    Magnitude = magnitude,
                    PreviousScore = previous.Score,
                    NewScore = current.Score,
                    ArticleCount = current.ArticleCount
                });
            }
        }

        return shifts;
    }

    private double AnalyzeText(string text, bool isTitle)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var words = TokenizeText(text);
        var score = 0.0;
        var wordCount = 0;
        var negationActive = false;
        var intensifierMultiplier = 1.0;

        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i].ToLowerInvariant();

            // Check for negation
            if (_negationWords.Contains(word))
            {
                negationActive = true;
                continue;
            }

            // Check for intensifiers
            if (_intensifiers.Contains(word))
            {
                intensifierMultiplier = _options.IntensifierMultiplier;
                continue;
            }

            // Check financial lexicon first (domain-specific)
            if (_financialLexicon.TryGetValue(word, out var financialScore))
            {
                var adjustedScore = financialScore * intensifierMultiplier;
                if (negationActive)
                    adjustedScore = -adjustedScore;

                score += adjustedScore * _options.FinancialLexiconWeight;
                wordCount++;
            }
            // Then check general positive/negative lexicons
            else if (_positiveLexicon.TryGetValue(word, out var positiveScore))
            {
                var adjustedScore = positiveScore * intensifierMultiplier;
                if (negationActive)
                    adjustedScore = -adjustedScore;

                score += adjustedScore;
                wordCount++;
            }
            else if (_negativeLexicon.TryGetValue(word, out var negativeScore))
            {
                var adjustedScore = negativeScore * intensifierMultiplier;
                if (negationActive)
                    adjustedScore = -adjustedScore;

                score += adjustedScore;
                wordCount++;
            }

            // Reset modifiers after applying
            negationActive = false;
            intensifierMultiplier = 1.0;
        }

        return wordCount > 0 ? score / wordCount : 0;
    }

    private List<string> TokenizeText(string text)
    {
        // Simple tokenization - split on whitespace and punctuation
        var tokens = new List<string>();
        var currentToken = new System.Text.StringBuilder();

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '\'')
            {
                currentToken.Append(c);
            }
            else
            {
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens;
    }

    private List<string> ExtractKeyPhrases(string text)
    {
        var words = TokenizeText(text);
        var phrases = new List<string>();

        // Extract bigrams containing sentiment words
        for (int i = 0; i < words.Count - 1; i++)
        {
            var word1 = words[i].ToLowerInvariant();
            var word2 = words[i + 1].ToLowerInvariant();

            var hasSentiment = _financialLexicon.ContainsKey(word1) ||
                              _financialLexicon.ContainsKey(word2) ||
                              _positiveLexicon.ContainsKey(word1) ||
                              _positiveLexicon.ContainsKey(word2) ||
                              _negativeLexicon.ContainsKey(word1) ||
                              _negativeLexicon.ContainsKey(word2);

            if (hasSentiment)
            {
                phrases.Add($"{word1} {word2}");
            }
        }

        return phrases.Distinct().Take(10).ToList();
    }

    private List<string> ExtractEntities(string text)
    {
        // Simple entity extraction - words starting with capital letters
        var entities = new List<string>();
        var words = text.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i].Trim([',', '.', '!', '?', ':', ';', '"', '\'']);
            if (word.Length > 1 && char.IsUpper(word[0]) && !IsCommonWord(word))
            {
                entities.Add(word);
            }
        }

        return entities.GroupBy(e => e)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(10)
            .ToList();
    }

    private static bool IsCommonWord(string word)
    {
        var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "The", "A", "An", "And", "Or", "But", "In", "On", "At", "To", "For",
            "Of", "With", "By", "From", "Is", "Are", "Was", "Were", "It", "This",
            "That", "These", "Those", "Has", "Have", "Had", "Will", "Would", "Could",
            "Should", "May", "Might", "Must", "Can"
        };
        return common.Contains(word);
    }

    private double CalculateRecencyWeight(DateTime publishedAt)
    {
        var age = DateTime.UtcNow - publishedAt;
        var halfLife = _options.RecencyHalfLife;

        // Exponential decay
        return Math.Exp(-age.TotalHours / halfLife.TotalHours);
    }

    private double GetSourceCredibilityWeight(string source)
    {
        if (_options.SourceCredibility.TryGetValue(source, out var credibility))
        {
            return credibility;
        }

        return _options.DefaultSourceCredibility;
    }

    private static SentimentScore ClassifySentiment(double score)
    {
        return score switch
        {
            > 0.6 => SentimentScore.VeryPositive,
            > 0.2 => SentimentScore.Positive,
            < -0.6 => SentimentScore.VeryNegative,
            < -0.2 => SentimentScore.Negative,
            _ => SentimentScore.Neutral
        };
    }

    private static double CalculateConfidence(List<ArticleSentimentResult> results)
    {
        if (results.Count < 2)
            return 0.5;

        var scores = results.Select(r => r.Score).ToList();
        var mean = scores.Average();
        var variance = scores.Sum(s => Math.Pow(s - mean, 2)) / scores.Count;
        var stdDev = Math.Sqrt(variance);

        // Lower variance = higher confidence
        var confidence = 1.0 / (1.0 + stdDev);

        // Boost confidence with more articles
        var countBoost = Math.Min(1.0, results.Count / 20.0);

        return Math.Min(1.0, confidence * 0.7 + countBoost * 0.3);
    }

    private double CalculateArticleConfidence(NewsArticle article)
    {
        var confidence = 0.5;

        // Longer content = more confidence
        var contentLength = article.Content?.Length ?? 0;
        confidence += Math.Min(0.2, contentLength / 5000.0);

        // Known source = more confidence
        if (_options.SourceCredibility.ContainsKey(article.Source))
        {
            confidence += 0.2;
        }

        // Recent = more confidence
        var age = DateTime.UtcNow - article.PublishedAt;
        if (age.TotalHours < 24)
        {
            confidence += 0.1;
        }

        return Math.Min(1.0, confidence);
    }

    private static double CalculateSentimentVolatility(List<SentimentWindow> windows)
    {
        if (windows.Count < 2)
            return 0;

        var scores = windows.Select(w => w.Score).ToList();
        var mean = scores.Average();
        var variance = scores.Sum(s => Math.Pow(s - mean, 2)) / scores.Count;

        return Math.Sqrt(variance);
    }

    private static List<string> ExtractTopKeywords(List<ArticleSentimentResult> results, bool positive)
    {
        var allKeywords = results
            .SelectMany(r => r.KeyPhrases)
            .GroupBy(k => k)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        return allKeywords;
    }

    private Dictionary<string, double> BuildPositiveLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "good", 0.5 }, { "great", 0.8 }, { "excellent", 0.9 }, { "positive", 0.6 },
            { "gain", 0.6 }, { "gains", 0.6 }, { "rise", 0.5 }, { "rising", 0.5 },
            { "increase", 0.5 }, { "increased", 0.5 }, { "growth", 0.6 }, { "growing", 0.6 },
            { "profit", 0.7 }, { "profitable", 0.7 }, { "profits", 0.7 },
            { "success", 0.7 }, { "successful", 0.7 }, { "beat", 0.6 }, { "beats", 0.6 },
            { "exceed", 0.6 }, { "exceeds", 0.6 }, { "exceeded", 0.6 },
            { "strong", 0.6 }, { "stronger", 0.7 }, { "strongest", 0.8 },
            { "high", 0.4 }, { "higher", 0.5 }, { "highest", 0.6 },
            { "up", 0.3 }, { "upgrade", 0.6 }, { "upgraded", 0.6 },
            { "outperform", 0.7 }, { "outperforms", 0.7 }, { "outperformed", 0.7 },
            { "buy", 0.5 }, { "bullish", 0.7 }, { "optimistic", 0.6 },
            { "recover", 0.5 }, { "recovery", 0.5 }, { "recovered", 0.5 },
            { "improve", 0.5 }, { "improved", 0.5 }, { "improvement", 0.5 },
            { "opportunity", 0.5 }, { "opportunities", 0.5 },
            { "confident", 0.6 }, { "confidence", 0.6 }
        };
    }

    private Dictionary<string, double> BuildNegativeLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "bad", -0.5 }, { "worse", -0.7 }, { "worst", -0.9 }, { "negative", -0.6 },
            { "loss", -0.6 }, { "losses", -0.6 }, { "lose", -0.5 }, { "losing", -0.5 },
            { "fall", -0.5 }, { "falling", -0.5 }, { "fell", -0.5 }, { "fallen", -0.5 },
            { "decline", -0.5 }, { "declined", -0.5 }, { "declining", -0.5 },
            { "decrease", -0.5 }, { "decreased", -0.5 }, { "decreasing", -0.5 },
            { "drop", -0.5 }, { "dropped", -0.5 }, { "dropping", -0.5 },
            { "weak", -0.5 }, { "weaker", -0.6 }, { "weakest", -0.7 },
            { "low", -0.4 }, { "lower", -0.5 }, { "lowest", -0.6 },
            { "down", -0.3 }, { "downgrade", -0.6 }, { "downgraded", -0.6 },
            { "underperform", -0.7 }, { "underperforms", -0.7 }, { "underperformed", -0.7 },
            { "sell", -0.5 }, { "bearish", -0.7 }, { "pessimistic", -0.6 },
            { "fail", -0.6 }, { "failed", -0.6 }, { "failure", -0.6 },
            { "miss", -0.5 }, { "missed", -0.5 }, { "missing", -0.5 },
            { "risk", -0.4 }, { "risky", -0.5 }, { "risks", -0.4 },
            { "concern", -0.4 }, { "concerns", -0.4 }, { "concerned", -0.4 },
            { "worry", -0.5 }, { "worried", -0.5 }, { "worries", -0.5 },
            { "fear", -0.6 }, { "fears", -0.6 }, { "fearful", -0.6 },
            { "crisis", -0.8 }, { "crash", -0.8 }, { "collapse", -0.8 },
            { "bankruptcy", -0.9 }, { "bankrupt", -0.9 },
            { "recession", -0.7 }, { "slowdown", -0.5 }
        };
    }

    private Dictionary<string, double> BuildFinancialLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            // Positive financial terms
            { "dividend", 0.4 }, { "dividends", 0.4 },
            { "earnings", 0.3 }, { "revenue", 0.3 }, { "revenues", 0.3 },
            { "acquisition", 0.3 }, { "merger", 0.2 },
            { "expansion", 0.5 }, { "expand", 0.5 }, { "expanding", 0.5 },
            { "innovation", 0.5 }, { "innovative", 0.5 },
            { "breakthrough", 0.7 }, { "milestone", 0.5 },
            { "partnership", 0.4 }, { "collaboration", 0.4 },
            { "approval", 0.5 }, { "approved", 0.5 },
            { "launch", 0.4 }, { "launched", 0.4 }, { "launching", 0.4 },

            // Negative financial terms
            { "debt", -0.3 }, { "debts", -0.3 },
            { "lawsuit", -0.5 }, { "litigation", -0.5 }, { "sued", -0.5 },
            { "investigation", -0.4 }, { "investigated", -0.4 },
            { "fraud", -0.8 }, { "fraudulent", -0.8 },
            { "default", -0.7 }, { "defaulted", -0.7 },
            { "layoff", -0.5 }, { "layoffs", -0.5 },
            { "restructuring", -0.3 }, { "restructure", -0.3 },
            { "warning", -0.4 }, { "warned", -0.4 },
            { "recall", -0.5 }, { "recalled", -0.5 },
            { "penalty", -0.5 }, { "fine", -0.4 }, { "fined", -0.4 },
            { "violation", -0.5 }, { "violations", -0.5 },
            { "scandal", -0.7 }, { "controversy", -0.5 }
        };
    }

    private HashSet<string> BuildNegationWords()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "not", "no", "never", "neither", "nobody", "nothing", "nowhere",
            "hardly", "barely", "scarcely", "seldom", "rarely",
            "don't", "doesn't", "didn't", "won't", "wouldn't", "couldn't",
            "shouldn't", "can't", "cannot", "isn't", "aren't", "wasn't", "weren't"
        };
    }

    private HashSet<string> BuildIntensifiers()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "very", "extremely", "highly", "significantly", "substantially",
            "considerably", "remarkably", "exceptionally", "particularly",
            "absolutely", "completely", "totally", "utterly", "entirely"
        };
    }
}

/// <summary>
/// Options for news sentiment analysis.
/// </summary>
public sealed class NewsSentimentOptions
{
    /// <summary>Weight applied to article titles vs content (0-1).</summary>
    public double TitleWeight { get; set; } = 0.4;

    /// <summary>Weight multiplier for financial lexicon terms.</summary>
    public double FinancialLexiconWeight { get; set; } = 1.5;

    /// <summary>Multiplier for intensifier words.</summary>
    public double IntensifierMultiplier { get; set; } = 1.5;

    /// <summary>Half-life for recency weighting.</summary>
    public TimeSpan RecencyHalfLife { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Default credibility for unknown sources.</summary>
    public double DefaultSourceCredibility { get; set; } = 0.5;

    /// <summary>Source credibility weights (0-1).</summary>
    public Dictionary<string, double> SourceCredibility { get; set; } = new()
    {
        { "Reuters", 0.95 },
        { "Bloomberg", 0.95 },
        { "WSJ", 0.9 },
        { "Financial Times", 0.9 },
        { "CNBC", 0.8 },
        { "Yahoo Finance", 0.7 },
        { "MarketWatch", 0.75 },
        { "Seeking Alpha", 0.6 },
        { "Motley Fool", 0.6 }
    };

    /// <summary>Time window for shift detection.</summary>
    public TimeSpan ShiftDetectionWindow { get; set; } = TimeSpan.FromHours(4);

    /// <summary>Threshold for significant sentiment shift.</summary>
    public double SignificantShiftThreshold { get; set; } = 0.3;

    /// <summary>Threshold for trend detection.</summary>
    public double TrendThreshold { get; set; } = 0.15;
}

/// <summary>
/// Represents a news article.
/// </summary>
public sealed class NewsArticle
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string Url { get; set; } = string.Empty;
    public IReadOnlyList<string> Symbols { get; set; } = [];
}

/// <summary>
/// Result of news sentiment analysis.
/// </summary>
public sealed class NewsSentimentResult
{
    public SentimentScore OverallSentiment { get; set; }
    public double AggregateScore { get; set; }
    public double Confidence { get; set; }
    public int ArticleCount { get; set; }
    public int PositiveCount { get; set; }
    public int NegativeCount { get; set; }
    public int NeutralCount { get; set; }
    public IReadOnlyList<ArticleSentimentResult> ArticleResults { get; set; } = [];
    public IReadOnlyList<string> TopPositiveKeywords { get; set; } = [];
    public IReadOnlyList<string> TopNegativeKeywords { get; set; } = [];
}

/// <summary>
/// Sentiment result for a single article.
/// </summary>
public sealed class ArticleSentimentResult
{
    public string ArticleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public double Score { get; set; }
    public double TitleScore { get; set; }
    public double ContentScore { get; set; }
    public SentimentScore Sentiment { get; set; }
    public double Confidence { get; set; }
    public IReadOnlyList<string> KeyPhrases { get; set; } = [];
    public IReadOnlyList<string> Entities { get; set; } = [];
}

/// <summary>
/// Sentiment trend analysis result.
/// </summary>
public sealed class SentimentTrend
{
    public IReadOnlyList<SentimentWindow> Windows { get; set; } = [];
    public TrendDirection TrendDirection { get; set; }
    public double Momentum { get; set; }
    public double Volatility { get; set; }
}

/// <summary>
/// A time window of sentiment data.
/// </summary>
public sealed class SentimentWindow
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double Score { get; set; }
    public int ArticleCount { get; set; }
    public SentimentScore Sentiment { get; set; }
}

/// <summary>
/// Represents a significant sentiment shift.
/// </summary>
public sealed class SentimentShift
{
    public DateTime Timestamp { get; set; }
    public ShiftDirection Direction { get; set; }
    public double Magnitude { get; set; }
    public double PreviousScore { get; set; }
    public double NewScore { get; set; }
    public int ArticleCount { get; set; }
}

/// <summary>
/// Sentiment classification.
/// </summary>
public enum SentimentScore
{
    VeryNegative,
    Negative,
    Neutral,
    Positive,
    VeryPositive
}

/// <summary>
/// Trend direction.
/// </summary>
public enum TrendDirection
{
    Deteriorating,
    Sideways,
    Improving
}

/// <summary>
/// Shift direction.
/// </summary>
public enum ShiftDirection
{
    NegativeShift,
    PositiveShift
}
