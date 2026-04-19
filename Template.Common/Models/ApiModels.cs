namespace Template.Common.Models;

/// <summary>
/// Base response models for API communication
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }
}
