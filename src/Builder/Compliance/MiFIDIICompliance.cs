using OoplesFinance.StockIndicators.Builder.Extensions;

namespace OoplesFinance.StockIndicators.Builder.Compliance;

/// <summary>
/// MiFID II compliance assessment for EU investors.
/// Implements appropriateness and suitability requirements under MiFID II.
/// </summary>
public interface IMiFIDIIComplianceService
{
    /// <summary>Gets the appropriateness questionnaire for a product type.</summary>
    MiFIDQuestionnaire GetAppropriatenessQuestionnaire(MiFIDProductType productType);

    /// <summary>Gets the suitability questionnaire.</summary>
    MiFIDQuestionnaire GetSuitabilityQuestionnaire();

    /// <summary>Evaluates appropriateness test responses.</summary>
    AppropriatenessResult EvaluateAppropriateness(MiFIDProductType productType, MiFIDResponses responses);

    /// <summary>Evaluates suitability test responses.</summary>
    MiFIDSuitabilityProfile EvaluateSuitability(MiFIDResponses responses);

    /// <summary>Gets the PRIIPs Key Information Document (KID) requirements.</summary>
    PRIIPsRequirement GetPRIIPsRequirements(MiFIDProductType productType);

    /// <summary>Gets cost and charges disclosure for a product.</summary>
    CostDisclosure GetCostDisclosure(MiFIDProductType productType, decimal investmentAmount);

    /// <summary>Gets required ex-ante disclosures.</summary>
    IReadOnlyList<ExAnteDisclosure> GetExAnteDisclosures(MiFIDProductType productType);
}

/// <summary>
/// Implementation of MiFID II compliance service.
/// </summary>
public sealed class MiFIDIIComplianceService : IMiFIDIIComplianceService
{
    public MiFIDQuestionnaire GetAppropriatenessQuestionnaire(MiFIDProductType productType)
    {
        var questions = new List<MiFIDQuestion>();

        // Common knowledge questions
        questions.Add(new MiFIDQuestion
        {
            Id = "product_understanding",
            Text = $"Do you understand the key features of {GetProductName(productType)}?",
            Type = MiFIDQuestionType.YesNo,
            Category = QuestionCategory.Knowledge,
            Required = true
        });

        questions.Add(new MiFIDQuestion
        {
            Id = "risk_understanding",
            Text = "Do you understand that you could lose some or all of your invested capital?",
            Type = MiFIDQuestionType.YesNo,
            Category = QuestionCategory.Knowledge,
            Required = true
        });

        // Product-specific questions
        switch (productType)
        {
            case MiFIDProductType.CFD:
                questions.AddRange(GetCFDQuestions());
                break;
            case MiFIDProductType.Forex:
                questions.AddRange(GetForexQuestions());
                break;
            case MiFIDProductType.Options:
                questions.AddRange(GetOptionsQuestions());
                break;
            case MiFIDProductType.Futures:
                questions.AddRange(GetFuturesQuestions());
                break;
        }

        // Experience questions
        questions.Add(new MiFIDQuestion
        {
            Id = "trading_experience",
            Text = $"How many times have you traded {GetProductName(productType)} in the last 3 years?",
            Type = MiFIDQuestionType.SingleChoice,
            Category = QuestionCategory.Experience,
            Options = new[]
            {
                new MiFIDQuestionOption("none", "Never"),
                new MiFIDQuestionOption("few", "1-10 times"),
                new MiFIDQuestionOption("moderate", "11-40 times"),
                new MiFIDQuestionOption("extensive", "More than 40 times")
            },
            Required = true
        });

        questions.Add(new MiFIDQuestion
        {
            Id = "portfolio_size",
            Text = "What is the size of your financial instrument portfolio (excluding real estate)?",
            Type = MiFIDQuestionType.SingleChoice,
            Category = QuestionCategory.Experience,
            Options = new[]
            {
                new MiFIDQuestionOption("under_10k", "Under EUR 10,000"),
                new MiFIDQuestionOption("10k_50k", "EUR 10,000 - 50,000"),
                new MiFIDQuestionOption("50k_250k", "EUR 50,000 - 250,000"),
                new MiFIDQuestionOption("250k_500k", "EUR 250,000 - 500,000"),
                new MiFIDQuestionOption("over_500k", "Over EUR 500,000")
            },
            Required = true
        });

        return new MiFIDQuestionnaire
        {
            ProductType = productType,
            Type = MiFIDQuestionnaireType.Appropriateness,
            Questions = questions
        };
    }

