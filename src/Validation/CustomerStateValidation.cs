using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class CustomerStateValidation
{
    internal static bool RequiresCheck(IIndicator indicator)
    {
        var pending = new Stack<IIndicator>();
        var visited = new HashSet<IIndicator>(IndicatorIdentity.Comparer);
        pending.Push(indicator);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) continue;
            if (current is not IBuiltInIndicator) return true;
            foreach (var component in current.Components) pending.Push(component);
            if (current.Source is not null) pending.Push(current.Source);
        }
        return false;
    }

    internal static async Task CheckResetAsync(IIndicator indicator, IReadOnlyList<Bar> bars,
        IReadOnlyList<IReadOnlyList<double>> expected, CancellationToken cancellationToken,
        HashSet<IIndicator>? visited = null)
    {
        visited ??= new HashSet<IIndicator>(IndicatorIdentity.Comparer);
        if (!visited.Add(indicator) || !RequiresCheck(indicator)) return;
        // Native built-in states have a separate preview/reset harness. Customer states share these four interfaces.
        var state = indicator is IBuiltInIndicator ? null : indicator switch
        {
            IndicatorBase single => single.CreateState(),
            MultiOutputIndicatorBase multi => multi.CreateState(),
            _ => null
        };
        using var lifetime = state as IDisposable;
        var dependencies = indicator.Components.Concat(indicator.Source is null
            ? Array.Empty<IIndicator>() : new[] { indicator.Source }).ToArray();
        using var dependencyRun = dependencies.Length == 0 ? null : await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(dependencies).BuildAsync(cancellationToken).ConfigureAwait(false);
        foreach (var dependency in dependencies)
            if (RequiresCheck(dependency))
                await CheckResetAsync(dependency, bars, dependency.Outputs.Select(output =>
                    (IReadOnlyList<double>)dependencyRun![output].ToArray()).ToArray(), cancellationToken, visited).ConfigureAwait(false);
        if (indicator is IBuiltInIndicator) return;
        var components = indicator.Components.Select(component => dependencyRun![component].ToArray()).ToArray();
        var source = indicator.Source is null ? null : dependencyRun![indicator.Source].ToArray();
        var inputs = new double[components.Length];
        var outputs = new double[indicator.Outputs.Count];
        Replay("fresh state");
        switch (state)
        {
            case IIndicatorState single: single.Reset(); break;
            case IComposedIndicatorState composed: composed.Reset(); break;
            case IMultiOutputState multi: multi.Reset(); break;
            case IComposedMultiOutputState multi: multi.Reset(); break;
            default: throw new InvalidOperationException("No supported customer state was supplied for reset validation.");
        }
        Replay("after reset");

        void Replay(string phase)
        {
            for (var i = 0; i < bars.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var original = bars[i];
                var input = source is null ? original : new Bar(original.Time, original.Open, original.High,
                    original.Low, source[i], original.Volume);
                for (var component = 0; component < inputs.Length; component++) inputs[component] = components[component][i];
                Array.Clear(outputs, 0, outputs.Length);
                switch (state)
                {
                    case IIndicatorState single: outputs[0] = single.Update(in input); break;
                    case IComposedIndicatorState composed: outputs[0] = composed.Update(in input, inputs); break;
                    case IMultiOutputState multi: multi.Update(in input, outputs); break;
                    case IComposedMultiOutputState multi: multi.Update(in input, inputs, outputs); break;
                    default: throw new InvalidOperationException("No supported customer state was supplied for reset validation.");
                }
                for (var slot = 0; slot < outputs.Length; slot++)
                    if (!outputs[slot].Equals(expected[slot][i])) // NOSONAR: S1244 - Lifecycle replay requires identical published values, not tolerance agreement.
                        throw new InvalidOperationException($"{indicator.GetType().FullName}, output {slot}, bar {i}, {phase}: expected {expected[slot][i]:R}, got {outputs[slot]:R}.");
            }
        }
    }
}
