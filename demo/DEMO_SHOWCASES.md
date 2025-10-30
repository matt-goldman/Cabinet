# Cabinet Demo App - Feature Showcase

This demo app showcases **all** Cabinet best practices and features. It serves as a reference implementation for developers learning to use Cabinet.

## 🎯 What This Demo Demonstrates

### 1. **Source Generator Usage (`[AotRecord]`)**

Both record types use the `[AotRecord]` attribute to enable source-generated RecordSet extensions:

```csharp
[AotRecord]
public class LessonRecord { ... }

[AotRecord]
public class StudentRecord { ... }
```

**Generated extensions:**

- `store.CreateLessonRecordRecordSet()` - Type-safe RecordSet creation
- `store.CreateStudentRecordRecordSet()` - Type-safe RecordSet creation  
- `CabinetStoreExtensions.CreateCabinetStore()` - Store creation with all dependencies

### 2. **Aggregated File Stores**

The `OfflineDataService` demonstrates the recommended pattern: **one `IOfflineStore` instance serving multiple `RecordSet<T>` instances**.

```csharp
public class OfflineDataService
{
    private readonly IOfflineStore _store;  // Single store
    private readonly RecordSet<LessonRecord> _lessons;    // Type-safe access to lessons
    private readonly RecordSet<StudentRecord> _students;  // Type-safe access to students

    public OfflineDataService(IOfflineStore store)
    {
        _store = store;
        
        // Use source-generated extensions for type-safe RecordSet creation
        _lessons = store.CreateLessonRecordRecordSet();
        _students = store.CreateStudentRecordRecordSet();
    }
}
```

**Benefits:**

- All records stored in same directory structure
- Unified encryption and indexing
- Easy unified search across all record types
- Reduced memory overhead (single index, single encryption provider)

### 3. **RecordSet Pattern**

The demo uses `RecordSet<T>` for all CRUD operations, showcasing the recommended high-level API:

```csharp
// Add records
await _lessons.AddAsync(lesson);
await _students.AddAsync(student);

// Search across both types
var lessonResults = await _lessons.FindAsync("seagulls");
var studentResults = await _students.FindAsync("Alice");

// Get counts
int lessonCount = _lessons.Count();
int studentCount = _students.Count();
```

**Why RecordSet?**

- High-level, domain-oriented API
- Automatic persistence
- In-memory caching for fast queries  
- LINQ-style querying
- Type-safe operations

### 4. **Multiple Record Types**

Two distinct record types demonstrate real-world multi-entity scenarios:

**LessonRecord:**

- `Guid Id` property (demonstrates `.ToString()!` in generated code)
- `DateOnly`, `List<string>` fields
- `List<FileAttachment>? Attachments` collection

**StudentRecord:**

- `string Id` property
- Standard data fields (Name, Age, Grade, etc.)
- `FileAttachment? ProfilePhoto` - attachment as property
- `string? CertificateBase64` - custom encoding example

### 5. **Attachment Patterns**

The demo showcases **THREE** different attachment handling patterns:

#### Pattern 1: FileAttachment Collection Property (LessonRecord)

```csharp
lesson.Attachments = [new FileAttachment("photo.jpg", "image/jpeg", photoStream)];
await _lessons.AddAsync(lesson);
```

**Use when:** You want Cabinet to automatically serialize attachments with the record.

#### Pattern 2: FileAttachment as Record Property (StudentRecord)

```csharp
student.ProfilePhoto = new FileAttachment("profile.jpg", "image/jpeg", photoStream);
await _students.AddAsync(student);
```

**Use when:** The attachment is a single, well-defined property of the entity.

#### Pattern 3: Custom Base64 Encoding (StudentRecord)

```csharp
var certBytes = Encoding.UTF8.GetBytes($"CERTIFICATE:{name}");
student.CertificateBase64 = Convert.ToBase64String(certBytes);
await _students.AddAsync(student);
```

**Use when:** You need full control over encoding/decoding or want to optimize storage format.

### 6. **AOT Compatibility**

The demo is fully AOT-compatible:

