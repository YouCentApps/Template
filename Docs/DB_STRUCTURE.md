# Database Structure

## Overview

This template uses **Azure Table Storage** as its database. Tables are partitioned for optimal performance.

---

## Table Schemas

### 1. **TemplateUsers** (User Accounts)

**Purpose**: Store user account information with authentication credentials

**Partition Strategy**: First 6 characters of UserId  
**Row Key**: Remaining characters of UserId

| Column | Type | Description |
|--------|------|-------------|
| UserId | string | Unique user identifier (32-char GUID) |
| Email | string | User email (normalized lowercase) |
| Username | string | Display username |
| NormalizedUsername | string | Lowercase username for lookups |
| PasswordHash | string | PBKDF2 password hash (Base64) |
| PasswordSalt | string | Cryptographic salt (Base64) |
| PreferredAuthMethod | string | "Email", "Password", or "Both" |
| IsActive | bool | Account active status |
| CreatedDate | DateTime | Account creation timestamp (UTC) |
| LastLoginDate | DateTime? | Last successful login (UTC) |

**Indexes**:
- Email (for login by email)
- NormalizedUsername (for login by username)

**Example Query**:
```csharp
// Find by email
var filter = $"Email eq '{normalizedEmail}'";
await tableClient.QueryAsync<TableEntity>(filter: filter);
```

---

### 2. **TemplateAuthSessions** (Active Sessions)

**Purpose**: Track authenticated user sessions

**Partition Key**: "Session"  
**Row Key**: SessionId (GUID)

| Column | Type | Description |
|--------|------|-------------|
| SessionId | string | Unique session identifier |
| UserId | string | Associated user ID |
| Email | string | User email (for reference) |
| Username | string | User username (for reference) |
| CreatedDate | DateTime | Session creation time (UTC) |
| ExpiryDate | DateTime | Session expiration time (UTC) |
| IsActive | bool | Session validity flag |

**Lifetime**: 30 days (configurable)

**Example Query**:
```csharp
// Validate session
var session = await tableClient.GetEntityAsync<TableEntity>("Session", sessionId);
```

---

### 3. **TemplateOTPCodes** (One-Time Passwords)

**Purpose**: Store temporary OTP codes for email verification

**Partition Key**: "OTP"  
**Row Key**: GUID (unique per OTP)

| Column | Type | Description |
|--------|------|-------------|
| Email | string | Target email address |
| Code | string | 6-digit OTP code |
| UserId | string | Associated user ID (or temp ID for registration) |
| ExpiryTime | DateTime | Code expiration (UTC) |
| IsUsed | bool | Whether code has been consumed |
| IsRegistration | bool | True for registration OTPs, false for login |

**Lifetime**: 10 minutes  
**Auto-cleanup**: Expired codes can be purged periodically

**Example Query**:
```csharp
// Find valid OTP
var cutoffTime = DateTime.UtcNow;
var filter = $"PartitionKey eq 'OTP' and Email eq '{email}' and IsUsed eq false and ExpiryTime gt datetime'{cutoffTime:yyyy-MM-ddTHH:mm:ss.fffffffZ}'";
await tableClient.QueryAsync<TableEntity>(filter: filter);
```

---

### 4. **TemplateAdmins** (Administrator Privileges)

**Purpose**: Store admin privileges separately from users

**Partition Key**: "Admin"  
**Row Key**: UserId

| Column | Type | Description |
|--------|------|-------------|
| UserId | string | Reference to TemplateUsers |
| IsActive | bool | Admin privileges active status |
| CanManageAdmins | bool | Permission to add/remove other admins |
| CreatedDate | DateTime | When admin privileges granted (UTC) |
| CreatedBy | string | UserId of granting admin |
| LastModifiedDate | DateTime? | Last privilege update (UTC) |
| LastModifiedBy | string? | UserId of last modifier |

**Access Levels**:
1. **Regular Admin**: `IsActive = true, CanManageAdmins = false`
2. **Super Admin**: `IsActive = true, CanManageAdmins = true`

**Example Query**:
```csharp
// Check if user is active admin
var admin = await tableClient.GetEntityAsync<TableEntity>("Admin", userId);
bool isActiveAdmin = admin.GetBoolean("IsActive") ?? false;
```

---

### 5. **TemplatePendingReg** (Pending Registrations)

**Purpose**: Temporary storage for registrations awaiting OTP verification

**Partition Key**: "PendingReg"  
**Row Key**: TempUserId (GUID)

| Column | Type | Description |
|--------|------|-------------|
| Email | string | Pending user email |
| Username | string | Pending username |
| NormalizedUsername | string | Normalized username |
| PasswordHash | string | Pre-hashed password |
| PasswordSalt | string | Password salt |
| PreferredAuthMethod | string | "Email", "Password", or "Both" |
| ExpiryTime | DateTime | Registration expiry (30 min) |

**Cleanup**: Deleted after successful verification or expiry

---

## Partition Key Strategies

### Why Partition?

Azure Table Storage performance depends on good partitioning:
- **Hot partitions**: Avoid putting all data in one partition
- **Query efficiency**: Queries within a partition are faster
- **Scalability**: Different partitions can be on different servers

### Our Strategies:

1. **Users**: Partitioned by UserId prefix (6 chars)
   - Distributes users evenly across partitions
   - Example: User `abc123def456...` → Partition: `abc123`, Row: `def456...`

