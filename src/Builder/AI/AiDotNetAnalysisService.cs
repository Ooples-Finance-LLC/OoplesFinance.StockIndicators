using AiDotNet.Enums;
using AiDotNet.Finance.Forecasting.Transformers;
using AiDotNet.Finance.NLP;
using AiDotNet.Finance.Risk;
using AiDotNet.Models.Options;
using AiDotNet.NeuralNetworks;
using AiDotNet.Tensors.LinearAlgebra;

namespace OoplesFinance.StockIndicators.Builder.AI;

/// <summary>
/// Implementation of IAIAnalysisService using AiDotNet finance models.
/// Uses FinBERT for sentiment, PatchTST for forecasting, and NeuralVaR for risk.
/// </summary>
public sealed class AiDotNetAnalysisService : IAIAnalysisService, IDisposable
{
    private readonly AiDotNetOptions _options;
    private readonly SemaphoreSlim _modelLock = new(1, 1);

    // Lazy-initialized models
    private PatchTST<double>? _patchTstModel;
    private FinBERT<float>? _finbertModel;
    private NeuralVaR<double>? _neuralVarModel;

    private bool _disposed;

    /// <summary>
    /// Creates a new AiDotNet analysis service.
    /// </summary>
    public AiDotNetAnalysisService(AiDotNetOptions? options = null)
    {
        _options = options ?? new AiDotNetOptions();
    }

