using System.Reflection;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// Every streaming state computes what its batch twin computes when the caller asks for something other than
/// the defaults.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StreamingBatchValueParityTests"/> already sweeps every published key of every public state, but it
/// builds both engines from declared defaults on both sides. <see cref="BuilderArmTests"/> does run two parameter
/// sets - and <see cref="BuilderStreamingArmTests"/> streams them - but only for the typed Builder specs, which
/// the streaming table can build for around sixty indicators. An indicator with no typed spec is therefore only
/// ever compared at its own defaults, in either engine, anywhere in this suite.
/// </para>
/// <para>
/// That hides a whole class of defect, because an argument which is accepted and then ignored agrees at the
/// defaults and nowhere else. The Keltner channel fixed in #226 was exactly this shape: the multiplier argument
/// landed on the ATR length parameter and the multiplier itself did nothing at all. No parity test could see it,
/// because not one of them ever passed a multiplier.
/// </para>
/// <para>
/// Parameters are joined by NAME, never by position. The batch method and the state constructor are written
/// separately and do not agree on order - the generated factory's own Keltner arm reads its first parameter as
/// <c>length1</c> while the batch method's first is <c>maType</c> - so an index would perturb one side's length
/// against the other side's smoothing type and report a divergence of the harness's own making. A parameter named
/// on only one side keeps its default rather than being guessed at.
/// </para>
/// <para>
/// The perturbation itself is <see cref="BuilderArmTests.Value"/>, the same rules the typed-spec arms are already
/// held to, so the two suites cannot drift apart over what "alternate parameters" means.
/// </para>
/// </remarks>
public sealed class StreamingAlternateParameterParityTests : GlobalTestData
{
    private const int Bars = 251;

    [Fact]
    public void EveryStreamingStateComputesItsBatchTwinAtNonDefaultParameters()
    {
        var tickers = StockTestData.Take(Bars).ToList();
        var stateType = typeof(IStreamingIndicatorState);
        var types = stateType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && stateType.IsAssignableFrom(t))
            // The custom-input wrapper, not an indicator: it has no defaults and no batch twin.
            .Where(t => t.Name != "CustomInputState")
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var unmatched = new List<string>();
        var couldNotRun = new List<string>();
        var keyMismatches = new List<string>();
        var disagreements = new List<string>();
        var compared = 0;
        var takesNoParameters = 0;

        foreach (var type in types)
        {
            var ctor = type.GetConstructors()
                .Where(c => c.GetParameters().All(p => p.HasDefaultValue))
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();
            if (ctor is null)
            {
                // Named by the defaults sweep, which holds every state to having one. Not this test's subject.
                continue;
            }

            var ctorParameters = ctor.GetParameters();
            if (ctorParameters.Length == 0)
            {
                // Nothing to vary: a state with no arguments computes one thing, and the defaults sweep has it.
                takesNoParameters++;
                continue;
            }

            MethodInfo method;
            try
            {
                var probe = (IStreamingIndicatorState)ctor.Invoke(ctorParameters.Select(p => p.DefaultValue).ToArray());
                var found = IndicatorInvoker.GetMethod(probe.Name);
                if (found is null)
                {
                    // Pairing is the defaults sweep's assertion; repeating it here would report it twice.
                    continue;
                }

                method = found;
            }
            catch (Exception)
            {
                continue;
            }

            var batchNames = new HashSet<string>(
                method.GetParameters().Skip(1).Select(p => p.Name).OfType<string>(), StringComparer.Ordinal);
            var shared = ctorParameters.Where(p => p.Name is { } n && batchNames.Contains(n)).ToList();
            if (shared.Count == 0)
            {
                unmatched.Add($"{type.Name}: constructor takes [{string.Join(",", ctorParameters.Select(p => p.Name))}], " +
                    $"batch takes [{string.Join(",", batchNames)}]");
                continue;
            }

            // The same value on both sides for every shared name, so any disagreement is the indicator's.
            var overrides = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var parameter in shared)
            {
                if (parameter.Name is { } name)
                {
                    overrides[name] = BuilderArmTests.Value(parameter.ParameterType, parameter.DefaultValue, alternate: true);
                }
            }

            if (shared.All(p => p.Name is { } n && Equals(overrides[n], p.DefaultValue)))
            {
                // Perturbing moved nothing - every shared parameter is a type the alternate set leaves alone - so
                // this would silently repeat the defaults sweep and count as coverage it is not.
                takesNoParameters++;
                continue;
            }

            Dictionary<string, List<double>> batch;
            Dictionary<string, List<double>> streamed;
            try
            {
                var args = ctorParameters
                    .Select(p => p.Name is { } n && overrides.TryGetValue(n, out var value) ? value : p.DefaultValue)
                    .ToArray();
                var state = (IStreamingIndicatorState)ctor.Invoke(args);
                batch = RunBatch(method, tickers, overrides);
                streamed = RunStreaming(state, tickers);
            }
            catch (Exception ex)
            {
                couldNotRun.Add($"{type.Name} ({(ex.InnerException ?? ex).GetType().Name})");
                continue;
            }

            compared++;

            var batchKeys = new HashSet<string>(batch.Keys, StringComparer.Ordinal);
            var streamKeys = new HashSet<string>(streamed.Keys, StringComparer.Ordinal);
            if (!batchKeys.Contains(Primary))
            {
                // A band indicator publishes its bands and no single series in batch; its streaming value is one
                // of those bands, compared under its own name.
                streamKeys.Remove(Primary);
            }

            if (!batchKeys.SetEquals(streamKeys))
            {
                keyMismatches.Add($"{type.Name}: batch only [{string.Join(",", batchKeys.Except(streamKeys))}], " +
                    $"streaming only [{string.Join(",", streamKeys.Except(batchKeys))}]");
                continue;
            }

            foreach (var key in batchKeys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var expected = batch[key];
                var actual = streamed[key];
                if (expected.Count != actual.Count)
                {
                    disagreements.Add($"{type.Name}.{key}: {expected.Count} batch values, {actual.Count} streamed");
                    continue;
                }

                for (var i = 0; i < expected.Count; i++)
                {
                    if (!IsClose(expected[i], actual[i]))
                    {
                        disagreements.Add($"{type.Name}.{key} at bar {i}: batch {expected[i]}, streaming {actual[i]} " +
                            $"(parameters {string.Join(", ", overrides.Select(o => $"{o.Key}={o.Value}"))})");
                        break;
                    }
                }
            }
        }

        compared.Should().BeGreaterThan(500,
            $"most states take a parameter worth varying; {takesNoParameters} take none the alternate set moves");

        using var scope = new AssertionScope();
        unmatched.Should().BeEmpty(
            $"a state and its batch twin name the same parameters ({unmatched.Count}): {string.Join(" | ", unmatched)}");
        couldNotRun.Should().BeEmpty(
            $"every state completes both runs away from its defaults ({couldNotRun.Count}): {string.Join(" | ", couldNotRun)}");
        keyMismatches.Should().BeEmpty(
            $"both engines name the same outputs away from the defaults ({keyMismatches.Count}): {string.Join(" | ", keyMismatches)}");
        disagreements.Should().BeEmpty($"{compared} states compared at non-default parameters; " +
            $"streaming computes what batch computes ({disagreements.Count}): {string.Join(" | ", disagreements)}");
    }
}
