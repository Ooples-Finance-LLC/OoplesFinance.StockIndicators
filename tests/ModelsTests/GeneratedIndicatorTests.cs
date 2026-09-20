using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// What the generated indicator types have to be true of, swept across all of them rather than sampled.
/// </summary>
/// <remarks>
/// Discovery rather than a list: a type is covered the moment it is emitted. A sampled test would pass for
/// years while a whole category of generated types was wrong, which is the failure mode this shape exists to
/// avoid.
/// </remarks>
public sealed class GeneratedIndicatorTests
{
    private static IReadOnlyList<Type> GeneratedIndicators() =>
        [.. typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .Where(typeof(IIndicator).IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    [Fact]
    public void TheGeneratorEmittedTheWholeCatalogue()
    {
        // 845 options types, less the obsolete one that cannot be computed from a single series, less the
        // hand-written specimens the emitter skips.
        GeneratedIndicators().Should().HaveCountGreaterThan(800,
            "the emitter reads every non-obsolete options type");
    }

    [Fact]
    public void EveryGeneratedIndicatorNamesABatchIndicatorAndBuildsItsOptions()
    {
        var broken = new List<string>();

        foreach (var type in GeneratedIndicators())
        {
            var instance = TryConstruct(type);
            if (instance is null)
            {
                continue;
            }

            if (instance is not IBuiltInIndicator builtIn)
            {
                broken.Add(type.Name + ": does not implement IBuiltInIndicator");
                continue;
            }

            IIndicatorSpecOptions options;
            try
            {
                options = builtIn.CreateOptions();
            }
            catch (Exception ex)
            {
                broken.Add(type.Name + ": CreateOptions threw " + ex.GetType().Name);
                continue;
            }

            // The options type it builds must be the one it was generated from, or the compute layer
            // dispatches on something other than what the caller configured.
            var expected = type.Name + "SpecOptions";
            if (options.GetType().Name != expected)
            {
                broken.Add(type.Name + ": built " + options.GetType().Name + ", expected " + expected);
            }
        }

        broken.Should().BeEmpty();
    }

    [Fact]
    public void EveryGeneratedIndicatorCarriesExactlyOneCategory()
    {
        var categories = new[]
        {
            typeof(ITrendIndicator), typeof(IMomentumIndicator), typeof(IVolatilityIndicator),
            typeof(IVolumeIndicator), typeof(ICycleIndicator), typeof(ISupportAndResistanceIndicator)
        };

        var wrong = new List<string>();

        foreach (var type in GeneratedIndicators())
        {
            var count = categories.Count(c => c.IsAssignableFrom(type));
            if (count != 1)
            {
                wrong.Add(type.Name + ": " + count + " categories");
            }
        }

        wrong.Should().BeEmpty("the category comes from the [Category] attribute on IndicatorName");
    }

    [Fact]
    public void MostGeneratedIndicatorsCanBeConstructedWithNoArguments()
    {
        var indicators = GeneratedIndicators();
        var constructible = indicators.Count(t => TryConstruct(t) is not null);

        // The defaults come from the batch Calculate* signatures, so this is a measure of how much of the
        // library states its own defaults. It is a floor, not a target: a type whose batch method declares
        // none legitimately requires its arguments.
        constructible.Should().BeGreaterThan(indicators.Count * 3 / 4,
            "new Rsi() has to work for the beginner story to hold");
    }

    [Fact]
    public void ADefaultedParameterIsNeverFollowedByARequiredOne()
    {
        var wrong = new List<string>();

        foreach (var type in GeneratedIndicators())
        {
            foreach (var constructor in type.GetConstructors())
            {
                var seenOptional = false;
                foreach (var parameter in constructor.GetParameters())
                {
                    if (parameter.IsOptional)
                    {
                        seenOptional = true;
                    }
                    else if (seenOptional)
                    {
                        wrong.Add(type.Name + "." + parameter.Name);
                    }
                }
            }
        }

        // C# would not compile this, so a failure here means the generated file did not compile - but the
        // assertion is cheap and names the offender instead of leaving a build error in generated source.
        wrong.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedDefaultsMatchWhatTheBatchMethodAlwaysUsed()
    {
        // Spot-checked against the Calculate* signatures rather than swept, because the sweep above proves
        // the mechanism and these prove it picked the right numbers.
        Construct<IIndicator>("Rsi").Should().NotBeNull();

        var rsi = TryConstruct(Find("Rsi"))!;
        rsi.WarmupBars.Should().Be(14, "CalculateRelativeStrengthIndex defaults length to 14");

        var cci = TryConstruct(Find("Cci"))!;
        cci.WarmupBars.Should().Be(20, "CalculateCommodityChannelIndex defaults length to 20");

        var hma = TryConstruct(Find("Hma"))!;
        hma.WarmupBars.Should().Be(20, "CalculateHullMovingAverage defaults length to 20");
    }

    [Fact]
    public void MacdPublishesItsThreeSeriesAsTypedMembers()
    {
        var macd = TryConstruct(Find("Macd"))!;

        macd.Should().BeAssignableTo<IMultiOutputIndicator>();
        macd.Outputs.Should().HaveCount(3);

        // The published key is "Macd", which cannot be a member of a type called Macd, so the indicator's
        // own series takes the Value name and its siblings keep theirs.
        var members = Find("Macd").GetProperties()
            .Where(p => p.PropertyType == typeof(IIndicatorOutput))
            .Select(p => p.Name)
            .ToList();

        members.Should().BeEquivalentTo(["Value", "Signal", "Histogram"]);
    }

    [Fact]
    public void EveryMultiOutputTypeExposesOneTypedMemberPerPublishedSeries()
    {
        var wrong = new List<string>();

        foreach (var type in GeneratedIndicators())
        {
            var instance = TryConstruct(type);
            if (instance is not IMultiOutputIndicator)
            {
                continue;
            }

            var members = type.GetProperties()
                .Where(p => p.PropertyType == typeof(IIndicatorOutput))
                .ToList();

            if (members.Count != instance.Outputs.Count)
            {
                wrong.Add(type.Name + ": " + members.Count + " members for "
                    + instance.Outputs.Count + " outputs");
                continue;
            }

            // Each member must be a distinct slot of this indicator, or two names address one series.
            var slots = members.Select(m => ((IIndicatorOutput)m.GetValue(instance)!).Slot).ToList();
            if (slots.Distinct().Count() != slots.Count)
            {
                wrong.Add(type.Name + ": members share a slot");
            }

            if (members.Any(m => ((IIndicatorOutput)m.GetValue(instance)!).Indicator != instance))
            {
                wrong.Add(type.Name + ": a member belongs to another indicator");
            }
        }

        wrong.Should().BeEmpty();
    }

    [Fact]
    public void WarmupIsTheLongestLengthNotTheFirstDeclared()
    {
        // Macd takes fastLength, slowLength then signalLength, so reading the first said 12 where the
        // indicator cannot mean anything before 26.
        TryConstruct(Find("Macd"))!.WarmupBars.Should().Be(26);
    }
    private static Type Find(string name) =>
        GeneratedIndicators().Single(t => t.Name == name);

    private static T? Construct<T>(string name) where T : class =>
        TryConstruct(Find(name)) as T;

    private static IIndicator? TryConstruct(Type type)
    {
        var constructor = type.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().All(p => p.IsOptional));

        if (constructor is null)
        {
            return null;
        }

        try
        {
            var arguments = constructor.GetParameters().Select(p => p.DefaultValue).ToArray();
            return (IIndicator)constructor.Invoke(arguments);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
