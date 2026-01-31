namespace OoplesFinance.StockIndicators.Builder.ML.Sentiment;

/// <summary>
/// Analyzes sentiment from earnings calls, transcripts, and guidance.
/// Extracts key metrics, tone analysis, and forward-looking statements.
/// </summary>
public sealed class EarningsSentimentAnalyzer
{
    private readonly EarningsSentimentOptions _options;
    private readonly Dictionary<string, double> _financialLexicon;
    private readonly Dictionary<string, double> _uncertaintyLexicon;
    private readonly Dictionary<string, double> _confidenceLexicon;
    private readonly HashSet<string> _forwardLookingIndicators;

    /// <summary>
    /// Initializes a new instance of the EarningsSentimentAnalyzer.
    /// </summary>
    public EarningsSentimentAnalyzer(EarningsSentimentOptions? options = null)
    {
        _options = options ?? new EarningsSentimentOptions();
        _financialLexicon = BuildFinancialLexicon();
        _uncertaintyLexicon = BuildUncertaintyLexicon();
        _confidenceLexicon = BuildConfidenceLexicon();
        _forwardLookingIndicators = BuildForwardLookingIndicators();
    }

    /// <summary>
    /// Analyzes a complete earnings call transcript.
    /// </summary>
    public EarningsCallAnalysis AnalyzeEarningsCall(EarningsCallTranscript transcript)
    {
        var sections = SplitIntoSections(transcript.Content);

        // Analyze prepared remarks vs Q&A separately
        var preparedRemarksAnalysis = AnalyzeSection(sections.PreparedRemarks, SectionType.PreparedRemarks);
        var qaAnalysis = AnalyzeSection(sections.QAndA, SectionType.QAndA);

        // Q&A often reveals more authentic sentiment
        var combinedScore = preparedRemarksAnalysis.Score * _options.PreparedRemarksWeight +
                           qaAnalysis.Score * _options.QAndAWeight;

        // Extract key metrics mentioned
        var metricsMentioned = ExtractMetrics(transcript.Content);

        // Extract forward-looking statements
        var forwardLookingStatements = ExtractForwardLookingStatements(transcript.Content);

        // Analyze management tone
        var toneAnalysis = AnalyzeManagementTone(transcript.Content);

        // Detect hedging language
        var hedgingScore = CalculateHedgingScore(transcript.Content);

        // Compare to guidance language
        var guidanceAnalysis = AnalyzeGuidance(transcript.Content);

        // Calculate overall confidence
        var confidence = CalculateAnalysisConfidence(transcript, preparedRemarksAnalysis, qaAnalysis);

        return new EarningsCallAnalysis
        {
            Symbol = transcript.Symbol,
            Quarter = transcript.Quarter,
            Year = transcript.Year,
            CallDate = transcript.CallDate,
            OverallSentiment = ClassifySentiment(combinedScore),
            SentimentScore = combinedScore,
            PreparedRemarksSentiment = preparedRemarksAnalysis.Sentiment,
            PreparedRemarksScore = preparedRemarksAnalysis.Score,
            QAndASentiment = qaAnalysis.Sentiment,
            QAndAScore = qaAnalysis.Score,
            Confidence = confidence,
            ToneAnalysis = toneAnalysis,
            HedgingScore = hedgingScore,
            ForwardLookingStatements = forwardLookingStatements,
            MetricsMentioned = metricsMentioned,
            GuidanceAnalysis = guidanceAnalysis,
            KeyPhrases = ExtractKeyPhrases(transcript.Content),
            SentimentByTopic = AnalyzeSentimentByTopic(transcript.Content)
        };
    }

    /// <summary>
    /// Compares current earnings sentiment to historical calls.
    /// </summary>
    public EarningsSentimentComparison CompareToHistorical(
        EarningsCallAnalysis current,
        IReadOnlyList<EarningsCallAnalysis> historical)
    {
        if (historical.Count == 0)
        {
            return new EarningsSentimentComparison
            {
                CurrentAnalysis = current,
                TrendDirection = TrendDirection.Sideways
            };
        }

        var sortedHistorical = historical.OrderBy(h => h.CallDate).ToList();
        var previousCall = sortedHistorical[sortedHistorical.Count - 1];

        // Calculate changes
        var sentimentChange = current.SentimentScore - previousCall.SentimentScore;
        var hedgingChange = current.HedgingScore - previousCall.HedgingScore;
        var confidenceChange = current.ToneAnalysis.ConfidenceScore - previousCall.ToneAnalysis.ConfidenceScore;

        // Calculate historical average
        var historicalAvgSentiment = sortedHistorical.Average(h => h.SentimentScore);
        var deviationFromAverage = current.SentimentScore - historicalAvgSentiment;

        // Determine trend
        var trendDirection = TrendDirection.Sideways;
        if (sentimentChange > _options.SignificantChangeThreshold)
            trendDirection = TrendDirection.Improving;
        else if (sentimentChange < -_options.SignificantChangeThreshold)
            trendDirection = TrendDirection.Deteriorating;

        // Identify notable changes
        var notableChanges = IdentifyNotableChanges(current, previousCall);

        return new EarningsSentimentComparison
        {
            CurrentAnalysis = current,
            PreviousAnalysis = previousCall,
            SentimentChange = sentimentChange,
            HedgingChange = hedgingChange,
            ConfidenceChange = confidenceChange,
            HistoricalAverageSentiment = historicalAvgSentiment,
            DeviationFromAverage = deviationFromAverage,
            TrendDirection = trendDirection,
            NotableChanges = notableChanges
        };
    }

