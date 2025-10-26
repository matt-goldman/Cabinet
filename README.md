# Plugin.Maui.OfflineData

A lightweight, encrypted offline data layer for .NET MAUI apps.
It’s not a database — it’s a structured file-store that keeps your data secure and searchable without native dependencies.

## Why?

Most MAUI apps don’t need SQLite or Realm — they just need:

* Offline persistence of structured data
* Encryption at rest
* Simple full-text search
* Cross-platform reliability

Plugin.Maui.OfflineData delivers that in pure .NET, AOT-safe and dependency-free.

## Features

✅ AES-256-GCM encryption (per-file)
✅ HKDF key derivation and SecureStorage master key
✅ Atomic writes, no plaintext on disk
✅ Optional full-text index
✅ JSON serialisation (customisable)
✅ Cross-platform (.NET 8/9 MAUI Android, iOS, Windows, Catalyst)

## Quick start

```csharp
using Plugin.Maui.OfflineData;

// Configure the store
var store = new FileOfflineStore(
    FileSystem.AppDataDirectory,
    new AesGcmEncryptionProvider());

// Save a record
await store.SaveAsync("lesson-2025-10-27", new LessonRecord {
    Subject = "Science",
    Description = "Observed seagulls at the beach"
});

// Search
var results = await store.SearchAsync("seagulls");
```

## Architecture

```tree
/AppData/
 ├── records/
 │    ├── {id}.dat        # Encrypted JSON
 │    ├── {id}.meta       # Encrypted metadata
 ├── attachments/
 │    ├── {id}-{filename}.bin
 ├── index/
 │    └── search.idx      # Encrypted inverted index
 └── summary/
      └── {year}.sum      # Encrypted summaries
```

## API

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
    Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string context);
    Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> ciphertext, string context);
}

public interface IIndexProvider
{
    Task IndexAsync(string id, string content, IDictionary<string, string> metadata);
    Task<IEnumerable<SearchResult>> QueryAsync(string query);
}
```

## Security model

* Master key in SecureStorage
* Per-file keys via HKDF(masterKey, fileId)
* AES-GCM authenticated encryption
* Atomic writes to .tmp then rename
* No decrypted files written to disk

## Indexing

Default indexer tokenises text via regex and stores an inverted index encrypted on disk.
Developers can replace this with a custom index provider (Lucene, ML, etc.).

## Extensibility

* Swap encryption algorithms (e.g. XChaCha20)
* Plug custom tokenizers or metadata processors
* Custom serialisers for domain-specific data

## Non-goals

* Relational querying or joins
* Multi-GB datasets
* Background sync (there are better ways to do this)

## Roadmap

| Item                                        | Status |
| ------------------------------------------- | ------ |
| Add incremental index update support        | ⬜     |
| Add thumbnail caching for attachments       | ⬜     |
| Provide OfflineData.SqliteAdapter sample    | ⬜     |
| Add unit-tested atomic writer abstraction   | ⬜     |
| Publish NuGet package with docs and samples | ⬜     |