    /// <inheritdoc />
    public async Task<FinBertSentimentResult> AnalyzeSentimentAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var results = await AnalyzeBatchSentimentAsync(new[] { text }, cancellationToken)
            .ConfigureAwait(false);
        return results[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FinBertSentimentResult>> AnalyzeBatchSentimentAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textArray = texts.ToArray();
        if (textArray.Length == 0)
            return Array.Empty<FinBertSentimentResult>();

        await _modelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var finbert = GetOrCreateFinBertModel();

            // FinBERT's AnalyzeSentiment returns SentimentResult<float>[]
            var sentimentResults = finbert.AnalyzeSentiment(textArray);

            var results = new List<FinBertSentimentResult>();
            for (int i = 0; i < sentimentResults.Length; i++)
            {
                var sr = sentimentResults[i];

                // Get probabilities from the result
                var positive = sr.ClassProbabilities.TryGetValue("positive", out var pos) ? pos : 0f;
                var neutral = sr.ClassProbabilities.TryGetValue("neutral", out var neu) ? neu : 0f;
                var negative = sr.ClassProbabilities.TryGetValue("negative", out var neg) ? neg : 0f;

                // Score from -1 (bearish) to +1 (bullish)
                var score = (decimal)(positive - negative);

                results.Add(new FinBertSentimentResult
                {
                    Text = sr.OriginalText ?? textArray[i],
                    SentimentScore = score,
                    Sentiment = ClassifySentiment(score),
                    Confidence = (decimal)sr.Confidence,
                    Probabilities = new FinBertProbabilities
                    {
                        Positive = (decimal)positive,
                        Neutral = (decimal)neutral,
                        Negative = (decimal)negative
                    },
                    AnalyzedAt = DateTime.UtcNow
                });
            }

            return results;
        }
        finally
        {
            _modelLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PriceForecastResult> ForecastPriceAsync(
        string symbol,
        IReadOnlyList<OhlcvData> historicalPrices,
        int forecastHorizon = 5,
        CancellationToken cancellationToken = default)
    {
        if (historicalPrices.Count < _options.MinimumSequenceLength)
        {
            throw new ArgumentException(
                $"Need at least {_options.MinimumSequenceLength} data points for forecasting, got {historicalPrices.Count}");
        }

        await _modelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var patchTst = GetOrCreatePatchTstModel(historicalPrices.Count);

            // Prepare input tensor from OHLCV data
            var inputTensor = PrepareInputTensor(historicalPrices);

            // Use PatchTST's Forecast method
            var forecastTensor = patchTst.Forecast(inputTensor);

            // Convert to forecast points with confidence intervals
            var forecasts = new List<ForecastPoint>();
            var lastDate = historicalPrices[^1].Timestamp;
            var lastPrice = historicalPrices[^1].Close;

            for (int i = 0; i < Math.Min(forecastHorizon, forecastTensor.Shape[0]); i++)
            {
                var predictedChange = (decimal)forecastTensor[i, 0];
                var predictedPrice = lastPrice * (1 + predictedChange);

                // Estimate confidence intervals based on historical volatility
                var volatility = CalculateVolatility(historicalPrices);
                var stdDev = predictedPrice * volatility * (decimal)Math.Sqrt(i + 1);

                forecasts.Add(new ForecastPoint
                {
                    Timestamp = lastDate.AddDays(i + 1),
                    PredictedPrice = predictedPrice,
                    LowerBound95 = predictedPrice - 1.96m * stdDev,
                    UpperBound95 = predictedPrice + 1.96m * stdDev,
                    LowerBound80 = predictedPrice - 1.28m * stdDev,
                    UpperBound80 = predictedPrice + 1.28m * stdDev
                });

                lastPrice = predictedPrice;
            }

            // Determine overall direction
            var totalChange = forecasts.Count > 0
                ? (forecasts[^1].PredictedPrice - historicalPrices[^1].Close) / historicalPrices[^1].Close
                : 0;

            // Get metrics from the model
            var metrics = patchTst.GetFinancialMetrics();

            return new PriceForecastResult
            {
                Symbol = symbol,
                Forecasts = forecasts,
                Direction = ClassifyDirection(totalChange),
                Confidence = CalculateForecastConfidence(forecasts, historicalPrices),
                Metrics = new ForecastMetrics
                {
                    MAE = (decimal)(metrics.TryGetValue("MAE", out var mae) ? mae : 0),
                    RMSE = (decimal)(metrics.TryGetValue("RMSE", out var rmse) ? rmse : 0),
                    MAPE = (decimal)(metrics.TryGetValue("MAPE", out var mape) ? mape : 0),
                    R2 = (decimal)(metrics.TryGetValue("R2", out var r2) ? r2 : 0)
                },
                GeneratedAt = DateTime.UtcNow
            };
        }
        finally
        {
            _modelLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<NeuralVaRResult> CalculateNeuralVaRAsync(
        IReadOnlyList<PortfolioPosition> portfolio,
        decimal confidenceLevel = 0.95m,
        int holdingPeriod = 1,
        CancellationToken cancellationToken = default)
    {
        if (portfolio.Count == 0)
        {
            return new NeuralVaRResult
            {
                PortfolioValue = 0,
                VaR = 0,
                CVaR = 0,
                ConfidenceLevel = confidenceLevel,
                HoldingPeriod = holdingPeriod,
                RiskContributions = Array.Empty<RiskContribution>(),
                CalculatedAt = DateTime.UtcNow
            };
        }

        await _modelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var neuralVar = GetOrCreateNeuralVarModel(
                portfolio.Count,
                (double)confidenceLevel,
                holdingPeriod);

            var portfolioValue = portfolio.Sum(p => p.MarketValue);

            // Prepare input tensor with portfolio positions
            var inputTensor = PreparePortfolioTensor(portfolio);

            // Use NeuralVaR's CalculateRisk method
            var riskValue = neuralVar.CalculateRisk(inputTensor);

            // VaR as percentage of portfolio
            var varValue = (decimal)riskValue * portfolioValue;

            // CVaR (Expected Shortfall) - typically 1.2-1.4x VaR for normal distributions
            var cvarValue = varValue * 1.25m;

            // Calculate risk contributions
            var contributions = portfolio.Select((p, idx) =>
            {
                var weight = portfolioValue > 0 ? p.MarketValue / portfolioValue : 0;
                return new RiskContribution
                {
                    Symbol = p.Symbol,
                    MarketValue = p.MarketValue,
                    PortfolioWeight = weight,
                    RiskContributionPercent = weight, // Simplified - would need component VaR for accurate
                    MarginalVaR = varValue * weight
                };
            }).ToList();

            return new NeuralVaRResult
            {
                PortfolioValue = portfolioValue,
                VaR = varValue,
                CVaR = cvarValue,
                ConfidenceLevel = confidenceLevel,
                HoldingPeriod = holdingPeriod,
                RiskContributions = contributions,
                CalculatedAt = DateTime.UtcNow
            };
        }
        finally
        {
            _modelLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AITradingSignal> GetTradingSignalAsync(
        string symbol,
        MarketContext context,
        CancellationToken cancellationToken = default)
    {
        var signals = new List<(SignalType signal, decimal weight)>();

        // Sentiment signal
        if (context.SentimentScore != 0)
        {
            var sentimentSignal = context.SentimentScore switch
            {
                > 0.5m => SignalType.StrongBuy,
                > 0.2m => SignalType.Buy,
                < -0.5m => SignalType.StrongSell,
                < -0.2m => SignalType.Sell,
                _ => SignalType.Hold
            };
            signals.Add((sentimentSignal, 0.3m));
        }

        // Technical signal (RSI-based)
        if (context.RSI > 0)
        {
            var rsiSignal = context.RSI switch
            {
                < 30 => SignalType.Buy,
                < 40 => SignalType.Buy,
                > 70 => SignalType.Sell,
                > 60 => SignalType.Sell,
                _ => SignalType.Hold
            };
            signals.Add((rsiSignal, 0.25m));
        }

        // MACD signal
        if (context.MACD != 0 && context.MACDSignal != 0)
        {
            var macdSignal = context.MACD > context.MACDSignal
                ? SignalType.Buy
                : SignalType.Sell;
            signals.Add((macdSignal, 0.2m));
        }

        // Volume signal
        if (context.Volume24h > 0 && context.AverageVolume > 0)
        {
            var volumeRatio = context.Volume24h / (long)context.AverageVolume;
            if (volumeRatio > 2)
            {
                var volumeSignal = context.PriceChangePercent24h > 0
                    ? SignalType.Buy
                    : SignalType.Sell;
                signals.Add((volumeSignal, 0.15m));
            }
        }

        // Price forecast using PatchTST if we have historical data
        if (context.RecentPrices.Count >= _options.MinimumSequenceLength)
        {
            try
            {
                var forecast = await ForecastPriceAsync(symbol, context.RecentPrices, 5, cancellationToken)
                    .ConfigureAwait(false);

                var forecastSignal = forecast.Direction switch
                {
                    ForecastDirection.StrongBullish => SignalType.StrongBuy,
                    ForecastDirection.Bullish => SignalType.Buy,
                    ForecastDirection.StrongBearish => SignalType.StrongSell,
                    ForecastDirection.Bearish => SignalType.Sell,
                    _ => SignalType.Hold
                };
                signals.Add((forecastSignal, 0.1m));
            }
            catch
            {
                // Forecast failed, continue without it
            }
        }

        // Aggregate signals
        var aggregatedSignal = AggregateSignals(signals);
        var keyFactors = GenerateKeyFactors(context, signals);

        return new AITradingSignal
        {
            Symbol = symbol,
            Signal = aggregatedSignal.signal,
            Strength = aggregatedSignal.strength,
            Confidence = aggregatedSignal.confidence,
            SuggestedEntry = context.CurrentPrice,
            SuggestedStopLoss = CalculateStopLoss(context, aggregatedSignal.signal),
            SuggestedTakeProfit = CalculateTakeProfit(context, aggregatedSignal.signal),
            Reasoning = GenerateReasoning(context, aggregatedSignal.signal, keyFactors),
            KeyFactors = keyFactors,
            TimeHorizon = DetermineTimeHorizon(context),
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(_options.SignalExpirationHours)
        };
    }

    /// <inheritdoc />
    public async Task<float[]> GetEmbeddingsAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        await _modelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var finbert = GetOrCreateFinBertModel();

            // Tokenize the text
            var tokenIds = finbert.Tokenize(text, _options.MaxSequenceLength);

            // Create tensor from token IDs
            var tokenTensor = new Tensor<float>(
                tokenIds.Select(t => (float)t).ToArray(),
                [1, tokenIds.Length]);

            // Get sequence embedding using FinBERT's GetSequenceEmbedding
            var embeddingTensor = finbert.GetSequenceEmbedding(tokenTensor);

            // Convert to float array
            var embedding = new float[embeddingTensor.Shape[^1]];
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = embeddingTensor[0, i];
            }

            return embedding;
        }
        finally
        {
            _modelLock.Release();
        }
    }

