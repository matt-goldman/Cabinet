using System.Text;
using Plugin.Maui.OfflineData.Security;

namespace Plugin.Maui.OfflineData.Tests;

public class AesGcmEncryptionProviderTests
{
	private readonly byte[] _testKey;

	public AesGcmEncryptionProviderTests()
	{
		// Generate a 256-bit (32 byte) key for AES-256-GCM
		_testKey = new byte[32];
		Random.Shared.NextBytes(_testKey);
	}

	[Fact]
	public async Task EncryptAsync_ShouldProduceNonEmptyCiphertext()
	{
		// Arrange
		var provider = new AesGcmEncryptionProvider(_testKey);
		var plaintext = Encoding.UTF8.GetBytes("Test data");

		// Act
		var ciphertext = await provider.EncryptAsync(plaintext, "test-context");

		// Assert
		Assert.NotNull(ciphertext);
		Assert.NotEmpty(ciphertext);
		Assert.NotEqual(plaintext.Length, ciphertext.Length);
	}

	[Fact]
	public async Task EncryptDecrypt_RoundTrip_ShouldReturnOriginalData()
	{
		// Arrange
		var provider = new AesGcmEncryptionProvider(_testKey);
		var originalText = "This is a test message for encryption";
		var plaintext = Encoding.UTF8.GetBytes(originalText);

		// Act
		var ciphertext = await provider.EncryptAsync(plaintext, "test-context");
		var decrypted = await provider.DecryptAsync(ciphertext, "test-context");
		var decryptedText = Encoding.UTF8.GetString(decrypted);

		// Assert
		Assert.Equal(originalText, decryptedText);
	}

	[Fact]
	public async Task EncryptAsync_SamePlaintextDifferentCalls_ShouldProduceDifferentCiphertext()
	{
		// Arrange
		var provider = new AesGcmEncryptionProvider(_testKey);
		var plaintext = Encoding.UTF8.GetBytes("Test data");

		// Act
		var ciphertext1 = await provider.EncryptAsync(plaintext, "test-context");
		var ciphertext2 = await provider.EncryptAsync(plaintext, "test-context");

		// Assert - due to random nonce, ciphertexts should differ
		Assert.NotEqual(ciphertext1, ciphertext2);
	}

	[Fact]
	public async Task DecryptAsync_WithDifferentContext_ShouldThrow()
	{
		// Arrange
		var provider = new AesGcmEncryptionProvider(_testKey);
		var plaintext = Encoding.UTF8.GetBytes("Test data");

		// Act
		var ciphertext = await provider.EncryptAsync(plaintext, "original-context");

		// Assert - decrypting with wrong context should fail
		// AuthenticationTagMismatchException is a subclass of CryptographicException
		await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(
			async () => await provider.DecryptAsync(ciphertext, "different-context")
		);
	}

	[Fact]
	public async Task DecryptAsync_WithTamperedCiphertext_ShouldThrow()
	{
		// Arrange
		var provider = new AesGcmEncryptionProvider(_testKey);
		var plaintext = Encoding.UTF8.GetBytes("Test data");
		var ciphertext = await provider.EncryptAsync(plaintext, "test-context");

		// Act - tamper with ciphertext
		ciphertext[ciphertext.Length / 2] ^= 0xFF;

		// Assert - AuthenticationTagMismatchException is a subclass of CryptographicException
		await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(
			async () => await provider.DecryptAsync(ciphertext, "test-context")
		);
	}

	[Fact]
	public async Task EncryptAsync_WithEmptyData_ShouldSucceed()
	{
		// Arrange
		var provider = new AesGcmEncryptionProvider(_testKey);
		var plaintext = Array.Empty<byte>();

		// Act
		var ciphertext = await provider.EncryptAsync(plaintext, "test-context");
		var decrypted = await provider.DecryptAsync(ciphertext, "test-context");

		// Assert
		Assert.Empty(decrypted);
	}

	[Fact]
	public async Task EncryptAsync_WithLargeData_ShouldSucceed()
	{
		// Arrange
		var provider = new AesGcmEncryptionProvider(_testKey);
		var largeData = new byte[1024 * 1024]; // 1MB
		Random.Shared.NextBytes(largeData);

		// Act
		var ciphertext = await provider.EncryptAsync(largeData, "test-context");
		var decrypted = await provider.DecryptAsync(ciphertext, "test-context");

		// Assert
		Assert.Equal(largeData, decrypted);
	}

	[Fact]
	public async Task EncryptAsync_WithDifferentContexts_ShouldProduceDifferentCiphertexts()
	{
		// Arrange
		var provider = new AesGcmEncryptionProvider(_testKey);
		var plaintext = Encoding.UTF8.GetBytes("Test data");

		// Act
		var ciphertext1 = await provider.EncryptAsync(plaintext, "context-1");
		var ciphertext2 = await provider.EncryptAsync(plaintext, "context-2");

		// Assert - different contexts should produce different ciphertexts
		// (due to AAD in AES-GCM)
		// Note: The nonces are random, so they'll differ anyway, but the context affects the tag
		Assert.NotEqual(ciphertext1, ciphertext2);
	}
}
