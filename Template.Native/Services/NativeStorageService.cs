using System.Text.Json;

namespace Template.Native.Services;

/// <summary>
/// MAUI-specific storage using Preferences API instead of browser localStorage
/// </summary>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Must be public for XAML binding in MAUI projects")]
public class NativeStorageService : IStorageService
{
    public Task<T?> GetItemAsync<T>(string key)
    {
        try
        {
            var json = Preferences.Default.Get<string?>(key, null);
            if (string.IsNullOrEmpty(json))
                return Task.FromResult(default(T));

            return Task.FromResult(JsonSerializer.Deserialize<T>(json));
        }
#pragma warning disable CA1031 // We catch all exceptions to prevent app crashes due to storage issues
        catch
        {
            return Task.FromResult(default(T));
        }
#pragma warning restore CA1031
    }

    public Task SetItemAsync<T>(string key, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            Preferences.Default.Set(key, json);
        }
#pragma warning disable CA1031 // We catch all exceptions to prevent app crashes due to storage issues
        catch
        {
            // Silently fail
        }
#pragma warning restore CA1031
        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(string key)
    {
        Preferences.Default.Remove(key);
        return Task.CompletedTask;
    }
}
