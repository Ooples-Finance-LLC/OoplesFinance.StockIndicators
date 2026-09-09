using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Net.Http.Json;

namespace OoplesFinance.E2E.Playwright;

/// <summary>
/// Test configuration for all smoke tests
/// </summary>
public class TestConfig
{
    // Base URL for the web application (set via environment or appsettings)
    public static string BaseUrl => Environment.GetEnvironmentVariable("TEST_BASE_URL") ?? "http://localhost:5000";

    // Supabase configuration
    public static string SupabaseUrl => Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "https://xueswywycjwmsuyhcgop.supabase.co";
    public static string SupabaseAnonKey => Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inh1ZXN3eXd5Y2p3bXN1eWhjZ29wIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Njk3NDM3MTMsImV4cCI6MjA4NTMxOTcxM30.b84t8ufZEIexGUdtafYhbK5NzxJ2qEBGpNHMMy9kLfg";

    // Test user credentials
    public static class TestUsers
    {
        public static readonly (string Email, string Password) Free = ("test.free@ooplesfinance.com", "TestPassword123!");
        public static readonly (string Email, string Password) Starter = ("test.starter@ooplesfinance.com", "TestPassword123!");
        public static readonly (string Email, string Password) Pro = ("test.pro@ooplesfinance.com", "TestPassword123!");
        public static readonly (string Email, string Password) Enterprise = ("test.enterprise@ooplesfinance.com", "TestPassword123!");
    }
}

/// <summary>
/// Base class for all Playwright tests with common setup
/// </summary>
public class PlaywrightTestBase : PageTest
{
    protected string BaseUrl => TestConfig.BaseUrl;

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true,
            Locale = "en-US",
            TimezoneId = "America/New_York"
        };
    }

    /// <summary>
    /// Helper to login a test user via Supabase Auth API
    /// </summary>
    protected async Task<string> LoginUserAsync(string email, string password)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("apikey", TestConfig.SupabaseAnonKey);
        client.DefaultRequestHeaders.Add("Content-Type", "application/json");

        var response = await client.PostAsJsonAsync(
            $"{TestConfig.SupabaseUrl}/auth/v1/token?grant_type=password",
            new { email, password });

        var body = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("access_token", out var token)
            ? token.GetString() ?? string.Empty
            : string.Empty;
    }
}

/// <summary>
/// Authentication smoke tests
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class AuthenticationSmokeTests : PlaywrightTestBase
{
    [Test]
    [Description("Verify the login page loads correctly")]
    public async Task LoginPage_ShouldLoad()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        // Verify page elements
        await Expect(Page.Locator("input[type='email'], input[name='email']")).ToBeVisibleAsync();
        await Expect(Page.Locator("input[type='password']")).ToBeVisibleAsync();
        await Expect(Page.Locator("button[type='submit'], button:has-text('Login'), button:has-text('Sign In')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify login with valid credentials succeeds")]
    public async Task Login_WithValidCredentials_ShouldSucceed()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        // Fill in credentials
        await Page.FillAsync("input[type='email'], input[name='email']", TestConfig.TestUsers.Free.Email);
        await Page.FillAsync("input[type='password']", TestConfig.TestUsers.Free.Password);

        // Submit
        await Page.ClickAsync("button[type='submit'], button:has-text('Login'), button:has-text('Sign In')");

        // Wait for navigation or dashboard
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });

        // Verify we're on dashboard or home
        var url = Page.Url;
        Assert.That(url, Does.Not.Contain("/login"), "Should navigate away from login page");
    }

    [Test]
    [Description("Verify login with invalid credentials shows error")]
    public async Task Login_WithInvalidCredentials_ShouldShowError()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        // Fill in wrong credentials
        await Page.FillAsync("input[type='email'], input[name='email']", "invalid@test.com");
        await Page.FillAsync("input[type='password']", "WrongPassword123!");

        // Submit
        await Page.ClickAsync("button[type='submit'], button:has-text('Login'), button:has-text('Sign In')");

        // Wait for error message
        await Expect(Page.Locator(".error, .alert-danger, [role='alert'], :has-text('Invalid')")).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    [Description("Verify registration page loads correctly")]
    public async Task RegisterPage_ShouldLoad()
    {
        await Page.GotoAsync($"{BaseUrl}/register");

        // Verify page elements
        await Expect(Page.Locator("input[type='email'], input[name='email']")).ToBeVisibleAsync();
        await Expect(Page.Locator("input[type='password']")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify logout functionality works")]
    public async Task Logout_ShouldRedirectToLogin()
    {
        // First login
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.FillAsync("input[type='email'], input[name='email']", TestConfig.TestUsers.Free.Email);
        await Page.FillAsync("input[type='password']", TestConfig.TestUsers.Free.Password);
        await Page.ClickAsync("button[type='submit'], button:has-text('Login'), button:has-text('Sign In')");
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });

        // Find and click logout
        await Page.ClickAsync("button:has-text('Logout'), a:has-text('Logout'), [aria-label='Logout']");

        // Verify redirected to login
        await Page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 5000 });
    }
}

