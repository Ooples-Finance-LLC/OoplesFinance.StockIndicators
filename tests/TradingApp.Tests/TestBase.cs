using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using System.Diagnostics;

namespace OoplesFinance.TradingApp.UITests;

/// <summary>
/// Test configuration for MAUI UI tests
/// </summary>
public static class TestConfig
{
    /// <summary>
    /// Path to the MAUI app executable
    /// </summary>
    public static string AppPath => Environment.GetEnvironmentVariable("MAUI_APP_PATH")
        ?? Path.Combine(GetSolutionDirectory(), "src", "UI", "OoplesFinance.TradingApp.Maui", "bin", "Debug", "net10.0-windows10.0.19041.0", "win-x64", "OoplesFinance.TradingApp.Maui.exe");

    /// <summary>
    /// Timeout for UI operations
    /// </summary>
    public static TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Short timeout for quick operations
    /// </summary>
    public static TimeSpan ShortTimeout => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the solution directory
    /// </summary>
    public static string GetSolutionDirectory()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "OoplesFinance.StockIndicators.sln")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? Directory.GetCurrentDirectory();
    }
}

/// <summary>
/// Base class for all MAUI UI tests
/// </summary>
public abstract class UITestBase : IDisposable
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }
    protected ConditionFactory CF => Automation!.ConditionFactory;

    private bool _disposed;

    /// <summary>
    /// Launches the application and waits for the main window
    /// </summary>
    /// <param name="clearData">If true, clears app data before launch (for onboarding tests)</param>
    protected virtual async Task LaunchAppAsync(bool clearData = false)
    {
        Automation = new UIA3Automation();

        if (clearData)
        {
            ClearAppData();
        }

        // Check if app is already running
        var existingProcess = Process.GetProcessesByName("OoplesFinance.TradingApp.Maui").FirstOrDefault();
        if (existingProcess != null)
        {
            if (clearData)
            {
                // Kill existing process if we need fresh data
                existingProcess.Kill();
                await Task.Delay(1000);
            }
            else
            {
                App = Application.Attach(existingProcess);
            }
        }

        if (App == null)
        {
            // Build the app first if needed
            if (!File.Exists(TestConfig.AppPath))
            {
                await BuildAppAsync();
            }

            App = Application.Launch(TestConfig.AppPath);
        }

        // Wait for main window
        MainWindow = await WaitForMainWindowAsync();
    }

    /// <summary>
    /// Clears app data for fresh onboarding tests
    /// </summary>
    protected virtual void ClearAppData()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDataPath = Path.Combine(localAppData, "OoplesFinance.TradingApp.Maui");

            if (Directory.Exists(appDataPath))
            {
                Directory.Delete(appDataPath, recursive: true);
            }
        }
        catch
        {
            // Ignore errors when clearing data
        }
    }

    /// <summary>
    /// Builds the MAUI app
    /// </summary>
    protected virtual async Task BuildAppAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build -c Debug -f net10.0-windows10.0.19041.0 -r win-x64",
            WorkingDirectory = Path.Combine(TestConfig.GetSolutionDirectory(), "src", "UI", "OoplesFinance.TradingApp.Maui"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }

    /// <summary>
    /// Waits for the main window to appear
    /// </summary>
    protected virtual async Task<Window> WaitForMainWindowAsync()
    {
        var timeout = DateTime.Now.Add(TestConfig.DefaultTimeout);

        while (DateTime.Now < timeout)
        {
            var windows = App!.GetAllTopLevelWindows(Automation!);
            var mainWindow = windows.FirstOrDefault(w =>
                w.Title?.Contains("Ooples") == true ||
                w.Title?.Contains("Trading") == true);

            if (mainWindow != null)
            {
                return mainWindow;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Main window did not appear within timeout");
    }

    /// <summary>
    /// Finds an element by automation ID
    /// </summary>
    protected AutomationElement? FindByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        timeout ??= TestConfig.DefaultTimeout;
        var endTime = DateTime.Now.Add(timeout.Value);

        while (DateTime.Now < endTime)
        {
            var element = MainWindow?.FindFirstDescendant(CF.ByAutomationId(automationId));
            if (element != null)
            {
                return element;
            }
            Thread.Sleep(100);
        }

        return null;
    }

    /// <summary>
    /// Finds an element by name/text
    /// </summary>
    protected AutomationElement? FindByName(string name, TimeSpan? timeout = null)
    {
        timeout ??= TestConfig.DefaultTimeout;
        var endTime = DateTime.Now.Add(timeout.Value);

        while (DateTime.Now < endTime)
        {
            var element = MainWindow?.FindFirstDescendant(CF.ByName(name));
            if (element != null)
            {
                return element;
            }
            Thread.Sleep(100);
        }

        return null;
    }

    /// <summary>
    /// Finds an element containing text
    /// </summary>
    protected AutomationElement? FindByText(string text, TimeSpan? timeout = null)
    {
        timeout ??= TestConfig.DefaultTimeout;
        var endTime = DateTime.Now.Add(timeout.Value);

        while (DateTime.Now < endTime)
        {
            try
            {
                var elements = MainWindow?.FindAllDescendants();
                if (elements != null)
                {
                    foreach (var e in elements)
                    {
                        try
                        {
                            var name = e.Name;
                            if (name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                return e;
                            }
                        }
                        catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
                        {
                            // Skip elements that don't support the Name property
                            continue;
                        }
                        catch
                        {
                            // Skip any other problematic elements
                            continue;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors during search
            }
            Thread.Sleep(100);
        }

        return null;
    }

    /// <summary>
    /// Finds a button by text
    /// </summary>
    protected Button? FindButton(string text)
    {
        var element = FindByName(text) ?? FindByText(text);
        return element?.AsButton();
    }

    /// <summary>
    /// Clicks a button with the given text
    /// </summary>
    protected void ClickButton(string text)
    {
        var button = FindButton(text);
        button?.Click();
    }

    /// <summary>
    /// Waits for an element to be visible
    /// </summary>
    protected async Task<bool> WaitForElementAsync(string text, TimeSpan? timeout = null)
    {
        timeout ??= TestConfig.DefaultTimeout;
        var endTime = DateTime.Now.Add(timeout.Value);

        while (DateTime.Now < endTime)
        {
            var element = FindByText(text, TimeSpan.FromMilliseconds(100));
            if (element != null && element.IsOffscreen == false)
            {
                return true;
            }
            await Task.Delay(100);
        }

        return false;
    }

    /// <summary>
    /// Takes a screenshot of the current window
    /// </summary>
    protected void TakeScreenshot(string name)
    {
        try
        {
            var screenshotDir = Path.Combine(TestConfig.GetSolutionDirectory(), "tests", "screenshots");
            Directory.CreateDirectory(screenshotDir);

            var screenshot = MainWindow?.Capture();
            var filePath = Path.Combine(screenshotDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            screenshot?.Save(filePath);
        }
        catch
        {
            // Ignore screenshot errors
        }
    }

    /// <summary>
    /// Navigates to a tab by clicking its name
    /// </summary>
    protected void NavigateToTab(string tabName)
    {
        var tab = FindByText(tabName, TestConfig.ShortTimeout);
        tab?.Click();
        Thread.Sleep(500); // Wait for navigation
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Don't close the app if we attached to an existing process
                if (App?.HasExited == false)
                {
                    // Leave app running for development
                    // App.Close();
                }
                Automation?.Dispose();
            }
            _disposed = true;
        }
    }
}
