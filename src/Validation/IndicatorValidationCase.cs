using System.Reflection;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

/// <summary>A reproducible indicator configuration. The factory must return a fresh instance each time.</summary>
public sealed class IndicatorValidationCase
{
    public IndicatorValidationCase(Type indicatorType, string name, Func<IIndicator> factory,
        params IndicatorValidationRule[] rules)
    {
        if (indicatorType is null) throw new ArgumentNullException(nameof(indicatorType));
        if (!typeof(IIndicator).IsAssignableFrom(indicatorType))
            throw new ArgumentException("The type must implement IIndicator.", nameof(indicatorType));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A case needs a name.", nameof(name));
        IndicatorType = indicatorType;
        Name = name;
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Rules = Array.AsReadOnly((rules ?? throw new ArgumentNullException(nameof(rules))).ToArray());
        if (Rules.Any(r => r is null)) throw new ArgumentException("A rule cannot be null.", nameof(rules));
    }

    public Type IndicatorType { get; }
    public string Name { get; }
    public Func<IIndicator> Factory { get; }
    public IReadOnlyList<IndicatorValidationRule> Rules { get; }
    public override string ToString() => IndicatorType.FullName + "/" + Name;
}

/// <summary>Discovers concrete indicators in explicitly supplied assemblies, regardless of namespace.</summary>
public static partial class IndicatorValidationDiscovery
{
    /// <summary>
    /// Creates default, minimum-period, shorter-period, longer-period, and alternate-average cases where applicable.
    /// Includes promoted built-in component combinations with independent numerical contracts.
    /// Registrations replace automatic cases for their type and handle dependencies or special parameters.
    /// A type that cannot be constructed becomes a failing case; it is never silently skipped.
    /// Assembly loading failures propagate to the caller.
    /// </summary>
    public static IReadOnlyList<IndicatorValidationCase> Discover(IEnumerable<Assembly> assemblies,
        IEnumerable<IndicatorValidationCase>? registrations = null)
    {
        if (assemblies is null) throw new ArgumentNullException(nameof(assemblies));
        var types = assemblies.Distinct().SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IIndicator).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        if (types.Length == 0) throw new InvalidOperationException("No concrete indicators were discovered.");
        var registered = (registrations ?? Array.Empty<IndicatorValidationCase>()).ToArray();
        if (registered.Any(c => c is null || !types.Contains(c.IndicatorType)))
            throw new ArgumentException("Every registration must target a discovered indicator type.", nameof(registrations));
        if (registered.GroupBy(c => (c.IndicatorType, c.Name)).Any(g => g.Count() > 1))
            throw new ArgumentException("Case names must be unique within an indicator type.", nameof(registrations));

