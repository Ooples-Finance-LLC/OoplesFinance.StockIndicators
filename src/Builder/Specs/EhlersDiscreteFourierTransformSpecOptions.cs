namespace OoplesFinance.StockIndicators.Builder.Specs;

public sealed class EhlersDiscreteFourierTransformSpecOptions : IIndicatorSpecOptions
{
    public EhlersDiscreteFourierTransformSpecOptions(int minLength = 8, int maxLength = 50, int length = 40)
    { MinLength = Math.Max(3, minLength); MaxLength = Math.Max(MinLength, maxLength); Length = Math.Max(1, length); }
    public int MinLength { get; }
    public int MaxLength { get; }
    public int Length { get; }
}
