using System.Security.Cryptography;
using SAMA.Data.Services;

namespace SAMA.Tests.Unit.Data.Services;

[TestClass]
public class AesEncryptionServiceTests
{
    private const string TestKey = "test-encryption-key-123";
    private const string TestPlainText = "Hello, World!";
    private readonly AesEncryptionService _service = new();

    [TestMethod]
    public void EncryptShouldThrowExceptionWhenKeyIsNull()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => _service.Encrypt(TestPlainText, null!));
        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void EncryptShouldThrowExceptionWhenKeyIsEmpty()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => _service.Encrypt(TestPlainText, string.Empty));
        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void EncryptShouldThrowExceptionWhenKeyIsWhitespace()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => _service.Encrypt(TestPlainText, "   "));
        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void EncryptShouldReturnBase64String()
    {
        var encrypted = _service.Encrypt(TestPlainText, TestKey);

        Assert.IsNotNull(encrypted);
        Assert.IsGreaterThan(0, encrypted.Length);

        // Verify it's valid Base64
        var bytes = Convert.FromBase64String(encrypted);
        Assert.IsNotEmpty(bytes);
    }

    [TestMethod]
    public void EncryptShouldReturnDifferentValuesForSameInput()
    {
        var encrypted1 = _service.Encrypt(TestPlainText, TestKey);
        var encrypted2 = _service.Encrypt(TestPlainText, TestKey);

        Assert.AreNotEqual(encrypted1, encrypted2);
    }

    [TestMethod]
    public void EncryptShouldReturnEmptyStringForEmptyInput()
    {
        var encrypted = _service.Encrypt(string.Empty, TestKey);

        Assert.AreEqual(string.Empty, encrypted);
    }

    [TestMethod]
    public void EncryptShouldReturnNullForNullInput()
    {
        var encrypted = _service.Encrypt(null!, TestKey);

        Assert.IsNull(encrypted);
    }

    [TestMethod]
    public void DecryptShouldReturnOriginalPlainText()
    {
        var encrypted = _service.Encrypt(TestPlainText, TestKey);

        var decrypted = _service.Decrypt(encrypted, TestKey);

        Assert.AreEqual(TestPlainText, decrypted);
    }

    [TestMethod]
    public void DecryptShouldWorkWithLongText()
    {
        var longText = string.Join(" ", Enumerable.Repeat("This is a longer text to test encryption.", 100));
        var encrypted = _service.Encrypt(longText, TestKey);

        var decrypted = _service.Decrypt(encrypted, TestKey);

        Assert.AreEqual(longText, decrypted);
    }

    [TestMethod]
    public void DecryptShouldReturnEmptyStringForEmptyInput()
    {
        var decrypted = _service.Decrypt(string.Empty, TestKey);

        Assert.AreEqual(string.Empty, decrypted);
    }

    [TestMethod]
    public void DecryptShouldReturnNullForNullInput()
    {
        var decrypted = _service.Decrypt(null!, TestKey);

        Assert.IsNull(decrypted);
    }

    [TestMethod]
    public void DecryptShouldThrowExceptionForInvalidCipherText()
    {
        var invalidCipherText = "InvalidBase64!@#$";

        Assert.ThrowsExactly<FormatException>(() => _service.Decrypt(invalidCipherText, TestKey));
    }

    [TestMethod]
    public void DecryptShouldThrowExceptionForTooShortCipherText()
    {
        var tooShortCipherText = Convert.ToBase64String(new byte[10]); // Less than nonce + tag

        Assert.ThrowsExactly<CryptographicException>(() => _service.Decrypt(tooShortCipherText, TestKey));
    }

    [TestMethod]
    public void DecryptShouldThrowExceptionForTamperedCipherText()
    {
        var encrypted = _service.Encrypt(TestPlainText, TestKey);

        // Tamper with the ciphertext
        var bytes = Convert.FromBase64String(encrypted);
        bytes[^1] ^= 0xFF; // Flip bits in last byte
        var tamperedCipherText = Convert.ToBase64String(bytes);

        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => _service.Decrypt(tamperedCipherText, TestKey));
    }

    [TestMethod]
    public void DecryptShouldFailWithDifferentKey()
    {
        var encrypted = _service.Encrypt(TestPlainText, TestKey);

        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => _service.Decrypt(encrypted, "different-key"));
    }

    [TestMethod]
    public void EncryptDecryptShouldWorkWithSpecialCharacters()
    {
        var specialText = "Hello! 🔐 @#$%^&*()";

        var encrypted = _service.Encrypt(specialText, TestKey);
        var decrypted = _service.Decrypt(encrypted, TestKey);

        Assert.AreEqual(specialText, decrypted);
    }

    [TestMethod]
    public void EncryptDecryptShouldWorkWithMultilineText()
    {
        var multilineText = "Line 1\nLine 2\r\nLine 3\tTabbed";

        var encrypted = _service.Encrypt(multilineText, TestKey);
        var decrypted = _service.Decrypt(encrypted, TestKey);

        Assert.AreEqual(multilineText, decrypted);
    }

    [TestMethod]
    public void SameKeyShouldProduceSameDecryptionResult()
    {
        var encrypted = _service.Encrypt(TestPlainText, TestKey);

        var decrypted1 = _service.Decrypt(encrypted, TestKey);
        var decrypted2 = _service.Decrypt(encrypted, TestKey);

        Assert.AreEqual(decrypted1, decrypted2);
        Assert.AreEqual(TestPlainText, decrypted1);
    }

    [TestMethod]
    public void EncryptShouldProduceCiphertextLongerThanPlaintext()
    {
        var encrypted = _service.Encrypt(TestPlainText, TestKey);

        var encryptedBytes = Convert.FromBase64String(encrypted);

        // Encrypted should be at least: nonce (12) + tag (16) + plaintext length
        Assert.IsGreaterThanOrEqualTo(12 + 16 + TestPlainText.Length, encryptedBytes.Length);
    }

    [TestMethod]
    public void DifferentKeysShouldProduceDifferentDerivedKeys()
    {
        var encrypted1 = _service.Encrypt(TestPlainText, "key1");
        var encrypted2 = _service.Encrypt(TestPlainText, "key2");

        // The same plaintext encrypted with different keys should not be decryptable
        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => _service.Decrypt(encrypted2, "key1"));
        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => _service.Decrypt(encrypted1, "key2"));
    }
}
