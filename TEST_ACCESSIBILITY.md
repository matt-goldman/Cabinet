# Source Generator Accessibility Test Results

## Summary

The source generator now correctly honors the accessibility modifiers of record classes. Here's what it does:

### Individual RecordSet Extensions

Each record class gets its own extension class with **matching accessibility**:

- Public record → public extension class
- Internal record → internal extension class

### CabinetStoreExtensions  

The shared `CabinetStoreExtensions` class uses the **most restrictive accessibility**:

- If ANY record is internal → `CabinetStoreExtensions` is internal
- If ALL records are public → `CabinetStoreExtensions` is public

This ensures `CreateCabinetStore` can accept the matching `JsonSerializerContext` without accessibility conflicts.

## Test Results

✅ **All public records:** Generates public `CabinetStoreExtensions`
✅ **All internal records:** Generates internal `CabinetStoreExtensions`
✅ **Mixed public + internal:** Generates internal `CabinetStoreExtensions` (most restrictive)

## Example: All Public

```csharp
[AotRecord]
public class PublicRecord
{
    public string Id { get; set; }
}
```

**Generates:**

```csharp
public static class PublicRecordExtensions { ... }
public static class CabinetStoreExtensions
{
    public static IOfflineStore CreateCabinetStore(..., JsonSerializerContext jsonContext)
}
```

**Works with:**

```csharp
public partial class CabinetJsonContext : JsonSerializerContext
{
    [JsonSerializable(typeof(PublicRecord))]
}
```

## Example: All Internal

```csharp
[AotRecord]
internal class InternalRecord
{
    public string Id { get; set; }
}
```

**Generates:**

```csharp
internal static class InternalRecordExtensions { ... }
internal static class CabinetStoreExtensions
{
    internal static IOfflineStore CreateCabinetStore(..., JsonSerializerContext jsonContext)
}
```

**Works with:**

```csharp
internal partial class CabinetJsonContext : JsonSerializerContext
{
    [JsonSerializable(typeof(InternalRecord))]
}
```

## Example: Mixed (Public + Internal)

```csharp
[AotRecord]
public class PublicRecord
{
    public string Id { get; set; }
}

[AotRecord]
internal class InternalRecord
{
    public string Id { get; set; }
}
```

**Generates:**

```csharp
public static class PublicRecordExtensions { ... }
internal static class InternalRecordExtensions { ... }
internal static class CabinetStoreExtensions  // ← internal because one record is internal!
{
    internal static IOfflineStore CreateCabinetStore(..., JsonSerializerContext jsonContext)
}
```

**Must use internal context:**

```csharp
internal partial class CabinetJsonContext : JsonSerializerContext
{
    [JsonSerializable(typeof(PublicRecord))]    // public types OK in internal context
    [JsonSerializable(typeof(InternalRecord))]
}
```

## Documentation Requirement

Users must ensure:

1. All `[AotRecord]` classes have **the same accessibility** as their `JsonSerializerContext`
2. If mixing public/internal records, use **internal** `JsonSerializerContext`

The C# compiler will enforce this - if there's a mismatch, you'll get CS0053 errors.
