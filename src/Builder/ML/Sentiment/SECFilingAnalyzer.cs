namespace OoplesFinance.StockIndicators.Builder.ML.Sentiment;

/// <summary>
/// Analyzes sentiment and risk factors from SEC filings (10-K, 10-Q, 8-K, etc.).
/// Extracts key changes, risk factor analysis, and forward-looking language.
/// </summary>
public sealed class SECFilingAnalyzer
{
    private readonly SECFilingOptions _options;
    private readonly Dictionary<string, double> _riskLexicon;
    private readonly Dictionary<string, double> _legalLexicon;
    private readonly HashSet<string> _materialChangeIndicators;

    /// <summary>
    /// Initializes a new instance of the SECFilingAnalyzer.
    /// </summary>
    public SECFilingAnalyzer(SECFilingOptions? options = null)
    {
        _options = options ?? new SECFilingOptions();
        _riskLexicon = BuildRiskLexicon();
        _legalLexicon = BuildLegalLexicon();
        _materialChangeIndicators = BuildMaterialChangeIndicators();
    }

    /// <summary>
    /// Analyzes a complete SEC filing.
    /// </summary>
    public SECFilingAnalysis AnalyzeFiling(SECFiling filing)
    {
        // Extract sections based on filing type
        var sections = ExtractSections(filing);

        // Analyze risk factors section
        var riskAnalysis = AnalyzeRiskFactors(sections.RiskFactors);

        // Analyze MD&A section
        var mdaAnalysis = AnalyzeMDA(sections.MDA);

        // Analyze legal proceedings
        var legalAnalysis = AnalyzeLegalProceedings(sections.LegalProceedings);

        // Calculate overall sentiment
        var overallSentiment = CalculateOverallSentiment(riskAnalysis, mdaAnalysis, legalAnalysis);

        // Extract forward-looking statements
        var forwardLooking = ExtractForwardLookingStatements(filing.Content);

        // Detect material changes
        var materialChanges = DetectMaterialChanges(filing.Content);

        // Calculate readability metrics
        var readability = CalculateReadabilityMetrics(filing.Content);

        // Analyze tone
        var tone = AnalyzeTone(filing.Content);

        return new SECFilingAnalysis
        {
            Symbol = filing.Symbol,
            FilingType = filing.FilingType,
            FilingDate = filing.FilingDate,
            Period = filing.Period,
            OverallSentiment = ClassifySentiment(overallSentiment),
            SentimentScore = overallSentiment,
            RiskFactorAnalysis = riskAnalysis,
            MDAAnalysis = mdaAnalysis,
            LegalProceedingsAnalysis = legalAnalysis,
            ForwardLookingStatements = forwardLooking,
            MaterialChanges = materialChanges,
            ReadabilityMetrics = readability,
            ToneAnalysis = tone,
            Confidence = CalculateConfidence(sections, filing)
        };
    }

    /// <summary>
    /// Compares current filing to previous filing of the same type.
    /// </summary>
    public FilingComparison CompareToPrevious(SECFiling current, SECFiling previous)
    {
        var currentAnalysis = AnalyzeFiling(current);
        var previousAnalysis = AnalyzeFiling(previous);

        // Identify added/removed risk factors
        var addedRisks = IdentifyAddedRisks(
            previousAnalysis.RiskFactorAnalysis.RiskFactors,
            currentAnalysis.RiskFactorAnalysis.RiskFactors);

        var removedRisks = IdentifyRemovedRisks(
            previousAnalysis.RiskFactorAnalysis.RiskFactors,
            currentAnalysis.RiskFactorAnalysis.RiskFactors);

        var modifiedRisks = IdentifyModifiedRisks(
            previousAnalysis.RiskFactorAnalysis.RiskFactors,
            currentAnalysis.RiskFactorAnalysis.RiskFactors);

        // Calculate changes
        var sentimentChange = currentAnalysis.SentimentScore - previousAnalysis.SentimentScore;
        var riskScoreChange = currentAnalysis.RiskFactorAnalysis.OverallRiskScore -
                            previousAnalysis.RiskFactorAnalysis.OverallRiskScore;

        // Detect significant changes
        var significantChanges = DetectSignificantChanges(currentAnalysis, previousAnalysis);

        // Calculate text similarity
        var similarityScore = CalculateTextSimilarity(current.Content, previous.Content);

        return new FilingComparison
        {
            CurrentAnalysis = currentAnalysis,
            PreviousAnalysis = previousAnalysis,
            SentimentChange = sentimentChange,
            RiskScoreChange = riskScoreChange,
            AddedRiskFactors = addedRisks,
            RemovedRiskFactors = removedRisks,
            ModifiedRiskFactors = modifiedRisks,
            SignificantChanges = significantChanges,
            TextSimilarityScore = similarityScore,
            IsSubstantiallyDifferent = similarityScore < _options.SubstantialDifferenceThreshold
        };
    }

    /// <summary>
    /// Analyzes trends across multiple filings.
    /// </summary>
    public FilingTrendAnalysis AnalyzeTrend(IReadOnlyList<SECFiling> filings)
    {
        if (filings.Count == 0)
        {
            return new FilingTrendAnalysis { Filings = [] };
        }

        var sortedFilings = filings.OrderBy(f => f.FilingDate).ToList();
        var analyses = sortedFilings.Select(AnalyzeFiling).ToList();

        // Calculate trends
        var sentimentTrend = CalculateTrend(analyses.Select(a => a.SentimentScore).ToList());
        var riskTrend = CalculateTrend(analyses.Select(a => a.RiskFactorAnalysis.OverallRiskScore).ToList());
        var readabilityTrend = CalculateTrend(analyses.Select(a => a.ReadabilityMetrics.FleschKincaidGrade).ToList());

        // Track risk factor evolution
        var riskEvolution = TrackRiskFactorEvolution(analyses);

        // Identify persistent concerns
        var persistentConcerns = IdentifyPersistentConcerns(analyses);

        return new FilingTrendAnalysis
        {
            Filings = analyses,
            SentimentTrend = sentimentTrend,
            RiskTrend = riskTrend,
            ReadabilityTrend = readabilityTrend,
            RiskFactorEvolution = riskEvolution,
            PersistentConcerns = persistentConcerns
        };
    }

