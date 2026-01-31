namespace OoplesFinance.StockIndicators.Builder.Extensions;

/// <summary>
/// Extension methods for Dictionary to provide compatibility across .NET versions.
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Gets the value associated with the specified key, or a default value if the key is not found.
    /// This provides compatibility with .NET Framework 4.6.1 which doesn't have this method built-in.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to search.</param>
    /// <param name="key">The key to locate.</param>
    /// <returns>The value if found; otherwise, the default value for TValue.</returns>
    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        if (dictionary is null)
        {
            return default;
        }

        return dictionary.TryGetValue(key, out var value) ? value : default;
    }

    /// <summary>
    /// Gets the value associated with the specified key, or a specified default value if the key is not found.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to search.</param>
    /// <param name="key">The key to locate.</param>
    /// <param name="defaultValue">The default value to return if the key is not found.</param>
    /// <returns>The value if found; otherwise, the specified default value.</returns>
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        where TKey : notnull
    {
        if (dictionary is null)
        {
            return defaultValue;
        }

        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Gets the value associated with the specified key, or a default value if the key is not found.
    /// This overload works with IReadOnlyDictionary interface.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to search.</param>
    /// <param name="key">The key to locate.</param>
    /// <returns>The value if found; otherwise, the default value for TValue.</returns>
    public static TValue? GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        if (dictionary is null)
        {
            return default;
        }

        return dictionary.TryGetValue(key, out var value) ? value : default;
    }

    /// <summary>
    /// Gets the value associated with the specified key, or a specified default value if the key is not found.
    /// This overload works with IReadOnlyDictionary interface.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to search.</param>
    /// <param name="key">The key to locate.</param>
    /// <param name="defaultValue">The default value to return if the key is not found.</param>
    /// <returns>The value if found; otherwise, the specified default value.</returns>
    public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        where TKey : notnull
    {
        if (dictionary is null)
        {
            return defaultValue;
        }

        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Gets the value associated with the specified key, or a default value if the key is not found.
    /// This overload works with IDictionary interface.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to search.</param>
    /// <param name="key">The key to locate.</param>
    /// <returns>The value if found; otherwise, the default value for TValue.</returns>
    public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        if (dictionary is null)
        {
            return default;
        }

        return dictionary.TryGetValue(key, out var value) ? value : default;
    }
}
