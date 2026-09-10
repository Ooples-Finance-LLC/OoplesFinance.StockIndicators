using System.Text.RegularExpressions;
using FluentAssertions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// The source generator's Roslyn reference is a compatibility floor for every consumer, not an ordinary
/// dependency.
///
/// <para><b>Why this test exists.</b> A source generator is loaded by the <i>compiler</i>, and csc refuses any
/// analyzer built against a Roslyn newer than itself:</para>
///
/// <code>
/// CSC : error CS9057: Analyzer assembly '...SourceGeneration.dll' cannot be used because it
///       references version '5.9.0.0' of the compiler, which is newer than the currently running
///       version '5.0.0.0'
/// </code>
///
/// <para>So this reference sets the <b>minimum .NET SDK every consumer of this package is forced onto</b>.
/// PR #155 raised it 4.8.0 → 5.9.0 as a routine dependency bump. Nothing here failed — the repository's own CI
/// runs a current SDK — but the published package silently stopped building on SDK 10.0.1xx, whose compiler is
/// 5.0, and that broke a downstream production deployment.</para>
///
/// <para>A comment in the csproj is not enough to prevent a repeat, because the thing that raised it was a bot
/// and the thing that missed it was a green build on a newer SDK. This asserts the floor.</para>
///
/// <para><b>To raise it deliberately</b>, change the constants here in the same commit and say in the message
/// which SDK becomes the new minimum — that is a breaking change for consumers and should read like one.</para>
/// </summary>
public sealed class SourceGeneratorRoslynFloorTests
{
    /// <summary>Roslyn 4.8 ships with the .NET 8 SDK; everything the generators use predates it.</summary>
    private const string MaxCodeAnalysisCSharp = "4.8.0";

    /// <summary>Paired with the above; 3.3.4 is the analyzer-rules package that matches that era.</summary>
    private const string MaxCodeAnalysisAnalyzers = "3.3.4";

    [Theory]
    [InlineData("Microsoft.CodeAnalysis.CSharp", MaxCodeAnalysisCSharp)]
    [InlineData("Microsoft.CodeAnalysis.Analyzers", MaxCodeAnalysisAnalyzers)]
    public void The_generator_does_not_reference_a_roslyn_newer_than_its_floor(string package, string expected)
    {
        var csproj = File.ReadAllText(GeneratorProject());

        var match = Regex.Match(
            csproj,
            @"PackageReference\s+Include=""" + Regex.Escape(package) + @"""\s+Version=""(?<version>[^""]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        match.Success.Should().BeTrue($"{package} should still be referenced by the generator project");
        match.Groups["version"].Value.Should().Be(
            expected,
            "this version is the minimum .NET SDK every consumer is forced onto (CS9057), not a preference. "
            + "Raising it is a breaking change for consumers: say which SDK becomes the new minimum.");
    }

    [Fact]
    public void The_generators_use_no_api_that_would_justify_raising_the_floor()
    {
        // The floor is only defensible while the generators stay inside the API surface that shipped with it.
        // IIncrementalGenerator, CreateSyntaxProvider and RegisterPostInitializationOutput are all Roslyn 4.0.
        // If a generator starts using something newer, this test still passes and the one above starts being a
        // real constraint - which is the moment to raise both deliberately rather than discover it downstream.
        var directory = Path.GetDirectoryName(GeneratorProject())!;
        var sources = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        sources.Should().NotBeEmpty("the generator project should contain sources");
        string.Join("\n", sources.Select(File.ReadAllText))
            .Should().Contain("IIncrementalGenerator", "the generators are incremental, which is the 4.0 API");
    }

    /// <summary>Walks up to the repository root so the test does not depend on the runner's working directory.</summary>
    private static string GeneratorProject()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "src", "SourceGeneration", "OoplesFinance.StockIndicators.SourceGeneration.csproj")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the repository root should be an ancestor of the test output directory");
        return Path.Combine(directory!.FullName, "src", "SourceGeneration", "OoplesFinance.StockIndicators.SourceGeneration.csproj");
    }
}
