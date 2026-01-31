namespace OoplesFinance.StockIndicators.Builder.ML.Sentiment;

/// <summary>
/// Analyzes sentiment from social media posts (Twitter/X, StockTwits, Reddit).
/// Handles real-time streaming and batch analysis.
/// </summary>
public sealed class SocialSentimentAnalyzer
{
    private readonly SocialSentimentOptions _options;
    private readonly Dictionary<string, double> _slangLexicon;
    private readonly Dictionary<string, double> _emojiSentiment;
    private readonly HashSet<string> _cashtags;
    private readonly Dictionary<string, int> _userInfluenceCache;

    /// <summary>
    /// Initializes a new instance of the SocialSentimentAnalyzer.
    /// </summary>
    public SocialSentimentAnalyzer(SocialSentimentOptions? options = null)
    {
        _options = options ?? new SocialSentimentOptions();
        _slangLexicon = BuildSlangLexicon();
        _emojiSentiment = BuildEmojiSentiment();
        _cashtags = [];
        _userInfluenceCache = [];
    }

    /// <summary>
    /// Analyzes sentiment from a collection of social media posts.
    /// </summary>
    public SocialSentimentResult Analyze(IReadOnlyList<SocialPost> posts)
    {
        if (posts.Count == 0)
        {
            return new SocialSentimentResult
            {
                OverallSentiment = SentimentScore.Neutral,
                AggregateScore = 0,
                BullBearRatio = 1.0,
                PostResults = []
            };
        }

        var postResults = new List<PostSentimentResult>();
        double totalWeightedScore = 0;
        double totalWeight = 0;
        int bullishCount = 0;
        int bearishCount = 0;

        foreach (var post in posts)
        {
            var result = AnalyzePost(post);
            postResults.Add(result);

            // Weight by user influence, engagement, and recency
            var influenceWeight = CalculateInfluenceWeight(post);
            var engagementWeight = CalculateEngagementWeight(post);
            var recencyWeight = CalculateRecencyWeight(post.Timestamp);
            var combinedWeight = influenceWeight * engagementWeight * recencyWeight;

            totalWeightedScore += result.Score * combinedWeight;
            totalWeight += combinedWeight;

            if (result.IsBullish) bullishCount++;
            if (result.IsBearish) bearishCount++;
        }

        var aggregateScore = totalWeight > 0 ? totalWeightedScore / totalWeight : 0;
        var overallSentiment = ClassifySentiment(aggregateScore);
        var bullBearRatio = bearishCount > 0 ? (double)bullishCount / bearishCount : bullishCount > 0 ? double.PositiveInfinity : 1.0;

        // Calculate velocity (posts per hour)
        var timeSpan = posts.Max(p => p.Timestamp) - posts.Min(p => p.Timestamp);
        var postsPerHour = timeSpan.TotalHours > 0 ? posts.Count / timeSpan.TotalHours : posts.Count;

        // Calculate engagement metrics
        var totalEngagement = posts.Sum(p => p.Likes + p.Retweets + p.Comments);
        var avgEngagement = posts.Count > 0 ? (double)totalEngagement / posts.Count : 0;

        // Extract trending cashtags and hashtags
        var trendingCashtags = ExtractTrendingCashtags(posts);
        var trendingHashtags = ExtractTrendingHashtags(posts);

        return new SocialSentimentResult
        {
            OverallSentiment = overallSentiment,
            AggregateScore = aggregateScore,
            Confidence = CalculateConfidence(postResults),
            PostCount = posts.Count,
            BullishCount = bullishCount,
            BearishCount = bearishCount,
            NeutralCount = posts.Count - bullishCount - bearishCount,
            BullBearRatio = bullBearRatio,
            PostsPerHour = postsPerHour,
            TotalEngagement = totalEngagement,
            AverageEngagement = avgEngagement,
            PostResults = postResults,
            TrendingCashtags = trendingCashtags,
            TrendingHashtags = trendingHashtags
        };
    }

