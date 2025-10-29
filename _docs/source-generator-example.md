# Source Generator Example

This example demonstrates how to use the Cabinet source generator for AOT-compatible applications.

## Basic Usage

### 1. Define Your Record Types

```csharp
using Cabinet;

namespace MyApp.Models;

[AotRecord]
public class LessonRecord
{
    public string LessonRecordId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
}

[AotRecord]
public class ChildRecord
{
    public string ChildRecordId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}
```

### 2. Use the Generated Code

The source generator automatically creates:
- JSON serialization context
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
var store = CabinetStoreExtensions.CreateCabinetStore(
    FileSystem.AppDataDirectory,
    masterKey);

// Create RecordSets using generated extensions
var lessons = store.CreateRecordSet<LessonRecord>();
var children = store.CreateRecordSet<ChildRecord>();

// Load data
await lessons.LoadAsync();
await children.LoadAsync();

// Use as normal
await lessons.AddAsync(new LessonRecord
{
    LessonRecordId = "lesson-001",
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

### 1. JSON Serialization Context
```csharp
// CabinetJsonSerializerContext.g.cs
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSerializable(typeof(ChildRecord))]
[JsonSerializable(typeof(List<ChildRecord>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CabinetJsonSerializerContext : JsonSerializerContext
{
}
```

### 2. RecordSet Extensions
```csharp
// LessonRecordExtensions.g.cs
public static class LessonRecordExtensions
{
    public static RecordSetOptions<LessonRecord> CreateRecordSetOptions()
        => new()
        {
            IdSelector = record => record.LessonRecordId
        };
    
    public static RecordSet<LessonRecord> CreateRecordSet(this IOfflineStore store)
        => new(store, CreateRecordSetOptions());
}
```

### 3. Store Extensions
```csharp
// CabinetStoreExtensions.g.cs
public static class CabinetStoreExtensions
{
    public static IOfflineStore CreateCabinetStore(
        string dataDirectory,
        byte[] masterKey)
    {
        var encryptionProvider = new AesGcmEncryptionProvider(masterKey);
        return new FileOfflineStore(
            dataDirectory,
            encryptionProvider,
            new JsonSerializerOptions
            {
                TypeInfoResolver = CabinetJsonSerializerContext.Default
            });
    }
}
```

## Benefits

### Before (Manual Configuration)

```csharp
// 1. Create JSON context manually
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSourceGenerationOptions(...)]
public partial class MyJsonContext : JsonSerializerContext { }

// 2. Configure RecordSet with IdSelector
var lessons = new RecordSet<LessonRecord>(store, new RecordSetOptions<LessonRecord>
{
    IdSelector = lesson => lesson.LessonRecordId
});

// 3. Easy to forget List<T> serialization
// 4. Easy to miss IdSelector configuration
```

### After (Source Generator)

```csharp
// 1. Just add the attribute
[AotRecord]
public class LessonRecord { ... }

// 2. Use the generated extensions
var lessons = store.CreateRecordSet<LessonRecord>();

// Everything else is automatic! ✨
```

## AOT Compilation

The source generator ensures your application is fully AOT-compatible by:

1. **Generating all JSON serialization metadata at compile time**
   - No runtime reflection needed
   - All types registered, including `List<T>`

2. **Creating type-safe IdSelectors**
   - No runtime property name lookup
   - Compile-time verification

3. **Providing a consistent, validated setup**
   - No missing registrations
   - No runtime errors

You can now publish your application with `PublishAot=true`!

## Troubleshooting

### "No ID property found"

The generator couldn't find a suitable ID property. Solutions:
1. Add a property named `Id`
2. Add a property named `{TypeName}Id` (e.g., `LessonRecordId` for `LessonRecord`)
3. Use explicit specification: `[AotRecord(IdPropertyName = "YourPropertyName")]`

### "Multiple potential ID properties"

Your class has both `Id` and `{TypeName}Id` properties. Solution:
- Use explicit specification to choose which one: `[AotRecord(IdPropertyName = "Id")]`

### Generated code not appearing

1. Rebuild the solution
2. Check that the attribute is in the `Cabinet` namespace
3. Verify the class is public and not nested
