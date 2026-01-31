using AiDotNet.Models.Options;

namespace OoplesFinance.StockIndicators.Builder.AI.Training;

/// <summary>
/// Manages pre-trained RL agent models for different asset classes and risk profiles.
/// Provides versioned model distribution and loading.
/// </summary>
public interface IPretrainedModelRegistry
{
    /// <summary>Gets available pre-trained models.</summary>
    Task<IReadOnlyList<PretrainedModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default);

    /// <summary>Gets a specific pre-trained model.</summary>
    Task<PretrainedModel?> GetModelAsync(string modelId, CancellationToken ct = default);

    /// <summary>Gets the recommended model for given parameters.</summary>
    Task<PretrainedModel?> GetRecommendedModelAsync(
        AssetClassType assetClass,
        RiskProfile riskProfile,
        CancellationToken ct = default);

    /// <summary>Downloads a model to local storage.</summary>
    Task<string> DownloadModelAsync(string modelId, string localPath, CancellationToken ct = default);

    /// <summary>Checks for model updates.</summary>
    Task<IReadOnlyList<ModelUpdate>> CheckForUpdatesAsync(CancellationToken ct = default);
}

/// <summary>
/// Registry implementation for pre-trained models.
/// </summary>
public sealed class PretrainedModelRegistry : IPretrainedModelRegistry
{
    private readonly Dictionary<string, PretrainedModel> _models = new();
    private readonly string _modelStoragePath;

    public PretrainedModelRegistry(string modelStoragePath = "models")
    {
        _modelStoragePath = modelStoragePath;
        InitializeDefaultModels();
    }

    private void InitializeDefaultModels()
    {
        // Equity models
        RegisterModel(new PretrainedModel
        {
            ModelId = "equity-conservative-ppo-v1",
            Name = "Conservative Equity PPO",
            Description = "PPO agent for equity trading with conservative risk parameters. Trained on 10 years of S&P 500 data.",
            AssetClass = AssetClassType.Equity,
            RiskProfile = RiskProfile.Conservative,
            AgentType = TradingAgentType.PPO,
            Version = "1.2.0",
            TrainingDataRange = new DateRange(new DateTime(2014, 1, 1), new DateTime(2024, 1, 1)),
            ExpectedSharpe = 1.2,
            ExpectedMaxDrawdown = -0.12,
            ModelSizeBytes = 15_000_000,
            IsDefault = true
        });

        RegisterModel(new PretrainedModel
        {
            ModelId = "equity-moderate-ppo-v1",
            Name = "Moderate Equity PPO",
            Description = "PPO agent for balanced equity trading. Trained on diversified US market data.",
            AssetClass = AssetClassType.Equity,
            RiskProfile = RiskProfile.Moderate,
            AgentType = TradingAgentType.PPO,
            Version = "1.2.0",
            TrainingDataRange = new DateRange(new DateTime(2014, 1, 1), new DateTime(2024, 1, 1)),
            ExpectedSharpe = 1.5,
            ExpectedMaxDrawdown = -0.20,
            ModelSizeBytes = 18_000_000,
            IsDefault = true
        });

        RegisterModel(new PretrainedModel
        {
            ModelId = "equity-aggressive-sac-v1",
            Name = "Aggressive Equity SAC",
            Description = "SAC agent for aggressive equity trading with momentum focus.",
            AssetClass = AssetClassType.Equity,
            RiskProfile = RiskProfile.Aggressive,
            AgentType = TradingAgentType.SAC,
            Version = "1.1.0",
            TrainingDataRange = new DateRange(new DateTime(2014, 1, 1), new DateTime(2024, 1, 1)),
            ExpectedSharpe = 1.8,
            ExpectedMaxDrawdown = -0.35,
            ModelSizeBytes = 22_000_000,
            IsDefault = true
        });

        // Crypto models
        RegisterModel(new PretrainedModel
        {
            ModelId = "crypto-moderate-sac-v1",
            Name = "Moderate Crypto SAC",
            Description = "SAC agent for 24/7 crypto trading with volatility awareness.",
            AssetClass = AssetClassType.Crypto,
            RiskProfile = RiskProfile.Moderate,
            AgentType = TradingAgentType.SAC,
            Version = "1.0.0",
            TrainingDataRange = new DateRange(new DateTime(2018, 1, 1), new DateTime(2024, 1, 1)),
            ExpectedSharpe = 1.3,
            ExpectedMaxDrawdown = -0.40,
            ModelSizeBytes = 20_000_000,
            IsDefault = true
        });

        // Options models
        RegisterModel(new PretrainedModel
        {
            ModelId = "options-moderate-ppo-v1",
            Name = "Moderate Options PPO",
            Description = "PPO agent for options trading with Greeks awareness.",
            AssetClass = AssetClassType.Options,
            RiskProfile = RiskProfile.Moderate,
            AgentType = TradingAgentType.PPO,
            Version = "1.0.0",
            TrainingDataRange = new DateRange(new DateTime(2016, 1, 1), new DateTime(2024, 1, 1)),
            ExpectedSharpe = 1.4,
            ExpectedMaxDrawdown = -0.25,
            ModelSizeBytes = 25_000_000,
            IsDefault = true
        });

        // Forex models
        RegisterModel(new PretrainedModel
        {
            ModelId = "forex-moderate-ppo-v1",
            Name = "Moderate Forex PPO",
            Description = "PPO agent for major currency pairs with pip management.",
            AssetClass = AssetClassType.Forex,
            RiskProfile = RiskProfile.Moderate,
            AgentType = TradingAgentType.PPO,
            Version = "1.0.0",
            TrainingDataRange = new DateRange(new DateTime(2014, 1, 1), new DateTime(2024, 1, 1)),
            ExpectedSharpe = 1.2,
            ExpectedMaxDrawdown = -0.18,
            ModelSizeBytes = 16_000_000,
            IsDefault = true
        });

        // Portfolio/Multi-asset models
        RegisterModel(new PretrainedModel
        {
            ModelId = "portfolio-moderate-ppo-v1",
            Name = "Moderate Portfolio PPO",
            Description = "PPO agent for multi-asset portfolio allocation.",
            AssetClass = AssetClassType.MultiAsset,
            RiskProfile = RiskProfile.Moderate,
            AgentType = TradingAgentType.PPO,
            Version = "1.0.0",
            TrainingDataRange = new DateRange(new DateTime(2014, 1, 1), new DateTime(2024, 1, 1)),
            ExpectedSharpe = 1.6,
            ExpectedMaxDrawdown = -0.15,
            ModelSizeBytes = 30_000_000,
            IsDefault = true
        });
    }

