using Azure.Data.Tables;

namespace Template.Common.Services.Data;

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
public class TableClientFactory(string storageUri, string accountName, string accountKey) : ITableClientFactory
{
    private readonly string _storageUri = storageUri ?? throw new ArgumentNullException(nameof(storageUri));
    private readonly string _accountName = accountName ?? throw new ArgumentNullException(nameof(accountName));
    private readonly string _accountKey = accountKey ?? throw new ArgumentNullException(nameof(accountKey));

    public TableClient GetTableClient(string tableName)
    {
        return new TableClient(
            new Uri(_storageUri),
            tableName,
            new TableSharedKeyCredential(_accountName, _accountKey));
    }
}