    /// <summary>
    /// Extracts insider transactions from Form 4 filings.
    /// </summary>
    public InsiderActivityAnalysis AnalyzeInsiderActivity(IReadOnlyList<Form4Filing> form4s)
    {
        if (form4s.Count == 0)
        {
            return new InsiderActivityAnalysis { Transactions = [] };
        }

        var totalBuyValue = form4s.Where(f => f.TransactionType == TransactionType.Buy)
            .Sum(f => f.Shares * f.PricePerShare);
        var totalSellValue = form4s.Where(f => f.TransactionType == TransactionType.Sell)
            .Sum(f => f.Shares * f.PricePerShare);

        var netActivity = totalBuyValue - totalSellValue;
        var buyCount = form4s.Count(f => f.TransactionType == TransactionType.Buy);
        var sellCount = form4s.Count(f => f.TransactionType == TransactionType.Sell);

        // Identify significant transactions
        var significantTransactions = form4s
            .Where(f => f.Shares * f.PricePerShare >= _options.SignificantTransactionThreshold)
            .OrderByDescending(f => f.Shares * f.PricePerShare)
            .ToList();

        // Analyze by insider type
        var activityByType = form4s
            .GroupBy(f => f.InsiderType)
            .ToDictionary(
                g => g.Key,
                g => new InsiderTypeActivity
                {
                    InsiderType = g.Key,
                    BuyValue = g.Where(t => t.TransactionType == TransactionType.Buy).Sum(t => t.Shares * t.PricePerShare),
                    SellValue = g.Where(t => t.TransactionType == TransactionType.Sell).Sum(t => t.Shares * t.PricePerShare),
                    TransactionCount = g.Count()
                });

        // Determine sentiment
        var sentiment = InsiderSentiment.Neutral;
        if (netActivity > _options.BullishNetActivityThreshold)
            sentiment = InsiderSentiment.Bullish;
        else if (netActivity < -_options.BearishNetActivityThreshold)
            sentiment = InsiderSentiment.Bearish;

        return new InsiderActivityAnalysis
        {
            Transactions = form4s.ToList(),
            TotalBuyValue = totalBuyValue,
            TotalSellValue = totalSellValue,
            NetActivity = netActivity,
            BuyCount = buyCount,
            SellCount = sellCount,
            SignificantTransactions = significantTransactions,
            ActivityByInsiderType = activityByType,
            Sentiment = sentiment
        };
    }

    /// <summary>
    /// Analyzes 8-K filings for material events.
    /// </summary>
    public Form8KAnalysis Analyze8K(SECFiling filing)
    {
        if (filing.FilingType != FilingType.Form8K)
        {
            throw new ArgumentException("Filing must be an 8-K", nameof(filing));
        }

        // Identify event types
        var eventTypes = IdentifyEventTypes(filing.Content);

        // Determine materiality
        var materialityScore = CalculateMaterialityScore(filing.Content, eventTypes);

        // Analyze urgency
        var urgencyScore = CalculateUrgencyScore(filing);

        // Detect specific events
        var detectedEvents = DetectSpecificEvents(filing.Content);

        // Calculate sentiment
        var sentiment = AnalyzeEventSentiment(filing.Content, eventTypes);

        return new Form8KAnalysis
        {
            Symbol = filing.Symbol,
            FilingDate = filing.FilingDate,
            EventTypes = eventTypes,
            DetectedEvents = detectedEvents,
            MaterialityScore = materialityScore,
            UrgencyScore = urgencyScore,
            Sentiment = sentiment,
            IsSignificant = materialityScore >= _options.SignificantMaterialityThreshold
        };
    }

    private FilingSections ExtractSections(SECFiling filing)
    {
        var sections = new FilingSections();
        var content = filing.Content;

        // Extract Risk Factors (Item 1A in 10-K, Item 1A in 10-Q)
        sections.RiskFactors = ExtractSection(content, new[]
        {
            "ITEM 1A", "Item 1A", "Risk Factors", "RISK FACTORS"
        }, new[] { "ITEM 1B", "Item 1B", "ITEM 2", "Item 2" });

        // Extract MD&A (Item 7 in 10-K, Item 2 in 10-Q)
        sections.MDA = ExtractSection(content, new[]
        {
            "ITEM 7", "Item 7", "Management's Discussion", "MANAGEMENT'S DISCUSSION"
        }, new[] { "ITEM 7A", "Item 7A", "ITEM 8", "Item 8" });

        // Extract Legal Proceedings (Item 3)
        sections.LegalProceedings = ExtractSection(content, new[]
        {
            "ITEM 3", "Item 3", "Legal Proceedings", "LEGAL PROCEEDINGS"
        }, new[] { "ITEM 4", "Item 4" });

        return sections;
    }

    private static string ExtractSection(string content, string[] startMarkers, string[] endMarkers)
    {
        var lowerContent = content.ToLowerInvariant();
        var startIndex = -1;

        foreach (var marker in startMarkers)
        {
            var index = lowerContent.IndexOf(marker.ToLowerInvariant(), StringComparison.Ordinal);
            if (index >= 0)
            {
                startIndex = index;
                break;
            }
        }

        if (startIndex < 0)
            return string.Empty;

        var endIndex = content.Length;
        foreach (var marker in endMarkers)
        {
            var index = lowerContent.IndexOf(marker.ToLowerInvariant(), startIndex + 10, StringComparison.Ordinal);
            if (index > startIndex && index < endIndex)
            {
                endIndex = index;
            }
        }

        return content.Substring(startIndex, endIndex - startIndex);
    }

    private RiskFactorAnalysis AnalyzeRiskFactors(string riskFactorsSection)
    {
        if (string.IsNullOrWhiteSpace(riskFactorsSection))
        {
            return new RiskFactorAnalysis { RiskFactors = [] };
        }

        var riskFactors = new List<RiskFactor>();
        var paragraphs = SplitIntoParagraphs(riskFactorsSection);

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length < 100) continue;

            var category = ClassifyRiskCategory(paragraph);
            var severity = CalculateRiskSeverity(paragraph);
            var isNew = DetectIfNewRisk(paragraph);