    #region Model Creation Methods

    private FinBERT<float> GetOrCreateFinBertModel()
    {
        if (_finbertModel is not null)
            return _finbertModel;

        var finbertOptions = new FinBERTOptions<float>
        {
            MaxSequenceLength = _options.MaxSequenceLength,
            NumSentimentClasses = 3,
            UsePretrainedWeights = true,
            PretrainedModelPath = _options.FinBertModelPath,
            DropoutRate = _options.Dropout
        };

        var architecture = new NeuralNetworkArchitecture<float>(
            inputType: InputType.OneDimensional,
            taskType: NeuralNetworkTaskType.MultiClassClassification,
            inputSize: finbertOptions.MaxSequenceLength,
            outputSize: finbertOptions.NumSentimentClasses);

        // Use ONNX mode if model path is provided, otherwise native mode
        _finbertModel = !string.IsNullOrEmpty(_options.FinBertModelPath)
            ? new FinBERT<float>(architecture, _options.FinBertModelPath, finbertOptions)
            : new FinBERT<float>(architecture, finbertOptions);

        return _finbertModel;
    }

    private PatchTST<double> GetOrCreatePatchTstModel(int dataLength)
    {
        if (_patchTstModel is not null)
            return _patchTstModel;

        var sequenceLength = Math.Min(dataLength, _options.SequenceLength);

        var architecture = new NeuralNetworkArchitecture<double>(
            inputType: InputType.ThreeDimensional,
            taskType: NeuralNetworkTaskType.TimeSeriesForecasting,
            inputHeight: sequenceLength,
            inputWidth: _options.NumFeatures,
            outputSize: _options.PredictionHorizon);

        // Use ONNX mode if model path is provided, otherwise native mode
        _patchTstModel = !string.IsNullOrEmpty(_options.PatchTstModelPath)
            ? new PatchTST<double>(
                architecture: architecture,
                onnxModelPath: _options.PatchTstModelPath,
                sequenceLength: sequenceLength,
                predictionHorizon: _options.PredictionHorizon,
                numFeatures: _options.NumFeatures,
                patchSize: _options.PatchSize,
                stride: _options.Stride)
            : new PatchTST<double>(
                architecture: architecture,
                sequenceLength: sequenceLength,
                predictionHorizon: _options.PredictionHorizon,
                numFeatures: _options.NumFeatures,
                patchSize: _options.PatchSize,
                stride: _options.Stride,
                numLayers: _options.NumLayers,
                numHeads: _options.NumHeads,
                modelDimension: _options.ModelDimension,
                feedForwardDimension: _options.FeedForwardDimension,
                channelIndependent: true,
                useInstanceNormalization: true,
                dropout: _options.Dropout);

        return _patchTstModel;
    }

