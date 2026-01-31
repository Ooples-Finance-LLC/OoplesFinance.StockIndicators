using System.Net;

namespace OoplesFinance.TradingApp.Maui.Helpers;

/// <summary>
/// Global error handler with retry logic, user-friendly messages, and offline detection.
/// </summary>
public sealed class ErrorHandler
{
    private readonly IConnectivity _connectivity;
    private static readonly TimeSpan[] DefaultRetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };

    /// <summary>
    /// Event raised when network status changes.
    /// </summary>
    public event EventHandler<NetworkStatusChangedEventArgs>? NetworkStatusChanged;

    /// <summary>
    /// Gets whether the device is currently online.
    /// </summary>
    public bool IsOnline => _connectivity.NetworkAccess == NetworkAccess.Internet;

    public ErrorHandler(IConnectivity connectivity)
    {
        _connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var isOnline = e.NetworkAccess == NetworkAccess.Internet;
        NetworkStatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs(isOnline));
    }

    /// <summary>
    /// Executes an action with automatic retry on transient failures.
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        int maxRetries = 3,
        TimeSpan[]? retryDelays = null,
        CancellationToken cancellationToken = default)
    {
        retryDelays ??= DefaultRetryDelays;
        var lastException = default(Exception);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Check connectivity before attempting
                if (!IsOnline)
                {
                    throw new OfflineException("No internet connection. Please check your network settings.");
                }

                return await action().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                lastException = ex;
                var delay = attempt < retryDelays.Length ? retryDelays[attempt] : retryDelays[retryDelays.Length - 1];

                // Log retry attempt
                System.Diagnostics.Debug.WriteLine($"Retry {attempt + 1}/{maxRetries} after {delay.TotalSeconds}s: {ex.Message}");

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!IsTransientError(ex))
            {
                // Non-transient error, don't retry
                throw new TradingException(GetUserFriendlyMessage(ex), ex);
            }
        }

        // All retries exhausted
        throw new TradingException(
            "The operation failed after multiple attempts. Please try again later.",
            lastException);
    }

    /// <summary>
    /// Executes an action with automatic retry (void return).
    /// </summary>
    public async Task ExecuteWithRetryAsync(
        Func<Task> action,
        int maxRetries = 3,
        TimeSpan[]? retryDelays = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }, maxRetries, retryDelays, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines if an error is transient and should be retried.
    /// </summary>
    public static bool IsTransientError(Exception ex)
    {
        // Network errors
        if (ex is HttpRequestException httpEx)
        {
            // Server errors (5xx) are transient
            if (httpEx.StatusCode.HasValue && (int)httpEx.StatusCode >= 500)
                return true;

            // Too many requests (429) is transient
            if (httpEx.StatusCode == HttpStatusCode.TooManyRequests)
                return true;

            // Request timeout (408) is transient
            if (httpEx.StatusCode == HttpStatusCode.RequestTimeout)
                return true;
        }

        // Timeout
        if (ex is TaskCanceledException or OperationCanceledException)
            return true;

        // Socket errors
        if (ex is System.Net.Sockets.SocketException)
            return true;

        // Check inner exceptions
        if (ex.InnerException is not null && IsTransientError(ex.InnerException))
            return true;

        return false;
    }

    /// <summary>
    /// Gets a user-friendly error message for an exception.
    /// </summary>
    public static string GetUserFriendlyMessage(Exception ex)
    {
        return ex switch
        {
            OfflineException => ex.Message,
            TradingException => ex.Message,
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Unauthorized =>
                "Authentication failed. Please check your API credentials.",
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Forbidden =>
                "Access denied. You may not have permission for this operation.",
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.NotFound =>
                "The requested resource was not found.",
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.BadRequest =>
                "Invalid request. Please check your input and try again.",
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.TooManyRequests =>
                "Too many requests. Please wait a moment and try again.",
            HttpRequestException httpEx when (int?)httpEx.StatusCode >= 500 =>
                "The server is experiencing issues. Please try again later.",
            TaskCanceledException or OperationCanceledException =>
                "The request timed out. Please try again.",
            System.Net.Sockets.SocketException =>
                "Unable to connect to the server. Please check your internet connection.",
            _ => "An unexpected error occurred. Please try again."
        };
    }

    /// <summary>
    /// Shows an error alert to the user.
    /// </summary>
    public static async Task ShowErrorAsync(Exception ex, string title = "Error")
    {
        var message = GetUserFriendlyMessage(ex);
        await Application.Current!.MainPage!.DisplayAlert(title, message, "OK");
    }

    /// <summary>
    /// Shows an error alert with retry option.
    /// </summary>
    public static async Task<bool> ShowErrorWithRetryAsync(Exception ex, string title = "Error")
    {
        var message = GetUserFriendlyMessage(ex);
        return await Application.Current!.MainPage!.DisplayAlert(title, message, "Retry", "Cancel");
    }
}

/// <summary>
/// Exception indicating the device is offline.
/// </summary>
public sealed class OfflineException : Exception
{
    public OfflineException(string message) : base(message) { }
}

/// <summary>
/// General trading exception with user-friendly message.
/// </summary>
public sealed class TradingException : Exception
{
    public TradingException(string message) : base(message) { }
    public TradingException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// Event args for network status changes.
/// </summary>
public sealed class NetworkStatusChangedEventArgs : EventArgs
{
    public bool IsOnline { get; }

    public NetworkStatusChangedEventArgs(bool isOnline)
    {
        IsOnline = isOnline;
    }
}

/// <summary>
/// Extension methods for error handling.
/// </summary>
public static class ErrorHandlerExtensions
{
    /// <summary>
    /// Executes an async action with error handling and optional retry.
    /// </summary>
    public static async Task SafeExecuteAsync(this Task task, string operationName, bool showError = true)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{operationName} failed: {ex}");

            if (showError)
            {
                await ErrorHandler.ShowErrorAsync(ex, operationName);
            }
        }
    }

    /// <summary>
    /// Executes an async function with error handling and optional retry.
    /// </summary>
    public static async Task<T?> SafeExecuteAsync<T>(this Task<T> task, string operationName, bool showError = true)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{operationName} failed: {ex}");

            if (showError)
            {
                await ErrorHandler.ShowErrorAsync(ex, operationName);
            }

            return default;
        }
    }
}