/// <summary>
/// Portfolio/Dashboard smoke tests
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class DashboardSmokeTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeTest()
    {
        // Login before each test
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.FillAsync("input[type='email'], input[name='email']", TestConfig.TestUsers.Pro.Email);
        await Page.FillAsync("input[type='password']", TestConfig.TestUsers.Pro.Password);
        await Page.ClickAsync("button[type='submit'], button:has-text('Login'), button:has-text('Sign In')");
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify dashboard loads and shows portfolio summary")]
    public async Task Dashboard_ShouldShowPortfolioSummary()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");

        // Verify portfolio elements are visible
        await Expect(Page.Locator(":has-text('Portfolio'), :has-text('Account'), :has-text('Balance')").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify positions page loads")]
    public async Task PositionsPage_ShouldLoad()
    {
        await Page.GotoAsync($"{BaseUrl}/positions");

        // Verify page loaded
        await Expect(Page.Locator(":has-text('Positions'), :has-text('Holdings')").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify market data is displayed")]
    public async Task Dashboard_ShouldShowMarketData()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");

        // Look for market data indicators (prices, charts, etc.)
        await Expect(Page.Locator(".chart, canvas, [data-chart], :has-text('S&P'), :has-text('Market')").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}

/// <summary>
/// Order entry smoke tests
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class OrderEntrySmokeTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeTest()
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.FillAsync("input[type='email'], input[name='email']", TestConfig.TestUsers.Pro.Email);
        await Page.FillAsync("input[type='password']", TestConfig.TestUsers.Pro.Password);
        await Page.ClickAsync("button[type='submit'], button:has-text('Login'), button:has-text('Sign In')");
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify order entry page loads")]
    public async Task OrderEntryPage_ShouldLoad()
    {
        await Page.GotoAsync($"{BaseUrl}/trade");

        // Verify order form elements
        await Expect(Page.Locator("input[name='symbol'], input[placeholder*='Symbol']").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify symbol search works")]
    public async Task OrderEntry_SymbolSearch_ShouldWork()
    {
        await Page.GotoAsync($"{BaseUrl}/trade");

        // Enter a symbol
        await Page.FillAsync("input[name='symbol'], input[placeholder*='Symbol']", "AAPL");

        // Wait for autocomplete or validation
        await Task.Delay(500);

        // Symbol should be accepted (no error)
        var symbolInput = Page.Locator("input[name='symbol'], input[placeholder*='Symbol']");
        await Expect(symbolInput).ToHaveValueAsync("AAPL");
    }

    [Test]
    [Description("Verify order validation - quantity required")]
    public async Task OrderEntry_WithoutQuantity_ShouldShowError()
    {
        await Page.GotoAsync($"{BaseUrl}/trade");

        // Enter symbol but not quantity
        await Page.FillAsync("input[name='symbol'], input[placeholder*='Symbol']", "AAPL");

        // Try to submit
        await Page.ClickAsync("button:has-text('Buy'), button:has-text('Submit'), button[type='submit']");

        // Should show validation error
        await Expect(Page.Locator(".error, .validation-error, [role='alert'], :has-text('required')").First).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    [Description("Verify order confirmation dialog appears")]
    public async Task OrderEntry_ValidOrder_ShouldShowConfirmation()
    {
        await Page.GotoAsync($"{BaseUrl}/trade");

        // Fill in valid order details
        await Page.FillAsync("input[name='symbol'], input[placeholder*='Symbol']", "AAPL");
        await Page.FillAsync("input[name='quantity'], input[placeholder*='Quantity'], input[type='number']", "10");

        // Select market order if available
        var marketRadio = Page.Locator("input[value='market'], label:has-text('Market')");
        if (await marketRadio.CountAsync() > 0)
            await marketRadio.First.ClickAsync();

        // Submit
        await Page.ClickAsync("button:has-text('Buy'), button:has-text('Review'), button[type='submit']");

        // Confirmation dialog should appear
        await Expect(Page.Locator(".modal, .dialog, [role='dialog'], :has-text('Confirm')").First).ToBeVisibleAsync(new() { Timeout = 5000 });
    }
}

/// <summary>
/// Watchlist smoke tests
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class WatchlistSmokeTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeTest()
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.FillAsync("input[type='email'], input[name='email']", TestConfig.TestUsers.Free.Email);
        await Page.FillAsync("input[type='password']", TestConfig.TestUsers.Free.Password);
        await Page.ClickAsync("button[type='submit'], button:has-text('Login'), button:has-text('Sign In')");
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify watchlist page loads")]
    public async Task WatchlistPage_ShouldLoad()
    {
        await Page.GotoAsync($"{BaseUrl}/watchlist");

        await Expect(Page.Locator(":has-text('Watchlist'), :has-text('Watch List')").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify add symbol to watchlist")]
    public async Task Watchlist_AddSymbol_ShouldWork()
    {
        await Page.GotoAsync($"{BaseUrl}/watchlist");

        // Find add button
        var addButton = Page.Locator("button:has-text('Add'), button[aria-label*='Add'], .add-symbol");
        if (await addButton.CountAsync() > 0)
        {
            await addButton.First.ClickAsync();

            // Enter symbol
            await Page.FillAsync("input[name='symbol'], input[placeholder*='Symbol']", "GOOGL");

            // Confirm
            await Page.ClickAsync("button:has-text('Add'), button:has-text('Save'), button[type='submit']");

            // Verify symbol added
            await Expect(Page.Locator(":has-text('GOOGL')").First).ToBeVisibleAsync(new() { Timeout = 5000 });
        }
    }
}

/// <summary>
/// Strategy builder smoke tests
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class StrategyBuilderSmokeTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeTest()
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.FillAsync("input[type='email'], input[name='email']", TestConfig.TestUsers.Pro.Email);
        await Page.FillAsync("input[type='password']", TestConfig.TestUsers.Pro.Password);
        await Page.ClickAsync("button[type='submit'], button:has-text('Login'), button:has-text('Sign In')");
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify strategy builder page loads")]
    public async Task StrategyBuilderPage_ShouldLoad()
    {
        await Page.GotoAsync($"{BaseUrl}/strategies");

        await Expect(Page.Locator(":has-text('Strategy'), :has-text('Strategies')").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Description("Verify can create new strategy")]
    public async Task StrategyBuilder_CreateNew_ShouldWork()
    {
        await Page.GotoAsync($"{BaseUrl}/strategies/new");

        // Verify builder elements
        await Expect(Page.Locator(":has-text('New Strategy'), :has-text('Create'), input[name='name']").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}

/// <summary>
/// API endpoint smoke tests (direct HTTP calls)
/// </summary>
[TestFixture]
public class ApiSmokeTests
{
    private HttpClient _client = null!;
    private string _authToken = string.Empty;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("apikey", TestConfig.SupabaseAnonKey);

        // Login to get auth token
        var loginResponse = await _client.PostAsJsonAsync(
            $"{TestConfig.SupabaseUrl}/auth/v1/token?grant_type=password",
            new
            {
                email = TestConfig.TestUsers.Pro.Email,
                password = TestConfig.TestUsers.Pro.Password
            });

        var body = await loginResponse.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(body);
        _authToken = json.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        _client.Dispose();
    }

    [Test]
    [Description("Verify Supabase API is reachable")]
    public async Task SupabaseApi_ShouldBeReachable()
    {
        var response = await _client.GetAsync($"{TestConfig.SupabaseUrl}/rest/v1/");
        Assert.That(response.IsSuccessStatusCode, Is.True, "Supabase API should be reachable");
    }

    [Test]
    [Description("Verify can fetch subscription tiers")]
    public async Task Api_GetSubscriptionTiers_ShouldSucceed()
    {
        var response = await _client.GetAsync($"{TestConfig.SupabaseUrl}/rest/v1/subscription_tiers?select=*");
        Assert.That(response.IsSuccessStatusCode, Is.True, "Should fetch subscription tiers");

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("free"), "Should contain free tier");
        Assert.That(content, Does.Contain("pro"), "Should contain pro tier");
    }

    [Test]
    [Description("Verify authenticated user can access own profile")]
    public async Task Api_GetUserProfile_ShouldSucceed()
    {
        var response = await _client.GetAsync($"{TestConfig.SupabaseUrl}/rest/v1/user_profiles?select=*");
        Assert.That(response.IsSuccessStatusCode, Is.True, "Should fetch user profile");
    }

    [Test]
    [Description("Verify can fetch strategies")]
    public async Task Api_GetStrategies_ShouldSucceed()
    {
        var response = await _client.GetAsync($"{TestConfig.SupabaseUrl}/rest/v1/strategies?select=*");
        Assert.That(response.IsSuccessStatusCode, Is.True, "Should fetch strategies");
    }

    [Test]
    [Description("Verify can fetch watchlists")]
    public async Task Api_GetWatchlists_ShouldSucceed()
    {
        var response = await _client.GetAsync($"{TestConfig.SupabaseUrl}/rest/v1/watchlists?select=*");
        Assert.That(response.IsSuccessStatusCode, Is.True, "Should fetch watchlists");
    }

    [Test]
    [Description("Verify RLS prevents access to other users data")]
    public async Task Api_RlsSecurity_ShouldWork()
    {
        // Try to access all users - should only see own data due to RLS
        var response = await _client.GetAsync($"{TestConfig.SupabaseUrl}/rest/v1/user_profiles?select=*");
        var content = await response.Content.ReadAsStringAsync();

        // Should only see the logged-in user's profile
        Assert.That(content, Does.Not.Contain("test.free@"), "Should not see other users due to RLS");
    }
}

/// <summary>
/// Performance smoke tests
/// </summary>
[TestFixture]
public class PerformanceSmokeTests
{
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("apikey", TestConfig.SupabaseAnonKey);
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        _client.Dispose();
    }

    [Test]
    [Description("Verify API response time is acceptable")]
    public async Task Api_ResponseTime_ShouldBeAcceptable()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _client.GetAsync($"{TestConfig.SupabaseUrl}/rest/v1/subscription_tiers?select=*");
        stopwatch.Stop();

        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000), "API response should be under 2 seconds");
    }

    [Test]
    [Description("Verify concurrent requests are handled")]
    public async Task Api_ConcurrentRequests_ShouldSucceed()
    {
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var response = await _client.GetAsync($"{TestConfig.SupabaseUrl}/rest/v1/subscription_tiers?select=*");
            return response.IsSuccessStatusCode;
        });

        var results = await Task.WhenAll(tasks);
        Assert.That(results.All(r => r), Is.True, "All concurrent requests should succeed");
    }
}
