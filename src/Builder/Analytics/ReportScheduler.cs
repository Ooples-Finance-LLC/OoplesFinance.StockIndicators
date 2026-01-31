using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;

namespace OoplesFinance.StockIndicators.Builder.Analytics;

/// <summary>
/// Schedules and delivers reports on configurable schedules.
/// Supports multiple delivery channels and report formats.
/// </summary>
public sealed class ReportScheduler : IDisposable
{
    private readonly ReportSchedulerOptions _options;
    private readonly ConcurrentDictionary<string, ScheduledReport> _scheduledReports = new();
    private readonly ConcurrentDictionary<string, IReportGenerator> _reportGenerators = new();
    private readonly ConcurrentDictionary<string, IReportDeliveryChannel> _deliveryChannels = new();
    private readonly Timer _schedulerTimer;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ReportScheduler.
    /// </summary>
    public ReportScheduler(ReportSchedulerOptions? options = null)
    {
        _options = options ?? new ReportSchedulerOptions();

        _schedulerTimer = new Timer(
            CheckAndExecuteSchedules,
            null,
            TimeSpan.FromSeconds(10),
            _options.ScheduleCheckInterval);
    }

    /// <summary>
    /// Registers a report generator.
    /// </summary>
    public void RegisterGenerator(string reportType, IReportGenerator generator)
    {
        _reportGenerators[reportType] = generator;
    }

    /// <summary>
    /// Registers a delivery channel.
    /// </summary>
    public void RegisterDeliveryChannel(string channelId, IReportDeliveryChannel channel)
    {
        _deliveryChannels[channelId] = channel;
    }

    /// <summary>
    /// Schedules a new report.
    /// </summary>
    public ScheduledReport ScheduleReport(
        string name,
        string reportType,
        ReportSchedule schedule,
        List<DeliveryTarget> deliveryTargets,
        Dictionary<string, object>? parameters = null)
    {
        if (!_reportGenerators.ContainsKey(reportType))
        {
            throw new ArgumentException($"Unknown report type: {reportType}");
        }

        var report = new ScheduledReport
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            ReportType = reportType,
            Schedule = schedule,
            DeliveryTargets = deliveryTargets,
            Parameters = parameters ?? new Dictionary<string, object>(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            NextRunTime = CalculateNextRunTime(schedule, DateTime.UtcNow)
        };

        _scheduledReports[report.Id] = report;
        return report;
    }

    /// <summary>
    /// Unschedules a report.
    /// </summary>
    public bool UnscheduleReport(string reportId)
    {
        return _scheduledReports.TryRemove(reportId, out _);
    }

    /// <summary>
    /// Enables or disables a scheduled report.
    /// </summary>
    public bool SetReportEnabled(string reportId, bool enabled)
    {
        if (!_scheduledReports.TryGetValue(reportId, out var report))
        {
            return false;
        }

        report.IsEnabled = enabled;

        if (enabled)
        {
            report.NextRunTime = CalculateNextRunTime(report.Schedule, DateTime.UtcNow);
        }

        return true;
    }

    /// <summary>
    /// Gets all scheduled reports.
    /// </summary>
    public IReadOnlyList<ScheduledReport> GetScheduledReports()
    {
        return _scheduledReports.Values.ToList();
    }

    /// <summary>
    /// Gets a scheduled report by ID.
    /// </summary>
    public ScheduledReport? GetScheduledReport(string reportId)
    {
        return _scheduledReports.TryGetValue(reportId, out var report) ? report : null;
    }

    /// <summary>
    /// Runs a report immediately, bypassing the schedule.
    /// </summary>
    public async Task<ReportExecutionResult> RunReportNowAsync(
        string reportId,
        CancellationToken ct = default)
    {
        if (!_scheduledReports.TryGetValue(reportId, out var report))
        {
            return new ReportExecutionResult
            {
                Success = false,
                Error = $"Report not found: {reportId}"
            };
        }

        return await ExecuteReportAsync(report, ct);
    }

