# GitHub Copilot Instructions for Cabinet

## Project Overview

Cabinet is a lightweight, encrypted offline data layer for .NET applications, primarily .NET MAUI but useful for other .NET platforms as well. It provides structured file storage with encryption at rest, full-text search capabilities, and cross-platform reliability without requiring native database dependencies like SQLite or Realm.

**Key characteristics:**
- Pure .NET implementation
- AOT-safe (no JIT or reflection dependencies)
- Cross-platform (.NET MAUI: Android, iOS, Windows, macOS, Catalyst)
- Security-first design with AES-256-GCM encryption
- Simple, developer-friendly API

## Architecture

The project follows a clean, modular architecture:

```
src/
├── Abstractions/       # Core interfaces (IOfflineStore, IEncryptionProvider, IIndexProvider)
├── Core/              # Main implementations (FileOfflineStore, data models)
├── Security/          # Encryption providers (AesGcmEncryptionProvider)
└── Utilities/         # Helper classes and extensions
```

**Data storage structure:**
```
/AppData/
 ├── records/          # Encrypted JSON records
 ├── attachments/      # One directory per record, keyed by a hash of the record id
 │   └── {hash(recordId)}/
 │       ├── manifest.dat        # Encrypted attachment metadata
 │       └── {hash(name)}.bin    # Encrypted attachment content
 ├── index/           # Encrypted search index
 └── summary/         # Encrypted metadata summaries
```

**Core design patterns:**
- Interface-based abstraction for extensibility
- Dependency injection friendly
- Atomic file writes (write to .tmp, then rename)
- Per-file authenticated encryption, with the file's identity bound in as AAD
- No plaintext ever written to disk

## Development Guidelines

**Always search Microsoft documentation (MS Learn) when working with .NET, Windows, or Microsoft features, or APIs.** Reference official Microsoft Learn documentation to find the most current information about capabilities, best practices, and implementation patterns before making changes.

### .NET Version Requirements

- **Minimum .NET version: .NET 9**
- **Never downgrade .NET versions**
- Development environments must always be upgraded to use .NET 9 or later
- The project targets: `net10.0`

### Code Style and Formatting

- **Don't commit formatting-only changes** unless specifically requested
- **Use tab spacing in code** (follow existing indentation patterns in the codebase)
- **Code comments:** Use Australian English spelling (e.g., "tokenise", "colour", "serialise")
- **Code identifiers:** Use US English spelling (e.g., "Tokenize", "Color", "Serialize" in class/method/variable names)
- **Documentation:** Use Australian English spelling in all non-code content (README.md, markdown docs, XML comments)

### .NET MAUI Best Practices

- Follow MVVM pattern recommendations where applicable
- Leverage platform-specific APIs through .NET MAUI abstractions
- Use `FileSystem.AppDataDirectory` for cross-platform file storage
- Utilize `SecureStorage` for sensitive data like encryption keys
- Ensure AOT compatibility (avoid reflection, dynamic code generation)

### Security and Encryption

- All data must be encrypted at rest using authenticated encryption (AES-GCM)
- Master keys are stored in platform `SecureStorage`
- A single master key is used with AES-256-GCM; the file's context (record id, and for
  attachments the attachment name too) is bound in as additional authenticated data. Note
  that this is AAD binding, not HKDF per-file key derivation
- Atomic writes prevent partial/corrupted data on disk
- Never write decrypted content to disk, only to memory
- File extensions: `.dat` for encrypted data, `.bin` for attachment content, `.tmp` for in-progress writes
- A save stages every file as `.tmp`, then renames attachment blobs, then the manifest, then the
  record. The record rename is the commit point, so a record is never visible referencing
  attachments that are not on disk. This is crash consistency, not durability — staged writes are
  not flushed before rename
- Attachment blobs are authenticated against both the record id and the attachment name, so a blob
  cannot be substituted for another record's or another name's
- User-supplied names are never used directly as path segments; record ids and attachment names are
  hashed to form directory and file names

### API Design

- Keep interfaces simple and focused
- Use async/await for all I/O operations
- Generic types for flexibility (`Task<T?>`, `SaveAsync<T>`)
- Optional parameters for extensibility (e.g., `attachments` parameter)
- Return meaningful types (`SearchResult`, not raw dictionaries)

### Testing Considerations

