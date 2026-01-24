using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>
/// Interface for indicator options.
/// </summary>
public interface IIndicatorSpecOptions { }

/// <summary>
/// Specification for an indicator computation.
/// </summary>
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
        // Validate StdDevMult to prevent invalid band calculations (inverted/collapsed bands)
        StdDevMult = Math.Max(0.001, stdDevMult);
    }

    public int Length { get; }
    public double StdDevMult { get; }
}

/// <summary>
/// ATR indicator options.
/// </summary>
public sealed class AtrSpecOptions : IIndicatorSpecOptions
{
    public AtrSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Stochastic indicator options.
/// </summary>
public sealed class StochasticSpecOptions : IIndicatorSpecOptions
{
    public StochasticSpecOptions(int kLength, int dLength)
    {
        KLength = Math.Max(1, kLength);
        DLength = Math.Max(1, dLength);
    }

    public int KLength { get; }
    public int DLength { get; }
}

/// <summary>
/// ADX indicator options.
/// </summary>
public sealed class AdxSpecOptions : IIndicatorSpecOptions
{
    public AdxSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// PPO indicator options.
/// </summary>
public sealed class PpoSpecOptions : IIndicatorSpecOptions
{
    public PpoSpecOptions(int fastLength, int slowLength)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
}

/// <summary>
/// APO indicator options.
/// </summary>
public sealed class ApoSpecOptions : IIndicatorSpecOptions
{
    public ApoSpecOptions(int fastLength, int slowLength)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
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
    {
        LongLength = Math.Max(1, longLength);
        ShortLength = Math.Max(1, shortLength);
    }

    public int LongLength { get; }
    public int ShortLength { get; }
}

/// <summary>
/// Stochastic RSI indicator options.
/// </summary>
public sealed class StochRsiSpecOptions : IIndicatorSpecOptions
{
    public StochRsiSpecOptions(int rsiLength, int stochLength)
    {
        RsiLength = Math.Max(1, rsiLength);
        StochLength = Math.Max(1, stochLength);
    }

