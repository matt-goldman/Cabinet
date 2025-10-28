# RecordSet<T> API

The `RecordSet<T>` class provides a fluent, LINQ-style API for querying records from the offline store. It wraps an `IEnumerable<T>` and provides deferred execution with chainable operations.

## Overview

`RecordSet<T>` is returned by the `FindManyAsync<T>()` and `FindWhereAsync<T>()` extension methods, allowing you to build complex queries using familiar LINQ patterns.

## Key Features

- **Fluent API**: Chain multiple operations together
- **Deferred Execution**: Operations are not executed until materialized (e.g., `.ToList()`, `.ToArray()`)
- **AOT-Safe**: Uses `Func<T, bool>` delegates instead of expression trees
- **Familiar LINQ Methods**: `Where`, `Select`, `OrderBy`, `Skip`, `Take`, etc.

## Example Usage

### Basic Query

```csharp
// Find all maths lessons
var lessons = await store
    .FindManyAsync<LessonRecord>("maths")
    .Where(l => l.Date.Year == 2025)
    .OrderBy(l => l.Date)
    .ToList();
```

### Multiple Search Terms (OR)

```csharp
// Find lessons mentioning "seagulls" OR "Dylan"
var records = await store
    .FindManyAsync<LessonRecord>("seagulls", "Dylan");

// Then filter to only Maths lessons where Dylan participated
var results = records
    .WhereMatch(r => r.Subject == "Maths")
    .WhereMatch(r => r.Children.Contains("Dylan"))
    .OrderByDescending(r => r.Date)
    .ToList();
```

### Complex Query

```csharp
// Find recent maths lessons for specific children
var recentLessons = await store
    .FindManyAsync<LessonRecord>("maths", "experiment", "volcano")
    .Where(l => l.Subject == "Maths")
    .Where(l => l.Children.Any(c => c == "Dylan" || c == "Jessica"))
    .Where(l => l.Date >= new DateOnly(2025, 10, 1))
    .OrderByDescending(l => l.Date)
    .Take(10)
    .ToList();
```

### Projection

```csharp
// Get just the subjects from recent lessons
var subjects = await store
    .FindManyAsync<LessonRecord>("science", "experiment")
    .Select(l => l.Subject)
    .Distinct()
    .ToList();
```

### Pagination

```csharp
// Get page 2 of results (items 11-20)
var page = await store
    .FindManyAsync<LessonRecord>("maths")
    .OrderBy(l => l.Date)
    .Skip(10)
    .Take(10)
    .ToList();
```

### Aggregation

```csharp
// Check if any lessons exist
var hasLessons = await store
    .FindManyAsync<LessonRecord>("dylan")
    .Any(l => l.Children.Contains("Dylan"));

// Count matching lessons
var count = await store
    .FindManyAsync<LessonRecord>("maths")
    .Count(l => l.Date.Year == 2025);

// Check if all match a condition
var allMaths = await store
    .FindManyAsync<LessonRecord>("lesson")
    .All(l => l.Subject == "Maths");
```

### Combined Search and Filter

```csharp
// FindWhereAsync combines search and filter
var results = await store
    .FindWhereAsync<LessonRecord>(
        l => l.Subject == "Maths" && l.Children.Contains("Dylan"),
        "seagulls", "volcano", "experiment")
    .OrderBy(l => l.Date)
    .ToList();
```

## Available Methods

### Filtering
- `Where(Func<T, bool> predicate)` - Filter records
- `WhereMatch(Func<T, bool> predicate)` - Alias for `Where` (for discoverability)

### Projection
- `Select<TResult>(Func<T, TResult> selector)` - Transform records

### Sorting
- `OrderBy<TKey>(Func<T, TKey> keySelector)` - Sort ascending
- `OrderByDescending<TKey>(Func<T, TKey> keySelector)` - Sort descending

### Pagination
- `Skip(int count)` - Skip records
- `Take(int count)` - Take records

### Single Result
- `First()` - First record (throws if empty)
- `FirstOrDefault()` - First record or default
- `Single()` - Single record (throws if not exactly one)
- `SingleOrDefault()` - Single record or default

### Aggregation
- `Any()` - Check if any records exist
- `Any(Func<T, bool> predicate)` - Check if any match predicate
- `All(Func<T, bool> predicate)` - Check if all match predicate
- `Count()` - Count all records
- `Count(Func<T, bool> predicate)` - Count matching records

### Materialization
- `ToList()` - Convert to list
- `ToArray()` - Convert to array
- `AsEnumerable()` - Get underlying enumerable

## Performance Considerations

1. **Index First**: Always use search terms in `FindManyAsync` to leverage the index
2. **Filter in Memory**: Use `Where` for additional filtering after index lookup
3. **Deferred Execution**: Operations are chained but not executed until materialized
4. **Multiple Terms**: Use multiple terms for OR searches (they're deduplicated automatically)

## Example: Real-World Scenario

```csharp
// Find all science lessons from October 2025 where either Dylan or Jessica participated,
// involving experiments with either volcanoes or seagulls
var lessons = await store
    .FindManyAsync<LessonRecord>("volcano", "seagulls", "experiment", "Dylan", "Jessica")
    .Where(l => l.Subject == "Science")
    .Where(l => l.Date.Year == 2025 && l.Date.Month == 10)
    .Where(l => l.Children.Contains("Dylan") || l.Children.Contains("Jessica"))
    .Where(l => l.Description.Contains("volcano") || l.Description.Contains("seagulls"))
    .OrderByDescending(l => l.Date)
    .ToList();

// Display results
foreach (var lesson in lessons)
{
    Console.WriteLine($"{lesson.Date}: {lesson.Subject} - {lesson.Description}");
    Console.WriteLine($"  Children: {string.Join(", ", lesson.Children)}");
}
```

## Design Philosophy

`RecordSet<T>` follows these principles:

1. **Two-Stage Query Pattern**: Use the index first, then filter in memory
2. **Familiarity**: Mirrors LINQ's API for easy adoption
3. **Type Safety**: Strong typing throughout the pipeline
4. **AOT Compatibility**: No expression trees or runtime code generation
5. **Simplicity**: Wraps `IEnumerable<T>` with a clean fluent API
