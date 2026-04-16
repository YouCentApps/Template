using Azure.Data.Tables;

namespace Template.Shared.Services.Data;

/// <summary>
/// Factory interface for creating Azure Table Storage clients
/// </summary>
public interface ITableClientFactory
{
    TableClient GetTableClient(string tableName);
}

/// <summary>
/// Factory implementation for creating Azure Table Storage clients
/// </summary>
public class TableClientFactory : ITableClientFactory
{
    private readonly string _storageUri;
    private readonly string _accountName;
    private readonly string _accountKey;

    public TableClientFactory(string storageUri, string accountName, string accountKey)
    {
        _storageUri = storageUri ?? throw new ArgumentNullException(nameof(storageUri));
        _accountName = accountName ?? throw new ArgumentNullException(nameof(accountName));
        _accountKey = accountKey ?? throw new ArgumentNullException(nameof(accountKey));
    }

    public TableClient GetTableClient(string tableName)
    {
        return new TableClient(
            new Uri(_storageUri),
            tableName,
            new TableSharedKeyCredential(_accountName, _accountKey));
    }
}
