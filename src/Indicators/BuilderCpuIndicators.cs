using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

// Complete-history calculations cannot be represented by a live Update method.
// Kept internal: this bridge does not change the public custom-state contract.
internal interface IHistoricalIndicator
{
    double[][] CalculateHistory(IReadOnlyList<Bar> bars);
}

/// <summary>Volatility-adaptive Jurik smoothing, using the JurikAdaptiveSnapshot recurrence.</summary>
public sealed class JurikAdaptive : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the adaptive recurrence with exact rounded stages.</summary>
    public JurikAdaptive(int period = 20, double phase = 0, int volatilityPeriod = 10)
    {
        if (period < 1 || volatilityPeriod < 1 || !Helpers.FrameworkCompatibility.IsFinite(phase))
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period; Phase = phase; VolatilityPeriod = volatilityPeriod;
    }
    /// <summary>Long volatility window.</summary>
    public int Period { get; }
    /// <summary>Phase response parameter.</summary>
    public double Phase { get; }
    /// <summary>Short volatility window.</summary>
    public int VolatilityPeriod { get; }
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.ReferenceWithOverflowRejection(0,
            bars => BuilderCpuReferences.Jurik(bars, Period, Phase, VolatilityPeriod), IndicatorErrorBudget.Exact)];
    /// <inheritdoc/>
    // The builder's output policy rejects infinity with its precise slot/bar
    // evidence; standalone kernels retain their immediate OverflowException.
    protected internal override object CreateState() => new State(
        new JurikCpuKernel(Period, Phase, VolatilityPeriod, Math.Max(Period, VolatilityPeriod), rejectOverflow: false));

    private sealed class State(IndicatorKernel kernel) : IIndicatorState
    {
        public void Reset() => kernel.Reset();
        public double Update(in Bar bar)
        {
            Span<double> value = stackalloc double[1];
            kernel.Update(bar, value);
            return value[0];
        }
    }
}

/// <summary>Previous-window pivot levels, with explicit presence outputs.</summary>
/// <remarks>Absent values are zero and their corresponding IsDefined output is zero.
/// Values and presence flags follow PP,S1,S2,S3,S4,R1,R2,R3,R4 order.</remarks>
public sealed class RollingPivotLevels : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates pivots over a preceding window with an optional gap.</summary>
    public RollingPivotLevels(int period = 20, int offset = 0, PivotLevelStyle style = PivotLevelStyle.Standard) : base(18)
    {
        if (period < 1 || offset < 0 || (long)period + offset > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (style is < PivotLevelStyle.Standard or > PivotLevelStyle.Woodie)
            throw new ArgumentOutOfRangeException(nameof(style));
        Period = period; Offset = offset; Style = style;
    }
    /// <summary>Number of bars in the preceding window.</summary>
    public int Period { get; }
    /// <summary>Bars skipped before the current bar.</summary>
    public int Offset { get; }
    /// <summary>Pivot formula.</summary>
    public PivotLevelStyle Style { get; }
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules => Enumerable.Range(0, 18).Select(slot =>
        IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
            bars => BuilderCpuReferences.Pivots(bars, Period, Offset, Style)[slot], IndicatorErrorBudget.Exact));
    /// <inheritdoc/>
    public override int WarmupBars => Period + Offset;
    /// <summary>Pivot point.</summary>
    public IIndicatorOutput PP => Outputs[0];
    /// <summary>First support.</summary>
    public IIndicatorOutput S1 => Outputs[1];
    /// <summary>Second support.</summary>
    public IIndicatorOutput S2 => Outputs[2];
    /// <summary>Third support.</summary>
    public IIndicatorOutput S3 => Outputs[3];
    /// <summary>Fourth support.</summary>
    public IIndicatorOutput S4 => Outputs[4];
    /// <summary>First resistance.</summary>
    public IIndicatorOutput R1 => Outputs[5];
    /// <summary>Second resistance.</summary>
    public IIndicatorOutput R2 => Outputs[6];
    /// <summary>Third resistance.</summary>
    public IIndicatorOutput R3 => Outputs[7];
    /// <summary>Fourth resistance.</summary>
    public IIndicatorOutput R4 => Outputs[8];
    /// <summary>Presence flag for a value output belonging to this indicator.</summary>
    public IIndicatorOutput IsDefined(IIndicatorOutput level)
    {
        if (level is null) throw new ArgumentNullException(nameof(level));
        if (!ReferenceEquals(level.Indicator, this) || level.Slot < 0 || level.Slot >= 9)
            throw new ArgumentException("Expected a pivot value output from this indicator.", nameof(level));
        return Outputs[9 + level.Slot];
    }
    /// <inheritdoc/>
    protected internal override object CreateState() => new State(new PivotCpuKernel(Period, Offset, Style, rejectOverflow: false));
    private sealed class State(IndicatorKernel kernel) : IMultiOutputState
    {
        public void Reset() => kernel.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            kernel.Update(bar, outputs);
            for (var i = 0; i < 9; i++)
            {
                outputs[9 + i] = double.IsNaN(outputs[i]) ? 0 : 1;
                if (outputs[9 + i] == 0) outputs[i] = 0;
            }
        }
    }
}

