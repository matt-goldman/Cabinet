using System.Security.Cryptography;
using System.Text;
using Plugin.Maui.OfflineData.Abstractions;

namespace Plugin.Maui.OfflineData.Security;

/// <summary>
/// Provides AES-GCM authenticated encryption for data at rest.
/// Uses AES-256-GCM with 96-bit nonces and 128-bit authentication tags.
/// </summary>
public sealed class AesGcmEncryptionProvider : IEncryptionProvider
{
    private readonly byte[] _masterKey;

    /// <summary>
    /// Initialises a new instance of the <see cref="AesGcmEncryptionProvider"/> class.
    /// </summary>
    /// <param name="masterKey">The 256-bit (32-byte) master encryption key</param>
    /// <exception cref="ArgumentException">Thrown if the master key is not 32 bytes</exception>
    public AesGcmEncryptionProvider(byte[] masterKey) => _masterKey = masterKey;

    /// <summary>
    /// Encrypts the provided plaintext data using AES-256-GCM authenticated encryption.
    /// The context is used as additional authenticated data (AAD) to bind the encryption to a specific purpose.
    /// </summary>
    /// <param name="plaintext">The data to encrypt</param>
    /// <param name="context">Additional context data used in authenticated encryption (e.g., record ID)</param>
    /// <returns>A byte array containing [nonce(12) | ciphertext(variable) | tag(16)]</returns>
    public Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string context)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var aad = Encoding.UTF8.GetBytes(context);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_masterKey, 16);
        aes.Encrypt(nonce, plaintext.Span, cipher, tag, aad);

        var result = new byte[nonce.Length + cipher.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length, cipher.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + cipher.Length, tag.Length);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Decrypts the provided ciphertext data using AES-256-GCM and verifies its authenticity.
    /// The context must match the context used during encryption.
    /// </summary>
    /// <param name="ciphertext">The encrypted data in format [nonce(12) | ciphertext(variable) | tag(16)]</param>
    /// <param name="context">Additional context data used in authenticated encryption (e.g., record ID)</param>
    /// <returns>The decrypted plaintext data</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown if authentication or decryption fails</exception>
    public Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> ciphertext, string context)
    {
        var aad = Encoding.UTF8.GetBytes(context);
        var nonce = ciphertext[..12].ToArray();
        var tag = ciphertext.Slice(ciphertext.Length - 16, 16).ToArray();
        var enc = ciphertext.Slice(12, ciphertext.Length - 28).ToArray();
        var plain = new byte[enc.Length];

        using var aes = new AesGcm(_masterKey, 16);
        aes.Decrypt(nonce, enc, tag, plain, aad);

        return Task.FromResult(plain);
    }
}