    /// <summary>
    /// Analyzes sentiment changes throughout the call.
    /// </summary>
    public CallSentimentProgression AnalyzeProgression(EarningsCallTranscript transcript)
    {
        var segments = SplitIntoSegments(transcript.Content, _options.SegmentSize);
        var segmentResults = new List<SegmentSentiment>();

        for (int i = 0; i < segments.Count; i++)
        {
            var segmentAnalysis = AnalyzeSection(segments[i], SectionType.General);
            segmentResults.Add(new SegmentSentiment
            {
                SegmentIndex = i,
                Text = segments[i].Length > 200 ? segments[i].Substring(0, 200) + "..." : segments[i],
                Score = segmentAnalysis.Score,
                Sentiment = segmentAnalysis.Sentiment
            });
        }

        // Calculate progression metrics
        var startSentiment = segmentResults.Take(3).Average(s => s.Score);
        var endSentiment = segmentResults.Skip(Math.Max(0, segmentResults.Count - 3)).Average(s => s.Score);
        var sentimentDrift = endSentiment - startSentiment;

        // Find sentiment peaks and valleys
        var peakIndex = segmentResults.IndexOf(segmentResults.MaxBy(s => s.Score)!);
        var valleyIndex = segmentResults.IndexOf(segmentResults.MinBy(s => s.Score)!);

        // Calculate volatility
        var scores = segmentResults.Select(s => s.Score).ToList();
        var avgScore = scores.Average();
        var variance = scores.Sum(s => Math.Pow(s - avgScore, 2)) / scores.Count;
        var volatility = Math.Sqrt(variance);

        return new CallSentimentProgression
        {
            Segments = segmentResults,
            StartSentiment = startSentiment,
            EndSentiment = endSentiment,
            SentimentDrift = sentimentDrift,
            PeakSegmentIndex = peakIndex,
            ValleySegmentIndex = valleyIndex,
            SentimentVolatility = volatility
        };
    }

    /// <summary>
    /// Extracts and analyzes analyst questions.
    /// </summary>
    public AnalystQuestionsAnalysis AnalyzeAnalystQuestions(EarningsCallTranscript transcript)
    {
        var questions = ExtractAnalystQuestions(transcript.Content);
        var questionAnalyses = new List<AnalystQuestionAnalysis>();

        foreach (var question in questions)
        {
            var sentiment = AnalyzeQuestionSentiment(question.Text);
            var topics = IdentifyQuestionTopics(question.Text);
            var tone = ClassifyQuestionTone(question.Text);

            questionAnalyses.Add(new AnalystQuestionAnalysis
            {
                AnalystName = question.AnalystName,
                Firm = question.Firm,
                QuestionText = question.Text,
                Sentiment = sentiment,
                Topics = topics,
                Tone = tone
            });
        }

        // Aggregate analysis
        var topicFrequency = questionAnalyses
            .SelectMany(q => q.Topics)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        var avgSentiment = questionAnalyses.Count > 0 ?
            questionAnalyses.Average(q => q.Sentiment) : 0;

        var toneDistribution = questionAnalyses
            .GroupBy(q => q.Tone)
            .ToDictionary(g => g.Key, g => g.Count());

        return new AnalystQuestionsAnalysis
        {
            TotalQuestions = questions.Count,
            Questions = questionAnalyses,
            AverageSentiment = avgSentiment,
            TopicFrequency = topicFrequency,
            ToneDistribution = toneDistribution,
            MostCommonTopics = topicFrequency.OrderByDescending(t => t.Value).Take(5).Select(t => t.Key).ToList()
        };
    }

