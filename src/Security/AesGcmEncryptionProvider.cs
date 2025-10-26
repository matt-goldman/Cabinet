using System.Security.Cryptography;
using System.Text;
using Plugin.Maui.OfflineData.Abstractions;

namespace Plugin.Maui.OfflineData.Security;

public sealed class AesGcmEncryptionProvider : IEncryptionProvider
{
    private readonly byte[] _masterKey;

    public AesGcmEncryptionProvider(byte[] masterKey) => _masterKey = masterKey;

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