2. **Sessions**: Single partition ("Session")
   - Sessions are accessed by ID, not scanned
   - Direct GetEntity lookups are fast

3. **OTP**: Single partition ("OTP")
   - Short-lived, frequently cleaned up
   - Filtered queries by Email and ExpiryTime

4. **Admins**: Single partition ("Admin")
   - Small dataset (few admins)
   - Direct lookups by UserId

---

## Index Optimization

### Email & Username Lookups
- Add `NormalizedUsername` and lowercase `Email` columns
- Create indexes on these fields for fast lookups
- Use OData filters for queries

### Session Validation
- Direct partition + row key lookups (fastest)
- No scanning required

---

## Data Relationships

```
User (1) ────────────┐
  │                  │
  │ (1:1)           (1:1)
  │                  │
  └─► AuthSession   Admin
  
User (1) ──(1:N)──► OTPCode
```

### Foreign Key Enforcement
- Azure Table Storage doesn't enforce foreign keys
- Application logic must maintain referential integrity
- Example: Delete user → delete associated sessions

---

## Maintenance Tasks

### Regular Cleanup
```csharp
// 1. Expired OTP codes (run daily)
var cutoff = DateTime.UtcNow.AddHours(-24);
var filter = $"PartitionKey eq 'OTP' and ExpiryTime lt datetime'{cutoff:yyyy-MM-ddTHH:mm:ss.fffffffZ}'";
// Delete matching entities

// 2. Expired sessions (run weekly)
var sessionCutoff = DateTime.UtcNow;
var sessionFilter = $"PartitionKey eq 'Session' and ExpiryDate lt datetime'{sessionCutoff:yyyy-MM-ddTHH:mm:ss.fffffffZ}'";
// Delete matching entities

// 3. Expired pending registrations (run daily)
var pendingCutoff = DateTime.UtcNow.AddHours(-1);
var pendingFilter = $"PartitionKey eq 'PendingReg' and ExpiryTime lt datetime'{pendingCutoff:yyyy-MM-ddTHH:mm:ss.fffffffZ}'";
// Delete matching entities
```

---

## Adding New Tables

### Template for New Features

```csharp
// 1. Add to TableNames.cs
public const string YourFeature = "TemplateYourFeature";

// 2. Create model in Models/
public class YourFeature
{
    public string Id { get; set; }
    // ... other properties
}

// 3. Create repository in Services/Data/
public interface IYourFeatureRepository
{
    Task<YourFeature?> GetByIdAsync(string id);
    Task<bool> CreateAsync(YourFeature item);
    // ... other methods
}

// 4. Implement repository
public class YourFeatureRepository : IYourFeatureRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    
    public YourFeatureRepository(ITableClientFactory factory)
    {
        _tableClientFactory = factory;
    }
    
    // ... implementation
}
```

---

## Migration Guide

### From MS SQL to Azure Tables

Key differences:
- ❌ No JOINs → Denormalize data
- ❌ No transactions → Use eventual consistency
- ❌ No foreign keys → Enforce in code
- ✅ Horizontal scaling
- ✅ High availability
- ✅ Cost-effective

### Best Practices:
1. **Denormalize** related data for faster queries
2. **Duplicate** data to avoid multiple queries
3. **Use batch operations** for related entities
4. **Plan partition keys** carefully (can't change later)

---

## Backup & Recovery

### Azure Portal Backup
1. Go to Azure Storage Account
2. Enable "Backup" in settings
3. Configure retention policy

### Manual Backup (Code)
```csharp
// Export all entities to JSON
var tableClient = factory.GetTableClient("TemplateUsers");
var entities = new List<TableEntity>();

await foreach (var entity in tableClient.QueryAsync<TableEntity>())
{
    entities.Add(entity);
}

// Save to JSON file
var json = JsonSerializer.Serialize(entities);
File.WriteAllText("backup.json", json);
```

---

## Performance Tips

1. **Batch Operations**: Use `TableTransactionAction` for multiple inserts
2. **Parallel Queries**: Query different partitions concurrently
3. **Select Specific Columns**: Don't fetch unnecessary data
4. **Cache Frequently Read Data**: Reduce storage calls
5. **Monitor Costs**: Azure Storage is billed per operation

---

## Security

### Data at Rest
- ✅ Azure Storage encrypts all data by default
- ✅ Use Azure Key Vault for connection strings

### Data in Transit
- ✅ Always use HTTPS for storage requests
- ✅ Configured automatically in `TableClient`

### Access Control
- ✅ Use Shared Key or SAS tokens
- ✅ Rotate keys regularly
- ✅ Never commit keys to source control

---

## Monitoring

### Key Metrics
- Total storage size
- Request count per hour
- Average latency
- Error rate

### Azure Monitor
Set up alerts for:
- High latency (> 100ms)
- Error rate (> 1%)
- Unexpected storage growth

---

## Cost Optimization

1. **Delete old data**: OTP codes, expired sessions
2. **Use appropriate redundancy**: LRS vs GRS
3. **Monitor transaction costs**: Each query/insert counts
4. **Batch operations**: Reduce transaction count

---

## 🚀 Ready to Extend

This schema provides the foundation for:
- User management
- Authentication
- Authorization
- Admin privileges

Add your domain-specific tables following these patterns!