    private SectionAnalysis AnalyzeSection(string content, SectionType sectionType)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SectionAnalysis { Score = 0, Sentiment = SentimentScore.Neutral };
        }

        var words = TokenizeText(content);
        var positiveScore = 0.0;
        var negativeScore = 0.0;
        var wordCount = 0;

        foreach (var word in words)
        {
            var lowerWord = word.ToLowerInvariant();

            if (_financialLexicon.TryGetValue(lowerWord, out var score))
            {
                if (score > 0)
                    positiveScore += score;
                else
                    negativeScore += Math.Abs(score);
                wordCount++;
            }
        }

        var netScore = wordCount > 0 ? (positiveScore - negativeScore) / wordCount : 0;

        // Adjust for section type
        if (sectionType == SectionType.QAndA)
        {
            // Q&A tends to be more volatile, normalize
            netScore *= 0.9;
        }

        return new SectionAnalysis
        {
            Score = netScore,
            Sentiment = ClassifySentiment(netScore),
            PositiveWordCount = (int)positiveScore,
            NegativeWordCount = (int)negativeScore
        };
    }

    private TranscriptSections SplitIntoSections(string content)
    {
        var qaIndicators = new[] {
            "question-and-answer", "q&a session", "q & a", "questions and answers",
            "operator:", "we will now take questions", "we'll now take questions"
        };

        var lowerContent = content.ToLowerInvariant();
        var splitIndex = -1;

        foreach (var indicator in qaIndicators)
        {
            var index = lowerContent.IndexOf(indicator, StringComparison.Ordinal);
            if (index > 0 && (splitIndex < 0 || index < splitIndex))
            {
                splitIndex = index;
            }
        }

        if (splitIndex > 0)
        {
            return new TranscriptSections
            {
                PreparedRemarks = content.Substring(0, splitIndex),
                QAndA = content.Substring(splitIndex)
            };
        }

        // If no clear split, assume all is prepared remarks
        return new TranscriptSections
        {
            PreparedRemarks = content,
            QAndA = string.Empty
        };
    }

    private static List<string> SplitIntoSegments(string content, int segmentSize)
    {
        var words = content.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<string>();

        for (int i = 0; i < words.Length; i += segmentSize)
        {
            var segmentWords = words.Skip(i).Take(segmentSize);
            segments.Add(string.Join(" ", segmentWords));
        }

        return segments;
    }

    private ToneAnalysis AnalyzeManagementTone(string content)
    {
        var words = TokenizeText(content);
        var uncertaintyCount = 0;
        var confidenceCount = 0;
        var totalSentimentWords = 0;

        foreach (var word in words)
        {
            var lowerWord = word.ToLowerInvariant();

            if (_uncertaintyLexicon.ContainsKey(lowerWord))
            {
                uncertaintyCount++;
                totalSentimentWords++;
            }
            else if (_confidenceLexicon.ContainsKey(lowerWord))
            {
                confidenceCount++;
                totalSentimentWords++;
            }
        }

        var uncertaintyScore = totalSentimentWords > 0 ?
            (double)uncertaintyCount / totalSentimentWords : 0;
        var confidenceScore = totalSentimentWords > 0 ?
            (double)confidenceCount / totalSentimentWords : 0;

        var tone = ManagementTone.Neutral;
        if (confidenceScore > uncertaintyScore * 1.5)
            tone = ManagementTone.Confident;
        else if (uncertaintyScore > confidenceScore * 1.5)
            tone = ManagementTone.Cautious;
        else if (uncertaintyScore > 0.3)
            tone = ManagementTone.Defensive;

        return new ToneAnalysis
        {
            OverallTone = tone,
            UncertaintyScore = uncertaintyScore,
            ConfidenceScore = confidenceScore,
            UncertaintyWordCount = uncertaintyCount,
            ConfidenceWordCount = confidenceCount
        };
    }

    private double CalculateHedgingScore(string content)
    {
        var hedgingPhrases = new[]
        {
            "we believe", "we think", "we expect", "we anticipate",
            "approximately", "roughly", "around", "about",
            "subject to", "depending on", "contingent upon",
            "may", "might", "could", "possibly", "potentially",
            "uncertain", "unclear", "difficult to predict",
            "various factors", "market conditions"
        };

        var lowerContent = content.ToLowerInvariant();
        var hedgingCount = hedgingPhrases.Sum(phrase =>
            CountOccurrences(lowerContent, phrase));

        var wordCount = TokenizeText(content).Count;

        return wordCount > 0 ? (double)hedgingCount / (wordCount / 100.0) : 0;
    }

    private GuidanceAnalysis AnalyzeGuidance(string content)
    {
        var guidanceIndicators = new[]
        {
            "guidance", "outlook", "forecast", "expect", "anticipate",
            "target", "range", "full year", "quarter", "fiscal year"
        };

        var lowerContent = content.ToLowerInvariant();

        // Check if guidance mentioned
        var hasGuidance = guidanceIndicators.Any(g => lowerContent.Contains(g));

        // Check guidance direction
        var raisedIndicators = new[] { "raised", "increased", "higher", "above", "exceeded", "beat" };
        var loweredIndicators = new[] { "lowered", "decreased", "reduced", "below", "revised down" };
        var maintainedIndicators = new[] { "maintained", "reaffirmed", "reiterated", "unchanged" };

        var raisedCount = raisedIndicators.Sum(i => CountOccurrences(lowerContent, i));
        var loweredCount = loweredIndicators.Sum(i => CountOccurrences(lowerContent, i));
        var maintainedCount = maintainedIndicators.Sum(i => CountOccurrences(lowerContent, i));

        var direction = GuidanceDirection.NotProvided;
        if (!hasGuidance)
            direction = GuidanceDirection.NotProvided;
        else if (raisedCount > loweredCount && raisedCount > maintainedCount)
            direction = GuidanceDirection.Raised;
        else if (loweredCount > raisedCount && loweredCount > maintainedCount)
            direction = GuidanceDirection.Lowered;
        else if (maintainedCount > 0)
            direction = GuidanceDirection.Maintained;

        return new GuidanceAnalysis
        {
            HasGuidance = hasGuidance,
            Direction = direction,
            RaisedIndicatorCount = raisedCount,
            LoweredIndicatorCount = loweredCount,
            MaintainedIndicatorCount = maintainedCount
        };
    }

    private List<ForwardLookingStatement> ExtractForwardLookingStatements(string content)
    {
        var statements = new List<ForwardLookingStatement>();
        var sentences = SplitIntoSentences(content);

        foreach (var sentence in sentences)
        {
            var lowerSentence = sentence.ToLowerInvariant();
            var isForwardLooking = _forwardLookingIndicators.Any(i => lowerSentence.Contains(i));

            if (isForwardLooking)
            {
                var sentiment = AnalyzeSentenceSentiment(sentence);
                var timeframe = DetectTimeframe(sentence);

                statements.Add(new ForwardLookingStatement
                {
                    Text = sentence.Trim(),
                    Sentiment = sentiment,
                    Timeframe = timeframe
                });
            }
        }

        return statements.Take(20).ToList(); // Limit to top 20
    }

    private List<MetricMention> ExtractMetrics(string content)
    {
        var metrics = new List<MetricMention>();
        var metricPatterns = new Dictionary<string, string[]>
        {
            { "Revenue", new[] { "revenue", "sales", "top line" } },
            { "EPS", new[] { "eps", "earnings per share" } },
            { "Margin", new[] { "margin", "gross margin", "operating margin", "net margin" } },
            { "Growth", new[] { "growth", "grew", "increased by", "year-over-year" } },
            { "EBITDA", new[] { "ebitda", "adjusted ebitda" } },
            { "Cash Flow", new[] { "cash flow", "free cash flow", "operating cash" } },
            { "Guidance", new[] { "guidance", "outlook", "forecast" } },
            { "Backlog", new[] { "backlog", "order book", "pipeline" } }
        };

        var lowerContent = content.ToLowerInvariant();

        foreach (var metric in metricPatterns)
        {
            var mentions = metric.Value.Sum(p => CountOccurrences(lowerContent, p));
            if (mentions > 0)
            {
                metrics.Add(new MetricMention
                {
                    MetricName = metric.Key,
                    MentionCount = mentions,
                    Importance = CalculateMetricImportance(metric.Key, mentions)
                });
            }
        }

        return metrics.OrderByDescending(m => m.MentionCount).ToList();
    }

    private Dictionary<string, double> AnalyzeSentimentByTopic(string content)
    {
        var topics = new Dictionary<string, string[]>
        {
            { "Revenue", new[] { "revenue", "sales", "top line", "bookings" } },
            { "Costs", new[] { "cost", "expense", "spending", "investment" } },
            { "Competition", new[] { "competition", "competitor", "market share" } },
            { "Innovation", new[] { "innovation", "r&d", "research", "development", "product" } },
            { "Customers", new[] { "customer", "client", "user", "retention", "churn" } },
            { "Macro", new[] { "economy", "macro", "inflation", "interest rate", "recession" } }
        };

        var results = new Dictionary<string, double>();
        var sentences = SplitIntoSentences(content);

        foreach (var topic in topics)
        {
            var topicSentences = sentences.Where(s =>
                topic.Value.Any(t => s.Contains(t, StringComparison.OrdinalIgnoreCase))).ToList();

            if (topicSentences.Count > 0)
            {
                var avgSentiment = topicSentences.Average(s => AnalyzeSentenceSentiment(s));
                results[topic.Key] = avgSentiment;
            }
        }

        return results;
    }

    private List<AnalystQuestion> ExtractAnalystQuestions(string content)
    {
        var questions = new List<AnalystQuestion>();

        // Look for patterns like "John Smith from Goldman Sachs" followed by question
        var questionPatterns = new[]
        {
            @"([A-Z][a-z]+ [A-Z][a-z]+)\s+(?:from|with|of)\s+([A-Za-z\s&]+)[\.\:\-]\s*(.+\?)",
            @"([A-Z][a-z]+ [A-Z][a-z]+)\s*[\-\:]\s*(.+\?)"
        };

        var sentences = SplitIntoSentences(content);
        foreach (var sentence in sentences)
        {
            if (sentence.Contains("?") && sentence.Length > 20)
            {
                questions.Add(new AnalystQuestion
                {
                    AnalystName = "Unknown",
                    Firm = "Unknown",
                    Text = sentence.Trim()
                });
            }
        }

        return questions.Take(30).ToList(); // Limit
    }

    private double AnalyzeQuestionSentiment(string question)
    {
        var concernIndicators = new[] { "concern", "worried", "risk", "decline", "miss", "weak", "challenge" };
        var positiveIndicators = new[] { "growth", "opportunity", "strong", "beat", "exceed", "impressive" };

        var lowerQuestion = question.ToLowerInvariant();

        var concernCount = concernIndicators.Count(i => lowerQuestion.Contains(i));
        var positiveCount = positiveIndicators.Count(i => lowerQuestion.Contains(i));

        return (positiveCount - concernCount) * 0.3;
    }

    private static List<string> IdentifyQuestionTopics(string question)
    {
        var topicKeywords = new Dictionary<string, string[]>
        {
            { "Guidance", new[] { "guidance", "outlook", "forecast", "expect" } },
            { "Margins", new[] { "margin", "profitability", "cost" } },
            { "Growth", new[] { "growth", "revenue", "sales", "expansion" } },
            { "Competition", new[] { "competition", "market share", "competitor" } },
            { "Capital Allocation", new[] { "buyback", "dividend", "acquisition", "capex" } },
            { "Macro", new[] { "economy", "macro", "demand", "environment" } }
        };

        var lowerQuestion = question.ToLowerInvariant();
        var topics = new List<string>();

        foreach (var topic in topicKeywords)
        {
            if (topic.Value.Any(k => lowerQuestion.Contains(k)))
            {
                topics.Add(topic.Key);
            }
        }

        return topics;
    }

    private static QuestionTone ClassifyQuestionTone(string question)
    {
        var lowerQuestion = question.ToLowerInvariant();

        if (lowerQuestion.Contains("concern") || lowerQuestion.Contains("worried") ||
            lowerQuestion.Contains("risk") || lowerQuestion.Contains("decline"))
            return QuestionTone.Skeptical;

        if (lowerQuestion.Contains("impressive") || lowerQuestion.Contains("congratulations") ||
            lowerQuestion.Contains("great") || lowerQuestion.Contains("strong"))
            return QuestionTone.Supportive;

        if (lowerQuestion.Contains("clarify") || lowerQuestion.Contains("detail") ||
            lowerQuestion.Contains("breakdown") || lowerQuestion.Contains("specific"))
            return QuestionTone.Probing;

        return QuestionTone.Neutral;
    }

    private static List<string> ExtractKeyPhrases(string content)
    {
        var keyPhrases = new List<string>();
        var phrases = new[]
        {
            "record quarter", "record year", "all-time high", "strong momentum",
            "headwinds", "tailwinds", "challenges", "opportunities",
            "beat expectations", "exceeded guidance", "ahead of plan",
            "below expectations", "missed", "shortfall",
            "market share gains", "customer wins", "new products"
        };

        var lowerContent = content.ToLowerInvariant();
        foreach (var phrase in phrases)
        {
            if (lowerContent.Contains(phrase))
            {
                keyPhrases.Add(phrase);
            }
        }

        return keyPhrases;
    }

    private static List<string> SplitIntoSentences(string content)
    {
        return content.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 10)
            .ToList();
    }

    private static List<string> TokenizeText(string text)
    {
        return text.Split([' ', '\n', '\t', ',', '.', '!', '?', ':', ';'],
            StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToList();
    }

    private double AnalyzeSentenceSentiment(string sentence)
    {
        var words = TokenizeText(sentence);
        var score = 0.0;
        var count = 0;

        foreach (var word in words)
        {
            if (_financialLexicon.TryGetValue(word.ToLowerInvariant(), out var wordScore))
            {
                score += wordScore;
                count++;
            }
        }

        return count > 0 ? score / count : 0;
    }

    private static TimeframeType DetectTimeframe(string sentence)
    {
        var lowerSentence = sentence.ToLowerInvariant();

        if (lowerSentence.Contains("next quarter") || lowerSentence.Contains("q1") ||
            lowerSentence.Contains("q2") || lowerSentence.Contains("q3") || lowerSentence.Contains("q4"))
            return TimeframeType.NextQuarter;

        if (lowerSentence.Contains("full year") || lowerSentence.Contains("fiscal year") ||
            lowerSentence.Contains("this year") || lowerSentence.Contains("annual"))
            return TimeframeType.FullYear;

        if (lowerSentence.Contains("long-term") || lowerSentence.Contains("long term") ||
            lowerSentence.Contains("over time") || lowerSentence.Contains("multi-year"))
            return TimeframeType.LongTerm;

        return TimeframeType.Unspecified;
    }

    private static double CalculateMetricImportance(string metricName, int mentions)
    {
        var baseImportance = metricName switch
        {
            "Revenue" => 1.0,
            "EPS" => 1.0,
            "Guidance" => 0.9,
            "Margin" => 0.8,
            "Growth" => 0.8,
            "Cash Flow" => 0.7,
            "EBITDA" => 0.7,
            "Backlog" => 0.6,
            _ => 0.5
        };

        return baseImportance * Math.Log10(1 + mentions);
    }

    private static double CalculateAnalysisConfidence(
        EarningsCallTranscript transcript,
        SectionAnalysis preparedRemarks,
        SectionAnalysis qaAnalysis)
    {
        var confidence = 0.5;

        // Longer transcripts = more confidence
        var wordCount = transcript.Content.Split(' ').Length;
        confidence += Math.Min(0.2, wordCount / 10000.0);

        // Both sections present = more confidence
        if (preparedRemarks.Score != 0 && qaAnalysis.Score != 0)
            confidence += 0.15;

        // More sentiment words = more confidence
        var totalSentimentWords = preparedRemarks.PositiveWordCount + preparedRemarks.NegativeWordCount +
                                 qaAnalysis.PositiveWordCount + qaAnalysis.NegativeWordCount;
        confidence += Math.Min(0.15, totalSentimentWords / 500.0);

        return Math.Min(1.0, confidence);
    }

    private static List<string> IdentifyNotableChanges(
        EarningsCallAnalysis current,
        EarningsCallAnalysis previous)
    {
        var changes = new List<string>();

        if (current.SentimentScore - previous.SentimentScore > 0.2)
            changes.Add("Significant improvement in overall sentiment");
        else if (previous.SentimentScore - current.SentimentScore > 0.2)
            changes.Add("Significant deterioration in overall sentiment");

        if (current.HedgingScore - previous.HedgingScore > 0.5)
            changes.Add("Increased use of hedging language");
        else if (previous.HedgingScore - current.HedgingScore > 0.5)
            changes.Add("Decreased use of hedging language");

        if (current.ToneAnalysis.OverallTone != previous.ToneAnalysis.OverallTone)
            changes.Add($"Management tone changed from {previous.ToneAnalysis.OverallTone} to {current.ToneAnalysis.OverallTone}");

        if (current.GuidanceAnalysis.Direction != previous.GuidanceAnalysis.Direction)
            changes.Add($"Guidance direction changed to {current.GuidanceAnalysis.Direction}");

        return changes;
    }

    private static SentimentScore ClassifySentiment(double score)
    {
        return score switch
        {
            > 0.4 => SentimentScore.VeryPositive,
            > 0.15 => SentimentScore.Positive,
            < -0.4 => SentimentScore.VeryNegative,
            < -0.15 => SentimentScore.Negative,
            _ => SentimentScore.Neutral
        };
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

    private Dictionary<string, double> BuildFinancialLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            // Strong positive
            { "exceeded", 0.8 }, { "beat", 0.7 }, { "record", 0.7 }, { "exceptional", 0.8 },
            { "outstanding", 0.8 }, { "remarkable", 0.7 }, { "impressive", 0.7 },

            // Moderate positive
            { "strong", 0.5 }, { "solid", 0.4 }, { "healthy", 0.4 }, { "robust", 0.5 },
            { "growth", 0.4 }, { "improved", 0.4 }, { "momentum", 0.4 }, { "progress", 0.4 },
            { "opportunity", 0.4 }, { "optimistic", 0.5 }, { "confident", 0.5 },

            // Mild positive
            { "pleased", 0.3 }, { "satisfied", 0.2 }, { "encouraged", 0.3 },
            { "positive", 0.3 }, { "good", 0.2 }, { "stable", 0.1 },

            // Strong negative
            { "missed", -0.7 }, { "shortfall", -0.7 }, { "disappointing", -0.8 },
            { "weak", -0.6 }, { "declined", -0.6 }, { "deteriorated", -0.7 },

            // Moderate negative
            { "challenging", -0.4 }, { "headwinds", -0.4 }, { "pressure", -0.4 },
            { "concern", -0.4 }, { "uncertain", -0.4 }, { "difficult", -0.4 },
            { "slowdown", -0.5 }, { "contraction", -0.5 },

            // Mild negative
            { "cautious", -0.2 }, { "careful", -0.1 }, { "moderate", -0.1 },
            { "soft", -0.3 }, { "flat", -0.2 }
        };
    }

    private Dictionary<string, double> BuildUncertaintyLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "uncertain", 1.0 }, { "uncertainty", 1.0 }, { "unclear", 0.9 },
            { "unpredictable", 0.9 }, { "volatile", 0.8 }, { "volatility", 0.8 },
            { "may", 0.3 }, { "might", 0.3 }, { "could", 0.3 }, { "possibly", 0.5 },
            { "perhaps", 0.5 }, { "potentially", 0.4 }, { "approximately", 0.2 },
            { "roughly", 0.3 }, { "around", 0.2 }, { "about", 0.1 },
            { "believe", 0.3 }, { "think", 0.3 }, { "expect", 0.2 }, { "anticipate", 0.2 }
        };
    }

    private Dictionary<string, double> BuildConfidenceLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "confident", 1.0 }, { "certain", 0.9 }, { "definitely", 0.9 },
            { "clearly", 0.7 }, { "absolutely", 0.8 }, { "certainly", 0.8 },
            { "committed", 0.7 }, { "determined", 0.7 }, { "focused", 0.5 },
            { "will", 0.4 }, { "shall", 0.4 }, { "going to", 0.5 },
            { "proven", 0.6 }, { "demonstrated", 0.6 }, { "established", 0.5 }
        };
    }

    private HashSet<string> BuildForwardLookingIndicators()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "expect", "expects", "expected", "expecting",
            "anticipate", "anticipates", "anticipated", "anticipating",
            "believe", "believes", "believed", "believing",
            "plan", "plans", "planned", "planning",
            "forecast", "forecasts", "forecasted", "forecasting",
            "project", "projects", "projected", "projecting",
            "outlook", "guidance", "target", "goal",
            "will", "would", "should", "may", "might", "could",
            "going forward", "in the future", "next quarter", "next year",
            "full year", "fiscal year", "long-term", "over time"
        };
    }
}

