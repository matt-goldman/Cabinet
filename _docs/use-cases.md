# Use Cases

`Cabinet` is designed for scenarios where you need secure, persistent, searchable local data — but don’t need (or want) a full database engine.

It’s ideal when your app:

* Must work entirely offline
* Must encrypt all data at rest
* Needs fast lookups or text search
* Should avoid native or platform-specific dependencies

Uses domain models directly rather than database entities

## 1. Secure Offline Data for Mobile Apps

Most .NET MAUI apps don’t need SQLite or Realm. They need something simpler: a place to store structured objects securely.

Examples:

* Education or journaling apps storing notes, lessons, or reflections offline.
* Field service apps that record inspections or maintenance logs with attachments.
* Healthcare or client apps capturing sensitive data offline for later sync.

Cabinet provides this with:

* AES-256-GCM encryption per file
* HKDF-derived per-file keys
* Zero plaintext on disk
* Persistent full-text index

Your users’ data never leaves the device unencrypted.

## 2. Encrypted Personal Data Stores

For apps that need to persist personal information locally (e.g., task lists, personal logs, or password-like data), Cabinet functions as an encrypted vault.

You can:

* Store objects directly using `SaveAsync<T>()`
* Retrieve with `LoadAsync<T>()`
* Search via `FindAsync(query)`

All data remains encrypted on disk, even when the device is compromised.

## 3. Structured Offline Content

Apps that serve structured but immutable data (like e-books, documentation, or reference material) can pre-package datasets as `.dat` files.

Cabinet can index these for search while keeping them encrypted.

Example:

* A training app that ships encrypted learning modules.
* A recipe app that allows local search across an encrypted catalog.

## 4. Hybrid Offline/Online Scenarios

Cabinet works well in apps that sync data periodically but need local performance and privacy between syncs.

It can complement cloud sync or Graph API backends by:

* Acting as a write-through cache
* Indexing recent data for instant search

Allowing offline editing with encrypted persistence

## 5. Lightweight Domain Persistence

When your app’s domain models are small and self-contained (and relationships are implicit rather than relational) you don’t need a database schema.

Cabinet is purpose-built for this style:

* Save aggregates (e.g. a `LessonRecord` or `InspectionLog`)
* Retrieve and filter using the encrypted index
* Extend indexing or metadata with custom providers

It’s effectively an encrypted, persistent, domain object store.

## 6. Embedded Plugins or Framework Extensions

If you’re building SDKs or plugins (for example, white-label apps or enterprise extensions), Cabinet is ideal:

* 100% managed code
* AOT-safe and dependency-free
* No need for native SQLite bindings or Realm runtime
* Simple configuration through DI or factory methods

It fits anywhere you’d otherwise use `SecureStorage` plus JSON files, but with structure, indexing, and encryption built in.

## When Not to Use It

Cabinet is not a relational database and doesn’t aim to be.

Avoid it if you need:

* Cross-table joins or SQL-like querying
* Multi-GB datasets or binary blobs >100 MB
* Continuous background sync

For those cases, a dedicated embedded database like SQLite remains a better fit.

## Summary Table

| Scenario                      | Recommended | Reason                          |
| ----------------------------- | ----------- | ------------------------------- |
| Encrypted offline persistence | ✅          | Full encryption, atomic writes  |
| Local search and filtering    | ✅          | Encrypted inverted index        |
| Offline sync caching          | ✅          | Easy integration with APIs      |
| Multi-entity relationships    | ⚠️        | Possible but not natural        |
| Heavy analytics or joins      | ❌          | Use SQLite or server-side query |
| Multi-GB datasets             | ❌          | Not optimised for bulk data     |
