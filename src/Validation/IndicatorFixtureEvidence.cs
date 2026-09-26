namespace OoplesFinance.StockIndicators.Validation;

/// <summary>Executed evidence for one named input case, including partial failures.
/// A passed fixture establishes only the checks actually recorded for that input.</summary>
public sealed class IndicatorFixtureEvidence
{
    internal IndicatorFixtureEvidence(string name, int inputBars, int valuesChecked,
        int inputRejectionsChecked, bool completed, bool passed, bool customerResetChecked = false, int outputOverflowRejectionsChecked = 0, int? outputOverflowBarIndex = null,
        int? outputOverflowSlot = null, int? outputOverflowSign = null)
    {
        Name = name; InputBars = inputBars; ValuesChecked = valuesChecked;
        InputRejectionsChecked = inputRejectionsChecked; Completed = completed; Passed = passed;
        CustomerResetChecked = customerResetChecked;
        OutputOverflowRejectionsChecked = outputOverflowRejectionsChecked;
        OutputOverflowBarIndex = outputOverflowBarIndex; OutputOverflowSlot = outputOverflowSlot; OutputOverflowSign = outputOverflowSign;
    }
    public string Name { get; }
    /// <summary>Requested bars per series, before replacing rejected observations with recovery bars.</summary>
    public int InputBars { get; }
    public int ValuesChecked { get; }
    public int InputRejectionsChecked { get; }
    /// <summary>Fresh executions rejected at an independently proven output overflow. Later bars were not evaluated.</summary>
    public int OutputOverflowRejectionsChecked { get; }
    public int? OutputOverflowBarIndex { get; }
    public int? OutputOverflowSlot { get; }
    public int? OutputOverflowSign { get; }
    /// <summary>False when an execution failure interrupted the fixture.</summary>
    public bool Completed { get; }
    public bool Passed { get; }
    /// <summary>A customer state replayed the fixture identically before and after reset.</summary>
    public bool CustomerResetChecked { get; }
}