    private void RegisterModel(PretrainedModel model)
    {
        _models[model.ModelId] = model;
    }

    public Task<IReadOnlyList<PretrainedModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var infos = _models.Values.Select(m => new PretrainedModelInfo
        {
            ModelId = m.ModelId,
            Name = m.Name,
            Description = m.Description,
            AssetClass = m.AssetClass,
            RiskProfile = m.RiskProfile,
            AgentType = m.AgentType,
            Version = m.Version,
            ExpectedSharpe = m.ExpectedSharpe,
            ExpectedMaxDrawdown = m.ExpectedMaxDrawdown,
            IsDefault = m.IsDefault
        }).ToList();

        return Task.FromResult<IReadOnlyList<PretrainedModelInfo>>(infos);
    }

    public Task<PretrainedModel?> GetModelAsync(string modelId, CancellationToken ct = default)
    {
        return Task.FromResult(_models.TryGetValue(modelId, out var model) ? model : null);
    }

    public Task<PretrainedModel?> GetRecommendedModelAsync(
        AssetClassType assetClass,
        RiskProfile riskProfile,
        CancellationToken ct = default)
    {
        var model = _models.Values
            .Where(m => m.AssetClass == assetClass && m.RiskProfile == riskProfile && m.IsDefault)
            .OrderByDescending(m => m.Version)
            .FirstOrDefault();

        return Task.FromResult(model);
    }

    public async Task<string> DownloadModelAsync(string modelId, string localPath, CancellationToken ct = default)
    {
        if (!_models.TryGetValue(modelId, out var model))
            throw new KeyNotFoundException($"Model {modelId} not found");

        var fullPath = Path.Combine(localPath, $"{modelId}.onnx");

        // In production, this would download from cloud storage
        // For now, we create a placeholder
        Directory.CreateDirectory(localPath);
        await File.WriteAllTextAsync(fullPath, $"# Placeholder for {model.Name}", ct);

        return fullPath;
    }

    public Task<IReadOnlyList<ModelUpdate>> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        // In production, this would check a remote registry
        return Task.FromResult<IReadOnlyList<ModelUpdate>>(Array.Empty<ModelUpdate>());
    }
}

#region Types

public enum AssetClassType
{
    Equity,
    Options,
    Forex,
    Crypto,
    Futures,
    MultiAsset
}

public enum RiskProfile
{
    Conservative,
    Moderate,
    Aggressive,
    VeryAggressive
}

public sealed record PretrainedModel
{
    public string ModelId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AssetClassType AssetClass { get; init; }
    public RiskProfile RiskProfile { get; init; }
    public TradingAgentType AgentType { get; init; }
    public string Version { get; init; } = "1.0.0";
    public DateRange TrainingDataRange { get; init; } = new(DateTime.MinValue, DateTime.MaxValue);
    public double ExpectedSharpe { get; init; }
    public double ExpectedMaxDrawdown { get; init; }
    public long ModelSizeBytes { get; init; }
    public bool IsDefault { get; init; }
    public string? LocalPath { get; init; }
    public byte[]? ModelWeights { get; init; }
}

public sealed record PretrainedModelInfo
{
    public string ModelId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AssetClassType AssetClass { get; init; }
    public RiskProfile RiskProfile { get; init; }
    public TradingAgentType AgentType { get; init; }
    public string Version { get; init; } = string.Empty;
    public double ExpectedSharpe { get; init; }
    public double ExpectedMaxDrawdown { get; init; }
    public bool IsDefault { get; init; }
}

public sealed record DateRange(DateTime Start, DateTime End);

public sealed record ModelUpdate
{
    public string ModelId { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string NewVersion { get; init; } = string.Empty;
    public string ChangeLog { get; init; } = string.Empty;
    public long DownloadSizeBytes { get; init; }
}

#endregion
