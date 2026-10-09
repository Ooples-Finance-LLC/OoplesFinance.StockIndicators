namespace OoplesFinance.StockIndicators.Builder;

/// <summary>Selects the execution backend for a finite typed builder run.</summary>
public enum IndicatorExecutionBackend
{
    /// <summary>Currently uses CPU execution; automatic GPU selection awaits a measured crossover.</summary>
    Auto,
    /// <summary>Execute on the CPU.</summary>
    Cpu,
    /// <summary>Require GPU execution; unsupported configurations or data throw instead of falling back.</summary>
    Gpu
}

/// <summary>Reports the actual backend used by the last successful builder run.</summary>
public sealed class IndicatorExecutionInfo
{
    /// <summary>The backend that actually performed the indicator arithmetic.</summary>
    public IndicatorExecutionBackend Backend { get; }
    /// <summary>The physical GPU name, or null for CPU execution.</summary>
    public string? DeviceName { get; }
    /// <summary>Explains automatic selection or fallback.</summary>
    public string Reason { get; }

    internal IndicatorExecutionInfo(IndicatorExecutionBackend backend, string? deviceName, string reason)
    { Backend = backend; DeviceName = deviceName; Reason = reason; }
}
