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
        // A named key wins, and is read before the slot: a spec built from a key carries Output = Primary, so
        // the branch below would answer with the state's own value and never look at the named outputs - the
        // batch half of that same mistake is in BuilderArmBinding. See issue #219.
        if (spec.OutputKey is { } named)
        {
            return update.Outputs is not null && update.Outputs.TryGetValue(named, out var namedValue)
                ? namedValue
                : double.NaN;
        }

        // A spec that names no key wants the state's own value.
        return update.Value;
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
}