1. **Manual JsonSerializerContext** (`CabinetJsonContext.cs`):

   ```csharp
   [JsonSerializable(typeof(LessonRecord))]
   [JsonSerializable(typeof(List<LessonRecord>))]
   [JsonSerializable(typeof(StudentRecord))]
   [JsonSerializable(typeof(List<StudentRecord>))]
   public partial class CabinetJsonContext : JsonSerializerContext { }
   ```

2. **Source Generator**: Creates RecordSet extensions with explicit IdSelector (no reflection at runtime)

3. **No Dynamic Code**: All serialization and record access uses compile-time generated code

### 7. **Unified Search Across Record Types**

The `SearchRecordsAsync` method demonstrates searching across multiple record types:

```csharp
// Search both types
var lessonResults = await _lessons.FindAsync(query);
var studentResults = await _students.FindAsync(query);

// Combine and display
var combined = lessonResults
    .Select(l => (Type: "Lesson", Title: l.Subject, Details: l.Description))
    .Concat(studentResults.Select(s => (Type: "Student", Title: s.Name, Details: $"Age {s.Age}")));
```

**Demonstrates:**

- Same index searches all record types
- Each RecordSet provides typed results
- Easy to combine and present unified results

### 8. **Cross-Platform Data Directory**

Uses `.NET MAUI`'s `FileSystem.AppDataDirectory` for cross-platform compatibility:

```csharp
var store = CabinetStoreExtensions.CreateCabinetStore(
    FileSystem.AppDataDirectory,
    masterKey,
    jsonContext);
```

Works on Android, iOS, Windows, macOS, and Catalyst without changes.

### 9. **Secure Key Storage**

Master encryption key stored using platform `SecureStorage`:

```csharp
var masterKey = await SecureStorage.GetAsync("Cabinet_MasterKey");
if (string.IsNullOrEmpty(masterKey))
{
    masterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    await SecureStorage.SetAsync("Cabinet_MasterKey", masterKey);
}
```

**Security:**

- Uses platform keychain/keystore
- Never stored in plain text
- Separate key per installation

## 🚀 How to Run

1. Open `Cabinet.slnx` in Visual Studio 2022 or JetBrains Rider
2. Set `demo` as the startup project
3. Select your target platform (Windows, Android, iOS, etc.)
4. Run the app
5. Try:
   - **Generate Records**: Creates mix of LessonRecord and StudentRecord
   - **Include Attachments**: Demonstrates all three attachment patterns
   - **Search**: Unified search across both record types
   - **Purge Data**: Clean up all data

## 📊 What Gets Generated

When you run "Generate 10 Records with Attachments", you get:

- **6 LessonRecords** (60%)
  - Each with `Guid` ID
  - Photo attachment in `Attachments` collection
  - Searchable subject, description, and tags

- **4 StudentRecords** (40%)
  - Each with `string` ID  
  - ProfilePhoto as FileAttachment property
  - Base64-encoded certificate
  - Searchable name, grade, subjects

All data is **encrypted at rest** with **AES-256-GCM**.

## 💡 Key Takeaways

1. **Use `RecordSet<T>` for high-level access** - Simplest, most maintainable API
2. **One store, multiple RecordSets** - Aggregated store pattern is recommended
3. **Source generator for AOT** - `[AotRecord]` + manual `JsonSerializerContext`
4. **Three attachment patterns** - Choose based on your needs:
   - Collection property: Multiple attachments
   - Single property: Well-defined attachment
   - Custom encoding: Full control
5. **Unified search** - Single encrypted index searches all record types
6. **Cross-platform by design** - Works everywhere .NET MAUI runs

## 📚 Next Steps

- Read `_docs/source-generator-usage.md` for detailed generator documentation
- Check `_docs/Specification.md` for complete Cabinet API reference
- Explore `_docs/RecordSet.md` for `RecordSet<T>` deep dive
- See `_docs/adr/0001-source-generator-accessibility-strategy.md` for design decisions

---

**This demo represents the recommended way to use Cabinet in production applications.**
