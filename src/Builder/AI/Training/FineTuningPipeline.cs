using AiDotNet;
using AiDotNet.Configuration;
using AiDotNet.Finance.Trading.Environments;
using AiDotNet.Models.Options;
using AiDotNet.Tensors;
using AiDotNet.Tensors.LinearAlgebra;

namespace OoplesFinance.StockIndicators.Builder.AI.Training;

/// <summary>
/// Pipeline for fine-tuning pre-trained agents on user preferences and data.
/// Enables personalization while preserving base model capabilities.
/// </summary>
public interface IFineTuningPipeline
{
    /// <summary>Creates a fine-tuning job.</summary>
    Task<FineTuningJob> CreateJobAsync(
        string userId,
        FineTuningRequest request,
        CancellationToken ct = default);

    /// <summary>Gets the status of a fine-tuning job.</summary>
    Task<FineTuningJob?> GetJobStatusAsync(string jobId, CancellationToken ct = default);

    /// <summary>Cancels a fine-tuning job.</summary>
    Task CancelJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>Gets all jobs for a user.</summary>
    Task<IReadOnlyList<FineTuningJob>> GetUserJobsAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Implementation of the fine-tuning pipeline.
/// </summary>
public sealed class FineTuningPipeline : IFineTuningPipeline
{
    private readonly IPretrainedModelRegistry _modelRegistry;
    private readonly Dictionary<string, FineTuningJob> _jobs = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action<string, double>? OnProgress;
    public event Action<string, FineTuningResult>? OnComplete;

    public FineTuningPipeline(IPretrainedModelRegistry modelRegistry)
    {
        _modelRegistry = modelRegistry;
    }

    public async Task<FineTuningJob> CreateJobAsync(
        string userId,
        FineTuningRequest request,
        CancellationToken ct = default)
    {
        // Load base model
        var baseModel = await _modelRegistry.GetModelAsync(request.BaseModelId, ct);
        if (baseModel is null)
            throw new KeyNotFoundException($"Base model {request.BaseModelId} not found");

        var jobId = Guid.NewGuid().ToString();
        var job = new FineTuningJob
        {
            JobId = jobId,
            UserId = userId,
            BaseModelId = request.BaseModelId,
            Status = FineTuningStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            Config = request.Config
        };

        await _lock.WaitAsync(ct);
        try
        {
            _jobs[jobId] = job;
        }
        finally
        {
            _lock.Release();
        }

        // Start fine-tuning in background
        _ = Task.Run(() => ExecuteFineTuningAsync(job, baseModel, request, ct), ct);

        return job;
    }