/// <summary>Strict retrospective fractals at their center bars, for finite builder sources.</summary>
/// <remarks>Requires rightSpan future bars. Live sources are rejected; this indicator must
/// not be used as a causal trading signal. Absent values are zero with a zero presence flag.</remarks>
public sealed class RetrospectiveFractals : MultiOutputIndicatorBase, IHistoricalIndicator, IIndicatorValidationContract
{
    /// <summary>Creates a complete-history high/low or close fractal calculation.</summary>
    public RetrospectiveFractals(int leftSpan = 2, int rightSpan = 2, bool useClose = false) : base(4)
    {
        if (leftSpan < 2) throw new ArgumentOutOfRangeException(nameof(leftSpan));
        if (rightSpan < 2) throw new ArgumentOutOfRangeException(nameof(rightSpan));
        LeftSpan = leftSpan; RightSpan = rightSpan; UseClose = useClose;
    }
    /// <summary>Bars required before the center.</summary>
    public int LeftSpan { get; }
    /// <summary>Future bars required after the center.</summary>
    public int RightSpan { get; }
    /// <summary>Whether to compare closes instead of highs and lows.</summary>
    public bool UseClose { get; }
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules => Enumerable.Range(0, 4).Select(slot =>
        IndicatorValidationRule.Reference(slot,
            bars => BuilderCpuReferences.Fractals(bars, LeftSpan, RightSpan, UseClose)[slot], IndicatorErrorBudget.Exact));
    /// <summary>Strict local high, or zero when absent.</summary>
    public IIndicatorOutput Bear => Outputs[0];
    /// <summary>Strict local low, or zero when absent.</summary>
    public IIndicatorOutput Bull => Outputs[1];
    /// <summary>One when Bear is present.</summary>
    public IIndicatorOutput BearIsDefined => Outputs[2];
    /// <summary>One when Bull is present.</summary>
    public IIndicatorOutput BullIsDefined => Outputs[3];
    double[][] IHistoricalIndicator.CalculateHistory(IReadOnlyList<Bar> bars)
    {
        var points = FractalSnapshot.Calculate(bars, LeftSpan, RightSpan, UseClose);
        var result = new[] { new double[bars.Count], new double[bars.Count], new double[bars.Count], new double[bars.Count] };
        for (var i = 0; i < bars.Count; i++)
        {
            result[0][i] = points[i].Bear ?? 0; result[1][i] = points[i].Bull ?? 0;
            result[2][i] = points[i].Bear.HasValue ? 1 : 0; result[3][i] = points[i].Bull.HasValue ? 1 : 0;
        }
        return result;
    }
}
