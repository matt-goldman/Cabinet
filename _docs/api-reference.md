# API Reference

This document provides a comprehensive reference for the core interfaces, classes, and extension points in `Plugin.Maui.OfflineData`.

## Core Interfaces

### IOfflineStore

The primary interface for interacting with the offline data store.

```csharp
public interface IOfflineStore
{
    Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null);
    Task<T?> LoadAsync<T>(string id);
    Task DeleteAsync(string id);
    Task<IEnumerable<SearchResult>> FindAsync(string query);
    Task<IEnumerable<SearchResult<T>>> FindAsync<T>(string query);
}
```

#### Methods

**SaveAsync\<T>**
- Saves data with the specified identifier to encrypted storage.
- The data is serialised to JSON, encrypted, and written atomically to disk.
- Optionally accepts file attachments that are stored separately and encrypted.
- Automatically updates the search index if an `IIndexProvider` is configured.

**LoadAsync\<T>**
- Loads data with the specified identifier from encrypted storage.
- Returns `null` if the record does not exist.
- Decrypts the file and deserialises the JSON content to type `T`.

**DeleteAsync**
- Deletes the record and all associated attachments.
- Does not throw if the record doesn't exist.

**FindAsync**
- Searches for records matching the specified query string.
- Returns metadata-only results (no data loaded).
- Requires an `IIndexProvider` to be configured; returns empty collection otherwise.

**FindAsync\<T>**
- Searches for records and loads their data.
- Attempts to deserialise as `T` or `List<T>` (for aggregate file patterns).
- Returns empty collection if no indexer is configured or no results match.

### IEncryptionProvider

Defines the contract for authenticated encryption and decryption.

```csharp
public interface IEncryptionProvider
{
    Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string context);
    Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> ciphertext, string context);
}
```

#### Methods

**EncryptAsync**
- Encrypts plaintext using authenticated encryption (e.g., AES-GCM).
- The `context` parameter provides additional authenticated data (AAD) to bind the encryption to a specific purpose (typically the record ID).
- Returns the encrypted data including nonce, ciphertext, and authentication tag.

**DecryptAsync**
- Decrypts and authenticates the ciphertext.
- The `context` parameter must match the value used during encryption.
- Throws `CryptographicException` if decryption or authentication fails.

### IIndexProvider

Enables full-text search and metadata-based querying.

```csharp
public interface IIndexProvider
{
    Task IndexAsync(string id, string content, IDictionary<string, string> metadata);
    Task<IEnumerable<SearchResult>> QueryAsync(string query);
    Task ClearAsync();
}
```

#### Methods

**IndexAsync**
- Adds or updates a record in the search index.
- The `content` parameter should contain all searchable text.
- The `metadata` parameter stores additional key-value pairs for filtering or display.

**QueryAsync**
- Searches the index for records matching the query terms.
- Returns results ordered by relevance score.
- Typically implements tokenisation and term frequency analysis.

**ClearAsync**
- Removes all entries from the index.
- Used when rebuilding the index or clearing all data.

## Core Classes

### FileOfflineStore

The default implementation of `IOfflineStore` that persists encrypted records to the local file system.

```csharp
public sealed class FileOfflineStore : IOfflineStore
{
    public FileOfflineStore(
        string rootPath,
        IEncryptionProvider crypto,
        IIndexProvider? indexer = null);
}
```

**Constructor Parameters:**
- `rootPath`: The root directory where records, attachments, and index files will be stored.
- `crypto`: The encryption provider for encrypting and decrypting data.
- `indexer`: Optional index provider for enabling search capabilities.

**Storage Structure:**
```
/AppData/
 ├── records/          # Encrypted JSON records (.dat files)
 ├── attachments/      # Encrypted binary attachments (.bin files)
 └── index/            # Encrypted search index
```

### AesGcmEncryptionProvider

The default implementation of `IEncryptionProvider` using AES-256-GCM authenticated encryption.

```csharp
public sealed class AesGcmEncryptionProvider : IEncryptionProvider
{
    public AesGcmEncryptionProvider(byte[] masterKey);
}
```

**Constructor Parameters:**
- `masterKey`: A 256-bit (32-byte) encryption key. Typically retrieved from `SecureStorage`.

**Encryption Format:**
- Nonce: 96 bits (12 bytes)
- Authentication tag: 128 bits (16 bytes)
- Output format: `[nonce(12) | ciphertext(variable) | tag(16)]`

