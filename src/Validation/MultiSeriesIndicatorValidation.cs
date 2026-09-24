using System.Reflection;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using SeriesKey = OoplesFinance.StockIndicators.Streaming.SeriesKey;

namespace OoplesFinance.StockIndicators.Validation;

/// <summary>Independent formula over explicitly paired primary and benchmark bars.</summary>
public delegate IReadOnlyDictionary<string, IReadOnlyList<double>> MultiSeriesFormulaReference(
    IReadOnlyList<Bar> primary, IReadOnlyList<Bar> benchmark);

/// <summary>Optional discovery metadata for customer two-series states.</summary>
public interface IMultiSeriesIndicatorValidationContract
{
    IReadOnlyList<string> ValidationOutputKeys { get; }
    MultiSeriesFormulaReference? FormulaReference { get; }
}

/// <summary>A fresh two-series state factory, its published outputs, and an optional independent formula.</summary>
public sealed class MultiSeriesIndicatorValidationCase
{
    public MultiSeriesIndicatorValidationCase(Type indicatorType, string name,
        Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> factory, IEnumerable<string> outputKeys,
        MultiSeriesFormulaReference? reference = null, IReadOnlyDictionary<string, IndicatorErrorBudget>? errorBudgets = null,
        IndicatorInputDomain? primaryDomain = null, IndicatorInputDomain? benchmarkDomain = null)
    {
        if (indicatorType is null || !typeof(IMultiSeriesIndicatorState).IsAssignableFrom(indicatorType))
            throw new ArgumentException("The type must implement IMultiSeriesIndicatorState.", nameof(indicatorType));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A case needs a name.", nameof(name));
        IndicatorType = indicatorType; Name = name; Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        var keys = (outputKeys ?? throw new ArgumentNullException(nameof(outputKeys))).ToArray();
        if (keys.Length == 0 || keys.Any(string.IsNullOrWhiteSpace) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
            throw new ArgumentException("Declare distinct nonempty output keys.", nameof(outputKeys));
        OutputKeys = Array.AsReadOnly(keys); Reference = reference;
        PrimaryDomain = primaryDomain ?? IndicatorInputDomain.Finite; BenchmarkDomain = benchmarkDomain ?? IndicatorInputDomain.Finite;
        if (errorBudgets is not null && (!keys.OrderBy(k => k).SequenceEqual(errorBudgets.Keys.OrderBy(k => k))
            || errorBudgets.Values.Any(b => b is null)))
            throw new ArgumentException("Supply a numerical budget for every output and no undeclared outputs.", nameof(errorBudgets));
        ErrorBudgets = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IndicatorErrorBudget>(
            keys.ToDictionary(k => k, k => errorBudgets is null ? new IndicatorErrorBudget(1e-9, 1e-9) : errorBudgets[k]));
    }
    public Type IndicatorType { get; }
    public string Name { get; }
    public Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> Factory { get; }
    public IReadOnlyList<string> OutputKeys { get; }
    public MultiSeriesFormulaReference? Reference { get; }
    public IReadOnlyDictionary<string, IndicatorErrorBudget> ErrorBudgets { get; }
    public IndicatorInputDomain PrimaryDomain { get; }
    public IndicatorInputDomain BenchmarkDomain { get; }
    public override string ToString() => IndicatorType.FullName+"/"+Name;
}

public static partial class IndicatorValidationDiscovery
{
    /// <summary>Discovers native two-series states, including customer implementations. Register factories
    /// and output contracts for customer states. Unconstructible types become failing cases.</summary>
    public static IReadOnlyList<MultiSeriesIndicatorValidationCase> DiscoverMultiSeries(IEnumerable<Assembly> assemblies,
        IEnumerable<MultiSeriesIndicatorValidationCase>? registrations = null)
    {
        if (assemblies is null) throw new ArgumentNullException(nameof(assemblies));
        var types = assemblies.Distinct().SelectMany(a => a.GetTypes()).Where(t => t.IsClass && !t.IsAbstract &&
            typeof(IMultiSeriesIndicatorState).IsAssignableFrom(t)).OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        if (types.Length == 0) throw new InvalidOperationException("No multi-series states were discovered.");
        var supplied = (registrations ?? Array.Empty<MultiSeriesIndicatorValidationCase>()).ToArray();
        if (supplied.Any(c => c is null || !types.Contains(c.IndicatorType)) || supplied.GroupBy(c => (c.IndicatorType, c.Name)).Any(g => g.Count() > 1))
            throw new ArgumentException("Registrations must target discovered types with distinct case names.", nameof(registrations));
        var result = new List<MultiSeriesIndicatorValidationCase>();
        foreach (var type in types)
        {
            var replacements = supplied.Where(c => c.IndicatorType == type).ToArray();
            if (replacements.Length > 0) { result.AddRange(replacements); continue; }
            var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault(c =>
                c.GetParameters().Count(p => p.ParameterType == typeof(SeriesKey)) == 2 &&
                c.GetParameters().All(p => p.ParameterType == typeof(SeriesKey) || p.IsOptional));
            if (constructor is null || type.ContainsGenericParameters)
            {
                result.Add(new(type, "discovery", (_, _) => throw new InvalidOperationException(
                    "Register a multi-series factory and output contract for "+type.FullName), new[] { "Value" }));
                continue;
            }
            var parameters = constructor.GetParameters();
            var variants = new List<(string Name, double Scale, bool Weighted, string? Period)>
            {
                ("default", 1, false, null), ("minimum-periods", 0, false, null),
                ("shorter-periods", .5, false, null), ("longer-periods", 1.5, false, null),
                ("weighted-average", 1, true, null)
            };
            if (parameters.Count(IsPeriod) > 1)
                foreach (var period in parameters.Where(IsPeriod))
                {
                    variants.Add(("minimum-" + period.Name, 0, false, period.Name));
                    variants.Add(("longer-" + period.Name, 1.5, false, period.Name));
                }
            foreach (var variant in variants)
            {
                if (variant.Scale != 1 && !parameters.Any(IsPeriod)) continue;
                if (variant.Weighted && !parameters.Any(p => p.ParameterType == typeof(MovingAvgType))) continue;
                var arguments = parameters.Where(p => p.ParameterType != typeof(SeriesKey)).ToDictionary(p => p.Name!, p =>
                    IsPeriod(p) ? (object?)Math.Max(1, (int)Math.Round((int)p.DefaultValue!
                        * (variant.Period is null || variant.Period == p.Name ? variant.Scale : 1))) :
                    p.ParameterType == typeof(MovingAvgType) && variant.Weighted ? MovingAvgType.WeightedMovingAverage : p.DefaultValue);
                IMultiSeriesIndicatorState Create(SeriesKey primary, SeriesKey market)
                {
                    var keyIndex = 0;
                    return (IMultiSeriesIndicatorState)constructor.Invoke(parameters.Select(p => p.ParameterType == typeof(SeriesKey)
                        ? (object)(keyIndex++ == 0 ? primary : market) : arguments[p.Name!]).ToArray());
                }
                var probe = Create(MultiSeriesIndicatorValidation.PrimaryKey, MultiSeriesIndicatorValidation.BenchmarkKey);
                try
                {
                    var contract = probe as IMultiSeriesIndicatorValidationContract;
                    if (contract is null && type.Assembly != typeof(IIndicator).Assembly)
                    {
                        result.Add(new(type, variant.Name, (_, _) => throw new InvalidOperationException(
                            "Register an output contract or implement IMultiSeriesIndicatorValidationContract for "+type.FullName), new[] { "Value" }));
                    }
                    else
                    {
                        var keys = contract?.ValidationOutputKeys ?? GeneratedIndicatorOutputs.KeysFor(probe.Name);
                        var domains = probe as IMultiSeriesInputDomainContract;
                        result.Add(new(type, variant.Name, Create, keys, contract?.FormulaReference ?? BuiltInFormulaReferences.MultiSeriesFormula(probe.Name, arguments),
                            primaryDomain: domains?.PrimaryInputDomain, benchmarkDomain: domains?.BenchmarkInputDomain));
                    }
                }
                finally { (probe as IDisposable)?.Dispose(); }
            }
        }
        return result.AsReadOnly();
    }
}

/// <summary>Runs paired-series formula, finite-value, determinism, preview, and reset checks outside production execution.</summary>
public static class MultiSeriesIndicatorValidation
{
    internal static readonly SeriesKey PrimaryKey = new("VALIDATION_PRIMARY", BarTimeframe.Minutes(1));
    internal static readonly SeriesKey BenchmarkKey = new("VALIDATION_BENCHMARK", BarTimeframe.Minutes(1));