    public MiFIDQuestionnaire GetSuitabilityQuestionnaire()
    {
        return new MiFIDQuestionnaire
        {
            Type = MiFIDQuestionnaireType.Suitability,
            Questions = new List<MiFIDQuestion>
            {
                new()
                {
                    Id = "investment_objective",
                    Text = "What is your primary investment objective?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.InvestmentObjectives,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("preservation", "Capital preservation"),
                        new MiFIDQuestionOption("income", "Regular income generation"),
                        new MiFIDQuestionOption("balanced", "Balanced growth and income"),
                        new MiFIDQuestionOption("growth", "Capital growth"),
                        new MiFIDQuestionOption("speculation", "Speculation")
                    },
                    Required = true
                },
                new()
                {
                    Id = "investment_horizon",
                    Text = "What is your intended investment time horizon?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.InvestmentObjectives,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("very_short", "Less than 1 year"),
                        new MiFIDQuestionOption("short", "1-3 years"),
                        new MiFIDQuestionOption("medium", "3-5 years"),
                        new MiFIDQuestionOption("long", "5-10 years"),
                        new MiFIDQuestionOption("very_long", "More than 10 years")
                    },
                    Required = true
                },
                new()
                {
                    Id = "risk_appetite",
                    Text = "Which statement best describes your attitude to risk?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.RiskTolerance,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("averse", "I cannot afford any losses"),
                        new MiFIDQuestionOption("cautious", "I accept small losses for stability"),
                        new MiFIDQuestionOption("balanced", "I accept moderate losses for growth potential"),
                        new MiFIDQuestionOption("tolerant", "I accept significant losses for higher returns"),
                        new MiFIDQuestionOption("seeking", "I actively seek high-risk investments")
                    },
                    Required = true
                },
                new()
                {
                    Id = "loss_capacity",
                    Text = "What percentage of your invested capital could you afford to lose?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.FinancialSituation,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("none", "I cannot afford any loss"),
                        new MiFIDQuestionOption("up_to_10", "Up to 10%"),
                        new MiFIDQuestionOption("up_to_25", "Up to 25%"),
                        new MiFIDQuestionOption("up_to_50", "Up to 50%"),
                        new MiFIDQuestionOption("total", "I could lose my entire investment")
                    },
                    Required = true
                },
                new()
                {
                    Id = "annual_income",
                    Text = "What is your annual income?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.FinancialSituation,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("under_30k", "Under EUR 30,000"),
                        new MiFIDQuestionOption("30k_60k", "EUR 30,000 - 60,000"),
                        new MiFIDQuestionOption("60k_100k", "EUR 60,000 - 100,000"),
                        new MiFIDQuestionOption("100k_200k", "EUR 100,000 - 200,000"),
                        new MiFIDQuestionOption("over_200k", "Over EUR 200,000")
                    },
                    Required = true
                },
                new()
                {
                    Id = "net_worth",
                    Text = "What is your net worth (excluding primary residence)?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.FinancialSituation,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("under_50k", "Under EUR 50,000"),
                        new MiFIDQuestionOption("50k_100k", "EUR 50,000 - 100,000"),
                        new MiFIDQuestionOption("100k_500k", "EUR 100,000 - 500,000"),
                        new MiFIDQuestionOption("500k_1m", "EUR 500,000 - 1,000,000"),
                        new MiFIDQuestionOption("over_1m", "Over EUR 1,000,000")
                    },
                    Required = true
                },
                new()
                {
                    Id = "source_of_funds",
                    Text = "What is the primary source of your investment funds?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.FinancialSituation,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("employment", "Employment income"),
                        new MiFIDQuestionOption("savings", "Savings"),
                        new MiFIDQuestionOption("inheritance", "Inheritance/Gift"),
                        new MiFIDQuestionOption("investment", "Investment returns"),
                        new MiFIDQuestionOption("pension", "Pension/Retirement"),
                        new MiFIDQuestionOption("other", "Other")
                    },
                    Required = true
                },
                new()
                {
                    Id = "knowledge_level",
                    Text = "How would you describe your knowledge of financial instruments?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.Knowledge,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("none", "No knowledge"),
                        new MiFIDQuestionOption("basic", "Basic understanding"),
                        new MiFIDQuestionOption("intermediate", "Intermediate - stocks, bonds, funds"),
                        new MiFIDQuestionOption("advanced", "Advanced - derivatives, complex products"),
                        new MiFIDQuestionOption("professional", "Professional/Expert")
                    },
                    Required = true
                },
                new()
                {
                    Id = "esg_preference",
                    Text = "Do you have sustainability/ESG preferences for your investments?",
                    Type = MiFIDQuestionType.SingleChoice,
                    Category = QuestionCategory.SustainabilityPreferences,
                    Options = new[]
                    {
                        new MiFIDQuestionOption("none", "No specific preferences"),
                        new MiFIDQuestionOption("consider", "I consider ESG but it's not essential"),
                        new MiFIDQuestionOption("important", "ESG is important in my decisions"),
                        new MiFIDQuestionOption("essential", "ESG is essential - only sustainable investments")
                    },
                    Required = true
                }
            }
        };
    }

    public AppropriatenessResult EvaluateAppropriateness(MiFIDProductType productType, MiFIDResponses responses)
    {
        var knowledgeScore = 0;
        var experienceScore = 0;
        var warnings = new List<string>();

        // Evaluate knowledge
        foreach (var answer in responses.Answers.Where(a => IsKnowledgeQuestion(a.Key)))
        {
            knowledgeScore += EvaluateKnowledgeAnswer(answer.Key, answer.Value);
        }

        // Evaluate experience
        var tradingExp = responses.Answers.GetValueOrDefault("trading_experience", "none");
        experienceScore = tradingExp switch
        {
            "none" => 0,
            "few" => 25,
            "moderate" => 50,
            "extensive" => 100,
            _ => 0
        };

        // Determine appropriateness
        var isAppropriate = knowledgeScore >= 50 && experienceScore >= 25;

        if (!isAppropriate)
        {
            if (knowledgeScore < 50)
            {
                warnings.Add("Based on your responses, you may not have sufficient knowledge of this product type.");
            }
            if (experienceScore < 25)
            {
                warnings.Add("Based on your responses, you may not have sufficient experience with this product type.");
            }
        }

        // ESMA leverage restrictions for retail CFD clients
        var maxLeverage = 30.0m;
        if (productType == MiFIDProductType.CFD)
        {
            maxLeverage = GetESMALeverageCap(responses);
        }

        return new AppropriatenessResult
        {
            IsAppropriate = isAppropriate,
            KnowledgeScore = knowledgeScore,
            ExperienceScore = experienceScore,
            Warnings = warnings,
            CanProceedWithWarning = true, // MiFID II allows proceeding with appropriateness warning
            MaxLeverage = maxLeverage,
            EvaluatedAt = DateTime.UtcNow
        };
    }

    public MiFIDSuitabilityProfile EvaluateSuitability(MiFIDResponses responses)
    {
        var riskScore = CalculateMiFIDRiskScore(responses);

        return new MiFIDSuitabilityProfile
        {
            RiskScore = riskScore,
            RiskCategory = DetermineMiFIDRiskCategory(riskScore),
            InvestmentHorizon = ParseInvestmentHorizon(responses.Answers.GetValueOrDefault("investment_horizon", "medium")),
            LossCapacity = ParseLossCapacity(responses.Answers.GetValueOrDefault("loss_capacity", "up_to_10")),
            ESGPreference = ParseESGPreference(responses.Answers.GetValueOrDefault("esg_preference", "none")),
            ClientClassification = DetermineClientClassification(responses),
            SuitableProducts = DetermineSuitableProducts(riskScore, responses),
            EvaluatedAt = DateTime.UtcNow
        };
    }

    public PRIIPsRequirement GetPRIIPsRequirements(MiFIDProductType productType)
    {
        return new PRIIPsRequirement
        {
            RequiresKID = RequiresPRIIPsKID(productType),
            ProductType = productType,
            KIDMustBeProvided = true,
            KIDLanguage = "en", // Would be based on client preference
            MaxRiskIndicator = 7, // 1-7 scale per PRIIPs
            MustDisclosePerformanceScenarios = true
        };
    }

    public CostDisclosure GetCostDisclosure(MiFIDProductType productType, decimal investmentAmount)
    {
        // Example cost structure (would be product-specific in production)
        var entryCost = investmentAmount * 0.001m; // 0.1% entry
        var exitCost = investmentAmount * 0.001m; // 0.1% exit
        var ongoingCost = investmentAmount * 0.005m; // 0.5% ongoing annual
        var transactionCosts = investmentAmount * 0.002m; // 0.2% transaction

        return new CostDisclosure
        {
            ProductType = productType,
            InvestmentAmount = investmentAmount,
            EntryCost = entryCost,
            ExitCost = exitCost,
            OngoingCostsPerYear = ongoingCost,
            TransactionCosts = transactionCosts,
            TotalCostFirstYear = entryCost + ongoingCost + transactionCosts,
            ImpactOnReturnFirstYear = (entryCost + ongoingCost + transactionCosts) / investmentAmount * 100,
            Currency = "EUR"
        };
    }

    public IReadOnlyList<ExAnteDisclosure> GetExAnteDisclosures(MiFIDProductType productType)
    {
        var disclosures = new List<ExAnteDisclosure>
        {
            new()
            {
                Type = DisclosureType.RiskWarning,
                Title = "Risk Warning",
                Content = GetRiskWarning(productType),
                MustBeAcknowledged = true
            },
            new()
            {
                Type = DisclosureType.CostAndCharges,
                Title = "Costs and Charges",
                Content = "You will receive a detailed breakdown of all costs and charges before trading.",
                MustBeAcknowledged = true
            },
            new()
            {
                Type = DisclosureType.BestExecution,
                Title = "Best Execution Policy",
                Content = "We will take all sufficient steps to obtain the best possible result for you.",
                MustBeAcknowledged = false
            },
            new()
            {
                Type = DisclosureType.ConflictsOfInterest,
                Title = "Conflicts of Interest",
                Content = "Our conflicts of interest policy is available upon request.",
                MustBeAcknowledged = false
            }
        };

        // CFD-specific disclosures (ESMA requirement)
        if (productType == MiFIDProductType.CFD)
        {
            disclosures.Add(new ExAnteDisclosure
            {
                Type = DisclosureType.RetailLossWarning,
                Title = "CFD Risk Warning",
                Content = "CFDs are complex instruments and come with a high risk of losing money rapidly due to leverage. " +
                          "Between 74-89% of retail investor accounts lose money when trading CFDs. " +
                          "You should consider whether you understand how CFDs work and whether you can afford to take the high risk of losing your money.",
                MustBeAcknowledged = true
            });
        }

        return disclosures;
    }

    private static IEnumerable<MiFIDQuestion> GetCFDQuestions()
    {
        yield return new MiFIDQuestion
        {
            Id = "cfd_leverage",
            Text = "CFDs use leverage. This means:",
            Type = MiFIDQuestionType.SingleChoice,
            Category = QuestionCategory.Knowledge,
            Options = new[]
            {
                new MiFIDQuestionOption("wrong1", "Your losses are limited to your deposit"),
                new MiFIDQuestionOption("correct", "You can lose more than your initial deposit"),
                new MiFIDQuestionOption("wrong2", "Leverage reduces your risk")
            },
            CorrectAnswer = "correct",
            Required = true
        };

        yield return new MiFIDQuestion
        {
            Id = "cfd_margin",
            Text = "What happens if your margin falls below the maintenance level?",
            Type = MiFIDQuestionType.SingleChoice,
            Category = QuestionCategory.Knowledge,
            Options = new[]
            {
                new MiFIDQuestionOption("wrong1", "Nothing, you can continue trading"),
                new MiFIDQuestionOption("correct", "Your position may be closed automatically"),
                new MiFIDQuestionOption("wrong2", "You receive bonus margin")
            },
            CorrectAnswer = "correct",
            Required = true
        };
    }

    private static IEnumerable<MiFIDQuestion> GetForexQuestions()
    {
        yield return new MiFIDQuestion
        {
            Id = "forex_spread",
            Text = "What is the spread in forex trading?",
            Type = MiFIDQuestionType.SingleChoice,
            Category = QuestionCategory.Knowledge,
            Options = new[]
            {
                new MiFIDQuestionOption("wrong1", "The commission charged per trade"),
                new MiFIDQuestionOption("correct", "The difference between bid and ask price"),
                new MiFIDQuestionOption("wrong2", "The interest rate differential")
            },
            CorrectAnswer = "correct",
            Required = true
        };
    }

    private static IEnumerable<MiFIDQuestion> GetOptionsQuestions()
    {
        yield return new MiFIDQuestion
        {
            Id = "options_time_value",
            Text = "What happens to an option's time value as expiration approaches?",
            Type = MiFIDQuestionType.SingleChoice,
            Category = QuestionCategory.Knowledge,
            Options = new[]
            {
                new MiFIDQuestionOption("wrong1", "It increases"),
                new MiFIDQuestionOption("correct", "It decreases (time decay)"),
                new MiFIDQuestionOption("wrong2", "It stays the same")
            },
            CorrectAnswer = "correct",
            Required = true
        };
    }

    private static IEnumerable<MiFIDQuestion> GetFuturesQuestions()
    {
        yield return new MiFIDQuestion
        {
            Id = "futures_obligation",
            Text = "What obligation does a futures contract create?",
            Type = MiFIDQuestionType.SingleChoice,
            Category = QuestionCategory.Knowledge,
            Options = new[]
            {
                new MiFIDQuestionOption("wrong1", "An option to buy or sell"),
                new MiFIDQuestionOption("correct", "An obligation to buy or sell"),
                new MiFIDQuestionOption("wrong2", "No obligation, just a forecast")
            },
            CorrectAnswer = "correct",
            Required = true
        };
    }

    private static string GetProductName(MiFIDProductType productType) => productType switch
    {
        MiFIDProductType.CFD => "Contracts for Difference (CFDs)",
        MiFIDProductType.Forex => "Foreign Exchange (Forex)",
        MiFIDProductType.Options => "Options",
        MiFIDProductType.Futures => "Futures",
        MiFIDProductType.Stocks => "Stocks/Equities",
        MiFIDProductType.Bonds => "Bonds",
        MiFIDProductType.ETF => "Exchange Traded Funds (ETFs)",
        MiFIDProductType.StructuredProduct => "Structured Products",
        _ => productType.ToString()
    };

    private static bool IsKnowledgeQuestion(string questionId)
    {
        return questionId.Contains("understanding") || questionId.Contains("leverage") ||
               questionId.Contains("margin") || questionId.Contains("spread") ||
               questionId.Contains("time_value") || questionId.Contains("obligation");
    }

    private static int EvaluateKnowledgeAnswer(string questionId, string answer)
    {
        // Check if it's a yes/no understanding question
        if (questionId.Contains("understanding") && answer == "yes")
        {
            return 25;
        }

        // Check if it's a knowledge test question with correct answer
        if (answer == "correct")
        {
            return 25;
        }

        return 0;
    }

    private static decimal GetESMALeverageCap(MiFIDResponses responses)
    {
        // ESMA leverage restrictions for retail clients:
        // Major forex pairs: 30:1
        // Non-major forex, major indices, gold: 20:1
        // Non-major indices, other commodities: 10:1
        // Individual equities, other: 5:1
        // Crypto: 2:1

        return 30.0m; // Default for forex majors
    }

    private static int CalculateMiFIDRiskScore(MiFIDResponses responses)
    {
        var score = 0;

        score += responses.Answers.GetValueOrDefault("investment_objective") switch
        {
            "preservation" => 10,
            "income" => 25,
            "balanced" => 50,
            "growth" => 75,
            "speculation" => 100,
            _ => 50
        };

        score += responses.Answers.GetValueOrDefault("risk_appetite") switch
        {
            "averse" => 0,
            "cautious" => 15,
            "balanced" => 30,
            "tolerant" => 45,
            "seeking" => 60,
            _ => 30
        };

        return Math.Min(100, score / 2); // Normalize to 0-100
    }

    private static MiFIDRiskCategory DetermineMiFIDRiskCategory(int riskScore) => riskScore switch
    {
        <= 20 => MiFIDRiskCategory.VeryLow,
        <= 40 => MiFIDRiskCategory.Low,
        <= 60 => MiFIDRiskCategory.Medium,
        <= 80 => MiFIDRiskCategory.High,
        _ => MiFIDRiskCategory.VeryHigh
    };

    private static InvestmentHorizonType ParseInvestmentHorizon(string value) => value switch
    {
        "very_short" => InvestmentHorizonType.VeryShort,
        "short" => InvestmentHorizonType.Short,
        "medium" => InvestmentHorizonType.Medium,
        "long" => InvestmentHorizonType.Long,
        "very_long" => InvestmentHorizonType.VeryLong,
        _ => InvestmentHorizonType.Medium
    };

    private static decimal ParseLossCapacity(string value) => value switch
    {
        "none" => 0,
        "up_to_10" => 0.10m,
        "up_to_25" => 0.25m,
        "up_to_50" => 0.50m,
        "total" => 1.0m,
        _ => 0.10m
    };

    private static ESGPreference ParseESGPreference(string value) => value switch
    {
        "none" => ESGPreference.None,
        "consider" => ESGPreference.Consider,
        "important" => ESGPreference.Important,
        "essential" => ESGPreference.Essential,
        _ => ESGPreference.None
    };

    private static MiFIDClientClassification DetermineClientClassification(MiFIDResponses responses)
    {
        var knowledge = responses.Answers.GetValueOrDefault("knowledge_level", "basic");
        var netWorth = responses.Answers.GetValueOrDefault("net_worth", "under_50k");

        // Simplified - in production would include professional investor criteria
        if (knowledge == "professional" && netWorth == "over_1m")
        {
            return MiFIDClientClassification.ElectiveProfessional;
        }

        return MiFIDClientClassification.Retail;
    }

    private static IReadOnlyList<MiFIDProductType> DetermineSuitableProducts(int riskScore, MiFIDResponses responses)
    {
        var products = new List<MiFIDProductType> { MiFIDProductType.Stocks, MiFIDProductType.Bonds, MiFIDProductType.ETF };

        if (riskScore >= 50)
        {
            products.Add(MiFIDProductType.Forex);
        }

        if (riskScore >= 70)
        {
            products.Add(MiFIDProductType.CFD);
            products.Add(MiFIDProductType.Options);
            products.Add(MiFIDProductType.Futures);
        }

        return products;
    }

    private static bool RequiresPRIIPsKID(MiFIDProductType productType) => productType switch
    {
        MiFIDProductType.CFD => true,
        MiFIDProductType.StructuredProduct => true,
        MiFIDProductType.ETF => true,
        MiFIDProductType.Options => true,
        _ => false
    };

    private static string GetRiskWarning(MiFIDProductType productType) => productType switch
    {
        MiFIDProductType.CFD => "CFDs are complex instruments and come with a high risk of losing money rapidly due to leverage.",
        MiFIDProductType.Forex => "Foreign exchange trading carries significant risk due to leverage and market volatility.",
        MiFIDProductType.Options => "Options trading involves significant risks and is not suitable for all investors.",
        MiFIDProductType.Futures => "Futures trading carries substantial risk and is not appropriate for all investors.",
        _ => "All investments involve risk. Past performance is not indicative of future results."
    };
}

