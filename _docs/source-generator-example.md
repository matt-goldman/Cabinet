# Source Generator Example

This example demonstrates how to use the Cabinet source generator for AOT-compatible applications.

## Prerequisites

**⚠️ Critical Requirements:**

1. You must create a `JsonSerializerContext` in your project. The source generator cannot create this for you due to source generator coordination limitations.
2. **All record types MUST be public**. This is a C# language requirement - `JsonSerializerContext` must be public for AOT, which requires all serialized types to also be public.

## Step 1: Create Your JsonSerializerContext

Create a file (e.g., `CabinetJsonContext.cs`) in your project:

```csharp
using System.Text.Json.Serialization;
using MyApp.Models;  // Your models namespace

namespace MyApp;

/// <summary>
/// AOT-safe JSON serialization context for Cabinet.
/// Add [JsonSerializable] for each record type you use with Cabinet.
/// </summary>
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSerializable(typeof(ChildRecord))]
[JsonSerializable(typeof(List<ChildRecord>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CabinetJsonContext : JsonSerializerContext
{
}
```

> **Why is this manual?** Source generators can't reliably chain with System.Text.Json's generator in the same compilation pass. By creating this in your own code, System.Text.Json can properly implement the abstract members.

## Step 2: Define Your Record Types

**Important:** Records must be public to work with AOT compilation.

```csharp
using Cabinet;

namespace MyApp.Models;

[AotRecord]
public record LessonRecord  // Must be public
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
}

[AotRecord]
public record ChildRecord  // Must be public
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}
```

## Step 3: Use the Generated Code

The source generator automatically creates:

- RecordSet extension methods  
- Store helper methods

```csharp
using Cabinet.Generated;
using System.Security.Cryptography;

// Generate or retrieve a master key
var masterKey = new byte[32];
RandomNumberGenerator.Fill(masterKey);
// In production, store this in SecureStorage

// Create the store using the generated helper
// 👇 Pass your JsonSerializerContext here
var store = CabinetStoreExtensions.CreateCabinetStore(
    FileSystem.AppDataDirectory,
    masterKey,
    CabinetJsonContext.Default);

// Create RecordSets using generated extensions
var lessons = store.CreateLessonRecordRecordSet();
var children = store.CreateChildRecordRecordSet();

// Load data
await lessons.LoadAsync();
await children.LoadAsync();

// Use as normal
await lessons.AddAsync(new LessonRecord
{
    Id = "lesson-001",
    Title = "Beach Day",
    Subject = "Science",
    Date = DateTime.Today,
    Description = "Observed seagulls at the beach"
});

var allLessons = await lessons.GetAllAsync();
```

## ID Property Detection

The generator automatically finds your ID property using these rules:

### Rule 1: Property named "Id"

```csharp
[AotRecord]
public class SimpleRecord
{
    public string Id { get; set; } = string.Empty;  // ✓ Found automatically
}
```

### Rule 2: Property named "{TypeName}Id"

```csharp
[AotRecord]
public class LessonRecord
{
    public string LessonRecordId { get; set; } = string.Empty;  // ✓ Found automatically
}
```

### Rule 3: Explicit specification

```csharp
[AotRecord(IdPropertyName = "CustomIdentifier")]
public class CustomRecord
{
    public string CustomIdentifier { get; set; } = string.Empty;  // ✓ Explicitly specified
}
```

## What Gets Generated

For each `[AotRecord]` class, the generator creates:

### 1. RecordSet Extensions

```csharp
// LessonRecordExtensions.g.cs
public static class LessonRecordExtensions
{
    public static RecordSetOptions<LessonRecord> CreateRecordSetOptions()
        => new()
        {
            IdSelector = record => record.Id.ToString()!
        };
    
    public static RecordSet<LessonRecord> CreateRecordSet(this IOfflineStore store)
        => new(store, CreateRecordSetOptions());
}
```

### 2. Store Extensions

