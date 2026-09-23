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

        // The length itself, not WarmupBars standing in for it. They are equal for most indicators, which
        // made WarmupBars look like a reading of the default - but a filter that needs more than its length
        // to settle declares more, and then this test was asserting the wrong contract.
        LengthOf(TryConstruct(Find("Rsi"))!).Should().Be(14, "CalculateRelativeStrengthIndex defaults length to 14");
        LengthOf(TryConstruct(Find("Cci"))!).Should().Be(20, "CalculateCommodityChannelIndex defaults length to 20");
        LengthOf(TryConstruct(Find("Hma"))!).Should().Be(20, "CalculateHullMovingAverage defaults length to 20");

        TryConstruct(Find("Rsi"))!.WarmupBars.Should().Be(14, "a 14 bar RSI means something after 14 bars");
        TryConstruct(Find("Hma"))!.WarmupBars.Should().Be(25,
            "a Hull average runs a length window and then a sqrt(length) one over it, so 20 + ceil(sqrt(20))");
    }

    private static int LengthOf(IIndicator indicator) =>
        (int)indicator.GetType().GetProperty("Length")!.GetValue(indicator)!;

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
            // PrimaryOutput names one of the series below rather than publishing another, so it is not a member
            // of the indicator's output set and must not be counted as one.
            .Where(p => p.Name != nameof(IPrimaryOutputIndicator.PrimaryOutput))
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
                // PrimaryOutput names one of the series below rather than publishing another, so it is not a member
                // of the indicator's output set and must not be counted as one.
                .Where(p => p.Name != nameof(IPrimaryOutputIndicator.PrimaryOutput))
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
    private sealed class MyOwnAverage : IndicatorBase, IMovingAverage
    {
        protected internal override object CreateState() => new State();

        private sealed class State : IIndicatorState
        {
            public void Reset() { }

            public double Update(in Bar bar) => bar.Close;
        }
    }

    [Fact]
    public void EveryMemberOfTheMovingAvgTypeEnumIsAnIMovingAverage()
    {
        var averages = GeneratedIndicators().Count(t => typeof(IMovingAverage).IsAssignableFrom(t));

        averages.Should().BeGreaterThan(150,
            "a component parameter asking for IMovingAverage has to have something to accept");
    }

    [Fact]
    public void OneOfOurAveragesCollapsesIntoTheEnumTheBatchCalculationTakes()
    {
        // Rsi asks for three averages, so its generated constructor carries SecondAverage and ThirdAverage
        // beside the first. Activator does not fill optional parameters unless told to.
        var rsi = (IIndicator)Activator.CreateInstance(Find("Rsi"),
            System.Reflection.BindingFlags.CreateInstance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.OptionalParamBinding,
            binder: null,
            args: [14, Construct<IMovingAverage>("Ema"), Type.Missing, Type.Missing],
            culture: null)!;

        var options = ((IBuiltInIndicator)rsi).CreateOptions();
        var maType = options.GetType().GetProperty("MaType")!.GetValue(options);

        maType.Should().Be(MovingAvgType.ExponentialMovingAverage,
            "a built-in average is one the batch calculation already knows how to run");
    }

    [Fact]
    public void ACallersOwnAverageStaysAComponentAndTheOptionsKeepTheirDefault()
    {
        var mine = new MyOwnAverage();
        var rsi = (IIndicator)Activator.CreateInstance(Find("Rsi"),
            System.Reflection.BindingFlags.CreateInstance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.OptionalParamBinding,
            binder: null,
            args: [14, mine, Type.Missing, Type.Missing],
            culture: null)!;

        // There is no enum member for someone else's average - which is why the parameter is an interface.
        rsi.Components.Should().ContainSingle().Which.Should().BeSameAs(mine);

        var options = ((IBuiltInIndicator)rsi).CreateOptions();
        options.GetType().GetProperty("MaType")!.GetValue(options)
            .Should().Be(MovingAvgType.WildersSmoothingMethod, "the batch default survives");
    }

    [Fact]
    public void AnAverageSuppliedToAnyGeneratedTypeBecomesADeclaredComponent()
    {
        var wrong = new List<string>();
        var checkedTypes = 0;

        foreach (var type in GeneratedIndicators())
        {
            var constructor = type.GetConstructors()
                .FirstOrDefault(c => c.GetParameters().All(p => p.IsOptional)
                    && c.GetParameters().Any(p => p.ParameterType == typeof(IMovingAverage)));

            if (constructor is null)
            {
                continue;
            }

            var mine = new MyOwnAverage();
            var arguments = constructor.GetParameters()
                .Select(p => p.ParameterType == typeof(IMovingAverage) ? mine : p.DefaultValue)
                .ToArray();

            IIndicator instance;
            try
            {
                instance = (IIndicator)constructor.Invoke(arguments);
            }
            catch (Exception)
            {
                continue;
            }

            checkedTypes++;
            if (!instance.Components.Contains(mine))
            {
                wrong.Add(type.Name);
            }
        }

        checkedTypes.Should().BeGreaterThan(200, "the sweep has to actually reach the component types");
        wrong.Should().BeEmpty("a supplied average must join the graph, or it is silently ignored");
    }
    [Fact]
    public void EveryMultiOutputTypeHasAnOutputEnumThatAgreesWithItsTypedMembers()
    {
        var wrong = new List<string>();
        var checkedTypes = 0;

        foreach (var type in GeneratedIndicators())
        {
            var nested = type.GetNestedType("Output");

            // Declared only: IndicatorBase gives every single-output type an inherited Value, which would
            // otherwise make all 600 of them look multi-output.
            var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType == typeof(IIndicatorOutput))
                // PrimaryOutput names one of the series below rather than publishing another, so it is not a member
                // of the indicator's output set and must not be counted as one.
                .Where(p => p.Name != nameof(IPrimaryOutputIndicator.PrimaryOutput))
                .ToList();

            if (members.Count == 0)
            {
                nested.Should().BeNull(type.Name + " publishes one series and needs no Output enum");
                continue;
            }

            if (nested is null || !nested.IsEnum)
            {
                wrong.Add(type.Name + ": no Output enum");
                continue;
            }

            checkedTypes++;

            // The enum is the same vocabulary as the typed members, in the same order, with each value being
            // the slot it names - so casting to int is the lookup rather than a table nobody maintains.
            var enumNames = Enum.GetNames(nested);
            var memberNames = members.Select(m => m.Name).ToArray();

            if (!enumNames.OrderBy(n => n, StringComparer.Ordinal)
                    .SequenceEqual(memberNames.OrderBy(n => n, StringComparer.Ordinal)))
            {
                wrong.Add(type.Name + ": enum [" + string.Join(",", enumNames) + "] against members ["
                    + string.Join(",", memberNames) + "]");
                continue;
            }

            var instance = TryConstruct(type);
            if (instance is null)
            {
                continue;
            }

            foreach (var member in members)
            {
                var slot = ((IIndicatorOutput)member.GetValue(instance)!).Slot;
                var value = (int)Enum.Parse(nested, member.Name);
                if (slot != value)
                {
                    wrong.Add(type.Name + "." + member.Name + ": slot " + slot + " but enum " + value);
                }
            }
        }

        checkedTypes.Should().BeGreaterThan(200, "the sweep has to reach the multi-output types");
        wrong.Should().BeEmpty();
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
