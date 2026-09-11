//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright � Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

// Suppress obsolete warnings for internal Calculate* method calls - this helper
// needs to invoke these methods to provide the dynamic indicator invocation API.
#pragma warning disable CS0618

using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;
using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Helpers;

public static class CalculationsHelper
{
    private static readonly ConditionalWeakTable<StockData, Dictionary<DerivedSeriesKind, List<double>>> DerivedSeriesCache
        = new();

    public static T GetLastOrDefault<T>(IReadOnlyList<T> list)
    {
        return list.Count > 0 ? list[list.Count - 1] : default!;
    }

    private static double SumValues(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

#if NET8_0_OR_GREATER
        if (values is List<double> list)
        {
            return VectorMath.Sum(SpanCompat.AsReadOnlySpan(list));
        }

        if (values is double[] array)
        {
            return VectorMath.Sum(array);
        }
#endif

        var sum = 0d;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum;
    }

    internal static List<double> GetDifferenceList(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count != right.Count)
        {
            throw new ArgumentException("Input lists must have the same length.");
        }

        var count = left.Count;
#if NET8_0_OR_GREATER
        if (left is List<double> leftList && right is List<double> rightList)
        {
            var output = SpanCompat.CreateOutputBuffer(count);
            VectorMath.Diff(SpanCompat.AsReadOnlySpan(leftList), SpanCompat.AsReadOnlySpan(rightList), output.Span);
            return output.ToList();
        }

        if (left is double[] leftArray && right is double[] rightArray)
        {
            var output = SpanCompat.CreateOutputBuffer(count);
            VectorMath.Diff(leftArray, rightArray, output.Span);
            return output.ToList();
        }
#endif

