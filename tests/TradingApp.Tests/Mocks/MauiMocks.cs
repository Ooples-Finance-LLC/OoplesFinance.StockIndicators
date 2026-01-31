// Mock implementations of MAUI interfaces for testing without MAUI runtime.
// These allow unit tests to run without requiring platform-specific MAUI components.

namespace Microsoft.Maui.Networking;

/// <summary>
/// Mock IConnectivity interface for testing network-dependent code.
/// </summary>
public interface IConnectivity
{
    NetworkAccess NetworkAccess { get; }
    IEnumerable<ConnectionProfile> ConnectionProfiles { get; }
    event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;
}

/// <summary>
/// Network access levels.
/// </summary>
public enum NetworkAccess
{
    Unknown = 0,
    None = 1,
    Local = 2,
    ConstrainedInternet = 3,
    Internet = 4
}

/// <summary>
/// Connection profile types.
/// </summary>
public enum ConnectionProfile
{
    Unknown = 0,
    Bluetooth = 1,
    Cellular = 2,
    Ethernet = 3,
    WiFi = 4
}

/// <summary>
/// Event args for connectivity changes.
/// </summary>
public class ConnectivityChangedEventArgs : EventArgs
{
    public NetworkAccess NetworkAccess { get; }
    public IEnumerable<ConnectionProfile> ConnectionProfiles { get; }

    public ConnectivityChangedEventArgs(NetworkAccess networkAccess, IEnumerable<ConnectionProfile> connectionProfiles)
    {
        NetworkAccess = networkAccess;
        ConnectionProfiles = connectionProfiles;
    }
}

namespace Microsoft.Maui.Graphics;

/// <summary>
/// Mock Color class for testing UI-related code.
/// </summary>
public readonly struct Color : IEquatable<Color>
{
    public float Red { get; }
    public float Green { get; }
    public float Blue { get; }
    public float Alpha { get; }

    public Color(float red, float green, float blue, float alpha = 1.0f)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    public static Color FromArgb(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return new Color(0, 0, 0, 1);

        hex = hex.TrimStart('#');

        if (hex.Length == 6)
        {
            var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            return new Color(r, g, b);
        }
        else if (hex.Length == 8)
        {
            var a = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            var r = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            var g = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            var b = Convert.ToInt32(hex.Substring(6, 2), 16) / 255f;
            return new Color(r, g, b, a);
        }

        return new Color(0, 0, 0, 1);
    }

    public bool Equals(Color other)
    {
        return Red.Equals(other.Red) && Green.Equals(other.Green) &&
               Blue.Equals(other.Blue) && Alpha.Equals(other.Alpha);
    }

    public override bool Equals(object? obj)
    {
        return obj is Color other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Red, Green, Blue, Alpha);
    }

    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);
}

namespace Microsoft.Maui.Controls;

/// <summary>
/// Mock Application class for testing.
/// </summary>
public class Application
{
    private static Application? _current;

    public static Application? Current
    {
        get => _current;
        set => _current = value;
    }

    public Page? MainPage { get; set; }
}

/// <summary>
/// Mock Page class for testing.
/// </summary>
public class Page
{
    public virtual Task DisplayAlert(string title, string message, string cancel)
    {
        return Task.CompletedTask;
    }

    public virtual Task<bool> DisplayAlert(string title, string message, string accept, string cancel)
    {
        return Task.FromResult(true);
    }
}
