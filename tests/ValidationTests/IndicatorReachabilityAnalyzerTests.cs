using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using OoplesFinance.StockIndicators.SourceGeneration;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// SI0006, the rule that makes "a calculation which never says which indicator it is" a build error.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IndicatorReachabilityAnalyzer"/> reports at Error severity and this library builds with
/// TreatWarningsAsErrors, so the rule gates every build while having had no test of its own. #189 covered
/// the five streaming rules and left this one out of scope; the harness it added makes it cheap. See #220.
/// </para>
/// <para>
/// The rule reads syntax and never asks the semantic model anything, so these snippets declare their own
/// two-line <c>StockData</c> and <c>IndicatorName</c> rather than referencing the real ones. That is not a
/// shortcut - SI0006 runs only in a compilation named for the library, and having such a compilation also
/// reference an assembly of that same name is a fight worth avoiding. Stubs make the identity question moot.
/// </para>
/// <para>
/// Every case asserts the snippet compiles clean before looking at the analyzer, for the reason the streaming
/// suite gives: source that does not compile yields no analyzer diagnostics at all, so an expectation of
/// silence would be satisfied by a typo and the control arms would prove nothing.
/// </para>
/// </remarks>
public sealed class IndicatorReachabilityAnalyzerTests
{
    /// <summary>The one compilation name the rule runs under.</summary>
    private const string LibraryName = "OoplesFinance.StockIndicators";

    /// <summary>Anything else: the analyzer ships in the package and runs in consumer builds too.</summary>
    private const string ConsumerName = "SomeConsumer.Strategies";

    /// <summary>
    /// Stub types and a routing helper, around whichever member a case puts under test.
    /// </summary>
    /// <remarks>
    /// <c>Route</c> is private and does not begin with Calculate, so it is never itself a subject; it exists
    /// so a calculation can hand its name on, which is the shape the rule must stay silent about.
    /// </remarks>
    private const string Preamble = """
        namespace Probe;

        public enum IndicatorName { None, SimpleMovingAverage }

        public sealed class StockData
        {
            public IndicatorName IndicatorName { get; set; }
        }

        public static class Calculations
        {
        """;

    private const string Postamble = """

            private static StockData Route(StockData stockData, IndicatorName name)
            {
                stockData.IndicatorName = name;
                return stockData;
            }
        }
        """;

