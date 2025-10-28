# Data Organization

This library isn’t a relational database.
It’s an indexed, encrypted object store.
How you organise your files determines how fast and maintainable your app will be.

## How to Think About It

Think of it as _persistent storage for your domain models_, not a table for your entities.

If it helps, imagine each `.dat` file as a “table”, but one that stores objects, not rows.

![Doctor Evil meme captioned with "table"](drevil.png)

Don’t design around schemas, design around behaviour and access patterns (see the Beer Driven Devs episode [Data Driven Domains: We're all wrong about SQL](https://www.beerdriven.dev/episodes/56/)).

## Recommended Patterns

### 1. Aggregate Files (Preferred)

Store related records together in logical batches.

Example structure for an educational app:

```tree
/records/
 ├── Children.json
 ├── Subjects.json
 ├── Lessons-2025.json
 └── Lessons-2024.json
```

Each aggregate file can contain hundreds or thousands of records.
Each record inside is still individually indexed.

#### Why it matters:

Aggregate storage reduces I/O and improves indexing performance by up to 300× compared to one-file-per-record.

### 2. Attachments

Store binary attachments separately but keep their metadata in the main record.

```tree
/attachments/
 ├── {lessonId}-photo1.bin
 ├── {lessonId}-photo2.bin
```

All attachments are encrypted with the same provider.
When attachments are large, consider a lazy-load strategy to avoid unnecessary decryption.

### 3. Summaries

Keep summary files for high-level views or dashboards.
For example, annual or monthly summaries that list available records and metadata.

```tree
/summary/
 ├── 2025.sum
 ├── 2024.sum
```

This allows you to populate a calendar or activity feed without decrypting full records.

## Benchmarks Summary

Structured (aggregate) organisation is the fastest and most efficient.

| Dataset Size | Files | Save & Index | Search (single) | Search (multi) | Cold Start |
| ------------ | ----- | ------------ | --------------- | -------------- | ---------- |
| 10           | 4     | 14 ms        | 0.0 ms          | 0.0 ms         | 0 ms       |
| 100          | 5     | 2 ms         | 0.0 ms          | 0.0 ms         | 0 ms       |
| 1,000        | 6     | 4 ms         | 0.0 ms          | 0.0 ms         | 0 ms       |
| 5,000        | 19    | 30 ms        | 0.1 ms          | 0.2 ms         | 4 ms       |

In contrast, writing 5 000 individual files takes around 10 000 ms.
The difference comes purely from file count, not encryption overhead.

## Common Mistakes

| Mistake                       | Consequence                            |
| ----------------------------- | -------------------------------------- |
| One file per record           | Massive I/O overhead, poor scalability |
| Mixing unrelated entities     | Unpredictable read patterns            |
| Storing plaintext temp data   | Violates atomic write guarantees       |
| Re-serialising index manually | Breaks consistency                     |

## Quick Heuristics

* Write rarely, read often: Prefer batch updates to frequent small writes.
* Keep files under ~10 MB: Beyond that, split logically (e.g., per year).
* Encrypt everything: There’s no “opt-out,” and that’s intentional.
* Model the domain, not the schema: Your aggregates are your “tables.”

## Performance Notes

* Aggregate files are faster for both read and write.
* Attachments have negligible impact when stored separately.
* Searches remain sub-millisecond regardless of organisation.
* Cold start is dominated by index load, typically under 20 ms for 5 000+ records.

## Closing Thought

You don’t need a database to have fast, reliable offline persistence.
You need a good data model and a storage layer that makes the secure path the easy path.
That’s what `Plugin.Maui.OfflineData` is designed for.