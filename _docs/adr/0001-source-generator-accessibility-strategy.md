# ADR 0001: Source Generator Accessibility Strategy

**Status:** Accepted

**Date:** 2024-10-30

**Context:**
The Cabinet source generator creates convenience methods (RecordSet extensions, CreateCabinetStore helper) for AOT-compatible offline data storage. Users must manually create a `JsonSerializerContext` for System.Text.Json AOT compilation.

## Decision

The source generator honors the accessibility modifiers of decorated record classes with the following strategy:

1. **Individual RecordSet extensions match their record's accessibility**
   - `public record` → `public static class {TypeName}Extensions`
   - `internal record` → `internal static class {TypeName}Extensions`

2. **CabinetStoreExtensions uses the most restrictive accessibility**
   - If ANY record is `internal` → `CabinetStoreExtensions` is `internal`
   - If ALL records are `public` → `CabinetStoreExtensions` is `public`

3. **No diagnostic for non-public types** - C# compiler enforces accessibility rules

4. **JsonSerializerContext is never generated** - users always create it manually

## Rationale

### Why Not Generate JsonSerializerContext?

We initially attempted to generate the `JsonSerializerContext`, but discovered source generators cannot reliably coordinate with System.Text.Json's generator in the same compilation pass:

```csharp
// We tried generating this:
public partial class CabinetJsonContext : JsonSerializerContext { }

// But System.Text.Json's generator couldn't see it to implement abstract members
// Error: does not implement inherited abstract member 'GetTypeInfo'
```

**Solution:** Users create the context manually in their own code, allowing System.Text.Json's generator to process it correctly.

### Why Most Restrictive Accessibility for CabinetStoreExtensions?

The `CreateCabinetStore` method accepts a `JsonSerializerContext` parameter:

```csharp
public static IOfflineStore CreateCabinetStore(
    string dataDirectory,
    byte[] masterKey,
    JsonSerializerContext jsonContext)  // ← Must match context accessibility
```

If we had mixed accessibility records, we face C# constraint CS0053: types in public members must be public.

**Options considered:**

1. ❌ **Always public** - Breaks when users need internal records with internal context
2. ❌ **Always internal** - Unnecessarily restrictive when all records are public
3. ❌ **Generate separate extension classes per accessibility** - Would require:
   - Multiple `CabinetStoreExtensions` classes (e.g., `PublicCabinetStoreExtensions`, `InternalCabinetStoreExtensions`)
   - Either no `CreateCabinetStore` helper, or multiple versions with different accessibility
   - Fragmented API surface with minimal benefit
4. ✅ **Most restrictive** - Simple, predictable, works with any combination

### Why No CABINET001 Diagnostic?

Initially we blocked non-public types with error CABINET001, but this was redundant:

```csharp
// User's code:
public partial class CabinetJsonContext : JsonSerializerContext { }
[AotRecord]
internal record MyRecord { }

// C# compiler already gives clear error:
// CS0053: Inconsistent accessibility: parameter type 'MyRecord' is less accessible than method
```

The C# compiler's error message is clearer than anything we could provide. Our diagnostic was just noise.

### Why Individual Extensions Match Record Accessibility?

This allows using the generated RecordSet helpers even when records have different accessibility:

```csharp
public static class PublicRecordExtensions    // ← public, can be used anywhere
{
    public static RecordSet<PublicRecord> CreateRecordSet(this IOfflineStore store) { }
}

internal static class InternalRecordExtensions  // ← internal, used within assembly
{
    internal static RecordSet<InternalRecord> CreateRecordSet(this IOfflineStore store) { }
}
```

## Consequences

### Positive

- ✅ **Works with any accessibility** (public, internal, private) - not just public
- ✅ **Simple mental model** - "all records and context must match accessibility"
- ✅ **C# compiler provides clear errors** - no custom diagnostics needed
- ✅ **Unified API** - Single `CabinetStoreExtensions` class, single `CreateCabinetStore` helper
- ✅ **Predictable behavior** - Most restrictive wins, easy to understand

### Negative

- ⚠️ **Mixing public + internal requires internal context** - Most restrictive accessibility applies
- ⚠️ **Users must manually create JsonSerializerContext** - Can't be automated due to generator coordination issues

### Trade-offs Accepted

We chose simplicity and clarity over maximum flexibility:

- Could generate separate extension classes per accessibility → chose unified API
- Could attempt complex JsonSerializerContext generation → chose reliable manual approach
- Could add custom diagnostics → chose to rely on C# compiler's clear errors

## Alternatives Considered

### 1. Always Require Public Types

**Rejected:** Too restrictive. Users legitimately need internal records for encapsulation.

### 2. Generate Multiple CabinetStoreExtensions Classes

```csharp
public static class PublicCabinetStoreExtensions
{
    public static IOfflineStore CreateCabinetStore(..., PublicJsonContext ctx) { }
}

internal static class InternalCabinetStoreExtensions
{
    internal static IOfflineStore CreateCabinetStore(..., InternalJsonContext ctx) { }
}
```

**Rejected:**

- Fragments API (which extension class do I use?)
- Loses naming convention (can't just call `CreateCabinetStore`)
- Adds complexity for minimal benefit (JsonSerializerContext is identical in both approaches)

### 3. Remove CreateCabinetStore Helper Entirely

Just generate individual RecordSet extensions, let users wire up store themselves.

**Rejected:** The helper significantly reduces boilerplate and is a key convenience feature.

## References

- C# Accessibility Levels: <https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/access-modifiers>
- Source Generator Coordination Issues: <https://github.com/dotnet/roslyn/discussions/47517>
- System.Text.Json Source Generation: <https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation>

## Related Files

- `src/Cabinet.SourceGenerators/AotRecordGenerator.cs` - Implementation
- `_docs/source-generator-usage.md` - User documentation
- `_docs/aot-manual-setup.md` - Manual setup guide
- `tests/Cabinet.SourceGenerators.Tests/AotRecordGeneratorTests.cs` - Test coverage
