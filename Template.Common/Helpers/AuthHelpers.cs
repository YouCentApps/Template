using System.Security.Cryptography;
using System.Text;

namespace Template.Common.Helpers;

/// <summary>
/// Utilities for password hashing and validation
/// </summary>
public static class PasswordHelper
{
    /// <summary>
    /// Generate a cryptographic salt for password hashing
    /// </summary>
    public static string GenerateSalt()
    {
        byte[] saltBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Hash a password with a salt using PBKDF2
    /// </summary>
    public static string HashPassword(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, saltBytes, 10000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verify a password against a stored hash and salt
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash, string salt)
    {
        string computedHash = HashPassword(password, salt);
        return computedHash == storedHash;
    }
}

/// <summary>
/// Utilities for OTP generation and validation
/// </summary>
public static class OTPHelper
{
    /// <summary>
    /// Generate a 6-digit OTP code
    /// </summary>
    public static string GenerateOTP()
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        int value = Math.Abs(BitConverter.ToInt32(randomBytes, 0));
        return (value % 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Utilities for input validation and normalization
/// </summary>
public static class InputValidator
{
    /// <summary>
    /// Normalize username to uppercase for case-insensitive lookups.
    /// Uppercase is intentional — Azure Table Storage OData filters use exact string matching,
    /// so all normalized values must be consistently uppercase to match stored data.
    /// </summary>
    public static string NormalizeUsername(string username)
    {
        if (username is null)
            return string.Empty;

        return username.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Normalize email to uppercase.
    /// Uppercase is intentional — Azure Table Storage OData filters use exact string matching,
    /// so all normalized values must be consistently uppercase to match stored data.
    /// </summary>
    public static string NormalizeEmail(string email)
    {
        if (email is null)
            return string.Empty;

        return email.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Generate a unique user ID
    /// </summary>
    public static string GenerateUserId()
    {
        return Guid.NewGuid().ToString("N"); // 32 characters, no dashes
    }
}
