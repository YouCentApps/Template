using Microsoft.JSInterop;
using System.Text.Json;

namespace Template.Common.Services.State;

public interface IStorageService
{
    Task<T?> GetItemAsync<T>(string key);
    Task SetItemAsync<T>(string key, T value);
    Task RemoveItemAsync(string key);
}

public class BrowserStorageService(IJSRuntime jsRuntime) : IStorageService
{
    private readonly IJSRuntime _jsRuntime = jsRuntime;

    public async Task<T?> GetItemAsync<T>(string key)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (ArgumentNullException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            // Silently fail
        }
        catch (ArgumentNullException)
        {
            // Silently fail
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key).ConfigureAwait(false);
        }
        catch (ArgumentNullException)
        {
            // Silently fail
        }
    }
}

public class SessionData
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
}