    private NeuralVaR<double> GetOrCreateNeuralVarModel(
        int numPositions,
        double confidenceLevel,
        int holdingPeriod)
    {
        if (_neuralVarModel is not null)
            return _neuralVarModel;

        var varOptions = new VaROptions<double>
        {
            ConfidenceLevel = confidenceLevel,
            TimeHorizon = holdingPeriod,
            NumFeatures = numPositions * 2
        };

        var architecture = new NeuralNetworkArchitecture<double>(
            inputType: InputType.TwoDimensional,
            taskType: NeuralNetworkTaskType.Regression,
            inputSize: varOptions.NumFeatures,
            outputSize: 1);

        // Use ONNX mode if model path is provided, otherwise native mode
        _neuralVarModel = !string.IsNullOrEmpty(_options.NeuralVarModelPath)
            ? new NeuralVaR<double>(architecture, _options.NeuralVarModelPath, varOptions)
            : new NeuralVaR<double>(architecture, varOptions);

        return _neuralVarModel;
    }

    #endregion

    #region Helper Methods

    private Tensor<double> PrepareInputTensor(IReadOnlyList<OhlcvData> prices)
    {
        var sequenceLength = Math.Min(prices.Count, _options.SequenceLength);
        var startIdx = prices.Count - sequenceLength;

        var data = new double[sequenceLength * _options.NumFeatures];

        for (int i = 0; i < sequenceLength; i++)
        {
            var bar = prices[startIdx + i];
            var prevClose = i > 0 ? prices[startIdx + i - 1].Close : bar.Open;

            data[i * _options.NumFeatures + 0] = (double)(bar.Open / prevClose - 1);
            data[i * _options.NumFeatures + 1] = (double)(bar.High / prevClose - 1);
            data[i * _options.NumFeatures + 2] = (double)(bar.Low / prevClose - 1);
            data[i * _options.NumFeatures + 3] = (double)(bar.Close / prevClose - 1);

            var avgVolume = prices.Take(i + startIdx + 1).Average(p => p.Volume);
            data[i * _options.NumFeatures + 4] = avgVolume > 0 ? bar.Volume / avgVolume - 1 : 0;

            if (_options.NumFeatures > 5)
            {
                data[i * _options.NumFeatures + 5] = (double)((bar.High - bar.Low) / prevClose);
                data[i * _options.NumFeatures + 6] = (double)((bar.Close - bar.Open) / prevClose);
            }
        }

        return new Tensor<double>(data, [1, sequenceLength, _options.NumFeatures]);
    }

