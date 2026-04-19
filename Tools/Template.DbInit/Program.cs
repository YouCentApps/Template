using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Template.DbInit;

sealed class Program
{
    private static IConfiguration? _configuration;
    private static string? _storageUri;
    private static string? _accountName;
    private static string? _accountKey;

    // Table names matching Template.Common.Helpers.TableNames
    private static readonly string[] TableNames =
    [
        "TemplateUsers",
        "TemplateAuthSessions",
        "TemplateOTPCodes",
        "TemplateAdmins",
        "TemplatePendingReg"
    ];

    static async Task Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("Template Database Initialization Tool");
        Console.WriteLine("===========================================\n");

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<Program>(optional: true)
            .Build();

        _storageUri = _configuration["AzureStorage:StorageUri"];
        _accountName = _configuration["AzureStorage:AccountName"];
        _accountKey = _configuration["AzureStorage:AccountKey"];

        if (string.IsNullOrWhiteSpace(_storageUri) || string.IsNullOrWhiteSpace(_accountName) || string.IsNullOrWhiteSpace(_accountKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Azure Storage connection information is missing!");
            Console.WriteLine("Please configure AzureStorage settings in appsettings.json or user secrets.");
            Console.ResetColor();
            return;
        }

        var createTables = bool.TryParse(_configuration["Options:CreateTables"], out var ct) ? ct : true;
        var addSampleData = bool.TryParse(_configuration["Options:AddSampleData"], out var asd) ? asd : true;
        var dropExisting = bool.TryParse(_configuration["Options:DropExistingTables"], out var de) ? de : false;

        Console.WriteLine($"Storage Account: {_accountName}");
        Console.WriteLine($"Create Tables: {createTables}");
        Console.WriteLine($"Add Sample Data: {addSampleData}");
        Console.WriteLine($"Drop Existing: {dropExisting}\n");

        if (dropExisting)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("WARNING: This will delete ALL existing Template tables. Are you sure? (yes/no): ");
            Console.ResetColor();
            var confirm = Console.ReadLine();
            if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Operation cancelled.");
                return;
            }
        }

        try
        {
            if (dropExisting)
            {
                await DropTablesAsync().ConfigureAwait(false);
            }

            if (createTables)
            {
                await CreateTablesAsync().ConfigureAwait(false);
            }

            if (addSampleData)
            {
                await AddSampleDataAsync().ConfigureAwait(false);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n===========================================");
            Console.WriteLine("Database initialization completed successfully!");
            Console.WriteLine("===========================================");
            Console.ResetColor();
        }
        #pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine($"Details: {ex}");
            Console.ResetColor();
        }
    }

    static async Task DropTablesAsync()
    {
        Console.WriteLine("\n--- Dropping Existing Tables ---");

        foreach (var tableName in TableNames)
        {
            try
            {
                var client = CreateTableClient(tableName);
                await client.DeleteAsync().ConfigureAwait(false);
                Console.WriteLine($"  Dropped: {tableName}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"  {tableName} does not exist (skipped)");
            }
        }

        // Wait for Azure to finish deleting
        Console.WriteLine("  Waiting for table deletion to complete...");
        await Task.Delay(5000).ConfigureAwait(false);
    }

    static async Task CreateTablesAsync()
    {
        Console.WriteLine("\n--- Creating Tables ---");

        foreach (var tableName in TableNames)
        {
            var client = CreateTableClient(tableName);
            await client.CreateIfNotExistsAsync().ConfigureAwait(false);
            Console.WriteLine($"  Created: {tableName}");
        }
    }

    static async Task AddSampleDataAsync()
    {
        Console.WriteLine("\n--- Adding Sample Data ---");

        // Create admin user
        var adminEmail = _configuration!["AdminUser:Email"] ?? "admin@example.com";
        var adminUsername = _configuration["AdminUser:Username"] ?? "Admin";
        var adminPassword = _configuration["AdminUser:Password"] ?? "Admin123!";

        var adminUserId = await CreateUserAsync(adminEmail, adminUsername, adminPassword, "Both").ConfigureAwait(false);
        Console.WriteLine($"  Created admin user: {adminUsername} ({adminEmail})");

        // Grant admin privileges
        var adminClient = CreateTableClient("TemplateAdmins");
        var adminEntity = new TableEntity("Admin", adminUserId)
        {
            ["UserId"] = adminUserId,
            ["IsActive"] = true,
            ["CanManageAdmins"] = true,
            ["CreatedDate"] = DateTime.UtcNow,
            ["CreatedBy"] = "DbInit"
        };
        await adminClient.UpsertEntityAsync(adminEntity).ConfigureAwait(false);
        Console.WriteLine($"  Granted super admin privileges to: {adminUsername}");

        // Create sample users
        var sampleUsers = new[]
        {
            ("user1@example.com", "TestUser1", "Test123!", "Both"),
            ("user2@example.com", "TestUser2", "Test123!", "Password"),
            ("user3@example.com", "TestUser3", "Test123!", "Email")
        };

        foreach (var (email, username, password, authMethod) in sampleUsers)
        {
            await CreateUserAsync(email, username, password, authMethod).ConfigureAwait(false);
            Console.WriteLine($"  Created user: {username} ({email}) - Auth: {authMethod}");
        }

        // Summary
        Console.WriteLine("\n--- Summary ---");
        Console.WriteLine($"Tables created: {TableNames.Length}");
        Console.WriteLine($"Admin user: {adminEmail} / {adminPassword}");
        Console.WriteLine($"Sample users: {sampleUsers.Length}");
        Console.WriteLine("\nTest Login Credentials:");
        Console.WriteLine($"  Admin:  {adminEmail} | {adminUsername} | {adminPassword}");
        foreach (var (email, username, password, authMethod) in sampleUsers)
        {
            Console.WriteLine($"  User:   {email} | {username} | {password} | Auth: {authMethod}");
        }
    }

    static async Task<string> CreateUserAsync(string email, string username, string password, string preferredAuthMethod)
    {
        var userId = Guid.NewGuid().ToString("N");
        var (hash, salt) = HashPassword(password);
        var partitionKey = userId[..6];
        var rowKey = userId[6..];

        var userClient = CreateTableClient("TemplateUsers");
        var userEntity = new TableEntity(partitionKey, rowKey)
        {
            ["UserId"] = userId,
            ["Email"] = email.ToLowerInvariant(),
            ["Username"] = username,
            ["NormalizedUsername"] = username.ToLowerInvariant(),
            ["PasswordHash"] = hash,
            ["PasswordSalt"] = salt,
            ["PreferredAuthMethod"] = preferredAuthMethod,
            ["IsActive"] = true,
            ["CreatedDate"] = DateTime.UtcNow
        };

        await userClient.UpsertEntityAsync(userEntity).ConfigureAwait(false);
        return userId;
    }

    static TableClient CreateTableClient(string tableName)
    {
        return new TableClient(
            new Uri(_storageUri!),
            tableName,
            new TableSharedKeyCredential(_accountName!, _accountKey!));
    }

    static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        var salt = Convert.ToBase64String(saltBytes);

        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 10000, HashAlgorithmName.SHA256, 32);
        var hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }
}
