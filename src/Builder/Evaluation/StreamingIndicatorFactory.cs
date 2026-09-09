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

    private static string? GetOutputKey(IndicatorName name, IndicatorOutput output)
    {
        if (name == IndicatorName.MovingAverageConvergenceDivergence)
        {
            return output switch
            {
                IndicatorOutput.Signal => "Signal",
                IndicatorOutput.Histogram => "Histogram",
                _ => null
            };
        }

        if (name == IndicatorName.BollingerBands)
        {
            return output switch
            {
                IndicatorOutput.UpperBand => "UpperBand",
                IndicatorOutput.LowerBand => "LowerBand",
                _ => "MiddleBand"
            };
        }

        return null;
    }
}
