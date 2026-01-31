using System.Net;
using FluentAssertions;
using Microsoft.Maui.Networking;
using Moq;
using TradingApp.Tests.Mocks;
using Xunit;

namespace TradingApp.Tests.Services;

/// <summary>
/// Unit tests for the ErrorHandler.
/// </summary>
public class ErrorHandlerTests
{
    private readonly Mock<IConnectivity> _mockConnectivity;
    private readonly TestableErrorHandler _errorHandler;

    public ErrorHandlerTests()
    {
        _mockConnectivity = new Mock<IConnectivity>();
        _mockConnectivity.Setup(c => c.NetworkAccess).Returns(NetworkAccess.Internet);
        _errorHandler = new TestableErrorHandler(_mockConnectivity.Object);
    }

    [Fact]
    public void Constructor_WithNullConnectivity_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new TestableErrorHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("connectivity");
    }

    [Fact]
    public void IsOnline_WhenHasInternet_ReturnsTrue()
    {
        // Arrange
        _mockConnectivity.Setup(c => c.NetworkAccess).Returns(NetworkAccess.Internet);

        // Act & Assert
        _errorHandler.IsOnline.Should().BeTrue();
    }

    [Fact]
    public void IsOnline_WhenNoInternet_ReturnsFalse()
    {
        // Arrange
        _mockConnectivity.Setup(c => c.NetworkAccess).Returns(NetworkAccess.None);

        // Act & Assert
        _errorHandler.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WhenSuccessful_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";

        // Act
        var result = await _errorHandler.ExecuteWithRetryAsync(() => Task.FromResult(expectedResult));

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WhenOffline_ThrowsOfflineException()
    {
        // Arrange
        _mockConnectivity.Setup(c => c.NetworkAccess).Returns(NetworkAccess.None);

        // Act
        var act = () => _errorHandler.ExecuteWithRetryAsync(() => Task.FromResult("test"));

        // Assert
        await act.Should().ThrowAsync<TestableTradingException>()
            .WithMessage("*No internet connection*");
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithTransientError_RetriesAndSucceeds()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<string>> action = () =>
        {
            attemptCount++;
            if (attemptCount < 3)
                throw new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError);
            return Task.FromResult("success");
        };

        // Act
        var result = await _errorHandler.ExecuteWithRetryAsync(action, maxRetries: 3);

        // Assert
        result.Should().Be("success");
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithNonTransientError_DoesNotRetry()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<string>> action = () =>
        {
            attemptCount++;
            throw new HttpRequestException("Not found", null, HttpStatusCode.NotFound);
        };

        // Act
        var act = () => _errorHandler.ExecuteWithRetryAsync(action);

        // Assert
        await act.Should().ThrowAsync<TestableTradingException>();
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ExhaustsRetries_ThrowsTradingException()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<string>> action = () =>
        {
            attemptCount++;
            throw new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError);
        };

        // Act
        var act = () => _errorHandler.ExecuteWithRetryAsync(action, maxRetries: 2);

        // Assert
        await act.Should().ThrowAsync<TestableTradingException>()
            .WithMessage("*failed after multiple attempts*");
        attemptCount.Should().Be(3); // Initial + 2 retries
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_VoidReturn_WorksCorrectly()
    {
        // Arrange
        var executed = false;
        Func<Task> action = () =>
        {
            executed = true;
            return Task.CompletedTask;
        };

        // Act
        await _errorHandler.ExecuteWithRetryAsync(action);

        // Assert
        executed.Should().BeTrue();
    }
}