    [Fact]
    public void ACalculationThatNamesNoIndicatorReportsSI0006()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData)
                {
                    return stockData;
                }
            """;

        Ids(member).Should().Equal(new[] { "SI0006" },
            "it is an indicator by shape and says nothing about which indicator it is");
    }

    [Fact]
    public void ACalculationThatAssignsItsIndicatorNameReportsNothing()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData)
                {
                    stockData.IndicatorName = IndicatorName.SimpleMovingAverage;
                    return stockData;
                }
            """;

        Ids(member).Should().BeEmpty("the generated map gets an entry from exactly this assignment");
    }

    /// <summary>The routed shape from #199: the helper does the assigning on the calculation's behalf.</summary>
    [Fact]
    public void ACalculationThatHandsItsNameToAHelperReportsNothing()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData)
                {
                    return Route(stockData, IndicatorName.SimpleMovingAverage);
                }
            """;

        Ids(member).Should().BeEmpty("passing the name on is how the routed calculations say what they are");
    }

    /// <summary>
    /// A helper assigning the name it was handed, which cannot name a literal because the literal is at its
    /// caller.
    /// </summary>
    [Fact]
    public void ACalculationAssigningTheNameItWasGivenReportsNothing()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData, IndicatorName name)
                {
                    stockData.IndicatorName = name;
                    return stockData;
                }
            """;

        Ids(member).Should().BeEmpty("a bare identifier is a helper passing on the name its caller chose");
    }

    /// <summary>
    /// The sentinel arms. Both satisfy "there is an assignment" while leaving the calculation exactly as
    /// unreachable as assigning nothing, because the generator emits an entry only for a concrete member that
    /// is not None. A rule that merely looked for an assignment would pass both and be useless.
    /// </summary>
    [Fact]
    public void ACalculationAssigningIndicatorNameNoneReportsSI0006()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData)
                {
                    stockData.IndicatorName = IndicatorName.None;
                    return stockData;
                }
            """;

        Ids(member).Should().Equal(new[] { "SI0006" }, "None leaves the Builder unable to reach it");
    }

    [Fact]
    public void ACalculationAssigningDefaultReportsSI0006()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData)
                {
                    stockData.IndicatorName = default;
                    return stockData;
                }
            """;

        Ids(member).Should().Equal(new[] { "SI0006" }, "default is the same sentinel spelled differently");
    }

    /// <summary>Expression bodies are analysed too, which the rule reaches through its ExpressionBody fallback.</summary>
    [Fact]
    public void AnExpressionBodiedCalculationThatNamesNothingReportsSI0006()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData) => stockData;
            """;

        Ids(member).Should().Equal(new[] { "SI0006" },
            "a one-line calculation is as unreachable as a long one that says nothing");
    }

    [Fact]
    public void AnExpressionBodiedCalculationThatRoutesItsNameReportsNothing()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData)
                    => Route(stockData, IndicatorName.SimpleMovingAverage);
            """;

        Ids(member).Should().BeEmpty("two of the four routed wrappers in the library are exactly this shape");
    }

    /// <summary>
    /// The arm that matters most: the rule is about shape, not about the name beginning with Calculate.
    /// </summary>
    /// <remarks>
    /// The library has CalculateSharpeRatio, CalculatePipValue, CalculateGreeks, CalculateTaxImpact and a
    /// catalog method called plain Calculate. An earlier draft of the rule matched any public Calculate* and
    /// broke the build on about thirty methods that are not indicators and have no business in the output map.
    /// </remarks>
    [Fact]
    public void AMethodNamedCalculateThatIsNotAnIndicatorReportsNothing()
    {
        const string member = """
                public static double CalculateSharpeRatio(this StockData stockData, double riskFree)
                {
                    return riskFree;
                }
            """;

        Ids(member).Should().BeEmpty("it does not return a StockData, so it is not an indicator calculation");
    }

    /// <summary>The other half of the shape: an indicator is dispatched as an extension of StockData.</summary>
    [Fact]
    public void ANonExtensionCalculateMethodReportsNothing()
    {
        const string member = """
                public static StockData CalculateThing(StockData stockData)
                {
                    return stockData;
                }
            """;

        Ids(member).Should().BeEmpty("IndicatorInvoker dispatches on the extension shape, and so does the rule");
    }

    /// <summary>
    /// The compilation-name gate, which is the difference between this suite and the streaming one.
    /// </summary>
    /// <remarks>
    /// The analyzer ships inside the package, so it runs in consumer compilations where a method of theirs
    /// beginning with Calculate is none of its business. This is the same source as the first case, which
    /// reports SI0006 under the library's name - so the only variable is the assembly name.
    /// </remarks>
    [Fact]
    public void AConsumerCompilationReportsNothingWhateverTheSourceSays()
    {
        const string member = """
                public static StockData CalculateThing(this StockData stockData)
                {
                    return stockData;
                }
            """;

        Ids(member, LibraryName).Should().Equal(new[] { "SI0006" },
            "the same source under the library's own name is a violation");
        Ids(member, ConsumerName).Should().BeEmpty(
            "a consumer's own Calculate method is not this rule's business");
    }

    /// <summary>The analyzer's diagnostic ids for one member, ordered so assertions read the same way twice.</summary>
    /// <remarks>
    /// Expected ids are passed as an explicit array. <c>Equal("SI0006", "because...")</c> binds to
    /// <c>Equal(params string[])</c> and quietly takes the reason as a second expected id, which fails looking
    /// exactly like the analyzer under-reporting.
    /// </remarks>
    private static ImmutableArray<string> Ids(string member, string compilationName = LibraryName)
    {
        var source = Preamble + "\n" + member + "\n" + Postamble;
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            compilationName,
            new[] { tree },
            StreamingStateAnalyzerTests.PlatformReferences(includeLibrary: false),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .ToList();
        errors.Should().BeEmpty("the snippet under test must compile, or the analyzer has nothing to judge");

        var diagnostics = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new IndicatorReachabilityAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        return diagnostics.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToImmutableArray();
    }
}