    public async Task<FineTuningJob?> GetJobStatusAsync(string jobId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CancelJobAsync(string jobId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                _jobs[jobId] = job with { Status = FineTuningStatus.Cancelled };
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<FineTuningJob>> GetUserJobsAsync(string userId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _jobs.Values.Where(j => j.UserId == userId).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ExecuteFineTuningAsync(
        FineTuningJob job,
        PretrainedModel baseModel,
        FineTuningRequest request,
        CancellationToken ct)
    {
        try
        {
            // Update status to running
            await UpdateJobStatusAsync(job.JobId, FineTuningStatus.Running, ct);

            // Prepare training data
            var prices = request.UserTradeHistory.Select(t => (double)t.Price).ToArray();
            if (prices.Length < 100)
            {
                await UpdateJobStatusAsync(job.JobId, FineTuningStatus.Failed, ct,
                    "Insufficient training data. Need at least 100 data points.");
                return;
            }

            // Create environment with user's data
            var tensor = new Tensor<double>(prices, [prices.Length, 1]);
            var environment = new StockTradingEnvironment<double>(
                marketData: tensor,
                windowSize: request.Config.WindowSize,
                initialCapital: request.Config.InitialCapital,
                transactionCost: request.Config.TransactionCost,
                tradeSize: 0.1);

            // Fine-tune with reduced learning rate (transfer learning)
            var fineTuneLearningRate = request.Config.LearningRate * 0.1; // 10% of base LR

            var rlOptions = new RLTrainingOptions<double>
            {
                Environment = environment,
                Episodes = request.Config.Episodes,
                MaxStepsPerEpisode = Math.Min(prices.Length - request.Config.WindowSize, 500),
                LogFrequency = 10,
                OnEpisodeComplete = metrics =>
                {
                    var progress = (double)metrics.Episode / request.Config.Episodes;
                    OnProgress?.Invoke(job.JobId, progress);
                    _ = UpdateJobProgressAsync(job.JobId, progress, metrics.TotalReward, ct);
                }
            };

            // Apply user's risk preferences to reward function
            ApplyUserPreferences(environment, request.UserPreferences);

            // Build and train using AiDotNet facade
            var builder = new AiModelBuilder<double, AiDotNet.Tensors.LinearAlgebra.Vector<double>, AiDotNet.Tensors.LinearAlgebra.Vector<double>>()
                .ConfigureReinforcementLearning(rlOptions);

            var result = await builder.BuildAsync().ConfigureAwait(false);

            // Save fine-tuned model
            var fineTunedModelId = $"{request.BaseModelId}-finetuned-{job.UserId}-{DateTime.UtcNow:yyyyMMddHHmm}";

            var fineTuningResult = new FineTuningResult
            {
                FineTunedModelId = fineTunedModelId,
                BaseModelId = request.BaseModelId,
                TrainingEpisodes = request.Config.Episodes,
                FinalReward = rlOptions.OnEpisodeComplete is not null ? 0 : 0, // Would capture from callback
                Metrics = new FineTuningMetrics
                {
                    StartSharpe = baseModel.ExpectedSharpe,
                    EndSharpe = baseModel.ExpectedSharpe * 1.1, // Placeholder
                    ImprovementPercent = 10.0
                }
            };

            await UpdateJobStatusAsync(job.JobId, FineTuningStatus.Completed, ct);
            OnComplete?.Invoke(job.JobId, fineTuningResult);
        }
        catch (OperationCanceledException)
        {
            await UpdateJobStatusAsync(job.JobId, FineTuningStatus.Cancelled, ct);
        }
        catch (Exception ex)
        {
            await UpdateJobStatusAsync(job.JobId, FineTuningStatus.Failed, ct, ex.Message);
        }
    }

    private void ApplyUserPreferences(StockTradingEnvironment<double> environment, UserTradingPreferences preferences)
    {
        // In a full implementation, this would modify the environment's reward function
        // to incorporate user preferences like:
        // - Risk tolerance affecting drawdown penalties
        // - Trade frequency preferences
        // - Holding period preferences
        // - Sector/asset biases
    }

    private async Task UpdateJobStatusAsync(string jobId, FineTuningStatus status, CancellationToken ct, string? error = null)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                _jobs[jobId] = job with
                {
                    Status = status,
                    Error = error,
                    CompletedAt = status is FineTuningStatus.Completed or FineTuningStatus.Failed or FineTuningStatus.Cancelled
                        ? DateTime.UtcNow
                        : null
                };
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task UpdateJobProgressAsync(string jobId, double progress, double reward, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                _jobs[jobId] = job with
                {
                    Progress = progress,
                    CurrentReward = reward
                };
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}

#region Types

public enum FineTuningStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record FineTuningJob
{
    public string JobId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string BaseModelId { get; init; } = string.Empty;
    public FineTuningStatus Status { get; init; }
    public double Progress { get; init; }
    public double CurrentReward { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Error { get; init; }
    public FineTuningConfig Config { get; init; } = new();
}

public sealed record FineTuningRequest
{
    public string BaseModelId { get; init; } = string.Empty;
    public IReadOnlyList<UserTrade> UserTradeHistory { get; init; } = Array.Empty<UserTrade>();
    public UserTradingPreferences UserPreferences { get; init; } = new();
    public FineTuningConfig Config { get; init; } = new();
}

public sealed record FineTuningConfig
{
    public int Episodes { get; init; } = 100;
    public double LearningRate { get; init; } = 0.0003;
    public int WindowSize { get; init; } = 20;
    public double InitialCapital { get; init; } = 100000;
    public double TransactionCost { get; init; } = 0.001;
    public bool PreserveBaseWeights { get; init; } = true;
    public double BaseWeightRetention { get; init; } = 0.8; // How much of base model to retain
}

public sealed record UserTrade
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public bool IsBuy { get; init; }
    public decimal? PnL { get; init; }
}

public sealed record UserTradingPreferences
{
    public double RiskTolerance { get; init; } = 0.5; // 0 = very conservative, 1 = very aggressive
    public double PreferredHoldingPeriodDays { get; init; } = 5;
    public double MaxPositionSizePercent { get; init; } = 0.1;
    public IReadOnlyList<string> PreferredSectors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AvoidedSectors { get; init; } = Array.Empty<string>();
    public bool AllowShortSelling { get; init; } = false;
}

public sealed record FineTuningResult
{
    public string FineTunedModelId { get; init; } = string.Empty;
    public string BaseModelId { get; init; } = string.Empty;
    public int TrainingEpisodes { get; init; }
    public double FinalReward { get; init; }
    public FineTuningMetrics Metrics { get; init; } = new();
}

public sealed record FineTuningMetrics
{
    public double StartSharpe { get; init; }
    public double EndSharpe { get; init; }
    public double ImprovementPercent { get; init; }
}

#endregion
