# `RecordSet<T>` Usage Guide

## AOT Compatibility

For AOT (Ahead-of-Time) compilation compatibility, Cabinet requires two things:

### 1. ID Selector Function

Always provide an `IdSelector` in `RecordSetOptions<T>` to avoid reflection:

```csharp
var options = new RecordSetOptions<LessonRecord>
{
    IdSelector = lesson => lesson.LessonId  // AOT-safe
};

var lessons = new RecordSet<LessonRecord>(store, options);
```

**Without IdSelector** (not AOT-compatible):

- RecordSet falls back to reflection to discover ID properties
- Looks for: `Id` → `{TypeName}Id` → throws exception
- Works in JIT mode but will fail in AOT scenarios

### 2. JSON Source Generation

For AOT scenarios, you must use System.Text.Json source generators. Add the `[JsonSerializable]` attribute to a context class:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]  // Important: RecordSet uses List<T> internally
[JsonSerializable(typeof(ChildRecord))]
[JsonSerializable(typeof(List<ChildRecord>))]
[JsonSerializable(typeof(SubjectRecord))]
[JsonSerializable(typeof(List<SubjectRecord>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
```

Then pass it to FileOfflineStore:

```csharp
var store = new FileOfflineStore(
    FileSystem.AppDataDirectory,
    encryptionProvider,
    new JsonSerializerOptions
    {
        TypeInfoResolver = AppJsonSerializerContext.Default
    });
```

## Complete AOT-Safe Example

```csharp
// 1. Define your JSON serialization context
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }

// 2. Create store with source-generated serialization
var encryptionProvider = new AesGcmEncryptionProvider(masterKey);
var store = new FileOfflineStore(
    FileSystem.AppDataDirectory,
    encryptionProvider,
    new JsonSerializerOptions
    {
        TypeInfoResolver = AppJsonSerializerContext.Default
    });

// 3. Create RecordSet with IdSelector
var options = new RecordSetOptions<LessonRecord>
{
    IdSelector = lesson => lesson.LessonId
};
var lessons = new RecordSet<LessonRecord>(store, options);

// 4. Use normally
await lessons.LoadAsync();
await lessons.AddAsync(new LessonRecord { LessonId = "123", Title = "Seagulls" });
var allLessons = await lessons.GetAllAsync();
```

## Why This Matters

- **Reflection**: Not available in AOT-compiled apps
- **Source generators**: Generate code at compile time, work in AOT
- **RecordSet internal storage**: Uses `List<T>`, so you must register both `T` and `List<T>`

## Development vs Production

**During development** (JIT mode):

- Reflection-based ID discovery works
- Default JSON serialization works
- No special configuration needed

**For production** (.NET MAUI Release builds with AOT):

- Must use `IdSelector`
- Must use JSON source generation
- Add `[JsonSerializable]` for all record types and their `List<T>` variants

## See Also

- [Microsoft Docs: JSON source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [.NET MAUI AOT](https://learn.microsoft.com/en-us/dotnet/maui/deployment/nativeaot)