            riskFactors.Add(new RiskFactor
            {
                Text = paragraph.Length > 500 ? paragraph.Substring(0, 500) + "..." : paragraph,
                Category = category,
                Severity = severity,
                IsNew = isNew
            });
        }

        var overallRiskScore = riskFactors.Count > 0 ?
            riskFactors.Average(r => (int)r.Severity) : 0;

        var categoryBreakdown = riskFactors
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RiskFactorAnalysis
        {
            RiskFactors = riskFactors,
            TotalRiskFactors = riskFactors.Count,
            OverallRiskScore = overallRiskScore,
            CategoryBreakdown = categoryBreakdown,
            HighSeverityCount = riskFactors.Count(r => r.Severity == RiskSeverity.High || r.Severity == RiskSeverity.Critical),
            NewRiskFactorsCount = riskFactors.Count(r => r.IsNew)
        };
    }

    private MDAAnalysis AnalyzeMDA(string mdaSection)
    {
        if (string.IsNullOrWhiteSpace(mdaSection))
        {
            return new MDAAnalysis();
        }

        var words = TokenizeText(mdaSection);
        var positiveCount = 0;
        var negativeCount = 0;

        foreach (var word in words)
        {
            if (_riskLexicon.TryGetValue(word.ToLowerInvariant(), out var score))
            {
                if (score > 0) positiveCount++;
                else negativeCount++;
            }
        }

        var totalSentimentWords = positiveCount + negativeCount;
        var sentiment = totalSentimentWords > 0 ?
            (double)(positiveCount - negativeCount) / totalSentimentWords : 0;

        // Extract key metrics mentioned
        var metrics = ExtractFinancialMetrics(mdaSection);

        // Detect tone
        var tone = AnalyzeMDATone(mdaSection);

        return new MDAAnalysis
        {
            Sentiment = sentiment,
            PositiveWordCount = positiveCount,
            NegativeWordCount = negativeCount,
            KeyMetrics = metrics,
            Tone = tone
        };
    }

    private LegalProceedingsAnalysis AnalyzeLegalProceedings(string legalSection)
    {
        if (string.IsNullOrWhiteSpace(legalSection))
        {
            return new LegalProceedingsAnalysis { Proceedings = [] };
        }

        var proceedings = new List<LegalProceeding>();
        var paragraphs = SplitIntoParagraphs(legalSection);

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length < 50) continue;

            var severity = CalculateLegalSeverity(paragraph);
            var proceedingType = ClassifyProceedingType(paragraph);

            proceedings.Add(new LegalProceeding
            {
                Description = paragraph.Length > 300 ? paragraph.Substring(0, 300) + "..." : paragraph,
                Type = proceedingType,
                Severity = severity
            });
        }

        var materialAmount = EstimateMaterialAmount(legalSection);

        return new LegalProceedingsAnalysis
        {
            Proceedings = proceedings,
            TotalProceedings = proceedings.Count,
            HighSeverityCount = proceedings.Count(p => p.Severity == LegalSeverity.High || p.Severity == LegalSeverity.Material),
            EstimatedMaterialAmount = materialAmount
        };
    }

    private static double CalculateOverallSentiment(
        RiskFactorAnalysis riskAnalysis,
        MDAAnalysis mdaAnalysis,
        LegalProceedingsAnalysis legalAnalysis)
    {
        var riskContribution = -riskAnalysis.OverallRiskScore / 10.0; // Normalize
        var mdaContribution = mdaAnalysis.Sentiment;
        var legalContribution = -legalAnalysis.HighSeverityCount * 0.1;

        return (riskContribution * 0.4 + mdaContribution * 0.4 + legalContribution * 0.2);
    }

    private List<ForwardLookingStatement> ExtractForwardLookingStatements(string content)
    {
        var statements = new List<ForwardLookingStatement>();
        var sentences = SplitIntoSentences(content);

        var indicators = new[] {
            "expect", "anticipate", "believe", "estimate", "plan",
            "project", "forecast", "outlook", "guidance", "may",
            "will", "should", "could", "might", "intend"
        };

        foreach (var sentence in sentences)
        {
            var lowerSentence = sentence.ToLowerInvariant();
            if (indicators.Any(i => lowerSentence.Contains(i)))
            {
                var sentiment = AnalyzeSentenceSentiment(sentence);
                statements.Add(new ForwardLookingStatement
                {
                    Text = sentence.Trim(),
                    Sentiment = sentiment,
                    Timeframe = TimeframeType.Unspecified
                });
            }
        }

        return statements.Take(30).ToList();
    }

    private List<MaterialChange> DetectMaterialChanges(string content)
    {
        var changes = new List<MaterialChange>();
        var lowerContent = content.ToLowerInvariant();

        foreach (var indicator in _materialChangeIndicators)
        {
            if (lowerContent.Contains(indicator))
            {
                var context = ExtractContext(content, indicator);
                var changeType = ClassifyChangeType(indicator);

                changes.Add(new MaterialChange
                {
                    Description = context,
                    Type = changeType,
                    Indicator = indicator
                });
            }
        }

        return changes;
    }

    private ReadabilityMetrics CalculateReadabilityMetrics(string content)
    {
        var sentences = SplitIntoSentences(content);
        var words = TokenizeText(content);

        var avgSentenceLength = sentences.Count > 0 ?
            (double)words.Count / sentences.Count : 0;

        var syllables = words.Sum(CountSyllables);
        var avgSyllablesPerWord = words.Count > 0 ?
            (double)syllables / words.Count : 0;

        // Flesch-Kincaid Grade Level
        var fleschKincaidGrade = 0.39 * avgSentenceLength + 11.8 * avgSyllablesPerWord - 15.59;

        // Flesch Reading Ease
        var fleschReadingEase = 206.835 - 1.015 * avgSentenceLength - 84.6 * avgSyllablesPerWord;

        // Gunning Fog Index
        var complexWords = words.Count(w => CountSyllables(w) >= 3);
        var complexWordPercentage = words.Count > 0 ? (double)complexWords / words.Count * 100 : 0;
        var gunningFog = 0.4 * (avgSentenceLength + complexWordPercentage);

        return new ReadabilityMetrics
        {
            FleschKincaidGrade = fleschKincaidGrade,
            FleschReadingEase = fleschReadingEase,
            GunningFogIndex = gunningFog,
            AverageSentenceLength = avgSentenceLength,
            AverageSyllablesPerWord = avgSyllablesPerWord,
            ComplexWordPercentage = complexWordPercentage
        };
    }

    private FilingToneAnalysis AnalyzeTone(string content)
    {
        var words = TokenizeText(content);
        var legalCount = 0;
        var technicalCount = 0;
        var cautionaryCount = 0;

        foreach (var word in words)
        {
            var lowerWord = word.ToLowerInvariant();

            if (_legalLexicon.ContainsKey(lowerWord))
                legalCount++;

            if (IsTechnicalTerm(lowerWord))
                technicalCount++;

            if (_riskLexicon.TryGetValue(lowerWord, out var score) && score < 0)
                cautionaryCount++;
        }

        var totalWords = words.Count;
        var legalDensity = totalWords > 0 ? (double)legalCount / totalWords : 0;
        var technicalDensity = totalWords > 0 ? (double)technicalCount / totalWords : 0;
        var cautionaryDensity = totalWords > 0 ? (double)cautionaryCount / totalWords : 0;

        return new FilingToneAnalysis
        {
            LegalLanguageDensity = legalDensity,
            TechnicalLanguageDensity = technicalDensity,
            CautionaryLanguageDensity = cautionaryDensity
        };
    }

    private RiskCategory ClassifyRiskCategory(string text)
    {
        var lowerText = text.ToLowerInvariant();

        if (lowerText.Contains("market") || lowerText.Contains("competition") ||
            lowerText.Contains("economic") || lowerText.Contains("industry"))
            return RiskCategory.Market;

        if (lowerText.Contains("regulatory") || lowerText.Contains("compliance") ||
            lowerText.Contains("legal") || lowerText.Contains("litigation"))
            return RiskCategory.Regulatory;

        if (lowerText.Contains("financial") || lowerText.Contains("debt") ||
            lowerText.Contains("liquidity") || lowerText.Contains("credit"))
            return RiskCategory.Financial;

        if (lowerText.Contains("operational") || lowerText.Contains("supply chain") ||
            lowerText.Contains("manufacturing") || lowerText.Contains("production"))
            return RiskCategory.Operational;

        if (lowerText.Contains("cyber") || lowerText.Contains("security") ||
            lowerText.Contains("data breach") || lowerText.Contains("technology"))
            return RiskCategory.Technology;

        if (lowerText.Contains("personnel") || lowerText.Contains("employee") ||
            lowerText.Contains("management") || lowerText.Contains("talent"))
            return RiskCategory.Personnel;

        return RiskCategory.Other;
    }

    private RiskSeverity CalculateRiskSeverity(string text)
    {
        var lowerText = text.ToLowerInvariant();

        var criticalIndicators = new[] { "material adverse", "significant harm", "could fail", "bankruptcy" };
        var highIndicators = new[] { "substantial", "significant", "major", "serious" };
        var mediumIndicators = new[] { "may", "could", "might", "potential" };

        if (criticalIndicators.Any(i => lowerText.Contains(i)))
            return RiskSeverity.Critical;

        if (highIndicators.Any(i => lowerText.Contains(i)))
            return RiskSeverity.High;

        if (mediumIndicators.Any(i => lowerText.Contains(i)))
            return RiskSeverity.Medium;

        return RiskSeverity.Low;
    }

    private static bool DetectIfNewRisk(string text)
    {
        var newIndicators = new[] { "recently", "new", "emerging", "beginning", "started" };
        var lowerText = text.ToLowerInvariant();
        return newIndicators.Any(i => lowerText.Contains(i));
    }

    private LegalSeverity CalculateLegalSeverity(string text)
    {
        var lowerText = text.ToLowerInvariant();

        if (lowerText.Contains("class action") || lowerText.Contains("securities fraud") ||
            lowerText.Contains("material") || lowerText.Contains("criminal"))
            return LegalSeverity.Material;

        if (lowerText.Contains("lawsuit") || lowerText.Contains("litigation") ||
            lowerText.Contains("damages"))
            return LegalSeverity.High;

        if (lowerText.Contains("dispute") || lowerText.Contains("claim"))
            return LegalSeverity.Medium;

        return LegalSeverity.Low;
    }

    private static ProceedingType ClassifyProceedingType(string text)
    {
        var lowerText = text.ToLowerInvariant();

        if (lowerText.Contains("securities") || lowerText.Contains("sec ") ||
            lowerText.Contains("investor"))
            return ProceedingType.Securities;

        if (lowerText.Contains("patent") || lowerText.Contains("intellectual property") ||
            lowerText.Contains("trademark"))
            return ProceedingType.IntellectualProperty;

        if (lowerText.Contains("antitrust") || lowerText.Contains("competition law"))
            return ProceedingType.Antitrust;

        if (lowerText.Contains("employment") || lowerText.Contains("discrimination") ||
            lowerText.Contains("labor"))
            return ProceedingType.Employment;

        if (lowerText.Contains("environmental") || lowerText.Contains("pollution"))
            return ProceedingType.Environmental;

        return ProceedingType.Other;
    }

    private static decimal EstimateMaterialAmount(string text)
    {
        // Simple pattern matching for dollar amounts
        var patterns = new[]
        {
            @"\$(\d+(?:\.\d+)?)\s*(million|billion)",
            @"(\d+(?:\.\d+)?)\s*(million|billion)\s*dollars"
        };

        decimal maxAmount = 0;
        var lowerText = text.ToLowerInvariant();

        // Simplified extraction
        if (lowerText.Contains("billion"))
        {
            maxAmount = 1_000_000_000m;
        }
        else if (lowerText.Contains("million"))
        {
            maxAmount = 10_000_000m;
        }

        return maxAmount;
    }

    private static List<string> ExtractFinancialMetrics(string text)
    {
        var metrics = new List<string>();
        var metricKeywords = new[] {
            "revenue", "net income", "earnings", "cash flow", "margin",
            "eps", "ebitda", "gross profit", "operating income"
        };

        var lowerText = text.ToLowerInvariant();
        foreach (var keyword in metricKeywords)
        {
            if (lowerText.Contains(keyword))
                metrics.Add(keyword);
        }

        return metrics.Distinct().ToList();
    }

    private static MDATone AnalyzeMDATone(string text)
    {
        var lowerText = text.ToLowerInvariant();

        if (lowerText.Contains("exceeded") || lowerText.Contains("record") ||
            lowerText.Contains("strong growth"))
            return MDATone.Optimistic;

        if (lowerText.Contains("challenging") || lowerText.Contains("headwinds") ||
            lowerText.Contains("difficult"))
            return MDATone.Cautious;

        if (lowerText.Contains("decline") || lowerText.Contains("decreased") ||
            lowerText.Contains("weakness"))
            return MDATone.Pessimistic;

        return MDATone.Neutral;
    }

    private double AnalyzeSentenceSentiment(string sentence)
    {
        var words = TokenizeText(sentence);
        var score = 0.0;
        var count = 0;

        foreach (var word in words)
        {
            if (_riskLexicon.TryGetValue(word.ToLowerInvariant(), out var wordScore))
            {
                score += wordScore;
                count++;
            }
        }

        return count > 0 ? score / count : 0;
    }

    private static string ExtractContext(string content, string indicator)
    {
        var index = content.IndexOf(indicator, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return indicator;

        var start = Math.Max(0, index - 50);
        var end = Math.Min(content.Length, index + indicator.Length + 100);

        return content.Substring(start, end - start).Trim();
    }

    private static MaterialChangeType ClassifyChangeType(string indicator)
    {
        var lowerIndicator = indicator.ToLowerInvariant();

        if (lowerIndicator.Contains("acquisition") || lowerIndicator.Contains("merger"))
            return MaterialChangeType.Acquisition;

        if (lowerIndicator.Contains("executive") || lowerIndicator.Contains("ceo") ||
            lowerIndicator.Contains("cfo") || lowerIndicator.Contains("director"))
            return MaterialChangeType.ManagementChange;

        if (lowerIndicator.Contains("restructuring") || lowerIndicator.Contains("reorganization"))
            return MaterialChangeType.Restructuring;

        if (lowerIndicator.Contains("impairment") || lowerIndicator.Contains("writedown"))
            return MaterialChangeType.AssetImpairment;

        return MaterialChangeType.Other;
    }

    private static double CalculateConfidence(FilingSections sections, SECFiling filing)
    {
        var confidence = 0.5;

        // More content = more confidence
        confidence += Math.Min(0.2, filing.Content.Length / 100000.0);

        // All sections present = more confidence
        if (!string.IsNullOrEmpty(sections.RiskFactors)) confidence += 0.1;
        if (!string.IsNullOrEmpty(sections.MDA)) confidence += 0.1;
        if (!string.IsNullOrEmpty(sections.LegalProceedings)) confidence += 0.05;

        return Math.Min(1.0, confidence);
    }

    private static List<RiskFactor> IdentifyAddedRisks(
        IReadOnlyList<RiskFactor> previous,
        IReadOnlyList<RiskFactor> current)
    {
        // Simplified: consider risks with no similar text as "new"
        var added = new List<RiskFactor>();

        foreach (var risk in current)
        {
            var hasSimilar = previous.Any(p =>
                CalculateSimilarity(p.Text, risk.Text) > 0.7);

            if (!hasSimilar)
            {
                added.Add(risk);
            }
        }

        return added;
    }

    private static List<RiskFactor> IdentifyRemovedRisks(
        IReadOnlyList<RiskFactor> previous,
        IReadOnlyList<RiskFactor> current)
    {
        var removed = new List<RiskFactor>();

        foreach (var risk in previous)
        {
            var hasSimilar = current.Any(c =>
                CalculateSimilarity(c.Text, risk.Text) > 0.7);

            if (!hasSimilar)
            {
                removed.Add(risk);
            }
        }

        return removed;
    }

    private static List<ModifiedRiskFactor> IdentifyModifiedRisks(
        IReadOnlyList<RiskFactor> previous,
        IReadOnlyList<RiskFactor> current)
    {
        var modified = new List<ModifiedRiskFactor>();

        foreach (var currentRisk in current)
        {
            var bestMatch = previous
                .Select(p => new { Risk = p, Similarity = CalculateSimilarity(p.Text, currentRisk.Text) })
                .OrderByDescending(x => x.Similarity)
                .FirstOrDefault();

            if (bestMatch != null && bestMatch.Similarity is > 0.5 and < 0.9)
            {
                modified.Add(new ModifiedRiskFactor
                {
                    Previous = bestMatch.Risk,
                    Current = currentRisk,
                    SimilarityScore = bestMatch.Similarity
                });
            }
        }

        return modified;
    }

    private static double CalculateSimilarity(string text1, string text2)
    {
        var words1 = new HashSet<string>(text1.ToLowerInvariant().Split(' '));
        var words2 = new HashSet<string>(text2.ToLowerInvariant().Split(' '));

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0;
    }

    private static double CalculateTextSimilarity(string text1, string text2)
    {
        // Jaccard similarity on word sets
        return CalculateSimilarity(text1, text2);
    }

    private static List<string> DetectSignificantChanges(
        SECFilingAnalysis current,
        SECFilingAnalysis previous)
    {
        var changes = new List<string>();

        if (Math.Abs(current.SentimentScore - previous.SentimentScore) > 0.2)
            changes.Add("Significant sentiment change detected");

        if (current.RiskFactorAnalysis.TotalRiskFactors - previous.RiskFactorAnalysis.TotalRiskFactors >= 3)
            changes.Add("Multiple new risk factors added");

        if (current.RiskFactorAnalysis.HighSeverityCount > previous.RiskFactorAnalysis.HighSeverityCount)
            changes.Add("Increase in high-severity risk factors");

        if (current.LegalProceedingsAnalysis.TotalProceedings > previous.LegalProceedingsAnalysis.TotalProceedings)
            changes.Add("New legal proceedings disclosed");

        if (Math.Abs(current.ReadabilityMetrics.FleschKincaidGrade - previous.ReadabilityMetrics.FleschKincaidGrade) > 2)
            changes.Add("Significant change in document complexity");

        return changes;
    }

    private static TrendResult CalculateTrend(List<double> values)
    {
        if (values.Count < 2)
            return new TrendResult { Direction = TrendDirection.Sideways };

        var firstHalf = values.Take(values.Count / 2).Average();
        var secondHalf = values.Skip(values.Count / 2).Average();
        var change = secondHalf - firstHalf;

        var direction = TrendDirection.Sideways;
        if (change > 0.1) direction = TrendDirection.Improving;
        else if (change < -0.1) direction = TrendDirection.Deteriorating;

        return new TrendResult
        {
            Direction = direction,
            AverageChange = change,
            Values = values
        };
    }

    private static RiskFactorEvolution TrackRiskFactorEvolution(List<SECFilingAnalysis> analyses)
    {
        var evolution = new RiskFactorEvolution
        {
            TotalCountByPeriod = analyses.Select(a => a.RiskFactorAnalysis.TotalRiskFactors).ToList(),
            HighSeverityByPeriod = analyses.Select(a => a.RiskFactorAnalysis.HighSeverityCount).ToList()
        };

        return evolution;
    }

    private static List<string> IdentifyPersistentConcerns(List<SECFilingAnalysis> analyses)
    {
        var concerns = new List<string>();

        // Categories that appear in all filings
        var allCategories = analyses
            .SelectMany(a => a.RiskFactorAnalysis.CategoryBreakdown.Keys)
            .Distinct()
            .ToList();

        foreach (var category in allCategories)
        {
            var appearsInAll = analyses.All(a =>
                a.RiskFactorAnalysis.CategoryBreakdown.ContainsKey(category));

            if (appearsInAll)
            {
                concerns.Add($"Persistent {category} risk factors across all filings");
            }
        }

        return concerns;
    }

    private static List<EventType8K> IdentifyEventTypes(string content)
    {
        var events = new List<EventType8K>();
        var lowerContent = content.ToLowerInvariant();

        if (lowerContent.Contains("item 1.01") || lowerContent.Contains("entry into a material"))
            events.Add(EventType8K.MaterialAgreement);

        if (lowerContent.Contains("item 2.01") || lowerContent.Contains("completion of acquisition"))
            events.Add(EventType8K.AcquisitionDisposition);

        if (lowerContent.Contains("item 2.02") || lowerContent.Contains("results of operations"))
            events.Add(EventType8K.ResultsOfOperations);

        if (lowerContent.Contains("item 5.02") || lowerContent.Contains("departure of directors"))
            events.Add(EventType8K.ManagementChange);

        if (lowerContent.Contains("item 7.01") || lowerContent.Contains("regulation fd"))
            events.Add(EventType8K.RegulationFDDisclosure);

        if (lowerContent.Contains("item 8.01") || lowerContent.Contains("other events"))
            events.Add(EventType8K.OtherEvents);

        return events;
    }

    private double CalculateMaterialityScore(string content, List<EventType8K> eventTypes)
    {
        var score = 0.0;

        // Weight by event type
        if (eventTypes.Contains(EventType8K.AcquisitionDisposition)) score += 0.4;
        if (eventTypes.Contains(EventType8K.ManagementChange)) score += 0.3;
        if (eventTypes.Contains(EventType8K.MaterialAgreement)) score += 0.3;
        if (eventTypes.Contains(EventType8K.ResultsOfOperations)) score += 0.2;

        // Check for material language
        var lowerContent = content.ToLowerInvariant();
        if (lowerContent.Contains("material")) score += 0.2;
        if (lowerContent.Contains("significant")) score += 0.1;

        return Math.Min(1.0, score);
    }

    private double CalculateUrgencyScore(SECFiling filing)
    {
        // Calculate based on filing timing
        var dayOfWeek = filing.FilingDate.DayOfWeek;
        var hour = filing.FilingDate.Hour;

        var urgency = 0.5;

        // Friday evening filings often indicate bad news
        if (dayOfWeek == DayOfWeek.Friday && hour >= 16)
            urgency += 0.3;

        // After market hours
        if (hour >= 16 || hour < 9)
            urgency += 0.1;

        return Math.Min(1.0, urgency);
    }

    private static List<DetectedEvent> DetectSpecificEvents(string content)
    {
        var events = new List<DetectedEvent>();
        var lowerContent = content.ToLowerInvariant();

        var eventPatterns = new Dictionary<string, string>
        {
            { "CEO departure", "chief executive officer" },
            { "CFO departure", "chief financial officer" },
            { "Acquisition announced", "acquisition" },
            { "Merger announced", "merger" },
            { "Restructuring", "restructuring" },
            { "Layoffs", "workforce reduction" },
            { "Earnings announcement", "earnings" },
            { "Dividend declaration", "dividend" },
            { "Stock buyback", "repurchase" }
        };

        foreach (var pattern in eventPatterns)
        {
            if (lowerContent.Contains(pattern.Value))
            {
                events.Add(new DetectedEvent
                {
                    EventName = pattern.Key,
                    Detected = true
                });
            }
        }

        return events;
    }

    private double AnalyzeEventSentiment(string content, List<EventType8K> eventTypes)
    {
        var baseSentiment = AnalyzeSentenceSentiment(content);

        // Adjust based on event types
        if (eventTypes.Contains(EventType8K.ManagementChange))
            baseSentiment -= 0.1; // Management changes often negative

        if (eventTypes.Contains(EventType8K.AcquisitionDisposition))
            baseSentiment += 0.1; // Acquisitions often positive

        return baseSentiment;
    }

    private static bool IsTechnicalTerm(string word)
    {
        var technicalTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ebitda", "gaap", "non-gaap", "amortization", "depreciation",
            "goodwill", "impairment", "diluted", "dilution", "covenant",
            "leverage", "liquidity", "derivative", "hedging"
        };

        return technicalTerms.Contains(word);
    }

    private static int CountSyllables(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;

        var vowels = "aeiouAEIOU";
        var count = 0;
        var prevVowel = false;

        foreach (var c in word)
        {
            var isVowel = vowels.Contains(c);
            if (isVowel && !prevVowel)
            {
                count++;
            }
            prevVowel = isVowel;
        }

        // Adjust for silent 'e'
        if (word.EndsWith("e", StringComparison.OrdinalIgnoreCase) && count > 1)
            count--;

        return Math.Max(1, count);
    }

    private static List<string> SplitIntoParagraphs(string content)
    {
        return content.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 20)
            .ToList();
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

    private static SentimentScore ClassifySentiment(double score)
    {
        return score switch
        {
            > 0.3 => SentimentScore.VeryPositive,
            > 0.1 => SentimentScore.Positive,
            < -0.3 => SentimentScore.VeryNegative,
            < -0.1 => SentimentScore.Negative,
            _ => SentimentScore.Neutral
        };
    }

    private Dictionary<string, double> BuildRiskLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            // Positive
            { "growth", 0.5 }, { "opportunity", 0.4 }, { "strength", 0.5 },
            { "improve", 0.4 }, { "benefit", 0.4 }, { "advantage", 0.4 },

            // Negative / Risk
            { "risk", -0.3 }, { "uncertainty", -0.4 }, { "adverse", -0.6 },
            { "decline", -0.5 }, { "loss", -0.5 }, { "failure", -0.6 },
            { "impairment", -0.5 }, { "litigation", -0.4 }, { "lawsuit", -0.5 },
            { "volatility", -0.3 }, { "challenge", -0.3 }, { "threat", -0.5 },
            { "default", -0.6 }, { "bankruptcy", -0.8 }, { "fraud", -0.8 },
            { "violation", -0.5 }, { "penalty", -0.4 }, { "fine", -0.4 }
        };
    }

    private Dictionary<string, double> BuildLegalLexicon()
    {
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "pursuant", 1.0 }, { "herein", 1.0 }, { "thereof", 1.0 },
            { "hereby", 1.0 }, { "whereas", 1.0 }, { "notwithstanding", 1.0 },
            { "covenant", 1.0 }, { "indemnify", 1.0 }, { "liability", 1.0 },
            { "provision", 0.8 }, { "compliance", 0.8 }, { "regulation", 0.8 }
        };
    }

    private HashSet<string> BuildMaterialChangeIndicators()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "material change", "significant change", "material modification",
            "acquisition", "merger", "divestiture", "restructuring",
            "impairment", "writedown", "goodwill impairment",
            "executive departure", "ceo", "cfo", "director resignation",
            "material weakness", "restatement", "going concern"
        };
    }
}

