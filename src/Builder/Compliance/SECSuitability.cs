using OoplesFinance.StockIndicators.Builder.Extensions;

namespace OoplesFinance.StockIndicators.Builder.Compliance;

/// <summary>
/// SEC suitability assessment for US investors.
/// Based on FINRA Rule 2111 - Suitability.
/// </summary>
public interface ISECSuitabilityService
{
    /// <summary>Gets the suitability questionnaire.</summary>
    SuitabilityQuestionnaire GetQuestionnaire();

    /// <summary>Evaluates questionnaire responses and returns suitability profile.</summary>
    SuitabilityProfile EvaluateResponses(SuitabilityResponses responses);

    /// <summary>Checks if a specific investment is suitable for a profile.</summary>
    SuitabilityCheck CheckSuitability(SuitabilityProfile profile, InvestmentType investment);

    /// <summary>Gets required disclosures for a profile.</summary>
    IReadOnlyList<Disclosure> GetRequiredDisclosures(SuitabilityProfile profile);
}

/// <summary>
/// Implementation of SEC suitability assessment.
/// </summary>
public sealed class SECSuitabilityService : ISECSuitabilityService
{
    public SuitabilityQuestionnaire GetQuestionnaire()
    {
        return new SuitabilityQuestionnaire
        {
            Questions = new List<SuitabilityQuestion>
            {
                new()
                {
                    Id = "investment_objective",
                    Text = "What is your primary investment objective?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("preservation", "Capital Preservation", "Protect principal, accept lower returns"),
                        new QuestionOption("income", "Income", "Generate regular income from investments"),
                        new QuestionOption("growth", "Growth", "Grow capital over time with moderate risk"),
                        new QuestionOption("aggressive_growth", "Aggressive Growth", "Maximize returns, accept higher risk"),
                        new QuestionOption("speculation", "Speculation", "High risk/reward opportunities")
                    },
                    Required = true
                },
                new()
                {
                    Id = "investment_experience",
                    Text = "How would you describe your investment experience?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("none", "None", "I have never invested before"),
                        new QuestionOption("limited", "Limited", "1-3 years, mainly stocks and bonds"),
                        new QuestionOption("moderate", "Moderate", "3-5 years, stocks, bonds, and funds"),
                        new QuestionOption("extensive", "Extensive", "5+ years, multiple asset classes"),
                        new QuestionOption("professional", "Professional", "Work in finance or related field")
                    },
                    Required = true
                },
                new()
                {
                    Id = "risk_tolerance",
                    Text = "How would you react if your portfolio lost 20% in value?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("sell_all", "Sell everything", "I would sell to prevent further losses"),
                        new QuestionOption("sell_some", "Sell some", "I would reduce risk by selling some holdings"),
                        new QuestionOption("hold", "Hold", "I would wait for recovery"),
                        new QuestionOption("buy_more", "Buy more", "I would see it as an opportunity to buy")
                    },
                    Required = true
                },
                new()
                {
                    Id = "time_horizon",
                    Text = "What is your investment time horizon?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("short", "Less than 1 year", string.Empty),
                        new QuestionOption("medium_short", "1-3 years", string.Empty),
                        new QuestionOption("medium", "3-5 years", string.Empty),
                        new QuestionOption("medium_long", "5-10 years", string.Empty),
                        new QuestionOption("long", "More than 10 years", string.Empty)
                    },
                    Required = true
                },
                new()
                {
                    Id = "annual_income",
                    Text = "What is your annual income range?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("under_50k", "Under $50,000", string.Empty),
                        new QuestionOption("50k_100k", "$50,000 - $100,000", string.Empty),
                        new QuestionOption("100k_200k", "$100,000 - $200,000", string.Empty),
                        new QuestionOption("200k_500k", "$200,000 - $500,000", string.Empty),
                        new QuestionOption("over_500k", "Over $500,000", string.Empty)
                    },
                    Required = true
                },
                new()
                {
                    Id = "liquid_net_worth",
                    Text = "What is your liquid net worth (excluding primary residence)?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("under_50k", "Under $50,000", string.Empty),
                        new QuestionOption("50k_100k", "$50,000 - $100,000", string.Empty),
                        new QuestionOption("100k_250k", "$100,000 - $250,000", string.Empty),
                        new QuestionOption("250k_500k", "$250,000 - $500,000", string.Empty),
                        new QuestionOption("500k_1m", "$500,000 - $1,000,000", string.Empty),
                        new QuestionOption("over_1m", "Over $1,000,000", string.Empty)
                    },
                    Required = true
                },
                new()
                {
                    Id = "liquidity_needs",
                    Text = "What percentage of this investment might you need within the next 12 months?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("none", "0%", "I won't need any of it"),
                        new QuestionOption("small", "Less than 25%", string.Empty),
                        new QuestionOption("moderate", "25% - 50%", string.Empty),
                        new QuestionOption("significant", "50% - 75%", string.Empty),
                        new QuestionOption("high", "More than 75%", string.Empty)
                    },
                    Required = true
                },
                new()
                {
                    Id = "options_experience",
                    Text = "What is your experience with options trading?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("none", "None", "I have never traded options"),
                        new QuestionOption("basic", "Basic", "Covered calls, cash-secured puts"),
                        new QuestionOption("intermediate", "Intermediate", "Spreads, straddles"),
                        new QuestionOption("advanced", "Advanced", "Complex multi-leg strategies")
                    },
                    Required = true
                },
                new()
                {
                    Id = "margin_experience",
                    Text = "What is your experience with margin/leverage?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("none", "None", "I have never used margin"),
                        new QuestionOption("limited", "Limited", "Some experience with margin accounts"),
                        new QuestionOption("experienced", "Experienced", "Regular use of margin")
                    },
                    Required = true
                },
                new()
                {
                    Id = "trading_frequency",
                    Text = "How frequently do you plan to trade?",
                    Type = QuestionType.SingleChoice,
                    Options = new[]
                    {
                        new QuestionOption("rarely", "Rarely", "A few times per year"),
                        new QuestionOption("occasionally", "Occasionally", "A few times per month"),
                        new QuestionOption("regularly", "Regularly", "Weekly"),
                        new QuestionOption("frequently", "Frequently", "Multiple times per week"),
                        new QuestionOption("day_trade", "Day Trading", "Multiple times per day")
                    },
                    Required = true
                }
            }
        };
    }

    public SuitabilityProfile EvaluateResponses(SuitabilityResponses responses)
    {
        var riskScore = CalculateRiskScore(responses);
        var experienceScore = CalculateExperienceScore(responses);

        var profile = new SuitabilityProfile
        {
            RiskScore = riskScore,
            ExperienceScore = experienceScore,
            RiskTolerance = DetermineRiskTolerance(riskScore),
            InvestorClassification = DetermineClassification(experienceScore, riskScore),
            ApprovedProducts = DetermineApprovedProducts(responses),
            MaxLeverage = DetermineMaxLeverage(responses),
            OptionsApprovalLevel = DetermineOptionsLevel(responses),
            IsDayTraderEligible = CheckDayTradingEligibility(responses),
            RequiresEnhancedDisclosure = riskScore > 70 || experienceScore < 30,
            EvaluatedAt = DateTime.UtcNow
        };

        return profile;
    }

    public SuitabilityCheck CheckSuitability(SuitabilityProfile profile, InvestmentType investment)
    {
        var suitable = true;
        var warnings = new List<string>();

        switch (investment)
        {
            case InvestmentType.Options:
                if (profile.OptionsApprovalLevel < 1)
                {
                    suitable = false;
                    warnings.Add("Options trading requires additional approval and experience.");
                }
                break;

            case InvestmentType.Margin:
                if (profile.MaxLeverage <= 1.0m)
                {
                    suitable = false;
                    warnings.Add("Margin trading requires experience and appropriate risk tolerance.");
                }
                break;

            case InvestmentType.DayTrading:
                if (!profile.IsDayTraderEligible)
                {
                    suitable = false;
                    warnings.Add("Pattern Day Trading requires $25,000+ account equity.");
                }
                break;

            case InvestmentType.Crypto:
                if (profile.RiskTolerance == RiskToleranceLevel.Conservative)
                {
                    warnings.Add("Cryptocurrency is a highly volatile asset class.");
                }
                break;

            case InvestmentType.Futures:
                if (profile.ExperienceScore < 50)
                {
                    suitable = false;
                    warnings.Add("Futures trading requires significant experience.");
                }
                break;
        }

        return new SuitabilityCheck
        {
            IsSuitable = suitable,
            Warnings = warnings,
            RequiresAcknowledgment = warnings.Count > 0
        };
    }

    public IReadOnlyList<Disclosure> GetRequiredDisclosures(SuitabilityProfile profile)
    {
        var disclosures = new List<Disclosure>();

        // General risk disclosure
        disclosures.Add(new Disclosure
        {
            Id = "general_risk",
            Title = "General Investment Risk",
            Content = "All investments involve risk. You may lose some or all of your principal.",
            Required = true
        });

        // Options disclosure
        if (profile.OptionsApprovalLevel > 0)
        {
            disclosures.Add(new Disclosure
            {
                Id = "options_risk",
                Title = "Options Risk Disclosure",
                Content = "Options involve significant risks and are not appropriate for all investors. " +
                          "Read the Characteristics and Risks of Standardized Options before trading.",
                Required = true,
                ExternalUrl = "https://www.theocc.com/Company-Information/Documents-and-Archives/Options-Disclosure-Document"
            });
        }

        // Margin disclosure
        if (profile.MaxLeverage > 1.0m)
        {
            disclosures.Add(new Disclosure
            {
                Id = "margin_risk",
                Title = "Margin Risk Disclosure",
                Content = "Trading on margin involves risk of losses in excess of your deposit. " +
                          "You may be required to deposit additional funds on short notice.",
                Required = true
            });
        }

        // Day trading disclosure
        if (profile.IsDayTraderEligible)
        {
            disclosures.Add(new Disclosure
            {
                Id = "day_trading_risk",
                Title = "Day Trading Risk Disclosure",
                Content = "Day trading can result in significant losses. It requires sufficient knowledge, " +
                          "experience, and financial resources.",
                Required = true
            });
        }

        return disclosures;
    }

    private static int CalculateRiskScore(SuitabilityResponses responses)
    {
        var score = 0;

        // Investment objective (0-40 points)
        score += responses.Answers.GetValueOrDefault("investment_objective") switch
        {
            "preservation" => 0,
            "income" => 10,
            "growth" => 25,
            "aggressive_growth" => 35,
            "speculation" => 40,
            _ => 0
        };

        // Risk tolerance (0-30 points)
        score += responses.Answers.GetValueOrDefault("risk_tolerance") switch
        {
            "sell_all" => 0,
            "sell_some" => 10,
            "hold" => 20,
            "buy_more" => 30,
            _ => 0
        };

        // Time horizon (0-20 points)
        score += responses.Answers.GetValueOrDefault("time_horizon") switch
        {
            "short" => 0,
            "medium_short" => 5,
            "medium" => 10,
            "medium_long" => 15,
            "long" => 20,
            _ => 0
        };

        // Liquidity needs (reduce score for high needs)
        score -= responses.Answers.GetValueOrDefault("liquidity_needs") switch
        {
            "high" => 20,
            "significant" => 10,
            _ => 0
        };

        return Math.Max(0, Math.Min(100, score));
    }

    private static int CalculateExperienceScore(SuitabilityResponses responses)
    {
        var score = 0;

        // Investment experience (0-40 points)
        score += responses.Answers.GetValueOrDefault("investment_experience") switch
        {
            "none" => 0,
            "limited" => 10,
            "moderate" => 25,
            "extensive" => 35,
            "professional" => 40,
            _ => 0
        };

        // Options experience (0-30 points)
        score += responses.Answers.GetValueOrDefault("options_experience") switch
        {
            "none" => 0,
            "basic" => 10,
            "intermediate" => 20,
            "advanced" => 30,
            _ => 0
        };

        // Margin experience (0-20 points)
        score += responses.Answers.GetValueOrDefault("margin_experience") switch
        {
            "none" => 0,
            "limited" => 10,
            "experienced" => 20,
            _ => 0
        };

        return Math.Max(0, Math.Min(100, score));
    }

    private static RiskToleranceLevel DetermineRiskTolerance(int riskScore) => riskScore switch
    {
        < 20 => RiskToleranceLevel.Conservative,
        < 40 => RiskToleranceLevel.ModeratelyConservative,
        < 60 => RiskToleranceLevel.Moderate,
        < 80 => RiskToleranceLevel.ModeratelyAggressive,
        _ => RiskToleranceLevel.Aggressive
    };

    private static InvestorClassification DetermineClassification(int experienceScore, int riskScore) =>
        (experienceScore, riskScore) switch
        {
            ( < 20, _) => InvestorClassification.Retail,
            ( >= 70, >= 60) => InvestorClassification.AccreditedInvestor,
            ( >= 50, >= 40) => InvestorClassification.ExperiencedRetail,
            _ => InvestorClassification.Retail
        };

    private static IReadOnlyList<InvestmentType> DetermineApprovedProducts(SuitabilityResponses responses)
    {
        var products = new List<InvestmentType> { InvestmentType.Stocks, InvestmentType.ETFs, InvestmentType.Bonds };

        var experience = responses.Answers.GetValueOrDefault("investment_experience");
        var optionsExp = responses.Answers.GetValueOrDefault("options_experience");

        if (experience is "moderate" or "extensive" or "professional")
        {
            products.Add(InvestmentType.MutualFunds);
        }

        if (optionsExp is not "none")
        {
            products.Add(InvestmentType.Options);
        }

        return products;
    }

    private static decimal DetermineMaxLeverage(SuitabilityResponses responses)
    {
        var marginExp = responses.Answers.GetValueOrDefault("margin_experience");
        var experience = responses.Answers.GetValueOrDefault("investment_experience");

        if (marginExp == "none" || experience is "none" or "limited")
        {
            return 1.0m; // No leverage
        }

        if (marginExp == "limited")
        {
            return 2.0m; // Reg T standard
        }

        return 4.0m; // Day trading margin
    }

    private static int DetermineOptionsLevel(SuitabilityResponses responses)
    {
        var optionsExp = responses.Answers.GetValueOrDefault("options_experience");

        return optionsExp switch
        {
            "none" => 0,
            "basic" => 1, // Covered calls, cash-secured puts
            "intermediate" => 2, // Spreads
            "advanced" => 3, // Naked options
            _ => 0
        };
    }

    private static bool CheckDayTradingEligibility(SuitabilityResponses responses)
    {
        var netWorth = responses.Answers.GetValueOrDefault("liquid_net_worth");
        var frequency = responses.Answers.GetValueOrDefault("trading_frequency");

        // Need $25k+ for pattern day trading
        var hasCapital = netWorth is "250k_500k" or "500k_1m" or "over_1m";
        var wantsDayTrading = frequency is "frequently" or "day_trade";

        return hasCapital && wantsDayTrading;
    }
}

