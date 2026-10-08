namespace OoplesFinance.StockIndicators.Builder.Specs;

public sealed class EhlersDualDifferentiatorDominantCycleSpecOptions : IIndicatorSpecOptions
{
    public EhlersDualDifferentiatorDominantCycleSpecOptions(int length1 = 48, int length2 = 20, int length3 = 8)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3);
        
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    
}

public sealed class EhlersHomodyneDominantCycleSpecOptions : IIndicatorSpecOptions
{
    public EhlersHomodyneDominantCycleSpecOptions(int length1 = 48, int length2 = 20, int length3 = 10)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3);
        
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    
}

public sealed class EhlersPhaseAccumulationDominantCycleSpecOptions : IIndicatorSpecOptions
{
    public EhlersPhaseAccumulationDominantCycleSpecOptions(int length1 = 48, int length2 = 20, int length3 = 10, int length4 = 40)
    {
        Length1 = Math.Max(1, length1); Length2 = Math.Max(1, length2); Length3 = Math.Max(1, length3);
        Length4 = Math.Max(1, length4);
    }
    public int Length1 { get; }
    public int Length2 { get; }
    public int Length3 { get; }
    public int Length4 { get; }
}

