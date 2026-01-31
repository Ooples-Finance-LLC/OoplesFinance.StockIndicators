using FluentAssertions;
using OoplesFinance.TradingApp.Maui.Services;
using Xunit;

namespace TradingApp.Tests.Services;

/// <summary>
/// Unit tests for the RealtimeService.
/// </summary>
public class RealtimeServiceTests
{
    [Fact]
    public void Constructor_WithValidArgs_CreatesService()
    {
        // Arrange & Act
        var service = new RealtimeService("https://test.supabase.co", "test-anon-key");

        // Assert
        service.Should().NotBeNull();
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new RealtimeService(null!, "test-key");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("supabaseUrl");
    }

    [Fact]
    public void Constructor_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new RealtimeService("https://test.supabase.co", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("anonKey");
    }

    [Fact]
    public void IsConnected_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        var service = new RealtimeService("https://test.supabase.co", "test-key");

        // Assert
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task SubscribeAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new RealtimeService("https://test.supabase.co", "test-key");

        // Act
        var act = () => service.SubscribeAsync("positions");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Not connected*");
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        // Arrange
        var service = new RealtimeService("https://test.supabase.co", "test-key");

        // Act
        var act = () => service.DisconnectAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_MultipleDispose_DoesNotThrow()
    {
        // Arrange
        var service = new RealtimeService("https://test.supabase.co", "test-key");

        // Act
        var act = () =>
        {
            service.Dispose();
            service.Dispose(); // Should not throw
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ConnectionStatusChanged_EventCanBeSubscribed()
    {
        // Arrange
        var service = new RealtimeService("https://test.supabase.co", "test-key");
        var eventRaised = false;

        // Act
        service.ConnectionStatusChanged += (s, e) => eventRaised = true;

        // Assert - Event handler registered successfully (no assertion needed, just verifying subscription works)
        service.Should().NotBeNull();
    }

    [Fact]
    public void MessageReceived_EventCanBeSubscribed()
    {
        // Arrange
        var service = new RealtimeService("https://test.supabase.co", "test-key");
        RealtimeMessageEventArgs? receivedArgs = null;

        // Act
        service.MessageReceived += (s, e) => receivedArgs = e;

        // Assert
        service.Should().NotBeNull();
    }
}

/// <summary>
/// Tests for RealtimePayload data class.
/// </summary>
public class RealtimePayloadTests
{
    [Fact]
    public void RealtimePayload_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var payload = new RealtimePayload();

        // Assert
        payload.Topic.Should().Be(string.Empty);
        payload.EventType.Should().Be(string.Empty);
        payload.Table.Should().Be(string.Empty);
        payload.Schema.Should().Be(string.Empty);
        payload.OldRecord.Should().BeNull();
        payload.NewRecord.Should().BeNull();
        payload.Timestamp.Should().Be(default);
    }

    [Fact]
    public void RealtimePayload_SetProperties_WorksCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var payload = new RealtimePayload
        {
            Topic = "realtime:public:positions",
            EventType = "INSERT",
            Table = "positions",
            Schema = "public",
            NewRecord = new Dictionary<string, object?>
            {
                ["id"] = "123",
                ["symbol"] = "AAPL",
                ["quantity"] = 100
            },
            Timestamp = timestamp
        };

        // Assert
        payload.Topic.Should().Be("realtime:public:positions");
        payload.EventType.Should().Be("INSERT");
        payload.Table.Should().Be("positions");
        payload.Schema.Should().Be("public");
        payload.NewRecord.Should().NotBeNull();
        payload.NewRecord!["symbol"].Should().Be("AAPL");
        payload.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void GetValue_WhenRecordIsNull_ReturnsDefault()
    {
        // Arrange
        var payload = new RealtimePayload { NewRecord = null };

        // Act
        var result = payload.GetValue<string>("key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetValue_WhenKeyNotFound_ReturnsDefault()
    {
        // Arrange
        var payload = new RealtimePayload
        {
            NewRecord = new Dictionary<string, object?>
            {
                ["other"] = "value"
            }
        };

        // Act
        var result = payload.GetValue<string>("missing");

        // Assert
        result.Should().BeNull();
    }
}

/// <summary>
/// Tests for ConnectionStatusChangedEventArgs.
/// </summary>
public class ConnectionStatusChangedEventArgsTests
{
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(false, "Connection failed")]
    public void Constructor_SetsPropertiesCorrectly(bool isConnected, string? errorMessage)
    {
        // Arrange & Act
        var args = new ConnectionStatusChangedEventArgs(isConnected, errorMessage);

        // Assert
        args.IsConnected.Should().Be(isConnected);
        args.ErrorMessage.Should().Be(errorMessage);
    }
}

/// <summary>
/// Tests for RealtimeMessageEventArgs.
/// </summary>
public class RealtimeMessageEventArgsTests
{
    [Fact]
    public void Constructor_SetsPayload()
    {
        // Arrange
        var payload = new RealtimePayload
        {
            Topic = "test",
            EventType = "INSERT"
        };

        // Act
        var args = new RealtimeMessageEventArgs(payload);

        // Assert
        args.Payload.Should().Be(payload);
        args.Payload.Topic.Should().Be("test");
        args.Payload.EventType.Should().Be("INSERT");
    }
}

/// <summary>
/// Integration tests for RealtimeService (require Supabase connection).
/// </summary>
[Trait("Category", "Integration")]
public class RealtimeServiceIntegrationTests
{
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;
    private readonly bool _canRun;

    public RealtimeServiceIntegrationTests()
    {
        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? string.Empty;
        _supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? string.Empty;
        _canRun = !string.IsNullOrEmpty(_supabaseUrl) && !string.IsNullOrEmpty(_supabaseKey);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectAsync_WithValidCredentials_Connects()
    {
        if (!_canRun)
        {
            // Skip if not configured
            return;
        }

        // Arrange
        using var service = new RealtimeService(_supabaseUrl, _supabaseKey);
        var connectionChanged = false;
        service.ConnectionStatusChanged += (s, e) => connectionChanged = true;

        // Act
        await service.ConnectAsync("test-token");

        // Assert
        service.IsConnected.Should().BeTrue();
        connectionChanged.Should().BeTrue();

        // Cleanup
        await service.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DisconnectAsync_AfterConnect_Disconnects()
    {
        if (!_canRun)
        {
            return;
        }

        // Arrange
        using var service = new RealtimeService(_supabaseUrl, _supabaseKey);
        await service.ConnectAsync("test-token");
        service.IsConnected.Should().BeTrue();

        // Act
        await service.DisconnectAsync();

        // Assert
        service.IsConnected.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SubscribeAsync_AfterConnect_ReturnsChannelId()
    {
        if (!_canRun)
        {
            return;
        }

        // Arrange
        using var service = new RealtimeService(_supabaseUrl, _supabaseKey);
        await service.ConnectAsync("test-token");

        // Act
        var channelId = await service.SubscribeAsync(
            table: "positions",
            onInsert: p => { });

        // Assert
        channelId.Should().NotBeNullOrEmpty();
        channelId.Should().HaveLength(8); // Guid substring

        // Cleanup
        await service.DisconnectAsync();
    }
}
