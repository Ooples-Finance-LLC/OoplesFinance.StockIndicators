using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// Every typed Builder spec computes the batch indicator it stands for.
/// </summary>
/// <remarks>
/// <para>
/// A typed spec (<c>RsiSpecOptions</c> and the 844 others) used to run a fast arm of its own, written apart
/// from the batch indicator and never compared with it. Of the arms that could be compared, over half
/// disagreed: RSI, ATR, ADX and the stochastic among them, by a warmup convention, a different formula, or by
/// computing another indicator altogether. The Builder now serves an arm only when it is in
/// <see cref="BuilderVerifiedArms"/>, and computes every other typed spec with its batch indicator.
/// </para>
/// <para>
/// This holds every spec, at two parameter sets, through the Builder's public path to its batch indicator; a
/// verified arm that disagrees at either set fails here. There is no list of exceptions.
/// </para>
/// </remarks>
public sealed class BuilderArmTests : GlobalTestData
{
    internal static readonly List<Type> OptionTypes = typeof(IIndicatorSpecOptions).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(IIndicatorSpecOptions).IsAssignableFrom(t)
            && t != typeof(GenericIndicatorOptions))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToList();

    [Fact]
    public void EveryTypedSpecStandsForABatchIndicator()
    {
        var unbound = OptionTypes.Where(t => !BuilderArmBinding.TryGetTarget(t, out _)).Select(t => t.Name).ToList();
        unbound.Should().BeEmpty($"every typed spec names the batch indicator it computes: {string.Join(", ", unbound)}");
    }

    [Fact]
    public void EveryOptionReachesTheBatchIndicator()
    {
        var ignored = new List<string>();
        foreach (var type in OptionTypes)
        {
            if (!BuilderArmBinding.TryGetTarget(type, out var target))
            {
                continue;
            }

            if (BuilderArmBinding.UnmappedProperties(type, target) is { Count: > 0 } unmapped)
            {
                ignored.Add($"{type.Name} -> {target.Name}: {string.Join(",", unmapped)}");
            }

            // A declared argument that names nothing would silently pass nothing.
            ignored.AddRange(BuilderArmBinding.InvalidArguments(type, target).Select(invalid => $"{type.Name} -> {target.Name}: {invalid}"));
        }

        ignored.Should().BeEmpty($"a spec option the batch indicator never sees is silently ignored: {string.Join(" | ", ignored)}");
    }

    [Fact]
    public void EveryTypedSpecComputesItsBatchIndicator()
    {
        var tickers = StockTestData.ToList();
        var failures = new List<string>();
        var compared = 0;
        foreach (var type in OptionTypes)
        {
            if (!BuilderArmBinding.TryGetTarget(type, out var target))
            {
                continue;
            }

            foreach (var alternate in new[] { false, true })
            {
                var options = Create(type, alternate);
                if (options is null)
                {
                    failures.Add($"{type.Name}: no constructor this test can call");
                    break;
                }

                double[]? primary = null;
                foreach (IndicatorOutput output in Enum.GetValues(typeof(IndicatorOutput)))
                {
                    var spec = new IndicatorSpec(target.Name, options, output);
                    double[]? arm;
                    try
                    {
                        arm = Run(IndicatorCompute.ComputeArm, tickers, spec);
                    }
                    catch (Exception)
                    {
                        // An arm that cannot run is not served unless verified; the served path is checked below.
                        arm = output == IndicatorOutput.Primary ? Array.Empty<double>() : null;
                    }

                    if (arm is null || (output != IndicatorOutput.Primary && primary is not null && Same(primary, arm)))
                    {
                        continue;
                    }

                    if (output == IndicatorOutput.Primary)
                    {
                        primary = arm;
                    }

                    compared++;
                    var label = $"{type.Name} {output}{(alternate ? " (alternate parameters)" : string.Empty)}";
                    try
                    {
                        var served = Run(IndicatorCompute.TryComputeFast, tickers, spec)
                            ?? throw new InvalidOperationException("the Builder served nothing");
                        var expected = BuilderArmBinding.Compute(new StockData(tickers), spec, target);
                        var first = Enumerable.Range(0, Math.Min(served.Length, expected.Count)).FirstOrDefault(i => !IsClose(expected[i], served[i]), -1);
                        if (served.Length != expected.Count)
                        {
                            failures.Add($"{label}: {served.Length} values, batch {expected.Count}");
                        }
                        else if (first >= 0)
                        {
                            failures.Add($"{label} bar {first}: Builder {served[first]}, batch {target.Name} {expected[first]}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        failures.Add($"{label}: {inner.GetType().Name} {inner.Message}");
                    }
                }
            }
        }

        compared.Should().BeGreaterThan(700, "every bound typed spec is compared");
        failures.Should().BeEmpty($"{compared} spec outputs compared: {string.Join(" | ", failures)}");
    }

    [Fact]
    public void EveryVerifiedArmIsABoundSpec()
    {
        using var scope = new AssertionScope();
        foreach (var (options, _) in BuilderVerifiedArms.Arms)
        {
            BuilderArmBinding.TryGetTarget(options, out _).Should().BeTrue($"{options.Name} is verified against a batch indicator it must name");
        }
    }

    private static double[]? Run(Func<StockData, IndicatorSpec, ComputeContext, ComputeBuffer?> compute, List<TickerData> tickers, IndicatorSpec spec)
    {
        using var context = new ComputeContext();
        var result = compute(new StockData(tickers), spec, context);
        if (result is null)
        {
            return null;
        }

        using var buffer = result.Value;
        return buffer.ToArray();
    }

    private static bool Same(double[] a, double[] b) => a.Length == b.Length && !a.Where((v, i) => !IsClose(v, b[i])).Any();

    internal static IIndicatorSpecOptions? Create(Type type, bool alternate)
    {
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().All(p => p.HasDefaultValue || Value(p.ParameterType, null, false) is not null));
        if (ctor is null)
        {
            return null;
        }

        var args = ctor.GetParameters()
            .Select(p => Value(p.ParameterType, p.HasDefaultValue ? p.DefaultValue : null, alternate))
            .ToArray();
        return (IIndicatorSpecOptions)ctor.Invoke(args);
    }

    // Defaults as declared, and an alternate set that moves every length and multiplier off its default, so an arm
    // that matches only at its defaults does not count as verified.
    private static object? Value(Type type, object? declared, bool alternate)
    {
        if (type == typeof(int))
        {
            var value = declared is int i ? i : 14;
            return alternate ? value + 3 : value;
        }

        if (type == typeof(double))
        {
            var value = declared is double d ? d : 2.0;
            return alternate ? (value == 0 ? 0.5 : value * 1.5) : value;
        }

        if (type == typeof(MovingAvgType))
        {
            return alternate ? MovingAvgType.ExponentialMovingAverage : declared ?? MovingAvgType.SimpleMovingAverage;
        }

        if (type == typeof(InputName))
        {
            return declared ?? InputName.Close;
        }

        if (type == typeof(bool))
        {
            var value = declared is bool b && b;
            return alternate ? !value : value;
        }

        if (type.IsEnum)
        {
            return declared ?? Enum.GetValues(type).GetValue(0);
        }

        return declared;
    }
}
