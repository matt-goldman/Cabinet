# Manual AOT Setup (Without Source Generator)

If you prefer full control over RecordSet configuration, or want to understand how Cabinet works under the hood, you can skip the source generator and configure everything manually.

## When to Use Manual Setup

- **Learning:** Understand exactly how Cabinet works without generated abstractions
- **Custom configuration:** Need specialized IdSelectors or RecordSet options
- **Build-time control:** Prefer explicit code over generated code
- **No attributes:** Don't want `[AotRecord]` attributes on your models

**Note:** The source generator now supports any accessibility level (public, internal, private). You don't need manual setup just for internal types anymore.

## Understanding What the Generator Does

The source generator **only creates convenience methods**. It does NOT generate `JsonSerializerContext` - you always create that manually.

| You Create | With Source Generator | Manual Setup |
|------------|----------------------|--------------|
| `JsonSerializerContext` | ✅ Manual | ✅ Manual |
| RecordSet extensions | ✅ Auto-generated | ❌ Write yourself |
| `IdSelector` configuration | ✅ Auto-generated | ❌ Write yourself |
| `CreateCabinetStore` helper | ✅ Auto-generated | ❌ Write yourself |

**Bottom line:** Manual setup means you write 10-20 lines of boilerplate that the generator would create for you.

## Complete Example

### 1. Define Your Records

Your records can have any accessibility:

```csharp
namespace MyApp.Models;

// Public, internal, private - all work!
internal record LessonRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

internal record StudentRecord
{
    public int StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}
```

### 2. Create JsonSerializerContext

Match the accessibility to your records:

```csharp
using System.Text.Json.Serialization;
using MyApp.Models;

namespace MyApp;

// Internal context for internal types
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSerializable(typeof(StudentRecord))]
[JsonSerializable(typeof(List<StudentRecord>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class MyJsonContext : JsonSerializerContext
{
}
```

**This is identical to what you'd create WITH the source generator** - the context is always manually created.

### 3. Manually Configure Cabinet Store

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using Cabinet.Core;
using Cabinet.Security;
using Cabinet.Index;
using MyApp.Models;

namespace MyApp;

public class DataService
{
    private readonly IOfflineStore _store;
    private readonly RecordSet<LessonRecord> _lessons;
    private readonly RecordSet<StudentRecord> _students;

    public DataService(string dataDirectory, byte[] masterKey)
    {
        // 1. Create encryption provider
        var encryption = new AesGcmEncryptionProvider(masterKey);
        
        // 2. Create index provider
        var indexer = new PersistentIndexProvider(dataDirectory, encryption);
        
        // 3. Create JSON options with your context
        var jsonOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = MyJsonContext.Default
        };
        
        // 4. Create the store
        _store = new FileOfflineStore(
            dataDirectory,
            encryption,
            jsonOptions,
            indexer);
        
        // 5. Create RecordSets with manual IdSelectors
        _lessons = new RecordSet<LessonRecord>(_store, new RecordSetOptions<LessonRecord>
        {
            IdSelector = lesson => lesson.Id.ToString()
        });
        
        _students = new RecordSet<StudentRecord>(_store, new RecordSetOptions<StudentRecord>
        {
            IdSelector = student => student.StudentId.ToString()
        });
    }
    
    public RecordSet<LessonRecord> Lessons => _lessons;
    public RecordSet<StudentRecord> Students => _students;
}
```

### 4. Use in Your Application

```csharp
// In MauiProgram.cs or your DI setup
builder.Services.AddSingleton<DataService>(sp =>
{
    // Get or create master key
    var keyString = SecureStorage.GetAsync("MasterKey").GetAwaiter().GetResult();
    byte[] masterKey;
    
    if (keyString == null)
    {
        masterKey = new byte[32];
        RandomNumberGenerator.Fill(masterKey);
        SecureStorage.SetAsync("MasterKey", Convert.ToBase64String(masterKey))
            .GetAwaiter().GetResult();
    }
    else
    {
        masterKey = Convert.FromBase64String(keyString);
    }
    
    var dataDir = Path.Combine(FileSystem.AppDataDirectory, "Data");
    return new DataService(dataDir, masterKey);
});

// Then in your ViewModels/Services:
public class MyViewModel
{
    private readonly DataService _data;
    
    public MyViewModel(DataService data)
    {
        _data = data;
    }
    
    public async Task LoadDataAsync()
    {
        await _data.Lessons.LoadAsync();
        await _data.Students.LoadAsync();
        
        var allLessons = await _data.Lessons.GetAllAsync();
        var searchResults = await _data.Lessons.FindAsync("science");
    }
    