    private Tensor<double> PreparePortfolioTensor(IReadOnlyList<PortfolioPosition> portfolio)
    {
        var totalValue = portfolio.Sum(p => p.MarketValue);
        var data = new double[portfolio.Count * 2];

        for (int i = 0; i < portfolio.Count; i++)
        {
            data[i * 2] = (double)portfolio[i].MarketValue;
            data[i * 2 + 1] = totalValue > 0 ? (double)(portfolio[i].MarketValue / totalValue) : 0;
        }

        return new Tensor<double>(data, [1, portfolio.Count, 2]);
    }

    private decimal CalculateVolatility(IReadOnlyList<OhlcvData> prices)
    {
        if (prices.Count < 2) return 0.02m;

        var returns = new List<double>();
        for (int i = 1; i < prices.Count; i++)
        {
            var ret = (double)(prices[i].Close / prices[i - 1].Close - 1);
            returns.Add(ret);
        }

        var mean = returns.Average();
        var variance = returns.Sum(r => Math.Pow(r - mean, 2)) / returns.Count;
        return (decimal)Math.Sqrt(variance);
    }

    private static FinBertSentiment ClassifySentiment(decimal score) => score switch
    {
        > 0.5m => FinBertSentiment.VeryBullish,
        > 0.2m => FinBertSentiment.Bullish,
        < -0.5m => FinBertSentiment.VeryBearish,
        < -0.2m => FinBertSentiment.Bearish,
        _ => FinBertSentiment.Neutral
    };

    private static ForecastDirection ClassifyDirection(decimal change) => change switch
    {
        > 0.05m => ForecastDirection.StrongBullish,
        > 0.02m => ForecastDirection.Bullish,
        < -0.05m => ForecastDirection.StrongBearish,
        < -0.02m => ForecastDirection.Bearish,
        _ => ForecastDirection.Neutral
    };

    private decimal CalculateForecastConfidence(List<ForecastPoint> forecasts, IReadOnlyList<OhlcvData> historical)
    {
        if (forecasts.Count == 0) return 0;

        var volatility = CalculateVolatility(historical);
        var volConfidence = Math.Max(0.3m, 1 - volatility * 10);

        var directionChanges = 0;
        for (int i = 1; i < forecasts.Count; i++)
        {
            var prevChange = forecasts[i - 1].PredictedPrice - (i > 1 ? forecasts[i - 2].PredictedPrice : historical[^1].Close);
            var currChange = forecasts[i].PredictedPrice - forecasts[i - 1].PredictedPrice;
            if (Math.Sign(prevChange) != Math.Sign(currChange))
                directionChanges++;
        }

        var consistencyConfidence = forecasts.Count > 1
            ? 1 - (decimal)directionChanges / (forecasts.Count - 1)
            : 0.5m;

        return Math.Clamp(volConfidence * 0.5m + consistencyConfidence * 0.5m, 0.1m, 0.9m);
    }

    private (SignalType signal, decimal strength, decimal confidence) AggregateSignals(
        List<(SignalType signal, decimal weight)> signals)
    {
        if (signals.Count == 0)
            return (SignalType.Hold, 0.5m, 0.3m);

        var totalWeight = signals.Sum(s => s.weight);
        var weightedScore = signals.Sum(s => SignalToScore(s.signal) * s.weight) / totalWeight;

        var agreement = signals.Count(s => Math.Sign(SignalToScore(s.signal)) == Math.Sign(weightedScore))
            / (decimal)signals.Count;

        var signal = ScoreToSignal(weightedScore);
        var strength = Math.Abs(weightedScore);
        var confidence = Math.Clamp(agreement * 0.6m + strength * 0.4m, 0.1m, 0.9m);

        return (signal, strength, confidence);
    }

    private static decimal SignalToScore(SignalType signal) => signal switch
    {
        SignalType.StrongBuy => 1.0m,
        SignalType.Buy => 0.5m,
        SignalType.Hold => 0.0m,
        SignalType.Sell => -0.5m,
        SignalType.StrongSell => -1.0m,
        _ => 0.0m
    };

    private static SignalType ScoreToSignal(decimal score) => score switch
    {
        > 0.6m => SignalType.StrongBuy,
        > 0.2m => SignalType.Buy,
        < -0.6m => SignalType.StrongSell,
        < -0.2m => SignalType.Sell,
        _ => SignalType.Hold
    };

    private static decimal? CalculateStopLoss(MarketContext context, SignalType signal)
    {
        if (signal == SignalType.Hold) return null;

        var stopPercent = 0.03m;
        return signal is SignalType.Buy or SignalType.StrongBuy
            ? context.CurrentPrice * (1 - stopPercent)
            : context.CurrentPrice * (1 + stopPercent);
    }

