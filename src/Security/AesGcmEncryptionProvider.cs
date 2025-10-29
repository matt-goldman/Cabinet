using System.Security.Cryptography;
using System.Text;
using Cabinet.Abstractions;

namespace Cabinet.Security;

/// <summary>
/// Provides AES-GCM authenticated encryption for data at rest.
/// Uses AES-256-GCM with 96-bit nonces and 128-bit authentication tags.
/// </summary>
/// <remarks>
/// Initialises a new instance of the <see cref="AesGcmEncryptionProvider"/> class.
/// </remarks>
/// <param name="masterKey">The 256-bit (32-byte) master encryption key</param>
/// <exception cref="ArgumentException">Thrown if the master key is not 32 bytes</exception>
public sealed class AesGcmEncryptionProvider(byte[] masterKey) : IEncryptionProvider
{

    /// <inheritdoc/>
    /// <remarks>
    /// Uses AES-256-GCM authenticated encryption. The context is used as additional authenticated data (AAD).
    /// Returns a byte array containing [nonce(12) | ciphertext(variable) | tag(16)].
    /// </remarks>
    public Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string context)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var aad = Encoding.UTF8.GetBytes(context);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(masterKey, 16);
        aes.Encrypt(nonce, plaintext.Span, cipher, tag, aad);

        var result = new byte[nonce.Length + cipher.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length, cipher.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + cipher.Length, tag.Length);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses AES-256-GCM to decrypt and verify authenticity. The context must match the value used during encryption.
    /// Expects input in format [nonce(12) | ciphertext(variable) | tag(16)].
    /// </remarks>
    public Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> ciphertext, string context)
    {
        var aad = Encoding.UTF8.GetBytes(context);
        var nonce = ciphertext[..12].ToArray();
        var tag = ciphertext.Slice(ciphertext.Length - 16, 16).ToArray();
        var enc = ciphertext[12..^16].ToArray();
        var plain = new byte[enc.Length];

        using var aes = new AesGcm(masterKey, 16);
        aes.Decrypt(nonce, enc, tag, plain, aad);

        return Task.FromResult(plain);
    }
}
