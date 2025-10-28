namespace Plugin.Maui.OfflineData.Abstractions;

/// <summary>
/// Defines the contract for an encryption provider that handles authenticated encryption
/// and decryption of data at rest.
/// </summary>
public interface IEncryptionProvider
{
    /// <summary>
    /// Encrypts the provided plaintext data using authenticated encryption.
    /// </summary>
    /// <param name="plaintext">The data to encrypt</param>
    /// <param name="context">Additional context data used in authenticated encryption (e.g., record ID)</param>
    /// <returns>The encrypted data including nonce, ciphertext, and authentication tag</returns>
    Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string context);
    
    /// <summary>
    /// Decrypts the provided ciphertext data and verifies its authenticity.
    /// </summary>
    /// <param name="ciphertext">The encrypted data including nonce, ciphertext, and authentication tag</param>
    /// <param name="context">Additional context data used in authenticated encryption (e.g., record ID)</param>
    /// <returns>The decrypted plaintext data</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown if decryption or authentication fails</exception>
    Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> ciphertext, string context);
}