/// <summary>
/// Options for earnings sentiment analysis.
/// </summary>
public sealed class EarningsSentimentOptions
{
    /// <summary>Weight for prepared remarks vs Q&A (0-1).</summary>
    public double PreparedRemarksWeight { get; set; } = 0.4;

    /// <summary>Weight for Q&A section.</summary>
    public double QAndAWeight { get; set; } = 0.6;

    /// <summary>Words per segment for progression analysis.</summary>
    public int SegmentSize { get; set; } = 500;

    /// <summary>Threshold for significant sentiment change.</summary>
    public double SignificantChangeThreshold { get; set; } = 0.15;
}

/// <summary>
/// Represents an earnings call transcript.
/// </summary>
public sealed class EarningsCallTranscript
{
    public string Symbol { get; set; } = string.Empty;
    public int Quarter { get; set; }
    public int Year { get; set; }
    public DateTime CallDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public string CEO { get; set; } = string.Empty;
    public string CFO { get; set; } = string.Empty;
}

/// <summary>
/// Complete analysis of an earnings call.
/// </summary>
public sealed class EarningsCallAnalysis
{
    public string Symbol { get; set; } = string.Empty;
    public int Quarter { get; set; }
    public int Year { get; set; }
    public DateTime CallDate { get; set; }
    public SentimentScore OverallSentiment { get; set; }
    public double SentimentScore { get; set; }
    public SentimentScore PreparedRemarksSentiment { get; set; }
    public double PreparedRemarksScore { get; set; }
    public SentimentScore QAndASentiment { get; set; }
    public double QAndAScore { get; set; }
    public double Confidence { get; set; }
    public ToneAnalysis ToneAnalysis { get; set; } = new();
    public double HedgingScore { get; set; }
    public IReadOnlyList<ForwardLookingStatement> ForwardLookingStatements { get; set; } = [];
    public IReadOnlyList<MetricMention> MetricsMentioned { get; set; } = [];
    public GuidanceAnalysis GuidanceAnalysis { get; set; } = new();
    public IReadOnlyList<string> KeyPhrases { get; set; } = [];
    public Dictionary<string, double> SentimentByTopic { get; set; } = [];
}

