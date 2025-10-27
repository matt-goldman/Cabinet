# Plugin.Maui.OfflineData Tests

This test project contains comprehensive unit and integration tests for the Plugin.Maui.OfflineData library.

## Test Structure

The test suite is organised into the following test classes:

### AesGcmEncryptionProviderTests
Tests for the AES-GCM encryption provider implementation:
- Encryption produces non-empty ciphertext
- Encrypt/decrypt round-trip preserves data
- Same plaintext produces different ciphertexts (due to random nonce)
- Different contexts produce different ciphertexts
- Tampered ciphertext fails decryption
- Empty and large data handling
- Wrong context fails decryption

### FileOfflineStoreTests
Tests for the file-based offline store implementation:
- Directory creation on initialisation
- Encrypted file creation
- Save and load operations
- Overwriting existing records
- Delete operations
- File attachment handling (single and multiple)
- Search without index provider
- Complex object serialisation

### IndexProviderTests
Tests for index provider integration:
- Index callback during save operations
- Query matching and ranking
- Empty result handling
- Complex content indexing
- Metadata passing

**Note:** These tests use a mock index provider. When EasyIndex (https://github.com/matt-goldman/easyindex) is integrated, these tests can be adapted to test the actual implementation.

### IntegrationTests
End-to-end integration tests simulating realistic usage:
- Complete save/load/delete workflow
- Attachments with records
- Multiple independent records
- Record updates
- Atomic write behaviour
- Encryption at rest verification
- Key isolation (different keys can't decrypt each other's data)

## Test Data Models

Tests use simple record types that match the README examples:
- `LessonRecord` - matching the documentation example
- `TestRecord` - simple test data
- `ComplexTestRecord` - nested structures

## Current Status

⚠️ **Note:** As documented in issue #[number], these tests are designed to validate the expected behaviour of the product. Some tests currently fail due to implementation issues that need to be addressed:

1. **File path handling** - The atomic write implementation has a path extension issue (`.dat.tmp` → `.dat.dat` instead of `.dat`)
2. **LoadAsync returning null** - Related to the file path issue above
3. **Attachment file extension** - Double extension issue similar to records

These test failures are expected and help identify the bugs that need to be fixed in the implementation.

## Running the Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~AesGcmEncryptionProviderTests"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## Test Coverage

The test suite covers:
- ✅ Encryption and decryption operations
- ✅ File I/O operations (with known bugs)
- ✅ Atomic writes
- ✅ Attachment handling
- ✅ Index provider integration (mock)
- ✅ Complex data serialisation
- ✅ Security properties (encryption at rest, key isolation)

## Future Work

- Integrate EasyIndex for actual search functionality
- Add tests for concurrent operations
- Add tests for error recovery scenarios
- Add performance benchmarks
- Add tests for cross-platform path handling
