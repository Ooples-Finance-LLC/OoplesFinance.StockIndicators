using Microsoft.Playwright;

namespace OoplesFinance.E2E.Playwright;

/// <summary>
/// Playwright test configuration - applies to all test classes
/// </summary>
public static class PlaywrightConfig
{
    /// <summary>
    /// Install browsers before running tests (run once)
    /// </summary>
    public static async Task InstallBrowsersAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
        if (exitCode != 0)
        {
            throw new Exception($"Playwright browser installation failed with code {exitCode}");
        }
    }

    /// <summary>
    /// Default browser launch options
    /// </summary>
    public static BrowserTypeLaunchOptions LaunchOptions => new()
    {
        Headless = Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADLESS")?.ToLower() != "false",
        SlowMo = int.TryParse(Environment.GetEnvironmentVariable("PLAYWRIGHT_SLOW_MO"), out var slowMo) ? slowMo : 0,
        Timeout = 30000
    };

    /// <summary>
    /// Default context options
    /// </summary>
    public static BrowserNewContextOptions ContextOptions => new()
    {
        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
        IgnoreHTTPSErrors = true,
        Locale = "en-US",
        TimezoneId = "America/New_York",
        RecordVideoDir = Environment.GetEnvironmentVariable("PLAYWRIGHT_VIDEO_DIR"),
        RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 }
    };

    /// <summary>
    /// Test timeout settings
    /// </summary>
    public static class Timeouts
    {
        public const int NavigationTimeout = 30000;
        public const int ActionTimeout = 10000;
        public const int ExpectTimeout = 5000;
    }
}
