namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Roofing and Fourier scan periods for the spectral cycle estimate.</summary>
public sealed class EhlersDiscreteFourierTransformSpectralEstimateSpecOptions : IIndicatorSpecOptions
{
    public EhlersDiscreteFourierTransformSpectralEstimateSpecOptions(int length1 = 48, int length2 = 10)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2);
    }
    public int Length1 { get; }
    public int Length2 { get; }
}