    public static IndicatorValidationReport Validate(MultiSeriesIndicatorValidationCase testCase,
        IndicatorValidationOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (testCase is null) throw new ArgumentNullException(nameof(testCase));
        options ??= new();
        if (options.BarsPerFixture < 2 || options.MaximumBarsPerFixture < options.BarsPerFixture) throw new ArgumentOutOfRangeException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();
        var additional = (options.AdditionalFixtures ?? throw new ArgumentNullException(nameof(options.AdditionalFixtures))).ToArray();
        var builtInFixtures = IndicatorValidationFixtures.Create(options.BarsPerFixture, 0, false)
            .Concat(testCase.PrimaryDomain.BoundaryFixtures(options.BarsPerFixture, "primary-domain"))
            .Concat(testCase.BenchmarkDomain.BoundaryFixtures(options.BarsPerFixture, "benchmark-domain")).ToArray();
        if (additional.Any(f => f is null || f.Bars.Count > options.MaximumBarsPerFixture)
            || builtInFixtures.Select(f => f.Name).Concat(additional.Select(f => f.Name)).Distinct(StringComparer.Ordinal).Count()
                != builtInFixtures.Length + additional.Length)
            throw new ArgumentException("Additional fixtures must have unique names and fit the configured bar budget.", nameof(options));
        var failures = new List<IndicatorValidationFailure>(); int fixtures = 0, values = 0, rejections = 0;
        var fixtureEvidence = new List<IndicatorFixtureEvidence>();
        var slots = Enumerable.Range(0, testCase.OutputKeys.Count).ToArray();
        var coverage = new IndicatorFormulaCoverage(slots, testCase.Reference is null ? Array.Empty<int>() : slots);
        if ((options.RequireFormulaReference || options.RequireMathematicalContract) && testCase.Reference is null)
            failures.Add(new("coverage", "FormulaReference", "No independent paired-series formula was registered."));
        var instances = new HashSet<IMultiSeriesIndicatorState>(StateIdentity.Instance);
        foreach (var fixture in builtInFixtures.Concat(additional.Select(f => (f.Name, f.Bars))))
        foreach (var benchmarkShape in new[] { "identical", "constant", "independent", "scaled", "zero" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var label = fixture.Name+"/"+benchmarkShape;
            var initialValues = values; var initialRejections = rejections; var completed = false;
            var initialFailures = failures.Count;
            try
            {
                var benchmark = fixture.Bars.Select((bar, i) => benchmarkShape switch
                {
                    "identical" => bar,
                    "zero" => new Bar(bar.Time, 0, 0, 0, 0, 0),
                    "constant" => new Bar(bar.Time, 50, 50, 50, 50, 1000),
                    "scaled" => new Bar(bar.Time, bar.Open*2, bar.High*2, bar.Low*2, bar.Close*2, bar.Volume),
                    _ => new Bar(bar.Time, 75+4*Math.Sin(i*.19), 81+4*Math.Sin(i*.19), 70+4*Math.Sin(i*.19), 76+4*Math.Sin(i*.19), 2000)
                }).ToArray();
                var originalBenchmark = benchmark;
                var primaryBars = fixture.Bars.Select(b => testCase.PrimaryDomain.Violation(b) is null ? b : testCase.PrimaryDomain.ValidExample(b.Time)).ToArray();
                benchmark = benchmark.Select(b => testCase.BenchmarkDomain.Violation(b) is null ? b : testCase.BenchmarkDomain.ValidExample(b.Time)).ToArray();
                var state = Create();
                try
                {
                    var first = Run(state, primaryBars, benchmark, false, fixture.Bars, originalBenchmark);
                    state.Reset();
                    var replay = Run(state, primaryBars, benchmark, true);
                    var fresh = Create();
                    Dictionary<string, List<double>> second;
                    try { second = Run(fresh, primaryBars, benchmark, false); }
                    finally { (fresh as IDisposable)?.Dispose(); }
                    var expected = testCase.Reference?.Invoke(primaryBars, benchmark);
                    if (expected is not null && !expected.Keys.OrderBy(k => k).SequenceEqual(testCase.OutputKeys.OrderBy(k => k)))
                        throw new InvalidOperationException("Reference must return every declared output and no undeclared outputs.");
                    foreach (var key in testCase.OutputKeys)
                    {
                        if (!first[key].SequenceEqual(second[key]) || !first[key].SequenceEqual(replay[key]))
                            throw new InvalidOperationException("Fresh, reset, and preview replay disagree for "+key);
                        if (expected is not null && expected[key].Count != first[key].Count)
                            throw new InvalidOperationException("Reference output length differs for "+key);
                        for (var i = 0; i < first[key].Count; i++)
                        {
                            values++; var actual = first[key][i];
                            if (double.IsNaN(actual) || double.IsInfinity(actual)) throw new InvalidOperationException($"{key}, bar {i}: nonfinite output.");
                            if (expected is not null)
                            {
                                var wanted = expected[key][i];
                                if (!testCase.ErrorBudgets[key].Accepts(wanted, actual))
                                    throw new InvalidOperationException($"{key}, bar {i}: expected {wanted:R}, got {actual:R}.");
                            }
                        }
                    }
                    fixtures++;
                    completed = true;
                }
                finally { (state as IDisposable)?.Dispose(); }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { failures.Add(new(label, "PairedSeries", ex.GetBaseException().Message)); }
            finally
            {
                fixtureEvidence.Add(new(label, fixture.Bars.Count, values - initialValues,
                    rejections - initialRejections, completed, completed && failures.Count == initialFailures));
            }
        }
        return new(testCase.ToString(), fixtures, values, testCase.Reference is null ? 0 : slots.Length, failures,
            coverage, rejections, fixtureEvidence);


        IMultiSeriesIndicatorState Create()
        {
            var state = testCase.Factory(PrimaryKey, BenchmarkKey);
            if (state is null || state.GetType() != testCase.IndicatorType || !instances.Add(state))
                throw new InvalidOperationException("The factory must return a fresh instance of the declared state type.");
            return state;
        }
        Dictionary<string, List<double>> Run(IMultiSeriesIndicatorState state, IReadOnlyList<Bar> primary, IReadOnlyList<Bar> benchmark, bool preview,
            IReadOnlyList<Bar>? originalPrimary = null, IReadOnlyList<Bar>? originalBenchmark = null)
        {
            var store = new SeriesStore(); var context = new MultiSeriesContext(store);
            var output = testCase.OutputKeys.ToDictionary(k => k, _ => new List<double>());
            for (var i = 0; i < primary.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (i == 0 && originalBenchmark is not null)
                    foreach (var invalid in testCase.BenchmarkDomain.InvalidExamples())
                        Reject(BenchmarkKey, AtTime(invalid, benchmark[i].Time));
                if (originalBenchmark is not null && testCase.BenchmarkDomain.Violation(originalBenchmark[i]) is not null)
                    Reject(BenchmarkKey, originalBenchmark[i]);
                var market = ToBar(benchmark[i], BenchmarkKey, true);
                store.Update(BenchmarkKey, market); state.Update(context, BenchmarkKey, market, true, true);
                if (i == 0 && originalPrimary is not null)
                    foreach (var invalid in testCase.PrimaryDomain.InvalidExamples())
                        Reject(PrimaryKey, AtTime(invalid, primary[i].Time));
                if (originalPrimary is not null && testCase.PrimaryDomain.Violation(originalPrimary[i]) is not null)
                    Reject(PrimaryKey, originalPrimary[i]);
                var bar = ToBar(primary[i], PrimaryKey, true);
                MultiSeriesIndicatorStateResult? speculative = null;
                if (preview)
                {
                    // A speculative next benchmark must not replace the committed benchmark
                    // used for the current primary bar, or contaminate a later final update.
                    var speculativeMarket = testCase.BenchmarkDomain.PreviewExample(
                        AtTime(benchmark[i], benchmark[i].Time.AddMinutes(1)), 10);
                    state.Update(context, BenchmarkKey, ToBar(speculativeMarket, BenchmarkKey, false), false, true);
                    var changed = testCase.PrimaryDomain.PreviewExample(primary[i], .25);
                    state.Update(context, PrimaryKey, ToBar(changed, PrimaryKey, false), false, true);
                    speculative = state.Update(context, PrimaryKey, ToBar(primary[i], PrimaryKey, false), false, true);
                }
                store.Update(PrimaryKey, bar);
                var result = state.Update(context, PrimaryKey, bar, true, true);
                if (!result.HasValue || result.Outputs is null || !result.Outputs.Keys.OrderBy(k => k).SequenceEqual(testCase.OutputKeys.OrderBy(k => k)))
                    throw new InvalidOperationException("A paired bar did not publish exactly the declared outputs.");
                foreach (var key in testCase.OutputKeys)
                {
                    if (speculative is { } tentative && (!tentative.HasValue || tentative.Outputs is null || tentative.Outputs[key] != result.Outputs[key]))
                        throw new InvalidOperationException("Preview differs from final for "+key);
                    output[key].Add(result.Outputs[key]);
                }
            }
            return output;

            Bar AtTime(Bar b, DateTime time) => new(time, b.Open, b.High, b.Low, b.Close, b.Volume);

            void Reject(SeriesKey key, Bar bar)
            {
                try { state.Update(context, key, ToBar(bar, key, true), true, true); }
                catch (ArgumentException) { rejections++; return; }
                throw new InvalidOperationException("The declared input domain was violated but the observation was accepted: " + key);
            }
        }
    }
    public static void ValidateAndThrow(MultiSeriesIndicatorValidationCase testCase, IndicatorValidationOptions? options = null,
        CancellationToken cancellationToken = default) => Validate(testCase, options, cancellationToken).ThrowIfInvalid();

    private sealed class StateIdentity : IEqualityComparer<IMultiSeriesIndicatorState>
    {
        internal static readonly StateIdentity Instance = new();
        public bool Equals(IMultiSeriesIndicatorState? x, IMultiSeriesIndicatorState? y) => ReferenceEquals(x, y);
        public int GetHashCode(IMultiSeriesIndicatorState obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private static OhlcvBar ToBar(Bar bar, SeriesKey key, bool final) => new(key.Symbol, key.Timeframe,
        bar.Time, bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, final);
}
