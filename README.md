# Cabinet

![icon](/assets/icon-256.png)

A secure, indexed offline datastore for .NET.
It’s the simplest way to persist structured data locally - encrypted, searchable, and AOT-safe - without the complexity of a traditional database.

## Why

Most mobile apps don’t need a database, they need an offline data store. Something that can securely persist structured data, search it quickly, and work seamlessly across all target platforms.

Traditional solutions like SQLite or LiteDB are excellent tools, but they bring baggage:

* Native dependencies and platform-specific quirks
* AOT compilation issues (LiteDB)
* Configuration overhead (SQLite encryption - which has commercial implications too)
* API friction for what often boils down to “store, query, retrieve”

`Cabinet` was created to solve that problem.

It gives you database-like capabilities without the database:
encryption, indexing, fast lookups, and predictable performance — all in pure .NET.

## What it is

Cabinet borrows the document-style persistence approach of NoSQL systems, but applies it in a lightweight, encrypted, AOT-friendly way tailored for mobile apps.

| **Concept**                    | **Description**                                                                                           |
| ------------------------------ | --------------------------------------------------------------------------------------------------------- |
| **A secure offline datastore** | Encrypted at rest, zero configuration. There is no “unencrypted mode.”                                    |
| **Indexed by design**          | Full-text search and keyword filtering are built in, not bolted on.                                       |
| **AOT-safe**                   | No runtime code generation, reflection, or JIT reliance. Works on iOS, Android, macOS, Windows, Catalyst. |
| **Extensible**                 | Bring your own storage, indexer, or encryption provider if you need to.                                   |
| **“Pit of success” defaults**  | Security, predictability, and simplicity are defaults — not optional flags.                               |

If you think you need a mobile database, you might actually need this.

## What it isn't

| **It isn’t**          | **Because…**                                                               |
| --------------------- | -------------------------------------------------------------------------- |
| A relational database | There are no joins or schemas, it stores domain objects, not rows.        |
| An ORM                | You interact with your models directly.                                    |
| A cloud sync engine   | Data lives locally; sync is up to you.                                     |
| A toy                 | It’s built for production use: encrypted, indexed, and tested under load. |

If you’ve ever used IndexedDB in the browser, you can think of this as IndexedDB for standalone .NET projects, but simpler, safer, and designed for .NET idioms.

## Features

* AES-256-GCM encryption (per file)
* HKDF key derivation with SecureStorage master key
* Persistent encrypted full-text index
* Atomic writes (no plaintext ever on disk)
* JSON serialisation (customisable)
* Extensible architecture (BYO store, index, encryption)
* 100% managed .NET, AOT-safe, no native dependencies

## Performance Summary

`Cabinet` offers consistent, predictable performance, even with encryption.

| **Pattern**     | **Records** | **Operation**            | **Duration** | **Search (avg)** | **Cold Start** |
| --------------- | ----------- | ------------------------ | ------------ | ---------------- | -------------- |
| Record-per-file | 5,000       | Save + Index all records | 10,000 ms    | 0.9 ms           | 19 ms          |
| Aggregate files | 5,000       | Save + Index all records | **30 ms**    | **0.15 ms**      | **4 ms**       |

Tests measure saving and indexing the _entire dataset_, not single-record inserts.
For incremental operations, performance is effectively instantaneous.

## Quick start

```csharp
using Cabinet;
using Cabinet.Index;
using Cabinet.Security;

// Generate or retrieve a master encryption key (32 bytes)
var masterKey = new byte[32];
RandomNumberGenerator.Fill(masterKey);
// In production, store this key securely using SecureStorage

var encryption = new AesGcmEncryptionProvider(masterKey);
var index = new PersistentIndexProvider(FileSystem.AppDataDirectory, encryption);

var store = new FileOfflineStore(
    FileSystem.AppDataDirectory,
    encryption,
    index);

// Save
await store.SaveAsync("lesson-2025-10-27", new LessonRecord {
    Subject = "Science",
    Description = "Observed seagulls at the beach"
});

// Search
var results = await store.FindAsync("seagulls");
```

## Extensibility

The core components (storage, indexing, and encryption) are all replaceable.
You can implement `IOfflineStore`, `IIndexProvider`, or `IEncryptionProvider` to extend behaviour.

Examples:

* Replace AES-GCM with a hardware-backed key provider
* Implement a custom indexer for vector search
* Store large attachments in a separate encrypted volume

See Architecture
 for extension points and examples.

## Conceptual Architecture

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

## Learn Mode

| **Topic**                                                        | **Description**                                          |
| ---------------------------------------------------------------- | -------------------------------------------------------- |
| [docs/data-organization.md](_docs/data-organization.md)           | How to structure your data for speed and maintainability |
| [docs/performance.md](_docs/performance.md)                       | Full benchmark data and comparisons                      |
| [docs/performance-principles.md](_docs/performance-principles.md) | Why the design scales so well                            |
| [docs/architecture.md](_docs/architecture.md)                     | Encryption, atomic writes, and extensibility             |
| [docs/api-reference.md](_docs/api-reference.md)                   | Interfaces, extension points, and contracts              |
| [docs/use-cases.md](_docs/use-cases.md)                           | Examples of real-world usage patterns                    |