    public int RsiLength { get; }
    public int StochLength { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Mass Index indicator options.
/// </summary>
public sealed class MassIndexSpecOptions : IIndicatorSpecOptions
{
    public MassIndexSpecOptions(int emaLength, int sumLength)
    {
        EmaLength = Math.Max(1, emaLength);
        SumLength = Math.Max(1, sumLength);
    }

    public int EmaLength { get; }
    public int SumLength { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Chaikin Volatility indicator options.
/// </summary>
public sealed class ChaikinVolatilitySpecOptions : IIndicatorSpecOptions
{
    public ChaikinVolatilitySpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// Awesome Oscillator indicator options.
/// </summary>
public sealed class AwesomeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public AwesomeOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Accelerator Oscillator indicator options.
/// </summary>
public sealed class AcceleratorOscillatorSpecOptions : IIndicatorSpecOptions
{
    public AcceleratorOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// VIDYA (Variable Index Dynamic Average) indicator options.
/// </summary>
public sealed class VidyaSpecOptions : IIndicatorSpecOptions
{
    public VidyaSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// SuperTrend indicator options.
/// </summary>
public sealed class SuperTrendSpecOptions : IIndicatorSpecOptions
{
    public SuperTrendSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Balance of Power indicator options.
/// </summary>
public sealed class BalanceOfPowerSpecOptions : IIndicatorSpecOptions
{
    public BalanceOfPowerSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
        Momentum = Math.Max(1, momentum);
    }

    public int Length { get; }
    public int Momentum { get; }
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

    public int Length { get; }
}

/// <summary>
/// Elliott Wave Oscillator indicator options.
/// </summary>
public sealed class ElliottWaveOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ElliottWaveOscillatorSpecOptions(int fastLength, int slowLength)
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
    }

    public int FastLength { get; }
    public int SlowLength { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Chandelier Exit Short indicator options.
/// </summary>
public sealed class ChandelierExitShortSpecOptions : IIndicatorSpecOptions
{
    public ChandelierExitShortSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Smoothed ROC indicator options.
/// </summary>
public sealed class SmoothedRocSpecOptions : IIndicatorSpecOptions
{
    public SmoothedRocSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Derivative Oscillator indicator options.
/// </summary>
public sealed class DerivativeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DerivativeOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// Disparity Index indicator options.
/// </summary>
public sealed class DisparityIndexSpecOptions : IIndicatorSpecOptions
{
    public DisparityIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Dynamic Momentum Index indicator options.
/// </summary>
public sealed class DynamicMomentumIndexSpecOptions : IIndicatorSpecOptions
{
    public DynamicMomentumIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    public AccumulativeSwingIndexSpecOptions(double limitMove = 0)
    {
        LimitMove = limitMove;
    }

    public double LimitMove { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ergodic Candlestick Oscillator indicator options.
/// </summary>
public sealed class ErgodicCandlestickOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ErgodicCandlestickOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int ShortLength { get; }
    public int LongLength { get; }
}

/// <summary>
/// Chande Kroll R-Squared Index indicator options.
/// </summary>
public sealed class ChandeKrollRSquaredIndexSpecOptions : IIndicatorSpecOptions
{
    public ChandeKrollRSquaredIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Bayesian Oscillator indicator options.
/// </summary>
public sealed class BayesianOscillatorSpecOptions : IIndicatorSpecOptions
{
    public BayesianOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Anchored Momentum indicator options.
/// </summary>
public sealed class AnchoredMomentumSpecOptions : IIndicatorSpecOptions
{
    public AnchoredMomentumSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Chartmill Value Indicator options.
/// </summary>
public sealed class ChartmillValueIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ChartmillValueIndicatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int MinLength { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
    }

    public int Length { get; }
    public double Multiplier { get; }
}

/// <summary>
/// Compound Ratio Moving Average indicator options.
/// </summary>
public sealed class CompoundRatioMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public CompoundRatioMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Conditional Accumulator indicator options.
/// </summary>
public sealed class ConditionalAccumulatorSpecOptions : IIndicatorSpecOptions
{
    public ConditionalAccumulatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
        AnnualizationFactor = Math.Max(1, annualizationFactor);
    }

    public int Length { get; }
    public int AnnualizationFactor { get; }
}

/// <summary>
/// Average True Range Channel indicator options.
/// </summary>
public sealed class AverageTrueRangeChannelSpecOptions : IIndicatorSpecOptions
{
    public AverageTrueRangeChannelSpecOptions(int length = 14, double multiplier = 2)
    {
        Length = Math.Max(1, length);
        Multiplier = multiplier;
    }

    public int Length { get; }
    public double Multiplier { get; }
}

/// <summary>
/// Volatility Ratio indicator options.
/// </summary>
public sealed class VolatilityRatioSpecOptions : IIndicatorSpecOptions
{
    public VolatilityRatioSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    public DoubleSmoothedMomentaSpecOptions(int momentumLength = 1, int firstSmooth = 25, int secondSmooth = 13)
    {
        MomentumLength = Math.Max(1, momentumLength);
        FirstSmooth = Math.Max(1, firstSmooth);
        SecondSmooth = Math.Max(1, secondSmooth);
    }

    public int MomentumLength { get; }
    public int FirstSmooth { get; }
    public int SecondSmooth { get; }
}

/// <summary>
/// High Low Index indicator options.
/// </summary>
public sealed class HighLowIndexSpecOptions : IIndicatorSpecOptions
{
    public HighLowIndexSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Repulse indicator options.
/// </summary>
public sealed class RepulseSpecOptions : IIndicatorSpecOptions
{
    public RepulseSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Gann Hi-Lo Activator indicator options.
/// </summary>
public sealed class GannHiLoActivatorSpecOptions : IIndicatorSpecOptions
{
    public GannHiLoActivatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Half Trend indicator options.
/// </summary>
public sealed class HalfTrendSpecOptions : IIndicatorSpecOptions
{
    public HalfTrendSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// Double Stochastic Oscillator indicator options.
/// </summary>
public sealed class DoubleStochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DoubleStochasticOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// DT Oscillator indicator options.
/// </summary>
public sealed class DTOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DTOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Compare Price Momentum Oscillator indicator options.
/// </summary>
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Demand Oscillator indicator options.
/// </summary>
public sealed class DemandOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DemandOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// Dynamic Momentum Oscillator indicator options.
/// </summary>
public sealed class DynamicMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public DynamicMomentumOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Average Money Flow Oscillator indicator options.
/// </summary>
public sealed class AverageMoneyFlowOscillatorSpecOptions : IIndicatorSpecOptions
{
    public AverageMoneyFlowOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// DMI Stochastic indicator options.
/// </summary>
public sealed class DMIStochasticSpecOptions : IIndicatorSpecOptions
{
    public DMIStochasticSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// Bilateral Stochastic Oscillator indicator options.
/// </summary>
public sealed class BilateralStochasticOscillatorSpecOptions : IIndicatorSpecOptions
{
    public BilateralStochasticOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// Chande Momentum Oscillator Filter indicator options.
/// </summary>
public sealed class ChandeMomentumOscillatorFilterSpecOptions : IIndicatorSpecOptions
{
    public ChandeMomentumOscillatorFilterSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Grover Llorens Cycle Oscillator indicator options.
/// </summary>
public sealed class GroverLlorensCycleOscillatorSpecOptions : IIndicatorSpecOptions
{
    public GroverLlorensCycleOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
/// Linda Raschke 3-10 Oscillator indicator options.
/// </summary>
public sealed class LindaRaschke310OscillatorSpecOptions : IIndicatorSpecOptions
{
    public LindaRaschke310OscillatorSpecOptions(int length)
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Percent Change Oscillator indicator options.
/// </summary>
public sealed class PercentChangeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PercentChangeOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Price Cycle Oscillator indicator options.
/// </summary>
public sealed class PriceCycleOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PriceCycleOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// Projection Oscillator indicator options.
/// </summary>
public sealed class ProjectionOscillatorSpecOptions : IIndicatorSpecOptions
{
    public ProjectionOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Rainbow Oscillator indicator options.
/// </summary>
public sealed class RainbowOscillatorSpecOptions : IIndicatorSpecOptions
{
    public RainbowOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Sentiment Zone Oscillator indicator options.
/// </summary>
public sealed class SentimentZoneOscillatorSpecOptions : IIndicatorSpecOptions
{
    public SentimentZoneOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Smoothed Delta Ratio Oscillator indicator options.
/// </summary>
public sealed class SmoothedDeltaRatioOscillatorSpecOptions : IIndicatorSpecOptions
{
    public SmoothedDeltaRatioOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Fast Slow Degree Oscillator indicator options.
/// </summary>
public sealed class FastSlowDegreeOscillatorSpecOptions : IIndicatorSpecOptions
{
    public FastSlowDegreeOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Robust Weighting Oscillator indicator options.
/// </summary>
public sealed class RobustWeightingOscillatorSpecOptions : IIndicatorSpecOptions
{
    public RobustWeightingOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Kase Peak Oscillator V2 indicator options.
/// </summary>
public sealed class KasePeakOscillatorV2SpecOptions : IIndicatorSpecOptions
{
    public KasePeakOscillatorV2SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Stochastic Custom Oscillator indicator options.
/// </summary>
public sealed class StochasticCustomOscillatorSpecOptions : IIndicatorSpecOptions
{
    public StochasticCustomOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Pivot Detector Oscillator indicator options.
/// </summary>
public sealed class PivotDetectorOscillatorSpecOptions : IIndicatorSpecOptions
{
    public PivotDetectorOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Tick Line Momentum Oscillator indicator options.
/// </summary>
public sealed class TickLineMomentumOscillatorSpecOptions : IIndicatorSpecOptions
{
    public TickLineMomentumOscillatorSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Ehlers Decycler Oscillator V2 indicator options.
/// </summary>
public sealed class EhlersDecyclerOscillatorV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersDecyclerOscillatorV2SpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

// ========== Batch 7 SpecOptions ==========

/// <summary>
/// Ultimate Moving Average indicator options.
/// </summary>
public sealed class UltimateMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public UltimateMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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

    public int Length { get; }
}

/// <summary>
/// Slow Smoothed Moving Average indicator options.
/// </summary>
public sealed class SlowSmoothedMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public SlowSmoothedMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Repulsion Moving Average indicator options.
/// </summary>
public sealed class RepulsionMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public RepulsionMovingAverageSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
/// Ehlers High Pass Filter V2 indicator options.
/// </summary>
public sealed class EhlersHighPassFilterV2SpecOptions : IIndicatorSpecOptions
{
    public EhlersHighPassFilterV2SpecOptions(int length)
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Fast Z-Score indicator options.
/// </summary>
public sealed class FastZScoreSpecOptions : IIndicatorSpecOptions
{
    public FastZScoreSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
}

/// <summary>
/// Keltner Channel Width indicator options.
/// </summary>
public sealed class KeltnerChannelWidthSpecOptions : IIndicatorSpecOptions
{
    public KeltnerChannelWidthSpecOptions(int length)
    {
        Length = Math.Max(1, length);
    }

    public int Length { get; }
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
    {
        Length = Math.Max(1, length);
        Factor = factor;
    }
    public int Length { get; }
    public double Factor { get; }
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
    public CorrectedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public EdgePreservingFilterSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public int Length { get; }
}

/// <summary>
/// Ehlers Variable Index Dynamic Average indicator options.
/// </summary>
public sealed class EhlersVariableIndexDynamicAverageSpecOptions : IIndicatorSpecOptions
{
    public EhlersVariableIndexDynamicAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public FisherLeastSquaresMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Following Adaptive Moving Average indicator options.
/// </summary>
public sealed class FollowingAdaptiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public FollowingAdaptiveMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
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
    public LinearRegressionLineSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public McNichollMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public MovingAverageV3SpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// One LC Least Squares Moving Average indicator options.
/// </summary>
public sealed class OneLCLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public OneLCLeastSquaresMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public OvershootReductionMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public EquityMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public QuadraticRegressionSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// R2 Adaptive Regression indicator options.
/// </summary>
public sealed class R2AdaptiveRegressionSpecOptions : IIndicatorSpecOptions
{
    public R2AdaptiveRegressionSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public SequentiallyFilteredMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Setting Less Trend Step Filtering indicator options.
/// </summary>
public sealed class SettingLessTrendStepFilteringSpecOptions : IIndicatorSpecOptions
{
    public SettingLessTrendStepFilteringSpecOptions(int length) { Length = Math.Max(1, length); }
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
    public SharpModifiedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public TillsonIE2SpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// T-Step Least Squares Moving Average indicator options.
/// </summary>
public sealed class TStepLeastSquaresMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public TStepLeastSquaresMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Variable Adaptive Moving Average indicator options.
/// </summary>
public sealed class VariableAdaptiveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VariableAdaptiveMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Variable Length Moving Average indicator options.
/// </summary>
public sealed class VariableLengthMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VariableLengthMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public VolatilityMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
}

/// <summary>
/// Volatility Wave Moving Average indicator options.
/// </summary>
public sealed class VolatilityWaveMovingAverageSpecOptions : IIndicatorSpecOptions
{
    public VolatilityWaveMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public MiddleHighLowMovingAverageSpecOptions(int length1, int length2 = 10) { Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); }
    public int Length1 { get; }
    public int Length2 { get; }
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
    public VolumeWeightedMovingAverageSpecOptions(int length) { Length = Math.Max(1, length); }
    public int Length { get; }
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
    public int Length { get; }
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
    public double Alpha { get; }
}

/// <summary>
/// Ehlers Roofing Filter indicator options.
/// </summary>
public sealed class EhlersRoofingFilterSpecOptions : IIndicatorSpecOptions
{
    public EhlersRoofingFilterSpecOptions(int hpLength, int lpLength = 48) { HpLength = Math.Max(1, hpLength); LpLength = Math.Max(1, lpLength); }
    public int HpLength { get; }
    public int LpLength { get; }
}

// === Batch 28 SpecOptions ===

/// <summary>
/// Ehlers Deviation Scaled Super Smoother indicator options.
/// </summary>
public sealed class EhlersDeviationScaledSuperSmootherSpecOptions : IIndicatorSpecOptions
{
    public EhlersDeviationScaledSuperSmootherSpecOptions(int length, int poles = 2) { Length = Math.Max(1, length); Poles = poles; }
    public int Length { get; }
    public int Poles { get; }
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
    public ElasticVolumeWeightedMovingAverageV2SpecOptions(int length) { Length = Math.Max(1, length); }
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
    {
        FastLength = Math.Max(1, fastLength);
        SlowLength = Math.Max(1, slowLength);
    }
    public int FastLength { get; }
    public int SlowLength { get; }
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
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
    }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>
/// TurboTrigger indicator options.
/// </summary>
public sealed class TurboTriggerSpecOptions : IIndicatorSpecOptions
{
    public TurboTriggerSpecOptions(int length = 100, double pctMultiplier = 1.0)
    {
        Length = Math.Max(1, length);
        PctMultiplier = pctMultiplier;
    }
    public int Length { get; }
    public double PctMultiplier { get; }
}

/// <summary>
/// TurboScaler indicator options.
/// </summary>
public sealed class TurboScalerSpecOptions : IIndicatorSpecOptions
{
    public TurboScalerSpecOptions(int length = 50, double pctMultiplier = 1.0)
    {
        Length = Math.Max(1, length);
        PctMultiplier = pctMultiplier;
    }
    public int Length { get; }
    public double PctMultiplier { get; }
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
    {
        Length1 = Math.Max(1, length1);
        Length2 = Math.Max(1, length2);
    }
    public int Length1 { get; }
    public int Length2 { get; }
}

/// <summary>
/// Value Chart Indicator options.
/// </summary>
public sealed class ValueChartIndicatorSpecOptions : IIndicatorSpecOptions
{
    public ValueChartIndicatorSpecOptions(int length = 5, int numAtrs = 8)
    {
        Length = Math.Max(1, length);
        NumAtrs = Math.Max(1, numAtrs);
    }
    public int Length { get; }
    public int NumAtrs { get; }
}

/// <summary>
/// Sell Gravitation Index indicator options.
/// </summary>
public sealed class SellGravitationIndexSpecOptions : IIndicatorSpecOptions
{
    public SellGravitationIndexSpecOptions(int length = 20) { Length = Math.Max(1, length); }
    public int Length { get; }
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
