# Cabinet

**Fun fact:** The original issue in my app that prompted me to build this turned out to be nothing to do with my data storage. Fixed a startup deadlock and everything worked, with LiteDB. So...I guess I learned something...?

![NuGet Version](https://img.shields.io/nuget/v/Cabinet?style=for-the-badge)

![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/matt-goldman/cabinet/ci.yml?branch=main&style=for-the-badge)

![icon](https://raw.githubusercontent.com/matt-goldman/Cabinet/refs/heads/main/assets/icon-256.png)

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
* **Source generator for AOT compilation** (automatic JSON context and IdSelector generation)
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

## Architecture: Three Layers

Cabinet has a layered architecture that lets you choose your level of control:

### Layer 1: Core Storage (Advanced Users)

The foundational layer provides low-level encrypted storage:

- `IOfflineStore` - Core encrypted storage operations
- `IEncryptionProvider` - Pluggable encryption (default: AES-256-GCM)
- `IIndexProvider` - Pluggable full-text search

**When to use:** You need maximum control over storage behavior, custom encryption, or specialized indexing.

### Layer 2: RecordSet API (🎯 Start Here - Most Users)

The high-level domain abstraction that makes Cabinet feel like a document database:

- `RecordSet<T>` - Manages collections of typed records with auto-caching and CRUD operations
- `RecordQuery<T>` - LINQ-style fluent queries
- `RecordCollection<T>` - Scoped collections under a single ID

**When to use:** This is the recommended API for 95% of scenarios. It handles file discovery, loading, caching, and persistence automatically.

**Example:**
```csharp
// Setup (once at app startup)
var store = /* ... see Layer 1 for setup ... */;

// Create a RecordSet for your domain type
var lessons = new RecordSet<LessonRecord>(store, new RecordSetOptions<LessonRecord>
{
    IdSelector = lesson => lesson.LessonId  // For AOT compatibility
});

// Load all lessons into memory cache
await lessons.LoadAsync();

// Add a new lesson (auto-persists to disk)
await lessons.AddAsync(new LessonRecord
{
    LessonId = "lesson-001",
    Subject = "Science",
    Description = "Observed seagulls at the beach"
});

// Query cached data (no disk I/O)
var scienceLessons = lessons.Where(l => l.Subject == "Science");
var recentLessons = lessons.OrderByDescending(l => l.Date).Take(10);

// Search using encrypted index
var results = await lessons.FindAsync("seagulls");
```

### Layer 3: Extension Methods (Convenience)

Syntactic sugar for cleaner code:

- `FindManyAsync()` - Search with automatic data extraction
- `WhereMatch()` - Fluent filter chaining

**When to use:** These make your code more readable but are entirely optional.

## Quick Start

### Install the NuGet package

```bash
dotnet add package Cabinet
```

### Using Source Generator (Recommended for AOT)

The source generator creates RecordSet extensions and helper methods to reduce boilerplate.

> **⚠️ IMPORTANT:**
>
> * You must manually create a `JsonSerializerContext` (System.Text.Json requirement for AOT - source generators cannot coordinate with each other reliably)
> * **All record types and your JsonSerializerContext must have the same accessibility** - If using public records, use a public context. If using internal records, use an internal context. This is a C# language rule.
> * The generator **only creates convenience methods**, not the JsonSerializerContext itself

#### Step 1: Create a JsonSerializerContext (Required)

You must manually create this in your project:

```csharp
using System.Text.Json.Serialization;

namespace MyApp;

[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CabinetJsonContext : JsonSerializerContext
{
}
```

> **Why is this required?** Source generators can't reliably coordinate with System.Text.Json's generator in the same compilation pass. By creating this in your own code, System.Text.Json can properly implement the abstract members for AOT compatibility.

#### Step 2: Mark Your Records and Use Generated Extensions

The generator creates convenience methods (RecordSet extensions, CreateCabinetStore) for you:

```csharp
using Cabinet;

// 1. Decorate your record types
[AotRecord]
public record LessonRecord
{
    public string Id { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
}

// 2. Use generated extensions
using Cabinet.Generated;

var masterKey = new byte[32];
RandomNumberGenerator.Fill(masterKey);

var store = CabinetStoreExtensions.CreateCabinetStore(
    FileSystem.AppDataDirectory, 
    masterKey,
    CabinetJsonContext.Default);  // Pass your context

var lessons = store.CreateLessonRecordRecordSet();  // Generated method

// 3. Load and use
await lessons.LoadAsync();
await lessons.AddAsync(new LessonRecord { 
    Id = "001", 
    Subject = "Science",
    Description = "Observed seagulls at the beach"
});
var all = await lessons.GetAllAsync();
```

See [Source Generator Usage Guide](_docs/source-generator-usage.md) for complete details.

### Using `RecordSet<T>` (Manual Setup)

This approach gives you full control and supports **any accessibility level** (public, internal, private). You manually configure everything, including AOT-compatible JSON serialization.

```csharp
using Cabinet;
using Cabinet.Core;
using Cabinet.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

// Your records can be internal/private
internal record LessonRecord
{
    public string LessonId { get; set; } = "";
    public string Subject { get; set; } = "";
}

// Create internal JsonSerializerContext for internal types
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
internal partial class MyJsonContext : JsonSerializerContext { }

// 1. Setup storage (once at app startup)
var masterKey = new byte[32];
RandomNumberGenerator.Fill(masterKey);
// In production, store this in SecureStorage

var encryption = new AesGcmEncryptionProvider(masterKey);
var jsonOptions = new JsonSerializerOptions
{
    TypeInfoResolver = MyJsonContext.Default  // AOT-safe
};
var store = new FileOfflineStore(
    FileSystem.AppDataDirectory, 
    encryption,
    jsonOptions);

// 2. Create RecordSet for your domain type
var options = new RecordSetOptions<LessonRecord>
{
    IdSelector = lesson => lesson.LessonId  // AOT-safe
};
var lessons = new RecordSet<LessonRecord>(store, options);

// 3. Load and use
await lessons.LoadAsync();
await lessons.AddAsync(new LessonRecord { LessonId = "001", Subject = "Science" });
var all = await lessons.GetAllAsync();
```

See [Manual AOT Setup Guide](_docs/aot-manual-setup.md) for a complete example with internal types.

### Using IOfflineStore Directly (Advanced)

```csharp
using Cabinet;
using Cabinet.Index;
using Cabinet.Security;

// Generate or retrieve a master encryption key (32 bytes)
var masterKey = new byte[32];
RandomNumberGenerator.Fill(masterKey);

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
var results = await store.FindAsync<LessonRecord>("seagulls");
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
| [docs/aot-manual-setup.md](_docs/aot-manual-setup.md)            | Manual AOT setup for internal/private types              |
| [docs/source-generator-usage.md](_docs/source-generator-usage.md)| Complete source generator guide                          |
| [docs/data-organization.md](_docs/data-organization.md)           | How to structure your data for speed and maintainability |
| [docs/performance.md](_docs/performance.md)                       | Full benchmark data and comparisons                      |
| [docs/performance-principles.md](_docs/performance-principles.md) | Why the design scales so well                            |
| [docs/architecture.md](_docs/architecture.md)                     | Encryption, atomic writes, and extensibility             |
| [docs/api-reference.md](_docs/api-reference.md)                   | Interfaces, extension points, and contracts              |
| [docs/use-cases.md](_docs/use-cases.md)                           | Examples of real-world usage patterns                    |
