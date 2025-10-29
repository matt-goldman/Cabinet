using Cabinet.Benchmarks;

Console.WriteLine("=======================================================================");
Console.WriteLine("Running Simple Benchmarks (one record per file)");
Console.WriteLine("=======================================================================");
Console.WriteLine();

await SimpleBenchmarks.Run();

Console.WriteLine();
Console.WriteLine();
Console.WriteLine("=======================================================================");
Console.WriteLine("Running Structured Benchmarks (aggregate record files)");
Console.WriteLine("=======================================================================");
Console.WriteLine();

await StructuredBenchmarks.Run();

Console.WriteLine();
Console.WriteLine();
Console.WriteLine("=======================================================================");
Console.WriteLine("Comparison Summary");
Console.WriteLine("=======================================================================");
Console.WriteLine();
Console.WriteLine("Simple Benchmarks: One record per file");
Console.WriteLine("  - Suitable for: Individual entities, documents, or records");
Console.WriteLine("  - File count: Equal to record count");
Console.WriteLine("  - Best for: Fine-grained access patterns");
Console.WriteLine();
Console.WriteLine("Structured Benchmarks: Multiple records per file");
Console.WriteLine("  - Suitable for: Related data, collections, logical groupings");
Console.WriteLine("  - File count: Much fewer than record count");
Console.WriteLine("  - Best for: Batch operations, data aggregation");
Console.WriteLine();