### PersistentIndexProvider

A persistent index provider that stores its index to encrypted files on disk.

```csharp
public class PersistentIndexProvider : IIndexProvider
{
    public PersistentIndexProvider(
        string indexDirectory,
        IEncryptionProvider encryptionProvider);
}
```

**Constructor Parameters:**
- `indexDirectory`: The directory where the encrypted index file will be stored.
- `encryptionProvider`: The encryption provider for encrypting the index data.

**Features:**
- In-memory index with automatic persistence on changes
- Survives app restarts
- Full-text search with term frequency scoring
- Filters out very short terms (< 3 characters)
- Thread-safe with semaphore locking

## Data Models

### FileAttachment

Represents a file attachment that can be stored alongside a record.

```csharp
public sealed record FileAttachment(
    string LogicalName,
    string ContentType,
    Stream Content);
```

**Properties:**
- `LogicalName`: The logical file name for the attachment (e.g., "photo.jpg")
- `ContentType`: The MIME type or content type (e.g., "image/jpeg")
- `Content`: The stream containing the attachment data

### SearchResult

Represents a search result with metadata but no data loaded.

```csharp
public sealed record SearchResult(
    string RecordId,
    double Score,
    RecordHeader Header);
```

**Properties:**
- `RecordId`: The unique identifier of the matching record
- `Score`: The relevance score (higher is better)
- `Header`: Metadata including creation timestamp and custom metadata

### SearchResult\<T>

Represents a typed search result with data loaded.

```csharp
public sealed record SearchResult<T>(
    string RecordId,
    double Score,
    RecordHeader Header,
    T Data);
```

**Properties:**
- Same as `SearchResult`, plus:
- `Data`: The deserialised record data

### RecordHeader

Contains metadata about a stored record.

```csharp
public sealed record RecordHeader(
    string Id,
    DateTimeOffset Created,
    IDictionary<string, string>? Metadata = null);
```

**Properties:**
- `Id`: The unique identifier of the record
- `Created`: The timestamp when the record was created
- `Metadata`: Optional custom metadata key-value pairs

### RecordSet\<T>

A queryable collection of records with LINQ-style operations.

```csharp
public sealed class RecordSet<T>
{
    public RecordSet<T> Where(Func<T, bool> predicate);
    public RecordSet<TResult> Select<TResult>(Func<T, TResult> selector);
    public RecordSet<T> OrderBy<TKey>(Func<T, TKey> keySelector);
    public RecordSet<T> OrderByDescending<TKey>(Func<T, TKey> keySelector);
    public RecordSet<T> Skip(int count);
    public RecordSet<T> Take(int count);
    public T First();
    public T? FirstOrDefault();
    public T Single();
    public T? SingleOrDefault();
    public bool Any(Func<T, bool> predicate);
    public bool Any();
    public bool All(Func<T, bool> predicate);
    public int Count();
    public int Count(Func<T, bool> predicate);
    public List<T> ToList();
    public T[] ToArray();
    public IEnumerable<T> AsEnumerable();
}
```

Provides deferred execution and composable queries over search results.

## Extension Methods

### OfflineStoreExtensions

```csharp
public static class OfflineStoreExtensions
{
    // Find records matching multiple search terms (OR operation)
    public static Task<RecordSet<T>> FindManyAsync<T>(
        this IOfflineStore store,
        params string[] terms);

    // Apply predicate filter to RecordSet
    public static RecordSet<T> WhereMatch<T>(
        this RecordSet<T> source,
        Func<T, bool> predicate);

    // Apply predicate filter to IEnumerable
    public static IEnumerable<T> WhereMatch<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate);

    // Find and filter in one operation
    public static Task<RecordSet<T>> FindWhereAsync<T>(
        this IOfflineStore store,
        Func<T, bool> predicate,
        params string[] terms);
}
```

These extensions enable fluent, LINQ-style querying over search results.

**Example:**
```csharp
var mathsLessons = await store
    .FindManyAsync<LessonRecord>("maths", "Dylan", "Jessica")
    .WhereMatch(l => l.Subject == "Maths")
    .OrderBy(l => l.Date)
    .ToList();
```

## Usage Examples

### Basic Setup

