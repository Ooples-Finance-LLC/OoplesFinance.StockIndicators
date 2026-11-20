using OoplesFinance.StockIndicators.Builder;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Indicators.Fast;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// Holds every fast kernel to the exact route: same published series to within floating-point rounding, on
/// every output, from both source shapes and in both history modes.
/// </summary>
[Collection("IndicatorValuesDispatch")]
public sealed class FastNumericsParityTests
{
    // Relative to the larger of the exact value and the price scale, so a value that is legitimately near
    // zero is not held to a relative bound it cannot meet.
    private const double Tolerance = 1e-9;

    /// <summary>One configuration per kernel branch. Every type with a fast kernel must appear here.</summary>
    public static IEnumerable<(string Name, Func<IIndicator> Create)> Cases() =>
    [
        ("Sma(1)", () => new Sma(1)),
        ("Sma(20)", () => new Sma(20)),
        ("Sma(400)", () => new Sma(400)),
        ("FirstValueEma(1)", () => new FirstValueEma(1)),
        ("FirstValueEma(20)", () => new FirstValueEma(20)),
        ("SeededExponentialAverage(20,2,Shared)", () => new SeededExponentialAverage(20, 2, ExponentialSeedMode.Shared)),
        ("SeededExponentialAverage(20,3,Shared)", () => new SeededExponentialAverage(20, 3, ExponentialSeedMode.Shared)),
        ("SeededExponentialAverage(20,2,Cascaded)", () => new SeededExponentialAverage(20, 2, ExponentialSeedMode.Cascaded)),
        ("SeededExponentialAverage(5,3,Cascaded)", () => new SeededExponentialAverage(5, 3, ExponentialSeedMode.Cascaded)),
        ("SeededExponentialAverage(1,3,Cascaded)", () => new SeededExponentialAverage(1, 3, ExponentialSeedMode.Cascaded)),
        ("ClassicMovingAverage(1,Sma)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(2,Sma)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(9,Sma)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(30,Sma)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(14,Sma,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Sma, true, 3)),
        ("ClassicMovingAverage(14,Sma,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Sma, false, 5)),
        ("ClassicPriceOscillator(12,26,Sma)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Sma)),
        ("ClassicPriceOscillator(12,26,Sma,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Sma, true)),
        ("ClassicPriceOscillator(5,9,Sma,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Sma, true, true, 2)),
        ("ClassicMacd(12,26,9,Sma)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Sma, ClassicAverageMethod.Sma, ClassicAverageMethod.Sma)),
        ("ClassicMovingAverage(1,Ema)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(2,Ema)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(9,Ema)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(30,Ema)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(14,Ema,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Ema, true, 3)),
        ("ClassicMovingAverage(14,Ema,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Ema, false, 5)),
        ("ClassicPriceOscillator(12,26,Ema)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Ema)),
        ("ClassicPriceOscillator(12,26,Ema,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Ema, true)),
        ("ClassicPriceOscillator(5,9,Ema,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Ema, true, true, 2)),
        ("ClassicMacd(12,26,9,Ema)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Ema, ClassicAverageMethod.Ema, ClassicAverageMethod.Ema)),
        ("ClassicMovingAverage(1,Wma)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(2,Wma)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(9,Wma)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(30,Wma)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(14,Wma,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Wma, true, 3)),
        ("ClassicMovingAverage(14,Wma,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Wma, false, 5)),
        ("ClassicPriceOscillator(12,26,Wma)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Wma)),
        ("ClassicPriceOscillator(12,26,Wma,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Wma, true)),
        ("ClassicPriceOscillator(5,9,Wma,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Wma, true, true, 2)),
        ("ClassicMacd(12,26,9,Wma)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Wma, ClassicAverageMethod.Wma, ClassicAverageMethod.Wma)),
        ("ClassicMovingAverage(1,Dema)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(2,Dema)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(9,Dema)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(30,Dema)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(14,Dema,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Dema, true, 3)),
        ("ClassicMovingAverage(14,Dema,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Dema, false, 5)),
        ("ClassicPriceOscillator(12,26,Dema)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Dema)),
        ("ClassicPriceOscillator(12,26,Dema,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Dema, true)),
        ("ClassicPriceOscillator(5,9,Dema,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Dema, true, true, 2)),
        ("ClassicMacd(12,26,9,Dema)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Dema, ClassicAverageMethod.Dema, ClassicAverageMethod.Dema)),
        ("ClassicMovingAverage(1,Tema)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(2,Tema)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(9,Tema)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(30,Tema)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(14,Tema,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Tema, true, 3)),
        ("ClassicMovingAverage(14,Tema,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Tema, false, 5)),
        ("ClassicPriceOscillator(12,26,Tema)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Tema)),
        ("ClassicPriceOscillator(12,26,Tema,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Tema, true)),
        ("ClassicPriceOscillator(5,9,Tema,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Tema, true, true, 2)),
        ("ClassicMacd(12,26,9,Tema)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Tema, ClassicAverageMethod.Tema, ClassicAverageMethod.Tema)),
        ("ClassicMovingAverage(1,Trima)", () => new ClassicMovingAverage(1, ClassicAverageMethod.Trima)),
        ("ClassicMovingAverage(2,Trima)", () => new ClassicMovingAverage(2, ClassicAverageMethod.Trima)),
        ("ClassicMovingAverage(9,Trima)", () => new ClassicMovingAverage(9, ClassicAverageMethod.Trima)),
        ("ClassicMovingAverage(30,Trima)", () => new ClassicMovingAverage(30, ClassicAverageMethod.Trima)),
        ("ClassicMovingAverage(14,Trima,firstSeed,supp3)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Trima, true, 3)),
        ("ClassicMovingAverage(14,Trima,supp5)", () => new ClassicMovingAverage(14, ClassicAverageMethod.Trima, false, 5)),
        ("ClassicPriceOscillator(12,26,Trima)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Trima)),
        ("ClassicPriceOscillator(12,26,Trima,pct)", () => new ClassicPriceOscillator(12, 26, ClassicAverageMethod.Trima, true)),
        ("ClassicPriceOscillator(5,9,Trima,pct,first,supp2)", () => new ClassicPriceOscillator(5, 9, ClassicAverageMethod.Trima, true, true, 2)),
        ("ClassicMacd(12,26,9,Trima)", () => new ClassicMacd(12, 26, 9, ClassicAverageMethod.Trima, ClassicAverageMethod.Trima, ClassicAverageMethod.Trima)),
        ("ClassicMacd(mixed)", () => new ClassicMacd(26, 12, 9, ClassicAverageMethod.Ema, ClassicAverageMethod.Wma, ClassicAverageMethod.Tema)),
        ("ClassicMacd(first,supp)", () => new ClassicMacd(5, 13, 4, ClassicAverageMethod.Ema, ClassicAverageMethod.Dema, ClassicAverageMethod.Ema, true, 2)),
        ("ClassicMacd(supp)", () => new ClassicMacd(5, 13, 4, ClassicAverageMethod.Tema, ClassicAverageMethod.Ema, ClassicAverageMethod.Trima, false, 3)),
        ("SeededAverageTrueRange(1)", () => new SeededAverageTrueRange(1)),
        ("SeededAverageTrueRange(14)", () => new SeededAverageTrueRange(14)),
        ("NormalizedSeededAverageTrueRange(1)", () => new NormalizedSeededAverageTrueRange(1)),
        ("NormalizedSeededAverageTrueRange(14)", () => new NormalizedSeededAverageTrueRange(14)),
        ("SmoothedAccumulationOscillator()", () => new SmoothedAccumulationOscillator()),
        ("SmoothedAccumulationOscillator(details)", () => new SmoothedAccumulationOscillator(3, 10, false, 0, true)),
        ("SmoothedAccumulationOscillator(first,supp,details)", () => new SmoothedAccumulationOscillator(10, 3, true, 4, true)),
        ("SmoothedAccumulationOscillator(1,1)", () => new SmoothedAccumulationOscillator(1, 1)),
        ("WilderStrengthOscillator(14)", () => new WilderStrengthOscillator(14)),
        ("WilderStrengthOscillator(1)", () => new WilderStrengthOscillator(1)),
        ("WilderStrengthOscillator(14,HundredFlat)", () => new WilderStrengthOscillator(14, WilderStrengthConvention.RsiHundredFlat)),
        ("WilderStrengthOscillator(9,Chande,unstable5)", () => new WilderStrengthOscillator(9, WilderStrengthConvention.ChandeZeroFlat, 5)),
        ("TillsonAverage(1,.7,FirstPrice,0)", () => new TillsonAverage(1, .7, TillsonSeed.FirstPrice, 0)),
        ("TillsonAverage(5,.7,FirstPrice,0)", () => new TillsonAverage(5, .7, TillsonSeed.FirstPrice, 0)),
        ("TillsonAverage(14,.7,FirstPrice,3)", () => new TillsonAverage(14, .7, TillsonSeed.FirstPrice, 3)),
        ("TillsonAverage(1,.7,FollowingPrefix,0)", () => new TillsonAverage(1, .7, TillsonSeed.FollowingPrefix, 0)),
        ("TillsonAverage(5,.7,FollowingPrefix,0)", () => new TillsonAverage(5, .7, TillsonSeed.FollowingPrefix, 0)),
        ("TillsonAverage(14,.7,FollowingPrefix,3)", () => new TillsonAverage(14, .7, TillsonSeed.FollowingPrefix, 3)),
        ("TillsonAverage(1,.7,CascadedMeans,0)", () => new TillsonAverage(1, .7, TillsonSeed.CascadedMeans, 0)),
        ("TillsonAverage(5,.7,CascadedMeans,0)", () => new TillsonAverage(5, .7, TillsonSeed.CascadedMeans, 0)),
        ("TillsonAverage(14,.7,CascadedMeans,3)", () => new TillsonAverage(14, .7, TillsonSeed.CascadedMeans, 3)),
        ("TillsonAverage(8,.4,CascadedMeans)", () => new TillsonAverage(8, .4, TillsonSeed.CascadedMeans)),
        ("PriceRoundingTransform(Ceiling)", () => new PriceRoundingTransform(PriceRoundingOperation.Ceiling)),
        ("PriceRoundingTransform(Floor)", () => new PriceRoundingTransform(PriceRoundingOperation.Floor)),
        ("PriceRoundingTransform(SquareRoot)", () => new PriceRoundingTransform(PriceRoundingOperation.SquareRoot)),
        ("PriceCircularTransform(Sine)", () => new PriceCircularTransform(PriceCircularOperation.Sine)),
        ("PriceCircularTransform(Cosine)", () => new PriceCircularTransform(PriceCircularOperation.Cosine)),
        ("PriceCircularTransform(Tangent)", () => new PriceCircularTransform(PriceCircularOperation.Tangent)),
        ("PriceCircularTransform(ArcSine)", () => new PriceCircularTransform(PriceCircularOperation.ArcSine)),
        ("PriceCircularTransform(ArcCosine)", () => new PriceCircularTransform(PriceCircularOperation.ArcCosine)),
        ("PriceCircularTransform(ArcTangent)", () => new PriceCircularTransform(PriceCircularOperation.ArcTangent)),
        ("PriceTranscendentalTransform(NaturalLogarithm)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.NaturalLogarithm)),
        ("PriceTranscendentalTransform(CommonLogarithm)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.CommonLogarithm)),
        ("PriceTranscendentalTransform(Exponential)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.Exponential)),
        ("PriceTranscendentalTransform(HyperbolicSine)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicSine)),
        ("PriceTranscendentalTransform(HyperbolicCosine)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicCosine)),
        ("PriceTranscendentalTransform(HyperbolicTangent)", () => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicTangent)),
        ("TripleExponentialRate()", () => new TripleExponentialRate()),
        ("TripleExponentialRate(1,shared,signal 1)", () => new TripleExponentialRate(1, TripleRateSeed.SharedMean, 1)),
        ("TripleExponentialRate(9,shared,signal 5)", () => new TripleExponentialRate(9, TripleRateSeed.SharedMean, 5)),
        ("TripleExponentialRate(5,means,signal 3,supp 2)", () => new TripleExponentialRate(5, TripleRateSeed.CascadedMeans, 3, 2)),
        ("TripleExponentialRate(14,means)", () => new TripleExponentialRate(14, TripleRateSeed.CascadedMeans)),
        ("TripleExponentialRate(1,means,signal 2)", () => new TripleExponentialRate(1, TripleRateSeed.CascadedMeans, 2)),
        ("TripleExponentialRate(6,first,signal 4)", () => new TripleExponentialRate(6, TripleRateSeed.CascadedFirstPrice, 4)),
        ("TripleExponentialRate(3,first,supp 4)", () => new TripleExponentialRate(3, TripleRateSeed.CascadedFirstPrice, null, 4)),
        ("Directional(PositiveMovement)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveMovement)),
        ("Directional(PositiveMovement,1)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveMovement, 1)),
        ("Directional(PositiveMovement,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveMovement, 5, 3)),
        ("Directional(NegativeMovement)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeMovement)),
        ("Directional(NegativeMovement,1)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeMovement, 1)),
        ("Directional(NegativeMovement,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeMovement, 5, 3)),
        ("Directional(PositiveIndicator)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveIndicator)),
        ("Directional(PositiveIndicator,1)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveIndicator, 1)),
        ("Directional(PositiveIndicator,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveIndicator, 5, 3)),
        ("Directional(NegativeIndicator)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeIndicator)),
        ("Directional(NegativeIndicator,1)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeIndicator, 1)),
        ("Directional(NegativeIndicator,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeIndicator, 5, 3)),
        ("Directional(Index)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Index)),
        ("Directional(Index,2)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Index, 2)),
        ("Directional(Index,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Index, 5, 3)),
        ("Directional(Index,9, 20)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Index, 9, 20)),
        ("Directional(Average)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average)),
        ("Directional(Average,2)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average, 2)),
        ("Directional(Average,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average, 5, 3)),
        ("Directional(Average,9, 20)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average, 9, 20)),
        ("Directional(Rating)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Rating)),
        ("Directional(Rating,2)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Rating, 2)),
        ("Directional(Rating,5, 3)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Rating, 5, 3)),
        ("Directional(Rating,9, 20)", () => new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Rating, 9, 20)),
        ("WindowDispersion(20, Variance)", () => new WindowDispersion(20, WindowDispersionOutput.Variance)),
        ("WindowDispersion(1, Variance)", () => new WindowDispersion(1, WindowDispersionOutput.Variance)),
        ("WindowDispersion(2, Variance)", () => new WindowDispersion(2, WindowDispersionOutput.Variance)),
        ("WindowDispersion(5, Variance, true)", () => new WindowDispersion(5, WindowDispersionOutput.Variance, true)),
        ("WindowDispersion(50, Variance, false, -2.5)", () => new WindowDispersion(50, WindowDispersionOutput.Variance, false, -2.5)),
        ("WindowDispersion(14, Variance, true, 0)", () => new WindowDispersion(14, WindowDispersionOutput.Variance, true, 0)),
        ("WindowDispersion(400, Variance, true)", () => new WindowDispersion(400, WindowDispersionOutput.Variance, true)),
        ("WindowDispersion(20, StandardDeviation)", () => new WindowDispersion(20, WindowDispersionOutput.StandardDeviation)),
        ("WindowDispersion(1, StandardDeviation)", () => new WindowDispersion(1, WindowDispersionOutput.StandardDeviation)),
        ("WindowDispersion(2, StandardDeviation)", () => new WindowDispersion(2, WindowDispersionOutput.StandardDeviation)),
        ("WindowDispersion(5, StandardDeviation, true)", () => new WindowDispersion(5, WindowDispersionOutput.StandardDeviation, true)),
        ("WindowDispersion(50, StandardDeviation, false, -2.5)", () => new WindowDispersion(50, WindowDispersionOutput.StandardDeviation, false, -2.5)),
        ("WindowDispersion(14, StandardDeviation, true, 0)", () => new WindowDispersion(14, WindowDispersionOutput.StandardDeviation, true, 0)),
        ("WindowDispersion(400, StandardDeviation, true)", () => new WindowDispersion(400, WindowDispersionOutput.StandardDeviation, true)),
        ("WindowDispersion(20, ZScore)", () => new WindowDispersion(20, WindowDispersionOutput.ZScore)),
        ("WindowDispersion(1, ZScore)", () => new WindowDispersion(1, WindowDispersionOutput.ZScore)),
        ("WindowDispersion(2, ZScore)", () => new WindowDispersion(2, WindowDispersionOutput.ZScore)),
        ("WindowDispersion(5, ZScore, true)", () => new WindowDispersion(5, WindowDispersionOutput.ZScore, true)),
        ("WindowDispersion(50, ZScore, false, -2.5)", () => new WindowDispersion(50, WindowDispersionOutput.ZScore, false, -2.5)),
        ("WindowDispersion(14, ZScore, true, 0)", () => new WindowDispersion(14, WindowDispersionOutput.ZScore, true, 0)),
        ("WindowDispersion(400, ZScore, true)", () => new WindowDispersion(400, WindowDispersionOutput.ZScore, true)),
        ("ClassicStochastic()", () => new ClassicStochastic()),
        ("ClassicStochastic(14, 3, 3)", () => new ClassicStochastic(14, 3, 3)),
        ("ClassicStochastic(1, 1, 1)", () => new ClassicStochastic(1, 1, 1)),
        ("ClassicStochastic(5, 1, 3)", () => new ClassicStochastic(5, 1, 3)),
        ("ClassicStochastic(9, 3, 1)", () => new ClassicStochastic(9, 3, 1)),
        ("ClassicStochastic(14, 5, 4, Ema, Ema)", () => new ClassicStochastic(14, 5, 4, ClassicAverageMethod.Ema, ClassicAverageMethod.Ema)),
        ("ClassicStochastic(14, 5, 4, Ema, Wma, true)", () => new ClassicStochastic(14, 5, 4, ClassicAverageMethod.Ema, ClassicAverageMethod.Wma, true)),
        ("ClassicStochastic(10, 3, 5, Dema, Tema, false, 2)", () => new ClassicStochastic(10, 3, 5, ClassicAverageMethod.Dema, ClassicAverageMethod.Tema, false, 2)),
        ("ClassicStochastic(40, 6, 3, Trima, Sma)", () => new ClassicStochastic(40, 6, 3, ClassicAverageMethod.Trima, ClassicAverageMethod.Sma)),
        ("ClassicStochastic(5, 3, 3, Wma, Ema, true, 3)", () => new ClassicStochastic(5, 3, 3, ClassicAverageMethod.Wma, ClassicAverageMethod.Ema, true, 3)),
        ("SeededAdaptiveAverage()", () => new SeededAdaptiveAverage()),
        ("SeededAdaptiveAverage(1)", () => new SeededAdaptiveAverage(1)),
        ("SeededAdaptiveAverage(5, 2, 30, true)", () => new SeededAdaptiveAverage(5, 2, 30, true)),
        ("SeededAdaptiveAverage(10, 20, 30, false, true)", () => new SeededAdaptiveAverage(10, 20, 30, false, true)),
        ("SeededAdaptiveAverage(10, 2, 30, false, false, false)", () => new SeededAdaptiveAverage(10, 2, 30, false, false, false)),
        ("SeededAdaptiveAverage(10, 2, 30, false, false, true, true)", () => new SeededAdaptiveAverage(10, 2, 30, false, false, true, true)),
        ("SeededAdaptiveAverage(7, 3, 12, true, true, false, true, 4)", () => new SeededAdaptiveAverage(7, 3, 12, true, true, false, true, 4)),
        ("SeededAdaptiveAverage(300, 2, 30)", () => new SeededAdaptiveAverage(300, 2, 30)),
        ("WindowLinearRegression(14,Endpoint)", () => new WindowLinearRegression(14, WindowRegressionOutput.Endpoint)),
        ("WindowLinearRegression(1,Endpoint)", () => new WindowLinearRegression(1, WindowRegressionOutput.Endpoint)),
        ("WindowLinearRegression(2,Endpoint)", () => new WindowLinearRegression(2, WindowRegressionOutput.Endpoint)),
        ("WindowLinearRegression(300,Endpoint)", () => new WindowLinearRegression(300, WindowRegressionOutput.Endpoint)),
        ("WindowLinearRegression(14,Forecast)", () => new WindowLinearRegression(14, WindowRegressionOutput.Forecast)),
        ("WindowLinearRegression(1,Forecast)", () => new WindowLinearRegression(1, WindowRegressionOutput.Forecast)),
        ("WindowLinearRegression(2,Forecast)", () => new WindowLinearRegression(2, WindowRegressionOutput.Forecast)),
        ("WindowLinearRegression(300,Forecast)", () => new WindowLinearRegression(300, WindowRegressionOutput.Forecast)),
        ("WindowLinearRegression(14,Slope)", () => new WindowLinearRegression(14, WindowRegressionOutput.Slope)),
        ("WindowLinearRegression(1,Slope)", () => new WindowLinearRegression(1, WindowRegressionOutput.Slope)),
        ("WindowLinearRegression(2,Slope)", () => new WindowLinearRegression(2, WindowRegressionOutput.Slope)),
        ("WindowLinearRegression(300,Slope)", () => new WindowLinearRegression(300, WindowRegressionOutput.Slope)),
        ("WindowLinearRegression(14,Intercept)", () => new WindowLinearRegression(14, WindowRegressionOutput.Intercept)),
        ("WindowLinearRegression(1,Intercept)", () => new WindowLinearRegression(1, WindowRegressionOutput.Intercept)),
        ("WindowLinearRegression(2,Intercept)", () => new WindowLinearRegression(2, WindowRegressionOutput.Intercept)),
        ("WindowLinearRegression(300,Intercept)", () => new WindowLinearRegression(300, WindowRegressionOutput.Intercept)),
        ("WindowLinearRegression(14,Angle)", () => new WindowLinearRegression(14, WindowRegressionOutput.Angle)),
        ("WindowLinearRegression(1,Angle)", () => new WindowLinearRegression(1, WindowRegressionOutput.Angle)),
        ("WindowLinearRegression(2,Angle)", () => new WindowLinearRegression(2, WindowRegressionOutput.Angle)),
        ("WindowLinearRegression(300,Angle)", () => new WindowLinearRegression(300, WindowRegressionOutput.Angle)),
        ("Ema(1)", () => new Ema(1)),
        ("Ema(14)", () => new Ema(14)),
        ("Ema(40)", () => new Ema(40)),
        ("Ema(300)", () => new Ema(300)),
        ("Wma(1)", () => new Wma(1)),
        ("Wma(14)", () => new Wma(14)),
        ("Wma(40)", () => new Wma(40)),
        ("Wma(300)", () => new Wma(300)),
        ("HighestHigh(1)", () => new HighestHigh(1)),
        ("HighestHigh(14)", () => new HighestHigh(14)),
        ("HighestHigh(40)", () => new HighestHigh(40)),
        ("HighestHigh(300)", () => new HighestHigh(300)),
        ("LowestLow(1)", () => new LowestLow(1)),
        ("LowestLow(14)", () => new LowestLow(14)),
        ("LowestLow(40)", () => new LowestLow(40)),
        ("LowestLow(300)", () => new LowestLow(300)),
        ("WilliamsR(1)", () => new WilliamsR(1)),
        ("WilliamsR(14)", () => new WilliamsR(14)),
        ("WilliamsR(40)", () => new WilliamsR(40)),
        ("WilliamsR(300)", () => new WilliamsR(300)),
        ("MedianPrice(1)", () => new MedianPrice(1)),
        ("TypicalPrice(1)", () => new TypicalPrice(1)),
        ("WeightedClose(1)", () => new WeightedClose(1)),
        ("FullTypicalPrice(1)", () => new FullTypicalPrice(1)),
        ("TrueRange(1)", () => new TrueRange(1)),
        ("WilderMovingAverage()", () => new WilderMovingAverage()),
        ("WilderMovingAverage(1)", () => new WilderMovingAverage(1)),
        ("WilderMovingAverage(5, false)", () => new WilderMovingAverage(5, false)),
        ("WilderMovingAverage(40, true)", () => new WilderMovingAverage(40, true)),
        ("WilderMovingAverage(1, false)", () => new WilderMovingAverage(1, false)),
        ("CompensatedExponentialAverage()", () => new CompensatedExponentialAverage()),
        ("CompensatedExponentialAverage(1, 2)", () => new CompensatedExponentialAverage(1, 2)),
        ("CompensatedExponentialAverage(9, 3)", () => new CompensatedExponentialAverage(9, 3)),
        ("CompensatedExponentialAverage(200, 3)", () => new CompensatedExponentialAverage(200, 3)),
        ("CompensatedExponentialAverage(2, 2)", () => new CompensatedExponentialAverage(2, 2)),
        ("CandleArithmetic(Add)", () => new CandleArithmetic(CandleArithmeticOperation.Add)),
        ("CandleArithmetic(Add,High, Low)", () => new CandleArithmetic(CandleArithmeticOperation.Add, CandlePriceField.High, CandlePriceField.Low)),
        ("CandleArithmetic(Add,Low, Close)", () => new CandleArithmetic(CandleArithmeticOperation.Add, CandlePriceField.Low, CandlePriceField.Close)),
        ("CandleArithmetic(Subtract)", () => new CandleArithmetic(CandleArithmeticOperation.Subtract)),
        ("CandleArithmetic(Subtract,High, Low)", () => new CandleArithmetic(CandleArithmeticOperation.Subtract, CandlePriceField.High, CandlePriceField.Low)),
        ("CandleArithmetic(Subtract,Low, Close)", () => new CandleArithmetic(CandleArithmeticOperation.Subtract, CandlePriceField.Low, CandlePriceField.Close)),
        ("CandleArithmetic(Multiply)", () => new CandleArithmetic(CandleArithmeticOperation.Multiply)),
        ("CandleArithmetic(Multiply,High, Low)", () => new CandleArithmetic(CandleArithmeticOperation.Multiply, CandlePriceField.High, CandlePriceField.Low)),
        ("CandleArithmetic(Multiply,Low, Close)", () => new CandleArithmetic(CandleArithmeticOperation.Multiply, CandlePriceField.Low, CandlePriceField.Close)),
        ("CandleArithmetic(Divide)", () => new CandleArithmetic(CandleArithmeticOperation.Divide)),
        ("CandleArithmetic(Divide,High, Low)", () => new CandleArithmetic(CandleArithmeticOperation.Divide, CandlePriceField.High, CandlePriceField.Low)),
        ("CandleArithmetic(Divide,Low, Close)", () => new CandleArithmetic(CandleArithmeticOperation.Divide, CandlePriceField.Low, CandlePriceField.Close)),
        ("WindowAroon()", () => new WindowAroon()),
        ("WindowAroon(1)", () => new WindowAroon(1)),
        ("WindowAroon(25, false)", () => new WindowAroon(25, false)),
        ("WindowAroon(5, true, true)", () => new WindowAroon(5, true, true)),
        ("WindowAroon(40, false, true)", () => new WindowAroon(40, false, true)),
        ("WindowAroon(300)", () => new WindowAroon(300)),
        ("WindowExtremeIndex()", () => new WindowExtremeIndex()),
        ("WindowExtremeIndex(1, true)", () => new WindowExtremeIndex(1, true)),
        ("WindowExtremeIndex(14, true)", () => new WindowExtremeIndex(14, true)),
        ("WindowExtremeIndex(100, false)", () => new WindowExtremeIndex(100, false)),
        ("WindowExtremeIndex(100, true)", () => new WindowExtremeIndex(100, true)),
        ("WindowRangeMidpoint()", () => new WindowRangeMidpoint()),
        ("WindowRangeMidpoint(1)", () => new WindowRangeMidpoint(1)),
        ("WindowRangeMidpoint(14, true)", () => new WindowRangeMidpoint(14, true)),
        ("WindowRangeMidpoint(100, true)", () => new WindowRangeMidpoint(100, true)),
        ("WindowRangeMidpoint(100)", () => new WindowRangeMidpoint(100)),
        ("EmaDifferenceSignal()", () => new EmaDifferenceSignal()),
        ("EmaDifferenceSignal(1, 1, 1)", () => new EmaDifferenceSignal(1, 1, 1)),
        ("EmaDifferenceSignal(5, 9, 4, true)", () => new EmaDifferenceSignal(5, 9, 4, true)),
        ("EmaDifferenceSignal(12, 26, 9, false, true)", () => new EmaDifferenceSignal(12, 26, 9, false, true)),
        ("EmaDifferenceSignal(12, 26, 9, true, true, true)", () => new EmaDifferenceSignal(12, 26, 9, true, true, true)),
        ("EmaDifferenceSignal(3, 10, 16, false, false, true)", () => new EmaDifferenceSignal(3, 10, 16, false, false, true)),
        ("EmaDifferenceSignal(12, 26, 9, false, false, false, Oscillator)", () => new EmaDifferenceSignal(12, 26, 9, false, false, false, EmaDifferenceSelection.Oscillator)),
        ("EmaDifferenceSignal(12, 26, 9, false, true, false, Signal | Histogram)", () => new EmaDifferenceSignal(12, 26, 9, false, true, false, EmaDifferenceSelection.Signal | EmaDifferenceSelection.Histogram)),
        ("EmaDifferenceSignal(8, 3, 5, false, false, false, FastAverage | SlowAverage)", () => new EmaDifferenceSignal(8, 3, 5, false, false, false, EmaDifferenceSelection.FastAverage | EmaDifferenceSelection.SlowAverage)),
        ("WindowCommodityChannelIndex()", () => new WindowCommodityChannelIndex()),
        ("WindowCommodityChannelIndex(1)", () => new WindowCommodityChannelIndex(1)),
        ("WindowCommodityChannelIndex(5, true)", () => new WindowCommodityChannelIndex(5, true)),
        ("WindowCommodityChannelIndex(20, true, true)", () => new WindowCommodityChannelIndex(20, true, true)),
        ("WindowCommodityChannelIndex(14, false, true)", () => new WindowCommodityChannelIndex(14, false, true)),
        ("WindowCommodityChannelIndex(300)", () => new WindowCommodityChannelIndex(300)),
        ("WindowCommodityChannelIndex(2, true)", () => new WindowCommodityChannelIndex(2, true)),
        ("WindowMoneyFlowIndex()", () => new WindowMoneyFlowIndex()),
        ("WindowMoneyFlowIndex(2)", () => new WindowMoneyFlowIndex(2)),
        ("WindowMoneyFlowIndex(5, true)", () => new WindowMoneyFlowIndex(5, true)),
        ("WindowMoneyFlowIndex(14, false, 7)", () => new WindowMoneyFlowIndex(14, false, 7)),
        ("WindowMoneyFlowIndex(300)", () => new WindowMoneyFlowIndex(300)),
        ("WindowMoneyFlowIndex(2, true, 3)", () => new WindowMoneyFlowIndex(2, true, 3)),
        ("ArnaudLegouxWindow()", () => new ArnaudLegouxWindow()),
        ("ArnaudLegouxWindow(1)", () => new ArnaudLegouxWindow(1)),
        ("ArnaudLegouxWindow(9, .85, 6, false)", () => new ArnaudLegouxWindow(9, .85, 6, false)),
        ("ArnaudLegouxWindow(21, 0, 6)", () => new ArnaudLegouxWindow(21, 0, 6)),
        ("ArnaudLegouxWindow(21, 1, 6, false)", () => new ArnaudLegouxWindow(21, 1, 6, false)),
        ("ArnaudLegouxWindow(50, .5, 60)", () => new ArnaudLegouxWindow(50, .5, 60)),
        ("ArnaudLegouxWindow(200, .85, 6)", () => new ArnaudLegouxWindow(200, .85, 6)),
        ("ArnaudLegouxWindow(5, .3, .01, false)", () => new ArnaudLegouxWindow(5, .3, .01, false)),
        ("WindowLogVolatility()", () => new WindowLogVolatility()),
        ("WindowLogVolatility(2)", () => new WindowLogVolatility(2)),
        ("WindowLogVolatility(20, true)", () => new WindowLogVolatility(20, true)),
        ("WindowLogVolatility(20, true, false)", () => new WindowLogVolatility(20, true, false)),
        ("WindowLogVolatility(5, false, false)", () => new WindowLogVolatility(5, false, false)),
        ("WindowLogVolatility(300, true)", () => new WindowLogVolatility(300, true)),
        ("WindowLogVolatility(300)", () => new WindowLogVolatility(300)),
        ("SeededAtrTrailingStop()", () => new SeededAtrTrailingStop()),
        ("SeededAtrTrailingStop(2, 1)", () => new SeededAtrTrailingStop(2, 1)),
        ("SeededAtrTrailingStop(10, 2.5, HighLow)", () => new SeededAtrTrailingStop(10, 2.5, AtrTrailBasis.HighLow)),
        ("SeededAtrTrailingStop(21, 0.5, Midpoint)", () => new SeededAtrTrailingStop(21, 0.5, AtrTrailBasis.Midpoint)),
        ("SeededAtrTrailingStop(300, 3, Midpoint)", () => new SeededAtrTrailingStop(300, 3, AtrTrailBasis.Midpoint)),
        ("SeededAtrTrailingStop(5, 4, HighLow)", () => new SeededAtrTrailingStop(5, 4, AtrTrailBasis.HighLow)),
        ("RollingPriceSum()", () => new RollingPriceSum()),
        ("RollingPriceSum(1)", () => new RollingPriceSum(1)),
        ("RollingPriceSum(2)", () => new RollingPriceSum(2)),
        ("RollingPriceSum(300)", () => new RollingPriceSum(300)),
        ("ReverseWilderAverage()", () => new ReverseWilderAverage()),
        ("ReverseWilderAverage(5)", () => new ReverseWilderAverage(5)),
        ("ReverseWilderAverage(300)", () => new ReverseWilderAverage(300)),
        ("SeededPriceMomentum()", () => new SeededPriceMomentum()),
        ("SeededPriceMomentum(2, 1, 1)", () => new SeededPriceMomentum(2, 1, 1)),
        ("SeededPriceMomentum(5, 3, 4)", () => new SeededPriceMomentum(5, 3, 4)),
        ("SeededPriceMomentum(10, 30, 2)", () => new SeededPriceMomentum(10, 30, 2)),
        ("SeededPriceMomentum(300, 20, 10)", () => new SeededPriceMomentum(300, 20, 10)),
        ("ClassicDeviationBands()", () => new ClassicDeviationBands()),
        ("ClassicDeviationBands(1)", () => new ClassicDeviationBands(1)),
        ("ClassicDeviationBands(20, 2.5, 1.5)", () => new ClassicDeviationBands(20, 2.5, 1.5)),
        ("ClassicDeviationBands(300, 0, 3)", () => new ClassicDeviationBands(300, 0, 3)),
        ("ClassicDeviationBands(20, 2, 2, Ema)", () => new ClassicDeviationBands(20, 2, 2, ClassicAverageMethod.Ema)),
        ("ClassicDeviationBands(10, 1, 2, Ema, true)", () => new ClassicDeviationBands(10, 1, 2, ClassicAverageMethod.Ema, true)),
        ("ClassicDeviationBands(14, 2, 2, Wma)", () => new ClassicDeviationBands(14, 2, 2, ClassicAverageMethod.Wma)),
        ("ClassicDeviationBands(9, 2, 2, Dema)", () => new ClassicDeviationBands(9, 2, 2, ClassicAverageMethod.Dema)),
        ("ClassicDeviationBands(9, 2, 2, Tema, false, 5)", () => new ClassicDeviationBands(9, 2, 2, ClassicAverageMethod.Tema, false, 5)),
        ("ClassicDeviationBands(12, 2, 2, Trima)", () => new ClassicDeviationBands(12, 2, 2, ClassicAverageMethod.Trima)),
        ("LaggedPriceChange()", () => new LaggedPriceChange()),
        ("LaggedPriceChange(1, Fraction)", () => new LaggedPriceChange(1, PriceChangeKind.Fraction)),
        ("LaggedPriceChange(5, Percent)", () => new LaggedPriceChange(5, PriceChangeKind.Percent)),
        ("LaggedPriceChange(12, Ratio)", () => new LaggedPriceChange(12, PriceChangeKind.Ratio)),
        ("LaggedPriceChange(12, RatioPercent)", () => new LaggedPriceChange(12, PriceChangeKind.RatioPercent)),
        ("LaggedPriceChange(3, Gain)", () => new LaggedPriceChange(3, PriceChangeKind.Gain)),
        ("LaggedPriceChange(3, Loss)", () => new LaggedPriceChange(3, PriceChangeKind.Loss)),
        ("LaggedPriceChange(300, Percent)", () => new LaggedPriceChange(300, PriceChangeKind.Percent)),
        ("RangeAccelerationBands()", () => new RangeAccelerationBands()),
        ("RangeAccelerationBands(1)", () => new RangeAccelerationBands(1)),
        ("RangeAccelerationBands(7)", () => new RangeAccelerationBands(7)),
        ("RangeAccelerationBands(300)", () => new RangeAccelerationBands(300)),
        ("SeededTrueStrength()", () => new SeededTrueStrength()),
        ("SeededTrueStrength(1, 1, 0)", () => new SeededTrueStrength(1, 1, 0)),
        ("SeededTrueStrength(1, 1, 3)", () => new SeededTrueStrength(1, 1, 3)),
        ("SeededTrueStrength(5, 1, 4)", () => new SeededTrueStrength(5, 1, 4)),
        ("SeededTrueStrength(3, 7, 1)", () => new SeededTrueStrength(3, 7, 1)),
        ("SeededTrueStrength(13, 25, 9)", () => new SeededTrueStrength(13, 25, 9)),
        ("SeededTrueStrength(300, 13, 7)", () => new SeededTrueStrength(300, 13, 7)),
        ("SeededForceIndex()", () => new SeededForceIndex()),
        ("SeededForceIndex(1)", () => new SeededForceIndex(1)),
        ("SeededForceIndex(2)", () => new SeededForceIndex(2)),
        ("SeededForceIndex(300)", () => new SeededForceIndex(300)),
        ("SineWeightedAverage()", () => new SineWeightedAverage()),
        ("SineWeightedAverage(1)", () => new SineWeightedAverage(1)),
        ("SineWeightedAverage(2)", () => new SineWeightedAverage(2)),
        ("SineWeightedAverage(300)", () => new SineWeightedAverage(300)),
        ("GaussianWeightedAverage()", () => new GaussianWeightedAverage()),
        ("GaussianWeightedAverage(2, 0.5)", () => new GaussianWeightedAverage(2, 0.5)),
        ("GaussianWeightedAverage(9, -2)", () => new GaussianWeightedAverage(9, -2)),
        ("GaussianWeightedAverage(30, 0.01)", () => new GaussianWeightedAverage(30, 0.01)),
        ("GaussianWeightedAverage(300, 3)", () => new GaussianWeightedAverage(300, 3)),
        ("TriangularWindowAverage()", () => new TriangularWindowAverage()),
        ("TriangularWindowAverage(1)", () => new TriangularWindowAverage(1)),
        ("TriangularWindowAverage(2)", () => new TriangularWindowAverage(2)),
        ("TriangularWindowAverage(9)", () => new TriangularWindowAverage(9)),
        ("TriangularWindowAverage(300)", () => new TriangularWindowAverage(300)),
        ("WindowMeanAbsoluteDeviation()", () => new WindowMeanAbsoluteDeviation()),
        ("WindowMeanAbsoluteDeviation(1)", () => new WindowMeanAbsoluteDeviation(1)),
        ("WindowMeanAbsoluteDeviation(2)", () => new WindowMeanAbsoluteDeviation(2)),
        ("WindowMeanAbsoluteDeviation(300)", () => new WindowMeanAbsoluteDeviation(300)),
        ("MovingAverageDiagnostics()", () => new MovingAverageDiagnostics()),
        ("MovingAverageDiagnostics(1)", () => new MovingAverageDiagnostics(1)),
        ("MovingAverageDiagnostics(7)", () => new MovingAverageDiagnostics(7)),
        ("MovingAverageDiagnostics(300)", () => new MovingAverageDiagnostics(300)),
        ("WindowDeviationBands()", () => new WindowDeviationBands()),
        ("WindowDeviationBands(1)", () => new WindowDeviationBands(1)),
        ("WindowDeviationBands(2, 1)", () => new WindowDeviationBands(2, 1)),
        ("WindowDeviationBands(10, 1.5, true)", () => new WindowDeviationBands(10, 1.5, true)),
        ("WindowDeviationBands(50, -2)", () => new WindowDeviationBands(50, -2)),
        ("WindowDeviationBands(20, 0)", () => new WindowDeviationBands(20, 0)),
        ("WindowDeviationBands(300, 2.5, true)", () => new WindowDeviationBands(300, 2.5, true)),
        ("Adl()", () => new Adl()),
        ("Adl(1)", () => new Adl(1)),
        ("Adl(2)", () => new Adl(2)),
        ("Adl(300)", () => new Adl(300)),
        ("BalanceOfPower()", () => new BalanceOfPower()),
        ("BalanceOfPower(1)", () => new BalanceOfPower(1)),
        ("BalanceOfPower(2)", () => new BalanceOfPower(2)),
        ("BalanceOfPower(300)", () => new BalanceOfPower(300)),
        ("ExpandingRegularizedEma()", () => new ExpandingRegularizedEma()),
        ("ExpandingRegularizedEma(1, 0)", () => new ExpandingRegularizedEma(1, 0)),
        ("ExpandingRegularizedEma(2, 1)", () => new ExpandingRegularizedEma(2, 1)),
        ("ExpandingRegularizedEma(30, 0.1)", () => new ExpandingRegularizedEma(30, 0.1)),
        ("ExpandingRegularizedEma(300, 0.5)", () => new ExpandingRegularizedEma(300, 0.5)),
        ("ExpandingZeroLagEma()", () => new ExpandingZeroLagEma()),
        ("ExpandingZeroLagEma(1)", () => new ExpandingZeroLagEma(1)),
        ("ExpandingZeroLagEma(2)", () => new ExpandingZeroLagEma(2)),
        ("ExpandingZeroLagEma(3)", () => new ExpandingZeroLagEma(3)),
        ("ExpandingZeroLagEma(300)", () => new ExpandingZeroLagEma(300)),
        ("SeededOnBalanceVolume()", () => new SeededOnBalanceVolume()),
        ("SeededOnBalanceVolume(Zero)", () => new SeededOnBalanceVolume(ObvSeed.Zero)),
        ("SeededOnBalanceVolume(FirstVolume, 1)", () => new SeededOnBalanceVolume(ObvSeed.FirstVolume, 1)),
        ("SeededOnBalanceVolume(Zero, 10)", () => new SeededOnBalanceVolume(ObvSeed.Zero, 10)),
        ("SeededOnBalanceVolume(FirstVolume, 300)", () => new SeededOnBalanceVolume(ObvSeed.FirstVolume, 300)),
        ("KlingerVolumeWithSignal()", () => new KlingerVolumeWithSignal()),
        ("KlingerVolumeWithSignal(3, 4, 1)", () => new KlingerVolumeWithSignal(3, 4, 1)),
        ("KlingerVolumeWithSignal(5, 20, 3)", () => new KlingerVolumeWithSignal(5, 20, 3)),
        ("KlingerVolumeWithSignal(34, 300, 13)", () => new KlingerVolumeWithSignal(34, 300, 13)),
        ("WindowUlcerIndex()", () => new WindowUlcerIndex()),
        ("WindowUlcerIndex(1)", () => new WindowUlcerIndex(1)),
        ("WindowUlcerIndex(2)", () => new WindowUlcerIndex(2)),
        ("WindowUlcerIndex(300)", () => new WindowUlcerIndex(300)),
        ("ClassicStochasticRsi()", () => new ClassicStochasticRsi()),
        ("ClassicStochasticRsi(2, 1, 1)", () => new ClassicStochasticRsi(2, 1, 1)),
        ("ClassicStochasticRsi(14, 14, 3, Ema)", () => new ClassicStochasticRsi(14, 14, 3, ClassicAverageMethod.Ema)),
        ("ClassicStochasticRsi(9, 5, 3, Ema, true)", () => new ClassicStochasticRsi(9, 5, 3, ClassicAverageMethod.Ema, true)),
        ("ClassicStochasticRsi(14, 9, 5, Wma, false, 7, 2)", () => new ClassicStochasticRsi(14, 9, 5, ClassicAverageMethod.Wma, false, 7, 2)),
        ("ClassicStochasticRsi(300, 20, 10)", () => new ClassicStochasticRsi(300, 20, 10)),
        ("ZeroSeedLaguerreFilter()", () => new ZeroSeedLaguerreFilter()),
        ("ZeroSeedLaguerreFilter(0)", () => new ZeroSeedLaguerreFilter(0)),
        ("ZeroSeedLaguerreFilter(1)", () => new ZeroSeedLaguerreFilter(1)),
        ("ZeroSeedLaguerreFilter(0.8)", () => new ZeroSeedLaguerreFilter(0.8)),
        ("MassNormalizedQuadrupleEma()", () => new MassNormalizedQuadrupleEma()),
        ("MassNormalizedQuadrupleEma(1, 1, 1, 1)", () => new MassNormalizedQuadrupleEma(1, 1, 1, 1)),
        ("MassNormalizedQuadrupleEma(0.5, 0.1, 0.9, 0.02)", () => new MassNormalizedQuadrupleEma(0.5, 0.1, 0.9, 0.02)),
        ("MassNormalizedQuadrupleEma(0.01, 0.01, 0.01, 0.01)", () => new MassNormalizedQuadrupleEma(0.01, 0.01, 0.01, 0.01)),
        ("WindowUltimateOscillator()", () => new WindowUltimateOscillator()),
        ("WindowUltimateOscillator(1, 2, 3)", () => new WindowUltimateOscillator(1, 2, 3)),
        ("WindowUltimateOscillator(7, 14, 28, true)", () => new WindowUltimateOscillator(7, 14, 28, true)),
        ("WindowUltimateOscillator(5, 5, 9, true)", () => new WindowUltimateOscillator(5, 5, 9, true)),
        ("WindowUltimateOscillator(10, 100, 300)", () => new WindowUltimateOscillator(10, 100, 300)),
        ("SumSeededDirectionalIndex()", () => new SumSeededDirectionalIndex()),
        ("SumSeededDirectionalIndex(2)", () => new SumSeededDirectionalIndex(2)),
        ("SumSeededDirectionalIndex(5)", () => new SumSeededDirectionalIndex(5)),
        ("SumSeededDirectionalIndex(300)", () => new SumSeededDirectionalIndex(300)),
        ("QuarticDynamicAverage()", () => new QuarticDynamicAverage()),
        ("QuarticDynamicAverage(1, 1)", () => new QuarticDynamicAverage(1, 1)),
        ("QuarticDynamicAverage(10, 0.6, ResetDelay)", () => new QuarticDynamicAverage(10, 0.6, McGinleyStartup.ResetDelay)),
        ("QuarticDynamicAverage(40, 2, ResetDelay)", () => new QuarticDynamicAverage(40, 2, McGinleyStartup.ResetDelay)),
        ("QuarticDynamicAverage(300, 0.6)", () => new QuarticDynamicAverage(300, 0.6)),
        ("FilteredDeviationAverage()", () => new FilteredDeviationAverage()),
        ("FilteredDeviationAverage(1, 1)", () => new FilteredDeviationAverage(1, 1)),
        ("FilteredDeviationAverage(2, 0.5)", () => new FilteredDeviationAverage(2, 0.5)),
        ("FilteredDeviationAverage(40, 0.1)", () => new FilteredDeviationAverage(40, 0.1)),
        ("FilteredDeviationAverage(300, 0.9)", () => new FilteredDeviationAverage(300, 0.9)),
        ("PriceRelativeStrength()", () => new PriceRelativeStrength()),
        ("PriceRelativeStrength(1, 1)", () => new PriceRelativeStrength(1, 1)),
        ("PriceRelativeStrength(14, 10)", () => new PriceRelativeStrength(14, 10)),
        ("PriceRelativeStrength(null, 20)", () => new PriceRelativeStrength(null, 20)),
        ("PriceRelativeStrength(60, null)", () => new PriceRelativeStrength(60, null)),
        ("PriceRelativeStrength(5, 300, High, Low)", () => new PriceRelativeStrength(5, 300, CandlePriceField.High, CandlePriceField.Low)),
        ("RangeFractalAverage()", () => new RangeFractalAverage()),
        ("RangeFractalAverage(2)", () => new RangeFractalAverage(2)),
        ("RangeFractalAverage(3)", () => new RangeFractalAverage(3)),
        ("RangeFractalAverage(16)", () => new RangeFractalAverage(16)),
        ("RangeFractalAverage(301)", () => new RangeFractalAverage(301)),
        ("WindowStochasticKdj()", () => new WindowStochasticKdj()),
        ("WindowStochasticKdj(1, 1, 1)", () => new WindowStochasticKdj(1, 1, 1)),
        ("WindowStochasticKdj(9, 3, 3, true)", () => new WindowStochasticKdj(9, 3, 3, true)),
        ("WindowStochasticKdj(5, 1, 4, false, 2.5, 1.5)", () => new WindowStochasticKdj(5, 1, 4, false, 2.5, 1.5)),
        ("WindowStochasticKdj(300, 20, 10)", () => new WindowStochasticKdj(300, 20, 10)),
        ("WindowStochasticKdj(40, 7, 1, true, 3, 2)", () => new WindowStochasticKdj(40, 7, 1, true, 3, 2)),
        ("WindowRegressionStatistics()", () => new WindowRegressionStatistics()),
        ("WindowRegressionStatistics(2)", () => new WindowRegressionStatistics(2)),
        ("WindowRegressionStatistics(3)", () => new WindowRegressionStatistics(3)),
        ("WindowRegressionStatistics(50)", () => new WindowRegressionStatistics(50)),
        ("WindowRegressionStatistics(300)", () => new WindowRegressionStatistics(300)),
        ("RegressionWithFinalLine()", () => new RegressionWithFinalLine()),
        ("RegressionWithFinalLine(2)", () => new RegressionWithFinalLine(2)),
        ("RegressionWithFinalLine(3)", () => new RegressionWithFinalLine(3)),
        ("RegressionWithFinalLine(50)", () => new RegressionWithFinalLine(50)),
        ("RegressionWithFinalLine(300)", () => new RegressionWithFinalLine(300)),
        ("SeededPhaseAdaptiveAverage()", () => new SeededPhaseAdaptiveAverage()),
        ("SeededPhaseAdaptiveAverage(0.5, 0.05, true)", () => new SeededPhaseAdaptiveAverage(0.5, 0.05, true)),
        ("SeededPhaseAdaptiveAverage(0.8, 0.1, false, true)", () => new SeededPhaseAdaptiveAverage(0.8, 0.1, false, true)),
        ("SeededPhaseAdaptiveAverage(0.5, 0.05, false, false, true)", () => new SeededPhaseAdaptiveAverage(0.5, 0.05, false, false, true)),
        ("SeededPhaseAdaptiveAverage(1, 1, true, true, true)", () => new SeededPhaseAdaptiveAverage(1, 1, true, true, true)),
        ("DelayedPhaseAdaptiveAverage()", () => new DelayedPhaseAdaptiveAverage()),
        ("DelayedPhaseAdaptiveAverage(0.99, 0.01)", () => new DelayedPhaseAdaptiveAverage(0.99, 0.01)),
        ("DelayedPhaseAdaptiveAverage(0.3, 0.2, 40)", () => new DelayedPhaseAdaptiveAverage(0.3, 0.2, 40)),
        ("HilbertCyclePeriod()", () => new HilbertCyclePeriod()),
        ("HilbertCyclePeriod(31)", () => new HilbertCyclePeriod(31)),
        ("HilbertPhasor()", () => new HilbertPhasor()),
        ("HilbertPhasor(31)", () => new HilbertPhasor(31)),
        ("SeededHilbertTrendline()", () => new SeededHilbertTrendline()),
        ("SeededHilbertTrendline(false)", () => new SeededHilbertTrendline(false)),
        ("DelayedHilbertTrendline()", () => new DelayedHilbertTrendline()),
        ("DelayedHilbertTrendline(25)", () => new DelayedHilbertTrendline(25)),
        ("DelayedHilbertPhase()", () => new DelayedHilbertPhase()),
        ("DelayedHilbertPhase(17)", () => new DelayedHilbertPhase(17)),
        ("DelayedHilbertSine()", () => new DelayedHilbertSine()),
        ("DelayedHilbertSine(17)", () => new DelayedHilbertSine(17)),
        ("DelayedHilbertTrendMode()", () => new DelayedHilbertTrendMode()),
        ("DelayedHilbertTrendMode(17)", () => new DelayedHilbertTrendMode(17)),
        ("MarubozuCandle()", () => new MarubozuCandle()),
        ("MarubozuCandle(1)", () => new MarubozuCandle(1)),
        ("MarubozuCandle(300)", () => new MarubozuCandle(300)),
        ("ClosingMarubozuCandle()", () => new ClosingMarubozuCandle()),
        ("ClosingMarubozuCandle(1)", () => new ClosingMarubozuCandle(1)),
        ("ClosingMarubozuCandle(300)", () => new ClosingMarubozuCandle(300)),
        ("SpinningTopCandle()", () => new SpinningTopCandle()),
        ("SpinningTopCandle(1)", () => new SpinningTopCandle(1)),
        ("SpinningTopCandle(300)", () => new SpinningTopCandle(300)),
        ("HighWaveCandle()", () => new HighWaveCandle()),
        ("HighWaveCandle(1)", () => new HighWaveCandle(1)),
        ("HighWaveCandle(300)", () => new HighWaveCandle(300)),
        ("LongLineCandle()", () => new LongLineCandle()),
        ("LongLineCandle(1)", () => new LongLineCandle(1)),
        ("LongLineCandle(300)", () => new LongLineCandle(300)),
        ("ShortLineCandle()", () => new ShortLineCandle()),
        ("ShortLineCandle(1)", () => new ShortLineCandle(1)),
        ("ShortLineCandle(300)", () => new ShortLineCandle(300)),
        ("BeltHoldCandle()", () => new BeltHoldCandle()),
        ("BeltHoldCandle(1)", () => new BeltHoldCandle(1)),
        ("BeltHoldCandle(300)", () => new BeltHoldCandle(300)),
        ("HammerCandle()", () => new HammerCandle()),
        ("HammerCandle(1, 1)", () => new HammerCandle(1, 1)),
        ("HammerCandle(3, 40)", () => new HammerCandle(3, 40)),
        ("HammerCandle(300, 5)", () => new HammerCandle(300, 5)),
        ("HangingManCandle()", () => new HangingManCandle()),
        ("HangingManCandle(1, 1)", () => new HangingManCandle(1, 1)),
        ("HangingManCandle(3, 40)", () => new HangingManCandle(3, 40)),
        ("HangingManCandle(300, 5)", () => new HangingManCandle(300, 5)),
        ("InvertedHammerCandle()", () => new InvertedHammerCandle()),
        ("InvertedHammerCandle(1)", () => new InvertedHammerCandle(1)),
        ("InvertedHammerCandle(300)", () => new InvertedHammerCandle(300)),
        ("ShootingStarCandle()", () => new ShootingStarCandle()),
        ("ShootingStarCandle(1)", () => new ShootingStarCandle(1)),
        ("ShootingStarCandle(300)", () => new ShootingStarCandle(300)),
        ("HikkakeCandle()", () => new HikkakeCandle()),
        ("ModifiedHikkakeCandle()", () => new ModifiedHikkakeCandle()),
        ("ModifiedHikkakeCandle(1)", () => new ModifiedHikkakeCandle(1)),
        ("ModifiedHikkakeCandle(300)", () => new ModifiedHikkakeCandle(300)),
        ("MatchingLowCandle()", () => new MatchingLowCandle()),
        ("MatchingLowCandle(1)", () => new MatchingLowCandle(1)),
        ("MatchingLowCandle(300)", () => new MatchingLowCandle(300)),
        ("StickSandwichCandle()", () => new StickSandwichCandle()),
        ("StickSandwichCandle(1)", () => new StickSandwichCandle(1)),
        ("StickSandwichCandle(300)", () => new StickSandwichCandle(300)),
        ("ThreeLineStrikeCandle()", () => new ThreeLineStrikeCandle()),
        ("ThreeLineStrikeCandle(1)", () => new ThreeLineStrikeCandle(1)),
        ("ThreeLineStrikeCandle(300)", () => new ThreeLineStrikeCandle(300)),
        ("ThreeOutsideCandle()", () => new ThreeOutsideCandle()),
        ("EngulfingPattern()", () => new EngulfingPattern()),
        ("WindowReturnBeta()", () => new WindowReturnBeta()),
        ("WindowReturnBeta(2)", () => new WindowReturnBeta(2)),
        ("WindowReturnBeta(5, High, Low)", () => new WindowReturnBeta(5, CandlePriceField.High, CandlePriceField.Low)),
        ("WindowReturnBeta(300, Open, Close)", () => new WindowReturnBeta(300, CandlePriceField.Open, CandlePriceField.Close)),
        ("WindowBetaStatistics()", () => new WindowBetaStatistics()),
        ("WindowBetaStatistics(2, All)", () => new WindowBetaStatistics(2, ReturnBetaSelection.All)),
        ("WindowBetaStatistics(20, All)", () => new WindowBetaStatistics(20, ReturnBetaSelection.All)),
        ("WindowBetaStatistics(10, Up)", () => new WindowBetaStatistics(10, ReturnBetaSelection.Up)),
        ("WindowBetaStatistics(10, Down, High, Low)", () => new WindowBetaStatistics(10, ReturnBetaSelection.Down, CandlePriceField.High, CandlePriceField.Low)),
        ("WindowBetaStatistics(300, All)", () => new WindowBetaStatistics(300, ReturnBetaSelection.All)),
        ("TristarCandle()", () => new TristarCandle()),
        ("TristarCandle(1)", () => new TristarCandle(1)),
        ("TristarCandle(300)", () => new TristarCandle(300)),
        ("TasukiGapCandle()", () => new TasukiGapCandle()),
        ("TasukiGapCandle(1)", () => new TasukiGapCandle(1)),
        ("TasukiGapCandle(300)", () => new TasukiGapCandle(300)),
        ("UpDownSideGapThreeMethodsCandle()", () => new UpDownSideGapThreeMethodsCandle()),
        ("HaramiCandle()", () => new HaramiCandle()),
        ("HaramiCandle(1)", () => new HaramiCandle(1)),
        ("HaramiCandle(300)", () => new HaramiCandle(300)),
        ("HaramiCrossCandle()", () => new HaramiCrossCandle()),
        ("HaramiCrossCandle(1)", () => new HaramiCrossCandle(1)),
        ("HaramiCrossCandle(300)", () => new HaramiCrossCandle(300)),
        ("HomingPigeonCandle()", () => new HomingPigeonCandle()),
        ("HomingPigeonCandle(1)", () => new HomingPigeonCandle(1)),
        ("HomingPigeonCandle(300)", () => new HomingPigeonCandle(300)),
        ("DojiStarCandle()", () => new DojiStarCandle()),
        ("DojiStarCandle(1)", () => new DojiStarCandle(1)),
        ("DojiStarCandle(300)", () => new DojiStarCandle(300)),
        ("PiercingLineCandle()", () => new PiercingLineCandle()),
        ("PiercingLineCandle(1)", () => new PiercingLineCandle(1)),
        ("PiercingLineCandle(300)", () => new PiercingLineCandle(300)),
        ("DarkCloudCoverCandle()", () => new DarkCloudCoverCandle()),
        ("DarkCloudCoverCandle(1, 0.1)", () => new DarkCloudCoverCandle(1, 0.1)),
        ("DarkCloudCoverCandle(300, 0.9)", () => new DarkCloudCoverCandle(300, 0.9)),
        ("DarkCloudCoverCandle(10, 0)", () => new DarkCloudCoverCandle(10, 0)),
        ("OnNeckCandle()", () => new OnNeckCandle()),
        ("OnNeckCandle(1, 1)", () => new OnNeckCandle(1, 1)),
        ("OnNeckCandle(3, 300)", () => new OnNeckCandle(3, 300)),
        ("OnNeckCandle(300, 2)", () => new OnNeckCandle(300, 2)),
        ("InNeckCandle()", () => new InNeckCandle()),
        ("InNeckCandle(1, 1)", () => new InNeckCandle(1, 1)),
        ("InNeckCandle(3, 300)", () => new InNeckCandle(3, 300)),
        ("InNeckCandle(300, 2)", () => new InNeckCandle(300, 2)),
        ("ThrustingCandle()", () => new ThrustingCandle()),
        ("ThrustingCandle(1, 1)", () => new ThrustingCandle(1, 1)),
        ("ThrustingCandle(3, 300)", () => new ThrustingCandle(3, 300)),
        ("ThrustingCandle(300, 2)", () => new ThrustingCandle(300, 2)),
        ("AverageRangeDoji()", () => new AverageRangeDoji()),
        ("AverageRangeDoji(1)", () => new AverageRangeDoji(1)),
        ("AverageRangeDoji(300)", () => new AverageRangeDoji(300)),
        ("AverageRangeDragonflyDoji()", () => new AverageRangeDragonflyDoji()),
        ("AverageRangeDragonflyDoji(1)", () => new AverageRangeDragonflyDoji(1)),
        ("AverageRangeDragonflyDoji(300)", () => new AverageRangeDragonflyDoji(300)),
        ("AverageRangeGravestoneDoji()", () => new AverageRangeGravestoneDoji()),
        ("AverageRangeGravestoneDoji(1)", () => new AverageRangeGravestoneDoji(1)),
        ("AverageRangeGravestoneDoji(300)", () => new AverageRangeGravestoneDoji(300)),
        ("AverageRangeLongLeggedDoji()", () => new AverageRangeLongLeggedDoji()),
        ("AverageRangeLongLeggedDoji(1)", () => new AverageRangeLongLeggedDoji(1)),
        ("AverageRangeLongLeggedDoji(300)", () => new AverageRangeLongLeggedDoji(300)),
        ("TakuriLineCandle()", () => new TakuriLineCandle()),
        ("TakuriLineCandle(1)", () => new TakuriLineCandle(1)),
        ("TakuriLineCandle(300)", () => new TakuriLineCandle(300)),
        ("ThreeInsideCandle()", () => new ThreeInsideCandle()),
        ("ThreeInsideCandle(1, 1)", () => new ThreeInsideCandle(1, 1)),
        ("ThreeInsideCandle(3, 300)", () => new ThreeInsideCandle(3, 300)),
        ("ThreeInsideCandle(300, 2)", () => new ThreeInsideCandle(300, 2)),
        ("TwoCrowsCandle()", () => new TwoCrowsCandle()),
        ("TwoCrowsCandle(1)", () => new TwoCrowsCandle(1)),
        ("TwoCrowsCandle(300)", () => new TwoCrowsCandle(300)),
        ("UpsideGapTwoCrowsCandle()", () => new UpsideGapTwoCrowsCandle()),
        ("UpsideGapTwoCrowsCandle(1, 1)", () => new UpsideGapTwoCrowsCandle(1, 1)),
        ("UpsideGapTwoCrowsCandle(3, 300)", () => new UpsideGapTwoCrowsCandle(3, 300)),
        ("UpsideGapTwoCrowsCandle(300, 2)", () => new UpsideGapTwoCrowsCandle(300, 2)),
        ("UniqueThreeRiverCandle()", () => new UniqueThreeRiverCandle()),
        ("UniqueThreeRiverCandle(1, 1)", () => new UniqueThreeRiverCandle(1, 1)),
        ("UniqueThreeRiverCandle(3, 300)", () => new UniqueThreeRiverCandle(3, 300)),
        ("UniqueThreeRiverCandle(300, 2)", () => new UniqueThreeRiverCandle(300, 2)),
        ("CounterattackCandle()", () => new CounterattackCandle()),
        ("CounterattackCandle(1, 1)", () => new CounterattackCandle(1, 1)),
        ("CounterattackCandle(3, 300)", () => new CounterattackCandle(3, 300)),
        ("CounterattackCandle(300, 2)", () => new CounterattackCandle(300, 2)),
        ("SeparatingLinesCandle()", () => new SeparatingLinesCandle()),
        ("SeparatingLinesCandle(1, 1, 1)", () => new SeparatingLinesCandle(1, 1, 1)),
        ("SeparatingLinesCandle(3, 300, 7)", () => new SeparatingLinesCandle(3, 300, 7)),
        ("SeparatingLinesCandle(300, 2, 40)", () => new SeparatingLinesCandle(300, 2, 40)),
        ("GapSideBySideWhiteLinesCandle()", () => new GapSideBySideWhiteLinesCandle()),
        ("GapSideBySideWhiteLinesCandle(1, 1)", () => new GapSideBySideWhiteLinesCandle(1, 1)),
        ("GapSideBySideWhiteLinesCandle(3, 300)", () => new GapSideBySideWhiteLinesCandle(3, 300)),
        ("GapSideBySideWhiteLinesCandle(300, 2)", () => new GapSideBySideWhiteLinesCandle(300, 2)),
        ("StrictHaramiCandle()", () => new StrictHaramiCandle()),
        ("StrictHaramiCandle(true)", () => new StrictHaramiCandle(true)),
        ("BullishHaramiPattern()", () => new BullishHaramiPattern()),
        ("BullishHaramiPattern(1, true)", () => new BullishHaramiPattern(1, true)),
        ("BullishHaramiPattern(2)", () => new BullishHaramiPattern(2)),
        ("BullishHaramiPattern(300)", () => new BullishHaramiPattern(300)),
        ("BearishHaramiPattern()", () => new BearishHaramiPattern()),
        ("BearishHaramiPattern(1, true)", () => new BearishHaramiPattern(1, true)),
        ("BearishHaramiPattern(2)", () => new BearishHaramiPattern(2)),
        ("BearishHaramiPattern(300)", () => new BearishHaramiPattern(300)),
        ("ThreeBlackCrowsCandle()", () => new ThreeBlackCrowsCandle()),
        ("ThreeBlackCrowsCandle(1)", () => new ThreeBlackCrowsCandle(1)),
        ("ThreeBlackCrowsCandle(300)", () => new ThreeBlackCrowsCandle(300)),
        ("IdenticalThreeCrowsCandle()", () => new IdenticalThreeCrowsCandle()),
        ("IdenticalThreeCrowsCandle(1, 1)", () => new IdenticalThreeCrowsCandle(1, 1)),
        ("IdenticalThreeCrowsCandle(3, 300)", () => new IdenticalThreeCrowsCandle(3, 300)),
        ("IdenticalThreeCrowsCandle(300, 2)", () => new IdenticalThreeCrowsCandle(300, 2)),
        ("ThreeWhiteSoldiersCandle()", () => new ThreeWhiteSoldiersCandle()),
        ("ThreeWhiteSoldiersCandle(1, 1, 1, 1)", () => new ThreeWhiteSoldiersCandle(1, 1, 1, 1)),
        ("ThreeWhiteSoldiersCandle(3, 300, 7, 2)", () => new ThreeWhiteSoldiersCandle(3, 300, 7, 2)),
        ("ThreeWhiteSoldiersCandle(300, 2, 40, 300)", () => new ThreeWhiteSoldiersCandle(300, 2, 40, 300)),
        ("ThreeWhiteSoldiersCandle(2, 4, 300, 6)", () => new ThreeWhiteSoldiersCandle(2, 4, 300, 6)),
        ("AdvanceBlockCandle()", () => new AdvanceBlockCandle()),
        ("AdvanceBlockCandle(1, 1, 1, 1)", () => new AdvanceBlockCandle(1, 1, 1, 1)),
        ("AdvanceBlockCandle(3, 300, 7, 2)", () => new AdvanceBlockCandle(3, 300, 7, 2)),
        ("AdvanceBlockCandle(300, 2, 40, 300)", () => new AdvanceBlockCandle(300, 2, 40, 300)),
        ("AdvanceBlockCandle(2, 4, 300, 6)", () => new AdvanceBlockCandle(2, 4, 300, 6)),
        ("StalledCandle()", () => new StalledCandle()),
        ("StalledCandle(1, 1, 1, 1)", () => new StalledCandle(1, 1, 1, 1)),
        ("StalledCandle(3, 300, 7, 2)", () => new StalledCandle(3, 300, 7, 2)),
        ("StalledCandle(300, 2, 40, 300)", () => new StalledCandle(300, 2, 40, 300)),
        ("StalledCandle(2, 4, 300, 6)", () => new StalledCandle(2, 4, 300, 6)),
        ("ThreeStarsInSouthCandle()", () => new ThreeStarsInSouthCandle()),
        ("ThreeStarsInSouthCandle(1, 1, 1)", () => new ThreeStarsInSouthCandle(1, 1, 1)),
        ("ThreeStarsInSouthCandle(3, 300, 7)", () => new ThreeStarsInSouthCandle(3, 300, 7)),
        ("ThreeStarsInSouthCandle(300, 2, 40)", () => new ThreeStarsInSouthCandle(300, 2, 40)),
        ("KickingCandle()", () => new KickingCandle()),
        ("KickingCandle(1, 1)", () => new KickingCandle(1, 1)),
        ("KickingCandle(3, 300)", () => new KickingCandle(3, 300)),
        ("KickingCandle(300, 2)", () => new KickingCandle(300, 2)),
        ("KickingByLengthCandle()", () => new KickingByLengthCandle()),
        ("KickingByLengthCandle(1, 1)", () => new KickingByLengthCandle(1, 1)),
        ("KickingByLengthCandle(3, 300)", () => new KickingByLengthCandle(3, 300)),
        ("KickingByLengthCandle(300, 2)", () => new KickingByLengthCandle(300, 2)),
        ("MorningStarCandle()", () => new MorningStarCandle()),
        ("MorningStarCandle(1, 1, 0)", () => new MorningStarCandle(1, 1, 0)),
        ("MorningStarCandle(3, 300, 0.9)", () => new MorningStarCandle(3, 300, 0.9)),
        ("MorningStarCandle(300, 2, 0.1)", () => new MorningStarCandle(300, 2, 0.1)),
        ("EveningStarCandle()", () => new EveningStarCandle()),
        ("EveningStarCandle(1, 1, 0)", () => new EveningStarCandle(1, 1, 0)),
        ("EveningStarCandle(3, 300, 0.9)", () => new EveningStarCandle(3, 300, 0.9)),
        ("EveningStarCandle(300, 2, 0.1)", () => new EveningStarCandle(300, 2, 0.1)),
        ("MorningDojiStarCandle()", () => new MorningDojiStarCandle()),
        ("MorningDojiStarCandle(1, 1, 1, 0)", () => new MorningDojiStarCandle(1, 1, 1, 0)),
        ("MorningDojiStarCandle(3, 300, 7, 0.9)", () => new MorningDojiStarCandle(3, 300, 7, 0.9)),
        ("MorningDojiStarCandle(300, 2, 40, 0.1)", () => new MorningDojiStarCandle(300, 2, 40, 0.1)),
        ("EveningDojiStarCandle()", () => new EveningDojiStarCandle()),
        ("EveningDojiStarCandle(1, 1, 1, 0)", () => new EveningDojiStarCandle(1, 1, 1, 0)),
        ("EveningDojiStarCandle(3, 300, 7, 0.9)", () => new EveningDojiStarCandle(3, 300, 7, 0.9)),
        ("EveningDojiStarCandle(300, 2, 40, 0.1)", () => new EveningDojiStarCandle(300, 2, 40, 0.1)),
        ("AbandonedBabyCandle()", () => new AbandonedBabyCandle()),
        ("AbandonedBabyCandle(1, 1, 1, 0)", () => new AbandonedBabyCandle(1, 1, 1, 0)),
        ("AbandonedBabyCandle(3, 300, 7, 0.9)", () => new AbandonedBabyCandle(3, 300, 7, 0.9)),
        ("AbandonedBabyCandle(300, 2, 40, 0.1)", () => new AbandonedBabyCandle(300, 2, 40, 0.1)),
        ("BreakawayCandle()", () => new BreakawayCandle()),
        ("BreakawayCandle(1)", () => new BreakawayCandle(1)),
        ("BreakawayCandle(300)", () => new BreakawayCandle(300)),
        ("ConcealingBabySwallowCandle()", () => new ConcealingBabySwallowCandle()),
        ("ConcealingBabySwallowCandle(1)", () => new ConcealingBabySwallowCandle(1)),
        ("ConcealingBabySwallowCandle(300)", () => new ConcealingBabySwallowCandle(300)),
        ("LadderBottomCandle()", () => new LadderBottomCandle()),
        ("LadderBottomCandle(1)", () => new LadderBottomCandle(1)),
        ("LadderBottomCandle(300)", () => new LadderBottomCandle(300)),
        ("RateOfChangeWithAverage()", () => new RateOfChangeWithAverage()),
        ("RateOfChangeWithAverage(1, 1)", () => new RateOfChangeWithAverage(1, 1)),
        ("RateOfChangeWithAverage(20, 5)", () => new RateOfChangeWithAverage(20, 5)),
        ("RateOfChangeWithAverage(3, 300)", () => new RateOfChangeWithAverage(3, 300)),
        ("RateOfChangeWithAverage(300, 2)", () => new RateOfChangeWithAverage(300, 2)),
        ("BalanceOfPowerWithValidity()", () => new BalanceOfPowerWithValidity()),
        ("BalanceOfPowerWithValidity(1)", () => new BalanceOfPowerWithValidity(1)),
        ("BalanceOfPowerWithValidity(2)", () => new BalanceOfPowerWithValidity(2)),
        ("BalanceOfPowerWithValidity(300)", () => new BalanceOfPowerWithValidity(300)),
        ("AverageTrueRangeWithDetails()", () => new AverageTrueRangeWithDetails()),
        ("AverageTrueRangeWithDetails(1)", () => new AverageTrueRangeWithDetails(1)),
        ("AverageTrueRangeWithDetails(2)", () => new AverageTrueRangeWithDetails(2)),
        ("AverageTrueRangeWithDetails(300)", () => new AverageTrueRangeWithDetails(300)),
        ("AccumulationDistributionWithAverage()", () => new AccumulationDistributionWithAverage()),
        ("AccumulationDistributionWithAverage(1)", () => new AccumulationDistributionWithAverage(1)),
        ("AccumulationDistributionWithAverage(20)", () => new AccumulationDistributionWithAverage(20)),
        ("AccumulationDistributionWithAverage(300)", () => new AccumulationDistributionWithAverage(300)),
        ("AwesomeWithDetails()", () => new AwesomeWithDetails()),
        ("AwesomeWithDetails(1, 2)", () => new AwesomeWithDetails(1, 2)),
        ("AwesomeWithDetails(3, 300)", () => new AwesomeWithDetails(3, 300)),
        ("AwesomeWithDetails(299, 300)", () => new AwesomeWithDetails(299, 300)),
        ("AlligatorWithDetails()", () => new AlligatorWithDetails()),
        ("AlligatorWithDetails(3, 2, 2, 1, 1, 1)", () => new AlligatorWithDetails(3, 2, 2, 1, 1, 1)),
        ("AlligatorWithDetails(300, 8, 40, 5, 5, 3)", () => new AlligatorWithDetails(300, 8, 40, 5, 5, 3)),
        ("AlligatorWithDetails(13, 300, 8, 150, 5, 3)", () => new AlligatorWithDetails(13, 300, 8, 150, 5, 3)),
        ("ElderRayWithDetails()", () => new ElderRayWithDetails()),
        ("ElderRayWithDetails(1)", () => new ElderRayWithDetails(1)),
        ("ElderRayWithDetails(2)", () => new ElderRayWithDetails(2)),
        ("ElderRayWithDetails(300)", () => new ElderRayWithDetails(300)),
        ("WindowVortex()", () => new WindowVortex()),
        ("WindowVortex(1)", () => new WindowVortex(1)),
        ("WindowVortex(2)", () => new WindowVortex(2)),
        ("WindowVortex(300)", () => new WindowVortex(300)),
        ("WindowChoppinessIndex()", () => new WindowChoppinessIndex()),
        ("WindowChoppinessIndex(2)", () => new WindowChoppinessIndex(2)),
        ("WindowChoppinessIndex(40)", () => new WindowChoppinessIndex(40)),
        ("WindowChoppinessIndex(300)", () => new WindowChoppinessIndex(300)),
        ("WindowChandelierExit()", () => new WindowChandelierExit()),
        ("WindowChandelierExit(1, 0)", () => new WindowChandelierExit(1, 0)),
        ("WindowChandelierExit(2, -1.5, Long)", () => new WindowChandelierExit(2, -1.5, ChandelierExitSelection.Long)),
        ("WindowChandelierExit(40, 2.25, Short)", () => new WindowChandelierExit(40, 2.25, ChandelierExitSelection.Short)),
        ("WindowChandelierExit(300, 3, Both, true)", () => new WindowChandelierExit(300, 3, ChandelierExitSelection.Both, true)),
        ("WindowFisherTransform()", () => new WindowFisherTransform()),
        ("WindowFisherTransform(1)", () => new WindowFisherTransform(1)),
        ("WindowFisherTransform(2, false)", () => new WindowFisherTransform(2, false)),
        ("WindowFisherTransform(40, false)", () => new WindowFisherTransform(40, false)),
        ("WindowFisherTransform(300)", () => new WindowFisherTransform(300)),
        ("MoneyFlowWithDetails()", () => new MoneyFlowWithDetails()),
        ("MoneyFlowWithDetails(1)", () => new MoneyFlowWithDetails(1)),
        ("MoneyFlowWithDetails(2)", () => new MoneyFlowWithDetails(2)),
        ("MoneyFlowWithDetails(300)", () => new MoneyFlowWithDetails(300)),
        ("StandardDeviationWithDetails()", () => new StandardDeviationWithDetails()),
        ("StandardDeviationWithDetails(2, 1)", () => new StandardDeviationWithDetails(2, 1)),
        ("StandardDeviationWithDetails(20, 5)", () => new StandardDeviationWithDetails(20, 5)),
        ("StandardDeviationWithDetails(3, 300)", () => new StandardDeviationWithDetails(3, 300)),
        ("StandardDeviationWithDetails(300, 2)", () => new StandardDeviationWithDetails(300, 2)),
        ("GatorWithDetails()", () => new GatorWithDetails()),
        ("GatorWithDetails(3, 2, 2, 1, 1, 1)", () => new GatorWithDetails(3, 2, 2, 1, 1, 1)),
        ("GatorWithDetails(300, 8, 40, 5, 5, 3)", () => new GatorWithDetails(300, 8, 40, 5, 5, 3)),
        ("GatorWithDetails(13, 300, 8, 150, 5, 3)", () => new GatorWithDetails(13, 300, 8, 150, 5, 3)),
        ("WindowAverageEnvelope()", () => new WindowAverageEnvelope()),
        ("WindowAverageEnvelope(1, 0.5)", () => new WindowAverageEnvelope(1, 0.5)),
        ("WindowAverageEnvelope(300, 10)", () => new WindowAverageEnvelope(300, 10)),
        ("WindowAverageEnvelope(14, 2.5, Ema)", () => new WindowAverageEnvelope(14, 2.5, EnvelopeAverage.Ema)),
        ("WindowAverageEnvelope(1, 2.5, Ema)", () => new WindowAverageEnvelope(1, 2.5, EnvelopeAverage.Ema)),
        ("WindowAverageEnvelope(300, 2.5, Ema)", () => new WindowAverageEnvelope(300, 2.5, EnvelopeAverage.Ema)),
        ("WindowAverageEnvelope(14, 2.5, Alma)", () => new WindowAverageEnvelope(14, 2.5, EnvelopeAverage.Alma)),
        ("WindowAverageEnvelope(2, 2.5, Alma)", () => new WindowAverageEnvelope(2, 2.5, EnvelopeAverage.Alma)),
        ("WindowAverageEnvelope(14, 2.5, Dema)", () => new WindowAverageEnvelope(14, 2.5, EnvelopeAverage.Dema)),
        ("WindowAverageEnvelope(40, 2.5, Tema)", () => new WindowAverageEnvelope(40, 2.5, EnvelopeAverage.Tema)),
        ("WindowAverageEnvelope(14, 2.5, Smma)", () => new WindowAverageEnvelope(14, 2.5, EnvelopeAverage.Smma)),
        ("WindowAverageEnvelope(1, 2.5, Smma)", () => new WindowAverageEnvelope(1, 2.5, EnvelopeAverage.Smma)),
        ("WindowAverageEnvelope(14, 2.5, Epma)", () => new WindowAverageEnvelope(14, 2.5, EnvelopeAverage.Epma)),
        ("WindowAverageEnvelope(2, 2.5, Epma)", () => new WindowAverageEnvelope(2, 2.5, EnvelopeAverage.Epma)),
        ("WindowAverageEnvelope(14, 2.5, Wma)", () => new WindowAverageEnvelope(14, 2.5, EnvelopeAverage.Wma)),
        ("WindowAverageEnvelope(16, 2.5, Hma)", () => new WindowAverageEnvelope(16, 2.5, EnvelopeAverage.Hma)),
        ("WindowSampleShape()", () => new WindowSampleShape()),
        ("WindowSampleShape(3)", () => new WindowSampleShape(3)),
        ("WindowSampleShape(300)", () => new WindowSampleShape(300)),
        ("WindowSampleShape(20, true)", () => new WindowSampleShape(20, true)),
        ("WindowSampleShape(4, true)", () => new WindowSampleShape(4, true)),
        ("WindowSampleShape(300, true)", () => new WindowSampleShape(300, true)),
        ("SingleStochasticCycle()", () => new SingleStochasticCycle()),
        ("SingleStochasticCycle(1, 1, 2)", () => new SingleStochasticCycle(1, 1, 2)),
        ("SingleStochasticCycle(2, 3, 5)", () => new SingleStochasticCycle(2, 3, 5)),
        ("SingleStochasticCycle(40, 12, 26)", () => new SingleStochasticCycle(40, 12, 26)),
        ("SingleStochasticCycle(300, 23, 50)", () => new SingleStochasticCycle(300, 23, 50)),
        ("SingleStochasticCycle(10, 150, 300)", () => new SingleStochasticCycle(10, 150, 300)),
        ("WindowCorrelation()", () => new WindowCorrelation()),
        ("WindowCorrelation(1)", () => new WindowCorrelation(1)),
        ("WindowCorrelation(2, High, Low)", () => new WindowCorrelation(2, CandlePriceField.High, CandlePriceField.Low)),
        ("WindowCorrelation(20, Close, Close, true)", () => new WindowCorrelation(20, CandlePriceField.Close, CandlePriceField.Close, true)),
        ("WindowCorrelation(300, Low, Close, true)", () => new WindowCorrelation(300, CandlePriceField.Low, CandlePriceField.Close, true)),
        ("WindowCorrelation(40, Open, High)", () => new WindowCorrelation(40, CandlePriceField.Open, CandlePriceField.High)),
        ("HoltWinterForecast()", () => new HoltWinterForecast()),
        ("HoltWinterForecast(1)", () => new HoltWinterForecast(1)),
        ("HoltWinterForecast(2)", () => new HoltWinterForecast(2)),
        ("HoltWinterForecast(300)", () => new HoltWinterForecast(300)),
        ("HoltWinterForecast(0.2, 0.1, 0.1)", () => new HoltWinterForecast(0.2, 0.1, 0.1)),
        ("HoltWinterForecast(14, 0.5, 0.05, 0.02)", () => new HoltWinterForecast(14, 0.5, 0.05, 0.02)),
        ("RateOfChangeRmsBands()", () => new RateOfChangeRmsBands()),
        ("RateOfChangeRmsBands(1, 1, 1)", () => new RateOfChangeRmsBands(1, 1, 1)),
        ("RateOfChangeRmsBands(2, 5, 2)", () => new RateOfChangeRmsBands(2, 5, 2)),
        ("RateOfChangeRmsBands(300, 3, 3)", () => new RateOfChangeRmsBands(300, 3, 3)),
        ("RateOfChangeRmsBands(20, 300, 20)", () => new RateOfChangeRmsBands(20, 300, 20)),
        ("RateOfChangeRmsBands(40, 9, 1)", () => new RateOfChangeRmsBands(40, 9, 1)),
        ("HullWindow()", () => new HullWindow()),
        ("HullWindow(2)", () => new HullWindow(2)),
        ("HullWindow(300)", () => new HullWindow(300)),
        ("HullWindow(16, ExpandingFloor)", () => new HullWindow(16, HullWindowConvention.ExpandingFloor)),
        ("HullWindow(2, ExpandingFloor)", () => new HullWindow(2, HullWindowConvention.ExpandingFloor)),
        ("HullWindow(300, ExpandingFloor)", () => new HullWindow(300, HullWindowConvention.ExpandingFloor)),
        ("HullWindow(15, FullWindowRounded)", () => new HullWindow(15, HullWindowConvention.FullWindowRounded)),
        ("HullWindow(3, FullWindowRounded)", () => new HullWindow(3, HullWindowConvention.FullWindowRounded)),
        ("FixedPeriodWma()", () => new FixedPeriodWma()),
        ("FixedPeriodWma(1)", () => new FixedPeriodWma(1)),
        ("FixedPeriodWma(2)", () => new FixedPeriodWma(2)),
        ("FixedPeriodWma(300)", () => new FixedPeriodWma(300)),
        ("RelativeVolatilityStrength()", () => new RelativeVolatilityStrength()),
        ("RelativeVolatilityStrength(2)", () => new RelativeVolatilityStrength(2)),
        ("RelativeVolatilityStrength(3)", () => new RelativeVolatilityStrength(3)),
        ("RelativeVolatilityStrength(40)", () => new RelativeVolatilityStrength(40)),
        ("RelativeVolatilityStrength(300)", () => new RelativeVolatilityStrength(300)),
        ("WindowStochasticMomentum()", () => new WindowStochasticMomentum()),
        ("WindowStochasticMomentum(1)", () => new WindowStochasticMomentum(1)),
        ("WindowStochasticMomentum(300)", () => new WindowStochasticMomentum(300)),
        ("DoubleSmoothedStochasticMomentum()", () => new DoubleSmoothedStochasticMomentum()),
        ("DoubleSmoothedStochasticMomentum(1, 1, 1, 1)", () => new DoubleSmoothedStochasticMomentum(1, 1, 1, 1)),
        ("DoubleSmoothedStochasticMomentum(2, 3, 5, 2)", () => new DoubleSmoothedStochasticMomentum(2, 3, 5, 2)),
        ("DoubleSmoothedStochasticMomentum(300, 25, 2, 3)", () => new DoubleSmoothedStochasticMomentum(300, 25, 2, 3)),
        ("DoubleSmoothedStochasticMomentum(14, 300, 150, 60)", () => new DoubleSmoothedStochasticMomentum(14, 300, 150, 60)),
        ("ConnorsStrength()", () => new ConnorsStrength()),
        ("ConnorsStrength(2, 2, 2)", () => new ConnorsStrength(2, 2, 2)),
        ("ConnorsStrength(14, 5, 20)", () => new ConnorsStrength(14, 5, 20)),
        ("ConnorsStrength(300, 2, 100)", () => new ConnorsStrength(300, 2, 100)),
        ("ConnorsStrength(3, 300, 10)", () => new ConnorsStrength(3, 300, 10)),
        ("ConnorsStrength(3, 2, 300)", () => new ConnorsStrength(3, 2, 300)),
        ("WindowAtrBands()", () => new WindowAtrBands()),
        ("WindowAtrBands(1, 1)", () => new WindowAtrBands(1, 1)),
        ("WindowAtrBands(20, 10, 2, Simple)", () => new WindowAtrBands(20, 10, 2, AtrBandCenterMode.Simple)),
        ("WindowAtrBands(5, 30, 1.5, Simple, false)", () => new WindowAtrBands(5, 30, 1.5, AtrBandCenterMode.Simple, false)),
        ("WindowAtrBands(30, 5, 3, FirstSeededExponential)", () => new WindowAtrBands(30, 5, 3, AtrBandCenterMode.FirstSeededExponential)),
        ("WindowAtrBands(5, 30, -2, FirstSeededExponential)", () => new WindowAtrBands(5, 30, -2, AtrBandCenterMode.FirstSeededExponential)),
        ("WindowAtrBands(30, 5, 0.5, MeanSeededExponential, false)", () => new WindowAtrBands(30, 5, 0.5, AtrBandCenterMode.MeanSeededExponential, false)),
        ("WindowAtrBands(300, 14)", () => new WindowAtrBands(300, 14)),
        ("AlignedEmaMacd()", () => new AlignedEmaMacd()),
        ("AlignedEmaMacd(fixed)", () => new AlignedEmaMacd(12, 26, 9, false, 0, true)),
        ("AlignedEmaMacd(first,supp)", () => new AlignedEmaMacd(5, 13, 4, true, 3)),
        ("AlignedEmaMacd(supp,selection)", () => new AlignedEmaMacd(5, 13, 1, false, 2, false, EmaDifferenceSelection.Oscillator | EmaDifferenceSelection.Histogram)),
    ];

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var (name, _) in Cases()) data.Add(name);
        return data;
    }

    [Fact]
    public void EveryFastKernelTypeHasAParityCase()
    {
        var covered = Cases().Select(c => c.Create().GetType()).ToHashSet();
        var missing = FastExecution.KernelTypes.Where(type => !covered.Contains(type)).Select(type => type.Name).ToArray();
        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task FastAgreesWithExactOnEveryOutput(string name)
    {
        var create = Cases().Single(c => c.Name == name).Create;
        foreach (var (fixture, bars) in Fixtures())
        {
            var exactIndicator = create();
            // Some indicators have no finite value on some fixtures (a division by a close of exactly zero).
            // The exact route rejects those; the fast route must reject them at the same bar with the same value.
            IndicatorOutputException? rejection = null;
            IIndicatorRun? exactRun = null;
            try
            {
                exactRun = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(exactIndicator).BuildAsync();
            }
            catch (IndicatorOutputException rejected)
            {
                rejection = rejected;
            }
            using var exact = exactRun;
            foreach (var columnar in new[] { false, true })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = create();
                var source = columnar
                    ? Bars.FromColumns(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
                        bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(),
                        bars.Select(b => b.Volume).ToArray(), bars.Select(b => b.Time).ToArray())
                    : Bars.From(bars);
                var builder = new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(indicator)
                    .ConfigureNumerics(IndicatorNumerics.Fast).ConfigureHistory(history);
                var where = $"{name} / {fixture} / columnar={columnar} / {history}";
                if (rejection is not null || exact is null)
                {
                    var fastRejection = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                    Assert.True(rejection is not null && rejection.Message == fastRejection.Message,
                        $"{where}: exact rejected with '{rejection?.Message}', fast with '{fastRejection.Message}'");
                    continue;
                }
                using var fast = await builder.BuildAsync();
                Assert.True(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal), where + ": did not take the fast route");
                Assert.Equal(bars.Length, fast.BarCount);
                Assert.Equal(exactIndicator.Outputs.Count, indicator.Outputs.Count);
                for (var slot = 0; slot < indicator.Outputs.Count; slot++)
                {
                    var expected = exact[exactIndicator.Outputs[slot]];
                    var actual = fast[indicator.Outputs[slot]];
                    Assert.Equal(expected.Length, actual.Length);
                    for (var i = 0; i < expected.Length; i++)
                    {
                        if (double.IsNaN(expected[i]))
                        {
                            Assert.True(double.IsNaN(actual[i]), $"{where}: slot {slot}, bar {i} expected NaN, was {actual[i]:R}");
                            continue;
                        }
                        var scale = Math.Max(Math.Abs(expected[i]), Math.Abs(bars[i].Close));
                        Assert.True(Math.Abs(expected[i] - actual[i]) <= Tolerance * Math.Max(scale, 1e-300),
                            $"{where}: slot {slot}, bar {i} expected {expected[i]:R}, was {actual[i]:R}");
                    }
                }
                if (bars.Length != 0)
                    Assert.Equal(bars[^1].Close, fast.Latest.Bar.Close);
            }
        }
    }

    [Fact]
    public async Task FastValidatesOnlyTheFieldsItReads()
    {
        var bars = Walk(64, 11);
        bars[10] = new Bar(bars[10].Time, bars[10].Open, bars[10].High, bars[10].Low, bars[10].Close, double.NaN);
        var sma = new Sma(5);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(sma)
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync();
        Assert.Equal(bars.Length, run[sma].Length);

        bars[12] = new Bar(bars[12].Time, bars[12].Open, bars[12].High, bars[12].Low, double.PositiveInfinity, 1);
        foreach (var columnar in new[] { false, true })
        {
            var source = columnar
                ? Bars.FromColumns(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
                    bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray())
                : Bars.From(bars);
            var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
                .ConfigureSource(source).ConfigureIndicators(new Sma(5)).ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync());
            Assert.Contains("Close must be finite.", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExactStaysTheDefaultAndUnsupportedBuildsKeepTheExactRoute()
    {
        var bars = Walk(64, 3);
        var plain = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new Sma(5));
        using (await plain.BuildAsync())
            Assert.False(plain.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));

        // An indicator reading another indicator's series has no fast kernel, so the whole build stays exact.
        var chained = new Sma(5).Of(new Sma(3));
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(chained)
            .ConfigureNumerics(IndicatorNumerics.Fast);
        using (await builder.BuildAsync())
            Assert.False(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FastFailuresArriveThroughTheTask()
    {
        var bars = Walk(32, 7);
        bars[4] = new Bar(bars[4].Time, 1, 1, 1, double.NaN, 1);
        // Calling BuildAsync must not throw; the failure belongs to the task it returns.
        var failing = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new Sma(3))
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => failing);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = new StockIndicatorBuilder().ConfigureSource(Bars.From(Walk(32, 7))).ConfigureIndicators(new Sma(3))
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync(cancelled.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopped);
        Assert.True(stopped.IsCanceled);
    }

    [Fact]
    public async Task FastRejectsANonFiniteOutputExactlyAsTheExactRouteDoes()
    {
        // exp(1000) overflows on bar 5 only.
        var bars = Walk(16, 9);
        bars[5] = new Bar(bars[5].Time, 1000, 1000, 1000, 1000, 1);
        Task<IIndicatorRun> Build(IndicatorNumerics numerics) => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new PriceTranscendentalTransform(PriceTranscendentalOperation.Exponential))
            .ConfigureNumerics(numerics).BuildAsync();
        var exact = await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(IndicatorNumerics.Exact));
        var fast = await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(IndicatorNumerics.Fast));
        Assert.Equal(exact.Message, fast.Message);
        Assert.Equal(5, fast.BarIndex);
    }

    [Fact]
    public async Task ColumnSourceServesTheExactRouteAndReplay()
    {
        var bars = Walk(120, 13);
        IBarSource Columns() => Bars.FromColumns(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(),
            bars.Select(b => b.Time).ToArray());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var fromBars = new SeededAverageTrueRange(14);
            var fromColumns = new SeededAverageTrueRange(14);
            using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(fromBars).ConfigureHistory(history).BuildAsync();
            var builder = new StockIndicatorBuilder().ConfigureSource(Columns()).ConfigureIndicators(fromColumns)
                .ConfigureHistory(history);
            using var actual = await builder.BuildAsync();
            Assert.False(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
            Assert.Equal(expected[fromBars].ToArray(), actual[fromColumns].ToArray());
        }

        var replayed = new Sma(5);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Columns()).ConfigureIndicators(replayed)
            .ConfigureNumerics(IndicatorNumerics.Fast).BuildAsync();
        var index = 0;
        await foreach (var snapshot in run)
        {
            Assert.Equal(bars[index].Close, snapshot.Bar.Close);
            Assert.Equal(bars[index].Time, snapshot.Bar.Time);
            Assert.Equal(run[replayed][index], snapshot[replayed]);
            index++;
        }
        Assert.Equal(bars.Length, index);

        await Assert.ThrowsAsync<ArgumentException>(() => Task.Run(() => Bars.FromColumns(new double[2], new double[2],
            new double[2], new double[3], new double[2])));
    }

    [Fact]
    public async Task AColumnBuildWithoutAKernelTakesTheRouteAnArrayBuildTakes()
    {
        var bars = Walk(200, 17);
        double[] open = bars.Select(b => b.Open).ToArray(), high = bars.Select(b => b.High).ToArray(),
            low = bars.Select(b => b.Low).ToArray(), close = bars.Select(b => b.Close).ToArray(),
            volume = bars.Select(b => b.Volume).ToArray();
        var times = bars.Select(b => b.Time).ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            // No kernel covers this average, so both builds fall back to the exact routes.
            var fromArray = new ClassicMovingAverage(10, ClassicAverageMethod.Kama);
            var arrayBuilder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(fromArray)
                .ConfigureNumerics(IndicatorNumerics.Fast).ConfigureHistory(history);
            using var arrayRun = await arrayBuilder.BuildAsync();
            var fromColumns = new ClassicMovingAverage(10, ClassicAverageMethod.Kama);
            var columnBuilder = new StockIndicatorBuilder()
                .ConfigureSource(Bars.FromColumns(open, high, low, close, volume, times)).ConfigureIndicators(fromColumns)
                .ConfigureNumerics(IndicatorNumerics.Fast).ConfigureHistory(history);
            using (var columnRun = await columnBuilder.BuildAsync())
            {
                Assert.False(arrayBuilder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
                Assert.Equal(arrayBuilder.LastExecution?.Reason, columnBuilder.LastExecution?.Reason);
                Assert.Equal(arrayRun[fromArray].ToArray(), columnRun[fromColumns].ToArray());
            }

            // The columns are borrowed, so a later build reads what they hold then.
            var kept = close[^1];
            close[^1] = kept + 50;
            high[^1] += 50;
            using (var changed = await columnBuilder.BuildAsync())
                Assert.NotEqual(arrayRun[fromArray][^1], changed[fromColumns][^1]);
            close[^1] = kept;
            high[^1] -= 50;
        }
    }

    [Fact]
    public void TheFiniteCheckFindsANonFiniteValueAtEveryPosition()
    {
        // Lengths on both sides of every vector and unrolled-block boundary, and a negative zero, which is finite.
        foreach (var length in new[] { 0, 1, 3, 4, 7, 15, 16, 17, 31, 32, 33, 63, 64, 65, 100 })
        {
            var values = Enumerable.Range(0, length).Select(i => i % 5 == 0 ? -0.0 : i * 1e300 / 3 - 1e299).ToArray();
            Assert.True(OoplesFinance.StockIndicators.Indicators.Fast.FastMath.AllFinite(values), $"length {length}");
            foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            for (var at = 0; at < length; at++)
            {
                var kept = values[at];
                values[at] = bad;
                Assert.False(OoplesFinance.StockIndicators.Indicators.Fast.FastMath.AllFinite(values), $"length {length}, {bad} at {at}");
                values[at] = kept;
            }
        }
    }

    [Fact]
    public async Task AKernelThatDoesNotCoverAConfigurationLeavesItToTheExactRoute()
    {
        var bars = Walk(200, 19);
        var exactIndicator = new ClassicMovingAverage(10, ClassicAverageMethod.Kama);
        var fastIndicator = new ClassicMovingAverage(10, ClassicAverageMethod.Kama);
        using var exact = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(exactIndicator).BuildAsync();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(fastIndicator)
            .ConfigureNumerics(IndicatorNumerics.Fast);
        using var fast = await builder.BuildAsync();
        Assert.False(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
        Assert.Equal(exact[exactIndicator].ToArray(), fast[fastIndicator].ToArray());
    }

    [Fact]
    public async Task ALegacyBuildAfterAFastBuildReadsTheSameSource()
    {
        var bars = Enumerable.Range(0, 6).Select(i => new Bar(Day(i), i + 1, i + 1, i + 1, i + 1, 1)).ToArray();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new Sma(3))
            .ConfigureNumerics(IndicatorNumerics.Fast);
        using var typed = await builder.BuildAsync();
        Assert.True(builder.LastExecution?.Reason.StartsWith("Fast", StringComparison.Ordinal));
        SeriesHandle average = default;
        using var legacy = builder.ConfigureIndicators(catalog => average = catalog.Sma(3)).Build();
        legacy.Start();
        Assert.Equal(new[] { 0d, 0d, 2d, 3d, 4d, 5d }, legacy.GetSeries(average).ToArray());
    }

    private static IEnumerable<(string Name, Bar[] Bars)> Fixtures()
    {
        yield return ("empty", []);
        yield return ("one bar", Walk(1, 5));
        yield return ("shorter than most windows", Walk(7, 5));
        yield return ("walk 300", Walk(300, 17));
        yield return ("walk 2000", Walk(2000, 29));
        yield return ("signed unit range", Walk(400, 41).Select((bar, i) =>
        {
            // Closes in about [-1.6, 1.6], so inverse circular functions, logarithms and square roots meet
            // both sides of their domains. The close is kept off the middle of the range: at the exact middle
            // a money-flow multiplier is zero in real arithmetic, and all that is left to compare is the
            // rounding of the inputs.
            var close = Math.Round(1.6 * Math.Sin(i * 0.37) + (bar.Close % 1 - .5) * .1, 4);
            return new Bar(bar.Time, close - .05, close + .3, close - .2, close, bar.Volume);
        }).ToArray());
        // Zero prices make every ratio zero over zero, which each indicator defines for itself.
        yield return ("zero", Enumerable.Range(0, 60).Select(i => new Bar(Day(i), 0, 0, 0, 0, 0)).ToArray());
        yield return ("flat", Enumerable.Range(0, 250).Select(i => new Bar(Day(i), 50, 50, 50, 50, 1000)).ToArray());
    }

    private static DateTime Day(int index) => new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index);

    /// <summary>A deterministic cent-grid random walk with consistent OHLC and varying volume.</summary>
    private static Bar[] Walk(int count, int seed)
    {
        var random = RandomHelper.CreateSeededRandom(seed);
        var bars = new Bar[count];
        double close = 100;
        for (var i = 0; i < count; i++)
        {
            var open = close;
            close = Math.Round(Math.Max(1, close + (random.NextDouble() - .5) * 2), 2);
            var high = Math.Round(Math.Max(open, close) + random.NextDouble(), 2);
            var low = Math.Round(Math.Max(.01, Math.Min(open, close) - random.NextDouble()), 2);
            bars[i] = new Bar(Day(i), open, high, low, close, 1000 + random.Next(0, 5000));
        }
        return bars;
    }
}