    /// <summary>
    /// Analyzes a single social media post.
    /// </summary>
    public PostSentimentResult AnalyzePost(SocialPost post)
    {
        var textScore = AnalyzeText(post.Content);
        var emojiScore = AnalyzeEmojis(post.Content);
        var hashtagScore = AnalyzeHashtags(post.Hashtags);

        // Combine scores
        var combinedScore = textScore * _options.TextWeight +
                           emojiScore * _options.EmojiWeight +
                           hashtagScore * _options.HashtagWeight;

        // Detect explicit bull/bear signals
        var isBullish = DetectBullishSignal(post.Content);
        var isBearish = DetectBearishSignal(post.Content);

        // Override score if explicit signal detected
        if (isBullish && !isBearish)
            combinedScore = Math.Max(combinedScore, 0.5);
        else if (isBearish && !isBullish)
            combinedScore = Math.Min(combinedScore, -0.5);

        var sentiment = ClassifySentiment(combinedScore);

        return new PostSentimentResult
        {
            PostId = post.Id,
            Platform = post.Platform,
            Username = post.Username,
            Timestamp = post.Timestamp,
            Score = combinedScore,
            TextScore = textScore,
            EmojiScore = emojiScore,
            Sentiment = sentiment,
            IsBullish = isBullish,
            IsBearish = isBearish,
            Cashtags = ExtractCashtags(post.Content),
            EngagementScore = CalculateEngagementScore(post)
        };
    }