/// <summary>
/// Tone analysis of management.
/// </summary>
public sealed class ToneAnalysis
{
    public ManagementTone OverallTone { get; set; }
    public double UncertaintyScore { get; set; }
    public double ConfidenceScore { get; set; }
    public int UncertaintyWordCount { get; set; }
    public int ConfidenceWordCount { get; set; }
}

/// <summary>
/// Management tone classification.
/// </summary>
public enum ManagementTone
{
    Confident,
    Neutral,
    Cautious,
    Defensive
}

/// <summary>
/// Forward-looking statement.
/// </summary>
public sealed class ForwardLookingStatement
{
    public string Text { get; set; } = string.Empty;
    public double Sentiment { get; set; }
    public TimeframeType Timeframe { get; set; }
}

/// <summary>
/// Timeframe for forward-looking statements.
/// </summary>
public enum TimeframeType
{
    NextQuarter,
    FullYear,
    LongTerm,
    Unspecified
}

/// <summary>
/// Metric mentioned in the call.
/// </summary>
public sealed class MetricMention
{
    public string MetricName { get; set; } = string.Empty;
    public int MentionCount { get; set; }
    public double Importance { get; set; }
}

/// <summary>
/// Guidance analysis.
/// </summary>
public sealed class GuidanceAnalysis
{
    public bool HasGuidance { get; set; }
    public GuidanceDirection Direction { get; set; }
    public int RaisedIndicatorCount { get; set; }
    public int LoweredIndicatorCount { get; set; }
    public int MaintainedIndicatorCount { get; set; }
}

