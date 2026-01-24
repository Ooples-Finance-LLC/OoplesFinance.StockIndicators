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