#region Types

public sealed class MiFIDQuestionnaire
{
    public MiFIDProductType ProductType { get; init; }
    public MiFIDQuestionnaireType Type { get; init; }
    public IReadOnlyList<MiFIDQuestion> Questions { get; init; } = Array.Empty<MiFIDQuestion>();
}

public enum MiFIDQuestionnaireType
{
    Appropriateness,
    Suitability
}

public enum MiFIDProductType
{
    Stocks,
    Bonds,
    ETF,
    Forex,
    CFD,
    Options,
    Futures,
    StructuredProduct
}

public sealed class MiFIDQuestion
{
    public string Id { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public MiFIDQuestionType Type { get; init; }
    public QuestionCategory Category { get; init; }
    public IReadOnlyList<MiFIDQuestionOption> Options { get; init; } = Array.Empty<MiFIDQuestionOption>();
    public string? CorrectAnswer { get; init; }
    public bool Required { get; init; }
}

public enum MiFIDQuestionType
{
    YesNo,
    SingleChoice,
    MultipleChoice,
    Scale,
    FreeText
}

public enum QuestionCategory
{
    Knowledge,
    Experience,
    FinancialSituation,
    InvestmentObjectives,
    RiskTolerance,
    SustainabilityPreferences
}

public sealed record MiFIDQuestionOption(string Value, string Label);

public sealed class MiFIDResponses
{
    public Dictionary<string, string> Answers { get; init; } = new();
    public DateTime CompletedAt { get; init; }
}

public sealed class AppropriatenessResult
{
    public bool IsAppropriate { get; init; }
    public int KnowledgeScore { get; init; }
    public int ExperienceScore { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public bool CanProceedWithWarning { get; init; }
    public decimal MaxLeverage { get; init; }
    public DateTime EvaluatedAt { get; init; }
}

public sealed class MiFIDSuitabilityProfile
{
    public int RiskScore { get; init; }
    public MiFIDRiskCategory RiskCategory { get; init; }
    public InvestmentHorizonType InvestmentHorizon { get; init; }
    public decimal LossCapacity { get; init; }
    public ESGPreference ESGPreference { get; init; }
    public MiFIDClientClassification ClientClassification { get; init; }
    public IReadOnlyList<MiFIDProductType> SuitableProducts { get; init; } = Array.Empty<MiFIDProductType>();
    public DateTime EvaluatedAt { get; init; }
}

public enum MiFIDRiskCategory
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}

public enum InvestmentHorizonType
{
    VeryShort,
    Short,
    Medium,
    Long,
    VeryLong
}

public enum ESGPreference
{
    None,
    Consider,
    Important,
    Essential
}

public enum MiFIDClientClassification
{
    Retail,
    ElectiveProfessional,
    PerSeProfessional,
    EligibleCounterparty
}

public sealed class PRIIPsRequirement
{
    public bool RequiresKID { get; init; }
    public MiFIDProductType ProductType { get; init; }
    public bool KIDMustBeProvided { get; init; }
    public string KIDLanguage { get; init; } = "en";
    public int MaxRiskIndicator { get; init; }
    public bool MustDisclosePerformanceScenarios { get; init; }
}

public sealed class CostDisclosure
{
    public MiFIDProductType ProductType { get; init; }
    public decimal InvestmentAmount { get; init; }
    public decimal EntryCost { get; init; }
    public decimal ExitCost { get; init; }
    public decimal OngoingCostsPerYear { get; init; }
    public decimal TransactionCosts { get; init; }
    public decimal TotalCostFirstYear { get; init; }
    public decimal ImpactOnReturnFirstYear { get; init; }
    public string Currency { get; init; } = "EUR";
}

public sealed class ExAnteDisclosure
{
    public DisclosureType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool MustBeAcknowledged { get; init; }
}

public enum DisclosureType
{
    RiskWarning,
    CostAndCharges,
    BestExecution,
    ConflictsOfInterest,
    RetailLossWarning,
    PRIIPsKID
}

#endregion
