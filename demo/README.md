# OfflineData Demo Application

This demo application showcases the capabilities of Cabinet with real-world usage examples.

## Features Demonstrated

### Data Generation
- Random `LessonRecord` generation with realistic vocabulary
- Configurable record count (default: 10)
- Optional random binary attachments (512 bytes each)
- Performance metrics (time taken, record count)
- **Data persists to encrypted files on disk**

### Full-Text Search
- Search across all saved records
- Performance metrics (time taken, result count)
- Result ranking by relevance score
- **Display of detailed metadata** including:
  - Subject and date
  - Description
  - Children associated with the lesson
  - Relevance score
- **Persistent encrypted search index** that survives app restarts

### Data Management
- **Purge Data**: Delete all stored records, attachments, and index data
- Allows clean restart for testing and demonstrations

### Security
- AES-256-GCM encryption for all data at rest
- Per-record encryption with HKDF key derivation
- Encrypted attachments
- **Encrypted search index** stored on disk
- Master key persisted in platform SecureStorage
- No plaintext stored on disk

### UI Features
- Busy indicator during operations
- Real-time results display
- Error handling with user-friendly messages
- Disabled buttons during operations to prevent concurrent access

## Data Structure

The demo uses a `LessonRecord` model that represents a homeschooling lesson entry:

```csharp
public class LessonRecord
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Subject { get; set; }
    public string Description { get; set; }
    public List<string> Children { get; set; }
    public List<string> Tags { get; set; }
    public List<FileAttachment>? Attachments { get; set; }
}
```

## Sample Vocabulary

### Subjects
- Maths, Science, English, Art, Geography, Music

### Activities
- counted seagulls
- built a volcano
- painted a landscape
- read a story
- played piano
- made a map

### Children
- Alice, Ben, Chloe, Dylan

## Usage

1. **Generate Records**
   - Enter the number of records to generate (e.g., 10)
   - Optionally check "Add random attachments"
   - Click "Generate Records"
   - Results show count and time taken
   - **Data is encrypted and persisted to disk**

2. **Search Records**
   - Enter a search term (e.g., "volcano", "piano", "Alice")
   - Click "Search Records"
   - Results show matching records with:
     - Subject and date
     - Full description
     - Associated children
     - Relevance score
   - **Search index persists across app restarts**

3. **Purge Data**
   - Click "Purge Data" to delete all stored data
   - Removes all records, attachments, and search index
   - Useful for clean testing or resetting the demo

## Search Examples

Try searching for:
- `volcano` - finds records with volcano activity
- `piano` - finds music-related records
- `seagulls` - finds records from beach activities
- `Alice` - finds all records for child Alice
- `science` - finds all science lessons

## Index Provider

The demo uses `PersistentIndexProvider` from the core plugin (`Cabinet.Index`) which provides:
- **Persistent encrypted search index** stored on disk
- **Survives app restarts** - no data loss
- Full-text search with TF-IDF inspired scoring
- Metadata preservation for enhanced search results
- Atomic writes to prevent corruption

**Storage location:** `/OfflineData/index/search-index.dat` (encrypted)

### Alternative Implementations

You can replace the index provider with:
- **SimpleInMemoryIndexProvider** (demo implementation) - for testing without persistence
- **Custom implementation** - implement `IIndexProvider` for specialized needs (e.g., Lucene.NET, ElasticSearch integration)

## Performance Notes

- Record generation includes full encryption overhead
- Search performance depends on index size and query complexity
- Attachments add to storage and encryption time
- All operations are asynchronous and don't block the UI
- **Index is loaded once on startup** and cached in memory for fast queries
- **Index updates are persisted immediately** to disk for durability

## Future Enhancements

- Record viewing and editing UI
- Attachment preview capabilities
- Export/import functionality
- Advanced search with date range and tag filters
- Batch operations for large datasets
