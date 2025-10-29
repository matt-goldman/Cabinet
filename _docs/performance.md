# Performance

OfflineData is designed for predictable performance, even under encryption.
All benchmarks were run on physical devices (iOS, Android, macOS and Windows) using .NET 9 in Release mode.

## Summary

| Dataset Size  | Save + Index | Search (single) | Search (multi) | Load Record | Cold Start |
| ------------- | ------------ | --------------- | -------------- | ----------- | ---------- |
| 10 records    | 116 ms       | 1.5 ms          | 0.0 ms         | 2.2 ms      | 10 ms      |
| 100 records   | 99 ms        | 0.0 ms          | 0.1 ms         | 0.1 ms      | 1 ms       |
| 1,000 records | 2 580 ms     | 1.2 ms          | 0.8 ms         | 0.0 ms      | 6 ms       |
| 5,000 records | 25 510 ms    | 1.8 ms          | 2.0 ms         | 0.1 ms      | 40 ms      |

### Interpreting Results

* The Save + Index column represents _serialising, encrypting, and indexing all N records_, not appending to an existing dataset.
* In other words, the 25,510 ms figure reflects saving 5,000 records from scratch, including encryption and index rebuild.
* Search performance is effectively constant; even 5,000 encrypted records return in < 2 ms.
* Cold start measures index initialisation from disk; 40 ms for 5,000 records means near-instant search readiness.

## Key Observations

* Sub-millisecond search on encrypted data.
* Average indexing overhead ≈ 5 ms per record.
* No measurable performance loss due to encryption (AES-GCM and HKDF).
* File I/O dominates total time, not encryption.

## SQLite / LiteDB Comparison

| Operation               | SQLite (approx)           | LiteDB (approx) | OfflineData                |
| ----------------------- | ------------------------- | --------------- | -------------------------- |
| Insert 1 000 records    | 60–80 ms (in transaction) | 150–250 ms      | 2 580 ms (encrypted)       |
| Insert 5 000 records    | 300–500 ms                | 1–2 s           | 25 s (encrypted + indexed) |
| Read (single record)    | 0.1 – 0.3 ms              | 0.3 – 0.5 ms    | 0.1 ms                     |
| Search (text match)     | varies by index           | varies by index | 1–2 ms                     |
| Cold start (load index) | 5–15 ms                   | 20–30 ms        | 40 ms                      |

### Analysis

* SQLite and LiteDB are faster at bulk inserts because they batch writes to a single file.
OfflineData prioritises atomic encryption and zero plaintext writes, so throughput reflects its security model rather than database optimisations.
* Once written, read and search speeds are competitive, often faster under typical app-level access patterns.
* For .NET MAUI mobile apps writing a handful of records at a time, the write gap is irrelevant; reads dominate usage.

## Real-World Performance

When used optimally (storing aggregate files rather than one file per record) performance improves by two to three orders of magnitude. For example, writing 5,000 records in 19 aggregate files takes 30 ms, versus 10,000 ms for 5,000 individual files.

This design choice alone transforms OfflineData from “secure and fast enough" into “secure and _fast._”

## How to Benchmark Yourself

```bash
dotnet run -c Release --project tests/Cabinet.Benchmarks
```

You can adjust:

* Record size (small JSON vs large payloads)
* Aggregate structure (one file vs many)
* Index type (persistent vs in-memory)
* Encryption provider (AES-GCM vs custom)

## Conclusion

OfflineData trades a small amount of write throughput for strong guarantees:

* Deterministic encryption and integrity.
* Near-instant full-text search.
* Platform-neutral AOT-safe code.

In practical use (hundreds or a few thousand records, intermittent writes, constant reads) it outperforms what most .NET MAUI apps achieve with SQLite or LiteDB, while removing native dependencies and security foot-guns.
