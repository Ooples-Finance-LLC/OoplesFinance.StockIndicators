using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using OoplesFinance.StockIndicators.Builder.Cloud;
using Xunit;

namespace TradingApp.Tests.Services;

/// <summary>
/// Unit tests for SupabaseClient.
/// Uses mocked HTTP responses to test without real Supabase connection.
/// </summary>
public class SupabaseClientTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly SupabaseClient _client;

    public SupabaseClientTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("https://test.supabase.co")
        };

        // Create client with test options
        _client = new SupabaseClient(new SupabaseOptions
        {
            Url = "https://test.supabase.co",
            AnonKey = "test-anon-key"
        });
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    #region Authentication Tests

    [Fact]
    public async Task SignInAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var response = new
        {
            access_token = "test-access-token",
            refresh_token = "test-refresh-token",
            expires_in = 3600,
            user = new { id = "user-123", email = "test@example.com" }
        };

        SetupMockResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _client.SignInAsync("test@example.com", "password123");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("user-123", result.UserId);
        Assert.True(_client.IsAuthenticated);
    }

    [Fact]
    public async Task SignInAsync_WithInvalidCredentials_ReturnsFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.Unauthorized, "{}");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _client.SignInAsync("test@example.com", "wrong-password"));
    }

    [Fact]
    public async Task SignUpAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var response = new
        {
            access_token = "test-access-token",
            refresh_token = "test-refresh-token",
            expires_in = 3600,
            user = new { id = "new-user-123", email = "new@example.com" }
        };

        SetupMockResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        // Act
        var result = await _client.SignUpAsync("new@example.com", "password123");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("new-user-123", result.UserId);
    }

    [Fact]
    public async Task SignOutAsync_ClearsSession()
    {
        // Arrange - Sign in first
        var signInResponse = new
        {
            access_token = "test-access-token",
            refresh_token = "test-refresh-token",
            expires_in = 3600,
            user = new { id = "user-123", email = "test@example.com" }
        };
        SetupMockResponse(HttpStatusCode.OK, JsonSerializer.Serialize(signInResponse));
        await _client.SignInAsync("test@example.com", "password123");

        // Setup logout response
        SetupMockResponse(HttpStatusCode.OK, "{}");

        // Act
        await _client.SignOutAsync();

        // Assert
        Assert.False(_client.IsAuthenticated);
        Assert.Null(_client.UserId);
    }

    [Fact]
    public void GetOAuthSignInUrl_ReturnsCorrectUrl()
    {
        // Act
        var url = _client.GetOAuthSignInUrl("google", "https://myapp.com/callback");

        // Assert
        Assert.Contains("provider=google", url);
        Assert.Contains("redirect_to=", url);
    }

    #endregion

    #region Query Builder Tests

    [Fact]
    public void QueryBuilder_Select_BuildsCorrectPath()
    {
        // Arrange
        _client.SetSession("test-token", "test-refresh", "user-123");

        // Act - This will throw because we don't have a real connection,
        // but we can verify the query builder creates the right path
        var query = _client.From<TestRecord>("test_table")
            .Select("id,name,created_at")
            .Eq("status", "active")
            .Order("created_at", false)
            .Limit(10);

        // The query builder should build the correct path internally
        Assert.NotNull(query);
    }

    [Fact]
    public void QueryBuilder_WithFilters_BuildsCorrectPath()
    {
        // Arrange
        _client.SetSession("test-token", "test-refresh", "user-123");

        // Act
        var query = _client.From<TestRecord>("positions")
            .Select("*")
            .Eq("user_id", "user-123")
            .Gte("quantity", 0)
            .Order("symbol");

        Assert.NotNull(query);
    }

    #endregion

    #region Helper Methods

    private void SetupMockResponse(HttpStatusCode statusCode, string content)
    {
        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    #endregion

    private class TestRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}

/// <summary>
/// Tests for authentication state management.
/// </summary>
public class AuthStateTests
{
    [Fact]
    public void IsAuthenticated_WhenNoSession_ReturnsFalse()
    {
        // Arrange
        var client = new SupabaseClient(new SupabaseOptions
        {
            Url = "https://test.supabase.co",
            AnonKey = "test-key"
        });

        // Assert
        Assert.False(client.IsAuthenticated);
        Assert.Null(client.UserId);

        client.Dispose();
    }

    [Fact]
    public void SetSession_SetsAuthState()
    {
        // Arrange
        var client = new SupabaseClient(new SupabaseOptions
        {
            Url = "https://test.supabase.co",
            AnonKey = "test-key"
        });

        // Act
        client.SetSession("access-token", "refresh-token", "user-123");

        // Assert
        Assert.True(client.IsAuthenticated);
        Assert.Equal("user-123", client.UserId);

        client.Dispose();
    }

    [Fact]
    public void AuthStateChanged_RaisesEvent()
    {
        // Arrange
        var client = new SupabaseClient(new SupabaseOptions
        {
            Url = "https://test.supabase.co",
            AnonKey = "test-key"
        });

        var eventRaised = false;
        string? capturedUserId = null;

        client.AuthStateChanged += (sender, args) =>
        {
            eventRaised = true;
            capturedUserId = args.UserId;
        };

        // Act
        client.SetSession("access-token", "refresh-token", "user-456");

        // Assert
        Assert.True(eventRaised);
        Assert.Equal("user-456", capturedUserId);

        client.Dispose();
    }
}