#region Types

public sealed class SuitabilityQuestionnaire
{
    public IReadOnlyList<SuitabilityQuestion> Questions { get; init; } = Array.Empty<SuitabilityQuestion>();
}

public sealed class SuitabilityQuestion
{
    public string Id { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public QuestionType Type { get; init; }
    public IReadOnlyList<QuestionOption> Options { get; init; } = Array.Empty<QuestionOption>();
    public bool Required { get; init; }
}

public enum QuestionType
{
    SingleChoice,
    MultipleChoice,
    FreeText,
    Number
}

public sealed record QuestionOption(string Value, string Label, string Description);

public sealed class SuitabilityResponses
{
    public Dictionary<string, string> Answers { get; init; } = new();
    public DateTime CompletedAt { get; init; }
}

public sealed class SuitabilityProfile
{
    public int RiskScore { get; init; }
    public int ExperienceScore { get; init; }
    public RiskToleranceLevel RiskTolerance { get; init; }
    public InvestorClassification InvestorClassification { get; init; }
    public IReadOnlyList<InvestmentType> ApprovedProducts { get; init; } = Array.Empty<InvestmentType>();
    public decimal MaxLeverage { get; init; }
    public int OptionsApprovalLevel { get; init; }
    public bool IsDayTraderEligible { get; init; }
    public bool RequiresEnhancedDisclosure { get; init; }
    public DateTime EvaluatedAt { get; init; }
}

public enum RiskToleranceLevel
{
    Conservative,
    ModeratelyConservative,
    Moderate,
    ModeratelyAggressive,
    Aggressive
}

public enum InvestorClassification
{
    Retail,
    ExperiencedRetail,
    AccreditedInvestor,
    QualifiedPurchaser,
    InstitutionalInvestor
}

public enum InvestmentType
{
    Stocks,
    Bonds,
    ETFs,
    MutualFunds,
    Options,
    Futures,
    Forex,
    Crypto,
    Margin,
    DayTrading
}

public sealed class SuitabilityCheck
{
    public bool IsSuitable { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public bool RequiresAcknowledgment { get; init; }
}

public sealed class Disclosure
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string? ExternalUrl { get; init; }
}

#endregion
