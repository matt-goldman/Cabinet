# Migrating from Cabinet 1.x to 2.0

Cabinet 2.0 makes attachments work. In 1.x they could be written but never read back, and putting a `FileAttachment` on a record model threw during serialisation. Fixing that meant changing the attachment API, the storage layout, and the `IOfflineStore` contract.

**Records, search indexes and everything unrelated to attachments are unaffected.** Data written by
1.x continues to load in 2.0 without migration.

## At a glance

| Change | Affects you if… |
| --- | --- |
| Target framework is now `net10.0` | You are on .NET 9 or earlier |
| `IOfflineStore` has five new members | You implement `IOfflineStore` yourself |
| `FileAttachment` is a class, not a record | You use `with`, deconstruction, or value equality on it |
| `FileAttachment` is invalid on a record model | Your models have a `FileAttachment` property |
| Attachment names are validated | You use names containing `/`, `\`, or control characters |
| Attachment storage layout changed | You read attachment files off disk directly |
| `RecordSet.RemoveAsync` deletes attachments | You relied on attachments outliving their record |

## 1. Target framework

Cabinet 2.0 targets `net10.0`. Upgrade your app's target framework and SDK before taking the
package. There is no 1.x-compatible `net9.0` build of 2.0.

## 2. Models: `FileAttachment` becomes `AttachmentInfo`

This is the change most consumers will hit. In 1.x the documentation suggested a `FileAttachment`
property was serialised with the record; it never was. `FileAttachment` wraps a live `Stream`, so
System.Text.Json walked the stream's own properties and failed — with
`InvalidOperationException: Timeouts are not supported on this stream` under reflection, or an
exception from `Stream.Length` on a non-seekable stream under source generation.

Record models now carry `AttachmentInfo`: the name, content type and length. The bytes live in the
attachment store.

**Before (1.x — never actually worked):**

```csharp
public class LessonRecord
{
    public Guid Id { get; set; }
    public List<FileAttachment>? Attachments { get; set; }
}

lesson.Attachments = [new FileAttachment("photo.jpg", "image/jpeg", photoStream)];
await lessons.AddAsync(lesson);   // throws during serialisation
```

**After (2.0):**

```csharp
public class LessonRecord
{
    public Guid Id { get; set; }
    public List<AttachmentInfo>? Attachments { get; set; }
}

await lessons.AddAsync(lesson);

await using var photoStream = File.OpenRead("photo.jpg");
lesson.Attachments = [await lessons.AddAttachmentAsync(
    lesson.Id.ToString(),
    new FileAttachment("photo.jpg", "image/jpeg", photoStream))];

await lessons.UpdateAsync(lesson.Id.ToString(), lesson);
```

Reading the content back is now an explicit call, so a record load never pays for attachment bytes
it does not need:

```csharp
await using var content = await lessons.OpenAttachmentAsync(lesson.Id.ToString(), "photo.jpg");
```

`FileAttachment` remains the type you pass *in* when writing. It is a write-side handle only, and
serialising one now throws `NotSupportedException` with guidance rather than failing obscurely deep
inside the serialiser. If you use `[AotRecord]`, the source generator reports **CAB002** at build
time for any model with a `FileAttachment` or `Stream` property, so you will find these at compile
time rather than at runtime.

## 3. `RecordSet<T>` attachment API

```csharp
Task<AttachmentInfo>                AddAttachmentAsync(string recordId, FileAttachment attachment);
Task<Stream?>                       OpenAttachmentAsync(string recordId, string name);
Task<IReadOnlyList<AttachmentInfo>> ListAttachmentsAsync(string recordId);
Task<bool>                          RemoveAttachmentAsync(string recordId, string name);
Task<int>                           CompactAttachmentsAsync();
```

Attachments are keyed on the record's own ID and namespaced by the set, because a `RecordSet` stores
every record in one document keyed on the type name — record IDs are only unique within a set.

`RemoveAsync` now deletes a record's attachments along with the record. It saves the set first and
deletes the attachments after, so an interruption leaves orphaned bytes rather than a record
referencing attachments that are gone. `CompactAttachmentsAsync` reclaims those orphans; it opens
every attachment directory in the store, so call it on a maintenance path rather than on load.

## 4. `IOfflineStore` implementers

If you implement `IOfflineStore` yourself, you must add five members:

```csharp
Task<AttachmentInfo>                SaveAttachmentAsync(string id, FileAttachment attachment, CancellationToken ct = default);
Task<Stream?>                       OpenAttachmentAsync(string id, string name, CancellationToken ct = default);
Task<IReadOnlyList<AttachmentInfo>> ListAttachmentsAsync(string id, CancellationToken ct = default);
Task<bool>                          DeleteAttachmentAsync(string id, string name, CancellationToken ct = default);
Task<IReadOnlyList<string>>         ListAttachmentRecordIdsAsync(CancellationToken ct = default);
```

If your store has no concept of attachments, throwing `NotSupportedException` from all five is a
legitimate implementation — but note that `RecordSet.RemoveAsync` and `CompactAttachmentsAsync` will
then throw too.

`SaveAsync` keeps its `attachments` parameter, with semantics now stated explicitly:

- `null` — leave any existing attachments untouched
- a collection — replace the record's attachment set, so an **empty collection removes all of them**

## 5. `FileAttachment` is a class

It was a `record` wrapping a `Stream`, which gave it value equality over a stream *reference*: two
attachments over the same stream compared equal, and `with` produced copies sharing one stream
position. Neither was meaningful, so it is now a `sealed class`. If you used positional
deconstruction, `with` expressions, or equality, replace those with direct construction.

Names are also validated now. A name must be non-empty and must not contain path separators or
control characters, and must be unique within a record; violations throw `ArgumentException` from
the constructor. In 1.x the logical name was concatenated straight into a file path, so a name
containing `../` escaped the attachments directory.

## 6. Storage layout

1.x wrote attachments flat, as `attachments/{recordId}-{logicalName}.bin`. 2.0 gives each record its
own directory, keyed by a hash of the record ID:

```
attachments/
 └── {hash(recordId)}/
      ├── manifest.dat        # Encrypted attachment metadata
      ├── owner.dat           # Encrypted record id, so owners can be enumerated
      └── {hash(name)}.bin    # Encrypted content
```

Hashing the names is what makes an arbitrary logical name unable to escape the directory. It also
fixes a bug in 1.x's delete: the flat glob `{id}-*.bin` meant deleting record `rec-1` also deleted
the attachments of `rec-12`.

Attachment blobs are now authenticated against both the record ID and the attachment name, so a blob
cannot be substituted for another attachment, or for the same attachment on a different record.

**No migration path is provided for 1.x attachment files**, and none is needed in practice: 1.x had
no way to read an attachment back through the library, so no application can have depended on that
data. If you wrote attachments in 1.x and read the files yourself, extract what you need before
upgrading, then write them back through `SaveAttachmentAsync`. Old `.bin` files are simply ignored
by 2.0; delete the `attachments/` directory to reclaim the space.

## 7. Commit ordering

A save that includes attachments now stages every file first, then renames the attachment blobs, the
manifest, and the record — in that order. The record rename is the commit point, so a record never
becomes visible referencing attachments that are not on disk. In 1.x the record was written *first*,
so a failure partway through the attachments left a visible record pointing at attachments that did
not exist.

This is crash consistency, not durability: staged writes are not flushed to the storage medium
before being renamed, so a power loss can still leave a renamed file with incomplete contents.
Cabinet is not a database and does not offer a durability guarantee.
