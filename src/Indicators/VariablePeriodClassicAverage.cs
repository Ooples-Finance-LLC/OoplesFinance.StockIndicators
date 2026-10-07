using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A classical average selected independently on each bar by a variable period.</summary>
/// <remarks>The selector must be a pure function of the supplied bar. It is evaluated once
/// per published observation, truncated toward zero and clamped to the configured limits.
/// Nonfinite selections are rejected before changing this state. All selected averages
/// align their first output with the maximum-period lookback. Mean seeds use the window
/// ending at that boundary; first-price exponential seeds start at the first input while
/// subsequent DEMA/TEMA stages begin at their aligned boundaries. History and selected
/// engines grow lazily; no array is allocated from a requested period. Reset discards both.
/// The selector sees the same bar as the average, including a chained close.</remarks>
public sealed class VariablePeriodClassicAverage
    : MultiOutputIndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates an average with ordered positive period limits and a pure period selector.</summary>
    public VariablePeriodClassicAverage(
        Func<Bar, double> periodSelector,
        int minimumPeriod = 2,
        int maximumPeriod = 30,
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool firstPriceSeed = false,
        int suppression = 0
    )
        : base(2)
    {
        ArgumentNullException.ThrowIfNull(periodSelector);
        if (minimumPeriod < 1 || minimumPeriod > maximumPeriod)
            throw new ArgumentOutOfRangeException(nameof(minimumPeriod));
        MaximumAverage = new(maximumPeriod, method, firstPriceSeed, suppression);
        PeriodSelector = periodSelector;
        MinimumPeriod = minimumPeriod;
        MaximumPeriod = maximumPeriod;
        Method = method;
        FirstPriceSeed = firstPriceSeed;
        Suppression = suppression;
    }

    /// <summary>Pure per-bar selection, evaluated after startup.</summary>
    public Func<Bar, double> PeriodSelector { get; }

    /// <summary>Smallest selected period.</summary>
    public int MinimumPeriod { get; }

    /// <summary>Largest selected period; determines the common publication boundary.</summary>
    public int MaximumPeriod { get; }

    /// <summary>Formula used for every selected period.</summary>
    public ClassicAverageMethod Method { get; }

    /// <summary>First-price exponential seeding.</summary>
    public bool FirstPriceSeed { get; }

    /// <summary>Additional averaging startup suppression.</summary>
    public int Suppression { get; }

    /// <summary>Selected average, or zero before publication.</summary>
    public IIndicatorOutput Average => Outputs[0];

    /// <summary>One when a selected average is present.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];
    internal ClassicMovingAverage MaximumAverage { get; }

    /// <inheritdoc/>
    public override int WarmupBars => MaximumAverage.WarmupBars;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        VariablePeriodAverageReference
                            .Values(bars, this)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(VariablePeriodClassicAverage owner) : IMultiOutputState, IDisposable
    {
        private sealed class Entry(IMultiOutputState state)
        {
            internal readonly IMultiOutputState State = state;
            internal int Next;
        }

        private readonly List<Bar> _history = new();
        private readonly Dictionary<int, Entry> _engines = new();

        public void Dispose()
        {
            foreach (var entry in _engines.Values)
                if (entry.State is IDisposable disposable)
                    disposable.Dispose();
            _engines.Clear();
            _history.Clear();
        }

        public void Reset() => Dispose();

        public void Update(in Bar bar, Span<double> outputs)
        {
            var publish = _history.Count >= owner.MaximumAverage.First;
            var period = owner.MinimumPeriod;
            if (publish)
            {
                var selection = owner.PeriodSelector(bar);
                if (!double.IsFinite(selection))
                    throw new ArgumentOutOfRangeException(
                        nameof(owner.PeriodSelector),
                        "The selected period must be finite."
                    );
                period =
                    selection <= owner.MinimumPeriod ? owner.MinimumPeriod
                    : selection >= owner.MaximumPeriod ? owner.MaximumPeriod
                    : (int)selection;
            }
            outputs.Clear();
            _history.Add(bar);
            if (!publish)
                return;
            if (!_engines.TryGetValue(period, out var entry))
            {
                var average = new ClassicMovingAverage(
                    period,
                    owner.Method,
                    owner.FirstPriceSeed,
                    owner.Suppression
                );
                entry = new(
                    (IMultiOutputState)average.CreateAlignedState(owner.MaximumAverage.First)
                );
                _engines.Add(period, entry);
            }
            while (entry.Next < _history.Count)
                entry.State.Update(_history[entry.Next++], outputs);
        }
    }
}
