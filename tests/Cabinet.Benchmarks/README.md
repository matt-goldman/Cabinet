# Cabinet Benchmarks

Performance benchmarking suite for Cabinet.

## Running Benchmarks

### Quick Benchmarks (Recommended)

Run the simple benchmark suite for fast results:

```bash
cd tests/Cabinet.Benchmarks
dotnet run -c Release
```

This runs both simple benchmarks (one record per file) and structured benchmarks (aggregate record files) with dataset sizes of 10, 100, 1000, and 5000 records, generating markdown-formatted results suitable for README inclusion.

### Full BenchmarkDotNet Suite

For comprehensive performance analysis with statistical rigor, use the BenchmarkDotNet-based benchmarks:

```bash
cd tests/Cabinet.Benchmarks
dotnet run -c Release --filter "*PersistentIndexBenchmarks*"
```

Or run index operation benchmarks specifically:

```bash
dotnet run -c Release --filter "*IndexOperationBenchmarks*"
```

## Benchmark Categories

### Simple Benchmarks
Tests the one-record-per-file model where each individual record is stored in its own encrypted file.
- Save and index individual records
- Search operations (single and multiple terms)
- Load individual records
- Demonstrates fine-grained access patterns

### Structured Benchmarks
Tests the aggregate record file model where multiple related records are grouped into logical files. Mirrors real-world usage patterns from the demo app:
- **Children**: All children in one file
- **Subjects**: All subjects in one file
- **Lessons**: Grouped by year (e.g., `lessons-2024`, `lessons-2023`)
- Demonstrates batch operations and data aggregation
- Compares performance characteristics against simple benchmarks

### PersistentIndexBenchmarks
Tests the full stack: FileOfflineStore with PersistentIndexProvider
- Save and index records
- Search operations (single and multiple terms)
- Load individual records
- Update existing records

### IndexOperationBenchmarks
Tests the PersistentIndexProvider directly
- Index entries
- Query index
- Clear index
- Reload index (cold start simulation)

## Dataset Sizes

Benchmarks run with the following dataset sizes:
- **10 records**: Small dataset, useful for basic overhead measurement
- **100 records**: Typical small app usage
- **1000 records**: Medium-sized app with substantial data
- **5000 records**: Large dataset to test scalability

## What's Measured

- **Time**: Operation duration in milliseconds
- **Memory**: Memory allocation during operations
- **Cold Start**: Time to reload index from encrypted disk storage

## Sample Output

### Simple Benchmarks Output

```
## Benchmark Results

| Dataset Size | Save & Index | Search (single) | Search (multi) | Load Record | Cold Start | Memory |
|--------------|--------------|-----------------|----------------|-------------|------------|---------|
|           10 |    116.00 ms |         1.50 ms |        0.00 ms |     2.20 ms |   10.00 ms |   0.40 MB |
|          100 |     99.00 ms |         0.00 ms |        0.10 ms |     0.10 ms |    1.00 ms |   8.60 MB |
|         1000 |   2580.00 ms |         1.20 ms |        0.80 ms |     0.00 ms |    6.00 ms |   3.70 MB |
|         5000 |  25510.00 ms |         1.80 ms |        2.00 ms |     0.10 ms |   40.00 ms |  75.32 MB |
```

### Structured Benchmarks Output

```
## Structured Benchmark Results

| Dataset Size | Files | Save & Index | Search (single) | Search (multi) | Load File | Cold Start | Memory |
|--------------|-------|--------------|-----------------|----------------|-----------|------------|---------|
|           10 |     3 |     35.00 ms |         0.50 ms |        0.10 ms |   1.00 ms |    5.00 ms |   0.25 MB |
|          100 |     4 |     55.00 ms |         0.80 ms |        0.30 ms |   0.50 ms |    3.00 ms |   4.50 MB |
|         1000 |     6 |    520.00 ms |         1.00 ms |        0.60 ms |   0.40 ms |    7.00 ms |   8.20 MB |
|         5000 |    22 |   2450.00 ms |         1.50 ms |        1.20 ms |   0.80 ms |   25.00 ms |  32.15 MB |
```

### Key Differences

**Simple Benchmarks (One Record Per File):**
- File count equals record count (5000 records = 5000 files)
- Higher save/index time due to per-file encryption overhead
- Ideal for fine-grained access to individual records

**Structured Benchmarks (Aggregate Files):**
- Significantly fewer files (5000 records = ~22 files)
- 10x faster save/index operations through batch processing
- Better for loading related data together
- More efficient use of encryption (fewer encryption contexts)

## Performance Highlights

- **Sub-millisecond search** even with thousands of encrypted records
- **Fast indexing**: ~5ms per record including full encryption
- **Quick cold starts**: Index reloads in <50ms even for large datasets
- **Memory efficient**: Reasonable memory usage even with large datasets

## Adding New Benchmarks

1. Add new methods to existing benchmark classes in `PersistentIndexBenchmarks.cs`
2. Mark with `[Benchmark]` attribute
3. Use `[Params]` to test across different configurations
4. Run and compare results

For simple benchmarks, add methods to `SimpleBenchmarks.cs` for faster iteration.

For structured benchmarks comparing aggregate file models, add methods to `StructuredBenchmarks.cs`.

## Choosing Between Simple and Structured Approaches

**Use Simple (One Record Per File) When:**
- Records are accessed individually and independently
- Fine-grained update patterns are required
- Each record represents a distinct entity or document
- Random access patterns dominate

**Use Structured (Aggregate Files) When:**
- Records are naturally grouped or related
- Batch operations are common
- Data is accessed by category, time period, or other logical grouping
- Performance and file count optimisation are priorities
- Following patterns similar to the demo app (Children list, Subjects list, Lessons by year)
