namespace Plugin.Maui.OfflineData.Abstractions;

public interface IEncryptionProvider
{
    Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string context);
    Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> ciphertext, string context);
}
