using Azure;
using Azure.Data.Tables;

namespace Template.Shared.Services.Data;

/// <summary>
/// Repository interface for User operations
/// </summary>
public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByIdAsync(string userId);
    Task<bool> CreateUserAsync(User user);
    Task<bool> UpdateUserAsync(User user);
    Task<bool> UpdateLastLoginAsync(string userId);
    Task<bool> DeleteUserAsync(string userId);
    Task<List<User>> GetAllUsersAsync();
}

/// <summary>
/// Repository implementation for User operations with Azure Table Storage
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ITableClientFactory _tableClientFactory;

    public UserRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Users);

        try
        {
            var normalizedEmail = email.ToLowerInvariant();
            var safeEmail = normalizedEmail.Replace("'", "''", StringComparison.Ordinal);
            var filter = $"Email eq '{safeEmail}'";
            
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter).ConfigureAwait(false))
            {
                return MapToUser(entity);
            }
        }
        catch (RequestFailedException)
        {
            return null;
        }

        return null;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Users);

        try
        {
            var normalizedUsername = username.ToLowerInvariant();
            var safeUsername = normalizedUsername.Replace("'", "''", StringComparison.Ordinal);
            var filter = $"NormalizedUsername eq '{safeUsername}'";
            
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter).ConfigureAwait(false))
            {
                return MapToUser(entity);
            }
        }
        catch (RequestFailedException)
        {
            return null;
        }

        return null;
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Users);

        try
        {
            // Use first 6 chars as partition key, rest as row key
            var partitionKey = userId.Length >= 6 ? userId.Substring(0, 6) : userId;
            var rowKey = userId.Length > 6 ? userId.Substring(6) : string.Empty;
            
            var response = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey).ConfigureAwait(false);
            return MapToUser(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> CreateUserAsync(User user)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Users);

        try
        {
            var partitionKey = user.UserId.Substring(0, 6);
            var rowKey = user.UserId.Substring(6);

            var entity = new TableEntity(partitionKey, rowKey)
            {
                ["UserId"] = user.UserId,
                ["Email"] = user.Email.ToLowerInvariant(),
                ["Username"] = user.Username,
                ["NormalizedUsername"] = user.NormalizedUsername.ToLowerInvariant(),
                ["PasswordHash"] = user.PasswordHash,
                ["PasswordSalt"] = user.PasswordSalt,
                ["PreferredAuthMethod"] = user.PreferredAuthMethod,
                ["IsActive"] = user.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(user.CreatedDate, DateTimeKind.Utc),
                ["LastLoginDate"] = user.LastLoginDate.HasValue 
                    ? DateTime.SpecifyKind(user.LastLoginDate.Value, DateTimeKind.Utc) 
                    : (DateTime?)null
            };

            await tableClient.AddEntityAsync(entity).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Users);

        try
        {
            var partitionKey = user.UserId.Substring(0, 6);
            var rowKey = user.UserId.Substring(6);

            var entity = new TableEntity(partitionKey, rowKey)
            {
                ["UserId"] = user.UserId,
                ["Email"] = user.Email.ToLowerInvariant(),
                ["Username"] = user.Username,
                ["NormalizedUsername"] = user.NormalizedUsername.ToLowerInvariant(),
                ["PasswordHash"] = user.PasswordHash,
                ["PasswordSalt"] = user.PasswordSalt,
                ["PreferredAuthMethod"] = user.PreferredAuthMethod,
                ["IsActive"] = user.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(user.CreatedDate, DateTimeKind.Utc),
                ["LastLoginDate"] = user.LastLoginDate.HasValue 
                    ? DateTime.SpecifyKind(user.LastLoginDate.Value, DateTimeKind.Utc) 
                    : (DateTime?)null
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> UpdateLastLoginAsync(string userId)
    {
        var user = await GetUserByIdAsync(userId).ConfigureAwait(false);
        if (user == null) return false;

        user.LastLoginDate = DateTime.UtcNow;
        return await UpdateUserAsync(user).ConfigureAwait(false);
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Users);

        try
        {
            var partitionKey = userId.Substring(0, 6);
            var rowKey = userId.Substring(6);
            
            await tableClient.DeleteEntityAsync(partitionKey, rowKey).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Users);
        var users = new List<User>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableEntity>().ConfigureAwait(false))
            {
                users.Add(MapToUser(entity));
            }
        }
        catch (RequestFailedException)
        {
            return users;
        }

        return users.OrderBy(u => u.CreatedDate).ToList();
    }

    private static User MapToUser(TableEntity entity)
    {
        return new User
        {
            UserId = entity.GetString("UserId") ?? string.Empty,
            Email = entity.GetString("Email") ?? string.Empty,
            Username = entity.GetString("Username") ?? string.Empty,
            NormalizedUsername = entity.GetString("NormalizedUsername") ?? string.Empty,
            PasswordHash = entity.GetString("PasswordHash") ?? string.Empty,
            PasswordSalt = entity.GetString("PasswordSalt") ?? string.Empty,
            PreferredAuthMethod = entity.GetString("PreferredAuthMethod") ?? "Both",
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.UtcDateTime ?? DateTime.UtcNow,
            LastLoginDate = entity.GetDateTimeOffset("LastLoginDate")?.UtcDateTime
        };
    }
}
