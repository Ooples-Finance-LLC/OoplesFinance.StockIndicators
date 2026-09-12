using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>
/// Interface for indicator options.
/// </summary>
public interface IIndicatorSpecOptions { }

/// <summary>
/// Specification for an indicator computation.
/// </summary>
// Some options below are obsolete because their batch indicator has nothing they could set; their own
// constructors still assign them until they are removed.
#pragma warning disable CS0618
public sealed class IndicatorSpec
{
    /// <summary>
    /// Creates a new indicator specification.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public IndicatorSpec(IndicatorName name, IIndicatorSpecOptions options, IndicatorOutput output)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        Name = name;
        Options = options;
        Output = output;
    }

    /// <summary>
    /// Gets the indicator name.
    /// </summary>
    public IndicatorName Name { get; }

    /// <summary>
    /// Gets the indicator options.
    /// </summary>
    public IIndicatorSpecOptions Options { get; }

    /// <summary>
    /// Gets the output type.
    /// </summary>
    public IndicatorOutput Output { get; }
}

/// <summary>
/// Factory for creating indicator specifications.
/// </summary>
public static class IndicatorSpecs
{
    /// <summary>
    /// Creates an SMA specification.
    /// </summary>
    public static IndicatorSpec Sma(int length)
    {
        return new IndicatorSpec(IndicatorName.SimpleMovingAverage, new SmaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates an EMA specification.
    /// </summary>
    public static IndicatorSpec Ema(int length)
    {
        return new IndicatorSpec(IndicatorName.ExponentialMovingAverage, new EmaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates an RSI specification.
    /// </summary>
    public static IndicatorSpec Rsi(int length)
    {
        return new IndicatorSpec(IndicatorName.RelativeStrengthIndex, new RsiSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a MACD specification.
    /// </summary>
    public static IndicatorSpec Macd(int fastLength, int slowLength, int signalLength, IndicatorOutput output)
    {
        return new IndicatorSpec(IndicatorName.MovingAverageConvergenceDivergence,
            new MacdSpecOptions(fastLength, slowLength, signalLength), output);
    }

    /// <summary>
    /// Creates a Bollinger Bands specification.
    /// </summary>
    public static IndicatorSpec BollingerBands(int length, double stdDevMult, IndicatorOutput output)
    {
        return new IndicatorSpec(IndicatorName.BollingerBands, new BollingerBandsSpecOptions(length, stdDevMult), output);
    }

    /// <summary>
    /// Creates an ATR specification.
    /// </summary>
    public static IndicatorSpec Atr(int length)
    {
        return new IndicatorSpec(IndicatorName.AverageTrueRange, new AtrSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a Stochastic specification.
    /// </summary>
    public static IndicatorSpec Stochastic(int kLength, int dLength, IndicatorOutput output)
    {
        return new IndicatorSpec(IndicatorName.StochasticOscillator, new StochasticSpecOptions(kLength, dLength), output);
    }

    /// <summary>
    /// Creates an ADX specification.
    /// </summary>
    public static IndicatorSpec Adx(int length)
    {
        return new IndicatorSpec(IndicatorName.AverageDirectionalIndex, new AdxSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a WMA specification.
    /// </summary>
    public static IndicatorSpec Wma(int length)
    {
        return new IndicatorSpec(IndicatorName.WeightedMovingAverage, new WmaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a DEMA specification.
    /// </summary>
    public static IndicatorSpec Dema(int length)
    {
        return new IndicatorSpec(IndicatorName.DoubleExponentialMovingAverage, new DemaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a TEMA specification.
    /// </summary>
    public static IndicatorSpec Tema(int length)
    {
        return new IndicatorSpec(IndicatorName.TripleExponentialMovingAverage, new TemaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a HMA specification.
    /// </summary>
    public static IndicatorSpec Hma(int length)
    {
        return new IndicatorSpec(IndicatorName.HullMovingAverage, new HmaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a TMA specification.
    /// </summary>
    public static IndicatorSpec Tma(int length)
    {
        return new IndicatorSpec(IndicatorName.TriangularMovingAverage, new TmaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a WWMA (Welles Wilder Moving Average) specification.
    /// </summary>
    public static IndicatorSpec Wwma(int length)
    {
        return new IndicatorSpec(IndicatorName.WellesWilderMovingAverage, new WwmaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a Linear Regression specification.
    /// </summary>
    public static IndicatorSpec LinReg(int length)
    {
        return new IndicatorSpec(IndicatorName.LinearRegression, new LinRegSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a KAMA specification.
    /// </summary>
    public static IndicatorSpec Kama(int length)
    {
        return new IndicatorSpec(IndicatorName.KaufmanAdaptiveMovingAverage, new KamaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a ZLEMA specification.
    /// </summary>
    public static IndicatorSpec Zlema(int length)
    {
        return new IndicatorSpec(IndicatorName.ZeroLagExponentialMovingAverage, new ZlemaSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a ROC specification.
    /// </summary>
    public static IndicatorSpec Roc(int length)
    {
        return new IndicatorSpec(IndicatorName.RateOfChange, new RocSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a Momentum specification.
    /// </summary>
    public static IndicatorSpec Momentum(int length)
    {
        return new IndicatorSpec(IndicatorName.MomentumOscillator, new MomentumSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a Williams %R specification.
    /// </summary>
    public static IndicatorSpec WilliamsR(int length)
    {
        return new IndicatorSpec(IndicatorName.WilliamsR, new WilliamsRSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a CCI specification.
    /// </summary>
    public static IndicatorSpec Cci(int length)
    {
        return new IndicatorSpec(IndicatorName.CommodityChannelIndex, new CciSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a CMO specification.
    /// </summary>
    public static IndicatorSpec Cmo(int length)
    {
        return new IndicatorSpec(IndicatorName.ChandeMomentumOscillator, new CmoSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a PPO specification.
    /// </summary>
    public static IndicatorSpec Ppo(int fastLength, int slowLength)
    {
        return new IndicatorSpec(IndicatorName.PercentagePriceOscillator, new PpoSpecOptions(fastLength, slowLength), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates an APO specification.
    /// </summary>
    public static IndicatorSpec Apo(int fastLength, int slowLength)
    {
        return new IndicatorSpec(IndicatorName.AbsolutePriceOscillator, new ApoSpecOptions(fastLength, slowLength), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates an Ultimate Oscillator specification.
    /// </summary>
    public static IndicatorSpec UltimateOscillator(int length1, int length2, int length3)
    {
        return new IndicatorSpec(IndicatorName.UltimateOscillator, new UltimateOscillatorSpecOptions(length1, length2, length3), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a TSI specification.
    /// </summary>
    public static IndicatorSpec Tsi(int longLength, int shortLength)
    {
        return new IndicatorSpec(IndicatorName.TrueStrengthIndex, new TsiSpecOptions(longLength, shortLength), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a Stochastic RSI specification.
    /// </summary>
    public static IndicatorSpec StochRsi(int rsiLength, int stochLength)
    {
        return new IndicatorSpec(IndicatorName.StochasticRelativeStrengthIndex, new StochRsiSpecOptions(rsiLength, stochLength), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates an Aroon specification.
    /// </summary>
    public static IndicatorSpec Aroon(int length, IndicatorOutput output)
    {
        return new IndicatorSpec(IndicatorName.AroonOscillator, new AroonSpecOptions(length), output);
    }

    /// <summary>
    /// Creates a DPO specification.
    /// </summary>
    public static IndicatorSpec Dpo(int length)
    {
        return new IndicatorSpec(IndicatorName.DetrendedPriceOscillator, new DpoSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a TRIX specification.
    /// </summary>
    public static IndicatorSpec Trix(int length)
    {
        return new IndicatorSpec(IndicatorName.Trix, new TrixSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a Mass Index specification.
    /// </summary>
    public static IndicatorSpec MassIndex(int emaLength, int sumLength)
    {
        return new IndicatorSpec(IndicatorName.MassIndex, new MassIndexSpecOptions(emaLength, sumLength), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates an OBV specification.
    /// </summary>
    public static IndicatorSpec Obv()
    {
        return new IndicatorSpec(IndicatorName.OnBalanceVolume, new ObvSpecOptions(), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates an ADL specification.
    /// </summary>
    public static IndicatorSpec Adl()
    {
        return new IndicatorSpec(IndicatorName.AccumulationDistributionLine, new AdlSpecOptions(), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a CMF specification.
    /// </summary>
    public static IndicatorSpec Cmf(int length)
    {
        return new IndicatorSpec(IndicatorName.ChaikinMoneyFlow, new CmfSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a Force Index specification.
    /// </summary>
    public static IndicatorSpec ForceIndex(int length)
    {
        return new IndicatorSpec(IndicatorName.ForceIndex, new ForceIndexSpecOptions(length), IndicatorOutput.Primary);
    }

    /// <summary>
    /// Creates a generic indicator specification.
    /// </summary>
    public static IndicatorSpec Create(IndicatorName name, IIndicatorSpecOptions options, IndicatorOutput output = IndicatorOutput.Primary)
    {
        return new IndicatorSpec(name, options, output);
    }

    /// <summary>
    /// Creates a multi-stock indicator specification.
    /// </summary>
    public static IndicatorSpec CreateMultiStock(IndicatorName name, MultiStockIndicatorOptions options, IndicatorOutput output = IndicatorOutput.Primary)
    {
        return new IndicatorSpec(name, options, output);
    }
}

/// <summary>
/// SMA indicator options.
/// </summary>
public sealed class SmaSpecOptions : IIndicatorSpecOptions
{
    public SmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// EMA indicator options.
/// </summary>
public sealed class EmaSpecOptions : IIndicatorSpecOptions
{
    public EmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// RSI indicator options.
/// </summary>
public sealed class RsiSpecOptions : IIndicatorSpecOptions
{
    public RsiSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public RsiSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// MACD indicator options.
/// </summary>
public sealed class MacdSpecOptions : IIndicatorSpecOptions
{
    public MacdSpecOptions(int fastLength, int slowLength, int signalLength)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
}

/// <summary>
/// Bollinger Bands indicator options.
/// </summary>
public sealed class BollingerBandsSpecOptions : IIndicatorSpecOptions
{
    public BollingerBandsSpecOptions(int length, double stdDevMult)
        : this(length, stdDevMult, MovingAvgType.SimpleMovingAverage)
    {
    }

    public BollingerBandsSpecOptions(int length, double stdDevMult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        // Validate StdDevMult to prevent invalid band calculations (inverted/collapsed bands)
        StdDevMult = Math.Max(0.001, stdDevMult);
        MaType = maType;
    }

    public int Length { get; }
    public double StdDevMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// ATR indicator options.
/// </summary>
public sealed class AtrSpecOptions : IIndicatorSpecOptions
{
    public AtrSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public AtrSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Stochastic indicator options.
/// </summary>
public sealed class StochasticSpecOptions : IIndicatorSpecOptions
{
    public StochasticSpecOptions(int kLength, int dLength)
        : this(kLength, dLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StochasticSpecOptions(int kLength, int dLength, MovingAvgType maType)
    {
        KLength = Math.Max(1, kLength);
        DLength = Math.Max(1, dLength);
        MaType = maType;
    }

    public int KLength { get; }
    public int DLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// ADX indicator options.
/// </summary>
public sealed class AdxSpecOptions : IIndicatorSpecOptions
{
    public AdxSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public AdxSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// WMA indicator options.
/// </summary>
public sealed class WmaSpecOptions : IIndicatorSpecOptions
{
    public WmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// DEMA indicator options.
/// </summary>
public sealed class DemaSpecOptions : IIndicatorSpecOptions
{
    public DemaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// TEMA indicator options.
/// </summary>
public sealed class TemaSpecOptions : IIndicatorSpecOptions
{
    public TemaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// HMA indicator options.
/// </summary>
public sealed class HmaSpecOptions : IIndicatorSpecOptions
{
    public HmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// TMA indicator options.
/// </summary>
public sealed class TmaSpecOptions : IIndicatorSpecOptions
{
    public TmaSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TmaSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// WWMA (Welles Wilder Moving Average) indicator options.
/// </summary>
public sealed class WwmaSpecOptions : IIndicatorSpecOptions
{
    public WwmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Linear Regression indicator options.
/// </summary>
public sealed class LinRegSpecOptions : IIndicatorSpecOptions
{
    public LinRegSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// KAMA indicator options.
/// </summary>
public sealed class KamaSpecOptions : IIndicatorSpecOptions
{
    public KamaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// ZLEMA indicator options.
/// </summary>
public sealed class ZlemaSpecOptions : IIndicatorSpecOptions
{
    public ZlemaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// ROC indicator options.
/// </summary>
public sealed class RocSpecOptions : IIndicatorSpecOptions
{
    public RocSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Momentum indicator options.
/// </summary>
public sealed class MomentumSpecOptions : IIndicatorSpecOptions
{
    public MomentumSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Williams %R indicator options.
/// </summary>
public sealed class WilliamsRSpecOptions : IIndicatorSpecOptions
{
    public WilliamsRSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// CCI indicator options.
/// </summary>
public sealed class CciSpecOptions : IIndicatorSpecOptions
{
    public CciSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// CMO (Chande Momentum Oscillator) indicator options.
/// </summary>
public sealed class CmoSpecOptions : IIndicatorSpecOptions
{
    public CmoSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public CmoSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// PPO indicator options.
/// </summary>
public sealed class PpoSpecOptions : IIndicatorSpecOptions
{
    public PpoSpecOptions(int fastLength, int slowLength)
        : this(fastLength, slowLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PpoSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// APO indicator options.
/// </summary>
public sealed class ApoSpecOptions : IIndicatorSpecOptions
{
    public ApoSpecOptions(int fastLength, int slowLength)
        : this(fastLength, slowLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ApoSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ultimate Oscillator indicator options.
/// </summary>
public sealed class UltimateOscillatorSpecOptions : IIndicatorSpecOptions
{
    public UltimateOscillatorSpecOptions(int length1, int length2, int length3)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
}

/// <summary>
/// TSI indicator options.
/// </summary>
public sealed class TsiSpecOptions : IIndicatorSpecOptions
{
    public TsiSpecOptions(int longLength, int shortLength)
        : this(longLength, shortLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TsiSpecOptions(int longLength, int shortLength, MovingAvgType maType)
    {
        LongLength = Math.Max(1, longLength);
        ShortLength = Math.Max(1, shortLength);
        MaType = maType;
    }

    public int LongLength { get; }
    public int ShortLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Stochastic RSI indicator options.
/// </summary>
public sealed class StochRsiSpecOptions : IIndicatorSpecOptions
{
    public StochRsiSpecOptions(int rsiLength, int stochLength)
        : this(rsiLength, stochLength, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public StochRsiSpecOptions(int rsiLength, int stochLength, MovingAvgType maType)
    {
        RsiLength = Math.Max(1, rsiLength);
        StochLength = Math.Max(1, stochLength);
        MaType = maType;
    }

    public int RsiLength { get; }
    public int StochLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Aroon indicator options.
/// </summary>
public sealed class AroonSpecOptions : IIndicatorSpecOptions
{
    public AroonSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// DPO indicator options.
/// </summary>
public sealed class DpoSpecOptions : IIndicatorSpecOptions
{
    public DpoSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// TRIX indicator options.
/// </summary>
public sealed class TrixSpecOptions : IIndicatorSpecOptions
{
    public TrixSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TrixSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Mass Index indicator options.
/// </summary>
public sealed class MassIndexSpecOptions : IIndicatorSpecOptions
{
    public MassIndexSpecOptions(int emaLength, int sumLength)
        : this(emaLength, sumLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MassIndexSpecOptions(int emaLength, int sumLength, MovingAvgType maType)
    {
        EmaLength = Math.Max(1, emaLength);
        SumLength = Math.Max(1, sumLength);
        MaType = maType;
    }

    public int EmaLength { get; }
    public int SumLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// OBV indicator options.
/// </summary>
public sealed class ObvSpecOptions : IIndicatorSpecOptions
{
    public ObvSpecOptions(int length = 14)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// ADL indicator options.
/// </summary>
public sealed class AdlSpecOptions : IIndicatorSpecOptions
{
    public AdlSpecOptions(int length = 14)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// CMF indicator options.
/// </summary>
public sealed class CmfSpecOptions : IIndicatorSpecOptions
{
    public CmfSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Force Index indicator options.
/// </summary>
public sealed class ForceIndexSpecOptions : IIndicatorSpecOptions
{
    public ForceIndexSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ForceIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volume ROC (VROC) indicator options.
/// </summary>
public sealed class VrocSpecOptions : IIndicatorSpecOptions
{
    public VrocSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Negative Volume Index (NVI) indicator options.
/// </summary>
public sealed class NviSpecOptions : IIndicatorSpecOptions
{
    public NviSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Positive Volume Index (PVI) indicator options.
/// </summary>
public sealed class PviSpecOptions : IIndicatorSpecOptions
{
    public PviSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Price Volume Trend (PVT) indicator options.
/// </summary>
public sealed class PvtSpecOptions : IIndicatorSpecOptions
{
    public PvtSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Chaikin Oscillator indicator options.
/// </summary>
public sealed class ChaikinOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ChaikinOscillatorSpecOptions(int fastLength, int slowLength)
        : this(fastLength, slowLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ChaikinOscillatorSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ease of Movement (EMV) indicator options.
/// </summary>
public sealed class EmvSpecOptions : IIndicatorSpecOptions
{
    public EmvSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Klinger Volume Oscillator (KVO) indicator options.
/// </summary>
public sealed class KvoSpecOptions : IIndicatorSpecOptions
{
    public KvoSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Money Flow Index (MFI) indicator options.
/// </summary>
public sealed class MfiSpecOptions : IIndicatorSpecOptions
{
    public MfiSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Standard Deviation (StdDev) indicator options.
/// </summary>
public sealed class StdDevSpecOptions : IIndicatorSpecOptions
{
    public StdDevSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Historical Volatility indicator options.
/// </summary>
public sealed class HistoricalVolatilitySpecOptions : IIndicatorSpecOptions
{
    public HistoricalVolatilitySpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public HistoricalVolatilitySpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chaikin Volatility indicator options.
/// </summary>
public sealed class ChaikinVolatilitySpecOptions : IIndicatorSpecOptions
{
    public ChaikinVolatilitySpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ChaikinVolatilitySpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ulcer Index indicator options.
/// </summary>
public sealed class UlcerIndexSpecOptions : IIndicatorSpecOptions
{
    public UlcerIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Normalized ATR (NATR) indicator options.
/// </summary>
public sealed class NatrSpecOptions : IIndicatorSpecOptions
{
    public NatrSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Donchian Channel indicator options.
/// </summary>
public sealed class DonchianChannelSpecOptions : IIndicatorSpecOptions
{
    public DonchianChannelSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Highest High indicator options.
/// </summary>
public sealed class HighestHighSpecOptions : IIndicatorSpecOptions
{
    public HighestHighSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Lowest Low indicator options.
/// </summary>
public sealed class LowestLowSpecOptions : IIndicatorSpecOptions
{
    public LowestLowSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Percentage Change indicator options.
/// </summary>
public sealed class PercentageChangeSpecOptions : IIndicatorSpecOptions
{
    public PercentageChangeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Linear Regression Slope indicator options.
/// </summary>
public sealed class LinRegSlopeSpecOptions : IIndicatorSpecOptions
{
    public LinRegSlopeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// R-Squared indicator options.
/// </summary>
public sealed class RSquaredSpecOptions : IIndicatorSpecOptions
{
    public RSquaredSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Vertical Horizontal Filter (VHF) indicator options.
/// </summary>
public sealed class VhfSpecOptions : IIndicatorSpecOptions
{
    public VhfSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// True Range indicator options.
/// </summary>
public sealed class TrueRangeSpecOptions : IIndicatorSpecOptions
{
    public TrueRangeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: TrueRange has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Awesome Oscillator indicator options.
/// </summary>
public sealed class AwesomeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public AwesomeOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public AwesomeOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Accelerator Oscillator indicator options.
/// </summary>
public sealed class AcceleratorOscillatorSpecOptions : IIndicatorSpecOptions
{
    public AcceleratorOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public AcceleratorOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Stochastic Fast K indicator options.
/// </summary>
public sealed class StochasticKSpecOptions : IIndicatorSpecOptions
{
    public StochasticKSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Fisher Transform indicator options.
/// </summary>
public sealed class FisherTransformSpecOptions : IIndicatorSpecOptions
{
    public FisherTransformSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Connors RSI indicator options.
/// </summary>
public sealed class ConnorsRsiSpecOptions : IIndicatorSpecOptions
{
    public ConnorsRsiSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Price Momentum Oscillator (PMO) indicator options.
/// </summary>
public sealed class PmoSpecOptions : IIndicatorSpecOptions
{
    public PmoSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PmoSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Know Sure Thing (KST) indicator options.
/// </summary>
public sealed class KstSpecOptions : IIndicatorSpecOptions
{
    public KstSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Percent Rank indicator options.
/// </summary>
public sealed class PercentRankSpecOptions : IIndicatorSpecOptions
{
    public PercentRankSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Choppiness Index indicator options.
/// </summary>
public sealed class ChoppinessIndexSpecOptions : IIndicatorSpecOptions
{
    public ChoppinessIndexSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ChoppinessIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// SMMA (Smoothed Moving Average) indicator options.
/// </summary>
public sealed class SmmaSpecOptions : IIndicatorSpecOptions
{
    public SmmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// McGinley Dynamic indicator options.
/// </summary>
public sealed class McGinleyDynamicSpecOptions : IIndicatorSpecOptions
{
    public McGinleyDynamicSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// T3 Moving Average indicator options.
/// </summary>
public sealed class T3SpecOptions : IIndicatorSpecOptions
{
    public T3SpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public T3SpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// VIDYA (Variable Index Dynamic Average) indicator options.
/// </summary>
public sealed class VidyaSpecOptions : IIndicatorSpecOptions
{
    public VidyaSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public VidyaSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// VMA (Variable Moving Average) indicator options.
/// </summary>
public sealed class VmaSpecOptions : IIndicatorSpecOptions
{
    public VmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// ALMA (Arnaud Legoux Moving Average) indicator options.
/// </summary>
public sealed class AlmaSpecOptions : IIndicatorSpecOptions
{
    public AlmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// LSMA (Least Squares Moving Average) indicator options.
/// </summary>
public sealed class LsmaSpecOptions : IIndicatorSpecOptions
{
    public LsmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// FRAMA (Fractal Adaptive Moving Average) indicator options.
/// </summary>
public sealed class FramaSpecOptions : IIndicatorSpecOptions
{
    public FramaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// AMA (Adaptive Moving Average) indicator options.
/// </summary>
public sealed class AmaSpecOptions : IIndicatorSpecOptions
{
    public AmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// JMA (Jurik Moving Average) indicator options.
/// </summary>
public sealed class JmaSpecOptions : IIndicatorSpecOptions
{
    public JmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Super Smoother indicator options.
/// </summary>
public sealed class SuperSmootherSpecOptions : IIndicatorSpecOptions
{
    public SuperSmootherSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Butterworth Filter indicator options.
/// </summary>
public sealed class ButterworthFilterSpecOptions : IIndicatorSpecOptions
{
    public ButterworthFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// MACD Line indicator options.
/// </summary>
public sealed class MacdLineSpecOptions : IIndicatorSpecOptions
{
    public MacdLineSpecOptions(int fastLength, int slowLength)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
}

/// <summary>
/// MACD Signal indicator options.
/// </summary>
public sealed class MacdSignalSpecOptions : IIndicatorSpecOptions
{
    public MacdSignalSpecOptions(int fastLength, int slowLength, int signalLength)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
}

/// <summary>
/// MACD Histogram indicator options.
/// </summary>
public sealed class MacdHistogramSpecOptions : IIndicatorSpecOptions
{
    public MacdHistogramSpecOptions(int fastLength, int slowLength, int signalLength)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
}

/// <summary>
/// Parabolic SAR indicator options.
/// </summary>
public sealed class ParabolicSarSpecOptions : IIndicatorSpecOptions
{
    public ParabolicSarSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: ParabolicSAR has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// SuperTrend indicator options.
/// </summary>
public sealed class SuperTrendSpecOptions : IIndicatorSpecOptions
{
    public SuperTrendSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public SuperTrendSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Balance of Power indicator options.
/// </summary>
public sealed class BalanceOfPowerSpecOptions : IIndicatorSpecOptions
{
    public BalanceOfPowerSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public BalanceOfPowerSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Relative Vigor Index indicator options.
/// </summary>
public sealed class RviSpecOptions : IIndicatorSpecOptions
{
    public RviSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// PVO (Percentage Volume Oscillator) indicator options.
/// </summary>
public sealed class PvoSpecOptions : IIndicatorSpecOptions
{
    public PvoSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Coppock Curve indicator options.
/// </summary>
public sealed class CoppockCurveSpecOptions : IIndicatorSpecOptions
{
    public CoppockCurveSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public CoppockCurveSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chande Forecast Oscillator indicator options.
/// </summary>
public sealed class ChandeForecastOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ChandeForecastOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Bull Power indicator options.
/// </summary>
public sealed class BullPowerSpecOptions : IIndicatorSpecOptions
{
    public BullPowerSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Bear Power indicator options.
/// </summary>
public sealed class BearPowerSpecOptions : IIndicatorSpecOptions
{
    public BearPowerSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Polarized Fractal Efficiency indicator options.
/// </summary>
public sealed class PfeSpecOptions : IIndicatorSpecOptions
{
    public PfeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Schaff Trend Cycle indicator options.
/// </summary>
public sealed class StcSpecOptions : IIndicatorSpecOptions
{
    public StcSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Price Zone Oscillator indicator options.
/// </summary>
public sealed class PzoSpecOptions : IIndicatorSpecOptions
{
    public PzoSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Elder Force Index indicator options.
/// </summary>
public sealed class ElderForceIndexSpecOptions : IIndicatorSpecOptions
{
    public ElderForceIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Pretty Good Oscillator indicator options.
/// </summary>
public sealed class PgoSpecOptions : IIndicatorSpecOptions
{
    public PgoSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Relative Volatility Index indicator options.
/// </summary>
public sealed class RelativeVolatilityIndexSpecOptions : IIndicatorSpecOptions
{
    public RelativeVolatilityIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// QStick indicator options.
/// </summary>
public sealed class QstickSpecOptions : IIndicatorSpecOptions
{
    public QstickSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Special K indicator options.
/// </summary>
public sealed class SpecialKSpecOptions : IIndicatorSpecOptions
{
    public SpecialKSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: PringSpecialK has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Vortex Positive indicator options.
/// </summary>
public sealed class VortexPositiveSpecOptions : IIndicatorSpecOptions
{
    public VortexPositiveSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Vortex Negative indicator options.
/// </summary>
public sealed class VortexNegativeSpecOptions : IIndicatorSpecOptions
{
    public VortexNegativeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Trend Intensity Index indicator options.
/// </summary>
public sealed class TrendIntensityIndexSpecOptions : IIndicatorSpecOptions
{
    public TrendIntensityIndexSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TrendIntensityIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Absolute Strength Index indicator options.
/// </summary>
public sealed class AbsoluteStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public AbsoluteStrengthIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Relative Momentum Index indicator options.
/// </summary>
public sealed class RelativeMomentumIndexSpecOptions : IIndicatorSpecOptions
{
    public RelativeMomentumIndexSpecOptions(int length, int momentum)
        : this(length, momentum, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public RelativeMomentumIndexSpecOptions(int length, int momentum, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Momentum = Math.Max(1, momentum);
        MaType = maType;
    }

    public int Length { get; }
    public int Momentum { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Intraday Momentum Index indicator options.
/// </summary>
public sealed class IntradayMomentumIndexSpecOptions : IIndicatorSpecOptions
{
    public IntradayMomentumIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// VWMA (Volume Weighted Moving Average) indicator options.
/// </summary>
public sealed class VwmaSpecOptions : IIndicatorSpecOptions
{
    public VwmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// VWAP (Volume Weighted Average Price) indicator options.
/// </summary>
public sealed class VwapSpecOptions : IIndicatorSpecOptions
{
    public VwapSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: VolumeWeightedAveragePrice has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Elliott Wave Oscillator indicator options.
/// </summary>
public sealed class ElliottWaveOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ElliottWaveOscillatorSpecOptions(int fastLength, int slowLength)
        : this(fastLength, slowLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ElliottWaveOscillatorSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Gator Oscillator indicator options.
/// </summary>
public sealed class GatorOscillatorSpecOptions : IIndicatorSpecOptions
{
    public GatorOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Chandelier Exit Long indicator options.
/// </summary>
public sealed class ChandelierExitLongSpecOptions : IIndicatorSpecOptions
{
    public ChandelierExitLongSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public ChandelierExitLongSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chandelier Exit Short indicator options.
/// </summary>
public sealed class ChandelierExitShortSpecOptions : IIndicatorSpecOptions
{
    public ChandelierExitShortSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public ChandelierExitShortSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ichimoku Tenkan Sen indicator options.
/// </summary>
public sealed class IchimokuTenkanSenSpecOptions : IIndicatorSpecOptions
{
    public IchimokuTenkanSenSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ichimoku Kijun Sen indicator options.
/// </summary>
public sealed class IchimokuKijunSenSpecOptions : IIndicatorSpecOptions
{
    public IchimokuKijunSenSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

// ========== Batch 4 SpecOptions ==========

/// <summary>
/// Typical Price indicator options.
/// </summary>
public sealed class TypicalPriceSpecOptions : IIndicatorSpecOptions
{
    public TypicalPriceSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: TypicalPrice has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Median Price indicator options.
/// </summary>
public sealed class MedianPriceSpecOptions : IIndicatorSpecOptions
{
    public MedianPriceSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: MedianPrice has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Weighted Close indicator options.
/// </summary>
public sealed class WeightedCloseSpecOptions : IIndicatorSpecOptions
{
    public WeightedCloseSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: WeightedClose has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Average Price indicator options.
/// </summary>
public sealed class AveragePriceSpecOptions : IIndicatorSpecOptions
{
    public AveragePriceSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: AveragePrice has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Midpoint indicator options.
/// </summary>
public sealed class MidpointSpecOptions : IIndicatorSpecOptions
{
    public MidpointSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Midprice indicator options.
/// </summary>
public sealed class MidpriceSpecOptions : IIndicatorSpecOptions
{
    public MidpriceSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Variance indicator options.
/// </summary>
public sealed class VarianceSpecOptions : IIndicatorSpecOptions
{
    public VarianceSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Coefficient of Variation indicator options.
/// </summary>
public sealed class CoefficientOfVariationSpecOptions : IIndicatorSpecOptions
{
    public CoefficientOfVariationSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Standard Error indicator options.
/// </summary>
public sealed class StandardErrorSpecOptions : IIndicatorSpecOptions
{
    public StandardErrorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Aroon Up indicator options.
/// </summary>
public sealed class AroonUpSpecOptions : IIndicatorSpecOptions
{
    public AroonUpSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Aroon Down indicator options.
/// </summary>
public sealed class AroonDownSpecOptions : IIndicatorSpecOptions
{
    public AroonDownSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// DeMarker indicator options.
/// </summary>
public sealed class DemarkerSpecOptions : IIndicatorSpecOptions
{
    public DemarkerSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DemarkerSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Smoothed ROC indicator options.
/// </summary>
public sealed class SmoothedRocSpecOptions : IIndicatorSpecOptions
{
    public SmoothedRocSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public SmoothedRocSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Derivative Oscillator indicator options.
/// </summary>
public sealed class DerivativeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DerivativeOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public DerivativeOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Fractal Chaos Oscillator indicator options.
/// </summary>
public sealed class FractalChaosOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FractalChaosOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: FractalChaosOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Disparity Index indicator options.
/// </summary>
public sealed class DisparityIndexSpecOptions : IIndicatorSpecOptions
{
    public DisparityIndexSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DisparityIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Dynamic Momentum Index indicator options.
/// </summary>
public sealed class DynamicMomentumIndexSpecOptions : IIndicatorSpecOptions
{
    public DynamicMomentumIndexSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DynamicMomentumIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    [Obsolete("Has no effect: DynamicMomentumIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Sine WMA indicator options.
/// </summary>
public sealed class SineWmaSpecOptions : IIndicatorSpecOptions
{
    public SineWmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Hamming MA indicator options.
/// </summary>
public sealed class HammingMaSpecOptions : IIndicatorSpecOptions
{
    public HammingMaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Geometric MA indicator options.
/// </summary>
public sealed class GeoMaSpecOptions : IIndicatorSpecOptions
{
    public GeoMaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Regularized EMA indicator options.
/// </summary>
public sealed class RegularizedEmaSpecOptions : IIndicatorSpecOptions
{
    public RegularizedEmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Modified MA indicator options.
/// </summary>
public sealed class ModifiedMaSpecOptions : IIndicatorSpecOptions
{
    public ModifiedMaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// End Point Moving Average indicator options.
/// </summary>
public sealed class EndPointMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EndPointMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Cubic WMA indicator options.
/// </summary>
public sealed class CubicWmaSpecOptions : IIndicatorSpecOptions
{
    public CubicWmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Natural MA indicator options.
/// </summary>
public sealed class NaturalMaSpecOptions : IIndicatorSpecOptions
{
    public NaturalMaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Trade Volume Index indicator options.
/// </summary>
public sealed class TradeVolumeIndexSpecOptions : IIndicatorSpecOptions
{
    public TradeVolumeIndexSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TradeVolumeIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volume Oscillator indicator options.
/// </summary>
public sealed class VolumeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public VolumeOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Volume Zone Oscillator indicator options.
/// </summary>
public sealed class VolumeZoneOscillatorSpecOptions : IIndicatorSpecOptions
{
    public VolumeZoneOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Net Volume indicator options.
/// </summary>
public sealed class NetVolumeSpecOptions : IIndicatorSpecOptions
{
    public NetVolumeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: NetVolume has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Volume Momentum indicator options.
/// </summary>
public sealed class VolumeMomentumSpecOptions : IIndicatorSpecOptions
{
    public VolumeMomentumSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Normalized Volume indicator options.
/// </summary>
public sealed class NormalizedVolumeSpecOptions : IIndicatorSpecOptions
{
    public NormalizedVolumeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Stochastic %D indicator options.
/// </summary>
public sealed class StochasticDSpecOptions : IIndicatorSpecOptions
{
    public StochasticDSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Double Smoothed Stochastic indicator options.
/// </summary>
public sealed class DoubleSmoothedStochasticSpecOptions : IIndicatorSpecOptions
{
    public DoubleSmoothedStochasticSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public DoubleSmoothedStochasticSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Premier Stochastic indicator options.
/// </summary>
public sealed class PremierStochasticSpecOptions : IIndicatorSpecOptions
{
    public PremierStochasticSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Close-to-Close Volatility indicator options.
/// </summary>
public sealed class CloseToCloseVolatilitySpecOptions : IIndicatorSpecOptions
{
    public CloseToCloseVolatilitySpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Parkinson Volatility indicator options.
/// </summary>
public sealed class ParkinsonVolatilitySpecOptions : IIndicatorSpecOptions
{
    public ParkinsonVolatilitySpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Garman-Klass Volatility indicator options.
/// </summary>
public sealed class GarmanKlassVolatilitySpecOptions : IIndicatorSpecOptions
{
    public GarmanKlassVolatilitySpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public GarmanKlassVolatilitySpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

// ========== Batch 5 SpecOptions ==========

/// <summary>
/// Average Daily Range indicator options.
/// </summary>
public sealed class AdrSpecOptions : IIndicatorSpecOptions
{
    public AdrSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Bollinger Bands Middle Band indicator options.
/// </summary>
public sealed class BollingerBandsMiddleSpecOptions : IIndicatorSpecOptions
{
    public BollingerBandsMiddleSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// VPCI (Volume Price Confirmation Indicator) options.
/// </summary>
public sealed class VpciSpecOptions : IIndicatorSpecOptions
{
    public VpciSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Keltner Channel Middle indicator options.
/// </summary>
public sealed class KeltnerChannelMiddleSpecOptions : IIndicatorSpecOptions
{
    public KeltnerChannelMiddleSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public KeltnerChannelMiddleSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trend Detection indicator options.
/// </summary>
public sealed class TrendDetectionSpecOptions : IIndicatorSpecOptions
{
    public TrendDetectionSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: TrendDetectionIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Price Channel Middle indicator options.
/// </summary>
public sealed class PriceChannelMiddleSpecOptions : IIndicatorSpecOptions
{
    public PriceChannelMiddleSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Swing Index indicator options.
/// </summary>
public sealed class SwingIndexSpecOptions : IIndicatorSpecOptions
{
    public SwingIndexSpecOptions(double limitMove = 0)
    {
        LimitMove = limitMove;
    }

    public double LimitMove { get; }
}

/// <summary>
/// Accumulative Swing Index indicator options.
/// </summary>
public sealed class AccumulativeSwingIndexSpecOptions : IIndicatorSpecOptions
{
    public AccumulativeSwingIndexSpecOptions(double limitMove = 0, int length = 14)
        : this(limitMove, length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public AccumulativeSwingIndexSpecOptions(double limitMove, int length, MovingAvgType maType)
    {
        LimitMove = limitMove;
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public double LimitMove { get; }
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// ZigZag indicator options.
/// </summary>
public sealed class ZigZagSpecOptions : IIndicatorSpecOptions
{
    public ZigZagSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Pivot Point indicator options.
/// </summary>
public sealed class PivotPointSpecOptions : IIndicatorSpecOptions
{
    public PivotPointSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: FloorPivotPoints has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Range indicator options.
/// </summary>
public sealed class RangeSpecOptions : IIndicatorSpecOptions
{
    public RangeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: Range has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Price Momentum indicator options.
/// </summary>
public sealed class PriceMomentumSpecOptions : IIndicatorSpecOptions
{
    public PriceMomentumSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// MFI Core indicator options.
/// </summary>
public sealed class MfiCoreSpecOptions : IIndicatorSpecOptions
{
    public MfiCoreSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Twiggs Money Flow indicator options.
/// </summary>
public sealed class TwiggsMoneyFlowSpecOptions : IIndicatorSpecOptions
{
    public TwiggsMoneyFlowSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TwiggsMoneyFlowSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Demand Index indicator options.
/// </summary>
public sealed class DemandIndexSpecOptions : IIndicatorSpecOptions
{
    public DemandIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Directional Trend Index indicator options.
/// </summary>
public sealed class DirectionalTrendIndexSpecOptions : IIndicatorSpecOptions
{
    public DirectionalTrendIndexSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public DirectionalTrendIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ergodic Candlestick Oscillator indicator options.
/// </summary>
public sealed class ErgodicCandlestickOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ErgodicCandlestickOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ErgodicCandlestickOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Linear Regression Intercept indicator options.
/// </summary>
public sealed class LinRegInterceptSpecOptions : IIndicatorSpecOptions
{
    public LinRegInterceptSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Elder Impulse System indicator options.
/// </summary>
public sealed class ElderImpulseSystemSpecOptions : IIndicatorSpecOptions
{
    public ElderImpulseSystemSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Mass Thrust indicator options.
/// </summary>
public sealed class MassThrustSpecOptions : IIndicatorSpecOptions
{
    public MassThrustSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Williams AD indicator options.
/// </summary>
public sealed class WilliamsADSpecOptions : IIndicatorSpecOptions
{
    public WilliamsADSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: WilliamsAccumulationDistribution has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Cumulative Volume Index indicator options.
/// </summary>
public sealed class CumulativeVolumeIndexSpecOptions : IIndicatorSpecOptions
{
    public CumulativeVolumeIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: CumulativeVolumeIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Volume Price Trend indicator options.
/// </summary>
public sealed class VolumePriceTrendSpecOptions : IIndicatorSpecOptions
{
    public VolumePriceTrendSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Elder Ray Bull Power indicator options.
/// </summary>
public sealed class ElderRayBullPowerSpecOptions : IIndicatorSpecOptions
{
    public ElderRayBullPowerSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Elder Ray Bear Power indicator options.
/// </summary>
public sealed class ElderRayBearPowerSpecOptions : IIndicatorSpecOptions
{
    public ElderRayBearPowerSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Volume Weighted RSI indicator options.
/// </summary>
public sealed class VolumeWeightedRsiSpecOptions : IIndicatorSpecOptions
{
    public VolumeWeightedRsiSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Chande Composite Momentum Index indicator options.
/// </summary>
public sealed class ChandeCompositeMomentumIndexSpecOptions : IIndicatorSpecOptions
{
    public ChandeCompositeMomentumIndexSpecOptions(int shortLength = 3, int longLength = 10)
    {
        ShortLength = Math.Max(1, shortLength);
        LongLength = Math.Max(1, longLength);
    }

    [Obsolete("Has no effect: ChandeCompositeMomentumIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int ShortLength { get; }
    [Obsolete("Has no effect: ChandeCompositeMomentumIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int LongLength { get; }
}

/// <summary>
/// Chande Kroll R-Squared Index indicator options.
/// </summary>
public sealed class ChandeKrollRSquaredIndexSpecOptions : IIndicatorSpecOptions
{
    public ChandeKrollRSquaredIndexSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ChandeKrollRSquaredIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Bayesian Oscillator indicator options.
/// </summary>
public sealed class BayesianOscillatorSpecOptions : IIndicatorSpecOptions
{
    public BayesianOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public BayesianOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Anchored Momentum indicator options.
/// </summary>
public sealed class AnchoredMomentumSpecOptions : IIndicatorSpecOptions
{
    public AnchoredMomentumSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public AnchoredMomentumSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chartmill Value Indicator options.
/// </summary>
public sealed class ChartmillValueIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ChartmillValueIndicatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ChartmillValueIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Center of Linearity indicator options.
/// </summary>
public sealed class CenterOfLinearitySpecOptions : IIndicatorSpecOptions
{
    public CenterOfLinearitySpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Breakout RSI indicator options.
/// </summary>
public sealed class BreakoutRsiSpecOptions : IIndicatorSpecOptions
{
    public BreakoutRsiSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Asymmetrical RSI indicator options.
/// </summary>
public sealed class AsymmetricalRsiSpecOptions : IIndicatorSpecOptions
{
    public AsymmetricalRsiSpecOptions(int upLength = 14, int downLength = 7)
    {
        UpLength = Math.Max(1, upLength);
        DownLength = Math.Max(1, downLength);
    }

    public int UpLength { get; }
    [Obsolete("Has no effect: AsymmetricalRelativeStrengthIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int DownLength { get; }
}

/// <summary>
/// Adaptive Stochastic indicator options.
/// </summary>
public sealed class AdaptiveStochasticSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveStochasticSpecOptions(int minLength = 5, int maxLength = 20)
    {
        MinLength = Math.Max(1, minLength);
        MaxLength = Math.Max(1, maxLength);
    }

    public int MinLength { get; }
    public int MaxLength { get; }
}

/// <summary>
/// Adaptive RSI indicator options.
/// </summary>
public sealed class AdaptiveRsiSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveRsiSpecOptions(int minLength = 5, int maxLength = 20)
    {
        MinLength = Math.Max(1, minLength);
        MaxLength = Math.Max(1, maxLength);
    }

    [Obsolete("Has no effect: AdaptiveRelativeStrengthIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int MinLength { get; }
    [Obsolete("Has no effect: AdaptiveRelativeStrengthIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int MaxLength { get; }
}

/// <summary>
/// Chande Trend Score indicator options.
/// </summary>
public sealed class ChandeTrendScoreSpecOptions : IIndicatorSpecOptions
{
    public ChandeTrendScoreSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Chop Zone indicator options.
/// </summary>
public sealed class ChopZoneSpecOptions : IIndicatorSpecOptions
{
    public ChopZoneSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ChopZoneSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Auto Line indicator options.
/// </summary>
public sealed class AutoLineSpecOptions : IIndicatorSpecOptions
{
    public AutoLineSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Auto Line With Drift indicator options.
/// </summary>
public sealed class AutoLineWithDriftSpecOptions : IIndicatorSpecOptions
{
    public AutoLineWithDriftSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Auto Filter indicator options.
/// </summary>
public sealed class AutoFilterSpecOptions : IIndicatorSpecOptions
{
    public AutoFilterSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public AutoFilterSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Buff Average indicator options.
/// </summary>
public sealed class BuffAverageSpecOptions : IIndicatorSpecOptions
{
    public BuffAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Bryant Adaptive Moving Average indicator options.
/// </summary>
public sealed class BryantAdaptiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public BryantAdaptiveMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// ATR Trailing Stops indicator options.
/// </summary>
public sealed class AtrTrailingStopsSpecOptions : IIndicatorSpecOptions
{
    public AtrTrailingStopsSpecOptions(int length = 14, double multiplier = 3)
        : this(length, multiplier, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public AtrTrailingStopsSpecOptions(int length, double multiplier, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
        MaType = maType;
    }

    public int Length { get; }
    public double Multiplier { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Compound Ratio Moving Average indicator options.
/// </summary>
public sealed class CompoundRatioMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public CompoundRatioMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public CompoundRatioMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Conditional Accumulator indicator options.
/// </summary>
public sealed class ConditionalAccumulatorSpecOptions : IIndicatorSpecOptions
{
    public ConditionalAccumulatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ConditionalAccumulatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ahrens Moving Average indicator options.
/// </summary>
public sealed class AhrensMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public AhrensMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Rogers-Satchell Volatility indicator options.
/// </summary>
public sealed class RogersSatchellVolatilitySpecOptions : IIndicatorSpecOptions
{
    public RogersSatchellVolatilitySpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Yang-Zhang Volatility indicator options.
/// </summary>
public sealed class YangZhangVolatilitySpecOptions : IIndicatorSpecOptions
{
    public YangZhangVolatilitySpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Calmar Ratio indicator options.
/// </summary>
public sealed class CalmarRatioSpecOptions : IIndicatorSpecOptions
{
    public CalmarRatioSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Downside Deviation indicator options.
/// </summary>
public sealed class DownsideDeviationSpecOptions : IIndicatorSpecOptions
{
    public DownsideDeviationSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// ATR Channel Width indicator options.
/// </summary>
public sealed class AtrChannelWidthSpecOptions : IIndicatorSpecOptions
{
    public AtrChannelWidthSpecOptions(int length = 14, double multiplier = 2)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
    }

    public int Length { get; }
    public double Multiplier { get; }
}

/// <summary>
/// Commodity Selection Index indicator options.
/// </summary>
public sealed class CommoditySelectionIndexSpecOptions : IIndicatorSpecOptions
{
    public CommoditySelectionIndexSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public CommoditySelectionIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Alpha Decreasing EMA indicator options.
/// </summary>
public sealed class AlphaDecreasingEmaSpecOptions : IIndicatorSpecOptions
{
    public AlphaDecreasingEmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: AlphaDecreasingExponentialMovingAverage has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Adaptive EMA indicator options.
/// </summary>
public sealed class AdaptiveEmaSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveEmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Autonomous Recursive MA indicator options.
/// </summary>
public sealed class AutonomousRecursiveMaSpecOptions : IIndicatorSpecOptions
{
    public AutonomousRecursiveMaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Adaptive Least Squares indicator options.
/// </summary>
public sealed class AdaptiveLeastSquaresSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveLeastSquaresSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// ATR Filtered EMA indicator options.
/// </summary>
public sealed class AtrFilteredEmaSpecOptions : IIndicatorSpecOptions
{
    public AtrFilteredEmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Median MA indicator options.
/// </summary>
public sealed class MedianMaSpecOptions : IIndicatorSpecOptions
{
    public MedianMaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Volume Adjusted MA indicator options.
/// </summary>
public sealed class VolumeAdjustedMaSpecOptions : IIndicatorSpecOptions
{
    public VolumeAdjustedMaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Quadratic WMA indicator options.
/// </summary>
public sealed class QuadraticWmaSpecOptions : IIndicatorSpecOptions
{
    public QuadraticWmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Parabolic WMA indicator options.
/// </summary>
public sealed class ParabolicWmaSpecOptions : IIndicatorSpecOptions
{
    public ParabolicWmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Smoothed Williams %R indicator options.
/// </summary>
public sealed class SmoothedWilliamsRSpecOptions : IIndicatorSpecOptions
{
    public SmoothedWilliamsRSpecOptions(int length = 14, int smoothLength = 3)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
    }

    public int Length { get; }
    public int SmoothLength { get; }
}

/// <summary>
/// Price Oscillator Percent indicator options.
/// </summary>
public sealed class PriceOscillatorPercentSpecOptions : IIndicatorSpecOptions
{
    public PriceOscillatorPercentSpecOptions(int shortLength = 10, int longLength = 20)
    {
        ShortLength = Math.Max(1, shortLength);
        LongLength = Math.Max(1, longLength);
    }

    public int ShortLength { get; }
    public int LongLength { get; }
}

/// <summary>
/// Normalized MACD indicator options.
/// </summary>
public sealed class NormalizedMacdSpecOptions : IIndicatorSpecOptions
{
    public NormalizedMacdSpecOptions(int fastLength = 12, int slowLength = 26)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
}

/// <summary>
/// Relative Vigor Index Signal indicator options.
/// </summary>
public sealed class RelativeVigorIndexSignalSpecOptions : IIndicatorSpecOptions
{
    public RelativeVigorIndexSignalSpecOptions(int length = 10, int signalLength = 4)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
    }

    public int Length { get; }
    [Obsolete("Has no effect: RelativeVigorIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int SignalLength { get; }
}

/// <summary>
/// Volume Momentum Oscillator indicator options.
/// </summary>
public sealed class VolumeMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public VolumeMomentumOscillatorSpecOptions(int shortLength = 5, int longLength = 20)
    {
        ShortLength = Math.Max(1, shortLength);
        LongLength = Math.Max(1, longLength);
    }

    public int ShortLength { get; }
    public int LongLength { get; }
}

/// <summary>
/// Trend Continuation Factor indicator options.
/// </summary>
public sealed class TrendContinuationFactorSpecOptions : IIndicatorSpecOptions
{
    public TrendContinuationFactorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Trend Persistence Rate indicator options.
/// </summary>
public sealed class TrendPersistenceRateSpecOptions : IIndicatorSpecOptions
{
    public TrendPersistenceRateSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Inertia indicator options.
/// </summary>
public sealed class InertiaSpecOptions : IIndicatorSpecOptions
{
    public InertiaSpecOptions(int rviLength = 14, int smoothLength = 20)
    {
        RviLength = Math.Max(1, rviLength);
        SmoothLength = Math.Max(1, smoothLength);
    }

    public int RviLength { get; }
    public int SmoothLength { get; }
}

/// <summary>
/// Standard Deviation Channel indicator options.
/// </summary>
public sealed class StandardDeviationChannelSpecOptions : IIndicatorSpecOptions
{
    public StandardDeviationChannelSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Standard Deviation Volatility indicator options.
/// </summary>
public sealed class StandardDeviationVolatilitySpecOptions : IIndicatorSpecOptions
{
    public StandardDeviationVolatilitySpecOptions(int length = 20, int annualizationFactor = 252)
        : this(length, annualizationFactor, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StandardDeviationVolatilitySpecOptions(int length, int annualizationFactor, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        AnnualizationFactor = Math.Max(1, annualizationFactor);
        MaType = maType;
    }

    public int Length { get; }
    [Obsolete("Has no effect: StandardDeviationVolatility has no parameter this option could set. It will be removed in the next major version.")]
    public int AnnualizationFactor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Average True Range Channel indicator options.
/// </summary>
public sealed class AverageTrueRangeChannelSpecOptions : IIndicatorSpecOptions
{
    public AverageTrueRangeChannelSpecOptions(int length = 14, double multiplier = 2)
        : this(length, multiplier, MovingAvgType.SimpleMovingAverage)
    {
    }

    public AverageTrueRangeChannelSpecOptions(int length, double multiplier, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
        MaType = maType;
    }

    public int Length { get; }
    public double Multiplier { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volatility Ratio indicator options.
/// </summary>
public sealed class VolatilityRatioSpecOptions : IIndicatorSpecOptions
{
    public VolatilityRatioSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public VolatilityRatioSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volatility Stop indicator options.
/// </summary>
public sealed class VolatilityStopSpecOptions : IIndicatorSpecOptions
{
    public VolatilityStopSpecOptions(int length = 14, double multiplier = 2)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
    }

    public int Length { get; }
    public double Multiplier { get; }
}

/// <summary>
/// Bollinger Bands Percent B indicator options.
/// </summary>
public sealed class BollingerBandsPercentBSpecOptions : IIndicatorSpecOptions
{
    public BollingerBandsPercentBSpecOptions(int length = 20, double multiplier = 2)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
    }

    public int Length { get; }
    public double Multiplier { get; }
}

/// <summary>
/// Bollinger Bands ATR indicator options.
/// </summary>
public sealed class BollingerBandsAtrSpecOptions : IIndicatorSpecOptions
{
    public BollingerBandsAtrSpecOptions(int length = 20, double multiplier = 2)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
    }

    public int Length { get; }
    public double Multiplier { get; }
}

/// <summary>
/// Chande Momentum Oscillator Absolute indicator options.
/// </summary>
public sealed class ChandeMomentumOscillatorAbsoluteSpecOptions : IIndicatorSpecOptions
{
    public ChandeMomentumOscillatorAbsoluteSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Percent Change indicator options.
/// </summary>
public sealed class PercentChangeSpecOptions : IIndicatorSpecOptions
{
    public PercentChangeSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Price Change indicator options (no length parameter variant).
/// </summary>
public sealed class PriceChangeSpecOptions : IIndicatorSpecOptions
{
    public PriceChangeSpecOptions() { }
}

/// <summary>
/// Mid Range indicator options (no length parameter variant).
/// </summary>
public sealed class MidRangeSpecOptions : IIndicatorSpecOptions
{
    public MidRangeSpecOptions() { }
}

/// <summary>
/// OHLC Average indicator options (no length parameter variant).
/// </summary>
public sealed class OhlcAverageSpecOptions : IIndicatorSpecOptions
{
    public OhlcAverageSpecOptions() { }
}

/// <summary>
/// HLC Average indicator options (no length parameter variant).
/// </summary>
public sealed class HlcAverageSpecOptions : IIndicatorSpecOptions
{
    public HlcAverageSpecOptions() { }
}

/// <summary>
/// Double Smoothed Momenta indicator options.
/// </summary>
public sealed class DoubleSmoothedMomentaSpecOptions : IIndicatorSpecOptions
{
    // The batch Double Smoothed Momenta's own defaults: a lookback of 2, then EMAs of 5 and 25. The 1, 25 and 13
    // these used to default to described a different formula, and as a lookback 1 leaves a zero range.
    public DoubleSmoothedMomentaSpecOptions(int momentumLength = 2, int firstSmooth = 5, int secondSmooth = 25)
        : this(momentumLength, firstSmooth, secondSmooth, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public DoubleSmoothedMomentaSpecOptions(int momentumLength, int firstSmooth, int secondSmooth, MovingAvgType maType)
    {
        MomentumLength = Math.Max(1, momentumLength);
        FirstSmooth = Math.Max(1, firstSmooth);
        SecondSmooth = Math.Max(1, secondSmooth);
        MaType = maType;
    }

    public int MomentumLength { get; }
    public int FirstSmooth { get; }
    public int SecondSmooth { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// High Low Index indicator options.
/// </summary>
public sealed class HighLowIndexSpecOptions : IIndicatorSpecOptions
{
    public HighLowIndexSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public HighLowIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Market Facilitation Index indicator options.
/// </summary>
public sealed class MarketFacilitationIndexSpecOptions : IIndicatorSpecOptions
{
    public MarketFacilitationIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: MarketFacilitationIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Trend Score indicator options.
/// </summary>
public sealed class TrendScoreSpecOptions : IIndicatorSpecOptions
{
    public TrendScoreSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Median Value indicator options.
/// </summary>
public sealed class MedianValueSpecOptions : IIndicatorSpecOptions
{
    public MedianValueSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Log Returns indicator options.
/// </summary>
public sealed class LogReturnsSpecOptions : IIndicatorSpecOptions
{
    public LogReturnsSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Simple Returns indicator options.
/// </summary>
public sealed class SimpleReturnsSpecOptions : IIndicatorSpecOptions
{
    public SimpleReturnsSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Cumulative Sum indicator options (no length parameter variant).
/// </summary>
public sealed class CumulativeSumSpecOptions : IIndicatorSpecOptions
{
    public CumulativeSumSpecOptions() { }
}

/// <summary>
/// Rolling Max indicator options.
/// </summary>
public sealed class RollingMaxSpecOptions : IIndicatorSpecOptions
{
    public RollingMaxSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Rolling Min indicator options.
/// </summary>
public sealed class RollingMinSpecOptions : IIndicatorSpecOptions
{
    public RollingMinSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Price Position indicator options.
/// </summary>
public sealed class PricePositionSpecOptions : IIndicatorSpecOptions
{
    public PricePositionSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// ATR Percent indicator options.
/// </summary>
public sealed class AtrPercentSpecOptions : IIndicatorSpecOptions
{
    public AtrPercentSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Forecast Oscillator indicator options.
/// </summary>
public sealed class ForecastOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ForecastOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ForecastOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Repulse indicator options.
/// </summary>
public sealed class RepulseSpecOptions : IIndicatorSpecOptions
{
    public RepulseSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public RepulseSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Gann Hi-Lo Activator indicator options.
/// </summary>
public sealed class GannHiLoActivatorSpecOptions : IIndicatorSpecOptions
{
    public GannHiLoActivatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public GannHiLoActivatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Half Trend indicator options.
/// </summary>
public sealed class HalfTrendSpecOptions : IIndicatorSpecOptions
{
    public HalfTrendSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public HalfTrendSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

// ========== Batch 6 SpecOptions ==========

/// <summary>
/// Chande Momentum Oscillator Absolute Average indicator options.
/// </summary>
public sealed class ChandeMomentumOscillatorAbsoluteAverageSpecOptions : IIndicatorSpecOptions
{
    public ChandeMomentumOscillatorAbsoluteAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: ChandeMomentumOscillatorAbsoluteAverage has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Chande Momentum Oscillator Average indicator options.
/// </summary>
public sealed class ChandeMomentumOscillatorAverageSpecOptions : IIndicatorSpecOptions
{
    public ChandeMomentumOscillatorAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: ChandeMomentumOscillatorAverage has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Double Stochastic Oscillator indicator options.
/// </summary>
public sealed class DoubleStochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DoubleStochasticOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DoubleStochasticOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// DT Oscillator indicator options.
/// </summary>
public sealed class DTOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DTOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public DTOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Compare Price Momentum Oscillator indicator options.
/// </summary>
[Obsolete("Compares a stock against a market series, so it cannot be computed from one series. Use " +
    "IndicatorCatalog.ComparePriceMomentumOscillator, which passes both through MultiStockIndicatorOptions. " +
    "It will be removed in the next major version.")]
public sealed class ComparePriceMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ComparePriceMomentumOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Daily Average Price Delta indicator options.
/// </summary>
public sealed class DailyAveragePriceDeltaSpecOptions : IIndicatorSpecOptions
{
    public DailyAveragePriceDeltaSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DailyAveragePriceDeltaSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Demand Oscillator indicator options.
/// </summary>
public sealed class DemandOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DemandOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public DemandOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    [Obsolete("Has no effect: DemandOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Double Smoothed Relative Strength Index indicator options.
/// </summary>
public sealed class DoubleSmoothedRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public DoubleSmoothedRelativeStrengthIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: DoubleSmoothedRelativeStrengthIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Dynamic Momentum Oscillator indicator options.
/// </summary>
public sealed class DynamicMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DynamicMomentumOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DynamicMomentumOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Average Money Flow Oscillator indicator options.
/// </summary>
public sealed class AverageMoneyFlowOscillatorSpecOptions : IIndicatorSpecOptions
{
    public AverageMoneyFlowOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public AverageMoneyFlowOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// DMI Stochastic indicator options.
/// </summary>
public sealed class DMIStochasticSpecOptions : IIndicatorSpecOptions
{
    public DMIStochasticSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DMIStochasticSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// CCT Stoch Relative Strength Index indicator options.
/// </summary>
public sealed class CCTStochRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public CCTStochRelativeStrengthIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: CCTStochRelativeStrengthIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Bilateral Stochastic Oscillator indicator options.
/// </summary>
public sealed class BilateralStochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public BilateralStochasticOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public BilateralStochasticOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chande Momentum Oscillator Average Disparity Index indicator options.
/// </summary>
public sealed class ChandeMomentumOscillatorAverageDisparityIndexSpecOptions : IIndicatorSpecOptions
{
    public ChandeMomentumOscillatorAverageDisparityIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: ChandeMomentumOscillatorAverageDisparityIndex has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Chande Momentum Oscillator Filter indicator options.
/// </summary>
public sealed class ChandeMomentumOscillatorFilterSpecOptions : IIndicatorSpecOptions
{
    public ChandeMomentumOscillatorFilterSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public ChandeMomentumOscillatorFilterSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// DiNapoli Percentage Price Oscillator indicator options.
/// </summary>
public sealed class DiNapoliPercentagePriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DiNapoliPercentagePriceOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: DiNapoliPercentagePriceOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// DiNapoli Preferred Stochastic Oscillator indicator options.
/// </summary>
public sealed class DiNapoliPreferredStochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DiNapoliPreferredStochasticOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ergodic Percentage Price Oscillator indicator options.
/// </summary>
public sealed class ErgodicPercentagePriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ErgodicPercentagePriceOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ErgodicPercentagePriceOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Fast Slow Kurtosis Oscillator indicator options.
/// </summary>
public sealed class FastSlowKurtosisOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FastSlowKurtosisOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Fast Slow RSI Oscillator indicator options.
/// </summary>
public sealed class FastSlowRsiOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FastSlowRsiOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: FastandSlowRelativeStrengthIndexOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Fast Slow Stochastic Oscillator indicator options.
/// </summary>
public sealed class FastSlowStochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FastSlowStochasticOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: FastandSlowStochasticOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// G Oscillator indicator options.
/// </summary>
public sealed class GOscillatorSpecOptions : IIndicatorSpecOptions
{
    public GOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Gann Swing Oscillator indicator options.
/// </summary>
public sealed class GannSwingOscillatorSpecOptions : IIndicatorSpecOptions
{
    public GannSwingOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Gann Trend Oscillator indicator options.
/// </summary>
public sealed class GannTrendOscillatorSpecOptions : IIndicatorSpecOptions
{
    public GannTrendOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Firefly Oscillator indicator options.
/// </summary>
public sealed class FireflyOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FireflyOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ZeroLagExponentialMovingAverage)
    {
    }

    public FireflyOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Fisher Transform Stochastic Oscillator indicator options.
/// </summary>
public sealed class FisherTransformStochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FisherTransformStochasticOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Karobein Oscillator indicator options.
/// </summary>
public sealed class KarobeinOscillatorSpecOptions : IIndicatorSpecOptions
{
    public KarobeinOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public KarobeinOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Grover Llorens Cycle Oscillator indicator options.
/// </summary>
public sealed class GroverLlorensCycleOscillatorSpecOptions : IIndicatorSpecOptions
{
    public GroverLlorensCycleOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public GroverLlorensCycleOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Impulse Percentage Price Oscillator indicator options.
/// </summary>
public sealed class ImpulsePercentagePriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ImpulsePercentagePriceOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Midpoint Oscillator indicator options.
/// </summary>
public sealed class MidpointOscillatorSpecOptions : IIndicatorSpecOptions
{
    public MidpointOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MidpointOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Mirrored Percentage Price Oscillator indicator options.
/// </summary>
public sealed class MirroredPercentagePriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public MirroredPercentagePriceOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Mobility Oscillator indicator options.
/// </summary>
public sealed class MobilityOscillatorSpecOptions : IIndicatorSpecOptions
{
    public MobilityOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public MobilityOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Percent Change Oscillator indicator options.
/// </summary>
public sealed class PercentChangeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PercentChangeOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public PercentChangeOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Price Cycle Oscillator indicator options.
/// </summary>
public sealed class PriceCycleOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PriceCycleOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PriceCycleOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Price Volume Oscillator indicator options.
/// </summary>
public sealed class PriceVolumeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PriceVolumeOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: PriceVolumeOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Projection Oscillator indicator options.
/// </summary>
public sealed class ProjectionOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ProjectionOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public ProjectionOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Rainbow Oscillator indicator options.
/// </summary>
public sealed class RainbowOscillatorSpecOptions : IIndicatorSpecOptions
{
    public RainbowOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public RainbowOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Regression Oscillator indicator options.
/// </summary>
public sealed class RegressionOscillatorSpecOptions : IIndicatorSpecOptions
{
    public RegressionOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Rex Oscillator indicator options.
/// </summary>
public sealed class RexOscillatorSpecOptions : IIndicatorSpecOptions
{
    public RexOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public RexOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Sentiment Zone Oscillator indicator options.
/// </summary>
public sealed class SentimentZoneOscillatorSpecOptions : IIndicatorSpecOptions
{
    public SentimentZoneOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.TripleExponentialMovingAverage)
    {
    }

    public SentimentZoneOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Wave Trend Oscillator indicator options.
/// </summary>
public sealed class WaveTrendOscillatorSpecOptions : IIndicatorSpecOptions
{
    public WaveTrendOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// WAMI Oscillator indicator options.
/// </summary>
public sealed class WamiOscillatorSpecOptions : IIndicatorSpecOptions
{
    public WamiOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public WamiOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volume Accumulation Oscillator indicator options.
/// </summary>
public sealed class VolumeAccumulationOscillatorSpecOptions : IIndicatorSpecOptions
{
    public VolumeAccumulationOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Kase Peak Oscillator V1 indicator options.
/// </summary>
public sealed class KasePeakOscillatorV1SpecOptions : IIndicatorSpecOptions
{
    public KasePeakOscillatorV1SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Varadi Oscillator indicator options.
/// </summary>
public sealed class VaradiOscillatorSpecOptions : IIndicatorSpecOptions
{
    public VaradiOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VaradiOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Prime Number Oscillator indicator options.
/// </summary>
public sealed class PrimeNumberOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PrimeNumberOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Trigonometric Oscillator indicator options.
/// </summary>
public sealed class TrigonometricOscillatorSpecOptions : IIndicatorSpecOptions
{
    public TrigonometricOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ultimate Trader Oscillator indicator options.
/// </summary>
public sealed class UltimateTraderOscillatorSpecOptions : IIndicatorSpecOptions
{
    public UltimateTraderOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public UltimateTraderOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Smoothed Delta Ratio Oscillator indicator options.
/// </summary>
public sealed class SmoothedDeltaRatioOscillatorSpecOptions : IIndicatorSpecOptions
{
    public SmoothedDeltaRatioOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SmoothedDeltaRatioOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Fast Slow Degree Oscillator indicator options.
/// </summary>
public sealed class FastSlowDegreeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FastSlowDegreeOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public FastSlowDegreeOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Robust Weighting Oscillator indicator options.
/// </summary>
public sealed class RobustWeightingOscillatorSpecOptions : IIndicatorSpecOptions
{
    public RobustWeightingOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public RobustWeightingOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Kase Peak Oscillator V2 indicator options.
/// </summary>
public sealed class KasePeakOscillatorV2SpecOptions : IIndicatorSpecOptions
{
    public KasePeakOscillatorV2SpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public KasePeakOscillatorV2SpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Stochastic Custom Oscillator indicator options.
/// </summary>
public sealed class StochasticCustomOscillatorSpecOptions : IIndicatorSpecOptions
{
    public StochasticCustomOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StochasticCustomOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Pivot Detector Oscillator indicator options.
/// </summary>
public sealed class PivotDetectorOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PivotDetectorOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PivotDetectorOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    [Obsolete("Has no effect: PivotDetectorOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Tick Line Momentum Oscillator indicator options.
/// </summary>
public sealed class TickLineMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public TickLineMomentumOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TickLineMomentumOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Support And Resistance Oscillator indicator options.
/// </summary>
public sealed class SupportAndResistanceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public SupportAndResistanceOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: SupportAndResistanceOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Trading Made More Simpler Oscillator indicator options.
/// </summary>
public sealed class TradingMadeMoreSimplerOscillatorSpecOptions : IIndicatorSpecOptions
{
    public TradingMadeMoreSimplerOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Nth Order Differencing Oscillator indicator options.
/// </summary>
public sealed class NthOrderDifferencingOscillatorSpecOptions : IIndicatorSpecOptions
{
    public NthOrderDifferencingOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Osc Oscillator indicator options.
/// </summary>
public sealed class OscOscillatorSpecOptions : IIndicatorSpecOptions
{
    public OscOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public OscOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Center Of Gravity Oscillator indicator options.
/// </summary>
public sealed class EhlersCenterOfGravityOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersCenterOfGravityOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Decycler Oscillator V1 indicator options.
/// </summary>
public sealed class EhlersDecyclerOscillatorV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersDecyclerOscillatorV1SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Hilbert Oscillator indicator options.
/// </summary>
public sealed class EhlersHilbertOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersHilbertOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Universal Oscillator indicator options.
/// </summary>
public sealed class EhlersUniversalOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersUniversalOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersUniversalOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Recursive Median Oscillator indicator options.
/// </summary>
public sealed class EhlersRecursiveMedianOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersRecursiveMedianOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: EhlersRecursiveMedianOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Ehlers Stochastic Center Of Gravity Oscillator indicator options.
/// </summary>
public sealed class EhlersStochasticCenterOfGravityOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersStochasticCenterOfGravityOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Fisherized Deviation Scaled Oscillator indicator options.
/// </summary>
public sealed class EhlersFisherizedDeviationScaledOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersFisherizedDeviationScaledOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Adaptive Center Of Gravity Oscillator indicator options.
/// </summary>
public sealed class EhlersAdaptiveCenterOfGravityOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveCenterOfGravityOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Vervoort Smoothed Oscillator indicator options.
/// </summary>
public sealed class VervoortSmoothedOscillatorSpecOptions : IIndicatorSpecOptions
{
    public VervoortSmoothedOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: VervoortSmoothedOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Relative Difference Of Squares Oscillator indicator options.
/// </summary>
public sealed class RelativeDifferenceOfSquaresOscillatorSpecOptions : IIndicatorSpecOptions
{
    public RelativeDifferenceOfSquaresOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Linear Quadratic Convergence Divergence Oscillator indicator options.
/// </summary>
public sealed class LinearQuadraticConvergenceDivergenceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public LinearQuadraticConvergenceDivergenceOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Stationary Extrapolated Levels Oscillator indicator options.
/// </summary>
public sealed class StationaryExtrapolatedLevelsOscillatorSpecOptions : IIndicatorSpecOptions
{
    public StationaryExtrapolatedLevelsOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StationaryExtrapolatedLevelsOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Percentage Price Oscillator Leader indicator options.
/// </summary>
public sealed class PercentagePriceOscillatorLeaderSpecOptions : IIndicatorSpecOptions
{
    public PercentagePriceOscillatorLeaderSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Kaufman Adaptive Correlation Oscillator indicator options.
/// </summary>
public sealed class KaufmanAdaptiveCorrelationOscillatorSpecOptions : IIndicatorSpecOptions
{
    public KaufmanAdaptiveCorrelationOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Stochastic MACD Oscillator indicator options.
/// </summary>
public sealed class StochasticMacdOscillatorSpecOptions : IIndicatorSpecOptions
{
    public StochasticMacdOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// McClellan Oscillator indicator options.
/// </summary>
public sealed class McClellanOscillatorSpecOptions : IIndicatorSpecOptions
{
    public McClellanOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public McClellanOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    [Obsolete("Has no effect: McClellanOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Vervoort Heiken Ashi Candlestick Oscillator indicator options.
/// </summary>
public sealed class VervoortHeikenAshiCandlestickOscillatorSpecOptions : IIndicatorSpecOptions
{
    public VervoortHeikenAshiCandlestickOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Vervoort Heiken Ashi Long Term Candlestick Oscillator indicator options.
/// </summary>
public sealed class VervoortHeikenAshiLongTermCandlestickOscillatorSpecOptions : IIndicatorSpecOptions
{
    public VervoortHeikenAshiLongTermCandlestickOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Decision Point Breadth Swenlin Trading Oscillator indicator options.
/// </summary>
public sealed class DecisionPointBreadthSwenlinTradingOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DecisionPointBreadthSwenlinTradingOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Decision Point Price Momentum Oscillator indicator options.
/// </summary>
public sealed class DecisionPointPriceMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DecisionPointPriceMomentumOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// TFS MBO Percentage Price Oscillator indicator options.
/// </summary>
public sealed class TFSMboPercentagePriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public TFSMboPercentagePriceOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TFSMboPercentagePriceOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    [Obsolete("Has no effect: TFSMboPercentagePriceOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// TFS Volume Oscillator indicator options.
/// </summary>
public sealed class TFSVolumeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public TFSVolumeOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Mass Thrust Oscillator indicator options.
/// </summary>
public sealed class MassThrustOscillatorSpecOptions : IIndicatorSpecOptions
{
    public MassThrustOscillatorSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MassThrustOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

// ========== Batch 7 SpecOptions ==========

/// <summary>
/// Ultimate Moving Average indicator options.
/// </summary>
public sealed class UltimateMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public UltimateMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public UltimateMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    [Obsolete("Has no effect: UltimateMovingAverage has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Symmetrically Weighted Moving Average indicator options.
/// </summary>
public sealed class SymmetricallyWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SymmetricallyWeightedMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Square Root Weighted Moving Average indicator options.
/// </summary>
public sealed class SquareRootWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SquareRootWeightedMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Spencer 15 Point Moving Average indicator options.
/// </summary>
public sealed class Spencer15PointMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public Spencer15PointMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: Spencer15PointMovingAverage has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Spencer 21 Point Moving Average indicator options.
/// </summary>
public sealed class Spencer21PointMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public Spencer21PointMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: Spencer21PointMovingAverage has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Slow Smoothed Moving Average indicator options.
/// </summary>
public sealed class SlowSmoothedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SlowSmoothedMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public SlowSmoothedMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Repulsion Moving Average indicator options.
/// </summary>
public sealed class RepulsionMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public RepulsionMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public RepulsionMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Quick Moving Average indicator options.
/// </summary>
public sealed class QuickMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public QuickMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Better Exponential Moving Average indicator options.
/// </summary>
public sealed class EhlersBetterExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersBetterExponentialMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Deviation Scaled Moving Average indicator options.
/// </summary>
public sealed class EhlersDeviationScaledMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersDeviationScaledMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Hann Moving Average indicator options.
/// </summary>
public sealed class EhlersHannMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersHannMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Triangle Moving Average indicator options.
/// </summary>
public sealed class EhlersTriangleMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersTriangleMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Elastic Volume Weighted Moving Average V1 indicator options.
/// </summary>
public sealed class ElasticVolumeWeightedMovingAverageV1SpecOptions : IIndicatorSpecOptions
{
    public ElasticVolumeWeightedMovingAverageV1SpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ElasticVolumeWeightedMovingAverageV1SpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Holt Exponential Moving Average indicator options.
/// </summary>
public sealed class HoltExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public HoltExponentialMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Pentuple Exponential Moving Average indicator options.
/// </summary>
public sealed class PentupleExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public PentupleExponentialMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Quadruple Exponential Moving Average indicator options.
/// </summary>
public sealed class QuadrupleExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public QuadrupleExponentialMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ichimoku Senkou Span A indicator options.
/// </summary>
public sealed class IchimokuSenkouSpanASpecOptions : IIndicatorSpecOptions
{
    public IchimokuSenkouSpanASpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ichimoku Senkou Span B indicator options.
/// </summary>
public sealed class IchimokuSenkouSpanBSpecOptions : IIndicatorSpecOptions
{
    public IchimokuSenkouSpanBSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ichimoku Chikou Span indicator options.
/// </summary>
public sealed class IchimokuChikouSpanSpecOptions : IIndicatorSpecOptions
{
    public IchimokuChikouSpanSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Williams Fractal Up indicator options.
/// </summary>
public sealed class WilliamsFractalUpSpecOptions : IIndicatorSpecOptions
{
    public WilliamsFractalUpSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Williams Fractal Down indicator options.
/// </summary>
public sealed class WilliamsFractalDownSpecOptions : IIndicatorSpecOptions
{
    public WilliamsFractalDownSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Alligator Jaw indicator options.
/// </summary>
public sealed class AlligatorJawSpecOptions : IIndicatorSpecOptions
{
    public AlligatorJawSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Alligator Teeth indicator options.
/// </summary>
public sealed class AlligatorTeethSpecOptions : IIndicatorSpecOptions
{
    public AlligatorTeethSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Alligator Lips indicator options.
/// </summary>
public sealed class AlligatorLipsSpecOptions : IIndicatorSpecOptions
{
    public AlligatorLipsSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Laguerre Filter indicator options.
/// </summary>
public sealed class EhlersLaguerreFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersLaguerreFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Laguerre RSI indicator options.
/// </summary>
public sealed class EhlersLaguerreRsiSpecOptions : IIndicatorSpecOptions
{
    public EhlersLaguerreRsiSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Zero Lag EMA indicator options.
/// </summary>
public sealed class EhlersZeroLagEmaSpecOptions : IIndicatorSpecOptions
{
    public EhlersZeroLagEmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers FRAMA indicator options.
/// </summary>
public sealed class EhlersFramaSpecOptions : IIndicatorSpecOptions
{
    public EhlersFramaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Inverse Fisher Transform indicator options.
/// </summary>
public sealed class EhlersInverseFisherTransformSpecOptions : IIndicatorSpecOptions
{
    public EhlersInverseFisherTransformSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public EhlersInverseFisherTransformSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Cyber Cycle indicator options.
/// </summary>
public sealed class EhlersCyberCycleSpecOptions : IIndicatorSpecOptions
{
    public EhlersCyberCycleSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Stochastic indicator options.
/// </summary>
public sealed class EhlersStochasticSpecOptions : IIndicatorSpecOptions
{
    public EhlersStochasticSpecOptions(int length)
        : this(length, MovingAvgType.Ehlers2PoleSuperSmootherFilterV1)
    {
    }

    public EhlersStochasticSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Adaptive Laguerre Filter indicator options.
/// </summary>
public sealed class EhlersAdaptiveLaguerreFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveLaguerreFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Coral Trend Indicator options.
/// </summary>
public sealed class CoralTrendIndicatorSpecOptions : IIndicatorSpecOptions
{
    public CoralTrendIndicatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Damped Sine Wave Weighted Filter indicator options.
/// </summary>
public sealed class DampedSineWaveWeightedFilterSpecOptions : IIndicatorSpecOptions
{
    public DampedSineWaveWeightedFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Fibonacci Weighted Moving Average indicator options.
/// </summary>
public sealed class FibonacciWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public FibonacciWeightedMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Generalized Double EMA indicator options.
/// </summary>
public sealed class GeneralizedDoubleEmaSpecOptions : IIndicatorSpecOptions
{
    public GeneralizedDoubleEmaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Geometric Mean Moving Average indicator options.
/// </summary>
public sealed class GeometricMeanMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public GeometricMeanMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Harmonic Mean Moving Average indicator options.
/// </summary>
public sealed class HarmonicMeanMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public HarmonicMeanMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers 2 Pole Butterworth Filter V1 indicator options.
/// </summary>
public sealed class Ehlers2PoleButterworthFilterV1SpecOptions : IIndicatorSpecOptions
{
    public Ehlers2PoleButterworthFilterV1SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers 2 Pole Butterworth Filter V2 indicator options.
/// </summary>
public sealed class Ehlers2PoleButterworthFilterV2SpecOptions : IIndicatorSpecOptions
{
    public Ehlers2PoleButterworthFilterV2SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers 3 Pole Butterworth Filter V1 indicator options.
/// </summary>
public sealed class Ehlers3PoleButterworthFilterV1SpecOptions : IIndicatorSpecOptions
{
    public Ehlers3PoleButterworthFilterV1SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers 3 Pole Butterworth Filter V2 indicator options.
/// </summary>
public sealed class Ehlers3PoleButterworthFilterV2SpecOptions : IIndicatorSpecOptions
{
    public Ehlers3PoleButterworthFilterV2SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers 2 Pole Super Smoother Filter V1 indicator options.
/// </summary>
public sealed class Ehlers2PoleSuperSmootherFilterV1SpecOptions : IIndicatorSpecOptions
{
    public Ehlers2PoleSuperSmootherFilterV1SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers 2 Pole Super Smoother Filter V2 indicator options.
/// </summary>
public sealed class Ehlers2PoleSuperSmootherFilterV2SpecOptions : IIndicatorSpecOptions
{
    public Ehlers2PoleSuperSmootherFilterV2SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers 3 Pole Super Smoother Filter indicator options.
/// </summary>
public sealed class Ehlers3PoleSuperSmootherFilterSpecOptions : IIndicatorSpecOptions
{
    public Ehlers3PoleSuperSmootherFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Decycler indicator options.
/// </summary>
public sealed class EhlersDecyclerSpecOptions : IIndicatorSpecOptions
{
    public EhlersDecyclerSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Hamming Moving Average indicator options.
/// </summary>
public sealed class EhlersHammingMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersHammingMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Leading Indicator options.
/// </summary>
public sealed class EhlersLeadingIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersLeadingIndicatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: EhlersLeadingIndicator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Ehlers High Pass Filter V1 indicator options.
/// </summary>
public sealed class EhlersHighPassFilterV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersHighPassFilterV1SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Distance Weighted Moving Average indicator options.
/// </summary>
public sealed class DistanceWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public DistanceWeightedMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Filter indicator options.
/// </summary>
public sealed class EhlersFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers FIR Filter indicator options.
/// </summary>
public sealed class EhlersFirFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersFirFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: EhlersFiniteImpulseResponseFilter has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Ehlers IIR Filter indicator options.
/// </summary>
public sealed class EhlersIirFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersIirFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Simple Cycle indicator options.
/// </summary>
public sealed class SimpleCycleSpecOptions : IIndicatorSpecOptions
{
    public SimpleCycleSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Simple Lines indicator options.
/// </summary>
public sealed class SimpleLinesSpecOptions : IIndicatorSpecOptions
{
    public SimpleLinesSpecOptions(int length, double multiplier = 10)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
    }

    public int Length { get; }
    public double Multiplier { get; }
}

/// <summary>
/// Double Exponential Smoothing indicator options.
/// </summary>
public sealed class DoubleExponentialSmoothingSpecOptions : IIndicatorSpecOptions
{
    public DoubleExponentialSmoothingSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: DoubleExponentialSmoothing has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Detrended Synthetic Price indicator options.
/// </summary>
public sealed class DetrendedSyntheticPriceSpecOptions : IIndicatorSpecOptions
{
    public DetrendedSyntheticPriceSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Belkhayate Timing indicator options.
/// </summary>
public sealed class BelkhayateTimingSpecOptions : IIndicatorSpecOptions
{
    public BelkhayateTimingSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: BelkhayateTiming has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Demark Setup Indicator options.
/// </summary>
public sealed class DemarkSetupIndicatorSpecOptions : IIndicatorSpecOptions
{
    public DemarkSetupIndicatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Performance Index indicator options.
/// </summary>
public sealed class PerformanceIndexSpecOptions : IIndicatorSpecOptions
{
    public PerformanceIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Psychological Line indicator options.
/// </summary>
public sealed class PsychologicalLineSpecOptions : IIndicatorSpecOptions
{
    public PsychologicalLineSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Move Tracker indicator options.
/// </summary>
public sealed class MoveTrackerSpecOptions : IIndicatorSpecOptions
{
    public MoveTrackerSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: MoveTracker has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Multi Level Indicator options.
/// </summary>
public sealed class MultiLevelIndicatorSpecOptions : IIndicatorSpecOptions
{
    public MultiLevelIndicatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Market Direction Indicator options.
/// </summary>
public sealed class MarketDirectionIndicatorSpecOptions : IIndicatorSpecOptions
{
    public MarketDirectionIndicatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Morphed Sine Wave indicator options.
/// </summary>
public sealed class MorphedSineWaveSpecOptions : IIndicatorSpecOptions
{
    public MorphedSineWaveSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Full Typical Price indicator options.
/// </summary>
public sealed class FullTypicalPriceSpecOptions : IIndicatorSpecOptions
{
    public FullTypicalPriceSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: FullTypicalPrice has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Internal Bar Strength Indicator options.
/// </summary>
public sealed class InternalBarStrengthIndicatorSpecOptions : IIndicatorSpecOptions
{
    public InternalBarStrengthIndicatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Z-Score indicator options.
/// </summary>
public sealed class ZScoreSpecOptions : IIndicatorSpecOptions
{
    public ZScoreSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ZScoreSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Fast Z-Score indicator options.
/// </summary>
public sealed class FastZScoreSpecOptions : IIndicatorSpecOptions
{
    public FastZScoreSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public FastZScoreSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Kurtosis Indicator options.
/// </summary>
public sealed class KurtosisIndicatorSpecOptions : IIndicatorSpecOptions
{
    public KurtosisIndicatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    [Obsolete("Has no effect: KurtosisIndicator has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Demark Range Expansion Index indicator options.
/// </summary>
public sealed class DemarkRangeExpansionIndexSpecOptions : IIndicatorSpecOptions
{
    public DemarkRangeExpansionIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Demark Pressure Ratio V1 indicator options.
/// </summary>
public sealed class DemarkPressureRatioV1SpecOptions : IIndicatorSpecOptions
{
    public DemarkPressureRatioV1SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Demark Pressure Ratio V2 indicator options.
/// </summary>
public sealed class DemarkPressureRatioV2SpecOptions : IIndicatorSpecOptions
{
    public DemarkPressureRatioV2SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Demark Reversal Points indicator options.
/// </summary>
public sealed class DemarkReversalPointsSpecOptions : IIndicatorSpecOptions
{
    public DemarkReversalPointsSpecOptions(int length1, int length2)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
    }

    public int Length1 { get; }
    public int Length2 { get; }
}

// ========== Batch 8 SpecOptions (Final) ==========

/// <summary>
/// Bollinger Bands Width indicator options.
/// </summary>
public sealed class BollingerBandsWidthSpecOptions : IIndicatorSpecOptions
{
    public BollingerBandsWidthSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Donchian Channel Width indicator options.
/// </summary>
public sealed class DonchianChannelWidthSpecOptions : IIndicatorSpecOptions
{
    public DonchianChannelWidthSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DonchianChannelWidthSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Keltner Channel Width indicator options.
/// </summary>
public sealed class KeltnerChannelWidthSpecOptions : IIndicatorSpecOptions
{
    public KeltnerChannelWidthSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public KeltnerChannelWidthSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }

    [Obsolete("Has no effect: KeltnerChannelWidth averages exponentially and has no parameter this option could set. It will be removed in the next major version.")]
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Mass Index Core indicator options.
/// </summary>
public sealed class MassIndexCoreSpecOptions : IIndicatorSpecOptions
{
    public MassIndexCoreSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Rahul Mohindar Oscillator indicator options.
/// </summary>
public sealed class RahulMohindarOscillatorSpecOptions : IIndicatorSpecOptions
{
    public RahulMohindarOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// RVI Volatility indicator options.
/// </summary>
public sealed class RviVolatilitySpecOptions : IIndicatorSpecOptions
{
    public RviVolatilitySpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Standard Error Core indicator options.
/// </summary>
public sealed class StandardErrorCoreSpecOptions : IIndicatorSpecOptions
{
    public StandardErrorCoreSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

// ========== Batch 25 SpecOptions (Additional Unwired Core Methods) ==========

/// <summary>
/// Adaptive Autonomous Recursive Moving Average indicator options.
/// </summary>
public sealed class AdaptiveAutonomousRecursiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveAutonomousRecursiveMovingAverageSpecOptions(int length, double lambda = 1) { Length = Math.Max(1, length); Lambda = lambda; }
    public int Length { get; }
    public double Lambda { get; }
}

/// <summary>
/// Triple Hull Moving Average (3HMA) indicator options.
/// </summary>
public sealed class TripleHullMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public TripleHullMovingAverageSpecOptions(int length = 50) { Length = Math.Max(1, length); }
    public int Length { get; }
}

// ========== Batch 30 SpecOptions (Additional Missing Core Methods) ==========

/// <summary>
/// Generalized Double Exponential Moving Average (GDEMA) indicator options.
/// </summary>
public sealed class GeneralizedDoubleExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public GeneralizedDoubleExponentialMovingAverageSpecOptions(int length = 14, double volumeFactor = 1.0)
    {
        Length = Math.Max(1, length);
        VolumeFactor = volumeFactor;
    }
    public int Length { get; }
    public double VolumeFactor { get; }
}

/// <summary>
/// Ehlers Finite Impulse Response Filter indicator options.
/// </summary>
public sealed class EhlersFiniteImpulseResponseFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersFiniteImpulseResponseFilterSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    [Obsolete("Has no effect: EhlersFiniteImpulseResponseFilter has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Ehlers Infinite Impulse Response Filter indicator options.
/// </summary>
public sealed class EhlersInfiniteImpulseResponseFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersInfiniteImpulseResponseFilterSpecOptions(int length = 15) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Volume Adjusted Moving Average indicator options.
/// </summary>
public sealed class VolumeAdjustedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VolumeAdjustedMovingAverageSpecOptions(int length = 14, double factor = 0.67)
        : this(length, factor, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VolumeAdjustedMovingAverageSpecOptions(int length, double factor, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Factor = factor;
        MaType = maType;
    }

    public int Length { get; }
    public double Factor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Average Day Range indicator options.
/// </summary>
public sealed class AverageDayRangeSpecOptions : IIndicatorSpecOptions
{
    public AverageDayRangeSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Chande Intraday Momentum Index indicator options.
/// </summary>
public sealed class ChandeIntradayMomentumIndexSpecOptions : IIndicatorSpecOptions
{
    public ChandeIntradayMomentumIndexSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Contract High indicator options (running maximum).
/// </summary>
public sealed class ContractHighSpecOptions : IIndicatorSpecOptions
{
    public ContractHighSpecOptions() { }
}

/// <summary>
/// Contract Low indicator options (running minimum).
/// </summary>
public sealed class ContractLowSpecOptions : IIndicatorSpecOptions
{
    public ContractLowSpecOptions() { }
}

/// <summary>
/// Corrected Moving Average indicator options.
/// </summary>
public sealed class CorrectedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public CorrectedMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public CorrectedMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Cubed Weighted Moving Average indicator options.
/// </summary>
public sealed class CubedWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public CubedWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Dynamically Adjustable Filter indicator options.
/// </summary>
public sealed class DynamicallyAdjustableFilterSpecOptions : IIndicatorSpecOptions
{
    public DynamicallyAdjustableFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Edge Preserving Filter indicator options.
/// </summary>
public sealed class EdgePreservingFilterSpecOptions : IIndicatorSpecOptions
{
    public EdgePreservingFilterSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EdgePreservingFilterSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers All Pass Phase Shifter indicator options.
/// </summary>
public sealed class EhlersAllPassPhaseShifterSpecOptions : IIndicatorSpecOptions
{
    public EhlersAllPassPhaseShifterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Average Error Filter indicator options.
/// </summary>
public sealed class EhlersAverageErrorFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersAverageErrorFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Distance Coefficient Filter indicator options.
/// </summary>
public sealed class EhlersDistanceCoefficientFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersDistanceCoefficientFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Kaufman Adaptive Moving Average indicator options.
/// </summary>
public sealed class EhlersKaufmanAdaptiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersKaufmanAdaptiveMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Modified Optimum Elliptic Filter indicator options.
/// </summary>
public sealed class EhlersModifiedOptimumEllipticFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersModifiedOptimumEllipticFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    [Obsolete("Has no effect: EhlersModifiedOptimumEllipticFilter has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Ehlers Noise Elimination Technology indicator options.
/// </summary>
public sealed class EhlersNoiseEliminationTechnologySpecOptions : IIndicatorSpecOptions
{
    public EhlersNoiseEliminationTechnologySpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Optimum Elliptic Filter indicator options.
/// </summary>
public sealed class EhlersOptimumEllipticFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersOptimumEllipticFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    [Obsolete("Has no effect: EhlersOptimumEllipticFilter has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Ehlers Variable Index Dynamic Average indicator options.
/// </summary>
public sealed class EhlersVariableIndexDynamicAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersVariableIndexDynamicAverageSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public EhlersVariableIndexDynamicAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    [Obsolete("Has no effect: EhlersVariableIndexDynamicAverage has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Falling Rising Filter indicator options.
/// </summary>
public sealed class FallingRisingFilterSpecOptions : IIndicatorSpecOptions
{
    public FallingRisingFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Farey Sequence Weighted Moving Average indicator options.
/// </summary>
public sealed class FareySequenceWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public FareySequenceWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Fisher Least Squares Moving Average indicator options.
/// </summary>
public sealed class FisherLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public FisherLeastSquaresMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public FisherLeastSquaresMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Following Adaptive Moving Average indicator options.
/// </summary>
public sealed class FollowingAdaptiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public FollowingAdaptiveMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    [Obsolete("Has no effect: EhlersMotherOfAdaptiveMovingAverages has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// General Filter Estimator indicator options.
/// </summary>
public sealed class GeneralFilterEstimatorSpecOptions : IIndicatorSpecOptions
{
    public GeneralFilterEstimatorSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Henderson Weighted Moving Average indicator options.
/// </summary>
public sealed class HendersonWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public HendersonWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Hull Estimate indicator options.
/// </summary>
public sealed class HullEstimateSpecOptions : IIndicatorSpecOptions
{
    public HullEstimateSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Hybrid Convolution Filter indicator options.
/// </summary>
public sealed class HybridConvolutionFilterSpecOptions : IIndicatorSpecOptions
{
    public HybridConvolutionFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// IIR Least Squares Estimate indicator options.
/// </summary>
public sealed class IIRLeastSquaresEstimateSpecOptions : IIndicatorSpecOptions
{
    public IIRLeastSquaresEstimateSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Inverse Distance Weighted Moving Average indicator options.
/// </summary>
public sealed class InverseDistanceWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public InverseDistanceWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Inverse Fisher Transform Core indicator options.
/// </summary>
public sealed class InverseFisherTransformCoreSpecOptions : IIndicatorSpecOptions
{
    public InverseFisherTransformCoreSpecOptions(int length) { Length = Math.Max(1, length); }
    [Obsolete("Has no effect: EhlersInverseFisherTransform has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Jsa Moving Average indicator options.
/// </summary>
public sealed class JsaMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public JsaMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Kalman Smoother indicator options.
/// </summary>
public sealed class KalmanSmootherSpecOptions : IIndicatorSpecOptions
{
    public KalmanSmootherSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Kaufman Adaptive Least Squares Moving Average indicator options.
/// </summary>
public sealed class KaufmanAdaptiveLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public KaufmanAdaptiveLeastSquaresMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Leo Moving Average indicator options.
/// </summary>
public sealed class LeoMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public LeoMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Light Least Squares Moving Average indicator options.
/// </summary>
public sealed class LightLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public LightLeastSquaresMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Linear Extrapolation indicator options.
/// </summary>
public sealed class LinearExtrapolationSpecOptions : IIndicatorSpecOptions
{
    public LinearExtrapolationSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Linear Regression Line indicator options.
/// </summary>
public sealed class LinearRegressionLineSpecOptions : IIndicatorSpecOptions
{
    public LinearRegressionLineSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public LinearRegressionLineSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Linear Weighted Moving Average Core indicator options.
/// </summary>
public sealed class LinearWeightedMovingAverageCoreSpecOptions : IIndicatorSpecOptions
{
    public LinearWeightedMovingAverageCoreSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// McNicholl Moving Average indicator options.
/// </summary>
public sealed class McNichollMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public McNichollMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public McNichollMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Moving Average Adaptive Q indicator options.
/// </summary>
public sealed class MovingAverageAdaptiveQSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageAdaptiveQSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Moving Average V3 indicator options.
/// </summary>
public sealed class MovingAverageV3SpecOptions : IIndicatorSpecOptions
{
    public MovingAverageV3SpecOptions(int length)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MovingAverageV3SpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// One LC Least Squares Moving Average indicator options.
/// </summary>
public sealed class OneLCLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public OneLCLeastSquaresMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public OneLCLeastSquaresMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Optimal Weighted Moving Average indicator options.
/// </summary>
public sealed class OptimalWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public OptimalWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Overshoot Reduction Moving Average indicator options.
/// </summary>
public sealed class OvershootReductionMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public OvershootReductionMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public OvershootReductionMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Parametric Corrective Linear Moving Average indicator options.
/// </summary>
public sealed class ParametricCorrectiveLinearMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public ParametricCorrectiveLinearMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Parametric Kalman Filter indicator options.
/// </summary>
public sealed class ParametricKalmanFilterSpecOptions : IIndicatorSpecOptions
{
    public ParametricKalmanFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

// ========== Batch 26 SpecOptions (Additional Unwired Core Methods) ==========

/// <summary>
/// Zero Low Lag Moving Average indicator options.
/// </summary>
public sealed class ZeroLowLagMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public ZeroLowLagMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Recursive Moving Trend Average indicator options.
/// </summary>
public sealed class RecursiveMovingTrendAverageSpecOptions : IIndicatorSpecOptions
{
    public RecursiveMovingTrendAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Trimean indicator options.
/// </summary>
public sealed class TrimeanSpecOptions : IIndicatorSpecOptions
{
    public TrimeanSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Skewness indicator options.
/// </summary>
public sealed class SkewnessSpecOptions : IIndicatorSpecOptions
{
    public SkewnessSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Hampel Filter indicator options.
/// </summary>
public sealed class HampelFilterSpecOptions : IIndicatorSpecOptions
{
    public HampelFilterSpecOptions(int length, double scalingFactor = 3) { Length = Math.Max(1, length); ScalingFactor = scalingFactor; }
    public int Length { get; }
    public double ScalingFactor { get; }
}

/// <summary>
/// Modular Filter indicator options.
/// </summary>
public sealed class ModularFilterSpecOptions : IIndicatorSpecOptions
{
    public ModularFilterSpecOptions(int length, double beta = 0.8, double z = 0.5) { Length = Math.Max(1, length); Beta = beta; Z = z; }
    public int Length { get; }
    public double Beta { get; }
    public double Z { get; }
}

/// <summary>
/// Dynamically Adjustable Moving Average indicator options.
/// </summary>
public sealed class DynamicallyAdjustableMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public DynamicallyAdjustableMovingAverageSpecOptions(int fastLength, int slowLength = 200) { FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); }
    public int FastLength { get; }
    public int SlowLength { get; }
}

/// <summary>
/// Equity Moving Average indicator options.
/// </summary>
public sealed class EquityMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EquityMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EquityMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Multi Depth Zero Lag Exponential Moving Average indicator options.
/// </summary>
public sealed class MultiDepthZeroLagExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public MultiDepthZeroLagExponentialMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Polynomial Least Squares Moving Average indicator options.
/// </summary>
public sealed class PolynomialLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public PolynomialLeastSquaresMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Powered Kaufman Adaptive Moving Average indicator options.
/// </summary>
public sealed class PoweredKaufmanAdaptiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public PoweredKaufmanAdaptiveMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Quadratic Least Squares Moving Average indicator options.
/// </summary>
public sealed class QuadraticLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public QuadraticLeastSquaresMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Quadratic Moving Average indicator options.
/// </summary>
public sealed class QuadraticMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public QuadraticMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Quadratic Regression indicator options.
/// </summary>
public sealed class QuadraticRegressionSpecOptions : IIndicatorSpecOptions
{
    public QuadraticRegressionSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public QuadraticRegressionSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// R2 Adaptive Regression indicator options.
/// </summary>
public sealed class R2AdaptiveRegressionSpecOptions : IIndicatorSpecOptions
{
    public R2AdaptiveRegressionSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public R2AdaptiveRegressionSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Retention Acceleration Filter indicator options.
/// </summary>
public sealed class RetentionAccelerationFilterSpecOptions : IIndicatorSpecOptions
{
    public RetentionAccelerationFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Right Sided Ricker Moving Average indicator options.
/// </summary>
public sealed class RightSidedRickerMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public RightSidedRickerMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Self Weighted Moving Average indicator options.
/// </summary>
public sealed class SelfWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SelfWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Sequentially Filtered Moving Average indicator options.
/// </summary>
public sealed class SequentiallyFilteredMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SequentiallyFilteredMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SequentiallyFilteredMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Setting Less Trend Step Filtering indicator options.
/// </summary>
public sealed class SettingLessTrendStepFilteringSpecOptions : IIndicatorSpecOptions
{
    public SettingLessTrendStepFilteringSpecOptions(int length) { Length = Math.Max(1, length); }
    [Obsolete("Has no effect: SettingLessTrendStepFiltering has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
}

/// <summary>
/// Shapeshifting Moving Average indicator options.
/// </summary>
public sealed class ShapeshiftingMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public ShapeshiftingMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Sharp Modified Moving Average indicator options.
/// </summary>
public sealed class SharpModifiedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SharpModifiedMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SharpModifiedMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Simplified Least Squares Moving Average indicator options.
/// </summary>
public sealed class SimplifiedLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SimplifiedLeastSquaresMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Simplified Weighted Moving Average indicator options.
/// </summary>
public sealed class SimplifiedWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SimplifiedWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Svama indicator options.
/// </summary>
public sealed class SvamaSpecOptions : IIndicatorSpecOptions
{
    public SvamaSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Three HMA indicator options.
/// </summary>
public sealed class ThreeHMASpecOptions : IIndicatorSpecOptions
{
    public ThreeHMASpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Tillson IE2 indicator options.
/// </summary>
public sealed class TillsonIE2SpecOptions : IIndicatorSpecOptions
{
    public TillsonIE2SpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TillsonIE2SpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// T-Step Least Squares Moving Average indicator options.
/// </summary>
public sealed class TStepLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public TStepLeastSquaresMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TStepLeastSquaresMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Variable Adaptive Moving Average indicator options.
/// </summary>
public sealed class VariableAdaptiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VariableAdaptiveMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VariableAdaptiveMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Variable Length Moving Average indicator options.
/// </summary>
public sealed class VariableLengthMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VariableLengthMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VariableLengthMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Vertical Horizontal Moving Average indicator options.
/// </summary>
public sealed class VerticalHorizontalMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VerticalHorizontalMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Volatility Moving Average indicator options.
/// </summary>
public sealed class VolatilityMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VolatilityMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VolatilityMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volatility Wave Moving Average indicator options.
/// </summary>
public sealed class VolatilityWaveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VolatilityWaveMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public VolatilityWaveMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Well Rounded Moving Average indicator options.
/// </summary>
public sealed class WellRoundedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public WellRoundedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Wilders Summation Method indicator options.
/// </summary>
public sealed class WildersSummationMethodSpecOptions : IIndicatorSpecOptions
{
    public WildersSummationMethodSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Zero Lag Triple Exponential Moving Average indicator options.
/// </summary>
public sealed class ZeroLagTripleExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public ZeroLagTripleExponentialMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

// ========== Batch 27 SpecOptions (Multi-Input Core Methods) ==========

/// <summary>
/// DeMarker indicator options.
/// </summary>
public sealed class DeMarkerSpecOptions : IIndicatorSpecOptions
{
    public DeMarkerSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Middle High Low Moving Average indicator options.
/// </summary>
public sealed class MiddleHighLowMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public MiddleHighLowMovingAverageSpecOptions(int length1, int length2 = 10)
        : this(length1, length2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MiddleHighLowMovingAverageSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Vortex Minus indicator options.
/// </summary>
public sealed class VortexMinusSpecOptions : IIndicatorSpecOptions
{
    public VortexMinusSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Vortex Plus indicator options.
/// </summary>
public sealed class VortexPlusSpecOptions : IIndicatorSpecOptions
{
    public VortexPlusSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Volume Weighted Moving Average indicator options.
/// </summary>
public sealed class VolumeWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VolumeWeightedMovingAverageSpecOptions(int length)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VolumeWeightedMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Klinger Signal indicator options.
/// </summary>
public sealed class KlingerSignalSpecOptions : IIndicatorSpecOptions
{
    public KlingerSignalSpecOptions(int fastLength, int slowLength = 55, int signalLength = 13) { FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); SignalLength = Math.Max(1, signalLength); }
    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
}

/// <summary>
/// Ehlers Chebyshev Low Pass Filter indicator options.
/// </summary>
public sealed class EhlersChebyshevLowPassFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersChebyshevLowPassFilterSpecOptions(int length, double ripple = 0.5) { Length = Math.Max(1, length); Ripple = ripple; }
    [Obsolete("Has no effect: EhlersChebyshevLowPassFilter has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    [Obsolete("Has no effect: EhlersChebyshevLowPassFilter has no parameter this option could set. It will be removed in the next major version.")]
    public double Ripple { get; }
}

/// <summary>
/// Ehlers Gaussian Filter indicator options.
/// </summary>
public sealed class EhlersGaussianFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersGaussianFilterSpecOptions(int length, int poles = 3) { Length = Math.Max(1, length); Poles = poles; }
    public int Length { get; }
    public int Poles { get; }
}

/// <summary>
/// Ehlers Median Average Adaptive Filter indicator options.
/// </summary>
public sealed class EhlersMedianAverageAdaptiveFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersMedianAverageAdaptiveFilterSpecOptions(int length, double threshold = 0.002) { Length = Math.Max(1, length); Threshold = threshold; }
    public int Length { get; }
    public double Threshold { get; }
}

/// <summary>
/// Ehlers Mesa Adaptive Moving Average indicator options.
/// </summary>
public sealed class EhlersMesaAdaptiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersMesaAdaptiveMovingAverageSpecOptions(int length, double fastLimit = 0.5, double slowLimit = 0.05) { Length = Math.Max(1, length); FastLimit = fastLimit; SlowLimit = slowLimit; }
    [Obsolete("Has no effect: EhlersMotherOfAdaptiveMovingAverages has no parameter this option could set. It will be removed in the next major version.")]
    public int Length { get; }
    public double FastLimit { get; }
    public double SlowLimit { get; }
}

/// <summary>
/// Ehlers Recursive Median Filter indicator options.
/// </summary>
public sealed class EhlersRecursiveMedianFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersRecursiveMedianFilterSpecOptions(int length, double alpha = 0.5) { Length = Math.Max(1, length); Alpha = alpha; }
    public int Length { get; }
    [Obsolete("Has no effect: EhlersRecursiveMedianFilter has no parameter this option could set. It will be removed in the next major version.")]
    public double Alpha { get; }
}

/// <summary>
/// Ehlers Roofing Filter indicator options.
/// </summary>
public sealed class EhlersRoofingFilterSpecOptions : IIndicatorSpecOptions
{
    // Ehlers' roofing filter: a 48-bar high-pass, then a 10-bar super smoother, as the batch indicator's defaults.
    public EhlersRoofingFilterSpecOptions(int hpLength = 48, int lpLength = 10) { HpLength = Math.Max(1, hpLength); LpLength = Math.Max(1, lpLength); }
    public int HpLength { get; }
    public int LpLength { get; }
}

// === Batch 28 SpecOptions ===

/// <summary>
/// Ehlers Deviation Scaled Super Smoother indicator options.
/// </summary>
public sealed class EhlersDeviationScaledSuperSmootherSpecOptions : IIndicatorSpecOptions
{
    public EhlersDeviationScaledSuperSmootherSpecOptions(int length, int poles = 2)
        : this(length, poles, MovingAvgType.EhlersHannMovingAverage)
    {
    }

    public EhlersDeviationScaledSuperSmootherSpecOptions(int length, int poles, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Poles = poles;
        MaType = maType;
    }

    public int Length { get; }
    [Obsolete("Has no effect: EhlersDeviationScaledSuperSmoother has no parameter this option could set. It will be removed in the next major version.")]
    public int Poles { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// PPO MA indicator options.
/// </summary>
public sealed class PpoMaSpecOptions : IIndicatorSpecOptions
{
    public PpoMaSpecOptions(int fastLength, int slowLength = 26) { FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); }
    public int FastLength { get; }
    public int SlowLength { get; }
}

/// <summary>
/// Price Oscillator indicator options.
/// </summary>
public sealed class PriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PriceOscillatorSpecOptions(int shortLength, int longLength = 20) { ShortLength = Math.Max(1, shortLength); LongLength = Math.Max(1, longLength); }
    public int ShortLength { get; }
    public int LongLength { get; }
}

/// <summary>
/// Reverse Engineering RSI indicator options.
/// </summary>
public sealed class ReverseEngineeringRsiSpecOptions : IIndicatorSpecOptions
{
    public ReverseEngineeringRsiSpecOptions(int length, double rsiLevel = 50) { Length = Math.Max(1, length); RsiLevel = rsiLevel; }
    public int Length { get; }
    public double RsiLevel { get; }
}

/// <summary>
/// Reverse MACD indicator options.
/// </summary>
public sealed class ReverseMovingAverageConvergenceDivergenceSpecOptions : IIndicatorSpecOptions
{
    public ReverseMovingAverageConvergenceDivergenceSpecOptions(int fastLength, int slowLength = 26, double macdLevel = 0) { FastLength = Math.Max(1, fastLength); SlowLength = Math.Max(1, slowLength); MacdLevel = macdLevel; }
    public int FastLength { get; }
    public int SlowLength { get; }
    public double MacdLevel { get; }
}

/// <summary>
/// Simple Price Zone indicator options.
/// </summary>
public sealed class SimplePriceZoneSpecOptions : IIndicatorSpecOptions
{
    public SimplePriceZoneSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Stochastic RSI Oscillator indicator options.
/// </summary>
public sealed class StochasticRsiOscillatorSpecOptions : IIndicatorSpecOptions
{
    public StochasticRsiOscillatorSpecOptions(int rsiLength, int stochLength = 14) { RsiLength = Math.Max(1, rsiLength); StochLength = Math.Max(1, stochLength); }
    public int RsiLength { get; }
    public int StochLength { get; }
}

/// <summary>
/// Elastic Volume Weighted Moving Average V2 indicator options.
/// </summary>
public sealed class ElasticVolumeWeightedMovingAverageV2SpecOptions : IIndicatorSpecOptions
{
    public ElasticVolumeWeightedMovingAverageV2SpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Windowed Volume Weighted Moving Average indicator options.
/// </summary>
public sealed class WindowedVolumeWeightedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public WindowedVolumeWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// ATR Filtered Exponential Moving Average indicator options.
/// </summary>
public sealed class AtrFilteredExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public AtrFilteredExponentialMovingAverageSpecOptions(int length, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, double min = 5) { Length = Math.Max(1, length); AtrLength = Math.Max(1, atrLength); StdDevLength = Math.Max(1, stdDevLength); LbLength = Math.Max(1, lbLength); Min = min; }
    public int Length { get; }
    public int AtrLength { get; }
    public int StdDevLength { get; }
    public int LbLength { get; }
    public double Min { get; }
}

/// <summary>
/// True Range Adjusted Exponential Moving Average indicator options.
/// </summary>
public sealed class TrueRangeAdjustedExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public TrueRangeAdjustedExponentialMovingAverageSpecOptions(int length, double mult = 1.5) { Length = Math.Max(1, length); Mult = mult; }
    public int Length { get; }
    public double Mult { get; }
}

/// <summary>
/// Relative Volatility Index High indicator options.
/// </summary>
public sealed class RelativeVolatilityIndexHighSpecOptions : IIndicatorSpecOptions
{
    public RelativeVolatilityIndexHighSpecOptions(int length, int stdDevLength = 10) { Length = Math.Max(1, length); StdDevLength = Math.Max(1, stdDevLength); }
    public int Length { get; }
    public int StdDevLength { get; }
}

/// <summary>
/// Relative Volatility Index Low indicator options.
/// </summary>
public sealed class RelativeVolatilityIndexLowSpecOptions : IIndicatorSpecOptions
{
    public RelativeVolatilityIndexLowSpecOptions(int length, int stdDevLength = 10) { Length = Math.Max(1, length); StdDevLength = Math.Max(1, stdDevLength); }
    public int Length { get; }
    public int StdDevLength { get; }
}

/// <summary>
/// Typical Price Volatility indicator options.
/// </summary>
public sealed class TypicalPriceVolatilitySpecOptions : IIndicatorSpecOptions
{
    public TypicalPriceVolatilitySpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ratio OCHL Averager indicator options.
/// </summary>
public sealed class RatioOchlAveragerSpecOptions : IIndicatorSpecOptions
{
    public RatioOchlAveragerSpecOptions() { }
}

/// <summary>
/// Generic indicator options that stores parameters as an array.
/// Used for reflection-based indicator dispatch.
/// </summary>
public sealed class GenericIndicatorOptions : IIndicatorSpecOptions
{
    /// <summary>
    /// Creates generic indicator options with the specified parameters.
    /// </summary>
    /// <param name="parameters">The parameters to pass to the indicator calculation.</param>
    public GenericIndicatorOptions(object[] parameters)
    {
        Parameters = parameters ?? Array.Empty<object>();
    }

    /// <summary>
    /// Gets the parameters array.
    /// </summary>
    public object[] Parameters { get; }

    /// <summary>
    /// Gets a parameter by index with type conversion.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="index">The parameter index.</param>
    /// <param name="defaultValue">Default value if parameter not found.</param>
    /// <returns>The parameter value or default.</returns>
    public T GetParameter<T>(int index, T defaultValue)
    {
        if (index < 0 || index >= Parameters.Length)
        {
            return defaultValue;
        }

        var value = Parameters[index];
        if (value is T typedValue)
        {
            return typedValue;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets a length parameter (common for most indicators).
    /// </summary>
    public int Length => GetParameter(0, 14);

    /// <summary>
    /// Gets a multiplier parameter (common for bands/channels).
    /// </summary>
    public double Multiplier => GetParameter(1, 2.0);
}

/// <summary>
/// Options for multi-stock comparison indicators (e.g., RSMK, Sector Rotation Model).
/// These indicators compare a stock against a market index or benchmark.
/// </summary>
public sealed class MultiStockIndicatorOptions : IIndicatorSpecOptions
{
    /// <summary>
    /// Creates multi-stock indicator options with a single length parameter.
    /// </summary>
    public MultiStockIndicatorOptions(int length)
    {
        Length1 = Math.Max(1, length);
        Length2 = 0;
        Length3 = 0;
        Length4 = 0;
        Length5 = 0;
        SignalLength = 0;
        MaType = MovingAvgType.SimpleMovingAverage;
    }

    /// <summary>
    /// Creates multi-stock indicator options with length and moving average type.
    /// </summary>
    public MultiStockIndicatorOptions(int length, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length);
        Length2 = 0;
        Length3 = 0;
        Length4 = 0;
        Length5 = 0;
        SignalLength = 0;
        MaType = maType;
    }

    /// <summary>
    /// Creates multi-stock indicator options with two length parameters.
    /// </summary>
    public MultiStockIndicatorOptions(int length1, int length2, MovingAvgType maType)
        : this(length1, length2, 0, maType)
    {
    }

    /// <summary>
    /// Creates multi-stock indicator options with three parameters.
    /// </summary>
    public MultiStockIndicatorOptions(int length1, int length2, int signalLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = 0;
        Length4 = 0;
        Length5 = 0;
        SignalLength = signalLength;
        MaType = maType;
    }

    /// <summary>
    /// Creates multi-stock indicator options for RS3D (5 length parameters).
    /// </summary>
    public MultiStockIndicatorOptions(int length1, int length2, int length3, int length4, int length5, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        Length5 = Math.Max(1, length5);
        SignalLength = 0;
        MaType = maType;
    }

    /// <summary>Primary length parameter.</summary>
    public int Length1 { get; }

    /// <summary>Secondary length parameter.</summary>
    public int Length2 { get; }

    /// <summary>Third length parameter.</summary>
    public int Length3 { get; }

    /// <summary>Fourth length parameter.</summary>
    public int Length4 { get; }

    /// <summary>Fifth length parameter.</summary>
    public int Length5 { get; }

    /// <summary>Signal line length.</summary>
    public int SignalLength { get; }

    /// <summary>Moving average type.</summary>
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Oscar Indicator options.
/// </summary>
public sealed class OscarIndicatorSpecOptions : IIndicatorSpecOptions
{
    public OscarIndicatorSpecOptions(int length = 8) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Narrow Bandpass Filter indicator options.
/// </summary>
public sealed class NarrowBandpassFilterSpecOptions : IIndicatorSpecOptions
{
    public NarrowBandpassFilterSpecOptions(int length = 50) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// TFS Tether Line indicator options.
/// </summary>
public sealed class TFSTetherLineSpecOptions : IIndicatorSpecOptions
{
    public TFSTetherLineSpecOptions(int length = 50) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Williams Fractals Up indicator options.
/// </summary>
public sealed class WilliamsFractalsUpSpecOptions : IIndicatorSpecOptions
{
    public WilliamsFractalsUpSpecOptions(int length = 2) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Williams Fractals Down indicator options.
/// </summary>
public sealed class WilliamsFractalsDownSpecOptions : IIndicatorSpecOptions
{
    public WilliamsFractalsDownSpecOptions(int length = 2) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Upside Downside Volume indicator options.
/// </summary>
public sealed class UpsideDownsideVolumeSpecOptions : IIndicatorSpecOptions
{
    public UpsideDownsideVolumeSpecOptions(int length = 50) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Vortex Indicator Plus options.
/// </summary>
public sealed class VortexIndicatorPlusSpecOptions : IIndicatorSpecOptions
{
    public VortexIndicatorPlusSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Vortex Indicator Minus options.
/// </summary>
public sealed class VortexIndicatorMinusSpecOptions : IIndicatorSpecOptions
{
    public VortexIndicatorMinusSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Guppy Count Back Line indicator options.
/// </summary>
public sealed class GuppyCountBackLineSpecOptions : IIndicatorSpecOptions
{
    public GuppyCountBackLineSpecOptions(int length = 21) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Trendflex indicator options.
/// </summary>
public sealed class EhlersTrendflexSpecOptions : IIndicatorSpecOptions
{
    public EhlersTrendflexSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Reflex indicator options.
/// </summary>
public sealed class EhlersReflexSpecOptions : IIndicatorSpecOptions
{
    public EhlersReflexSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Correlation Trend Indicator options.
/// </summary>
public sealed class EhlersCorrelationTrendIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersCorrelationTrendIndicatorSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Trend Trigger Factor indicator options.
/// </summary>
public sealed class TrendTriggerFactorSpecOptions : IIndicatorSpecOptions
{
    public TrendTriggerFactorSpecOptions(int length = 15) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Trend Detection Index indicator options.
/// </summary>
public sealed class TrendDetectionIndexSpecOptions : IIndicatorSpecOptions
{
    public TrendDetectionIndexSpecOptions(int length1 = 20, int length2 = 40)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(length1, length2);
    }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>
/// Uber Trend Indicator options.
/// </summary>
public sealed class UberTrendIndicatorSpecOptions : IIndicatorSpecOptions
{
    public UberTrendIndicatorSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Percentage Trend indicator options.
/// </summary>
public sealed class PercentageTrendSpecOptions : IIndicatorSpecOptions
{
    public PercentageTrendSpecOptions(int length = 20, double pct = 0.15)
    {
        Length = Math.Max(1, length);
        Pct = pct;
    }
    public int Length { get; }
    public double Pct { get; }
}

/// <summary>
/// Liquid Relative Strength Index indicator options.
/// </summary>
public sealed class LiquidRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public LiquidRelativeStrengthIndexSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Asymmetrical Relative Strength Index indicator options.
/// </summary>
public sealed class AsymmetricalRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public AsymmetricalRelativeStrengthIndexSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Average Absolute Error Normalization indicator options.
/// </summary>
public sealed class AverageAbsoluteErrorNormalizationSpecOptions : IIndicatorSpecOptions
{
    public AverageAbsoluteErrorNormalizationSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Recursive Stochastic indicator options.
/// </summary>
public sealed class RecursiveStochasticSpecOptions : IIndicatorSpecOptions
{
    public RecursiveStochasticSpecOptions(int length = 200, double alpha = 0.1)
    {
        Length = Math.Max(1, length);
        Alpha = Math.Max(0, Math.Min(1, alpha));
    }
    public int Length { get; }
    public double Alpha { get; }
}

/// <summary>
/// Shinohara Intensity Ratio A indicator options.
/// </summary>
public sealed class ShinoharaIntensityRatioASpecOptions : IIndicatorSpecOptions
{
    public ShinoharaIntensityRatioASpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Shinohara Intensity Ratio B indicator options.
/// </summary>
public sealed class ShinoharaIntensityRatioBSpecOptions : IIndicatorSpecOptions
{
    public ShinoharaIntensityRatioBSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Range Action Verification Index (RAVI) indicator options.
/// </summary>
public sealed class RangeActionVerificationIndexSpecOptions : IIndicatorSpecOptions
{
    public RangeActionVerificationIndexSpecOptions(int fastLength = 7, int slowLength = 65)
        : this(fastLength, slowLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public RangeActionVerificationIndexSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Williams Accumulation Distribution indicator options.
/// </summary>
public sealed class WilliamsAccumulationDistributionSpecOptions : IIndicatorSpecOptions
{
    public WilliamsAccumulationDistributionSpecOptions() { }
}

/// <summary>
/// Total Power Indicator options.
/// </summary>
public sealed class TotalPowerIndicatorSpecOptions : IIndicatorSpecOptions
{
    public TotalPowerIndicatorSpecOptions(int length1 = 45, int length2 = 10)
        : this(length1, length2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TotalPowerIndicatorSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// TurboTrigger indicator options.
/// </summary>
public sealed class TurboTriggerSpecOptions : IIndicatorSpecOptions
{
    public TurboTriggerSpecOptions(int length = 100, double pctMultiplier = 1.0)
        : this(length, pctMultiplier, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TurboTriggerSpecOptions(int length, double pctMultiplier, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        PctMultiplier = pctMultiplier;
        MaType = maType;
    }

    public int Length { get; }
    [Obsolete("Has no effect: TurboTrigger has no parameter this option could set. It will be removed in the next major version.")]
    public double PctMultiplier { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// TurboScaler indicator options.
/// </summary>
public sealed class TurboScalerSpecOptions : IIndicatorSpecOptions
{
    public TurboScalerSpecOptions(int length = 50, double pctMultiplier = 1.0)
        : this(length, pctMultiplier, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TurboScalerSpecOptions(int length, double pctMultiplier, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        PctMultiplier = pctMultiplier;
        MaType = maType;
    }

    public int Length { get; }
    [Obsolete("Has no effect: TurboScaler has no parameter this option could set. It will be removed in the next major version.")]
    public double PctMultiplier { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// TTM Scalper Indicator options.
/// </summary>
public sealed class TTMScalperIndicatorSpecOptions : IIndicatorSpecOptions
{
    public TTMScalperIndicatorSpecOptions() { }
}

/// <summary>
/// Strength of Movement indicator options.
/// </summary>
public sealed class StrengthOfMovementSpecOptions : IIndicatorSpecOptions
{
    public StrengthOfMovementSpecOptions(int length1 = 10, int length2 = 3)
        : this(length1, length2, MovingAvgType.WeightedMovingAverage)
    {
    }

    public StrengthOfMovementSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Value Chart Indicator options.
/// </summary>
public sealed class ValueChartIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ValueChartIndicatorSpecOptions(int length = 5, int numAtrs = 8)
        : this(length, numAtrs, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ValueChartIndicatorSpecOptions(int length, int numAtrs, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        NumAtrs = Math.Max(1, numAtrs);
        MaType = maType;
    }

    public int Length { get; }
    [Obsolete("Has no effect: ValueChartIndicator has no parameter this option could set. It will be removed in the next major version.")]
    public int NumAtrs { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Sell Gravitation Index indicator options.
/// </summary>
public sealed class SellGravitationIndexSpecOptions : IIndicatorSpecOptions
{
    public SellGravitationIndexSpecOptions(int length = 20)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public SellGravitationIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// TFS Tether Line Indicator options.
/// </summary>
public sealed class TFSTetherLineIndicatorSpecOptions : IIndicatorSpecOptions
{
    public TFSTetherLineIndicatorSpecOptions(int length = 50) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Simple Cycle Indicator options.
/// </summary>
public sealed class EhlersSimpleCycleIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersSimpleCycleIndicatorSpecOptions(double alpha = 0.07) { Alpha = alpha; }
    public double Alpha { get; }
}

/// <summary>
/// Ehlers Fisher Transform options.
/// </summary>
public sealed class EhlersFisherTransformSpecOptions : IIndicatorSpecOptions
{
    public EhlersFisherTransformSpecOptions(int length = 10) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Voss Predictive Filter options.
/// </summary>
public sealed class EhlersVossPredictiveFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersVossPredictiveFilterSpecOptions(int length = 20, double predict = 3, double bw = 0.25)
    {
        Length = Math.Max(1, length);
        Predict = predict;
        Bandwidth = bw;
    }
    public int Length { get; }
    public double Predict { get; }
    public double Bandwidth { get; }
}

/// <summary>
/// Ehlers Spearman Rank Indicator options.
/// </summary>
public sealed class EhlersSpearmanRankIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersSpearmanRankIndicatorSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Correlation Cycle Indicator options.
/// </summary>
public sealed class EhlersCorrelationCycleIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersCorrelationCycleIndicatorSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Correlation Angle Indicator options.
/// </summary>
public sealed class EhlersCorrelationAngleIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersCorrelationAngleIndicatorSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Truncated BandPass Filter options.
/// </summary>
public sealed class EhlersTruncatedBandPassFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersTruncatedBandPassFilterSpecOptions(int length1 = 20, int length2 = 10, double bw = 0.1)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Bandwidth = bw;
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public double Bandwidth { get; }
}

/// <summary>
/// Ehlers Simple Decycler options.
/// </summary>
public sealed class EhlersSimpleDecyclerSpecOptions : IIndicatorSpecOptions
{
    public EhlersSimpleDecyclerSpecOptions(int length = 125) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Even Better Sine Wave Indicator options.
/// </summary>
public sealed class EhlersEvenBetterSineWaveIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersEvenBetterSineWaveIndicatorSpecOptions(int length1 = 40, int length2 = 10)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
    }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>
/// Ehlers Market State Indicator options.
/// </summary>
public sealed class EhlersMarketStateIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersMarketStateIndicatorSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Ehlers Instantaneous Trendline V2 options.
/// </summary>
public sealed class EhlersInstantaneousTrendlineV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersInstantaneousTrendlineV2SpecOptions(double alpha = 0.07) { Alpha = alpha; }
    public double Alpha { get; }
}

/// <summary>
/// Ehlers CyberCycle Oscillator options.
/// </summary>
public sealed class EhlersCyberCycleOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersCyberCycleOscillatorSpecOptions(double alpha = 0.07) { Alpha = alpha; }
    public double Alpha { get; }
}

/// <summary>
/// Ehlers Band Pass Filter V1 options.
/// </summary>
public sealed class EhlersBandPassFilterV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersBandPassFilterV1SpecOptions(int length = 20, double bw = 0.3)
    {
        Length = Math.Max(1, length);
        Bw = bw;
    }
    public int Length { get; }
    public double Bw { get; }
}

/// <summary>
/// Ehlers Band Pass Filter V2 options.
/// </summary>
public sealed class EhlersBandPassFilterV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersBandPassFilterV2SpecOptions(int length = 20, double bw = 0.3)
    {
        Length = Math.Max(1, length);
        Bw = bw;
    }
    public int Length { get; }
    public double Bw { get; }
}

/// <summary>
/// Ehlers Cycle Band Pass Filter options.
/// </summary>
public sealed class EhlersCycleBandPassFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersCycleBandPassFilterSpecOptions(int length = 20, double delta = 0.1)
    {
        Length = Math.Max(1, length);
        Delta = delta;
    }
    public int Length { get; }
    public double Delta { get; }
}

/// <summary>
/// Ehlers Cycle Amplitude options.
/// </summary>
public sealed class EhlersCycleAmplitudeSpecOptions : IIndicatorSpecOptions
{
    public EhlersCycleAmplitudeSpecOptions(int length = 20, double delta = 0.1)
    {
        Length = Math.Max(1, length);
        Delta = delta;
    }
    public int Length { get; }
    public double Delta { get; }
}

/// <summary>
/// Ehlers HP/LP Roofing Filter options.
/// </summary>
public sealed class EhlersHpLpRoofingFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersHpLpRoofingFilterSpecOptions(int length1 = 48, int length2 = 10)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
    }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>
/// Ehlers Early Onset Trend Indicator options.
/// </summary>
public sealed class EhlersEarlyOnsetTrendIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersEarlyOnsetTrendIndicatorSpecOptions(int length1 = 30, int length2 = 100, double k = 0.85)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        K = k;
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public double K { get; }
}

/// <summary>
/// Ehlers Detrended Leading Indicator options.
/// </summary>
public sealed class EhlersDetrendedLeadingIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersDetrendedLeadingIndicatorSpecOptions(int length = 14)
    {
        Length = Math.Max(1, length);
    }
    public int Length { get; }
}

/// <summary>
/// Ehlers Classic Hilbert Transformer options.
/// </summary>
public sealed class EhlersClassicHilbertTransformerSpecOptions : IIndicatorSpecOptions
{
    public EhlersClassicHilbertTransformerSpecOptions(int length1 = 48, int length2 = 10)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
    }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>
/// Ehlers Zero Mean Roofing Filter options.
/// </summary>
public sealed class EhlersZeroMeanRoofingFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersZeroMeanRoofingFilterSpecOptions(int length1 = 48, int length2 = 10)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
    }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>
/// Ehlers Super Passband Filter options.
/// </summary>
public sealed class EhlersSuperPassbandFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersSuperPassbandFilterSpecOptions(int fastLength = 40, int slowLength = 60, int length1 = 5, int length2 = 50)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
    }
    public int FastLength { get; }
    public int SlowLength { get; }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>
/// Ehlers Roofing Filter V2 options.
/// </summary>
public sealed class EhlersRoofingFilterV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersRoofingFilterV2SpecOptions(int upperLength = 80, int lowerLength = 40)
    {
        UpperLength = Math.Max(1, upperLength);
        LowerLength = Math.Max(1, lowerLength);
    }
    public int UpperLength { get; }
    public int LowerLength { get; }
}

/// <summary>
/// Ehlers Impulse Reaction options.
/// </summary>
public sealed class EhlersImpulseReactionSpecOptions : IIndicatorSpecOptions
{
    public EhlersImpulseReactionSpecOptions(int length1 = 2, int length2 = 20, double q = 0.9)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Q = q;
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public double Q { get; }
}

/// <summary>
/// Ehlers Reverse Exponential Moving Average Indicator V1 options.
/// </summary>
public sealed class EhlersReverseEmaIndicatorV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersReverseEmaIndicatorV1SpecOptions(double alpha = 0.1)
    {
        Alpha = Math.Max(0.01, Math.Min(0.99, alpha));
    }
    public double Alpha { get; }
}

/// <summary>
/// Ehlers Squelch Indicator options.
/// </summary>
public sealed class EhlersSquelchIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersSquelchIndicatorSpecOptions(int length1 = 6, int length2 = 20, int length3 = 40)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
}

/// <summary>
/// Ehlers Reverse Exponential Moving Average Indicator V2 options.
/// </summary>
public sealed class EhlersReverseEmaIndicatorV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersReverseEmaIndicatorV2SpecOptions(double trendAlpha = 0.05, double cycleAlpha = 0.3)
    {
        TrendAlpha = Math.Max(0.01, Math.Min(0.99, trendAlpha));
        CycleAlpha = Math.Max(0.01, Math.Min(0.99, cycleAlpha));
    }
    public double TrendAlpha { get; }
    public double CycleAlpha { get; }
}

/// <summary>
/// Ehlers Stochastic Cyber Cycle options.
/// </summary>
public sealed class EhlersStochasticCyberCycleSpecOptions : IIndicatorSpecOptions
{
    public EhlersStochasticCyberCycleSpecOptions(int length = 14, double alpha = 0.7)
    {
        Length = Math.Max(1, length);
        Alpha = Math.Max(0.01, Math.Min(0.99, alpha));
    }
    public int Length { get; }
    public double Alpha { get; }
}

/// <summary>
/// Ehlers Center of Gravity Oscillator options.
/// </summary>
public sealed class EhlersCenterofGravityOscillatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersCenterofGravityOscillatorSpecOptions(int length = 10)
    {
        Length = Math.Max(1, length);
    }
    public int Length { get; }
}

/// <summary>
/// Ehlers Reflex Indicator options.
/// </summary>
public sealed class EhlersReflexIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersReflexIndicatorSpecOptions(int length = 20)
    {
        Length = Math.Max(1, length);
    }
    public int Length { get; }
}

/// <summary>
/// Ehlers Trendflex Indicator options.
/// </summary>
public sealed class EhlersTrendflexIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersTrendflexIndicatorSpecOptions(int length = 20)
    {
        Length = Math.Max(1, length);
    }
    public int Length { get; }
}

/// <summary>
/// JMA RSX Clone options.
/// </summary>
public sealed class JmaRsxCloneSpecOptions : IIndicatorSpecOptions
{
    public JmaRsxCloneSpecOptions(int length = 14)
    {
        Length = Math.Max(1, length);
    }
    public int Length { get; }
}

/// <summary>
/// Rate of Change options.
/// </summary>
public sealed class RateOfChangeSpecOptions : IIndicatorSpecOptions
{
    public RateOfChangeSpecOptions(int length = 12)
    {
        Length = Math.Max(1, length);
    }
    public int Length { get; }
}

/// <summary>
/// Williams Fractals options.
/// </summary>
public sealed class WilliamsFractalsSpecOptions : IIndicatorSpecOptions
{
    public WilliamsFractalsSpecOptions(int length = 2)
    {
        Length = Math.Max(1, length);
    }
    public int Length { get; }
}

/// <summary>
/// Detrended Price Oscillator options.
/// </summary>
public sealed class DetrendedPriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DetrendedPriceOscillatorSpecOptions(int length = 20)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DetrendedPriceOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Polarized Fractal Efficiency options.
/// </summary>
public sealed class PolarizedFractalEfficiencySpecOptions : IIndicatorSpecOptions
{
    public PolarizedFractalEfficiencySpecOptions(int length = 10, int smoothLength = 5)
        : this(length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PolarizedFractalEfficiencySpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Schaff Trend Cycle options.
/// </summary>
public sealed class SchaffTrendCycleSpecOptions : IIndicatorSpecOptions
{
    public SchaffTrendCycleSpecOptions(int cycleLength = 10, int fastLength = 23, int slowLength = 50)
        : this(cycleLength, fastLength, slowLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public SchaffTrendCycleSpecOptions(int cycleLength, int fastLength, int slowLength, MovingAvgType maType)
    {
        CycleLength = Math.Max(1, cycleLength);
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int CycleLength { get; }
    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Smoothed Rate of Change options.
/// </summary>
public sealed class SmoothedRateOfChangeSpecOptions : IIndicatorSpecOptions
{
    public SmoothedRateOfChangeSpecOptions(int rocLength = 12, int smoothLength = 3)
        : this(rocLength, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public SmoothedRateOfChangeSpecOptions(int rocLength, int smoothLength, MovingAvgType maType)
    {
        RocLength = Math.Max(1, rocLength);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int RocLength { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Floor Pivot Point options.
/// </summary>
public sealed class FloorPivotPointSpecOptions : IIndicatorSpecOptions
{
    public FloorPivotPointSpecOptions() { }
}

/// <summary>
/// Floor Pivot Point Support Level 1 options.
/// </summary>
public sealed class FloorPivotPointS1SpecOptions : IIndicatorSpecOptions
{
    public FloorPivotPointS1SpecOptions() { }
}

/// <summary>
/// Floor Pivot Point Resistance Level 1 options.
/// </summary>
public sealed class FloorPivotPointR1SpecOptions : IIndicatorSpecOptions
{
    public FloorPivotPointR1SpecOptions() { }
}

/// <summary>
/// Camarilla Pivot Point options.
/// </summary>
public sealed class CamarillaPivotPointSpecOptions : IIndicatorSpecOptions
{
    public CamarillaPivotPointSpecOptions() { }
}

/// <summary>
/// Woodie Pivot Point options.
/// </summary>
public sealed class WoodiePivotPointSpecOptions : IIndicatorSpecOptions
{
    public WoodiePivotPointSpecOptions() { }
}

/// <summary>
/// Fibonacci Pivot Point options.
/// </summary>
public sealed class FibonacciPivotPointSpecOptions : IIndicatorSpecOptions
{
    public FibonacciPivotPointSpecOptions() { }
}

/// <summary>
/// Demark Pivot Point options.
/// </summary>
public sealed class DemarkPivotPointSpecOptions : IIndicatorSpecOptions
{
    public DemarkPivotPointSpecOptions() { }
}

/// <summary>
/// Linear Channel Middle options.
/// </summary>
public sealed class LinearChannelMiddleSpecOptions : IIndicatorSpecOptions
{
    public LinearChannelMiddleSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Price Channel Upper options.
/// </summary>
public sealed class PriceChannelUpperSpecOptions : IIndicatorSpecOptions
{
    public PriceChannelUpperSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Price Channel Lower options.
/// </summary>
public sealed class PriceChannelLowerSpecOptions : IIndicatorSpecOptions
{
    public PriceChannelLowerSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Donchian Channel Upper options.
/// </summary>
public sealed class DonchianChannelUpperSpecOptions : IIndicatorSpecOptions
{
    public DonchianChannelUpperSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Donchian Channel Lower options.
/// </summary>
public sealed class DonchianChannelLowerSpecOptions : IIndicatorSpecOptions
{
    public DonchianChannelLowerSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Three HMA (3HMA) options.
/// </summary>
public sealed class ThreeHmaSpecOptions : IIndicatorSpecOptions
{
    public ThreeHmaSpecOptions(int length = 50)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public ThreeHmaSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Adaptive Autonomous Recursive Trailing Stop options.
/// </summary>
public sealed class AdaptiveAutonomousRecursiveTrailingStopSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveAutonomousRecursiveTrailingStopSpecOptions(int length = 14, double lambda = 1) { Length = Math.Max(1, length); Lambda = lambda; }
    public int Length { get; }
    public double Lambda { get; }
}

/// <summary>
/// Adaptive Trailing Stop options.
/// </summary>
public sealed class AdaptiveTrailingStopSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveTrailingStopSpecOptions(int length = 14, double multiplier = 2) { Length = Math.Max(1, length); Multiplier = multiplier; }
    public int Length { get; }
    public double Multiplier { get; }
}

/// <summary>
/// Average True Range Trailing Stops options.
/// </summary>
public sealed class AverageTrueRangeTrailingStopsSpecOptions : IIndicatorSpecOptions
{
    public AverageTrueRangeTrailingStopsSpecOptions(int length = 14, double multiplier = 3)
        : this(length, multiplier, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public AverageTrueRangeTrailingStopsSpecOptions(int length, double multiplier, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
        MaType = maType;
    }

    public int Length { get; }
    public double Multiplier { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Welles Wilder Summation options.
/// </summary>
public sealed class WellesWilderSummationSpecOptions : IIndicatorSpecOptions
{
    public WellesWilderSummationSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Damping Index options.
/// </summary>
public sealed class DampingIndexSpecOptions : IIndicatorSpecOptions
{
    public DampingIndexSpecOptions(int length = 5)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DampingIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Didi Index options.
/// </summary>
public sealed class DidiIndexSpecOptions : IIndicatorSpecOptions
{
    public DidiIndexSpecOptions(int shortLength = 3, int mediumLength = 8, int longLength = 20)
        : this(shortLength, mediumLength, longLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DidiIndexSpecOptions(int shortLength, int mediumLength, int longLength, MovingAvgType maType)
    {
        ShortLength = Math.Max(1, shortLength);
        MediumLength = Math.Max(1, mediumLength);
        LongLength = Math.Max(1, longLength);
        MaType = maType;
    }

    public int ShortLength { get; }
    public int MediumLength { get; }
    public int LongLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Vertical Horizontal Filter options.
/// </summary>
public sealed class VerticalHorizontalFilterSpecOptions : IIndicatorSpecOptions
{
    public VerticalHorizontalFilterSpecOptions(int length = 28)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public VerticalHorizontalFilterSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Pretty Good Oscillator options.
/// </summary>
public sealed class PrettyGoodOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PrettyGoodOscillatorSpecOptions(int length = 14)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PrettyGoodOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Linear Regression Slope options.
/// </summary>
public sealed class LinearRegressionSlopeSpecOptions : IIndicatorSpecOptions
{
    public LinearRegressionSlopeSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Linear Regression Intercept options.
/// </summary>
public sealed class LinearRegressionInterceptSpecOptions : IIndicatorSpecOptions
{
    public LinearRegressionInterceptSpecOptions(int length = 14) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Natural Directional Combo options.
/// </summary>
public sealed class NaturalDirectionalComboSpecOptions : IIndicatorSpecOptions
{
    public NaturalDirectionalComboSpecOptions(int length = 40, int smoothLength = 20)
        : this(length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public NaturalDirectionalComboSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Natural Directional Index options.
/// </summary>
public sealed class NaturalDirectionalIndexSpecOptions : IIndicatorSpecOptions
{
    public NaturalDirectionalIndexSpecOptions(int length = 40, int smoothLength = 20)
        : this(length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public NaturalDirectionalIndexSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Natural Market Mirror options.
/// </summary>
public sealed class NaturalMarketMirrorSpecOptions : IIndicatorSpecOptions
{
    public NaturalMarketMirrorSpecOptions(int length = 40)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public NaturalMarketMirrorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Natural Market River options.
/// </summary>
public sealed class NaturalMarketRiverSpecOptions : IIndicatorSpecOptions
{
    public NaturalMarketRiverSpecOptions(int length = 40)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public NaturalMarketRiverSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Natural Market Combo options.
/// </summary>
public sealed class NaturalMarketComboSpecOptions : IIndicatorSpecOptions
{
    public NaturalMarketComboSpecOptions(int length = 40, int smoothLength = 20)
        : this(length, smoothLength, MovingAvgType.WeightedMovingAverage)
    {
    }

    public NaturalMarketComboSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// OC Histogram options.
/// </summary>
public sealed class OCHistogramSpecOptions : IIndicatorSpecOptions
{
    public OCHistogramSpecOptions(int length = 10)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public OCHistogramSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Recursive Differenciator options.
/// </summary>
public sealed class RecursiveDifferenciatorSpecOptions : IIndicatorSpecOptions
{
    public RecursiveDifferenciatorSpecOptions(int length = 14, double alpha = 0.6)
        : this(length, alpha, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public RecursiveDifferenciatorSpecOptions(int length, double alpha, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Alpha = alpha;
        MaType = maType;
    }

    public int Length { get; }
    public double Alpha { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Running Equity options.
/// </summary>
public sealed class RunningEquitySpecOptions : IIndicatorSpecOptions
{
    public RunningEquitySpecOptions(int length = 100)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public RunningEquitySpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Optimized Trend Tracker options.
/// </summary>
public sealed class OptimizedTrendTrackerSpecOptions : IIndicatorSpecOptions
{
    public OptimizedTrendTrackerSpecOptions(int length = 2, double percent = 1.4)
        : this(length, percent, MovingAvgType.VariableIndexDynamicAverage)
    {
    }

    public OptimizedTrendTrackerSpecOptions(int length, double percent, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Percent = percent;
        MaType = maType;
    }

    public int Length { get; }
    public double Percent { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Price Volume Trend options.
/// </summary>
public sealed class PriceVolumeTrendSpecOptions : IIndicatorSpecOptions
{
    public PriceVolumeTrendSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PriceVolumeTrendSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Modified Price Volume Trend options.
/// </summary>
public sealed class ModifiedPriceVolumeTrendSpecOptions : IIndicatorSpecOptions
{
    public ModifiedPriceVolumeTrendSpecOptions(int length = 23)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ModifiedPriceVolumeTrendSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Phase Calculation options.
/// </summary>
public sealed class EhlersPhaseCalculationSpecOptions : IIndicatorSpecOptions
{
    public EhlersPhaseCalculationSpecOptions(int length = 15)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersPhaseCalculationSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Rocket Relative Strength Index options.
/// </summary>
public sealed class EhlersRocketRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public EhlersRocketRelativeStrengthIndexSpecOptions(int length1 = 10, int length2 = 8, double obosLevel = 2, double mult = 1)
        : this(length1, length2, obosLevel, mult, MovingAvgType.Ehlers2PoleSuperSmootherFilterV2)
    {
    }

    public EhlersRocketRelativeStrengthIndexSpecOptions(int length1, int length2, double obosLevel, double mult, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        ObosLevel = obosLevel;
        Mult = mult;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double ObosLevel { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Modified Stochastic Indicator options.
/// </summary>
public sealed class EhlersModifiedStochasticIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersModifiedStochasticIndicatorSpecOptions(int length1 = 48, int length2 = 10, int length3 = 20)
        : this(length1, length2, length3, MovingAvgType.Ehlers2PoleSuperSmootherFilterV1)
    {
    }

    public EhlersModifiedStochasticIndicatorSpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Restoring Pull Indicator options.
/// </summary>
public sealed class EhlersRestoringPullIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersRestoringPullIndicatorSpecOptions(int minLength = 8, int maxLength = 50, int length1 = 40, int length2 = 10)
        : this(minLength, maxLength, length1, length2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersRestoringPullIndicatorSpecOptions(int minLength, int maxLength, int length1, int length2, MovingAvgType maType)
    {
        MinLength = Math.Max(1, minLength);
        MaxLength = Math.Max(1, maxLength);
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int MinLength { get; }
    public int MaxLength { get; }
    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Roofing Filter V1 options.
/// </summary>
public sealed class EhlersRoofingFilterV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersRoofingFilterV1SpecOptions(int length1 = 48, int length2 = 10)
        : this(length1, length2, MovingAvgType.Ehlers2PoleSuperSmootherFilterV1)
    {
    }

    public EhlersRoofingFilterV1SpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Mesa Predict Indicator V2 options.
/// </summary>
public sealed class EhlersMesaPredictIndicatorV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersMesaPredictIndicatorV2SpecOptions(int length1 = 5, int length2 = 135, int length3 = 12, int length4 = 4)
        : this(length1, length2, length3, length4, MovingAvgType.EhlersHannMovingAverage)
    {
    }

    public EhlersMesaPredictIndicatorV2SpecOptions(int length1, int length2, int length3, int length4, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Moving Average Difference Indicator options.
/// </summary>
public sealed class EhlersMovingAverageDifferenceIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersMovingAverageDifferenceIndicatorSpecOptions(int fastLength = 8, int slowLength = 23)
        : this(fastLength, slowLength, MovingAvgType.WeightedMovingAverage)
    {
    }

    public EhlersMovingAverageDifferenceIndicatorSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Smoothed Adaptive Momentum options.
/// </summary>
public sealed class EhlersSmoothedAdaptiveMomentumSpecOptions : IIndicatorSpecOptions
{
    public EhlersSmoothedAdaptiveMomentumSpecOptions(int length1 = 5, int length2 = 8)
        : this(length1, length2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersSmoothedAdaptiveMomentumSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Trend Extraction options.
/// </summary>
public sealed class EhlersTrendExtractionSpecOptions : IIndicatorSpecOptions
{
    public EhlersTrendExtractionSpecOptions(int length = 20, double delta = 0.1)
        : this(length, delta, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EhlersTrendExtractionSpecOptions(int length, double delta, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Delta = delta;
        MaType = maType;
    }

    public int Length { get; }
    public double Delta { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Sortino Ratio options.
/// </summary>
public sealed class SortinoRatioSpecOptions : IIndicatorSpecOptions
{
    public SortinoRatioSpecOptions(int length = 30, double bmk = 0.02)
        : this(length, bmk, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SortinoRatioSpecOptions(int length, double bmk, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Bmk = bmk;
        MaType = maType;
    }

    public int Length { get; }
    public double Bmk { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Sharpe Ratio options.
/// </summary>
public sealed class SharpeRatioSpecOptions : IIndicatorSpecOptions
{
    public SharpeRatioSpecOptions(int length = 30, double bmk = 0.02)
        : this(length, bmk, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SharpeRatioSpecOptions(int length, double bmk, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Bmk = bmk;
        MaType = maType;
    }

    public int Length { get; }
    public double Bmk { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Martin Ratio options.
/// </summary>
public sealed class MartinRatioSpecOptions : IIndicatorSpecOptions
{
    public MartinRatioSpecOptions(int length = 30, double bmk = 0.02)
        : this(length, bmk, MovingAvgType.SimpleMovingAverage)
    {
    }

    public MartinRatioSpecOptions(int length, double bmk, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Bmk = bmk;
        MaType = maType;
    }

    public int Length { get; }
    public double Bmk { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volatility Based Momentum options.
/// </summary>
public sealed class VolatilityBasedMomentumSpecOptions : IIndicatorSpecOptions
{
    public VolatilityBasedMomentumSpecOptions(int length1 = 22, int length2 = 65)
        : this(length1, length2, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public VolatilityBasedMomentumSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volatility Quality Index options.
/// </summary>
public sealed class VolatilityQualityIndexSpecOptions : IIndicatorSpecOptions
{
    public VolatilityQualityIndexSpecOptions(int fastLength = 9, int slowLength = 200)
        : this(fastLength, slowLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VolatilityQualityIndexSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Sigma Spikes options.
/// </summary>
public sealed class SigmaSpikesSpecOptions : IIndicatorSpecOptions
{
    public SigmaSpikesSpecOptions(int length = 20)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public SigmaSpikesSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Surface Roughness Estimator options.
/// </summary>
public sealed class SurfaceRoughnessEstimatorSpecOptions : IIndicatorSpecOptions
{
    public SurfaceRoughnessEstimatorSpecOptions(int length = 100)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public SurfaceRoughnessEstimatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Snake Universal Trading Filter options.
/// </summary>
public sealed class EhlersSnakeUniversalTradingFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersSnakeUniversalTradingFilterSpecOptions(int length1 = 23, int length2 = 50, double bw = 1.4)
        : this(length1, length2, bw, MovingAvgType.EhlersHannMovingAverage)
    {
    }

    public EhlersSnakeUniversalTradingFilterSpecOptions(int length1, int length2, double bw, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Bw = bw;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double Bw { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Universal Trading Filter options.
/// </summary>
public sealed class EhlersUniversalTradingFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersUniversalTradingFilterSpecOptions(int length1 = 16, int length2 = 50, double mult = 2)
        : this(length1, length2, mult, MovingAvgType.EhlersHannMovingAverage)
    {
    }

    public EhlersUniversalTradingFilterSpecOptions(int length1, int length2, double mult, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Mult = mult;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Simple Deriv Indicator options.
/// </summary>
public sealed class EhlersSimpleDerivIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersSimpleDerivIndicatorSpecOptions(int length = 2, int signalLength = 8)
        : this(length, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersSimpleDerivIndicatorSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Simple Clip Indicator options.
/// </summary>
public sealed class EhlersSimpleClipIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersSimpleClipIndicatorSpecOptions(int length1 = 2, int length2 = 10, int length3 = 50, int signalLength = 22)
        : this(length1, length2, length3, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersSimpleClipIndicatorSpecOptions(int length1, int length2, int length3, int signalLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Moving Average Band Width options.
/// </summary>
public sealed class MovingAverageBandWidthSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageBandWidthSpecOptions(int fastLength = 10, int slowLength = 50, double mult = 1)
        : this(fastLength, slowLength, mult, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MovingAverageBandWidthSpecOptions(int fastLength, int slowLength, double mult, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        Mult = mult;
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Mayer Multiple options.
/// </summary>
public sealed class MayerMultipleSpecOptions : IIndicatorSpecOptions
{
    public MayerMultipleSpecOptions(int length = 200, double threshold = 2.4)
        : this(length, threshold, MovingAvgType.SimpleMovingAverage)
    {
    }

    public MayerMultipleSpecOptions(int length, double threshold, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Threshold = threshold;
        MaType = maType;
    }

    public int Length { get; }
    public double Threshold { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Market Meanness Index options.
/// </summary>
public sealed class MarketMeannessIndexSpecOptions : IIndicatorSpecOptions
{
    public MarketMeannessIndexSpecOptions(int length = 100)
        : this(length, MovingAvgType.EhlersNoiseEliminationTechnology)
    {
    }

    public MarketMeannessIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Projection Bandwidth options.
/// </summary>
public sealed class ProjectionBandwidthSpecOptions : IIndicatorSpecOptions
{
    public ProjectionBandwidthSpecOptions(int length = 14)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public ProjectionBandwidthSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ultimate Volatility Indicator options.
/// </summary>
public sealed class UltimateVolatilityIndicatorSpecOptions : IIndicatorSpecOptions
{
    public UltimateVolatilityIndicatorSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public UltimateVolatilityIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volatility Switch Indicator options.
/// </summary>
public sealed class VolatilitySwitchIndicatorSpecOptions : IIndicatorSpecOptions
{
    public VolatilitySwitchIndicatorSpecOptions(int length = 14)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public VolatilitySwitchIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Statistical Volatility options.
/// </summary>
public sealed class StatisticalVolatilitySpecOptions : IIndicatorSpecOptions
{
    public StatisticalVolatilitySpecOptions(int length1 = 30, int length2 = 253)
        : this(length1, length2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public StatisticalVolatilitySpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Standard Deviation options.
/// </summary>
public sealed class StandardDevationSpecOptions : IIndicatorSpecOptions
{
    public StandardDevationSpecOptions(int length = 14)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StandardDevationSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Accumulation Distribution Line options.
/// </summary>
public sealed class AccumulationDistributionLineSpecOptions : IIndicatorSpecOptions
{
    public AccumulationDistributionLineSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public AccumulationDistributionLineSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Connors Relative Strength Index options.
/// </summary>
public sealed class ConnorsRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public ConnorsRelativeStrengthIndexSpecOptions(int length1 = 2, int length2 = 3, int length3 = 100)
        : this(length1, length2, length3, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public ConnorsRelativeStrengthIndexSpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Adaptive Relative Strength Index options.
/// </summary>
public sealed class AdaptiveRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveRelativeStrengthIndexSpecOptions(int length = 14)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public AdaptiveRelativeStrengthIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Apirine Slow Relative Strength Index options.
/// </summary>
public sealed class ApirineSlowRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public ApirineSlowRelativeStrengthIndexSpecOptions(int length = 14, int smoothLength = 6)
        : this(length, smoothLength, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public ApirineSlowRelativeStrengthIndexSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// CCT Stoch RSI options.
/// </summary>
public sealed class CCTStochRSISpecOptions : IIndicatorSpecOptions
{
    public CCTStochRSISpecOptions(int length1 = 5, int length2 = 8, int length3 = 13, int length4 = 14, int length5 = 21, int smoothLength1 = 3, int smoothLength2 = 8)
        : this(length1, length2, length3, length4, length5, smoothLength1, smoothLength2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public CCTStochRSISpecOptions(int length1, int length2, int length3, int length4, int length5, int smoothLength1, int smoothLength2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        Length5 = Math.Max(1, length5);
        SmoothLength1 = Math.Max(1, smoothLength1);
        SmoothLength2 = Math.Max(1, smoothLength2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public int Length5 { get; }
    public int SmoothLength1 { get; }
    public int SmoothLength2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Folded Relative Strength Index options.
/// </summary>
public sealed class FoldedRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public FoldedRelativeStrengthIndexSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public FoldedRelativeStrengthIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Rapid Relative Strength Index options.
/// </summary>
public sealed class RapidRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public RapidRelativeStrengthIndexSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public RapidRelativeStrengthIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Recursive Relative Strength Index options.
/// </summary>
public sealed class RecursiveRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public RecursiveRelativeStrengthIndexSpecOptions(int length = 14)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public RecursiveRelativeStrengthIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Anticipate Indicator options.
/// </summary>
public sealed class EhlersAnticipateIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersAnticipateIndicatorSpecOptions(int length = 14, double bw = 1)
        : this(length, bw, MovingAvgType.EhlersHannMovingAverage)
    {
    }

    public EhlersAnticipateIndicatorSpecOptions(int length, double bw, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Bw = bw;
        MaType = maType;
    }

    public int Length { get; }
    public double Bw { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Adaptive Relative Strength Index V2 options.
/// </summary>
public sealed class EhlersAdaptiveRelativeStrengthIndexV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveRelativeStrengthIndexV2SpecOptions(int length1 = 48, int length2 = 10, int length3 = 3)
        : this(length1, length2, length3, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersAdaptiveRelativeStrengthIndexV2SpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Adaptive RSI Fisher Transform V2 options.
/// </summary>
public sealed class EhlersAdaptiveRsiFisherTransformV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveRsiFisherTransformV2SpecOptions(int length1 = 48, int length2 = 10, int length3 = 3)
        : this(length1, length2, length3, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersAdaptiveRsiFisherTransformV2SpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Adaptive Stochastic Indicator V2 options.
/// </summary>
public sealed class EhlersAdaptiveStochasticIndicatorV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveStochasticIndicatorV2SpecOptions(int length1 = 48, int length2 = 10, int length3 = 3)
        : this(length1, length2, length3, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersAdaptiveStochasticIndicatorV2SpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Adaptive Commodity Channel Index V2 options.
/// </summary>
public sealed class EhlersAdaptiveCommodityChannelIndexV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersAdaptiveCommodityChannelIndexV2SpecOptions(int length1 = 48, int length2 = 10, int length3 = 3)
        : this(length1, length2, length3, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersAdaptiveCommodityChannelIndexV2SpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Auto Correlation Reversals options.
/// </summary>
public sealed class EhlersAutoCorrelationReversalsSpecOptions : IIndicatorSpecOptions
{
    public EhlersAutoCorrelationReversalsSpecOptions(int length1 = 48, int length2 = 10, int length3 = 3)
        : this(length1, length2, length3, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersAutoCorrelationReversalsSpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers AM Detector options.
/// </summary>
public sealed class EhlersAMDetectorSpecOptions : IIndicatorSpecOptions
{
    public EhlersAMDetectorSpecOptions(int length1 = 4, int length2 = 8)
        : this(length1, length2, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EhlersAMDetectorSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Impulse Response options.
/// </summary>
public sealed class EhlersImpulseResponseSpecOptions : IIndicatorSpecOptions
{
    public EhlersImpulseResponseSpecOptions(int length = 20, double bw = 1)
        : this(length, bw, MovingAvgType.EhlersHannMovingAverage)
    {
    }

    public EhlersImpulseResponseSpecOptions(int length, double bw, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Bw = bw;
        MaType = maType;
    }

    public int Length { get; }
    public double Bw { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Hann Window Indicator options.
/// </summary>
public sealed class EhlersHannWindowIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersHannWindowIndicatorSpecOptions(int length = 20)
        : this(length, MovingAvgType.EhlersHannMovingAverage)
    {
    }

    public EhlersHannWindowIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Hamming Window Indicator options.
/// </summary>
public sealed class EhlersHammingWindowIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersHammingWindowIndicatorSpecOptions(int length = 20, double pedestal = 10)
        : this(length, pedestal, MovingAvgType.EhlersHammingMovingAverage)
    {
    }

    public EhlersHammingWindowIndicatorSpecOptions(int length, double pedestal, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Pedestal = pedestal;
        MaType = maType;
    }

    public int Length { get; }
    public double Pedestal { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers FM Demodulator Indicator options.
/// </summary>
public sealed class EhlersFMDemodulatorIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersFMDemodulatorIndicatorSpecOptions(int fastLength = 10, int slowLength = 30)
        : this(fastLength, slowLength, MovingAvgType.Ehlers2PoleSuperSmootherFilterV2)
    {
    }

    public EhlersFMDemodulatorIndicatorSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Empirical Mode Decomposition options.
/// </summary>
public sealed class EhlersEmpiricalModeDecompositionSpecOptions : IIndicatorSpecOptions
{
    public EhlersEmpiricalModeDecompositionSpecOptions(int length1 = 20, int length2 = 50, double delta = 0.5, double fraction = 0.1)
        : this(length1, length2, delta, fraction, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EhlersEmpiricalModeDecompositionSpecOptions(int length1, int length2, double delta, double fraction, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Delta = delta;
        Fraction = fraction;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double Delta { get; }
    public double Fraction { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Triangle Window Indicator options.
/// </summary>
public sealed class EhlersTriangleWindowIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersTriangleWindowIndicatorSpecOptions(int length = 20)
        : this(length, MovingAvgType.EhlersTriangleMovingAverage)
    {
    }

    public EhlersTriangleWindowIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Simple Window Indicator options.
/// </summary>
public sealed class EhlersSimpleWindowIndicatorSpecOptions : IIndicatorSpecOptions
{
    public EhlersSimpleWindowIndicatorSpecOptions(int length = 20)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EhlersSimpleWindowIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Triple Delay Line Detrender options.
/// </summary>
public sealed class EhlersTripleDelayLineDetrenderSpecOptions : IIndicatorSpecOptions
{
    public EhlersTripleDelayLineDetrenderSpecOptions(int length = 14)
        : this(length, MovingAvgType.EhlersModifiedOptimumEllipticFilter)
    {
    }

    public EhlersTripleDelayLineDetrenderSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Signal To Noise Ratio V1 options.
/// </summary>
public sealed class EhlersSignalToNoiseRatioV1SpecOptions : IIndicatorSpecOptions
{
    public EhlersSignalToNoiseRatioV1SpecOptions(int length = 7)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersSignalToNoiseRatioV1SpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Relative Vigor Index options.
/// </summary>
public sealed class EhlersRelativeVigorIndexSpecOptions : IIndicatorSpecOptions
{
    public EhlersRelativeVigorIndexSpecOptions(int length = 10, int signalLength = 4)
        : this(length, signalLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EhlersRelativeVigorIndexSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Zero Lag Exponential Moving Average options.
/// </summary>
public sealed class EhlersZeroLagExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersZeroLagExponentialMovingAverageSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersZeroLagExponentialMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trend Analysis Index options.
/// </summary>
public sealed class TrendAnalysisIndexSpecOptions : IIndicatorSpecOptions
{
    public TrendAnalysisIndexSpecOptions(int length1 = 28, int length2 = 5)
        : this(length1, length2, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TrendAnalysisIndexSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trend Direction Force Index options.
/// </summary>
public sealed class TrendDirectionForceIndexSpecOptions : IIndicatorSpecOptions
{
    public TrendDirectionForceIndexSpecOptions(int length1 = 10, int length2 = 30)
        : this(length1, length2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TrendDirectionForceIndexSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trend Exhaustion Indicator options.
/// </summary>
public sealed class TrendExhaustionIndicatorSpecOptions : IIndicatorSpecOptions
{
    public TrendExhaustionIndicatorSpecOptions(int length = 10)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TrendExhaustionIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trend Impulse Filter options.
/// </summary>
public sealed class TrendImpulseFilterSpecOptions : IIndicatorSpecOptions
{
    public TrendImpulseFilterSpecOptions(int length1 = 100, int length2 = 10)
        : this(length1, length2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TrendImpulseFilterSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trend Analysis Indicator options.
/// </summary>
public sealed class TrendAnalysisIndicatorSpecOptions : IIndicatorSpecOptions
{
    public TrendAnalysisIndicatorSpecOptions(int length1 = 21, int length2 = 4)
        : this(length1, length2, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TrendAnalysisIndicatorSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trender options.
/// </summary>
public sealed class TrenderSpecOptions : IIndicatorSpecOptions
{
    public TrenderSpecOptions(int length = 14, double atrMult = 2)
        : this(length, atrMult, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TrenderSpecOptions(int length, double atrMult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        AtrMult = atrMult;
        MaType = maType;
    }

    public int Length { get; }
    public double AtrMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Self Adjusting Relative Strength Index options.
/// </summary>
public sealed class SelfAdjustingRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public SelfAdjustingRelativeStrengthIndexSpecOptions(int length = 14, int smoothingLength = 21, double mult = 2)
        : this(length, smoothingLength, mult, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SelfAdjustingRelativeStrengthIndexSpecOptions(int length, int smoothingLength, double mult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothingLength = Math.Max(1, smoothingLength);
        Mult = mult;
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothingLength { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Stochastic Connors Relative Strength Index options.
/// </summary>
public sealed class StochasticConnorsRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public StochasticConnorsRelativeStrengthIndexSpecOptions(int length1 = 2, int length2 = 3, int length3 = 100, int smoothLength1 = 3, int smoothLength2 = 3)
        : this(length1, length2, length3, smoothLength1, smoothLength2, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public StochasticConnorsRelativeStrengthIndexSpecOptions(int length1, int length2, int length3, int smoothLength1, int smoothLength2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        SmoothLength1 = Math.Max(1, smoothLength1);
        SmoothLength2 = Math.Max(1, smoothLength2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int SmoothLength1 { get; }
    public int SmoothLength2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Uhl MA Crossover System options.
/// </summary>
public sealed class UhlMaCrossoverSystemSpecOptions : IIndicatorSpecOptions
{
    public UhlMaCrossoverSystemSpecOptions(int length = 100)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public UhlMaCrossoverSystemSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Woodie Commodity Channel Index options.
/// </summary>
public sealed class WoodieCommodityChannelIndexSpecOptions : IIndicatorSpecOptions
{
    public WoodieCommodityChannelIndexSpecOptions(int fastLength = 6, int slowLength = 14)
        : this(fastLength, slowLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public WoodieCommodityChannelIndexSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Vostro Indicator options.
/// </summary>
public sealed class VostroIndicatorSpecOptions : IIndicatorSpecOptions
{
    public VostroIndicatorSpecOptions(int length1 = 5, int length2 = 100, double level = 8)
        : this(length1, length2, level, MovingAvgType.WeightedMovingAverage)
    {
    }

    public VostroIndicatorSpecOptions(int length1, int length2, double level, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Level = level;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double Level { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// VIX Trading System options.
/// </summary>
public sealed class VixTradingSystemSpecOptions : IIndicatorSpecOptions
{
    public VixTradingSystemSpecOptions(int length = 50, double maxCount = 11, double minCount = -11)
        : this(length, maxCount, minCount, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VixTradingSystemSpecOptions(int length, double maxCount, double minCount, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaxCount = maxCount;
        MinCount = minCount;
        MaType = maType;
    }

    public int Length { get; }
    public double MaxCount { get; }
    public double MinCount { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Traders Dynamic Index options.
/// </summary>
public sealed class TradersDynamicIndexSpecOptions : IIndicatorSpecOptions
{
    public TradersDynamicIndexSpecOptions(int length1 = 13, int length2 = 34, int length3 = 2, int length4 = 7)
        : this(length1, length2, length3, length4, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TradersDynamicIndexSpecOptions(int length1, int length2, int length3, int length4, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Tops And Bottoms Finder options.
/// </summary>
public sealed class TopsAndBottomsFinderSpecOptions : IIndicatorSpecOptions
{
    public TopsAndBottomsFinderSpecOptions(int length = 50)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TopsAndBottomsFinderSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trader Pressure Index options.
/// </summary>
public sealed class TraderPressureIndexSpecOptions : IIndicatorSpecOptions
{
    public TraderPressureIndexSpecOptions(int length1 = 7, int length2 = 2, int smoothLength = 3)
        : this(length1, length2, smoothLength, MovingAvgType.WeightedMovingAverage)
    {
    }

    public TraderPressureIndexSpecOptions(int length1, int length2, int smoothLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Technical Ratings options.
/// </summary>
public sealed class TechnicalRatingsSpecOptions : IIndicatorSpecOptions
{
    public TechnicalRatingsSpecOptions(int aoLength1 = 55, int aoLength2 = 34, int rsiLength = 14, int stochLength1 = 14, int stochLength2 = 3, int stochLength3 = 3)
        : this(aoLength1, aoLength2, rsiLength, stochLength1, stochLength2, stochLength3, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TechnicalRatingsSpecOptions(int aoLength1, int aoLength2, int rsiLength, int stochLength1, int stochLength2, int stochLength3, MovingAvgType maType)
    {
        AoLength1 = Math.Max(1, aoLength1);
        AoLength2 = Math.Max(1, aoLength2);
        RsiLength = Math.Max(1, rsiLength);
        StochLength1 = Math.Max(1, stochLength1);
        StochLength2 = Math.Max(1, stochLength2);
        StochLength3 = Math.Max(1, stochLength3);
        MaType = maType;
    }

    public int AoLength1 { get; }
    public int AoLength2 { get; }
    public int RsiLength { get; }
    public int StochLength1 { get; }
    public int StochLength2 { get; }
    public int StochLength3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// The Range Indicator options.
/// </summary>
public sealed class TheRangeIndicatorSpecOptions : IIndicatorSpecOptions
{
    public TheRangeIndicatorSpecOptions(int length = 10, int smoothLength = 3)
        : this(length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TheRangeIndicatorSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chande Quick Stick options.
/// </summary>
public sealed class ChandeQuickStickSpecOptions : IIndicatorSpecOptions
{
    public ChandeQuickStickSpecOptions(int length = 14)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ChandeQuickStickSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Bollinger Bands With ATR Pct options.
/// </summary>
public sealed class BollingerBandsWithAtrPctSpecOptions : IIndicatorSpecOptions
{
    public BollingerBandsWithAtrPctSpecOptions(int length = 14, int bbLength = 20, double stdDevMult = 2)
        : this(length, bbLength, stdDevMult, MovingAvgType.SimpleMovingAverage)
    {
    }

    public BollingerBandsWithAtrPctSpecOptions(int length, int bbLength, double stdDevMult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        BbLength = Math.Max(1, bbLength);
        StdDevMult = stdDevMult;
        MaType = maType;
    }

    public int Length { get; }
    public int BbLength { get; }
    public double StdDevMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chande Momentum Oscillator (with signal) options.
/// </summary>
public sealed class ChandeMomentumOscillatorSignalSpecOptions : IIndicatorSpecOptions
{
    public ChandeMomentumOscillatorSignalSpecOptions(int length = 14, int signalLength = 3)
        : this(length, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ChandeMomentumOscillatorSignalSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Klinger Volume Oscillator options.
/// </summary>
public sealed class KlingerVolumeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public KlingerVolumeOscillatorSpecOptions(int fastLength = 34, int slowLength = 55, int signalLength = 13)
        : this(fastLength, slowLength, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public KlingerVolumeOscillatorSpecOptions(int fastLength, int slowLength, int signalLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Finite Volume Elements options.
/// </summary>
public sealed class FiniteVolumeElementsSpecOptions : IIndicatorSpecOptions
{
    public FiniteVolumeElementsSpecOptions(int length = 22, double factor = 0.3)
        : this(length, factor, MovingAvgType.SimpleMovingAverage)
    {
    }

    public FiniteVolumeElementsSpecOptions(int length, double factor, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Factor = factor;
        MaType = maType;
    }

    public int Length { get; }
    public double Factor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// On Balance Volume Modified options.
/// </summary>
public sealed class OnBalanceVolumeModifiedSpecOptions : IIndicatorSpecOptions
{
    public OnBalanceVolumeModifiedSpecOptions(int length1 = 7, int length2 = 10)
        : this(length1, length2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public OnBalanceVolumeModifiedSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// On Balance Volume Reflex options.
/// </summary>
public sealed class OnBalanceVolumeReflexSpecOptions : IIndicatorSpecOptions
{
    public OnBalanceVolumeReflexSpecOptions(int length = 4, int signalLength = 14)
        : this(length, signalLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public OnBalanceVolumeReflexSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Negative Volume Disparity Indicator options.
/// </summary>
public sealed class NegativeVolumeDisparityIndicatorSpecOptions : IIndicatorSpecOptions
{
    public NegativeVolumeDisparityIndicatorSpecOptions(int length = 33, int signalLength = 4, double top = 1.1, double bottom = 0.9)
        : this(length, signalLength, top, bottom, MovingAvgType.SimpleMovingAverage)
    {
    }

    public NegativeVolumeDisparityIndicatorSpecOptions(int length, int signalLength, double top, double bottom, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        Top = top;
        Bottom = bottom;
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public double Top { get; }
    public double Bottom { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Relative Volume Indicator options.
/// </summary>
public sealed class RelativeVolumeIndicatorSpecOptions : IIndicatorSpecOptions
{
    public RelativeVolumeIndicatorSpecOptions(int length = 60)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public RelativeVolumeIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Price Volume Rank options.
/// </summary>
public sealed class PriceVolumeRankSpecOptions : IIndicatorSpecOptions
{
    public PriceVolumeRankSpecOptions(int fastLength = 5, int slowLength = 10)
        : this(fastLength, slowLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PriceVolumeRankSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Multi Vote On Balance Volume options.
/// </summary>
public sealed class MultiVoteOnBalanceVolumeSpecOptions : IIndicatorSpecOptions
{
    public MultiVoteOnBalanceVolumeSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MultiVoteOnBalanceVolumeSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Stoller Average Range Channels options.
/// </summary>
public sealed class StollerAverageRangeChannelsSpecOptions : IIndicatorSpecOptions
{
    public StollerAverageRangeChannelsSpecOptions(int length = 14, double atrMult = 2)
        : this(length, atrMult, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StollerAverageRangeChannelsSpecOptions(int length, double atrMult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        AtrMult = atrMult;
        MaType = maType;
    }

    public int Length { get; }
    public double AtrMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ultimate Moving Average Bands options.
/// </summary>
public sealed class UltimateMovingAverageBandsSpecOptions : IIndicatorSpecOptions
{
    public UltimateMovingAverageBandsSpecOptions(int minLength = 5, int maxLength = 50, double stdDevMult = 2)
        : this(minLength, maxLength, stdDevMult, MovingAvgType.SimpleMovingAverage)
    {
    }

    public UltimateMovingAverageBandsSpecOptions(int minLength, int maxLength, double stdDevMult, MovingAvgType maType)
    {
        MinLength = Math.Max(1, minLength);
        MaxLength = Math.Max(1, maxLength);
        StdDevMult = stdDevMult;
        MaType = maType;
    }

    public int MinLength { get; }
    public int MaxLength { get; }
    public double StdDevMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Uni Channel options.
/// </summary>
public sealed class UniChannelSpecOptions : IIndicatorSpecOptions
{
    public UniChannelSpecOptions(int length = 10, double ubFac = 0.02, double lbFac = 0.02, bool type1 = false)
        : this(length, ubFac, lbFac, type1, MovingAvgType.SimpleMovingAverage)
    {
    }

    public UniChannelSpecOptions(int length, double ubFac, double lbFac, bool type1, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        UbFac = ubFac;
        LbFac = lbFac;
        Type1 = type1;
        MaType = maType;
    }

    public int Length { get; }
    public double UbFac { get; }
    public double LbFac { get; }
    public bool Type1 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Wilson Relative Price Channel options.
/// </summary>
public sealed class WilsonRelativePriceChannelSpecOptions : IIndicatorSpecOptions
{
    public WilsonRelativePriceChannelSpecOptions(int length = 34, int smoothLength = 1, double overbought = 70, double oversold = 30,
        double upperNeutralZone = 55, double lowerNeutralZone = 45)
        : this(length, smoothLength, overbought, oversold, upperNeutralZone, lowerNeutralZone, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public WilsonRelativePriceChannelSpecOptions(int length, int smoothLength, double overbought, double oversold,
        double upperNeutralZone, double lowerNeutralZone, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        Overbought = overbought;
        Oversold = oversold;
        UpperNeutralZone = upperNeutralZone;
        LowerNeutralZone = lowerNeutralZone;
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public double Overbought { get; }
    public double Oversold { get; }
    public double UpperNeutralZone { get; }
    public double LowerNeutralZone { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Vortex Bands options.
/// </summary>
public sealed class VortexBandsSpecOptions : IIndicatorSpecOptions
{
    public VortexBandsSpecOptions(int length = 20)
        : this(length, MovingAvgType.McNichollMovingAverage)
    {
    }

    public VortexBandsSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Volume Adaptive Bands options.
/// </summary>
public sealed class VolumeAdaptiveBandsSpecOptions : IIndicatorSpecOptions
{
    public VolumeAdaptiveBandsSpecOptions(int length = 100)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VolumeAdaptiveBandsSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Variable Moving Average Bands options.
/// </summary>
public sealed class VariableMovingAverageBandsSpecOptions : IIndicatorSpecOptions
{
    public VariableMovingAverageBandsSpecOptions(int length = 6, double mult = 1.5)
        : this(length, mult, MovingAvgType.VariableMovingAverage)
    {
    }

    public VariableMovingAverageBandsSpecOptions(int length, double mult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Mult = mult;
        MaType = maType;
    }

    public int Length { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Vervoort Volatility Bands options.
/// </summary>
public sealed class VervoortVolatilityBandsSpecOptions : IIndicatorSpecOptions
{
    public VervoortVolatilityBandsSpecOptions(int length1 = 8, int length2 = 13, double devMult = 3.55, double lowBandMult = 0.9)
        : this(length1, length2, devMult, lowBandMult, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public VervoortVolatilityBandsSpecOptions(int length1, int length2, double devMult, double lowBandMult, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        DevMult = devMult;
        LowBandMult = lowBandMult;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double DevMult { get; }
    public double LowBandMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Trend Trader Bands options.
/// </summary>
public sealed class TrendTraderBandsSpecOptions : IIndicatorSpecOptions
{
    public TrendTraderBandsSpecOptions(int length = 21, double mult = 3, double bandStep = 20)
        : this(length, mult, bandStep, MovingAvgType.WeightedMovingAverage)
    {
    }

    public TrendTraderBandsSpecOptions(int length, double mult, double bandStep, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Mult = mult;
        BandStep = bandStep;
        MaType = maType;
    }

    public int Length { get; }
    public double Mult { get; }
    public double BandStep { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Time And Money Channel options.
/// </summary>
public sealed class TimeAndMoneyChannelSpecOptions : IIndicatorSpecOptions
{
    public TimeAndMoneyChannelSpecOptions(int length1 = 41, int length2 = 82)
        : this(length1, length2, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TimeAndMoneyChannelSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Support Resistance options.
/// </summary>
public sealed class SupportResistanceSpecOptions : IIndicatorSpecOptions
{
    public SupportResistanceSpecOptions(int length = 20)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SupportResistanceSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Stationary Extrapolated Levels options.
/// </summary>
public sealed class StationaryExtrapolatedLevelsSpecOptions : IIndicatorSpecOptions
{
    public StationaryExtrapolatedLevelsSpecOptions(int length = 200)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StationaryExtrapolatedLevelsSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Scalpers Channel options.
/// </summary>
public sealed class ScalpersChannelSpecOptions : IIndicatorSpecOptions
{
    public ScalpersChannelSpecOptions(int length1 = 15, int length2 = 20)
        : this(length1, length2, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ScalpersChannelSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Smoothed Volatility Bands options.
/// </summary>
public sealed class SmoothedVolatilityBandsSpecOptions : IIndicatorSpecOptions
{
    public SmoothedVolatilityBandsSpecOptions(int length1 = 20, int length2 = 21, double deviation = 2.4, double bandAdjust = 0.9)
        : this(length1, length2, deviation, bandAdjust, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public SmoothedVolatilityBandsSpecOptions(int length1, int length2, double deviation, double bandAdjust, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Deviation = deviation;
        BandAdjust = bandAdjust;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double Deviation { get; }
    public double BandAdjust { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Absolute Price Oscillator options.
/// </summary>
public sealed class AbsolutePriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public AbsolutePriceOscillatorSpecOptions(int fastLength = 10, int slowLength = 20)
        : this(fastLength, slowLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public AbsolutePriceOscillatorSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Absolute Strength MTF Indicator options.
/// </summary>
public sealed class AbsoluteStrengthMTFIndicatorSpecOptions : IIndicatorSpecOptions
{
    public AbsoluteStrengthMTFIndicatorSpecOptions(int length = 50, int smoothLength = 25)
        : this(length, smoothLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public AbsoluteStrengthMTFIndicatorSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Adaptive Ergodic Candlestick Oscillator options.
/// </summary>
public sealed class AdaptiveErgodicCandlestickOscillatorSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveErgodicCandlestickOscillatorSpecOptions(int smoothLength = 5, int stochLength = 14, int signalLength = 9)
        : this(smoothLength, stochLength, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public AdaptiveErgodicCandlestickOscillatorSpecOptions(int smoothLength, int stochLength, int signalLength, MovingAvgType maType)
    {
        SmoothLength = Math.Max(1, smoothLength);
        StochLength = Math.Max(1, stochLength);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int SmoothLength { get; }
    public int StochLength { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Adaptive Exponential Moving Average options.
/// </summary>
public sealed class AdaptiveExponentialMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public AdaptiveExponentialMovingAverageSpecOptions(int length = 10)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public AdaptiveExponentialMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Adaptive Price Zone Indicator options.
/// </summary>
public sealed class AdaptivePriceZoneIndicatorSpecOptions : IIndicatorSpecOptions
{
    public AdaptivePriceZoneIndicatorSpecOptions(int length = 20, double pct = 2)
        : this(length, pct, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public AdaptivePriceZoneIndicatorSpecOptions(int length, double pct, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Pct = pct;
        MaType = maType;
    }

    public int Length { get; }
    public double Pct { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Auto Dispersion Bands options.
/// </summary>
public sealed class AutoDispersionBandsSpecOptions : IIndicatorSpecOptions
{
    public AutoDispersionBandsSpecOptions(int length = 90, int smoothLength = 140)
        : this(length, smoothLength, MovingAvgType.WeightedMovingAverage)
    {
    }

    public AutoDispersionBandsSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Average Directional Index options.
/// </summary>
public sealed class AverageDirectionalIndexSpecOptions : IIndicatorSpecOptions
{
    public AverageDirectionalIndexSpecOptions(int length = 14)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public AverageDirectionalIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Average True Range options.
/// </summary>
public sealed class AverageTrueRangeSpecOptions : IIndicatorSpecOptions
{
    public AverageTrueRangeSpecOptions(int length = 14)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public AverageTrueRangeSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Bear Power Indicator options.
/// </summary>
public sealed class BearPowerIndicatorSpecOptions : IIndicatorSpecOptions
{
    public BearPowerIndicatorSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public BearPowerIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Bollinger Bands Average True Range options.
/// </summary>
public sealed class BollingerBandsAvgTrueRangeSpecOptions : IIndicatorSpecOptions
{
    public BollingerBandsAvgTrueRangeSpecOptions(int atrLength = 22, int length = 55, double stdDevMult = 2)
        : this(atrLength, length, stdDevMult, MovingAvgType.SimpleMovingAverage)
    {
    }

    public BollingerBandsAvgTrueRangeSpecOptions(int atrLength, int length, double stdDevMult, MovingAvgType maType)
    {
        AtrLength = Math.Max(1, atrLength);
        Length = Math.Max(1, length);
        StdDevMult = stdDevMult;
        MaType = maType;
    }

    public int AtrLength { get; }
    public int Length { get; }
    public double StdDevMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Bollinger Bands Fibonacci Ratios options.
/// </summary>
public sealed class BollingerBandsFibonacciRatiosSpecOptions : IIndicatorSpecOptions
{
    public BollingerBandsFibonacciRatiosSpecOptions(int length = 20, double fibRatio1 = 1.618, double fibRatio2 = 2.618, double fibRatio3 = 4.236)
        : this(length, fibRatio1, fibRatio2, fibRatio3, MovingAvgType.SimpleMovingAverage)
    {
    }

    public BollingerBandsFibonacciRatiosSpecOptions(int length, double fibRatio1, double fibRatio2, double fibRatio3, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        FibRatio1 = fibRatio1;
        FibRatio2 = fibRatio2;
        FibRatio3 = fibRatio3;
        MaType = maType;
    }

    public int Length { get; }
    public double FibRatio1 { get; }
    public double FibRatio2 { get; }
    public double FibRatio3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Bull Power Indicator options.
/// </summary>
public sealed class BullPowerIndicatorSpecOptions : IIndicatorSpecOptions
{
    public BullPowerIndicatorSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public BullPowerIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chandelier Exit options.
/// </summary>
public sealed class ChandelierExitSpecOptions : IIndicatorSpecOptions
{
    public ChandelierExitSpecOptions(int length = 22, double mult = 3)
        : this(length, mult, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public ChandelierExitSpecOptions(int length, double mult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Mult = mult;
        MaType = maType;
    }

    public int Length { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Chande Momentum Oscillator options.
/// </summary>
public sealed class ChandeMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ChandeMomentumOscillatorSpecOptions(int length = 14, int signalLength = 3)
        : this(length, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ChandeMomentumOscillatorSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Confluence Indicator options.
/// </summary>
public sealed class ConfluenceIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ConfluenceIndicatorSpecOptions(int length = 10)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ConfluenceIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Constance Brown Composite Index options.
/// </summary>
public sealed class ConstanceBrownCompositeIndexSpecOptions : IIndicatorSpecOptions
{
    public ConstanceBrownCompositeIndexSpecOptions(int fastLength = 13, int slowLength = 33, int length1 = 14, int length2 = 9, int smoothLength = 3)
        : this(fastLength, slowLength, length1, length2, smoothLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ConstanceBrownCompositeIndexSpecOptions(int fastLength, int slowLength, int length1, int length2, int smoothLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int Length1 { get; }
    public int Length2 { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Delta Moving Average options.
/// </summary>
public sealed class DeltaMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public DeltaMovingAverageSpecOptions(int length1 = 10, int length2 = 5)
        : this(length1, length2, MovingAvgType.SimpleMovingAverage)
    {
    }

    public DeltaMovingAverageSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Dynamic Support And Resistance options.
/// </summary>
public sealed class DynamicSupportAndResistanceSpecOptions : IIndicatorSpecOptions
{
    public DynamicSupportAndResistanceSpecOptions(int length = 25)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public DynamicSupportAndResistanceSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ease Of Movement options.
/// </summary>
public sealed class EaseOfMovementSpecOptions : IIndicatorSpecOptions
{
    public EaseOfMovementSpecOptions(int length = 14, double divisor = 1000000)
        : this(length, divisor, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EaseOfMovementSpecOptions(int length, double divisor, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Divisor = divisor;
        MaType = maType;
    }

    public int Length { get; }
    public double Divisor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Decycler Oscillator V2 options.
/// </summary>
public sealed class EhlersDecyclerOscillatorV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersDecyclerOscillatorV2SpecOptions(int fastLength = 10, int slowLength = 20)
        : this(fastLength, slowLength, MovingAvgType.WeightedMovingAverage)
    {
    }

    public EhlersDecyclerOscillatorV2SpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers High Pass Filter V2 options.
/// </summary>
public sealed class EhlersHighPassFilterV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersHighPassFilterV2SpecOptions(int length = 20)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public EhlersHighPassFilterV2SpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ehlers Signal To Noise Ratio V2 options.
/// </summary>
public sealed class EhlersSignalToNoiseRatioV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersSignalToNoiseRatioV2SpecOptions(int length = 6)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public EhlersSignalToNoiseRatioV2SpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    [Obsolete("Has no effect: EhlersSignalToNoiseRatioV2 has no parameter this option could set. It will be removed in the next major version.")]
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Elder Market Thermometer options.
/// </summary>
public sealed class ElderMarketThermometerSpecOptions : IIndicatorSpecOptions
{
    public ElderMarketThermometerSpecOptions(int length = 22)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ElderMarketThermometerSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Elder Ray Index options.
/// </summary>
public sealed class ElderRayIndexSpecOptions : IIndicatorSpecOptions
{
    public ElderRayIndexSpecOptions(int length = 13)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ElderRayIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Elder Safe Zone Stops options.
/// </summary>
public sealed class ElderSafeZoneStopsSpecOptions : IIndicatorSpecOptions
{
    public ElderSafeZoneStopsSpecOptions(int length = 10, double mult = 2.5)
        : this(length, mult, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ElderSafeZoneStopsSpecOptions(int length, double mult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Mult = mult;
        MaType = maType;
    }

    public int Length { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Enhanced Index options.
/// </summary>
public sealed class EnhancedIndexSpecOptions : IIndicatorSpecOptions
{
    public EnhancedIndexSpecOptions(int length = 14, int signalLength = 8)
        : this(length, signalLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EnhancedIndexSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Enhanced Williams R options.
/// </summary>
public sealed class EnhancedWilliamsRSpecOptions : IIndicatorSpecOptions
{
    public EnhancedWilliamsRSpecOptions(int length = 14, int signalLength = 5)
        : this(length, signalLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public EnhancedWilliamsRSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ergodic Commodity Selection Index options.
/// </summary>
public sealed class ErgodicCommoditySelectionIndexSpecOptions : IIndicatorSpecOptions
{
    public ErgodicCommoditySelectionIndexSpecOptions(int length = 32, int smoothLength = 5, double pointValue = 1)
        : this(length, smoothLength, pointValue, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public ErgodicCommoditySelectionIndexSpecOptions(int length, int smoothLength, double pointValue, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        PointValue = pointValue;
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public double PointValue { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ergodic Mean Deviation Indicator options.
/// </summary>
public sealed class ErgodicMeanDeviationIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ErgodicMeanDeviationIndicatorSpecOptions(int length1 = 32, int length2 = 5, int length3 = 5, int signalLength = 5)
        : this(length1, length2, length3, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ErgodicMeanDeviationIndicatorSpecOptions(int length1, int length2, int length3, int signalLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ergodic Moving Average Convergence Divergence options.
/// </summary>
public sealed class ErgodicMovingAverageConvergenceDivergenceSpecOptions : IIndicatorSpecOptions
{
    public ErgodicMovingAverageConvergenceDivergenceSpecOptions(int length1 = 32, int length2 = 5, int length3 = 5)
        : this(length1, length2, length3, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ErgodicMovingAverageConvergenceDivergenceSpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ergodic True Strength Index V1 options.
/// </summary>
public sealed class ErgodicTrueStrengthIndexV1SpecOptions : IIndicatorSpecOptions
{
    public ErgodicTrueStrengthIndexV1SpecOptions(int length1 = 4, int length2 = 8, int length3 = 6, int signalLength = 3)
        : this(length1, length2, length3, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ErgodicTrueStrengthIndexV1SpecOptions(int length1, int length2, int length3, int signalLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Ergodic True Strength Index V2 options.
/// </summary>
public sealed class ErgodicTrueStrengthIndexV2SpecOptions : IIndicatorSpecOptions
{
    public ErgodicTrueStrengthIndexV2SpecOptions(int length1 = 21, int length2 = 9, int length3 = 9, int length4 = 17, int length5 = 6, int length6 = 2, int signalLength = 2)
        : this(length1, length2, length3, length4, length5, length6, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ErgodicTrueStrengthIndexV2SpecOptions(int length1, int length2, int length3, int length4, int length5, int length6, int signalLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        Length5 = Math.Max(1, length5);
        Length6 = Math.Max(1, length6);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public int Length5 { get; }
    public int Length6 { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Fast and Slow Kurtosis Oscillator options.
/// </summary>
public sealed class FastandSlowKurtosisOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FastandSlowKurtosisOscillatorSpecOptions(int length = 3, double ratio = 0.03)
        : this(length, ratio, MovingAvgType.WeightedMovingAverage)
    {
    }

    public FastandSlowKurtosisOscillatorSpecOptions(int length, double ratio, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Ratio = ratio;
        MaType = maType;
    }

    public int Length { get; }
    public double Ratio { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Fear And Greed Indicator options.
/// </summary>
public sealed class FearAndGreedIndicatorSpecOptions : IIndicatorSpecOptions
{
    public FearAndGreedIndicatorSpecOptions(int fastLength = 10, int slowLength = 30, int smoothLength = 2)
        : this(fastLength, slowLength, smoothLength, MovingAvgType.WeightedMovingAverage)
    {
    }

    public FearAndGreedIndicatorSpecOptions(int fastLength, int slowLength, int smoothLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Fibonacci Retrace options.
/// </summary>
public sealed class FibonacciRetraceSpecOptions : IIndicatorSpecOptions
{
    public FibonacciRetraceSpecOptions(int length1 = 15, int length2 = 50, double factor = 0.382)
        : this(length1, length2, factor, MovingAvgType.WeightedMovingAverage)
    {
    }

    public FibonacciRetraceSpecOptions(int length1, int length2, double factor, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Factor = factor;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double Factor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Freedom Of Movement options.
/// </summary>
public sealed class FreedomOfMovementSpecOptions : IIndicatorSpecOptions
{
    public FreedomOfMovementSpecOptions(int length = 60)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public FreedomOfMovementSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Function To Candles options.
/// </summary>
public sealed class FunctionToCandlesSpecOptions : IIndicatorSpecOptions
{
    public FunctionToCandlesSpecOptions(int length = 14)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public FunctionToCandlesSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// FX Sniper Indicator options.
/// </summary>
public sealed class FXSniperIndicatorSpecOptions : IIndicatorSpecOptions
{
    public FXSniperIndicatorSpecOptions(int cciLength = 14, int t3Length = 5, double b = 0.618)
        : this(cciLength, t3Length, b, MovingAvgType.SimpleMovingAverage)
    {
    }

    public FXSniperIndicatorSpecOptions(int cciLength, int t3Length, double b, MovingAvgType maType)
    {
        CciLength = Math.Max(1, cciLength);
        T3Length = Math.Max(1, t3Length);
        B = b;
        MaType = maType;
    }

    public int CciLength { get; }
    public int T3Length { get; }
    public double B { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Gain Loss Moving Average options.
/// </summary>
public sealed class GainLossMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public GainLossMovingAverageSpecOptions(int length = 14, int signalLength = 7)
        : this(length, signalLength, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public GainLossMovingAverageSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Gopalakrishnan Range Index options.
/// </summary>
public sealed class GopalakrishnanRangeIndexSpecOptions : IIndicatorSpecOptions
{
    public GopalakrishnanRangeIndexSpecOptions(int length = 5)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public GopalakrishnanRangeIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Grover Llorens Activator options.
/// </summary>
public sealed class GroverLlorensActivatorSpecOptions : IIndicatorSpecOptions
{
    public GroverLlorensActivatorSpecOptions(int length = 100, double mult = 5)
        : this(length, mult, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public GroverLlorensActivatorSpecOptions(int length, double mult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Mult = mult;
        MaType = maType;
    }

    public int Length { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// High Low Bands options.
/// </summary>
public sealed class HighLowBandsSpecOptions : IIndicatorSpecOptions
{
    public HighLowBandsSpecOptions(int length = 14, double pctShift = 1)
        : this(length, pctShift, MovingAvgType.SimpleMovingAverage)
    {
    }

    public HighLowBandsSpecOptions(int length, double pctShift, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        PctShift = pctShift;
        MaType = maType;
    }

    public int Length { get; }
    public double PctShift { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// High Low Moving Average options.
/// </summary>
public sealed class HighLowMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public HighLowMovingAverageSpecOptions(int length = 14)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public HighLowMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Hirashima Sugita RS options.
/// </summary>
public sealed class HirashimaSugitaRSSpecOptions : IIndicatorSpecOptions
{
    public HirashimaSugitaRSSpecOptions(int length = 1000)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public HirashimaSugitaRSSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Hull Moving Average options.
/// </summary>
public sealed class HullMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public HullMovingAverageSpecOptions(int length = 20)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public HullMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Hurst Cycle Channel options.
/// </summary>
public sealed class HurstCycleChannelSpecOptions : IIndicatorSpecOptions
{
    public HurstCycleChannelSpecOptions(int fastLength = 10, int slowLength = 30, double fastMult = 1, double slowMult = 3)
        : this(fastLength, slowLength, fastMult, slowMult, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public HurstCycleChannelSpecOptions(int fastLength, int slowLength, double fastMult, double slowMult, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        FastMult = fastMult;
        SlowMult = slowMult;
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public double FastMult { get; }
    public double SlowMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Inertia Indicator options.
/// </summary>
public sealed class InertiaIndicatorSpecOptions : IIndicatorSpecOptions
{
    public InertiaIndicatorSpecOptions(int length = 20)
        : this(length, MovingAvgType.LinearRegression)
    {
    }

    public InertiaIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Information Ratio options.
/// </summary>
public sealed class InformationRatioSpecOptions : IIndicatorSpecOptions
{
    public InformationRatioSpecOptions(int length = 30, double bmk = 0.05)
        : this(length, bmk, MovingAvgType.SimpleMovingAverage)
    {
    }

    public InformationRatioSpecOptions(int length, double bmk, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Bmk = bmk;
        MaType = maType;
    }

    public int Length { get; }
    public double Bmk { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Inverse Fisher Fast Z Score options.
/// </summary>
public sealed class InverseFisherFastZScoreSpecOptions : IIndicatorSpecOptions
{
    public InverseFisherFastZScoreSpecOptions(int length = 50)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public InverseFisherFastZScoreSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Inverse Fisher Z Score options.
/// </summary>
public sealed class InverseFisherZScoreSpecOptions : IIndicatorSpecOptions
{
    public InverseFisherZScoreSpecOptions(int length = 100)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public InverseFisherZScoreSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Japanese Correlation Coefficient options.
/// </summary>
public sealed class JapaneseCorrelationCoefficientSpecOptions : IIndicatorSpecOptions
{
    public JapaneseCorrelationCoefficientSpecOptions(int length = 50)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public JapaneseCorrelationCoefficientSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// JRC Fractal Dimension options.
/// </summary>
public sealed class JrcFractalDimensionSpecOptions : IIndicatorSpecOptions
{
    public JrcFractalDimensionSpecOptions(int length1 = 20, int length2 = 5, int smoothLength = 5)
        : this(length1, length2, smoothLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public JrcFractalDimensionSpecOptions(int length1, int length2, int smoothLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Kase Convergence Divergence options.
/// </summary>
public sealed class KaseConvergenceDivergenceSpecOptions : IIndicatorSpecOptions
{
    public KaseConvergenceDivergenceSpecOptions(int length1 = 30, int length2 = 3, int length3 = 8)
        : this(length1, length2, length3, MovingAvgType.SimpleMovingAverage)
    {
    }

    public KaseConvergenceDivergenceSpecOptions(int length1, int length2, int length3, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Kase Indicator options.
/// </summary>
public sealed class KaseIndicatorSpecOptions : IIndicatorSpecOptions
{
    public KaseIndicatorSpecOptions(int length = 10)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public KaseIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Keltner Channels options.
/// </summary>
public sealed class KeltnerChannelsSpecOptions : IIndicatorSpecOptions
{
    public KeltnerChannelsSpecOptions(int length1 = 20, int length2 = 10, double multFactor = 2)
        : this(length1, length2, multFactor, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public KeltnerChannelsSpecOptions(int length1, int length2, double multFactor, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MultFactor = multFactor;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double MultFactor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Kirshenbaum Bands options.
/// </summary>
public sealed class KirshenbaumBandsSpecOptions : IIndicatorSpecOptions
{
    public KirshenbaumBandsSpecOptions(int length1 = 30, int length2 = 20, double stdDevFactor = 1)
        : this(length1, length2, stdDevFactor, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public KirshenbaumBandsSpecOptions(int length1, int length2, double stdDevFactor, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        StdDevFactor = stdDevFactor;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public double StdDevFactor { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Know Sure Thing options.
/// </summary>
public sealed class KnowSureThingSpecOptions : IIndicatorSpecOptions
{
    public KnowSureThingSpecOptions(int length1 = 10, int length2 = 10, int length3 = 10, int length4 = 15, int rocLength1 = 10, int rocLength2 = 15, int rocLength3 = 20, int rocLength4 = 30, int signalLength = 9)
        : this(length1, length2, length3, length4, rocLength1, rocLength2, rocLength3, rocLength4, signalLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public KnowSureThingSpecOptions(int length1, int length2, int length3, int length4, int rocLength1, int rocLength2, int rocLength3, int rocLength4, int signalLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        RocLength1 = Math.Max(1, rocLength1);
        RocLength2 = Math.Max(1, rocLength2);
        RocLength3 = Math.Max(1, rocLength3);
        RocLength4 = Math.Max(1, rocLength4);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public int RocLength1 { get; }
    public int RocLength2 { get; }
    public int RocLength3 { get; }
    public int RocLength4 { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Kwan Indicator options.
/// </summary>
public sealed class KwanIndicatorSpecOptions : IIndicatorSpecOptions
{
    public KwanIndicatorSpecOptions(int length = 9, int smoothLength = 2)
        : this(length, smoothLength, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public KwanIndicatorSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// LBR Paint Bars options.
/// </summary>
public sealed class LBRPaintBarsSpecOptions : IIndicatorSpecOptions
{
    public LBRPaintBarsSpecOptions(int length = 9, int lbLength = 16, double atrMult = 2.5)
        : this(length, lbLength, atrMult, MovingAvgType.SimpleMovingAverage)
    {
    }

    public LBRPaintBarsSpecOptions(int length, int lbLength, double atrMult, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        LbLength = Math.Max(1, lbLength);
        AtrMult = atrMult;
        MaType = maType;
    }

    public int Length { get; }
    public int LbLength { get; }
    public double AtrMult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Linda Raschke 3/10 Oscillator options.
/// </summary>
public sealed class LindaRaschke310OscillatorSpecOptions : IIndicatorSpecOptions
{
    public LindaRaschke310OscillatorSpecOptions(int fastLength = 3, int slowLength = 10, int smoothLength = 16)
        : this(fastLength, slowLength, smoothLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public LindaRaschke310OscillatorSpecOptions(int fastLength, int slowLength, int smoothLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Mac Z Indicator options.
/// </summary>
public sealed class MacZIndicatorSpecOptions : IIndicatorSpecOptions
{
    public MacZIndicatorSpecOptions(int fastLength = 12, int slowLength = 25, int signalLength = 9, int length = 25, double gamma = 0.02, double mult = 1)
        : this(fastLength, slowLength, signalLength, length, gamma, mult, MovingAvgType.SimpleMovingAverage)
    {
    }

    public MacZIndicatorSpecOptions(int fastLength, int slowLength, int signalLength, int length, double gamma, double mult, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
        Length = Math.Max(1, length);
        Gamma = gamma;
        Mult = mult;
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
    public int Length { get; }
    public double Gamma { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

/// <summary>
/// Mac Z VWAP Indicator options.
/// </summary>
public sealed class MacZVwapIndicatorSpecOptions : IIndicatorSpecOptions
{
    public MacZVwapIndicatorSpecOptions(int fastLength = 12, int slowLength = 25, int signalLength = 9, int length1 = 20, int length2 = 25, double gamma = 0.02)
        : this(fastLength, slowLength, signalLength, length1, length2, gamma, MovingAvgType.SimpleMovingAverage)
    {
    }

    public MacZVwapIndicatorSpecOptions(int fastLength, int slowLength, int signalLength, int length1, int length2, double gamma, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Gamma = gamma;
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
    public int Length1 { get; }
    public int Length2 { get; }
    public double Gamma { get; }
    public MovingAvgType MaType { get; }
}

public sealed class MassThrustIndicatorSpecOptions : IIndicatorSpecOptions
{
    public MassThrustIndicatorSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MassThrustIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class ModifiedGannHiloActivatorSpecOptions : IIndicatorSpecOptions
{
    public ModifiedGannHiloActivatorSpecOptions(int lookbackLength = 3, int length = 10)
        : this(lookbackLength, length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public ModifiedGannHiloActivatorSpecOptions(int lookbackLength, int length, MovingAvgType maType)
    {
        LookbackLength = Math.Max(1, lookbackLength);
        Length = Math.Max(1, length);
        MaType = maType;
    }

    [Obsolete("Has no effect: ModifiedGannHiloActivator has no parameter this option could set. It will be removed in the next major version.")]
    public int LookbackLength { get; }
    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class MomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public MomentumOscillatorSpecOptions(int length = 10, int smoothLength = 3)
        : this(length, smoothLength, MovingAvgType.WeightedMovingAverage)
    {
    }

    public MomentumOscillatorSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    [Obsolete("Has no effect: MomentumOscillator has no parameter this option could set. It will be removed in the next major version.")]
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class MovingAverageBandsSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageBandsSpecOptions(int fastLength = 10, int slowLength = 50, double mult = 1)
        : this(fastLength, slowLength, mult, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public MovingAverageBandsSpecOptions(int fastLength, int slowLength, double mult, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        Mult = mult;
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public double Mult { get; }
    public MovingAvgType MaType { get; }
}

public sealed class MovingAverageChannelSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageChannelSpecOptions(int length = 20)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public MovingAverageChannelSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class MovingAverageEnvelopeSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageEnvelopeSpecOptions(int length = 20, double pct = 0.025)
        : this(length, pct, MovingAvgType.SimpleMovingAverage)
    {
    }

    public MovingAverageEnvelopeSpecOptions(int length, double pct, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Pct = pct;
        MaType = maType;
    }

    public int Length { get; }
    public double Pct { get; }
    public MovingAvgType MaType { get; }
}

public sealed class MovingAverageSupportResistanceSpecOptions : IIndicatorSpecOptions
{
    public MovingAverageSupportResistanceSpecOptions(int length = 10)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public MovingAverageSupportResistanceSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class NarrowSidewaysChannelSpecOptions : IIndicatorSpecOptions
{
    public NarrowSidewaysChannelSpecOptions(int length = 20, double pct = 0.03)
        : this(length, pct, MovingAvgType.SimpleMovingAverage)
    {
    }

    public NarrowSidewaysChannelSpecOptions(int length, double pct, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Pct = pct;
        MaType = maType;
    }

    public int Length { get; }
    [Obsolete("Has no effect: NarrowSidewaysChannel has no parameter this option could set. It will be removed in the next major version.")]
    public double Pct { get; }
    public MovingAvgType MaType { get; }
}

public sealed class NaturalStochasticIndicatorSpecOptions : IIndicatorSpecOptions
{
    public NaturalStochasticIndicatorSpecOptions(int length = 20, int smoothLength = 3)
        : this(length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public NaturalStochasticIndicatorSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class NegativeVolumeIndexSpecOptions : IIndicatorSpecOptions
{
    public NegativeVolumeIndexSpecOptions(int length = 255, int initialValue = 1000)
        : this(length, initialValue, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public NegativeVolumeIndexSpecOptions(int length, int initialValue, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        InitialValue = initialValue;
        MaType = maType;
    }

    public int Length { get; }
    public int InitialValue { get; }
    public MovingAvgType MaType { get; }
}

public sealed class OceanIndicatorSpecOptions : IIndicatorSpecOptions
{
    public OceanIndicatorSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public OceanIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class OnBalanceVolumeSpecOptions : IIndicatorSpecOptions
{
    public OnBalanceVolumeSpecOptions(int length = 20)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public OnBalanceVolumeSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PeakValleyEstimationSpecOptions : IIndicatorSpecOptions
{
    public PeakValleyEstimationSpecOptions(int length = 500, int smoothLength = 100)
        : this(length, smoothLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PeakValleyEstimationSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PercentagePriceOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PercentagePriceOscillatorSpecOptions(int fastLength = 12, int slowLength = 26, int signalLength = 9)
        : this(fastLength, slowLength, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PercentagePriceOscillatorSpecOptions(int fastLength, int slowLength, int signalLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PercentageVolumeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PercentageVolumeOscillatorSpecOptions(int fastLength = 12, int slowLength = 26, int signalLength = 9)
        : this(fastLength, slowLength, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PercentageVolumeOscillatorSpecOptions(int fastLength, int slowLength, int signalLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PhaseChangeIndexSpecOptions : IIndicatorSpecOptions
{
    public PhaseChangeIndexSpecOptions(int length = 35, int smoothLength = 3)
        : this(length, smoothLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PhaseChangeIndexSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PivotPointAverageSpecOptions : IIndicatorSpecOptions
{
    public PivotPointAverageSpecOptions(int length = 3, InputLength inputLength = InputLength.Day)
        : this(length, inputLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PivotPointAverageSpecOptions(int length, InputLength inputLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        InputLength = inputLength;
        MaType = maType;
    }

    public int Length { get; }
    public InputLength InputLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PositiveVolumeIndexSpecOptions : IIndicatorSpecOptions
{
    public PositiveVolumeIndexSpecOptions(int length = 255, int initialValue = 1000)
        : this(length, initialValue, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PositiveVolumeIndexSpecOptions(int length, int initialValue, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        InitialValue = initialValue;
        MaType = maType;
    }

    public int Length { get; }
    public int InitialValue { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PremierStochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PremierStochasticOscillatorSpecOptions(int length = 8, int smoothLength = 25)
        : this(length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PremierStochasticOscillatorSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PriceChannelSpecOptions : IIndicatorSpecOptions
{
    public PriceChannelSpecOptions(int length = 21, double pct = 0.06)
        : this(length, pct, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PriceChannelSpecOptions(int length, double pct, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Pct = pct;
        MaType = maType;
    }

    public int Length { get; }
    public double Pct { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PriceCurveChannelSpecOptions : IIndicatorSpecOptions
{
    public PriceCurveChannelSpecOptions(int length = 100)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public PriceCurveChannelSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PriceHeadleyAccelerationBandsSpecOptions : IIndicatorSpecOptions
{
    public PriceHeadleyAccelerationBandsSpecOptions(int length = 20, double factor = 0.001)
        : this(length, factor, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PriceHeadleyAccelerationBandsSpecOptions(int length, double factor, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Factor = factor;
        MaType = maType;
    }

    public int Length { get; }
    public double Factor { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PriceLineChannelSpecOptions : IIndicatorSpecOptions
{
    public PriceLineChannelSpecOptions(int length = 100)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public PriceLineChannelSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PriceMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PriceMomentumOscillatorSpecOptions(int length1 = 35, int length2 = 20, int signalLength = 10)
        : this(length1, length2, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PriceMomentumOscillatorSpecOptions(int length1, int length2, int signalLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PriceZoneOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PriceZoneOscillatorSpecOptions(int length = 20)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public PriceZoneOscillatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PringSpecialKSpecOptions : IIndicatorSpecOptions
{
    public PringSpecialKSpecOptions(int length1 = 10, int length2 = 15, int length3 = 20, int length4 = 30, int length5 = 40,
        int length6 = 50, int length7 = 65, int length8 = 75, int length9 = 100, int length10 = 130, int length11 = 195,
        int length12 = 265, int length13 = 390, int length14 = 530, int smoothLength = 10)
        : this(length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, smoothLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PringSpecialKSpecOptions(int length1, int length2, int length3, int length4, int length5, int length6, int length7,
        int length8, int length9, int length10, int length11, int length12, int length13, int length14, int smoothLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        Length5 = Math.Max(1, length5);
        Length6 = Math.Max(1, length6);
        Length7 = Math.Max(1, length7);
        Length8 = Math.Max(1, length8);
        Length9 = Math.Max(1, length9);
        Length10 = Math.Max(1, length10);
        Length11 = Math.Max(1, length11);
        Length12 = Math.Max(1, length12);
        Length13 = Math.Max(1, length13);
        Length14 = Math.Max(1, length14);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public int Length5 { get; }
    public int Length6 { get; }
    public int Length7 { get; }
    public int Length8 { get; }
    public int Length9 { get; }
    public int Length10 { get; }
    public int Length11 { get; }
    public int Length12 { get; }
    public int Length13 { get; }
    public int Length14 { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class PseudoPolynomialChannelSpecOptions : IIndicatorSpecOptions
{
    public PseudoPolynomialChannelSpecOptions(int length = 14, double morph = 0.9)
        : this(length, morph, MovingAvgType.SimpleMovingAverage)
    {
    }

    public PseudoPolynomialChannelSpecOptions(int length, double morph, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        Morph = morph;
        MaType = maType;
    }

    public int Length { get; }
    public double Morph { get; }
    public MovingAvgType MaType { get; }
}

public sealed class QuasiWhiteNoiseSpecOptions : IIndicatorSpecOptions
{
    public QuasiWhiteNoiseSpecOptions(int length = 20, int noiseLength = 500, double divisor = 40)
        : this(length, noiseLength, divisor, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public QuasiWhiteNoiseSpecOptions(int length, int noiseLength, double divisor, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        NoiseLength = Math.Max(1, noiseLength);
        Divisor = divisor;
        MaType = maType;
    }

    public int Length { get; }
    public int NoiseLength { get; }
    public double Divisor { get; }
    public MovingAvgType MaType { get; }
}

public sealed class RandomWalkIndexSpecOptions : IIndicatorSpecOptions
{
    public RandomWalkIndexSpecOptions(int length = 14)
        : this(length, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public RandomWalkIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class RateOfChangeBandsSpecOptions : IIndicatorSpecOptions
{
    public RateOfChangeBandsSpecOptions(int length = 12, int smoothLength = 3)
        : this(length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public RateOfChangeBandsSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class ReallySimpleIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ReallySimpleIndicatorSpecOptions(int length = 21, int smoothLength = 10)
        : this(length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ReallySimpleIndicatorSpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class RelativeSpreadStrengthSpecOptions : IIndicatorSpecOptions
{
    public RelativeSpreadStrengthSpecOptions(int fastLength = 10, int slowLength = 40, int length = 14, int smoothLength = 5)
        : this(fastLength, slowLength, length, smoothLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public RelativeSpreadStrengthSpecOptions(int fastLength, int slowLength, int length, int smoothLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class RelativeVigorIndexSpecOptions : IIndicatorSpecOptions
{
    public RelativeVigorIndexSpecOptions(int length = 14)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public RelativeVigorIndexSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class RelativeVolatilityIndexV2SpecOptions : IIndicatorSpecOptions
{
    public RelativeVolatilityIndexV2SpecOptions(int length = 10, int smoothLength = 14)
        : this(length, smoothLength, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public RelativeVolatilityIndexV2SpecOptions(int length, int smoothLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength = Math.Max(1, smoothLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class ReversalPointsSpecOptions : IIndicatorSpecOptions
{
    public ReversalPointsSpecOptions(int length = 100)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public ReversalPointsSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class RSINGIndicatorSpecOptions : IIndicatorSpecOptions
{
    public RSINGIndicatorSpecOptions(int length = 20)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public RSINGIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class SMIErgodicIndicatorSpecOptions : IIndicatorSpecOptions
{
    public SMIErgodicIndicatorSpecOptions(int fastLength = 5, int slowLength = 20, int signalLength = 5)
        : this(fastLength, slowLength, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public SMIErgodicIndicatorSpecOptions(int fastLength, int slowLength, int signalLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class SmoothedWilliamsAccumulationDistributionSpecOptions : IIndicatorSpecOptions
{
    public SmoothedWilliamsAccumulationDistributionSpecOptions(int length = 14)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SmoothedWilliamsAccumulationDistributionSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class SpearmanIndicatorSpecOptions : IIndicatorSpecOptions
{
    public SpearmanIndicatorSpecOptions(int length = 10, int signalLength = 3)
        : this(length, signalLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SpearmanIndicatorSpecOptions(int length, int signalLength, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class SqueezeMomentumIndicatorSpecOptions : IIndicatorSpecOptions
{
    public SqueezeMomentumIndicatorSpecOptions(int length = 20)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public SqueezeMomentumIndicatorSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class StiffnessIndicatorSpecOptions : IIndicatorSpecOptions
{
    public StiffnessIndicatorSpecOptions(int length1 = 100, int length2 = 60, int smoothingLength = 3, double threshold = 90)
        : this(length1, length2, smoothingLength, threshold, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StiffnessIndicatorSpecOptions(int length1, int length2, int smoothingLength, double threshold, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        SmoothingLength = Math.Max(1, smoothingLength);
        Threshold = threshold;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int SmoothingLength { get; }
    public double Threshold { get; }
    public MovingAvgType MaType { get; }
}

public sealed class StochasticFastOscillatorSpecOptions : IIndicatorSpecOptions
{
    public StochasticFastOscillatorSpecOptions(int length = 14, int smoothLength1 = 3, int smoothLength2 = 2)
        : this(length, smoothLength1, smoothLength2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public StochasticFastOscillatorSpecOptions(int length, int smoothLength1, int smoothLength2, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength1 = Math.Max(1, smoothLength1);
        SmoothLength2 = Math.Max(1, smoothLength2);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength1 { get; }
    public int SmoothLength2 { get; }
    public MovingAvgType MaType { get; }
}

public sealed class StochasticMomentumIndexSpecOptions : IIndicatorSpecOptions
{
    public StochasticMomentumIndexSpecOptions(int length1 = 2, int length2 = 8, int smoothLength1 = 5, int smoothLength2 = 5)
        : this(length1, length2, smoothLength1, smoothLength2, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public StochasticMomentumIndexSpecOptions(int length1, int length2, int smoothLength1, int smoothLength2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        SmoothLength1 = Math.Max(1, smoothLength1);
        SmoothLength2 = Math.Max(1, smoothLength2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int SmoothLength1 { get; }
    public int SmoothLength2 { get; }
    public MovingAvgType MaType { get; }
}

public sealed class StochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public StochasticOscillatorSpecOptions(int length = 14, int smoothLength1 = 3, int smoothLength2 = 3)
        : this(length, smoothLength1, smoothLength2, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StochasticOscillatorSpecOptions(int length, int smoothLength1, int smoothLength2, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength1 = Math.Max(1, smoothLength1);
        SmoothLength2 = Math.Max(1, smoothLength2);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength1 { get; }
    public int SmoothLength2 { get; }
    public MovingAvgType MaType { get; }
}

public sealed class StochasticRegularSpecOptions : IIndicatorSpecOptions
{
    public StochasticRegularSpecOptions(int length1 = 5, int length2 = 3)
        : this(length1, length2, MovingAvgType.SimpleMovingAverage)
    {
    }

    public StochasticRegularSpecOptions(int length1, int length2, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public MovingAvgType MaType { get; }
}

public sealed class StochasticRelativeStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public StochasticRelativeStrengthIndexSpecOptions(int length = 14, int smoothLength1 = 3, int smoothLength2 = 3)
        : this(length, smoothLength1, smoothLength2, MovingAvgType.WildersSmoothingMethod)
    {
    }

    public StochasticRelativeStrengthIndexSpecOptions(int length, int smoothLength1, int smoothLength2, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        SmoothLength1 = Math.Max(1, smoothLength1);
        SmoothLength2 = Math.Max(1, smoothLength2);
        MaType = maType;
    }

    public int Length { get; }
    public int SmoothLength1 { get; }
    public int SmoothLength2 { get; }
    public MovingAvgType MaType { get; }
}

public sealed class TFSMboIndicatorSpecOptions : IIndicatorSpecOptions
{
    public TFSMboIndicatorSpecOptions(int fastLength = 25, int slowLength = 200, int signalLength = 18)
        : this(fastLength, slowLength, signalLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TFSMboIndicatorSpecOptions(int fastLength, int slowLength, int signalLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class TillsonT3MovingAverageSpecOptions : IIndicatorSpecOptions
{
    public TillsonT3MovingAverageSpecOptions(int length = 5, double vFactor = 0.7)
        : this(length, vFactor, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TillsonT3MovingAverageSpecOptions(int length, double vFactor, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        VFactor = vFactor;
        MaType = maType;
    }

    public int Length { get; }
    public double VFactor { get; }
    public MovingAvgType MaType { get; }
}

public sealed class TriangularMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public TriangularMovingAverageSpecOptions(int length = 20)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TriangularMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class TrueStrengthIndexSpecOptions : IIndicatorSpecOptions
{
    public TrueStrengthIndexSpecOptions(int length1 = 25, int length2 = 13, int signalLength = 7)
        : this(length1, length2, signalLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public TrueStrengthIndexSpecOptions(int length1, int length2, int signalLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        SignalLength = Math.Max(1, signalLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int SignalLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class TurboStochasticsFastSpecOptions : IIndicatorSpecOptions
{
    public TurboStochasticsFastSpecOptions(int length1 = 20, int length2 = 10, int turboLength = 2)
        : this(length1, length2, turboLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TurboStochasticsFastSpecOptions(int length1, int length2, int turboLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        TurboLength = Math.Max(1, turboLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int TurboLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class TurboStochasticsSlowSpecOptions : IIndicatorSpecOptions
{
    public TurboStochasticsSlowSpecOptions(int length1 = 20, int length2 = 10, int turboLength = 2)
        : this(length1, length2, turboLength, MovingAvgType.SimpleMovingAverage)
    {
    }

    public TurboStochasticsSlowSpecOptions(int length1, int length2, int turboLength, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        TurboLength = Math.Max(1, turboLength);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int TurboLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class VariableIndexDynamicAverageSpecOptions : IIndicatorSpecOptions
{
    public VariableIndexDynamicAverageSpecOptions(int length = 14)
        : this(length, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public VariableIndexDynamicAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class VolumeFlowIndicatorSpecOptions : IIndicatorSpecOptions
{
    public VolumeFlowIndicatorSpecOptions(int length1 = 130, int length2 = 30,
        int signalLength = 5, int smoothLength = 3, double coef = 0.2, double vcoef = 2.5)
        : this(length1, length2, signalLength, smoothLength, coef, vcoef, MovingAvgType.SimpleMovingAverage)
    {
    }

    public VolumeFlowIndicatorSpecOptions(int length1, int length2, int signalLength, int smoothLength,
        double coef, double vcoef, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        SignalLength = Math.Max(1, signalLength);
        SmoothLength = Math.Max(1, smoothLength);
        Coef = coef;
        Vcoef = vcoef;
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int SignalLength { get; }
    public int SmoothLength { get; }
    public double Coef { get; }
    public double Vcoef { get; }
    public MovingAvgType MaType { get; }
}

public sealed class _1LCLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public _1LCLeastSquaresMovingAverageSpecOptions(int length = 14)
        : this(length, MovingAvgType.SimpleMovingAverage)
    {
    }

    public _1LCLeastSquaresMovingAverageSpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class _3HMASpecOptions : IIndicatorSpecOptions
{
    public _3HMASpecOptions(int length = 50)
        : this(length, MovingAvgType.WeightedMovingAverage)
    {
    }

    public _3HMASpecOptions(int length, MovingAvgType maType)
    {
        Length = Math.Max(1, length);
        MaType = maType;
    }

    public int Length { get; }
    public MovingAvgType MaType { get; }
}

public sealed class Dema2LinesSpecOptions : IIndicatorSpecOptions
{
    public Dema2LinesSpecOptions(int fastLength = 10, int slowLength = 40)
        : this(fastLength, slowLength, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public Dema2LinesSpecOptions(int fastLength, int slowLength, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public MovingAvgType MaType { get; }
}

public sealed class GuppyDistanceIndicatorSpecOptions : IIndicatorSpecOptions
{
    public GuppyDistanceIndicatorSpecOptions(int length1 = 3, int length2 = 5, int length3 = 8, int length4 = 10,
        int length5 = 12, int length6 = 15, int length7 = 30, int length8 = 35, int length9 = 40, int length10 = 45,
        int length11 = 11, int length12 = 60)
        : this(length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public GuppyDistanceIndicatorSpecOptions(int length1, int length2, int length3, int length4, int length5, int length6,
        int length7, int length8, int length9, int length10, int length11, int length12, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        Length5 = Math.Max(1, length5);
        Length6 = Math.Max(1, length6);
        Length7 = Math.Max(1, length7);
        Length8 = Math.Max(1, length8);
        Length9 = Math.Max(1, length9);
        Length10 = Math.Max(1, length10);
        Length11 = Math.Max(1, length11);
        Length12 = Math.Max(1, length12);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public int Length5 { get; }
    public int Length6 { get; }
    public int Length7 { get; }
    public int Length8 { get; }
    public int Length9 { get; }
    public int Length10 { get; }
    public int Length11 { get; }
    public int Length12 { get; }
    public MovingAvgType MaType { get; }
}

public sealed class GuppyMultipleMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public GuppyMultipleMovingAverageSpecOptions(int length1 = 3, int length2 = 5, int length3 = 7, int length4 = 8,
        int length5 = 9, int length6 = 10, int length7 = 11, int length8 = 12, int length9 = 13, int length10 = 15,
        int length11 = 17, int length12 = 19, int length13 = 21, int length14 = 23, int length15 = 25, int length16 = 28,
        int length17 = 30, int length18 = 31, int length19 = 34, int length20 = 35, int length21 = 37, int length22 = 40,
        int length23 = 43, int length24 = 45, int length25 = 50, int length26 = 55, int length27 = 60)
        : this(length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12,
            length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23,
            length24, length25, length26, length27, MovingAvgType.ExponentialMovingAverage)
    {
    }

    public GuppyMultipleMovingAverageSpecOptions(int length1, int length2, int length3, int length4, int length5, int length6,
        int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15,
        int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23,
        int length24, int length25, int length26, int length27, MovingAvgType maType)
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
        Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
        Length5 = Math.Max(1, length5);
        Length6 = Math.Max(1, length6);
        Length7 = Math.Max(1, length7);
        Length8 = Math.Max(1, length8);
        Length9 = Math.Max(1, length9);
        Length10 = Math.Max(1, length10);
        Length11 = Math.Max(1, length11);
        Length12 = Math.Max(1, length12);
        Length13 = Math.Max(1, length13);
        Length14 = Math.Max(1, length14);
        Length15 = Math.Max(1, length15);
        Length16 = Math.Max(1, length16);
        Length17 = Math.Max(1, length17);
        Length18 = Math.Max(1, length18);
        Length19 = Math.Max(1, length19);
        Length20 = Math.Max(1, length20);
        Length21 = Math.Max(1, length21);
        Length22 = Math.Max(1, length22);
        Length23 = Math.Max(1, length23);
        Length24 = Math.Max(1, length24);
        Length25 = Math.Max(1, length25);
        Length26 = Math.Max(1, length26);
        Length27 = Math.Max(1, length27);
        MaType = maType;
    }

    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
    public int Length5 { get; }
    public int Length6 { get; }
    public int Length7 { get; }
    public int Length8 { get; }
    public int Length9 { get; }
    public int Length10 { get; }
    public int Length11 { get; }
    public int Length12 { get; }
    public int Length13 { get; }
    public int Length14 { get; }
    public int Length15 { get; }
    public int Length16 { get; }
    public int Length17 { get; }
    public int Length18 { get; }
    public int Length19 { get; }
    public int Length20 { get; }
    public int Length21 { get; }
    public int Length22 { get; }
    public int Length23 { get; }
    public int Length24 { get; }
    public int Length25 { get; }
    public int Length26 { get; }
    public int Length27 { get; }
    public MovingAvgType MaType { get; }
}

public sealed class InsyncIndexSpecOptions : IIndicatorSpecOptions
{
    public InsyncIndexSpecOptions(int fastLength = 12, int slowLength = 26, int signalLength = 9, int emoLength = 14,
        int mfiLength = 20, int bbLength = 20, int cciLength = 14, int dpoLength = 18, int rocLength = 10, int rsiLength = 14,
        int stochLength = 14, int stochKLength = 1, int stochDLength = 3, int smaLength = 10, double stdDevMult = 2, double divisor = 10000)
        : this(fastLength, slowLength, signalLength, emoLength, mfiLength, bbLength, cciLength, dpoLength, rocLength, rsiLength,
            stochLength, stochKLength, stochDLength, smaLength, stdDevMult, divisor, MovingAvgType.SimpleMovingAverage)
    {
    }

    public InsyncIndexSpecOptions(int fastLength, int slowLength, int signalLength, int emoLength, int mfiLength, int bbLength,
        int cciLength, int dpoLength, int rocLength, int rsiLength, int stochLength, int stochKLength, int stochDLength,
        int smaLength, double stdDevMult, double divisor, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        SignalLength = Math.Max(1, signalLength);
        EmoLength = Math.Max(1, emoLength);
        MfiLength = Math.Max(1, mfiLength);
        BbLength = Math.Max(1, bbLength);
        CciLength = Math.Max(1, cciLength);
        DpoLength = Math.Max(1, dpoLength);
        RocLength = Math.Max(1, rocLength);
        RsiLength = Math.Max(1, rsiLength);
        StochLength = Math.Max(1, stochLength);
        StochKLength = Math.Max(1, stochKLength);
        StochDLength = Math.Max(1, stochDLength);
        SmaLength = Math.Max(1, smaLength);
        StdDevMult = stdDevMult;
        Divisor = divisor;
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int SignalLength { get; }
    public int EmoLength { get; }
    public int MfiLength { get; }
    public int BbLength { get; }
    public int CciLength { get; }
    public int DpoLength { get; }
    public int RocLength { get; }
    public int RsiLength { get; }
    public int StochLength { get; }
    public int StochKLength { get; }
    public int StochDLength { get; }
    public int SmaLength { get; }
    public double StdDevMult { get; }
    public double Divisor { get; }
    public MovingAvgType MaType { get; }
}

public sealed class KaseDevStopV2SpecOptions : IIndicatorSpecOptions
{
    public KaseDevStopV2SpecOptions(int fastLength = 10, int slowLength = 21, int length = 20,
        double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6)
        : this(fastLength, slowLength, length, stdDev1, stdDev2, stdDev3, stdDev4, MovingAvgType.SimpleMovingAverage)
    {
    }

    public KaseDevStopV2SpecOptions(int fastLength, int slowLength, int length,
        double stdDev1, double stdDev2, double stdDev3, double stdDev4, MovingAvgType maType)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
        Length = Math.Max(1, length);
        StdDev1 = stdDev1;
        StdDev2 = stdDev2;
        StdDev3 = stdDev3;
        StdDev4 = stdDev4;
        MaType = maType;
    }

    public int FastLength { get; }
    public int SlowLength { get; }
    public int Length { get; }
    public double StdDev1 { get; }
    public double StdDev2 { get; }
    public double StdDev3 { get; }
    public double StdDev4 { get; }
    public MovingAvgType MaType { get; }
}

