using System.Collections.Concurrent;
using System.Text.Json;

namespace OoplesFinance.StockIndicators.Builder.Analytics;

/// <summary>
/// Builds customizable dashboards with various widget types.
/// Supports real-time updates and user personalization.
/// </summary>
public sealed class DashboardBuilder
{
    private readonly DashboardBuilderOptions _options;
    private readonly ConcurrentDictionary<string, Dashboard> _dashboards = new();
    private readonly ConcurrentDictionary<string, IWidgetDataProvider> _dataProviders = new();
    private readonly ConcurrentDictionary<string, WidgetTemplate> _widgetTemplates = new();

    /// <summary>
    /// Initializes a new instance of the DashboardBuilder.
    /// </summary>
    public DashboardBuilder(DashboardBuilderOptions? options = null)
    {
        _options = options ?? new DashboardBuilderOptions();
        RegisterBuiltInTemplates();
    }

    /// <summary>
    /// Creates a new dashboard.
    /// </summary>
    public Dashboard CreateDashboard(
        string name,
        string ownerId,
        DashboardLayout layout = DashboardLayout.Grid)
    {
        var dashboard = new Dashboard
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            OwnerId = ownerId,
            Layout = layout,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dashboards[dashboard.Id] = dashboard;
        return dashboard;
    }

    /// <summary>
    /// Gets a dashboard by ID.
    /// </summary>
    public Dashboard? GetDashboard(string dashboardId)
    {
        return _dashboards.TryGetValue(dashboardId, out var dashboard) ? dashboard : null;
    }

    /// <summary>
    /// Gets all dashboards for a user.
    /// </summary>
    public IReadOnlyList<Dashboard> GetUserDashboards(string ownerId)
    {
        return _dashboards.Values
            .Where(d => d.OwnerId == ownerId)
            .OrderByDescending(d => d.UpdatedAt)
            .ToList();
    }

    /// <summary>
    /// Adds a widget to a dashboard.
    /// </summary>
    public DashboardWidget AddWidget(
        string dashboardId,
        string templateId,
        WidgetPosition position,
        Dictionary<string, object>? configuration = null)
    {
        if (!_dashboards.TryGetValue(dashboardId, out var dashboard))
        {
            throw new ArgumentException($"Dashboard not found: {dashboardId}");
        }

        if (!_widgetTemplates.TryGetValue(templateId, out var template))
        {
            throw new ArgumentException($"Widget template not found: {templateId}");
        }

        var widget = new DashboardWidget
        {
            Id = Guid.NewGuid().ToString("N"),
            TemplateId = templateId,
            Title = template.DefaultTitle,
            Position = position,
            Configuration = configuration ?? new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow
        };

        // Apply default configuration
        foreach (var kvp in template.DefaultConfiguration)
        {
            if (!widget.Configuration.ContainsKey(kvp.Key))
            {
                widget.Configuration[kvp.Key] = kvp.Value;
            }
        }

        dashboard.Widgets.Add(widget);
        dashboard.UpdatedAt = DateTime.UtcNow;

        return widget;
    }

