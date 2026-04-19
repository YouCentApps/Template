using Azure;
using Azure.Data.Tables;

namespace Template.Common.Services.Data;

/// <summary>
/// Repository interface for Admin operations
/// </summary>
public interface IAdminRepository
{
    Task<Admin?> GetAdminAsync(string userId);
    Task<bool> IsAdminAsync(string userId);
    Task<bool> IsActiveAdminAsync(string userId);
    Task<bool> CanManageAdminsAsync(string userId);
    Task<List<Admin>> GetAllAdminsAsync();
    Task<bool> CreateAdminAsync(Admin admin);
    Task<bool> UpdateAdminAsync(Admin admin);
    Task<bool> DeleteAdminAsync(string userId);
}

/// <summary>
/// Repository implementation for Admin operations with Azure Table Storage
/// </summary>
public class AdminRepository(ITableClientFactory tableClientFactory) : IAdminRepository
{
    private readonly ITableClientFactory _tableClientFactory = tableClientFactory;

    public async Task<Admin?> GetAdminAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Admins);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>("Admin", userId).ConfigureAwait(false);
            return MapToAdmin(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> IsAdminAsync(string userId)
    {
        var admin = await GetAdminAsync(userId).ConfigureAwait(false);
        return admin != null;
    }

    public async Task<bool> IsActiveAdminAsync(string userId)
    {
        var admin = await GetAdminAsync(userId).ConfigureAwait(false);
        return admin != null && admin.IsActive;
    }

    public async Task<bool> CanManageAdminsAsync(string userId)
    {
        var admin = await GetAdminAsync(userId).ConfigureAwait(false);
        return admin != null && admin.IsActive && admin.CanManageAdmins;
    }

    public async Task<List<Admin>> GetAllAdminsAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Admins);
        var admins = new List<Admin>();

        try
        {
            var filter = "PartitionKey eq 'Admin'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter).ConfigureAwait(false))
            {
                admins.Add(MapToAdmin(entity));
            }
        }
        catch (RequestFailedException)
        {
            return admins;
        }

        return [.. admins.OrderBy(a => a.CreatedDate)];
    }

    public async Task<bool> CreateAdminAsync(Admin admin)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Admins);

        if (admin is null)
        {
            return false;
        }

        try
        {
            var entity = new TableEntity("Admin", admin.UserId)
            {
                ["UserId"] = admin.UserId,
                ["IsActive"] = admin.IsActive,
                ["CanManageAdmins"] = admin.CanManageAdmins,
                ["CreatedDate"] = DateTime.SpecifyKind(admin.CreatedDate, DateTimeKind.Utc),
                ["CreatedBy"] = admin.CreatedBy,
                ["LastModifiedDate"] = admin.LastModifiedDate.HasValue 
                    ? DateTime.SpecifyKind(admin.LastModifiedDate.Value, DateTimeKind.Utc) 
                    : (DateTime?)null,
                ["LastModifiedBy"] = admin.LastModifiedBy
            };

            await tableClient.AddEntityAsync(entity).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> UpdateAdminAsync(Admin admin)
    {
        if (admin is null)
        {  
            return false; 
        }

        var tableClient = _tableClientFactory.GetTableClient(TableNames.Admins);

        try
        {
            var entity = new TableEntity("Admin", admin.UserId)
            {
                ["UserId"] = admin.UserId,
                ["IsActive"] = admin.IsActive,
                ["CanManageAdmins"] = admin.CanManageAdmins,
                ["CreatedDate"] = DateTime.SpecifyKind(admin.CreatedDate, DateTimeKind.Utc),
                ["CreatedBy"] = admin.CreatedBy,
                ["LastModifiedDate"] = admin.LastModifiedDate.HasValue 
                    ? DateTime.SpecifyKind(admin.LastModifiedDate.Value, DateTimeKind.Utc) 
                    : (DateTime?)null,
                ["LastModifiedBy"] = admin.LastModifiedBy
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAdminAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableNames.Admins);

        try
        {
            await tableClient.DeleteEntityAsync("Admin", userId).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    private static Admin MapToAdmin(TableEntity entity)
    {
        return new Admin
        {
            UserId = entity.RowKey,
            IsActive = GetBooleanValue(entity, "IsActive"),
            CanManageAdmins = GetBooleanValue(entity, "CanManageAdmins"),
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.UtcDateTime ?? DateTime.UtcNow,
            CreatedBy = entity.GetString("CreatedBy") ?? string.Empty,
            LastModifiedDate = entity.GetDateTimeOffset("LastModifiedDate")?.UtcDateTime,
            LastModifiedBy = entity.GetString("LastModifiedBy")
        };
    }

    private static bool GetBooleanValue(TableEntity entity, string key)
    {
        if (entity.TryGetValue(key, out var value))
        {
            if (value is bool boolValue)
                return boolValue;
            if (value is string stringValue)
                return bool.TryParse(stringValue, out var parsed) && parsed;
        }
        return false;
    }
}