    /// <summary>
    /// Analyzes sentiment for a specific symbol across posts.
    /// </summary>
    public SymbolSocialSentiment AnalyzeSymbol(string symbol, IReadOnlyList<SocialPost> posts)
    {
        var cashtag = symbol.StartsWith("$") ? symbol : "$" + symbol;
        var symbolPosts = posts.Where(p =>
            p.Content.Contains(cashtag, StringComparison.OrdinalIgnoreCase) ||
            p.Cashtags.Any(c => c.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (symbolPosts.Count == 0)
        {
            return new SymbolSocialSentiment
            {
                Symbol = symbol,
                PostCount = 0,
                Sentiment = SentimentScore.Neutral
            };
        }

        var result = Analyze(symbolPosts);

        // Calculate mention velocity
        var firstMention = symbolPosts.Min(p => p.Timestamp);
        var lastMention = symbolPosts.Max(p => p.Timestamp);
        var timeSpan = lastMention - firstMention;
        var mentionsPerHour = timeSpan.TotalHours > 0 ? symbolPosts.Count / timeSpan.TotalHours : symbolPosts.Count;

        // Compare to baseline
        var baselineMentions = _options.BaselineMentionsPerHour.GetValueOrDefault(symbol, 1.0);
        var mentionsSurge = mentionsPerHour / baselineMentions;

        return new SymbolSocialSentiment
        {
            Symbol = symbol,
            PostCount = symbolPosts.Count,
            Sentiment = result.OverallSentiment,
            Score = result.AggregateScore,
            BullBearRatio = result.BullBearRatio,
            MentionsPerHour = mentionsPerHour,
            MentionsSurge = mentionsSurge,
            TotalEngagement = result.TotalEngagement,
            TopInfluencers = GetTopInfluencers(symbolPosts)
        };
    }

    /// <summary>
    /// Detects unusual activity spikes for symbols.
    /// </summary>
    public IReadOnlyList<SocialActivitySpike> DetectActivitySpikes(IReadOnlyList<SocialPost> posts)
    {
        var spikes = new List<SocialActivitySpike>();
        var symbolPosts = GroupBySymbol(posts);

        foreach (var kvp in symbolPosts)
        {
            var symbol = kvp.Key;
            var symbolPostList = kvp.Value;

            if (symbolPostList.Count < _options.MinimumPostsForSpike)
                continue;

            // Calculate recent activity vs baseline
            var recentPosts = symbolPostList
                .Where(p => p.Timestamp > DateTime.UtcNow - _options.SpikeDetectionWindow)
                .ToList();

            if (recentPosts.Count == 0)
                continue;

            var recentMentionsPerHour = recentPosts.Count / _options.SpikeDetectionWindow.TotalHours;
            var baselineMentions = _options.BaselineMentionsPerHour.GetValueOrDefault(symbol, 1.0);
            var spikeRatio = recentMentionsPerHour / baselineMentions;

            if (spikeRatio >= _options.SpikeThreshold)
            {
                var sentiment = Analyze(recentPosts);

                spikes.Add(new SocialActivitySpike
                {
                    Symbol = symbol,
                    Timestamp = DateTime.UtcNow,
                    SpikeRatio = spikeRatio,
                    MentionsPerHour = recentMentionsPerHour,
                    BaselineMentionsPerHour = baselineMentions,
                    PostCount = recentPosts.Count,
                    Sentiment = sentiment.OverallSentiment,
                    SentimentScore = sentiment.AggregateScore,
                    TopKeywords = ExtractTopKeywords(recentPosts)
                });
            }
        }

        return spikes.OrderByDescending(s => s.SpikeRatio).ToList();
    }

    /// <summary>
    /// Identifies influential users discussing a symbol.
    /// </summary>
    public IReadOnlyList<InfluencerActivity> IdentifyInfluencerActivity(string symbol, IReadOnlyList<SocialPost> posts)
    {
        var cashtag = symbol.StartsWith("$") ? symbol : "$" + symbol;
        var symbolPosts = posts.Where(p =>
            p.Content.Contains(cashtag, StringComparison.OrdinalIgnoreCase) ||
            p.Cashtags.Any(c => c.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var userPosts = symbolPosts.GroupBy(p => p.Username);
        var activities = new List<InfluencerActivity>();

        foreach (var group in userPosts)
        {
            var userPostList = group.ToList();
            var user = userPostList[0];
            var influenceScore = CalculateUserInfluenceScore(user);

            if (influenceScore >= _options.MinimumInfluenceScore)
            {
                var userSentiment = Analyze(userPostList);

                activities.Add(new InfluencerActivity
                {
                    Username = group.Key,
                    FollowerCount = user.FollowerCount,
                    InfluenceScore = influenceScore,
                    PostCount = userPostList.Count,
                    Sentiment = userSentiment.OverallSentiment,
                    SentimentScore = userSentiment.AggregateScore,
                    TotalEngagement = userPostList.Sum(p => p.Likes + p.Retweets + p.Comments),
                    LatestPost = userPostList.OrderByDescending(p => p.Timestamp).First()
                });
            }
        }

        return activities.OrderByDescending(a => a.InfluenceScore).ToList();
    }

    private double AnalyzeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var words = TokenizeText(text);
        var score = 0.0;
        var wordCount = 0;

        foreach (var word in words)
        {
            var lowerWord = word.ToLowerInvariant();

            if (_slangLexicon.TryGetValue(lowerWord, out var slangScore))
            {
                score += slangScore;
                wordCount++;
            }
        }

        return wordCount > 0 ? score / wordCount : 0;
    }

    private double AnalyzeEmojis(string text)
    {
        var emojiScore = 0.0;
        var emojiCount = 0;

        foreach (var kvp in _emojiSentiment)
        {
            var count = CountOccurrences(text, kvp.Key);
            if (count > 0)
            {
                emojiScore += kvp.Value * count;
                emojiCount += count;
            }
        }

        return emojiCount > 0 ? emojiScore / emojiCount : 0;
    }

    private double AnalyzeHashtags(IReadOnlyList<string> hashtags)
    {
        if (hashtags.Count == 0)
            return 0;

        var score = 0.0;
        var count = 0;

        foreach (var hashtag in hashtags)
        {
            var lower = hashtag.ToLowerInvariant();

            if (lower.Contains("bull") || lower.Contains("buy") || lower.Contains("long") ||
                lower.Contains("moon") || lower.Contains("rocket") || lower.Contains("pump"))
            {
                score += 0.6;
                count++;
            }
            else if (lower.Contains("bear") || lower.Contains("sell") || lower.Contains("short") ||
                     lower.Contains("crash") || lower.Contains("dump") || lower.Contains("rekt"))
            {
                score -= 0.6;
                count++;
            }
        }

        return count > 0 ? score / count : 0;
    }

    private static bool DetectBullishSignal(string content)
    {
        var lower = content.ToLowerInvariant();
        var bullishPatterns = new[]
        {
            "going long", "bought calls", "buy the dip", "to the moon", "bullish af",
            "diamond hands", "hodl", "let's go", "breaking out", "calls printing",
            "loaded up", "adding more", "accumulating", "price target"
        };

        return bullishPatterns.Any(p => lower.Contains(p));
    }

    private static bool DetectBearishSignal(string content)
    {
        var lower = content.ToLowerInvariant();
        var bearishPatterns = new[]
        {
            "going short", "bought puts", "sell everything", "crash incoming", "bearish af",
            "paper hands", "getting out", "dumping", "puts printing", "dead cat",
            "head and shoulders", "double top", "sell the rip", "exit now"
        };

        return bearishPatterns.Any(p => lower.Contains(p));
    }

    private static List<string> ExtractCashtags(string content)
    {
        var cashtags = new List<string>();
        var words = content.Split([' ', '\n', '\t', ',', '.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (word.StartsWith("$") && word.Length > 1 && word.Length <= 6)
            {
                var tag = word.TrimEnd([',', '.', '!', '?', ':', ';']);
                if (tag.Skip(1).All(c => char.IsLetter(c)))
                {
                    cashtags.Add(tag.ToUpperInvariant());
                }
            }
        }

        return cashtags.Distinct().ToList();
    }

    private static List<string> TokenizeText(string text)
    {
        return text.Split([' ', '\n', '\t', ',', '.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1 && !w.StartsWith("$") && !w.StartsWith("#") && !w.StartsWith("@"))
            .ToList();
    }

    private double CalculateInfluenceWeight(SocialPost post)
    {
        var influenceScore = CalculateUserInfluenceScore(post);
        return Math.Log10(1 + influenceScore) / 5.0; // Normalize to roughly 0-1
    }

    private double CalculateUserInfluenceScore(SocialPost post)
    {
        // Influence = followers + verification bonus + historical engagement
        var followerScore = Math.Log10(1 + post.FollowerCount);
        var verificationBonus = post.IsVerified ? 2.0 : 0.0;
        var engagementRate = post.FollowerCount > 0 ?
            (double)(post.Likes + post.Retweets) / post.FollowerCount : 0;

        return followerScore + verificationBonus + engagementRate * 10;
    }

    private static double CalculateEngagementWeight(SocialPost post)
    {
        var totalEngagement = post.Likes + post.Retweets + post.Comments;
        return Math.Log10(1 + totalEngagement) / 4.0; // Normalize
    }

    private double CalculateRecencyWeight(DateTime timestamp)
    {
        var age = DateTime.UtcNow - timestamp;
        var halfLife = _options.RecencyHalfLife;
        return Math.Exp(-age.TotalHours / halfLife.TotalHours);
    }

    private static double CalculateEngagementScore(SocialPost post)
    {
        return Math.Log10(1 + post.Likes + post.Retweets * 2 + post.Comments * 3);
    }

    private static SentimentScore ClassifySentiment(double score)
    {
        return score switch
        {
            > 0.5 => SentimentScore.VeryPositive,
            > 0.15 => SentimentScore.Positive,
            < -0.5 => SentimentScore.VeryNegative,
            < -0.15 => SentimentScore.Negative,
            _ => SentimentScore.Neutral
        };
    }

    private static double CalculateConfidence(List<PostSentimentResult> results)
    {
        if (results.Count < 5)
            return 0.3;

        var scores = results.Select(r => r.Score).ToList();
        var mean = scores.Average();
        var variance = scores.Sum(s => Math.Pow(s - mean, 2)) / scores.Count;
        var stdDev = Math.Sqrt(variance);

        // Lower variance = higher confidence
        var confidence = 1.0 / (1.0 + stdDev * 2);

        // Boost confidence with more posts
        var countBoost = Math.Min(0.3, results.Count / 100.0);

        return Math.Min(1.0, confidence * 0.7 + countBoost);
    }

    private static Dictionary<string, List<SocialPost>> GroupBySymbol(IReadOnlyList<SocialPost> posts)
    {
        var result = new Dictionary<string, List<SocialPost>>(StringComparer.OrdinalIgnoreCase);

        foreach (var post in posts)
        {
            var cashtags = ExtractCashtags(post.Content);
            foreach (var tag in cashtags)
            {
                var symbol = tag.TrimStart('$');
                if (!result.TryGetValue(symbol, out var list))
                {
                    list = [];
                    result[symbol] = list;
                }
                list.Add(post);
            }
        }

        return result;
    }

    private static List<TrendingItem> ExtractTrendingCashtags(IReadOnlyList<SocialPost> posts)
    {
        var cashtags = posts
            .SelectMany(p => ExtractCashtags(p.Content))
            .GroupBy(c => c)
            .Select(g => new TrendingItem
            {
                Tag = g.Key,
                Count = g.Count(),
                Engagement = posts.Where(p => p.Content.Contains(g.Key)).Sum(p => p.Likes + p.Retweets)
            })
            .OrderByDescending(t => t.Count)
            .Take(10)
            .ToList();

        return cashtags;
    }

    private static List<TrendingItem> ExtractTrendingHashtags(IReadOnlyList<SocialPost> posts)
    {
        var hashtags = posts
            .SelectMany(p => p.Hashtags)
            .GroupBy(h => h.ToLowerInvariant())
            .Select(g => new TrendingItem
            {
                Tag = g.Key,
                Count = g.Count(),
                Engagement = 0
            })
            .OrderByDescending(t => t.Count)
            .Take(10)
            .ToList();

        return hashtags;
    }

    private static List<string> ExtractTopKeywords(IReadOnlyList<SocialPost> posts)
    {
        var words = posts
            .SelectMany(p => TokenizeText(p.Content))
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 3)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        return words;
    }

    private static List<string> GetTopInfluencers(IReadOnlyList<SocialPost> posts)
    {
        return posts
            .GroupBy(p => p.Username)
            .Select(g => new { Username = g.Key, TotalFollowers = g.First().FollowerCount })
            .OrderByDescending(x => x.TotalFollowers)
            .Take(5)
            .Select(x => x.Username)
            .ToList();
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    private Dictionary<string, double> BuildSlangLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            // Bullish slang
            { "moon", 0.7 }, { "mooning", 0.8 }, { "rocket", 0.7 }, { "rocketship", 0.8 },
            { "tendies", 0.6 }, { "gains", 0.6 }, { "gainz", 0.6 }, { "lambo", 0.7 },
            { "bullish", 0.8 }, { "bull", 0.5 }, { "bulls", 0.5 },
            { "hodl", 0.5 }, { "diamond", 0.4 }, { "diamondhands", 0.6 },
            { "buy", 0.4 }, { "bought", 0.4 }, { "buying", 0.4 }, { "long", 0.5 },
            { "calls", 0.5 }, { "call", 0.4 }, { "yolo", 0.3 },
            { "breakout", 0.6 }, { "squeeze", 0.5 }, { "pump", 0.4 },
            { "ath", 0.5 }, { "high", 0.3 }, { "green", 0.4 },
            { "fire", 0.5 }, { "lit", 0.4 }, { "goat", 0.5 },

            // Bearish slang
            { "crash", -0.7 }, { "crashing", -0.8 }, { "dump", -0.6 }, { "dumping", -0.7 },
            { "bearish", -0.8 }, { "bear", -0.5 }, { "bears", -0.5 },
            { "rekt", -0.7 }, { "wrecked", -0.6 }, { "bagholding", -0.5 }, { "bagholder", -0.5 },
            { "paperhands", -0.4 }, { "paper", -0.2 },
            { "sell", -0.4 }, { "sold", -0.4 }, { "selling", -0.4 }, { "short", -0.5 },
            { "puts", -0.5 }, { "put", -0.4 },
            { "breakdown", -0.6 }, { "falling", -0.5 }, { "tanking", -0.6 },
            { "red", -0.4 }, { "blood", -0.5 }, { "dead", -0.5 },
            { "scam", -0.7 }, { "fraud", -0.8 }, { "ponzi", -0.8 },

            // Neutral/context-dependent
            { "volatile", 0.0 }, { "volatility", 0.0 },
            { "options", 0.0 }, { "stock", 0.0 }, { "stocks", 0.0 },
            { "market", 0.0 }, { "trading", 0.0 }, { "trade", 0.0 }
        };
    }

    private Dictionary<string, double> BuildEmojiSentiment()
    {
        return new Dictionary<string, double>
        {
            // Bullish emojis
            { "\U0001F680", 0.8 },  // rocket
            { "\U0001F31D", 0.7 },  // moon
            { "\U0001F4B0", 0.6 },  // money bag
            { "\U0001F4B8", 0.5 },  // money with wings
            { "\U0001F4C8", 0.6 },  // chart increasing
            { "\U0001F525", 0.5 },  // fire
            { "\U0001F4AA", 0.5 },  // flexed biceps
            { "\U0001F389", 0.5 },  // party popper
            { "\U0001F44D", 0.4 },  // thumbs up
            { "\U0001F60E", 0.4 },  // sunglasses
            { "\U0001F911", 0.5 },  // money face
            { "\U0001F48E", 0.6 },  // gem (diamond)
            { "\U0001F3AF", 0.5 },  // bullseye

            // Bearish emojis
            { "\U0001F4C9", -0.6 }, // chart decreasing
            { "\U0001F6A8", -0.5 }, // police light
            { "\U0001F4A9", -0.4 }, // poop
            { "\U0001F44E", -0.4 }, // thumbs down
            { "\U0001F622", -0.4 }, // crying
            { "\U0001F62D", -0.5 }, // loudly crying
            { "\U0001F4A3", -0.5 }, // bomb
            { "\U0001F480", -0.5 }, // skull
            { "\U0001F6AB", -0.4 }, // prohibited
            { "\U0001F4A2", -0.4 }, // anger
            { "\U0001F630", -0.5 }, // anxious
            { "\U0001F494", -0.4 }, // broken heart
            { "\U0001F640", -0.5 }  // weary cat
        };
    }
}

/// <summary>
/// Options for social sentiment analysis.
/// </summary>
public sealed class SocialSentimentOptions
{
    /// <summary>Weight for text analysis.</summary>
    public double TextWeight { get; set; } = 0.6;

    /// <summary>Weight for emoji analysis.</summary>
    public double EmojiWeight { get; set; } = 0.25;

    /// <summary>Weight for hashtag analysis.</summary>
    public double HashtagWeight { get; set; } = 0.15;

    /// <summary>Half-life for recency weighting.</summary>
    public TimeSpan RecencyHalfLife { get; set; } = TimeSpan.FromHours(4);

    /// <summary>Minimum influence score for influencer detection.</summary>
    public double MinimumInfluenceScore { get; set; } = 3.0;

    /// <summary>Baseline mentions per hour for symbols (for spike detection).</summary>
    public Dictionary<string, double> BaselineMentionsPerHour { get; set; } = [];

    /// <summary>Time window for spike detection.</summary>
    public TimeSpan SpikeDetectionWindow { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Minimum posts required for spike detection.</summary>
    public int MinimumPostsForSpike { get; set; } = 10;

    /// <summary>Threshold for activity spike (multiple of baseline).</summary>
    public double SpikeThreshold { get; set; } = 3.0;
}

/// <summary>
/// Represents a social media post.
/// </summary>
public sealed class SocialPost
{
    public string Id { get; set; } = string.Empty;
    public SocialPlatform Platform { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int Likes { get; set; }
    public int Retweets { get; set; }
    public int Comments { get; set; }
    public int FollowerCount { get; set; }
    public bool IsVerified { get; set; }
    public IReadOnlyList<string> Hashtags { get; set; } = [];
    public IReadOnlyList<string> Cashtags { get; set; } = [];
}

/// <summary>
/// Social media platforms.
/// </summary>
public enum SocialPlatform
{
    Twitter,
    StockTwits,
    Reddit,
    Discord,
    Telegram
}

/// <summary>
/// Result of social sentiment analysis.
/// </summary>
public sealed class SocialSentimentResult
{
    public SentimentScore OverallSentiment { get; set; }
    public double AggregateScore { get; set; }
    public double Confidence { get; set; }
    public int PostCount { get; set; }
    public int BullishCount { get; set; }
    public int BearishCount { get; set; }
    public int NeutralCount { get; set; }
    public double BullBearRatio { get; set; }
    public double PostsPerHour { get; set; }
    public long TotalEngagement { get; set; }
    public double AverageEngagement { get; set; }
    public IReadOnlyList<PostSentimentResult> PostResults { get; set; } = [];
    public IReadOnlyList<TrendingItem> TrendingCashtags { get; set; } = [];
    public IReadOnlyList<TrendingItem> TrendingHashtags { get; set; } = [];
}

/// <summary>
/// Sentiment result for a single post.
/// </summary>
public sealed class PostSentimentResult
{
    public string PostId { get; set; } = string.Empty;
    public SocialPlatform Platform { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Score { get; set; }
    public double TextScore { get; set; }
    public double EmojiScore { get; set; }
    public SentimentScore Sentiment { get; set; }
    public bool IsBullish { get; set; }
    public bool IsBearish { get; set; }
    public IReadOnlyList<string> Cashtags { get; set; } = [];
    public double EngagementScore { get; set; }
}

/// <summary>
/// Symbol-specific social sentiment.
/// </summary>
public sealed class SymbolSocialSentiment
{
    public string Symbol { get; set; } = string.Empty;
    public int PostCount { get; set; }
    public SentimentScore Sentiment { get; set; }
    public double Score { get; set; }
    public double BullBearRatio { get; set; }
    public double MentionsPerHour { get; set; }
    public double MentionsSurge { get; set; }
    public long TotalEngagement { get; set; }
    public IReadOnlyList<string> TopInfluencers { get; set; } = [];
}

/// <summary>
/// Trending item (cashtag or hashtag).
/// </summary>
public sealed class TrendingItem
{
    public string Tag { get; set; } = string.Empty;
    public int Count { get; set; }
    public long Engagement { get; set; }
}

/// <summary>
/// Represents an activity spike for a symbol.
/// </summary>
public sealed class SocialActivitySpike
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double SpikeRatio { get; set; }
    public double MentionsPerHour { get; set; }
    public double BaselineMentionsPerHour { get; set; }
    public int PostCount { get; set; }
    public SentimentScore Sentiment { get; set; }
    public double SentimentScore { get; set; }
    public IReadOnlyList<string> TopKeywords { get; set; } = [];
}

/// <summary>
/// Influencer activity for a symbol.
/// </summary>
public sealed class InfluencerActivity
{
    public string Username { get; set; } = string.Empty;
    public int FollowerCount { get; set; }
    public double InfluenceScore { get; set; }
    public int PostCount { get; set; }
    public SentimentScore Sentiment { get; set; }
    public double SentimentScore { get; set; }
    public long TotalEngagement { get; set; }
    public SocialPost LatestPost { get; set; } = new();
}
