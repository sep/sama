namespace SAMA.Data.Services;

/// <summary>
/// Provides the application-wide encryption key for database field encryption.
/// </summary>
public class EncryptionKeyProvider(string key)
{
    public string Key { get; } = !string.IsNullOrWhiteSpace(key)
        ? key
        : throw new ArgumentException("Encryption key cannot be null or empty.", nameof(key));
}
