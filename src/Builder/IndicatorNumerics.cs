namespace OoplesFinance.StockIndicators.Builder;

/// <summary>Selects the arithmetic an indicator build uses.</summary>
public enum IndicatorNumerics
{
    /// <summary>Correctly rounded results from exact intermediate arithmetic.</summary>
    Exact,

    /// <summary>
    /// The default. Ordinary IEEE 754 double arithmetic over borrowed input columns. Results agree with
    /// <see cref="Exact"/> to within ordinary floating-point rounding rather than bit for bit, only the
    /// input fields an indicator reads are validated, and the source array is read without being copied,
    /// so it must not be changed while the run is in use. Exponential stages use a fused multiply-add where
    /// the processor has one, so the last bits of a result can differ between machines. Non-finite outputs
    /// are rejected as they are on the exact route.
    /// </summary>
    Fast
}