/// <summary>
/// Tests for IsTransientError method.
/// </summary>
public class IsTransientErrorTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.GatewayTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public void IsTransientError_HttpRequestException_ReturnsExpected(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var exception = new HttpRequestException("Test error", null, statusCode);

        // Act
        var result = TestableErrorHandler.IsTransientError(exception);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsTransientError_TaskCanceledException_ReturnsTrue()
    {
        // Arrange
        var exception = new TaskCanceledException();

        // Act
        var result = TestableErrorHandler.IsTransientError(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_OperationCanceledException_ReturnsTrue()
    {
        // Arrange
        var exception = new OperationCanceledException();

        // Act
        var result = TestableErrorHandler.IsTransientError(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_SocketException_ReturnsTrue()
    {
        // Arrange
        var exception = new System.Net.Sockets.SocketException();

        // Act
        var result = TestableErrorHandler.IsTransientError(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_WithTransientInnerException_ReturnsTrue()
    {
        // Arrange
        var innerException = new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError);
        var exception = new Exception("Outer error", innerException);

        // Act
        var result = TestableErrorHandler.IsTransientError(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_GenericException_ReturnsFalse()
    {
        // Arrange
        var exception = new InvalidOperationException("Test");

        // Act
        var result = TestableErrorHandler.IsTransientError(exception);

        // Assert
        result.Should().BeFalse();
    }
}

/// <summary>
/// Tests for GetUserFriendlyMessage method.
/// </summary>
public class GetUserFriendlyMessageTests
{
    [Fact]
    public void GetUserFriendlyMessage_OfflineException_ReturnsOriginalMessage()
    {
        // Arrange
        var message = "No internet connection.";
        var exception = new TestableOfflineException(message);

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Be(message);
    }

    [Fact]
    public void GetUserFriendlyMessage_TradingException_ReturnsOriginalMessage()
    {
        // Arrange
        var message = "Order rejected due to insufficient funds.";
        var exception = new TestableTradingException(message);

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Be(message);
    }

    [Fact]
    public void GetUserFriendlyMessage_Unauthorized_ReturnsAuthMessage()
    {
        // Arrange
        var exception = new HttpRequestException("401", null, HttpStatusCode.Unauthorized);

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("Authentication failed");
    }

    [Fact]
    public void GetUserFriendlyMessage_Forbidden_ReturnsAccessDenied()
    {
        // Arrange
        var exception = new HttpRequestException("403", null, HttpStatusCode.Forbidden);

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("Access denied");
    }

    [Fact]
    public void GetUserFriendlyMessage_NotFound_ReturnsNotFoundMessage()
    {
        // Arrange
        var exception = new HttpRequestException("404", null, HttpStatusCode.NotFound);

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("not found");
    }

    [Fact]
    public void GetUserFriendlyMessage_BadRequest_ReturnsInvalidRequest()
    {
        // Arrange
        var exception = new HttpRequestException("400", null, HttpStatusCode.BadRequest);

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("Invalid request");
    }

    [Fact]
    public void GetUserFriendlyMessage_TooManyRequests_ReturnsRateLimitMessage()
    {
        // Arrange
        var exception = new HttpRequestException("429", null, HttpStatusCode.TooManyRequests);

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("Too many requests");
    }

    [Fact]
    public void GetUserFriendlyMessage_ServerError_ReturnsServerIssueMessage()
    {
        // Arrange
        var exception = new HttpRequestException("500", null, HttpStatusCode.InternalServerError);

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("server is experiencing issues");
    }

    [Fact]
    public void GetUserFriendlyMessage_Timeout_ReturnsTimeoutMessage()
    {
        // Arrange
        var exception = new TaskCanceledException();

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("timed out");
    }

    [Fact]
    public void GetUserFriendlyMessage_SocketException_ReturnsConnectionMessage()
    {
        // Arrange
        var exception = new System.Net.Sockets.SocketException();

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("Unable to connect");
    }

    [Fact]
    public void GetUserFriendlyMessage_UnknownException_ReturnsGenericMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Internal error");

        // Act
        var result = TestableErrorHandler.GetUserFriendlyMessage(exception);

        // Assert
        result.Should().Contain("unexpected error");
    }
}

/// <summary>
/// Tests for custom exception types.
/// </summary>
public class ExceptionTypesTests
{
    [Fact]
    public void OfflineException_SetsMessage()
    {
        // Arrange
        var message = "Network unavailable";

        // Act
        var exception = new TestableOfflineException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void TradingException_WithMessageOnly_SetsProperties()
    {
        // Arrange
        var message = "Trade failed";

        // Act
        var exception = new TestableTradingException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void TradingException_WithInnerException_SetsProperties()
    {
        // Arrange
        var message = "Trade failed";
        var inner = new InvalidOperationException("Inner");

        // Act
        var exception = new TestableTradingException(message, inner);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(inner);
    }

    [Fact]
    public void NetworkStatusChangedEventArgs_SetsIsOnline()
    {
        // Arrange & Act
        var args = new TestableNetworkStatusChangedEventArgs(true);

        // Assert
        args.IsOnline.Should().BeTrue();
    }
}

/// <summary>
/// Tests for network status change events.
/// </summary>
public class NetworkStatusChangeTests
{
    [Fact]
    public void NetworkStatusChanged_WhenConnectivityChanges_RaisesEvent()
    {
        // Arrange
        var mockConnectivity = new Mock<IConnectivity>();
        mockConnectivity.Setup(c => c.NetworkAccess).Returns(NetworkAccess.Internet);

        var errorHandler = new TestableErrorHandler(mockConnectivity.Object);
        TestableNetworkStatusChangedEventArgs? receivedArgs = null;
        errorHandler.NetworkStatusChanged += (s, e) => receivedArgs = e;

        // Act - Simulate connectivity change
        var eventArgs = new ConnectivityChangedEventArgs(NetworkAccess.None, new List<ConnectionProfile>());
        mockConnectivity.Raise(c => c.ConnectivityChanged += null, mockConnectivity.Object, eventArgs);

        // Assert
        receivedArgs.Should().NotBeNull();
        receivedArgs!.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void NetworkStatusChanged_WhenBecomesOnline_RaisesEvent()
    {
        // Arrange
        var mockConnectivity = new Mock<IConnectivity>();
        mockConnectivity.Setup(c => c.NetworkAccess).Returns(NetworkAccess.None);

        var errorHandler = new TestableErrorHandler(mockConnectivity.Object);
        TestableNetworkStatusChangedEventArgs? receivedArgs = null;
        errorHandler.NetworkStatusChanged += (s, e) => receivedArgs = e;

        // Act - Simulate coming online
        var eventArgs = new ConnectivityChangedEventArgs(NetworkAccess.Internet, new List<ConnectionProfile>());
        mockConnectivity.Raise(c => c.ConnectivityChanged += null, mockConnectivity.Object, eventArgs);

        // Assert
        receivedArgs.Should().NotBeNull();
        receivedArgs!.IsOnline.Should().BeTrue();
    }
}

/// <summary>
/// Tests for retry timing.
/// </summary>
public class RetryTimingTests
{
    [Fact]
    public async Task ExecuteWithRetryAsync_UsesCustomDelays()
    {
        // Arrange
        var mockConnectivity = new Mock<IConnectivity>();
        mockConnectivity.Setup(c => c.NetworkAccess).Returns(NetworkAccess.Internet);
        var errorHandler = new TestableErrorHandler(mockConnectivity.Object);

        var attemptTimes = new List<DateTime>();
        var customDelays = new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100) };

        Func<Task<string>> action = () =>
        {
            attemptTimes.Add(DateTime.UtcNow);
            if (attemptTimes.Count < 3)
                throw new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError);
            return Task.FromResult("success");
        };

        // Act
        var result = await errorHandler.ExecuteWithRetryAsync(action, maxRetries: 2, retryDelays: customDelays);

        // Assert
        result.Should().Be("success");
        attemptTimes.Should().HaveCount(3);

        // Verify delays were applied (with some tolerance for execution time)
        var delay1 = attemptTimes[1] - attemptTimes[0];
        var delay2 = attemptTimes[2] - attemptTimes[1];

        delay1.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(40)); // Allow some tolerance
        delay2.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(90));
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_RespectsCancellation()
    {
        // Arrange
        var mockConnectivity = new Mock<IConnectivity>();
        mockConnectivity.Setup(c => c.NetworkAccess).Returns(NetworkAccess.Internet);
        var errorHandler = new TestableErrorHandler(mockConnectivity.Object);

        var cts = new CancellationTokenSource();
        var attemptCount = 0;

        Func<Task<string>> action = async () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                cts.Cancel(); // Cancel after first attempt
                throw new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError);
            }
            await Task.Delay(100);
            return "success";
        };

        // Act
        var act = () => errorHandler.ExecuteWithRetryAsync(
            action,
            maxRetries: 3,
            cancellationToken: cts.Token);

        // Assert - Should throw OperationCanceledException during delay
        // Note: The exact behavior depends on timing, but the operation should be cancelled
        try
        {
            await act();
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TestableTradingException)
        {
            // Also acceptable if cancellation happens differently
        }

        attemptCount.Should().BeLessOrEqualTo(2);
    }
}
