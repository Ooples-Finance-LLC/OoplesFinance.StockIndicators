namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Long, medium, and short-term technical rank component periods.</summary>
public sealed class TechnicalRankSpecOptions : IIndicatorSpecOptions
{
    public TechnicalRankSpecOptions(int length1 = 200, int length2 = 125, int length3 = 50, int length4 = 20,
        int length5 = 12, int length6 = 26, int length7 = 9, int length8 = 3, int length9 = 14)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4); Length5 = Math.Max(1, length5); Length6 = Math.Max(1, length6);
        Length7 = Math.Max(1, length7); Length8 = Math.Max(1, length8); Length9 = Math.Max(1, length9);
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
}