    /// <summary>
    /// Removes a widget from a dashboard.
    /// </summary>
    public bool RemoveWidget(string dashboardId, string widgetId)
    {
        if (!_dashboards.TryGetValue(dashboardId, out var dashboard))
        {
            return false;
        }

        var widget = dashboard.Widgets.FirstOrDefault(w => w.Id == widgetId);
        if (widget == null)
        {
            return false;
        }

        dashboard.Widgets.Remove(widget);
        dashboard.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Updates a widget's configuration.
    /// </summary>
    public bool UpdateWidget(
        string dashboardId,
        string widgetId,
        Dictionary<string, object>? configuration = null,
        WidgetPosition? position = null,
        string? title = null)
    {
        if (!_dashboards.TryGetValue(dashboardId, out var dashboard))
        {
            return false;
        }

        var widget = dashboard.Widgets.FirstOrDefault(w => w.Id == widgetId);
        if (widget == null)
        {
            return false;
        }

        if (configuration != null)
        {
            foreach (var kvp in configuration)
            {
                widget.Configuration[kvp.Key] = kvp.Value;
            }
        }

        if (position != null)
        {
            widget.Position = position;
        }

        if (title != null)
        {
            widget.Title = title;
        }

        dashboard.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Gets data for all widgets in a dashboard.
    /// </summary>
    public async Task<DashboardData> GetDashboardDataAsync(
        string dashboardId,
        DashboardDataContext context,
        CancellationToken ct = default)
    {
        if (!_dashboards.TryGetValue(dashboardId, out var dashboard))
        {
            throw new ArgumentException($"Dashboard not found: {dashboardId}");
        }

        var dashboardData = new DashboardData
        {
            DashboardId = dashboardId,
            GeneratedAt = DateTime.UtcNow,
            WidgetData = new Dictionary<string, WidgetData>()
        };

        var tasks = dashboard.Widgets.Select(async widget =>
        {
            try
            {
                var data = await GetWidgetDataAsync(widget, context, ct);
                return (widget.Id, data);
            }
            catch (Exception ex)
            {
                return (widget.Id, new WidgetData
                {
                    WidgetId = widget.Id,
                    Error = ex.Message,
                    GeneratedAt = DateTime.UtcNow
                });
            }
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (widgetId, data) in results)
        {
            dashboardData.WidgetData[widgetId] = data;
        }

        return dashboardData;
    }

    /// <summary>
    /// Registers a widget template.
    /// </summary>
    public void RegisterTemplate(WidgetTemplate template)
    {
        _widgetTemplates[template.Id] = template;
    }

    /// <summary>
    /// Registers a data provider.
    /// </summary>
    public void RegisterDataProvider(string templateId, IWidgetDataProvider provider)
    {
        _dataProviders[templateId] = provider;
    }

    /// <summary>
    /// Gets all available widget templates.
    /// </summary>
    public IReadOnlyList<WidgetTemplate> GetAvailableTemplates()
    {
        return _widgetTemplates.Values.ToList();
    }

    /// <summary>
    /// Exports a dashboard to JSON.
    /// </summary>
    public string ExportDashboard(string dashboardId)
    {
        if (!_dashboards.TryGetValue(dashboardId, out var dashboard))
        {
            throw new ArgumentException($"Dashboard not found: {dashboardId}");
        }

        return JsonSerializer.Serialize(dashboard, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Imports a dashboard from JSON.
    /// </summary>
    public Dashboard ImportDashboard(string json, string newOwnerId)
    {
        var imported = JsonSerializer.Deserialize<Dashboard>(json)
            ?? throw new ArgumentException("Invalid dashboard JSON");

        var dashboard = new Dashboard
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = imported.Name + " (Imported)",
            OwnerId = newOwnerId,
            Layout = imported.Layout,
            Widgets = imported.Widgets.Select(w => new DashboardWidget
            {
                Id = Guid.NewGuid().ToString("N"),
                TemplateId = w.TemplateId,
                Title = w.Title,
                Position = w.Position,
                Configuration = new Dictionary<string, object>(w.Configuration),
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dashboards[dashboard.Id] = dashboard;
        return dashboard;
    }

    /// <summary>
    /// Deletes a dashboard.
    /// </summary>
    public bool DeleteDashboard(string dashboardId)
    {
        return _dashboards.TryRemove(dashboardId, out _);
    }

    private async Task<WidgetData> GetWidgetDataAsync(
        DashboardWidget widget,
        DashboardDataContext context,
        CancellationToken ct)
    {
        if (!_dataProviders.TryGetValue(widget.TemplateId, out var provider))
        {
            return new WidgetData
            {
                WidgetId = widget.Id,
                Error = $"No data provider for template: {widget.TemplateId}",
                GeneratedAt = DateTime.UtcNow
            };
        }

        return await provider.GetDataAsync(widget, context, ct);
    }

    private void RegisterBuiltInTemplates()
    {
        RegisterTemplate(new WidgetTemplate
        {
            Id = "portfolio-summary",
            Name = "Portfolio Summary",
            Description = "Shows portfolio value, P&L, and allocation",
            Category = WidgetCategory.Portfolio,
            DefaultTitle = "Portfolio Summary",
            MinWidth = 2,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["showAllocation"] = true,
                ["showDailyChange"] = true,
                ["showTotalValue"] = true
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "positions-table",
            Name = "Positions Table",
            Description = "Table of current positions with P&L",
            Category = WidgetCategory.Portfolio,
            DefaultTitle = "Positions",
            MinWidth = 3,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["columns"] = new[] { "symbol", "quantity", "avgCost", "currentPrice", "pnl", "pnlPercent" },
                ["sortBy"] = "pnlPercent",
                ["sortOrder"] = "desc"
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "price-chart",
            Name = "Price Chart",
            Description = "Candlestick or line chart for a symbol",
            Category = WidgetCategory.Charts,
            DefaultTitle = "Price Chart",
            MinWidth = 3,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["chartType"] = "candlestick",
                ["timeframe"] = "1D",
                ["showVolume"] = true,
                ["indicators"] = new[] { "SMA20", "SMA50" }
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "performance-chart",
            Name = "Performance Chart",
            Description = "Portfolio performance over time",
            Category = WidgetCategory.Performance,
            DefaultTitle = "Performance",
            MinWidth = 3,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["period"] = "1Y",
                ["benchmark"] = "SPY",
                ["showDrawdown"] = true
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "risk-metrics",
            Name = "Risk Metrics",
            Description = "VaR, Sharpe ratio, and other risk metrics",
            Category = WidgetCategory.Risk,
            DefaultTitle = "Risk Metrics",
            MinWidth = 2,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["metrics"] = new[] { "var95", "var99", "sharpe", "sortino", "maxDrawdown" },
                ["period"] = "1Y"
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "recent-trades",
            Name = "Recent Trades",
            Description = "List of recent trades",
            Category = WidgetCategory.Trading,
            DefaultTitle = "Recent Trades",
            MinWidth = 2,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["limit"] = 10,
                ["showPnl"] = true
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "alerts",
            Name = "Alerts",
            Description = "Active alerts and notifications",
            Category = WidgetCategory.Alerts,
            DefaultTitle = "Alerts",
            MinWidth = 2,
            MinHeight = 1,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["showTriggered"] = true,
                ["showPending"] = true
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "sector-allocation",
            Name = "Sector Allocation",
            Description = "Portfolio allocation by sector",
            Category = WidgetCategory.Portfolio,
            DefaultTitle = "Sector Allocation",
            MinWidth = 2,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["chartType"] = "pie",
                ["showPercentages"] = true
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "market-overview",
            Name = "Market Overview",
            Description = "Major indices and market status",
            Category = WidgetCategory.Market,
            DefaultTitle = "Market Overview",
            MinWidth = 2,
            MinHeight = 1,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["indices"] = new[] { "SPY", "QQQ", "DIA", "IWM", "VIX" }
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "watchlist",
            Name = "Watchlist",
            Description = "Custom watchlist with prices",
            Category = WidgetCategory.Market,
            DefaultTitle = "Watchlist",
            MinWidth = 2,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["symbols"] = Array.Empty<string>(),
                ["showChange"] = true,
                ["showVolume"] = true
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "strategy-performance",
            Name = "Strategy Performance",
            Description = "Performance metrics for trading strategies",
            Category = WidgetCategory.Performance,
            DefaultTitle = "Strategy Performance",
            MinWidth = 3,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["showEquityCurve"] = true,
                ["showMetrics"] = true,
                ["period"] = "1Y"
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "orders",
            Name = "Orders",
            Description = "Open and recent orders",
            Category = WidgetCategory.Trading,
            DefaultTitle = "Orders",
            MinWidth = 3,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["showOpen"] = true,
                ["showFilled"] = true,
                ["showCancelled"] = false,
                ["limit"] = 20
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "news-feed",
            Name = "News Feed",
            Description = "Relevant news for portfolio holdings",
            Category = WidgetCategory.News,
            DefaultTitle = "News",
            MinWidth = 2,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["sources"] = new[] { "reuters", "bloomberg", "wsj" },
                ["filterByHoldings"] = true,
                ["limit"] = 10
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "correlation-matrix",
            Name = "Correlation Matrix",
            Description = "Correlation between portfolio holdings",
            Category = WidgetCategory.Risk,
            DefaultTitle = "Correlations",
            MinWidth = 3,
            MinHeight = 3,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["period"] = "3M",
                ["heatmapColors"] = true
            }
        });

        RegisterTemplate(new WidgetTemplate
        {
            Id = "pnl-calendar",
            Name = "P&L Calendar",
            Description = "Daily P&L shown as calendar",
            Category = WidgetCategory.Performance,
            DefaultTitle = "P&L Calendar",
            MinWidth = 3,
            MinHeight = 2,
            DefaultConfiguration = new Dictionary<string, object>
            {
                ["period"] = "3M",
                ["colorScale"] = "redGreen"
            }
        });
    }
}

/// <summary>
/// Dashboard builder options.
/// </summary>
public sealed class DashboardBuilderOptions
{
    /// <summary>Maximum widgets per dashboard.</summary>
    public int MaxWidgetsPerDashboard { get; set; } = 20;

    /// <summary>Maximum dashboards per user.</summary>
    public int MaxDashboardsPerUser { get; set; } = 10;

    /// <summary>Default refresh interval.</summary>
    public TimeSpan DefaultRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Dashboard.
/// </summary>
public sealed class Dashboard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DashboardLayout Layout { get; set; }
    public List<DashboardWidget> Widgets { get; set; } = [];
    public Dictionary<string, object> Settings { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDefault { get; set; }
    public bool IsShared { get; set; }
}

/// <summary>
/// Dashboard layout type.
/// </summary>
public enum DashboardLayout
{
    Grid,
    Freeform,
    Vertical,
    Horizontal
}

/// <summary>
/// Dashboard widget.
/// </summary>
public sealed class DashboardWidget
{
    public string Id { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public WidgetPosition Position { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Widget position.
/// </summary>
public sealed class WidgetPosition
{
    public int Row { get; set; }
    public int Column { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
}

/// <summary>
/// Widget template.
/// </summary>
public sealed class WidgetTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WidgetCategory Category { get; set; }
    public string DefaultTitle { get; set; } = string.Empty;
    public int MinWidth { get; set; } = 1;
    public int MinHeight { get; set; } = 1;
    public int MaxWidth { get; set; } = 6;
    public int MaxHeight { get; set; } = 6;
    public Dictionary<string, object> DefaultConfiguration { get; set; } = [];
}

/// <summary>
/// Widget category.
/// </summary>
public enum WidgetCategory
{
    Portfolio,
    Charts,
    Performance,
    Risk,
    Trading,
    Alerts,
    Market,
    News
}

/// <summary>
/// Dashboard data context.
/// </summary>
public sealed class DashboardDataContext
{
    public string UserId { get; set; } = string.Empty;
    public string? AccountId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public string? TimeZone { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = [];
}

/// <summary>
/// Dashboard data.
/// </summary>
public sealed class DashboardData
{
    public string DashboardId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public Dictionary<string, WidgetData> WidgetData { get; set; } = [];
}

/// <summary>
/// Widget data.
/// </summary>
public sealed class WidgetData
{
    public string WidgetId { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string? Error { get; set; }
    public TimeSpan? CacheDuration { get; set; }
}

/// <summary>
/// Widget data provider interface.
/// </summary>
public interface IWidgetDataProvider
{
    Task<WidgetData> GetDataAsync(
        DashboardWidget widget,
        DashboardDataContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Portfolio summary data provider.
/// </summary>
public sealed class PortfolioSummaryDataProvider : IWidgetDataProvider
{
    private readonly Func<string, CancellationToken, Task<PortfolioSummaryData>> _getSummary;

    public PortfolioSummaryDataProvider(Func<string, CancellationToken, Task<PortfolioSummaryData>> getSummary)
    {
        _getSummary = getSummary;
    }

    public async Task<WidgetData> GetDataAsync(
        DashboardWidget widget,
        DashboardDataContext context,
        CancellationToken ct = default)
    {
        var summary = await _getSummary(context.UserId, ct);

        return new WidgetData
        {
            WidgetId = widget.Id,
            Data = summary,
            GeneratedAt = DateTime.UtcNow,
            CacheDuration = TimeSpan.FromSeconds(30)
        };
    }
}

/// <summary>
/// Portfolio summary data.
/// </summary>
public sealed class PortfolioSummaryData
{
    public decimal TotalValue { get; set; }
    public decimal DayChange { get; set; }
    public decimal DayChangePercent { get; set; }
    public decimal TotalPnl { get; set; }
    public decimal TotalPnlPercent { get; set; }
    public decimal Cash { get; set; }
    public int PositionCount { get; set; }
    public Dictionary<string, decimal> Allocation { get; set; } = [];
}
