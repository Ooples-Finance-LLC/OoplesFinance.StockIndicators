using Xunit;

namespace OoplesFinance.TradingApp.UITests;

/// <summary>
/// Test collection definition for UI tests.
/// All tests in this collection share the same app instance to speed up test execution.
/// </summary>
[CollectionDefinition("UITests")]
public class UITestCollection : ICollectionFixture<UITestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

/// <summary>
/// Fixture that manages the shared app instance for all UI tests.
/// </summary>
public class UITestFixture : IDisposable
{
    public UITestFixture()
    {
        // Fixture setup - can initialize shared resources here
    }

    public void Dispose()
    {
        // Cleanup shared resources
        GC.SuppressFinalize(this);
    }
}
