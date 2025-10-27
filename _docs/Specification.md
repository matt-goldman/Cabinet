# Plugin.Maui.OfflineData — Specification (Draft)

## Purpose

Provide a lightweight, AOT-safe, encrypted offline data layer for .NET MAUI apps that don’t need a full database. It stores structured data and attachments in encrypted files with an optional search index.

## Goals

* Cross-platform: Works on Android, iOS, macOS, Windows, Catalyst.
* AOT-safe: No JIT or reflection dependencies.
* Encrypted: All data encrypted at rest using AES-GCM or XChaCha20-Poly1305.
* Offline-first: Requires no network or server component.
* Queryable: Simple full-text search via pluggable indexer.
* Simple API: Developer experience similar to Preferences/Settings, but structured.

## Core Concepts

| Concept    | Description                                                     |
| ---------- | --------------------------------------------------------------- |
| Store      | Logical container for all app data (JSON + binary attachments). |
| Record     | A single structured unit of data (serialised JSON).             |
| Attachment | A binary file (e.g. image, PDF) linked to a record.             |
| Index      | Optional inverted index for text search, stored encrypted.      |
| Summary    | Encrypted metadata for efficient previews/lists.                |

## Architecture

```tree
/AppData/
 ├── store/
 │   ├── records/
 │   │    ├── {record-id}.dat        # Encrypted JSON
 │   │    └── {record-id}.meta       # Encrypted metadata
 │   ├── attachments/
 │   │    ├── {record-id}-{filename}.bin
 │   ├── index/
 │   │    └── search.idx             # Encrypted inverted index
 │   └── summary/
 │        └── {year}.sum             # Encrypted year summaries
```

## Key Interfaces

```csharp
public interface IOfflineStore
{
    Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null);
    Task<T?> LoadAsync<T>(string id);
    Task DeleteAsync(string id);
    Task<IEnumerable<SearchResult>> SearchAsync(string query);
}

public interface IEncryptionProvider
{
    Task<byte[]> EncryptAsync(byte[] plaintext, string context);
    Task<byte[]> DecryptAsync(byte[] ciphertext, string context);
}

public interface IIndexProvider
{
    Task IndexAsync(string id, string content, IDictionary<string, string> metadata);
    Task<IEnumerable<SearchResult>> QueryAsync(string query);
    Task ClearAsync();
}
```

## Implementations

### PersistentIndexProvider

The core plugin includes `PersistentIndexProvider` — a production-ready, encrypted persistent index:

- Stores encrypted index to disk
- Survives app restarts with fast cold-start performance
- Token-based full-text search with relevance scoring
- Metadata support for filtering and ranking
- Thread-safe with atomic writes
- Typical performance: sub-millisecond searches on thousands of records

## Security

* Key management
  * Master key stored in platform SecureStorage.
  * Per-file keys derived using HKDF with random nonce.
* Encryption: AES-256-GCM (or XChaCha20-Poly1305 for large files).
  * Default implementation: `AesGcmEncryptionProvider` uses AES-256-GCM
  * Alternative implementations can be plugged in via `IEncryptionProvider`
* Integrity: Authenticated encryption — corruption or tampering invalidates the file.
* No plaintext on disk: Decryption only in memory.

## Dependencies

* .NET 10 (.NET 9 for development temporarily)
* System.Security.Cryptography
* Optional: Microsoft.Maui.Storage (for SecureStorage)
* Optional: Plugin.Maui.OfflineData.Indexing (the index engine)

## Extensibility

* Pluggable indexer (default: regex-tokenised inverted index)
* Pluggable encryption provider
* Custom serialisation (System.Text.Json by default)

## Non-Goals

* Not a relational database.
* Not for large-scale data sets (> few hundred MB).
* No unencrypted or partial-encryption modes.

## Example

```csharp
using Plugin.Maui.OfflineData;
using Plugin.Maui.OfflineData.Index;
using Plugin.Maui.OfflineData.Security;

var encryptionProvider = new AesGcmEncryptionProvider();
var indexProvider = new PersistentIndexProvider(
    FileSystem.AppDataDirectory,
    encryptionProvider);

var store = new FileOfflineStore(
    rootPath: FileSystem.AppDataDirectory,
    encryptionProvider: encryptionProvider,
    indexProvider: indexProvider);

await store.SaveAsync("lesson-2025-10-27", new LessonRecord {
    Subject = "Science",
    Description = "Observed seagulls at the beach"
});

var results = await store.SearchAsync("seagulls");
```
