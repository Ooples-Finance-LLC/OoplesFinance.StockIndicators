namespace OoplesFinance.StockIndicators.Builder.AI.Explainability;

/// <summary>
/// User skill levels for adaptive UI and explanations.
/// </summary>
public enum UserSkillLevel
{
    Beginner,
    Intermediate,
    Expert
}

/// <summary>
/// Generates human-readable explanations for AI trading decisions.
/// Adapts explanation complexity based on user skill level.
/// </summary>
public interface IAIExplainer
{
    /// <summary>Generates an explanation for a trading decision.</summary>
    Task<TradingExplanation> ExplainDecisionAsync(
        TradingDecision decision,
        UserSkillLevel skillLevel,
        CancellationToken ct = default);

    /// <summary>Generates an explanation for portfolio allocation.</summary>
    Task<AllocationExplanation> ExplainAllocationAsync(
        PortfolioAllocation allocation,
        UserSkillLevel skillLevel,
        CancellationToken ct = default);

    /// <summary>Generates an explanation for risk assessment.</summary>
    Task<RiskExplanation> ExplainRiskAsync(
        RiskAssessment risk,
        UserSkillLevel skillLevel,
        CancellationToken ct = default);

    /// <summary>Answers a follow-up question about a decision.</summary>
    Task<string> AnswerFollowUpAsync(
        string questionId,
        string question,
        UserSkillLevel skillLevel,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of the AI explainer with skill-level adaptation.
/// </summary>
public sealed class AIExplainer : IAIExplainer
{
    private readonly Dictionary<string, TradingDecision> _recentDecisions = new();

    public Task<TradingExplanation> ExplainDecisionAsync(
        TradingDecision decision,
        UserSkillLevel skillLevel,
        CancellationToken ct = default)
    {
        // Store for follow-up questions
        _recentDecisions[decision.DecisionId] = decision;

        var explanation = skillLevel switch
        {
            UserSkillLevel.Beginner => GenerateBeginnerExplanation(decision),
            UserSkillLevel.Intermediate => GenerateIntermediateExplanation(decision),
            UserSkillLevel.Expert => GenerateExpertExplanation(decision),
            _ => GenerateBeginnerExplanation(decision)
        };

        return Task.FromResult(explanation);
    }

    public Task<AllocationExplanation> ExplainAllocationAsync(
        PortfolioAllocation allocation,
        UserSkillLevel skillLevel,
        CancellationToken ct = default)
    {
        var explanation = skillLevel switch
        {
            UserSkillLevel.Beginner => GenerateBeginnerAllocationExplanation(allocation),
            UserSkillLevel.Intermediate => GenerateIntermediateAllocationExplanation(allocation),
            UserSkillLevel.Expert => GenerateExpertAllocationExplanation(allocation),
            _ => GenerateBeginnerAllocationExplanation(allocation)
        };

        return Task.FromResult(explanation);
    }

    public Task<RiskExplanation> ExplainRiskAsync(
        RiskAssessment risk,
        UserSkillLevel skillLevel,
        CancellationToken ct = default)
    {
        var explanation = skillLevel switch
        {
            UserSkillLevel.Beginner => GenerateBeginnerRiskExplanation(risk),
            UserSkillLevel.Intermediate => GenerateIntermediateRiskExplanation(risk),
            UserSkillLevel.Expert => GenerateExpertRiskExplanation(risk),
            _ => GenerateBeginnerRiskExplanation(risk)
        };

        return Task.FromResult(explanation);
    }

    public Task<string> AnswerFollowUpAsync(
        string decisionId,
        string question,
        UserSkillLevel skillLevel,
        CancellationToken ct = default)
    {
        if (!_recentDecisions.TryGetValue(decisionId, out var decision))
        {
            return Task.FromResult("I don't have information about that decision. Please ask about a more recent trade.");
        }

        // Simple pattern matching for common questions
        var questionLower = question.ToLowerInvariant();

        if (questionLower.Contains("sentiment") || questionLower.Contains("news"))
        {
            return Task.FromResult(ExplainSentiment(decision, skillLevel));
        }

        if (questionLower.Contains("technical") || questionLower.Contains("indicator"))
        {
            return Task.FromResult(ExplainTechnicals(decision, skillLevel));
        }

        if (questionLower.Contains("risk") || questionLower.Contains("lose"))
        {
            return Task.FromResult(ExplainRiskDetails(decision, skillLevel));
        }

        if (questionLower.Contains("why") && questionLower.Contains("now"))
        {
            return Task.FromResult(ExplainTiming(decision, skillLevel));
        }

        return Task.FromResult(GetGenericResponse(skillLevel));
    }

    #region Beginner Explanations

    private TradingExplanation GenerateBeginnerExplanation(TradingDecision decision)
    {
        var actionWord = decision.Action == TradeAction.Buy ? "bought" : "sold";
        var reasonSimple = GetSimpleReason(decision);

        return new TradingExplanation
        {
            DecisionId = decision.DecisionId,
            SkillLevel = UserSkillLevel.Beginner,
            Summary = $"We {actionWord} {decision.Symbol} because {reasonSimple}.",
            Details = new List<ExplanationDetail>
            {
                new()
                {
                    Category = "What happened",
                    Text = $"The AI decided to {decision.Action.ToString().ToLower()} {decision.Quantity} shares of {decision.Symbol}.",
                    Importance = ExplanationImportance.High
                },
                new()
                {
                    Category = "Why",
                    Text = reasonSimple,
                    Importance = ExplanationImportance.High
                },
                new()
                {
                    Category = "What this means for you",
                    Text = GetSimpleImpact(decision),
                    Importance = ExplanationImportance.Medium
                }
            },
            ConfidenceDescription = GetConfidenceDescription(decision.Confidence, UserSkillLevel.Beginner),
            SuggestedFollowUps = new List<string>
            {
                "Why is the news positive?",
                "Is this a safe investment?",
                "What could go wrong?"
            }
        };
    }

    private string GetSimpleReason(TradingDecision decision)
    {
        if (decision.SentimentScore > 0.5)
            return "there's positive news about the company";
        if (decision.SentimentScore < -0.5)
            return "there's concerning news we want to avoid";
        if (decision.TechnicalSignals.Any(s => s.Contains("bullish", StringComparison.OrdinalIgnoreCase)))
            return "the stock is showing signs of going up";
        if (decision.TechnicalSignals.Any(s => s.Contains("bearish", StringComparison.OrdinalIgnoreCase)))
            return "the stock might be going down";
        if (decision.PriceChange > 0)
            return "the price has been trending upward";
        return "our analysis suggests this is a good opportunity";
    }

    private string GetSimpleImpact(TradingDecision decision)
    {
        if (decision.Action == TradeAction.Buy)
            return "You now own more of this stock. If it goes up, you'll make money.";
        return "You've reduced your position. This lowers your risk.";
    }

    private AllocationExplanation GenerateBeginnerAllocationExplanation(PortfolioAllocation allocation)
    {
        var description = allocation.RiskLevel switch
        {
            <= 0.3 => "Your portfolio is set up for safety, focusing on stable investments.",
            <= 0.6 => "Your portfolio is balanced between growth and safety.",
            _ => "Your portfolio is set up for growth, which means higher potential returns but more ups and downs."
        };

        return new AllocationExplanation
        {
            SkillLevel = UserSkillLevel.Beginner,
            Summary = description,
            Allocations = allocation.Holdings.Select(h => new AllocationItem
            {
                AssetName = h.Symbol,
                Percentage = h.Weight * 100,
                Reason = GetSimpleAllocationReason(h)
            }).ToList(),
            RiskDescription = GetSimpleRiskDescription(allocation.RiskLevel),
            ExpectedOutcome = $"Based on history, you might see returns of {allocation.ExpectedReturn:P0} to {allocation.ExpectedReturn * 1.5:P0} per year, but there are no guarantees."
        };
    }

    private string GetSimpleAllocationReason(PortfolioHolding holding)
    {
        if (holding.AssetType == "Stocks")
            return "For growth over time";
        if (holding.AssetType == "Bonds")
            return "For stability and income";
        if (holding.AssetType == "Cash")
            return "For safety and flexibility";
        return "Part of a diversified portfolio";
    }

    private string GetSimpleRiskDescription(double riskLevel)
    {
        if (riskLevel <= 0.3)
            return "Low risk - your money is mostly in safe investments";
        if (riskLevel <= 0.6)
            return "Medium risk - a mix of safe and growth investments";
        return "Higher risk - focused on growth, which can be bumpy";
    }

    private RiskExplanation GenerateBeginnerRiskExplanation(RiskAssessment risk)
    {
        var riskWord = risk.OverallRiskScore switch
        {
            <= 0.3 => "low",
            <= 0.6 => "moderate",
            _ => "high"
        };

        return new RiskExplanation
        {
            SkillLevel = UserSkillLevel.Beginner,
            Summary = $"Your portfolio has {riskWord} risk.",
            WhatItMeans = risk.OverallRiskScore switch
            {
                <= 0.3 => "Your investments are relatively safe. You might see smaller gains, but also smaller losses.",
                <= 0.6 => "You're taking some risk for potential growth. Expect some ups and downs.",
                _ => "You're taking significant risk for higher potential returns. Be prepared for volatility."
            },
            WhatCouldHappen = $"In a bad market, you could temporarily lose up to {Math.Abs(risk.MaxDrawdown):P0} of your investment. But markets historically recover.",
            Recommendations = risk.OverallRiskScore > 0.7
                ? new List<string> { "Consider reducing risk if this feels uncomfortable", "Make sure you won't need this money soon" }
                : new List<string> { "Your risk level matches your goals", "Stay the course during market dips" }
        };
    }

    #endregion

    #region Intermediate Explanations

    private TradingExplanation GenerateIntermediateExplanation(TradingDecision decision)
    {
        var indicators = string.Join(", ", decision.TechnicalSignals.Take(3));

        return new TradingExplanation
        {
            DecisionId = decision.DecisionId,
            SkillLevel = UserSkillLevel.Intermediate,
            Summary = $"{decision.Action} {decision.Symbol}: {indicators}. Sentiment: {GetSentimentLabel(decision.SentimentScore)}.",
            Details = new List<ExplanationDetail>
            {
                new()
                {
                    Category = "Technical Analysis",
                    Text = $"Signals: {string.Join(", ", decision.TechnicalSignals)}",
                    Importance = ExplanationImportance.High
                },
                new()
                {
                    Category = "Sentiment",
                    Text = $"Score: {decision.SentimentScore:+0.00;-0.00} ({GetSentimentLabel(decision.SentimentScore)})",
                    Importance = ExplanationImportance.High
                },
                new()
                {
                    Category = "Price Action",
                    Text = $"24h change: {decision.PriceChange:+0.00%;-0.00%}",
                    Importance = ExplanationImportance.Medium
                },
                new()
                {
                    Category = "Volume",
                    Text = $"Volume vs avg: {decision.VolumeRatio:0.0}x",
                    Importance = ExplanationImportance.Medium
                },
                new()
                {
                    Category = "Risk/Reward",
                    Text = $"Stop loss: {decision.StopLoss:C}, Target: {decision.TakeProfit:C}",
                    Importance = ExplanationImportance.High
                }
            },
            ConfidenceDescription = GetConfidenceDescription(decision.Confidence, UserSkillLevel.Intermediate),
            TechnicalDetails = new TechnicalExplanationDetails
            {
                Indicators = decision.TechnicalSignals.ToList(),
                SentimentScore = decision.SentimentScore,
                VolumeAnalysis = $"{decision.VolumeRatio:0.0}x average volume"
            },
            SuggestedFollowUps = new List<string>
            {
                "What's driving the sentiment score?",
                "Show me the indicator values",
                "Why this position size?"
            }
        };
    }

    private AllocationExplanation GenerateIntermediateAllocationExplanation(PortfolioAllocation allocation)
    {
        return new AllocationExplanation
        {
            SkillLevel = UserSkillLevel.Intermediate,
            Summary = $"Portfolio optimized for {allocation.ExpectedReturn:P1} expected return with {allocation.Volatility:P1} volatility.",
            Allocations = allocation.Holdings.Select(h => new AllocationItem
            {
                AssetName = h.Symbol,
                Percentage = h.Weight * 100,
                Reason = $"Contributes {h.ExpectedContribution:P1} to returns, {h.RiskContribution:P1} to risk"
            }).ToList(),
            RiskDescription = $"Portfolio beta: {allocation.Beta:F2}, Sharpe ratio: {allocation.SharpeRatio:F2}",
            ExpectedOutcome = $"Expected annual return: {allocation.ExpectedReturn:P1}. 95% confidence range: {allocation.ExpectedReturn - 2 * allocation.Volatility:P1} to {allocation.ExpectedReturn + 2 * allocation.Volatility:P1}",
            Metrics = new AllocationMetrics
            {
                SharpeRatio = allocation.SharpeRatio,
                Beta = allocation.Beta,
                Volatility = allocation.Volatility,
                MaxDrawdown = allocation.ExpectedMaxDrawdown
            }
        };
    }

    private RiskExplanation GenerateIntermediateRiskExplanation(RiskAssessment risk)
    {
        return new RiskExplanation
        {
            SkillLevel = UserSkillLevel.Intermediate,
            Summary = $"Risk score: {risk.OverallRiskScore:P0}. VaR (95%): {risk.VaR95:P2}. Max drawdown: {risk.MaxDrawdown:P2}.",
            WhatItMeans = $"At 95% confidence, daily losses should not exceed {Math.Abs(risk.VaR95):P2} of portfolio value.",
            WhatCouldHappen = $"Historical max drawdown: {Math.Abs(risk.MaxDrawdown):P2}. Stress test (2008-like): {Math.Abs(risk.StressTestLoss):P2}.",
            Metrics = new RiskMetrics
            {
                VaR95 = risk.VaR95,
                VaR99 = risk.VaR99,
                CVaR = risk.CVaR,
                Beta = risk.Beta,
                MaxDrawdown = risk.MaxDrawdown
            },
            Recommendations = GenerateIntermediateRecommendations(risk)
        };
    }

    private List<string> GenerateIntermediateRecommendations(RiskAssessment risk)
    {
        var recs = new List<string>();

        if (risk.ConcentrationRisk > 0.3)
            recs.Add($"Position concentration ({risk.ConcentrationRisk:P0}) exceeds recommended limit. Consider diversifying.");

        if (risk.SectorConcentration > 0.4)
            recs.Add($"Sector concentration high ({risk.SectorConcentration:P0}). Consider sector diversification.");

        if (risk.Beta > 1.3)
            recs.Add($"Portfolio beta ({risk.Beta:F2}) indicates high market sensitivity. Consider hedging.");

        if (recs.Count == 0)
            recs.Add("Risk metrics are within acceptable ranges.");

        return recs;
    }

    #endregion

    #region Expert Explanations

    private TradingExplanation GenerateExpertExplanation(TradingDecision decision)
    {
        return new TradingExplanation
        {
            DecisionId = decision.DecisionId,
            SkillLevel = UserSkillLevel.Expert,
            Summary = $"{decision.Action} {decision.Quantity} {decision.Symbol} @ {decision.Price:C} | Conf: {decision.Confidence:P0} | Sent: {decision.SentimentScore:+0.00;-0.00}",
            Details = new List<ExplanationDetail>
            {
                new()
                {
                    Category = "Signal Decomposition",
                    Text = $"Technical: {decision.TechnicalWeight:P0}, Sentiment: {decision.SentimentWeight:P0}, Momentum: {decision.MomentumWeight:P0}",
                    Importance = ExplanationImportance.High
                },
                new()
                {
                    Category = "Feature Importance",
                    Text = string.Join(" | ", decision.FeatureImportance.Select(f => $"{f.Key}: {f.Value:F3}")),
                    Importance = ExplanationImportance.High
                },
                new()
                {
                    Category = "Model Output",
                    Text = $"Raw action: {decision.RawModelOutput:F4}, Policy entropy: {decision.PolicyEntropy:F4}",
                    Importance = ExplanationImportance.Medium
                },
                new()
                {
                    Category = "Risk Parameters",
                    Text = $"Position size: Kelly {decision.KellyFraction:P1} (actual: {decision.ActualFraction:P1}), Sharpe contrib: {decision.ExpectedSharpeContribution:F3}",
                    Importance = ExplanationImportance.High
                }
            },
            ConfidenceDescription = GetConfidenceDescription(decision.Confidence, UserSkillLevel.Expert),
            TechnicalDetails = new TechnicalExplanationDetails
            {
                Indicators = decision.TechnicalSignals.ToList(),
                SentimentScore = decision.SentimentScore,
                VolumeAnalysis = $"Volume ratio: {decision.VolumeRatio:F2}x, VWAP dev: {decision.VwapDeviation:+0.00%;-0.00%}"
            },
            ModelDetails = new ModelExplanationDetails
            {
                ModelType = decision.ModelType,
                PolicyEntropy = decision.PolicyEntropy,
                ValueEstimate = decision.ValueEstimate,
                ActionProbabilities = decision.ActionProbabilities,
                FeatureImportance = decision.FeatureImportance
            },
            SuggestedFollowUps = new List<string>
            {
                "Show attention weights",
                "Compare to baseline model",
                "Run sensitivity analysis"
            }
        };
    }

    private AllocationExplanation GenerateExpertAllocationExplanation(PortfolioAllocation allocation)
    {
        return new AllocationExplanation
        {
            SkillLevel = UserSkillLevel.Expert,
            Summary = $"Mean-variance optimized: E[r]={allocation.ExpectedReturn:P2}, sigma={allocation.Volatility:P2}, Sharpe={allocation.SharpeRatio:F3}",
            Allocations = allocation.Holdings.Select(h => new AllocationItem
            {
                AssetName = h.Symbol,
                Percentage = h.Weight * 100,
                Reason = $"Marginal Sharpe: {h.MarginalSharpe:F3}, Risk parity weight: {h.RiskParityWeight:P1}"
            }).ToList(),
            RiskDescription = $"Ex-ante tracking error: {allocation.TrackingError:P2}, Information ratio: {allocation.InformationRatio:F2}",
            ExpectedOutcome = $"Monte Carlo 5th/50th/95th percentile returns: {allocation.MC5th:P1}/{allocation.MC50th:P1}/{allocation.MC95th:P1}",
            Metrics = new AllocationMetrics
            {
                SharpeRatio = allocation.SharpeRatio,
                SortinoRatio = allocation.SortinoRatio,
                Beta = allocation.Beta,
                Alpha = allocation.Alpha,
                Volatility = allocation.Volatility,
                MaxDrawdown = allocation.ExpectedMaxDrawdown,
                InformationRatio = allocation.InformationRatio,
                TrackingError = allocation.TrackingError
            },
            CorrelationMatrix = allocation.CorrelationMatrix,
            EfficientFrontierPosition = allocation.EfficientFrontierPosition
        };
    }

    private RiskExplanation GenerateExpertRiskExplanation(RiskAssessment risk)
    {
        return new RiskExplanation
        {
            SkillLevel = UserSkillLevel.Expert,
            Summary = $"VaR(95%): {risk.VaR95:P3} | CVaR: {risk.CVaR:P3} | Greeks: Delta={risk.PortfolioDelta:F2}, Gamma={risk.PortfolioGamma:F4}, Vega={risk.PortfolioVega:F2}",
            WhatItMeans = $"Parametric VaR assumes {risk.VaRMethod}. Historical simulation uses {risk.HistoricalWindow} days.",
            WhatCouldHappen = $"Stress scenarios: COVID-like: {risk.StressCovid:P2}, 2008-like: {risk.Stress2008:P2}, Rate shock: {risk.StressRateShock:P2}",
            Metrics = new RiskMetrics
            {
                VaR95 = risk.VaR95,
                VaR99 = risk.VaR99,
                CVaR = risk.CVaR,
                Beta = risk.Beta,
                MaxDrawdown = risk.MaxDrawdown,
                PortfolioDelta = risk.PortfolioDelta,
                PortfolioGamma = risk.PortfolioGamma,
                PortfolioVega = risk.PortfolioVega,
                PortfolioTheta = risk.PortfolioTheta
            },
            Recommendations = GenerateExpertRecommendations(risk),
            RiskDecomposition = risk.RiskDecomposition,
            StressTestResults = risk.StressTestResults
        };
    }

    private List<string> GenerateExpertRecommendations(RiskAssessment risk)
    {
        var recs = new List<string>();

        if (risk.PortfolioGamma < -0.1)
            recs.Add($"Negative gamma ({risk.PortfolioGamma:F4}): Consider buying options for gamma hedging.");

        if (Math.Abs(risk.PortfolioVega) > 100)
            recs.Add($"High vega exposure ({risk.PortfolioVega:F2}): Consider vega-neutral strategies.");

        if (risk.ConcentrationRisk > 0.25)
            recs.Add($"HHI concentration ({risk.ConcentrationRisk:F3}) above threshold. Apply position limits.");

        if (risk.TailRisk > 0.05)
            recs.Add($"Elevated tail risk ({risk.TailRisk:P2}). Consider tail hedging via puts.");

        return recs;
    }

    #endregion

    #region Helper Methods

    private string GetSentimentLabel(double score)
    {
        return score switch
        {
            > 0.5 => "Bullish",
            > 0.2 => "Slightly Bullish",
            > -0.2 => "Neutral",
            > -0.5 => "Slightly Bearish",
            _ => "Bearish"
        };
    }

    private string GetConfidenceDescription(double confidence, UserSkillLevel level)
    {
        return level switch
        {
            UserSkillLevel.Beginner => confidence switch
            {
                > 0.8 => "Very confident in this decision",
                > 0.6 => "Reasonably confident",
                > 0.4 => "Somewhat confident",
                _ => "Less certain, but still sees opportunity"
            },
            UserSkillLevel.Intermediate => $"Confidence: {confidence:P0} ({GetConfidenceCategory(confidence)})",
            UserSkillLevel.Expert => $"Confidence: {confidence:P2}, entropy: {1 - confidence:F4}",
            _ => $"Confidence: {confidence:P0}"
        };
    }

    private string GetConfidenceCategory(double confidence)
    {
        return confidence switch
        {
            > 0.8 => "High",
            > 0.6 => "Medium-High",
            > 0.4 => "Medium",
            > 0.2 => "Low",
            _ => "Very Low"
        };
    }

    private string ExplainSentiment(TradingDecision decision, UserSkillLevel level)
    {
        return level switch
        {
            UserSkillLevel.Beginner => decision.SentimentScore > 0
                ? "The news and social media are mostly positive about this stock right now."
                : "The news and social media are showing some concerns about this stock.",
            UserSkillLevel.Intermediate => $"Sentiment score of {decision.SentimentScore:+0.00;-0.00} is based on {decision.NewsCount} news articles and {decision.SocialMentions} social mentions in the last 24 hours.",
            UserSkillLevel.Expert => $"Sentiment: {decision.SentimentScore:+0.000;-0.000}. News: {decision.NewsSentiment:+0.00;-0.00} ({decision.NewsCount}), Social: {decision.SocialSentiment:+0.00;-0.00} ({decision.SocialMentions}). Weighted by recency and source credibility.",
            _ => $"Sentiment score: {decision.SentimentScore:+0.00;-0.00}"
        };
    }

    private string ExplainTechnicals(TradingDecision decision, UserSkillLevel level)
    {
        var indicators = decision.TechnicalSignals;
        return level switch
        {
            UserSkillLevel.Beginner => indicators.Any()
                ? $"The stock's price patterns suggest it might {(decision.Action == TradeAction.Buy ? "go up" : "go down")} soon."
                : "We looked at price patterns to make this decision.",
            UserSkillLevel.Intermediate => $"Technical signals: {string.Join(", ", indicators)}",
            UserSkillLevel.Expert => $"Technical decomposition: {string.Join(", ", decision.IndicatorValues.Select(iv => $"{iv.Key}={iv.Value:F2}"))}",
            _ => $"Signals: {string.Join(", ", indicators)}"
        };
    }

    private string ExplainRiskDetails(TradingDecision decision, UserSkillLevel level)
    {
        return level switch
        {
            UserSkillLevel.Beginner => $"If things go wrong, the most you could lose on this trade is about {Math.Abs(decision.MaxRisk):C}.",
            UserSkillLevel.Intermediate => $"Stop loss at {decision.StopLoss:C} ({decision.StopLossPercent:P1} below entry). Max loss: {Math.Abs(decision.MaxRisk):C}. Risk/reward: 1:{decision.RewardRatio:F1}",
            UserSkillLevel.Expert => $"Position risk: {decision.PositionVaR:P3} VaR. Kelly: {decision.KellyFraction:P2}. Actual size: {decision.ActualFraction:P2}. Expected Sharpe contribution: {decision.ExpectedSharpeContribution:F4}",
            _ => $"Max risk: {Math.Abs(decision.MaxRisk):C}"
        };
    }

    private string ExplainTiming(TradingDecision decision, UserSkillLevel level)
    {
        return level switch
        {
            UserSkillLevel.Beginner => "The AI saw a good opportunity right now based on recent price movements and news.",
            UserSkillLevel.Intermediate => $"Entry triggered by: {string.Join(", ", decision.EntryTriggers)}. Market conditions: {decision.MarketRegime}.",
            UserSkillLevel.Expert => $"Entry signals: {string.Join(", ", decision.EntryTriggers)}. Regime: {decision.MarketRegime}. Execution: {decision.ExecutionAlgo} targeting {decision.PriceTarget:C}.",
            _ => "Optimal entry point detected."
        };
    }

    private string GetGenericResponse(UserSkillLevel level)
    {
        return level switch
        {
            UserSkillLevel.Beginner => "I'd be happy to explain more. You can ask about the news, the price patterns, or the risks involved.",
            UserSkillLevel.Intermediate => "I can provide more details on technicals, sentiment, risk parameters, or position sizing. What would you like to know?",
            UserSkillLevel.Expert => "Available details: feature importance, model internals, sensitivity analysis, or alternative scenarios.",
            _ => "Please ask a specific question about the trade."
        };
    }

    #endregion
}

#region Types

public enum TradeAction
{
    Buy,
    Sell,
    Hold
}

public enum ExplanationImportance
{
    Low,
    Medium,
    High
}

public sealed record TradingDecision
{
    public string DecisionId { get; init; } = Guid.NewGuid().ToString();
    public string Symbol { get; init; } = string.Empty;
    public TradeAction Action { get; init; }
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public double Confidence { get; init; }
    public double SentimentScore { get; init; }
    public IReadOnlyList<string> TechnicalSignals { get; init; } = Array.Empty<string>();
    public double PriceChange { get; init; }
    public double VolumeRatio { get; init; }
    public decimal? StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
    public decimal MaxRisk { get; init; }
    public double StopLossPercent { get; init; }
    public double RewardRatio { get; init; }

    // Intermediate level
    public int NewsCount { get; init; }
    public int SocialMentions { get; init; }

    // Expert level
    public string ModelType { get; init; } = string.Empty;
    public double PolicyEntropy { get; init; }
    public double ValueEstimate { get; init; }
    public double TechnicalWeight { get; init; }
    public double SentimentWeight { get; init; }
    public double MomentumWeight { get; init; }
    public double RawModelOutput { get; init; }
    public double KellyFraction { get; init; }
    public double ActualFraction { get; init; }
    public double ExpectedSharpeContribution { get; init; }
    public double VwapDeviation { get; init; }
    public double PositionVaR { get; init; }
    public double NewsSentiment { get; init; }
    public double SocialSentiment { get; init; }
    public IReadOnlyDictionary<string, double> ActionProbabilities { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> FeatureImportance { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> IndicatorValues { get; init; } = new Dictionary<string, double>();
    public IReadOnlyList<string> EntryTriggers { get; init; } = Array.Empty<string>();
    public string MarketRegime { get; init; } = string.Empty;
    public string ExecutionAlgo { get; init; } = string.Empty;
    public decimal PriceTarget { get; init; }
}

public sealed record TradingExplanation
{
    public string DecisionId { get; init; } = string.Empty;
    public UserSkillLevel SkillLevel { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<ExplanationDetail> Details { get; init; } = Array.Empty<ExplanationDetail>();
    public string ConfidenceDescription { get; init; } = string.Empty;
    public TechnicalExplanationDetails? TechnicalDetails { get; init; }
    public ModelExplanationDetails? ModelDetails { get; init; }
    public IReadOnlyList<string> SuggestedFollowUps { get; init; } = Array.Empty<string>();
}

public sealed record ExplanationDetail
{
    public string Category { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public ExplanationImportance Importance { get; init; }
}

public sealed record TechnicalExplanationDetails
{
    public IReadOnlyList<string> Indicators { get; init; } = Array.Empty<string>();
    public double SentimentScore { get; init; }
    public string VolumeAnalysis { get; init; } = string.Empty;
}

public sealed record ModelExplanationDetails
{
    public string ModelType { get; init; } = string.Empty;
    public double PolicyEntropy { get; init; }
    public double ValueEstimate { get; init; }
    public IReadOnlyDictionary<string, double> ActionProbabilities { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> FeatureImportance { get; init; } = new Dictionary<string, double>();
}

public sealed record PortfolioAllocation
{
    public double RiskLevel { get; init; }
    public double ExpectedReturn { get; init; }
    public double Volatility { get; init; }
    public double SharpeRatio { get; init; }
    public double SortinoRatio { get; init; }
    public double Beta { get; init; }
    public double Alpha { get; init; }
    public double ExpectedMaxDrawdown { get; init; }
    public double InformationRatio { get; init; }
    public double TrackingError { get; init; }
    public double MC5th { get; init; }
    public double MC50th { get; init; }
    public double MC95th { get; init; }
    public double EfficientFrontierPosition { get; init; }
    public IReadOnlyList<PortfolioHolding> Holdings { get; init; } = Array.Empty<PortfolioHolding>();
    public double[,]? CorrelationMatrix { get; init; }
}

public sealed record PortfolioHolding
{
    public string Symbol { get; init; } = string.Empty;
    public string AssetType { get; init; } = string.Empty;
    public double Weight { get; init; }
    public double ExpectedContribution { get; init; }
    public double RiskContribution { get; init; }
    public double MarginalSharpe { get; init; }
    public double RiskParityWeight { get; init; }
}

public sealed record AllocationExplanation
{
    public UserSkillLevel SkillLevel { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<AllocationItem> Allocations { get; init; } = Array.Empty<AllocationItem>();
    public string RiskDescription { get; init; } = string.Empty;
    public string ExpectedOutcome { get; init; } = string.Empty;
    public AllocationMetrics? Metrics { get; init; }
    public double[,]? CorrelationMatrix { get; init; }
    public double EfficientFrontierPosition { get; init; }
}

public sealed record AllocationItem
{
    public string AssetName { get; init; } = string.Empty;
    public double Percentage { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record AllocationMetrics
{
    public double SharpeRatio { get; init; }
    public double SortinoRatio { get; init; }
    public double Beta { get; init; }
    public double Alpha { get; init; }
    public double Volatility { get; init; }
    public double MaxDrawdown { get; init; }
    public double InformationRatio { get; init; }
    public double TrackingError { get; init; }
}

public sealed record RiskAssessment
{
    public double OverallRiskScore { get; init; }
    public double VaR95 { get; init; }
    public double VaR99 { get; init; }
    public double CVaR { get; init; }
    public double MaxDrawdown { get; init; }
    public double Beta { get; init; }
    public double ConcentrationRisk { get; init; }
    public double SectorConcentration { get; init; }
    public double StressTestLoss { get; init; }
    public double TailRisk { get; init; }
    public string VaRMethod { get; init; } = string.Empty;
    public int HistoricalWindow { get; init; }
    public double StressCovid { get; init; }
    public double Stress2008 { get; init; }
    public double StressRateShock { get; init; }
    public double PortfolioDelta { get; init; }
    public double PortfolioGamma { get; init; }
    public double PortfolioVega { get; init; }
    public double PortfolioTheta { get; init; }
    public IReadOnlyDictionary<string, double> RiskDecomposition { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> StressTestResults { get; init; } = new Dictionary<string, double>();
}

public sealed record RiskExplanation
{
    public UserSkillLevel SkillLevel { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string WhatItMeans { get; init; } = string.Empty;
    public string WhatCouldHappen { get; init; } = string.Empty;
    public RiskMetrics? Metrics { get; init; }
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, double>? RiskDecomposition { get; init; }
    public IReadOnlyDictionary<string, double>? StressTestResults { get; init; }
}

public sealed record RiskMetrics
{
    public double VaR95 { get; init; }
    public double VaR99 { get; init; }
    public double CVaR { get; init; }
    public double Beta { get; init; }
    public double MaxDrawdown { get; init; }
    public double PortfolioDelta { get; init; }
    public double PortfolioGamma { get; init; }
    public double PortfolioVega { get; init; }
    public double PortfolioTheta { get; init; }
}

#endregion