    /// <summary>
    /// Generates a one-time report without scheduling.
    /// </summary>
    public async Task<GeneratedReport> GenerateReportAsync(
        string reportType,
        Dictionary<string, object>? parameters = null,
        ReportFormat format = ReportFormat.Pdf,
        CancellationToken ct = default)
    {
        if (!_reportGenerators.TryGetValue(reportType, out var generator))
        {
            throw new ArgumentException($"Unknown report type: {reportType}");
        }

        var context = new ReportGenerationContext
        {
            ReportType = reportType,
            Parameters = parameters ?? new Dictionary<string, object>(),
            Format = format,
            GeneratedAt = DateTime.UtcNow
        };

        return await generator.GenerateAsync(context, ct);
    }

    /// <summary>
    /// Gets the execution history for a report.
    /// </summary>
    public IReadOnlyList<ReportExecutionRecord> GetExecutionHistory(string reportId, int limit = 10)
    {
        if (!_scheduledReports.TryGetValue(reportId, out var report))
        {
            return [];
        }

        return report.ExecutionHistory
            .OrderByDescending(e => e.ExecutedAt)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Event raised when a report execution starts.
    /// </summary>
    public event EventHandler<ReportExecutionStartedEventArgs>? OnExecutionStarted;

    /// <summary>
    /// Event raised when a report execution completes.
    /// </summary>
    public event EventHandler<ReportExecutionCompletedEventArgs>? OnExecutionCompleted;

    /// <summary>
    /// Event raised when a report delivery completes.
    /// </summary>
    public event EventHandler<ReportDeliveryCompletedEventArgs>? OnDeliveryCompleted;

    private async void CheckAndExecuteSchedules(object? state)
    {
        if (_disposed) return;

        if (!await _executionLock.WaitAsync(0)) return;

        try
        {
            var now = DateTime.UtcNow;
            var dueReports = _scheduledReports.Values
                .Where(r => r.IsEnabled && r.NextRunTime <= now)
                .ToList();

            foreach (var report in dueReports)
            {
                try
                {
                    await ExecuteReportAsync(report, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Log but continue with other reports
                    report.LastError = ex.Message;
                }

                // Update next run time
                report.NextRunTime = CalculateNextRunTime(report.Schedule, now);
            }
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private async Task<ReportExecutionResult> ExecuteReportAsync(
        ScheduledReport report,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var executionId = Guid.NewGuid().ToString("N");

        OnExecutionStarted?.Invoke(this, new ReportExecutionStartedEventArgs
        {
            ReportId = report.Id,
            ExecutionId = executionId,
            StartedAt = startTime
        });

        var result = new ReportExecutionResult
        {
            ExecutionId = executionId,
            ReportId = report.Id,
            StartedAt = startTime
        };

        try
        {
            if (!_reportGenerators.TryGetValue(report.ReportType, out var generator))
            {
                result.Success = false;
                result.Error = $"Generator not found for type: {report.ReportType}";
                return result;
            }

            // Generate report for each format needed
            var formats = report.DeliveryTargets
                .Select(t => t.Format)
                .Distinct()
                .ToList();

            var generatedReports = new Dictionary<ReportFormat, GeneratedReport>();

            foreach (var format in formats)
            {
                var context = new ReportGenerationContext
                {
                    ReportType = report.ReportType,
                    Parameters = report.Parameters,
                    Format = format,
                    GeneratedAt = startTime
                };

                generatedReports[format] = await generator.GenerateAsync(context, ct);
            }

            // Deliver to each target
            var deliveryResults = new List<DeliveryResult>();

            foreach (var target in report.DeliveryTargets)
            {
                if (!_deliveryChannels.TryGetValue(target.ChannelId, out var channel))
                {
                    deliveryResults.Add(new DeliveryResult
                    {
                        TargetId = target.Id,
                        Success = false,
                        Error = $"Channel not found: {target.ChannelId}"
                    });
                    continue;
                }

                var generatedReport = generatedReports[target.Format];

                try
                {
                    var delivered = await channel.DeliverAsync(generatedReport, target, ct);

                    deliveryResults.Add(new DeliveryResult
                    {
                        TargetId = target.Id,
                        Success = delivered,
                        DeliveredAt = DateTime.UtcNow
                    });

                    OnDeliveryCompleted?.Invoke(this, new ReportDeliveryCompletedEventArgs
                    {
                        ReportId = report.Id,
                        ExecutionId = executionId,
                        TargetId = target.Id,
                        Success = delivered
                    });
                }
                catch (Exception ex)
                {
                    deliveryResults.Add(new DeliveryResult
                    {
                        TargetId = target.Id,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            result.Success = deliveryResults.All(d => d.Success);
            result.DeliveryResults = deliveryResults;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - startTime;

            // Update report state
            report.LastRunTime = startTime;
            report.LastError = result.Success ? null : result.Error;

            // Add to history
            report.ExecutionHistory.Add(new ReportExecutionRecord
            {
                ExecutionId = executionId,
                ExecutedAt = startTime,
                Success = result.Success,
                Duration = result.Duration,
                Error = result.Error
            });

            // Trim history
            while (report.ExecutionHistory.Count > _options.MaxHistoryPerReport)
            {
                report.ExecutionHistory.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - startTime;

            report.LastError = ex.Message;
        }

        OnExecutionCompleted?.Invoke(this, new ReportExecutionCompletedEventArgs
        {
            ReportId = report.Id,
            ExecutionId = executionId,
            Result = result
        });

        return result;
    }

    private DateTime CalculateNextRunTime(ReportSchedule schedule, DateTime fromTime)
    {
        switch (schedule.Frequency)
        {
            case ScheduleFrequency.Once:
                return schedule.StartTime ?? fromTime.AddHours(1);

            case ScheduleFrequency.Hourly:
                return fromTime.AddHours(1);

            case ScheduleFrequency.Daily:
                var nextDaily = fromTime.Date.AddDays(1).Add(schedule.TimeOfDay);
                return nextDaily <= fromTime ? nextDaily.AddDays(1) : nextDaily;

            case ScheduleFrequency.Weekly:
                var daysToAdd = ((int)schedule.DayOfWeek - (int)fromTime.DayOfWeek + 7) % 7;
                if (daysToAdd == 0 && fromTime.TimeOfDay >= schedule.TimeOfDay)
                {
                    daysToAdd = 7;
                }
                return fromTime.Date.AddDays(daysToAdd).Add(schedule.TimeOfDay);

            case ScheduleFrequency.Monthly:
                var nextMonthly = new DateTime(fromTime.Year, fromTime.Month, Math.Min(schedule.DayOfMonth, DateTime.DaysInMonth(fromTime.Year, fromTime.Month))).Add(schedule.TimeOfDay);
                if (nextMonthly <= fromTime)
                {
                    nextMonthly = nextMonthly.AddMonths(1);
                    nextMonthly = new DateTime(nextMonthly.Year, nextMonthly.Month, Math.Min(schedule.DayOfMonth, DateTime.DaysInMonth(nextMonthly.Year, nextMonthly.Month))).Add(schedule.TimeOfDay);
                }
                return nextMonthly;

            case ScheduleFrequency.Quarterly:
                var quarterMonth = ((fromTime.Month - 1) / 3 + 1) * 3 + 1;
                var quarterYear = fromTime.Year;
                if (quarterMonth > 12)
                {
                    quarterMonth = 1;
                    quarterYear++;
                }
                return new DateTime(quarterYear, quarterMonth, Math.Min(schedule.DayOfMonth, DateTime.DaysInMonth(quarterYear, quarterMonth))).Add(schedule.TimeOfDay);

            case ScheduleFrequency.Custom:
                if (schedule.CronExpression is { Length: > 0 } cron)
                {
                    return CalculateNextCronTime(cron, fromTime);
                }
                return fromTime.AddDays(1);

            default:
                return fromTime.AddDays(1);
        }
    }

    private DateTime CalculateNextCronTime(string cronExpression, DateTime fromTime)
    {
        // Simple cron parser for basic expressions
        // Format: minute hour dayOfMonth month dayOfWeek
        var parts = cronExpression.Split(' ');
        if (parts.Length != 5)
        {
            return fromTime.AddDays(1);
        }

        var minute = ParseCronField(parts[0], 0, 59);
        var hour = ParseCronField(parts[1], 0, 23);
        var dayOfMonth = ParseCronField(parts[2], 1, 31);
        var month = ParseCronField(parts[3], 1, 12);
        var dayOfWeek = ParseCronField(parts[4], 0, 6);

        var next = fromTime.AddMinutes(1);
        var maxIterations = 366 * 24 * 60; // Max 1 year ahead

        for (var i = 0; i < maxIterations; i++)
        {
            if ((minute == null || minute.Contains(next.Minute)) &&
                (hour == null || hour.Contains(next.Hour)) &&
                (dayOfMonth == null || dayOfMonth.Contains(next.Day)) &&
                (month == null || month.Contains(next.Month)) &&
                (dayOfWeek == null || dayOfWeek.Contains((int)next.DayOfWeek)))
            {
                return next;
            }

            next = next.AddMinutes(1);
        }

        return fromTime.AddDays(1);
    }

    private HashSet<int>? ParseCronField(string field, int min, int max)
    {
        if (field == "*")
        {
            return null;
        }

        var values = new HashSet<int>();

        foreach (var part in field.Split(','))
        {
            if (part.Contains('/'))
            {
                var stepParts = part.Split('/');
                var start = stepParts[0] == "*" ? min : int.Parse(stepParts[0], CultureInfo.InvariantCulture);
                var step = int.Parse(stepParts[1], CultureInfo.InvariantCulture);

                for (var i = start; i <= max; i += step)
                {
                    values.Add(i);
                }
            }
            else if (part.Contains('-'))
            {
                var rangeParts = part.Split('-');
                var rangeStart = int.Parse(rangeParts[0], CultureInfo.InvariantCulture);
                var rangeEnd = int.Parse(rangeParts[1], CultureInfo.InvariantCulture);

                for (var i = rangeStart; i <= rangeEnd; i++)
                {
                    values.Add(i);
                }
            }
            else
            {
                values.Add(int.Parse(part, CultureInfo.InvariantCulture));
            }
        }

        return values;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _schedulerTimer.Dispose();
        _executionLock.Dispose();

        foreach (var channel in _deliveryChannels.Values)
        {
            if (channel is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

/// <summary>
/// Report scheduler options.
/// </summary>
public sealed class ReportSchedulerOptions
{
    /// <summary>Interval for checking schedules.</summary>
    public TimeSpan ScheduleCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Maximum execution history per report.</summary>
    public int MaxHistoryPerReport { get; set; } = 100;

    /// <summary>Timeout for report generation.</summary>
    public TimeSpan GenerationTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Timeout for report delivery.</summary>
    public TimeSpan DeliveryTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Scheduled report.
/// </summary>
public sealed class ScheduledReport
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public ReportSchedule Schedule { get; set; } = new();
    public List<DeliveryTarget> DeliveryTargets { get; set; } = [];
    public Dictionary<string, object> Parameters { get; set; } = [];
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public string? LastError { get; set; }
    public List<ReportExecutionRecord> ExecutionHistory { get; set; } = [];
}

/// <summary>
/// Report schedule.
/// </summary>
public sealed class ReportSchedule
{
    public ScheduleFrequency Frequency { get; set; }
    public TimeSpan TimeOfDay { get; set; } = new TimeSpan(8, 0, 0);
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;
    public int DayOfMonth { get; set; } = 1;
    public DateTime? StartTime { get; set; }
    public string? CronExpression { get; set; }
    public string? TimeZone { get; set; }
}

/// <summary>
/// Schedule frequency.
/// </summary>
public enum ScheduleFrequency
{
    Once,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Custom
}

/// <summary>
/// Delivery target.
/// </summary>
public sealed class DeliveryTarget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ChannelId { get; set; } = string.Empty;
    public ReportFormat Format { get; set; }
    public Dictionary<string, string> Configuration { get; set; } = [];
}

/// <summary>
/// Report format.
/// </summary>
public enum ReportFormat
{
    Pdf,
    Excel,
    Csv,
    Html,
    Json
}

/// <summary>
/// Report execution result.
/// </summary>
public sealed class ReportExecutionResult
{
    public string ExecutionId { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public List<DeliveryResult> DeliveryResults { get; set; } = [];
    public string? Error { get; set; }
}

/// <summary>
/// Delivery result.
/// </summary>
public sealed class DeliveryResult
{
    public string TargetId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Report execution record.
/// </summary>
public sealed class ReportExecutionRecord
{
    public string ExecutionId { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public bool Success { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Report generation context.
/// </summary>
public sealed class ReportGenerationContext
{
    public string ReportType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = [];
    public ReportFormat Format { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Generated report.
/// </summary>
public sealed class GeneratedReport
{
    public string ReportType { get; set; } = string.Empty;
    public ReportFormat Format { get; set; }
    public byte[] Content { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Report generator interface.
/// </summary>
public interface IReportGenerator
{
    Task<GeneratedReport> GenerateAsync(ReportGenerationContext context, CancellationToken ct = default);
}

/// <summary>
/// Report delivery channel interface.
/// </summary>
public interface IReportDeliveryChannel
{
    Task<bool> DeliverAsync(GeneratedReport report, DeliveryTarget target, CancellationToken ct = default);
}

/// <summary>
/// Report execution started event args.
/// </summary>
public sealed class ReportExecutionStartedEventArgs : EventArgs
{
    public string ReportId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Report execution completed event args.
/// </summary>
public sealed class ReportExecutionCompletedEventArgs : EventArgs
{
    public string ReportId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public ReportExecutionResult Result { get; set; } = new();
}

/// <summary>
/// Report delivery completed event args.
/// </summary>
public sealed class ReportDeliveryCompletedEventArgs : EventArgs
{
    public string ReportId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public bool Success { get; set; }
}

/// <summary>
/// Email delivery channel.
/// </summary>
public sealed class EmailDeliveryChannel : IReportDeliveryChannel
{
    private readonly Func<string, string, byte[], string, CancellationToken, Task> _sendEmail;

    public EmailDeliveryChannel(Func<string, string, byte[], string, CancellationToken, Task> sendEmail)
    {
        _sendEmail = sendEmail;
    }

    public async Task<bool> DeliverAsync(GeneratedReport report, DeliveryTarget target, CancellationToken ct = default)
    {
        target.Configuration.TryGetValue("to", out var toAddress);
        toAddress ??= string.Empty;

        if (!target.Configuration.TryGetValue("subject", out var subject))
        {
            subject = $"Report: {report.ReportType}";
        }

        if (string.IsNullOrEmpty(toAddress))
        {
            return false;
        }

        await _sendEmail(toAddress, subject, report.Content, report.FileName, ct);
        return true;
    }
}

/// <summary>
/// File storage delivery channel.
/// </summary>
public sealed class FileStorageDeliveryChannel : IReportDeliveryChannel
{
    private readonly string _basePath;

    public FileStorageDeliveryChannel(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public Task<bool> DeliverAsync(GeneratedReport report, DeliveryTarget target, CancellationToken ct = default)
    {
        target.Configuration.TryGetValue("path", out var subPath);
        subPath ??= string.Empty;
        var fullPath = Path.Combine(_basePath, subPath, report.FileName);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(fullPath, report.Content);
        return Task.FromResult(true);
    }
}

/// <summary>
/// Webhook delivery channel.
/// </summary>
public sealed class WebhookDeliveryChannel : IReportDeliveryChannel, IDisposable
{
    private readonly HttpClient _httpClient;

    public WebhookDeliveryChannel(TimeSpan? timeout = null)
    {
        _httpClient = new HttpClient
        {
            Timeout = timeout ?? TimeSpan.FromMinutes(5)
        };
    }

    public async Task<bool> DeliverAsync(GeneratedReport report, DeliveryTarget target, CancellationToken ct = default)
    {
        target.Configuration.TryGetValue("url", out var url);
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        using var content = new ByteArrayContent(report.Content);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(report.MimeType);

        var response = await _httpClient.PostAsync(url, content, ct);
        return response.IsSuccessStatusCode;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