        var list = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(left[i] - right[i]);
        }

        return list;
    }

    private static bool SequenceEqualValues(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    public static void SetOutputValues(this StockData stockData, Func<Dictionary<string, List<double>>> outputFactory)
    {
        if (!ShouldIncludeOutputValues(stockData))
        {
            stockData.OutputValues?.Clear();
            return;
        }

        var outputs = outputFactory();
        if (TryGetRoundingDigits(stockData, out var roundingDigits))
        {
            outputs = RoundOutputValues(outputs, roundingDigits);
        }

        stockData.OutputValues = outputs;
    }

    public static List<Signal>? CreateSignalsList(StockData stockData, int capacity = 0)
    {
        if (!ShouldIncludeSignals(stockData))
        {
            return null;
        }

        if (capacity <= 0)
        {
            capacity = stockData.Count;
        }

        return capacity > 0 ? new List<Signal>(capacity) : new List<Signal>();
    }

    public static void SetSignals(this StockData stockData, List<Signal>? signalsList)
    {
        if (!ShouldIncludeSignals(stockData) || signalsList == null)
        {
            stockData.SignalsList?.Clear();
            return;
        }

        stockData.SignalsList = signalsList;
    }

    /// <summary>
    /// Hands the next calculation its input series, unaltered.
    /// </summary>
    /// <remarks>
    /// Not SetCustomValues. That publishes an indicator's OUTPUT, and honours IncludeCustomValues (which can
    /// drop it) and RoundingDigits (which rounds it). A caller's input series is neither: with
    /// IncludeCustomValues off, SetCustomValues cleared it and UseInput silently did nothing, and with
    /// RoundingDigits set the next indicator computed on rounded input.
    /// </remarks>
    internal static void SetInputSeries(this StockData stockData, List<double> series) =>
        stockData.CustomValuesList = series;

    /// <summary>
    /// A copy of the caller's input series, taken before a composite's components publish their own outputs.
    /// </summary>
    /// <remarks>
    /// Empty when the caller chained nothing. <see cref="StockData.CustomValuesList"/> has a public setter and can
    /// be null; a null series reads as the bars' own input, as <c>GetInputValuesList</c> treats it, instead of
    /// throwing from the copy.
    /// </remarks>
    internal static List<double> CaptureInputSeries(this StockData stockData) =>
        stockData.CustomValuesList is { } series ? new List<double>(series) : new List<double>();

    /// <summary>
    /// Hands the next component of a composite indicator the caller's input again, after an earlier
    /// component published its own output.
    /// </summary>
    /// <remarks>
    /// Every Calculate method leaves its result on CustomValuesList for chaining, so a second component
    /// called straight after a first computes on the first one's output. The signals go too: a component
    /// that publishes signals and no single series makes the next input read refuse to run.
    /// </remarks>
    internal static void RestoreInputSeries(this StockData stockData, List<double> callerSeries)
    {
        stockData.SetInputSeries(new List<double>(callerSeries));
        stockData.SignalsList = new List<Signal>();
    }

    public static void SetCustomValues(this StockData stockData, List<double> customValuesList)
    {
        if (!ShouldIncludeCustomValues(stockData))
        {
            stockData.CustomValuesList?.Clear();
            return;
        }

        if (TryGetRoundingDigits(stockData, out var roundingDigits))
        {
            stockData.CustomValuesList = RoundValuesList(customValuesList, roundingDigits);
            return;
        }

        stockData.CustomValuesList = customValuesList;
    }

    private static bool ShouldIncludeOutputValues(StockData stockData)
    {
        return stockData.Options?.IncludeOutputValues ?? true;
    }

    private static bool ShouldIncludeSignals(StockData stockData)
    {
        return stockData.Options?.IncludeSignals ?? true;
    }

    private static bool ShouldIncludeCustomValues(StockData stockData)
    {
        return stockData.Options?.IncludeCustomValues ?? true;
    }

    private static bool TryGetRoundingDigits(StockData stockData, out int roundingDigits)
    {
        var digits = stockData.Options?.RoundingDigits;
        if (!digits.HasValue)
        {
            roundingDigits = 0;
            return false;
        }

        roundingDigits = digits.Value;
        if (roundingDigits < -15)
        {
            roundingDigits = -15;
        }
        else if (roundingDigits > 15)
        {
            roundingDigits = 15;
        }

        return true;
    }

    internal static List<double> GetDerivedSeriesList(StockData stockData, DerivedSeriesKind kind)
    {
        if (!CanCacheDerivedSeries(stockData))
        {
            return BuildDerivedSeriesList(stockData, kind);
        }

        var cache = DerivedSeriesCache.GetOrCreateValue(stockData);
        if (cache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var series = BuildDerivedSeriesList(stockData, kind);
        cache[kind] = series;
        return series;
    }

    /// <summary>
    /// The population standard deviation of each bar's trailing window of <paramref name="input"/>: 0 until
    /// the window is full.
    /// </summary>
    /// <remarks>
    /// The standard deviation an indicator's source formula means by <c>stdev(src, length)</c>: every value in
    /// the window measured from that window's own mean. Not <c>CalculateStandardDeviationVolatility</c>, which
    /// measures each value from the moving average at its own bar and so is a different quantity - 55% wider
    /// than this on a typical price series. Streaming computes the same thing in RollingStandardDeviation.
    /// </remarks>
    internal static List<double> GetStandardDeviationList(List<double> input, int length)
    {
        var buffer = SpanCompat.CreateOutputBuffer(input.Count);
        VolatilityCore.StandardDeviation(SpanCompat.AsReadOnlySpan(input), buffer.Span, Math.Max(1, length));

        return buffer.ToList();
    }

    /// <summary>
    /// The average of each bar's trailing window of <paramref name="input"/>, summed afresh every bar: 0
    /// until the window is full.
    /// </summary>
    /// <remarks>
    /// A simple moving average by value, without the running sum. A running sum of values that are all 0
    /// leaves a residue near 1e-19 rather than 0, and a ratio that divides by its root turns that into
    /// nonsense. Streaming sums the same window in the same order.
    /// </remarks>
    internal static List<double> GetExactWindowAverageList(List<double> input, int length)
    {
        length = Math.Max(1, length);
        var output = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            if (i < length - 1)
            {
                output.Add(0);
                continue;
            }

            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sum += input[j];
            }

            output.Add(sum / length);
        }

        return output;
    }

    /// <summary>
    /// The volume-weighted mean of each bar's trailing window of <paramref name="input"/>, partial at the start.
    /// </summary>
    /// <remarks>sum(volume * value, length) / sum(volume, length), as LazyBear's calc_zvwap takes its mean.</remarks>
    internal static List<double> GetRollingVolumeWeightedMeanList(List<double> input, List<double> volumes, int length)
    {
        length = Math.Max(1, length);
        var output = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            double volumePriceSum = 0, volumeSum = 0;
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
            {
                volumePriceSum += volumes[j] * input[j];
                volumeSum += volumes[j];
            }

            output.Add(volumeSum != 0 ? volumePriceSum / volumeSum : 0);
        }

        return output;
    }

    /// <summary>
    /// Each value's distance from its mean in units of sqrt(sma((value - mean)^2, length)): LazyBear's
    /// calc_zvwap, with the squared distances averaged exactly over the window.
    /// </summary>
    internal static List<double> GetZScoreList(List<double> input, List<double> means, int length)
    {
        var devSquared = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            var deviation = input[i] - means[i];
            devSquared.Add(deviation * deviation);
        }

        var variance = GetExactWindowAverageList(devSquared, length);
        var output = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            var deviationSd = Math.Sqrt(variance[i]);
            output.Add(deviationSd != 0 ? (input[i] - means[i]) / deviationSd : 0);
        }

        return output;
    }

    internal static List<double> GetTrueRangeList(StockData stockData)
    {
        return GetDerivedSeriesList(stockData, DerivedSeriesKind.TrueRange);
    }

    private static bool CanCacheDerivedSeries(StockData stockData)
    {
        if (!(stockData.Options?.EnableDerivedSeriesCache ?? true))
        {
            return false;
        }

        return stockData.CustomValuesList == null || stockData.CustomValuesList.Count == 0;
    }

    private static IReadOnlyList<double> GetDerivedCloseList(StockData stockData)
    {
        if (stockData.CustomValuesList != null && stockData.CustomValuesList.Count > 0)
        {
            return stockData.CustomValuesList;
        }

        return stockData.ClosePrices;
    }

    private static List<double> BuildDerivedSeriesList(StockData stockData, DerivedSeriesKind kind)
    {
        var count = stockData.Count;
        var list = new List<double>(count);
        if (count == 0)
        {
            return list;
        }

        var opens = stockData.OpenPrices;
        var closes = GetDerivedCloseList(stockData);
        IReadOnlyList<double> highs = stockData.HighPrices;
        IReadOnlyList<double> lows = stockData.LowPrices;
        if (!ReferenceEquals(closes, stockData.ClosePrices) && !SequenceEqualValues(closes, stockData.ClosePrices))
        {
            // A chained series stands in for the close, and each bar's range follows the per-bar rule, as
            // GetInputValuesList and the streaming CustomInputState apply it. Built from the real high and low
            // around a custom close, a true range measured the gap between two different series: a log-close
            // of about 5 against highs near 180 gave ATR bands of +-400.
            (highs, lows) = GetCustomRangeLists(closes, stockData.HighPrices, stockData.LowPrices);
        }

        switch (kind)
        {
            case DerivedSeriesKind.Hl2:
                for (var i = 0; i < count; i++)
                {
                    list.Add((highs[i] + lows[i]) / 2);
                }
                break;
            case DerivedSeriesKind.Hlc3:
                for (var i = 0; i < count; i++)
                {
                    list.Add((highs[i] + lows[i] + closes[i]) / 3);
                }
                break;
            case DerivedSeriesKind.Ohlc4:
                for (var i = 0; i < count; i++)
                {
                    list.Add((opens[i] + highs[i] + lows[i] + closes[i]) / 4);
                }
                break;
            case DerivedSeriesKind.WeightedClose:
                for (var i = 0; i < count; i++)
                {
                    list.Add((highs[i] + lows[i] + (closes[i] * 2)) / 4);
                }
                break;
            case DerivedSeriesKind.AveragePrice:
                for (var i = 0; i < count; i++)
                {
                    list.Add((opens[i] + closes[i]) / 2);
                }
                break;
            case DerivedSeriesKind.TrueRange:
                for (var i = 0; i < count; i++)
                {
                    // For the first bar, use current close as prevClose (TR = High - Low)
                    // This avoids artificially high TR values when there's no previous bar
                    var prevClose = i >= 1 ? closes[i - 1] : closes[i];
                    list.Add(CalculateTrueRange(highs[i], lows[i], prevClose));
                }
                break;
            default:
                break;
        }

        return list;
    }

    private static List<double> RoundValuesList(List<double> values, int roundingDigits)
    {
        var count = values.Count;
        var rounded = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            rounded.Add(Math.Round(values[i], roundingDigits));
        }

        return rounded;
    }

    private static Dictionary<string, List<double>> RoundOutputValues(Dictionary<string, List<double>> outputs, int roundingDigits)
    {
        var rounded = new Dictionary<string, List<double>>(outputs.Count);
        foreach (var kvp in outputs)
        {
            rounded[kvp.Key] = RoundValuesList(kvp.Value, roundingDigits);
        }

        return rounded;
    }

    /// <summary>
    /// Calculates the user chosen moving average with user's custom settings
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="movingAvgType"></param>
    /// <param name="length"></param>
    /// <param name="customValuesList"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    /// <summary>
    /// A moving average of <paramref name="customValuesList"/>, or of the input series when none is given.
    /// </summary>
    /// <remarks>
    /// Leaves the caller's series exactly as it found it. It used to publish the average onto
    /// <see cref="StockData.CustomValuesList"/>, so whatever an indicator calculated NEXT ran on the average
    /// rather than the price: Bollinger Bands measured the standard deviation of its own middle band. It works
    /// on a copy because the calculations it delegates to clear the current series in place when
    /// IncludeCustomValues is off, and that series can be the very list the caller is holding.
    /// </remarks>
    public static List<double> GetMovingAverageList(StockData stockData, MovingAvgType movingAvgType, int length, List<double>? customValuesList = null,
        int? fastLength = null, int? slowLength = null)
    {
        var callerSeries = stockData.CustomValuesList;
        stockData.SetInputSeries(customValuesList is not null ? new List<double>(customValuesList) : stockData.CaptureInputSeries());
        try
        {
            return GetMovingAverageListCore(stockData, movingAvgType, length, customValuesList, fastLength, slowLength);
        }
        finally
        {
            stockData.SetInputSeries(callerSeries);
        }
    }

    private static List<double> GetMovingAverageListCore(StockData stockData, MovingAvgType movingAvgType, int length,
        List<double>? customValuesList, int? fastLength, int? slowLength)
    {
        List<double> movingAvgList = new();

        // Fast path for moving averages with simple (input, output, length) Core signatures
        // Note: All Core methods have been verified to match Calculate methods
        if (movingAvgType is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage
            or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod
            or MovingAvgType.DoubleExponentialMovingAverage or MovingAvgType.TripleExponentialMovingAverage
            or MovingAvgType.HullMovingAverage or MovingAvgType.McGinleyDynamicIndicator
            or MovingAvgType.TillsonT3MovingAverage or MovingAvgType.VariableIndexDynamicAverage
            or MovingAvgType.VariableMovingAverage or MovingAvgType.ArnaudLegouxMovingAverage
            or MovingAvgType.LeastSquaresMovingAverage or MovingAvgType.SineWeightedMovingAverage
            or MovingAvgType.RegularizedExponentialMovingAverage or MovingAvgType.JurikMovingAverage
            or MovingAvgType.EndPointWeightedMovingAverage or MovingAvgType.CubedWeightedMovingAverage
            or MovingAvgType.NaturalMovingAverage or MovingAvgType.AlphaDecreasingExponentialMovingAverage
            or MovingAvgType.AdaptiveExponentialMovingAverage or MovingAvgType.AutonomousRecursiveMovingAverage
            or MovingAvgType.AdaptiveLeastSquares or MovingAvgType.ParabolicWeightedMovingAverage
            or MovingAvgType.UltimateMovingAverage or MovingAvgType.SquareRootWeightedMovingAverage
            or MovingAvgType.Spencer15PointMovingAverage or MovingAvgType.Spencer21PointMovingAverage
            or MovingAvgType.SlowSmoothedMovingAverage or MovingAvgType.QuickMovingAverage
            or MovingAvgType.EhlersBetterExponentialMovingAverage or MovingAvgType.PentupleExponentialMovingAverage
            or MovingAvgType.QuadrupleExponentialMovingAverage or MovingAvgType.EhlersZeroLagExponentialMovingAverage
            or MovingAvgType.EhlersFractalAdaptiveMovingAverage or MovingAvgType.EhlersAdaptiveLaguerreFilter
            or MovingAvgType.DampedSineWaveWeightedFilter or MovingAvgType.FibonacciWeightedMovingAverage
            or MovingAvgType.GeneralizedDoubleExponentialMovingAverage or MovingAvgType.Ehlers2PoleButterworthFilterV1
            or MovingAvgType.Ehlers2PoleButterworthFilterV2 or MovingAvgType.Ehlers3PoleButterworthFilterV1
            or MovingAvgType.Ehlers3PoleButterworthFilterV2 or MovingAvgType.Ehlers2PoleSuperSmootherFilterV1
            or MovingAvgType.Ehlers2PoleSuperSmootherFilterV2 or MovingAvgType.Ehlers3PoleSuperSmootherFilter
            or MovingAvgType.EhlersSimpleDecycler or MovingAvgType.EhlersHammingMovingAverage
            or MovingAvgType.DistanceWeightedMovingAverage or MovingAvgType.EhlersFilter
            or MovingAvgType.EhlersFiniteImpulseResponseFilter or MovingAvgType.EhlersInfiniteImpulseResponseFilter
            or MovingAvgType.TriangularMovingAverage or MovingAvgType.LinearRegression
            or MovingAvgType.SymmetricallyWeightedMovingAverage or MovingAvgType.RepulsionMovingAverage
            or MovingAvgType.EhlersHannMovingAverage or MovingAvgType.EhlersTriangleMovingAverage
            or MovingAvgType.ZeroLagExponentialMovingAverage or MovingAvgType.HoltExponentialMovingAverage
            or MovingAvgType.KaufmanAdaptiveMovingAverage or MovingAvgType.EhlersSuperSmootherFilter
            or MovingAvgType.EhlersDeviationScaledMovingAverage
            // New fast path types
            or MovingAvgType.AhrensMovingAverage or MovingAvgType.DoubleExponentialSmoothing
            or MovingAvgType.CompoundRatioMovingAverage or MovingAvgType.CorrectedMovingAverage
            or MovingAvgType.DynamicallyAdjustableFilter or MovingAvgType.DynamicallyAdjustableMovingAverage
            or MovingAvgType.LinearWeightedMovingAverage or MovingAvgType.LeoMovingAverage
            or MovingAvgType.McNichollMovingAverage or MovingAvgType._3HMA
            or MovingAvgType.ZeroLagTripleExponentialMovingAverage or MovingAvgType.ZeroLowLagMovingAverage
            or MovingAvgType.WildersSummationMethod or MovingAvgType.SimplifiedWeightedMovingAverage
            or MovingAvgType.SimplifiedLeastSquaresMovingAverage or MovingAvgType.SharpModifiedMovingAverage
            or MovingAvgType.TillsonIE2 or MovingAvgType.RecursiveMovingTrendAverage
            or MovingAvgType.QuadraticMovingAverage or MovingAvgType.MultiDepthZeroLagExponentialMovingAverage
            or MovingAvgType.HullEstimate or MovingAvgType.InverseDistanceWeightedMovingAverage
            or MovingAvgType.Trimean or MovingAvgType.WellRoundedMovingAverage
            or MovingAvgType.LinearRegressionLine or MovingAvgType.LinearExtrapolation
            or MovingAvgType.JsaMovingAverage
            // Phase 2 fast path types (58 additional Core methods)
            or MovingAvgType.SelfWeightedMovingAverage or MovingAvgType.HendersonWeightedMovingAverage
            or MovingAvgType.FareySequenceWeightedMovingAverage or MovingAvgType.RightSidedRickerMovingAverage
            or MovingAvgType.HampelFilter or MovingAvgType.SequentiallyFilteredMovingAverage
            or MovingAvgType.KalmanSmoother or MovingAvgType.ModularFilter
            or MovingAvgType.RetentionAccelerationFilter or MovingAvgType.SettingLessTrendStepFiltering
            or MovingAvgType.ShapeshiftingMovingAverage or MovingAvgType.VariableLengthMovingAverage
            or MovingAvgType.EhlersGaussianFilter or MovingAvgType.EhlersRecursiveMedianFilter
            or MovingAvgType._1LCLeastSquaresMovingAverage or MovingAvgType.EhlersDeviationScaledSuperSmoother
            or MovingAvgType.EhlersOptimumEllipticFilter or MovingAvgType.EhlersModifiedOptimumEllipticFilter
            or MovingAvgType.EhlersChebyshevLowPassFilter or MovingAvgType.EhlersAverageErrorFilter
            or MovingAvgType.EhlersAllPassPhaseShifter or MovingAvgType.PolynomialLeastSquaresMovingAverage
            or MovingAvgType.QuadraticLeastSquaresMovingAverage or MovingAvgType.QuadraticRegression
            or MovingAvgType.FollowingAdaptiveMovingAverage or MovingAvgType.VariableAdaptiveMovingAverage
            or MovingAvgType.VerticalHorizontalMovingAverage or MovingAvgType.EdgePreservingFilter
            or MovingAvgType.AutoFilter or MovingAvgType.FallingRisingFilter
            or MovingAvgType.HybridConvolutionFilter or MovingAvgType.IIRLeastSquaresEstimate
            or MovingAvgType.GeneralFilterEstimator or MovingAvgType.MovingAverageV3
            or MovingAvgType.MovingAverageAdaptiveQ or MovingAvgType.TStepLeastSquaresMovingAverage
            or MovingAvgType.ParametricCorrectiveLinearMovingAverage or MovingAvgType.ParametricKalmanFilter
            or MovingAvgType.R2AdaptiveRegression or MovingAvgType.Svama
            or MovingAvgType.VolatilityMovingAverage or MovingAvgType.VolatilityWaveMovingAverage
            or MovingAvgType.PoweredKaufmanAdaptiveMovingAverage or MovingAvgType.EhlersLeadingIndicator
            or MovingAvgType.EhlersMedianAverageAdaptiveFilter or MovingAvgType.EhlersDistanceCoefficientFilter
            or MovingAvgType.EhlersNoiseEliminationTechnology or MovingAvgType.BryantAdaptiveMovingAverage
            or MovingAvgType.AdaptiveAutonomousRecursiveMovingAverage or MovingAvgType.EhlersVariableIndexDynamicAverage
            or MovingAvgType.EhlersKaufmanAdaptiveMovingAverage or MovingAvgType.EhlersMesaAdaptiveMovingAverage
            or MovingAvgType.AdaptiveMovingAverage or MovingAvgType.EhlersLaguerreFilter
            // Phase 3 fast path types (7 additional Core methods for remaining single-input types)
            or MovingAvgType.ReverseEngineeringRelativeStrengthIndex or MovingAvgType.ReverseMovingAverageConvergenceDivergence
            or MovingAvgType.OptimalWeightedMovingAverage or MovingAvgType.LightLeastSquaresMovingAverage
            or MovingAvgType.FisherLeastSquaresMovingAverage or MovingAvgType.OvershootReductionMovingAverage
            or MovingAvgType.KaufmanAdaptiveLeastSquaresMovingAverage)
        {
            var inputList = customValuesList ?? GetInputValuesList(stockData).inputList;
            var count = inputList.Count;
            var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
            var outputBuffer = SpanCompat.CreateOutputBuffer(count);
            var outputSpan = outputBuffer.Span;

            switch (movingAvgType)
            {
                case MovingAvgType.SimpleMovingAverage:
                    MovingAverageCore.SimpleMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.WeightedMovingAverage:
                    MovingAverageCore.WeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ExponentialMovingAverage:
                    MovingAverageCore.ExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.WildersSmoothingMethod:
                    MovingAverageCore.WellesWilderMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.DoubleExponentialMovingAverage:
                    MovingAverageCore.DoubleExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.TripleExponentialMovingAverage:
                    MovingAverageCore.TripleExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.HullMovingAverage:
                    MovingAverageCore.HullMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.McGinleyDynamicIndicator:
                    MovingAverageCore.McGinleyDynamic(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.TillsonT3MovingAverage:
                    MovingAverageCore.T3MovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.VariableIndexDynamicAverage:
                    MovingAverageCore.Vidya(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.VariableMovingAverage:
                    MovingAverageCore.VariableMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ArnaudLegouxMovingAverage:
                    MovingAverageCore.ArnaudLegouxMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.LeastSquaresMovingAverage:
                    MovingAverageCore.LeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SineWeightedMovingAverage:
                    MovingAverageCore.SineWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.RegularizedExponentialMovingAverage:
                    MovingAverageCore.RegularizedEma(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.JurikMovingAverage:
                    MovingAverageCore.JurikMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EndPointWeightedMovingAverage:
                    MovingAverageCore.EndPointMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.CubedWeightedMovingAverage:
                    MovingAverageCore.CubedWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.NaturalMovingAverage:
                    MovingAverageCore.NaturalMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.AlphaDecreasingExponentialMovingAverage:
                    MovingAverageCore.AlphaDecreasingEma(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.AdaptiveExponentialMovingAverage:
                    MovingAverageCore.AdaptiveExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.AutonomousRecursiveMovingAverage:
                    MovingAverageCore.AutonomousRecursiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.AdaptiveLeastSquares:
                    MovingAverageCore.AdaptiveLeastSquares(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ParabolicWeightedMovingAverage:
                    MovingAverageCore.ParabolicWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.UltimateMovingAverage:
                    MovingAverageCore.UltimateMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SquareRootWeightedMovingAverage:
                    MovingAverageCore.SquareRootWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Spencer15PointMovingAverage:
                    MovingAverageCore.Spencer15PointMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Spencer21PointMovingAverage:
                    MovingAverageCore.Spencer21PointMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SlowSmoothedMovingAverage:
                    MovingAverageCore.SlowSmoothedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.QuickMovingAverage:
                    MovingAverageCore.QuickMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersBetterExponentialMovingAverage:
                    MovingAverageCore.EhlersBetterExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.PentupleExponentialMovingAverage:
                    MovingAverageCore.PentupleExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.QuadrupleExponentialMovingAverage:
                    MovingAverageCore.QuadrupleExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersZeroLagExponentialMovingAverage:
                    MovingAverageCore.EhlersZeroLagExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersFractalAdaptiveMovingAverage:
                    MovingAverageCore.EhlersFractalAdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersAdaptiveLaguerreFilter:
                    MovingAverageCore.EhlersAdaptiveLaguerreFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.DampedSineWaveWeightedFilter:
                    MovingAverageCore.DampedSineWaveWeightedFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.FibonacciWeightedMovingAverage:
                    MovingAverageCore.FibonacciWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.GeneralizedDoubleExponentialMovingAverage:
                    MovingAverageCore.GeneralizedDoubleExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Ehlers2PoleButterworthFilterV1:
                    MovingAverageCore.Ehlers2PoleButterworthFilterV1(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Ehlers2PoleButterworthFilterV2:
                    MovingAverageCore.Ehlers2PoleButterworthFilterV2(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Ehlers3PoleButterworthFilterV1:
                    MovingAverageCore.Ehlers3PoleButterworthFilterV1(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Ehlers3PoleButterworthFilterV2:
                    MovingAverageCore.Ehlers3PoleButterworthFilterV2(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Ehlers2PoleSuperSmootherFilterV1:
                    MovingAverageCore.Ehlers2PoleSuperSmootherFilterV1(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Ehlers2PoleSuperSmootherFilterV2:
                    MovingAverageCore.Ehlers2PoleSuperSmootherFilterV2(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Ehlers3PoleSuperSmootherFilter:
                    MovingAverageCore.Ehlers3PoleSuperSmootherFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersSimpleDecycler:
                    MovingAverageCore.EhlersDecycler(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersHammingMovingAverage:
                    MovingAverageCore.EhlersHammingMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.DistanceWeightedMovingAverage:
                    MovingAverageCore.DistanceWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersFilter:
                    MovingAverageCore.EhlersFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersFiniteImpulseResponseFilter:
                    MovingAverageCore.EhlersFiniteImpulseResponseFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersInfiniteImpulseResponseFilter:
                    MovingAverageCore.EhlersInfiniteImpulseResponseFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.TriangularMovingAverage:
                    MovingAverageCore.TriangularMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.LinearRegression:
                    MovingAverageCore.LinearRegression(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SymmetricallyWeightedMovingAverage:
                    MovingAverageCore.SymmetricallyWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.RepulsionMovingAverage:
                    MovingAverageCore.RepulsionMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersHannMovingAverage:
                    MovingAverageCore.EhlersHannMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersTriangleMovingAverage:
                    MovingAverageCore.EhlersTriangleMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ZeroLagExponentialMovingAverage:
                    MovingAverageCore.ZeroLagEma(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.HoltExponentialMovingAverage:
                    MovingAverageCore.HoltExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.KaufmanAdaptiveMovingAverage:
                    MovingAverageCore.KaufmanAdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersSuperSmootherFilter:
                    MovingAverageCore.SuperSmoother(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersDeviationScaledMovingAverage:
                    MovingAverageCore.EhlersDeviationScaledMovingAverage(inputSpan, outputSpan, length);
                    break;
                // New fast path types
                case MovingAvgType.AhrensMovingAverage:
                    MovingAverageCore.AhrensMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.DoubleExponentialSmoothing:
                    MovingAverageCore.DoubleExponentialSmoothing(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.CompoundRatioMovingAverage:
                    MovingAverageCore.CompoundRatioMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.CorrectedMovingAverage:
                    MovingAverageCore.CorrectedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.DynamicallyAdjustableFilter:
                    MovingAverageCore.DynamicallyAdjustableFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.DynamicallyAdjustableMovingAverage:
                    MovingAverageCore.DynamicallyAdjustableMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.LinearWeightedMovingAverage:
                    MovingAverageCore.LinearWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.LeoMovingAverage:
                    MovingAverageCore.LeoMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.McNichollMovingAverage:
                    MovingAverageCore.McNichollMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType._3HMA:
                    MovingAverageCore.ThreeHMA(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ZeroLagTripleExponentialMovingAverage:
                    MovingAverageCore.ZeroLagTripleExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ZeroLowLagMovingAverage:
                    MovingAverageCore.ZeroLowLagMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.WildersSummationMethod:
                    MovingAverageCore.WildersSummationMethod(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SimplifiedWeightedMovingAverage:
                    MovingAverageCore.SimplifiedWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SimplifiedLeastSquaresMovingAverage:
                    MovingAverageCore.SimplifiedLeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SharpModifiedMovingAverage:
                    MovingAverageCore.SharpModifiedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.TillsonIE2:
                    MovingAverageCore.TillsonIE2(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.RecursiveMovingTrendAverage:
                    MovingAverageCore.RecursiveMovingTrendAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.QuadraticMovingAverage:
                    MovingAverageCore.QuadraticMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.MultiDepthZeroLagExponentialMovingAverage:
                    MovingAverageCore.MultiDepthZeroLagExponentialMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.HullEstimate:
                    MovingAverageCore.HullEstimate(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.InverseDistanceWeightedMovingAverage:
                    MovingAverageCore.InverseDistanceWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Trimean:
                    MovingAverageCore.Trimean(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.WellRoundedMovingAverage:
                    MovingAverageCore.WellRoundedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.LinearRegressionLine:
                    MovingAverageCore.LinearRegressionLine(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.LinearExtrapolation:
                    MovingAverageCore.LinearExtrapolation(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.JsaMovingAverage:
                    MovingAverageCore.JsaMovingAverage(inputSpan, outputSpan, length);
                    break;
                // Phase 2 fast path cases (58 additional Core methods)
                case MovingAvgType.SelfWeightedMovingAverage:
                    MovingAverageCore.SelfWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.HendersonWeightedMovingAverage:
                    MovingAverageCore.HendersonWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.FareySequenceWeightedMovingAverage:
                    MovingAverageCore.FareySequenceWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.RightSidedRickerMovingAverage:
                    MovingAverageCore.RightSidedRickerMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.HampelFilter:
                    MovingAverageCore.HampelFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SequentiallyFilteredMovingAverage:
                    MovingAverageCore.SequentiallyFilteredMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.KalmanSmoother:
                    MovingAverageCore.KalmanSmoother(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ModularFilter:
                    MovingAverageCore.ModularFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.RetentionAccelerationFilter:
                    MovingAverageCore.RetentionAccelerationFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.SettingLessTrendStepFiltering:
                    MovingAverageCore.SettingLessTrendStepFiltering(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ShapeshiftingMovingAverage:
                    MovingAverageCore.ShapeshiftingMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.VariableLengthMovingAverage:
                    MovingAverageCore.VariableLengthMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersGaussianFilter:
                    MovingAverageCore.EhlersGaussianFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersRecursiveMedianFilter:
                    MovingAverageCore.EhlersRecursiveMedianFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType._1LCLeastSquaresMovingAverage:
                    MovingAverageCore.OneLCLeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersDeviationScaledSuperSmoother:
                    MovingAverageCore.EhlersDeviationScaledSuperSmoother(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersOptimumEllipticFilter:
                    MovingAverageCore.EhlersOptimumEllipticFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersModifiedOptimumEllipticFilter:
                    MovingAverageCore.EhlersModifiedOptimumEllipticFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersChebyshevLowPassFilter:
                    MovingAverageCore.EhlersChebyshevLowPassFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersAverageErrorFilter:
                    MovingAverageCore.EhlersAverageErrorFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersAllPassPhaseShifter:
                    MovingAverageCore.EhlersAllPassPhaseShifter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.PolynomialLeastSquaresMovingAverage:
                    MovingAverageCore.PolynomialLeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.QuadraticLeastSquaresMovingAverage:
                    MovingAverageCore.QuadraticLeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.QuadraticRegression:
                    MovingAverageCore.QuadraticRegression(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.FollowingAdaptiveMovingAverage:
                    MovingAverageCore.FollowingAdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.VariableAdaptiveMovingAverage:
                    MovingAverageCore.VariableAdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.VerticalHorizontalMovingAverage:
                    MovingAverageCore.VerticalHorizontalMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EdgePreservingFilter:
                    MovingAverageCore.EdgePreservingFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.AutoFilter:
                    MovingAverageCore.AutoFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.FallingRisingFilter:
                    MovingAverageCore.FallingRisingFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.HybridConvolutionFilter:
                    MovingAverageCore.HybridConvolutionFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.IIRLeastSquaresEstimate:
                    MovingAverageCore.IIRLeastSquaresEstimate(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.GeneralFilterEstimator:
                    MovingAverageCore.GeneralFilterEstimator(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.MovingAverageV3:
                    MovingAverageCore.MovingAverageV3(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.MovingAverageAdaptiveQ:
                    MovingAverageCore.MovingAverageAdaptiveQ(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.TStepLeastSquaresMovingAverage:
                    MovingAverageCore.TStepLeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ParametricCorrectiveLinearMovingAverage:
                    MovingAverageCore.ParametricCorrectiveLinearMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ParametricKalmanFilter:
                    MovingAverageCore.ParametricKalmanFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.R2AdaptiveRegression:
                    MovingAverageCore.R2AdaptiveRegression(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.Svama:
                    MovingAverageCore.Svama(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.VolatilityMovingAverage:
                    MovingAverageCore.VolatilityMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.VolatilityWaveMovingAverage:
                    MovingAverageCore.VolatilityWaveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.PoweredKaufmanAdaptiveMovingAverage:
                    MovingAverageCore.PoweredKaufmanAdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersLeadingIndicator:
                    MovingAverageCore.EhlersLeadingIndicator(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersMedianAverageAdaptiveFilter:
                    MovingAverageCore.EhlersMedianAverageAdaptiveFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersDistanceCoefficientFilter:
                    MovingAverageCore.EhlersDistanceCoefficientFilter(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersNoiseEliminationTechnology:
                    MovingAverageCore.EhlersNoiseEliminationTechnology(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.BryantAdaptiveMovingAverage:
                    MovingAverageCore.BryantAdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.AdaptiveAutonomousRecursiveMovingAverage:
                    MovingAverageCore.AdaptiveAutonomousRecursiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersVariableIndexDynamicAverage:
                    MovingAverageCore.EhlersVariableIndexDynamicAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersKaufmanAdaptiveMovingAverage:
                    MovingAverageCore.EhlersKaufmanAdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersMesaAdaptiveMovingAverage:
                    MovingAverageCore.EhlersMesaAdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.AdaptiveMovingAverage:
                    MovingAverageCore.AdaptiveMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.EhlersLaguerreFilter:
                    MovingAverageCore.EhlersLaguerreFilter(inputSpan, outputSpan);
                    break;
                // Phase 3: Remaining single-input complex types
                case MovingAvgType.ReverseEngineeringRelativeStrengthIndex:
                    MovingAverageCore.ReverseEngineeringRsi(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.ReverseMovingAverageConvergenceDivergence:
                    MovingAverageCore.ReverseMovingAverageConvergenceDivergence(inputSpan, outputSpan, fastLength ?? 12, slowLength ?? 26);
                    break;
                case MovingAvgType.OptimalWeightedMovingAverage:
                    MovingAverageCore.OptimalWeightedMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.LightLeastSquaresMovingAverage:
                    MovingAverageCore.LightLeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.FisherLeastSquaresMovingAverage:
                    MovingAverageCore.FisherLeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.OvershootReductionMovingAverage:
                    MovingAverageCore.OvershootReductionMovingAverage(inputSpan, outputSpan, length);
                    break;
                case MovingAvgType.KaufmanAdaptiveLeastSquaresMovingAverage:
                    MovingAverageCore.KaufmanAdaptiveLeastSquaresMovingAverage(inputSpan, outputSpan, length);
                    break;
            }

            movingAvgList = outputBuffer.ToList();
            return movingAvgList;
        }

        // Fast path for multi-input moving averages (require OHLC/volume data)
        if (movingAvgType is MovingAvgType.ElasticVolumeWeightedMovingAverageV1 or MovingAvgType.ElasticVolumeWeightedMovingAverageV2
            or MovingAvgType.VolumeAdjustedMovingAverage or MovingAvgType.WindowedVolumeWeightedMovingAverage
            or MovingAvgType.VolumeWeightedMovingAverage or MovingAvgType.MiddleHighLowMovingAverage
            or MovingAvgType.EquityMovingAverage or MovingAvgType.RatioOCHLAverager
            or MovingAvgType.VolumeWeightedAveragePrice or MovingAvgType.TrueRangeAdjustedExponentialMovingAverage
            or MovingAvgType.AtrFilteredExponentialMovingAverage)
        {
            var (inputList, highList, lowList, openList, volumeList) = GetInputValuesList(stockData);
            var count = inputList.Count;
            var inputSpan = SpanCompat.AsReadOnlySpan(customValuesList ?? inputList);
            var highSpan = SpanCompat.AsReadOnlySpan(highList);
            var lowSpan = SpanCompat.AsReadOnlySpan(lowList);
            var openSpan = SpanCompat.AsReadOnlySpan(openList);
            var volumeSpan = SpanCompat.AsReadOnlySpan(volumeList);
            var outputBuffer = SpanCompat.CreateOutputBuffer(count);
            var outputSpan = outputBuffer.Span;

            switch (movingAvgType)
            {
                case MovingAvgType.ElasticVolumeWeightedMovingAverageV1:
                    MovingAverageCore.ElasticVolumeWeightedMovingAverageV1(inputSpan, volumeSpan, outputSpan, length);
                    break;
                case MovingAvgType.ElasticVolumeWeightedMovingAverageV2:
                    MovingAverageCore.ElasticVolumeWeightedMovingAverageV2(inputSpan, volumeSpan, outputSpan, length);
                    break;
                case MovingAvgType.VolumeAdjustedMovingAverage:
                    MovingAverageCore.VolumeAdjustedMovingAverage(inputSpan, volumeSpan, outputSpan, length);
                    break;
                case MovingAvgType.WindowedVolumeWeightedMovingAverage:
                    MovingAverageCore.WindowedVolumeWeightedMovingAverage(inputSpan, volumeSpan, outputSpan, length);
                    break;
                case MovingAvgType.VolumeWeightedMovingAverage:
                    MovingAverageCore.VolumeWeightedMovingAverage(inputSpan, volumeSpan, outputSpan, length);
                    break;
                case MovingAvgType.MiddleHighLowMovingAverage:
                    MovingAverageCore.MiddleHighLowMovingAverage(highSpan, lowSpan, outputSpan, slowLength ?? length, fastLength ?? 10);
                    break;
                case MovingAvgType.EquityMovingAverage:
                    MovingAverageCore.EquityMovingAverage(inputSpan, volumeSpan, outputSpan, length);
                    break;
                case MovingAvgType.RatioOCHLAverager:
                    MovingAverageCore.RatioOchlAverager(openSpan, inputSpan, highSpan, lowSpan, outputSpan);
                    break;
                case MovingAvgType.VolumeWeightedAveragePrice:
                    MovingAverageCore.VolumeWeightedAveragePrice(inputSpan, highSpan, lowSpan, volumeSpan, outputSpan);
                    break;
                case MovingAvgType.TrueRangeAdjustedExponentialMovingAverage:
                    MovingAverageCore.TrueRangeAdjustedExponentialMovingAverage(inputSpan, highSpan, lowSpan, outputSpan, length);
                    break;
                case MovingAvgType.AtrFilteredExponentialMovingAverage:
                    MovingAverageCore.AtrFilteredExponentialMovingAverage(inputSpan, highSpan, lowSpan, outputSpan, length);
                    break;
            }

            movingAvgList = outputBuffer.ToList();
            return movingAvgList;
        }

        return GetMovingAverageListByCalculation(stockData, movingAvgType, length, fastLength, slowLength);
    }

    /// <summary>
    /// The moving average computed by the indicator of the same name, never by a fast path.
    /// </summary>
    /// <remarks>
    /// What every fast path above must equal: MovingAvgType.X is the indicator CalculateX, whichever route
    /// computes it. The fast paths for the variable, VIDYA and McNicholl averages had each drifted to a
    /// different formula; MovingAverageFastPathTests holds every fast path to this.
    /// </remarks>
    internal static List<double> GetMovingAverageListByCalculation(StockData stockData, MovingAvgType movingAvgType, int length,
        int? fastLength = null, int? slowLength = null)
    {
        List<double> movingAvgList = new();

        switch (movingAvgType)
        {
            case MovingAvgType._1LCLeastSquaresMovingAverage:
                movingAvgList = stockData.Calculate1LCLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType._3HMA:
                movingAvgList = stockData.Calculate3HMA(length: length).CustomValuesList;
                break;
            case MovingAvgType.AdaptiveAutonomousRecursiveMovingAverage:
                movingAvgList = stockData.CalculateAdaptiveAutonomousRecursiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.AdaptiveExponentialMovingAverage:
                movingAvgList = stockData.CalculateAdaptiveExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.AdaptiveLeastSquares:
                movingAvgList = stockData.CalculateAdaptiveLeastSquares(length: length).CustomValuesList;
                break;
            case MovingAvgType.AdaptiveMovingAverage:
                movingAvgList = stockData.CalculateAdaptiveMovingAverage(fastLength ?? default, slowLength ?? length, length).CustomValuesList;
                break;
            case MovingAvgType.AhrensMovingAverage:
                movingAvgList = stockData.CalculateAhrensMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.AlphaDecreasingExponentialMovingAverage:
                movingAvgList = stockData.CalculateAlphaDecreasingExponentialMovingAverage().CustomValuesList;
                break;
            case MovingAvgType.ArnaudLegouxMovingAverage:
                movingAvgList = stockData.CalculateArnaudLegouxMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.AtrFilteredExponentialMovingAverage:
                movingAvgList = stockData.CalculateAtrFilteredExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.AutoFilter:
                movingAvgList = stockData.CalculateAutoFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.AutonomousRecursiveMovingAverage:
                movingAvgList = stockData.CalculateAutonomousRecursiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.BryantAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateBryantAdaptiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.CompoundRatioMovingAverage:
                movingAvgList = stockData.CalculateCompoundRatioMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.CorrectedMovingAverage:
                movingAvgList = stockData.CalculateCorrectedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.CubedWeightedMovingAverage:
                movingAvgList = stockData.CalculateCubedWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.DampedSineWaveWeightedFilter:
                movingAvgList = stockData.CalculateDampedSineWaveWeightedFilter(length).CustomValuesList;
                break;
            case MovingAvgType.DistanceWeightedMovingAverage:
                movingAvgList = stockData.CalculateDistanceWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.DoubleExponentialMovingAverage:
                movingAvgList = stockData.CalculateDoubleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.DoubleExponentialSmoothing:
                movingAvgList = stockData.CalculateDoubleExponentialSmoothing().CustomValuesList;
                break;
            case MovingAvgType.DynamicallyAdjustableFilter:
                movingAvgList = stockData.CalculateDynamicallyAdjustableFilter(length).CustomValuesList;
                break;
            case MovingAvgType.DynamicallyAdjustableMovingAverage:
                movingAvgList = stockData.CalculateDynamicallyAdjustableMovingAverage(fastLength ?? default, slowLength ?? length).CustomValuesList;
                break;
            case MovingAvgType.EdgePreservingFilter:
                movingAvgList = stockData.CalculateEdgePreservingFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers2PoleButterworthFilterV1:
                movingAvgList = stockData.CalculateEhlers2PoleButterworthFilterV1(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers2PoleButterworthFilterV2:
                movingAvgList = stockData.CalculateEhlers2PoleButterworthFilterV2(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers2PoleSuperSmootherFilterV1:
                movingAvgList = stockData.CalculateEhlers2PoleSuperSmootherFilterV1(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers2PoleSuperSmootherFilterV2:
                movingAvgList = stockData.CalculateEhlers2PoleSuperSmootherFilterV2(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers3PoleButterworthFilterV1:
                movingAvgList = stockData.CalculateEhlers3PoleButterworthFilterV1(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers3PoleButterworthFilterV2:
                movingAvgList = stockData.CalculateEhlers3PoleButterworthFilterV2(length).CustomValuesList;
                break;
            case MovingAvgType.Ehlers3PoleSuperSmootherFilter:
                movingAvgList = stockData.CalculateEhlers3PoleSuperSmootherFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersAdaptiveLaguerreFilter:
                movingAvgList = stockData.CalculateEhlersAdaptiveLaguerreFilter(slowLength ?? length, fastLength ?? default).CustomValuesList;
                break;
            case MovingAvgType.EhlersAllPassPhaseShifter:
                movingAvgList = stockData.CalculateEhlersAllPassPhaseShifter(length: length).CustomValuesList;
                break;
            case MovingAvgType.EhlersAverageErrorFilter:
                movingAvgList = stockData.CalculateEhlersAverageErrorFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersBetterExponentialMovingAverage:
                movingAvgList = stockData.CalculateEhlersBetterExponentialMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersChebyshevLowPassFilter:
                movingAvgList = stockData.CalculateEhlersChebyshevLowPassFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersDeviationScaledMovingAverage:
                movingAvgList = stockData.CalculateEhlersDeviationScaledMovingAverage(
                    fastLength: fastLength ?? length, slowLength: slowLength ?? length * 2).CustomValuesList;
                break;
            case MovingAvgType.EhlersDeviationScaledSuperSmoother:
                movingAvgList = stockData.CalculateEhlersDeviationScaledSuperSmoother(length1: fastLength ?? length, length2: slowLength ?? default).CustomValuesList;
                break;
            case MovingAvgType.EhlersDistanceCoefficientFilter:
                movingAvgList = stockData.CalculateEhlersDistanceCoefficientFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersFilter:
                movingAvgList = stockData.CalculateEhlersFilter(slowLength ?? length, fastLength ?? default).CustomValuesList;
                break;
            case MovingAvgType.EhlersFiniteImpulseResponseFilter:
                movingAvgList = stockData.CalculateEhlersFiniteImpulseResponseFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersFractalAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateEhlersFractalAdaptiveMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersGaussianFilter:
                movingAvgList = stockData.CalculateEhlersGaussianFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersHammingMovingAverage:
                movingAvgList = stockData.CalculateEhlersHammingMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.EhlersHannMovingAverage:
                movingAvgList = stockData.CalculateEhlersHannMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersInfiniteImpulseResponseFilter:
                movingAvgList = stockData.CalculateEhlersInfiniteImpulseResponseFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersKaufmanAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateEhlersKaufmanAdaptiveMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersLaguerreFilter:
                movingAvgList = stockData.CalculateEhlersLaguerreFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersLeadingIndicator:
                movingAvgList = stockData.CalculateEhlersLeadingIndicator().CustomValuesList;
                break;
            case MovingAvgType.EhlersMedianAverageAdaptiveFilter:
                movingAvgList = stockData.CalculateEhlersMedianAverageAdaptiveFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.EhlersMesaAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateEhlersMotherOfAdaptiveMovingAverages().CustomValuesList;
                break;
            case MovingAvgType.EhlersModifiedOptimumEllipticFilter:
                movingAvgList = stockData.CalculateEhlersModifiedOptimumEllipticFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersOptimumEllipticFilter:
                movingAvgList = stockData.CalculateEhlersOptimumEllipticFilter().CustomValuesList;
                break;
            case MovingAvgType.EhlersRecursiveMedianFilter:
                movingAvgList = stockData.CalculateEhlersRecursiveMedianFilter(fastLength ?? default, slowLength ?? length).CustomValuesList;
                break;
            case MovingAvgType.EhlersSuperSmootherFilter:
                movingAvgList = stockData.CalculateEhlersSuperSmootherFilter(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersTriangleMovingAverage:
                movingAvgList = stockData.CalculateEhlersTriangleMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersVariableIndexDynamicAverage:
                movingAvgList = stockData.CalculateEhlersVariableIndexDynamicAverage(fastLength: fastLength ?? default, slowLength: slowLength ?? length)
                    .CustomValuesList;
                break;
            case MovingAvgType.EhlersZeroLagExponentialMovingAverage:
                movingAvgList = stockData.CalculateEhlersZeroLagExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ElasticVolumeWeightedMovingAverageV1:
                movingAvgList = stockData.CalculateElasticVolumeWeightedMovingAverageV1(length: length).CustomValuesList;
                break;
            case MovingAvgType.ElasticVolumeWeightedMovingAverageV2:
                movingAvgList = stockData.CalculateElasticVolumeWeightedMovingAverageV2(length).CustomValuesList;
                break;
            case MovingAvgType.EndPointWeightedMovingAverage:
                movingAvgList = stockData.CalculateEndPointMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.EquityMovingAverage:
                movingAvgList = stockData.CalculateEquityMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ExponentialMovingAverage:
                movingAvgList = stockData.CalculateExponentialMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.FallingRisingFilter:
                movingAvgList = stockData.CalculateFallingRisingFilter(length).CustomValuesList;
                break;
            case MovingAvgType.FareySequenceWeightedMovingAverage:
                movingAvgList = stockData.CalculateFareySequenceWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.FibonacciWeightedMovingAverage:
                movingAvgList = stockData.CalculateFibonacciWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.FisherLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateFisherLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.FollowingAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateEhlersMotherOfAdaptiveMovingAverages().OutputValues["Fama"];
                break;
            case MovingAvgType.GeneralFilterEstimator:
                movingAvgList = stockData.CalculateGeneralFilterEstimator(length: length).CustomValuesList;
                break;
            case MovingAvgType.GeneralizedDoubleExponentialMovingAverage:
                movingAvgList = stockData.CalculateGeneralizedDoubleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.HampelFilter:
                movingAvgList = stockData.CalculateHampelFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.HendersonWeightedMovingAverage:
                movingAvgList = stockData.CalculateHendersonWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.HoltExponentialMovingAverage:
                movingAvgList = stockData.CalculateHoltExponentialMovingAverage(length, length).CustomValuesList;
                break;
            case MovingAvgType.HullEstimate:
                movingAvgList = stockData.CalculateHullEstimate(length).CustomValuesList;
                break;
            case MovingAvgType.HullMovingAverage:
                movingAvgList = stockData.CalculateHullMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.HybridConvolutionFilter:
                movingAvgList = stockData.CalculateHybridConvolutionFilter(length).CustomValuesList;
                break;
            case MovingAvgType.IIRLeastSquaresEstimate:
                movingAvgList = stockData.CalculateIIRLeastSquaresEstimate(length).CustomValuesList;
                break;
            case MovingAvgType.InverseDistanceWeightedMovingAverage:
                movingAvgList = stockData.CalculateInverseDistanceWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.JsaMovingAverage:
                movingAvgList = stockData.CalculateJsaMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.JurikMovingAverage:
                movingAvgList = stockData.CalculateJurikMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.KalmanSmoother:
                movingAvgList = stockData.CalculateKalmanSmoother(length).CustomValuesList;
                break;
            case MovingAvgType.KaufmanAdaptiveLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateKaufmanAdaptiveLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.KaufmanAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateKaufmanAdaptiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.LeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateLeastSquaresMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.LeoMovingAverage:
                movingAvgList = stockData.CalculateLeoMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.LightLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateLightLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.LinearExtrapolation:
                movingAvgList = stockData.CalculateLinearExtrapolation(length).CustomValuesList;
                break;
            case MovingAvgType.LinearRegression:
                movingAvgList = stockData.CalculateLinearRegression(length).CustomValuesList;
                break;
            case MovingAvgType.LinearRegressionLine:
                movingAvgList = stockData.CalculateLinearRegressionLine(length: length).CustomValuesList;
                break;
            case MovingAvgType.LinearWeightedMovingAverage:
                movingAvgList = stockData.CalculateLinearWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.McGinleyDynamicIndicator:
                movingAvgList = stockData.CalculateMcGinleyDynamicIndicator(length: length).CustomValuesList;
                break;
            case MovingAvgType.McNichollMovingAverage:
                movingAvgList = stockData.CalculateMcNichollMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.MiddleHighLowMovingAverage:
                movingAvgList = stockData.CalculateMiddleHighLowMovingAverage(length1: slowLength ?? length, length2: fastLength ?? default).CustomValuesList;
                break;
            case MovingAvgType.ModularFilter:
                movingAvgList = stockData.CalculateModularFilter(length: length).CustomValuesList;
                break;
            case MovingAvgType.MovingAverageAdaptiveQ:
                movingAvgList = stockData.CalculateMovingAverageAdaptiveQ(length: length).CustomValuesList;
                break;
            case MovingAvgType.MovingAverageV3:
                movingAvgList = stockData.CalculateMovingAverageV3(length1: length).CustomValuesList;
                break;
            case MovingAvgType.MultiDepthZeroLagExponentialMovingAverage:
                movingAvgList = stockData.CalculateMultiDepthZeroLagExponentialMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.NaturalMovingAverage:
                movingAvgList = stockData.CalculateNaturalMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.OptimalWeightedMovingAverage:
                movingAvgList = stockData.CalculateOptimalWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.OvershootReductionMovingAverage:
                movingAvgList = stockData.CalculateOvershootReductionMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ParabolicWeightedMovingAverage:
                movingAvgList = stockData.CalculateParabolicWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.ParametricCorrectiveLinearMovingAverage:
                movingAvgList = stockData.CalculateParametricCorrectiveLinearMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ParametricKalmanFilter:
                movingAvgList = stockData.CalculateParametricKalmanFilter(length).CustomValuesList;
                break;
            case MovingAvgType.PentupleExponentialMovingAverage:
                movingAvgList = stockData.CalculatePentupleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.PolynomialLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculatePolynomialLeastSquaresMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.PoweredKaufmanAdaptiveMovingAverage:
                movingAvgList = stockData.CalculatePoweredKaufmanAdaptiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.QuadraticLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateQuadraticLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.QuadraticMovingAverage:
                movingAvgList = stockData.CalculateQuadraticMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.QuadraticRegression:
                movingAvgList = stockData.CalculateQuadraticRegression(length: length).CustomValuesList;
                break;
            case MovingAvgType.QuadrupleExponentialMovingAverage:
                movingAvgList = stockData.CalculateQuadrupleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.QuickMovingAverage:
                movingAvgList = stockData.CalculateQuickMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.R2AdaptiveRegression:
                movingAvgList = stockData.CalculateR2AdaptiveRegression(length: length).CustomValuesList;
                break;
            case MovingAvgType.RatioOCHLAverager:
                movingAvgList = stockData.CalculateRatioOCHLAverager().CustomValuesList;
                break;
            case MovingAvgType.RecursiveMovingTrendAverage:
                movingAvgList = stockData.CalculateRecursiveMovingTrendAverage(length).CustomValuesList;
                break;
            case MovingAvgType.RegularizedExponentialMovingAverage:
                movingAvgList = stockData.CalculateRegularizedExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.RepulsionMovingAverage:
                movingAvgList = stockData.CalculateRepulsionMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.RetentionAccelerationFilter:
                movingAvgList = stockData.CalculateRetentionAccelerationFilter(length).CustomValuesList;
                break;
            case MovingAvgType.ReverseEngineeringRelativeStrengthIndex:
                movingAvgList = stockData.CalculateReverseEngineeringRelativeStrengthIndex(length: length).CustomValuesList;
                break;
            case MovingAvgType.ReverseMovingAverageConvergenceDivergence:
                movingAvgList = stockData.CalculateReverseMovingAverageConvergenceDivergence(fastLength: fastLength ?? default, slowLength: slowLength ?? length)
                    .CustomValuesList;
                break;
            case MovingAvgType.RightSidedRickerMovingAverage:
                movingAvgList = stockData.CalculateRightSidedRickerMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.SelfWeightedMovingAverage:
                movingAvgList = stockData.CalculateSelfWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SequentiallyFilteredMovingAverage:
                movingAvgList = stockData.CalculateSequentiallyFilteredMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.SettingLessTrendStepFiltering:
                movingAvgList = stockData.CalculateSettingLessTrendStepFiltering().CustomValuesList;
                break;
            case MovingAvgType.ShapeshiftingMovingAverage:
                movingAvgList = stockData.CalculateShapeshiftingMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SharpModifiedMovingAverage:
                movingAvgList = stockData.CalculateSharpModifiedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.SimpleMovingAverage:
                movingAvgList = stockData.CalculateSimpleMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SimplifiedLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateSimplifiedLeastSquaresMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SimplifiedWeightedMovingAverage:
                movingAvgList = stockData.CalculateSimplifiedWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SineWeightedMovingAverage:
                movingAvgList = stockData.CalculateSineWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.SlowSmoothedMovingAverage:
                movingAvgList = stockData.CalculateSlowSmoothedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.Spencer15PointMovingAverage:
                movingAvgList = stockData.CalculateSpencer15PointMovingAverage().CustomValuesList;
                break;
            case MovingAvgType.Spencer21PointMovingAverage:
                movingAvgList = stockData.CalculateSpencer21PointMovingAverage().CustomValuesList;
                break;
            case MovingAvgType.SquareRootWeightedMovingAverage:
                movingAvgList = stockData.CalculateSquareRootWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.Svama:
                movingAvgList = stockData.CalculateSvama(length).CustomValuesList;
                break;
            case MovingAvgType.SymmetricallyWeightedMovingAverage:
                movingAvgList = stockData.CalculateSymmetricallyWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.TStepLeastSquaresMovingAverage:
                movingAvgList = stockData.CalculateTStepLeastSquaresMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.TillsonIE2:
                movingAvgList = stockData.CalculateTillsonIE2(length: length).CustomValuesList;
                break;
            case MovingAvgType.TillsonT3MovingAverage:
                movingAvgList = stockData.CalculateTillsonT3MovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.TriangularMovingAverage:
                movingAvgList = stockData.CalculateTriangularMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.Trimean:
                movingAvgList = stockData.CalculateTrimean(length).CustomValuesList;
                break;
            case MovingAvgType.TripleExponentialMovingAverage:
                movingAvgList = stockData.CalculateTripleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.UltimateMovingAverage:
                movingAvgList = stockData.CalculateUltimateMovingAverage(minLength: fastLength ?? default, maxLength: slowLength ?? length).CustomValuesList;
                break;
            case MovingAvgType.VariableAdaptiveMovingAverage:
                movingAvgList = stockData.CalculateVariableAdaptiveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VariableIndexDynamicAverage:
                movingAvgList = stockData.CalculateVariableIndexDynamicAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VariableLengthMovingAverage:
                movingAvgList = stockData.CalculateVariableLengthMovingAverage(minLength: fastLength ?? default, maxLength: slowLength ?? length).CustomValuesList;
                break;
            case MovingAvgType.VariableMovingAverage:
                movingAvgList = stockData.CalculateVariableMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.VerticalHorizontalMovingAverage:
                movingAvgList = stockData.CalculateVerticalHorizontalMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.VolatilityMovingAverage:
                movingAvgList = stockData.CalculateVolatilityMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VolatilityWaveMovingAverage:
                movingAvgList = stockData.CalculateVolatilityWaveMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VolumeAdjustedMovingAverage:
                movingAvgList = stockData.CalculateVolumeAdjustedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.VolumeWeightedAveragePrice:
                movingAvgList = stockData.CalculateVolumeWeightedAveragePrice().CustomValuesList;
                break;
            case MovingAvgType.VolumeWeightedMovingAverage:
                movingAvgList = stockData.CalculateVolumeWeightedMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.WeightedMovingAverage:
                movingAvgList = stockData.CalculateWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.WellRoundedMovingAverage:
                movingAvgList = stockData.CalculateWellRoundedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.WildersSmoothingMethod:
                movingAvgList = stockData.CalculateWellesWilderMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.WildersSummationMethod:
                movingAvgList = stockData.CalculateWellesWilderSummation(length).CustomValuesList;
                break;
            case MovingAvgType.WindowedVolumeWeightedMovingAverage:
                movingAvgList = stockData.CalculateWindowedVolumeWeightedMovingAverage(length).CustomValuesList;
                break;
            case MovingAvgType.ZeroLagExponentialMovingAverage:
                movingAvgList = stockData.CalculateZeroLagExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ZeroLagTripleExponentialMovingAverage:
                movingAvgList = stockData.CalculateZeroLagTripleExponentialMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.ZeroLowLagMovingAverage:
                movingAvgList = stockData.CalculateZeroLowLagMovingAverage(length: length).CustomValuesList;
                break;
            case MovingAvgType.EhlersNoiseEliminationTechnology:
                movingAvgList = stockData.CalculateEhlersNoiseEliminationTechnology(length).CustomValuesList;
                break;
            case MovingAvgType.EhlersSimpleDecycler:
                movingAvgList = stockData.CalculateEhlersSimpleDecycler(length).CustomValuesList;
                break;
            default:
                Console.WriteLine($"Moving Avg Name: {movingAvgType} not supported!");
                break;
        }

        return movingAvgList;
    }

    /// <summary>
    /// Gets the input values list.
    /// </summary>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    internal static (List<double> inputList, List<double> highList, List<double> lowList, List<double> openList, List<double> closeList,
        List<double> volumeList) GetInputValuesList(InputName inputName, StockData stockData)
    {
        List<double> highList;
        List<double> lowList;
        List<double> openList;
        List<double> closeList;
        List<double> volumeList;

        // A chained series wins, exactly as it does for the single-argument overload. This overload
        // used to go straight to the named price field, so the 28 indicators whose default input is a
        // named price - AlligatorIndex, AwesomeOscillator, CommodityChannelIndex, MoneyFlowIndex,
        // VolumeWeightedAveragePrice and the rest - silently ignored anything chained in front of
        // them, as did every indicator built on one of them (GatorOscillator on AlligatorIndex,
        // AcceleratorOscillator on AwesomeOscillator). Callers pass values; the name only decides what
        // is read when nothing was passed.
        //
        // What is on CustomValuesList must therefore be the caller's chain. Each of the 24 methods reads
        // it on entry, before its own calculation writes anything there. But a COMPOSITE that calls one
        // of them after another component has published its output would hand it that output as if it
        // were a chain (the #145 pattern). Four composites make such a call - InsyncIndex,
        // TechnicalRatings, UltimateMomentumIndicator and WoodieCommodityChannelIndex - and each now
        // restores the caller's series first. Without that, three of them changed their UNCHAINED output
        // (a three-arm control against master: TechnicalRatings, UltimateMomentumIndicator and
        // WoodieCommodityChannelIndex differed; InsyncIndex happened not to). Found by auditing every
        // call site of these methods, not just their bodies.
        var inputList = stockData.CustomValuesList is { Count: > 0 } chained
            ? chained
            : inputName switch
        {
            InputName.Close => stockData.ClosePrices,
            InputName.Low => stockData.LowPrices,
            InputName.High => stockData.HighPrices,
            InputName.Volume => stockData.Volumes,
            InputName.TypicalPrice => GetDerivedSeriesList(stockData, DerivedSeriesKind.Hlc3),
            InputName.FullTypicalPrice => GetDerivedSeriesList(stockData, DerivedSeriesKind.Ohlc4),
            InputName.MedianPrice => GetDerivedSeriesList(stockData, DerivedSeriesKind.Hl2),
            InputName.WeightedClose => GetDerivedSeriesList(stockData, DerivedSeriesKind.WeightedClose),
            InputName.Open => stockData.OpenPrices,
            InputName.AdjustedClose => stockData.ClosePrices,
            InputName.Midpoint => stockData.CalculateMidpoint().CustomValuesList,
            InputName.Midprice => stockData.CalculateMidprice().CustomValuesList,
            InputName.AveragePrice => GetDerivedSeriesList(stockData, DerivedSeriesKind.AveragePrice),
            _ => stockData.ClosePrices,
        };

        if (inputList.Count > 0 && !SequenceEqualValues(inputList, stockData.ClosePrices))
        {
            // A series other than the close: the per-bar rule. See GetCustomRangeLists.
            (highList, lowList) = GetCustomRangeLists(inputList, stockData.HighPrices, stockData.LowPrices);
        }
        else
        {
            highList = stockData.HighPrices;
            lowList = stockData.LowPrices;
        }

        openList = stockData.OpenPrices;
        // A chained series stands in for the close, as it does in the bar CustomInputState hands a streaming
        // state; the named input only decides what is read when nothing was chained.
        closeList = stockData.CustomValuesList is { Count: > 0 } ? inputList : stockData.ClosePrices;
        volumeList = stockData.Volumes;

        return (inputList, highList, lowList, openList, closeList, volumeList);
    }

    /// <summary>
    /// Gets the input values list.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <returns></returns>
    /// <exception cref="OoplesFinance.StockIndicators.Exceptions.CalculationException">Calculations based off of 
    /// {stockData.IndicatorName} can't be completed because this indicator doesn't have a single output.</exception>
    public static (List<double> inputList, List<double> highList, List<double> lowList, List<double> openList, List<double> volumeList) 
        GetInputValuesList(StockData stockData)
    {
        List<double> inputList;
        List<double> highList;
        List<double> lowList;
        List<double> openList;
        List<double> volumeList;

        if (stockData.CustomValuesList != null && stockData.CustomValuesList.Count > 0)
        {
            inputList = stockData.CustomValuesList;
        }
        else if ((stockData.CustomValuesList == null || (stockData.CustomValuesList != null && stockData.CustomValuesList.Count == 0)) &&
            stockData.SignalsList != null && stockData.SignalsList.Count > 0)
        {
            throw new CalculationException($"Calculations based off of {stockData.IndicatorName} can't be completed because this indicator doesn't have a single output.");
        }
        else
        {
            inputList = stockData.InputValues;
        }

        if (inputList.Count > 0 && !SequenceEqualValues(inputList, stockData.ClosePrices))
        {
            // A series other than the close: the per-bar rule. See GetCustomRangeLists.
            (highList, lowList) = GetCustomRangeLists(inputList, stockData.HighPrices, stockData.LowPrices);
        }
        else
        {
            highList = stockData.HighPrices;
            lowList = stockData.LowPrices;
        }

        openList = stockData.OpenPrices;
        volumeList = stockData.Volumes;

        return (inputList, highList, lowList, openList, volumeList);
    }

    /// <summary>
    /// The period ordinal each BAR belongs to, using the same grouping
    /// <see cref="GetInputValuesList(StockData, InputLength)"/> applies.
    /// </summary>
    /// <remarks>
    /// <para>Period indicators aggregate bars into calendar groups and produce one value per GROUP. Their
    /// results are written back onto a StockData whose <c>TickerDataList</c> is still per BAR, and callers -
    /// including this library's own chained calculations - index the two in parallel. Without a way to project
    /// the grouped series back onto the bars, index <c>i</c> means a different instant in each.</para>
    ///
    /// <para>Returned one entry per bar, holding that bar's index into the grouped output. The keying is kept
    /// character-for-character identical to the grouping loop above; if the two ever diverge, the projection
    /// silently attributes a period's level to the wrong bars.</para>
    /// </remarks>
    public static List<int> GetInputLengthGroupIndexes(StockData stockData, InputLength inputLength)
    {
        var tickerDataList = stockData.TickerDataList;
        var indexes = new List<int>(tickerDataList.Count);
        var seen = new Dictionary<(DateTime Parent, int Child), int>();
        var next = 0;

        for (var i = 0; i < tickerDataList.Count; i++)
        {
            var ticker = tickerDataList[i];
            var parentKey = ticker.Date.Date;
            var childKey = inputLength switch
            {
                InputLength.Minute => ticker.Date.Minute,
                InputLength.Hour => ticker.Date.Hour,
                InputLength.Day => ticker.Date.Day,
                InputLength.Week => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(ticker.Date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday),
                InputLength.Month => ticker.Date.Month,
                InputLength.Year => ticker.Date.Year,
                _ => ticker.Date.Day,
            };

            var key = (parentKey, childKey);
            if (!seen.TryGetValue(key, out var ordinal))
            {
                ordinal = next++;
                seen[key] = ordinal;
            }

            indexes.Add(ordinal);
        }

        return indexes;
    }

    /// <summary>
    /// Projects a per-period series back onto the bar grid, so bar <c>i</c> carries its own period's value.
    /// </summary>
    /// <remarks>
    /// Causal by construction for the pivot family: a period's level is computed from the PRECEDING period, so
    /// it is already known when the first bar of its own period opens. Carrying it across that period's bars
    /// introduces no information the bar did not have - it is exactly how a level is used in practice.
    /// </remarks>
    /// <summary>
    /// Projects any per-period series onto the bar grid. Signals need this as much as levels do: a per-period
    /// signal list stored on a per-bar StockData misattributes each signal to whichever bar shares its index.
    /// </summary>
    public static List<T> ExpandPeriodItemsToBars<T>(List<T> periodItems, List<int> groupIndexes, T fallback)
    {
        var expanded = new List<T>(groupIndexes.Count);
        for (var i = 0; i < groupIndexes.Count; i++)
        {
            var ordinal = groupIndexes[i];
            expanded.Add(ordinal >= 0 && ordinal < periodItems.Count ? periodItems[ordinal] : fallback);
        }

        return expanded;
    }

    public static List<double> ExpandPeriodValuesToBars(List<double> periodValues, List<int> groupIndexes)
    {
        var expanded = new List<double>(groupIndexes.Count);
        for (var i = 0; i < groupIndexes.Count; i++)
        {
            var ordinal = groupIndexes[i];
            expanded.Add(ordinal >= 0 && ordinal < periodValues.Count ? periodValues[ordinal] : 0);
        }

        return expanded;
    }

    /// <summary>
    /// Gets input values using a fixed length according to the input length to be used with indicators such as Math.PIvot Points or similar indicators
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputLength"></param>
    /// <returns></returns>
    /// <exception cref="CalculationException"></exception>
    public static (List<double> inputList, List<double> highList, List<double> lowList, List<double> openList, List<double> volumeList)
        GetInputValuesList(StockData stockData, InputLength inputLength)
    {
        var capacity = stockData.TickerDataList.Count;
        List<double> inputList = new(capacity);
        List<double> highList = new(capacity);
        List<double> lowList = new(capacity);
        List<double> openList = new(capacity);
        List<double> volumeList = new(capacity);
        var parentGroups = new Dictionary<DateTime, InputLengthGroup>();
        var parentOrder = new List<DateTime>();
        var tickerDataList = stockData.TickerDataList;

        // A chained series is the close of every bar, and each bar's high and low follow the per-bar
        // rule (GetCustomRangeLists); the periods are then built from those adjusted bars exactly as
        // they are built from the real ones - open first, high the max, low the min, close last. This
        // used to rebuild from TickerDataList whatever was chained in front, so every pivot point
        // silently ignored a custom series.
        var chained = stockData.CustomValuesList is { Count: > 0 } custom && custom.Count == tickerDataList.Count
            ? custom
            : null;
        List<double>? customHighs = null;
        List<double>? customLows = null;
        if (chained != null)
        {
            var highs = new List<double>(tickerDataList.Count);
            var lows = new List<double>(tickerDataList.Count);
            for (var k = 0; k < tickerDataList.Count; k++)
            {
                highs.Add(tickerDataList[k].High);
                lows.Add(tickerDataList[k].Low);
            }

            (customHighs, customLows) = GetCustomRangeLists(chained, highs, lows);
        }

        for (var i = 0; i < tickerDataList.Count; i++)
        {
            var ticker = tickerDataList[i];
            var parentKey = ticker.Date.Date;

            if (!parentGroups.TryGetValue(parentKey, out var parentGroup))
            {
                parentGroup = new InputLengthGroup();
                parentGroups[parentKey] = parentGroup;
                parentOrder.Add(parentKey);
            }

            var childKey = inputLength switch
            {
                InputLength.Minute => ticker.Date.Minute,
                InputLength.Hour => ticker.Date.Hour,
                InputLength.Day => ticker.Date.Day,
                InputLength.Week => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(ticker.Date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday),
                InputLength.Month => ticker.Date.Month,
                InputLength.Year => ticker.Date.Year,
                _ => ticker.Date.Day,
            };

            if (!parentGroup.Children.TryGetValue(childKey, out var childGroup))
            {
                childGroup = new OhlcvAggregate();
                parentGroup.Children[childKey] = childGroup;
                parentGroup.ChildOrder.Add(childKey);
            }

            if (chained != null && customHighs != null && customLows != null)
            {
                childGroup.Add(ticker.Open, customHighs[i], customLows[i], chained[i], ticker.Volume);
            }
            else
            {
                childGroup.Add(ticker);
            }
        }

        for (var i = 0; i < parentOrder.Count; i++)
        {
            var parentGroup = parentGroups[parentOrder[i]];
            var childOrder = parentGroup.ChildOrder;
            for (var j = 0; j < childOrder.Count; j++)
            {
                var childGroup = parentGroup.Children[childOrder[j]];
                if (!childGroup.HasValue)
                {
                    continue;
                }

                highList.Add(childGroup.High);
                lowList.Add(childGroup.Low);
                volumeList.Add(childGroup.Volume);
                openList.Add(childGroup.Open);
                inputList.Add(childGroup.Close);
            }
        }

        return (inputList, highList, lowList, openList, volumeList);
    }

    private sealed class InputLengthGroup
    {
        public Dictionary<int, OhlcvAggregate> Children { get; } = new();
        public List<int> ChildOrder { get; } = new();
    }

    private sealed class OhlcvAggregate
    {
        public bool HasValue { get; private set; }
        public double Open { get; private set; }
        public double High { get; private set; }
        public double Low { get; private set; }
        public double Close { get; private set; }
        public double Volume { get; private set; }

        public void Add(TickerData ticker) =>
            Add(ticker.Open, ticker.High, ticker.Low, ticker.Close, ticker.Volume);

        public void Add(double open, double high, double low, double close, double volume)
        {
            if (!HasValue)
            {
                Open = open;
                High = high;
                Low = low;
                Close = close;
                Volume = volume;
                HasValue = true;
                return;
            }

            if (high > High)
            {
                High = high;
            }

            if (low < Low)
            {
                Low = low;
            }

            Volume += volume;
            Close = close;
        }
    }

    /// <summary>
    /// Calculates the ema.
    /// </summary>
    /// <param name="currentValue">The current value.</param>
    /// <param name="prevEma">The previous ema.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static double CalculateEMA(double currentValue, double prevEma, int length = 14)
    {
        var k = MinOrMax((double)2 / (length + 1), 0.99, 0.01);
        var ema = (currentValue * k) + (prevEma * (1 - k));

        return ema;
    }

    /// <summary>
    /// Calculates the true range.
    /// </summary>
    /// <param name="currentHigh">The current high.</param>
    /// <param name="currentLow">The current low.</param>
    /// <param name="prevClose">The previous close.</param>
    /// <returns></returns>
    public static double CalculateTrueRange(double currentHigh, double currentLow, double prevClose)
    {
        return Math.Max(currentHigh - currentLow, Math.Max(Math.Abs(currentHigh - prevClose), Math.Abs(currentLow - prevClose)));
    }

    /// <summary>
    /// Calculates the percent change.
    /// </summary>
    /// <param name="currentValue">The current value.</param>
    /// <param name="previousValue">The previous value.</param>
    /// <returns></returns>
    public static double CalculatePercentChange(double currentValue, double previousValue)
    {
        return previousValue != 0 ? (currentValue - previousValue) / Math.Abs(previousValue) * 100 : 0;
    }

    /// <summary>
    /// The high and low an indicator should read when its input is a series other than the close.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decided bar by bar. A value inside that bar's range is price-like - a median or typical price,
    /// a chained moving average - so the bar's true high and low still apply. A value outside it -
    /// an oscillator, a volume, a rescaled series - is its own scale, and its high and low are the
    /// max and min of the previous and current value.
    /// </para>
    /// <para>
    /// This replaces a whole-series rule that summed the input and compared the sum against the sums
    /// of the lows and highs. For a series entirely inside or entirely outside the bars' range the
    /// two agree exactly - the out-of-range branch was GetMaxAndMinValuesList(series, 0), whose window
    /// clamps to 2. What the per-bar rule adds is causality: it needs nothing after the current bar,
    /// so a streaming state can apply the same rule and the two engines agree bar by bar. The
    /// whole-series sum could not be computed by a streaming state at all.
    /// </para>
    /// </remarks>
    internal static (List<double> HighList, List<double> LowList) GetCustomRangeLists(IReadOnlyList<double> values,
        IReadOnlyList<double> highs, IReadOnlyList<double> lows)
    {
        var count = values.Count;
        var highList = new List<double>(count);
        var lowList = new List<double>(count);

        for (var i = 0; i < count; i++)
        {
            var value = values[i];
            var high = i < highs.Count ? highs[i] : value;
            var low = i < lows.Count ? lows[i] : value;

            if (value >= low && value <= high)
            {
                highList.Add(high);
                lowList.Add(low);
            }
            else
            {
                var prev = i > 0 ? values[i - 1] : value;
                highList.Add(Math.Max(prev, value));
                lowList.Add(Math.Min(prev, value));
            }
        }

        return (highList, lowList);
    }

    /// <summary>
    /// Gets the maximum and minimum values list.
    /// </summary>
    /// <param name="inputs">The inputs.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static (List<double>, List<double>) GetMaxAndMinValuesList(List<double> inputs, int length)
    {
        var count = inputs.Count;
        List<double> highestValuesList = new(count);
        List<double> lowestValuesList = new(count);
        var windowLength = Math.Max(length, 2);
        var window = new RollingMinMax(windowLength);

        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            window.Add(input);
            highestValuesList.Add(window.Max);
            lowestValuesList.Add(window.Min);
        }

        return (highestValuesList, lowestValuesList);
    }

    /// <summary>
    /// Gets the maximum and minimum values list.
    /// </summary>
    /// <param name="highList">The high list.</param>
    /// <param name="lowList">The low list.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static (List<double>, List<double>) GetMaxAndMinValuesList(List<double> highList, List<double> lowList, int length)
    {
        var count = highList.Count == lowList.Count ? highList.Count : 0;
        List<double> highestList = new(count);
        List<double> lowestList = new(count);
        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);

        for (var i = 0; i < count; i++)
        {
            var high = highList[i];
            var low = lowList[i];
            highWindow.Add(high);
            lowWindow.Add(low);
            highestList.Add(highWindow.Max);
            lowestList.Add(lowWindow.Min);
        }

        return (highestList, lowestList);
    }

    /// <summary>
    /// Ensures that there are enough past values to be able to perform calculations on the data
    /// </summary>
    /// <param name="currentIndex"></param>
    /// <param name="minIndex"></param>
    /// <param name="currentValue"></param>
    /// <returns></returns>
    public static double MinPastValues(int currentIndex, int minIndex, double currentValue)
    {
        return currentIndex >= minIndex ? currentValue : 0;
    }

    /// <summary>
    /// Extension for the default TakeLast method that works for older versions of .Net
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <param name="source"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static IEnumerable<T> TakeLastExt<T>(this IEnumerable<T> source, int count)
    {
        if (null == source)
            throw new ArgumentNullException(nameof(source));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (0 == count)
            yield break;

        if (source is IList<T> list)
        {
            var start = Math.Max(0, list.Count - count);
            for (var i = start; i < list.Count; i++)
                yield return list[i];

            yield break;
        }

        if (source is IReadOnlyList<T> readOnlyList)
        {
            var start = Math.Max(0, readOnlyList.Count - count);
            for (var i = start; i < readOnlyList.Count; i++)
                yield return readOnlyList[i];

            yield break;
        }

        if (source is ICollection<T> collection)
        {
            var skip = Math.Max(0, collection.Count - count);
            var index = 0;
            foreach (var item in source)
            {
                if (index++ >= skip)
                    yield return item;
            }

            yield break;
        }

        if (source is IReadOnlyCollection<T> readOnlyCollection)
        {
            var skip = Math.Max(0, readOnlyCollection.Count - count);
            var index = 0;
            foreach (var item in source)
            {
                if (index++ >= skip)
                    yield return item;
            }

            yield break;
        }

        Queue<T> result = new();

        foreach (var item in source)
        {
            if (result.Count == count)
                result.Dequeue();

            result.Enqueue(item);
        }

        while (result.Count > 0)
            yield return result.Dequeue();
    }

    /// <summary>
    /// Gets the Percentile Nearest Rank
    /// </summary>
    /// <param name="sequence"></param>
    /// <param name="percentile"></param>
    /// <returns></returns>
    public static double PercentileNearestRank(this IEnumerable<double> sequence, double percentile)
    {
        var list = new List<double>(sequence);
        list.Sort();
        var n = list.Count;
        var rank = n > 0 ? (int)Math.Ceiling(percentile / 100 * n) : 0;

        return list[Math.Max(rank - 1, 0)];
    }

    /// <summary>
    /// Rescales a value between a min and a max
    /// </summary>
    /// <param name="value"></param>
    /// <param name="oldMax"></param>
    /// <param name="oldMin"></param>
    /// <param name="newMax"></param>
    /// <param name="newMin"></param>
    /// <param name="isReversed"></param>
    /// <returns></returns>
    public static double RescaleValue(double value, double oldMax, double oldMin, double newMax, double newMin, bool isReversed = false)
    {
        var d = isReversed ? (oldMax - value) : (value - oldMin);
        var dRatio = oldMax - oldMin != 0 ? d / (oldMax - oldMin) : 0;

        return (dRatio * (newMax - newMin)) + newMin;
    }

    /// <summary>
    /// This needs to be called after you calculate an indicator if you are re-using the same input data to calculate a second indicator on a separate line
    /// </summary>
    /// <param name="stockData"></param>
    public static void Clear(this StockData stockData)
    {
        stockData.SignalsList?.Clear();
        stockData.CustomValuesList?.Clear();
    }

    /// <summary>
    /// Adds a rounded value to the list
    /// </summary>
    /// <param name="list"></param>
    /// <param name="value"></param>
    /// <param name="digits"></param>
    public static void AddRounded(this List<double> list, double value, int digits = 4)
    {
        list.Add(Math.Round(value, digits));
    }
}

internal enum DerivedSeriesKind
{
    Hl2,
    Hlc3,
    Ohlc4,
    WeightedClose,
    AveragePrice,
    TrueRange
}

