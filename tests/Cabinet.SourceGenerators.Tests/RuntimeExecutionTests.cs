using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace Cabinet.SourceGenerators.Tests;

/// <summary>
/// Tests that actually compile the generated code into an assembly and try to execute it.
/// This will definitively show whether the generated code actually works at runtime.
/// </summary>
public class RuntimeExecutionTests : IDisposable
{
	private readonly ITestOutputHelper _output;
	private readonly string _testDirectory;

	public RuntimeExecutionTests(ITestOutputHelper output)
	{
		_output = output;
		_testDirectory = Path.Combine(Path.GetTempPath(), $"cabinet_runtime_tests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testDirectory))
		{
			Directory.Delete(_testDirectory, true);
		}
	}

	private const string TestRecordSource = @"
using System;

namespace Cabinet
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class AotRecordAttribute : Attribute
	{
		public string? IdPropertyName { get; set; }
		public string? FileName { get; set; }
	}
}

namespace TestNamespace
{
	[Cabinet.AotRecord]
	public class TestRecord
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
	}
}";

	[Fact]
	public void GeneratedCode_ShouldCompileToAssembly()
	{
		// Arrange & Act
		var result = CompileToAssembly(TestRecordSource);

		// Assert
		_output.WriteLine($"Compilation success: {result.Success}");
		_output.WriteLine($"Diagnostics count: {result.Diagnostics.Length}");
		
		foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
		{
			_output.WriteLine($"ERROR: {diagnostic.GetMessage()}");
		}

		Assert.True(result.Success);
		Assert.NotNull(result.Assembly);
	}

	[Fact]
	public void GeneratedStoreExtension_CanBeInvokedAtRuntime()
	{
		// Arrange
		var result = CompileToAssembly(TestRecordSource);
		Assert.True(result.Success);
		Assert.NotNull(result.Assembly);

		// Act - Try to find and invoke the CreateCabinetStore method
		var extensionsType = result.Assembly.GetType("Cabinet.Generated.CabinetStoreExtensions");
		Assert.NotNull(extensionsType);

		var createStoreMethod = extensionsType.GetMethod("CreateCabinetStore", 
			BindingFlags.Public | BindingFlags.Static);
		Assert.NotNull(createStoreMethod);

		_output.WriteLine($"Found method: {createStoreMethod.Name}");
		_output.WriteLine($"Parameters: {createStoreMethod.GetParameters().Length}");
		foreach (var param in createStoreMethod.GetParameters())
		{
			_output.WriteLine($"  {param.Name}: {param.ParameterType.FullName}");
		}

		// Try to invoke it
		var testKey = new byte[32];
		Random.Shared.NextBytes(testKey);

		try
		{
			var store = createStoreMethod.Invoke(null, new object[] { _testDirectory, testKey });
			
			_output.WriteLine($"✓ CreateCabinetStore invoked successfully!");
			_output.WriteLine($"Returned type: {store?.GetType().FullName ?? "null"}");
			
			Assert.NotNull(store);

			// Check if it's actually a FileOfflineStore
			var storeType = store.GetType();
			_output.WriteLine($"Store type: {storeType.FullName}");
			
			// Try to inspect the store's fields to see what was actually set
			var fields = storeType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
			_output.WriteLine($"\nStore instance fields:");
			foreach (var field in fields)
			{
				var value = field.GetValue(store);
				_output.WriteLine($"  {field.Name} ({field.FieldType.Name}): {value?.GetType().Name ?? "null"}");
			}
		}
		catch (Exception ex)
		{
			_output.WriteLine($"✗ Exception invoking CreateCabinetStore:");
			_output.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
			if (ex.InnerException != null)
			{
				_output.WriteLine($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
				_output.WriteLine($"  Stack: {ex.InnerException.StackTrace}");
			}
			throw;
		}
	}

	[Fact]
	public void GeneratedStoreAndRecordSet_CanSaveAndLoadRecords()
	{
		// Arrange
		var result = CompileToAssembly(TestRecordSource);
		Assert.True(result.Success);
		Assert.NotNull(result.Assembly);

		// Get the extension types
		var storeExtensionsType = result.Assembly.GetType("Cabinet.Generated.CabinetStoreExtensions");
		var recordExtensionsType = result.Assembly.GetType("Cabinet.Generated.TestRecordExtensions");
		var testRecordType = result.Assembly.GetType("TestNamespace.TestRecord");

		Assert.NotNull(storeExtensionsType);
		Assert.NotNull(recordExtensionsType);
		Assert.NotNull(testRecordType);

		// Create a store using the generated extension
		var createStoreMethod = storeExtensionsType.GetMethod("CreateCabinetStore", 
			BindingFlags.Public | BindingFlags.Static);
		Assert.NotNull(createStoreMethod);

		var testKey = new byte[32];
		Random.Shared.NextBytes(testKey);

		var store = createStoreMethod.Invoke(null, new object[] { _testDirectory, testKey });
		Assert.NotNull(store);

		_output.WriteLine($"✓ Created store: {store.GetType().FullName}");

		// Create a record set using the generated extension
		var createRecordSetMethod = recordExtensionsType.GetMethod("CreateRecordSet",
			BindingFlags.Public | BindingFlags.Static);
		Assert.NotNull(createRecordSetMethod);

		var recordSet = createRecordSetMethod.Invoke(null, new object[] { store });
		Assert.NotNull(recordSet);

		_output.WriteLine($"✓ Created record set: {recordSet.GetType().FullName}");

		// Create a test record
		var testRecord = Activator.CreateInstance(testRecordType);
		Assert.NotNull(testRecord);

		// Set properties using reflection
		testRecordType.GetProperty("Id")!.SetValue(testRecord, "test-123");
		testRecordType.GetProperty("Name")!.SetValue(testRecord, "Test Record");
		testRecordType.GetProperty("Description")!.SetValue(testRecord, "This is a test");

		_output.WriteLine($"✓ Created test record with Id: test-123");

		// Try to add the record to the record set
		try
		{
			var recordSetType = recordSet.GetType();
			var addMethod = recordSetType.GetMethod("AddAsync");
			Assert.NotNull(addMethod);

			var addTask = addMethod.Invoke(recordSet, new object[] { testRecord });
			var taskType = addTask!.GetType();
			var getAwaiterMethod = taskType.GetMethod("GetAwaiter");
			var awaiter = getAwaiterMethod!.Invoke(addTask, null);
			var getResultMethod = awaiter!.GetType().GetMethod("GetResult");
			getResultMethod!.Invoke(awaiter, null);

			_output.WriteLine($"✓ Successfully added record to record set");

			// Try to get the record back
			var getMethod = recordSetType.GetMethod("GetAsync");
			Assert.NotNull(getMethod);

			var getTask = getMethod.Invoke(recordSet, new object[] { "test-123" });
			var getAwaiter = getAwaiterMethod!.Invoke(getTask, null);
			var retrievedRecord = getResultMethod!.Invoke(getAwaiter, null);

			Assert.NotNull(retrievedRecord);
			var retrievedName = testRecordType.GetProperty("Name")!.GetValue(retrievedRecord);
			
			_output.WriteLine($"✓ Successfully retrieved record with Name: {retrievedName}");
			Assert.Equal("Test Record", retrievedName);

			// Verify file was created
			var recordFile = Path.Combine(_testDirectory, "records", "test-123.dat");
			Assert.True(File.Exists(recordFile), "Record file should exist on disk");
			_output.WriteLine($"✓ Record file exists: {recordFile}");
		}
		catch (Exception ex)
		{
			_output.WriteLine($"✗ Exception during record set operations:");
			_output.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
			var inner = ex.InnerException;
			while (inner != null)
			{
				_output.WriteLine($"  Inner: {inner.GetType().Name}: {inner.Message}");
				_output.WriteLine($"    {inner.StackTrace}");
				inner = inner.InnerException;
			}
			throw;
		}
	}

	private CompilationResult CompileToAssembly(string source)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source);

		var references = new List<MetadataReference>();
		
		// Add required assembly references
		var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (var asm in loadedAssemblies)
		{
			if (!asm.IsDynamic && !string.IsNullOrWhiteSpace(asm.Location))
			{
				references.Add(MetadataReference.CreateFromFile(asm.Location));
			}
		}

		var compilation = CSharpCompilation.Create(
			"TestCompilation",
			new[] { syntaxTree },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		// Run source generator
		var generator = new AotRecordGenerator();
		var driver = CSharpGeneratorDriver.Create(generator);
		driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
			compilation, 
			out var outputCompilation, 
			out var diagnostics);

		// Output generated code for debugging
		_output.WriteLine("=== GENERATED FILES ===");
		var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
		foreach (var tree in generatedTrees)
		{
			_output.WriteLine($"\n--- File: {tree.FilePath} ---");
			var content = tree.ToString();
			if (content.Length > 1000)
			{
				_output.WriteLine(content.Substring(0, 1000) + "\n... (truncated)");
			}
			else
			{
				_output.WriteLine(content);
			}
		}

		// Emit to memory
		using var ms = new MemoryStream();
		var emitResult = outputCompilation.Emit(ms);

		if (!emitResult.Success)
		{
			_output.WriteLine("\n=== COMPILATION ERRORS ===");
			foreach (var diagnostic in emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
			{
				_output.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
				_output.WriteLine($"  Location: {diagnostic.Location}");
			}
		}
		else
		{
			_output.WriteLine("\n✓ Compilation succeeded");
		}

		Assembly? assembly = null;
		if (emitResult.Success)
		{
			ms.Seek(0, SeekOrigin.Begin);
			assembly = Assembly.Load(ms.ToArray());
			_output.WriteLine($"✓ Assembly loaded: {assembly.FullName}");
		}

		return new CompilationResult(emitResult.Success, emitResult.Diagnostics, assembly);
	}

	private record CompilationResult(bool Success, ImmutableArray<Diagnostic> Diagnostics, Assembly? Assembly);
}