/// <summary>
/// Options for SEC filing analysis.
/// </summary>
public sealed class SECFilingOptions
{
    /// <summary>Threshold for substantial text difference.</summary>
    public double SubstantialDifferenceThreshold { get; set; } = 0.5;

    /// <summary>Threshold for significant insider transaction.</summary>
    public decimal SignificantTransactionThreshold { get; set; } = 100_000m;

    /// <summary>Net activity threshold for bullish sentiment.</summary>
    public decimal BullishNetActivityThreshold { get; set; } = 500_000m;

    /// <summary>Net activity threshold for bearish sentiment.</summary>
    public decimal BearishNetActivityThreshold { get; set; } = 500_000m;

    /// <summary>Threshold for significant 8-K materiality.</summary>
    public double SignificantMaterialityThreshold { get; set; } = 0.5;
}

/// <summary>
/// Represents an SEC filing.
/// </summary>
public sealed class SECFiling
{
    public string Symbol { get; set; } = string.Empty;
    public FilingType FilingType { get; set; }
    public DateTime FilingDate { get; set; }
    public string Period { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// SEC filing types.
/// </summary>
public enum FilingType
{
    Form10K,
    Form10Q,
    Form8K,
    Form4,
    FormS1,
    FormDEF14A,
    Other
}

/// <summary>
/// Complete SEC filing analysis.
/// </summary>
public sealed class SECFilingAnalysis
{
    public string Symbol { get; set; } = string.Empty;
    public FilingType FilingType { get; set; }
    public DateTime FilingDate { get; set; }
    public string Period { get; set; } = string.Empty;
    public SentimentScore OverallSentiment { get; set; }
    public double SentimentScore { get; set; }
    public RiskFactorAnalysis RiskFactorAnalysis { get; set; } = new();
    public MDAAnalysis MDAAnalysis { get; set; } = new();
    public LegalProceedingsAnalysis LegalProceedingsAnalysis { get; set; } = new();
    public IReadOnlyList<ForwardLookingStatement> ForwardLookingStatements { get; set; } = [];
    public IReadOnlyList<MaterialChange> MaterialChanges { get; set; } = [];
    public ReadabilityMetrics ReadabilityMetrics { get; set; } = new();
    public FilingToneAnalysis ToneAnalysis { get; set; } = new();
    public double Confidence { get; set; }
}

/// <summary>
/// Risk factor analysis results.
/// </summary>
public sealed class RiskFactorAnalysis
{
    public IReadOnlyList<RiskFactor> RiskFactors { get; set; } = [];
    public int TotalRiskFactors { get; set; }
    public double OverallRiskScore { get; set; }
    public Dictionary<RiskCategory, int> CategoryBreakdown { get; set; } = [];
    public int HighSeverityCount { get; set; }
    public int NewRiskFactorsCount { get; set; }
}

/// <summary>
/// Individual risk factor.
/// </summary>
public sealed class RiskFactor
{
    public string Text { get; set; } = string.Empty;
    public RiskCategory Category { get; set; }
    public RiskSeverity Severity { get; set; }
    public bool IsNew { get; set; }
}

/// <summary>
/// Risk categories.
/// </summary>
public enum RiskCategory
{
    Market,
    Regulatory,
    Financial,
    Operational,
    Technology,
    Personnel,
    Other
}

/// <summary>
/// Risk severity levels.
/// </summary>
public enum RiskSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// MD&A analysis results.
/// </summary>
public sealed class MDAAnalysis
{
    public double Sentiment { get; set; }
    public int PositiveWordCount { get; set; }
    public int NegativeWordCount { get; set; }
    public IReadOnlyList<string> KeyMetrics { get; set; } = [];
    public MDATone Tone { get; set; }
}

/// <summary>
/// MD&A tone classification.
/// </summary>
public enum MDATone
{
    Optimistic,
    Neutral,
    Cautious,
    Pessimistic
}

/// <summary>
/// Legal proceedings analysis.
/// </summary>
public sealed class LegalProceedingsAnalysis
{
    public IReadOnlyList<LegalProceeding> Proceedings { get; set; } = [];
    public int TotalProceedings { get; set; }
    public int HighSeverityCount { get; set; }
    public decimal EstimatedMaterialAmount { get; set; }
}

/// <summary>
/// Individual legal proceeding.
/// </summary>
public sealed class LegalProceeding
{
    public string Description { get; set; } = string.Empty;
    public ProceedingType Type { get; set; }
    public LegalSeverity Severity { get; set; }
}

/// <summary>
/// Types of legal proceedings.
/// </summary>
public enum ProceedingType
{
    Securities,
    IntellectualProperty,
    Antitrust,
    Employment,
    Environmental,
    Other
}

/// <summary>
/// Legal proceeding severity.
/// </summary>
public enum LegalSeverity
{
    Low,
    Medium,
    High,
    Material
}

/// <summary>
/// Material change detected in filing.
/// </summary>
public sealed class MaterialChange
{
    public string Description { get; set; } = string.Empty;
    public MaterialChangeType Type { get; set; }
    public string Indicator { get; set; } = string.Empty;
}

/// <summary>
/// Types of material changes.
/// </summary>
public enum MaterialChangeType
{
    Acquisition,
    ManagementChange,
    Restructuring,
    AssetImpairment,
    Other
}

/// <summary>
/// Readability metrics.
/// </summary>
public sealed class ReadabilityMetrics
{
    public double FleschKincaidGrade { get; set; }
    public double FleschReadingEase { get; set; }
    public double GunningFogIndex { get; set; }
    public double AverageSentenceLength { get; set; }
    public double AverageSyllablesPerWord { get; set; }
    public double ComplexWordPercentage { get; set; }
}

/// <summary>
/// Filing tone analysis.
/// </summary>
public sealed class FilingToneAnalysis
{
    public double LegalLanguageDensity { get; set; }
    public double TechnicalLanguageDensity { get; set; }
    public double CautionaryLanguageDensity { get; set; }
}

/// <summary>
/// Comparison of two filings.
/// </summary>
public sealed class FilingComparison
{
    public SECFilingAnalysis CurrentAnalysis { get; set; } = new();
    public SECFilingAnalysis PreviousAnalysis { get; set; } = new();
    public double SentimentChange { get; set; }
    public double RiskScoreChange { get; set; }
    public IReadOnlyList<RiskFactor> AddedRiskFactors { get; set; } = [];
    public IReadOnlyList<RiskFactor> RemovedRiskFactors { get; set; } = [];
    public IReadOnlyList<ModifiedRiskFactor> ModifiedRiskFactors { get; set; } = [];
    public IReadOnlyList<string> SignificantChanges { get; set; } = [];
    public double TextSimilarityScore { get; set; }
    public bool IsSubstantiallyDifferent { get; set; }
}

/// <summary>
/// Modified risk factor with comparison.
/// </summary>
public sealed class ModifiedRiskFactor
{
    public RiskFactor Previous { get; set; } = new();
    public RiskFactor Current { get; set; } = new();
    public double SimilarityScore { get; set; }
}

/// <summary>
/// Filing trend analysis.
/// </summary>
public sealed class FilingTrendAnalysis
{
    public IReadOnlyList<SECFilingAnalysis> Filings { get; set; } = [];
    public TrendResult SentimentTrend { get; set; } = new();
    public TrendResult RiskTrend { get; set; } = new();
    public TrendResult ReadabilityTrend { get; set; } = new();
    public RiskFactorEvolution RiskFactorEvolution { get; set; } = new();
    public IReadOnlyList<string> PersistentConcerns { get; set; } = [];
}

/// <summary>
/// Trend calculation result.
/// </summary>
public sealed class TrendResult
{
    public TrendDirection Direction { get; set; }
    public double AverageChange { get; set; }
    public IReadOnlyList<double> Values { get; set; } = [];
}

/// <summary>
/// Risk factor evolution over time.
/// </summary>
public sealed class RiskFactorEvolution
{
    public IReadOnlyList<int> TotalCountByPeriod { get; set; } = [];
    public IReadOnlyList<int> HighSeverityByPeriod { get; set; } = [];
}

/// <summary>
/// Form 4 filing for insider transactions.
/// </summary>
public sealed class Form4Filing
{
    public string Symbol { get; set; } = string.Empty;
    public string InsiderName { get; set; } = string.Empty;
    public InsiderType InsiderType { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal Shares { get; set; }
    public decimal PricePerShare { get; set; }
    public DateTime TransactionDate { get; set; }
}

/// <summary>
/// Insider types.
/// </summary>
public enum InsiderType
{
    CEO,
    CFO,
    Director,
    Officer,
    TenPercentOwner,
    Other
}

/// <summary>
/// Transaction types.
/// </summary>
public enum TransactionType
{
    Buy,
    Sell,
    Exercise,
    Gift,
    Other
}

/// <summary>
/// Insider activity analysis.
/// </summary>
public sealed class InsiderActivityAnalysis
{
    public IReadOnlyList<Form4Filing> Transactions { get; set; } = [];
    public decimal TotalBuyValue { get; set; }
    public decimal TotalSellValue { get; set; }
    public decimal NetActivity { get; set; }
    public int BuyCount { get; set; }
    public int SellCount { get; set; }
    public IReadOnlyList<Form4Filing> SignificantTransactions { get; set; } = [];
    public Dictionary<InsiderType, InsiderTypeActivity> ActivityByInsiderType { get; set; } = [];
    public InsiderSentiment Sentiment { get; set; }
}

/// <summary>
/// Activity by insider type.
/// </summary>
public sealed class InsiderTypeActivity
{
    public InsiderType InsiderType { get; set; }
    public decimal BuyValue { get; set; }
    public decimal SellValue { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Insider sentiment classification.
/// </summary>
public enum InsiderSentiment
{
    Bullish,
    Neutral,
    Bearish
}

/// <summary>
/// Form 8-K analysis.
/// </summary>
public sealed class Form8KAnalysis
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime FilingDate { get; set; }
    public IReadOnlyList<EventType8K> EventTypes { get; set; } = [];
    public IReadOnlyList<DetectedEvent> DetectedEvents { get; set; } = [];
    public double MaterialityScore { get; set; }
    public double UrgencyScore { get; set; }
    public double Sentiment { get; set; }
    public bool IsSignificant { get; set; }
}

/// <summary>
/// 8-K event types.
/// </summary>
public enum EventType8K
{
    MaterialAgreement,
    AcquisitionDisposition,
    ResultsOfOperations,
    ManagementChange,
    RegulationFDDisclosure,
    OtherEvents
}

/// <summary>
/// Detected event in filing.
/// </summary>
public sealed class DetectedEvent
{
    public string EventName { get; set; } = string.Empty;
    public bool Detected { get; set; }
}

// Internal types
internal sealed class FilingSections
{
    public string RiskFactors { get; set; } = string.Empty;
    public string MDA { get; set; } = string.Empty;
    public string LegalProceedings { get; set; } = string.Empty;
}