/// <summary>
/// Guidance direction.
/// </summary>
public enum GuidanceDirection
{
    Raised,
    Maintained,
    Lowered,
    NotProvided
}

/// <summary>
/// Comparison of earnings sentiment.
/// </summary>
public sealed class EarningsSentimentComparison
{
    public EarningsCallAnalysis CurrentAnalysis { get; set; } = new();
    public EarningsCallAnalysis? PreviousAnalysis { get; set; }
    public double SentimentChange { get; set; }
    public double HedgingChange { get; set; }
    public double ConfidenceChange { get; set; }
    public double HistoricalAverageSentiment { get; set; }
    public double DeviationFromAverage { get; set; }
    public TrendDirection TrendDirection { get; set; }
    public IReadOnlyList<string> NotableChanges { get; set; } = [];
}

/// <summary>
/// Sentiment progression through the call.
/// </summary>
public sealed class CallSentimentProgression
{
    public IReadOnlyList<SegmentSentiment> Segments { get; set; } = [];
    public double StartSentiment { get; set; }
    public double EndSentiment { get; set; }
    public double SentimentDrift { get; set; }
    public int PeakSegmentIndex { get; set; }
    public int ValleySegmentIndex { get; set; }
    public double SentimentVolatility { get; set; }
}

/// <summary>
/// Sentiment for a segment of the call.
/// </summary>
public sealed class SegmentSentiment
{
    public int SegmentIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Score { get; set; }
    public SentimentScore Sentiment { get; set; }
}

