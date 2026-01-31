using NUnit.Framework;

namespace OoplesFinance.E2E.Playwright;

/// <summary>
/// Global setup that runs once before all tests
/// </summary>
[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        // Install Playwright browsers if needed
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exitCode != 0)
        {
            Console.WriteLine($"Warning: Playwright browser installation returned code {exitCode}");
        }

        // Log test configuration
        Console.WriteLine("=== E2E Test Configuration ===");
        Console.WriteLine($"Base URL: {TestConfig.BaseUrl}");
        Console.WriteLine($"Supabase URL: {TestConfig.SupabaseUrl}");
        Console.WriteLine($"Headless: {Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADLESS") ?? "true"}");
        Console.WriteLine("==============================");
    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Console.WriteLine("=== E2E Tests Complete ===");
    }
}