```csharp
using Plugin.Maui.OfflineData;
using Plugin.Maui.OfflineData.Index;
using Plugin.Maui.OfflineData.Security;

// Generate or retrieve a master key
var masterKey = new byte[32];
RandomNumberGenerator.Fill(masterKey);

// Create components
var encryption = new AesGcmEncryptionProvider(masterKey);
var index = new PersistentIndexProvider(FileSystem.AppDataDirectory, encryption);
var store = new FileOfflineStore(
    FileSystem.AppDataDirectory,
    encryption,
    index);
```

### Saving Data

```csharp
// Save a single record
await store.SaveAsync("lesson-001", new LessonRecord
{
    Subject = "Science",
    Date = DateTimeOffset.Now,
    Description = "We observed seagulls at the beach"
});

// Save with attachments
var photoStream = File.OpenRead("photo.jpg");
await store.SaveAsync("lesson-002", lessonData, new[]
{
    new FileAttachment("photo.jpg", "image/jpeg", photoStream)
});

// Save aggregate (multiple records in one file)
await store.SaveAsync("lessons-2025", new List<LessonRecord>
{
    lesson1,
    lesson2,
    lesson3
});
```

### Loading Data

```csharp
// Load a single record
var lesson = await store.LoadAsync<LessonRecord>("lesson-001");

// Load aggregate
var lessons = await store.LoadAsync<List<LessonRecord>>("lessons-2025");
```

### Searching

```csharp
// Simple search
var results = await store.FindAsync<LessonRecord>("seagulls");
foreach (var result in results)
{
    Console.WriteLine($"{result.RecordId}: {result.Score}");
    Console.WriteLine($"Data: {result.Data.Description}");
}

// Multi-term search with filtering
var mathsLessons = await store
    .FindManyAsync<LessonRecord>("maths", "science", "experiment")
    .WhereMatch(l => l.Subject == "Maths")
    .OrderByDescending(l => l.Date)
    .Take(10)
    .ToList();
```

### Deleting Data

```csharp
await store.DeleteAsync("lesson-001");
```

## Extensibility Points

You can replace any core component with your own implementation:

### Custom Encryption Provider

```csharp
public class CustomEncryptionProvider : IEncryptionProvider
{
    public Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string context)
    {
        // Your encryption logic (e.g., XChaCha20-Poly1305)
    }

    public Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> ciphertext, string context)
    {
        // Your decryption logic
    }
}
```

### Custom Index Provider

```csharp
public class LuceneIndexProvider : IIndexProvider
{
    public Task IndexAsync(string id, string content, IDictionary<string, string> metadata)
    {
        // Lucene.NET indexing logic
    }

    public Task<IEnumerable<SearchResult>> QueryAsync(string query)
    {
        // Lucene.NET query logic
    }

    public Task ClearAsync()
    {
        // Clear Lucene index
    }
}
```

### Custom Serialisation

While not currently exposed as an interface, you can fork `FileOfflineStore` and modify the `JsonSerializerOptions` or replace JSON with MessagePack, Protocol Buffers, etc.

## Best Practices

1. **Key Management**: Store your master key in `SecureStorage`, never hardcode it.
2. **Error Handling**: Wrap crypto operations in try-catch to handle `CryptographicException`.
3. **Aggregate Files**: Use aggregate storage patterns (multiple records per file) for better performance.
4. **Lazy Loading**: Load attachments only when needed to reduce memory usage.
5. **Index Maintenance**: Let the store handle indexing automatically; avoid manual index updates.
6. **Thread Safety**: All core operations are async and thread-safe, but avoid concurrent saves to the same record ID.

## Performance Considerations

- **Write Performance**: Dominated by file I/O, not encryption. Use aggregate files for bulk operations.
- **Read Performance**: Sub-millisecond for individual records.
- **Search Performance**: 1-2 ms for thousands of records.
- **Cold Start**: Index loads in 4-40 ms depending on size.
- **Memory**: Minimal; only active records are held in memory.

## Security Notes

- All data is encrypted at rest with AES-256-GCM.
- Authentication tags prevent tampering or corruption.
- Master keys should be stored in platform `SecureStorage`.
- No plaintext ever touches disk, even temporarily.
- Atomic writes prevent partial data exposure.

## Migration and Versioning

Currently, there is no built-in migration system. When updating data structures:

1. Version your record types explicitly (e.g., add a `Version` property).
2. Handle deserialisation gracefully with nullable properties.
3. Consider keeping old and new structures in separate files during transitions.

Future versions may include migration hooks or schema evolution support.
