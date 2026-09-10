using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Factory for creating streaming indicator states.
/// </summary>
internal static class StreamingIndicatorFactory
{
    /// <summary>
    /// Creates a streaming indicator state for the given spec.
    /// </summary>
    public static IStreamingIndicatorState? CreateState(IndicatorSpec spec)
    {
        return spec.Name switch
        {
            IndicatorName.SimpleMovingAverage => new SimpleMovingAverageState(((SmaSpecOptions)spec.Options).Length),
            IndicatorName.ExponentialMovingAverage => new ExponentialMovingAverageState(((EmaSpecOptions)spec.Options).Length),
            IndicatorName.RelativeStrengthIndex => new RelativeStrengthIndexState(((RsiSpecOptions)spec.Options).Length),
            IndicatorName.MovingAverageConvergenceDivergence => new MovingAverageConvergenceDivergenceState(
                ((MacdSpecOptions)spec.Options).FastLength,
                ((MacdSpecOptions)spec.Options).SlowLength,
                ((MacdSpecOptions)spec.Options).SignalLength),
            IndicatorName.BollingerBands => new BollingerBandsState(
                ((BollingerBandsSpecOptions)spec.Options).Length,
                ((BollingerBandsSpecOptions)spec.Options).StdDevMult),
            IndicatorName.AverageTrueRange => new AverageTrueRangeState(((AtrSpecOptions)spec.Options).Length),
            _ => null
        };
    }

    /// <summary>
    /// Extracts the appropriate value from a streaming update.
    /// </summary>
    public static double ExtractValue(StreamingIndicatorStateUpdate update, IndicatorSpec spec)
    {
        if (spec.Output == IndicatorOutput.Primary || spec.Output == IndicatorOutput.MiddleBand)
        {
            return update.Value;
        }

        if (update.Outputs == null)
        {
            return double.NaN;
        }

        var key = GetOutputKey(spec.Name, spec.Output);
        return key != null && update.Outputs.TryGetValue(key, out var value) ? value : double.NaN;
    }

    /// <summary>
    /// The key this indicator publishes for this slot.
    /// </summary>
    /// <remarks>
    /// This knew about two indicators by hand - MACD and Bollinger Bands - and returned null for the
    /// other seven hundred and sixty-nine, which turned into a NaN series. The generated map is read
    /// out of the SetOutputValues calls, so every indicator that publishes a named output is covered
    /// and none of them are spelled out here.
    /// </remarks>
    private static string? GetOutputKey(IndicatorName name, IndicatorOutput output) =>
        GeneratedIndicatorOutputs.TryGetKey(name, output, out var key) ? key : null;
}