- Unit tests should mock `IEncryptionProvider` and `IIndexProvider`
- Integration tests should verify encryption/decryption round-trips
- Test atomic write behaviour (crash/failure scenarios)
- Verify cross-platform file path handling
- Test with various data types and edge cases (null, empty, large files)

### Dependencies

- Minimize external dependencies
- Use .NET BCL (Base Class Library) whenever possible
- Avoid platform-specific code in core abstractions
- Plugin architecture allows custom implementations (indexers, encryption)

### Documentation

- Update README.md when adding new features or changing APIs
- Keep `_docs/Specification.md` aligned with implementation
- Use code examples in Australian English prose
- API documentation should be clear and include usage examples

## .NET MAUI Specific Guidance

When working with .NET MAUI features:

1. **Platform APIs:** Access through .NET MAUI abstractions first, fall back to platform-specific code only when necessary
2. **File System:** Use `FileSystem` class from `Microsoft.Maui.Storage` namespace
3. **Secure Storage:** Use `SecureStorage` class for sensitive data persistence
4. **Cross-platform paths:** Always use `Path.Combine()` for path construction
5. **Lifecycle:** Consider app lifecycle events for background operations

## Extensibility Points

The plugin is designed to be extended through:

- **IEncryptionProvider:** Swap encryption algorithms (e.g., XChaCha20-Poly1305)
- **IIndexProvider:** Custom search/indexing implementations (e.g., Lucene.NET, ML-based)
- **JsonSerializerOptions:** Custom JSON serialization settings
- **File structure:** Can be adapted for different storage backends

## Common Patterns

### Saving data
```csharp
var store = new FileOfflineStore(
    FileSystem.AppDataDirectory,
    new AesGcmEncryptionProvider());

await store.SaveAsync("record-id", myData);
```

### Loading data
```csharp
var data = await store.LoadAsync<MyType>("record-id");
```

### Searching
```csharp
var results = await store.SearchAsync("search query");
```

### With attachments

Attachment bytes are stored as separate encrypted files, never inside the record's JSON.

```csharp
// Save a record and its attachments together (all-or-nothing)
await using var photoStream = File.OpenRead("photo.jpg");
await store.SaveAsync("record-id", myData, new[] {
    new FileAttachment("photo.jpg", "image/jpeg", photoStream)
});

// Add one attachment to an existing record
var info = await store.SaveAttachmentAsync("record-id", attachment);

// List and read back
var attachments = await store.ListAttachmentsAsync("record-id");
await using var content = await store.OpenAttachmentAsync("record-id", "photo.jpg");
```

### Attachments via RecordSet

`RecordSet<T>` is the API most consumers should use. It keys attachments on the record's own ID,
namespaced by the set (records IDs are only unique within a set, since the whole set is one document).

```csharp
lesson.Attachments = [await lessons.AddAttachmentAsync(lesson.Id, attachment)];
await lessons.UpdateAsync(lesson.Id, lesson);

await using var content = await lessons.OpenAttachmentAsync(lesson.Id, "photo.jpg");

await lessons.RemoveAsync(lesson.Id);          // cascades to the record's attachments
await lessons.CompactAttachmentsAsync();       // reclaims orphans from interrupted removals
```

`RemoveAsync` saves the set before deleting attachments, so an interruption leaves orphaned bytes
rather than a record referencing attachments that are gone.

**`FileAttachment` is a write-side handle only.** It wraps a live `Stream`, so it cannot be
serialised and is never valid as a property on a record model — put `AttachmentInfo` (name, content
type, length) on the model instead and read the bytes back on demand. The `[AotRecord]` source
generator reports CAB002 for models that get this wrong.

## Important Notes

- **Atomic writes:** Always write to `.tmp` file first, then rename to final name
- **Error handling:** Encryption failures should not leave partial data on disk
- **Key management:** Master key must be initialized before first use
- **Platform differences:** Be aware of path separators and file system capabilities across platforms

## Keeping These Instructions Current

**As the project evolves, update this `copilot-instructions.md` file with:**
- New architectural patterns or major structural changes
- Updated API conventions or breaking changes
- New dependencies or framework requirements
- Additional best practices discovered during development
- Common pitfalls or gotchas encountered
- New extensibility points or plugin interfaces

Regular updates to these instructions help maintain consistency and improve the development experience for all contributors and Copilot-assisted development sessions.