```csharp
// CabinetStoreExtensions.g.cs
public static class CabinetStoreExtensions
{
    public static IOfflineStore CreateCabinetStore(
        string dataDirectory,
        byte[] masterKey,
        JsonSerializerContext jsonContext)
    {
        var encryptionProvider = new AesGcmEncryptionProvider(masterKey);
        var indexProvider = new PersistentIndexProvider(dataDirectory, encryptionProvider);
        return new FileOfflineStore(
            dataDirectory,
            encryptionProvider,
            jsonContext.Options,
            indexProvider);
    }
    
    public static RecordSet<LessonRecord> CreateLessonRecordRecordSet(this IOfflineStore store)
        => new(store, LessonRecordExtensions.CreateRecordSetOptions());
}
```

**Note:** The generator automatically calls `.ToString()!` on ID properties of any type (Guid, int, string, etc.) to ensure compatibility with `RecordSetOptions<T>.IdSelector` which requires a string.

## Benefits

### Before (Manual Configuration)

```csharp
// 1. Create JSON context manually
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSourceGenerationOptions(...)]
public partial class MyJsonContext : JsonSerializerContext { }

// 2. Configure store manually
var encryption = new AesGcmEncryptionProvider(masterKey);
var indexer = new PersistentIndexProvider(dataDirectory, encryption);
var store = new FileOfflineStore(dataDirectory, encryption, jsonOptions, indexer);

// 3. Configure RecordSet with IdSelector
var lessons = new RecordSet<LessonRecord>(store, new RecordSetOptions<LessonRecord>
{
    IdSelector = lesson => lesson.Id.ToString()  // Easy to forget!
});
```

### After (Source Generator)

```csharp
// 1. Create JSON context once (still manual, but required for AOT)
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
public partial class CabinetJsonContext : JsonSerializerContext { }

// 2. Just add the attribute to your records
[AotRecord]
public record LessonRecord { ... }

// 3. Use the generated extensions - everything is automatic!
var store = CabinetStoreExtensions.CreateCabinetStore(
    dataDirectory, masterKey, CabinetJsonContext.Default);
var lessons = store.CreateLessonRecordRecordSet();

// ✨ IdSelector, indexing, and encryption all configured automatically!
```

## AOT Compilation

The combination of your manual `JsonSerializerContext` and the Cabinet source generator ensures your application is fully AOT-compatible:

1. **System.Text.Json handles JSON serialization metadata at compile time**
   - Your `JsonSerializerContext` is processed by System.Text.Json's generator
   - No runtime reflection needed for JSON operations
   - All types registered, including `List<T>`

2. **Cabinet generator creates type-safe extensions**
   - No runtime property name lookup
   - Compile-time verification of ID properties
   - Automatic `.ToString()` conversion for non-string IDs

3. **Provides a consistent, validated setup**
   - No missing registrations
   - No runtime errors
   - Store creation with all dependencies configured

You can now publish your application with `PublishAot=true`!

## Troubleshooting

### "CABINET001: AotRecord type must be public"

Your record is not public. Change it from `internal`, `private`, or `protected` to `public`:

```csharp
// ❌ This causes an error
[AotRecord]
internal record MyRecord { ... }

// ✓ This works
[AotRecord]
public record MyRecord { ... }
```

**Why:** `JsonSerializerContext` must be public for AOT. C# requires all types referenced by public members to also be public.

### "CS0534: does not implement inherited abstract member"

Your `JsonSerializerContext` wasn't processed by System.Text.Json's generator. Verify:

1. The context is in your own code file (not generated)
2. It has the `partial` keyword
3. It has `[JsonSerializable]` attributes for all your record types
4. You've rebuilt the project from scratch

### "No ID property found"

The generator couldn't find a suitable ID property. Solutions:

1. Add a property named `Id`
2. Add a property named `{TypeName}Id` (e.g., `LessonRecordId` for `LessonRecord`)
3. Use explicit specification: `[AotRecord(IdPropertyName = "YourPropertyName")]`

### "Multiple potential ID properties"

Your class has both `Id` and `{TypeName}Id` properties. Solution:

- Use explicit specification to choose which one: `[AotRecord(IdPropertyName = "Id")]`

### Generated code not appearing

1. Rebuild the solution (clean build recommended)
2. Check that the `[AotRecord]` attribute is from the `Cabinet` namespace
3. Verify the class is public and not nested
4. Ensure the source generator is referenced with `OutputItemType="Analyzer"` in your `.csproj`