    public async Task SaveLessonAsync(LessonRecord lesson)
    {
        await _data.Lessons.AddAsync(lesson);
    }
}
```

## Comparison: Manual vs Source Generator

### The JsonSerializerContext (IDENTICAL IN BOTH)

```csharp
// WITH SOURCE GENERATOR
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
internal partial class CabinetJsonContext : JsonSerializerContext { }

// MANUAL SETUP  
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
internal partial class MyJsonContext : JsonSerializerContext { }

// ☝️ These are the same! Just different names.
// The source generator NEVER creates this - you always write it manually.
```

[JsonSerializable(typeof(List<LessonRecord>))]
internal partial class MyJsonContext : JsonSerializerContext { }

```

**Difference:** `public` vs `internal`. That's it. Everything else is identical.

#### Store Creation

```csharp
// WITH SOURCE GENERATOR (auto-generated method)
var store = CabinetStoreExtensions.CreateCabinetStore(
    dataDir, masterKey, CabinetJsonContext.Default);

// MANUAL SETUP (you write this)
var encryption = new AesGcmEncryptionProvider(masterKey);
var indexer = new PersistentIndexProvider(dataDir, encryption);
var jsonOptions = new JsonSerializerOptions 
{ 
    TypeInfoResolver = MyJsonContext.Default 
};
var store = new FileOfflineStore(dataDir, encryption, jsonOptions, indexer);
```

**Difference:** Generator creates a helper method. Manual setup, you wire it yourself.

#### RecordSet Creation

```csharp
// WITH SOURCE GENERATOR (auto-generated method)
var lessons = store.CreateLessonRecordRecordSet();

// MANUAL SETUP (you write this)
var lessons = new RecordSet<LessonRecord>(store, new RecordSetOptions<LessonRecord>
{
    IdSelector = lesson => lesson.Id.ToString()
});
```

**Difference:** Generator creates extensions with IdSelector. Manual setup, you write it.

### Summary Table

| Aspect | Manual Setup | Source Generator |
|--------|-------------|------------------|
| **Type accessibility** | ✅ Any (public, internal, private) | ✅ Any (public, internal, private) |
| **JsonSerializerContext** | ✅ You create (always) | ✅ You create (always) |
| **Context accessibility** | ✅ Any accessibility | ✅ Any accessibility |
| **Setup code** | ❌ More verbose (wire everything) | ✅ Minimal (use generated helpers) |
| **IdSelector** | ❌ Manual configuration | ✅ Auto-generated |
| **Store creation** | ❌ Manual wiring | ✅ Generated helper method |
| **AOT compatible** | ✅ Yes | ✅ Yes |
| **Best for** | Full control, understanding internals | Less boilerplate, convention-based |

## When to Use Manual Setup

✅ **Use manual setup when:**

- You want to understand exactly how Cabinet works
- You need custom RecordSetOptions or IdSelectors
- You prefer explicit code over generated code
- You're integrating with existing data layer abstractions

✅ **Use source generator when:**

- You want minimal boilerplate
- You prefer convention over configuration
- You're starting a new project
- Standard IdSelector (ToString on Id property) works for you

## AOT Publication

Both approaches are fully AOT compatible. To publish with AOT:

```xml
<!-- In your .csproj -->
<PropertyGroup>
    <PublishAot>true</PublishAot>
</PropertyGroup>
```

```bash
dotnet publish -c Release -r android-arm64
dotnet publish -c Release -r ios-arm64
```

Your manually-configured internal types will work perfectly with AOT as long as you've created the `JsonSerializerContext` with all the `[JsonSerializable]` attributes.

## Full Control Pattern

If you need even more control, you can also manually implement indexing:

```csharp
var customIndexer = new PersistentIndexProvider(
    dataDirectory,
    encryption,
    new IndexOptions 
    {
        // Custom tokenization, stop words, etc.
    });

var store = new FileOfflineStore(
    dataDirectory,
    encryption,
    jsonOptions,
    customIndexer);
```

Or skip indexing entirely:

```csharp
var store = new FileOfflineStore(
    dataDirectory,
    encryption,
    jsonOptions,
    indexer: null);  // No search capabilities
```

## Summary

- ✅ You CAN use AOT with internal/private types by skipping the source generator
- ✅ Manually create `JsonSerializerContext`, `FileOfflineStore`, and `RecordSet` instances
- ✅ Full flexibility and control
- ❌ More boilerplate code
- ❌ No generated convenience methods

The source generator is optional - it just reduces boilerplate when your types happen to be public anyway.