        var result = new List<IndicatorValidationCase>();
        foreach (var type in types)
        {
            var replacements = registered.Where(c => c.IndicatorType == type).ToArray();
            if (replacements.Length > 0) { result.AddRange(replacements); continue; }
            var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault(c => c.GetParameters().All(p => CanSupply(type, p)));
            if (ctor is null || type.ContainsGenericParameters)
            {
                result.Add(new IndicatorValidationCase(type, "discovery", () => throw new InvalidOperationException(
                    "Register a factory for " + type.FullName + "; no supported public constructor was found.")));
                continue;
            }

            var parameters = ctor.GetParameters();
            Add("default", 1, false);
            if (parameters.Any(IsPeriod))
            {
                Add("minimum-periods", 0, false);
                Add("shorter-periods", 0.5, false);
                Add("longer-periods", 1.5, false);
            }
            if (parameters.Count(IsPeriod) > 1)
            {
                // Scaling all periods together never exercises reversed or unequal period relationships.
                foreach (var period in parameters.Where(IsPeriod))
                    foreach (var scale in new[] { 0d, 1.5d })
                    {
                        var selected = period;
                        var selectedScale = scale;
                        result.Add(new IndicatorValidationCase(type,
                            (scale == 0 ? "minimum-" : "longer-") + period.Name,
                            () => (IIndicator)ctor.Invoke(parameters.Select((p, i) =>
                                Argument(type, p, i, p == selected ? selectedScale : 1, false)).ToArray())));
                    }
            }
            if (parameters.Any(p => p.ParameterType == typeof(IMovingAverage) || p.ParameterType == typeof(MovingAvgType)))
                Add("weighted-average", 1, true);

            result.AddRange(SymmetricCompositionCases(type));
            result.AddRange(WeightedCompositionCases(type));
            result.AddRange(PolynomialCompositionCases(type));
            result.AddRange(FoundationCompositionCases(type));
            result.AddRange(RootAndLagCompositionCases(type));
            result.AddRange(KaufmanCompositionCases(type));
            result.AddRange(MiddleCompositionCases(type));
            result.AddRange(SequentialCompositionCases(type));
            result.AddRange(SineNaturalCompositionCases(type));
            result.AddRange(HannCompositionCases(type));
            result.AddRange(VidyaCompositionCases(type));
            result.AddRange(AlmaCompositionCases(type));
            result.AddRange(ChandeCompositionCases(type));
            result.AddRange(StochasticCompositionCases(type));
            result.AddRange(MomentumCompositionCases(type));
            result.AddRange(BalanceCompositionCases(type));
            result.AddRange(HighLowCompositionCases(type));
            result.AddRange(ObvCompositionCases(type));
            result.AddRange(ApoCompositionCases(type));
            result.AddRange(PriceChannelCompositionCases(type));
            result.AddRange(EnvelopeCompositionCases(type));

            void Add(string name, double scale, bool weighted) => result.Add(new IndicatorValidationCase(type, name,
                () => (IIndicator)ctor.Invoke(parameters.Select((p, i) => Argument(type, p, i, scale, weighted)).ToArray())));
        }
        return result.AsReadOnly();
    }

    private static IEnumerable<IndicatorValidationCase> ObvCompositionCases(Type type)
    {
        if (type != typeof(Obv) && type != typeof(OnBalanceVolume)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return type == typeof(Obv) ? new Obv(length, average) : new OnBalanceVolume(length, average);
            }
            yield return new IndicatorValidationCase(type, $"obv-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> ApoCompositionCases(Type type)
    {
        if (type != typeof(Apo) && type != typeof(AbsolutePriceOscillator)) yield break;
        foreach (var (fast, slow) in new[] { (1, 3), (7, 3) })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return type == typeof(Apo) ? new Apo(fast, slow, average) : new AbsolutePriceOscillator(fast, slow, average);
            }
            yield return new IndicatorValidationCase(type, $"apo-composition/{fast}/{slow}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> EnvelopeCompositionCases(Type type)
    {
        if (type != typeof(MovingAverageEnvelope)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return new MovingAverageEnvelope(length, .025, average);
            }
            yield return new IndicatorValidationCase(type, $"envelope-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> PriceChannelCompositionCases(Type type)
    {
        if (type != typeof(PriceChannel)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return new PriceChannel(length, .06, average);
            }
            yield return new IndicatorValidationCase(type, $"price-channel-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> HighLowCompositionCases(Type type)
    {
        if (type != typeof(HighLowIndex)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return new HighLowIndex(length, average);
            }
            yield return new IndicatorValidationCase(type, $"high-low-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> BalanceCompositionCases(Type type)
    {
        if (type != typeof(BalanceOfPower)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return new BalanceOfPower(length, average);
            }
            yield return new IndicatorValidationCase(type, $"balance-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> MomentumCompositionCases(Type type)
    {
        if (type != typeof(MomentumOscillator)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return new MomentumOscillator(length, 3, average);
            }
            yield return new IndicatorValidationCase(type, $"momentum-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> StochasticCompositionCases(Type type)
    {
        if (type != typeof(Stochastic) && type != typeof(StochasticK) && type != typeof(PricePosition)
            && type != typeof(StochasticOscillator) && type != typeof(StochasticRegular)
            && type != typeof(DynamicMomentumOscillator) && type != typeof(DoubleStochasticOscillator)
            && type != typeof(StochasticFastOscillator)) yield break;
        foreach (var (length, first, third) in new[] { (1, 3, 7), (3, 1, 3), (14, 3, 1) })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            var last = type == typeof(Stochastic) ? 3 : third;
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return type == typeof(Stochastic) ? new Stochastic(length, first, average)
                    : type == typeof(StochasticK) ? new StochasticK(length, first, last, average)
                    : type == typeof(DoubleStochasticOscillator) ? new DoubleStochasticOscillator(length, average)
                    : type == typeof(DynamicMomentumOscillator) ? new DynamicMomentumOscillator(length, average)
                    : type == typeof(StochasticFastOscillator) ? new StochasticFastOscillator(length, first, last, average)
                    : type == typeof(StochasticRegular) ? new StochasticRegular(length, first, average)
                    : type == typeof(PricePosition) ? new PricePosition(length, first, last, average)
                    : new StochasticOscillator(length, first, last, average);
            }
            var prefix = type == typeof(DynamicMomentumOscillator) ? "dynamic-momentum-composition"
                : type == typeof(DoubleStochasticOscillator) ? "double-stochastic-composition"
                : type == typeof(StochasticFastOscillator) ? "stochastic-fast-composition" : "stochastic-composition";
            yield return new IndicatorValidationCase(type, $"{prefix}/{length}/{first}/{last}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> ChandeCompositionCases(Type type)
    {
        if (type != typeof(Cmo) && type != typeof(ChandeMomentumOscillator) && type != typeof(ChandeMomentumOscillatorSignal)
            && type != typeof(ChandeMomentumOscillatorFilter)) yield break;
        var periods = type == typeof(ChandeMomentumOscillatorFilter) ? new[] { (1, 1), (3, 3), (9, 9) }
            : type == typeof(Cmo) ? new[] { (1, 3), (3, 3), (14, 3) }
            : new[] { (1, 3), (3, 1), (3, 7), (14, 3) };
        foreach (var (length, signal) in periods)
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    16 => new SineWma(), 17 => new NaturalMa(), 18 => new EhlersHannMovingAverage(), 19 => new Vidya(), 20 => new Alma(),
                    _ => throw new InvalidOperationException()
                };
                return type == typeof(Cmo) ? new Cmo(length, average)
                    : type == typeof(ChandeMomentumOscillatorFilter) ? new ChandeMomentumOscillatorFilter(length, average)
                    : type == typeof(ChandeMomentumOscillatorSignal) ? new ChandeMomentumOscillatorSignal(length, signal, average)
                    : new ChandeMomentumOscillator(length, signal, average);
            }
            yield return new IndicatorValidationCase(type, $"chande-composition/{length}/{signal}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> SineNaturalCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage)
            && type != typeof(MiddleHighLowMovingAverage) && type != typeof(SequentiallyFilteredMovingAverage)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var natural in new[] { false, true })
        {
            IIndicator Create()
            {
                IMovingAverage average = natural ? new NaturalMa() : new SineWma();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : type == typeof(SlowSmoothedMovingAverage) ? new SlowSmoothedMovingAverage(length, average)
                    : type == typeof(MiddleHighLowMovingAverage) ? new MiddleHighLowMovingAverage(length, length, average)
                    : new SequentiallyFilteredMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"sine-natural-composition/{length}/{natural}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> HannCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage)
            && type != typeof(MiddleHighLowMovingAverage) && type != typeof(SequentiallyFilteredMovingAverage)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        {
            IIndicator Create()
            {
                IMovingAverage average = new EhlersHannMovingAverage();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : type == typeof(SlowSmoothedMovingAverage) ? new SlowSmoothedMovingAverage(length, average)
                    : type == typeof(MiddleHighLowMovingAverage) ? new MiddleHighLowMovingAverage(length, length, average)
                    : new SequentiallyFilteredMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"hann-composition/{length}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> VidyaCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage)
            && type != typeof(MiddleHighLowMovingAverage) && type != typeof(SequentiallyFilteredMovingAverage)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        {
            IIndicator Create()
            {
                IMovingAverage average = new Vidya();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : type == typeof(SlowSmoothedMovingAverage) ? new SlowSmoothedMovingAverage(length, average)
                    : type == typeof(MiddleHighLowMovingAverage) ? new MiddleHighLowMovingAverage(length, length, average)
                    : new SequentiallyFilteredMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"vidya-composition/{length}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> AlmaCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage)
            && type != typeof(MiddleHighLowMovingAverage) && type != typeof(SequentiallyFilteredMovingAverage)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        {
            IIndicator Create()
            {
                IMovingAverage average = new Alma();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : type == typeof(SlowSmoothedMovingAverage) ? new SlowSmoothedMovingAverage(length, average)
                    : type == typeof(MiddleHighLowMovingAverage) ? new MiddleHighLowMovingAverage(length, length, average)
                    : new SequentiallyFilteredMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"alma-composition/{length}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> SymmetricCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage))
            yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var triangle in new[] { false, true })
        {
            IIndicator Create()
            {
                IMovingAverage average = triangle ? new EhlersTriangleMovingAverage() : new SymmetricallyWeightedMovingAverage();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : new SlowSmoothedMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"symmetric-composition/{length}/{triangle}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> WeightedCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage))
            yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var squareRoot in new[] { false, true })
        {
            IIndicator Create()
            {
                IMovingAverage average = squareRoot ? new SquareRootWeightedMovingAverage() : new FibonacciWeightedMovingAverage();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : new SlowSmoothedMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"weighted-composition/{length}/{squareRoot}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> PolynomialCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage))
            yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 10, 11, 12 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind == 10 ? new ParabolicWma() : kind == 11 ? new CubedWeightedMovingAverage() : new QuickMovingAverage();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : new SlowSmoothedMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"polynomial-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> FoundationCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage))
            yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 1, 2, 3, 6 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind == 1 ? new Sma() : kind == 2 ? new Wma() : kind == 3 ? new Ema() : new Wwma();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : new SlowSmoothedMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"foundation-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> RootAndLagCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage))
            yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 13, 14 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind == 13 ? new JsaMovingAverage() : new QuadraticMovingAverage();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : new SlowSmoothedMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"root-lag-composition/{length}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> KaufmanCompositionCases(Type type)
    {
        if (type != typeof(Tma) && type != typeof(TriangularMovingAverage) && type != typeof(SlowSmoothedMovingAverage))
            yield break;
        foreach (var length in new[] { 1, 3, 14 })
        {
            IIndicator Create()
            {
                IMovingAverage average = new Kama();
                return type == typeof(Tma) ? new Tma(length, average)
                    : type == typeof(TriangularMovingAverage) ? new TriangularMovingAverage(length, average)
                    : new SlowSmoothedMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"kaufman-composition/{length}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> MiddleCompositionCases(Type type)
    {
        if (type != typeof(MiddleHighLowMovingAverage)) yield break;
        foreach (var periods in new[] { (1, 1), (3, 7), (7, 3), (14, 10) })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    _ => throw new InvalidOperationException()
                };
                return new MiddleHighLowMovingAverage(periods.Item1, periods.Item2, average);
            }
            yield return new IndicatorValidationCase(type, $"middle-composition/{periods.Item1}/{periods.Item2}/{kind}", Create);
        }
    }

    private static IEnumerable<IndicatorValidationCase> SequentialCompositionCases(Type type)
    {
        if (type != typeof(SequentiallyFilteredMovingAverage)) yield break;
        foreach (var length in new[] { 1, 3, 14 })
        foreach (var kind in new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })
        {
            IIndicator Create()
            {
                IMovingAverage average = kind switch
                {
                    1 => new Sma(), 2 => new Wma(), 3 => new Ema(), 6 => new Wwma(),
                    7 => new SymmetricallyWeightedMovingAverage(), 8 => new FibonacciWeightedMovingAverage(),
                    9 => new SquareRootWeightedMovingAverage(), 10 => new ParabolicWma(),
                    11 => new CubedWeightedMovingAverage(), 12 => new QuickMovingAverage(),
                    13 => new JsaMovingAverage(), 14 => new QuadraticMovingAverage(), 15 => new Kama(),
                    _ => throw new InvalidOperationException()
                };
                return new SequentiallyFilteredMovingAverage(length, average);
            }
            yield return new IndicatorValidationCase(type, $"sequential-composition/{length}/{kind}", Create);
        }
    }

    private static bool IsPeriod(ParameterInfo p) => p.ParameterType == typeof(int)
        && (p.Name!.IndexOf("length", StringComparison.OrdinalIgnoreCase) >= 0
            || p.Name.IndexOf("period", StringComparison.OrdinalIgnoreCase) >= 0
            || p.Name.IndexOf("lookback", StringComparison.OrdinalIgnoreCase) >= 0);

    private static bool CanSupply(Type type, ParameterInfo p) => p.IsOptional || SpecDefault(type, p) is not null || IsPeriod(p)
        || p.ParameterType == typeof(IMovingAverage) || p.ParameterType == typeof(MovingAvgType);

    // Generated constructors sometimes expose required arguments whose options overload declares
    // defaults. Read that metadata rather than guessing values for poles, multipliers, etc.
    private static ParameterInfo? SpecDefault(Type type, ParameterInfo parameter)
    {
        if (!typeof(IBuiltInIndicator).IsAssignableFrom(type)) return null;
        var options = type.Assembly.GetType("OoplesFinance.StockIndicators.Builder.Specs." + type.Name + "SpecOptions");
        return options?.GetConstructors().SelectMany(c => c.GetParameters()).FirstOrDefault(p =>
            p.IsOptional && p.Name == parameter.Name && p.ParameterType == parameter.ParameterType);
    }

    private static object? Argument(Type type, ParameterInfo p, int index, double scale, bool weighted)
    {
        var declaredDefault = p.IsOptional ? p : SpecDefault(type, p);
        if (IsPeriod(p))
        {
            var baseline = declaredDefault is not null ? (int)declaredDefault.DefaultValue! : 7 + 6 * index;
            return scale == 1 ? baseline : Math.Max(1, (int)Math.Round(baseline * scale)); // NOSONAR: S1244 - One is an exact discrete configuration selector, not a measured value.
        }
        if (p.ParameterType == typeof(IMovingAverage) && (weighted || !p.IsOptional))
        {
            // Extra stage arguments configure actual periods, not the enum-wide average choice.
            // Leave those optional slots empty in the alternate-enum fixture.
            var options = type.Assembly.GetType("OoplesFinance.StockIndicators.Builder.Specs." + type.Name + "SpecOptions");
            if (p.IsOptional && options is not null && !options.GetConstructors().SelectMany(c => c.GetParameters())
                .Any(option => string.Equals(option.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                return p.DefaultValue;
            return new Wma(7);
        }
        if (p.ParameterType == typeof(MovingAvgType) && weighted) return MovingAvgType.WeightedMovingAverage;
        if (declaredDefault is not null) return declaredDefault.DefaultValue;
        if (p.ParameterType == typeof(MovingAvgType)) return MovingAvgType.SimpleMovingAverage;
        return p.DefaultValue;
    }
}