/// <summary>
/// Analysis of analyst questions.
/// </summary>
public sealed class AnalystQuestionsAnalysis
{
    public int TotalQuestions { get; set; }
    public IReadOnlyList<AnalystQuestionAnalysis> Questions { get; set; } = [];
    public double AverageSentiment { get; set; }
    public Dictionary<string, int> TopicFrequency { get; set; } = [];
    public Dictionary<QuestionTone, int> ToneDistribution { get; set; } = [];
    public IReadOnlyList<string> MostCommonTopics { get; set; } = [];
}

/// <summary>
/// Analysis of a single analyst question.
/// </summary>
public sealed class AnalystQuestionAnalysis
{
    public string AnalystName { get; set; } = string.Empty;
    public string Firm { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public double Sentiment { get; set; }
    public IReadOnlyList<string> Topics { get; set; } = [];
    public QuestionTone Tone { get; set; }
}

/// <summary>
/// Tone of analyst questions.
/// </summary>
public enum QuestionTone
{
    Supportive,
    Neutral,
    Probing,
    Skeptical
}

// Internal types
internal sealed class TranscriptSections
{
    public string PreparedRemarks { get; set; } = string.Empty;
    public string QAndA { get; set; } = string.Empty;
}

internal sealed class SectionAnalysis
{
    public double Score { get; set; }
    public SentimentScore Sentiment { get; set; }
    public int PositiveWordCount { get; set; }
    public int NegativeWordCount { get; set; }
}

internal enum SectionType
{
    PreparedRemarks,
    QAndA,
    General
}

internal sealed class AnalystQuestion
{
    public string AnalystName { get; set; } = string.Empty;
    public string Firm { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