    private static decimal? CalculateTakeProfit(MarketContext context, SignalType signal)
    {
        if (signal == SignalType.Hold) return null;

        var profitPercent = signal is SignalType.StrongBuy or SignalType.StrongSell ? 0.10m : 0.06m;
        return signal is SignalType.Buy or SignalType.StrongBuy
            ? context.CurrentPrice * (1 + profitPercent)
            : context.CurrentPrice * (1 - profitPercent);
    }

    private static IReadOnlyList<string> GenerateKeyFactors(
        MarketContext context,
        List<(SignalType signal, decimal weight)> signals)
    {
        var factors = new List<string>();

        if (Math.Abs(context.SentimentScore) > 0.3m)
            factors.Add($"Sentiment: {(context.SentimentScore > 0 ? "Bullish" : "Bearish")} ({context.SentimentScore:+0.00;-0.00})");

        if (context.RSI is < 30 or > 70)
            factors.Add($"RSI: {(context.RSI < 30 ? "Oversold" : "Overbought")} ({context.RSI:F1})");

        if (context.MACD != 0 && context.MACDSignal != 0)
            factors.Add($"MACD: {(context.MACD > context.MACDSignal ? "Bullish crossover" : "Bearish crossover")}");

        if (context.Volume24h > (long)context.AverageVolume * 2)
            factors.Add($"Volume: {context.Volume24h / (long)context.AverageVolume:F1}x average");

        if (Math.Abs(context.PriceChangePercent24h) > 3)
            factors.Add($"24h Change: {context.PriceChangePercent24h:+0.0;-0.0}%");

        return factors;
    }

    private static string GenerateReasoning(MarketContext context, SignalType signal, IReadOnlyList<string> factors)
    {
        var direction = signal switch
        {
            SignalType.StrongBuy or SignalType.Buy => "bullish",
            SignalType.StrongSell or SignalType.Sell => "bearish",
            _ => "neutral"
        };

        if (factors.Count == 0)
            return $"Signal is {direction} based on overall market conditions.";

        return $"Signal is {direction} based on: {string.Join("; ", factors)}.";
    }

    private static SignalTimeHorizon DetermineTimeHorizon(MarketContext context)
    {
        if (context.SocialMentions > 1000)
            return SignalTimeHorizon.Intraday;

        if (context.RSI is < 25 or > 75)
            return SignalTimeHorizon.Swing;

        return SignalTimeHorizon.Swing;
    }

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _modelLock.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for AiDotNet analysis service.
/// </summary>
public sealed class AiDotNetOptions
{
    /// <summary>Path to pretrained FinBERT ONNX model.</summary>
    public string FinBertModelPath { get; set; } = string.Empty;

    /// <summary>Path to pretrained PatchTST ONNX model.</summary>
    public string PatchTstModelPath { get; set; } = string.Empty;

    /// <summary>Path to pretrained NeuralVaR ONNX model.</summary>
    public string NeuralVarModelPath { get; set; } = string.Empty;

    /// <summary>Maximum sequence length for text processing.</summary>
    public int MaxSequenceLength { get; set; } = 512;

    /// <summary>Sequence length for time series models.</summary>
    public int SequenceLength { get; set; } = 96;

    /// <summary>Minimum sequence length required for forecasting.</summary>
    public int MinimumSequenceLength { get; set; } = 30;

    /// <summary>Prediction horizon (number of future periods).</summary>
    public int PredictionHorizon { get; set; } = 24;

    /// <summary>Number of input features.</summary>
    public int NumFeatures { get; set; } = 7;

    /// <summary>Patch size for PatchTST.</summary>
    public int PatchSize { get; set; } = 16;

    /// <summary>Stride between patches.</summary>
    public int Stride { get; set; } = 8;

    /// <summary>Number of transformer layers.</summary>
    public int NumLayers { get; set; } = 3;

    /// <summary>Number of attention heads.</summary>
    public int NumHeads { get; set; } = 4;

    /// <summary>Model dimension.</summary>
    public int ModelDimension { get; set; } = 128;

    /// <summary>Feed-forward dimension.</summary>
    public int FeedForwardDimension { get; set; } = 256;

    /// <summary>Dropout rate.</summary>
    public double Dropout { get; set; } = 0.05;

    /// <summary>Hours until a trading signal expires.</summary>
    public int SignalExpirationHours { get; set; } = 4;
}
